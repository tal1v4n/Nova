using System;
using UnityEngine;

namespace Nova.Data
{
    // T6-09: el arte del panel de clic derecho y de lo que vive en él, y del menú de encendido (A.6):
    // `ART-UI-PANEL` «La sonda», `ART-UI-BTN-CONV` «El barrido», `ART-UI-WHEEL` «Las teclas» y `ART-UI-IGN-MENU`
    // «Los nodos». Lo rellena NodeArtAssets desde Assets/Sprites/ui/ con las medidas y los colores de cada .json;
    // sin él, el panel, la rueda y el menú siguen siendo las primitivas de la regla 4 de T.2.
    //
    // Todo se mide en píxeles de la referencia de 960 x 540, con el origen arriba a la izquierda, como en los
    // generadores: las vistas lo componen en un PixelCanvas (Nova.Utils). Cada grupo es una clase con sus campos
    // a la vista porque es lo que copian los .json, sin reglas propias.
    [CreateAssetMenu(menuName = "Nova/Panel Art", fileName = "PanelArt")]
    public sealed class PanelArt : ScriptableObject
    {
        // Las tres filas de las piezas por bando de «La sonda», en el orden de su .json.
        public const int Factions = 3;

        [Serializable]
        public sealed class PanelPieces
        {
            [Tooltip("La tesela de 8 x 8 de la trama del fondo.")]
            public Sprite screen;
            [Tooltip("Por bando (Jugador, Enemigo, neutral): la esquina de arriba a la izquierda; se refleja.")]
            public Sprite[] corner = Array.Empty<Sprite>();
            [Tooltip("Por bando: la tesela del filo entre esquinas, un punto cada 2 px.")]
            public Sprite[] edge = Array.Empty<Sprite>();
            [Tooltip("Por bando: la tesela del cable; su píxel encendido da el color del cable.")]
            public Sprite[] tether = Array.Empty<Sprite>();
            [Tooltip("Por bando: el pulso; los píxeles (2, 1) y (1, 1) dan la cabeza y la cola.")]
            public Sprite[] pulse = Array.Empty<Sprite>();
            [Tooltip("Por bando: el enchufe donde el cable toca el panel.")]
            public Sprite[] socket = Array.Empty<Sprite>();
            [Tooltip("Por bando: la tesela de la línea entre bloques.")]
            public Sprite[] divider = Array.Empty<Sprite>();
            [Tooltip("Por bando: la raya del título (columna 0) y el testigo de 3 x 3 (columnas 1-3).")]
            public Sprite[] rule = Array.Empty<Sprite>();
            public int width = 208;
            public int padding = 8;
            public int rowHeight = 24;
            [Tooltip("La fila de la mayúscula dentro de su fila de 24.")]
            public int capTop = 7;
            public int pulseFrames = 32;
            public float framesPerSecond = 12f;
            public int blinkFrames = 8;
            public int dividerStep = 4;
            public int ruleGap = 3;
            public int ruleLight = 3;
            [Tooltip("El cable sale a media huella más esto del centro del nodo.")]
            public int tetherFromFootprint = 2;
            [Tooltip("El enchufe va a esto del costado del panel.")]
            public int socketGap = 3;
            public Color32[] titleColor = Array.Empty<Color32>();
            public Color32 textColor = new Color32(0xE2, 0xE6, 0xEF, 0xFF);
            public Color32 labelColor = new Color32(0xB5, 0xBC, 0xCD, 0xFF);
            public Color32 mutedColor = new Color32(0x8A, 0x93, 0xA6, 0xFF);

            public bool IsComplete =>
                screen != null && Full(corner) && Full(edge) && Full(tether) && Full(pulse) && Full(socket) && Full(divider) &&
                Full(rule) && titleColor != null && titleColor.Length == Factions;

            static bool Full(Sprite[] sprites) => sprites != null && sprites.Length == Factions && Array.IndexOf(sprites, null) < 0;
        }

