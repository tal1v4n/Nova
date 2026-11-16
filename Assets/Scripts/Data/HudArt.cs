using System;
using UnityEngine;

namespace Nova.Data
{
    // T6-08: el arte del HUD permanente (13.6, A.6) y sus medidas, tal como las dejan los generadores en
    // ui_power_bar.json y ui_topbar.json. Lo rellena NodeArtAssets desde Assets/Sprites/ui/; sin él, el HUD
    // no se dibuja.
    //
    // Todo se mide en píxeles de la pantalla de referencia de 960 x 540 (T.8), con el origen arriba a la
    // izquierda y la y hacia abajo, como en el generador. La cámara enseña exactamente esa pantalla, así
    // que un píxel de aquí es 1/64 u del mundo (ScreenToWorld).
    [CreateAssetMenu(menuName = "Nova/Hud Art", fileName = "HudArt")]
    public sealed class HudArt : ScriptableObject
    {
        public const int ScreenWidth = 960;
        public const int ScreenHeight = 540;
        public const float PixelsPerUnit = 64f;

        [Header("Barra de poder (ART-UI-POWERBAR, «El conducto»)")]
        [Tooltip("La carcasa, 960 x 12, con el pivote arriba a la izquierda.")]
        [SerializeField] Sprite barFrame;
        [Tooltip("Las teselas del Jugador, un frame por elemento.")]
        [SerializeField] Sprite[] playerFill = Array.Empty<Sprite>();
        [Tooltip("Las teselas del Enemigo, un frame por elemento.")]
        [SerializeField] Sprite[] enemyFill = Array.Empty<Sprite>();
        [Tooltip("El frente, un frame por elemento.")]
        [SerializeField] Sprite[] front = Array.Empty<Sprite>();
        [SerializeField] int barHeight = 12;
        [Tooltip("El interior de la carcasa, donde van los rellenos: de x0 a x1.")]
        [SerializeField] int barX0 = 4;
        [SerializeField] int barX1 = 956;
        [Tooltip("El reparto se ajusta a múltiplos de este paso desde x0.")]
        [SerializeField, Min(1)] int barSnap = 1;
        [SerializeField, Min(1f)] float barFramesPerSecond = 12f;
        [Tooltip("Dónde cae el reparto dentro del frente.")]
        [SerializeField] int frontContact = 8;
        [Tooltip("Lo que tarda el frente en llegar a un reparto nuevo, con la curva orbital (13.12).")]
        [SerializeField, Min(0f)] float frontTweenSeconds = 0.32f;

        [Header("Barra de arriba (ART-UI-TOPBAR, «Las lecturas»)")]
        [Tooltip("La placa de nueve cortes, con el centro en mosaico.")]
        [SerializeField] Sprite plate;
        [Tooltip("El botón de pausa: normal, encima y pulsado.")]
        [SerializeField] Sprite[] pauseButton = Array.Empty<Sprite>();
        [SerializeField] int margin = 4;
        [SerializeField] int top = 4;
        [SerializeField] int plateHeight = 20;
        [SerializeField] int plateBorder = 6;
        [SerializeField] int padX = 6;
        [SerializeField] int gap = 4;
        [Tooltip("El centro de la placa sube de este paso en este paso: el mosaico entra entero.")]
        [SerializeField, Min(1)] int plateWidthStep = 8;
        [SerializeField] int letterSpacing = 1;
        [SerializeField] Color32 levelTextColor = new Color32(0xB5, 0xBC, 0xCD, 0xFF);
        [SerializeField] Color32 timerTextColor = new Color32(0xE2, 0xE6, 0xEF, 0xFF);
        [SerializeField] string timerTemplate = "00:00";
        [SerializeField] Vector2Int pauseCell = new Vector2Int(20, 22);

