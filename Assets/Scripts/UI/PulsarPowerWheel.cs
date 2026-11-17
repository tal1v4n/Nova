using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Utils;
using TMPro;
using UnityEngine;

namespace Nova.UI
{
    // Rueda de poderes del Púlsar (13.3 punto 6, 13.5, T3-07): las tres últimas filas del panel de
    // clic derecho, solo en un Púlsar propio. Un rótulo con el estado de la carga, tres ranuras en
    // orden fijo y «Cancelar +N». Solo dibuja: la geometría y el panel son de InfoPanelState, el
    // modo apuntado de PowerAimState, el clic lo resuelve InputRouter y cada ranura se valida con
    // PulsarCaster.CheckCharge, sin efectos.
    //
    // Cada ranura enseña su número y su coste, o «Nv N» si el nivel no la desbloquea. La del poder
    // en carga va del color del poder a media intensidad, y la del poder listo brilla con el color
    // entero: su clic entra en modo apuntado (desviación 209).
    //
    // T6-09: con PanelArt, la skin final, `ART-UI-WHEEL` «Las teclas» (A.6): tres teclas de 13.12 con el
    // pictograma del poder; encima, la trama cian; cargando, se llenan del color del poder hasta el progreso;
    // listas, enteras en él con una luz que las cruza. Se componen con PanelSkin; el texto va en TextMeshPro con
    // los colores del .json. Sin PanelArt, primitivas de la regla 4 de T.2.
    [DefaultExecutionOrder(50)] // UI, 15.10
    [DisallowMultipleComponent]
    public sealed class PulsarPowerWheel : MonoBehaviour
    {
        internal enum SlotLook { Hidden, Enabled, Disabled, Locked, Unavailable, Charging, Ready }

        static readonly Color32 TextColor = new Color32(0xE6, 0xEC, 0xF5, 0xFF);
        static readonly Color32 EnabledColor = new Color32(0x3F, 0xD7, 0xF5, 0xFF);   // Jugador, 14.4
        static readonly Color32 DisabledColor = new Color32(0x2A, 0x30, 0x42, 0xFF);
        static readonly Color32 ButtonTextColor = new Color32(0x0B, 0x0E, 0x1A, 0xFF);

        // Como NodeInfoPanel: Jersey 10 a tamaño nativo y por encima del fondo del panel (30).
        const float NativeEmPixels = 1400f / 75f;
        const float WorldTextScale = 0.1f;
        const int SortingOrder = 30;
        const float Padding = 0.125f;
        const float LineHeight = 0.375f;
        const float Ppu = PrimitiveSprites.PixelsPerUnit;

        [SerializeField] InfoPanelState panel;

