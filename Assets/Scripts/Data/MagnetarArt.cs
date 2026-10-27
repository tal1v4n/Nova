using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // El arte de mundo del Magnetar (A.4.1, T6-05), tal como lo copia ArtBridge a Assets/Sprites/world/:
    // los 15 anillos de radio, uno por facción y nivel («El susurro», v2), y los tres arcos de disparo,
    // uno por bando («El chispazo», v1). No se escribe a mano: lo rellena NodeArtAssets junto con el arte
    // de los nodos (Nova → Arte → Asignar sprites de nodo). Un solo asset para toda la partida.
    [CreateAssetMenu(menuName = "Nova/Magnetar Art", fileName = "MagnetarArt")]
    public sealed class MagnetarArt : ScriptableObject
    {
        public const int Levels = 5;

        // Las tres piezas del filamento de 14.9 (A.4.6), un sprite por frame. Los pivotes los pone
        // SpriteImportRules: el nacimiento en el centro del Magnetar, el tramo en su lado de la punta y la
        // punta en el contacto, el centro del protón.
        [Serializable]
        public struct ShotFrames
        {
            [SerializeField] Sprite[] birth;
            [SerializeField] Sprite[] tile;
            [SerializeField] Sprite[] tip;

            public ShotFrames(Sprite[] birthFrames, Sprite[] tileFrames, Sprite[] tipFrames)
            {
                birth = birthFrames;
                tile = tileFrames;
                tip = tipFrames;
            }

            public Sprite[] Birth => birth ?? Array.Empty<Sprite>();
            public Sprite[] Tile => tile ?? Array.Empty<Sprite>();
            public Sprite[] Tip => tip ?? Array.Empty<Sprite>();

            public int FrameCount => Birth.Length;

            public bool IsComplete => FrameCount > 0 && Tile.Length == FrameCount && Tip.Length == FrameCount;
        }

        [Tooltip("ART-WLD-RAD-NEUTRAL, -ENEMY y -PLAYER: el anillo de los niveles 1 a 5.")]
        [SerializeField] Sprite[] ringNeutral = Array.Empty<Sprite>();
        [SerializeField] Sprite[] ringPlayer = Array.Empty<Sprite>();
        [SerializeField] Sprite[] ringEnemy = Array.Empty<Sprite>();

        [Tooltip("ART-WLD-RAD-SHOT: 2 frames, sin bucle, en la rampa del Magnetar que dispara.")]
        [SerializeField] ShotFrames shotNeutral;
        [SerializeField] ShotFrames shotPlayer;
        [SerializeField] ShotFrames shotEnemy;

        // Lo hay cuando NodeArtAssets ya lo rellenó: sin él, la vista provisional sigue dibujando el
        // anillo liso y el trazo recto de T2-04.
        public bool IsComplete =>
            HasRings(ringNeutral) && HasRings(ringPlayer) && HasRings(ringEnemy) &&
            shotNeutral.IsComplete && shotPlayer.IsComplete && shotEnemy.IsComplete;

        // null si no hay anillo para ese nivel.
        public Sprite RingFor(Faction faction, int level)
        {
            Sprite[] rings = faction == Faction.Player ? ringPlayer : faction == Faction.Enemy ? ringEnemy : ringNeutral;
            int index = level - 1;
            return rings != null && index >= 0 && index < rings.Length ? rings[index] : null;
        }

        public ShotFrames ShotFor(Faction faction) =>
            faction == Faction.Player ? shotPlayer : faction == Faction.Enemy ? shotEnemy : shotNeutral;

        // Para NodeArtAssets y los tests: el editor escribe aquí lo que copia ArtBridge.
        public void Configure(Sprite[] neutralRings, Sprite[] playerRings, Sprite[] enemyRings,
            ShotFrames neutralShot, ShotFrames playerShot, ShotFrames enemyShot)
        {
            ringNeutral = neutralRings;
            ringPlayer = playerRings;
            ringEnemy = enemyRings;
            shotNeutral = neutralShot;
            shotPlayer = playerShot;
            shotEnemy = enemyShot;
        }

        static bool HasRings(Sprite[] rings) => rings != null && rings.Length == Levels && Array.IndexOf(rings, null) < 0;
    }
}