        // Los colores del texto de un botón, de su .json.
        [Serializable]
        public sealed class ButtonText
        {
            public Color32 normal = new Color32(0xE2, 0xE6, 0xEF, 0xFF);
            public Color32 cost = new Color32(0x3F, 0xD7, 0xF5, 0xFF);
            public Color32 hover = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
            public Color32 hoverCost = new Color32(0xE8, 0xFE, 0xFF, 0xFF);
            public Color32 muted = new Color32(0x8A, 0x93, 0xA6, 0xFF);
            public Color32 missing = new Color32(0xF2, 0x45, 0x6B, 0xFF);
            public Color32 unavailable = new Color32(0x5D, 0x65, 0x80, 0xFF);
            public Color32 onPower = new Color32(0x11, 0x14, 0x2A, 0xFF);
        }

        // Las columnas de ui_btn_convert_plate, en el orden de su .json.
        public enum ConversionLook { Normal = 0, Hover = 1, Pressed = 2, Missing = 3, Blocked = 4, Unavailable = 5 }

        [Serializable]
        public sealed class ConversionPieces
        {
            [Tooltip("La placa de 20 x 20 por estado: normal, encima, pulsado, falta, bloqueado, nodisp.")]
            public Sprite[] plate = Array.Empty<Sprite>();
            [Tooltip("El frente: el que barre y el parado.")]
            public Sprite[] front = Array.Empty<Sprite>();
            [Tooltip("Las teselas de 8 x 8: lo que tienes y lo que falta.")]
            public Sprite[] fill = Array.Empty<Sprite>();
            public Vector2Int size = new Vector2Int(94, 20);
            public int cellHeight = 22;
            public int pressedDrop = 2;
            public int capTop = 5;
            public int padX = 6;
            public int plateBorder = 6;
            public int innerX0 = 2;
            public int innerX1 = 92;
            public float hoverSeconds = 0.16f;
            public Color32 primaryFill = new Color32(0x3F, 0xD7, 0xF5, 0xFF);
            public Color32 pressedFill = new Color32(0xA6, 0xF2, 0xFD, 0xFF);
            public Color32 primaryText = new Color32(0x11, 0x14, 0x2A, 0xFF);
            public ButtonText text = new ButtonText();

            public bool IsComplete =>
                plate != null && plate.Length == 6 && Array.IndexOf(plate, null) < 0 &&
                front != null && front.Length == 2 && Array.IndexOf(front, null) < 0 &&
                fill != null && fill.Length == 2 && Array.IndexOf(fill, null) < 0;
        }

        // Las columnas de ui_power_wheel_plate, en el orden de su .json. La lista pulsada usa la placa de encima.
        public enum SlotLook { Normal = 0, Hover = 1, Pressed = 2, Blocked = 3, Locked = 4, Unavailable = 5, Charging = 6, Ready = 7, ReadyHover = 8, ReadyPressed = 9 }
        public const int SlotPlates = 9;
        public const int Powers = 3;
        // Las columnas de ui_power_wheel_icons.
        public enum IconLook { Color = 0, Ready = 1, Grey = 2, Outline = 3 }

        [Serializable]
        public sealed class WheelPieces
        {
            [Tooltip("La placa de 20 x 20: fila = poder, columna = estado (sin la lista pulsada, que es la de encima).")]
            public Sprite[] plate = Array.Empty<Sprite>();
            [Tooltip("La trama de la carga: fila = poder; pausada y cargando.")]
            public Sprite[] fill = Array.Empty<Sprite>();
            [Tooltip("La trama cian del encima, del paso 1 al último.")]
            public Sprite[] hover = Array.Empty<Sprite>();
            [Tooltip("Los pictogramas: fila = poder; color, lista, gris y contorno.")]
            public Sprite[] icons = Array.Empty<Sprite>();
            public Sprite lockIcon;
            public Vector2Int slotSize = new Vector2Int(60, 20);
            public Vector2Int cancelSize = new Vector2Int(192, 20);
            public int gap = 6;
            public int cellHeight = 22;
            public int pressedDrop = 2;
            public int plateBorder = 6;
            public Vector2Int iconAt = new Vector2Int(4, 4);
            public Vector2Int lockAt = new Vector2Int(6, 6);
            public int textRight = 56;
            public int capTop = 5;
            public int frames = 32;
            public float framesPerSecond = 12f;
            public float hoverSeconds = 0.16f;
            public Color32[] charging = Array.Empty<Color32>();
            public Color32[] paused = Array.Empty<Color32>();
            public Color32[] front = Array.Empty<Color32>();
            public Color32[] light = Array.Empty<Color32>();
            public Color32[] lightEdge = Array.Empty<Color32>();
            public int lightWidth = 3;
            public int lightFrom = -8;
            public int lightTravel = 76;
            public ButtonText text = new ButtonText();

