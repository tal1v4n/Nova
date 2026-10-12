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
        // 208 x 160 px de la referencia: seis filas de 24 px bajo un margen de 8. Jersey 10 a
        // tamaño nativo avanza ~7 px por carácter; «Cadencia: 4.3 disparos/s» ocupa ~170 px.
        // Filas: tipo y nivel, Energía, velocidad de ataque, mejora, «Reconvertir» y los dos
        // destinos, en el orden de 13.3 (T2-07 añade las dos últimas).
        public const float Width = 3.25f;
        public const float Height = 2.5f;

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
