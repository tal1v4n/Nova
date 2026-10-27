using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // Los nueve overlays de estado de A.3 (T6-02), tal como los copia ArtBridge a Assets/Sprites/states/.
    // No se escribe a mano: lo rellena NodeArtAssets junto con el arte de los nodos (Nova → Arte → Asignar
    // sprites de nodo). Un solo asset para toda la partida, porque los overlays son independientes del tipo
    // (A.3): lo que cambia entre nodos es dónde se pintan, no qué.
    //
    // Frames a 12 fps (14.5). Los que tienen hoja son tiras de la hoja ya cortada por SpriteImportRules.
    [CreateAssetMenu(menuName = "Nova/Node State Art", fileName = "NodeStateArt")]
    public sealed class NodeStateArt : ScriptableObject
    {
        [Tooltip("ART-ST-SLEEP, Dormido: 16 frames en bucle.")]
        [SerializeField] Sprite[] sleep = Array.Empty<Sprite>();

        [Tooltip("ART-ST-IGN, SinEncender con energy < 10: 1 frame por facción.")]
        [SerializeField] Sprite unlitPlayer;
        [SerializeField] Sprite unlitEnemy;

        [Tooltip("ART-ST-READY, SinEncender con energy >= 10: 16 frames en bucle por facción.")]
        [SerializeField] Sprite[] readyPlayer = Array.Empty<Sprite>();
        [SerializeField] Sprite[] readyEnemy = Array.Empty<Sprite>();

        [Tooltip("ART-ST-FROZEN, Congelado: 1 frame.")]
        [SerializeField] Sprite frozen;

        [Tooltip("ART-ST-DECAY, Infectado: 16 frames en bucle.")]
        [SerializeField] Sprite[] decay = Array.Empty<Sprite>();

        [Tooltip("ART-ST-CONV, Reconvirtiendo: 36 frames, 3.0 s, por facción.")]
        [SerializeField] Sprite[] convertPlayer = Array.Empty<Sprite>();
        [SerializeField] Sprite[] convertEnemy = Array.Empty<Sprite>();

        [Tooltip("ART-ST-CAP, Capturado: 5 frames, 0.4 s.")]
        [SerializeField] Sprite[] captured = Array.Empty<Sprite>();

        [Tooltip("ART-ST-BUFF: el galón de cada power-up, 1 frame.")]
        [SerializeField] Sprite buffCosmicRay;
        [SerializeField] Sprite buffHydrogen;

        public Sprite[] Sleep => sleep;
        public Sprite Frozen => frozen;
        public Sprite[] Decay => decay;
        public Sprite[] Captured => captured;

        // Lo hay cuando NodeArtAssets ya lo rellenó: sin él, la vista provisional sigue tiñendo el sprite.
        public bool IsComplete =>
            sleep.Length > 0 && unlitPlayer != null && unlitEnemy != null && readyPlayer.Length > 0 &&
            readyEnemy.Length > 0 && frozen != null && decay.Length > 0 && convertPlayer.Length > 0 &&
            convertEnemy.Length > 0 && captured.Length > 0 && buffCosmicRay != null && buffHydrogen != null;

        // Las variantes de facción solo existen para los dos bandos (A.3.2, A.3.6): un nodo neutral nunca
        // está SinEncender ni Reconvirtiendo.
        public Sprite UnlitFor(Faction faction) => faction == Faction.Enemy ? unlitEnemy : unlitPlayer;
        public Sprite[] ReadyFor(Faction faction) => faction == Faction.Enemy ? readyEnemy : readyPlayer;
        public Sprite[] ConvertFor(Faction faction) => faction == Faction.Enemy ? convertEnemy : convertPlayer;
        public Sprite BuffFor(PowerUpType type) => type == PowerUpType.HydrogenCloud ? buffHydrogen : buffCosmicRay;

        // Para NodeArtAssets: el editor escribe aquí lo que copia ArtBridge.
        public void Configure(Sprite[] sleepFrames, Sprite unlitP, Sprite unlitE, Sprite[] readyP, Sprite[] readyE,
            Sprite frozenSprite, Sprite[] decayFrames, Sprite[] convertP, Sprite[] convertE, Sprite[] capturedFrames,
            Sprite buffRay, Sprite buffHydrogenSprite)
        {
            sleep = sleepFrames;
            unlitPlayer = unlitP;
            unlitEnemy = unlitE;
            readyPlayer = readyP;
            readyEnemy = readyE;
            frozen = frozenSprite;
            decay = decayFrames;
            convertPlayer = convertP;
            convertEnemy = convertE;
            captured = capturedFrames;
            buffCosmicRay = buffRay;
            buffHydrogen = buffHydrogenSprite;
        }
    }
}
