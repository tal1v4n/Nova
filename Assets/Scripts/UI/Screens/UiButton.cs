using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nova.UI
{
    // El botón de la lámina «Lenguaje de UI» (13.12) tal como lo dibuja la maqueta (.b): marco de 4 px
    // y cara, los dos con la esquina en escalera, texto a la izquierda y, si lleva, un icono o una
    // tecla a la derecha.
    //   - Primario: cara cian con texto oscuro y un brillo que la recorre; encima, la cara se aclara.
    //   - Secundario: cara de panel; encima, el marco pasa a cian, un barrido la llena de izquierda a
    //     derecha y el texto se desplaza 12 px.
    //   - Peligro: confirma algo que tira la partida, en magenta, con el mismo barrido.
    // Pulsado, baja 4 px. Todo con tiempo no escalado y las duraciones de 13.12. Como el botón fija
    // su posición en cada frame, las entradas animadas mueven su contenedor, nunca el botón.
    [DisallowMultipleComponent]
    internal sealed class UiButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
                                     IPointerUpHandler, IPointerClickHandler
    {
        internal enum Style { Primary, Secondary, Danger }

        const float Pad = 24f;
        const float HoverShift = 12f;

        Style style;
        Image frame;
        Image face;
        RectTransform sweep;
        RectTransform shine;
        TextMeshProUGUI label;
        RectTransform icon;
        Vector2 home;
        Vector2 labelHome;
        float hover;
        float hoverTarget;
        float press;
        bool pressed;
        float iconTurn;
        bool spins;
        float shineTime;
        float shineDelay;
        bool hasShine;

        public int Id { get; private set; }
        public event Action<int> OnClick;
        public TextMeshProUGUI Label => label;
        public RectTransform Rect => (RectTransform)transform;

        // Construye el botón con su esquina superior izquierda en (x, y). size es el tamaño del texto
        // y h el alto de la cara: 72 px los grandes, 56 los de la confirmación (la maqueta).
        public static UiButton Create(Transform parent, TMP_FontAsset font, int id, string text, Style style,
                                      float x, float y, float w, float faceHeight = 72f, float size = UiKit.X3)
        {
            Image frame = UiKit.Box(parent, $"Button{id}", x, y, w, faceHeight + 8f, UiKit.Frame, true);
            frame.raycastTarget = true;
            var button = frame.gameObject.AddComponent<UiButton>();
            button.Build(font, id, text, style, w, faceHeight, size);
            return button;
        }

        void Build(TMP_FontAsset font, int id, string text, Style buttonStyle, float w, float faceHeight, float size)
        {
            Id = id;
            style = buttonStyle;
            frame = GetComponent<Image>();
            face = UiKit.Box(transform, "Face", 4f, 4f, w - 8f, faceHeight, UiKit.Panel, true);
            // La cara recorta el barrido y el brillo con su escalera.
            face.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            if (style == Style.Primary)
            {
                shine = UiKit.Node(face.transform, "Shine", 0f, 0f, (w - 8f) * 0.3f, faceHeight);
                Image band = shine.gameObject.AddComponent<Image>();
                band.sprite = UiKit.Shine;
                band.raycastTarget = false;
                shine.gameObject.SetActive(false);
            }
            else
            {
                Image fill = UiKit.Box(face.transform, "Sweep", 0f, 0f, 0f, faceHeight, style == Style.Danger ? UiKit.EnemySweep : UiKit.Sweep);
                sweep = fill.rectTransform;
            }

            Color32 ink = style == Style.Primary ? UiKit.Space : style == Style.Danger ? UiKit.EnemyText : UiKit.Text;
            label = UiKit.Label(face.transform, font, text, Pad, 0f, w - 8f - 2f * Pad, size, ink, faceHeight,
                align: TextAlignmentOptions.MidlineLeft);
            label.rectTransform.sizeDelta = new Vector2(w - 8f - 2f * Pad, faceHeight);
            labelHome = label.rectTransform.anchoredPosition;
            home = Rect.anchoredPosition;
            Paint();
        }

        // Un icono de pixel art a la derecha (▸, ↺), a ×4.
        public void SetIcon(Sprite sprite, Color32 color)
        {
            Vector2 size = sprite.rect.size * 4f;
            float w = face.rectTransform.sizeDelta.x;
            float h = face.rectTransform.sizeDelta.y;
            Image image = UiKit.Box(face.transform, "Icon", w - Pad - size.x, (h - size.y) / 2f, size.x, size.y, color);
            image.sprite = sprite;
            icon = image.rectTransform;
            spins = sprite == UiKit.Loop;
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition += new Vector2(size.x / 2f, -size.y / 2f);
        }

        // La tecla de CONTINUAR: Esc en un marco de 2 px del color del texto.
        public void SetKey(TMP_FontAsset font, string key)
        {
            float w = face.rectTransform.sizeDelta.x;
            float h = face.rectTransform.sizeDelta.y;
            RectTransform holder = UiKit.Node(face.transform, "Key", 0f, 0f, w, h);
            float keyWidth = UiKit.Key(holder, font, key, 0f, (h - 34f) / 2f, label.color, label.color);
            holder.anchoredPosition = new Vector2(w - Pad - keyWidth, 0f);
        }

        // El brillo recorre la cara cada 3.6 s, empezando a los delay s (la maqueta).
        public void StartShine(float delay)
        {
            if (shine == null)
                return;
            hasShine = true;
            shineDelay = delay;
            shineTime = 0f;
            shine.gameObject.SetActive(true);
            PlaceShine(0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoverTarget = 1f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverTarget = 0f;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Click();
        }

        // Lo mismo que un clic: para los tests y para quien quiera pulsarlo desde código.
        public void Click()
        {
            OnClick?.Invoke(Id);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            // Encima: 0.3 s para colores, como la maqueta; la curva orbital se aplica al pintar.
            hover = Mathf.MoveTowards(hover, hoverTarget, dt / 0.3f);
            // Pulsado: 0.15 s.
            press = Mathf.MoveTowards(press, pressed ? 1f : 0f, dt / 0.15f);
            if (spins)
            {
                // ↺ da una vuelta al entrar el ratón, en 0.6 s.
                iconTurn = hoverTarget > 0f ? Mathf.Min(1f, iconTurn + dt / 0.6f) : 0f;
                icon.localRotation = Quaternion.Euler(0f, 0f, 360f * UiMotion.Orbital(iconTurn));
            }
            if (hasShine)
            {
                shineTime += dt;
                if (shineTime >= shineDelay)
                    PlaceShine(Mathf.Repeat(shineTime - shineDelay, 3.6f) / 3.6f);
            }
            Paint();
        }

        void Paint()
        {
            float h = UiMotion.Orbital(hover);
            float faceWidth = face.rectTransform.sizeDelta.x;
            switch (style)
            {
                case Style.Primary:
                    frame.color = UiKit.PlayerLight;
                    face.color = Color.Lerp(UiKit.Player, UiKit.PlayerLight, hover);
                    break;
                case Style.Danger:
                    frame.color = UiKit.EnemyDeep;
                    face.color = UiKit.EnemyDark;
                    break;
                default:
                    frame.color = Color.Lerp(UiKit.Frame, UiKit.Player, hover);
                    face.color = UiKit.Panel;
                    label.color = Color.Lerp(UiKit.Text, UiKit.Bright, hover);
                    label.rectTransform.anchoredPosition = labelHome + new Vector2(HoverShift * h, 0f);
                    break;
            }
            if (sweep != null)
                sweep.sizeDelta = new Vector2(faceWidth * h, sweep.sizeDelta.y);
            Rect.anchoredPosition = home + new Vector2(0f, -4f * UiMotion.Orbital(press));
        }

        // La maqueta: de -40 % a 120 % del ancho en el primer 35 % del ciclo, con ease-in-out; luego quieto.
        void PlaceShine(float phase)
        {
            float w = face.rectTransform.sizeDelta.x;
            float t = UiMotion.EaseInOut(Mathf.Clamp01(phase / 0.35f));
            shine.anchoredPosition = new Vector2(Mathf.Lerp(-0.4f, 1.2f, t) * w, 0f);
        }
    }
}
