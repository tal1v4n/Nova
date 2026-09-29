using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Systems
{
    // Registro vivo de todos los nodos, indexado por facción (15.6). Lo consultan el algoritmo,
    // la victoria y la barra de poder. Singleton de 15.1 #6. Los nodos los registra quien los crea
    // (LevelLoader en T5-01, la escena de prueba en T1-12), ya inicializados: Initialize no emite
    // OnFactionChanged y el índice se construye con la facción de partida.
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class NodeRegistry : MonoBehaviour
    {
        public static NodeRegistry Instance { get; private set; }

        static readonly Faction[] Factions = (Faction[])Enum.GetValues(typeof(Faction));

        readonly List<NodeController> all = new List<NodeController>();
        readonly Dictionary<Faction, List<NodeController>> byFaction = new Dictionary<Faction, List<NodeController>>();
        readonly Dictionary<NodeController, Action<Faction, Faction>> handlers = new Dictionary<NodeController, Action<Faction, Faction>>();

        // En orden de registro; al cambiar de bando, un nodo pasa al final de la lista de su nueva facción.
        public IReadOnlyList<NodeController> All => all;
        public int Count => all.Count;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Nova] Hay dos NodeRegistry en escena; se descarta el segundo.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            Clear();
            if (Instance == this)
                Instance = null;
        }

        public IReadOnlyList<NodeController> GetNodes(Faction faction) => ListFor(faction);

        public int CountOf(Faction faction) => ListFor(faction).Count;

        public bool Contains(NodeController node) => node != null && handlers.ContainsKey(node);

        public void Register(NodeController node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (!node.IsInitialized)
                throw new InvalidOperationException($"{node.name}: se registra después de Initialize.");
            if (handlers.ContainsKey(node))
                return;

            all.Add(node);
            ListFor(node.Faction).Add(node);

            Action<Faction, Faction> handler = (previous, current) => Move(node, previous, current);
            handlers.Add(node, handler);
            node.OnFactionChanged += handler;
        }

        public void Unregister(NodeController node)
        {
            if (node == null || !handlers.TryGetValue(node, out Action<Faction, Faction> handler))
                return;

            node.OnFactionChanged -= handler;
            handlers.Remove(node);
            all.Remove(node);
            ListFor(node.Faction).Remove(node);
        }

        public void Clear()
        {
            foreach (KeyValuePair<NodeController, Action<Faction, Faction>> entry in handlers)
            {
                if (entry.Key != null)
                    entry.Key.OnFactionChanged -= entry.Value;
            }
            handlers.Clear();
            all.Clear();
            byFaction.Clear();
        }

        void Move(NodeController node, Faction previous, Faction current)
        {
            ListFor(previous).Remove(node);
            ListFor(current).Add(node);
        }

        List<NodeController> ListFor(Faction faction)
        {
            if (!byFaction.TryGetValue(faction, out List<NodeController> list))
            {
                if (Array.IndexOf(Factions, faction) < 0)
                    throw new ArgumentOutOfRangeException(nameof(faction), faction, "Facción desconocida.");
                list = new List<NodeController>();
                byFaction.Add(faction, list);
            }
            return list;
        }
    }
}
