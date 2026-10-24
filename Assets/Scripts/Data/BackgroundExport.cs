using System;

namespace Nova.Data
{
    // El JSON de un mapa que escribe `py tools/gen_fondos_v2.py --exporta` (bg_section{n}.json), tal
    // cual, para JsonUtility. Lo lee BackgroundAssets para generar el material y el
    // BackgroundDefinition, y BackgroundTests para comparar el material con los fotogramas de
    // referencia. Los nombres de campo son los del JSON: no se renombran.
    [Serializable]
    public sealed class BackgroundExport
    {
        public int map;
        public string name;
        public int version;
        public int width;
        public int height;
        public string frame;
        public float budget;
        public TextureEntry[] textures;
        public FloatEntry[] floats;
        public VectorEntry[] vectors;
        public ColorEntry[] colors;
        public PhaseEntry[] phases;
        public ListEntry[] lists;
        public double[] axis;
        public UfoEntry ufo;
        public RocksEntry rocks;
        public ReferenceEntry[] references;
        public UfoSample[] ufoSamples;

        [Serializable]
        public sealed class TextureEntry
        {
            public string property;
            public string file;
        }

        [Serializable]
        public sealed class FloatEntry
        {
            public string property;
            public float value;
        }

        [Serializable]
        public sealed class VectorEntry
        {
            public string property;
            public float[] value;
        }

        [Serializable]
        public sealed class ColorEntry
        {
            public string property;
            public string value;
        }

        [Serializable]
        public sealed class PhaseEntry
        {
            public string property;
            public double rate;
            public double period;
            public double offset;
        }

        [Serializable]
        public sealed class ListEntry
        {
            public string property;
            public double[] rates;
            public double period;
            public double[] offsets;
        }

        [Serializable]
        public sealed class UfoEntry
        {
            public string sheet;
            public int cell;
            public int frames;
            public int fps;
            public double speed;
            public double sway;
            public double swayPeriod;
            public double intervalMin;
            public double intervalMax;
            public int direction;
            public double startY;
            public double startYMin;
            public double startYMax;
            public double slope;
            public bool surf;
            public double surfOffset;
        }

        [Serializable]
        public sealed class RockEntry
        {
            public int half;
            public double dy;
            public double x0;
            public double speed;
        }

        [Serializable]
        public sealed class RocksEntry
        {
            public string sheet;
            public int cell;
            public RockEntry[] list;
        }

        // Un fotograma de referencia: su instante, su PNG en ref/ y el píxel central de cada roca,
        // aplanado (x0, y0, x1, y1…).
        [Serializable]
        public sealed class ReferenceEntry
        {
            public double t;
            public string file;
            public int[] rocks;
        }

        // Dónde está el ovni `s` segundos después de salir, según el generador.
        [Serializable]
        public sealed class UfoSample
        {
            public double s;
            public bool visible;
            public int x;
            public int y;
            public int frame;
        }
    }
}
