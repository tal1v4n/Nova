using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Nova.UI
{
    // Menú de pausa (13.10, 15.7, T5-05), fiel a la maqueta «Pausa» del lienzo «NOVA — Pantallas de
    // flujo» (T.2 regla 8, 18 #46): tres tarjetas sobre el tablero congelado y velado —este nivel, las
    // acciones y los controles— a 1920 x 1080. Lo abre y lo cierra GameManager: este script solo lo
    // dibuja y atiende sus botones.
    //
    // Con la escala a 0, InputRouter no lee nada (15.8, Tiempo de la UI y pausa): Esc lo lee este
    // script en su Update y los clics llegan por el EventSystem, que corre igual con la partida
    // parada. Reiniciar nivel y Volver al mapa piden confirmación dentro de la tarjeta (18 #39); con
    // ella abierta, Esc la cancela, y sin ella, reanuda.
    [DefaultExecutionOrder(50)] // UI, 15.10
    [DisallowMultipleComponent]
    public sealed class PauseMenu : MonoBehaviour
    {
        internal enum Choice { Resume = 0, Restart = 1, BackToMap = 2, Confirm = 3, Cancel = 4 }

        // Qué está pidiendo confirmación.
        internal enum Asking { None, Restart, BackToMap }

        // La maqueta: rejilla de 440 · 560 · 520 px con 40 de hueco, a 160 del borde y 200 de arriba;
        // tarjetas con 28 x 32 de margen interior.
        const float Top = 200f;
        static readonly float[] ColumnX = { 160f, 640f, 1240f };
        static readonly float[] ColumnW = { 440f, 560f, 520f };
        const float PadX = 32f;
        const float PadY = 28f;
        const int Order = 100;

        // Colores de los chips de poder, de 14.4.
        static readonly Color32 AbsoluteZeroColor = UiKit.Hex(0xB8F0FF);
        static readonly Color32 DecayColor = UiKit.Hex(0x7CF23F);
        static readonly Color32 WormholeColor = UiKit.Hex(0x9B5CF6);

        [SerializeField] GameManager game;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Textos de la UI (T.7 #39).")]
        [SerializeField] UiText text;

        GameObject root;
        UiMotion motion;
        readonly Dictionary<Choice, UiButton> buttons = new Dictionary<Choice, UiButton>();
        Image pauseCard;
        RectTransform choices;
        RectTransform confirmation;
        TextMeshProUGUI prompt;
        float choicesBottom;
        float confirmationBottom;
        float confirmationHeight;
        int openedFrame = -1;
        bool subscribed;

        internal bool IsVisible => root != null && root.activeSelf;
        internal Asking Pending { get; private set; }
        internal string PromptText => prompt != null && confirmation.gameObject.activeSelf ? prompt.text : null;

        // Si el botón está a la vista y responde.
        internal bool IsActive(Choice choice) => buttons.TryGetValue(choice, out UiButton b) && b.gameObject.activeInHierarchy;

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void Update()
        {
            if (!IsVisible)
                return;
            Keyboard keyboard = Keyboard.current;
            // Una pulsación abre o cierra, no las dos cosas: si la pausa se abrió en este frame, su Esc
            // no la cierra.
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && Time.frameCount != openedFrame)
                PressEscape();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(GameManager flow, TMP_FontAsset fontAsset, UiText uiText)
        {
            Unsubscribe();
            game = flow;
            font = fontAsset;
            text = uiText;
            Subscribe();
        }

        internal void Subscribe()
        {
            if (subscribed || game == null)
                return;
            game.OnStateChanged += StateChanged;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (subscribed && game != null)
                game.OnStateChanged -= StateChanged;
            subscribed = false;
        }

        // Esc con la pausa abierta: cancela la confirmación o reanuda.
        public void PressEscape()
        {
            if (Pending != Asking.None)
                Ask(Asking.None);
            else
                game.Resume();
        }

        internal void Choose(Choice choice)
        {
            switch (choice)
            {
                case Choice.Resume: game.Resume(); break;
                case Choice.Restart: Ask(Asking.Restart); break;
                case Choice.BackToMap: Ask(Asking.BackToMap); break;
                case Choice.Cancel: Ask(Asking.None); break;
                case Choice.Confirm:
                    if (Pending == Asking.Restart)
                        game.Restart();
                    else if (Pending == Asking.BackToMap)
                        game.BackToMap();
                    break;
            }
        }

        void StateChanged(GameFlowState state)
        {
            if (state == GameFlowState.Paused)
                Open();
            else
                Close();
        }

        void Open()
        {
            Close();
            openedFrame = Time.frameCount;
            Build();
            Ask(Asking.None, false);
        }

        void Close()
        {
            if (root != null)
            {
                if (Application.isPlaying)
                    Destroy(root);
                else
                    DestroyImmediate(root);
            }
            root = null;
            buttons.Clear();
            Pending = Asking.None;
        }

        // La confirmación sustituye a Reiniciar nivel y Volver al mapa y se despliega en 0.45 s; la
        // tarjeta crece o encoge con ella (unfold de la maqueta).
        void Ask(Asking asking, bool animate = true)
        {
            Pending = asking;
            if (root == null)
                return;
            bool open = asking != Asking.None;
            choices.gameObject.SetActive(!open);
            confirmation.gameObject.SetActive(open);
            prompt.text = asking == Asking.BackToMap ? Text.ConfirmBackToMap : Text.ConfirmRestart;
            buttons[Choice.Confirm].Label.text = asking == Asking.BackToMap ? Text.ConfirmBackToMapAction : Text.ConfirmRestartAction;

            float from = pauseCard.rectTransform.sizeDelta.y;
            float to = (open ? confirmationBottom : choicesBottom) + PadY + 8f;
            if (!animate)
            {
                UiKit.Resize(pauseCard, to);
                return;
            }
            float start = motion.Time;
            motion.Add(start, 0.45f, UiMotion.Orbital, t => UiKit.Resize(pauseCard, Mathf.Lerp(from, to, t)));
            if (open)
            {
                CanvasGroup group = UiMotion.Group(confirmation);
                var clip = confirmation.GetComponent<RectMask2D>();
                motion.Add(start, 0.45f, UiMotion.Orbital, t =>
                {
                    group.alpha = t;
                    clip.padding = new Vector4(0f, confirmationHeight * (1f - t), 0f, 0f);
                });
            }
        }

        void Build()
        {
            root = null;
            RectTransform stage = UiKit.Stage(transform, "PauseMenu", Order, out root, out Image veil);
            motion = root.AddComponent<UiMotion>();
            motion.Fade(veil.rectTransform, 0f, 0.35f);

            UiText t = Text;
            BuildLevel(stage, t);
            BuildPause(stage, t);
            BuildControls(stage, t);
        }

        // «Este nivel»: mapa y nivel, las dos condiciones, lo que el mapa añade y el tiempo.
        void BuildLevel(RectTransform stage, UiText t)
        {
            RectTransform fill = UiKit.Card(stage, "ThisLevel", ColumnX[0], Top, ColumnW[0], 100f, out Image card);
            float w = ColumnW[0] - 8f - 2f * PadX;
            float y = PadY;
            LevelDefinition level = game.Level;

            y = Cap(fill, t.ThisLevelHeader, PadX, y, w, UiKit.PlayerCap) + 12f;
            if (level != null)
                y += UiKit.Label(fill, font, t.LevelName(level.Map, level.Number), PadX, y, w, UiKit.X3, UiKit.Bright, 52f).rectTransform.sizeDelta.y + 12f;
            y += UiKit.Label(fill, font, t.WinCondition, PadX, y, w, UiKit.X2, UiKit.Soft, 34f, wrap: true).rectTransform.sizeDelta.y + 12f;
            y += UiKit.Label(fill, font, t.LoseCondition, PadX, y, w, UiKit.X2, UiKit.Soft, 34f, wrap: true).rectTransform.sizeDelta.y + 12f;
            if (level != null)
            {
                List<(string name, Color32 border, Color32 ink)> added = AddedMechanics(level, t);
                if (added.Count > 0)
                {
                    y = Cap(fill, t.AvailableHeader, PadX, y + 8f, w, UiKit.Muted) + 12f;
                    y = Chips(fill, added, PadX, y, w) + 12f;
                }
                float elapsed = game.Recorder != null ? game.Recorder.Elapsed : 0f;
                y = Cap(fill, t.PauseTime(level.TargetDuration, elapsed), PadX, y + 8f, w, UiKit.Muted) + 12f;
            }
            UiKit.Resize(card, y - 12f + PadY + 8f);
            motion.Rise(card.rectTransform, 0.2f);
        }

        // «Pausa»: el título, Continuar y las dos acciones que tiran la partida, o su confirmación.
        void BuildPause(RectTransform stage, UiText t)
        {
            RectTransform fill = UiKit.Card(stage, "Pause", ColumnX[1], Top, ColumnW[1], 100f, out pauseCard);
            float w = ColumnW[1] - 8f - 2f * PadX;
            float y = PadY;

            // PAUSA se congela al entrar: el espaciado se cierra de 56 a 12 px (freeze de la maqueta).
            TextMeshProUGUI title = UiKit.Label(fill, font, t.PauseTitle, PadX, y, w, UiKit.X8, UiKit.Bright, 120f, 12f);
            List<TextMeshProUGUI> layers = UiKit.Shadows(title, (8f, UiKit.Frame));
            layers.Add(title);
            var group = UiMotion.Group(title.rectTransform);
            var shadowGroup = UiMotion.Group(layers[0].rectTransform);
            motion.Add(0.15f, 0.9f, UiMotion.Orbital, k =>
            {
                float spacing = Mathf.Lerp(56f, 12f, k) / UiKit.X8 * 100f;
                foreach (TextMeshProUGUI layer in layers)
                    layer.characterSpacing = spacing;
                group.alpha = k;
                shadowGroup.alpha = k;
            });
            y += 120f + 16f;
            y = Cap(fill, t.PauseSubtitle, PadX, y, w, UiKit.Muted) + 8f + 16f;

            UiButton resume = Add(fill, Choice.Resume, t.Resume, UiButton.Style.Primary, PadX, y, w);
            resume.SetKey(font, t.EscapeKey);
            resume.StartShine(1.2f);
            y += 80f + 16f;

            // Las dos acciones, o la confirmación en el mismo sitio.
            choices = UiKit.Node(fill, "Choices", 0f, y, ColumnW[1] - 8f, 200f);
            Add(choices, Choice.Restart, t.RestartLevel, UiButton.Style.Secondary, PadX, 0f, w);
            Add(choices, Choice.BackToMap, t.BackToMap, UiButton.Style.Secondary, PadX, 96f, w);
            choicesBottom = y + 96f + 80f;

            // Recuadro discontinuo de 2 px, 16 de margen y 12 entre el texto y los botones.
            confirmation = UiKit.Node(fill, "Confirmation", PadX, y, w, 100f);
            confirmation.gameObject.AddComponent<RectMask2D>();
            float inner = w - 32f;
            prompt = UiKit.Label(confirmation, font, t.ConfirmRestart, 16f, 16f, inner, UiKit.X2, UiKit.EnemyText, 34f, wrap: true);
            // El más largo de los dos textos decide el alto.
            float promptHeight = Mathf.Max(prompt.rectTransform.sizeDelta.y, HeightOf(t.ConfirmBackToMap, inner));
            prompt.rectTransform.sizeDelta = new Vector2(inner, promptHeight);
            float half = (inner - 12f) / 2f;
            float buttonsY = 16f + promptHeight + 12f;
            Add(confirmation, Choice.Confirm, t.ConfirmRestartAction, UiButton.Style.Danger, 16f, buttonsY, half, 56f, UiKit.X2);
            Add(confirmation, Choice.Cancel, t.CancelConfirm, UiButton.Style.Secondary, 16f + half + 12f, buttonsY, half, 56f, UiKit.X2);
            confirmationHeight = buttonsY + 64f + 16f;
            confirmation.sizeDelta = new Vector2(w, confirmationHeight);
            UiKit.Outline(confirmation, 0f, 0f, w, confirmationHeight, UiKit.EnemyDeep, 2f, dashed: true);
            confirmationBottom = y + confirmationHeight;

            motion.Rise(pauseCard.rectTransform, 0.08f);
        }

        // «Controles»: el resumen de 13.1, fila a fila.
        void BuildControls(RectTransform stage, UiText t)
        {
            RectTransform fill = UiKit.Card(stage, "Controls", ColumnX[2], Top, ColumnW[2], 100f, out Image card);
            float w = ColumnW[2] - 8f - 2f * PadX;
            float y = Cap(fill, t.ControlsHeader, PadX, PadY, w, UiKit.PlayerCap) + 14f;
            const float KeyColumn = 200f;
            for (int i = 0; i < t.ControlCount; i++)
            {
                RectTransform row = UiKit.Node(fill, $"Row{i}", PadX, y, w, 34f);
                TextMeshProUGUI action = UiKit.Label(row, font, t.ControlAction(i), KeyColumn + 16f, 0f, w - KeyColumn - 16f,
                    UiKit.X2, UiKit.Text, 32f, wrap: true);
                float h = Mathf.Max(34f, action.rectTransform.sizeDelta.y);
                UiKit.Key(row, font, t.ControlKey(i), 0f, (h - 34f) / 2f, UiKit.KeyFrame, UiKit.Soft);
                action.rectTransform.anchoredPosition = new Vector2(KeyColumn + 16f, -(h - action.rectTransform.sizeDelta.y) / 2f);
                row.sizeDelta = new Vector2(w, h);
                motion.Rise(row, 0.42f + 0.06f * i, 0.5f);
                y += h + 14f;
            }
            UiKit.Resize(card, y - 14f + PadY + 8f);
            motion.Rise(card.rectTransform, 0.3f);
        }

        UiButton Add(Transform parent, Choice choice, string label, UiButton.Style style, float x, float y, float w,
                     float faceHeight = 72f, float size = UiKit.X3)
        {
            UiButton button = UiButton.Create(parent, font, (int)choice, label, style, x, y, w, faceHeight, size);
            button.OnClick += id => Choose((Choice)id);
            buttons[choice] = button;
            return button;
        }

        // Una etiqueta de la lámina: ×1 en mayúsculas espaciadas 4 px. Devuelve dónde acaba.
        float Cap(Transform parent, string value, float x, float y, float w, Color32 color)
        {
            return y + UiKit.Label(parent, font, value, x, y, w, UiKit.X1, color, 22f, 4f, wrap: true).rectTransform.sizeDelta.y;
        }

        // Chips de mecánica con borde de 2 px y 8 de margen, en filas que se parten (flex-wrap).
        float Chips(Transform parent, List<(string name, Color32 border, Color32 ink)> chips, float x, float y, float w)
        {
            float cx = 0f;
            float cy = 0f;
            foreach ((string name, Color32 border, Color32 ink) in chips)
            {
                RectTransform chip = UiKit.Node(parent, "Chip", 0f, 0f, 10f, 38f);
                TextMeshProUGUI label = UiKit.Label(chip, font, name, 10f, 2f, 400f, UiKit.X2, ink, 34f);
                float cw = Mathf.Ceil(UiKit.WidthOf(label)) + 20f;
                label.rectTransform.sizeDelta = new Vector2(cw - 20f, 34f);
                UiKit.Outline(chip, 0f, 0f, cw, 38f, border, 2f);
                if (cx > 0f && cx + cw > w)
                {
                    cx = 0f;
                    cy += 38f + 8f;
                }
                UiKit.Place(chip, x + cx, y + cy, cw, 38f);
                cx += cw + 8f;
            }
            return y + cy + 38f;
        }

        float HeightOf(string value, float w)
        {
            TextMeshProUGUI probe = UiKit.Label(root.transform, font, value, 0f, 0f, w, UiKit.X2, UiKit.Text, 34f, wrap: true);
            float h = probe.rectTransform.sizeDelta.y;
            if (Application.isPlaying)
                Destroy(probe.gameObject);
            else
                DestroyImmediate(probe.gameObject);
            return h;
        }

        // Lo que el mapa añade a lo que el anterior ya permitía (11.1): en el mapa 1, todo lo que hay.
        // Los tipos y la reconversión van en gris; los poderes, en su color de 14.4.
        List<(string, Color32, Color32)> AddedMechanics(LevelDefinition level, UiText t)
        {
            LevelDefinition before = level.Map > 1 ? game.LevelNumbered((level.Map - 2) * LevelDefinition.LevelsPerMap + 1) : null;
            var added = new List<(string, Color32, Color32)>();
            foreach (NodeType type in level.IgnitionTypes)
            {
                if (before == null || !before.AllowsIgnition(type))
                    added.Add((t.TypeName(type), UiKit.Off, UiKit.Text));
            }
            foreach (PowerType power in level.Powers)
            {
                if (before == null || !before.AllowsPower(power))
                    added.Add((t.PowerName(power), PowerColor(power), PowerColor(power)));
            }
            if (level.AllowsConversion && (before == null || !before.AllowsConversion))
                added.Add((t.ConversionMechanic, UiKit.Off, UiKit.Text));
            return added;
        }

        static Color32 PowerColor(PowerType power)
        {
            switch (power)
            {
                case PowerType.AbsoluteZero: return AbsoluteZeroColor;
                case PowerType.Decay: return DecayColor;
                default: return WormholeColor;
            }
        }

        UiText Text => text != null ? text : throw new System.InvalidOperationException("PauseMenu necesita UiText.");
    }
}
