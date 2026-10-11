using Nova.Core;
using Nova.Gameplay;
using Nova.Utils;
using UnityEngine;

namespace Nova.Presentation
{
    // T3-02: barra de carga provisional bajo el Púlsar. Un fondo oscuro y un relleno del color del
    // poder (14.4) que crece de izquierda a derecha con energíaInvertida / costeTotal (7.1). Se ve
    // en los Púlsares de los dos bandos: la barra del enemigo es el contrajuego entero (7.5).
    // Pausada, el relleno baja a la mitad de opacidad. Primitiva de la regla 4 de T.2: la sustituye
    // ART-UI-CHARGE en T6. Solo lee PulsarCaster.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController), typeof(PulsarCaster))]
    public sealed class ProvisionalPulsarView : MonoBehaviour
    {
        const int PixelsPerUnit = 64; // T0-04

        static readonly Color32 BackColor = new Color32(0x0B, 0x0E, 0x1A, 0xFF); // fondo profundo

        [Tooltip("Ancho de la barra en píxeles.")]
        [SerializeField, Range(16, 64)] int widthPixels = 48;

        [Tooltip("Alto de la barra en píxeles.")]
        [SerializeField, Range(2, 8)] int heightPixels = 4;

        [Tooltip("Centro de la barra bajo el centro del nodo, en píxeles: por fuera del círculo de 64.")]
        [SerializeField, Range(32, 64)] int offsetPixels = 40;

        NodeController node;
        PulsarCaster caster;
        SpriteRenderer back;
        SpriteRenderer fill;

        internal SpriteRenderer Back { get { EnsureBuilt(); return back; } }
        internal SpriteRenderer Fill { get { EnsureBuilt(); return fill; } }

        void LateUpdate()
        {
            Refresh();
        }

        public void Refresh()
        {
            EnsureBuilt();

            bool visible = Node.IsInitialized && Node.Type == NodeType.Pulsar && Caster.HasPowerCharge;
            back.enabled = visible;
            fill.enabled = visible && Caster.Invested > 0;
            if (!visible)
                return;

            float width = widthPixels / (float)PixelsPerUnit;
            float filled = FilledPixels(Caster.Progress, widthPixels) / (float)PixelsPerUnit;
            fill.transform.localPosition = new Vector3(-width / 2f + filled / 2f, back.transform.localPosition.y, 0f);
            fill.transform.localScale = new Vector3(filled * PixelsPerUnit, heightPixels, 1f);

            Color color = Caster.Power.BeamColor;
            color.a = Caster.IsPaused ? 0.5f : 1f;
            fill.color = color;
        }

        // Píxeles enteros de relleno: la barra no se interpola dentro de un píxel.
        internal static int FilledPixels(float progress, int width)
        {
            return Mathf.Clamp(Mathf.FloorToInt(progress * width), 0, width);
        }

        void EnsureBuilt()
        {
            if (back != null && fill != null)
                return;

            float y = -offsetPixels / (float)PixelsPerUnit;

            back = new GameObject("ProvisionalChargeBack").AddComponent<SpriteRenderer>();
            back.transform.SetParent(transform, false);
            back.transform.localPosition = new Vector3(0f, y, 0f);
            back.transform.localScale = new Vector3(widthPixels, heightPixels, 1f);
            back.sprite = PrimitiveSprites.Rect(1, 1);
            back.color = BackColor;
            back.sortingOrder = 2; // por encima del nodo y de su número
            back.enabled = false;

            fill = new GameObject("ProvisionalChargeFill").AddComponent<SpriteRenderer>();
            fill.transform.SetParent(transform, false);
            fill.transform.localPosition = new Vector3(0f, y, 0f);
            fill.sprite = PrimitiveSprites.Rect(1, 1);
            fill.sortingOrder = 3;
            fill.enabled = false;
        }

        // Se resuelven a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
        PulsarCaster Caster => caster != null ? caster : caster = GetComponent<PulsarCaster>();
    }
}
