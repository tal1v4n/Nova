using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // El arte de los haces de poder (A.4.2, T6-11), tal como lo copia ArtBridge a Assets/Sprites/world/: una
    // hoja por poder —«El rosario de escarcha», «La cadena de desintegración» y «El desgarro», todos v1—. No
    // se escribe a mano: lo rellena NodeArtAssets junto con el arte de los nodos (Nova → Arte → Asignar
    // sprites de nodo). Un solo asset para toda la partida.
    //
    // El color es del poder y no de la facción (7.2.2, 14.4): no hay variantes por bando.
    [CreateAssetMenu(menuName = "Nova/Power Beam Art", fileName = "PowerBeamArt")]
    public sealed class PowerBeamArt : ScriptableObject
    {
        // Las tres piezas del haz, montadas como la línea de envío (A.4.6): nacimiento, tramo en mosaico y
        // punta, un sprite por frame. Los pivotes los pone SpriteImportRules: el nacimiento en el centro del
        // Púlsar, el tramo en su lado de la punta y la punta en el contacto, el centro del objetivo.
        [Serializable]
        public struct BeamFrames
        {
            [SerializeField] Sprite[] birth;
            [SerializeField] Sprite[] tile;
            [SerializeField] Sprite[] tip;

            public BeamFrames(Sprite[] birthFrames, Sprite[] tileFrames, Sprite[] tipFrames)
            {
                birth = birthFrames;
                tile = tileFrames;
                tip = tipFrames;
            }

            public Sprite[] Birth => birth ?? Array.Empty<Sprite>();
            public Sprite[] Tile => tile ?? Array.Empty<Sprite>();
            public Sprite[] Tip => tip ?? Array.Empty<Sprite>();

            public int FrameCount => Birth.Length;

            public bool IsComplete => FrameCount > 0 && Tile.Length == FrameCount && Tip.Length == FrameCount &&
                                      Array.IndexOf(Birth, null) < 0 && Array.IndexOf(Tile, null) < 0 &&
                                      Array.IndexOf(Tip, null) < 0;
        }

        [Tooltip("ART-WLD-BEAM-ZER: 3 frames a 12 fps, sin bucle.")]
        [SerializeField] BeamFrames absoluteZero;

        [Tooltip("ART-WLD-BEAM-DEC: 3 frames a 12 fps, sin bucle.")]
        [SerializeField] BeamFrames decay;

        [Tooltip("ART-WLD-BEAM-WRM: 3 frames a 12 fps, sin bucle.")]
        [SerializeField] BeamFrames wormhole;

        // Lo hay cuando NodeArtAssets ya lo rellenó: sin él, la vista provisional sigue dibujando el trazo
        // recto de T3-03.
        public bool IsComplete => absoluteZero.IsComplete && decay.IsComplete && wormhole.IsComplete;

        public BeamFrames For(PowerType type)
        {
            switch (type)
            {
                case PowerType.AbsoluteZero:
                    return absoluteZero;
                case PowerType.Decay:
                    return decay;
                case PowerType.Wormhole:
                    return wormhole;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Poder sin haz.");
            }
        }

        // Para NodeArtAssets y los tests: el editor escribe aquí lo que copia ArtBridge.
        public void Configure(BeamFrames absoluteZeroBeam, BeamFrames decayBeam, BeamFrames wormholeBeam)
        {
            absoluteZero = absoluteZeroBeam;
            decay = decayBeam;
            wormhole = wormholeBeam;
        }
    }
}
