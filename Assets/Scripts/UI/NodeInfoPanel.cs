using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Utils;
using TMPro;
using UnityEngine;

namespace Nova.UI
{
    // Panel de clic derecho (13.3, 13.4, T2-03): tipo y nivel, Energía / Tope, velocidad de ataque,
    // el botón de mejora o NIVEL MÁXIMO y, desde T2-07, los dos destinos de reconversión. Solo
    // dibuja: el estado es de InfoPanelState, el clic lo resuelve InputRouter y los botones se
    // validan con NodeController.CheckUpgrade y NodeConverter.Check, sin efectos.
    //
    // El punto 6 de 13.3 —la rueda de poderes— lo dibuja PulsarPowerWheel (T3-07) en las tres últimas
    // filas, sobre el mismo fondo. Fondo, botones y texto son primitivas de la regla 4 de T.2; la
    // skin final es de T6-09.
    [DefaultExecutionOrder(50)] // UI, 15.10
    [DisallowMultipleComponent]
    public sealed class NodeInfoPanel : MonoBehaviour
    {
        // Disabled: bloqueado por algo que no es la Energía (congelado, reconvirtiendo); el coste
        // sale sin rojo. Unavailable: el tipo aún no existe en la partida (18 #29).
        internal enum ButtonLook { Hidden, Enabled, MissingEnergy, Disabled, Unavailable, MaxLevel }

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
        SpriteRenderer[] conversionButtons;
        TextMeshPro[] conversionLabels;
        ButtonLook[] conversionLooks;

        NodeController watched;
        bool subscribed;

        internal bool IsVisible => root != null && root.activeSelf;
        internal ButtonLook Button { get; private set; }
        internal string Line(int index) { EnsureBuilt(); return lines[index].enabled ? lines[index].text : null; }
        internal string ButtonText { get { EnsureBuilt(); return buttonLabel.text; } }
        internal Vector2 ButtonPosition { get { EnsureBuilt(); return button.transform.position; } }
        internal ButtonLook ConversionButton(int index) { EnsureBuilt(); return conversionLooks[index]; }
        internal string ConversionText(int index) { EnsureBuilt(); return conversionLabels[index].enabled ? conversionLabels[index].text : null; }
        internal Vector2 ConversionPosition(int index) { EnsureBuilt(); return conversionButtons[index].transform.position; }

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
                PaintConversion(null, t);
                return;
            }

