using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T1-07: NodeLevelComponent y NodeStateMachine con los estados de base de 14.1.
    public class NodeLevelAndStateTests
    {
        GameObject go;
        NodeController node;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Node");
            node = go.AddComponent<NodeController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ControllerBringsLevelAndStateMachine()
        {
            Assert.IsNotNull(go.GetComponent<NodeLevelComponent>());
            Assert.IsNotNull(go.GetComponent<NodeStateMachine>());
        }

        // --- NodeLevelComponent ---

        [TestCase("Asteroid", 0)]
        [TestCase("Star", 1)]
        [TestCase("Pulsar", 1)]
        [TestCase("Magnetar", 1)]
        public void DefaultStartingLevel(string type, int expected)
        {
            node.Initialize(Definition(type), Faction.Player, 0);
            Assert.AreEqual(expected, node.Level);
            Assert.AreEqual(expected, node.Snapshot().Level);
        }

        [Test]
        public void ExplicitStartingLevel()
        {
            node.Initialize(Definition("Magnetar"), Faction.Neutral, 100, 5);
            Assert.AreEqual(5, node.Level);
            Assert.IsTrue(go.GetComponent<NodeLevelComponent>().IsMaxLevel);
        }

        [TestCase("Asteroid", 1)]
        [TestCase("Star", 0)]
        [TestCase("Star", 6)]
        [TestCase("Pulsar", 4)]
        public void StartingLevelOutOfRangeIsRejected(string type, int level)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => node.Initialize(Definition(type), Faction.Player, 0, level));
        }

        [Test]
        public void CurrentLevelDataFollowsTheLevel()
        {
            NodeDefinition star = Definition("Star");
            node.Initialize(star, Faction.Player, 0, 3);
            var level = go.GetComponent<NodeLevelComponent>();

            Assert.AreEqual(star.GetLevel(3).GenerationInterval, level.CurrentLevelData.GenerationInterval);
            level.Apply(star, 4);
            Assert.AreEqual(star.GetLevel(4).GenerationInterval, level.CurrentLevelData.GenerationInterval);
        }

        [Test]
        public void AsteroidHasNoLevelData()
        {
            node.Initialize(Definition("Asteroid"), Faction.Neutral, 10);
            Assert.Throws<System.InvalidOperationException>(() => _ = go.GetComponent<NodeLevelComponent>().CurrentLevelData);
        }

        // 16.2: Estrella 20/30/40/50, Púlsar 25/40.
        [TestCase("Star", 1, 20)]
        [TestCase("Star", 4, 50)]
        [TestCase("Pulsar", 2, 40)]
        [TestCase("Magnetar", 2, 30)]
        public void NextUpgradeCostComesFromTheDefinition(string type, int level, int expected)
        {
            node.Initialize(Definition(type), Faction.Player, 0, level);
            Assert.AreEqual(expected, go.GetComponent<NodeLevelComponent>().NextUpgradeCost);
        }

        [Test]
        public void NextUpgradeCostAtMaxLevelIsZero()
        {
            node.Initialize(Definition("Pulsar"), Faction.Player, 0, 3);
            Assert.AreEqual(0, go.GetComponent<NodeLevelComponent>().NextUpgradeCost);
        }

        [Test]
        public void ApplyRaisesPreviousAndNew()
        {
            NodeDefinition star = Definition("Star");
            node.Initialize(star, Faction.Player, 0);
            var seen = new List<(int, int)>();
            node.OnLevelChanged += (a, b) => seen.Add((a, b));

            go.GetComponent<NodeLevelComponent>().Apply(star, 2);

            CollectionAssert.AreEqual(new[] { (1, 2) }, seen);
        }

        // Fin de reconversión (8.1): nivel 1 de otro tipo. El nivel no cambia, el evento no salta,
        // pero los datos del nivel ya son los del tipo nuevo.
        [Test]
        public void ApplyingTheSameLevelIsSilentButSwapsTheDefinition()
        {
            node.Initialize(Definition("Star"), Faction.Player, 0);
            int events = 0;
            node.OnLevelChanged += (_, __) => events++;

            NodeDefinition magnetar = Definition("Magnetar");
            var level = go.GetComponent<NodeLevelComponent>();
            level.Apply(magnetar, 1);

            Assert.AreEqual(0, events);
            Assert.AreEqual(magnetar.GetLevel(1).Radius, level.CurrentLevelData.Radius);
            Assert.AreEqual(magnetar.MaxLevel, level.MaxLevel);
        }

        [Test]
        public void InitializeRaisesNoLevelOrStateEvents()
        {
            int events = 0;
            node.OnLevelChanged += (_, __) => events++;
            node.OnStateChanged += (_, __) => events++;
            node.Initialize(Definition("Star"), Faction.Player, 0, 3);
            Assert.AreEqual(0, events);
        }

        // --- Validación de mejora ---

        [Test]
        public void UpgradeIsAllowedWithEnoughEnergy()
        {
            node.Initialize(Definition("Star"), Faction.Player, 20);
            ActionCheck check = node.CheckUpgrade(Faction.Player);
            Assert.IsTrue(check.IsAllowed);
            Assert.AreEqual(20, check.Cost);
        }

        [Test]
        public void UpgradeWithoutEnoughEnergyReportsTheCost()
        {
            node.Initialize(Definition("Star"), Faction.Player, 19);
            ActionCheck check = node.CheckUpgrade(Faction.Player);
            Assert.AreEqual(ActionBlock.InsufficientEnergy, check.Block);
            Assert.AreEqual(20, check.Cost);
        }

        [Test]
        public void UpgradeAtMaxLevelIsBlocked()
        {
            node.Initialize(Definition("Pulsar"), Faction.Player, 100, 3);
            Assert.AreEqual(ActionBlock.MaxLevel, node.CheckUpgrade(Faction.Player).Block);
        }

        [Test]
        public void AsteroidCannotBeUpgraded()
        {
            node.Initialize(Definition("Asteroid"), Faction.Player, 10);
            Assert.AreEqual(ActionBlock.IsAsteroid, node.CheckUpgrade(Faction.Player).Block);
        }

        // Caso 23 de la sección 17: sobre un nodo enemigo no hay mejora.
        [Test]
        public void OnlyTheOwnerCanUpgrade()
        {
            node.Initialize(Definition("Star"), Faction.Enemy, 50);
            Assert.AreEqual(ActionBlock.NotOwner, node.CheckUpgrade(Faction.Player).Block);
        }

        // --- NodeStateMachine ---

        [TestCase(NodeType.Asteroid, Faction.Neutral, NodeState.Sleeping)]
        [TestCase(NodeType.Asteroid, Faction.Player, NodeState.Unignited)]
        [TestCase(NodeType.Asteroid, Faction.Enemy, NodeState.Unignited)]
        [TestCase(NodeType.Star, Faction.Player, NodeState.Normal)]
        [TestCase(NodeType.Pulsar, Faction.Enemy, NodeState.Normal)]
        [TestCase(NodeType.Magnetar, Faction.Neutral, NodeState.Normal)]
        public void BaseStateFollows14_1(NodeType type, Faction faction, NodeState expected)
        {
            Assert.AreEqual(expected, NodeStateMachine.BaseState(type, faction));
        }

        [Test]
        public void InitializeSetsTheBaseState()
        {
            node.Initialize(Definition("Asteroid"), Faction.Neutral, 10);
            Assert.AreEqual(NodeState.Sleeping, node.State);
        }

        // Capturar un Asteroide dormido lo despierta: Dormido termina «cuando es capturado».
        [Test]
        public void CapturingASleepingAsteroidMakesItUnignited()
        {
            node.Initialize(Definition("Asteroid"), Faction.Neutral, 10);
            var seen = new List<(NodeState, NodeState)>();
            node.OnStateChanged += (a, b) => seen.Add((a, b));

            go.GetComponent<NodeOwnership>().ChangeFaction(Faction.Player);

            Assert.AreEqual(NodeState.Unignited, node.State);
            CollectionAssert.AreEqual(new[] { (NodeState.Sleeping, NodeState.Unignited) }, seen);
        }

        // Pasar de un dueño a otro no cambia el estado de base: no hay evento.
        [Test]
        public void ChangingOwnerOfAnOwnedAsteroidIsSilent()
        {
            node.Initialize(Definition("Asteroid"), Faction.Player, 4);
            int events = 0;
            node.OnStateChanged += (_, __) => events++;

            go.GetComponent<NodeOwnership>().ChangeFaction(Faction.Enemy);

            Assert.AreEqual(NodeState.Unignited, node.State);
            Assert.AreEqual(0, events);
        }

        [Test]
        public void CapturingALitNodeKeepsItNormal()
        {
            node.Initialize(Definition("Magnetar"), Faction.Neutral, 0);
            go.GetComponent<NodeOwnership>().ChangeFaction(Faction.Enemy);
            Assert.AreEqual(NodeState.Normal, node.State);
        }

        // --- Banderas de estado (T.7 #43, T2-07) ---

        [Test]
        public void FlagsStartEmptyAndAddToTheBaseState()
        {
            node.Initialize(Definition("Star"), Faction.Player, 10);
            Assert.AreEqual(NodeStateFlags.None, node.StateFlags);

            Assert.IsTrue(node.SetStateFlag(NodeStateFlags.Scared, true));
            Assert.IsTrue(node.SetStateFlag(NodeStateFlags.Frozen, true));

            Assert.AreEqual(NodeState.Normal, node.State, "La base no cambia con las banderas.");
            Assert.IsTrue(node.HasState(NodeStateFlags.Scared));
            Assert.IsTrue(node.HasState(NodeStateFlags.Frozen));
            Assert.IsFalse(node.HasState(NodeStateFlags.Infected));
            Assert.IsTrue(node.Snapshot().IsFrozen, "Las reglas de Core leen las banderas.");
        }

        [Test]
        public void FlagEventsFireOnlyOnRealChanges()
        {
            node.Initialize(Definition("Star"), Faction.Player, 10);
            var seen = new List<(NodeStateFlags, NodeStateFlags)>();
            node.OnStateFlagsChanged += (a, b) => seen.Add((a, b));

            node.SetStateFlag(NodeStateFlags.Converting, true);
            node.SetStateFlag(NodeStateFlags.Converting, true);
            node.SetStateFlag(NodeStateFlags.Converting, false);
            node.SetStateFlag(NodeStateFlags.Converting, false);

            CollectionAssert.AreEqual(new[]
            {
                (NodeStateFlags.None, NodeStateFlags.Converting),
                (NodeStateFlags.Converting, NodeStateFlags.None),
            }, seen);
        }

        // 14.6: Infectado y Reconvirtiendo es la única pareja prohibida, en los dos sentidos.
        [Test]
        public void InfectedAndConvertingCannotCoexist()
        {
            node.Initialize(Definition("Star"), Faction.Player, 10);

            Assert.IsTrue(node.SetStateFlag(NodeStateFlags.Converting, true));
            Assert.IsFalse(node.SetStateFlag(NodeStateFlags.Infected, true));
            Assert.AreEqual(NodeStateFlags.Converting, node.StateFlags);

            node.SetStateFlag(NodeStateFlags.Converting, false);
            Assert.IsTrue(node.SetStateFlag(NodeStateFlags.Infected, true));
            Assert.IsFalse(node.SetStateFlag(NodeStateFlags.Converting, true));
        }

        // 14.6: las otras cinco parejas de la matriz sí conviven.
        [TestCase(NodeStateFlags.Scared, NodeStateFlags.Frozen)]
        [TestCase(NodeStateFlags.Scared, NodeStateFlags.Infected)]
        [TestCase(NodeStateFlags.Scared, NodeStateFlags.Converting)]
        [TestCase(NodeStateFlags.Frozen, NodeStateFlags.Infected)]
        [TestCase(NodeStateFlags.Frozen, NodeStateFlags.Converting)]
        public void CompatiblePairsFrom14_6(NodeStateFlags first, NodeStateFlags second)
        {
            Assert.IsTrue(NodeStateMachine.CanCoexist(first, second));
            Assert.IsTrue(NodeStateMachine.CanCoexist(second, first));
        }

        [Test]
        public void OneFlagPerCall()
        {
            node.Initialize(Definition("Star"), Faction.Player, 10);
            Assert.Throws<System.ArgumentException>(() => node.SetStateFlag(NodeStateFlags.Scared | NodeStateFlags.Frozen, true));
            Assert.Throws<System.ArgumentException>(() => node.SetStateFlag(NodeStateFlags.None, true));
        }

        [Test]
        public void InitializeClearsTheFlags()
        {
            node.Initialize(Definition("Star"), Faction.Player, 10);
            node.SetStateFlag(NodeStateFlags.Frozen, true);

            node.Initialize(Definition("Star"), Faction.Player, 10);

            Assert.AreEqual(NodeStateFlags.None, node.StateFlags);
        }

        // Encender (T1-11b) cambia el tipo y reevalúa: SinEncender → Normal.
        [Test]
        public void EvaluateAfterIgnitionGoesNormal()
        {
            node.Initialize(Definition("Asteroid"), Faction.Player, 10);
            var seen = new List<(NodeState, NodeState)>();
            node.OnStateChanged += (a, b) => seen.Add((a, b));

            go.GetComponent<NodeStateMachine>().Evaluate(NodeType.Star, Faction.Player);

            CollectionAssert.AreEqual(new[] { (NodeState.Unignited, NodeState.Normal) }, seen);
        }
    }
}