        [Tooltip("Opcional: sin él la ranura lista no enseña el modo apuntado.")]
        [SerializeField] PowerAimState aim;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Textos de la UI (T.7 #39).")]
        [SerializeField] UiText text;

        [Tooltip("Opcional (T6-09): el arte de la rueda. Sin él, primitivas.")]
        [SerializeField] PanelArt art;

        [Tooltip("Facción que mira el panel: la rueda solo sale en sus Púlsares.")]
        [SerializeField] Faction viewer = Faction.Player;

        GameObject root;
        TextMeshPro header;
        SpriteRenderer[] slots;
        TextMeshPro[] slotLabels;
        SlotLook[] slotLooks;
        SpriteRenderer cancel;
        TextMeshPro cancelLabel;
        bool cancelAllowed;

        // Con arte.
        SpriteRenderer[] slotViews;
        CanvasSprite[] slotCanvases;
        TextMeshPro[] slotArtLabels;
        SpriteRenderer cancelView;
        CanvasSprite cancelCanvas;
        TextMeshPro cancelArtLabel;
        float[] hoverSince;
        readonly int[] drawnSlotKey = new int[3];
        int drawnCancelKey = int.MinValue;

        NodeController watched;
        PulsarCaster watchedCaster;
        bool subscribed;

        internal bool IsVisible => root != null && root.activeSelf;
        internal string Header { get { EnsureBuilt(); return header.text; } }
        internal SlotLook Slot(int index) { EnsureBuilt(); return slotLooks[index]; }
        internal string SlotText(int index) { EnsureBuilt(); return slotLabels[index].text.Length > 0 ? slotLabels[index].text : null; }
        internal Color SlotColor(int index) { EnsureBuilt(); return slots[index].color; }
        internal Vector2 SlotPosition(int index) { EnsureBuilt(); return slots[index].transform.position; }
        internal bool CancelVisible { get { EnsureBuilt(); return HasArt ? cancelView.enabled : cancel.enabled; } }
        internal string CancelText { get { EnsureBuilt(); return CancelVisible ? cancelLabel.text : null; } }
        internal Vector2 CancelPosition { get { EnsureBuilt(); return cancel.transform.position; } }
        internal bool HasArt => art != null && art.HasPanel;
        internal PixelCanvas SlotCanvas(int index) { EnsureBuilt(); return slotCanvases[index]?.Canvas; }
        internal PixelCanvas CancelCanvas { get { EnsureBuilt(); return cancelCanvas?.Canvas; } }
        internal string SlotArtText(int index) { EnsureBuilt(); return slotArtLabels[index].enabled ? slotArtLabels[index].text : null; }

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
            if (slotCanvases != null)
                foreach (CanvasSprite c in slotCanvases)
                    c?.Destroy();
            cancelCanvas?.Destroy();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(InfoPanelState state, PowerAimState powerAim, TMP_FontAsset fontAsset, UiText uiText,
            PanelArt panelArt = null)
        {
            Unsubscribe();
            panel = state;
            aim = powerAim;
            font = fontAsset;
            text = uiText;
            art = panelArt;
        }

        internal void Subscribe()
        {
            if (subscribed || panel == null)
                return;
            panel.OnChanged += Refresh;
            if (aim != null)
                aim.OnChanged += Refresh;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (subscribed)
            {
                if (panel != null)
                    panel.OnChanged -= Refresh;
                if (aim != null)
                    aim.OnChanged -= Refresh;
            }
            subscribed = false;
            Watch(null);
        }

        public void Refresh()
        {
            EnsureBuilt();

            bool open = panel != null && panel.IsOpen && panel.Node.IsInitialized;
            Watch(open ? panel.Node : null);
            bool shown = open && InfoPanelState.ShowsWheel(panel.Node, viewer);
            root.SetActive(shown);
            if (!shown)
                return;

            root.transform.position = panel.Center;
            NodeController node = panel.Node;
            PulsarCaster caster = node.GetComponent<PulsarCaster>();
            UiText t = Text;
            InfoPanelState.PanelLayout layout = panel.Layout;

            header.transform.localPosition = new Vector2(0f, layout.RowY(layout.WheelHeaderRow));
            header.text = HeaderText(node, caster, t);
            header.color = HasArt ? (Color)art.Panel.labelColor : (Color)TextColor;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].transform.localPosition = panel.WheelSlotOffset(i);
                PaintSlot(i, caster, t);
            }

            ActionCheck cancelCheck = caster.CheckCancel(viewer);
            bool hasCharge = caster.HasPowerCharge;
            cancelAllowed = cancelCheck.IsAllowed;
            cancel.transform.localPosition = panel.CancelOffset;
            cancel.enabled = hasCharge && !HasArt;
            cancel.color = cancelCheck.IsAllowed ? EnabledColor : DisabledColor;
            cancelLabel.enabled = hasCharge && !HasArt;
            cancelLabel.text = hasCharge ? t.Cancel(caster.RefundIfCancelled) : string.Empty;
            cancelLabel.color = cancelCheck.IsAllowed ? ButtonTextColor : TextColor;

            if (HasArt)
            {
                cancelView.enabled = hasCharge;
                cancelArtLabel.enabled = hasCharge;
                for (int i = 0; i < drawnSlotKey.Length; i++)
                    drawnSlotKey[i] = int.MinValue;
                drawnCancelKey = int.MinValue;
                Animate(Time.unscaledTime);
            }
        }

        void LateUpdate()
        {
            if (HasArt && root != null && root.activeSelf && panel != null && panel.IsOpen)
                Animate(Time.unscaledTime);
        }

        string HeaderText(NodeController node, PulsarCaster caster, UiText t)
        {
            switch (caster.State)
            {
                case PowerChargeState.Ready:
                    return aim != null && aim.IsAiming && aim.Pulsar == node ? t.Aiming(caster.Power.Type) : t.Ready(caster.Power.Type);
                case PowerChargeState.Charging:
                    return caster.IsPaused
                        ? t.Paused(caster.Power.Type)
                        : t.Charging(caster.Power.Type, Mathf.FloorToInt(caster.Progress * 100f));
                default:
                    return t.PowersHeader;
            }
        }

