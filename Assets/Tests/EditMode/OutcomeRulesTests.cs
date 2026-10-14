using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;

namespace Nova.Tests
{
    // T5-03: OutcomeRules (10.1, 10.2), sin Unity. Victoria, aniquilación y las cinco vías del
    // estancamiento por capacidad, con los casos 18 y 19 de la sección 17.
    public class OutcomeRulesTests
    {
        const int Ignition = 10;
        const float Resistance = 0.5f;

        static int Cap(NodeType type) => type == NodeType.Star ? 50 : type == NodeType.Asteroid ? 10 : 100;

        readonly List<OutcomeNode> nodes = new List<OutcomeNode>();

        [SetUp]
        public void SetUp()
        {
            nodes.Clear();
        }

        void Add(Faction faction, NodeType type, int energy, int level = 1, bool converting = false, bool charge = false,
                 int conversion = -1, int wormhole = -1)
        {
            var state = new NodeSnapshot(faction, type, energy, Cap(type), false, false,
                type == NodeType.Asteroid ? 0 : level, converting, charge);
            nodes.Add(new OutcomeNode(state, conversion, wormhole));
        }

        LevelOutcome Evaluate(bool protons = false) => OutcomeRules.Evaluate(nodes, protons, Ignition, Resistance);

        // --- Victoria y aniquilación ---

        [Test]
        public void WithBothSidesStandingTheLevelGoesOn()
        {
            Add(Faction.Player, NodeType.Star, 10);
            Add(Faction.Enemy, NodeType.Star, 10);
            Assert.AreEqual(LevelOutcome.None, Evaluate());
        }

        [Test]
        public void TheEnemyWithoutNodesIsAVictoryEvenWithNeutralsLeft()
        {
            Add(Faction.Player, NodeType.Star, 10);
            Add(Faction.Neutral, NodeType.Asteroid, 10);
            Add(Faction.Neutral, NodeType.Magnetar, 100, level: 3);
            Assert.AreEqual(LevelOutcome.Victory, Evaluate(), "Los neutrales los convierte la cascada (10.1).");
        }

        [Test]
        public void ThePlayerWithoutNodesIsAnnihilated()
        {
            Add(Faction.Enemy, NodeType.Star, 10);
            Add(Faction.Neutral, NodeType.Asteroid, 10);
            Assert.AreEqual(LevelOutcome.Annihilation, Evaluate(protons: true), "Con protones en vuelo o sin ellos (10.2, 1).");
        }

        // --- Estancamiento (10.2, 2) ---

        [Test]
        public void SixEnergyAndNoStarIsAStalemate()
        {
            // Caso 19.
            Add(Faction.Player, NodeType.Magnetar, 6);
            Add(Faction.Enemy, NodeType.Star, 10);
            Add(Faction.Neutral, NodeType.Asteroid, 10);
            Assert.AreEqual(LevelOutcome.Stalemate, Evaluate());
        }

        [Test]
        public void AStarIsEnoughEvenEmpty()
        {
            Add(Faction.Player, NodeType.Star, 0);
            Add(Faction.Enemy, NodeType.Star, 50);
            Assert.AreEqual(LevelOutcome.None, Evaluate(), "Genera: no está estancado.");
        }

        [Test]
        public void NothingIsCheckedWhileSomethingCanStillChangeTheCount()
        {
            Add(Faction.Player, NodeType.Magnetar, 6);
            Add(Faction.Enemy, NodeType.Star, 10);
            Assert.AreEqual(LevelOutcome.None, Evaluate(protons: true), "Protones propios en vuelo.");

            nodes.Clear();
            Add(Faction.Player, NodeType.Pulsar, 0, charge: true);
            Add(Faction.Enemy, NodeType.Star, 10);
            Assert.AreEqual(LevelOutcome.None, Evaluate(), "Un poder cargando cuenta como poder (10.2).");

            nodes.Clear();
            Add(Faction.Player, NodeType.Magnetar, 6, converting: true);
            Add(Faction.Enemy, NodeType.Star, 10);
            Assert.AreEqual(LevelOutcome.None, Evaluate(), "Un nodo reconvirtiéndose.");
        }

