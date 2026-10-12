using System;
using Nova.Core;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Input
{
    // Estado del panel de clic derecho (13.3, T2-03): si está abierto, de qué nodo y dónde. Lo
    // abre y lo cierra InputRouter, y el botón de mejora sale por el emisor de comandos como
    // cualquier otra acción; NodeInfoPanel (Nova.UI) solo lo dibuja.
    //
    // Como en IgnitionMenuState, la geometría vive aquí porque el router resuelve el clic contra
    // ella: el panel dibujado y su blanco salen del mismo sitio.
    [DisallowMultipleComponent]
    public sealed class InfoPanelState : MonoBehaviour
    {
        // 208 x 232 px de la referencia: nueve filas de 24 px entre dos márgenes de 8. Jersey 10 a
        // tamaño nativo avanza ~7 px por carácter; «Cadencia: 4.3 disparos/s» ocupa ~170 px.
        // Filas: tipo y nivel, Energía, velocidad de ataque, mejora, «Reconvertir» y los dos
        // destinos, en el orden de 13.3 (T2-07 añade esas dos), y la rueda de poderes del punto 6
        // (T3-07): su rótulo, las tres ranuras y «Cancelar». El alto es el mismo para todos los
        // nodos: en los que no son un Púlsar propio, las tres últimas filas quedan vacías.
        public const float Width = 3.25f;
        public const float Height = 3.625f;

        // El panel se abre a un lado del nodo, sin tapar su círculo de 64 px.
        public const float NodeClearance = 0.75f;

        // Centro de la fila n (0 arriba), medido desde el centro del panel: 8 px de margen más
        // media fila de 24.
        public static float RowY(int row) => Height / 2f - (8f + 12f + 24f * row) / 64f;

        // Botón de mejora: la cuarta fila, 192 x 26 px.
        public static readonly Vector2 ButtonSize = new Vector2(3f, 0.40625f);
        public static readonly Vector2 ButtonOffset = new Vector2(0f, RowY(3));

        // Reconversión (13.3 punto 5): dos botones de 94 x 26 px en la sexta fila, separados 4 px.
        // Son dos y no tres: nunca a Asteroide ni al propio tipo (8.1).
        public static readonly Vector2 ConversionButtonSize = new Vector2(1.46875f, 0.40625f);
        public static readonly Vector2[] ConversionOffsets =
        {
            new Vector2(-0.765625f, RowY(5)),
            new Vector2(0.765625f, RowY(5)),
        };

        // Rueda de poderes (13.5): tres ranuras de 60 x 26 px en la octava fila, separadas 6 px, en
        // el orden fijo 1 Cero Absoluto · 2 Decaimiento · 3 Agujero de Gusano.
        public static readonly PowerType[] WheelPowers = { PowerType.AbsoluteZero, PowerType.Decay, PowerType.Wormhole };
        public static readonly Vector2 WheelSlotSize = new Vector2(0.9375f, 0.40625f);
        public static readonly Vector2[] WheelSlotOffsets =
        {
            new Vector2(-1.03125f, RowY(7)),
            new Vector2(0f, RowY(7)),
            new Vector2(1.03125f, RowY(7)),
        };

        // «Cancelar +N» (7.1, 18 #19): la novena fila, del tamaño del botón de mejora.
        public static readonly Vector2 CancelOffset = new Vector2(0f, RowY(8));

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

        NodeController node;

        public bool IsOpen => node != null;
        public NodeController Node => node;
        public Vector2 Center { get; private set; }

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

        public bool IsOnButton(Vector2 world)
        {
            return IsOpen && Inside(world, Center + ButtonOffset, ButtonSize);
        }

        // Índice del botón de reconversión bajo world (0 o 1), o -1.
        public int ConversionOptionAt(Vector2 world)
        {
            if (!IsOpen)
                return -1;
            for (int i = 0; i < ConversionOffsets.Length; i++)
            {
                if (Inside(world, Center + ConversionOffsets[i], ConversionButtonSize))
                    return i;
            }
            return -1;
        }

        // Índice de la ranura de la rueda bajo world (0 a 2), o -1.
        public int WheelSlotAt(Vector2 world)
        {
            if (!IsOpen)
                return -1;
            for (int i = 0; i < WheelSlotOffsets.Length; i++)
            {
                if (Inside(world, Center + WheelSlotOffsets[i], WheelSlotSize))
                    return i;
            }
            return -1;
        }

        public bool IsOnCancel(Vector2 world)
        {
            return IsOpen && Inside(world, Center + CancelOffset, ButtonSize);
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
            OnChanged?.Invoke();
        }

        static bool Inside(Vector2 point, Vector2 center, Vector2 size)
        {
            Vector2 d = point - center;
            return Mathf.Abs(d.x) <= size.x / 2f && Mathf.Abs(d.y) <= size.y / 2f;
        }
    }
}
