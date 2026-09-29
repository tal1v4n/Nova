using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T1-06: NodeController y sus tres componentes (15.5).
    public class NodeComponentsTests
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

        // RequireComponent: añadir el controlador trae los tres componentes.
        [Test]
        public void ControllerBringsItsComponents()
        {
            Assert.IsNotNull(go.GetComponent<NodeIdentity>());
            Assert.IsNotNull(go.GetComponent<NodeOwnership>());
            Assert.IsNotNull(go.GetComponent<NodeEnergy>());
        }

        [Test]
        public void InitializeTakesTypeAndCapFromTheDefinition()
        {
            Assert.IsFalse(node.IsInitialized);
            node.Initialize(Definition("Star"), Faction.Player, 12);

            Assert.IsTrue(node.IsInitialized);
            Assert.AreEqual(NodeType.Star, node.Type);
            Assert.AreEqual(Faction.Player, node.Faction);
            Assert.AreEqual(12, node.Energy);
            Assert.AreEqual(50, node.EnergyCap);
        }

        [Test]
        public void InitializeRejectsEnergyAboveCap()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => node.Initialize(Definition("Asteroid"), Faction.Neutral, 11));
        }

        // Inicializar no es cambiar de bando ni ganar Energía: no hay eventos.
        [Test]
        public void InitializeRaisesNoEvents()
        {
            int events = 0;
            node.OnFactionChanged += (_, __) => events++;
            node.OnEnergyChanged += (_, __) => events++;
            node.Initialize(Definition("Asteroid"), Faction.Neutral, 10);
            Assert.AreEqual(0, events);
        }

        [Test]
        public void SnapshotReflectsTheNode()
        {
            node.Initialize(Definition("Magnetar"), Faction.Enemy, 40);
            NodeSnapshot s = node.Snapshot();
            Assert.AreEqual(Faction.Enemy, s.Faction);
            Assert.AreEqual(NodeType.Magnetar, s.Type);
            Assert.AreEqual(40, s.Energy);
            Assert.AreEqual(100, s.EnergyCap);
        }

        // --- NodeOwnership ---

        [Test]
        public void ChangeFactionRaisesPreviousAndNew()
        {
            node.Initialize(Definition("Asteroid"), Faction.Neutral, 10);
            var seen = new List<(Faction, Faction)>();
            node.OnFactionChanged += (a, b) => seen.Add((a, b));

            go.GetComponent<NodeOwnership>().ChangeFaction(Faction.Player);

            Assert.AreEqual(Faction.Player, node.Faction);
            CollectionAssert.AreEqual(new[] { (Faction.Neutral, Faction.Player) }, seen);
        }

        [Test]
        public void ChangeToTheSameFactionIsSilent()
        {
            node.Initialize(Definition("Star"), Faction.Player, 0);
            int events = 0;
            node.OnFactionChanged += (_, __) => events++;
            go.GetComponent<NodeOwnership>().ChangeFaction(Faction.Player);
            Assert.AreEqual(0, events);
        }

        // --- NodeEnergy ---

        [Test]
        public void AddDiscardsOverflow()
        {
            node.Initialize(Definition("Star"), Faction.Player, 45);
            var energy = go.GetComponent<NodeEnergy>();
            var seen = new List<(int, int)>();
            node.OnEnergyChanged += (a, b) => seen.Add((a, b));

            energy.Add(20);

            Assert.AreEqual(50, node.Energy);
            CollectionAssert.AreEqual(new[] { (45, 50) }, seen);
        }

        [Test]
        public void SubtractNeverGoesBelowZero()
        {
            node.Initialize(Definition("Star"), Faction.Player, 7);
            go.GetComponent<NodeEnergy>().Subtract(10);
            Assert.AreEqual(0, node.Energy);
        }

        [Test]
        public void AddingToAFullNodeIsSilent()
        {
            node.Initialize(Definition("Star"), Faction.Player, 50);
            int events = 0;
            node.OnEnergyChanged += (_, __) => events++;
            go.GetComponent<NodeEnergy>().Add(5);
            Assert.AreEqual(0, events);
        }

        // 8.1: al cambiar de tipo, la Energía se recorta al tope nuevo.
        [Test]
        public void ClampCutsEnergyToANewCap()
        {
            node.Initialize(Definition("Magnetar"), Faction.Player, 80);
            var energy = go.GetComponent<NodeEnergy>();
            energy.SetCap(50);
            Assert.AreEqual(80, node.Energy);
            energy.Clamp();
            Assert.AreEqual(50, node.Energy);
        }

        [Test]
        public void NegativeAmountsAreRejected()
        {
            node.Initialize(Definition("Star"), Faction.Player, 10);
            var energy = go.GetComponent<NodeEnergy>();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => energy.Add(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => energy.Subtract(-1));
        }
    }
}
