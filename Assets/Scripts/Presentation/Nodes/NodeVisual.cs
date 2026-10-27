using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-01: el sprite del nodo según su tipo, nivel y facción (15.8, A.2). Es la capa 1 de 14.7. Solo
    // lee eventos de NodeController, nunca escribe en él (14.0 #1).
    //
    // - Capturar cambia el sprite en el acto, en el frame 0 de Capturado (14.1): la silueta es la misma
    //   en las tres facciones (14.0 #3), así que es un recoloreado en el sitio.
    // - Mejorar cambia de sprite, y reconvertir o encender también: OnTypeChanged llega con el nodo ya
    //   en su nivel nuevo.
    // - Sin arte asignado (NodeDefinition.HasArt) o sin sprite para esa facción no dibuja nada, y
    //   ProvisionalNodeView vuelve a su círculo.
    //
    // Los estados (T6-02), el susto (T6-03) y el idle no son de aquí: esto es el sprite base. El
    // pivote es el centro de la celda de 96 y las posiciones de los niveles caen en la rejilla de
    // 1/64 u, así que el sprite queda a píxel entero.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class NodeVisual : MonoBehaviour
    {
        // Capa 1 de 14.7: por encima de la línea de envío y del arco (capa 0, −1) y por debajo del número.
        public const int SortingOrder = 0;

        NodeController node;
        SpriteRenderer body;
        bool subscribed;

        internal SpriteRenderer Body { get { EnsureBuilt(); return body; } }

        // Si hay sprite que enseñar: lo que decide si la vista provisional dibuja su círculo.
        public bool IsShowing => SpriteOf(Node) != null;

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

        public void Refresh()
        {
            EnsureBuilt();
            Sprite sprite = SpriteOf(Node);
            body.sprite = sprite;
            body.enabled = sprite != null;
        }

        // El sprite de un nodo, o null si no lo hay. Sin efectos: lo usa también la vista provisional.
        internal static Sprite SpriteOf(NodeController node)
        {
            if (node == null || !node.IsInitialized || !node.Definition.TryGetArt(node.Level, out NodeLevelArt art))
                return null;
            return art.SpriteFor(node.Faction);
        }

        // Huella sólida del sprite, en píxeles, o 0 si no lo hay.
        internal static int FootprintPixelsOf(NodeController node)
        {
            if (SpriteOf(node) == null)
                return 0;
            node.Definition.TryGetArt(node.Level, out NodeLevelArt art);
            return art.FootprintPixels;
        }

        internal void Subscribe()
        {
            if (subscribed)
                return;
            Node.OnFactionChanged += HandleFactionChanged;
            Node.OnLevelChanged += HandleLevelChanged;
            Node.OnTypeChanged += HandleTypeChanged;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (!subscribed)
                return;
            Node.OnFactionChanged -= HandleFactionChanged;
            Node.OnLevelChanged -= HandleLevelChanged;
            Node.OnTypeChanged -= HandleTypeChanged;
            subscribed = false;
        }

        void HandleFactionChanged(Faction previous, Faction current) => Refresh();
        void HandleLevelChanged(int previous, int current) => Refresh();
        void HandleTypeChanged(NodeType previous, NodeType current) => Refresh();

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();

        void EnsureBuilt()
        {
            if (body != null)
                return;

            var bodyObject = new GameObject("Sprite");
            bodyObject.transform.SetParent(transform, false);
            body = bodyObject.AddComponent<SpriteRenderer>();
            body.sortingOrder = SortingOrder;
            body.enabled = false;
        }
    }

    // Lo que cuelga bajo un nodo con sprite, de arriba abajo: la huella, el número de Energía y la barra
    // de carga del Púlsar. Decidido con el usuario el 2026-10-26: el número va bajo el sprite y no
    // encima, porque centrado tapaba la cara, y la cara es la que avisa del susto (14.7). Todo en
    // píxeles enteros desde el centro del nodo.
    internal static class NodeLayout
    {
        // Aire entre la huella y el número, y entre el número y la barra.
        internal const int Gap = 2;

        // Altura de la mayúscula de Jersey 10 a tamaño nativo (NOVA_Arte_Tecnica.md).
        internal const int NativeCapPixels = 10;

        // Media huella redondeada hacia fuera: el número nunca pisa la materia del sprite.
        internal static int HalfFootprint(int footprintPixels) => (footprintPixels + 1) / 2;

        // Centro del número bajo el centro del nodo.
        internal static int LabelCenterPixels(int footprintPixels, int textScale) =>
            HalfFootprint(footprintPixels) + Gap + NativeCapPixels * textScale / 2;

        // Borde de arriba de la barra de carga bajo el centro del nodo.
        internal static int ChargeBarTopPixels(int footprintPixels, int textScale) =>
            HalfFootprint(footprintPixels) + Gap + NativeCapPixels * textScale + Gap;
    }
}
