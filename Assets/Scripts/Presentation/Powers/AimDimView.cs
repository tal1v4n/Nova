using System.Collections.Generic;
using Nova.Gameplay;
using Nova.Input;
using Nova.Systems;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-06: la atenuación del modo apuntado (13.5, 7.2.1) sobre los nodos con arte. Lo que no es objetivo válido
    // —los nodos propios y todos los Asteroides— baja de color, y los objetivos se ven enteros. No es un asset
    // (A.4.3): es el sprite del nodo y sus overlays multiplicados por un color oscuro. Sustituye, en los nodos
    // con sprite, al velo de ProvisionalAimView, que tapaba con alfa parcial y el contrato no lo admite.
    //
    // El número de Energía no se atenúa: nunca se oculta (14.7). Solo lee PowerAimState y el registro; lo único
    // que escribe es el color de los SpriteRenderer que cuelgan del sprite del nodo, y lo devuelve a blanco al
    // dejar de apuntar. Con el arte de estados asignado nadie más escribe ese color (ProvisionalNodeView solo
    // tiñe sin NodeAnimator).
    [DefaultExecutionOrder(110)] // Presentation, después de las vistas de nodo (15.10)
    [DisallowMultipleComponent]
    public sealed class AimDimView : MonoBehaviour
    {
        // El sprite multiplicado: más o menos lo que dejaba ver el velo provisional, el fondo profundo al 60 %.
        public static readonly Color DimColor = new Color(0.40f, 0.42f, 0.50f, 1f);

        [SerializeField] PowerAimState aim;

        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance.")]
        [SerializeField] NodeRegistry nodes;

        readonly HashSet<NodeVisual> dimmed = new HashSet<NodeVisual>();
        readonly List<NodeVisual> released = new List<NodeVisual>();
        readonly List<SpriteRenderer> buffer = new List<SpriteRenderer>();

        internal bool IsDimmed(NodeController node) =>
            node != null && node.TryGetComponent(out NodeVisual visual) && dimmed.Contains(visual);

        // Para ProvisionalAimView: los nodos con sprite son de aquí y el velo no los tapa.
        internal static bool Handles(NodeController node) =>
            node != null && node.TryGetComponent(out NodeVisual _) && NodeVisual.SpriteOf(node) != null;

        void LateUpdate()
        {
            Refresh();
        }

        void OnDisable()
        {
            foreach (NodeVisual visual in dimmed)
                Paint(visual, Color.white);
            dimmed.Clear();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(PowerAimState powerAim, NodeRegistry registry = null)
        {
            aim = powerAim;
            nodes = registry;
        }

        public void Refresh()
        {
            released.Clear();
            released.AddRange(dimmed);
            NodeRegistry registry = nodes != null ? nodes : NodeRegistry.Instance;
            if (aim != null && aim.IsAiming && registry != null)
            {
                IReadOnlyList<NodeController> all = registry.All;
                for (int i = 0; i < all.Count; i++)
                {
                    NodeController node = all[i];
                    if (!Handles(node) || aim.IsValidTarget(node))
                        continue;
                    NodeVisual visual = node.GetComponent<NodeVisual>();
                    Paint(visual, DimColor); // cada frame: el animador cambia sprites y enciende overlays
                    dimmed.Add(visual);
                    released.Remove(visual);
                }
            }
            foreach (NodeVisual visual in released)
            {
                if (visual != null)
                    Paint(visual, Color.white);
                dimmed.Remove(visual);
            }
        }

        void Paint(NodeVisual visual, Color color)
        {
            if (visual == null)
                return;
            visual.Body.GetComponentsInChildren(true, buffer);
            foreach (SpriteRenderer renderer in buffer)
                renderer.color = color;
        }
    }
}
