using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nova.UI
{
    // Menú principal (13.8, 15.7, T5-06), fiel a la maqueta «MainMenu» del lienzo «NOVA — Pantallas
    // de flujo» (T.2 regla 8, 18 #46) a 1920 x 1080: el título, una Estrella del Jugador N5 grande a la
    // derecha y las tres opciones. El fondo del mapa 1 no es de esta pantalla: es un BackgroundView del
    // tablero, debajo del lienzo.
    //
    // La opción activa es la que está bajo el cursor; sin cursor encima, la última que lo estuvo, y al
    // entrar, la primera. Su Asteroide se ve como Estrella y la línea de envío (ART-WLD-LINK) la une con
    // la Estrella grande. Donde la maqueta y 13.8 difieren manda 13.8: el pie lleva la leyenda y la
    // versión, y no la tecla Esc, que aquí no hace nada.
    [DefaultExecutionOrder(50)] // UI, 15.10
    [DisallowMultipleComponent]
    public sealed class MainMenuScreen : MonoBehaviour
    {
        internal enum Choice { Continue = 0, StarMap = 1, Quit = 2 }

        // La maqueta: opciones de 640 px desde (144, 540), icono de 96 y 24 de hueco; la línea sale del
        // centro de la Estrella grande y toca la opción en x = 740.
        const float NavX = 144f;
        const float NavY = 540f;
        const float OptionW = 640f;
        const float IconSize = 96f;
        const float Marker = 40f;
        static readonly Vector2 StarCenter = new Vector2(1392f, 472f);
        const float LineEndX = 740f;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Textos de la UI (T.7 #39).")]
        [SerializeField] UiText text;

        [Tooltip("Los nueve niveles, en orden (11.2): Continuar carga uno de ellos.")]
        [SerializeField] LevelDefinition[] levels = Array.Empty<LevelDefinition>();

        [Tooltip("Los sprites de nodo de Assets/Sprites/nodes (A.2), por nombre: str_lvl5_player…")]
        [SerializeField] Sprite[] nodeSprites = Array.Empty<Sprite>();

        [Tooltip("world_link_sheet de Assets/Sprites/world/link (ART-WLD-LINK).")]
        [SerializeField] Texture2D linkSheet;

        sealed class Option
        {
            public RectTransform Rect;
            public Image Off;
            public Image On;
            public float OffAlpha;
            public RectTransform MarkerClip;
            public RectTransform Text;
            public Vector2 TextHome;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Subtitle;
            public float Center;
            public float Shown;
        }

        Action<string> loadScene = SceneManager.LoadScene;
        Action quit = Exit;
        GameObject root;
        UiMotion motion;
        UiPlasma line;
        readonly List<Option> options = new List<Option>();
        int active;
        float lineFrom;
        float lineTo;
        float lineTime = 1f;

        internal bool IsBuilt => root != null;
        internal int Active => active;
        internal int OptionCount => options.Count;
        internal string OptionLabel(int index) => options[index].Label.text;
        internal string OptionSubtitle(int index) => options[index].Subtitle != null ? options[index].Subtitle.text : null;
        internal UiPlasma Line => line;

        void Start()
        {
            Time.timeScale = 1f;
            Build();
        }

        void Update()
        {
            if (root == null)
                return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < options.Count; i++)
                Paint(options[i], Mathf.MoveTowards(options[i].Shown, i == active ? 1f : 0f, dt / 0.35f));
            if (lineTime < 1f)
            {
                lineTime = Mathf.Min(1f, lineTime + dt / 0.6f);
                PlaceLine(Mathf.Lerp(lineFrom, lineTo, UiMotion.Orbital(lineTime)));
            }
        }

        // Para la escena y los tests: el cableado normal es por el Inspector. sceneLoader sustituye a
        // SceneManager.LoadScene y quitter a Application.Quit, que en EditMode no se pueden llamar.
        public void Configure(TMP_FontAsset fontAsset, UiText uiText, LevelDefinition[] allLevels, Sprite[] sprites, Texture2D sheet,
                              Action<string> sceneLoader = null, Action quitter = null)
        {
            font = fontAsset;
            text = uiText;
            levels = allLevels ?? Array.Empty<LevelDefinition>();
            nodeSprites = sprites ?? Array.Empty<Sprite>();
            linkSheet = sheet;
            loadScene = sceneLoader ?? SceneManager.LoadScene;
            quit = quitter ?? Exit;
        }

        internal void Hover(int index)
        {
            if (index == active || index < 0 || index >= options.Count)
                return;
            active = index;
            lineFrom = line != null ? CurrentLineY : options[index].Center;
            lineTo = options[index].Center;
            lineTime = 0f;
        }

        internal void Choose(Choice choice)
        {
            switch (choice)
            {
                case Choice.Continue:
                    LevelDefinition level = LevelNumbered(ProgressSaver.ContinueLevel);
                    if (level == null)
                        return;
                    GameManager.Request(level);
                    loadScene(GameManager.GameScene);
                    break;
                case Choice.StarMap:
                    loadScene(GameManager.LevelSelectScene);
                    break;
                case Choice.Quit:
                    quit();
                    break;
            }
        }

        // Todo en su sitio final: lo que ven los tests, que no tienen Update.
        internal void Finish()
        {
            motion.Finish();
            foreach (Option option in options)
                Paint(option, options.IndexOf(option) == active ? 1f : 0f);
            lineTime = 1f;
            PlaceLine(options[active].Center);
        }

        internal void Build()
        {
            if (root != null)
                Discard(root);
            options.Clear();
            active = 0;
            RectTransform stage = UiKit.Stage(transform, "MainMenu", 0, out root, out Image veil);
            // Sin velo: aquí no hay partida que tapar, el fondo del mapa 1 se ve entero.
            veil.enabled = false;
            motion = root.AddComponent<UiMotion>();
            UiText t = Text;

            BuildSky(stage);
            BuildTitle(stage, t);
            BuildOptions(stage, t);
            BuildFooter(stage, t);
            Paint(options[0], 1f);
            for (int i = 1; i < options.Count; i++)
                Paint(options[i], 0f);
        }

        // El halo, la línea, los dos protones de paso y los cuerpos de la derecha.
        void BuildSky(RectTransform stage)
        {
            Image halo = UiKit.Box(stage, "Halo", 1112f, 192f, 560f, 560f, UiKit.Player);
            halo.sprite = UiKit.Glow;
            RectTransform haloRect = halo.rectTransform;
            haloRect.pivot = new Vector2(0.5f, 0.5f);
            haloRect.anchoredPosition += new Vector2(280f, -280f);
            // breathe: 5.2 s, de 0.94 a 1.05 y de 0.7 a 1 de opacidad.
            motion.Repeat(0f, 5.2f, k =>
            {
                float b = UiMotion.EaseInOut(k < 0.5f ? k * 2f : 2f - k * 2f);
                haloRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.05f, b);
                halo.color = UiKit.With(UiKit.Player, 0.22f * Mathf.Lerp(0.7f, 1f, b));
            });

            line = UiPlasma.Create(stage, linkSheet, StarCenter, new Vector2(LineEndX, 596f), 0);
            motion.Fade(line.GetComponent<RectTransform>(), 0.9f, 0.8f, UiMotion.Orbital);

            Proton(stage, UiKit.Player, UiKit.Bright, new Vector2(1392f, 472f), new Vector2(1300f, 700f), new Vector2(1160f, 830f),
                new Vector2(1048f, 848f), 1.6f, false);
            Proton(stage, UiKit.Enemy, UiKit.EnemyText, new Vector2(1736f, 192f), new Vector2(1840f, 400f), new Vector2(1790f, 620f),
                new Vector2(1696f, 786f), 3.4f, true);

            Image star = UiKit.NodeSprite(stage, "Star", Sprite(NodeType.Star, 5, Faction.Player), 1200f, 280f, 384f);
            Bob(star.rectTransform, 0f);
            Image enemy = UiKit.NodeSprite(stage, "EnemyStar", Sprite(NodeType.Star, 3, Faction.Enemy), 1640f, 96f, 192f);
            if (enemy.sprite != null)
                enemy.color = UiKit.With(UiKit.White, 0.9f);
            UiKit.NodeSprite(stage, "Magnetar", Sprite(NodeType.Magnetar, 3, Faction.Neutral), 1600f, 690f, 192f);
            UiKit.NodeSprite(stage, "Asteroid", Sprite(NodeType.Asteroid, 0, Faction.Neutral), 1000f, 800f, 96f);
            UiKit.NodeSprite(stage, "Asteroid", Sprite(NodeType.Asteroid, 0, Faction.Neutral), 1120f, 120f, 96f);
        }

        // Un protón que cruza el cielo por una cúbica cada 4.2 s. El del Enemigo se corta al 78 %: lo
        // derriba algo que no se ve.
        void Proton(RectTransform stage, Color32 ring, Color32 core, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float delay, bool cut)
        {
            RectTransform holder = UiKit.Node(stage, "Proton", 0f, 0f, 14f, 14f);
            holder.pivot = new Vector2(0.5f, 0.5f);
            Image glow = UiKit.Box(holder, "Glow", -22f, -22f, 58f, 58f, ring);
            glow.sprite = UiKit.Glow;
            glow.color = UiKit.With(ring, 0.45f);
            UiKit.Box(holder, "Ring", 0f, 0f, 14f, 14f, ring);
            UiKit.Box(holder, "Core", 3f, 3f, 8f, 8f, core);
            CanvasGroup group = UiMotion.Group(holder);
            group.alpha = 0f;
            float end = cut ? 0.44f : 0.58f;
            float reach = cut ? 0.76f : 1f;
            float gone = cut ? 0.48f : 0.64f;
            motion.Repeat(delay, 4.2f, k =>
            {
                float d = k < end ? reach * UiMotion.EaseInOut(k / end) : Mathf.Lerp(reach, cut ? 0.78f : 1f, Mathf.Clamp01((k - end) / (gone - end)));
                float v = 1f - d;
                Vector2 p = v * v * v * p0 + 3f * v * v * d * p1 + 3f * v * d * d * p2 + d * d * d * p3;
                holder.anchoredPosition = new Vector2(p.x, -p.y);
                group.alpha = k < 0.06f ? k / 0.06f : k < end ? 1f : Mathf.Clamp01(1f - (k - end) / (gone - end));
            });
        }

        // El cabeceo de la materia: 4 px arriba la mitad del ciclo de 1.6 s, a pasos (13.12).
        void Bob(RectTransform target, float delay)
        {
            Vector2 home = target.anchoredPosition;
            motion.Repeat(delay, 1.6f, k => target.anchoredPosition = home + new Vector2(0f, k < 0.5f ? 0f : 4f));
        }

        // El lema y NOVA, letra a letra, con su relieve de dos capas que sale al entrar (extrude).
        void BuildTitle(RectTransform stage, UiText t)
        {
            TextMeshProUGUI tagline = UiKit.Label(stage, font, t.Tagline, 160f, 120f, 1200f, UiKit.X2, UiKit.PlayerCap, 43f, 6f);
            motion.Fade(tagline.rectTransform, 0.1f, 1f);

            const float Size = 298.67f;
            const float Line = 240f;
            float x = 160f;
            float y = 120f + 43f + 8f;
            string title = t.GameTitle;
            for (int i = 0; i < title.Length; i++)
            {
                RectTransform letter = UiKit.Node(stage, $"Letter{i}", x, y, 400f, Line);
                TextMeshProUGUI face = UiKit.Label(letter, font, title[i].ToString(), 0f, 0f, 400f, Size, UiKit.Bright, Line);
                List<TextMeshProUGUI> shadows = UiKit.Shadows(face, (8f, UiKit.PlayerDeep), (16f, UiKit.Frame));
                Vector2 faceHome = face.rectTransform.anchoredPosition;
                motion.Add(0.5f, 1.1f, UiMotion.Orbital, k =>
                {
                    shadows[0].rectTransform.anchoredPosition = faceHome + new Vector2(8f, -8f) * k;
                    shadows[1].rectTransform.anchoredPosition = faceHome + new Vector2(16f, -16f) * k;
                });
                motion.Rise(letter, 0.05f + 0.08f * i, 0.9f, 24f);
                x += Mathf.Ceil(UiKit.WidthOf(face)) + 12f;
            }
        }

        // Continuar (o Jugar), Mapa estelar y Salir.
        void BuildOptions(RectTransform stage, UiText t)
        {
            int max = ProgressSaver.MaxUnlocked;
            int next = ProgressSaver.ContinueLevel;
            int map = (next - 1) / LevelDefinition.LevelsPerMap + 1;
            bool first = max <= ProgressSaver.FirstLevel;
            var defs = new (string label, string sub, Sprite off, Sprite on, float offAlpha)[]
            {
                (first ? t.Play : t.ContinueGame, first ? null : t.ContinueLine(map, next),
                    Sprite(NodeType.Asteroid, 0, Faction.Player), Sprite(NodeType.Star, 1, Faction.Player), 1f),
                (t.StarMap, null, Sprite(NodeType.Asteroid, 0, Faction.Neutral), Sprite(NodeType.Star, 1, Faction.Player), 1f),
                // Salir: lo que se pierde se ve en magenta (13.12).
                (t.Quit, null, Sprite(NodeType.Asteroid, 0, Faction.Neutral), Sprite(NodeType.Star, 1, Faction.Enemy), 0.6f),
            };

            float y = NavY;
            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                float content = Mathf.Max(IconSize, 64f + (def.sub != null ? 36f : 0f));
                float h = content + 16f;
                RectTransform rect = UiKit.Node(stage, $"Option{i}", NavX, y, OptionW, h);
                UiHit hit = UiHit.Add(rect);
                int index = i;
                hit.OnEnter += () => Hover(index);
                hit.OnClick += () => Choose((Choice)index);

                float iconY = (h - IconSize) / 2f;
                Image off = UiKit.NodeSprite(rect, "Off", def.off, 8f, iconY, IconSize);
                Image on = UiKit.NodeSprite(rect, "On", def.on, 8f, iconY, IconSize);
                foreach (Image icon in new[] { off, on })
                {
                    icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    icon.rectTransform.anchoredPosition += new Vector2(IconSize / 2f, -IconSize / 2f);
                }

                float textX = 8f + IconSize + 24f;
                float textY = (h - (64f + (def.sub != null ? 36f : 0f))) / 2f;
                RectTransform block = UiKit.Node(rect, "Text", textX, textY, OptionW - textX - 24f, 100f);
                RectTransform clip = UiKit.Node(block, "Marker", 0f, 0f, 0f, 64f);
                clip.gameObject.AddComponent<RectMask2D>();
                // ▸ no está en Jersey 10: el de UiKit, a ×4, la altura de una mayúscula a ×4.
                UiKit.Box(clip, "Arrow", 0f, 12f, 24f, 40f, UiKit.Player).sprite = UiKit.Arrow;
                TextMeshProUGUI label = UiKit.Label(block, font, def.label, 0f, 0f, OptionW, UiKit.X4, UiKit.Soft, 64f);
                TextMeshProUGUI sub = def.sub != null
                    ? UiKit.Label(block, font, def.sub, 0f, 64f, OptionW, UiKit.X2, UiKit.Muted, 36f)
                    : null;

                options.Add(new Option
                {
                    Rect = rect, Off = off, On = on, OffAlpha = def.offAlpha, MarkerClip = clip,
                    Text = label.rectTransform, TextHome = label.rectTransform.anchoredPosition, Label = label, Subtitle = sub,
                    Center = y + h / 2f, Shown = -1f,
                });
                // slidein: 32 px desde la izquierda en 0.7 s, escalonado 90 ms.
                CanvasGroup group = UiMotion.Group(rect);
                Vector2 home = rect.anchoredPosition;
                motion.Add(0.55f + 0.09f * i, 0.7f, UiMotion.Orbital, k =>
                {
                    group.alpha = k;
                    rect.anchoredPosition = home + new Vector2(-32f * (1f - k), 0f);
                });
                y += h + 8f;
            }
            lineTo = lineFrom = options[0].Center;
            PlaceLine(options[0].Center);
        }

        // Leyenda de facciones y versión, a la derecha (13.8).
        void BuildFooter(RectTransform stage, UiText t)
        {
            const float Bottom = 1080f - 56f - 40f;
            RectTransform footer = UiKit.Node(stage, "Footer", 0f, Bottom, UiKit.Width, 40f);
            var entries = new (Color32? swatch, string label, Color32 color)[]
            {
                (UiKit.Player, t.LegendPlayer, UiKit.Muted),
                (UiKit.Enemy, t.LegendEnemy, UiKit.Muted),
                (UiKit.Muted, t.LegendNeutral, UiKit.Muted),
                (null, t.Version(Application.version), UiKit.Off),
            };
            float x = UiKit.Width - 160f;
            for (int i = entries.Length - 1; i >= 0; i--)
            {
                TextMeshProUGUI label = UiKit.Label(footer, font, entries[i].label, 0f, 0f, 400f, UiKit.X2, entries[i].color, 40f);
                float w = Mathf.Ceil(UiKit.WidthOf(label));
                x -= w;
                label.rectTransform.anchoredPosition = new Vector2(x, 0f);
                if (entries[i].swatch.HasValue)
                {
                    x -= 12f + 16f;
                    UiKit.Box(footer, "Swatch", x, 12f, 16f, 16f, entries[i].swatch.Value);
                }
                x -= i == entries.Length - 1 ? 24f + 32f : 32f;
            }
            motion.Fade(footer, 1.1f, 1f);
        }

        // El estado de una opción, de 0 (apagada) a 1 (activa), con la curva orbital: el Asteroide se
        // vuelve Estrella, aparece el ▸ y el texto se aclara y se desplaza 4 px.
        void Paint(Option option, float shown)
        {
            if (Mathf.Approximately(option.Shown, shown))
                return;
            option.Shown = shown;
            float k = UiMotion.Orbital(shown);
            option.On.color = Alpha(option.On, k);
            option.On.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1f, k);
            option.Off.color = Alpha(option.Off, option.OffAlpha * (1f - k));
            option.Off.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.2f, k);
            option.MarkerClip.sizeDelta = new Vector2(Marker * k, option.MarkerClip.sizeDelta.y);
            option.Text.anchoredPosition = option.TextHome + new Vector2((Marker + 4f) * k, 0f);
            option.Label.color = Color.Lerp(UiKit.Soft, UiKit.Bright, k);
        }

        static Color Alpha(Image image, float alpha) => image.sprite == null ? new Color(0f, 0f, 0f, 0f) : new Color(1f, 1f, 1f, alpha);

        float CurrentLineY => Mathf.Lerp(lineFrom, lineTo, UiMotion.Orbital(lineTime));

        void PlaceLine(float y)
        {
            if (line != null)
                line.Set(StarCenter, new Vector2(LineEndX, y));
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

        static void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Discard(GameObject go)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        UiText Text => text != null ? text : throw new InvalidOperationException("MainMenuScreen necesita UiText.");
    }
}
