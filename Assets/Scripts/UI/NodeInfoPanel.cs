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
    // el coste de la mejora o NIVEL MÁXIMO y, desde T2-07, los dos destinos de reconversión. Solo
    // dibuja: el estado es de InfoPanelState, el clic lo resuelve InputRouter y los botones se
    // validan con NodeConverter.Check, sin efectos. Desde T6-15 la mejora es texto: se mejora desde
    // el icono sobre el nodo (13.13).
    //
    // El punto 6 de 13.3 —la rueda de poderes— lo dibuja PulsarPowerWheel (T3-07) en las tres últimas
    // filas, sobre el mismo fondo.
    //
    // T6-09: con PanelArt, la skin final (A.6). El fondo es «La sonda» —una lectura en trama con las esquinas del
    // bando y un cable de plasma hasta el nodo— y los botones de reconversión, «El barrido». El alto sigue a las
    // filas (InfoPanelState.Layout). Se compone en un PixelCanvas con PanelSkin y el texto va en TextMeshPro con
    // los colores de los .json. Sin PanelArt, todo sigue siendo las primitivas de la regla 4 de T.2.
    [DefaultExecutionOrder(50)] // UI, 15.10
    [DisallowMultipleComponent]
    public sealed class NodeInfoPanel : MonoBehaviour
    {
        // Disabled: bloqueado por algo que no es la Energía (congelado, reconvirtiendo); el coste
        // sale sin rojo. Unavailable: el tipo aún no existe en la partida (18 #29).
        internal enum ButtonLook { Hidden, Enabled, MissingEnergy, Disabled, Unavailable }

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

        // Por encima de nodos, protones y menú de encendido. El cable va debajo del panel.
        const int SortingOrder = 30;

        // Margen interior y altura de línea: 8 y 24 px de la referencia.
        const float Padding = 0.125f;
        const float LineHeight = 0.375f;
        const float Ppu = PrimitiveSprites.PixelsPerUnit;

        [SerializeField] InfoPanelState panel;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Textos de la UI (T.7 #39).")]
        [SerializeField] UiText text;

        [Tooltip("Opcional (T6-09): el arte del panel. Sin él, primitivas.")]
        [SerializeField] PanelArt art;

        [Tooltip("Facción que mira el panel: el coste de la mejora y los botones solo salen en sus nodos.")]
        [SerializeField] Faction viewer = Faction.Player;

        GameObject root;
        SpriteRenderer background;
        int backgroundRows = -1;
        TextMeshPro[] lines;
        SpriteRenderer[] conversionButtons;
        TextMeshPro[] conversionLabels;
        ButtonLook[] conversionLooks;

        // Con arte.
        SpriteRenderer panelView;
        CanvasSprite panelCanvas;
        SpriteRenderer tetherView;
        CanvasSprite tetherCanvas;
        SpriteRenderer[] conversionViews;
        CanvasSprite[] conversionCanvases;
        TextMeshPro[] conversionNames;
        TextMeshPro[] conversionCosts;
        int[] conversionCosts_;
        float[] hoverSince;
        int drawnPanelKey = int.MinValue;
        int drawnTetherKey = int.MinValue;
        readonly int[] drawnButtonKey = { int.MinValue, int.MinValue };

        NodeController watched;
        bool subscribed;

        internal bool IsVisible => root != null && root.activeSelf;
        internal string Line(int index) { EnsureBuilt(); return lines[index].enabled ? lines[index].text : null; }
        internal ButtonLook ConversionButton(int index) { EnsureBuilt(); return conversionLooks[index]; }
        internal string ConversionText(int index)
        {
            EnsureBuilt();
            if (HasArt)
                return conversionNames[index].enabled ? conversionNames[index].text + (conversionCosts[index].enabled ? " · " + conversionCosts[index].text : "") : null;
            return conversionLabels[index].enabled ? conversionLabels[index].text : null;
        }
        internal Vector2 ConversionPosition(int index) { EnsureBuilt(); return conversionButtons[index].transform.position; }
        internal bool HasArt => art != null && art.HasPanel;
        internal SpriteRenderer PanelView { get { EnsureBuilt(); return panelView; } }
        internal PixelCanvas PanelCanvas => panelCanvas?.Canvas;
        internal PixelCanvas ConversionCanvas(int index) { EnsureBuilt(); return conversionCanvases[index]?.Canvas; }
        internal bool ConversionArtVisible(int index) { EnsureBuilt(); return conversionViews[index].enabled; }
        internal SpriteRenderer TetherView { get { EnsureBuilt(); return tetherView; } }
        internal TextMeshPro LineLabel(int index) { EnsureBuilt(); return lines[index]; }

        void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void OnDestroy()
        {
            panelCanvas?.Destroy();
            tetherCanvas?.Destroy();
            if (conversionCanvases != null)
                foreach (CanvasSprite c in conversionCanvases)
                    c?.Destroy();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(InfoPanelState state, TMP_FontAsset fontAsset, UiText uiText, PanelArt panelArt = null)
        {
            Unsubscribe();
            panel = state;
            font = fontAsset;
            text = uiText;
            art = panelArt;
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
            InfoPanelState.PanelLayout layout = panel.Layout;
            LayOut(layout);

            if (node.Type == NodeType.Asteroid)
            {
                // 13.4: el Asteroide solo enseña que duerme y lo que cuesta encenderlo.
                SetLines(t.TypeName(node.Type), t.EnergyLine(node.Energy, node.EnergyCap), t.Sleeping,
                    t.IgnitionCost(IgnitionCostOf(node)));
                PaintConversion(null, t);
            }
            else
            {
                SetLines(t.Title(node.Type, node.Level), t.EnergyLine(node.Energy, node.EnergyCap), AttackSpeed(node, t),
                    UpgradeLine(node, t));
                PaintConversion(node, t);
            }
            ColorLines(node);
            drawnPanelKey = int.MinValue;
            drawnTetherKey = int.MinValue;
            drawnButtonKey[0] = drawnButtonKey[1] = int.MinValue;
            if (HasArt)
                Animate(Time.unscaledTime);
        }

        // El panel con arte se anima: el testigo de la raya, el pulso del cable y el encima de los botones.
        void LateUpdate()
        {
            if (HasArt && root != null && root.activeSelf && panel != null && panel.IsOpen)
                Animate(Time.unscaledTime);
        }

        // Coloca el fondo y las filas para el alto de este nodo.
        void LayOut(InfoPanelState.PanelLayout layout)
        {
            bool artOn = HasArt;
            background.enabled = !artOn;
            panelView.enabled = artOn;
            tetherView.enabled = artOn;
            if (!artOn && backgroundRows != layout.Rows)
            {
                backgroundRows = layout.Rows;
                background.sprite = PrimitiveSprites.Rect(Mathf.RoundToInt(InfoPanelState.Width * Ppu), layout.HeightPixels);
            }

            lines[0].transform.localPosition = new Vector2(0f, layout.RowY(0));
            lines[1].transform.localPosition = new Vector2(0f, layout.RowY(1));
            lines[2].transform.localPosition = new Vector2(0f, layout.RowY(2));
            lines[3].transform.localPosition = new Vector2(0f, layout.RowY(Mathf.Max(0, layout.UpgradeRow)));
            lines[4].transform.localPosition = new Vector2(0f, layout.RowY(Mathf.Max(0, layout.ConversionHeaderRow)));
            for (int i = 0; i < conversionButtons.Length; i++)
            {
                Vector2 offset = panel.ConversionOffset(i);
                conversionButtons[i].transform.localPosition = offset;
                if (artOn)
                {
                    // La celda del arte: su esquina de arriba a la izquierda en la del botón.
                    Vector2Int size = art.Conversion.size;
                    conversionViews[i].transform.localPosition = offset + new Vector2(-size.x / 2f, size.y / 2f) / Ppu;
                }
            }
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

        // 13.3 punto 4 (T6-15): NIVEL MÁXIMO en cualquier nodo al máximo, sea de quien sea; si no, el coste
        // del nivel siguiente como información, solo en un nodo propio y siempre, también sin Energía
        // suficiente, Congelado o Reconvirtiendo: el jugador necesita saber cuánto le falta.
        string UpgradeLine(NodeController node, UiText t)
        {
            if (node.Level >= node.Definition.MaxLevel)
                return t.MaxLevel;
            if (node.Faction != viewer)
                return null;
            return t.UpgradeCost(node.Definition.UpgradeCostTo(node.Level + 1));
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
                    PaintConversionButton(i, ButtonLook.Hidden, null, NodeType.Star, 0);
                    continue;
                }

                NodeType target = destinations[i];
                ActionCheck check = converter.Check(viewer, target);
                ButtonLook look = LookOf(check);
                string label = look == ButtonLook.Unavailable ? t.TypeName(target) : t.Convert(target, CostText(look, check.Cost));
                PaintConversionButton(i, look, label, target, check.Cost);
                if (HasArt)
                {
                    conversionNames[i].text = t.TypeName(target);
                    conversionCosts[i].text = look == ButtonLook.Unavailable ? string.Empty : check.Cost.ToString();
                }
            }
        }

        void PaintConversionButton(int index, ButtonLook look, string label, NodeType target, int cost)
        {
            conversionLooks[index] = look;
            conversionCosts_[index] = cost;
            bool artOn = HasArt;
            Paint(conversionButtons[index], conversionLabels[index], look, label);
            if (artOn)
            {
                // Con arte, la primitiva se aparta: el botón es su lienzo y sus dos etiquetas.
                conversionButtons[index].enabled = false;
                conversionLabels[index].enabled = false;
            }
            conversionViews[index].enabled = artOn && look != ButtonLook.Hidden;
            conversionNames[index].enabled = artOn && look != ButtonLook.Hidden;
            conversionCosts[index].enabled = artOn && look != ButtonLook.Hidden && look != ButtonLook.Unavailable;
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
            body.enabled = look != ButtonLook.Hidden;
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

        // Con arte, los colores de la lectura (A.6): el título en el del bando, los datos en claro, DORMIDO y
        // NIVEL MÁXIMO en gris y el rótulo de la reconversión, secundario.
        void ColorLines(NodeController node)
        {
            if (!HasArt)
            {
                foreach (TextMeshPro line in lines)
                    line.color = TextColor;
                return;
            }
            PanelArt.PanelPieces p = art.Panel;
            UiText t = Text;
            lines[0].color = p.titleColor[FactionRow(node.Faction)];
            lines[1].color = p.textColor;
            lines[2].color = node.Type == NodeType.Asteroid ? p.mutedColor : p.textColor;
            lines[3].color = lines[3].text == t.MaxLevel ? p.mutedColor : p.textColor;
            lines[4].color = p.labelColor;
        }

        internal static int FactionRow(Faction faction) =>
            faction == Faction.Player ? 0 : faction == Faction.Enemy ? 1 : 2;

        // --- Con arte: el fondo, el cable y los botones ---

        void Animate(float now)
        {
            NodeController node = panel.Node;
            PanelArt.PanelPieces p = art.Panel;
            int frame = Mathf.FloorToInt(now * p.framesPerSecond) % p.pulseFrames;
            InfoPanelState.PanelLayout layout = panel.Layout;
            int faction = FactionRow(node.Faction);

            // El fondo: cambia con las filas, el bando y el parpadeo del testigo.
            int blink = (frame / p.blinkFrames) % 2;
            int panelKey = (layout.Rows * 8 + faction) * 4 + blink + layout.Blocks.Length * 1000;
            if (panelKey != drawnPanelKey)
            {
                drawnPanelKey = panelKey;
                if (panelCanvas == null || panelCanvas.Canvas.Height != layout.HeightPixels)
                {
                    panelCanvas?.Destroy();
                    panelCanvas = new CanvasSprite(p.width, layout.HeightPixels, new Vector2(0.5f, 0.5f), "InfoPanel");
                    panelView.sprite = panelCanvas.Sprite;
                }
                PanelSkin.ComposePanel(panelCanvas.Canvas, p, faction, layout, frame);
                panelCanvas.Upload();
            }

            // El cable: del borde del nodo al costado del panel, en escalón si el panel se desplazó en vertical.
            Vector2 nodePos = node.transform.position;
            Vector2 delta = (panel.Center - nodePos) * Ppu;
            bool toLeft = delta.x < 0f;
            int inner = Mathf.RoundToInt(Mathf.Abs(delta.x)) - p.width / 2;
            int top = Mathf.RoundToInt(-delta.y) - layout.HeightPixels / 2;
            int footprint = node.Definition != null && node.Definition.TryGetArt(node.Level, out NodeLevelArt levelArt)
                ? levelArt.FootprintPixels : 64;
            var tether = new PanelSkin.TetherPath(p, footprint, inner, top, layout.HeightPixels, toLeft);
            RectInt bounds = tether.Bounds;
            int tetherKey = ((frame * 97 + faction) * 1009 + bounds.x * 31 + bounds.y) * 7 + bounds.width + bounds.height * 3 + (toLeft ? 1 : 0);
            if (tetherKey != drawnTetherKey)
            {
                drawnTetherKey = tetherKey;
                if (tetherCanvas == null || tetherCanvas.Canvas.Width != bounds.width || tetherCanvas.Canvas.Height != bounds.height)
                {
                    tetherCanvas?.Destroy();
                    tetherCanvas = new CanvasSprite(bounds.width, bounds.height, new Vector2(0f, 1f), "PanelTether");
                    tetherView.sprite = tetherCanvas.Sprite;
                }
                PanelSkin.ComposeTether(tetherCanvas.Canvas, p, faction, tether, frame);
                tetherCanvas.Upload();
                // El lienzo es del nodo: fuera de root, que va en el centro del panel.
                tetherView.transform.position = new Vector3(nodePos.x + bounds.x / Ppu, nodePos.y - bounds.y / Ppu, 0f);
            }

            for (int i = 0; i < conversionViews.Length; i++)
                AnimateButton(i, now, node);
        }

        void AnimateButton(int index, float now, NodeController node)
        {
            ButtonLook look = conversionLooks[index];
            if (look == ButtonLook.Hidden)
            {
                hoverSince[index] = -1f;
                return;
            }
            PanelArt.ConversionPieces p = art.Conversion;
            bool hovered = panel.HoveredConversion == index;
            bool pressed = panel.IsConversionPressed(index);
            if (!hovered)
                hoverSince[index] = -1f;
            else if (hoverSince[index] < 0f)
                hoverSince[index] = now;

            PanelArt.ConversionLook skin;
            float hover = 0f;
            switch (look)
            {
                case ButtonLook.Enabled:
                    skin = pressed ? PanelArt.ConversionLook.Pressed : hovered ? PanelArt.ConversionLook.Hover : PanelArt.ConversionLook.Normal;
                    hover = hovered ? Mathf.Clamp01((now - hoverSince[index]) / p.hoverSeconds) : 0f;
                    break;
                case ButtonLook.MissingEnergy: skin = PanelArt.ConversionLook.Missing; break;
                case ButtonLook.Unavailable: skin = PanelArt.ConversionLook.Unavailable; break;
                default: skin = PanelArt.ConversionLook.Blocked; break;
            }

            int energy = node.Energy;
            int cost = conversionCosts_[index];
            int step = Mathf.RoundToInt(hover * 16f);
            int key = ((int)skin * 17 + step) * 211 + (skin == PanelArt.ConversionLook.Missing ? energy : 0) + cost * 1000;
            if (key == drawnButtonKey[index])
                return;
            drawnButtonKey[index] = key;

            if (conversionCanvases[index] == null)
            {
                conversionCanvases[index] = new CanvasSprite(p.size.x, p.cellHeight, new Vector2(0f, 1f), $"ConversionButton{index}");
                conversionViews[index].sprite = conversionCanvases[index].Sprite;
            }
            PanelSkin.ComposeConversion(conversionCanvases[index].Canvas, p, skin, hover, energy, cost);
            conversionCanvases[index].Upload();

            // El texto: el tipo a la izquierda y el coste a la derecha, en los colores de su estado. Cuando el
            // barrido pasa de la mitad, el botón ya es primario y el texto va oscuro (TextMeshPro no se parte).
            PanelArt.ButtonText c = p.text;
            bool primary = skin == PanelArt.ConversionLook.Pressed || (skin == PanelArt.ConversionLook.Hover && UiMotion.Orbital(hover) >= 0.5f);
            Color32 name, price;
            switch (skin)
            {
                case PanelArt.ConversionLook.Missing: name = c.muted; price = c.missing; break;
                case PanelArt.ConversionLook.Blocked: name = c.muted; price = c.muted; break;
                case PanelArt.ConversionLook.Unavailable: name = c.unavailable; price = c.unavailable; break;
                case PanelArt.ConversionLook.Hover:
                case PanelArt.ConversionLook.Pressed:
                    name = primary ? p.primaryText : c.hover;
                    price = primary ? p.primaryText : c.hoverCost;
                    break;
                default: name = c.normal; price = c.cost; break;
            }
            conversionNames[index].color = name;
            conversionCosts[index].color = price;
            float drop = skin == PanelArt.ConversionLook.Pressed ? p.pressedDrop / Ppu : 0f;
            float capY = -(p.capTop + 5) / Ppu - drop;
            conversionNames[index].transform.localPosition = new Vector2(p.padX / Ppu, capY);
            conversionCosts[index].transform.localPosition = new Vector2((p.size.x - p.padX) / Ppu, capY);
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

            background = new GameObject("Background").AddComponent<SpriteRenderer>();
            background.transform.SetParent(root.transform, false);
            background.color = BackgroundColor;
            background.sortingOrder = SortingOrder;

            panelView = new GameObject("PanelArt").AddComponent<SpriteRenderer>();
            panelView.transform.SetParent(root.transform, false);
            panelView.sortingOrder = SortingOrder;
            panelView.enabled = false;

            // El cable cuelga del panel, pero se coloca en el mundo desde el nodo.
            tetherView = new GameObject("PanelTether").AddComponent<SpriteRenderer>();
            tetherView.transform.SetParent(root.transform, true);
            tetherView.sortingOrder = SortingOrder - 1;
            tetherView.enabled = false;

            // Cinco filas de texto de arriba abajo. La cuarta es la de la mejora —el coste o NIVEL
            // MÁXIMO; el Asteroide la usa para el coste de encendido— y la quinta es el rótulo de
            // reconversión; la sexta es la de sus dos botones.
            lines = new TextMeshPro[5];
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = Label(root.transform, new Vector2(0f, -i * LineHeight), TextAlignmentOptions.Left,
                    InfoPanelState.Width - 2f * Padding);
            }

            Vector2 conversionSize = InfoPanelState.ConversionButtonSize;
            int buttons = 2;
            conversionButtons = new SpriteRenderer[buttons];
            conversionLabels = new TextMeshPro[buttons];
            conversionLooks = new ButtonLook[buttons];
            conversionCosts_ = new int[buttons];
            conversionViews = new SpriteRenderer[buttons];
            conversionCanvases = new CanvasSprite[buttons];
            conversionNames = new TextMeshPro[buttons];
            conversionCosts = new TextMeshPro[buttons];
            hoverSince = new[] { -1f, -1f };
            int ppu = PrimitiveSprites.PixelsPerUnit;
            for (int i = 0; i < buttons; i++)
            {
                conversionButtons[i] = new GameObject($"ConversionButton{i}").AddComponent<SpriteRenderer>();
                conversionButtons[i].transform.SetParent(root.transform, false);
                conversionButtons[i].sprite = PrimitiveSprites.Rect(Mathf.RoundToInt(conversionSize.x * ppu), Mathf.RoundToInt(conversionSize.y * ppu));
                conversionButtons[i].sortingOrder = SortingOrder + 1;
                conversionLabels[i] = Label(conversionButtons[i].transform, Vector2.zero, TextAlignmentOptions.Center, conversionSize.x);
                conversionLabels[i].sortingOrder = SortingOrder + 2;

                conversionViews[i] = new GameObject($"ConversionArt{i}").AddComponent<SpriteRenderer>();
                conversionViews[i].transform.SetParent(root.transform, false);
                conversionViews[i].sortingOrder = SortingOrder + 1;
                conversionViews[i].enabled = false;
                if (art != null && art.HasPanel)
                {
                    PanelArt.ConversionPieces p = art.Conversion;
                    conversionCanvases[i] = new CanvasSprite(p.size.x, p.cellHeight, new Vector2(0f, 1f), $"ConversionButton{i}");
                    conversionViews[i].sprite = conversionCanvases[i].Sprite;
                }
                // Las etiquetas del arte se miden desde la esquina de arriba a la izquierda del botón.
                conversionNames[i] = Label(conversionViews[i].transform, Vector2.zero, TextAlignmentOptions.Left, conversionSize.x);
                conversionNames[i].rectTransform.pivot = new Vector2(0f, 0.5f);
                conversionNames[i].sortingOrder = SortingOrder + 2;
                conversionNames[i].enabled = false;
                conversionCosts[i] = Label(conversionViews[i].transform, Vector2.zero, TextAlignmentOptions.Right, conversionSize.x);
                conversionCosts[i].rectTransform.pivot = new Vector2(1f, 0.5f);
                conversionCosts[i].sortingOrder = SortingOrder + 2;
                conversionCosts[i].enabled = false;
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
