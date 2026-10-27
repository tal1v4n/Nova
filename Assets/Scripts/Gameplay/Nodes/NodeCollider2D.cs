using Nova.Core;
using UnityEngine;

namespace Nova.Gameplay
{
    // El trigger de clic del nodo (15.5, 18 #23, T6-01): el CircleCollider2D de la capa Nodes con el
    // radio de la huella sólida del sprite de su tipo y nivel, que mide el generador y llega en
    // NodeDefinition. Va de 0.3 a 0.6 u y cambia al mejorar y al reconvertir; no al capturar, porque la
    // silueta es la misma en las tres facciones (14.0 #3). Sin arte asignado, el radio se queda como esté.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController), typeof(CircleCollider2D))]
    public sealed class NodeCollider2D : MonoBehaviour
    {
        NodeController node;
        CircleCollider2D trigger;
        bool subscribed;

        public float Radius => Trigger.radius;

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

        // Se suscribe también aquí, porque OnEnable no se ejecuta en los tests de EditMode.
        public void Refresh()
        {
            Subscribe();
            if (Node.IsInitialized && Node.Definition.TryGetArt(Node.Level, out var art))
                Trigger.radius = art.FootprintRadius;
        }

        void Subscribe()
        {
            if (subscribed)
                return;
            Node.OnLevelChanged += HandleLevelChanged;
            Node.OnTypeChanged += HandleTypeChanged;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed)
                return;
            Node.OnLevelChanged -= HandleLevelChanged;
            Node.OnTypeChanged -= HandleTypeChanged;
            subscribed = false;
        }

        void HandleLevelChanged(int previous, int current) => Refresh();
        void HandleTypeChanged(NodeType previous, NodeType current) => Refresh();

        // Se resuelven a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
        CircleCollider2D Trigger => trigger != null ? trigger : trigger = GetComponent<CircleCollider2D>();
    }
}
