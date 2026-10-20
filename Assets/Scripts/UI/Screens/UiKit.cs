using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nova.UI
{
    // Piezas de las pantallas fuera de partida (13.8–13.12, T.2 regla 8): la paleta y las medidas de
    // la lámina «Lenguaje de UI», los sprites que se dibujan en código y la maquetación sobre un
    // lienzo de 1920 x 1080, el de la maqueta. Todo se coloca con la esquina superior izquierda en
    // (x, y) y el eje y hacia abajo, como en la maqueta, para poder copiar sus números tal cual.
    internal static class UiKit
    {
        public const float Width = 1920f;
        public const float Height = 1080f;

        // Tamaños de Jersey 10 de la lámina: múltiplos de 18.67 px.
        public const float X1 = 18.67f;
        public const float X2 = 37.33f;
        public const float X3 = 56f;
        public const float X4 = 74.67f;
        public const float X6 = 112f;
        public const float X8 = 149.33f;

        // Paleta de la maqueta, toda de nova_paleta.py (13.12).
        public static readonly Color32 Space = Hex(0x0B0E1A);
        public static readonly Color32 Panel = Hex(0x11142A);
        public static readonly Color32 Frame = Hex(0x26306B);
        public static readonly Color32 Rule = Hex(0x1D2144);
        public static readonly Color32 Sweep = Hex(0x1D3A6B);
        public static readonly Color32 Player = Hex(0x3FD7F5);
        public static readonly Color32 PlayerLight = Hex(0xA6F2FD);
        public static readonly Color32 PlayerCap = Hex(0x7FE8FC);
        public static readonly Color32 PlayerDeep = Hex(0x2A9AD4);
        public static readonly Color32 Enemy = Hex(0xF2456B);
        public static readonly Color32 EnemyCap = Hex(0xFF9EB2);
        public static readonly Color32 EnemyDeep = Hex(0xB92A5C);
        public static readonly Color32 EnemyDark = Hex(0x3D1440);
        public static readonly Color32 EnemySweep = Hex(0x7A1F52);
        public static readonly Color32 EnemyText = Hex(0xFFE7EC);
        public static readonly Color32 Text = Hex(0xE2E6EF);
        public static readonly Color32 Bright = Hex(0xE8FEFF);
        public static readonly Color32 Muted = Hex(0x8A93A6);
        public static readonly Color32 Soft = Hex(0xB5BCCD);
        public static readonly Color32 Off = Hex(0x5D6580);
        public static readonly Color32 KeyFrame = Hex(0x3B4060);
        public static readonly Color32 White = Hex(0xFFFFFF);

        static Sprite stepped;
        static Sprite square;
        static Sprite dither;
        static Sprite glow;
        static Sprite shine;
        static Sprite cross;
        static Sprite arrow;
        static Sprite loop;

        // La esquina en escalera de 4 + 4 px (13.12) como sprite de 9 cortes: estirado, la escalera
        // se queda de 8 px en cualquier tamaño. A 100 px por unidad, un texel es un píxel del lienzo.
        public static Sprite Stepped => stepped != null ? stepped : stepped = Make("Stepped", 24, 24, (x, y) =>
        {
            int edgeX = Mathf.Min(x, 23 - x);
            int edgeY = Mathf.Min(y, 23 - y);
            bool cut = (edgeY < 4 && edgeX < 8) || (edgeY < 8 && edgeX < 4);
            return cut ? 0f : 1f;
        }, new Vector4(8, 8, 8, 8));

        public static Sprite Square => square != null ? square : square = Make("Square", 4, 4, (x, y) => 1f);

        // El velo de pausa y fin: trama al 50 % en cuadros de 2 px, como el arte a ×2 (13.12).
        public static Sprite Dither => dither != null ? dither : dither = Make("Dither", 4, 4, (x, y) => (x / 2 + y / 2) % 2 == 0 ? 1f : 0f, wrap: TextureWrapMode.Repeat);

        // Halo radial de luz: degradado de alfa, permitido fuera del tablero (18 #44).
        public static Sprite Glow => glow != null ? glow : glow = Make("Glow", 64, 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
            return Mathf.Clamp01(1f - d / 0.65f);
        }, filter: FilterMode.Bilinear);

        // El brillo que recorre el botón primario: una banda inclinada -18° que se desvanece a los lados.
        public static Sprite Shine => shine != null ? shine : shine = Make("Shine", 64, 64, (x, y) =>
        {
            float u = (x + (y - 32f) * Mathf.Tan(18f * Mathf.Deg2Rad)) / 64f;
            return Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2f) * 0.55f;
        }, filter: FilterMode.Bilinear);

        // La X de 8 x 8 de una vía que no alcanza (13.11).
        public static Sprite Cross => cross != null ? cross : cross = Make("Cross", 8, 8, (x, y) =>
        {
            int cx = x / 2, cy = 3 - y / 2;
            return cx == cy || cx == 3 - cy ? 1f : 0f;
        });

        // ▸ de Siguiente.
        public static Sprite Arrow => arrow != null ? arrow : arrow = Make("Arrow", 6, 10, (x, y) =>
            x <= Mathf.Min(y, 9 - y) ? 1f : 0f);

        // ↺ de Reintentar: un aro con la punta arriba a la izquierda.
        public static Sprite Loop => loop != null ? loop : loop = Make("Loop", 10, 10, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(5f, 5f));
            bool ring = d > 2.6f && d < 4.6f && !(x < 4 && y > 5);
            bool tip = (y == 9 && x >= 1 && x <= 4) || (x == 1 && y >= 6 && y <= 9);
            return ring || tip ? 1f : 0f;
        });

        static Sprite Make(string name, int w, int h, System.Func<int, int, float> alpha, Vector4 border = default,
                           FilterMode filter = FilterMode.Point, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = $"Ui{name}", filterMode = filter, wrapMode = wrap };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha(x, y) * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = texture.name;
            return sprite;
        }

        // Un lienzo propio delante de todo, a 1920 x 1080 como la maqueta, con el velo de trama sobre
        // el tablero congelado (13.10). Devuelve el escenario: el contenido se coloca en él.
        public static RectTransform Stage(Transform parent, string name, int order, out GameObject root, out Image veil)
        {
            root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            canvas.pixelPerfect = true;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            var stage = (RectTransform)new GameObject("Stage", typeof(RectTransform)).transform;
            stage.SetParent(root.transform, false);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(Width, Height);
            stage.anchoredPosition = Vector2.zero;

            veil = Box(stage, "Veil", 0f, 0f, Width, Height, Space);
            veil.sprite = Dither;
            veil.type = Image.Type.Tiled;
            // El velo se traga los clics: nada de lo que hay debajo responde.
            veil.raycastTarget = true;
            EnsureEventSystem();
            return stage;
        }

        // Los botones necesitan un EventSystem con el módulo del Input System. La escena Game lo trae;
        // si falta, se crea uno.
        static void EnsureEventSystem()
        {
            if (!Application.isPlaying || UnityEngine.EventSystems.EventSystem.current != null)
                return;
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        public static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        public static Color With(Color32 color, float alpha) => new Color(color.r / 255f, color.g / 255f, color.b / 255f, alpha);

        // Un nodo del lienzo con la esquina superior izquierda en (x, y), relativo a su padre.
        public static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            Place(rect, x, y, w, h);
            return rect;
        }

        public static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        // Un rectángulo de color; con escalera, la del sprite de 9 cortes.
        public static Image Box(Transform parent, string name, float x, float y, float w, float h, Color32 color, bool stepped = false)
        {
            Image image = Node(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = stepped ? Stepped : Square;
            image.type = stepped ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // Una tarjeta de la maqueta (.card > div.st): marco de 4 px en escalera y relleno en escalera.
        // Devuelve el relleno: el contenido se coloca dentro, con su origen en la esquina del relleno.
        public static RectTransform Card(Transform parent, string name, float x, float y, float w, float h, out Image frame)
        {
            frame = Box(parent, name, x, y, w, h, Frame, true);
            frame.gameObject.AddComponent<CanvasGroup>();
            Image fill = Box(frame.transform, "Fill", 4f, 4f, w - 8f, h - 8f, Panel, true);
            return fill.rectTransform;
        }

        // Cambia el alto de una tarjeta y de su relleno a la vez.
        public static void Resize(Image frame, float h)
        {
            var rect = frame.rectTransform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, h);
            var fill = (RectTransform)rect.Find("Fill");
            fill.sizeDelta = new Vector2(fill.sizeDelta.x, h - 8f);
        }

        // Texto con la esquina superior izquierda en (x, y). El alto sale del texto: con wrap, del
        // número de líneas que ocupa en el ancho dado. lineHeight en px, como line-height de CSS.
        public static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string text, float x, float y, float w,
                                            float size, Color32 color, float lineHeight = 0f, float spacing = 0f,
                                            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool wrap = false)
        {
            RectTransform rect = Node(parent, "Label", x, y, w, 10f);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.richText = true;
            label.raycastTarget = false;
            // characterSpacing de TMP va en centésimas de em; letter-spacing de CSS, en px.
            label.characterSpacing = spacing / size * 100f;
            float line = lineHeight > 0f ? lineHeight : size * 1.15f;
            // lineSpacing de TMP suma al interlineado de la fuente, también en centésimas de em.
            label.lineSpacing = (line - size * LineRatio(label)) / size * 100f;
            label.text = text;
            rect.sizeDelta = new Vector2(w, HeightOf(label, line));
            return label;
        }

        // Interlineado propio de la fuente en ems; sin fuente cargada, el de Jersey 10.
        static float LineRatio(TextMeshProUGUI label)
        {
            TMP_FontAsset font = label.font;
            if (font == null || font.faceInfo.pointSize <= 0)
                return 1f;
            return font.faceInfo.lineHeight / font.faceInfo.pointSize;
        }

        // Alto de un texto: una línea por cada salto, con wrap medido por TMP.
        public static float HeightOf(TextMeshProUGUI label, float line)
        {
            if (label.font == null || label.textWrappingMode == TextWrappingModes.NoWrap)
            {
                int lines = 1;
                foreach (char c in label.text)
                {
                    if (c == '\n')
                        lines++;
                }
                return lines * line;
            }
            label.ForceMeshUpdate();
            int count = Mathf.Max(1, label.textInfo.lineCount);
            return count * line;
        }

        // Ancho de un texto en una sola línea, para colocar lo que va detrás.
        public static float WidthOf(TextMeshProUGUI label)
        {
            if (label.font == null)
                return label.text.Length * label.fontSize * 0.45f;
            return label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity).x;
        }

        // Sombra dura de pixel art (text-shadow de la maqueta): copias del texto detrás, desplazadas.
        public static List<TextMeshProUGUI> Shadows(TextMeshProUGUI label, params (float offset, Color32 color)[] layers)
        {
            var shadows = new List<TextMeshProUGUI>();
            for (int i = layers.Length - 1; i >= 0; i--)
            {
                TextMeshProUGUI copy = Object.Instantiate(label, label.transform.parent);
                copy.name = $"Shadow{i}";
                copy.color = layers[i].color;
                var rect = copy.rectTransform;
                rect.anchoredPosition = label.rectTransform.anchoredPosition + new Vector2(layers[i].offset, -layers[i].offset);
                copy.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
                shadows.Insert(0, copy);
            }
            return shadows;
        }

        // Un sprite de nodo a escala entera (96 px es ×1, 192 px es ×2).
        public static Image NodeSprite(Transform parent, string name, Sprite sprite, float x, float y, float size)
        {
            Image image = Node(parent, name, x, y, size, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (sprite == null)
                image.color = new Color(0f, 0f, 0f, 0f);
            return image;
        }

        // Una tecla de la lista de controles: marco de 2 px alrededor del texto.
        public static float Key(Transform parent, TMP_FontAsset font, string text, float x, float y, Color32 frame, Color32 color)
        {
            TextMeshProUGUI label = Label(parent, font, text, x + 10f, y + 2f, 400f, X2, color, 30f);
            float w = Mathf.Ceil(WidthOf(label)) + 20f;
            label.rectTransform.sizeDelta = new Vector2(w - 20f, 30f);
            Outline(parent, x, y, w, 34f, frame, 2f);
            return w;
        }

        // Un marco hueco de grosor dado.
        public static void Outline(Transform parent, float x, float y, float w, float h, Color32 color, float thickness, bool dashed = false)
        {
            if (!dashed)
            {
                Box(parent, "Top", x, y, w, thickness, color);
                Box(parent, "Bottom", x, y + h - thickness, w, thickness, color);
                Box(parent, "Left", x, y, thickness, h, color);
                Box(parent, "Right", x + w - thickness, y, thickness, h, color);
                return;
            }
            // Discontinuo, como border-style: dashed: trazos de 6 px con 4 de hueco.
            for (float i = 0f; i < w; i += 10f)
            {
                Box(parent, "Dash", x + i, y, Mathf.Min(6f, w - i), thickness, color);
                Box(parent, "Dash", x + i, y + h - thickness, Mathf.Min(6f, w - i), thickness, color);
            }
            for (float i = 0f; i < h; i += 10f)
            {
                Box(parent, "Dash", x, y + i, thickness, Mathf.Min(6f, h - i), color);
                Box(parent, "Dash", x + w - thickness, y + i, thickness, Mathf.Min(6f, h - i), color);
            }
        }
    }
}
