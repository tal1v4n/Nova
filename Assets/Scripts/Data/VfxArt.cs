using System;
using UnityEngine;

namespace Nova.Data
{
    // El arte de los efectos puntuales (A.5, 14.5, T6-07), tal como lo copia ArtBridge a Assets/Sprites/fx/: las
    // quince hojas fx_<efecto>_sheet.png con su .json. No se escribe a mano: lo rellena NodeArtAssets (Nova →
    // Arte → Asignar sprites de nodo). Un solo asset para toda la partida.
    //
    // Cada hoja es una rejilla: una fila por cada caso que el efecto distingue —bando, talla, tipo y nivel,
    // poder— con el nombre que le da su .json («player_28», «pwr_zer_32», «decay_2»…), y en columnas las
    // variantes una detrás de otra, cada una con sus frames. VFXPool elige la fila por nombre: así la regla de
    // qué fila toca vive en el generador y en su .json, y aquí no se repite.
    [CreateAssetMenu(menuName = "Nova/VFX Art", fileName = "VfxArt")]
    public sealed class VfxArt : ScriptableObject
    {
        [Serializable]
        public struct Sheet
        {
            [SerializeField] string[] rows;
            [SerializeField, Min(1)] int frames;
            [SerializeField, Min(1)] int variants;
            [Tooltip("Por filas: la fila r, variante v y frame f está en r × frames × variants + v × frames + f.")]
            [SerializeField] Sprite[] cells;

            public Sheet(string[] rowNames, int frameCount, int variantCount, Sprite[] gridCells)
            {
                rows = rowNames;
                frames = frameCount;
                variants = variantCount;
                cells = gridCells;
            }

            public int Frames => frames;
            public int Variants => Mathf.Max(variants, 1);
            public int Rows => rows?.Length ?? 0;

            public bool IsComplete =>
                Rows > 0 && frames > 0 && cells != null && cells.Length == Rows * frames * Variants &&
                Array.IndexOf(cells, null) < 0;

            // La fila de ese nombre, o −1 si la hoja no la tiene. Ninguna pasa de 20 filas.
            public int Row(string name) => rows != null ? Array.IndexOf(rows, name) : -1;

            public Sprite Frame(int row, int variant, int frame) =>
                cells[(row * Variants + variant) * frames + frame];
        }

        [Tooltip("ART-FX-SHOT: el destello sobre el protón alcanzado. Filas <bando>_<talla del protón, 1–10>; 4 variantes.")]
        [SerializeField] Sheet shot;
        [Tooltip("ART-FX-KILL: el protón destruido. Filas <bando>; 16 variantes, una por frame del bucle del protón.")]
        [SerializeField] Sheet kill;
        [Tooltip("ART-FX-VANISH: los protones huérfanos. Filas <bando>_<talla del protón>; 16 variantes, como KILL.")]
        [SerializeField] Sheet vanish;
        [Tooltip("ART-FX-SEND: el lanzamiento, sobre el nodo origen. Filas <bando>_<radio de la talla>.")]
        [SerializeField] Sheet send;
        [Tooltip("ART-FX-ABS: la absorción aliada, sobre el destino. Filas <bando>_<radio de la talla>.")]
        [SerializeField] Sheet absorb;
        [Tooltip("ART-FX-DMG: el daño sin captura. Filas <bando del protón>_<radio de la talla del nodo>; 4 variantes.")]
        [SerializeField] Sheet damage;
        [Tooltip("ART-FX-IGN: el encendido. Filas <bando>_<radio de la talla del nodo de nivel 1>.")]
        [SerializeField] Sheet ignite;
        [Tooltip("ART-FX-UPG: la mejora de nivel. Filas <bando>_<str|pul|mag><nivel nuevo>.")]
        [SerializeField] Sheet upgrade;
        [Tooltip("ART-FX-THAW: el deshielo. Filas thaw_<radio de la talla>; sin bando.")]
        [SerializeField] Sheet thaw;
        [Tooltip("ART-FX-CURE: la cura de Decaimiento. Filas cure_<radio>; 16 variantes, una por frame del bucle del miasma.")]
        [SerializeField] Sheet cure;
        [Tooltip("ART-FX-CAST: el lanzamiento de poder, sobre el Púlsar. Filas <zero|decay|wormhole>_<nivel del Púlsar>.")]
        [SerializeField] Sheet cast;
        [Tooltip("ART-FX-PWR-ZER: el impacto de Cero Absoluto. Filas pwr_zer_<radio>; acaba en el overlay de Congelado.")]
        [SerializeField] Sheet powerZero;
        [Tooltip("ART-FX-PWR-DEC: el impacto de Decaimiento. Filas pwr_dec_<radio>; acaba en el frame 15 del miasma.")]
        [SerializeField] Sheet powerDecay;
        [Tooltip("ART-FX-PWR-WRM: el impacto de Agujero de Gusano. Filas pwr_wrm_<radio>; debajo de Capturado.")]
        [SerializeField] Sheet powerWormhole;
        [Tooltip("ART-FX-CASCADE: la cascada de conquista, uno por nodo. Filas cascade_<radio>; debajo de Capturado.")]
        [SerializeField] Sheet cascade;

        [Tooltip("Cadencia de todos los efectos (14.5).")]
        [SerializeField, Min(1f)] float framesPerSecond = 12f;

        public Sheet Shot => shot;
        public Sheet Kill => kill;
        public Sheet Vanish => vanish;
        public Sheet Send => send;
        public Sheet Absorb => absorb;
        public Sheet Damage => damage;
        public Sheet Ignite => ignite;
        public Sheet Upgrade => upgrade;
        public Sheet Thaw => thaw;
        public Sheet Cure => cure;
        public Sheet Cast => cast;
        public Sheet PowerZero => powerZero;
        public Sheet PowerDecay => powerDecay;
        public Sheet PowerWormhole => powerWormhole;
        public Sheet Cascade => cascade;
        public float FramesPerSecond => framesPerSecond;

        // Lo hay cuando NodeArtAssets ya lo rellenó: sin él, VFXPool no dibuja nada.
        public bool IsComplete =>
            shot.IsComplete && kill.IsComplete && vanish.IsComplete && send.IsComplete && absorb.IsComplete &&
            damage.IsComplete && ignite.IsComplete && upgrade.IsComplete && thaw.IsComplete && cure.IsComplete &&
            cast.IsComplete && powerZero.IsComplete && powerDecay.IsComplete && powerWormhole.IsComplete &&
            cascade.IsComplete;

        // Para NodeArtAssets y los tests: el editor escribe aquí lo que copia ArtBridge, en el orden de A.5.
        public void Configure(Sheet shotSheet, Sheet killSheet, Sheet vanishSheet, Sheet sendSheet, Sheet absorbSheet,
            Sheet damageSheet, Sheet igniteSheet, Sheet upgradeSheet, Sheet thawSheet, Sheet cureSheet, Sheet castSheet,
            Sheet powerZeroSheet, Sheet powerDecaySheet, Sheet powerWormholeSheet, Sheet cascadeSheet)
        {
            shot = shotSheet;
            kill = killSheet;
            vanish = vanishSheet;
            send = sendSheet;
            absorb = absorbSheet;
            damage = damageSheet;
            ignite = igniteSheet;
            upgrade = upgradeSheet;
            thaw = thawSheet;
            cure = cureSheet;
            cast = castSheet;
            powerZero = powerZeroSheet;
            powerDecay = powerDecaySheet;
            powerWormhole = powerWormholeSheet;
            cascade = cascadeSheet;
        }
    }
}
