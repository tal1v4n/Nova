using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Presentation;
using Nova.Systems;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T3-03: lanzamiento y haz de poder (7.2.2) con el objetivo válido de 7.2.1, y los casos 27,
    // 28 y 29 de la sección 17. El haz dura 0.25 s: termina en el tick 13 (0.26 s).
    public class PowerBeamTests
    {
        const float Tick = 0.02f;
        const int TicksToLand = 13;

        readonly List<GameObject> spawned = new List<GameObject>();
        PowerBeamSystem beams;
        readonly List<PowerBeam> landed = new List<PowerBeam>();
        readonly List<PowerBeam> dissipated = new List<PowerBeam>();

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
            beams = go.AddComponent<PowerBeamSystem>();
            beams.OnLanded += landed.Add;
            beams.OnDissipated += dissipated.Add;
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
            landed.Clear();
            dissipated.Clear();
        }

        NodeController Node(string type, Faction faction, int energy, Vector2 position = default, int level = -1)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            go.AddComponent<NodeFreeze>().Configure(Balance); // T3-04: los haces de Cero Absoluto congelan al caer
            node.Initialize(Definition(type), faction, energy, level >= 0 ? level : (type == "Asteroid" ? 0 : 1));
            return node;
        }

        // Púlsar con Cero Absoluto ya cargado.
        PulsarCaster ReadyPulsar(Faction faction, int energy = 70, Vector2 position = default)
        {
            NodeController node = Node("Pulsar", faction, energy, position);
            var caster = node.gameObject.AddComponent<PulsarCaster>();
            caster.Configure(Balance, new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") }, beams);
            Assert.IsTrue(caster.TryCharge(faction, PowerType.AbsoluteZero).IsAllowed);
            for (int i = 0; i < 400; i++)
                caster.Tick(Tick);
            Assert.AreEqual(PowerChargeState.Ready, caster.State);
            return caster;
        }

        void Run(int ticks)
        {
            for (int i = 0; i < ticks; i++)
                beams.Tick(Tick);
        }

        static void Capture(NodeController node, Faction by)
        {
            node.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(by, node.Energy * 2 + 1), node.Snapshot(), Balance.MagnetarResistance));
        }

        // --- Objetivo válido (7.2.1) ---

        [TestCase("Star", Faction.Enemy, true)]
        [TestCase("Pulsar", Faction.Enemy, true)]
        [TestCase("Magnetar", Faction.Enemy, true)]
        [TestCase("Magnetar", Faction.Neutral, true)]
        [TestCase("Asteroid", Faction.Neutral, false)]
        [TestCase("Asteroid", Faction.Enemy, false)]
        [TestCase("Asteroid", Faction.Player, false)]
        [TestCase("Star", Faction.Player, false)]
        [TestCase("Magnetar", Faction.Player, false)]
        public void ValidTargetIsAnyLitNodeThatIsNotYours(string type, Faction owner, bool valid)
        {
            NodeController target = Node(type, owner, 10);
            Assert.AreEqual(valid, PowerRules.IsValidTarget(target.Snapshot(), Faction.Player));
        }

        // --- Lanzamiento ---

        [Test]
        public void LaunchingEmptiesThePulsarAndStartsABeam()
        {
            PulsarCaster caster = ReadyPulsar(Faction.Player);
            NodeController target = Node("Star", Faction.Enemy, 10, new Vector2(5f, 0f));
            int energy = caster.GetComponent<NodeController>().Energy;

            Assert.IsTrue(caster.TryLaunch(Faction.Player, target).IsAllowed);

            Assert.AreEqual(PowerChargeState.Idle, caster.State);
            Assert.IsNull(caster.Power);
            Assert.AreEqual(energy, caster.GetComponent<NodeController>().Energy, "Lanzar no cuesta nada más.");
            Assert.AreEqual(1, beams.Active.Count);
            PowerBeam beam = beams.Active[0];
            Assert.AreEqual(PowerType.AbsoluteZero, beam.Type);
            Assert.AreSame(target, beam.Target);
            Assert.AreEqual(Faction.Player, beam.Launcher);
            Assert.IsTrue(caster.CheckCharge(Faction.Player, PowerType.AbsoluteZero).IsAllowed, "Puede cargar otro.");
        }

        [Test]
        public void LaunchChecksFollowTheRules()
        {
            PulsarCaster ready = ReadyPulsar(Faction.Player);
            NodeController enemy = Node("Star", Faction.Enemy, 10, new Vector2(5f, 0f));
            NodeController asteroid = Node("Asteroid", Faction.Neutral, 10, new Vector2(0f, 3f));
            NodeController own = Node("Star", Faction.Player, 10, new Vector2(0f, -3f));

            Assert.AreEqual(ActionBlock.NotOwner, ready.CheckLaunch(Faction.Enemy, enemy).Block);
            Assert.AreEqual(ActionBlock.InvalidTarget, ready.CheckLaunch(Faction.Player, asteroid).Block, "Ningún Asteroide.");
            Assert.AreEqual(ActionBlock.InvalidTarget, ready.CheckLaunch(Faction.Player, own).Block, "Ningún nodo propio.");
            Assert.AreEqual(ActionBlock.InvalidTarget, ready.CheckLaunch(Faction.Player, ready.GetComponent<NodeController>()).Block);

            ready.GetComponent<NodeController>().SetStateFlag(NodeStateFlags.Frozen, true);
            Assert.AreEqual(ActionBlock.Frozen, ready.CheckLaunch(Faction.Player, enemy).Block, "7.3: congelado no lanza.");
        }

        [Test]
        public void AChargeThatIsNotCompleteCannotBeLaunched()
        {
            NodeController node = Node("Pulsar", Faction.Player, 70);
            var caster = node.gameObject.AddComponent<PulsarCaster>();
            caster.Configure(Balance, new[] { PowerAsset("AbsoluteZero") }, beams);
            NodeController enemy = Node("Star", Faction.Enemy, 10, new Vector2(5f, 0f));

            Assert.AreEqual(ActionBlock.NotReady, caster.TryLaunch(Faction.Player, enemy).Block);
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            caster.Tick(Tick);
            Assert.AreEqual(ActionBlock.NotReady, caster.TryLaunch(Faction.Player, enemy).Block);
            Assert.AreEqual(0, beams.Active.Count);
        }

        // --- El haz (7.2.2) ---

        [TestCase(1f)]
        [TestCase(17f)]
        public void TheBeamLandsAfterAQuarterOfASecondWhateverTheDistance(float distance)
        {
            PulsarCaster caster = ReadyPulsar(Faction.Player);
            NodeController target = Node("Star", Faction.Enemy, 10, new Vector2(distance, 0f));
            caster.TryLaunch(Faction.Player, target);

            Run(TicksToLand - 1);
            CollectionAssert.IsEmpty(landed, "El efecto llega al terminar, nunca al lanzar.");

            Run(1);
            Assert.AreEqual(1, landed.Count);
            Assert.AreSame(target, landed[0].Target);
            Assert.AreEqual(0, beams.Active.Count);
        }

        [Test]
        public void ABeamIsNotAProtonAndNoMagnetarSeesIt()
        {
            var go = new GameObject("Protons");
            spawned.Add(go);
            var protons = go.AddComponent<ProtonRegistry>();
            PulsarCaster caster = ReadyPulsar(Faction.Player);
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 10, new Vector2(2f, 0f));
            magnetar.gameObject.AddComponent<MagnetarTurret>().Configure(Balance, protons);

            caster.TryLaunch(Faction.Player, Node("Star", Faction.Enemy, 10, new Vector2(4f, 0f)));

            Assert.AreEqual(0, protons.Count, "No interceptable: el haz no está entre los protones.");
            Assert.IsNull(magnetar.GetComponent<MagnetarTurret>().FindTarget());
            Run(TicksToLand);
            Assert.AreEqual(1, landed.Count);
        }

        [Test]
        public void ATargetThatBecomesTheLaunchersDissipatesTheBeam()
        {
            PulsarCaster caster = ReadyPulsar(Faction.Player);
            NodeController target = Node("Star", Faction.Enemy, 5, new Vector2(5f, 0f));
            caster.TryLaunch(Faction.Player, target);
            Run(5);

            Capture(target, Faction.Player);
            Run(TicksToLand);

            CollectionAssert.IsEmpty(landed, "17 caso 27: sin efecto.");
            Assert.AreEqual(1, dissipated.Count);
            Assert.AreEqual(PowerChargeState.Idle, caster.State, "Y el poder no vuelve a estar cargado.");
        }

        [Test]
        public void ATargetTakenByAThirdSideIsStillValid()
        {
            PulsarCaster caster = ReadyPulsar(Faction.Player);
            NodeController magnetar = Node("Magnetar", Faction.Neutral, 5, new Vector2(0f, -3f));
            caster.TryLaunch(Faction.Player, magnetar);
            Run(5);

            Capture(magnetar, Faction.Enemy);
            Run(TicksToLand);

            Assert.AreEqual(1, landed.Count, "Sigue sin ser del lanzador.");
        }

        [Test]
        public void ATargetThatDisappearsDissipatesTheBeam()
        {
            PulsarCaster caster = ReadyPulsar(Faction.Player);
            NodeController target = Node("Star", Faction.Enemy, 10, new Vector2(5f, 0f));
            caster.TryLaunch(Faction.Player, target);

            Object.DestroyImmediate(target.gameObject);
            Run(TicksToLand);

            Assert.AreEqual(1, dissipated.Count);
        }

        [Test]
        public void ThePulsarCapturedDuringTheBeamStillDeliversForItsLauncher()
        {
            PulsarCaster caster = ReadyPulsar(Faction.Player);
            NodeController pulsar = caster.GetComponent<NodeController>();
            NodeController target = Node("Star", Faction.Enemy, 10, new Vector2(5f, 0f));
            caster.TryLaunch(Faction.Player, target);
            Run(5);

            Capture(pulsar, Faction.Enemy);
            Run(TicksToLand);

            Assert.AreEqual(1, landed.Count, "17 caso 28: el haz completa su efecto.");
            Assert.AreEqual(Faction.Player, landed[0].Launcher, "A favor de quien lo lanzó.");
            Assert.AreEqual(Faction.Enemy, pulsar.Faction);
            Assert.AreEqual(PowerChargeState.Idle, caster.State, "El Púlsar cambia de bando vacío.");
        }

        // --- Orden (17 caso 29) ---

        [Test]
        public void SameTickBeamsResolveByPulsarEnergyFirst()
        {
            PulsarCaster weak = ReadyPulsar(Faction.Player, 40, new Vector2(-4f, 0f));
            PulsarCaster strong = ReadyPulsar(Faction.Enemy, 90, new Vector2(4f, 0f));

            weak.TryLaunch(Faction.Player, strong.GetComponent<NodeController>());
            strong.TryLaunch(Faction.Enemy, weak.GetComponent<NodeController>());
            Run(TicksToLand);

            Assert.AreEqual(2, landed.Count);
            Assert.AreEqual(Faction.Enemy, landed[0].Launcher, "Con empate exacto, primero el del Púlsar con más Energía.");
            Assert.AreEqual(Faction.Player, landed[1].Launcher);
        }

        [Test]
        public void SameTickBeamsWithEqualEnergyResolveInLaunchOrder()
        {
            PulsarCaster first = ReadyPulsar(Faction.Player, 70, new Vector2(-4f, 0f));
            PulsarCaster second = ReadyPulsar(Faction.Enemy, 70, new Vector2(4f, 0f));

            first.TryLaunch(Faction.Player, second.GetComponent<NodeController>());
            second.TryLaunch(Faction.Enemy, first.GetComponent<NodeController>());
            Run(TicksToLand);

            Assert.AreEqual(Faction.Player, landed[0].Launcher);
            Assert.AreEqual(Faction.Enemy, landed[1].Launcher);
        }

        [Test]
        public void AnEarlierBeamResolvesFirstWhateverTheEnergy()
        {
            PulsarCaster early = ReadyPulsar(Faction.Player, 30, new Vector2(-4f, 0f));
            PulsarCaster late = ReadyPulsar(Faction.Enemy, 90, new Vector2(4f, 0f));
            NodeController target = Node("Magnetar", Faction.Neutral, 10, new Vector2(0f, 3f));

            early.TryLaunch(Faction.Player, target);
            Run(1);
            late.TryLaunch(Faction.Enemy, target);
            Run(TicksToLand);

            Assert.AreEqual(Faction.Player, landed[0].Launcher);
            Assert.AreEqual(Faction.Enemy, landed[1].Launcher);
        }

        // --- Canal de comandos y vista ---

        [Test]
        public void ThePlayerIssuerLaunches()
        {
            var go = new GameObject("Issuer");
            spawned.Add(go);
            var issuer = go.AddComponent<PlayerCommandIssuer>();
            PulsarCaster caster = ReadyPulsar(Faction.Player);
            NodeController target = Node("Star", Faction.Enemy, 10, new Vector2(5f, 0f));

            Assert.IsTrue(issuer.LaunchPower(caster.GetComponent<NodeController>(), target).IsAllowed);
            Assert.AreEqual(1, beams.Active.Count);
        }

        [Test]
        public void TheProvisionalViewDrawsTheBeamInThePowerColour()
        {
            var view = new GameObject("BeamView").AddComponent<ProvisionalBeamView>();
            spawned.Add(view.gameObject);
            view.Configure(beams);
            PulsarCaster caster = ReadyPulsar(Faction.Player);
            caster.TryLaunch(Faction.Player, Node("Star", Faction.Enemy, 10, new Vector2(4f, 0f)));

            view.Refresh();

            Assert.AreEqual(1, view.VisibleCount);
            Assert.AreEqual(new Vector3(2f, 0f, 0f), view.Lines[0].transform.position);
            Assert.AreEqual((Color32)PowerAsset("AbsoluteZero").BeamColor, (Color32)view.Lines[0].color);

            Run(TicksToLand);
            view.Refresh();
            Assert.AreEqual(0, view.VisibleCount);
        }
    }
}