        [TestCase(4, 6, LevelOutcome.None)]
        [TestCase(4, 5, LevelOutcome.Stalemate)]
        [TestCase(10, 0, LevelOutcome.None)]
        public void AnOwnAsteroidCanBeFilledAndLit(int asteroid, int magnetar, LevelOutcome expected)
        {
            Add(Faction.Player, NodeType.Asteroid, asteroid);
            Add(Faction.Player, NodeType.Magnetar, magnetar);
            Add(Faction.Enemy, NodeType.Star, 50);
            Assert.AreEqual(expected, Evaluate());
        }

        [TestCase(20, LevelOutcome.None)]
        [TestCase(19, LevelOutcome.Stalemate)]
        public void ANeutralAsteroidCanBeCapturedAndLit(int energy, LevelOutcome expected)
        {
            // Caso 18 en los mapas 1 y 2: 10 para capturarlo y 10 para encenderlo.
            Add(Faction.Player, NodeType.Magnetar, energy);
            Add(Faction.Enemy, NodeType.Star, 50);
            Add(Faction.Neutral, NodeType.Asteroid, 10);
            Assert.AreEqual(expected, Evaluate());
        }

        [TestCase(15, LevelOutcome.None)]
        [TestCase(16, LevelOutcome.Stalemate)]
        public void AnEnemyStarCanBeCaptured(int starEnergy, LevelOutcome expected)
        {
            Add(Faction.Player, NodeType.Magnetar, 15);
            Add(Faction.Enemy, NodeType.Star, starEnergy);
            Assert.AreEqual(expected, Evaluate());
        }

        [Test]
        public void TheSumOfAllLitNodesCounts()
        {
            Add(Faction.Player, NodeType.Magnetar, 8);
            Add(Faction.Player, NodeType.Pulsar, 8);
            Add(Faction.Enemy, NodeType.Star, 16);
            Assert.AreEqual(LevelOutcome.None, Evaluate(), "Cuentas optimistas: 8 + 8 llegan a 16.");
        }

        [TestCase(30, 30, LevelOutcome.None)]
        [TestCase(30, 29, LevelOutcome.Stalemate)]
        [TestCase(-1, 80, LevelOutcome.Stalemate)]
        public void ANodeCanBeReconvertedWhereTheLevelAllowsIt(int cost, int energy, LevelOutcome expected)
        {
            // Caso 18 en el mapa 3, sin Asteroides ni Estrella que capturar.
            Add(Faction.Player, NodeType.Magnetar, energy, conversion: cost);
            Add(Faction.Enemy, NodeType.Magnetar, 100);
            Assert.AreEqual(expected, Evaluate());
        }

        // Con el balance de 16.3 esta vía no decide nunca: el Gusano cuesta 60 y una Estrella no pasa de
        // 50, así que quien lo paga ya puede capturarla. Se prueba con un coste hipotético de 20.
        [TestCase(20, LevelOutcome.None)]
        [TestCase(-1, LevelOutcome.Stalemate)]
        public void AWormholeCanStealAnEnemyStar(int wormhole, LevelOutcome expected)
        {
            Add(Faction.Player, NodeType.Pulsar, 20, level: 3, wormhole: wormhole);
            Add(Faction.Enemy, NodeType.Star, 50);
            Assert.AreEqual(expected, Evaluate(), "20 no captura una Estrella con 50; el Gusano se la lleva entera.");
        }

        [Test]
        public void AWormholeNeedsAnEnemyStarToSteal()
        {
            Add(Faction.Player, NodeType.Pulsar, 20, level: 3, wormhole: 20);
            Add(Faction.Enemy, NodeType.Magnetar, 100);
            Assert.AreEqual(LevelOutcome.Stalemate, Evaluate(), "Robar un Magnetar no da una Estrella.");
        }
    }
}
