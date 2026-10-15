using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Systems
{
    // Power-ups del cielo (9, 15.6, T5-04): temporizadores, elección de Asteroide y expiración.
    //
    // - El primer drop cae a FirstDrop del inicio; los siguientes, a un intervalo al azar entre
    //   MinInterval y MaxInterval contado desde que el anterior se recoge o se apaga (9.3).
    // - Cae sobre un Asteroide neutral al azar, con el tipo sorteado por DropWeight (9.2). Solo hay
    //   uno marcado a la vez.
    // - Si nadie lo enciende en PowerUpExpiry (30 s), se apaga (17, caso 15).
    // - Sin Asteroides neutrales, el drop se salta, el temporizador vuelve a empezar con un intervalo
    //   nuevo y no cuenta contra el máximo (17, caso 16). Uno que se apaga sí cuenta.
    // - Quien enciende el Asteroide marcado se lleva el efecto en toda su facción (FactionBuffs).
    //
    // Determinismo (12.7): todo el azar sale de un System.Random con la semilla del Inspector, y los
    // relojes van en microsegundos enteros. Va a -80 (15.10), antes que los componentes de nodo.
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class PowerUpSpawner : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        [SerializeField] BalanceConfig balance;

        [Tooltip("Un PowerUpDefinition por tipo (9.2).")]
        [SerializeField] PowerUpDefinition[] powerUps = Array.Empty<PowerUpDefinition>();

        [SerializeField] FactionBuffs buffs;

        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance.")]
        [SerializeField] NodeRegistry nodes;

        [Tooltip("Opcional: empieza el calendario de cada nivel que cargue. Sin él, lo empieza Begin (GameManager, T5-05).")]
        [SerializeField] LevelLoader loader;

        [Tooltip("Semilla del azar de los drops: Asteroide, tipo e intervalo.")]
        [SerializeField] int seed;

        readonly List<NodeController> candidates = new List<NodeController>();
        System.Random random;
        DropSchedule schedule;
        bool running;
        long timerMicros;
        int dropped;
        NodeIgnition marked;
        PowerUpDefinition markedPowerUp;
        bool subscribedToLoader;

        // El Asteroide marcado y su power-up, o null.
        public NodeController Marked => marked != null ? marked.GetComponent<NodeController>() : null;
        public PowerUpDefinition MarkedPowerUp => marked != null ? markedPowerUp : null;

        // Drops que han caído en este nivel, sin contar los saltados.
        public int Dropped => dropped;

        // Quedan drops por caer o hay uno marcado.
        public bool IsRunning => running;

        // Segundos hasta el próximo drop o, con uno marcado, hasta que se apague.
        public float TimeRemaining => (float)(timerMicros / MicrosPerSecond);

        // (Asteroide, power-up) al caer.
        public event Action<NodeController, PowerUpDefinition> OnDropped;

        // (Asteroide, power-up) al apagarse sin que nadie lo encienda.
        public event Action<NodeController, PowerUpDefinition> OnExpired;

        // (Asteroide ya encendido, facción, power-up) al recogerse.
        public event Action<NodeController, Faction, PowerUpDefinition> OnCollected;

        // Un drop que no encontró Asteroide neutral.
        public event Action OnSkipped;

        void OnEnable()
        {
            if (loader != null && !subscribedToLoader)
            {
                loader.OnLoaded += HandleLoaded;
                subscribedToLoader = true;
            }
        }

        // LevelLoader carga en su Awake, a -90, antes de que este se suscriba.
        void Start()
        {
            if (loader != null && loader.Current != null && !running && dropped == 0)
                Begin(loader.Current.Drops);
        }

        void OnDisable()
        {
            if (subscribedToLoader)
            {
                loader.OnLoaded -= HandleLoaded;
                subscribedToLoader = false;
            }
        }

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        // Empieza el calendario de un nivel desde cero, con el azar resembrado.
        public void Begin(DropSchedule drops)
        {
            Stop();
            schedule = drops;
            random = new System.Random(seed);
            dropped = 0;
            timerMicros = ToMicros(drops.FirstDrop);
            running = drops.MaxDrops > 0;
        }

        // Apaga el power-up marcado y no deja caer más.
        public void Stop()
        {
            Unmark();
            running = false;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");
            if (!running)
                return;

            timerMicros -= ToMicros(deltaTime);

            // El Asteroide marcado desapareció con su nivel: como si se apagara.
            if (markedPowerUp != null && marked == null)
            {
                marked = null;
                markedPowerUp = null;
                Next(true);
                return;
            }

            if (timerMicros > 0)
                return;

            if (markedPowerUp != null)
                Expire();
            else
                Drop();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config, IEnumerable<PowerUpDefinition> definitions, FactionBuffs factionBuffs,
                              NodeRegistry nodeRegistry = null, int randomSeed = 0, LevelLoader levelLoader = null)
        {
            balance = config;
            powerUps = definitions != null ? new List<PowerUpDefinition>(definitions).ToArray() : Array.Empty<PowerUpDefinition>();
            buffs = factionBuffs;
            nodes = nodeRegistry;
            seed = randomSeed;
            loader = levelLoader;
        }

        void Drop()
        {
            NodeRegistry registry = Registry;
            candidates.Clear();
            if (registry != null)
            {
                foreach (NodeController node in registry.All)
                {
                    if (node != null && node.IsInitialized && node.Type == NodeType.Asteroid && node.Faction == Faction.Neutral
                        && node.TryGetComponent(out NodeIgnition _))
                        candidates.Add(node);
                }
            }

            if (candidates.Count == 0)
            {
                timerMicros += NextInterval();
                OnSkipped?.Invoke();
                return;
            }

            NodeController target = candidates[Random.Next(candidates.Count)];
            PowerUpDefinition powerUp = PickType();
            marked = target.GetComponent<NodeIgnition>();
            markedPowerUp = powerUp;
            marked.Mark(powerUp);
            marked.OnPowerUpCollected += HandleCollected;
            dropped++;
            timerMicros += ToMicros(Balance.PowerUpExpiry);
            OnDropped?.Invoke(target, powerUp);
        }

        void Expire()
        {
            NodeController node = Marked;
            PowerUpDefinition powerUp = markedPowerUp;
            Unmark();
            OnExpired?.Invoke(node, powerUp);
            Next(true);
        }

        void HandleCollected(Faction faction, PowerUpDefinition powerUp)
        {
            NodeController node = Marked;
            Unmark();
            Buffs.Apply(faction, powerUp);
            OnCollected?.Invoke(node, faction, powerUp);
            Next(false);
        }

        // Tras recogerse o apagarse: el siguiente drop, si quedan. Al apagarse dentro de un tick, el
        // sobrante del reloj pasa al intervalo; al recogerse, el intervalo empieza entero.
        void Next(bool carry)
        {
            if (dropped >= schedule.MaxDrops)
            {
                running = false;
                return;
            }
            timerMicros = (carry ? Math.Min(timerMicros, 0) : 0) + NextInterval();
        }

        void Unmark()
        {
            if (marked != null)
            {
                marked.OnPowerUpCollected -= HandleCollected;
                marked.Unmark();
            }
            marked = null;
            markedPowerUp = null;
        }

        PowerUpDefinition PickType()
        {
            float total = 0f;
            foreach (PowerUpDefinition powerUp in powerUps)
            {
                if (powerUp != null)
                    total += powerUp.DropWeight;
            }
            if (total <= 0f)
                throw new InvalidOperationException("PowerUpSpawner no tiene ningún PowerUpDefinition con peso.");

            double roll = Random.NextDouble() * total;
            PowerUpDefinition last = null;
            foreach (PowerUpDefinition powerUp in powerUps)
            {
                if (powerUp == null || powerUp.DropWeight <= 0f)
                    continue;
                last = powerUp;
                roll -= powerUp.DropWeight;
                if (roll < 0d)
                    return powerUp;
            }
            return last;
        }

        long NextInterval()
        {
            double min = schedule.MinInterval;
            double max = schedule.MaxInterval;
            return (long)Math.Round((min + Random.NextDouble() * (max - min)) * MicrosPerSecond);
        }

        void HandleLoaded(LevelDefinition level) => Begin(level.Drops);

        static long ToMicros(float seconds) => (long)Math.Round(seconds * MicrosPerSecond);

        System.Random Random => random ?? (random = new System.Random(seed));
        NodeRegistry Registry => nodes != null ? nodes : NodeRegistry.Instance;
        FactionBuffs Buffs => buffs != null ? buffs : throw new InvalidOperationException("PowerUpSpawner necesita FactionBuffs.");
        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("PowerUpSpawner necesita BalanceConfig.");
    }
}
