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

        // --- T5-05: las cinco vías, con su motivo, para la pantalla de derrota (13.11, 18 #41) ---

        StarRoute[] Routes(bool conversion = true, bool wormhole = true) =>
            OutcomeRules.StarRoutes(nodes, Ignition, Resistance, conversion, wormhole);

        static int Capture(int energy) => EnergyMath.CaptureCost(energy, 1f);

        [Test]
        public void TheRoutesComeInTheOrderOfTheTable()
        {
            Add(Faction.Player, NodeType.Magnetar, 5);
            Add(Faction.Enemy, NodeType.Star, 30);
            StarRoute[] routes = Routes();
            Assert.AreEqual(OutcomeRules.RouteCount, routes.Length);
            for (int i = 0; i < routes.Length; i++)
                Assert.AreEqual((StarRouteKind)i, routes[i].Kind);
        }

        [Test]
        public void EachRouteSaysWhyItFallsShort()
        {
            // S = 5 + 3 = 8, sin Asteroide propio ni Púlsar.
            Add(Faction.Player, NodeType.Magnetar, 5, conversion: 20);
            Add(Faction.Player, NodeType.Magnetar, 3, conversion: 20);
            Add(Faction.Neutral, NodeType.Asteroid, 10);
            Add(Faction.Neutral, NodeType.Asteroid, 7);
            Add(Faction.Enemy, NodeType.Star, 30);
            Add(Faction.Enemy, NodeType.Star, 12);
            StarRoute[] routes = Routes();

            Assert.AreEqual(StarRouteBlock.NoneOwned, routes[0].Block, "No tiene Asteroide propio.");

            Assert.AreEqual(StarRouteBlock.Short, routes[1].Block);
            Assert.AreEqual(Capture(7) + Ignition, routes[1].Cost, "El Asteroide más barato, más encenderlo.");
            Assert.AreEqual(8, routes[1].Have, "S");

            Assert.AreEqual(StarRouteBlock.Short, routes[2].Block);
            Assert.AreEqual(Capture(12), routes[2].Cost, "La Estrella más barata.");
            Assert.AreEqual(Capture(12) - 8, routes[2].Missing);

            Assert.AreEqual(StarRouteBlock.Short, routes[3].Block);
            Assert.AreEqual(20, routes[3].Cost);
            Assert.AreEqual(5, routes[3].Have, "Reconvertir es con la Energía del propio nodo: el que menos le falta.");
            Assert.AreEqual(15, routes[3].Missing);

            Assert.AreEqual(StarRouteBlock.NoneOwned, routes[4].Block, "Sin Púlsar que cargue el Gusano.");
        }

        [Test]
        public void AMechanicTheLevelLacksDoesNotExistInTheMap()
        {
            Add(Faction.Player, NodeType.Pulsar, 5, level: 3, conversion: 20, wormhole: 60);
            Add(Faction.Enemy, NodeType.Star, 30);
            StarRoute[] routes = Routes(conversion: false, wormhole: false);
            Assert.AreEqual(StarRouteBlock.NotInMap, routes[3].Block);
            Assert.AreEqual(StarRouteBlock.NotInMap, routes[4].Block);
        }

        [Test]
        public void WithoutTargetsNoneIsLeft()
        {
            Add(Faction.Player, NodeType.Pulsar, 5, level: 3, wormhole: 60);
            Add(Faction.Enemy, NodeType.Magnetar, 30);
            StarRoute[] routes = Routes();
            Assert.AreEqual(StarRouteBlock.NoneLeft, routes[1].Block, "No queda Asteroide que capturar.");
            Assert.AreEqual(StarRouteBlock.NoneLeft, routes[2].Block, "No queda Estrella que capturar.");
            Assert.AreEqual(StarRouteBlock.NoneLeft, routes[4].Block, "Tiene el Gusano, pero no hay Estrella del Enemigo.");
        }

        [Test]
        public void IgnitingAnOwnAsteroidCountsItsEnergyAndS()
        {
            Add(Faction.Player, NodeType.Asteroid, 3);
            Add(Faction.Player, NodeType.Magnetar, 4);
            Add(Faction.Enemy, NodeType.Star, 50);
            StarRoute route = Routes()[0];
            Assert.AreEqual(StarRouteBlock.Short, route.Block);
            Assert.AreEqual(Ignition, route.Cost);
            Assert.AreEqual(7, route.Have);
            Assert.AreEqual(3, route.Missing);
        }

        // Con las mecánicas permitidas, una vía alcanza si y solo si CanRecoverStar lo dice: la
        // pantalla enseña las mismas cuentas que declararon el estancamiento.
        [TestCase(0, 2, 0, -1, -1)]
        [TestCase(6, 2, 0, -1, -1)]
        [TestCase(2, 0, 40, -1, -1)]
        [TestCase(2, 0, 9, -1, -1)]
        [TestCase(25, 0, 0, 20, -1)]
        [TestCase(15, 0, 0, 20, -1)]
        [TestCase(30, 0, 0, -1, 20)]
        [TestCase(10, 3, 12, 30, 60)]
        public void TheRoutesAgreeWithCanRecoverStar(int energy, int ownAsteroid, int enemyStar, int conversion, int wormhole)
        {
            Add(Faction.Player, wormhole >= 0 ? NodeType.Pulsar : NodeType.Magnetar, energy, level: 3, conversion: conversion, wormhole: wormhole);
            if (ownAsteroid > 0)
                Add(Faction.Player, NodeType.Asteroid, ownAsteroid);
            Add(Faction.Neutral, NodeType.Asteroid, 10);
            if (enemyStar > 0)
                Add(Faction.Enemy, NodeType.Star, enemyStar);
            Add(Faction.Enemy, NodeType.Magnetar, 100);

            bool any = System.Array.Exists(Routes(), r => r.Reachable);
            Assert.AreEqual(OutcomeRules.CanRecoverStar(nodes, Ignition, Resistance), any);
        }
    }
}
