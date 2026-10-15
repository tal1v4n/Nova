using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Systems
{
    // Buffs activos por facción (9.1, 15.6, T5-04). Un power-up recogido acelera su estadística en
    // toda la facción durante su duración; los componentes de nodo lo consultan con Multiplier a
    // través de NodeController.
    //
    // - Dos del mismo tipo no se acumulan: el segundo reinicia la duración (17, caso 17).
    // - Los dos tipos sí conviven, y cada facción lleva los suyos.
    // - Neutral no recoge power-ups: solo enciende quien posee el Asteroide.
    //
    // Va a -80, como PowerUpSpawner (15.10): un buff que vence en este tick ya no lo ve ningún
    // componente de nodo (0). El reloj, en microsegundos enteros como el resto de temporizadores.
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class FactionBuffs : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        sealed class Buff
        {
            public Faction Faction;
            public PowerUpDefinition Definition;
            public long RemainingMicros;
        }

        readonly List<Buff> active = new List<Buff>();

        // (facción, power-up) al aplicarse, también cuando reinicia uno que ya estaba.
        public event Action<Faction, PowerUpDefinition> OnApplied;

        // (facción, tipo) al vencer.
        public event Action<Faction, PowerUpType> OnExpired;

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            long micros = ToMicros(deltaTime);
            for (int i = 0; i < active.Count; i++)
            {
                Buff buff = active[i];
                buff.RemainingMicros -= micros;
                if (buff.RemainingMicros > 0)
                    continue;
                active.RemoveAt(i--);
                OnExpired?.Invoke(buff.Faction, buff.Definition.Type);
            }
        }

        public void Apply(Faction faction, PowerUpDefinition powerUp)
        {
            if (powerUp == null)
                throw new ArgumentNullException(nameof(powerUp));
            if (faction == Faction.Neutral)
                throw new ArgumentException("Un power-up lo recoge quien enciende el Asteroide, y Neutral no enciende.", nameof(faction));

            Buff buff = Find(faction, powerUp.Type);
            if (buff == null)
            {
                buff = new Buff { Faction = faction };
                active.Add(buff);
            }
            buff.Definition = powerUp;
            buff.RemainingMicros = ToMicros(powerUp.Duration);
            OnApplied?.Invoke(faction, powerUp);
        }

        public bool IsActive(Faction faction, PowerUpType type) => Find(faction, type) != null;

        // Segundos que le quedan, o 0 si no está activo.
        public float Remaining(Faction faction, PowerUpType type)
        {
            Buff buff = Find(faction, type);
            return buff != null ? (float)(buff.RemainingMicros / MicrosPerSecond) : 0f;
        }

        // Factor sobre la velocidad de la estadística para esa facción: 1 sin buff, 1.5 con +50 %.
        public float Multiplier(Faction faction, PowerUpStat stat)
        {
            float multiplier = 1f;
            foreach (Buff buff in active)
            {
                if (buff.Faction == faction && buff.Definition.Stat == stat)
                    multiplier *= buff.Definition.Multiplier;
            }
            return multiplier;
        }

        // Para volver a empezar un nivel (T5-05).
        public void Clear()
        {
            active.Clear();
        }

        Buff Find(Faction faction, PowerUpType type)
        {
            foreach (Buff buff in active)
            {
                if (buff.Faction == faction && buff.Definition.Type == type)
                    return buff;
            }
            return null;
        }

        static long ToMicros(float seconds) => (long)Math.Round(seconds * MicrosPerSecond);
    }
}
