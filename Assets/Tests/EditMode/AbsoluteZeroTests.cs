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
    // T3-04: Cero Absoluto (7.3). NodeFreeze, el haz que congela, lo que el nodo deja de hacer
    // congelado, el deshielo por impacto y el auto-deshielo de 30 s, y los casos 8, 9 y 31 de 17.
    public class AbsoluteZeroTests
    {
        const float Tick = 0.02f;

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

        // Nodo con los componentes de la escena de prueba que entran en juego al congelar.
        NodeController Node(string type, Faction faction, int energy, Vector2 position = default, int level = 1)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, type == "Asteroid" ? 0 : level);
            go.AddComponent<NodeFreeze>().Configure(Balance);
            go.AddComponent<NodeConverter>().Configure(Balance, new[] { Definition("Star"), Definition("Pulsar"), Definition("Magnetar") });
            go.AddComponent<StarGenerator>();
            go.AddComponent<MagnetarTurret>().Configure(Balance, protons);
            go.AddComponent<PulsarCaster>().Configure(Balance, new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") }, beams);
            return node;
        }

        static NodeFreeze Freezer(NodeController node) => node.GetComponent<NodeFreeze>();

        // El tick de los componentes de nodo que importan aquí, en el orden de la escena.
        static void Run(NodeController node, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                node.GetComponent<NodeFreeze>().Tick(Tick);
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

        // --- El haz congela (7.3) ---

        [Test]
        public void TheAbsoluteZeroBeamFreezesTheTargetWhenItLands()
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 70, new Vector2(-4f, 0f));
            var caster = pulsar.GetComponent<PulsarCaster>();
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            for (int i = 0; i < 400; i++)
                caster.Tick(Tick);
            NodeController target = Node("Star", Faction.Enemy, 30, new Vector2(4f, 0f));

            Assert.IsTrue(caster.TryLaunch(Faction.Player, target).IsAllowed);
            for (int i = 0; i < BeamTicks - 1; i++)
                beams.Tick(Tick);
            Assert.IsFalse(target.HasState(NodeStateFlags.Frozen), "El efecto llega al terminar el haz (7.2.2).");

            beams.Tick(Tick);
            Assert.IsTrue(target.HasState(NodeStateFlags.Frozen));
            Assert.AreEqual(Faction.Enemy, target.Faction, "Congelar no cambia de bando.");
            Assert.AreEqual(30, target.Energy, "Ni toca la Energía.");
        }

        [Test]
        public void FreezingWithoutNodeFreezeIsAWiringError()
        {
            var go = new GameObject("Bare");
            spawned.Add(go);
            var bare = go.AddComponent<NodeController>();
            bare.Initialize(Definition("Star"), Faction.Enemy, 10);
            Assert.Throws<System.InvalidOperationException>(() => bare.Freeze());
        }

        [Test]
        public void FreezeAndThawAreAnnounced()
        {
            NodeController star = Node("Star", Faction.Enemy, 10);
            var seen = new List<string>();
            Freezer(star).OnFrozen += () => seen.Add("frozen");
            Freezer(star).OnThawed += byImpact => seen.Add(byImpact ? "thawed by impact" : "thawed alone");

            star.Freeze();
            Hit(star, Faction.Player, 10);
            star.Freeze();
            Run(star, AutoThawTicks);

            CollectionAssert.AreEqual(new[] { "frozen", "thawed by impact", "frozen", "thawed alone" }, seen);
        }

        // --- Lo que no hace congelado (7.3) ---

        [Test]
        public void AFrozenStarDoesNotGenerate()
        {
            NodeController star = Node("Star", Faction.Player, 10);
            star.Freeze();

            Run(star, 200);
            Assert.AreEqual(10, star.Energy, "4 s congelada: nada.");
        }

        [Test]
        public void AFrozenStarKeepsItsProgressAndGeneratesAgainAfterThawing()
        {
            NodeController star = Node("Star", Faction.Player, 10);
            Run(star, 50); // 1 s de los 2 del nivel 1
            star.Freeze();
            Run(star, AutoThawTicks);
            Assert.IsFalse(star.HasState(NodeStateFlags.Frozen));
            Assert.AreEqual(10, star.Energy);

            Run(star, 50);
            Assert.AreEqual(11, star.Energy, "El progreso de antes de congelarse se conserva.");
        }

        [TestCase(Faction.Enemy)]
        [TestCase(Faction.Neutral)]
        public void AFrozenMagnetarDoesNotFire(Faction owner)
        {
            NodeController magnetar = Node("Magnetar", owner, 50);
            var turret = magnetar.GetComponent<MagnetarTurret>();
            Proton player = Fly(Faction.Player, 10, new Vector2(1f, 0f));
            Proton enemy = Fly(Faction.Enemy, 10, new Vector2(-1f, 0f));

            magnetar.Freeze();
            for (int i = 0; i < 100; i++)
                turret.Tick(Tick);

            Assert.IsNull(turret.FindTarget());
            Assert.AreEqual(10, player.Charge, "17 caso 31: el Magnetar congelado no dispara.");
            Assert.AreEqual(10, enemy.Charge, "Al neutral, a ninguno de los dos bandos.");
        }

        [Test]
        public void AFrozenMagnetarFiresAgainAfterThawing()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 50);
            var turret = magnetar.GetComponent<MagnetarTurret>();
            Proton proton = Fly(Faction.Player, 10, new Vector2(1f, 0f));
            magnetar.Freeze();
            Run(magnetar, AutoThawTicks);

            turret.Tick(Tick);
            Assert.AreEqual(10 - Balance.MagnetarShotDamage, proton.Charge);
        }

        [Test]
        public void AFrozenNodeCannotSend()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            NodeController other = Node("Star", Faction.Player, 0, new Vector2(3f, 0f));
            star.Freeze();

            Assert.AreEqual(LaunchBlock.Frozen, ProtonLauncher.Check(star, other, Faction.Player, false, out _));
            Assert.AreEqual(LaunchBlock.Frozen, ProtonLauncher.Check(star, other, Faction.Player, true, out _));
        }

        [Test]
        public void AFrozenNodeCannotUpgradeNorStartAConversion()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            star.Freeze();

            Assert.AreEqual(ActionBlock.Frozen, star.TryUpgrade(Faction.Player).Block);
            Assert.AreEqual(ActionBlock.Frozen, star.GetComponent<NodeConverter>().TryConvert(Faction.Player, NodeType.Magnetar).Block);
            Assert.AreEqual(1, star.Level);
            Assert.AreEqual(50, star.Energy);
        }

        [Test]
        public void AFrozenPulsarCannotChargeLaunchNorCancel()
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 70);
            NodeController enemy = Node("Star", Faction.Enemy, 10, new Vector2(4f, 0f));
            var caster = pulsar.GetComponent<PulsarCaster>();
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            for (int i = 0; i < 400; i++)
                caster.Tick(Tick);
            pulsar.Freeze();

            Assert.AreEqual(ActionBlock.Frozen, caster.TryLaunch(Faction.Player, enemy).Block);
            Assert.AreEqual(ActionBlock.Frozen, caster.TryCancel(Faction.Player).Block);
            Assert.AreEqual(PowerChargeState.Ready, caster.State, "El poder cargado sigue ahí.");
        }

        // 17 caso 9.
        [Test]
        public void AChargeInProgressPausesWhileFrozenAndResumesAfterThawing()
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 70);
            var caster = pulsar.GetComponent<PulsarCaster>();
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Run(pulsar, 100); // 2 s de 8: 5 de 20
            Assert.AreEqual(5, caster.Invested);

            pulsar.Freeze();
            Run(pulsar, AutoThawTicks - 1);
            Assert.AreEqual(5, caster.Invested, "Pausada, no perdida.");
            Assert.IsTrue(caster.IsPaused);

            Run(pulsar, 1 + 300);
            Assert.AreEqual(PowerChargeState.Ready, caster.State, "Al descongelar se reanuda donde estaba.");
        }

        // 17 caso 8.
        [Test]
        public void AConversionInProgressPausesWhileFrozenAndResumesAfterThawing()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            var converter = star.GetComponent<NodeConverter>();
            Assert.IsTrue(converter.TryConvert(Faction.Player, NodeType.Magnetar).IsAllowed);
            Run(star, 100); // 2 s de 3

            star.Freeze();
            Assert.IsTrue(star.HasState(NodeStateFlags.Converting), "14.6: Congelado convive con Reconvirtiendo.");
            Run(star, AutoThawTicks);
            Assert.AreEqual(NodeType.Star, star.Type, "Pausada durante los 30 s.");
            Assert.IsTrue(converter.IsConverting);

            Run(star, 50);
            Assert.AreEqual(NodeType.Magnetar, star.Type, "Y se reanuda donde estaba.");
        }

        // --- Deshielo por impacto (7.3) ---

        [Test]
        public void AFrozenNodeStillReceivesImpacts()
        {
            NodeController star = Node("Star", Faction.Enemy, 30);
            star.Freeze();

            ImpactOutcome outcome = Hit(star, Faction.Player, 10);

            Assert.AreEqual(ImpactResult.ThawProgress, outcome.Result);
            Assert.AreEqual(20, star.Energy);
            Assert.AreEqual(Faction.Enemy, star.Faction);
            Assert.IsTrue(star.HasState(NodeStateFlags.Frozen));
        }

        [Test]
        public void ItsOwnFactionDrainsItToo()
        {
            NodeController star = Node("Star", Faction.Enemy, 30);
            star.Freeze();

            Hit(star, Faction.Enemy, 10);

            Assert.AreEqual(20, star.Energy, "El deshielo invierte el sentido de recibir Energía, también para el dueño.");
            Assert.IsTrue(star.HasState(NodeStateFlags.Frozen));
        }

        [Test]
        public void AtZeroItThawsAndGoesToTheLastEmitterLosingTheRemainder()
        {
            NodeController star = Node("Star", Faction.Enemy, 30);
            star.Freeze();
            Hit(star, Faction.Enemy, 10);

            ImpactOutcome outcome = Hit(star, Faction.Player, 45);

            Assert.AreEqual(ImpactResult.ThawedAndCaptured, outcome.Result);
            Assert.AreEqual(Faction.Player, star.Faction);
            Assert.AreEqual(0, star.Energy, "El remanente se pierde.");
            Assert.IsFalse(star.HasState(NodeStateFlags.Frozen));
            Assert.IsFalse(star.Snapshot().IsFrozen);
        }

        [Test]
        public void ThawedByItsOwnFactionItStaysItsOwn()
        {
            NodeController star = Node("Star", Faction.Enemy, 10);
            star.Freeze();

            Hit(star, Faction.Enemy, 25);

            Assert.AreEqual(Faction.Enemy, star.Faction);
            Assert.AreEqual(0, star.Energy);
            Assert.IsFalse(star.HasState(NodeStateFlags.Frozen));
        }

        [Test]
        public void AFrozenMagnetarHalvesTheDrain()
        {
            NodeController magnetar = Node("Magnetar", Faction.Neutral, 50);
            magnetar.Freeze();

            Hit(magnetar, Faction.Player, 30);
            Assert.AreEqual(35, magnetar.Energy, "floor(30 × 0.5) = 15.");

            Hit(magnetar, Faction.Enemy, 70);
            Assert.AreEqual(Faction.Enemy, magnetar.Faction, "17 caso 31: pasa al bando del último emisor.");
            Assert.AreEqual(0, magnetar.Energy);
            Assert.IsFalse(magnetar.HasState(NodeStateFlags.Frozen));
        }

        [Test]
        public void AFrozenNodeAtZeroFallsToChargeOne()
        {
            NodeController star = Node("Star", Faction.Enemy, 0);
            star.Freeze();

            Hit(star, Faction.Player, 1);

            Assert.AreEqual(Faction.Player, star.Faction);
            Assert.IsFalse(star.HasState(NodeStateFlags.Frozen));
        }

        [Test]
        public void ThawingIsAlreadyOverWhenTheCaptureIsAnnounced()
        {
            NodeController star = Node("Star", Faction.Enemy, 10);
            star.Freeze();
            bool frozenAtCapture = true;
            star.OnFactionChanged += (previous, current) => frozenAtCapture = star.HasState(NodeStateFlags.Frozen);

            Hit(star, Faction.Player, 10);

            Assert.IsFalse(frozenAtCapture, "Unfreeze antes de ChangeFaction, como en el pseudocódigo de 7.3.");
        }

        [Test]
        public void CapturedByThawingAConversionIsCancelled()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            var converter = star.GetComponent<NodeConverter>();
            converter.TryConvert(Faction.Player, NodeType.Magnetar);
            star.Freeze();

            Hit(star, Faction.Enemy, 50);

            Assert.AreEqual(Faction.Enemy, star.Faction);
            Assert.IsFalse(converter.IsConverting, "18 #21: capturado, la reconversión se cancela.");
            Assert.AreEqual(NodeType.Star, star.Type);
        }

        [Test]
        public void ARealProtonDrainsAFrozenNode()
        {
            NodeController star = Node("Star", Faction.Enemy, 30, new Vector2(1f, 0f));
            star.Freeze();
            var go = new GameObject("Proton");
            spawned.Add(go);
            var proton = go.AddComponent<Proton>();
            proton.Launch(Vector2.zero, star, Faction.Player, 12, Balance);

            for (int i = 0; i < 30 && proton.InFlight; i++)
                proton.Tick(Tick);

            Assert.IsFalse(proton.InFlight);
            Assert.AreEqual(18, star.Energy);
            Assert.IsTrue(star.HasState(NodeStateFlags.Frozen));
        }

        // --- Auto-deshielo (7.3) ---

        [Test]
        public void ThirtySecondsWithoutImpactsThawKeepingFactionAndEnergy()
        {
            NodeController star = Node("Star", Faction.Enemy, 30);
            star.Freeze();

            Run(star, AutoThawTicks - 1);
            Assert.IsTrue(star.HasState(NodeStateFlags.Frozen));

            Run(star, 1);
            Assert.IsFalse(star.HasState(NodeStateFlags.Frozen));
            Assert.AreEqual(Faction.Enemy, star.Faction);
            Assert.AreEqual(30, star.Energy);
        }

        [Test]
        public void EachImpactRestartsTheCount()
        {
            NodeController star = Node("Star", Faction.Enemy, 30);
            star.Freeze();

            Run(star, 1450); // segundo 29
            Hit(star, Faction.Player, 1);
            Assert.AreEqual(0f, Freezer(star).IdleTime);

            Run(star, AutoThawTicks - 1);
            Assert.IsTrue(star.HasState(NodeStateFlags.Frozen), "Golpeado en el 29, sigue congelado pasado el 59.");

            Run(star, 1);
            Assert.IsFalse(star.HasState(NodeStateFlags.Frozen));
            Assert.AreEqual(29, star.Energy);
        }

        [Test]
        public void AnImpactFromItsOwnFactionAlsoRestartsTheCount()
        {
            NodeController star = Node("Star", Faction.Enemy, 30);
            star.Freeze();

            Run(star, 1000);
            Hit(star, Faction.Enemy, 1);
            Run(star, AutoThawTicks - 1);

            Assert.IsTrue(star.HasState(NodeStateFlags.Frozen), "Cada impacto, de cualquier facción.");
        }

        [Test]
        public void AnotherAbsoluteZeroOnAFrozenNodeRestartsTheCount()
        {
            NodeController star = Node("Star", Faction.Enemy, 30);
            star.Freeze();

            Run(star, 1000);
            star.Freeze();
            Assert.AreEqual(0f, Freezer(star).IdleTime);
            Run(star, AutoThawTicks - 1);
            Assert.IsTrue(star.HasState(NodeStateFlags.Frozen), "El haz de Cero Absoluto también es un impacto (14.1).");

            Run(star, 1);
            Assert.IsFalse(star.HasState(NodeStateFlags.Frozen));
        }

        [Test]
        public void AnUnfrozenNodeDoesNotCount()
        {
            NodeController star = Node("Star", Faction.Enemy, 30);
            Run(star, 100);
            Assert.AreEqual(0f, Freezer(star).IdleTime);
        }
    }
}
