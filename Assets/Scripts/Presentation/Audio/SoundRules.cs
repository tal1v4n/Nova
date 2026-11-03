using System;
using Nova.Core;

namespace Nova.Presentation
{
    // Reglas puras del sonido de la partida (NOVA_Sonido.md, S.3.1, S.4 y S.6; T6-14), sin AudioSource, para los tests.
    public static class SoundRules
    {
        // S.3.1: los sufijos de poder de todo el proyecto.
        public static string Suffix(PowerType type)
        {
            switch (type)
            {
                case PowerType.AbsoluteZero: return "ZER";
                case PowerType.Decay: return "DEC";
                case PowerType.Wormhole: return "WRM";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        // PowerBeamSystem.OnLaunched: el haz, desde el Púlsar que lanza.
        public static string BeamId(PowerType type) => "SND-WLD-BEAM-" + Suffix(type);

        // PowerBeamSystem.OnLanded: el impacto, en el objetivo. OnDissipated no suena (S.4 #3).
        public static string ImpactId(PowerType type) => "SND-PWR-" + Suffix(type);

        // El panorama de un punto del tablero (S.6): la x sobre el ancho del tablero, con el origen en el centro,
        // de −maxPan en el borde izquierdo a +maxPan en el derecho. Fuera del tablero se queda en el borde.
        public static float Pan(float x, float boardWidth, float maxPan)
        {
            if (boardWidth <= 0f || float.IsNaN(x))
                return 0f;
            float side = x / (boardWidth * 0.5f);
            side = side < -1f ? -1f : side > 1f ? 1f : side;
            return side * maxPan;
        }
    }

    // El pool de voces de AudioDirector, sin AudioSource: qué voz suena cada sonido nuevo. Como mucho
    // maxPerId instancias del mismo ID a la vez; la siguiente corta a la más antigua de ese ID (S.6). Si el pool
    // entero está ocupado, se corta la más antigua de todas.
    public sealed class VoicePool
    {
        readonly string[] ids;
        readonly long[] order;
        readonly int maxPerId;
        long next;

        public VoicePool(int size, int maxPerId)
        {
            if (size < 1)
                throw new ArgumentOutOfRangeException(nameof(size));
            if (maxPerId < 1)
                throw new ArgumentOutOfRangeException(nameof(maxPerId));
            ids = new string[size];
            order = new long[size];
            this.maxPerId = maxPerId;
        }

        public int Size => ids.Length;

        // El ID que suena en una voz, o null si está libre.
        public string IdAt(int voice) => ids[voice];

        // isBusy dice si una voz sigue sonando (o está en pausa). Devuelve la voz que tiene que sonar el sonido.
        public int Acquire(string id, Func<int, bool> isBusy)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] != null && !isBusy(i))
                    ids[i] = null;
            }

            int same = 0, oldestSame = -1, free = -1, oldest = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == null)
                {
                    if (free < 0)
                        free = i;
                    continue;
                }
                if (ids[i] == id)
                {
                    same++;
                    if (oldestSame < 0 || order[i] < order[oldestSame])
                        oldestSame = i;
                }
                if (ids[oldest] == null || order[i] < order[oldest])
                    oldest = i;
            }

            int voice = same >= maxPerId ? oldestSame : free >= 0 ? free : oldest;
            ids[voice] = id;
            order[voice] = next++;
            return voice;
        }
    }
}