        void PaintSlot(int index, PulsarCaster caster, UiText t)
        {
            PowerType power = InfoPanelState.WheelPowers[index];
            PowerDefinition definition = caster.DefinitionOf(power);
            int number = index + 1;

            SlotLook look;
            string label;
            if (definition == null)
            {
                look = SlotLook.Unavailable;
                label = number.ToString();
            }
            else if (caster.Power != null && caster.Power.Type == power)
            {
                look = caster.State == PowerChargeState.Ready ? SlotLook.Ready : SlotLook.Charging;
                label = t.Slot(number, definition.Cost);
            }
            else
            {
                ActionCheck check = caster.CheckCharge(viewer, power);
                look = check.IsAllowed ? SlotLook.Enabled
                    : check.Block == ActionBlock.LevelTooLow ? SlotLook.Locked
                    : SlotLook.Disabled;
                label = look == SlotLook.Locked ? t.SlotLocked(definition.RequiredLevel) : t.Slot(number, definition.Cost);
            }

            slotLooks[index] = look;
            bool artOn = HasArt;
            slots[index].enabled = !artOn;
            slots[index].color = ColorOf(look, definition);
            slotLabels[index].enabled = !artOn;
            slotLabels[index].text = label;
            slotLabels[index].color = look == SlotLook.Enabled || look == SlotLook.Ready ? ButtonTextColor : TextColor;

            if (artOn)
            {
                Vector2Int size = art.Wheel.slotSize;
                slotViews[index].enabled = true;
                slotViews[index].transform.localPosition = panel.WheelSlotOffset(index) + new Vector2(-size.x / 2f, size.y / 2f) / Ppu;
                slotArtLabels[index].enabled = true;
            }
        }

        static Color ColorOf(SlotLook look, PowerDefinition definition)
        {
            switch (look)
            {
                case SlotLook.Enabled: return EnabledColor;
                case SlotLook.Ready: return definition.BeamColor;
                case SlotLook.Charging:
                    Color dim = definition.BeamColor * 0.5f;
                    dim.a = 1f;
                    return dim;
                default: return DisabledColor;
            }
        }

        // --- Con arte ---

        void Animate(float now)
        {
            PanelArt.WheelPieces p = art.Wheel;
            NodeController node = panel.Node;
            PulsarCaster caster = node.GetComponent<PulsarCaster>();
            int frame = Mathf.FloorToInt(now * p.framesPerSecond) % p.frames;
            UiText t = Text;

            for (int i = 0; i < slotViews.Length; i++)
            {
                SlotLook look = slotLooks[i];
                bool hovered = panel.HoveredSlot == i;
                bool pressed = panel.IsSlotPressed(i);
                bool clickable = look == SlotLook.Enabled || look == SlotLook.Ready;
                if (!hovered || !clickable)
                    hoverSince[i] = -1f;
                else if (hoverSince[i] < 0f)
                    hoverSince[i] = now;

                PanelArt.SlotLook skin = SkinOf(look, clickable && hovered, clickable && pressed);
                float hover = skin == PanelArt.SlotLook.Hover ? Mathf.Clamp01((now - hoverSince[i]) / p.hoverSeconds) : 0f;
                bool charging = look == SlotLook.Charging;
                float progress = charging ? caster.Progress : 0f;
                bool paused = charging && caster.IsPaused;
                bool animated = skin >= PanelArt.SlotLook.Ready;
                int key = ((((int)skin * 33 + (animated ? frame : 0)) * 17 + Mathf.RoundToInt(hover * 16f)) * 1001
                           + Mathf.FloorToInt(progress * 1000f)) * 2 + (paused ? 1 : 0);
                if (key == drawnSlotKey[i])
                    continue;
                drawnSlotKey[i] = key;

                if (slotCanvases[i] == null)
                {
                    slotCanvases[i] = new CanvasSprite(p.slotSize.x, p.cellHeight, new Vector2(0f, 1f), $"WheelSlot{i + 1}");
                    slotViews[i].sprite = slotCanvases[i].Sprite;
                }
                PanelSkin.ComposeSlot(slotCanvases[i].Canvas, p, i, skin, progress, paused, frame, hover);
                slotCanvases[i].Upload();
                PaintSlotText(i, skin, caster, t);
            }

            if (!cancelView.enabled)
                return;
            bool cancelHover = panel.CancelHovered && cancelAllowed;
            bool cancelPressed = panel.CancelPressed && cancelAllowed;
            int cancelKey = (cancelHover ? 1 : 0) + (cancelPressed ? 2 : 0) + (cancelAllowed ? 4 : 0) + caster.RefundIfCancelled * 8;
            if (cancelKey == drawnCancelKey)
                return;
            drawnCancelKey = cancelKey;
            if (cancelCanvas == null)
            {
                cancelCanvas = new CanvasSprite(p.cancelSize.x, p.cellHeight, new Vector2(0f, 1f), "WheelCancel");
                cancelView.sprite = cancelCanvas.Sprite;
            }
            PanelSkin.ComposeCancel(cancelCanvas.Canvas, p, cancelHover, cancelPressed, !cancelAllowed);
            cancelCanvas.Upload();
            cancelView.transform.localPosition = panel.CancelOffset + new Vector2(-p.cancelSize.x / 2f, p.cancelSize.y / 2f) / Ppu;

            PanelArt.ButtonText c = p.text;
            Color32 word = !cancelAllowed ? c.muted : cancelHover || cancelPressed ? c.hover : c.normal;
            Color32 refund = !cancelAllowed ? c.muted : cancelHover || cancelPressed ? c.hoverCost : c.cost;
            cancelArtLabel.text = $"<color={PanelSkin.Hex(word)}>{t.CancelWord}</color><color={PanelSkin.Hex(refund)}>{t.CancelRefund(caster.RefundIfCancelled)}</color>";
            float drop = cancelPressed ? p.pressedDrop / Ppu : 0f;
            cancelArtLabel.transform.localPosition = new Vector2(p.cancelSize.x / 2f / Ppu, -(p.capTop + 5) / Ppu - drop);
        }

