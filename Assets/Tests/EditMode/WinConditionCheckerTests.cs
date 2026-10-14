using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Systems;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T5-03: WinConditionChecker (10, 15.6) sobre nodos reales: cuándo decide, la cascada de
    // conquista de 10.1 con sus 3 s, el caso 21 y el estancamiento leído de los componentes.
    public class WinConditionCheckerTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();
        NodeRegistry registry;
        ProtonRegistry protons;
        ProtonLauncher launcher;
        WinConditionChecker checker;
        readonly List<LevelOutcome> ended = new List<LevelOutcome>();

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        static readonly string[] LitTypes = { "Star", "Pulsar", "Magnetar" };

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            registry = go.AddComponent<NodeRegistry>();
            protons = go.AddComponent<ProtonRegistry>();
            var pool = go.AddComponent<ProtonPool>();
            launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, pool, protons);
            checker = go.AddComponent<WinConditionChecker>();
            checker.Configure(Balance, registry, protons);
            ended.Clear();
            checker.OnLevelEnded += ended.Add;
        }

        [TearDown]
        public void TearDown()
        {
            registry.Clear();
            foreach (GameObject go in spawned)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            spawned.Clear();
        }

        NodeController Node(string type, Faction faction, int energy, Vector2 position, int level = 0,
                            bool conversion = true, bool wormhole = true)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, type == "Asteroid" ? 0 : Mathf.Max(1, level));
            var lit = System.Array.ConvertAll(LitTypes, Definition);
            go.AddComponent<NodeConverter>().Configure(Balance, conversion ? lit : new NodeDefinition[0]);
            go.AddComponent<NodeFreeze>().Configure(Balance);
            go.AddComponent<NodeDecay>().Configure(Balance);
            var powers = new List<PowerDefinition> { PowerAsset("AbsoluteZero"), PowerAsset("Decay") };
            if (wormhole)
                powers.Add(PowerAsset("Wormhole"));
            go.AddComponent<PulsarCaster>().Configure(Balance, powers);
            registry.Register(node);
            return node;
        }

        void Run(float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Tick); i++)
                checker.Tick(Tick);
        }

        // --- Cuándo decide ---

        [Test]
        public void AnEmptyBoardIsNotAVictory()
        {
            checker.Tick(Tick);
            Assert.AreEqual(LevelOutcome.None, checker.Outcome, "Sin nivel cargado no hay nada que ganar.");
        }

        [Test]
        public void WhileBothSidesStandNothingHappens()
        {
            Node("Star", Faction.Player, 25, new Vector2(-5f, 0f));
            Node("Star", Faction.Enemy, 25, new Vector2(5f, 0f));
            Run(1f);
            Assert.AreEqual(LevelOutcome.None, checker.Outcome);
            Assert.IsEmpty(ended);
        }

        [Test]
        public void AnnihilationEndsTheLevelAtOnce()
        {
            NodeController player = Node("Star", Faction.Player, 25, new Vector2(-5f, 0f));
            Node("Star", Faction.Enemy, 25, new Vector2(5f, 0f));
            Node("Asteroid", Faction.Neutral, 10, new Vector2(0f, 2f));
            player.Steal(Faction.Enemy);

            checker.Tick(Tick);

            Assert.AreEqual(LevelOutcome.Annihilation, checker.Outcome);
            Assert.IsTrue(checker.IsOver);
            Assert.IsEmpty(checker.Cascade, "La cascada es solo de la victoria.");
            CollectionAssert.AreEqual(new[] { LevelOutcome.Annihilation }, ended);
        }

        [Test]
        public void OnceDecidedItStopsLookingUntilRestart()
        {
            NodeController player = Node("Star", Faction.Player, 25, new Vector2(-5f, 0f));
            NodeController enemy = Node("Star", Faction.Enemy, 25, new Vector2(5f, 0f));
            player.Steal(Faction.Enemy);
            checker.Tick(Tick);

            enemy.Steal(Faction.Player);
            Run(1f);
            Assert.AreEqual(LevelOutcome.Annihilation, checker.Outcome);
            Assert.AreEqual(1, ended.Count, "Un solo final por nivel.");

            checker.Restart();
            checker.Tick(Tick);
            Assert.AreEqual(LevelOutcome.None, checker.Outcome, "Tras Restart vuelve a mirar: los dos bandos tienen nodo.");
        }

        // --- Victoria y cascada (10.1) ---

        [Test]
        public void TheCascadeTakesEveryNeutralFromNearestToFarthestInThreeSeconds()
        {
            Node("Star", Faction.Player, 25, new Vector2(-5f, 0f));
            NodeController far = Node("Magnetar", Faction.Neutral, 100, new Vector2(4f, 0f), level: 3);
            NodeController near = Node("Asteroid", Faction.Neutral, 10, new Vector2(-3f, 0f));
            NodeController middle = Node("Asteroid", Faction.Neutral, 10, new Vector2(0f, 0f));

            checker.Tick(Tick);

            Assert.AreEqual(LevelOutcome.Victory, checker.Outcome);
            Assert.IsTrue(checker.IsCascading);
            CollectionAssert.AreEqual(new[] { near, middle, far }, checker.Cascade);
            Assert.IsEmpty(ended, "El nivel termina con el último nodo de la cascada.");

            Run(1f);
            Assert.AreEqual(Faction.Player, near.Faction, "El primero, a 1 s de 3.");
            Assert.AreEqual(Faction.Neutral, middle.Faction);

            Run(2f);
            Assert.AreEqual(Faction.Player, middle.Faction);
            Assert.AreEqual(Faction.Player, far.Faction, "El Magnetar neutral también (T.7 #44).");
            Assert.AreEqual(NodeType.Magnetar, far.Type);
            Assert.AreEqual(3, far.Level);
            Assert.AreEqual(100, far.Energy, "Conserva tipo, nivel y Energía.");
            Assert.IsTrue(checker.IsOver);
            CollectionAssert.AreEqual(new[] { LevelOutcome.Victory }, ended);
        }

        [Test]
        public void AFrozenOrInfectedNeutralIsConqueredClean()
        {
            Node("Star", Faction.Player, 25, new Vector2(-5f, 0f));
            NodeController wall = Node("Magnetar", Faction.Neutral, 100, new Vector2(0f, 0f));
            wall.Freeze();
            wall.Infect();

            checker.Tick(Tick);
            Run(Balance.CascadeDuration);

            Assert.AreEqual(Faction.Player, wall.Faction);
            Assert.IsFalse(wall.HasState(NodeStateFlags.Frozen));
            Assert.IsFalse(wall.HasState(NodeStateFlags.Infected));
        }

        [Test]
        public void WithoutNeutralsTheVictoryIsImmediate()
        {
            Node("Star", Faction.Player, 25, new Vector2(-5f, 0f));
            checker.Tick(Tick);
            Assert.IsTrue(checker.IsOver);
            CollectionAssert.AreEqual(new[] { LevelOutcome.Victory }, ended);
        }

        [Test]
        public void TheEnemyProtonsInFlightVanishAtTheVictory()
        {
            // Caso 21.
            NodeController player = Node("Star", Faction.Player, 25, new Vector2(-5f, 0f));
            NodeController enemy = Node("Star", Faction.Enemy, 40, new Vector2(5f, 0f));
            Assert.AreEqual(LaunchBlock.None, launcher.TryLaunch(enemy, player, Faction.Enemy, false, out Proton hostile));
            Assert.AreEqual(LaunchBlock.None, launcher.TryLaunch(player, enemy, Faction.Player, false, out Proton own));
            bool vanished = false;
            hostile.OnVanished += _ => vanished = true;

            enemy.Steal(Faction.Player);
            checker.Tick(Tick);

            Assert.AreEqual(LevelOutcome.Victory, checker.Outcome);
            Assert.IsTrue(vanished);
            Assert.IsFalse(hostile.InFlight, "Desaparece sin impactar.");
            Assert.AreEqual(13, player.Energy, "25 − 12 enviados; los 20 del Enemigo nunca llegan.");
            Assert.IsTrue(own.InFlight, "Los del Jugador siguen su curso.");
            Assert.AreEqual(1, protons.Count);
        }

        // --- Estancamiento (10.2), leído de los componentes ---

        [Test]
        public void SixEnergyWithoutAStarIsAStalemate()
        {
            Node("Magnetar", Faction.Player, 6, new Vector2(-5f, 0f));
            Node("Star", Faction.Enemy, 25, new Vector2(5f, 0f));
            Node("Asteroid", Faction.Neutral, 10, new Vector2(0f, 2f));

            checker.Tick(Tick);

            Assert.AreEqual(LevelOutcome.Stalemate, checker.Outcome);
            CollectionAssert.AreEqual(new[] { LevelOutcome.Stalemate }, ended);
        }

        [Test]
        public void AProtonOfThePlayerInFlightPostponesTheStalemate()
        {
            NodeController player = Node("Magnetar", Faction.Player, 6, new Vector2(-5f, 0f));
            Node("Star", Faction.Enemy, 25, new Vector2(5f, 0f));
            NodeController rock = Node("Asteroid", Faction.Neutral, 10, new Vector2(-3f, 0f));
            Assert.AreEqual(LaunchBlock.None, launcher.TryLaunch(player, rock, Faction.Player, false, out _));

            checker.Tick(Tick);
            Assert.AreEqual(LevelOutcome.None, checker.Outcome);
        }

        [TestCase(true, LevelOutcome.None)]
        [TestCase(false, LevelOutcome.Stalemate)]
        public void ReconvertingCountsOnlyWhereTheLevelAllowsIt(bool allowed, LevelOutcome expected)
        {
            // Caso 18: 30 en un Magnetar, sin Asteroides ni Estrella que capturar.
            Node("Magnetar", Faction.Player, 30, new Vector2(-5f, 0f), conversion: allowed);
            Node("Magnetar", Faction.Enemy, 100, new Vector2(5f, 0f));

            checker.Tick(Tick);
            Assert.AreEqual(expected, checker.Outcome);
        }

        [Test]
        public void ACaptureInTheLastTickIsSeenInTheNext()
        {
            // 15.10 y caso 20: el impacto se resuelve tras los FixedUpdate, y +20 lo ve después.
            NodeController player = Node("Star", Faction.Player, 50, new Vector2(-1f, 0f));
            NodeController enemy = Node("Star", Faction.Enemy, 5, new Vector2(1f, 0f));
            Assert.AreEqual(LaunchBlock.None, launcher.TryLaunch(player, enemy, Faction.Player, false, out Proton proton));

            checker.Tick(Tick);
            Assert.AreEqual(LevelOutcome.None, checker.Outcome);

            proton.Impact();
            Assert.AreEqual(Faction.Player, enemy.Faction);
            checker.Tick(Tick);
            Assert.AreEqual(LevelOutcome.Victory, checker.Outcome);
        }
    }
}
