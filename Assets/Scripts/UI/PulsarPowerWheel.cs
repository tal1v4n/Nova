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
    // entero: su clic entra en modo apuntado (desviación 209). Primitivas de la regla 4 de T.2: la
    // skin final, ART-UI-WHEEL, es de T6-09.
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
        const int HeaderRow = 6;

        [SerializeField] InfoPanelState panel;

        [Tooltip("Opcional: sin él la ranura lista no enseña el modo apuntado.")]
        [SerializeField] PowerAimState aim;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Textos de la UI (T.7 #39).")]
        [SerializeField] UiText text;

        [Tooltip("Facción que mira el panel: la rueda solo sale en sus Púlsares.")]
        [SerializeField] Faction viewer = Faction.Player;

        GameObject root;
        TextMeshPro header;
        SpriteRenderer[] slots;
        TextMeshPro[] slotLabels;
        SlotLook[] slotLooks;
        SpriteRenderer cancel;
        TextMeshPro cancelLabel;

        NodeController watched;
        PulsarCaster watchedCaster;
        bool subscribed;

        internal bool IsVisible => root != null && root.activeSelf;
        internal string Header { get { EnsureBuilt(); return header.text; } }
        internal SlotLook Slot(int index) { EnsureBuilt(); return slotLooks[index]; }
        internal string SlotText(int index) { EnsureBuilt(); return slotLabels[index].enabled ? slotLabels[index].text : null; }
        internal Color SlotColor(int index) { EnsureBuilt(); return slots[index].color; }
        internal Vector2 SlotPosition(int index) { EnsureBuilt(); return slots[index].transform.position; }
        internal bool CancelVisible { get { EnsureBuilt(); return cancel.enabled; } }
        internal string CancelText { get { EnsureBuilt(); return cancelLabel.enabled ? cancelLabel.text : null; } }
        internal Vector2 CancelPosition { get { EnsureBuilt(); return cancel.transform.position; } }

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
        public void Configure(InfoPanelState state, PowerAimState powerAim, TMP_FontAsset fontAsset, UiText uiText)
        {
            Unsubscribe();
            panel = state;
            aim = powerAim;
            font = fontAsset;
            text = uiText;
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

            header.text = HeaderText(node, caster, t);
            for (int i = 0; i < slots.Length; i++)
                PaintSlot(i, caster, t);

            ActionCheck cancelCheck = caster.CheckCancel(viewer);
            bool hasCharge = caster.HasPowerCharge;
            cancel.enabled = hasCharge;
            cancel.color = cancelCheck.IsAllowed ? EnabledColor : DisabledColor;
            cancelLabel.enabled = hasCharge;
            cancelLabel.text = hasCharge ? t.Cancel(caster.RefundIfCancelled) : string.Empty;
            cancelLabel.color = cancelCheck.IsAllowed ? ButtonTextColor : TextColor;
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
            slots[index].enabled = true;
            slots[index].color = ColorOf(look, definition);
            slotLabels[index].enabled = true;
            slotLabels[index].text = label;
            slotLabels[index].color = look == SlotLook.Enabled || look == SlotLook.Ready ? ButtonTextColor : TextColor;
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

            header = Label(root.transform, new Vector2(0f, InfoPanelState.RowY(HeaderRow)), TextAlignmentOptions.Left,
                InfoPanelState.Width - 2f * Padding);

            int ppu = PrimitiveSprites.PixelsPerUnit;
            Vector2 slotSize = InfoPanelState.WheelSlotSize;
            slots = new SpriteRenderer[InfoPanelState.WheelSlotOffsets.Length];
            slotLabels = new TextMeshPro[slots.Length];
            slotLooks = new SlotLook[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new GameObject($"WheelSlot{i + 1}").AddComponent<SpriteRenderer>();
                slots[i].transform.SetParent(root.transform, false);
                slots[i].transform.localPosition = InfoPanelState.WheelSlotOffsets[i];
                slots[i].sprite = PrimitiveSprites.Rect(Mathf.RoundToInt(slotSize.x * ppu), Mathf.RoundToInt(slotSize.y * ppu));
                slots[i].sortingOrder = SortingOrder + 1;
                slotLabels[i] = Label(slots[i].transform, Vector2.zero, TextAlignmentOptions.Center, slotSize.x);
                slotLabels[i].sortingOrder = SortingOrder + 2;
            }

            Vector2 cancelSize = InfoPanelState.ButtonSize;
            cancel = new GameObject("CancelButton").AddComponent<SpriteRenderer>();
            cancel.transform.SetParent(root.transform, false);
            cancel.transform.localPosition = InfoPanelState.CancelOffset;
            cancel.sprite = PrimitiveSprites.Rect(Mathf.RoundToInt(cancelSize.x * ppu), Mathf.RoundToInt(cancelSize.y * ppu));
            cancel.sortingOrder = SortingOrder + 1;
            cancelLabel = Label(cancel.transform, Vector2.zero, TextAlignmentOptions.Center, cancelSize.x);
            cancelLabel.sortingOrder = SortingOrder + 2;

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
