using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // El arte del protón (A.4.2, T6-04), tal como lo copia ArtBridge a Assets/Sprites/world/proton/: «La
    // bolsa», v2. Diez tallas, una por cada 5 de Carga: 1–5, 6–10, …, 46–50, y de 51 en adelante la mayor
    // (pedido del usuario, 2026-11-01). Por bando y talla, los 16 frames del cuerpo y los 4 pasos de la
    // gota de estela; y las cifras propias de la Carga, de 0 a 9, con su avance. No se escribe a mano: lo
    // rellena NodeArtAssets (Nova → Arte → Asignar sprites de nodo) desde las hojas y los dos .json del
    // generador. Un solo asset para toda la partida.
    //
    // La talla es un índice de 0 a Sizes − 1. Los números de presentación viven aquí y no en el código: el
    // escalón de Carga, la estela y el hueco de la bolsa de cada talla, en píxeles de diseño.
    [CreateAssetMenu(menuName = "Nova/Proton Art", fileName = "ProtonArt")]
    public sealed class ProtonArt : ScriptableObject
    {
        public const int Digits = 10;

        // Por talla: el bucle del cuerpo y los pasos de la gota.
        [Serializable]
        public struct SizeFrames
        {
            [SerializeField] Sprite[] body;
            [SerializeField] Sprite[] drop;

            public SizeFrames(Sprite[] bodyFrames, Sprite[] dropSteps)
            {
                body = bodyFrames;
                drop = dropSteps;
            }

            public Sprite[] Body => body ?? Array.Empty<Sprite>();
            public Sprite[] Drop => drop ?? Array.Empty<Sprite>();

            public bool IsComplete => Body.Length > 0 && Drop.Length > 0 &&
                Array.IndexOf(Body, null) < 0 && Array.IndexOf(Drop, null) < 0;
        }

        [Tooltip("ART-WLD-PRO-P: de la talla más pequeña a la mayor.")]
        [SerializeField] SizeFrames[] player = Array.Empty<SizeFrames>();
        [Tooltip("ART-WLD-PRO-E: de la talla más pequeña a la mayor.")]
        [SerializeField] SizeFrames[] enemy = Array.Empty<SizeFrames>();

        [Tooltip("world_charge_digits: las cifras de 0 a 9, con el pivote abajo a la izquierda.")]
        [SerializeField] Sprite[] digits = new Sprite[Digits];
        [Tooltip("Píxeles que avanza cada cifra, de 0 a 9 (world_charge_digits.json).")]
        [SerializeField] int[] digitAdvance = new int[Digits];
        [Tooltip("Alto de la celda de una cifra, contorno incluido.")]
        [SerializeField, Min(1)] int digitHeight = 9;

        [Header("Tallas (A.4.2, world_proton_tallas.json)")]
        [Tooltip("Carga de cada escalón: la talla sube una vez cada tantos puntos, y la última se queda con todo lo de encima.")]
        [SerializeField, Min(1)] int chargePerSize = 5;
        [Tooltip("Radio de la bolsa más 2 px, por talla: una gota no se ve hasta que sale de ella.")]
        [SerializeField] int[] bagRadiusPixels = Array.Empty<int>();

        [Header("Animación")]
        [Tooltip("Cadencia del cuerpo y de la gota (contrato, Animación).")]
        [SerializeField, Min(1f)] float framesPerSecond = 12f;

        [Header("Estela")]
        [Tooltip("Cada cuántos píxeles de recorrido se suelta una gota.")]
        [SerializeField, Min(1)] int dropSpacingPixels = 7;
        [Tooltip("Frames que dura cada paso de la gota: con 4 pasos y 2 frames, 0.67 s.")]
        [SerializeField, Min(1)] int dropStepFrames = 2;

        // Lo hay cuando NodeArtAssets ya lo rellenó: sin él, la vista provisional sigue dibujando el
        // disco de T1-12.
        public bool IsComplete =>
            Sizes > 0 && enemy != null && enemy.Length == Sizes &&
            Array.TrueForAll(player, s => s.IsComplete) && Array.TrueForAll(enemy, s => s.IsComplete) &&
            bagRadiusPixels != null && bagRadiusPixels.Length == Sizes &&
            digits != null && digits.Length == Digits && Array.IndexOf(digits, null) < 0 &&
            digitAdvance != null && digitAdvance.Length == Digits && Array.TrueForAll(digitAdvance, a => a > 0);

        public int Sizes => player != null ? player.Length : 0;
        public int ChargePerSize => chargePerSize;
        public int DigitHeight => digitHeight;
        public float FramesPerSecond => framesPerSecond;
        public int DropSpacingPixels => dropSpacingPixels;
        public int DropStepFrames => dropStepFrames;

        // La talla que toca a una Carga: 1–5 la 0, 6–10 la 1… y la última para todo lo que pase de su escalón.
        public int SizeFor(int charge)
        {
            int size = (charge + chargePerSize - 1) / chargePerSize - 1;
            return Mathf.Clamp(size, 0, Mathf.Max(Sizes - 1, 0));
        }

        // Solo el Jugador y el Enemigo lanzan protones (6.1): un neutral no envía.
        public SizeFrames FramesFor(Faction faction, int size)
        {
            SizeFrames[] set = faction == Faction.Enemy ? enemy : player;
            return set != null && size >= 0 && size < set.Length ? set[size] : default;
        }

        public Sprite Digit(int value) => digits[value];
        public int Advance(int value) => digitAdvance[value];

        public int BagRadiusPixels(int size) =>
            bagRadiusPixels != null && size >= 0 && size < bagRadiusPixels.Length ? bagRadiusPixels[size] : 0;

        // Para NodeArtAssets y los tests: el editor escribe aquí lo que copia ArtBridge.
        public void Configure(SizeFrames[] playerFrames, SizeFrames[] enemyFrames, Sprite[] digitSprites, int[] advance,
            int stepCharge, int[] bagRadius)
        {
            player = playerFrames ?? Array.Empty<SizeFrames>();
            enemy = enemyFrames ?? Array.Empty<SizeFrames>();
            digits = digitSprites;
            digitAdvance = advance;
            chargePerSize = Mathf.Max(1, stepCharge);
            bagRadiusPixels = bagRadius;
        }
    }
}
