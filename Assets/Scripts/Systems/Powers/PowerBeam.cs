using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Systems
{
    // Un haz de poder en curso (7.2.2, 15.6): del Púlsar al objetivo, 0.25 s. No es un proyectil:
    // no se mueve, no colisiona y ningún Magnetar lo ve, porque no está en ProtonRegistry. Los dos
    // extremos se fijan al lanzar; los nodos no se mueven.
    //
    // Lleva la facción del lanzador en el momento de lanzar: si el Púlsar cambia de bando durante
    // el haz, el efecto sigue siendo de quien lo lanzó (17 caso 28). Lo lleva PowerBeamSystem.
    public sealed class PowerBeam
    {
        internal PowerBeam(PowerDefinition power, NodeController source, NodeController target, Faction launcher,
                           long durationMicros, long launchTick, int sourceEnergy, long sequence)
        {
            Power = power;
            Source = source;
            Target = target;
            Launcher = launcher;
            From = source.transform.position;
            To = target.transform.position;
            DurationMicros = durationMicros;
            LaunchTick = launchTick;
            SourceEnergyAtLaunch = sourceEnergy;
            Sequence = sequence;
        }

        public PowerDefinition Power { get; }
        public PowerType Type => Power.Type;
        public NodeController Source { get; }
        public NodeController Target { get; }
        public Faction Launcher { get; }
        public Vector2 From { get; }
        public Vector2 To { get; }

        public long DurationMicros { get; }
        public long ElapsedMicros { get; internal set; }
        public bool IsFinished => ElapsedMicros >= DurationMicros;

        // De 0 al lanzar a 1 al terminar, para la presentación.
        public float Progress => DurationMicros > 0 ? Mathf.Clamp01((float)ElapsedMicros / DurationMicros) : 1f;

        // Orden de resolución (17 caso 29): tick de lanzamiento, Energía del Púlsar y orden de llegada.
        internal long LaunchTick { get; }
        internal int SourceEnergyAtLaunch { get; }
        internal long Sequence { get; }
    }
}
