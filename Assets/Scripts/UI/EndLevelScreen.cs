using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nova.UI
{
    // Pantallas de victoria y de derrota (10.3, 13.11, 15.7, T5-05), fieles a las maquetas «Victoria» y
    // «Derrota» del lienzo «NOVA — Pantallas de flujo» (T.2 regla 8, 18 #46), a 1920 x 1080. Entran
    // cuando GameManager congela la partida, sobre el tablero quieto y velado. Solo leen: los datos de
    // MatchRecorder, el tablero de NodeRegistry y, en el estancamiento, las cinco vías que
    // WinConditionChecker guardó al declararlo (18 #40, #41).
    //
    // La coreografía es la de la maqueta, adelantada lo que allí ocupaba el tablero —la cascada en la
    // victoria, el nodo que se apaga en la derrota—, que en el juego ya pasó en tiempo de partida
    // (10.3). Así no pasa de los 3.5 s de 13.12. Los clics llegan por el EventSystem; Esc no hace nada.
    [DefaultExecutionOrder(50)] // UI, 15.10
    [DisallowMultipleComponent]
    public sealed class EndLevelScreen : MonoBehaviour
    {
        internal enum Choice { Next = 0, Repeat = 1, Retry = 2, BackToMap = 3 }

        // La maqueta: contenido de 1600 px a 160 del borde y 72 de arriba, 28 entre bloques, una
        // columna lateral de 520 con 24 de hueco y tarjetas con 24 x 32 de margen interior.
        const float Left = 160f;
        const float Top = 72f;
        const float ContentWidth = 1600f;
        const float Gap = 28f;
        const float Side = 520f;
        const float Gutter = 24f;
        const float PadX = 32f;
        const float PadY = 24f;
        const float IconSize = 64f;
        const int Order = 110;

        // Lo que la coreografía de la maqueta dedicaba al tablero.
        const float VictoryShift = 1f;
        const float DefeatShift = 0.6f;

        [SerializeField] GameManager game;

        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance.")]
        [SerializeField] NodeRegistry nodes;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Textos de la UI (T.7 #39).")]
        [SerializeField] UiText text;

        [Tooltip("Los sprites de nodo de Assets/Sprites/nodes (A.2), por nombre: str_lvl3_enemy…")]
        [SerializeField] Sprite[] nodeSprites = new Sprite[0];

        GameObject root;
        UiMotion motion;
        float shift;
        readonly Dictionary<Choice, UiButton> buttons = new Dictionary<Choice, UiButton>();
        readonly List<string> lines = new List<string>();
        bool subscribed;

        internal bool IsVisible => root != null && root.activeSelf;
        internal LevelOutcome Shown { get; private set; }

        // Todo el texto de la pantalla, con los números ya en su valor final. Para los tests.
        internal IReadOnlyList<string> Lines => lines;
        internal IEnumerable<Choice> Choices => buttons.Keys;

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(GameManager flow, TMP_FontAsset fontAsset, UiText uiText, NodeRegistry registry = null, Sprite[] sprites = null)
        {
            Unsubscribe();
            game = flow;
            font = fontAsset;
            text = uiText;
            nodes = registry;
            nodeSprites = sprites ?? new Sprite[0];
            Subscribe();
        }

        internal void Subscribe()
        {
            if (subscribed || game == null)
                return;
            game.OnLevelEnded += Show;
            game.OnStateChanged += StateChanged;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (subscribed && game != null)
            {
                game.OnLevelEnded -= Show;
                game.OnStateChanged -= StateChanged;
            }
            subscribed = false;
        }

        internal void Choose(Choice choice)
        {
            switch (choice)
            {
                case Choice.Next: game.Next(); break;
                case Choice.Repeat:
                case Choice.Retry: game.Restart(); break;
                case Choice.BackToMap: game.BackToMap(); break;
            }
        }

        void StateChanged(GameFlowState state)
        {
            if (state != GameFlowState.Ended)
                Hide();
        }

        void Show(LevelOutcome outcome)
        {
            Hide();
            Shown = outcome;
            RectTransform stage = UiKit.Stage(transform, "EndLevelScreen", Order, out root, out Image veil);
            motion = root.AddComponent<UiMotion>();
            if (outcome == LevelOutcome.Victory)
            {
                shift = VictoryShift;
                motion.Fade(veil.rectTransform, At(1.1f), 0.6f);
                BuildVictory(stage);
            }
            else
            {
                shift = DefeatShift;
                motion.Fade(veil.rectTransform, At(0.7f), 0.6f);
                BuildDefeat(stage, outcome);
            }
        }

        void Hide()
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
            lines.Clear();
            Shown = LevelOutcome.None;
        }

        // Un retardo de la maqueta, en el tiempo del juego.
        float At(float mockup) => Mathf.Max(0f, mockup - shift);

        // --- Victoria ---

        void BuildVictory(RectTransform stage)
        {
            UiText t = Text;
            LevelDefinition level = game.Level;
            MatchRecorder recorder = game.Recorder;
            float elapsed = recorder != null ? recorder.Elapsed : 0f;

            // Cabecera: rótulo, SISTEMA CONQUISTADO con su relieve y la Estrella N5 del Jugador. La cabecera
            // mide lo que el rótulo y el título (150); la Estrella, de 192, se alinea con su pie y sobresale
            // hacia el margen superior en vez de empujar el resto (maqueta «Victoria», 2026-10-20): así la
            // fila de abajo deja 72 px hasta el borde, como arriba.
            const float HeaderHeight = 150f;
            if (level != null)
                motion.Rise(Cap(stage, t.LevelCaption(level.Map, level.Number), Left, Top + HeaderHeight - 150f, 1300f, UiKit.PlayerCap).rectTransform, At(1.1f));
            Title(stage, t.VictoryTitle, Top + HeaderHeight - 128f, UiKit.Bright, UiKit.PlayerDeep, UiKit.Frame, false);
            Star(stage, Left + ContentWidth - 192f, Top + HeaderHeight - 192f);

            // Datos: cuatro tarjetas que cuentan desde 0.
            float y = Top + HeaderHeight + Gap;
            int own = Registry != null ? Registry.CountOf(Faction.Player) : 0;
            int total = Registry != null ? Registry.Count : 0;
            int peak = recorder != null ? recorder.PeakEnergy : 0;
            int made = recorder != null ? recorder.CapturesMade : 0;
            bool onTime = level == null || elapsed <= level.TargetDuration;
            float cardWidth = (ContentWidth - 3f * Gutter) / 4f;
            const float StatHeight = 217f;
            Stat(stage, 0, y, cardWidth, t.TimeStat, t.Clock(elapsed), p => t.Clock(elapsed * p),
                level != null ? t.TargetDelta(elapsed, level.TargetDuration) : string.Empty, onTime ? UiKit.Player : UiKit.Enemy);
            Stat(stage, 1, y, cardWidth, t.NodesStat, t.Nodes(own, total), p => t.Nodes(Mathf.RoundToInt(own * p), total),
                t.Cascade(recorder != null ? recorder.CascadeConquered : 0), UiKit.Soft);
            Stat(stage, 2, y, cardWidth, t.PeakStat, peak.ToString(), p => Mathf.RoundToInt(peak * p).ToString(),
                t.PeakWhen(recorder != null ? recorder.PeakTime : 0f), UiKit.Soft);
            Stat(stage, 3, y, cardWidth, t.CapturesStat, made.ToString(), p => Mathf.RoundToInt(made * p).ToString(),
                t.Lost(recorder != null ? recorder.CapturesLost : 0), UiKit.Soft);

            // Curva de poder y siguiente nivel.
            y += StatHeight + Gap;
            float curveWidth = ContentWidth - Side - Gutter;
            RectTransform curveFill = UiKit.Card(stage, "PowerCurve", Left, y, curveWidth, 100f, out Image curveCard);
            RectTransform nextFill = UiKit.Card(stage, "NextLevel", Left + curveWidth + Gutter, y, Side, 100f, out Image nextCard);
            float nextHeight = NextLevel(nextFill, t, level);
            float curveHeight = VictoryCurve(curveFill, t, recorder, elapsed, curveWidth);
            float height = Mathf.Max(nextHeight, curveHeight);
            UiKit.Resize(curveCard, height);
            UiKit.Resize(nextCard, height);
            PlaceNextButtons(nextFill, height);
            motion.Rise(curveCard.rectTransform, At(1.75f));
            motion.Rise(nextCard.rectTransform, At(1.85f));
        }

        // Una tarjeta de datos: rótulo, cifra que cuenta en 1.4 s y nota.
        void Stat(RectTransform stage, int index, float y, float w, string key, string final, System.Func<float, string> counting,
                  string note, Color32 noteColor)
        {
            RectTransform fill = UiKit.Card(stage, $"Stat{index}", Left + index * (w + Gutter), y, w, 217f, out Image card);
            float inner = w - 8f - 2f * PadX;
            Cap(fill, key, PadX, PadY, inner, UiKit.Muted);
            TextMeshProUGUI value = Put(fill, final, PadX, PadY + 22f, inner, UiKit.X6, UiKit.Bright, 96f);
            Put(fill, note, PadX, PadY + 22f + 96f, inner, UiKit.X2, noteColor, 43f);
            motion.Add(At(1.5f), 1.4f, UiMotion.OutCubic, p => value.text = counting(p));
            motion.Rise(card.rectTransform, At(1.45f + 0.08f * index));
        }

        float VictoryCurve(RectTransform fill, UiText t, MatchRecorder recorder, float elapsed, float cardWidth)
        {
            float inner = cardWidth - 8f - 2f * PadX;
            Cap(fill, t.CurveVictoryHeader, PadX, PadY + 12f, inner - 300f, UiKit.PlayerCap);
            Legend(fill, PadX + inner, PadY, t);

            IReadOnlyList<float> samples = recorder != null ? recorder.Samples : new List<float>();
            int hold = PowerCurve.HoldIndex(samples);
            float y = PadY + 43f + 12f;
            const float CurveHeight = 208f;
            Curve(fill, PadX, y, inner, CurveHeight, samples, UiKit.EnemyDeep, UiKit.Player,
                recorder != null ? recorder.CascadeSample : -1, hold, hold >= 0 ? t.HoldMark(hold) : null, At(2f), 1.6f, true);
            y += CurveHeight + 12f;
            Axis(fill, PadX, y, inner, elapsed, null);
            return y + 22f + PadY + 8f;
        }

        // Leyenda a la derecha del rótulo: un cuadro de 16 px y el nombre, 8 de hueco, 24 entre los dos.
        void Legend(RectTransform fill, float right, float y, UiText t)
        {
            float x = right;
            foreach ((string name, Color32 color) in new[] { (t.LegendEnemy, UiKit.Enemy), (t.LegendPlayer, UiKit.Player) })
            {
                TextMeshProUGUI label = Put(fill, name, 0f, y, 300f, UiKit.X2, UiKit.Text, 43f);
                float w = Mathf.Ceil(UiKit.WidthOf(label));
                x -= w;
                label.rectTransform.anchoredPosition = new Vector2(x, -y);
                x -= 8f + 16f;
                UiKit.Box(fill, "Swatch", x, y + 14f, 16f, 16f, color);
                x -= 24f;
            }
        }

        // DESBLOQUEADO y el nivel, sus nodos (el Enemigo y los neutrales especiales de 11.2) y con qué
        // empiezas; o los nueve conquistados. Devuelve el alto que pide la tarjeta.
        float NextLevel(RectTransform fill, UiText t, LevelDefinition level)
        {
            float inner = Side - 8f - 2f * PadX;
            LevelDefinition next = game.NextLevel;
            float y = PadY;
            if (next == null)
            {
                y += Put(fill, t.AllConquered, PadX, y, inner, UiKit.X3, UiKit.Bright, 52f, wrap: true).rectTransform.sizeDelta.y + 16f;
            }
            else
            {
                y += Cap(fill, t.Unlocked, PadX, y, inner, UiKit.PlayerCap).rectTransform.sizeDelta.y + 16f;
                y += Put(fill, t.NextLevel(next.Number), PadX, y, inner, UiKit.X4, UiKit.Bright, 64f).rectTransform.sizeDelta.y + 16f;
                List<(NodeType type, int level, Faction faction, int count)> icons = Icons(next);
                for (int i = 0; i < icons.Count; i++)
                {
                    var icon = icons[i];
                    // 64 px, el tamaño nativo del sprite (maqueta «Victoria», 2026-10-20).
                    RectTransform holder = UiKit.Node(fill, "Icon", PadX + i * (IconSize + 8f), y, IconSize, IconSize);
                    UiKit.NodeSprite(holder, "Sprite", SpriteFor(icon.type, icon.level, icon.faction), 0f, 0f, IconSize);
                    if (icon.count > 1)
                        Put(holder, $"×{icon.count}", 0f, IconSize - 22f, IconSize, UiKit.X1, UiKit.Bright, 22f, align: TextAlignmentOptions.TopRight);
                    motion.Rise(holder, At(2.1f + 0.08f * i));
                }
                y += IconSize + 16f;
                y += Put(fill, t.NextStart(Entries(next, t, Faction.Player), CountOf(next, Faction.Enemy)), PadX, y, inner,
                    UiKit.X2, UiKit.Soft, 34f, wrap: true).rectTransform.sizeDelta.y + 16f;
            }
            // Los dos botones, abajo del todo (margin-top: auto).
            return y + 80f + 12f + 80f + PadY + 8f;
        }

        void PlaceNextButtons(RectTransform fill, float cardHeight)
        {
            UiText t = Text;
            float inner = Side - 8f - 2f * PadX;
            float bottom = cardHeight - 8f - PadY;
            bool last = game.NextLevel == null;
            UiButton first = Add(fill, last ? Choice.BackToMap : Choice.Next, last ? t.BackToMap : t.Next, UiButton.Style.Primary,
                PadX, bottom - 80f - 12f - 80f, inner);
            first.SetIcon(UiKit.Arrow, UiKit.Space);
            first.StartShine(At(3f));
            Add(fill, Choice.Repeat, t.Repeat, UiButton.Style.Secondary, PadX, bottom - 80f, inner);
        }

        // La Estrella N5 del Jugador: respira con su halo (4.8 s) y cabecea a pasos (1.6 s).
        void Star(RectTransform stage, float x, float y)
        {
            RectTransform holder = UiKit.Node(stage, "Star", x, y, 192f, 192f);
            Image glow = UiKit.Box(holder, "Glow", -64f, -64f, 320f, 320f, UiKit.Player);
            glow.sprite = UiKit.Glow;
            glow.color = UiKit.With(UiKit.Player, 0.25f);
            Image star = UiKit.NodeSprite(holder, "Sprite", SpriteFor(NodeType.Star, 5, Faction.Player), 0f, 0f, 192f);
            RectTransform glowRect = glow.rectTransform;
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.anchoredPosition += new Vector2(160f, -160f);
            motion.Repeat(0f, 4.8f, phase =>
            {
                float k = (1f - Mathf.Cos(phase * 2f * Mathf.PI)) / 2f;
                glowRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.06f, k);
                glow.color = UiKit.With(UiKit.Player, 0.25f * Mathf.Lerp(0.6f, 1f, k));
            });
            RectTransform starRect = star.rectTransform;
            motion.Repeat(0f, 1.6f, phase => starRect.anchoredPosition = new Vector2(0f, phase < 0.5f ? 0f : 4f));
            motion.Rise(holder, At(1.3f));
        }

        // --- Derrota ---

        void BuildDefeat(RectTransform stage, LevelOutcome outcome)
        {
            UiText t = Text;
            LevelDefinition level = game.Level;
            MatchRecorder recorder = game.Recorder;
            float elapsed = recorder != null ? recorder.Elapsed : 0f;

            // Cabecera: rótulo con el tiempo, SEÑAL PERDIDA con su interferencia y la causa.
            const float HeaderHeight = 150f;
            if (level != null)
                motion.Rise(Cap(stage, $"{t.LevelCaption(level.Map, level.Number)} · {t.Clock(elapsed)}", Left, Top, 1300f, UiKit.EnemyCap).rectTransform, At(0.9f));
            Title(stage, t.DefeatTitle, Top + 22f, UiKit.EnemyText, UiKit.EnemyDeep, UiKit.EnemyDark, true);
            Cause(stage, t.Cause(outcome), Top + HeaderHeight - 16f);

            // Columna lateral: curva y botones.
            float y = Top + HeaderHeight + Gap;
            float sideX = Left + ContentWidth - Side;
            RectTransform curveFill = UiKit.Card(stage, "PowerCurve", sideX, y, Side, 100f, out Image curveCard);
            float curveHeight = DefeatCurve(curveFill, t, recorder, elapsed);
            UiKit.Resize(curveCard, curveHeight);
            motion.Rise(curveCard.rectTransform, At(1.7f));
            float buttonsY = y + curveHeight + Gutter;
            RectTransform retry = UiKit.Node(stage, "RetryHolder", sideX, buttonsY, Side, 80f);
            UiButton retryButton = Add(retry, Choice.Retry, t.Retry, UiButton.Style.Primary, 0f, 0f, Side);
            retryButton.SetIcon(UiKit.Loop, UiKit.Space);
            motion.Rise(retry, At(1.85f));
            RectTransform back = UiKit.Node(stage, "BackHolder", sideX, buttonsY + 80f + Gutter, Side, 80f);
            Add(back, Choice.BackToMap, t.BackToMap, UiButton.Style.Secondary, 0f, 0f, Side);
            motion.Rise(back, At(1.95f));
            float sideHeight = buttonsY + 80f + Gutter + 80f - y;

            // Por qué.
            float whyWidth = ContentWidth - Side - Gutter;
            RectTransform fill = UiKit.Card(stage, "Why", Left, y, whyWidth, 100f, out Image whyCard);
            float height = Mathf.Max(sideHeight, Why(fill, t, outcome, level, elapsed, whyWidth));
            UiKit.Resize(whyCard, height);
            // La pista, abajo del todo (margin-top: auto).
            if (level != null)
                Put(fill, t.Hint(level.Map), PadX, height - 8f - PadY - 34f, whyWidth - 8f - 2f * PadX, UiKit.X2, UiKit.PlayerCap, 34f, wrap: true);
            motion.Rise(whyCard.rectTransform, At(1.6f));
        }

        // Devuelve el alto que pide la tarjeta, contando la pista.
        float Why(RectTransform fill, UiText t, LevelOutcome outcome, LevelDefinition level, float elapsed, float cardWidth)
        {
            float inner = cardWidth - 8f - 2f * PadX;
            float y = PadY;
            y += Cap(fill, t.WhyHeader, PadX, y, inner, UiKit.EnemyCap).rectTransform.sizeDelta.y + 8f;
            if (outcome == LevelOutcome.Stalemate)
            {
                y += Put(fill, t.StalemateWhy(LitEnergy()), PadX, y, inner, UiKit.X3, UiKit.Bright, 52f, wrap: true).rectTransform.sizeDelta.y + 8f;
                y += 8f;
                y += Cap(fill, t.RoutesHeader, PadX, y, inner, UiKit.Muted).rectTransform.sizeDelta.y + 8f;
                IReadOnlyList<StarRoute> routes = game.Checker != null ? game.Checker.Routes : new StarRoute[0];
                int map = level != null ? level.Map : 0;
                for (int i = 0; i < routes.Count; i++)
                    y = Route(fill, t, routes[i], map, i, PadX, y, inner);
            }
            else
            {
                y += Put(fill, t.AnnihilationLine, PadX, y, inner, UiKit.X3, UiKit.Bright, 52f, wrap: true).rectTransform.sizeDelta.y + 8f;
                y += Put(fill, t.AnnihilationWhy(elapsed), PadX, y, inner, UiKit.X2, UiKit.Soft, 34f, wrap: true).rectTransform.sizeDelta.y + 8f;
            }
            return y + 12f + 34f + PadY + 8f;
        }

        // Una vía: raya de 2 px arriba, la X, el nombre y por qué no alcanza a la derecha. Entra
        // descubriéndose de izquierda a derecha y la X se estampa (slide y stamp de la maqueta).
        float Route(RectTransform fill, UiText t, StarRoute route, int map, int index, float x, float y, float w)
        {
            bool absent = route.Block == StarRouteBlock.NotInMap;
            Color32 color = absent ? UiKit.Off : UiKit.Enemy;
            const float Height = 56f;
            RectTransform row = UiKit.Node(fill, $"Route{index}", x, y, w, Height);
            row.gameObject.AddComponent<RectMask2D>();
            CanvasGroup group = UiMotion.Group(row);
            float rest = absent ? 0.6f : 1f;
            UiKit.Box(row, "Rule", 0f, 0f, w, 2f, UiKit.Rule);
            Image cross = UiKit.Box(row, "Cross", 0f, 2f + 10f + 1f, 32f, 32f, color);
            cross.sprite = UiKit.Cross;
            Put(row, t.RouteName(route.Kind), 56f, 12f, w - 56f - 316f, UiKit.X2, UiKit.Text, 34f);
            Put(row, t.RouteWhy(route, map), w - 300f, 12f, 300f, UiKit.X2, color, 34f, align: TextAlignmentOptions.TopRight);

            var mask = row.GetComponent<RectMask2D>();
            Vector2 home = row.anchoredPosition;
            motion.Add(At(2f + 0.16f * index), 0.5f, UiMotion.Orbital, k =>
            {
                mask.padding = new Vector4(0f, 0f, w * (1f - k), 0f);
                row.anchoredPosition = home + new Vector2(-24f * (1f - k), 0f);
                group.alpha = k > 0f ? rest : 0f;
            });
            // stamp: de ×1.8 a ×0.9 en el 60 % y a ×1 al final.
            RectTransform crossRect = cross.rectTransform;
            crossRect.pivot = new Vector2(0.5f, 0.5f);
            crossRect.anchoredPosition += new Vector2(16f, -16f);
            CanvasGroup crossGroup = UiMotion.Group(crossRect);
            motion.Add(At(2.15f + 0.16f * index), 0.45f, UiMotion.Orbital, k =>
            {
                float scale = k < 0.6f ? Mathf.Lerp(1.8f, 0.9f, k / 0.6f) : Mathf.Lerp(0.9f, 1f, (k - 0.6f) / 0.4f);
                crossRect.localScale = Vector3.one * scale;
                crossGroup.alpha = Mathf.Clamp01(k / 0.6f);
            });
            return y + Height;
        }

        float DefeatCurve(RectTransform fill, UiText t, MatchRecorder recorder, float elapsed)
        {
            float inner = Side - 8f - 2f * PadX;
            float y = PadY;
            y += Cap(fill, t.CurveHeader, PadX, y, inner, UiKit.EnemyCap).rectTransform.sizeDelta.y + 12f;
            IReadOnlyList<float> samples = recorder != null ? recorder.Samples : new List<float>();
            int best = PowerCurve.BestIndex(samples);
            const float CurveHeight = 160f;
            Curve(fill, PadX, y, inner, CurveHeight, samples, UiKit.Enemy, UiKit.PlayerDeep, -1, best, null, At(2f), 1.4f, false);
            y += CurveHeight + 12f;
            Axis(fill, PadX, y, inner, elapsed, best >= 0 ? t.BestMark(best) : null);
            return y + 22f + PadY + 8f;
        }

        // La causa, en un sello magenta con la esquina en escalera, abajo a la derecha de la cabecera.
        void Cause(RectTransform stage, string cause, float bottom)
        {
            RectTransform holder = UiKit.Node(stage, "Cause", 0f, 0f, 10f, 80f);
            Image stamp = UiKit.Box(holder, "Stamp", 0f, 0f, 10f, 80f, UiKit.Enemy, true);
            TextMeshProUGUI label = Put(holder, cause, 24f, 12f, 600f, UiKit.X3, UiKit.Space, 56f);
            float w = Mathf.Ceil(UiKit.WidthOf(label)) + 48f;
            stamp.rectTransform.sizeDelta = new Vector2(w, 80f);
            UiKit.Place(holder, Left + ContentWidth - w, bottom - 80f, w, 80f);
            motion.Rise(holder, At(1.5f));
        }

        // --- Piezas comunes ---

        // El título de 149 px con su relieve de dos sombras duras. En la victoria sube y el relieve se
        // extruye; en la derrota entra con la interferencia de «Señal perdida» (13.12, una sola vez).
        void Title(RectTransform stage, string value, float y, Color32 color, Color32 near, Color32 far, bool glitch)
        {
            const float W = 1316f;
            const float H = 144f;
            RectTransform holder = UiKit.Node(stage, "Title", Left, y, W, H);
            TextMeshProUGUI title = Put(holder, value, 0f, 0f, W - 16f, UiKit.X8, color, 128f, 6f);
            List<TextMeshProUGUI> hard = UiKit.Shadows(title, (8f, near), (16f, far));
            if (!glitch)
            {
                motion.Rise(holder, At(1.2f), 0.8f);
                motion.Add(At(1.6f), 1f, UiMotion.Orbital, k =>
                {
                    hard[0].rectTransform.anchoredPosition = new Vector2(8f * k, -8f * k);
                    hard[1].rectTransform.anchoredPosition = new Vector2(16f * k, -16f * k);
                });
                return;
            }

            List<TextMeshProUGUI> chroma = UiKit.Shadows(title, (0f, UiKit.Player), (0f, UiKit.Enemy));
            var clip = holder.gameObject.AddComponent<RectMask2D>();
            CanvasGroup all = UiMotion.Group(holder);
            CanvasGroup hardGroup0 = UiMotion.Group(hard[0].rectTransform);
            CanvasGroup hardGroup1 = UiMotion.Group(hard[1].rectTransform);
            CanvasGroup chromaGroup0 = UiMotion.Group(chroma[0].rectTransform);
            CanvasGroup chromaGroup1 = UiMotion.Group(chroma[1].rectTransform);
            Vector2 home = holder.anchoredPosition;
            // Fotogramas clave de la maqueta: (instante, desplazamiento x, recorte arriba, recorte abajo, desdoble).
            float[,] keys =
            {
                { 0f, -12f, 0.40f, 0.35f, 8f },
                { 0.12f, 10f, 0.10f, 0.60f, 8f },
                { 0.24f, -6f, 0.55f, 0.10f, 6f },
                { 0.36f, 4f, 0f, 0f, 4f },
                { 0.56f, 0f, 0f, 0f, 0f },
                { 1f, 0f, 0f, 0f, 0f },
            };
            motion.Add(At(1f), 0.9f, UiMotion.Linear, k =>
            {
                int i = 0;
                while (i < keys.GetLength(0) - 2 && k > keys[i + 1, 0])
                    i++;
                float u = Mathf.InverseLerp(keys[i, 0], keys[i + 1, 0], k);
                float dx = Mathf.Lerp(keys[i, 1], keys[i + 1, 1], u);
                float top = Mathf.Lerp(keys[i, 2], keys[i + 1, 2], u) * 128f;
                float bottom = Mathf.Lerp(keys[i, 3], keys[i + 1, 3], u) * 128f;
                float split = Mathf.Lerp(keys[i, 4], keys[i + 1, 4], u);
                holder.anchoredPosition = home + new Vector2(dx, 0f);
                clip.padding = new Vector4(0f, bottom + (bottom > 0f ? H - 128f : 0f), 0f, top);
                all.alpha = Mathf.Clamp01(k / 0.12f);
                chroma[0].rectTransform.anchoredPosition = new Vector2(-split, 0f);
                chroma[1].rectTransform.anchoredPosition = new Vector2(split, 0f);
                float settle = Mathf.InverseLerp(0.36f, 0.56f, k);
                chromaGroup0.alpha = chromaGroup1.alpha = 1f - settle;
                hardGroup0.alpha = hardGroup1.alpha = settle;
            });
        }

        // La curva de 13.11: 64 columnas con 2 px de hueco, la del Enemigo arriba y la del Jugador abajo,
        // a escalones de 8 px. Se descubre con un barrido y luego cae la marca.
        void Curve(RectTransform parent, float x, float y, float w, float h, IReadOnlyList<float> samples, Color32 top,
                   Color32 bottom, int cascadeSample, int mark, string markText, float sweepAt, float sweepFor, bool victory)
        {
            float[] columns = PowerCurve.Group(samples);
            int cascadeColumn = cascadeSample >= 0 && cascadeSample < samples.Count ? PowerCurve.ColumnOf(cascadeSample, samples.Count) : -1;
            for (int c = 0; c < columns.Length; c++)
            {
                ColumnSpan(c, columns.Length, w, out float cx, out float cw);
                float enemy = Mathf.Round((1f - columns[c]) * h / 8f) * 8f;
                if (enemy > 0f)
                    UiKit.Box(parent, $"Enemy{c}", x + cx, y, cw, enemy, top);
                if (enemy < h)
                    UiKit.Box(parent, $"Player{c}", x + cx, y + enemy, cw, h - enemy,
                        cascadeColumn >= 0 && c >= cascadeColumn ? UiKit.PlayerLight : bottom);
            }

            // El barrido: una tapa del color del panel que se retira hacia la derecha.
            Image cover = UiKit.Box(parent, "Cover", x, y, w, h, UiKit.Panel);
            RectTransform coverRect = cover.rectTransform;
            motion.Add(sweepAt, sweepFor, UiMotion.Reveal, k => UiKit.Place(coverRect, x + w * k, y, w * (1f - k), h));
            if (victory)
            {
                // En la victoria, una línea de luz lo encabeza y se apaga al llegar.
                RectTransform scanner = UiKit.Node(parent, "Scanner", x, y - 8f, 4f, h + 16f);
                Image halo = UiKit.Box(scanner, "Halo", -16f, -16f, 36f, h + 48f, UiKit.Player);
                halo.sprite = UiKit.Glow;
                halo.color = UiKit.With(UiKit.Player, 0.6f);
                UiKit.Box(scanner, "Line", 0f, 0f, 4f, h + 16f, UiKit.White);
                CanvasGroup glow = UiMotion.Group(scanner);
                motion.Add(sweepAt, sweepFor, UiMotion.Reveal, k => scanner.anchoredPosition = new Vector2(x + w * k - 2f, -(y - 8f)));
                motion.Add(sweepAt + sweepFor, 0.3f, UiMotion.Ease, k => glow.alpha = 1f - k);
            }

            if (mark < 0 || columns.Length == 0)
                return;
            ColumnSpan(PowerCurve.ColumnOf(mark, samples.Count), columns.Length, w, out float mx, out float mw);
            float markX = x + mx + mw / 2f - 2f;
            Image line = UiKit.Box(parent, "Mark", markX, y - 8f, 4f, h + 16f, UiKit.Bright);
            if (victory)
                motion.Drop(line.rectTransform, sweepAt + 1.2f);
            else
            {
                // En la derrota la marca aparece y late dos veces (pulse de la maqueta).
                Image pulse = UiKit.Box(parent, "Pulse", markX - 8f, y - 16f, 20f, h + 32f, UiKit.Bright);
                pulse.sprite = UiKit.Glow;
                pulse.color = UiKit.With(UiKit.Bright, 0f);
                motion.Fade(line.rectTransform, sweepAt + 1.2f, 0.4f);
                motion.Add(sweepAt + 1.4f, 2.4f, UiMotion.Linear, k => pulse.color = UiKit.With(UiKit.Bright, 0.25f * Mathf.Sin(Mathf.PI * Mathf.Repeat(k * 2f, 1f))));
            }

            if (markText == null)
                return;
            // La etiqueta, sobre fondo de espacio, a la derecha de la marca o a su izquierda si no cabe.
            RectTransform tag = UiKit.Node(parent, "MarkTag", 0f, 0f, 10f, 34f);
            Image back = UiKit.Box(tag, "Back", 0f, 0f, 10f, 34f, UiKit.Space);
            TextMeshProUGUI label = Put(tag, markText, 8f, 0f, 600f, UiKit.X2, UiKit.Bright, 34f);
            float tw = Mathf.Ceil(UiKit.WidthOf(label)) + 16f;
            back.rectTransform.sizeDelta = new Vector2(tw, 34f);
            float tagX = markX + 4f + 12f;
            if (tagX + tw > x + w)
                tagX = markX - 12f - tw;
            UiKit.Place(tag, tagX, y - 8f, tw, 34f);
            motion.Drop(tag, sweepAt + 1.3f);
        }

        // Una columna de la curva en píxeles enteros: el ancho se reparte como lo haría flex.
        static void ColumnSpan(int c, int count, float w, out float x, out float width)
        {
            float step = (w + 2f) / count;
            x = Mathf.Round(c * step);
            width = Mathf.Round((c + 1) * step) - x - 2f;
        }

        // Los tiempos bajo la curva. Sin etiqueta central, cinco repartidos; con ella, el principio, la
        // etiqueta y el final.
        void Axis(RectTransform parent, float x, float y, float w, float elapsed, string middle)
        {
            UiText t = Text;
            var marks = new List<(string label, float at)>();
            if (middle == null)
            {
                for (int i = 0; i <= 4; i++)
                    marks.Add((t.Clock(elapsed * i / 4f), i / 4f));
            }
            else
            {
                marks.Add((t.Clock(0f), 0f));
                marks.Add((middle, 0.5f));
                marks.Add((t.Clock(elapsed), 1f));
            }
            foreach ((string label, float at) in marks)
            {
                TextAlignmentOptions align = at <= 0f ? TextAlignmentOptions.TopLeft : at >= 1f ? TextAlignmentOptions.TopRight : TextAlignmentOptions.Top;
                float lw = 400f;
                float lx = at <= 0f ? x : at >= 1f ? x + w - lw : x + w * at - lw / 2f;
                Put(parent, label, lx, y, lw, UiKit.X1, UiKit.Muted, 22f, 2f, align);
            }
        }

        UiButton Add(Transform parent, Choice choice, string label, UiButton.Style style, float x, float y, float w)
        {
            UiButton button = UiButton.Create(parent, font, (int)choice, label, style, x, y, w);
            button.OnClick += id => Choose((Choice)id);
            buttons[choice] = button;
            return button;
        }

        // Una etiqueta de la lámina: ×1 en mayúsculas espaciadas 4 px.
        TextMeshProUGUI Cap(Transform parent, string value, float x, float y, float w, Color32 color)
        {
            return Put(parent, value, x, y, w, UiKit.X1, color, 22f, 4f, wrap: true);
        }

        TextMeshProUGUI Put(Transform parent, string value, float x, float y, float w, float size, Color32 color, float lineHeight,
                            float spacing = 0f, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool wrap = false)
        {
            lines.Add(value);
            return UiKit.Label(parent, font, value, x, y, w, size, color, lineHeight, spacing, align, wrap);
        }

        // Los nodos de partida de un bando agrupados por tipo y nivel, para los iconos: el Enemigo y,
        // de los neutrales, solo los especiales (11.2).
        static List<(NodeType type, int level, Faction faction, int count)> Icons(LevelDefinition level)
        {
            var icons = new List<(NodeType, int, Faction, int)>();
            foreach (Faction faction in new[] { Faction.Enemy, Faction.Neutral })
            {
                foreach (var group in Groups(level, faction))
                    icons.Add((group.type, group.level, faction, group.count));
            }
            return icons;
        }

        static List<(NodeType type, int level, int count)> Groups(LevelDefinition level, Faction faction)
        {
            var order = new List<(NodeType, int)>();
            var counts = new Dictionary<(NodeType, int), int>();
            foreach (NodeSpawnData spawn in level.Spawns)
            {
                if (spawn.Faction != faction || (faction == Faction.Neutral && spawn.Type == NodeType.Asteroid))
                    continue;
                // Nivel 0 es el de partida del tipo: 1 para un nodo encendido.
                var key = (spawn.Type, spawn.Type == NodeType.Asteroid ? 0 : Mathf.Max(1, spawn.Level));
                if (counts.TryGetValue(key, out int count))
                    counts[key] = count + 1;
                else
                {
                    counts.Add(key, 1);
                    order.Add(key);
                }
            }
            var groups = new List<(NodeType, int, int)>(order.Count);
            foreach (var key in order)
                groups.Add((key.Item1, key.Item2, counts[key]));
            return groups;
        }

        // «Estrella(2), Magnetar(1)», como las columnas de 11.2.
        static string Entries(LevelDefinition level, UiText t, Faction faction)
        {
            var entries = new List<string>();
            foreach (var group in Groups(level, faction))
                entries.Add(t.NodeEntry(group.type, group.level, group.count));
            return string.Join(", ", entries);
        }

        static int CountOf(LevelDefinition level, Faction faction)
        {
            int count = 0;
            foreach (NodeSpawnData spawn in level.Spawns)
            {
                if (spawn.Faction == faction)
                    count++;
            }
            return count;
        }

        // Sprites de A.2 por su nombre de archivo: str_lvl3_enemy, ast_lvl0_neutral…
        Sprite SpriteFor(NodeType type, int level, Faction faction)
        {
            string prefix = type == NodeType.Star ? "str" : type == NodeType.Pulsar ? "pul" : type == NodeType.Magnetar ? "mag" : "ast";
            string name = $"{prefix}_lvl{level}_{faction.ToString().ToLowerInvariant()}";
            foreach (Sprite sprite in nodeSprites)
            {
                if (sprite != null && sprite.name == name)
                    return sprite;
            }
            return null;
        }

        // La S de 10.2: la Energía de los nodos encendidos del Jugador.
        int LitEnergy()
        {
            int sum = 0;
            if (Registry == null)
                return sum;
            foreach (NodeController node in Registry.GetNodes(Faction.Player))
            {
                if (node != null && node.IsInitialized && node.Type != NodeType.Asteroid)
                    sum += node.Energy;
            }
            return sum;
        }

        NodeRegistry Registry => nodes != null ? nodes : NodeRegistry.Instance;
        UiText Text => text != null ? text : throw new System.InvalidOperationException("EndLevelScreen necesita UiText.");
    }
}
