using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Systems
{
    // Instancia el mapa de un LevelDefinition (15.6, T5-01): un nodo por NodeSpawnData, en su
    // orden, a partir de la plantilla de nodo. Cada uno se inicializa y se registra en NodeRegistry,
    // y sus componentes reciben las mecánicas del nivel (11.1, 18 #36): los tipos que NodeIgnition
    // puede encender, los de NodeConverter y los poderes de PulsarCaster. Lo que el nivel no permite
    // queda fuera de sus listas, así que sale Unavailable, gris y sin coste (18 #29), para los dos
    // bandos: el Enemigo lo lee de los mismos componentes (EnemyBoard).
    //
    // Cargar otro nivel descarga el anterior. Va a -90, como PlacedNode: después de los registros
    // (-100), que fijan su Instance en Awake, y antes que cualquier otro script que lea los nodos.
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class LevelLoader : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;

        [Tooltip("Plantilla de nodo: NodeController con todos sus componentes y sin inicializar (Assets/Prefabs/Node.prefab).")]
        [SerializeField] NodeController nodePrefab;

        [Tooltip("Una NodeDefinition por tipo: Asteroide, Estrella, Púlsar y Magnetar.")]
        [SerializeField] NodeDefinition[] definitions = Array.Empty<NodeDefinition>();

        [Tooltip("Un PowerDefinition por poder (16.3).")]
        [SerializeField] PowerDefinition[] powers = Array.Empty<PowerDefinition>();

        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance.")]
        [SerializeField] NodeRegistry registry;

        [Tooltip("Opcional: el nivel que se carga al empezar. Para las escenas de prueba; en el juego lo pide GameManager (T5-05).")]
        [SerializeField] LevelDefinition startLevel;

        readonly List<NodeController> spawned = new List<NodeController>();

        // El nivel cargado, o null.
        public LevelDefinition Current { get; private set; }

        // Los nodos del nivel, en el orden de sus NodeSpawnData.
        public IReadOnlyList<NodeController> Nodes => spawned;

        // Al terminar de cargar un nivel, con sus nodos ya registrados.
        public event Action<LevelDefinition> OnLoaded;

        void Awake()
        {
            if (startLevel != null)
                Load(startLevel);
        }

        public void Load(LevelDefinition level)
        {
            if (level == null)
                throw new ArgumentNullException(nameof(level));
            if (nodePrefab == null)
                throw new InvalidOperationException("LevelLoader necesita la plantilla de nodo.");
            NodeRegistry nodes = Registry;
            if (nodes == null)
                throw new InvalidOperationException("LevelLoader necesita NodeRegistry.");

            // Todo se comprueba antes de crear nada: un nivel mal hecho no deja medio mapa.
            List<NodeDefinition> ignition = DefinitionsFor(level.IgnitionTypes);
            List<NodeDefinition> conversion = level.AllowsConversion ? ignition : new List<NodeDefinition>();
            List<PowerDefinition> allowedPowers = PowersFor(level.Powers);
            for (int i = 0; i < level.Spawns.Count; i++)
                Check(level, i);

            Unload();
            for (int i = 0; i < level.Spawns.Count; i++)
            {
                NodeSpawnData spawn = level.Spawns[i];
                NodeDefinition definition = Definition(spawn.Type);

                NodeController node = Instantiate(nodePrefab, spawn.Position, Quaternion.identity, transform);
                node.name = $"{spawn.Faction} {spawn.Type} {i + 1}";

                if (node.TryGetComponent(out NodeIgnition nodeIgnition))
                    nodeIgnition.Configure(Balance, ignition);
                if (node.TryGetComponent(out NodeConverter converter))
                    converter.Configure(Balance, conversion);
                if (node.TryGetComponent(out PulsarCaster caster))
                    caster.Configure(Balance, allowedPowers);

                if (spawn.Level > 0)
                    node.Initialize(definition, spawn.Faction, spawn.InitialEnergy, spawn.Level);
                else
                    node.Initialize(definition, spawn.Faction, spawn.InitialEnergy);
                nodes.Register(node);
                spawned.Add(node);
            }

            Current = level;
            OnLoaded?.Invoke(level);
        }

        // Quita del registro y destruye los nodos del nivel cargado.
        public void Unload()
        {
            NodeRegistry nodes = Registry;
            foreach (NodeController node in spawned)
            {
                if (node == null)
                    continue;
                if (nodes != null)
                    nodes.Unregister(node);
                if (Application.isPlaying)
                    Destroy(node.gameObject);
                else
                    DestroyImmediate(node.gameObject);
            }
            spawned.Clear();
            Current = null;
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config, NodeController template, IEnumerable<NodeDefinition> nodeDefinitions,
                              IEnumerable<PowerDefinition> powerDefinitions, NodeRegistry nodeRegistry = null, LevelDefinition level = null)
        {
            balance = config;
            nodePrefab = template;
            definitions = nodeDefinitions != null ? new List<NodeDefinition>(nodeDefinitions).ToArray() : Array.Empty<NodeDefinition>();
            powers = powerDefinitions != null ? new List<PowerDefinition>(powerDefinitions).ToArray() : Array.Empty<PowerDefinition>();
            registry = nodeRegistry;
            startLevel = level;
        }

        void Check(LevelDefinition level, int index)
        {
            NodeSpawnData spawn = level.Spawns[index];
            NodeDefinition definition = Definition(spawn.Type);
            if (spawn.InitialEnergy > definition.EnergyCap)
                throw new InvalidOperationException(
                    $"{level.name}, nodo {index + 1}: {spawn.InitialEnergy} de Energía no cabe en el Tope de {spawn.Type} ({definition.EnergyCap}).");
            if (spawn.Level > definition.MaxLevel)
                throw new InvalidOperationException(
                    $"{level.name}, nodo {index + 1}: {spawn.Type} no tiene nivel {spawn.Level} (máximo {definition.MaxLevel}).");
        }

        List<NodeDefinition> DefinitionsFor(IReadOnlyList<NodeType> types)
        {
            var result = new List<NodeDefinition>(types.Count);
            foreach (NodeType type in types)
                result.Add(Definition(type));
            return result;
        }

        List<PowerDefinition> PowersFor(IReadOnlyList<PowerType> types)
        {
            var result = new List<PowerDefinition>(types.Count);
            foreach (PowerType type in types)
            {
                PowerDefinition power = Array.Find(powers, p => p != null && p.Type == type);
                if (power == null)
                    throw new InvalidOperationException($"LevelLoader no tiene el PowerDefinition de {type}.");
                result.Add(power);
            }
            return result;
        }

        NodeDefinition Definition(NodeType type)
        {
            NodeDefinition definition = Array.Find(definitions, d => d != null && d.Type == type);
            if (definition == null)
                throw new InvalidOperationException($"LevelLoader no tiene la NodeDefinition de {type}.");
            return definition;
        }

        NodeRegistry Registry => registry != null ? registry : NodeRegistry.Instance;
        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("LevelLoader necesita BalanceConfig.");
    }
}
