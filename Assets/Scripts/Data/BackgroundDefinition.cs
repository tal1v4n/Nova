using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Data
{
    // Una capa del fondo que avanza `rate` por segundo y cierra cada `period` (fase() de
    // tools/gen_fondos_v2.py). El material recibe la fase ya reducida, calculada en double: con el
    // tiempo de partida en float, a la hora de juego un desplazamiento de 6 px/s perdería la fracción.
    [Serializable]
    public struct BackgroundPhase
    {
        [SerializeField] string property;
        [SerializeField] double rate;
        [SerializeField] double period;
        [SerializeField] double offset;

        public BackgroundPhase(string property, double rate, double period, double offset)
        {
            this.property = property;
            this.rate = rate;
            this.period = period;
            this.offset = offset;
        }

        public string Property => property;
        public double Rate => rate;
        public double Period => period;
        public double Offset => offset;

        public double Evaluate(double time) => BackgroundDefinition.Reduce(offset + rate * time, period);
    }

    // Lo mismo, un valor por fila: lo que avanza distinto en cada fila de una banda (las chispas del
    // Cinturón), o una tabla fija si el periodo es 0 (la fila de la banda por columna).
    [Serializable]
    public sealed class BackgroundPhaseList
    {
        [SerializeField] string property;
        [SerializeField] double[] rates = Array.Empty<double>();
        [SerializeField] double[] offsets = Array.Empty<double>();
        [SerializeField] double period;

        public BackgroundPhaseList(string property, double[] rates, double[] offsets, double period)
        {
            if (rates == null || offsets == null || rates.Length != offsets.Length)
                throw new ArgumentException($"{property}: hace falta un ritmo por cada desfase.");
            this.property = property;
            this.rates = rates;
            this.offsets = offsets;
            this.period = period;
        }

        public string Property => property;
        public int Count => offsets.Length;

        public void Evaluate(double time, float[] values)
        {
            for (int i = 0; i < offsets.Length; i++)
                values[i] = (float)BackgroundDefinition.Reduce(offsets[i] + rates[i] * time, period);
        }
    }

    // Una roca de la grava del Cinturón: media celda, desplazamiento respecto al eje de la corriente,
    // columna de partida y velocidad en px/s.
    [Serializable]
    public struct BackgroundRock
    {
        [SerializeField] int half;
        [SerializeField] double dy;
        [SerializeField] double x0;
        [SerializeField] double speed;

        public BackgroundRock(int half, double dy, double x0, double speed)
        {
            this.half = half;
            this.dy = dy;
            this.x0 = x0;
            this.speed = speed;
        }

        public int Half => half;
        public double Dy => dy;
        public double X0 => x0;
        public double Speed => speed;
    }

    // El fondo con movimiento de un mapa (A.4.5, 15.4, T5-05b): su material, las fases que corren sus
    // capas, la ruta del ovni y la grava. No se escribe a mano: lo genera BackgroundAssets
    // (Nova → Arte → Generar fondos) desde el JSON que exporta tools/gen_fondos_v2.py, que es la
    // referencia. Tres instancias, una por mapa, en Assets/Data/Backgrounds/.
    //
    // Las posiciones van en píxeles del fondo con el origen arriba a la izquierda, como en el
    // generador; pasarlas a unidades es de BackgroundView.
    [CreateAssetMenu(menuName = "Nova/Background Definition", fileName = "Background")]
    public sealed class BackgroundDefinition : ScriptableObject
    {
        public const int Width = 960;   // 15 u a PPU 64 (T.8)
        public const int Height = 540;  // 8.4375 u
        public const int PixelsPerUnit = 64;

        [SerializeField, Range(1, 3)] int map = 1;

        [Tooltip("El material del mapa, con sus texturas de datos y constantes (Assets/Data/Backgrounds/Map{n}.mat).")]
        [SerializeField] Material material;

        [Tooltip("El fotograma en t = 0 (bg_section{n}.png): el sprite que lleva el material.")]
        [SerializeField] Sprite frame;

        [SerializeField] BackgroundPhase[] phases = Array.Empty<BackgroundPhase>();
        [SerializeField] BackgroundPhaseList[] lists = Array.Empty<BackgroundPhaseList>();

        [Tooltip("Eje de la corriente por columna, en píxeles (mapa 2): lo siguen la grava y el ovni.")]
        [SerializeField] double[] axis = Array.Empty<double>();

        [Header("Ovni (ART-BG-UFO)")]
        [SerializeField] Sprite[] ufoFrames = Array.Empty<Sprite>();
        [SerializeField] int ufoFrameCount = 16;
        [SerializeField] int ufoFps = 12;
        [SerializeField] int ufoCell = 32;
        [SerializeField] double ufoSpeed = 30.0;
        [SerializeField] double ufoSway = 3.0;
        [SerializeField] double ufoSwayPeriod = 2.4;
        [SerializeField] double ufoIntervalMin = 45.0;
        [SerializeField] double ufoIntervalMax = 90.0;

        [Tooltip("1: de izquierda a derecha; -1: al revés.")]
        [SerializeField] int ufoDirection = 1;

        [SerializeField] double ufoStartY;
        [SerializeField] double ufoSlope;

        [Tooltip("Sigue el eje de la corriente a ufoSurfOffset píxeles, en vez de ir en línea recta.")]
        [SerializeField] bool ufoSurf;

        [SerializeField] double ufoSurfOffset;

        [Header("Grava (mapa 2)")]
        [SerializeField] Sprite[] rockFrames = Array.Empty<Sprite>();
        [SerializeField] BackgroundRock[] rocks = Array.Empty<BackgroundRock>();

        public int Map => map;
        public Material Material => material;
        public Sprite Frame => frame;
        public IReadOnlyList<BackgroundPhase> Phases => phases;
        public IReadOnlyList<BackgroundPhaseList> Lists => lists;
        public IReadOnlyList<Sprite> UfoFrames => ufoFrames;
        public IReadOnlyList<Sprite> RockFrames => rockFrames;
        public IReadOnlyList<BackgroundRock> Rocks => rocks;
        public int UfoCell => ufoCell;
        public double UfoIntervalMin => ufoIntervalMin;
        public double UfoIntervalMax => ufoIntervalMax;

        // (value mod period) en [0, period); con periodo 0, el valor tal cual.
        public static double Reduce(double value, double period)
        {
            if (period <= 0.0)
                return value;
            double r = value % period;
            return r < 0.0 ? r + period : r;
        }

        // ruta() y ovni_en() de gen_fondos_v2.py: el píxel central del ovni y su frame `age` segundos
        // después de salir, o false si ya ha cruzado el tablero.
        public bool UfoAt(double age, out Vector2Int pixel, out int ufoFrame)
        {
            pixel = default;
            ufoFrame = 0;
            if (age < 0.0)
                return false;
            double travelled = ufoSpeed * age - ufoCell;
            if (travelled > Width + ufoCell)
                return false;
            double x = ufoDirection > 0 ? travelled : Width - travelled;
            double y = ufoSurf
                ? axis[(int)Math.Min(Math.Max(x, 0.0), Width - 1)] + ufoSurfOffset
                : ufoStartY + ufoSlope * travelled;
            y += ufoSway * Math.Sin(2.0 * Math.PI * age / ufoSwayPeriod);
            pixel = new Vector2Int((int)Math.Round(x), (int)Math.Round(y));
            ufoFrame = (int)(age * ufoFps) % ufoFrameCount;
            return true;
        }

        // _centro() de gen_fondos_v2.py: el píxel central de una roca en el instante `time`.
        public Vector2Int RockAt(int index, double time)
        {
            BackgroundRock rock = rocks[index];
            int lap = Width + 2 * rock.Half;
            int x = (int)Reduce(rock.X0 + rock.Speed * time, lap) - rock.Half;
            int y = (int)Math.Round(axis[Math.Clamp(x, 0, Width - 1)] + rock.Dy);
            return new Vector2Int(x, y);
        }

        // Para BackgroundAssets y los tests: el cableado normal es el generador.
        public void Configure(int mapNumber, Material backgroundMaterial, Sprite firstFrame, BackgroundPhase[] layerPhases,
                              BackgroundPhaseList[] layerLists, double[] streamAxis)
        {
            if (mapNumber < 1 || mapNumber > 3)
                throw new ArgumentOutOfRangeException(nameof(mapNumber), mapNumber, "Los mapas van del 1 al 3.");
            map = mapNumber;
            material = backgroundMaterial;
            frame = firstFrame;
            phases = layerPhases ?? Array.Empty<BackgroundPhase>();
            lists = layerLists ?? Array.Empty<BackgroundPhaseList>();
            axis = streamAxis ?? Array.Empty<double>();
        }

        public void ConfigureUfo(Sprite[] frames, int frameCount, int fps, int cell, double speed, double sway, double swayPeriod,
                                 double intervalMin, double intervalMax, int direction, double startY, double slope,
                                 bool surf, double surfOffset)
        {
            if (frameCount <= 0 || fps <= 0 || swayPeriod <= 0.0 || intervalMin <= 0.0 || intervalMax < intervalMin)
                throw new ArgumentException("Ovni sin frames, sin cadencia o con un intervalo imposible.");
            ufoFrames = frames ?? Array.Empty<Sprite>();
            ufoFrameCount = frameCount;
            ufoFps = fps;
            ufoCell = cell;
            ufoSpeed = speed;
            ufoSway = sway;
            ufoSwayPeriod = swayPeriod;
            ufoIntervalMin = intervalMin;
            ufoIntervalMax = intervalMax;
            ufoDirection = direction;
            ufoStartY = startY;
            ufoSlope = slope;
            ufoSurf = surf;
            ufoSurfOffset = surfOffset;
        }

        public void ConfigureRocks(Sprite[] frames, BackgroundRock[] gravel)
        {
            rockFrames = frames ?? Array.Empty<Sprite>();
            rocks = gravel ?? Array.Empty<BackgroundRock>();
        }
    }
}
