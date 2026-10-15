using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Systems;
using Nova.Utils;
using UnityEngine;

namespace Nova.Presentation
{
    // T5-04: power-up provisional. Un halo detrás del Asteroide marcado, en el color del power-up
    // (14.4), y una línea en la consola cuando se recoge o se apaga. Primitiva de la regla 4 de T.2:
    // la sustituyen FX-DROP y FX-BUFF en T6-07. Solo lee PowerUpSpawner.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class ProvisionalPowerUpView : MonoBehaviour
    {
        // Paleta funcional de 14.4.
        static readonly Color32 CosmicRayColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        static readonly Color32 HydrogenCloudColor = new Color32(0xFF, 0x8F, 0xD0, 0xFF);

        [SerializeField] PowerUpSpawner spawner;

        [Tooltip("Diámetro del halo en píxeles: algo más que el Asteroide provisional (48 px).")]
        [SerializeField, Range(48, 96)] int haloPixels = 64;

        SpriteRenderer halo;
        bool subscribed;

        internal SpriteRenderer Halo { get { EnsureBuilt(); return halo; } }

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void LateUpdate()
        {
            Refresh();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(PowerUpSpawner powerUpSpawner)
        {
            Unsubscribe();
            spawner = powerUpSpawner;
            if (isActiveAndEnabled)
                Subscribe();
        }

        public void Refresh()
        {
            EnsureBuilt();
            NodeController node = spawner != null ? spawner.Marked : null;
            halo.enabled = node != null;
            if (node == null)
                return;
            halo.transform.position = node.transform.position;
            halo.color = ColorOf(spawner.MarkedPowerUp.Type);
        }

        internal static Color32 ColorOf(PowerUpType type) =>
            type == PowerUpType.CosmicRay ? CosmicRayColor : HydrogenCloudColor;

        void Subscribe()
        {
            if (subscribed || spawner == null)
                return;
            spawner.OnCollected += HandleCollected;
            spawner.OnExpired += HandleExpired;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed)
                return;
            if (spawner != null)
            {
                spawner.OnCollected -= HandleCollected;
                spawner.OnExpired -= HandleExpired;
            }
            subscribed = false;
        }

        void HandleCollected(NodeController node, Faction faction, PowerUpDefinition powerUp) =>
            Debug.Log($"[Nova] {faction} recoge {powerUp.Type}: {powerUp.Duration} s.");

        void HandleExpired(NodeController node, PowerUpDefinition powerUp) =>
            Debug.Log($"[Nova] {powerUp.Type} se apaga sin recoger.");

        void EnsureBuilt()
        {
            if (halo != null)
                return;
            var go = new GameObject("ProvisionalPowerUpHalo");
            go.transform.SetParent(transform, false);
            halo = go.AddComponent<SpriteRenderer>();
            halo.sprite = PrimitiveSprites.Disc(haloPixels);
            halo.sortingOrder = -2; // Detrás del cuerpo (0) y del halo de Infectado (-1)
            halo.enabled = false;
        }
    }
}