        static PanelArt.SlotLook SkinOf(SlotLook look, bool hovered, bool pressed)
        {
            switch (look)
            {
                case SlotLook.Enabled:
                    return pressed ? PanelArt.SlotLook.Pressed : hovered ? PanelArt.SlotLook.Hover : PanelArt.SlotLook.Normal;
                case SlotLook.Ready:
                    return pressed ? PanelArt.SlotLook.ReadyPressed : hovered ? PanelArt.SlotLook.ReadyHover : PanelArt.SlotLook.Ready;
                case SlotLook.Charging: return PanelArt.SlotLook.Charging;
                case SlotLook.Locked: return PanelArt.SlotLook.Locked;
                case SlotLook.Unavailable: return PanelArt.SlotLook.Unavailable;
                default: return PanelArt.SlotLook.Blocked;
            }
        }

        // `1 · 20` a la derecha: el número en el color del texto y el coste en cian; `Nv N` con el candado; solo el
        // número si el mapa no tiene ese poder; oscuro sobre la tecla lista.
        void PaintSlotText(int index, PanelArt.SlotLook skin, PulsarCaster caster, UiText t)
        {
            PanelArt.WheelPieces p = art.Wheel;
            PanelArt.ButtonText c = p.text;
            PowerDefinition definition = caster.DefinitionOf(InfoPanelState.WheelPowers[index]);
            int number = index + 1;
            string label;
            switch (skin)
            {
                case PanelArt.SlotLook.Unavailable:
                    label = $"<color={PanelSkin.Hex(c.unavailable)}>{number}</color>";
                    break;
                case PanelArt.SlotLook.Locked:
                    label = $"<color={PanelSkin.Hex(c.muted)}>{t.SlotLocked(definition.RequiredLevel)}</color>";
                    break;
                default:
                {
                    Color32 left, right;
                    if (skin == PanelArt.SlotLook.Blocked)
                        left = right = c.muted;
                    else if (skin >= PanelArt.SlotLook.Ready)
                        left = right = c.onPower;
                    else if (skin == PanelArt.SlotLook.Hover || skin == PanelArt.SlotLook.Pressed)
                    {
                        left = c.hover;
                        right = c.hoverCost;
                    }
                    else
                    {
                        left = c.normal;
                        right = c.cost;
                    }
                    label = $"<color={PanelSkin.Hex(left)}>{t.SlotNumber(number)}</color><color={PanelSkin.Hex(right)}>{definition.Cost}</color>";
                    break;
                }
            }
            slotArtLabels[index].text = label;
            bool pressed = skin == PanelArt.SlotLook.Pressed || skin == PanelArt.SlotLook.ReadyPressed;
            float drop = pressed ? p.pressedDrop / Ppu : 0f;
            slotArtLabels[index].transform.localPosition = new Vector2(p.textRight / Ppu, -(p.capTop + 5) / Ppu - drop);
        }

        UiText Text => text != null ? text : throw new System.InvalidOperationException("PulsarPowerWheel necesita UiText.");

