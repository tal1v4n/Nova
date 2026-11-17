using System;
using Nova.Core;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Input
{
    // Estado del panel de clic derecho (13.3, T2-03): si está abierto, de qué nodo y dónde. Lo
    // abre y lo cierra InputRouter, y sus botones salen por el emisor de comandos como cualquier
    // otra acción; NodeInfoPanel (Nova.UI) solo lo dibuja. Desde T6-15 la mejora no es un botón
    // del panel sino el icono sobre el nodo (13.13): la cuarta fila es solo texto.
    //
    // Como en IgnitionMenuState, la geometría vive aquí porque el router resuelve el clic contra
    // ella: el panel dibujado y su blanco salen del mismo sitio.
    //
    // T6-09: el alto sigue al contenido (13.3, ART-UI-PANEL). Las filas salen del nodo y de quién mira
    // (Layout): tres de información, la de la mejora si hay algo que decir, las dos de la reconversión en
    // un nodo propio y las tres de la rueda en un Púlsar propio. Y como el arte tiene encima y pulsado, el
    // router escribe aquí el cursor en cada frame (SetPointer), como en HudState.
    [DisallowMultipleComponent]
    public sealed class InfoPanelState : MonoBehaviour
    {
        // 208 px de ancho; de alto, dos márgenes de 8 y una fila de 24 por cada fila del contenido. Jersey 10 a
        // tamaño nativo avanza ~7 px por carácter; «Cadencia: 4.3 disparos/s» ocupa ~170 px.
        public const float Width = 3.25f;
        public const int PaddingPixels = 8;
        public const int RowPixels = 24;
        public const int WidthPixels = 208;

        // El panel se abre a un lado del nodo, sin tapar su círculo de 64 px.
        public const float NodeClearance = 0.75f;

        // Reconversión (13.3 punto 5, ART-UI-BTN-CONV): dos botones de 94 x 20 px en su fila, separados 4 px.
        // Son dos y no tres: nunca a Asteroide ni al propio tipo (8.1).
        public static readonly Vector2 ConversionButtonSize = new Vector2(1.46875f, 0.3125f);
        static readonly float[] ConversionX = { -0.765625f, 0.765625f };

        // Rueda de poderes (13.5, ART-UI-WHEEL): tres ranuras de 60 x 20 px separadas 6 px, en el orden fijo
        // 1 Cero Absoluto · 2 Decaimiento · 3 Agujero de Gusano, y debajo «Cancelar +N», de 192 x 20.
        public static readonly PowerType[] WheelPowers = { PowerType.AbsoluteZero, PowerType.Decay, PowerType.Wormhole };
        public static readonly Vector2 WheelSlotSize = new Vector2(0.9375f, 0.3125f);
        static readonly float[] WheelSlotX = { -1.03125f, 0f, 1.03125f };
        public static readonly Vector2 CancelSize = new Vector2(3f, 0.3125f);

        // Las filas del panel de un nodo. -1: esa fila no está. Blocks: la primera fila de cada bloque de 13.3
        // —información, mejora, reconversión y poderes—, para los separadores del arte.
        public readonly struct PanelLayout
        {
            public readonly int Rows;
            public readonly int UpgradeRow;
            public readonly int ConversionHeaderRow;
            public readonly int ConversionRow;
            public readonly int WheelHeaderRow;
            public readonly int WheelRow;
            public readonly int CancelRow;
            public readonly int[] Blocks;

            public PanelLayout(int rows, int upgrade, int conversionHeader, int wheelHeader, int[] blocks)
            {
                Rows = rows;
                UpgradeRow = upgrade;
                ConversionHeaderRow = conversionHeader;
                ConversionRow = conversionHeader >= 0 ? conversionHeader + 1 : -1;
                WheelHeaderRow = wheelHeader;
                WheelRow = wheelHeader >= 0 ? wheelHeader + 1 : -1;
                CancelRow = wheelHeader >= 0 ? wheelHeader + 2 : -1;
                Blocks = blocks;
            }

            public bool HasConversion => ConversionHeaderRow >= 0;
            public bool HasWheel => WheelHeaderRow >= 0;
            public int HeightPixels => 2 * PaddingPixels + RowPixels * Rows;
            public float Height => HeightPixels / 64f;

            // Centro de la fila n (0 arriba), medido desde el centro del panel.
            public float RowY(int row) => Height / 2f - (PaddingPixels + RowPixels / 2f + RowPixels * row) / 64f;
        }

        // 13.3: un Asteroide enseña su nombre, su Energía, DORMIDO y el coste de encendido (13.4). Un nodo
        // encendido, tipo y nivel, Energía y velocidad de ataque; la mejora si es propio o está al máximo
        // (NIVEL MÁXIMO es información para todos); la reconversión si es propio; y la rueda en un Púlsar propio.
        public static PanelLayout LayoutFor(NodeController node, Faction viewer)
        {
            if (node == null || !node.IsInitialized)
                return new PanelLayout(3, -1, -1, -1, new[] { 0 });
            if (node.Type == NodeType.Asteroid)
                return new PanelLayout(4, 3, -1, -1, new[] { 0 });

            int rows = 3;
            var blocks = new System.Collections.Generic.List<int> { 0 };
            int upgrade = -1;
            bool own = node.Faction == viewer;
            if (own || (node.Definition != null && node.Level >= node.Definition.MaxLevel))
            {
                upgrade = rows++;
                blocks.Add(upgrade);
            }
            int conversion = -1;
            if (own && node.TryGetComponent(out NodeConverter _))
            {
                conversion = rows;
                rows += 2;
                blocks.Add(conversion);
            }
            int wheel = -1;
            if (ShowsWheel(node, viewer))
            {
                wheel = rows;
                rows += 3;
                blocks.Add(wheel);
            }
            return new PanelLayout(rows, upgrade, conversion, wheel, blocks.ToArray());
        }

        // Los dos destinos de un tipo encendido, en el orden de 16.2: Estrella, Púlsar, Magnetar.
        public static NodeType[] ConversionTargets(NodeType from)
        {
            switch (from)
            {
                case NodeType.Star: return new[] { NodeType.Pulsar, NodeType.Magnetar };
                case NodeType.Pulsar: return new[] { NodeType.Star, NodeType.Magnetar };
                case NodeType.Magnetar: return new[] { NodeType.Star, NodeType.Pulsar };
                default: return Array.Empty<NodeType>();
            }
        }

        [Tooltip("Facción que mira el panel: decide qué filas tiene (13.3).")]
        [SerializeField] Faction viewer = Faction.Player;

        NodeController node;

        public bool IsOpen => node != null;
        public NodeController Node => node;
        public Vector2 Center { get; private set; }
        public Faction Viewer => viewer;

        // Las filas del nodo abierto, tal como está ahora: si lo capturan, pierde las suyas.
        public PanelLayout Layout => LayoutFor(node, viewer);
        public float Height => Layout.Height;
        public float RowY(int row) => Layout.RowY(row);

        // Abierto, cerrado o pasado a otro nodo.
        public event Action OnChanged;

        // Centro del panel a la derecha (o a la izquierda) de un nodo.
        public static Vector2 CenterBeside(Vector2 nodePosition, bool toLeft)
        {
            float side = toLeft ? -1f : 1f;
            return nodePosition + new Vector2(side * (NodeClearance + Width / 2f), 0f);
        }

        public bool Contains(Vector2 world)
        {
            return IsOpen && Inside(world, Center, new Vector2(Width, Height));
        }

        // Dónde está cada botón, relativo al centro del panel; Vector2.zero si su fila no está.
        public Vector2 ConversionOffset(int index)
        {
            PanelLayout layout = Layout;
            return layout.HasConversion ? new Vector2(ConversionX[index], layout.RowY(layout.ConversionRow)) : Vector2.zero;
        }

        public Vector2 WheelSlotOffset(int index)
        {
            PanelLayout layout = Layout;
            return layout.HasWheel ? new Vector2(WheelSlotX[index], layout.RowY(layout.WheelRow)) : Vector2.zero;
        }

        public Vector2 CancelOffset
        {
            get
            {
                PanelLayout layout = Layout;
                return layout.HasWheel ? new Vector2(0f, layout.RowY(layout.CancelRow)) : Vector2.zero;
            }
        }

        // Índice del botón de reconversión bajo world (0 o 1), o -1.
        public int ConversionOptionAt(Vector2 world)
        {
            if (!IsOpen || !Layout.HasConversion)
                return -1;
            for (int i = 0; i < ConversionX.Length; i++)
            {
                if (Inside(world, Center + ConversionOffset(i), ConversionButtonSize))
                    return i;
            }
            return -1;
        }

        // Índice de la ranura de la rueda bajo world (0 a 2), o -1.
        public int WheelSlotAt(Vector2 world)
        {
            if (!IsOpen || !Layout.HasWheel)
                return -1;
            for (int i = 0; i < WheelSlotX.Length; i++)
            {
                if (Inside(world, Center + WheelSlotOffset(i), WheelSlotSize))
                    return i;
            }
            return -1;
        }

        public bool IsOnCancel(Vector2 world)
        {
            return IsOpen && Layout.HasWheel && Inside(world, Center + CancelOffset, CancelSize);
        }

        // 13.3 punto 6: la rueda solo sale en un Púlsar propio que pueda cargar.
        public static bool ShowsWheel(NodeController node, Faction viewer)
        {
            return node != null && node.IsInitialized && node.Type == NodeType.Pulsar
                && node.Faction == viewer && node.TryGetComponent(out PulsarCaster _);
        }

        public void Open(NodeController target, Vector2 center)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            node = target;
            Center = center;
            OnChanged?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            node = null;
            Release();
            OnChanged?.Invoke();
        }

        // --- El cursor, para el encima y el pulsado del arte (T6-09) ---

        // Qué botón tiene el cursor encima: un botón de reconversión (0, 1), una ranura (0 a 2) o Cancelar.
        public int HoveredConversion { get; private set; } = -1;
        public int HoveredSlot { get; private set; } = -1;
        public bool CancelHovered { get; private set; }

        bool held;
        int pressedTarget = NoTarget;
        const int NoTarget = -1;
        const int CancelTarget = 10;

        // Pulsado: el botón izquierdo bajó sobre ese mismo botón, sigue abajo y el cursor sigue encima.
        public bool IsConversionPressed(int index) => held && pressedTarget == index && HoveredConversion == index;
        public bool IsSlotPressed(int index) => held && pressedTarget == 3 + index && HoveredSlot == index;
        public bool CancelPressed => held && pressedTarget == CancelTarget && CancelHovered;

        // Del router, en Update: dónde está el cursor y si el botón izquierdo está abajo.
        public void SetPointer(Vector2 world, bool leftHeld)
        {
            HoveredConversion = ConversionOptionAt(world);
            HoveredSlot = WheelSlotAt(world);
            CancelHovered = IsOnCancel(world);
            int target = HoveredConversion >= 0 ? HoveredConversion
                : HoveredSlot >= 0 ? 3 + HoveredSlot
                : CancelHovered ? CancelTarget
                : NoTarget;
            if (leftHeld && !held)
                pressedTarget = target;
            held = leftHeld;
            if (!held)
                pressedTarget = NoTarget;
        }

        // Al pausar o cerrar: los botones vuelven a su estado normal.
        public void Release()
        {
            held = false;
            pressedTarget = NoTarget;
            HoveredConversion = -1;
            HoveredSlot = -1;
            CancelHovered = false;
        }

        static bool Inside(Vector2 point, Vector2 center, Vector2 size)
        {
            Vector2 d = point - center;
            return Mathf.Abs(d.x) <= size.x / 2f && Mathf.Abs(d.y) <= size.y / 2f;
        }
    }
}
