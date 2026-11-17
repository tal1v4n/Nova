using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // T6-09: el arte de lo que cuelga bajo cada nodo (A.6): el número de Energía, `ART-UI-HUD-ENERGY` «El
    // filamento», y la barra de carga del Púlsar, `ART-UI-CHARGE` «El latido» (v4: la mecha de «La mecha» con un
    // remate al final y, lista, un haz sólido que late). Lo rellena NodeArtAssets desde
    // Assets/Sprites/ui/energy_label/ y ui/charge_bar/ con las medidas de sus .json; sin él, el número y la barra
    // siguen siendo las primitivas de la regla 4 de T.2.
    //
    // Todo se mide en píxeles de la referencia de 960 x 540, con el origen arriba a la izquierda, como en los
    // generadores: EnergyLabelView y ChargeBarView lo componen en un PixelCanvas.
    [CreateAssetMenu(menuName = "Nova/Node Label Art", fileName = "NodeLabelArt")]
    public sealed class NodeLabelArt : ScriptableObject
    {
        public const int Digits = 10;

        [Header("Número de Energía (ART-UI-HUD-ENERGY, «El filamento»)")]
        [Tooltip("Las cifras del Jugador: fila = cifra, columna = frame.")]
        [SerializeField] Sprite[] playerDigits = Array.Empty<Sprite>();
        [SerializeField] Sprite[] enemyDigits = Array.Empty<Sprite>();
        [Tooltip("Las diez cifras quietas de lo neutral.")]
        [SerializeField] Sprite[] neutralDigits = Array.Empty<Sprite>();
        [SerializeField] Vector2Int digitCell = new Vector2Int(12, 16);
        [Tooltip("Lo que avanza cada cifra, de 0 a 9: el 1 es más estrecho.")]
        [SerializeField] int[] advance = { 12, 8, 12, 12, 12, 12, 12, 12, 12, 12 };
        [SerializeField, Min(1)] int digitFrames = 32;
        [SerializeField, Min(1f)] float digitFramesPerSecond = 12f;
        [Tooltip("La cifra k del número usa el frame (f + k · desfase) % frames: la corriente salta de una a otra.")]
        [SerializeField] int digitPhaseOffset = 11;
        [Tooltip("El hueco que reserva NodeLayout bajo la huella; el número va centrado en él.")]
        [SerializeField] int labelBoxHeight = 20;

        [Header("Barra de carga (ART-UI-CHARGE, «El latido»)")]
        [Tooltip("El casquillo: Jugador y Enemigo.")]
        [SerializeField] Sprite[] socket = Array.Empty<Sprite>();
        [Tooltip("El remate, el casquillo reflejado que cierra la barra por la derecha: Jugador y Enemigo.")]
        [SerializeField] Sprite[] cap = Array.Empty<Sprite>();
        [Tooltip("La tesela de la pista, lo que falta: Jugador y Enemigo.")]
        [SerializeField] Sprite[] track = Array.Empty<Sprite>();
        [Tooltip("La mecha: fila = poder (Cero, Decaimiento, Gusano), frames y una columna más, la quieta.")]
        [SerializeField] Sprite[] fuse = Array.Empty<Sprite>();
        [Tooltip("La llama: filas <poder>_charging y <poder>_ready; la última columna de charging es la brasa.")]
        [SerializeField] Sprite[] flame = Array.Empty<Sprite>();
        [SerializeField] int chargeWidth = 48;
        [SerializeField] int chargeHeight = 6;
        [SerializeField] int socketWidth = 2;
        [SerializeField] int fuseX0 = 2;
        [SerializeField] int fuseLength = 46;
        [SerializeField] int flameWidth = 5;
        [SerializeField] int flameFront = 2;
        [SerializeField, Min(1)] int chargeFrames = 32;
        [SerializeField, Min(1f)] float chargeFramesPerSecond = 12f;
        [Tooltip("El haz de la carga lista, por poder: la tesela encendida y la apagada.")]
        [SerializeField] Sprite[] ready = Array.Empty<Sprite>();
        [Tooltip("El haz late: encendido estos frames y apagado otros tantos.")]
        [SerializeField, Min(1)] int readyBeatFrames = 4;
        [Tooltip("La x de la llama lista, pegada al remate.")]
        [SerializeField] int readyFlameX = 41;

        public bool HasEnergyLabel =>
            playerDigits != null && playerDigits.Length == Digits * digitFrames && Array.IndexOf(playerDigits, null) < 0 &&
            enemyDigits != null && enemyDigits.Length == Digits * digitFrames && Array.IndexOf(enemyDigits, null) < 0 &&
            neutralDigits != null && neutralDigits.Length == Digits && Array.IndexOf(neutralDigits, null) < 0 &&
            advance != null && advance.Length == Digits;

        public const int Powers = 3;

        public bool HasChargeBar =>
            socket != null && socket.Length == 2 && Array.IndexOf(socket, null) < 0 &&
            cap != null && cap.Length == 2 && Array.IndexOf(cap, null) < 0 &&
            track != null && track.Length == 2 && Array.IndexOf(track, null) < 0 &&
            fuse != null && fuse.Length == Powers * (chargeFrames + 1) && Array.IndexOf(fuse, null) < 0 &&
            flame != null && flame.Length == 2 * Powers * (chargeFrames + 1) && Array.IndexOf(flame, null) < 0 &&
            ready != null && ready.Length == 2 * Powers && Array.IndexOf(ready, null) < 0;

        // --- Número ---

        public Vector2Int DigitCell => digitCell;
        public int DigitFrames => digitFrames;
        public float DigitFramesPerSecond => digitFramesPerSecond;
        public int DigitPhaseOffset => digitPhaseOffset;
        public int LabelBoxHeight => labelBoxHeight;
        public int Advance(int digit) => advance[digit];

        // Lo neutral no tiene plasma: sus cifras van quietas (A.6).
        public Sprite Digit(Faction faction, int digit, int frame)
        {
            switch (faction)
            {
                case Faction.Player: return playerDigits[digit * digitFrames + frame];
                case Faction.Enemy: return enemyDigits[digit * digitFrames + frame];
                default: return neutralDigits[digit];
            }
        }

        // --- Barra ---

        public int ChargeWidth => chargeWidth;
        public int ChargeHeight => chargeHeight;
        public int SocketWidth => socketWidth;
        public int FuseX0 => fuseX0;
        public int FuseLength => fuseLength;
        public int FlameWidth => flameWidth;
        public int FlameFront => flameFront;
        public int ChargeFrames => chargeFrames;
        public float ChargeFramesPerSecond => chargeFramesPerSecond;
        public int ReadyBeatFrames => readyBeatFrames;
        public int ReadyFlameX => readyFlameX;

        // La fila de cada poder en las hojas, en el orden de 16.3.
        public static int PowerRow(PowerType power)
        {
            switch (power)
            {
                case PowerType.AbsoluteZero: return 0;
                case PowerType.Decay: return 1;
                default: return 2;
            }
        }

        static int FactionRow(Faction faction) => faction == Faction.Enemy ? 1 : 0;

        public Sprite Socket(Faction faction) => socket[FactionRow(faction)];
        public Sprite Cap(Faction faction) => cap[FactionRow(faction)];
        public Sprite Track(Faction faction) => track[FactionRow(faction)];

        // frame == ChargeFrames: la mecha quieta de la carga pausada.
        public Sprite Fuse(PowerType power, int frame) => fuse[PowerRow(power) * (chargeFrames + 1) + frame];

        // frame == ChargeFrames en la llama que carga: la brasa.
        public Sprite Flame(PowerType power, bool ready, int frame) =>
            flame[(2 * PowerRow(power) + (ready ? 1 : 0)) * (chargeFrames + 1) + frame];

        // El haz de la carga lista: encendido los frames (f / readyBeatFrames) pares.
        public Sprite Ready(PowerType power, bool on) => ready[PowerRow(power) * 2 + (on ? 0 : 1)];

        // Para NodeArtAssets y los tests: el editor escribe aquí lo que copia ArtBridge.
        public void ConfigureEnergyLabel(Sprite[] player, Sprite[] enemy, Sprite[] neutral, Vector2Int cell, int[] advances,
            int frames, float framesPerSecond, int phaseOffset, int boxHeight)
        {
            playerDigits = player ?? Array.Empty<Sprite>();
            enemyDigits = enemy ?? Array.Empty<Sprite>();
            neutralDigits = neutral ?? Array.Empty<Sprite>();
            digitCell = cell;
            advance = advances ?? Array.Empty<int>();
            digitFrames = Mathf.Max(1, frames);
            digitFramesPerSecond = Mathf.Max(1f, framesPerSecond);
            digitPhaseOffset = phaseOffset;
            labelBoxHeight = boxHeight;
        }

        public void ConfigureChargeBar(Sprite[] sockets, Sprite[] caps, Sprite[] tracks, Sprite[] fuses, Sprite[] flames,
            Sprite[] readyTiles, int width, int height, int socketW, int x0, int length, int flameW, int front, int frames,
            float framesPerSecond, int beatFrames, int readyFlame)
        {
            socket = sockets ?? Array.Empty<Sprite>();
            cap = caps ?? Array.Empty<Sprite>();
            ready = readyTiles ?? Array.Empty<Sprite>();
            track = tracks ?? Array.Empty<Sprite>();
            fuse = fuses ?? Array.Empty<Sprite>();
            flame = flames ?? Array.Empty<Sprite>();
            chargeWidth = width;
            chargeHeight = height;
            socketWidth = socketW;
            fuseX0 = x0;
            fuseLength = length;
            flameWidth = flameW;
            flameFront = front;
            chargeFrames = Mathf.Max(1, frames);
            chargeFramesPerSecond = Mathf.Max(1f, framesPerSecond);
            readyBeatFrames = Mathf.Max(1, beatFrames);
            readyFlameX = readyFlame;
        }
    }
}
