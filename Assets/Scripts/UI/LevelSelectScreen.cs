using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nova.UI
{
    // Carta estelar (13.9, 15.7, T5-06), fiel a la maqueta «LevelSelect» del lienzo «NOVA — Pantallas
    // de flujo» (T.2 regla 8, 18 #46) a 1920 x 1080, sin scroll. Arriba, la carta: los fondos de los
    // tres mapas —tres BackgroundView del tablero con su ventana, debajo del lienzo— y los nueve
    // niveles en una ruta. Abajo, el informe del nivel seleccionado: al entrar, el siguiente; con un
    // clic, cualquier otro, también uno bloqueado, que se consulta pero no se lanza.
    //
    // Todo sale de ProgressSaver —el máximo desbloqueado, y nada más— y de los nueve LevelDefinition:
    // la pantalla no guarda ni duplica datos de nivel. Esc o ◂ MENÚ vuelven al menú principal.
    [DefaultExecutionOrder(50)] // UI, 15.10
    [DisallowMultipleComponent]
    public sealed class LevelSelectScreen : MonoBehaviour
    {
        internal enum NodeState { Conquered, Next, Locked }

        // La maqueta: cabecera de 96 px, tres franjas de 640 x 624 y el informe de 1872 x 320 en (24, 736).
        const float Header = 96f;
        const float BandW = 640f;
        const float BandH = 624f;
        const float ReportX = 24f;
        const float ReportY = 736f;
        const float ReportW = 1872f;
        const float ReportH = 320f;

        // El centro de cada nivel en la carta, del 1 al 9.
        static readonly Vector2[] Spots =
        {
            new Vector2(120f, 520f), new Vector2(300f, 340f), new Vector2(500f, 540f),
            new Vector2(770f, 330f), new Vector2(960f, 560f), new Vector2(1140f, 340f),
            new Vector2(1410f, 540f), new Vector2(1600f, 330f), new Vector2(1800f, 520f),
        };

        // Las columnas del informe: 260 · resto · 440 · 240, con 32 de hueco y 24 x 32 de margen.
        const float PadX = 32f;
        const float PadY = 24f;
        const float Col1 = 260f;
        const float Col3 = 440f;
        const float Col4 = 240f;
        const float Gap = 32f;

        // Colores de 14.4 para los chips de poder; el del Gusano, en el tono claro de su rampa para
        // que se lea sobre el panel, como en la maqueta.
        static readonly Color32 AbsoluteZeroColor = UiKit.Hex(0xB8F0FF);
        static readonly Color32 DecayColor = UiKit.Hex(0x7CF23F);
        static readonly Color32 WormholeColor = UiKit.Hex(0xC9A2FF);
        static readonly Color32 PipNext = UiKit.Hex(0x2B5CA6);
        static readonly Color32 PipLocked = UiKit.Hex(0x22203F);
        static readonly Color32 OffBorder = UiKit.Hex(0x22203F);

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Textos de la UI (T.7 #39).")]
        [SerializeField] UiText text;

        [Tooltip("Los nueve niveles, en orden (11.2).")]
        [SerializeField] LevelDefinition[] levels = Array.Empty<LevelDefinition>();

        [Tooltip("Los sprites de nodo de Assets/Sprites/nodes (A.2), por nombre: str_lvl1_player…")]
        [SerializeField] Sprite[] nodeSprites = Array.Empty<Sprite>();

        [Tooltip("world_link_sheet de Assets/Sprites/world/link (ART-WLD-LINK).")]
        [SerializeField] Texture2D linkSheet;

        sealed class Spot
        {
            public RectTransform Lift;
            public Vector2 LiftHome;
            public TextMeshProUGUI Label;
            public Color32 LabelColor;
            public RectTransform Brackets;
            public float Hover;
            public float HoverTarget;
        }

        Action<string> loadScene = SceneManager.LoadScene;
        GameObject root;
        UiMotion motion;
        RectTransform reportFill;
        RectTransform report;
        RectTransform back;
        Vector2 backHome;
        TextMeshProUGUI backLabel;
        Image backArrow;
        float backHover;
        float backTarget;
        readonly List<Spot> spots = new List<Spot>();
        readonly List<bool> lit = new List<bool>();
        readonly List<string> reportTexts = new List<string>();
        int max;
        int selected;
        UiButton launch;

        internal bool IsBuilt => root != null;
        internal int Selected => selected;
        internal int Max => max;
        internal bool CanLaunch => launch != null;
        internal bool SegmentLit(int from) => lit[from - 1];
        internal IReadOnlyList<string> ReportTexts => reportTexts;
        internal string ConqueredText { get; private set; }

        internal NodeState StateOf(int level) =>
            level < max ? NodeState.Conquered : level == max ? NodeState.Next : NodeState.Locked;

        void Start()
        {
            Time.timeScale = 1f;
            Build();
        }

        void Update()
        {
            if (root == null)
                return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                BackToMenu();
                return;
            }
            float dt = Time.unscaledDeltaTime;
            foreach (Spot spot in spots)
            {
                spot.Hover = Mathf.MoveTowards(spot.Hover, spot.HoverTarget, dt / 0.35f);
                float k = UiMotion.Orbital(spot.Hover);
                spot.Lift.anchoredPosition = spot.LiftHome + new Vector2(0f, 8f * k);
                spot.Label.color = Color.Lerp(spot.LabelColor, UiKit.Bright, Mathf.Clamp01(spot.Hover / 0.7f));
            }
            backHover = Mathf.MoveTowards(backHover, backTarget, dt / 0.3f);
            back.anchoredPosition = backHome + new Vector2(-6f * UiMotion.Orbital(backHover), 0f);
            backLabel.color = Color.Lerp(UiKit.Soft, UiKit.Bright, backHover);
            backArrow.color = backLabel.color;
        }

        // Para la escena y los tests: el cableado normal es por el Inspector. sceneLoader sustituye a
        // SceneManager.LoadScene, que en EditMode no se puede llamar.
        public void Configure(TMP_FontAsset fontAsset, UiText uiText, LevelDefinition[] allLevels, Sprite[] sprites, Texture2D sheet,
                              Action<string> sceneLoader = null)
        {
            font = fontAsset;
            text = uiText;
            levels = allLevels ?? Array.Empty<LevelDefinition>();
            nodeSprites = sprites ?? Array.Empty<Sprite>();
            linkSheet = sheet;
            loadScene = sceneLoader ?? SceneManager.LoadScene;
        }

        // Esc y ◂ MENÚ.
        internal void BackToMenu()
        {
            loadScene(GameManager.MainMenuScene);
        }

        // Lanzar: solo un nivel desbloqueado.
        internal void Launch()
        {
            LevelDefinition level = LevelNumbered(selected);
            if (level == null || selected > max)
                return;
            GameManager.Request(level);
            loadScene(GameManager.GameScene);
        }

        // Selecciona un nivel y rehace el informe.
        internal void Select(int level)
        {
            if (level < 1 || level > Spots.Length)
                return;
            selected = level;
            for (int i = 0; i < spots.Count; i++)
                Bracket(spots[i], i + 1 == level, i + 1 > max);
            BuildReport(motion.Time);
        }

        internal void Finish()
        {
            motion.Finish();
        }

        internal void Build()
        {
            if (root != null)
                Discard(root);
            spots.Clear();
            lit.Clear();
            max = ProgressSaver.MaxUnlocked;
            selected = Mathf.Min(max, ProgressSaver.LastLevel);
            RectTransform stage = UiKit.Stage(transform, "LevelSelect", 0, out root, out Image veil);
            // Sin velo: los fondos de las franjas se ven enteros.
            veil.enabled = false;
            motion = root.AddComponent<UiMotion>();
            UiText t = Text;

            BuildBands(stage, t);
            BuildRoute(stage);
            BuildSpots(stage, t);
            BuildHeader(stage, t);
            Image card;
            reportFill = UiKit.Card(stage, "Report", ReportX, ReportY, ReportW, ReportH, out card);
            motion.Rise(card.rectTransform, 0.5f, 0.8f, 16f);
            Select(selected);
        }

        // Las tres franjas: atenuada la de un mapa al que no se ha llegado, rótulo de cada mapa y
        // separadores discontinuos.
        void BuildBands(RectTransform stage, UiText t)
        {
            for (int map = 1; map <= 3; map++)
            {
                int first = (map - 1) * LevelDefinition.LevelsPerMap + 1;
                bool reached = first <= max;
                float x = (map - 1) * BandW;
                if (!reached)
                {
                    Image dim = UiKit.Box(stage, "Dim", x, Header, BandW, BandH, UiKit.Space);
                    dim.sprite = UiKit.Dither;
                    dim.type = Image.Type.Tiled;
                    dim.color = UiKit.With(UiKit.Space, 0.85f);
                }

                RectTransform holder = UiKit.Node(stage, $"Map{map}", x + 40f, 120f, 560f, 140f);
                UiMotion.Group(holder).alpha = reached ? 1f : 0.45f;
                RectTransform caption = UiKit.Node(holder, "Caption", 0f, 0f, 560f, 140f);
                float y = Cap(caption, t.MapCap(map), 0f, 0f, 560f, UiKit.PlayerCap);
                y += UiKit.Label(caption, font, t.MapName(map), 0f, y, 560f, UiKit.X3, UiKit.Bright, 52f).rectTransform.sizeDelta.y;
                LevelDefinition a = LevelNumbered(first);
                LevelDefinition b = LevelNumbered(first + 2);
                if (a != null && b != null)
                    UiKit.Label(caption, font, t.MapRange(first, first + 2, a.TargetDuration, b.TargetDuration), 0f, y, 560f, UiKit.X2, UiKit.Muted, 43f);
                motion.Rise(caption, 0.1f + 0.12f * map, 0.7f, 16f);
            }
            foreach (float x in new[] { BandW - 2f, 2f * BandW - 2f })
            {
                for (float y = Header; y < Header + BandH; y += 16f)
                    UiKit.Box(stage, "Separator", x, y, 4f, 8f, UiKit.KeyFrame);
            }
        }

        // La ruta: plasma del Jugador hasta el siguiente nivel, discontinua y gris después.
        void BuildRoute(RectTransform stage)
        {
            for (int j = 1; j < Spots.Length; j++)
            {
                bool on = j + 1 <= max;
                lit.Add(on);
                Vector2 a = Spots[j - 1];
                Vector2 b = Spots[j];
                RectTransform segment;
                if (on)
                    segment = UiPlasma.Create(stage, linkSheet, a, b, j * 5).GetComponent<RectTransform>();
                else
                    segment = Dashed(stage, a, b);
                motion.Fade(segment, 0.35f + 0.16f * j, on ? 0.7f : 0.8f, UiMotion.Orbital);
            }
        }

        // Una línea discontinua gris de 4 px: trazos de 8 y huecos de 12.
        RectTransform Dashed(RectTransform stage, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            RectTransform rect = UiKit.Node(stage, "Dashed", 0f, 0f, length, 4f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(a.x, -a.y);
            rect.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            for (float x = 0f; x < length; x += 20f)
                UiKit.Box(rect, "Dash", x, 0f, Mathf.Min(8f, length - x), 4f, UiKit.KeyFrame);
            return rect;
        }

        // Los nueve niveles: Estrella ganada, Asteroide propio con sonar y anillo, o Asteroide neutral.
        void BuildSpots(RectTransform stage, UiText t)
        {
            for (int i = 1; i <= Spots.Length; i++)
            {
                NodeState state = StateOf(i);
                Vector2 c = Spots[i - 1];
                RectTransform node = UiKit.Node(stage, $"Level{i}", c.x - 80f, c.y - 64f, 160f, 208f);
                RectTransform holder = UiKit.Node(node, "Holder", 16f, 0f, 128f, 128f);

                if (state == NodeState.Next)
                {
                    for (int s = 0; s < 2; s++)
                    {
                        Image sonar = UiKit.Box(holder, "Sonar", 16f, 16f, 96f, 96f, UiKit.Player);
                        sonar.sprite = UiKit.Ring;
                        RectTransform r = sonar.rectTransform;
                        r.pivot = new Vector2(0.5f, 0.5f);
                        r.anchoredPosition += new Vector2(48f, -48f);
                        motion.Repeat(1.2f * s, 2.4f, k =>
                        {
                            float e = UiMotion.Orbital(k);
                            r.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.9f, e);
                            sonar.color = UiKit.With(UiKit.Player, Mathf.Lerp(0.8f, 0f, e));
                        });
                    }
                    Image ring = UiKit.Box(holder, "Ring", 0f, 0f, 128f, 128f, UiKit.Player);
                    ring.sprite = UiKit.DashedRing;
                    RectTransform rr = ring.rectTransform;
                    rr.pivot = new Vector2(0.5f, 0.5f);
                    rr.anchoredPosition += new Vector2(64f, -64f);
                    motion.Repeat(0f, 9f, k => rr.localRotation = Quaternion.Euler(0f, 0f, -360f * k));
                }

                Image brackets = UiKit.Box(holder, "Brackets", 0f, 0f, 128f, 128f, UiKit.Bright);
                brackets.sprite = UiKit.Brackets;
                RectTransform br = brackets.rectTransform;
                br.pivot = new Vector2(0.5f, 0.5f);
                br.anchoredPosition += new Vector2(64f, -64f);

                Sprite sprite = state == NodeState.Conquered ? Sprite(NodeType.Star, 1, Faction.Player)
                    : state == NodeState.Next ? Sprite(NodeType.Asteroid, 0, Faction.Player)
                    : Sprite(NodeType.Asteroid, 0, Faction.Neutral);
                RectTransform lift = UiKit.Node(holder, "Lift", 16f, 16f, 96f, 96f);
                Image body = UiKit.NodeSprite(lift, "Sprite", sprite, 0f, 0f, 96f);
                if (state == NodeState.Locked && sprite != null)
                    body.color = UiKit.With(UiKit.White, 0.45f);
                else
                    Bob(body.rectTransform);

                Color32 labelColor = state == NodeState.Locked ? UiKit.Off : state == NodeState.Next ? UiKit.Player : UiKit.Soft;
                TextMeshProUGUI label = UiKit.Label(node, font, t.NodeLevel(i), 0f, 132f, 160f,
                    UiKit.X2, labelColor, 32f, align: TextAlignmentOptions.Top);
                if (state == NodeState.Locked)
                {
                    // El ▪ de un nivel bloqueado: un cuadro de 8 px antes del texto (Jersey 10 no lo tiene).
                    float textW = Mathf.Ceil(UiKit.WidthOf(label));
                    float x0 = (160f - (8f + 8f + textW)) / 2f;
                    UiKit.Box(node, "Lock", x0, 132f + 12f, 8f, 8f, labelColor);
                    label.rectTransform.anchoredPosition += new Vector2(8f, 0f);
                }
                if (state == NodeState.Next)
                    UiKit.Label(node, font, t.NextTag, 0f, 168f, 160f, UiKit.X1, UiKit.Player, 22f, 4f, TextAlignmentOptions.Top);

                var spot = new Spot { Lift = lift, LiftHome = lift.anchoredPosition, Label = label, LabelColor = labelColor, Brackets = br };
                spots.Add(spot);
                Bracket(spot, false, state == NodeState.Locked);

                UiHit hit = UiHit.Add(node);
                int level = i;
                hit.OnEnter += () => spot.HoverTarget = 1f;
                hit.OnExit += () => spot.HoverTarget = 0f;
                hit.OnClick += () => Select(level);

                // pop: sube 16 px y crece de 0.9 en 0.7 s, escalonado 160 ms.
                CanvasGroup group = UiMotion.Group(node);
                Vector2 home = node.anchoredPosition;
                motion.Add(0.2f + 0.16f * i, 0.7f, UiMotion.Orbital, k =>
                {
                    group.alpha = k;
                    node.anchoredPosition = home + new Vector2(0f, -16f * (1f - k));
                    node.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, k);
                });
            }
        }

        // Los corchetes del seleccionado entran cerrándose de 1.35 en 0.45 s (lockon).
        void Bracket(Spot spot, bool on, bool locked)
        {
            spot.Brackets.gameObject.SetActive(on);
            if (!on)
                return;
            Image image = spot.Brackets.GetComponent<Image>();
            Color32 color = locked ? UiKit.Muted : UiKit.Bright;
            RectTransform r = spot.Brackets;
            if (motion == null)
                return;
            motion.Add(motion.Time, 0.45f, UiMotion.Orbital, k =>
            {
                r.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, k);
                image.color = UiKit.With(color, k);
            }, r);
        }

        // La cabecera: ◂ MENÚ, CARTA ESTELAR y los conquistados con sus nueve marcas.
        void BuildHeader(RectTransform stage, UiText t)
        {
            UiKit.Box(stage, "Header", 0f, 0f, UiKit.Width, Header, UiKit.Space);
            UiKit.Box(stage, "HeaderRule", 0f, Header - 4f, UiKit.Width, 4f, UiKit.Rule);

            // ◂ no está en Jersey 10: es el ▸ de UiKit, girado, a ×2 —la altura de una mayúscula a ×2—.
            back = UiKit.Node(stage, "Back", 48f, (Header - 40f) / 2f, 220f, 40f);
            backArrow = UiKit.Box(back, "Arrow", 0f, 10f, 12f, 20f, UiKit.Soft);
            backArrow.sprite = UiKit.Arrow;
            backArrow.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            backArrow.rectTransform.pivot = new Vector2(0.5f, 1f);
            backArrow.rectTransform.anchoredPosition += new Vector2(6f, 0f);
            backLabel = UiKit.Label(back, font, t.BackToMenu, 20f, 0f, 200f, UiKit.X2, UiKit.Soft, 40f);
            back.sizeDelta = new Vector2(20f + Mathf.Ceil(UiKit.WidthOf(backLabel)), 40f);
            backHome = back.anchoredPosition;
            UiHit hit = UiHit.Add(back);
            hit.OnEnter += () => backTarget = 1f;
            hit.OnExit += () => backTarget = 0f;
            hit.OnClick += BackToMenu;

            TextMeshProUGUI title = UiKit.Label(stage, font, t.StarChart, 0f, (Header - 4f - 64f) / 2f, UiKit.Width, UiKit.X3, UiKit.Bright, 64f, 8f,
                TextAlignmentOptions.Top);
            motion.Fade(title.rectTransform, 0f, 0.8f);

            const float PipW = 16f;
            const float PipGap = 4f;
            float pipsX = UiKit.Width - 48f - (9f * PipW + 8f * PipGap);
            for (int k = 1; k <= 9; k++)
            {
                Color32 color = k < max ? UiKit.Player : k == max ? PipNext : PipLocked;
                Image pip = UiKit.Box(stage, $"Pip{k}", pipsX + (k - 1) * (PipW + PipGap), (Header - 4f - 24f) / 2f, PipW, 24f, color);
                RectTransform r = pip.rectTransform;
                r.pivot = new Vector2(0.5f, 0f);
                r.anchoredPosition += new Vector2(PipW / 2f, -24f);
                motion.Add(0.3f + 0.05f * k, 0.4f, UiMotion.Orbital, s => r.localScale = new Vector3(1f, s, 1f));
            }
            ConqueredText = t.ConqueredCount(ProgressSaver.Conquered);
            TextMeshProUGUI count = UiKit.Label(stage, font, ConqueredText, 0f, (Header - 4f - 40f) / 2f, 600f, UiKit.X2, UiKit.Soft, 40f);
            float w = Mathf.Ceil(UiKit.WidthOf(count));
            count.rectTransform.anchoredPosition = new Vector2(pipsX - 16f - w, count.rectTransform.anchoredPosition.y);
        }

        // El informe del nivel seleccionado, en cuatro columnas que entran escalonadas.
        void BuildReport(float start)
        {
            if (report != null)
                Discard(report.gameObject);
            reportTexts.Clear();
            launch = null;
            UiText t = Text;
            LevelDefinition level = LevelNumbered(selected);
            report = UiKit.Node(reportFill, "Content", PadX, PadY, ReportW - 8f - 2f * PadX, ReportH - 8f - 2f * PadY);
            if (level == null)
                return;
            float inner = report.sizeDelta.x;
            float col2 = inner - Col1 - Col3 - Col4 - 3f * Gap;
            float x2 = Col1 + Gap;
            float x3 = x2 + col2 + Gap;
            float x4 = x3 + Col3 + Gap;

            // 1. Mapa, nivel, duración y, en el primero de cada mapa, lo que introduce.
            RectTransform c1 = UiKit.Node(report, "Level", 0f, 0f, Col1, report.sizeDelta.y);
            float y = Cap(c1, Record(t.ReportMap(level.Map)), 0f, 0f, Col1, UiKit.PlayerCap) + 4f;
            y += UiKit.Label(c1, font, Record(t.NextLevel(level.Number)), 0f, y, Col1, UiKit.X6, UiKit.Bright, 96f).rectTransform.sizeDelta.y + 4f;
            UiKit.Label(c1, font, Record(t.TargetDuration(level.TargetDuration)), 0f, y, Col1, UiKit.X2, UiKit.Soft, 43f);
            motion.Rise(c1, start, 0.5f, 16f);

            // 2. Los nodos iniciales en tres grupos.
            RectTransform c2 = UiKit.Node(report, "Sides", x2, 0f, col2, report.sizeDelta.y);
            float gx = 0f;
            int step = 0;
            foreach ((string title, Color32 color, Faction faction) in new[]
                     {
                         (t.SideYou, UiKit.Player, Faction.Player),
                         (t.SideEnemy, UiKit.Enemy, Faction.Enemy),
                         (t.SideNeutrals, UiKit.Muted, Faction.Neutral),
                     })
            {
                List<(NodeType type, int lvl, int count)> units = Units(level, faction);
                if (units.Count == 0)
                    continue;
                RectTransform group = UiKit.Node(c2, title, gx, 0f, 400f, report.sizeDelta.y);
                TextMeshProUGUI cap = UiKit.Label(group, font, Record(title), 0f, 0f, 400f, UiKit.X1, color, 22f, 4f);
                float ux = 0f;
                foreach ((NodeType type, int lvl, int count) in units)
                {
                    RectTransform unit = UiKit.Node(group, "Unit", ux, 30f, 96f, 126f);
                    UiKit.NodeSprite(unit, "Sprite", Sprite(type, lvl, faction), 0f, 0f, 96f);
                    UiKit.Label(unit, font, Record(t.UnitLevel(lvl)), 0f, 96f, 96f, UiKit.X2, UiKit.Soft, 30f, align: TextAlignmentOptions.Top);
                    if (count > 1)
                    {
                        RectTransform badge = UiKit.Node(unit, "Count", 0f, 0f, 60f, 30f);
                        Image fill = badge.gameObject.AddComponent<Image>();
                        fill.sprite = UiKit.Square;
                        fill.color = UiKit.Text;
                        fill.raycastTarget = false;
                        TextMeshProUGUI n = UiKit.Label(badge, font, Record(t.UnitCount(count)), 6f, 0f, 60f, UiKit.X2, UiKit.Space, 30f);
                        float bw = Mathf.Ceil(UiKit.WidthOf(n)) + 12f;
                        UiKit.Place(badge, 96f + 4f - bw, 0f, bw, 30f);
                    }
                    step++;
                    motion.Rise(unit, start + 0.08f + 0.05f * step, 0.5f, 16f);
                    ux += 96f + 8f;
                }
                gx += Mathf.Max(ux - 8f, Mathf.Ceil(UiKit.WidthOf(cap))) + 24f;
            }
            // En el primer nivel de cada mapa, lo que introduce (11.1), debajo de los nodos: en la
            // columna del nivel, de 260 px, ocupaba cuatro líneas y se salía del informe.
            if ((level.Number - 1) % LevelDefinition.LevelsPerMap == 0)
                UiKit.Label(c2, font, Record(t.Introduced(level.Map)), 0f, 30f + 126f + 12f, col2, UiKit.X2, UiKit.Player, 32f, wrap: true);
            motion.Rise(c2, start + 0.06f, 0.5f, 16f);

            // 3. Lo que el mapa permite; lo que no, tachado en gris (18 #36).
            RectTransform c3 = UiKit.Node(report, "Mechanics", x3, 0f, Col3, report.sizeDelta.y);
            y = Cap(c3, Record(t.IgnitionHeader), 0f, 0f, Col3, UiKit.Muted) + 6f;
            var ignition = new List<(string, Color32, Color32, bool)>();
            foreach (NodeType type in new[] { NodeType.Star, NodeType.Pulsar, NodeType.Magnetar })
            {
                bool allowed = level.AllowsIgnition(type);
                ignition.Add((t.TypeName(type), allowed ? UiKit.Off : OffBorder, allowed ? UiKit.Bright : UiKit.Off, allowed));
            }
            y = Chips(c3, ignition, 0f, y, Col3) + 10f;
            y = Cap(c3, Record(t.PulsarPowersHeader), 0f, y, Col3, UiKit.Muted) + 6f;
            var powers = new List<(string, Color32, Color32, bool)>();
            foreach (PowerType power in new[] { PowerType.AbsoluteZero, PowerType.Decay, PowerType.Wormhole })
            {
                bool allowed = level.AllowsPower(power);
                Color32 color = PowerColor(power);
                powers.Add((t.PowerName(power), allowed ? color : OffBorder, allowed ? color : UiKit.Off, allowed));
            }
            y = Chips(c3, powers, 0f, y, Col3) + 10f;
            string yesNo = t.YesNo(level.AllowsConversion);
            string value = $"<color=#{ColorUtility.ToHtmlStringRGB(level.AllowsConversion ? UiKit.Bright : UiKit.Off)}>{yesNo}</color>";
            Record(t.ConversionHeader(yesNo));
            Cap(c3, t.ConversionHeader(value), 0f, y, Col3, UiKit.Muted);
            motion.Rise(c3, start + 0.12f, 0.5f, 16f);

            // 4. Lanzar, o bloqueado y qué hay que ganar.
            RectTransform c4 = UiKit.Node(report, "Action", x4, (report.sizeDelta.y - 144f) / 2f, Col4, 144f);
            if (selected <= max)
            {
                launch = UiButton.Create(c4, font, 0, Record(t.Launch), UiButton.Style.Primary, 0f, 0f, Col4, 136f, UiKit.X4);
                TextMeshProUGUI label = launch.Label;
                label.alignment = TextAlignmentOptions.Top;
                label.rectTransform.anchoredPosition = new Vector2(0f, -21f);
                label.rectTransform.sizeDelta = new Vector2(Col4 - 8f, 64f);
                UiKit.Label(label.transform.parent, font, Record(t.NextLevel(level.Number)), 0f, 85f, Col4 - 8f, UiKit.X2, UiKit.Space, 30f,
                    align: TextAlignmentOptions.Top);
                launch.StartShine(1.4f);
                launch.OnClick += _ => Launch();
            }
            else
            {
                UiKit.Box(c4, "Frame", 0f, 0f, Col4, 144f, UiKit.KeyFrame, true);
                UiKit.Box(c4, "Face", 4f, 4f, Col4 - 8f, 136f, PipLocked, true);
                UiKit.Label(c4, font, Record(t.Locked), 0f, 22f, Col4, UiKit.X3, UiKit.Muted, 52f, align: TextAlignmentOptions.Top);
                UiKit.Label(c4, font, Record(t.WinToUnlock(max)), 0f, 80f, Col4, UiKit.X2, UiKit.Muted, 30f, align: TextAlignmentOptions.Top);
            }
            motion.Rise(c4, start + 0.18f, 0.5f, 16f);
        }

        // Los nodos de un bando, agrupados por tipo y nivel en el orden del nivel.
        static List<(NodeType, int, int)> Units(LevelDefinition level, Faction faction)
        {
            var units = new List<(NodeType type, int lvl, int count)>();
            foreach (NodeSpawnData spawn in level.Spawns)
            {
                if (spawn.Faction != faction)
                    continue;
                int lvl = spawn.Type == NodeType.Asteroid ? 0 : Mathf.Max(1, spawn.Level);
                int index = units.FindIndex(u => u.type == spawn.Type && u.lvl == lvl);
                if (index >= 0)
                    units[index] = (spawn.Type, lvl, units[index].count + 1);
                else
                    units.Add((spawn.Type, lvl, 1));
            }
            return units;
        }

        // Chips con borde de 2 px y 8 de margen; los que el mapa no permite, tachados.
        float Chips(Transform parent, List<(string name, Color32 border, Color32 ink, bool on)> chips, float x, float y, float w)
        {
            float cx = 0f;
            float cy = 0f;
            foreach ((string name, Color32 border, Color32 ink, bool on) in chips)
            {
                Record(name);
                RectTransform chip = UiKit.Node(parent, "Chip", 0f, 0f, 10f, 38f);
                TextMeshProUGUI label = UiKit.Label(chip, font, on ? name : $"<s>{name}</s>", 10f, 2f, 400f, UiKit.X2, ink, 34f);
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

        // Una etiqueta de la lámina: ×1 en mayúsculas espaciadas 4 px. Devuelve dónde acaba.
        float Cap(Transform parent, string value, float x, float y, float w, Color32 color)
        {
            return y + UiKit.Label(parent, font, value, x, y, w, UiKit.X1, color, 22f, 4f, wrap: true).rectTransform.sizeDelta.y;
        }

        // Lo que el informe enseña, para los tests.
        string Record(string value)
        {
            reportTexts.Add(value);
            return value;
        }

        // El cabeceo de la materia: 4 px arriba la mitad del ciclo de 1.6 s, a pasos (13.12).
        void Bob(RectTransform target)
        {
            Vector2 home = target.anchoredPosition;
            motion.Repeat(0f, 1.6f, k => target.anchoredPosition = home + new Vector2(0f, k < 0.5f ? 0f : 4f));
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

        Sprite Sprite(NodeType type, int level, Faction faction) => UiKit.NodeSpriteFor(nodeSprites, type, level, faction);

        LevelDefinition LevelNumbered(int number)
        {
            foreach (LevelDefinition level in levels)
            {
                if (level != null && level.Number == number)
                    return level;
            }
            return null;
        }

        static void Discard(GameObject go)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        UiText Text => text != null ? text : throw new InvalidOperationException("LevelSelectScreen necesita UiText.");
    }
}