        [Header("Icono de mejora (ART-UI-BTN-UPG, «El sello», T6-15)")]
        [Tooltip("Las celdas de ui_upgrade_icon_sheet por filas —aviso, listo, activo, encima y pulsado— y en cada fila sus frames.")]
        [SerializeField] Sprite[] upgradeIcon = Array.Empty<Sprite>();
        [SerializeField, Min(1)] int upgradeIconFrames = 16;
        [SerializeField, Min(1f)] float upgradeIconFramesPerSecond = 12f;
        [Tooltip("La celda, con el pivote abajo al centro: el icono va centrado sobre la huella, a gap px de ella.")]
        [SerializeField] Vector2Int upgradeIconCell = new Vector2Int(29, 20);
        [SerializeField] int upgradeIconGap = 2;
        [Tooltip("La zona que se pulsa: el rombo, arriba y centrado en la celda. El sonar no cuenta.")]
        [SerializeField] Vector2Int upgradeIconHitBox = new Vector2Int(19, 19);

        public bool HasPowerBar =>
            barFrame != null && FillFrames > 0 && enemyFill != null && enemyFill.Length == FillFrames &&
            Array.IndexOf(playerFill, null) < 0 && Array.IndexOf(enemyFill, null) < 0 &&
            front != null && front.Length > 0 && Array.IndexOf(front, null) < 0 && barX1 > barX0;

        public bool HasTopBar =>
            plate != null && pauseButton != null && pauseButton.Length == 3 && Array.IndexOf(pauseButton, null) < 0;

        // Las cinco filas de ui_upgrade_icon_sheet, en el orden de su .json.
        public const int UpgradeIconRows = 5;

        public bool HasUpgradeIcon =>
            upgradeIcon != null && upgradeIcon.Length == UpgradeIconRows * upgradeIconFrames && Array.IndexOf(upgradeIcon, null) < 0;

        // El icono de mejora no entra: el HUD permanente se dibuja sin él, y él sin el HUD.
        public bool IsComplete => HasPowerBar && HasTopBar;

        public Sprite BarFrame => barFrame;
        public int FillFrames => playerFill != null ? playerFill.Length : 0;
        public Sprite PlayerFill(int frame) => playerFill[frame];
        public Sprite EnemyFill(int frame) => enemyFill[frame];
        public int FrontFrames => front != null ? front.Length : 0;
        public Sprite Front(int frame) => front[frame];
        public int BarHeight => barHeight;
        public int BarX0 => barX0;
        public int BarX1 => barX1;
        public int BarSnap => barSnap;
        public float BarFramesPerSecond => barFramesPerSecond;
        public int FrontContact => frontContact;
        public float FrontTweenSeconds => frontTweenSeconds;

        public Sprite Plate => plate;
        public Sprite PauseButton(int state) => pauseButton[state];
        public int Margin => margin;
        public int Top => top;
        public int PlateHeight => plateHeight;
        public int PlateBorder => plateBorder;
        public int PadX => padX;
        public int Gap => gap;
        public int LetterSpacing => letterSpacing;
        public Color32 LevelTextColor => levelTextColor;
        public Color32 TimerTextColor => timerTextColor;
        public string TimerTemplate => timerTemplate;
        public Vector2Int PauseCell => pauseCell;

        public int UpgradeIconFrames => upgradeIconFrames;
        public float UpgradeIconFramesPerSecond => upgradeIconFramesPerSecond;
        public Sprite UpgradeIcon(int row, int frame) => upgradeIcon[row * upgradeIconFrames + frame];
        public Vector2Int UpgradeIconCell => upgradeIconCell;
        public int UpgradeIconGap => upgradeIconGap;
        public Vector2Int UpgradeIconHitBox => upgradeIconHitBox;

        // --- Geometría, en píxeles de la pantalla de referencia ---

        // Arriba del todo de la barra de poder: pegada al borde de abajo.
        public int BarTop => ScreenHeight - barHeight;