            SetLines(t.Title(node.Type, node.Level), t.EnergyLine(node.Energy, node.EnergyCap), AttackSpeed(node, t), null);
            PaintUpgrade(node, t);
            PaintConversion(node, t);
        }

        // 13.4: un solo campo que significa algo distinto en cada tipo. El del Púlsar es el drenaje
        // del poder que carga o tiene cargado, y en reposo queda vacío (T.7 #49, T3-02).
        static string AttackSpeed(NodeController node, UiText t)
        {
            NodeLevelData data = node.CurrentLevelData;
            switch (node.Type)
            {
                case NodeType.Star:
                    return data.GenerationInterval > 0f ? t.Generation(1f / data.GenerationInterval) : null;
                case NodeType.Magnetar:
                    return t.FireRate(data.FireRate);
                case NodeType.Pulsar:
                    return node.TryGetComponent(out PulsarCaster caster) && caster.Power != null
                        ? t.Charge(caster.Power.DrainRate)
                        : null;
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
            else
            {
                ButtonLook look = LookOf(check);
                PaintButton(look, t.Upgrade(CostText(look, check.Cost)));
            }
        }

        void PaintButton(ButtonLook look, string label)
        {
            Button = look;
            Paint(button, buttonLabel, look, label);
        }

        // 13.3 punto 5: solo en un nodo propio encendido, los dos destinos con su coste. Mientras
        // dura la reconversión, los botones dejan sitio a «Reconvirtiendo a …» en la fila del rótulo.
        void PaintConversion(NodeController node, UiText t)
        {
            NodeConverter converter = node != null ? node.GetComponent<NodeConverter>() : null;
            bool shown = converter != null && node.Faction == viewer;
            NodeType[] destinations = shown ? InfoPanelState.ConversionTargets(node.Type) : System.Array.Empty<NodeType>();

            string header = null;
            if (shown)
                header = converter.IsConverting ? t.Converting(converter.TargetType) : t.ConvertHeader;
            lines[4].enabled = header != null;
            lines[4].text = header ?? string.Empty;

            for (int i = 0; i < conversionButtons.Length; i++)
            {
                if (!shown || converter.IsConverting || i >= destinations.Length)
                {
                    PaintConversionButton(i, ButtonLook.Hidden, null);
                    continue;
                }

                NodeType target = destinations[i];
                ActionCheck check = converter.Check(viewer, target);
                ButtonLook look = LookOf(check);
                string label = look == ButtonLook.Unavailable ? t.TypeName(target) : t.Convert(target, CostText(look, check.Cost));
                PaintConversionButton(i, look, label);
            }
        }

        void PaintConversionButton(int index, ButtonLook look, string label)
        {
            conversionLooks[index] = look;
            Paint(conversionButtons[index], conversionLabels[index], look, label);
        }

        static ButtonLook LookOf(ActionCheck check)
        {
            if (check.IsAllowed)
                return ButtonLook.Enabled;
            switch (check.Block)
            {
                case ActionBlock.InsufficientEnergy: return ButtonLook.MissingEnergy;
                case ActionBlock.Unavailable: return ButtonLook.Unavailable;
                default: return ButtonLook.Disabled;
            }
        }

        // 13.3: el coste va en rojo solo cuando lo que falta es Energía.
        static string CostText(ButtonLook look, int cost) =>
            look == ButtonLook.MissingEnergy ? $"<color={MissingCostColor}>{cost}</color>" : cost.ToString();

        static void Paint(SpriteRenderer body, TextMeshPro label, ButtonLook look, string text)
        {
            body.enabled = look != ButtonLook.Hidden && look != ButtonLook.MaxLevel;
            body.color = look == ButtonLook.Enabled ? EnabledColor : DisabledColor;
            label.enabled = look != ButtonLook.Hidden;
            label.text = text ?? string.Empty;
            label.color = look == ButtonLook.Enabled ? ButtonTextColor : TextColor;
        }

        // Las cuatro primeras filas de texto; la quinta, el rótulo de reconversión, es de PaintConversion.
        void SetLines(params string[] values)
        {
            for (int i = 0; i < 4; i++)
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
                watched.OnStateFlagsChanged -= HandleFlagsChanged;
                if (watched.TryGetComponent(out PulsarCaster previousCaster))
                    previousCaster.OnStateChanged -= HandleChargeChanged;
            }
            watched = node;
            if (watched != null)
            {
                watched.OnEnergyChanged += HandleIntChanged;
                watched.OnLevelChanged += HandleIntChanged;
                watched.OnTypeChanged += HandleTypeChanged;
                watched.OnFactionChanged += HandleFactionChanged;
                watched.OnStateFlagsChanged += HandleFlagsChanged;
                if (watched.TryGetComponent(out PulsarCaster caster))
                    caster.OnStateChanged += HandleChargeChanged;
            }
        }

        void HandleIntChanged(int previous, int current) => Refresh();
        void HandleTypeChanged(NodeType previous, NodeType current) => Refresh();
        void HandleFactionChanged(Faction previous, Faction current) => Refresh();
        // Reconvirtiendo empieza, termina o se cancela; desde T3-04, también Congelado.
        void HandleFlagsChanged(NodeStateFlags previous, NodeStateFlags current) => Refresh();
        // La carga empieza, se cancela o se pierde: la línea de 13.4 del Púlsar cambia (T3-02).
        void HandleChargeChanged(PowerChargeState previous, PowerChargeState current) => Refresh();

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

            // Cinco filas de texto de arriba abajo. La cuarta comparte sitio con el botón de mejora
            // (el Asteroide la usa para el coste de encendido) y la quinta es el rótulo de
            // reconversión; la sexta es la de sus dos botones.
            lines = new TextMeshPro[5];
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = Label(root.transform, new Vector2(0f, InfoPanelState.RowY(i)), TextAlignmentOptions.Left,
                    InfoPanelState.Width - 2f * Padding);
            }

            button = new GameObject("UpgradeButton").AddComponent<SpriteRenderer>();
            button.transform.SetParent(root.transform, false);
            button.transform.localPosition = InfoPanelState.ButtonOffset;
            button.sprite = PrimitiveSprites.Rect(Mathf.RoundToInt(InfoPanelState.ButtonSize.x * ppu), Mathf.RoundToInt(InfoPanelState.ButtonSize.y * ppu));
            button.sortingOrder = SortingOrder + 1;
            buttonLabel = Label(button.transform, Vector2.zero, TextAlignmentOptions.Center, InfoPanelState.ButtonSize.x);
            buttonLabel.sortingOrder = SortingOrder + 2;

            Vector2 conversionSize = InfoPanelState.ConversionButtonSize;
            conversionButtons = new SpriteRenderer[InfoPanelState.ConversionOffsets.Length];
            conversionLabels = new TextMeshPro[conversionButtons.Length];
            conversionLooks = new ButtonLook[conversionButtons.Length];
            for (int i = 0; i < conversionButtons.Length; i++)
            {
                conversionButtons[i] = new GameObject($"ConversionButton{i}").AddComponent<SpriteRenderer>();
                conversionButtons[i].transform.SetParent(root.transform, false);
                conversionButtons[i].transform.localPosition = InfoPanelState.ConversionOffsets[i];
                conversionButtons[i].sprite = PrimitiveSprites.Rect(Mathf.RoundToInt(conversionSize.x * ppu), Mathf.RoundToInt(conversionSize.y * ppu));
                conversionButtons[i].sortingOrder = SortingOrder + 1;
                conversionLabels[i] = Label(conversionButtons[i].transform, Vector2.zero, TextAlignmentOptions.Center, conversionSize.x);
                conversionLabels[i].sortingOrder = SortingOrder + 2;
            }

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