        // Lo que cambia con el panel abierto: la carga, el nivel (desbloquea ranuras), el tipo, el
        // dueño y los estados (Congelado bloquea).
        void Watch(NodeController node)
        {
            if (watched == node)
                return;
            if (watched != null)
            {
                watched.OnLevelChanged -= HandleIntChanged;
                watched.OnTypeChanged -= HandleTypeChanged;
                watched.OnFactionChanged -= HandleFactionChanged;
                watched.OnStateFlagsChanged -= HandleFlagsChanged;
            }
            if (watchedCaster != null)
            {
                watchedCaster.OnStateChanged -= HandleChargeChanged;
                watchedCaster.OnProgress -= Refresh;
            }
            watched = node;
            watchedCaster = node != null ? node.GetComponent<PulsarCaster>() : null;
            if (watched != null)
            {
                watched.OnLevelChanged += HandleIntChanged;
                watched.OnTypeChanged += HandleTypeChanged;
                watched.OnFactionChanged += HandleFactionChanged;
                watched.OnStateFlagsChanged += HandleFlagsChanged;
            }
            if (watchedCaster != null)
            {
                watchedCaster.OnStateChanged += HandleChargeChanged;
                watchedCaster.OnProgress += Refresh;
            }
        }

        void HandleIntChanged(int previous, int current) => Refresh();
        void HandleTypeChanged(NodeType previous, NodeType current) => Refresh();
        void HandleFactionChanged(Faction previous, Faction current) => Refresh();
        void HandleFlagsChanged(NodeStateFlags previous, NodeStateFlags current) => Refresh();
        void HandleChargeChanged(PowerChargeState previous, PowerChargeState current) => Refresh();

        void EnsureBuilt()
        {
            if (root != null)
                return;

            root = new GameObject("PulsarPowerWheel");
            root.transform.SetParent(transform, false);

            header = Label(root.transform, Vector2.zero, TextAlignmentOptions.Left, InfoPanelState.Width - 2f * Padding);

            int ppu = PrimitiveSprites.PixelsPerUnit;
            Vector2 slotSize = InfoPanelState.WheelSlotSize;
            int count = InfoPanelState.WheelPowers.Length;
            slots = new SpriteRenderer[count];
            slotLabels = new TextMeshPro[count];
            slotLooks = new SlotLook[count];
            slotViews = new SpriteRenderer[count];
            slotCanvases = new CanvasSprite[count];
            slotArtLabels = new TextMeshPro[count];
            hoverSince = new[] { -1f, -1f, -1f };
            for (int i = 0; i < count; i++)
            {
                slots[i] = new GameObject($"WheelSlot{i + 1}").AddComponent<SpriteRenderer>();
                slots[i].transform.SetParent(root.transform, false);
                slots[i].sprite = PrimitiveSprites.Rect(Mathf.RoundToInt(slotSize.x * ppu), Mathf.RoundToInt(slotSize.y * ppu));
                slots[i].sortingOrder = SortingOrder + 1;
                slotLabels[i] = Label(slots[i].transform, Vector2.zero, TextAlignmentOptions.Center, slotSize.x);
                slotLabels[i].sortingOrder = SortingOrder + 2;

                slotViews[i] = new GameObject($"WheelSlotArt{i + 1}").AddComponent<SpriteRenderer>();
                slotViews[i].transform.SetParent(root.transform, false);
                slotViews[i].sortingOrder = SortingOrder + 1;
                slotViews[i].enabled = false;
                slotArtLabels[i] = Label(slotViews[i].transform, Vector2.zero, TextAlignmentOptions.Right, slotSize.x);
                slotArtLabels[i].rectTransform.pivot = new Vector2(1f, 0.5f);
                slotArtLabels[i].sortingOrder = SortingOrder + 2;
                slotArtLabels[i].enabled = false;
            }

            Vector2 cancelSize = InfoPanelState.CancelSize;
            cancel = new GameObject("CancelButton").AddComponent<SpriteRenderer>();
            cancel.transform.SetParent(root.transform, false);
            cancel.sprite = PrimitiveSprites.Rect(Mathf.RoundToInt(cancelSize.x * ppu), Mathf.RoundToInt(cancelSize.y * ppu));
            cancel.sortingOrder = SortingOrder + 1;
            cancelLabel = Label(cancel.transform, Vector2.zero, TextAlignmentOptions.Center, cancelSize.x);
            cancelLabel.sortingOrder = SortingOrder + 2;

            cancelView = new GameObject("CancelButtonArt").AddComponent<SpriteRenderer>();
            cancelView.transform.SetParent(root.transform, false);
            cancelView.sortingOrder = SortingOrder + 1;
            cancelView.enabled = false;
            cancelArtLabel = Label(cancelView.transform, Vector2.zero, TextAlignmentOptions.Center, cancelSize.x);
            cancelArtLabel.sortingOrder = SortingOrder + 2;
            cancelArtLabel.enabled = false;

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
                Debug.LogWarning($"[Nova] {name}: PulsarPowerWheel sin font asset; asigna Jersey10 Raster56.", this);
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
