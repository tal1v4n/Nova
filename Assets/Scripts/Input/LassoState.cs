using UnityEngine;

namespace Nova.Input
{
    // El lazo mientras se arrastra (13.1, T6-06): de dónde sale y hasta dónde llega. Lo escribe InputRouter en el
    // tick, como SendLineState, y lo lee la vista del lazo. Al soltar, el lazo selecciona y desaparece; en modo
    // apuntado un arrastre no es lazo y no hay nada que dibujar.
    [DisallowMultipleComponent]
    public sealed class LassoState : MonoBehaviour
    {
        public bool IsActive { get; private set; }

        // Los dos extremos en el mundo: donde se pulsó y donde está el cursor.
        public Vector2 From { get; private set; }
        public Vector2 To { get; private set; }

        public void Set(Vector2 from, Vector2 to)
        {
            IsActive = true;
            From = from;
            To = to;
        }

        public void Clear()
        {
            IsActive = false;
        }
    }
}
