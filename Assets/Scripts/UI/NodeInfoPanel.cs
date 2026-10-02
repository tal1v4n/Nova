using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Utils;
using TMPro;
using UnityEngine;

namespace Nova.UI
{
    // Panel de clic derecho (13.3, 13.4, T2-03): tipo y nivel, Energía / Tope, velocidad de ataque
    // y el botón de mejora o NIVEL MÁXIMO. Solo dibuja: el estado es de InfoPanelState, el clic lo
    // resuelve InputRouter y el botón se valida con NodeController.CheckUpgrade, sin efectos.
    //
    // Los puntos 5 y 6 de 13.3 —reconversión y rueda de poderes— llegan con T2-07 y T3-03. Fondo,
    // botón y texto son primitivas de la regla 4 de T.2; la skin final es de T6-09.
    [DefaultExecutionOrder(50)] // UI, 15.10
    [DisallowMultipleComponent]
    public sealed class NodeInfoPanel : MonoBehaviour
    {
        internal enum ButtonLook { Hidden, Enabled, MissingEnergy, MaxLevel }

        static readonly Color32 BackgroundColor = new Color32(0x14, 0x1A, 0x2A, 0xF0);
        static readonly Color32 TextColor = new Color32(0xE6, 0xEC, 0xF5, 0xFF);
        static readonly Color32 EnabledColor = new Color32(0x3F, 0xD7, 0xF5, 0xFF);   // Jugador, 14.4
        static readonly Color32 DisabledColor = new Color32(0x2A, 0x30, 0x42, 0xFF);
        static readonly Color32 ButtonTextColor = new Color32(0x0B, 0x0E, 0x1A, 0xFF);
        const string MissingCostColor = "#F2456B"; // el rojo de 14.4: coste que no se alcanza

        // Jersey 10 (NOVA_Arte_Tecnica.md): 18.67 px por em; TextMeshPro en mundo mide el em en
        // fontSize × 0.1 unidades. El panel usa el tamaño nativo.
        const float NativeEmPixels = 1400f / 75f;
        const float WorldTextScale = 0.1f;

        // Por encima de nodos, protones y menú de encendido.
        const int SortingOrder = 30;

        // Margen interior y altura de línea: 8 y 24 px de la referencia.
        const float Padding = 0.125f;
        const float LineHeight = 0.375f;

        [SerializeField] InfoPanelState panel;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Textos de la UI (T.7 #39).")]
        [SerializeField] UiText text;

        [Tooltip("Facción que mira el panel: el botón solo sale en sus nodos.")]
        [SerializeField] Faction viewer = Faction.Player;

        GameObject root;
        TextMeshPro[] lines;
        SpriteRenderer button;
        TextMeshPro buttonLabel;

        NodeController watched;
        bool subscribed;

        internal bool IsVisible => root != null && root.activeSelf;
        internal ButtonLook Button { get; private set; }
        internal string Line(int index) { EnsureBuilt(); return lines[index].enabled ? lines[index].text : null; }
        internal string ButtonText { get { EnsureBuilt(); return buttonLabel.text; } }
        internal Vector2 ButtonPosition { get { EnsureBuilt(); return button.transform.position; } }

        void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(InfoPanelState state, TMP_FontAsset fontAsset, UiText uiText)
        {
            Unsubscribe();
            panel = state;
            font = fontAsset;
            text = uiText;
        }

        internal void Subscribe()
        {
            if (subscribed || panel == null)
                return;
            panel.OnChanged += Refresh;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (subscribed && panel != null)
                panel.OnChanged -= Refresh;
            subscribed = false;
            Watch(null);
        }

        public void Refresh()
        {
            EnsureBuilt();

            bool open = panel != null && panel.IsOpen && panel.Node.IsInitialized;
            Watch(open ? panel.Node : null);
            root.SetActive(open);
            if (!open)
                return;

            root.transform.position = panel.Center;
            NodeController node = panel.Node;
            UiText t = Text;

            if (node.Type == NodeType.Asteroid)
            {
                // 13.4: el Asteroide solo enseña que duerme y lo que cuesta encenderlo.
                SetLines(t.TypeName(node.Type), t.EnergyLine(node.Energy, node.EnergyCap), t.Sleeping,
                    t.IgnitionCost(IgnitionCostOf(node)));
                PaintButton(ButtonLook.Hidden, null);
                return;
            }

            SetLines(t.Title(node.Type, node.Level), t.EnergyLine(node.Energy, node.EnergyCap), AttackSpeed(node, t), null);
            PaintUpgrade(node, t);
        }

        // 13.4: un solo campo que significa algo distinto en cada tipo. El del Púlsar depende
        // del poder que carga y llega con PulsarCaster (T3-02).
        static string AttackSpeed(NodeController node, UiText t)
        {
            NodeLevelData data = node.CurrentLevelData;
            switch (node.Type)
            {
                case NodeType.Star:
                    return data.GenerationInterval > 0f ? t.Generation(1f / data.GenerationInterval) : null;
                case NodeType.Magnetar:
                    return t.FireRate(data.FireRate);
                default:
                    return null;
            }
        }

        // 13.3 punto 4: NIVEL MÁXIMO al máximo; el botón, solo en un nodo propio, deshabilitado
        // y con el coste en rojo si falta Energía, nunca oculto.
        void PaintUpgrade(NodeController node, UiText t)
        {
            ActionCheck check = node.CheckUpgrade(viewer);
            if (node.Level >= node.Definition.MaxLevel)
                PaintButton(ButtonLook.MaxLevel, t.MaxLevel);
            else if (node.Faction != viewer)
                PaintButton(ButtonLook.Hidden, null);
            else if (check.IsAllowed)
                PaintButton(ButtonLook.Enabled, t.Upgrade(check.Cost.ToString()));
            else
                PaintButton(ButtonLook.MissingEnergy, t.Upgrade($"<color={MissingCostColor}>{check.Cost}</color>"));
        }

        void PaintButton(ButtonLook look, string label)
        {
            Button = look;
            bool isButton = look == ButtonLook.Enabled || look == ButtonLook.MissingEnergy;
            button.enabled = isButton;
            button.color = look == ButtonLook.Enabled ? EnabledColor : DisabledColor;
            buttonLabel.enabled = look != ButtonLook.Hidden;
            buttonLabel.text = label ?? string.Empty;
            buttonLabel.color = look == ButtonLook.Enabled ? ButtonTextColor : TextColor;
        }

        void SetLines(params string[] values)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string value = i < values.Length ? values[i] : null;
                lines[i].enabled = value != null;
                lines[i].text = value ?? string.Empty;
            }
        }

