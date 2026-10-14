using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Systems
{
    // Fin del nivel (10, 15.6, T5-03). Cada tick lee el tablero de NodeRegistry y pregunta a
    // OutcomeRules si alguien ha ganado:
    //   - Victoria (10.1): el Enemigo no posee ningún nodo. Sus protones en vuelo se desvanecen
    //     (17, caso 21) y empieza la cascada de conquista: cada nodo neutral pasa al Jugador, del más
    //     cercano a sus nodos al más lejano, repartidos en CascadeDuration. El nivel termina con el
    //     último.
    //   - Aniquilación y estancamiento (10.2): el nivel termina en el acto.
    //
    // Va a +20 (15.10): ve los impactos del tick anterior, que ProtonRegistry resuelve después de
    // todos los FixedUpdate. Decidido el resultado, deja de mirar hasta Restart.
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    public sealed class WinConditionChecker : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        [SerializeField] BalanceConfig balance;

        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance.")]
        [SerializeField] NodeRegistry nodes;

        [Tooltip("Opcional: sin él se usa ProtonRegistry.Instance. Sin ninguno, no hay protones que mirar.")]
        [SerializeField] ProtonRegistry protons;

        readonly List<OutcomeNode> board = new List<OutcomeNode>();
        readonly List<NodeController> cascade = new List<NodeController>();
        long cascadeMicros;
        long cascadeElapsed;
        int conquered;
        bool ended;

        public LevelOutcome Outcome { get; private set; }

        // Los nodos neutrales de la cascada, en su orden, y cuántos han pasado ya al Jugador.
        public IReadOnlyList<NodeController> Cascade => cascade;
        public int Conquered => conquered;

        public bool IsCascading => Outcome == LevelOutcome.Victory && !ended;
        public bool IsOver => ended;

        // Al decidirse el resultado: con la victoria, al empezar la cascada.
        public event Action<LevelOutcome> OnOutcome;

        // Al terminar el nivel: la derrota en el acto, la victoria con el último nodo de la cascada.
        // Lo atiende GameManager con EndLevelScreen (T5-05).
        public event Action<LevelOutcome> OnLevelEnded;

        // Cada nodo que la cascada pasa al Jugador, para la presentación (FX-CASCADE, 14.5).
        public event Action<NodeController> OnConquered;

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            if (Outcome == LevelOutcome.None)
                Check();
            else if (IsCascading)
                Advance((long)Math.Round(deltaTime * MicrosPerSecond));
        }

        // Para volver a empezar un nivel (T5-05): olvida el resultado y la cascada.
        public void Restart()
        {
            Outcome = LevelOutcome.None;
            cascade.Clear();
            cascadeElapsed = 0;
            conquered = 0;
            ended = false;
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config, NodeRegistry nodeRegistry = null, ProtonRegistry protonRegistry = null)
        {
            balance = config;
            nodes = nodeRegistry;
            protons = protonRegistry;
            Restart();
        }

        void Check()
        {
            NodeRegistry registry = Nodes;
            // Sin nodos no hay nivel cargado, y un tablero vacío no es una victoria.
            if (registry == null || registry.Count == 0)
                return;

            board.Clear();
            foreach (NodeController node in registry.All)
            {
                if (node != null && node.IsInitialized)
                    board.Add(new OutcomeNode(node.Snapshot(), StarConversionCost(node), WormholeCost(node)));
            }

            LevelOutcome outcome = OutcomeRules.Evaluate(board, PlayerProtonsInFlight(), Balance.IgnitionCost, Balance.MagnetarResistance);
            if (outcome == LevelOutcome.None)
                return;

            Outcome = outcome;
            if (outcome == LevelOutcome.Victory)
            {
                VanishEnemyProtons();
                BuildCascade(registry);
            }
            OnOutcome?.Invoke(outcome);

            if (outcome != LevelOutcome.Victory || cascade.Count == 0)
                End();
        }

        // 10.1: del más cercano a algún nodo del Jugador al más lejano; con empate, el orden de
        // NodeRegistry.
        void BuildCascade(NodeRegistry registry)
        {
            cascade.Clear();
            var distances = new Dictionary<NodeController, float>();
            foreach (NodeController node in registry.All)
            {
                if (node == null || !node.IsInitialized || node.Faction != Faction.Neutral)
                    continue;
                float nearest = float.PositiveInfinity;
                foreach (NodeController own in registry.GetNodes(Faction.Player))
                    nearest = Mathf.Min(nearest, Vector2.Distance(node.transform.position, own.transform.position));
                distances.Add(node, nearest);
                cascade.Add(node);
            }

            // Ordenación estable: List.Sort no lo es.
            var order = new List<NodeController>(cascade);
            cascade.Sort((a, b) =>
            {
                int byDistance = distances[a].CompareTo(distances[b]);
                return byDistance != 0 ? byDistance : order.IndexOf(a).CompareTo(order.IndexOf(b));
            });

            cascadeMicros = (long)Math.Round(Balance.CascadeDuration * MicrosPerSecond);
            cascadeElapsed = 0;
            conquered = 0;
        }

        // El nodo i pasa al Jugador al cumplirse (i + 1) / n de la duración: el último, al final.
        void Advance(long micros)
        {
            cascadeElapsed += micros;
            int count = cascade.Count;
            while (conquered < count && cascadeElapsed * count >= cascadeMicros * (conquered + 1))
            {
                NodeController node = cascade[conquered++];
                // Si el Jugador lo tomó por su cuenta mientras tanto, ya es suyo.
                if (node == null || node.Faction != Faction.Neutral)
                    continue;
                // Conserva tipo, nivel y Energía, como con el Agujero de Gusano (10.1).
                node.Steal(Faction.Player);
                OnConquered?.Invoke(node);
            }
            if (conquered >= count)
                End();
        }

        void End()
        {
            ended = true;
            Debug.Log($"[Nova] Fin del nivel: {Outcome}.");
            OnLevelEnded?.Invoke(Outcome);
        }

        void VanishEnemyProtons()
        {
            ProtonRegistry registry = Protons;
            if (registry == null)
                return;
            foreach (Proton proton in new List<Proton>(registry.InFlight))
            {
                if (proton != null && proton.InFlight && proton.Faction == Faction.Enemy)
                    proton.Vanish();
            }
        }

        bool PlayerProtonsInFlight()
        {
            ProtonRegistry registry = Protons;
            if (registry == null)
                return false;
            foreach (Proton proton in registry.InFlight)
            {
                if (proton != null && proton.InFlight && proton.Faction == Faction.Player)
                    return true;
            }
            return false;
        }

        // Lo que el nodo ofrece según el nivel (11.1): lo dejó LevelLoader en sus componentes (T5-01).
        static int StarConversionCost(NodeController node)
        {
            if (node.Type == NodeType.Asteroid || node.Type == NodeType.Star)
                return -1;
            return node.TryGetComponent(out NodeConverter converter) && converter.Offers(NodeType.Star)
                ? node.Definition.ConversionCost
                : -1;
        }

        static int WormholeCost(NodeController node)
        {
            if (node.Type != NodeType.Pulsar || !node.TryGetComponent(out PulsarCaster caster))
                return -1;
            PowerDefinition wormhole = caster.DefinitionOf(PowerType.Wormhole);
            return wormhole != null && node.Level >= wormhole.RequiredLevel ? wormhole.Cost : -1;
        }

        NodeRegistry Nodes => nodes != null ? nodes : NodeRegistry.Instance;
        ProtonRegistry Protons => protons != null ? protons : ProtonRegistry.Instance;
        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("WinConditionChecker necesita BalanceConfig.");
    }
}
