using Nova.Core;
using Nova.Gameplay;
using Nova.Utils;
using TMPro;
using UnityEngine;

namespace Nova.Presentation
{
    // T1-06b: vista provisional del nodo. Un círculo del color de su facción (14.4) con la
    // Energía encima en Jersey 10. Desde T1-12 el Asteroide es un círculo más pequeño, para que
    // el encendido se vea sin leer números. Primitiva de la regla 4 de T.2: no es arte, es desechable
    // y la sustituye NodeVisual en T6-01. Solo lee eventos de NodeController, nunca escribe en él.
    //
    // T2-08: asustado (14.2), el círculo y el número tiemblan 1 px a los lados. Se mueven los hijos
    // y no el nodo, para que el collider no tiemble.
    //
    // T6-01: con NodeVisual enseñando su sprite, el círculo no se dibuja. Lo que queda es lo que aún
    // no tiene arte: el número de Energía, bajo el sprite y en claro sobre el cielo (NodeLayout;
    // NodeHUD, ART-UI-HUD-ENERGY), y el tinte, el halo y el temblor de los estados, que se aplican al
    // sprite hasta T6-02 y T6-03. Sin sprite, todo sigue como antes.
    //
    // T6-02: con NodeAnimator y el arte de A.3, los estados los dibuja él: aquí ya no se tiñe el sprite
    // ni se pinta el halo. Y el sprite lo mueve él (el cabeceo),
    // porque dos componentes escribiendo la misma posición se pisan.
    //
    // T6-09: con el arte del número (EnergyLabelView, ART-UI-HUD-ENERGY), aquí ya no se dibuja el número.
    //
    // T6-03: el susto definitivo es de NodeAnimator (salto y temblor, capa 5 de 14.7). Con él moviendo el
    // sprite, aquí no tiembla nada: el número se queda quieto, como con el cabeceo (A.2.5). Este temblor
    // solo queda para el círculo de las escenas sin arte.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class ProvisionalNodeView : MonoBehaviour
    {
        // Paleta funcional de 14.4.
        static readonly Color32 PlayerColor = new Color32(0x3F, 0xD7, 0xF5, 0xFF);
        static readonly Color32 EnemyColor = new Color32(0xF2, 0x45, 0x6B, 0xFF);
        static readonly Color32 NeutralColor = new Color32(0x8A, 0x93, 0xA6, 0xFF);
        static readonly Color32 FrozenColor = new Color32(0xB8, 0xF0, 0xFF, 0xFF); // Congelado
        static readonly Color32 InfectedColor = new Color32(0x7C, 0xF2, 0x3F, 0xFF); // Infectado
        static readonly Color32 LabelColor = new Color32(0x0B, 0x0E, 0x1A, 0xFF); // fondo profundo
        static readonly Color32 SkyLabelColor = new Color32(0xE6, 0xEC, 0xF5, 0xFF); // texto de la UI, sobre el cielo
        static readonly Color32 White = new Color32(0xFF, 0xFF, 0xFF, 0xFF); // el sprite sin teñir

        const int PixelsPerUnit = 64; // T0-04

        // Temblor provisional del susto, en píxeles enteros (NOVA_Arte_Tecnica.md, *Animación*).
        const int ShakePixels = 1;
        const float ShakeStepsPerSecond = 15f;

        // Halo provisional de Infectado: 4 px más por cada lado que el círculo.
        const int AuraMarginPixels = 4;

        // Jersey 10: rejilla de 75 unidades en un em de 1400, 18.67 px por em (NOVA_Arte_Tecnica.md).
        const float NativeEmPixels = 1400f / 75f;

        // TextMeshPro en mundo mide el em en fontSize × 0.1 unidades.
        const float WorldTextScale = 0.1f;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Diámetro del círculo de un nodo encendido, en píxeles. La huella de un nodo va de 38 a 77 px (A.8 #4).")]
        [SerializeField, Range(38, 77)] int diameterPixels = 64;

        [Tooltip("Diámetro del círculo del Asteroide, en píxeles. Menor que el encendido para que el encendido se vea.")]
        [SerializeField, Range(38, 77)] int asteroidDiameterPixels = 48;

        [Tooltip("Múltiplo entero del tamaño nativo de Jersey 10: un tamaño intermedio se interpola.")]
        [SerializeField, Range(1, 4)] int textScale = 2;

        NodeController node;
        NodeVisual visual;
        bool visualLooked;
        NodeAnimator animator;
        bool animatorLooked;
        Vector3 labelPosition; // el número sin temblor: bajo el sprite, o en el centro del círculo
        SpriteRenderer body;
        SpriteRenderer aura;
        TextMeshPro label;
        bool subscribed;

        internal SpriteRenderer Body { get { EnsureBuilt(); return body; } }
        internal TextMeshPro Label { get { EnsureBuilt(); return label; } }
        internal int TextScale => textScale;
        internal SpriteRenderer Aura { get { EnsureBuilt(); return aura; } }

        void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        // Initialize no emite eventos: quien crea el nodo lo inicializa antes de su primer frame.
        void Start()
        {
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        // Presentación en Update (15.10): solo lee la bandera y mueve los hijos.
        void Update()
        {
            if (body == null || label == null || !ShakesItself)
                return;
            ApplyOffset(ShakeOffset(Mathf.FloorToInt(Time.time * ShakeStepsPerSecond)));
        }

        public void Refresh()
        {
            EnsureBuilt();

            bool visible = Node.IsInitialized;
            body.enabled = visible;
            // T6-09: con el arte del número (EnergyLabelView), el TextMeshPro se aparta.
            label.enabled = visible && !LabelDrawnByArt;
            aura.enabled = visible && Node.HasState(NodeStateFlags.Infected) && !AnimatorDrawsStates;
            if (!visible)
                return;

            // T6-01: con sprite, el círculo se apaga y el tinte va al sprite, que parte de blanco.
            bool art = HasArt;
            body.enabled = !art;
            SpriteRenderer tinted = art ? Visual.Body : body;

            // T2-07: reconvirtiéndose, el círculo baja a la mitad de brillo. Sigue siendo del color
            // de su dueño, porque durante los 3 s el nodo es suyo (8.1, «sin tipo, no sin bando»).
            // T3-04: congelado, el círculo va a medio camino del hielo, y el dueño se sigue leyendo.
            // Congelado gana a Reconvirtiendo, como en el orden de tintes de 14.7.
            Color32 color = art ? White : ColorOf(Node.Faction);
            if (art && AnimatorDrawsStates)
                tinted.color = White;
            else if (Node.HasState(NodeStateFlags.Frozen))
                tinted.color = Iced(color);
            else
                tinted.color = Node.HasState(NodeStateFlags.Converting) ? Dimmed(color) : color;
            int diameter = art ? NodeVisual.FootprintPixelsOf(Node)
                : Node.Type == NodeType.Asteroid ? asteroidDiameterPixels : diameterPixels;
            body.sprite = CircleSprite(diameter);
            // T3-05: infectado, un halo verde por detrás del círculo (la capa 3 de 14.7, por debajo del
            // tinte). Convive con el hielo, que tiñe el círculo y no el halo.
            aura.sprite = CircleSprite(diameter + 2 * AuraMarginPixels);
            label.text = Node.Energy.ToString();
            label.color = art ? SkyLabelColor : LabelColor;
            labelPosition = art
                ? new Vector3(0f, -NodeLayout.LabelCenterPixels(diameter, textScale) / (float)PixelsPerUnit, 0f)
                : Vector3.zero;

            ApplyOffset(ShakesItself ? ShakeOffset(Mathf.FloorToInt(Time.time * ShakeStepsPerSecond)) : Vector3.zero);
        }

        // T6-03: con NodeAnimator moviendo el sprite, el susto es suyo.
        bool ShakesItself => IsScared && !(HasArt && Animator != null);

        bool HasArt => Visual != null && NodeVisual.SpriteOf(Node) != null;

        bool LabelDrawnByArt => TryGetComponent(out EnergyLabelView energyLabel) && energyLabel.Draws;

        bool AnimatorDrawsStates => Animator != null && Animator.DrawsStates;

        // NodeAnimator es opcional, como NodeVisual.
        NodeAnimator Animator
        {
            get
            {
                if (!animatorLooked)
                {
                    animator = GetComponent<NodeAnimator>();
                    animatorLooked = true;
                }
                return animator;
            }
        }

        // NodeVisual es opcional: las escenas de prueba de las fases 1–5 no lo llevan.
        NodeVisual Visual
        {
            get
            {
                if (!visualLooked)
                {
                    visual = GetComponent<NodeVisual>();
                    visualLooked = true;
                }
                return visual;
            }
        }

        // Paso del temblor: un píxel a la derecha en los pares y uno a la izquierda en los impares.
        internal static Vector3 ShakeOffset(int step)
        {
            int direction = (step & 1) == 0 ? 1 : -1;
            return new Vector3(direction * ShakePixels / (float)PixelsPerUnit, 0f, 0f);
        }

        bool IsScared => Node.IsInitialized && Node.HasState(NodeStateFlags.Scared);

        void ApplyOffset(Vector3 offset)
        {
            body.transform.localPosition = offset;
            aura.transform.localPosition = offset;
            label.transform.localPosition = labelPosition + offset;
            if (HasArt && Animator == null)
                Visual.Body.transform.localPosition = offset;
        }

        internal static Color32 ColorOf(Faction faction)
        {
            switch (faction)
            {
                case Faction.Player: return PlayerColor;
                case Faction.Enemy: return EnemyColor;
                default: return NeutralColor;
            }
        }

        internal static Color32 Dimmed(Color32 color)
        {
            return new Color32((byte)(color.r / 2), (byte)(color.g / 2), (byte)(color.b / 2), color.a);
        }

        internal static Color32 Iced(Color32 color)
        {
            return Color32.Lerp(color, FrozenColor, 0.5f);
        }

        internal static float FontSizeFor(int scale)
        {
            return NativeEmPixels * scale / PixelsPerUnit / WorldTextScale;
        }

        internal void Subscribe()
        {
            if (subscribed)
                return;
            Node.OnFactionChanged += HandleFactionChanged;
            Node.OnEnergyChanged += HandleEnergyChanged;
            Node.OnLevelChanged += HandleLevelChanged;
            Node.OnTypeChanged += HandleTypeChanged;
            Node.OnStateFlagsChanged += HandleFlagsChanged;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (!subscribed)
                return;
            Node.OnFactionChanged -= HandleFactionChanged;
            Node.OnEnergyChanged -= HandleEnergyChanged;
            Node.OnLevelChanged -= HandleLevelChanged;
            Node.OnTypeChanged -= HandleTypeChanged;
            Node.OnStateFlagsChanged -= HandleFlagsChanged;
            subscribed = false;
        }

        void HandleFactionChanged(Faction previous, Faction current) => Refresh();
        void HandleEnergyChanged(int previous, int current) => Refresh();
        void HandleLevelChanged(int previous, int current) => Refresh();
        void HandleTypeChanged(NodeType previous, NodeType current) => Refresh();
        void HandleFlagsChanged(NodeStateFlags previous, NodeStateFlags current) => Refresh();

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();

        void EnsureBuilt()
        {
            if (body != null && label != null && aura != null)
                return;

            var auraObject = new GameObject("ProvisionalAura");
            auraObject.transform.SetParent(transform, false);
            aura = auraObject.AddComponent<SpriteRenderer>();
            aura.sprite = CircleSprite(diameterPixels + 2 * AuraMarginPixels);
            aura.color = InfectedColor;
            aura.sortingOrder = BoardOrder.Strokes;
            aura.enabled = false;

            var bodyObject = new GameObject("ProvisionalBody");
            bodyObject.transform.SetParent(transform, false);
            body = bodyObject.AddComponent<SpriteRenderer>();
            body.sprite = CircleSprite(diameterPixels);
            body.sortingOrder = BoardOrder.Body;

            var labelObject = new GameObject("ProvisionalEnergy");
            labelObject.transform.SetParent(transform, false);
            label = labelObject.AddComponent<TextMeshPro>();
            if (font != null)
                label.font = font;
            else
                Debug.LogWarning($"[Nova] {name}: ProvisionalNodeView sin font asset; asigna Jersey10 Raster56.", this);
            label.fontSize = FontSizeFor(textScale);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = LabelColor;
            label.rectTransform.sizeDelta = new Vector2(diameterPixels, diameterPixels) / PixelsPerUnit;
            label.sortingOrder = BoardOrder.Energy;
        }

        // Disco blanco de alfa binario y filtro Point, teñido por SpriteRenderer.color.
        internal static Sprite CircleSprite(int diameter) => PrimitiveSprites.Disc(diameter);
    }
}
