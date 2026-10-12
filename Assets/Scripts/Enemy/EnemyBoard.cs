using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Enemy
{
    // Un protón en vuelo hacia un nodo, visto por el algoritmo (12.4): lo que lee ThreatCalculator más
    // el índice del nodo del que salió, para congelar el origen del ataque. -1 si no se sabe.
    public readonly struct BoardProton
    {
        public readonly IncomingProton Proton;
        public readonly int SourceIndex;

        public BoardProton(IncomingProton proton, int sourceIndex)
        {
            Proton = proton;
            SourceIndex = sourceIndex;
        }
    }

    // Un nodo tal como lo ve el algoritmo (12.1, paso 1): lo que dicen las reglas de Core más lo que
    // hace falta para puntuar —posición, radio del Magnetar, costes y el estado de la carga del
    // Púlsar—. Datos sin comportamiento: EnemyEvaluator no toca nada de Unity salvo Vector2.
    //
    // 12.6: no lleva protones que no se han lanzado, y la carga del Púlsar es lo que enseña su barra,
    // que se ve en los dos bandos (7.5).
    public sealed class BoardNode
    {
        public BoardNode(int index, NodeSnapshot state, Vector2 position, float magnetarRadius = 0f,
                         int upgradeCost = -1, int conversionCost = 0,
                         PowerType? chargingPower = null, PowerType? readyPower = null, bool powerUpMarked = false,
                         IReadOnlyList<BoardProton> incoming = null)
        {
            Index = index;
            Incoming = incoming ?? Array.Empty<BoardProton>();
            State = state;
            Position = position;
            MagnetarRadius = magnetarRadius;
            UpgradeCost = upgradeCost;
            ConversionCost = conversionCost;
            ChargingPower = chargingPower;
            ReadyPower = readyPower;
            PowerUpMarked = powerUpMarked;
        }

        // Orden de NodeRegistry: el desempate de 12.3.3.
        public int Index { get; }
        public NodeSnapshot State { get; }
        public Vector2 Position { get; }

        // Radio de disparo si es Magnetar (4.4); 0 si no.
        public float MagnetarRadius { get; }

        // Coste del nivel siguiente (16.2); -1 al máximo.
        public int UpgradeCost { get; }

        // Coste de reconvertirlo (16.4).
        public int ConversionCost { get; }

        // Púlsar cargando ese poder, o con ese poder listo (7.1).
        public PowerType? ChargingPower { get; }
        public PowerType? ReadyPower { get; }

        // Asteroide neutral con un power-up encima (9); hasta T5-04, nunca.
        public bool PowerUpMarked { get; }

        // Protones en vuelo hacia este nodo, de cualquier bando (12.4, T4-05).
        public IReadOnlyList<BoardProton> Incoming { get; }

        // Lo que pide ThreatCalculator: los mismos protones sin el origen.
        public List<IncomingProton> IncomingProtons()
        {
            var list = new List<IncomingProton>(Incoming.Count);
            foreach (BoardProton proton in Incoming)
                list.Add(proton.Proton);
            return list;
        }

        public Faction Faction => State.Faction;
        public NodeType Type => State.Type;
        public int Energy => State.Energy;
        public bool IsLit => State.Type != NodeType.Asteroid;

        // Lo que el algoritmo ve de un nodo de la partida (T4-04). El power-up llega con T5-04; los
        // protones que le llegan los pone EnemyBoard.Capture (T4-05).
        public static BoardNode From(int index, NodeController node, IReadOnlyList<BoardProton> incoming = null)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            NodeDefinition definition = node.Definition;
            bool lit = node.Type != NodeType.Asteroid;
            float radius = node.Type == NodeType.Magnetar ? node.CurrentLevelData.Radius : 0f;
            int upgradeCost = lit && node.Level < definition.MaxLevel ? definition.UpgradeCostTo(node.Level + 1) : -1;

            PowerType? charging = null;
            PowerType? ready = null;
            if (node.TryGetComponent(out PulsarCaster caster) && caster.Power != null)
            {
                if (caster.State == PowerChargeState.Ready)
                    ready = caster.Power.Type;
                else
                    charging = caster.Power.Type;
            }

            return new BoardNode(index, node.Snapshot(), node.transform.position, radius, upgradeCost,
                definition.ConversionCost, charging, ready, false, incoming);
        }
    }

    // El tablero de un ciclo de decisión (12.1, paso 1): todos los nodos en el orden de NodeRegistry,
    // el bando que decide y los datos globales que piden las fórmulas. Lo construye EnemyController
    // (T4-04) a partir de NodeRegistry; los tests, a mano.
    public sealed class EnemyBoard
    {
        public EnemyBoard(Faction self, IReadOnlyList<BoardNode> nodes, float magnetarResistance, int ignitionCost,
                          IReadOnlyList<PowerDefinition> powers, float protonSpeed = 0f)
        {
            if (self == Faction.Neutral)
                throw new ArgumentException("Neutral no decide.", nameof(self));
            Self = self;
            Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            MagnetarResistance = magnetarResistance;
            IgnitionCost = ignitionCost;
            Powers = powers ?? Array.Empty<PowerDefinition>();
            ProtonSpeed = protonSpeed;
        }

        public Faction Self { get; }

        // El rival: el Jugador cuando decide el Enemigo.
        public Faction Opponent => Self == Faction.Enemy ? Faction.Player : Faction.Enemy;

        public IReadOnlyList<BoardNode> Nodes { get; }
        public float MagnetarResistance { get; }
        public int IgnitionCost { get; }

        // Los tres PowerDefinition, para coste y nivel requerido (16.3).
        public IReadOnlyList<PowerDefinition> Powers { get; }

        // u/s (6.1): convierte distancias en tiempos de llegada para la defensa (12.4).
        public float ProtonSpeed { get; }

        // El BoardNode con ese índice de NodeRegistry, o null.
        public BoardNode Find(int index)
        {
            foreach (BoardNode node in Nodes)
            {
                if (node.Index == index)
                    return node;
            }
            return null;
        }

        // El tablero de la partida tal como está ahora (12.1, paso 1): un BoardNode por nodo
        // inicializado, con el índice de su posición en nodes, que es el que usa Execute. Con un
        // ProtonRegistry, cada nodo lleva los protones en vuelo hacia él y de qué nodo salieron (T4-05).
        public static EnemyBoard Capture(Faction self, IReadOnlyList<NodeController> nodes, BalanceConfig balance,
                                         IReadOnlyList<PowerDefinition> powers, Systems.ProtonRegistry protons = null)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var indices = new Dictionary<NodeController, int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && !indices.ContainsKey(nodes[i]))
                    indices.Add(nodes[i], i);
            }

            var board = new List<BoardNode>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].IsInitialized)
                    board.Add(BoardNode.From(i, nodes[i], IncomingTo(nodes[i], protons, indices)));
            }
            return new EnemyBoard(self, board, balance.MagnetarResistance, balance.IgnitionCost, powers, balance.ProtonSpeed);
        }

        // 12.6: solo protones ya lanzados, los que están en el registro. La distancia, al centro del nodo.
        static IReadOnlyList<BoardProton> IncomingTo(NodeController node, Systems.ProtonRegistry protons,
                                                    Dictionary<NodeController, int> indices)
        {
            if (protons == null)
                return null;
            IReadOnlyList<Systems.Proton> list = protons.GetIncoming(node);
            if (list.Count == 0)
                return null;

            var result = new List<BoardProton>(list.Count);
            Vector2 center = node.transform.position;
            foreach (Systems.Proton proton in list)
            {
                if (proton == null || !proton.InFlight)
                    continue;
                float distance = Vector2.Distance(proton.transform.position, center);
                int source = proton.Source != null && indices.TryGetValue(proton.Source, out int s) ? s : -1;
                result.Add(new BoardProton(new IncomingProton(proton.Faction, proton.Charge, distance), source));
            }
            return result;
        }

        public PowerDefinition Power(PowerType type)
        {
            foreach (PowerDefinition power in Powers)
            {
                if (power != null && power.Type == type)
                    return power;
            }
            return null;
        }
    }
}
