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
    // T3-05: Decaimiento (7.4). NodeDecay, el haz que infecta, la pérdida de 2 E/s hasta 0, la cura
    // por impacto y el reinicio, con los casos 11, 12, 13 y 32 de 17, la convivencia con Congelado
    // (7.3, 14.6) y la cancelación de una reconversión en curso (desviación 203).
    public class DecayTests
    {
        const float Tick = 0.02f;

        // Con 2 E/s, un punto cada 0.5 s: 25 ticks a 50 Hz (16.1).
        const int TicksPerPoint = 25;

        // 30 s a 50 Hz (16.1).
        const int AutoThawTicks = 1500;

        // 0.25 s de haz: termina en el tick 13 (desviación 196).
        const int BeamTicks = 13;

        readonly List<GameObject> spawned = new List<GameObject>();
        ProtonRegistry protons;
        PowerBeamSystem beams;
        NodeController far;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            protons = go.AddComponent<ProtonRegistry>();
            beams = go.AddComponent<PowerBeamSystem>();
            // Destino lejano de los protones que no deben llegar durante el test.
            far = Node("Star", Faction.Neutral, 0, new Vector2(50f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            spawned.Clear();
        }

        // Nodo con los componentes de la escena de prueba que entran en juego al infectar.
        NodeController Node(string type, Faction faction, int energy, Vector2 position = default, int level = 1)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, type == "Asteroid" ? 0 : level);
            go.AddComponent<NodeFreeze>().Configure(Balance);
            go.AddComponent<NodeDecay>().Configure(Balance);
            go.AddComponent<NodeConverter>().Configure(Balance, new[] { Definition("Star"), Definition("Pulsar"), Definition("Magnetar") });
            go.AddComponent<StarGenerator>();
            go.AddComponent<MagnetarTurret>().Configure(Balance, protons);
            go.AddComponent<PulsarCaster>().Configure(Balance, new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") }, beams);
            return node;
        }

        static NodeDecay Decay(NodeController node) => node.GetComponent<NodeDecay>();

        // El tick de los componentes de nodo que importan aquí, en el orden de la escena.
        static void Run(NodeController node, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                node.GetComponent<NodeFreeze>().Tick(Tick);
                node.GetComponent<NodeDecay>().Tick(Tick);
                node.GetComponent<NodeConverter>().Tick(Tick);
                node.GetComponent<StarGenerator>().Tick(Tick);
                node.GetComponent<PulsarCaster>().Tick(Tick);
            }
        }

        // Un protón de esa facción y esa Carga llega al nodo (6.2, 7.3).
        static ImpactOutcome Hit(NodeController node, Faction by, int charge)
        {
            ImpactOutcome outcome = ImpactResolver.Resolve(new ProtonSnapshot(by, charge), node.Snapshot(), Balance.MagnetarResistance);
            node.ApplyImpact(outcome);
            return outcome;
        }

        Proton Fly(Faction faction, int charge, Vector2 at)
        {
            var go = new GameObject("Proton");
            spawned.Add(go);
            var proton = go.AddComponent<Proton>();
            proton.Launch(at, far, faction, charge, Balance);
            protons.Register(proton);
            return proton;
        }

        // --- El haz infecta (7.4) ---

        [Test]
        public void TheDecayBeamInfectsTheTargetWhenItLands()
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 70, new Vector2(-4f, 0f), level: 2);
            var caster = pulsar.GetComponent<PulsarCaster>();
            Assert.IsTrue(caster.TryCharge(Faction.Player, PowerType.Decay).IsAllowed);
            for (int i = 0; i < 600; i++)
                caster.Tick(Tick);
            Assert.AreEqual(PowerChargeState.Ready, caster.State);
            NodeController target = Node("Star", Faction.Enemy, 30, new Vector2(4f, 0f));

            Assert.IsTrue(caster.TryLaunch(Faction.Player, target).IsAllowed);
            for (int i = 0; i < BeamTicks - 1; i++)
                beams.Tick(Tick);
            Assert.IsFalse(target.HasState(NodeStateFlags.Infected), "El efecto llega al terminar el haz (7.2.2).");

            beams.Tick(Tick);
            Assert.IsTrue(target.HasState(NodeStateFlags.Infected));
            Assert.AreEqual(Faction.Enemy, target.Faction);
            Assert.AreEqual(30, target.Energy, "Infectar no quita nada en el acto.");
        }

        [Test]
        public void InfectingWithoutNodeDecayIsAWiringError()
        {
            var go = new GameObject("Bare");
            spawned.Add(go);
            var bare = go.AddComponent<NodeController>();
            bare.Initialize(Definition("Star"), Faction.Enemy, 10);
            Assert.Throws<System.InvalidOperationException>(() => bare.Infect());
        }

        [Test]
        public void InfectionAndCureAreAnnounced()
        {
            NodeController star = Node("Star", Faction.Enemy, 10);
            var seen = new List<string>();
            Decay(star).OnInfected += () => seen.Add("infected");
            Decay(star).OnCured += () => seen.Add("cured");

            star.Infect();
            star.Infect();
            Hit(star, Faction.Enemy, 1);

            CollectionAssert.AreEqual(new[] { "infected", "infected", "cured" }, seen);
        }

        // --- −2 E/s hasta 0 (7.4) ---

        [Test]
        public void ItLosesOnePointEveryHalfSecond()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 30);
            magnetar.Infect();

            Run(magnetar, TicksPerPoint - 1);
            Assert.AreEqual(30, magnetar.Energy);
            Run(magnetar, 1);
            Assert.AreEqual(29, magnetar.Energy, "El primer punto, a los 0.5 s.");

            Run(magnetar, 50 - TicksPerPoint);
            Assert.AreEqual(28, magnetar.Energy, "2 por segundo.");
            Run(magnetar, 500);
            Assert.AreEqual(8, magnetar.Energy, "11 s: 22 puntos.");
        }

        // 17 caso 11.
        [Test]
        public void AtZeroItStaysInfectedAndWithItsOwner()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 3);
            magnetar.Infect();

            Run(magnetar, 200);

            Assert.AreEqual(0, magnetar.Energy);
            Assert.IsTrue(magnetar.HasState(NodeStateFlags.Infected));
            Assert.AreEqual(Faction.Enemy, magnetar.Faction);
        }

        // 17 caso 13.
        [Test]
        public void AnotherDecayRestartsTheEffectWithoutDoublingTheRate()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 30);
            magnetar.Infect();
            Run(magnetar, 20); // 0.4 s

            magnetar.Infect();
            Assert.AreEqual(0f, Decay(magnetar).Progress);
            Run(magnetar, 20);
            Assert.AreEqual(30, magnetar.Energy, "El reloj volvió a 0: el punto de los 0.5 s aún no toca.");

            Run(magnetar, 30); // 1 s desde el segundo Decaimiento
            Assert.AreEqual(28, magnetar.Energy, "Sigue a 2 E/s, no a 4.");
        }

        // --- Funciona con normalidad (7.4) ---

        [Test]
        public void AnInfectedStarStillGeneratesButLosesMore()
        {
            NodeController star = Node("Star", Faction.Player, 10);
            star.Infect();

            Run(star, 100); // 2 s: +1 de la Estrella N1, −4 del Decaimiento

            Assert.AreEqual(7, star.Energy);
        }

        [Test]
        public void AnInfectedMagnetarStillFires()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 20);
            magnetar.Infect();
            Proton proton = Fly(Faction.Player, 10, new Vector2(1f, 0f));

            magnetar.GetComponent<MagnetarTurret>().Tick(Tick);

            Assert.AreEqual(10 - Balance.MagnetarShotDamage, proton.Charge);
        }

        [Test]
        public void AnInfectedNodeCanSendUpgradeAndChargeButNotStartAConversion()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            NodeController other = Node("Star", Faction.Player, 0, new Vector2(3f, 0f));
            NodeController pulsar = Node("Pulsar", Faction.Player, 50, new Vector2(-3f, 0f));
            star.Infect();
            pulsar.Infect();

            Assert.AreEqual(LaunchBlock.None, ProtonLauncher.Check(star, other, Faction.Player, false, out _));
            Assert.AreEqual(ActionBlock.Infected, star.GetComponent<NodeConverter>().Check(Faction.Player, NodeType.Magnetar).Block, "8.1.");
            Assert.IsTrue(star.CheckUpgrade(Faction.Player).IsAllowed, "18 #18: infectado puede mejorar.");
            Assert.IsTrue(pulsar.GetComponent<PulsarCaster>().TryCharge(Faction.Player, PowerType.AbsoluteZero).IsAllowed);
        }

        // --- Cura (7.4) ---

        // 17 caso 12.
        [Test]
        public void ItsOwnerCuresItAndAddsTheEnergy()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 10);
            magnetar.Infect();

            Hit(magnetar, Faction.Enemy, 5);

            Assert.IsFalse(magnetar.HasState(NodeStateFlags.Infected));
            Assert.AreEqual(15, magnetar.Energy);
            Run(magnetar, 100);
            Assert.AreEqual(15, magnetar.Energy, "Curado, deja de perder.");
        }

        [Test]
        public void AHostileHitThatFallsShortCuresButKeepsTheOwner()
        {
            NodeController star = Node("Star", Faction.Enemy, 20);
            star.Infect();

            Hit(star, Faction.Player, 5);

            Assert.IsFalse(star.HasState(NodeStateFlags.Infected));
            Assert.AreEqual(Faction.Enemy, star.Faction);
            Assert.AreEqual(15, star.Energy);
        }

        [Test]
        public void AHostileHitThatCapturesTakesItWithoutInfection()
        {
            NodeController star = Node("Star", Faction.Enemy, 4);
            star.Infect();
            bool infectedAtCapture = true;
            star.OnFactionChanged += (previous, current) => infectedAtCapture = star.HasState(NodeStateFlags.Infected);

            Hit(star, Faction.Player, 10);

            Assert.AreEqual(Faction.Player, star.Faction);
            Assert.AreEqual(6, star.Energy);
            Assert.IsFalse(star.HasState(NodeStateFlags.Infected));
            Assert.IsFalse(infectedAtCapture, "La cura va antes del cambio de bando.");
        }

        // 17 caso 32.
        [Test]
        public void ANeutralMagnetarDrainsToZeroKeepsFiringAndFallsToChargeOne()
        {
            NodeController magnetar = Node("Magnetar", Faction.Neutral, 100);
            magnetar.Infect();

            Run(magnetar, 50 * 50);
            Assert.AreEqual(0, magnetar.Energy, "35 de coste vacían sus 100 en 50 s.");
            Assert.AreEqual(Faction.Neutral, magnetar.Faction);
            Assert.IsTrue(magnetar.HasState(NodeStateFlags.Infected), "No tiene dueño que lo cure.");

            Proton proton = Fly(Faction.Player, 10, new Vector2(1f, 0f));
            magnetar.GetComponent<MagnetarTurret>().Tick(Tick);
            Assert.AreEqual(10 - Balance.MagnetarShotDamage, proton.Charge, "Sigue disparando.");

            Hit(magnetar, Faction.Enemy, 1);
            Assert.AreEqual(Faction.Enemy, magnetar.Faction, "17 caso 7: a 0 cae con Carga 1.");
            Assert.IsFalse(magnetar.HasState(NodeStateFlags.Infected));
        }

        // --- Con Congelado (7.3, 14.6) ---

        [Test]
        public void FrozenItKeepsDecayingAndStaysFrozenAtZero()
        {
            NodeController star = Node("Star", Faction.Enemy, 5);
            star.Freeze();
            star.Infect();

            Run(star, 10 * TicksPerPoint);

            Assert.AreEqual(0, star.Energy);
            Assert.IsTrue(star.HasState(NodeStateFlags.Frozen), "El Decaimiento no descongela.");
            Assert.IsTrue(star.HasState(NodeStateFlags.Infected));
        }

        [Test]
        public void DecayDoesNotRestartTheAutoThaw()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 100);
            magnetar.Freeze();
            magnetar.Infect();

            Run(magnetar, AutoThawTicks - 1);
            Assert.IsTrue(magnetar.HasState(NodeStateFlags.Frozen));
            Run(magnetar, 1);

            Assert.IsFalse(magnetar.HasState(NodeStateFlags.Frozen), "30 s sin impactos: el Decaimiento no cuenta como uno.");
            Assert.AreEqual(Faction.Enemy, magnetar.Faction);
            Assert.IsTrue(magnetar.HasState(NodeStateFlags.Infected), "Se descongela todavía infectado.");
            Assert.AreEqual(40, magnetar.Energy, "100 − 60 puntos en 30 s.");
        }

        [Test]
        public void AFrozenInfectedNodeAtZeroThawsToTheNextProtonAndIsCured()
        {
            NodeController star = Node("Star", Faction.Enemy, 2);
            star.Freeze();
            star.Infect();
            Run(star, 2 * TicksPerPoint);
            Assert.AreEqual(0, star.Energy);

            Hit(star, Faction.Player, 1);

            Assert.AreEqual(Faction.Player, star.Faction, "7.3: a un protón de Carga 1 de cambiar de bando.");
            Assert.IsFalse(star.HasState(NodeStateFlags.Frozen));
            Assert.IsFalse(star.HasState(NodeStateFlags.Infected));
        }

        // --- Sobre una reconversión en curso (desviación 203) ---

        [Test]
        public void DecayOnAConvertingNodeCancelsTheConversion()
        {
            NodeController star = Node("Star", Faction.Enemy, 50);
            var converter = star.GetComponent<NodeConverter>();
            Assert.IsTrue(converter.TryConvert(Faction.Enemy, NodeType.Magnetar).IsAllowed);
            int afterCost = star.Energy;
            var cancelled = new List<NodeType>();
            converter.OnConversionCancelled += (from, to) => cancelled.Add(to);
            Run(star, 100);

            star.Infect();

            Assert.IsFalse(converter.IsConverting);
            Assert.IsFalse(star.HasState(NodeStateFlags.Converting));
            Assert.IsTrue(star.HasState(NodeStateFlags.Infected));
            CollectionAssert.AreEqual(new[] { NodeType.Magnetar }, cancelled);
            Assert.AreEqual(afterCost, star.Energy, "El coste pagado se pierde, como al capturarlo (18 #21).");

            Run(star, 100);
            Assert.AreEqual(NodeType.Star, star.Type, "Queda como estaba antes de iniciarla.");
        }

        [Test]
        public void AFrozenConvertingNodeHitByDecayAlsoLosesTheConversion()
        {
            NodeController star = Node("Star", Faction.Enemy, 50);
            var converter = star.GetComponent<NodeConverter>();
            converter.TryConvert(Faction.Enemy, NodeType.Pulsar);
            star.Freeze();

            star.Infect();

            Assert.IsFalse(converter.IsConverting);
            Assert.IsTrue(star.HasState(NodeStateFlags.Frozen));
            Assert.IsTrue(star.HasState(NodeStateFlags.Infected));
        }
    }
}
