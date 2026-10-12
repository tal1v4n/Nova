using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Enemy;
using Nova.Gameplay;
using Nova.Systems;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T4-04: EnemyController (12.1, 12.7). El ciclo de decisión sobre nodos reales: la foto del
    // tablero, una acción como máximo por ciclo, el umbral y el ruido con semilla.
    public class EnemyControllerTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();
        NodeRegistry registry;
        PowerBeamSystem beams;
        EnemyCommandIssuer issuer;
        EnemyController controller;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static PowerDefinition[] Powers => new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") };

        static EnemyProfile Profile(string name) =>
            AssetDatabase.LoadAssetAtPath<EnemyProfile>($"Assets/Data/EnemyProfiles/{name}.asset");

        static EnemyScoring Scoring => AssetDatabase.LoadAssetAtPath<EnemyScoring>("Assets/Data/EnemyScoring.asset");

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            Build("Map3", 7);
        }

        [TearDown]
        public void TearDown()
        {
            Clear();
        }

        void Build(string profile, int seed)
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            registry = go.AddComponent<NodeRegistry>();
            beams = go.AddComponent<PowerBeamSystem>();
            var pool = go.AddComponent<ProtonPool>();
            var launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, pool);
            issuer = go.AddComponent<EnemyCommandIssuer>();
            issuer.Configure(launcher);
            controller = go.AddComponent<EnemyController>();
            controller.Configure(Profile(profile), Scoring, Balance, issuer, Powers, registry, seed);
        }

        void Clear()
        {
            if (registry != null)
                registry.Clear();
            foreach (GameObject go in spawned)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            spawned.Clear();
        }

        NodeController Node(string type, Faction faction, int energy, Vector2 position, int level = 1)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, type == "Asteroid" ? 0 : level);
            var types = new[] { Definition("Star"), Definition("Pulsar"), Definition("Magnetar") };
            go.AddComponent<NodeIgnition>().Configure(Balance, types);
            go.AddComponent<NodeConverter>().Configure(Balance, types);
            go.AddComponent<NodeFreeze>().Configure(Balance);
            go.AddComponent<PulsarCaster>().Configure(Balance, Powers, beams);
            registry.Register(node);
            return node;
        }

        void Run(int ticks)
        {
            for (int i = 0; i < ticks; i++)
                controller.Tick(Tick);
        }

        // Un tablero con varias candidatas: dos Estrellas propias, un Asteroide propio y objetivos.
        void Skirmish()
        {
            Node("Star", Faction.Enemy, 40, new Vector2(5f, 0f));
            Node("Star", Faction.Enemy, 30, new Vector2(5f, 2f));
            Node("Asteroid", Faction.Enemy, 10, new Vector2(3f, -2f));
            Node("Star", Faction.Player, 12, new Vector2(-5f, 0f));
            Node("Asteroid", Faction.Neutral, 10, new Vector2(0f, 2f));
            Node("Magnetar", Faction.Neutral, 30, new Vector2(0f, -3f));
        }

        // --- El reloj (12.1) ---

        [Test]
        public void TheFirstCycleComesAfterOneInterval()
        {
            Skirmish();
            int ticks = Mathf.RoundToInt(Profile("Map3").DecisionInterval / Tick);

            Run(ticks - 1);
            Assert.AreEqual(0, controller.Cycles, "No decide en el tick 0.");
            Run(1);
            Assert.AreEqual(1, controller.Cycles);
            Run(ticks);
            Assert.AreEqual(2, controller.Cycles);
        }

        [Test]
        public void EachMapThinksAtItsOwnPace()
        {
            foreach (string map in new[] { "Map1", "Map2" })
            {
                Clear();
                Build(map, 0);
                Skirmish();
                Run(Mathf.RoundToInt(Profile(map).DecisionInterval / Tick) * 3);
                Assert.AreEqual(3, controller.Cycles, map);
            }
        }

        [Test]
        public void ALongTickStillDecidesOnlyOnce()
        {
            Skirmish();
            controller.Tick(10f);
            Assert.AreEqual(1, controller.Cycles, "12.7: una acción por ciclo, también si se acumulan intervalos.");
        }

        // --- Una acción por ciclo (12.7) ---

        [Test]
        public void ACycleRunsExactlyOneAction()
        {
            Skirmish();
            var before = new List<(int energy, NodeType type, int level)>();
            foreach (NodeController node in registry.All)
                before.Add((node.Energy, node.Type, node.Level));

            EnemyAction? chosen = controller.Decide();

            Assert.IsTrue(chosen.HasValue);
            Assert.IsTrue(controller.LastExecuted);
            int changed = 0;
            for (int i = 0; i < registry.All.Count; i++)
            {
                NodeController node = registry.All[i];
                if ((node.Energy, node.Type, node.Level) != before[i])
                    changed++;
            }
            Assert.AreEqual(1, changed, $"Solo cambia el nodo que actuó: {chosen}");
        }

        [Test]
        public void ItExecutesWhatTheEvaluatorPicks()
        {
            Skirmish();
            EnemyBoard board = EnemyBoard.Capture(Faction.Enemy, registry.All, Balance, Powers);
            EnemyAction? expected = EnemyEvaluator.Pick(EnemyEvaluator.Candidates(board, Profile("Map3"), Scoring),
                Profile("Map3"), new System.Random(7));

            EnemyAction? chosen = controller.Decide();

            Assert.AreEqual(expected.Value.Kind, chosen.Value.Kind);
            Assert.AreEqual(expected.Value.Actor, chosen.Value.Actor);
            Assert.AreEqual(expected.Value.Target, chosen.Value.Target);
            Assert.AreEqual(expected.Value.Score, chosen.Value.Score);
        }

        [Test]
        public void BelowTheThresholdItWaits()
        {
            Node("Star", Faction.Enemy, 0, new Vector2(5f, 0f));
            Node("Star", Faction.Player, 50, new Vector2(-5f, 0f));

            EnemyAction? chosen = controller.Decide();

            Assert.IsFalse(chosen.HasValue, "Sin Energía no hay candidatas: acumula.");
            Assert.IsFalse(controller.LastExecuted);
            Assert.AreEqual(1, controller.Cycles);
        }

        [Test]
        public void ItNeverCommandsThePlayersNodes()
        {
            NodeController player = Node("Star", Faction.Player, 50, new Vector2(-5f, 0f), level: 2);
            Node("Star", Faction.Enemy, 50, new Vector2(5f, 0f));

            for (int i = 0; i < 20; i++)
                controller.Decide();

            Assert.AreEqual(Faction.Player, player.Faction);
            Assert.AreEqual(2, player.Level);
            Assert.LessOrEqual(player.Energy, 50, "Nunca sube por un comando del Enemigo.");
        }

        // --- Determinismo (12.7) ---

        List<string> Play(int seed, int cycles)
        {
            Clear();
            Build("Map1", seed);
            Skirmish();
            var log = new List<string>();
            for (int i = 0; i < cycles; i++)
            {
                EnemyAction? a = controller.Decide();
                log.Add(a.HasValue ? $"{a.Value.Kind} {a.Value.Actor}->{a.Value.Target} {a.Value.NodeType}/{a.Value.Power} {a.Value.FullSend}" : "-");
            }
            return log;
        }

        [Test]
        public void TheSameSeedPlaysTheSameGame()
        {
            CollectionAssert.AreEqual(Play(3, 6), Play(3, 6));
        }

        [Test]
        public void DifferentSeedsCanPlayDifferently()
        {
            string first = Play(0, 1)[0];
            bool differs = false;
            for (int seed = 1; seed < 60 && !differs; seed++)
                differs = Play(seed, 1)[0] != first;
            Assert.IsTrue(differs, "Con ±25 % de ruido, alguna semilla elige otra cosa en el primer ciclo.");
        }

        [Test]
        public void ReseedRestartsTheNoise()
        {
            Skirmish();
            controller.Reseed(11);
            EnemyBoard board = EnemyBoard.Capture(Faction.Enemy, registry.All, Balance, Powers);
            EnemyAction? expected = EnemyEvaluator.Pick(EnemyEvaluator.Candidates(board, Profile("Map3"), Scoring),
                Profile("Map3"), new System.Random(11));

            Assert.AreEqual(expected.Value.Score, controller.Decide().Value.Score);
        }

        // --- La foto del tablero (12.1, paso 1) ---

        [Test]
        public void TheBoardReadsWhatTheFormulasNeed()
        {
            NodeController magnetar = Node("Magnetar", Faction.Player, 40, new Vector2(1f, 2f), level: 3);
            NodeController star = Node("Star", Faction.Enemy, 20, new Vector2(4f, 0f), level: 5);
            NodeController pulsar = Node("Pulsar", Faction.Enemy, 80, new Vector2(4f, 2f), level: 2);
            pulsar.GetComponent<PulsarCaster>().TryCharge(Faction.Enemy, PowerType.Decay);

            EnemyBoard board = EnemyBoard.Capture(Faction.Enemy, registry.All, Balance, Powers);

            Assert.AreEqual(Faction.Enemy, board.Self);
            Assert.AreEqual(Faction.Player, board.Opponent);
            BoardNode m = board.Nodes[0], s = board.Nodes[1], p = board.Nodes[2];
            Assert.AreEqual(magnetar.CurrentLevelData.Radius, m.MagnetarRadius);
            Assert.AreEqual(new Vector2(1f, 2f), m.Position);
            Assert.AreEqual(40, m.UpgradeCost, "Magnetar 3 → 4.");
            Assert.AreEqual(30, m.ConversionCost);
            Assert.AreEqual(-1, s.UpgradeCost, "La Estrella 5 está al máximo.");
            Assert.AreEqual(0f, s.MagnetarRadius);
            Assert.AreEqual(PowerType.Decay, p.ChargingPower);
            Assert.IsNull(p.ReadyPower);
            Assert.IsTrue(p.State.HasPowerCharge);
            Assert.AreEqual(star.Energy, s.Energy);
        }

        [Test]
        public void UninitialisedNodesStayOutButKeepTheIndices()
        {
            Node("Star", Faction.Enemy, 20, new Vector2(4f, 0f));
            var go = new GameObject("Uninitialised");
            spawned.Add(go);
            var raw = go.AddComponent<NodeController>();
            var list = new List<NodeController> { registry.All[0], raw, registry.All[0] };

            EnemyBoard board = EnemyBoard.Capture(Faction.Enemy, list, Balance, Powers);

            Assert.AreEqual(2, board.Nodes.Count);
            Assert.AreEqual(0, board.Nodes[0].Index);
            Assert.AreEqual(2, board.Nodes[1].Index, "El índice es el de la lista, que es el que usa Execute.");
        }
    }
}