            public bool IsComplete =>
                plate != null && plate.Length == Powers * SlotPlates && Array.IndexOf(plate, null) < 0 &&
                fill != null && fill.Length == Powers * 2 && Array.IndexOf(fill, null) < 0 &&
                hover != null && hover.Length > 0 && Array.IndexOf(hover, null) < 0 &&
                icons != null && icons.Length == Powers * 4 && Array.IndexOf(icons, null) < 0 && lockIcon != null &&
                charging != null && charging.Length == Powers && paused != null && paused.Length == Powers &&
                front != null && front.Length == Powers && light != null && light.Length == Powers &&
                lightEdge != null && lightEdge.Length == Powers;

            public Sprite Plate(int power, SlotLook look)
            {
                int column = look == SlotLook.ReadyPressed ? (int)SlotLook.ReadyHover : (int)look;
                return plate[power * SlotPlates + column];
            }

            public Sprite Fill(int power, bool paused) => fill[power * 2 + (paused ? 0 : 1)];
            public Sprite Icon(int power, IconLook look) => icons[power * 4 + (int)look];
        }

        // Las columnas de ui_ignition_menu_corner y ui_ignition_menu_nodes.
        public enum CornerLook { Normal = 0, Hover = 1, Pressed = 2, Missing = 3 }
        public enum NodeLook { Full = 0, Dithered = 1, Outline = 2 }

        [Serializable]
        public sealed class IgnitionPieces
        {
            [Tooltip("La esquina de arriba a la izquierda por estado: normal, encima, pulsado, falta.")]
            public Sprite[] corner = Array.Empty<Sprite>();
            [Tooltip("El nodo de nivel 1: fila = Estrella, Púlsar, Magnetar; entero, tramado y contorno.")]
            public Sprite[] nodes = Array.Empty<Sprite>();
            public int cell = 72;
            public int frame = 48;
            public int cornerSize = 7;
            public int cornerInset = 1;
            public int hoverClose = 2;
            public int pressedClose = 3;
            public int pressedDrop = 2;
            public int nameGap = 4;
            public int costGap = 4;
            public float enterSeconds = 0.32f;
            public float staggerSeconds = 0.08f;
            public float hoverSeconds = 0.16f;
            public ButtonText text = new ButtonText();

            public bool IsComplete =>
                corner != null && corner.Length == 4 && Array.IndexOf(corner, null) < 0 &&
                nodes != null && nodes.Length == 9 && Array.IndexOf(nodes, null) < 0;

            public Sprite Node(int option, NodeLook look) => nodes[option * 3 + (int)look];
        }

        [SerializeField] PanelPieces panel = new PanelPieces();
        [SerializeField] ConversionPieces conversion = new ConversionPieces();
        [SerializeField] WheelPieces wheel = new WheelPieces();
        [SerializeField] IgnitionPieces ignition = new IgnitionPieces();

        public PanelPieces Panel => panel;
        public ConversionPieces Conversion => conversion;
        public WheelPieces Wheel => wheel;
        public IgnitionPieces Ignition => ignition;

        // El panel se dibuja con su arte solo si lo tiene todo: piezas, botones y rueda. El menú va aparte.
        public bool HasPanel => panel != null && panel.IsComplete && conversion != null && conversion.IsComplete &&
                                wheel != null && wheel.IsComplete;

        public bool HasIgnitionMenu => ignition != null && ignition.IsComplete;

        // Para NodeArtAssets y los tests.
        public void Configure(PanelPieces panelPieces, ConversionPieces conversionPieces, WheelPieces wheelPieces,
            IgnitionPieces ignitionPieces)
        {
            panel = panelPieces ?? new PanelPieces();
            conversion = conversionPieces ?? new ConversionPieces();
            wheel = wheelPieces ?? new WheelPieces();
            ignition = ignitionPieces ?? new IgnitionPieces();
        }
    }
}
