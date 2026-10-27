using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // El arte de un nivel de nodo (15.4, T6-01): un sprite por facción y la huella sólida, que es el
    // radio del collider de clic (18 #23). No se escribe a mano: lo rellena NodeArtAssets desde
    // Assets/Sprites/nodes/, con la huella que mide el generador (NOVA_Arte_Tecnica.md, *Huella del
    // nodo*). La silueta es la misma en las tres facciones (14.0 #3), así que la huella va por nivel.
    //
    // T6-02: los frames de la hoja de idle (A.2.2, A.2.3), solo en la Estrella y el Púlsar. El Asteroide y
    // el Magnetar cabecean por transform (14.0 #4) y los llevan vacíos. El frame 0 es el sprite base.
    [Serializable]
    public struct NodeLevelArt
    {
        public const int PixelsPerUnit = 64;

        [Tooltip("Vacío en la Estrella y el Púlsar: solo el Asteroide y el Magnetar son neutrales (3.2).")]
        [SerializeField] Sprite neutral;
        [SerializeField] Sprite player;
        [SerializeField] Sprite enemy;

        [Tooltip("Lado mayor de la caja que encierra la materia del sprite, en píxeles. La luz tramada no cuenta.")]
        [SerializeField, Min(0)] int footprintPixels;

        [Tooltip("Hoja de idle de 16 frames (T6-02). Vacías donde el idle es por transform.")]
        [SerializeField] Sprite[] neutralIdle;
        [SerializeField] Sprite[] playerIdle;
        [SerializeField] Sprite[] enemyIdle;

        public NodeLevelArt(Sprite neutralSprite, Sprite playerSprite, Sprite enemySprite, int footprint)
            : this(neutralSprite, playerSprite, enemySprite, footprint, null, null, null)
        {
        }

        public NodeLevelArt(Sprite neutralSprite, Sprite playerSprite, Sprite enemySprite, int footprint,
            Sprite[] neutralIdleFrames, Sprite[] playerIdleFrames, Sprite[] enemyIdleFrames)
        {
            neutral = neutralSprite;
            player = playerSprite;
            enemy = enemySprite;
            footprintPixels = footprint;
            neutralIdle = neutralIdleFrames ?? Array.Empty<Sprite>();
            playerIdle = playerIdleFrames ?? Array.Empty<Sprite>();
            enemyIdle = enemyIdleFrames ?? Array.Empty<Sprite>();
        }

        public int FootprintPixels => footprintPixels;

        // Radio del trigger de clic, en u: media huella.
        public float FootprintRadius => footprintPixels / 2f / PixelsPerUnit;

        // null si el tipo no tiene sprite para esa facción.
        public Sprite SpriteFor(Faction faction)
        {
            switch (faction)
            {
                case Faction.Player: return player;
                case Faction.Enemy: return enemy;
                default: return neutral;
            }
        }

        // Los frames del idle de esa facción, o vacío si su idle es por transform.
        public Sprite[] IdleFor(Faction faction)
        {
            Sprite[] frames;
            switch (faction)
            {
                case Faction.Player: frames = playerIdle; break;
                case Faction.Enemy: frames = enemyIdle; break;
                default: frames = neutralIdle; break;
            }
            return frames ?? Array.Empty<Sprite>();
        }
    }
}
