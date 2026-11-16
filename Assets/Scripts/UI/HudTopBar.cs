using Nova.Data;
using Nova.Input;
using Nova.Systems;
using TMPro;
using UnityEngine;

namespace Nova.UI
{
    // T6-08: la barra de arriba del HUD (13.6) con su arte, «Las lecturas» (A.6). A la izquierda el nivel,
    // `MAPA 2 · NIVEL 5`; a la derecha el cronómetro con el tiempo de juego de MatchRecorder y el botón de
    // pausa. El texto es TextMeshPro con Jersey 10 a su tamaño nativo (contrato, Importación en Unity) sobre
    // la sombra en trama de la placa; el botón cambia de estado con HudState, y su clic lo resuelve
    // InputRouter. Solo lee.
    //
    // La placa del nivel mide su texto con su aire y el centro redondeado al paso del mosaico
    // (HudArt.PlateWidth); la del cronómetro, lo que `00:00`, para no bailar al cambiar de cifra. UI de
    // partida: tiempo escalado; el cronómetro cuenta tiempo de juego y en pausa se para (15.8).
    [DefaultExecutionOrder(50)] // UI, 15.10
    [DisallowMultipleComponent]
    public sealed class HudTopBar : MonoBehaviour
    {
        // Jersey 10 (NOVA_Arte_Tecnica.md): 18.67 px por em; TextMeshPro en mundo mide el em en
        // fontSize × 0.1 unidades, y el espaciado en centésimas de em.
        const float NativeEmPixels = 1400f / 75f;
        const float WorldTextScale = 0.1f;

        // Con la barra de poder, por encima del tablero y por debajo del menú de encendido y del panel.
        internal const int PlateOrder = 15;
        internal const int TextOrder = 16;
        internal const int ButtonOrder = 16;

        [SerializeField] HudArt art;
        [SerializeField] HudState hud;
        [SerializeField] GameManager game;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Textos de la UI (T.7 #39).")]
        [SerializeField] UiText text;

        Transform root;
        SpriteRenderer levelPlate;
        SpriteRenderer timerPlate;
        SpriteRenderer pause;
        TextMeshPro levelLabel;
        TextMeshPro timerLabel;

        string levelShown;
        int secondsShown = -1;
        int timerWidth;

        public bool HasArt => art != null && art.HasTopBar;

        internal string LevelText => levelLabel != null ? levelLabel.text : null;
        internal string TimerText => timerLabel != null ? timerLabel.text : null;
        internal SpriteRenderer LevelPlate { get { EnsureBuilt(); return levelPlate; } }
        internal SpriteRenderer TimerPlate { get { EnsureBuilt(); return timerPlate; } }
        internal SpriteRenderer Pause { get { EnsureBuilt(); return pause; } }
        internal int TimerWidth { get { EnsureBuilt(); return timerWidth; } }

        void Update()
        {
            Refresh();
        }

        void OnDisable()
        {
            if (root != null)
                root.gameObject.SetActive(false);
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(HudArt hudArt, HudState state, GameManager flow, TMP_FontAsset fontAsset, UiText uiText)
        {
            art = hudArt;
            hud = state;
            game = flow;
            font = fontAsset;
            text = uiText;
        }

        internal void Refresh()
        {
            if (!HasArt || text == null)
            {
                if (root != null)
                    root.gameObject.SetActive(false);
                return;
            }
            EnsureBuilt();
            root.gameObject.SetActive(true);

            LevelDefinition level = game != null ? game.Level : null;
            levelPlate.gameObject.SetActive(level != null);
            levelLabel.gameObject.SetActive(level != null);
            if (level != null)
            {
                string caption = text.HudLevel(level.Map, level.Number);
                if (caption != levelShown)
                {
                    levelShown = caption;
                    levelLabel.text = caption;
                    LayoutPlate(levelPlate, levelLabel, art.LevelRect(art.PlateWidth(MeasurePixels(levelLabel, caption))));
                }
            }

            float elapsed = game != null && game.Recorder != null ? game.Recorder.Elapsed : 0f;
            int seconds = Mathf.Max(0, Mathf.FloorToInt(elapsed));
            if (seconds != secondsShown)
            {
                secondsShown = seconds;
                timerLabel.text = text.Clock(seconds);
            }

            pause.sprite = art.PauseButton(hud != null ? hud.ButtonState : 0);
            Place(pause.transform, art.PauseRect.x, art.PauseRect.y);
        }

        // El ancho de un texto en píxeles de la pantalla de referencia, a tamaño nativo.
        static int MeasurePixels(TextMeshPro label, string value) =>
            Mathf.CeilToInt(label.GetPreferredValues(value).x * HudArt.PixelsPerUnit - 0.01f);

        void LayoutPlate(SpriteRenderer plate, TextMeshPro label, RectInt rect)
        {
            plate.sprite = art.Plate;
            plate.size = new Vector2(rect.width, rect.height) / HudArt.PixelsPerUnit;
            Place(plate.transform, rect.x, rect.y);
            // El texto, centrado en la placa por su línea media: la mayúscula cae en el centro de los 20 px.
            label.rectTransform.sizeDelta = new Vector2(rect.width, rect.height) / HudArt.PixelsPerUnit;
            Vector2 center = HudArt.ScreenToWorld(rect.x + rect.width / 2f, rect.y + rect.height / 2f);
            label.transform.localPosition = new Vector3(center.x, center.y, 0f);
        }

        static void Place(Transform piece, int x, int y)
        {
            Vector2 world = HudArt.ScreenToWorld(x, y);
            piece.localPosition = new Vector3(world.x, world.y, 0f);
        }

        void EnsureBuilt()
        {
            if (root != null)
                return;
            root = new GameObject("TopBar").transform;
            root.SetParent(transform, false);
            levelPlate = Plate("LevelPlate");
            levelLabel = Label("Level", art.LevelTextColor);
            timerPlate = Plate("TimerPlate");
            timerLabel = Label("Timer", art.TimerTextColor);
            var button = new GameObject("PauseButton").AddComponent<SpriteRenderer>();
            button.transform.SetParent(root, false);
            button.sortingOrder = ButtonOrder;
            pause = button;

            timerWidth = art.PlateWidth(MeasurePixels(timerLabel, art.TimerTemplate));
            LayoutPlate(timerPlate, timerLabel, art.TimerRect(timerWidth));
        }

        SpriteRenderer Plate(string name)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(root, false);
            renderer.sortingOrder = PlateOrder;
            // Nueve cortes con el centro en mosaico: la trama no se estira (A.6).
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            return renderer;
        }

        TextMeshPro Label(string name, Color32 color)
        {
            var label = new GameObject(name).AddComponent<TextMeshPro>();
            label.transform.SetParent(root, false);
            if (font != null)
                label.font = font;
            else
                Debug.LogWarning($"[Nova] {this.name}: HudTopBar sin font asset; asigna Jersey10 Raster56.", this);
            label.fontSize = NativeEmPixels / HudArt.PixelsPerUnit / WorldTextScale;
            label.characterSpacing = art.LetterSpacing * 100f / NativeEmPixels;
            label.alignment = TextAlignmentOptions.Midline;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = color;
            label.sortingOrder = TextOrder;
            return label;
        }
    }
}
