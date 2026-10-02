using System;
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
        // 208 x 112 px de la referencia: cuatro líneas de Jersey 10 a tamaño nativo, que avanza
        // ~7 px por carácter; «Cadencia: 4.3 disparos/s» ocupa ~170 px.
        public const float Width = 3.25f;
        public const float Height = 1.75f;

        // El panel se abre a un lado del nodo, sin tapar su círculo de 64 px.
        public const float NodeClearance = 0.75f;

        // Botón de mejora: la última línea del panel, 192 x 26 px.
        public static readonly Vector2 ButtonSize = new Vector2(3f, 0.40625f);
        public static readonly Vector2 ButtonOffset = new Vector2(0f, -Height / 2f + 0.3125f);

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
