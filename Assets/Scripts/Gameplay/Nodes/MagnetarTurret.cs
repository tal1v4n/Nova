using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Systems;
using UnityEngine;

namespace Nova.Gameplay
{
    // Torreta del Magnetar (4.4, 6.3, 15.5): dispara a los protones hostiles que están dentro de
    // su radio, con la cadencia de su nivel, y cada disparo les resta la Carga de BalanceConfig.
    // Corre en el tick fijo de 50 Hz, a 0: antes de que los protones (+10) se muevan.
    //
    // Objetivo (4.4): el protón hostil más cercano al centro del Magnetar; con empate, el de más
    // Carga, y con empate también, el que se lanzó antes, que es el orden de ProtonRegistry.
    // Un protón está dentro con distancia <= radio: el borde dibujado cuenta (A.4.1).
    //
    // Cadencia: el arma está cargada en reposo y dispara en cuanto entra un objetivo. Disparando
    // sin pausa, el tiempo se acumula en microsegundos enteros y el sobrante pasa al disparo
    // siguiente, como el temporizador de la Estrella: 1/1.33 s no es múltiplo de 20 ms.
    //
    // Como StarGenerator, va en todo nodo que pueda llegar a ser Magnetar y solo actúa cuando lo
    // es (desviación 143). El Magnetar neutral dispara a los dos bandos (3.2, T2-06); reconvirtiendo
    // no dispara (8.1, T2-07), y congelado tampoco (7.3, T3-04).
    //
    // Rayo Cósmico (9.2, T5-04): la recarga corre ×1.5 con el buff de su facción. El neutral no
    // tiene facción que lo recoja.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class MagnetarTurret : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        [SerializeField] BalanceConfig balance;

        [Tooltip("Opcional: sin él se usa ProtonRegistry.Instance.")]
        [SerializeField] ProtonRegistry protons;

        NodeController node;

        // Tiempo que falta para poder disparar. 0 o menos: cargada.
        long cooldownMicros;

        // (Magnetar, protón alcanzado). Se emite antes de restar la Carga, con el protón en su sitio.
        public event Action<MagnetarTurret, Proton> OnShot;

        public bool IsReady => cooldownMicros <= 0;

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            if (!CanFire)
            {
                cooldownMicros = 0;
                return;
            }

            float fireRate = Node.CurrentLevelData.FireRate;
            if (fireRate <= 0f)
                throw new InvalidOperationException($"Magnetar nivel {Node.Level} sin cadencia.");
            long interval = (long)Math.Round(MicrosPerSecond / fireRate);

            // Recargando, el tick cuenta; cargada, el arma no adelanta el siguiente disparo.
            if (cooldownMicros > 0)
                cooldownMicros -= ToMicros(deltaTime * Node.BuffMultiplier(PowerUpStat.AttackSpeed));
            while (cooldownMicros <= 0)
            {
                Proton target = FindTarget();
                if (target == null)
                {
                    // En reposo el arma espera cargada; no acumula disparos.
                    cooldownMicros = 0;
                    return;
                }
                Shoot(target);
                cooldownMicros += interval;
            }
        }

        // El protón al que dispararía ahora, o null. Sin efectos: lo usan Tick y los tests.
        public Proton FindTarget()
        {
            ProtonRegistry registry = Registry;
            if (registry == null || !CanFire)
                return null;

            Vector2 center = transform.position;
            float radius = Node.CurrentLevelData.Radius;
            float radiusSquared = radius * radius;

            Proton best = null;
            float bestDistance = float.MaxValue;
            IReadOnlyList<Proton> inFlight = registry.InFlight;
            for (int i = 0; i < inFlight.Count; i++)
            {
                Proton proton = inFlight[i];
                if (proton == null || !proton.InFlight || !IsHostile(proton.Faction))
                    continue;

                float distance = ((Vector2)proton.transform.position - center).sqrMagnitude;
                if (distance > radiusSquared)
                    continue;

                // Orden de registro = orden de lanzamiento: con empate total gana el primero.
                if (best == null || distance < bestDistance || (distance == bestDistance && proton.Charge > best.Charge))
                {
                    best = proton;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // Tabla de 4.4: nunca a los de su facción. Un protón siempre es de Jugador o de Enemigo,
        // así que el Magnetar neutral, que no tiene facción que proteger, dispara a los dos (3.2).
        public bool IsHostile(Faction protonFaction)
        {
            return protonFaction != Node.Faction && protonFaction != Faction.Neutral;
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config, ProtonRegistry registry = null)
        {
            balance = config;
            protons = registry;
        }

        void Shoot(Proton target)
        {
            OnShot?.Invoke(this, target);
            target.TakeShot(Balance.MagnetarShotDamage);
        }

        // Reconvirtiendo (8.1) o congelado (7.3) no dispara: el arma se queda cargada, sin acumular
        // disparos. Vale también para el Magnetar neutral, que congelado calla a los dos bandos (17 caso 31).
        bool CanFire => Node.IsInitialized && Node.Type == NodeType.Magnetar
            && !Node.HasState(NodeStateFlags.Converting) && !Node.HasState(NodeStateFlags.Frozen);

        ProtonRegistry Registry => protons != null ? protons : ProtonRegistry.Instance;

        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("MagnetarTurret necesita BalanceConfig.");

        static long ToMicros(float seconds) => (long)Math.Round(seconds * MicrosPerSecond);

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
    }
}
