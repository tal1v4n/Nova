using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Gameplay;
using Nova.Systems;
using UnityEngine;

namespace Nova.Input
{
    // Conjunto de nodos seleccionados y lazo (13.1, 15.7). Solo entran orígenes legales:
    // nodos propios que pueden enviar, así que ningún Asteroide (5.2).
    //
    // El feedback de selección es arte (ART-WLD-SEL, T6-06); aquí solo se emite OnSelectionChanged.
    [DisallowMultipleComponent]
    public sealed class SelectionManager : MonoBehaviour
    {
        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance. Solo lo consulta el lazo.")]
        [SerializeField] NodeRegistry registry;

        readonly List<NodeController> selected = new List<NodeController>();
        readonly Dictionary<NodeController, Action<Faction, Faction>> handlers = new Dictionary<NodeController, Action<Faction, Faction>>();

        // Facción a la que pertenece la selección. Hoy solo selecciona el Jugador.
        public Faction Owner => Faction.Player;

        // En orden de selección.
        public IReadOnlyList<NodeController> Selected => selected;
        public bool HasSelection => selected.Count > 0;

        public event Action OnSelectionChanged;

        // Origen legal para la facción: suyo, inicializado y no Asteroide (5.2, 13.1).
        public static bool CanSelect(NodeController node, Faction owner)
        {
            return node != null
                && node.IsInitialized
                && owner != Faction.Neutral
                && node.Faction == owner
                && node.Type != NodeType.Asteroid;
        }

        public bool IsSelected(NodeController node) => node != null && handlers.ContainsKey(node);

        public bool Select(NodeController node)
        {
            if (!Add(node))
                return false;
            OnSelectionChanged?.Invoke();
            return true;
        }

        public bool Deselect(NodeController node)
        {
            if (!Remove(node))
                return false;
            OnSelectionChanged?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (!HasSelection)
                return;
            RemoveAll();
            OnSelectionChanged?.Invoke();
        }

        // Lazo (13.1): los nodos propios que pueden enviar con el centro dentro del rectángulo
        // de las dos esquinas, bordes incluidos. Sustituye a la selección activa (18 #26).
        public void SelectInRect(Vector2 cornerA, Vector2 cornerB)
        {
            NodeRegistry source = registry != null ? registry : NodeRegistry.Instance;
            if (source == null)
                throw new InvalidOperationException("SelectionManager necesita un NodeRegistry para el lazo.");

            Vector2 min = Vector2.Min(cornerA, cornerB);
            Vector2 max = Vector2.Max(cornerA, cornerB);

            RemoveAll();
            foreach (NodeController node in source.GetNodes(Owner))
            {
                Vector2 center = node.transform.position;
                if (center.x >= min.x && center.x <= max.x && center.y >= min.y && center.y <= max.y)
                    Add(node);
            }
            OnSelectionChanged?.Invoke();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(NodeRegistry nodeRegistry)
        {
            registry = nodeRegistry;
        }

        void OnDestroy()
        {
            RemoveAll();
        }

        bool Add(NodeController node)
        {
            if (!CanSelect(node, Owner) || handlers.ContainsKey(node))
                return false;

            // 18 #27: un nodo que deja de ser propio sale solo de la selección.
            Action<Faction, Faction> handler = (previous, current) =>
            {
                if (current != Owner)
                    Deselect(node);
            };
            handlers.Add(node, handler);
            node.OnFactionChanged += handler;
            selected.Add(node);
            return true;
        }

        bool Remove(NodeController node)
        {
            if (node == null || !handlers.TryGetValue(node, out Action<Faction, Faction> handler))
                return false;

            node.OnFactionChanged -= handler;
            handlers.Remove(node);
            selected.Remove(node);
            return true;
        }

        void RemoveAll()
        {
            foreach (KeyValuePair<NodeController, Action<Faction, Faction>> entry in handlers)
            {
                if (entry.Key != null)
                    entry.Key.OnFactionChanged -= entry.Value;
            }
            handlers.Clear();
            selected.Clear();
        }
    }
}
