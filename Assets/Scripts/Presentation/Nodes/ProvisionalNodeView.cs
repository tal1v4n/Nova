using Nova.Core;
using Nova.Gameplay;
using Nova.Utils;
using TMPro;
using UnityEngine;

namespace Nova.Presentation
{
    // T1-06b: vista provisional del nodo. Un círculo del color de su facción (14.4) con la
    // Energía encima en Jersey 10. Desde T1-12 el Asteroide es un círculo más pequeño, para que
    // el encendido se vea sin leer números. Primitiva de la regla 4 de T.2: no es arte, es desechable
    // y la sustituye NodeVisual en T6-01. Solo lee eventos de NodeController, nunca escribe en él.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class ProvisionalNodeView : MonoBehaviour
    {
        // Paleta funcional de 14.4.
        static readonly Color32 PlayerColor = new Color32(0x3F, 0xD7, 0xF5, 0xFF);
        static readonly Color32 EnemyColor = new Color32(0xF2, 0x45, 0x6B, 0xFF);
        static readonly Color32 NeutralColor = new Color32(0x8A, 0x93, 0xA6, 0xFF);
        static readonly Color32 LabelColor = new Color32(0x0B, 0x0E, 0x1A, 0xFF); // fondo profundo

        const int PixelsPerUnit = 64; // T0-04

        // Jersey 10: rejilla de 75 unidades en un em de 1400, 18.67 px por em (NOVA_Arte_Tecnica.md).
        const float NativeEmPixels = 1400f / 75f;

        // TextMeshPro en mundo mide el em en fontSize × 0.1 unidades.
        const float WorldTextScale = 0.1f;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Diámetro del círculo de un nodo encendido, en píxeles. La huella de un nodo va de 38 a 77 px (A.8 #4).")]
        [SerializeField, Range(38, 77)] int diameterPixels = 64;

        [Tooltip("Diámetro del círculo del Asteroide, en píxeles. Menor que el encendido para que el encendido se vea.")]
        [SerializeField, Range(38, 77)] int asteroidDiameterPixels = 48;

        [Tooltip("Múltiplo entero del tamaño nativo de Jersey 10: un tamaño intermedio se interpola.")]
        [SerializeField, Range(1, 4)] int textScale = 2;

        NodeController node;
        SpriteRenderer body;
        TextMeshPro label;
        bool subscribed;

        internal SpriteRenderer Body { get { EnsureBuilt(); return body; } }
        internal TextMeshPro Label { get { EnsureBuilt(); return label; } }

        void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        // Initialize no emite eventos: quien crea el nodo lo inicializa antes de su primer frame.
        void Start()
        {
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        public void Refresh()
        {
            EnsureBuilt();

            bool visible = Node.IsInitialized;
            body.enabled = visible;
            label.enabled = visible;
            if (!visible)
                return;

            body.color = ColorOf(Node.Faction);
            body.sprite = CircleSprite(Node.Type == NodeType.Asteroid ? asteroidDiameterPixels : diameterPixels);
            label.text = Node.Energy.ToString();
        }

        internal static Color32 ColorOf(Faction faction)
        {
            switch (faction)
            {
                case Faction.Player: return PlayerColor;
                case Faction.Enemy: return EnemyColor;
                default: return NeutralColor;
            }
        }

        internal static float FontSizeFor(int scale)
        {
            return NativeEmPixels * scale / PixelsPerUnit / WorldTextScale;
        }

        internal void Subscribe()
        {
            if (subscribed)
                return;
            Node.OnFactionChanged += HandleFactionChanged;
            Node.OnEnergyChanged += HandleEnergyChanged;
            Node.OnTypeChanged += HandleTypeChanged;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (!subscribed)
                return;
            Node.OnFactionChanged -= HandleFactionChanged;
            Node.OnEnergyChanged -= HandleEnergyChanged;
            Node.OnTypeChanged -= HandleTypeChanged;
            subscribed = false;
        }

        void HandleFactionChanged(Faction previous, Faction current) => Refresh();
        void HandleEnergyChanged(int previous, int current) => Refresh();
        void HandleTypeChanged(NodeType previous, NodeType current) => Refresh();

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();

        void EnsureBuilt()
        {
            if (body != null && label != null)
                return;

            var bodyObject = new GameObject("ProvisionalBody");
            bodyObject.transform.SetParent(transform, false);
            body = bodyObject.AddComponent<SpriteRenderer>();
            body.sprite = CircleSprite(diameterPixels);
            body.sortingOrder = 0;

            var labelObject = new GameObject("ProvisionalEnergy");
            labelObject.transform.SetParent(transform, false);
            label = labelObject.AddComponent<TextMeshPro>();
            if (font != null)
                label.font = font;
            else
                Debug.LogWarning($"[Nova] {name}: ProvisionalNodeView sin font asset; asigna Jersey10 Raster56.", this);
            label.fontSize = FontSizeFor(textScale);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = LabelColor;
            label.rectTransform.sizeDelta = new Vector2(diameterPixels, diameterPixels) / PixelsPerUnit;
            label.sortingOrder = 1;
        }

        // Disco blanco de alfa binario y filtro Point, teñido por SpriteRenderer.color.
        internal static Sprite CircleSprite(int diameter) => PrimitiveSprites.Disc(diameter);
    }
}
