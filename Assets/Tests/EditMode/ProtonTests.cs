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
    // T1-09: ProtonLauncher, Proton y ProtonPool (5.2, 5.3, 6.1, 6.2).
    public class ProtonTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();
        ProtonLauncher launcher;
        ProtonPool pool;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Protons");
            spawned.Add(go);
            pool = go.AddComponent<ProtonPool>();
            launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, pool);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
        }

        NodeController Node(string type, Faction faction, int energy, Vector2 position, int? level = null)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            if (level.HasValue)
                node.Initialize(Definition(type), faction, energy, level.Value);
            else
                node.Initialize(Definition(type), faction, energy);
            return node;
        }

        Proton Launch(NodeController source, NodeController target, bool fullSend = false)
        {
            Assert.AreEqual(LaunchBlock.None, launcher.TryLaunch(source, target, source.Faction, fullSend, out Proton proton));
            return proton;
        }

        static int FlyUntilArrival(Proton proton)
        {
            int ticks = 0;
            while (!proton.Step(Tick))
            {
                ticks++;
                Assert.Less(ticks, 100000, "El protón no llega nunca.");
            }
            return ticks + 1;
        }

        static void FlyAndImpact(Proton proton)
        {
            FlyUntilArrival(proton);
            proton.Impact();
        }

        // --- Envío (5.2) ---

        [Test]
        public void HalfSendRoundsDown()
        {
            NodeController star = Node("Star", Faction.Player, 21, Vector2.zero);
            NodeController other = Node("Star", Faction.Player, 0, Vector2.right);

            Proton proton = Launch(star, other);

            Assert.AreEqual(10, proton.Charge);
            Assert.AreEqual(11, star.Energy);
        }

        // Instantáneo en el origen; el nodo queda a 0 sin cambiar de bando ni de nivel.
        [Test]
        public void FullSendEmptiesTheSourceAndNothingElse()
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero, 3);
            NodeController other = Node("Star", Faction.Player, 0, Vector2.right);

            Proton proton = Launch(star, other, fullSend: true);

            Assert.AreEqual(20, proton.Charge);
            Assert.AreEqual(0, star.Energy);
            Assert.AreEqual(Faction.Player, star.Faction);
            Assert.AreEqual(3, star.Level);
        }

        // 5.2: el 50 % de 1 no se ejecuta; el 100 % de 1 sí.
        [Test]
        public void HalfOfOneDoesNotLaunch()
        {
            NodeController star = Node("Star", Faction.Player, 1, Vector2.zero);
            NodeController other = Node("Star", Faction.Player, 0, Vector2.right);

            Assert.AreEqual(LaunchBlock.NothingToSend, launcher.TryLaunch(star, other, Faction.Player, false, out Proton proton));
            Assert.IsNull(proton);
            Assert.AreEqual(1, star.Energy);
            Assert.AreEqual(0, pool.CreatedCount);

            Assert.AreEqual(LaunchBlock.None, launcher.TryLaunch(star, other, Faction.Player, true, out proton));
            Assert.AreEqual(1, proton.Charge);
        }

        [Test]
        public void EmptyNodeDoesNotLaunch()
        {
            NodeController star = Node("Star", Faction.Player, 0, Vector2.zero);
            NodeController other = Node("Star", Faction.Player, 0, Vector2.right);
            Assert.AreEqual(LaunchBlock.NothingToSend, launcher.TryLaunch(star, other, Faction.Player, true, out _));
        }

        [Test]
        public void AsteroidCannotSend()
        {
            NodeController rock = Node("Asteroid", Faction.Player, 10, Vector2.zero);
            NodeController other = Node("Star", Faction.Player, 0, Vector2.right);
            Assert.AreEqual(LaunchBlock.IsAsteroid, launcher.TryLaunch(rock, other, Faction.Player, true, out _));
            Assert.AreEqual(10, rock.Energy);
        }

        [Test]
        public void OnlyTheOwnerSends()
        {
            NodeController enemy = Node("Star", Faction.Enemy, 20, Vector2.zero);
            NodeController magnetar = Node("Magnetar", Faction.Neutral, 50, Vector2.up);
            NodeController other = Node("Star", Faction.Player, 0, Vector2.right);

            Assert.AreEqual(LaunchBlock.NotOwner, launcher.TryLaunch(enemy, other, Faction.Player, false, out _));
            Assert.AreEqual(LaunchBlock.NotOwner, launcher.TryLaunch(magnetar, other, Faction.Neutral, false, out _));
            Assert.AreEqual(20, enemy.Energy);
            Assert.AreEqual(50, magnetar.Energy);
        }

        [Test]
        public void CannotSendToItself()
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero);
            Assert.AreEqual(LaunchBlock.SameNode, launcher.TryLaunch(star, star, Faction.Player, false, out _));
        }

        // Check no toca nada: es lo que usará el lazo para ignorar orígenes sin Energía (17, caso 22).
        [Test]
        public void CheckHasNoSideEffects()
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController other = Node("Star", Faction.Player, 0, Vector2.right);

            Assert.AreEqual(LaunchBlock.None, ProtonLauncher.Check(star, other, Faction.Player, false, out int charge));
            Assert.AreEqual(10, charge);
            Assert.AreEqual(20, star.Energy);
            Assert.AreEqual(0, pool.CreatedCount);
        }

        // --- Vuelo (6.1) ---

        // 2.5 u/s a 50 Hz: 1 u son 0.4 s = 20 ticks y 5 u son 2 s = 100 ticks.
        [TestCase(1f, 20)]
        [TestCase(5f, 100)]
        [TestCase(3f, 60)]
        public void FliesAtConstantSpeed(float distance, int expectedTicks)
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController other = Node("Star", Faction.Player, 0, new Vector2(distance, 0f));

            Assert.AreEqual(expectedTicks, FlyUntilArrival(Launch(star, other)));
        }

        [Test]
        public void FliesInAStraightLineBetweenCenters()
        {
            NodeController star = Node("Star", Faction.Player, 20, new Vector2(1f, 1f));
            NodeController other = Node("Star", Faction.Player, 0, new Vector2(4f, 5f));   // 5 u
            Proton proton = Launch(star, other);

            Assert.AreEqual(new Vector2(1f, 1f), (Vector2)proton.transform.position);
            for (int i = 0; i < 50; i++)
                proton.Step(Tick);

            Vector2 half = proton.transform.position;
            Assert.AreEqual(2.5f, half.x, 1e-4f);
            Assert.AreEqual(3f, half.y, 1e-4f);
        }

        // 17, caso 3: el protón conserva la facción del emisor en el lanzamiento.
        [Test]
        public void ProtonKeepsTheFactionOfItsLaunch()
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController rock = Node("Asteroid", Faction.Neutral, 10, Vector2.right);
            Proton proton = Launch(star, rock, fullSend: true);

            star.GetComponent<NodeOwnership>().ChangeFaction(Faction.Enemy);
            FlyAndImpact(proton);

            Assert.AreEqual(Faction.Player, rock.Faction);
        }

        // --- Impacto (5.3, 6.2) ---

        [Test]
        public void FriendlyImpactIsAbsorbedAndCapped()
        {
            NodeController star = Node("Star", Faction.Player, 40, Vector2.zero);
            NodeController other = Node("Star", Faction.Player, 45, Vector2.right);

            FlyAndImpact(Launch(star, other, fullSend: true));

            Assert.AreEqual(50, other.Energy);
        }

        // 5.3: el Magnetar aliado recibe 1:1.
        [Test]
        public void FriendlyMagnetarAbsorbsOneToOne()
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController magnetar = Node("Magnetar", Faction.Player, 0, Vector2.right);

            FlyAndImpact(Launch(star, magnetar));

            Assert.AreEqual(10, magnetar.Energy);
        }

        [Test]
        public void HostileImpactDamages()
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController enemy = Node("Star", Faction.Enemy, 30, Vector2.right);

            FlyAndImpact(Launch(star, enemy));

            Assert.AreEqual(20, enemy.Energy);
            Assert.AreEqual(Faction.Enemy, enemy.Faction);
        }

        // Capturar un Asteroide neutral: cambia de bando, queda el remanente y despierta (14.1).
        [Test]
        public void CapturingANeutralAsteroid()
        {
            NodeController star = Node("Star", Faction.Player, 24, Vector2.zero);
            NodeController rock = Node("Asteroid", Faction.Neutral, 10, Vector2.right);

            FlyAndImpact(Launch(star, rock));   // Carga 12

            Assert.AreEqual(Faction.Player, rock.Faction);
            Assert.AreEqual(2, rock.Energy);
            Assert.AreEqual(NodeState.Unignited, rock.State);
        }

        // 6.2, nota obligatoria: el remanente también lleva el multiplicador. 100 de Carga contra
        // un Magnetar con 40 son 50 efectivos: lo captura y le deja 10, con su tipo y su nivel.
        [Test]
        public void CapturingAMagnetarAppliesResistanceToTheRemainder()
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 100, Vector2.zero);
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 40, Vector2.right, 4);

            FlyAndImpact(Launch(pulsar, magnetar, fullSend: true));

            Assert.AreEqual(Faction.Player, magnetar.Faction);
            Assert.AreEqual(10, magnetar.Energy);
            Assert.AreEqual(NodeType.Magnetar, magnetar.Type);
            Assert.AreEqual(4, magnetar.Level);
        }

        // Resistencia ×2 en un impacto hostil sin captura: 20 de Carga quitan 10.
        [Test]
        public void HostileMagnetarHalvesTheCharge()
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 50, Vector2.right);

            FlyAndImpact(Launch(star, magnetar, fullSend: true));

            Assert.AreEqual(40, magnetar.Energy);
        }

        // 17, caso 2: la facción se comprueba al llegar, no al lanzar.
        [Test]
        public void TargetThatBecameFriendlyAbsorbs()
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController enemy = Node("Star", Faction.Enemy, 5, Vector2.right);
            Proton proton = Launch(star, enemy);

            enemy.GetComponent<NodeOwnership>().ChangeFaction(Faction.Player);
            FlyAndImpact(proton);

            Assert.AreEqual(15, enemy.Energy);
            Assert.AreEqual(Faction.Player, enemy.Faction);
        }

        [Test]
        public void ImpactIsNotified()
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController enemy = Node("Star", Faction.Enemy, 30, Vector2.right);
            Proton proton = Launch(star, enemy);

            var seen = new List<(NodeController, ImpactResult)>();
            proton.OnImpact += (p, node, outcome) => seen.Add((node, outcome.Result));
            FlyAndImpact(proton);

            CollectionAssert.AreEqual(new[] { (enemy, ImpactResult.Damaged) }, seen);
        }

        // --- Pool ---

        [Test]
        public void ProtonReturnsToThePoolAndIsReused()
        {
            NodeController star = Node("Star", Faction.Player, 40, Vector2.zero);
            NodeController other = Node("Star", Faction.Player, 0, Vector2.right);

            Proton first = Launch(star, other);
            Assert.IsTrue(first.gameObject.activeSelf);
            FlyAndImpact(first);

            Assert.IsFalse(first.InFlight);
            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.AreEqual(1, pool.FreeCount);

            Proton second = Launch(star, other);
            Assert.AreSame(first, second);
            Assert.AreEqual(1, pool.CreatedCount);
        }

        // Cada envío es un protón (6.1): dos en vuelo a la vez son dos objetos.
        [Test]
        public void EachSendIsItsOwnProton()
        {
            NodeController a = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController b = Node("Star", Faction.Player, 20, Vector2.up);
            NodeController target = Node("Star", Faction.Enemy, 30, Vector2.right);

            Proton pa = Launch(a, target);
            Proton pb = Launch(b, target);

            Assert.AreNotSame(pa, pb);
            Assert.AreEqual(2, pool.CreatedCount);
        }

        [Test]
        public void InFlightProtonCannotBeReleased()
        {
            NodeController star = Node("Star", Faction.Player, 20, Vector2.zero);
            NodeController other = Node("Star", Faction.Player, 0, Vector2.right);
            Proton proton = Launch(star, other);

            Assert.Throws<System.InvalidOperationException>(() => pool.Release(proton));
        }
    }
}