        // La x del reparto: x0 + reparto · (x1 − x0), ajustada al paso.
        public int FrontPixel(float share)
        {
            float clamped = Mathf.Clamp01(share);
            int steps = Mathf.RoundToInt(clamped * (barX1 - barX0) / barSnap);
            return barX0 + steps * barSnap;
        }

        // El ancho de una placa para un texto: su aire a cada lado y el centro redondeado hacia arriba al paso.
        public int PlateWidth(int textPixels)
        {
            int center = textPixels + 2 * padX - 2 * plateBorder;
            center = Mathf.Max(plateWidthStep, (center + plateWidthStep - 1) / plateWidthStep * plateWidthStep);
            return center + 2 * plateBorder;
        }

        // El botón de pausa, arriba a la derecha.
        public RectInt PauseRect => new RectInt(ScreenWidth - margin - pauseCell.x, top, pauseCell.x, plateHeight);

        // La placa del cronómetro, a la izquierda del botón, para un ancho de placa.
        public RectInt TimerRect(int width) => new RectInt(PauseRect.x - gap - width, top, width, plateHeight);

        // La placa del nivel, arriba a la izquierda.
        public RectInt LevelRect(int width) => new RectInt(margin, top, width, plateHeight);

        // Un punto de la pantalla de referencia (y hacia abajo) en el mundo, con la cámara en el centro.
        public static Vector2 ScreenToWorld(float x, float y) =>
            new Vector2((x - ScreenWidth / 2f) / PixelsPerUnit, (ScreenHeight / 2f - y) / PixelsPerUnit);

        // Un rectángulo de la pantalla de referencia en el mundo.
        public static Rect ScreenToWorld(RectInt rect)
        {
            Vector2 min = ScreenToWorld(rect.x, rect.y + rect.height);
            return new Rect(min, new Vector2(rect.width, rect.height) / PixelsPerUnit);
        }

        // Para NodeArtAssets y los tests: el editor escribe aquí lo que copia ArtBridge.
        public void ConfigurePowerBar(Sprite frame, Sprite[] player, Sprite[] enemy, Sprite[] frontFrames, int height,
            int x0, int x1, int snap, float framesPerSecond, int contact)
        {
            barFrame = frame;
            playerFill = player ?? Array.Empty<Sprite>();
            enemyFill = enemy ?? Array.Empty<Sprite>();
            front = frontFrames ?? Array.Empty<Sprite>();
            barHeight = height;
            barX0 = x0;
            barX1 = x1;
            barSnap = Mathf.Max(1, snap);
            barFramesPerSecond = Mathf.Max(1f, framesPerSecond);
            frontContact = contact;
        }

        public void ConfigureTopBar(Sprite plateSprite, Sprite[] pause, int screenMargin, int screenTop, int height,
            int border, int paddingX, int spacing, int widthStep, int letters, Color32 levelColor, Color32 timerColor,
            string template, Vector2Int cell)
        {
            plate = plateSprite;
            pauseButton = pause ?? Array.Empty<Sprite>();
            margin = screenMargin;
            top = screenTop;
            plateHeight = height;
            plateBorder = border;
            padX = paddingX;
            gap = spacing;
            plateWidthStep = Mathf.Max(1, widthStep);
            letterSpacing = letters;
            levelTextColor = levelColor;
            timerTextColor = timerColor;
            timerTemplate = string.IsNullOrEmpty(template) ? "00:00" : template;
            pauseCell = cell;
        }

        // cells va por filas, como la hoja: UpgradeIconRows filas de frames celdas cada una.
        public void ConfigureUpgradeIcon(Sprite[] cells, int frames, float framesPerSecond, Vector2Int cell, int iconGap,
            Vector2Int hitBox)
        {
            upgradeIcon = cells ?? Array.Empty<Sprite>();
            upgradeIconFrames = Mathf.Max(1, frames);
            upgradeIconFramesPerSecond = Mathf.Max(1f, framesPerSecond);
            upgradeIconCell = cell;
            upgradeIconGap = iconGap;
            upgradeIconHitBox = hitBox;
        }
    }
}
