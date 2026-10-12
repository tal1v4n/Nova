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
    // T4-05: EnemyThreatResponder (12.4) y el test 5 de 12.7: reforzar, congelar el origen del ataque
    // y evacuar, en ese orden, y la defensa dentro del ciclo de EnemyController.
    public class EnemyThreatResponderTests
    {
        static EnemyProfile Profile(string name) =>
            AssetDatabase.LoadAssetAtPath<EnemyProfile>($"Assets/Data/EnemyProfiles/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static EnemyProfile Map1 => Profile("Map1");
        static EnemyProfile Map2 => Profile("Map2");

        static int Cap(NodeType type) => type == NodeType.Star ? 50 : type == NodeType.Asteroid ? 10 : 100;

        readonly List<BoardNode> nodes = new List<BoardNode>();

        [SetUp]
        public void SetUp()
        {
            nodes.Clear();
        }

        BoardNode Add(Faction faction, NodeType type, int energy, Vector2 position, bool frozen = false,
                      PowerType? ready = null, BoardProton[] incoming = null)
        {
            var state = new NodeSnapshot(faction, type, energy, Cap(type), frozen, false,
                type == NodeType.Asteroid ? 0 : 1, false, ready.HasValue);
            var node = new BoardNode(nodes.Count, state, position, type == NodeType.Magnetar ? 1.5f : 0f, -1, 20,
                null, ready, false, incoming);
            nodes.Add(node);
            return node;
        }

        // Un protón a esa distancia del nodo; a 2.5 u/s, 1 u son 0.4 s.
        static BoardProton Hostile(int charge, float distance, int source = -1, Faction faction = Faction.Player) =>
            new BoardProton(new IncomingProton(faction, charge, distance), source);

        EnemyBoard Board() => new EnemyBoard(Faction.Enemy, nodes, Balance.MagnetarResistance, Balance.IgnitionCost,
            new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") }, Balance.ProtonSpeed);

        // --- Amenaza (12.4) ---

        [Test]
        public void ANodeIsThreatenedWhenTheHostileChargeReachesItsEnergy()
        {
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, incoming: new[] { Hostile(10, 5f) });
            Add(Faction.Enemy, NodeType.Star, 10, new Vector2(0f, 3f), incoming: new[] { Hostile(9, 5f) });
            Add(Faction.Enemy, NodeType.Star, 10, new Vector2(0f, -3f), incoming: new[] { Hostile(30, 5f, faction: Faction.Enemy) });

            List<EnemyThreatResponder.Threat> threats = EnemyThreatResponder.Threats(Board());

            Assert.AreEqual(1, threats.Count, "Los propios no amenazan y 9 no llega a 10.");
            Assert.AreEqual(0, threats[0].Node.Index);
            Assert.AreEqual(2f, threats[0].Assessment.TimeToCapture, 1e-5f);
        }

        [Test]
        public void TheThreatThatCapturesFirstGoesFirst()
        {
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, incoming: new[] { Hostile(10, 5f) });
            Add(Faction.Enemy, NodeType.Star, 10, new Vector2(0f, 3f), incoming: new[] { Hostile(10, 1f) });

            List<EnemyThreatResponder.Threat> threats = EnemyThreatResponder.Threats(Board());

            Assert.AreEqual(1, threats[0].Node.Index);
            Assert.AreEqual(0, threats[1].Node.Index);
        }

        [Test]
        public void WithoutThreatsThereIsNoDefence()
        {
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero);
            Add(Faction.Player, NodeType.Star, 10, new Vector2(5f, 0f), incoming: new[] { Hostile(50, 1f, faction: Faction.Enemy) });
            Assert.IsNull(EnemyThreatResponder.Respond(Board(), Map2));
        }

        // --- 1. Reforzar ---

        [Test]
        public void ItReinforcesFromANodeThatArrivesInTimeWithEnough()
        {
            BoardNode threatened = Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, incoming: new[] { Hostile(10, 5f) }); // 2 s
            BoardNode donor = Add(Faction.Enemy, NodeType.Star, 40, new Vector2(2.5f, 0f));                          // 1 s

            EnemyAction action = EnemyThreatResponder.Respond(Board(), Map2).Value;

            Assert.AreEqual(EnemyActionKind.Reinforce, action.Kind);
            Assert.AreEqual(donor.Index, action.Actor);
            Assert.AreEqual(threatened.Index, action.Target);
        }

        [Test]
        public void TheReinforcementMustCoverTheGap()
        {
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, incoming: new[] { Hostile(30, 5f) });
            Add(Faction.Enemy, NodeType.Star, 40, new Vector2(1f, 0f));      // 20 < 30 − 10 + 1
            BoardNode rich = Add(Faction.Enemy, NodeType.Star, 50, new Vector2(2f, 0f)); // 25 ≥ 21
            EnemyBoard board = Board();

            Assert.AreEqual(21, EnemyThreatResponder.ReinforcementNeeded(EnemyThreatResponder.Threats(board)[0]));
            Assert.AreEqual(rich.Index, EnemyThreatResponder.Respond(board, Map2).Value.Actor);
        }

        [Test]
        public void TheDonorThatArrivesFirstWinsAndThenTheRichest()
        {
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, incoming: new[] { Hostile(10, 5f) });
            Add(Faction.Enemy, NodeType.Star, 50, new Vector2(3f, 0f));
            BoardNode near = Add(Faction.Enemy, NodeType.Star, 20, new Vector2(1f, 0f));
            BoardNode nearRich = Add(Faction.Enemy, NodeType.Star, 30, new Vector2(-1f, 0f));

            Assert.AreEqual(nearRich.Index, EnemyThreatResponder.Respond(Board(), Map2).Value.Actor,
                "A la misma distancia, el que más tiene.");
            Assert.IsNotNull(near);
        }

        [Test]
        public void AFrozenNodeIsNotReinforced()
        {
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, frozen: true, incoming: new[] { Hostile(10, 5f) });
            Add(Faction.Enemy, NodeType.Star, 40, new Vector2(1f, 0f));

            Assert.IsNull(EnemyThreatResponder.Respond(Board(), Map2),
                "Su propia Energía lo drenaría (7.3), y congelado tampoco puede evacuar.");
        }

        // --- 2. Congelar el origen ---

        [Test]
        public void WithAbsoluteZeroReadyItFreezesTheSourceOfTheAttack()
        {
            BoardNode attacker = Add(Faction.Player, NodeType.Star, 30, new Vector2(-6f, 0f));
            BoardNode small = Add(Faction.Player, NodeType.Star, 30, new Vector2(-6f, 3f));
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, incoming: new[] { Hostile(8, 1f, small.Index), Hostile(15, 3f, attacker.Index) });
            BoardNode pulsar = Add(Faction.Enemy, NodeType.Pulsar, 50, new Vector2(4f, 0f), ready: PowerType.AbsoluteZero);
            Add(Faction.Enemy, NodeType.Star, 30, new Vector2(8f, 0f)); // donante, pero no llega a tiempo

            EnemyAction action = EnemyThreatResponder.Respond(Board(), Map2).Value;

            Assert.AreEqual(EnemyActionKind.LaunchPower, action.Kind);
            Assert.AreEqual(PowerType.AbsoluteZero, action.Power);
            Assert.AreEqual(pulsar.Index, action.Actor);
            Assert.AreEqual(attacker.Index, action.Target, "El origen del golpe más fuerte.");
        }

        [Test]
        public void ReinforcingGoesBeforeFreezing()
        {
            BoardNode attacker = Add(Faction.Player, NodeType.Star, 30, new Vector2(-6f, 0f));
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, incoming: new[] { Hostile(10, 5f, attacker.Index) });
            Add(Faction.Enemy, NodeType.Pulsar, 50, new Vector2(1f, 0f), ready: PowerType.AbsoluteZero);

            Assert.AreEqual(EnemyActionKind.Reinforce, EnemyThreatResponder.Respond(Board(), Map2).Value.Kind,
                "El Púlsar con 50 también puede reforzar, y llega a tiempo.");
        }

        [Test]
        public void AnotherReadyPowerDoesNotFreeze()
        {
            BoardNode attacker = Add(Faction.Player, NodeType.Star, 30, new Vector2(-6f, 0f));
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, incoming: new[] { Hostile(10, 1f, attacker.Index) });
            Add(Faction.Enemy, NodeType.Pulsar, 0, new Vector2(4f, 0f), ready: PowerType.Decay);

            Assert.IsNull(EnemyThreatResponder.Respond(Board(), Map1), "Solo Cero Absoluto, y el mapa 1 no evacúa.");
        }

        [Test]
        public void ASourceThatIsAlreadyOursIsNotFrozen()
        {
            BoardNode taken = Add(Faction.Enemy, NodeType.Star, 30, new Vector2(-6f, 0f));
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, incoming: new[] { Hostile(10, 1f, taken.Index) });
            Add(Faction.Enemy, NodeType.Pulsar, 0, new Vector2(4f, 0f), ready: PowerType.AbsoluteZero);

            EnemyAction? action = EnemyThreatResponder.Respond(Board(), Map1);
            Assert.IsNull(action, "7.2.1: el origen ya es del Enemigo, y el mapa 1 no evacúa.");
        }

        // --- 3. Evacuar (test 5 de 12.7) ---

        [Test]
        public void ItEvacuatesInsteadOfReinforcingWhenTheReinforcementWouldArriveLate()
        {
            BoardNode threatened = Add(Faction.Enemy, NodeType.Star, 40, Vector2.zero, incoming: new[] { Hostile(45, 2.5f) }); // 1 s
            Add(Faction.Enemy, NodeType.Star, 50, new Vector2(6f, 0f));      // 2.4 s: tarde
            BoardNode shelter = Add(Faction.Enemy, NodeType.Magnetar, 10, new Vector2(4f, 0f));

            EnemyAction action = EnemyThreatResponder.Respond(Board(), Map2).Value;

            Assert.AreEqual(EnemyActionKind.Evacuate, action.Kind);
            Assert.AreEqual(threatened.Index, action.Actor);
            Assert.AreEqual(shelter.Index, action.Target, "El nodo propio no amenazado más cercano.");
            Assert.IsTrue(action.FullSend);
        }

        [Test]
        public void WithoutEvacuationInTheProfileItDoesNothing()
        {
            Add(Faction.Enemy, NodeType.Star, 40, Vector2.zero, incoming: new[] { Hostile(45, 2.5f) });
            Add(Faction.Enemy, NodeType.Star, 50, new Vector2(6f, 0f));

            Assert.IsNull(EnemyThreatResponder.Respond(Board(), Map1));
        }

        [Test]
        public void ItDoesNotEvacuateIntoAnotherThreatenedNode()
        {
            Add(Faction.Enemy, NodeType.Star, 40, Vector2.zero, incoming: new[] { Hostile(45, 2.5f) });
            Add(Faction.Enemy, NodeType.Star, 5, new Vector2(1f, 0f), incoming: new[] { Hostile(5, 4f) });
            BoardNode safe = Add(Faction.Enemy, NodeType.Star, 5, new Vector2(7f, 0f));

            EnemyAction action = EnemyThreatResponder.Respond(Board(), Map2).Value;

            Assert.AreEqual(EnemyActionKind.Evacuate, action.Kind);
            Assert.AreEqual(safe.Index, action.Target);
        }

        // --- Dentro del ciclo (12.1, paso 2) ---

        [Test]
        public void TheControllerDefendsBeforeScoring()
        {
            var spawned = new List<GameObject>();
            try
            {
                var go = new GameObject("Systems");
                spawned.Add(go);
                var registry = go.AddComponent<NodeRegistry>();
                var protons = go.AddComponent<ProtonRegistry>();
                var beams = go.AddComponent<PowerBeamSystem>();
                var pool = go.AddComponent<ProtonPool>();
                var launcher = go.AddComponent<ProtonLauncher>();
                launcher.Configure(Balance, pool, protons);
                var issuer = go.AddComponent<EnemyCommandIssuer>();
                issuer.Configure(launcher);
                var controller = go.AddComponent<EnemyController>();
                controller.Configure(Map2, AssetDatabase.LoadAssetAtPath<EnemyScoring>("Assets/Data/EnemyScoring.asset"),
                    Balance, issuer, new[] { PowerAsset("AbsoluteZero") }, registry, 0, protons);

                NodeController Node(string type, Faction faction, int energy, Vector2 position)
                {
                    var n = new GameObject(type);
                    spawned.Add(n);
                    n.transform.position = position;
                    var node = n.AddComponent<NodeController>();
                    node.Initialize(AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{type}.asset"), faction, energy);
                    registry.Register(node);
                    return node;
                }

                NodeController player = Node("Star", Faction.Player, 50, new Vector2(-6f, 0f));
                NodeController target = Node("Star", Faction.Enemy, 10, new Vector2(4f, 0f));
                NodeController donor = Node("Star", Faction.Enemy, 40, new Vector2(5f, 0f));
                Assert.AreEqual(LaunchBlock.None, launcher.TryLaunch(player, target, Faction.Player, false, out Proton proton));
                Assert.AreSame(player, proton.Source, "El protón sabe de qué nodo salió.");

                EnemyAction? action = controller.Decide();

                Assert.IsTrue(controller.LastWasDefense);
                Assert.IsTrue(controller.LastExecuted);
                Assert.AreEqual(EnemyActionKind.Reinforce, action.Value.Kind);
                Assert.AreEqual(20, donor.Energy, "El donante envió su 50 %.");
                registry.Clear();
            }
            finally
            {
                foreach (GameObject go in spawned)
                {
                    if (go != null)
                        Object.DestroyImmediate(go);
                }
            }
        }
    }
}