        static int IgnitionCostOf(NodeController node)
        {
            NodeIgnition ignition = node.GetComponent<NodeIgnition>();
            return ignition != null ? ignition.Check(node.Faction, NodeType.Star).Cost : 0;
        }

        UiText Text => text != null ? text : throw new System.InvalidOperationException("NodeInfoPanel necesita UiText.");

        // Lo que cambia con el panel abierto: Energía, nivel, tipo y dueño.
        void Watch(NodeController node)
        {
            if (watched == node)
                return;
            if (watched != null)
            {
                watched.OnEnergyChanged -= HandleIntChanged;
                watched.OnLevelChanged -= HandleIntChanged;
                watched.OnTypeChanged -= HandleTypeChanged;
                watched.OnFactionChanged -= HandleFactionChanged;
            }
            watched = node;
            if (watched != null)
            {
                watched.OnEnergyChanged += HandleIntChanged;
                watched.OnLevelChanged += HandleIntChanged;
                watched.OnTypeChanged += HandleTypeChanged;
                watched.OnFactionChanged += HandleFactionChanged;
            }
        }

        void HandleIntChanged(int previous, int current) => Refresh();
        void HandleTypeChanged(NodeType previous, NodeType current) => Refresh();
        void HandleFactionChanged(Faction previous, Faction current) => Refresh();

        void EnsureBuilt()
        {
            if (root != null)
                return;

            root = new GameObject("NodeInfoPanel");
            root.transform.SetParent(transform, false);

            int ppu = PrimitiveSprites.PixelsPerUnit;
            var background = new GameObject("Background").AddComponent<SpriteRenderer>();
            background.transform.SetParent(root.transform, false);
            background.sprite = PrimitiveSprites.Rect(Mathf.RoundToInt(InfoPanelState.Width * ppu), Mathf.RoundToInt(InfoPanelState.Height * ppu));
            background.color = BackgroundColor;
            background.sortingOrder = SortingOrder;

            // Tres líneas de arriba abajo; la cuarta fila es la del botón.
            lines = new TextMeshPro[4];
            float top = InfoPanelState.Height / 2f - Padding - LineHeight / 2f;
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = Label(root.transform, new Vector2(0f, top - i * LineHeight), TextAlignmentOptions.Left,
                    InfoPanelState.Width - 2f * Padding);
            }

            button = new GameObject("UpgradeButton").AddComponent<SpriteRenderer>();
            button.transform.SetParent(root.transform, false);
            button.transform.localPosition = InfoPanelState.ButtonOffset;
            button.sprite = PrimitiveSprites.Rect(Mathf.RoundToInt(InfoPanelState.ButtonSize.x * ppu), Mathf.RoundToInt(InfoPanelState.ButtonSize.y * ppu));
            button.sortingOrder = SortingOrder + 1;
            buttonLabel = Label(button.transform, Vector2.zero, TextAlignmentOptions.Center, InfoPanelState.ButtonSize.x);
            buttonLabel.sortingOrder = SortingOrder + 2;

            root.SetActive(false);
        }

        TextMeshPro Label(Transform parent, Vector2 position, TextAlignmentOptions alignment, float width)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var label = go.AddComponent<TextMeshPro>();
            if (font != null)
                label.font = font;
            else
                Debug.LogWarning($"[Nova] {name}: NodeInfoPanel sin font asset; asigna Jersey10 Raster56.", this);
            label.fontSize = NativeEmPixels / PrimitiveSprites.PixelsPerUnit / WorldTextScale;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = true;
            label.color = TextColor;
            label.rectTransform.sizeDelta = new Vector2(width, LineHeight);
            label.sortingOrder = SortingOrder + 1;
            return label;
        }
    }
}
