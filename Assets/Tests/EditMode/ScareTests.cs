using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Presentation;
using Nova.Systems;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T2-08: NodeScare con protones reales (14.2) y el temblor provisional de la vista.
    // El nodo está en el origen y los protones salen del eje X hacia él: a 2.5 u/s, el umbral
    // de 1.5 s cae a 3.75 u.
    public class ScareTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();
        ProtonRegistry protons;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            protons = go.AddComponent<ProtonRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
        }

        NodeScare Node(string type, Faction faction, int energy)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, type == "Asteroid" ? 0 : 1);
            var scare = go.AddComponent<NodeScare>();
            scare.Configure(Balance, protons);
            return scare;
        }

        Proton Fly(NodeScare target, Faction faction, int charge, float distance)
        {
            var go = new GameObject("Proton");
            spawned.Add(go);
            var proton = go.AddComponent<Proton>();
            proton.Launch(new Vector2(distance, 0f), target.GetComponent<NodeController>(), faction, charge, Balance);
            protons.Register(proton);
            return proton;
        }

        // --- Condición ---

        [Test]
        public void ALethalProtonInsideTheThresholdScaresTheNode()
        {
            NodeScare scare = Node("Star", Faction.Player, 10);
            Fly(scare, Faction.Enemy, 10, 3f);

            scare.Tick();

            Assert.IsTrue(scare.IsScared);
            Assert.IsTrue(scare.GetComponent<NodeController>().HasState(NodeStateFlags.Scared));
        }

        [Test]
        public void TheScareStartsWhenTheProtonCrossesTheThreshold()
        {
            NodeScare scare = Node("Star", Faction.Player, 10);
            Proton proton = Fly(scare, Faction.Enemy, 10, 5f);

            // 24 ticks: 1.2 u recorridas, a 3.8 u (1.52 s). 27 ticks: a 3.65 u (1.46 s).
            for (int i = 0; i < 24; i++)
                proton.Tick(Tick);
            scare.Tick();
            Assert.IsFalse(scare.IsScared, "A 1.52 s todavía no asusta.");

            for (int i = 0; i < 3; i++)
                proton.Tick(Tick);
            scare.Tick();
            Assert.IsTrue(scare.IsScared, "A 1.46 s ya asusta.");
        }

        [Test]
        public void ANonLethalProtonDoesNotScare()
        {
            NodeScare scare = Node("Star", Faction.Player, 10);
            Fly(scare, Faction.Enemy, 9, 1f);

            scare.Tick();

            Assert.IsFalse(scare.IsScared);
        }

        [Test]
        public void AReinforcementDoesNotScare()
        {
            NodeScare scare = Node("Star", Faction.Player, 1);
            Fly(scare, Faction.Player, 40, 1f);

            scare.Tick();

            Assert.IsFalse(scare.IsScared);
        }

        [TestCase("Star", Faction.Enemy, Faction.Player)]
        [TestCase("Star", Faction.Neutral, Faction.Enemy)]
        [TestCase("Asteroid", Faction.Neutral, Faction.Player)]
        [TestCase("Magnetar", Faction.Neutral, Faction.Player)]
        public void EveryNodeCanBeScared(string type, Faction owner, Faction attacker)
        {
            NodeScare scare = Node(type, owner, 5);
            Fly(scare, attacker, 10, 1f);

            scare.Tick();

            Assert.IsTrue(scare.IsScared, "14.2: todos los nodos, también neutrales y del Enemigo.");
        }

        [Test]
        public void TheMagnetarOnlyFearsTwiceItsEnergy()
        {
            NodeScare scare = Node("Magnetar", Faction.Enemy, 10);
            Fly(scare, Faction.Player, 19, 1f);
            scare.Tick();
            Assert.IsFalse(scare.IsScared, "floor(19 × 0.5) = 9 < 10.");

            Fly(scare, Faction.Player, 20, 1f);
            scare.Tick();
            Assert.IsTrue(scare.IsScared);
        }

        [Test]
        public void SeveralLethalProtonsGiveASingleScare()
        {
            NodeScare scare = Node("Star", Faction.Player, 10);
            Fly(scare, Faction.Enemy, 10, 1f);
            Fly(scare, Faction.Enemy, 20, 2f);
            int changes = 0;
            scare.GetComponent<NodeController>().OnStateFlagsChanged += (before, after) => changes++;

            for (int i = 0; i < 5; i++)
                scare.Tick();

            Assert.IsTrue(scare.IsScared);
            Assert.AreEqual(1, changes, "14.2: el susto no se acumula.");
        }

        // --- Fin del susto ---

        [Test]
        public void AReinforcementThatArrivesInTimeEndsTheScare()
        {
            NodeScare scare = Node("Star", Faction.Player, 10);
            Fly(scare, Faction.Enemy, 10, 2f);
            scare.Tick();
            Assert.IsTrue(scare.IsScared);

            scare.GetComponent<NodeController>().AddEnergy(5);
            scare.Tick();

            Assert.IsFalse(scare.IsScared, "14.2: el refuerzo llega y el protón deja de ser letal.");
        }

        [Test]
        public void TheImpactEndsTheScare()
        {
            NodeScare scare = Node("Star", Faction.Player, 10);
            Proton proton = Fly(scare, Faction.Enemy, 10, 0.04f);
            scare.Tick();
            Assert.IsTrue(scare.IsScared);

            proton.Tick(Tick);
            proton.Tick(Tick);
            protons.ResolveArrivals();
            scare.Tick();

            Assert.AreEqual(Faction.Enemy, scare.GetComponent<NodeController>().Faction);
            Assert.IsFalse(scare.IsScared);
        }

        [Test]
        public void BeingShotDownEndsTheScare()
        {
            NodeScare scare = Node("Star", Faction.Player, 10);
            Proton proton = Fly(scare, Faction.Enemy, 10, 1f);
            scare.Tick();
            Assert.IsTrue(scare.IsScared);

            proton.TakeShot(10);
            scare.Tick();

            Assert.IsFalse(scare.IsScared);
        }

        [Test]
        public void AMagnetarShotThatLeavesItNonLethalEndsTheScare()
        {
            NodeScare scare = Node("Star", Faction.Player, 10);
            Proton proton = Fly(scare, Faction.Enemy, 11, 1f);
            scare.Tick();
            Assert.IsTrue(scare.IsScared);

            proton.TakeShot(Balance.MagnetarShotDamage);
            scare.Tick();

            Assert.IsFalse(scare.IsScared, "11 − 2 = 9 de Carga ya no captura 10.");
        }

        // --- Sin datos ---

        [Test]
        public void WithoutARegistryNothingScares()
        {
            NodeScare scare = Node("Star", Faction.Player, 0);
            Fly(scare, Faction.Enemy, 10, 1f);
            scare.Configure(Balance, null);

            Assume.That(ProtonRegistry.Instance, Is.Null, "En EditMode Awake no corre y no hay singleton.");
            Assert.IsFalse(scare.Evaluate());
        }

        [Test]
        public void AnUninitializedNodeIsNeverScared()
        {
            var go = new GameObject("Node");
            spawned.Add(go);
            go.AddComponent<NodeController>();
            var scare = go.AddComponent<NodeScare>();
            scare.Configure(Balance, protons);

            Assert.IsFalse(scare.Evaluate());
        }

        // --- Vista provisional ---

        [Test]
        public void TheShakeMovesOneWholePixelEachWay()
        {
            Assert.AreEqual(1f / 64f, ProvisionalNodeView.ShakeOffset(0).x, 1e-6f);
            Assert.AreEqual(-1f / 64f, ProvisionalNodeView.ShakeOffset(1).x, 1e-6f);
            Assert.AreEqual(1f / 64f, ProvisionalNodeView.ShakeOffset(2).x, 1e-6f);
            Assert.AreEqual(0f, ProvisionalNodeView.ShakeOffset(0).y);
        }

        [Test]
        public void TheViewSettlesWhenTheScareEnds()
        {
            NodeScare scare = Node("Star", Faction.Player, 10);
            var view = scare.gameObject.AddComponent<ProvisionalNodeView>();
            view.Subscribe();
            view.Body.transform.localPosition = ProvisionalNodeView.ShakeOffset(0);
            view.Label.transform.localPosition = ProvisionalNodeView.ShakeOffset(0);

            Fly(scare, Faction.Enemy, 10, 1f);
            scare.Tick();
            Assert.AreNotEqual(Vector3.zero, view.Body.transform.localPosition, "Asustado, Refresh no lo recoloca.");

            scare.GetComponent<NodeController>().AddEnergy(5);
            scare.Tick();

            Assert.AreEqual(Vector3.zero, view.Body.transform.localPosition);
            Assert.AreEqual(Vector3.zero, view.Label.transform.localPosition);
            view.Unsubscribe();
        }
    }
}
