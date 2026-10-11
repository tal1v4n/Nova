using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Systems
{
    // Haces de poder en curso (7.2.2, 15.6, T3-03). Los lanza PulsarCaster; aquí corre su reloj de
    // 0.25 s en el tick fijo, a +10 como los protones (15.10), y al terminar se revalida el objetivo:
    //
    // - Sigue siendo un nodo encendido que no es del lanzador (7.2.1): el efecto cae (OnLanded).
    // - Ya no lo es, porque es del lanzador o dejó de existir: el haz se disipa sin efecto y lo
    //   invertido se pierde (17 caso 27; OnDissipated).
    //
    // La facción es la del lanzamiento: si el Púlsar cambió de bando durante el haz, el efecto
    // sigue siendo de quien lo lanzó (17 caso 28). Los efectos de cada poder llegan con T3-04,
    // T3-05 y T3-06 y se aplican en Land.
    //
    // Varios haces que terminan en el mismo tick se resuelven por orden de lanzamiento; con empate
    // exacto, primero el del Púlsar con más Energía al lanzar, y después el que se lanzó antes.
    // El segundo opera sobre el resultado del primero y puede disiparse (17 caso 29).
    //
    // Singleton como los registros (15.1 #6): sin él, PulsarCaster no puede lanzar.
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class PowerBeamSystem : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        public static PowerBeamSystem Instance { get; private set; }

        readonly List<PowerBeam> active = new List<PowerBeam>();
        readonly List<PowerBeam> finished = new List<PowerBeam>();
        long tick;
        long nextSequence;

        // En orden de lanzamiento.
        public IReadOnlyList<PowerBeam> Active => active;

        public event Action<PowerBeam> OnLaunched;
        public event Action<PowerBeam> OnLanded;
        public event Action<PowerBeam> OnDissipated;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Nova] Hay dos PowerBeamSystem en escena; se descarta el segundo.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // Empieza un haz. Quien lo llama ya validó el lanzamiento y consumió el poder.
        public PowerBeam Launch(NodeController source, NodeController target, Faction launcher, PowerDefinition power)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (power == null)
                throw new ArgumentNullException(nameof(power));

            var beam = new PowerBeam(power, source, target, launcher, ToMicros(power.BeamDuration), tick, source.Energy, nextSequence++);
            active.Add(beam);
            OnLaunched?.Invoke(beam);
            return beam;
        }

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        // Un tick: avanza todos los haces y resuelve los que terminan, en el orden de 17 caso 29.
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            long step = ToMicros(deltaTime);
            finished.Clear();
            foreach (PowerBeam beam in active)
            {
                beam.ElapsedMicros = Math.Min(beam.ElapsedMicros + step, beam.DurationMicros);
                if (beam.IsFinished)
                    finished.Add(beam);
            }
            tick++;

            if (finished.Count == 0)
                return;

            finished.Sort(CompareResolution);
            foreach (PowerBeam beam in finished)
            {
                active.Remove(beam);
                Resolve(beam);
            }
            finished.Clear();
        }

        // 7.2.2: la validez se comprueba otra vez al terminar el haz.
        public static bool IsStillValid(PowerBeam beam)
        {
            NodeController target = beam.Target;
            return target != null && target.IsInitialized && PowerRules.IsValidTarget(target.Snapshot(), beam.Launcher);
        }

        void Resolve(PowerBeam beam)
        {
            if (IsStillValid(beam))
                Land(beam);
            else
                OnDissipated?.Invoke(beam);
        }

        // El efecto de cada poder llega con su tarea (T3-04, T3-05, T3-06).
        void Land(PowerBeam beam)
        {
            OnLanded?.Invoke(beam);
        }

        static int CompareResolution(PowerBeam a, PowerBeam b)
        {
            int byTick = a.LaunchTick.CompareTo(b.LaunchTick);
            if (byTick != 0)
                return byTick;
            int byEnergy = b.SourceEnergyAtLaunch.CompareTo(a.SourceEnergyAtLaunch);
            if (byEnergy != 0)
                return byEnergy;
            return a.Sequence.CompareTo(b.Sequence);
        }

        static long ToMicros(float seconds) => (long)Math.Round(seconds * MicrosPerSecond);
    }
}
