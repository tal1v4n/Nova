using System;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Systems
{
    // T1-12: inicializa y registra un nodo colocado a mano en una escena de prueba. Es el mínimo
    // que hace falta para jugar sin LevelLoader, que en T5-01 lo sustituye leyendo NodeSpawnData.
    //
    // Va a -90: después de los registros (-100), que fijan su Instance en Awake, y antes que
    // cualquier otro script que lea el nodo.
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class PlacedNode : MonoBehaviour
    {
        [SerializeField] NodeDefinition definition;
        [SerializeField] Faction faction;
        [SerializeField, Min(0)] int initialEnergy;

        [Tooltip("0 = el de partida del tipo: 0 el Asteroide, 1 el resto.")]
        [SerializeField, Min(0)] int initialLevel;

        public NodeDefinition Definition => definition;
        public Faction Faction => faction;
        public int InitialEnergy => initialEnergy;

        void Awake()
        {
            Place(NodeRegistry.Instance);
        }

        // Inicializa el nodo y lo registra. Público para los tests, que no ejecutan Awake.
        public NodeController Place(NodeRegistry registry)
        {
            if (definition == null)
                throw new InvalidOperationException($"{name}: PlacedNode sin NodeDefinition.");

            var node = GetComponent<NodeController>();
            if (initialLevel > 0)
                node.Initialize(definition, faction, initialEnergy, initialLevel);
            else
                node.Initialize(definition, faction, initialEnergy);

            if (registry != null)
                registry.Register(node);
            else
                Debug.LogError($"[Nova] {name}: no hay NodeRegistry en escena; el nodo no cuenta para el lazo.", this);
            return node;
        }

        // Para el generador de la escena y los tests: el cableado normal es por el Inspector.
        public void Configure(NodeDefinition nodeDefinition, Faction nodeFaction, int energy, int level = 0)
        {
            definition = nodeDefinition;
            faction = nodeFaction;
            initialEnergy = energy;
            initialLevel = level;
        }
    }
}
