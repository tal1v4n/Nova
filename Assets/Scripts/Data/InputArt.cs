using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // El arte de la entrada del Jugador (A.4.3, T6-06), tal como lo copia ArtBridge a Assets/Sprites/world/:
    // la línea de envío «El Hilo Vivo» v2, el anillo de selección «La órbita», el lazo «El marco» y la retícula
    // de apuntado «El cepo». No se escribe a mano: lo rellena NodeArtAssets (Nova → Arte → Asignar sprites de
    // nodo). Un solo asset para toda la partida; solo el Jugador tiene cursor (14.9).
    [CreateAssetMenu(menuName = "Nova/Input Art", fileName = "InputArt")]
    public sealed class InputArt : ScriptableObject
    {
        // Una hoja ya cortada: sus frames en orden. Unity no serializa Sprite[][], así que va envuelta.
        [Serializable]
        public struct Frames
        {
            [SerializeField] Sprite[] frames;

            public Frames(Sprite[] sprites)
            {
                frames = sprites;
            }

            public Sprite[] Sprites => frames ?? Array.Empty<Sprite>();
            public int Count => Sprites.Length;
            public bool IsComplete => Count > 0 && Array.IndexOf(Sprites, null) < 0;
        }

        // ART-WLD-LINK: nacimiento, tramo y punta, 16 frames en bucle, con los pivotes de SpriteImportRules.
        [SerializeField] Frames linkBirth;
        [SerializeField] Frames linkTile;
        [SerializeField] Frames linkTip;

        // ART-WLD-SEL: una hoja de 16 frames por talla. El radio libre de cada talla y el aire entre la media
        // huella y el anillo vienen de world_select_tallas.json (tools/gen_select_v1.py).
        [SerializeField] int ringAirPixels;
        [SerializeField] int[] ringRadiusPixels;
        [SerializeField] Frames[] rings;

        // ART-WLD-LASSO: la esquina de arriba a la izquierda y el tramo de 8 × 1 en 8 frames.
        [SerializeField] Sprite lassoCorner;
        [SerializeField] Frames lassoEdge;

        // ART-WLD-AIM: una hoja de 4 frames por poder y talla (las del anillo): tres de entrada y el reposo.
        [SerializeField] Frames[] aimZero;
        [SerializeField] Frames[] aimDecay;
        [SerializeField] Frames[] aimWormhole;

        public Sprite[] LinkBirth => linkBirth.Sprites;
        public Sprite[] LinkTile => linkTile.Sprites;
        public Sprite[] LinkTip => linkTip.Sprites;
        public int LinkFrames => linkBirth.Count;

        public int RingSizes => ringRadiusPixels?.Length ?? 0;
        public Sprite[] RingFrames(int size) => rings[size].Sprites;

        public Sprite LassoCorner => lassoCorner;
        public Sprite[] LassoEdge => lassoEdge.Sprites;

        public bool IsComplete =>
            linkBirth.IsComplete && linkTile.IsComplete && linkTip.IsComplete &&
            linkTile.Count == linkBirth.Count && linkTip.Count == linkBirth.Count &&
            RingSizes > 0 && AllComplete(rings, RingSizes) &&
            lassoCorner != null && lassoEdge.IsComplete &&
            AllComplete(aimZero, RingSizes) && AllComplete(aimDecay, RingSizes) && AllComplete(aimWormhole, RingSizes);

        static bool AllComplete(Frames[] sheets, int count)
        {
            if (sheets == null || sheets.Length != count)
                return false;
            foreach (Frames sheet in sheets)
            {
                if (!sheet.IsComplete)
                    return false;
            }
            return true;
        }

        // La talla de un nodo por su huella: la primera cuyo radio libre deja el aire entre la media huella y el
        // anillo, o la mayor. Es talla_de() de tools/gen_select_v1.py: si cambia una, cambia la otra. La retícula
        // de apuntado usa la misma.
        public int SizeFor(int footprintPixels)
        {
            int free = (footprintPixels + 1) / 2 + ringAirPixels;
            for (int i = 0; i < RingSizes; i++)
            {
                if (ringRadiusPixels[i] >= free)
                    return i;
            }
            return RingSizes - 1;
        }

        public int RingRadiusPixels(int size) => ringRadiusPixels[size];

        // La hoja de la retícula del poder y la talla. El color es del poder, no del bando (14.4).
        public Sprite[] AimFrames(PowerType power, int size)
        {
            switch (power)
            {
                case PowerType.AbsoluteZero:
                    return aimZero[size].Sprites;
                case PowerType.Decay:
                    return aimDecay[size].Sprites;
                case PowerType.Wormhole:
                    return aimWormhole[size].Sprites;
                default:
                    throw new ArgumentOutOfRangeException(nameof(power), power, "Poder sin retícula.");
            }
        }

        // Para NodeArtAssets y los tests: el editor escribe aquí lo que copia ArtBridge.
        public void Configure(Frames birth, Frames tile, Frames tip, int airPixels, int[] radiusPixels, Frames[] ringSheets,
            Sprite corner, Frames edge, Frames[] zero, Frames[] decay, Frames[] wormhole)
        {
            linkBirth = birth;
            linkTile = tile;
            linkTip = tip;
            ringAirPixels = airPixels;
            ringRadiusPixels = radiusPixels;
            rings = ringSheets;
            lassoCorner = corner;
            lassoEdge = edge;
            aimZero = zero;
            aimDecay = decay;
            aimWormhole = wormhole;
        }
    }
}
