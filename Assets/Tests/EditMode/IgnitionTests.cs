using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Systems;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T1-11b: NodeIgnition, NodeController.ChangeType y el comando Ignite (4.1, 13.7, 16.1).
    public class IgnitionTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();
        PlayerCommandIssuer issuer;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Commands");
            spawned.Add(go);
            var launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, go.AddComponent<ProtonPool>());
            issuer = go.AddComponent<PlayerCommandIssuer>();
            issuer.Configure(launcher);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
        }

        NodeController Node(string type, Faction faction, int energy)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy);
            go.AddComponent<NodeIgnition>().Configure(Balance, new[] { Definition("Star"), Definition("Pulsar"), Definition("Magnetar") });
            return node;
        }

        static NodeIgnition IgnitionOf(NodeController node) => node.GetComponent<NodeIgnition>();

        [Test]
        public void OwnAsteroidWithTenBecomesStarLevelOneAtZero()
        {
            NodeController node = Node("Asteroid", Faction.Player, 10);

            ActionCheck result = IgnitionOf(node).TryIgnite(Faction.Player, NodeType.Star);

            Assert.IsTrue(result.IsAllowed);
            Assert.AreEqual(10, result.Cost, "16.1: coste de encendido.");
            Assert.AreEqual(NodeType.Star, node.Type);
            Assert.AreSame(Definition("Star"), node.Definition);
            Assert.AreEqual(1, node.Level);
            Assert.AreEqual(0, node.Energy, "4.1: consume los 10 y nace con 0.");
            Assert.AreEqual(50, node.EnergyCap, "El Tope pasa al del tipo nuevo (5.4).");
            Assert.AreEqual(NodeState.Normal, node.State, "14.1: SinEncender termina al encender.");
            Assert.AreEqual(Faction.Player, node.Faction);
        }

        [TestCase("Pulsar", NodeType.Pulsar, 100)]
        [TestCase("Magnetar", NodeType.Magnetar, 100)]
        public void IgnitionWorksForTheOtherTwoTypes(string name, NodeType type, int cap)
        {
            // La regla vale para los tres (4.1); lo que la fase 1 no tiene es el menú para elegir (T2-02).
            NodeController node = Node("Asteroid", Faction.Player, 10);

            Assert.IsTrue(IgnitionOf(node).TryIgnite(Faction.Player, type).IsAllowed);

            Assert.AreSame(Definition(name), node.Definition);
            Assert.AreEqual(1, node.Level);
            Assert.AreEqual(cap, node.EnergyCap);
            Assert.AreEqual(0, node.Energy);
        }

        [Test]
        public void IgnitionEmitsTypeLevelEnergyAndStateOnce()
        {
            NodeController node = Node("Asteroid", Faction.Player, 10);
            var log = new List<string>();
            node.OnTypeChanged += (a, b) => log.Add($"type {a}->{b}");
            node.OnLevelChanged += (a, b) => log.Add($"level {a}->{b}");
            node.OnEnergyChanged += (a, b) => log.Add($"energy {a}->{b}");
            node.OnStateChanged += (a, b) => log.Add($"state {a}->{b}");
            int factionChanges = 0;
            node.OnFactionChanged += (a, b) => factionChanges++;

            IgnitionOf(node).TryIgnite(Faction.Player, NodeType.Star);

            CollectionAssert.AreEquivalent(new[]
            {
                "energy 10->0",
                "level 0->1",
                "type Asteroid->Star",
                "state Unignited->Normal",
            }, log);
            Assert.AreEqual(0, factionChanges);
        }

        [Test]
        public void NotEnoughEnergyIsReportedWithTheCostAndChangesNothing()
        {
            NodeController node = Node("Asteroid", Faction.Player, 9);

            ActionCheck result = IgnitionOf(node).TryIgnite(Faction.Player, NodeType.Star);

            Assert.AreEqual(ActionBlock.InsufficientEnergy, result.Block);
            Assert.AreEqual(10, result.Cost, "13.7: el menú pinta el coste en rojo.");
            Assert.AreEqual(NodeType.Asteroid, node.Type);
            Assert.AreEqual(9, node.Energy);
            Assert.AreEqual(NodeState.Unignited, node.State);
        }

        [TestCase(Faction.Neutral, Faction.Player)]
        [TestCase(Faction.Enemy, Faction.Player)]
        [TestCase(Faction.Player, Faction.Enemy)]
        [TestCase(Faction.Neutral, Faction.Neutral)]
        public void OnlyTheOwnerIgnites(Faction owner, Faction actor)
        {
            NodeController node = Node("Asteroid", owner, 10);

            Assert.AreEqual(ActionBlock.NotOwner, IgnitionOf(node).TryIgnite(actor, NodeType.Star).Block);
            Assert.AreEqual(NodeType.Asteroid, node.Type);
            Assert.AreEqual(10, node.Energy);
        }

        [Test]
        public void EnemyIgnitesItsOwnAsteroidByTheSameRule()
        {
            // 3.1: paridad. El Enemigo enciende con la misma regla y el mismo coste.
            NodeController node = Node("Asteroid", Faction.Enemy, 10);

            Assert.IsTrue(IgnitionOf(node).TryIgnite(Faction.Enemy, NodeType.Star).IsAllowed);
            Assert.AreEqual(NodeType.Star, node.Type);
            Assert.AreEqual(Faction.Enemy, node.Faction);
        }

        [Test]
        public void IgnitedNodeCannotBeIgnitedAgain()
        {
            NodeController node = Node("Star", Faction.Player, 40);

            Assert.AreEqual(ActionBlock.NotAsteroid, IgnitionOf(node).TryIgnite(Faction.Player, NodeType.Magnetar).Block);
            Assert.AreEqual(40, node.Energy);
        }

        [Test]
        public void AsteroidIsNotAnIgnitionTarget()
        {
            NodeController node = Node("Asteroid", Faction.Player, 10);

            Assert.AreEqual(ActionBlock.InvalidTargetType, IgnitionOf(node).TryIgnite(Faction.Player, NodeType.Asteroid).Block);
            Assert.AreEqual(10, node.Energy);
        }

        [Test]
        public void CheckHasNoSideEffects()
        {
            NodeController node = Node("Asteroid", Faction.Player, 10);

            Assert.IsTrue(IgnitionOf(node).Check(Faction.Player, NodeType.Star).IsAllowed);

            Assert.AreEqual(NodeType.Asteroid, node.Type);
            Assert.AreEqual(10, node.Energy);
        }

        // T2-02: un tipo sin NodeDefinition es un tipo que esta fase todavía no tiene. El menú
        // lo enseña deshabilitado, y encenderlo no cobra ni cambia nada.
        [Test]
        public void TypeWithoutDefinitionIsUnavailable()
        {
            NodeController node = Node("Asteroid", Faction.Player, 10);
            IgnitionOf(node).Configure(Balance, new[] { Definition("Star") });

            Assert.IsTrue(IgnitionOf(node).Offers(NodeType.Star));
            Assert.IsFalse(IgnitionOf(node).Offers(NodeType.Pulsar));
            ActionCheck result = IgnitionOf(node).TryIgnite(Faction.Player, NodeType.Pulsar);

            Assert.AreEqual(ActionBlock.Unavailable, result.Block);
            Assert.AreEqual(Balance.IgnitionCost, result.Cost);
            Assert.AreEqual(NodeType.Asteroid, node.Type);
            Assert.AreEqual(10, node.Energy);
        }

        // Reunir la Energía no arregla un tipo que no existe: Unavailable va por delante.
        [Test]
        public void UnavailableWinsOverMissingEnergy()
        {
            NodeController node = Node("Asteroid", Faction.Player, 5);
            IgnitionOf(node).Configure(Balance, new[] { Definition("Star") });

            Assert.AreEqual(ActionBlock.InsufficientEnergy, IgnitionOf(node).Check(Faction.Player, NodeType.Star).Block);
            Assert.AreEqual(ActionBlock.Unavailable, IgnitionOf(node).Check(Faction.Player, NodeType.Magnetar).Block);
        }

        // Las reglas de dueño y tipo siguen mandando sobre la disponibilidad.
        [Test]
        public void OwnershipIsCheckedBeforeAvailability()
        {
            NodeController node = Node("Asteroid", Faction.Neutral, 10);
            IgnitionOf(node).Configure(Balance, new[] { Definition("Star") });

            Assert.AreEqual(ActionBlock.NotOwner, IgnitionOf(node).Check(Faction.Player, NodeType.Magnetar).Block);
        }

        [Test]
        public void IgnitedStarStartsGenerating()
        {
            NodeController node = Node("Asteroid", Faction.Player, 10);
            var generator = node.gameObject.AddComponent<StarGenerator>();
            generator.Tick(1f);   // Como Asteroide no acumula nada (4.2: solo la Estrella genera).

            IgnitionOf(node).TryIgnite(Faction.Player, NodeType.Star);
            for (int i = 0; i < 100; i++)
                generator.Tick(Tick);

            Assert.AreEqual(1, node.Energy, "Estrella N1: +1 cada 2.00 s (4.2).");
        }

        [Test]
        public void SnapshotAfterIgnitionReadsTheNewTypeAndLevel()
        {
            NodeController node = Node("Asteroid", Faction.Player, 10);
            IgnitionOf(node).TryIgnite(Faction.Player, NodeType.Star);

            NodeSnapshot snapshot = node.Snapshot();

            Assert.AreEqual(NodeType.Star, snapshot.Type);
            Assert.AreEqual(1, snapshot.Level);
            Assert.AreEqual(50, snapshot.EnergyCap);
        }

        [Test]
        public void IgnitedStarCanSend()
        {
            // Criterio de salida de la fase 1: capturar, encender y tomar. Tras encender, el
            // nodo es un origen legal.
            NodeController node = Node("Asteroid", Faction.Player, 10);
            IgnitionOf(node).TryIgnite(Faction.Player, NodeType.Star);
            node.AddEnergy(10);
            NodeController target = Node("Asteroid", Faction.Neutral, 10);

            Assert.IsTrue(SelectionManager.CanSelect(node, Faction.Player));
            Assert.AreEqual(LaunchBlock.None, issuer.Send(node, target, false));
            Assert.AreEqual(5, node.Energy);
        }

        [Test]
        public void IssuerIgnitesAsPlayer()
        {
            NodeController own = Node("Asteroid", Faction.Player, 10);
            NodeController enemy = Node("Asteroid", Faction.Enemy, 10);

            Assert.IsTrue(issuer.Ignite(own, NodeType.Star).IsAllowed);
            Assert.AreEqual(ActionBlock.NotOwner, issuer.Ignite(enemy, NodeType.Star).Block);
            Assert.AreEqual(NodeType.Asteroid, enemy.Type);
        }

        [Test]
        public void InitializeDoesNotEmitTypeChanged()
        {
            var go = new GameObject("Asteroid");
            spawned.Add(go);
            var node = go.AddComponent<NodeController>();
            int changes = 0;
            node.OnTypeChanged += (a, b) => changes++;

            node.Initialize(Definition("Asteroid"), Faction.Player, 10);
            node.Initialize(Definition("Star"), Faction.Player, 10);

            Assert.AreEqual(0, changes, "Initialize fija el estado de partida sin eventos.");
        }
    }
}
