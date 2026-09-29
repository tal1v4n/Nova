using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Presentation;
using Nova.Systems;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nova.Tests
{
    // T1-10: NodeRegistry, ProtonRegistry, orden de impactos en el mismo tick y orden de
    // ejecución de 15.10.
    public class RegistryTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();
        NodeRegistry nodes;
        ProtonRegistry protons;
        ProtonLauncher launcher;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            nodes = go.AddComponent<NodeRegistry>();
            protons = go.AddComponent<ProtonRegistry>();
            var pool = go.AddComponent<ProtonPool>();
            launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset"), pool, protons);
        }

        [TearDown]
        public void TearDown()
        {
            nodes.Clear();
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
        }

        NodeController Node(string type, Faction faction, int energy, Vector2 position = default)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy);
            return node;
        }

        Proton Launch(NodeController source, NodeController target, bool fullSend = false)
        {
            Assert.AreEqual(LaunchBlock.None, launcher.TryLaunch(source, target, source.Faction, fullSend, out Proton proton));
            return proton;
        }

        // Un tick de partida: todos los protones avanzan y después el registro resuelve.
        void RunTick()
        {
            foreach (Proton proton in new List<Proton>(protons.InFlight))
                proton.Tick(Tick);
            protons.ResolveArrivals();
        }

        void RunUntilEmpty()
        {
            for (int i = 0; protons.Count > 0; i++)
            {
                Assert.Less(i, 100000, "Quedan protones que no llegan.");
                RunTick();
            }
        }

        // --- NodeRegistry ---

        [Test]
        public void IndexesNodesByFaction()
        {
            NodeController a = Node("Star", Faction.Player, 0);
            NodeController b = Node("Asteroid", Faction.Neutral, 10);
            NodeController c = Node("Star", Faction.Enemy, 0);
            NodeController d = Node("Pulsar", Faction.Player, 0);
            foreach (NodeController n in new[] { a, b, c, d })
                nodes.Register(n);

            CollectionAssert.AreEqual(new[] { a, b, c, d }, nodes.All);
            CollectionAssert.AreEqual(new[] { a, d }, nodes.GetNodes(Faction.Player));
            CollectionAssert.AreEqual(new[] { c }, nodes.GetNodes(Faction.Enemy));
            CollectionAssert.AreEqual(new[] { b }, nodes.GetNodes(Faction.Neutral));
            Assert.AreEqual(2, nodes.CountOf(Faction.Player));
        }

        [Test]
        public void CaptureMovesTheNodeToItsNewFaction()
        {
            NodeController rock = Node("Asteroid", Faction.Neutral, 10);
            NodeController star = Node("Star", Faction.Player, 0);
            nodes.Register(star);
            nodes.Register(rock);

            rock.GetComponent<NodeOwnership>().ChangeFaction(Faction.Player);

            CollectionAssert.IsEmpty(nodes.GetNodes(Faction.Neutral));
            CollectionAssert.AreEqual(new[] { star, rock }, nodes.GetNodes(Faction.Player));
        }

        [Test]
        public void UnregisterStopsTracking()
        {
            NodeController rock = Node("Asteroid", Faction.Neutral, 10);
            nodes.Register(rock);
            nodes.Unregister(rock);

            rock.GetComponent<NodeOwnership>().ChangeFaction(Faction.Enemy);

            Assert.AreEqual(0, nodes.Count);
            CollectionAssert.IsEmpty(nodes.GetNodes(Faction.Enemy));
            Assert.IsFalse(nodes.Contains(rock));
        }

        [Test]
        public void RegisteringTwiceIsIgnored()
        {
            NodeController star = Node("Star", Faction.Player, 0);
            nodes.Register(star);
            nodes.Register(star);
            Assert.AreEqual(1, nodes.Count);
            Assert.AreEqual(1, nodes.CountOf(Faction.Player));
        }

        [Test]
        public void UninitializedNodeCannotRegister()
        {
            var go = new GameObject("Raw");
            spawned.Add(go);
            var raw = go.AddComponent<NodeController>();
            Assert.Throws<InvalidOperationException>(() => nodes.Register(raw));
        }

        [Test]
        public void ClearForgetsEverythingAndStopsListening()
        {
            NodeController rock = Node("Asteroid", Faction.Neutral, 10);
            nodes.Register(rock);
            nodes.Clear();

            rock.GetComponent<NodeOwnership>().ChangeFaction(Faction.Player);

            Assert.AreEqual(0, nodes.Count);
            CollectionAssert.IsEmpty(nodes.GetNodes(Faction.Player));
        }

        // --- ProtonRegistry ---

        [Test]
        public void LaunchedProtonsAreIndexedByTarget()
        {
            NodeController a = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController b = Node("Star", Faction.Player, 20, Vector2.up);
            NodeController enemy = Node("Star", Faction.Enemy, 30, Vector2.right);
            NodeController other = Node("Star", Faction.Enemy, 30, Vector2.left);

            Proton p1 = Launch(a, enemy);
            Proton p2 = Launch(b, enemy);
            Proton p3 = Launch(a, other);

            Assert.AreEqual(3, protons.Count);
            CollectionAssert.AreEqual(new[] { p1, p2 }, protons.GetIncoming(enemy));
            CollectionAssert.AreEqual(new[] { p3 }, protons.GetIncoming(other));
            CollectionAssert.IsEmpty(protons.GetIncoming(a));
        }

        // Registrado, el protón llega pero no resuelve hasta que el registro cierra el tick.
        [Test]
        public void ArrivalWaitsForTheEndOfTheTick()
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController enemy = Node("Star", Faction.Enemy, 30, Vector2.right);
            Proton proton = Launch(star, enemy);

            for (int i = 0; i < 20; i++)
                proton.Tick(Tick);

            Assert.IsTrue(proton.InFlight);
            Assert.AreEqual(1, protons.PendingArrivals);
            Assert.AreEqual(30, enemy.Energy);

            protons.ResolveArrivals();

            Assert.AreEqual(20, enemy.Energy);
            Assert.AreEqual(0, protons.Count);
            Assert.AreEqual(0, protons.PendingArrivals);
            CollectionAssert.IsEmpty(protons.GetIncoming(enemy));
        }

        [Test]
        public void ReusedProtonIsRegisteredAgain()
        {
            NodeController star = Node("Star", Faction.Player, 40, Vector2.zero);
            NodeController other = Node("Star", Faction.Player, 0, Vector2.right);

            Proton first = Launch(star, other);
            RunUntilEmpty();
            Proton second = Launch(star, other);

            Assert.AreSame(first, second);
            CollectionAssert.AreEqual(new[] { second }, protons.GetIncoming(other));
            RunUntilEmpty();
            Assert.AreEqual(30, other.Energy);
        }

        // --- Orden de impactos del mismo tick (15.10, 17 caso 1) ---

        // Mismo tick, distinta llegada: el Jugador (0.99 u, llega a los 16 ms del tick) resuelve
        // antes que el Enemigo (1 u, a los 20 ms). El segundo opera sobre el resultado del primero.
        [Test]
        public void EarlierArrivalInTheTickResolvesFirst()
        {
            NodeController rock = Node("Asteroid", Faction.Neutral, 10, Vector2.zero);
            NodeController enemy = Node("Star", Faction.Enemy, 24, new Vector2(1f, 0f));
            NodeController player = Node("Star", Faction.Player, 24, new Vector2(-0.99f, 0f));

            Launch(enemy, rock);    // Carga 12, lanzado primero
            Launch(player, rock);   // Carga 12
            RunUntilEmpty();

            // Jugador: 12 contra 10, captura con 2. Enemigo: 12 contra 2, captura con 10.
            Assert.AreEqual(Faction.Enemy, rock.Faction);
            Assert.AreEqual(10, rock.Energy);
        }

        // Empate exacto de llegada: primero el de mayor Carga.
        [Test]
        public void OnATieTheBiggerChargeResolvesFirst()
        {
            NodeController rock = Node("Asteroid", Faction.Neutral, 10, Vector2.zero);
            NodeController player = Node("Star", Faction.Player, 20, new Vector2(-1f, 0f));
            NodeController enemy = Node("Star", Faction.Enemy, 24, new Vector2(1f, 0f));

            Launch(player, rock);   // Carga 10, lanzado primero
            Launch(enemy, rock);    // Carga 12
            RunUntilEmpty();

            // Enemigo: 12 contra 10, captura con 2. Jugador: 10 contra 2, captura con 8.
            Assert.AreEqual(Faction.Player, rock.Faction);
            Assert.AreEqual(8, rock.Energy);
        }

        // Empate de llegada y de Carga: primero el que se lanzó antes.
        [Test]
        public void OnAFullTieTheFirstLaunchedResolvesFirst()
        {
            NodeController rock = Node("Asteroid", Faction.Neutral, 10, Vector2.zero);
            NodeController player = Node("Star", Faction.Player, 24, new Vector2(-1f, 0f));
            NodeController enemy = Node("Star", Faction.Enemy, 24, new Vector2(1f, 0f));

            Launch(player, rock);   // Carga 12
            Launch(enemy, rock);    // Carga 12
            RunUntilEmpty();

            // Jugador: captura con 2. Enemigo: 12 contra 2, captura con 10.
            Assert.AreEqual(Faction.Enemy, rock.Faction);
            Assert.AreEqual(10, rock.Energy);
        }

        // Llegadas en ticks distintos no se reordenan: el Jugador llega un tick antes.
        [Test]
        public void ArrivalsInDifferentTicksKeepTheirOrder()
        {
            NodeController rock = Node("Asteroid", Faction.Neutral, 10, Vector2.zero);
            NodeController player = Node("Star", Faction.Player, 20, new Vector2(-0.9f, 0f));
            NodeController enemy = Node("Star", Faction.Enemy, 24, new Vector2(1f, 0f));

            Launch(enemy, rock);    // Carga 12, tick 20
            Launch(player, rock);   // Carga 10, tick 18
            RunUntilEmpty();

            // Jugador: 10 contra 10, captura con 0. Enemigo: 12 contra 0, captura y el remanente
            // se recorta al Tope del Asteroide (6.2): 10. En el orden inverso ganaría el Jugador con 8.
            Assert.AreEqual(Faction.Enemy, rock.Faction);
            Assert.AreEqual(10, rock.Energy);
        }

        // --- Orden de ejecución (15.10) ---

        static int OrderOf(Type type)
        {
            var attribute = type.GetCustomAttribute<DefaultExecutionOrder>();
            return attribute != null ? attribute.order : 0;
        }

        [TestCase(typeof(NodeRegistry), -100)]
        [TestCase(typeof(ProtonRegistry), -100)]
        [TestCase(typeof(NodeController), 0)]
        [TestCase(typeof(NodeIdentity), 0)]
        [TestCase(typeof(NodeOwnership), 0)]
        [TestCase(typeof(NodeEnergy), 0)]
        [TestCase(typeof(NodeLevelComponent), 0)]
        [TestCase(typeof(NodeStateMachine), 0)]
        [TestCase(typeof(StarGenerator), 0)]
        [TestCase(typeof(Proton), 10)]
        [TestCase(typeof(ProvisionalNodeView), 100)]
        public void ExecutionOrderFollows15_10(Type type, int expected)
        {
            Assert.AreEqual(expected, OrderOf(type));
        }
    }
}
