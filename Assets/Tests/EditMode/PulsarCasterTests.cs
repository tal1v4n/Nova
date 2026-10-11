using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Presentation;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T3-02: PowerRules y PulsarCaster (7.1, 7.2), la carga acoplada a la Energía, la pausa, la
    // cancelación con devolución del 50 % y lo que pasa con la carga al capturar el Púlsar (7.5).
    // Cero Absoluto: 20 de coste en 8 s, un punto cada 0.4 s.
    public class PulsarCasterTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        static PowerDefinition[] AllPowers => new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") };

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
        }

        PulsarCaster Pulsar(int energy, int level = 1, Faction faction = Faction.Player, string type = "Pulsar", PowerDefinition[] powers = null)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, level);
            var caster = go.AddComponent<PulsarCaster>();
            caster.Configure(Balance, powers ?? AllPowers);
            return caster;
        }

        static NodeController NodeOf(PulsarCaster caster) => caster.GetComponent<NodeController>();

        static void Run(PulsarCaster caster, float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds / Tick);
            for (int i = 0; i < ticks; i++)
                caster.Tick(Tick);
        }

        static void Capture(NodeController node, Faction by)
        {
            node.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(by, node.Energy + 1), node.Snapshot(), Balance.MagnetarResistance));
        }

        // --- PowerRules ---

        static NodeSnapshot Snap(Faction faction = Faction.Player, NodeType type = NodeType.Pulsar, int level = 1,
                                 bool frozen = false, bool converting = false, bool charge = false, int energy = 50) =>
            new NodeSnapshot(faction, type, energy, 100, isFrozen: frozen, level: level, isConverting: converting, hasPowerCharge: charge);

        [Test]
        public void CheckChargeFollowsTheRulesInOrder()
        {
            Assert.AreEqual(ActionBlock.NotOwner, PowerRules.CheckCharge(Snap(Faction.Enemy), Faction.Player, 1, 20).Block);
            Assert.AreEqual(ActionBlock.NotOwner, PowerRules.CheckCharge(Snap(Faction.Neutral), Faction.Neutral, 1, 20).Block);
            Assert.AreEqual(ActionBlock.NotPulsar, PowerRules.CheckCharge(Snap(type: NodeType.Star), Faction.Player, 1, 20).Block);
            Assert.AreEqual(ActionBlock.LevelTooLow, PowerRules.CheckCharge(Snap(level: 1), Faction.Player, 2, 35).Block);
            Assert.AreEqual(ActionBlock.Frozen, PowerRules.CheckCharge(Snap(frozen: true), Faction.Player, 1, 20).Block);
            Assert.AreEqual(ActionBlock.Converting, PowerRules.CheckCharge(Snap(converting: true), Faction.Player, 1, 20).Block);
            Assert.AreEqual(ActionBlock.PowerCharge, PowerRules.CheckCharge(Snap(charge: true), Faction.Player, 1, 20).Block);
        }

        [Test]
        public void ChargingDoesNotNeedEnergyUpFront()
        {
            ActionCheck check = PowerRules.CheckCharge(Snap(energy: 0), Faction.Player, 1, 20);
            Assert.IsTrue(check.IsAllowed, "7.1: sin Energía la carga empieza pausada.");
            Assert.AreEqual(20, check.Cost);
        }

        [Test]
        public void CheckCancelNeedsAChargeOfYourOwnAndNoIce()
        {
            Assert.AreEqual(ActionBlock.NotOwner, PowerRules.CheckCancel(Snap(Faction.Enemy, charge: true), Faction.Player).Block);
            Assert.AreEqual(ActionBlock.NoPowerCharge, PowerRules.CheckCancel(Snap(), Faction.Player).Block);
            Assert.AreEqual(ActionBlock.Frozen, PowerRules.CheckCancel(Snap(frozen: true, charge: true), Faction.Player).Block);
            Assert.IsTrue(PowerRules.CheckCancel(Snap(charge: true), Faction.Player).IsAllowed);
        }

        [Test]
        public void InvestedFollowsTheChargeTimeRoundingDown()
        {
            Assert.AreEqual(0, PowerRules.InvestedAt(20, 0, 8_000_000));
            Assert.AreEqual(0, PowerRules.InvestedAt(20, 399_999, 8_000_000));
            Assert.AreEqual(1, PowerRules.InvestedAt(20, 400_000, 8_000_000));
            Assert.AreEqual(10, PowerRules.InvestedAt(20, 4_000_000, 8_000_000));
            Assert.AreEqual(20, PowerRules.InvestedAt(20, 9_000_000, 8_000_000));
        }

        [TestCase(5, 20, 8_000_000L)]
        [TestCase(1, 35, 12_000_000L)]
        [TestCase(34, 35, 12_000_000L)]
        [TestCase(59, 60, 16_000_000L)]
        public void ProgressForIsTheFirstInstantWithThatMuchInvested(int invested, int cost, long charge)
        {
            long progress = PowerRules.ProgressFor(invested, cost, charge);
            Assert.AreEqual(invested, PowerRules.InvestedAt(cost, progress, charge));
            Assert.AreEqual(invested - 1, PowerRules.InvestedAt(cost, progress - 1, charge));
        }

        [TestCase(15, 7)]
        [TestCase(20, 10)]
        [TestCase(1, 0)]
        [TestCase(0, 0)]
        public void CancelRefundIsHalfRoundedDown(int invested, int refund)
        {
            Assert.AreEqual(refund, PowerRules.CancelRefund(invested, 0.5f));
        }

        // --- Carga ---

        [Test]
        public void ChargingDrainsTheEnergyAsTheBarFills()
        {
            PulsarCaster caster = Pulsar(100);
            Assert.IsTrue(caster.TryCharge(Faction.Player, PowerType.AbsoluteZero).IsAllowed);
            Assert.AreEqual(100, NodeOf(caster).Energy, "No se cobra nada al empezar.");

            Run(caster, 4f);

            Assert.AreEqual(PowerChargeState.Charging, caster.State);
            Assert.AreEqual(10, caster.Invested);
            Assert.AreEqual(90, NodeOf(caster).Energy);
            Assert.AreEqual(0.5f, caster.Progress, 1e-6f);
            Assert.IsFalse(caster.IsPaused);
        }

        [TestCase("AbsoluteZero", PowerType.AbsoluteZero, 1, 8f, 20)]
        [TestCase("Decay", PowerType.Decay, 2, 12f, 35)]
        [TestCase("Wormhole", PowerType.Wormhole, 3, 16f, 60)]
        public void AFullChargeTakesItsChargeTimeAndHoldsIndefinitely(string asset, PowerType type, int level, float seconds, int cost)
        {
            PulsarCaster caster = Pulsar(100, level);
            caster.TryCharge(Faction.Player, type);

            Run(caster, seconds - Tick);
            Assert.AreEqual(PowerChargeState.Charging, caster.State, "Un tick antes, todavía no.");

            Run(caster, Tick);
            Assert.AreEqual(PowerChargeState.Ready, caster.State);
            Assert.AreEqual(cost, caster.Invested);
            Assert.AreEqual(100 - cost, NodeOf(caster).Energy);

            Run(caster, 60f);
            Assert.AreEqual(PowerChargeState.Ready, caster.State, "7.1: se mantiene hasta lanzarlo.");
            Assert.AreEqual(100 - cost, NodeOf(caster).Energy);
        }

        [Test]
        public void WithoutEnergyTheChargePausesAndResumesWhereItWas()
        {
            PulsarCaster caster = Pulsar(5);
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);

            Run(caster, 20f);
            Assert.AreEqual(PowerChargeState.Charging, caster.State);
            Assert.AreEqual(5, caster.Invested, "Lo invertido no se pierde.");
            Assert.AreEqual(0, NodeOf(caster).Energy);
            Assert.AreEqual(0.25f, caster.Progress, 1e-6f);
            Assert.IsTrue(caster.IsPaused);

            NodeOf(caster).AddEnergy(50);
            Run(caster, 6f - Tick);
            Assert.IsFalse(caster.IsPaused);
            Assert.AreEqual(PowerChargeState.Charging, caster.State, "Sigue desde 2 s: le quedan 6.");

            Run(caster, Tick);
            Assert.AreEqual(PowerChargeState.Ready, caster.State);
            Assert.AreEqual(35, NodeOf(caster).Energy);
        }

        [Test]
        public void FrozenPausesTheChargeWithoutLosingIt()
        {
            PulsarCaster caster = Pulsar(100);
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Run(caster, 2f);
            NodeOf(caster).SetStateFlag(NodeStateFlags.Frozen, true);

            Run(caster, 10f);
            Assert.AreEqual(5, caster.Invested, "17 caso 9: congelado, la carga se pausa.");
            Assert.IsTrue(caster.IsPaused);

            NodeOf(caster).SetStateFlag(NodeStateFlags.Frozen, false);
            Run(caster, 6f);
            Assert.AreEqual(PowerChargeState.Ready, caster.State, "Al descongelar se reanuda donde estaba.");
        }

        [Test]
        public void OnlyOnePowerAtATime()
        {
            PulsarCaster caster = Pulsar(100, 3);
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Assert.AreEqual(ActionBlock.PowerCharge, caster.TryCharge(Faction.Player, PowerType.Decay).Block);

            Run(caster, 8f);
            Assert.AreEqual(ActionBlock.PowerCharge, caster.TryCharge(Faction.Player, PowerType.Wormhole).Block,
                "Tampoco con el poder ya cargado.");
            Assert.AreEqual(PowerType.AbsoluteZero, caster.Power.Type);
        }

        [TestCase(1, PowerType.Decay)]
        [TestCase(1, PowerType.Wormhole)]
        [TestCase(2, PowerType.Wormhole)]
        public void TheLevelUnlocksEachPower(int level, PowerType type)
        {
            Assert.AreEqual(ActionBlock.LevelTooLow, Pulsar(100, level).CheckCharge(Faction.Player, type).Block);
        }

        [Test]
        public void ALevelThreePulsarKeepsAllThree()
        {
            PulsarCaster caster = Pulsar(100, 3);
            Assert.IsTrue(caster.CheckCharge(Faction.Player, PowerType.AbsoluteZero).IsAllowed);
            Assert.IsTrue(caster.CheckCharge(Faction.Player, PowerType.Decay).IsAllowed);
            Assert.IsTrue(caster.CheckCharge(Faction.Player, PowerType.Wormhole).IsAllowed);
        }

        [Test]
        public void OnlyAPulsarCharges()
        {
            PulsarCaster caster = Pulsar(50, 1, Faction.Player, "Star");
            Assert.AreEqual(ActionBlock.NotPulsar, caster.TryCharge(Faction.Player, PowerType.AbsoluteZero).Block);
        }

        [Test]
        public void APowerItDoesNotOfferIsUnavailable()
        {
            PulsarCaster caster = Pulsar(100, 3, powers: new[] { PowerAsset("AbsoluteZero") });
            Assert.AreEqual(ActionBlock.Unavailable, caster.CheckCharge(Faction.Player, PowerType.Decay).Block);
            Assert.AreEqual(ActionBlock.NotOwner, caster.CheckCharge(Faction.Enemy, PowerType.Decay).Block);
        }

        [Test]
        public void NoChargeWhileFrozenOrConverting()
        {
            PulsarCaster frozen = Pulsar(100);
            NodeOf(frozen).SetStateFlag(NodeStateFlags.Frozen, true);
            Assert.AreEqual(ActionBlock.Frozen, frozen.TryCharge(Faction.Player, PowerType.AbsoluteZero).Block);

            PulsarCaster converting = Pulsar(100);
            NodeOf(converting).SetStateFlag(NodeStateFlags.Converting, true);
            Assert.AreEqual(ActionBlock.Converting, converting.TryCharge(Faction.Player, PowerType.AbsoluteZero).Block);
        }

        // --- Cancelación ---

        [Test]
        public void CancellingRefundsHalfOfWhatWasInvested()
        {
            PulsarCaster caster = Pulsar(100);
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Run(caster, 2.8f);
            Assert.AreEqual(7, caster.Invested);
            int refund = -1;
            caster.OnCancelled += (type, amount) => refund = amount;

            Assert.IsTrue(caster.TryCancel(Faction.Player).IsAllowed);

            Assert.AreEqual(3, refund, "floor(7 × 0.5).");
            Assert.AreEqual(96, NodeOf(caster).Energy);
            Assert.AreEqual(PowerChargeState.Idle, caster.State);
            Assert.AreEqual(0f, caster.Progress);
            Assert.IsNull(caster.Power);
        }

        [Test]
        public void ACompletePowerCanBeCancelledToo()
        {
            PulsarCaster caster = Pulsar(100);
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Run(caster, 8f);

            caster.TryCancel(Faction.Player);

            Assert.AreEqual(90, NodeOf(caster).Energy, "18 #19: lanzarlo o cancelarlo. Vuelven 10 de 20.");
            Assert.IsTrue(caster.CheckCharge(Faction.Player, PowerType.AbsoluteZero).IsAllowed);
        }

        [Test]
        public void TheRefundRespectsTheCap()
        {
            PulsarCaster caster = Pulsar(100);
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Run(caster, 4f);
            NodeOf(caster).AddEnergy(10);
            Assert.AreEqual(100, NodeOf(caster).Energy);

            caster.TryCancel(Faction.Player);

            Assert.AreEqual(100, NodeOf(caster).Energy, "7.1: respetando el tope.");
        }

        [Test]
        public void NothingToCancelOrNotYours()
        {
            PulsarCaster caster = Pulsar(100);
            Assert.AreEqual(ActionBlock.NoPowerCharge, caster.TryCancel(Faction.Player).Block);

            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Run(caster, 4f);
            Assert.AreEqual(ActionBlock.NotOwner, caster.TryCancel(Faction.Enemy).Block);
            Assert.AreEqual(10, caster.Invested);
        }

        // --- Captura (7.5, 17 caso 10) ---

        [Test]
        public void CapturedMidChargeLosesTheCharge()
        {
            PulsarCaster caster = Pulsar(100);
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Run(caster, 4f);
            PowerType? lost = null;
            caster.OnLost += type => lost = type;

            Capture(NodeOf(caster), Faction.Enemy);

            Assert.AreEqual(Faction.Enemy, NodeOf(caster).Faction);
            Assert.AreEqual(PowerChargeState.Idle, caster.State, "La carga se resetea a 0 %.");
            Assert.AreEqual(0, caster.Invested);
            Assert.AreEqual(PowerType.AbsoluteZero, lost);
        }

        [Test]
        public void CapturedWithThePowerReadyHandsItToTheNewOwner()
        {
            PulsarCaster caster = Pulsar(100);
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Run(caster, 8f);

            Capture(NodeOf(caster), Faction.Enemy);

            Assert.AreEqual(PowerChargeState.Ready, caster.State, "Cambia de bando con el poder intacto.");
            Assert.AreEqual(PowerType.AbsoluteZero, caster.Power.Type);
            Assert.IsTrue(caster.CheckCancel(Faction.Enemy).IsAllowed, "Es del nuevo dueño.");
            Assert.AreEqual(ActionBlock.NotOwner, caster.CheckCancel(Faction.Player).Block);
        }

        // --- Lo que la carga bloquea y lo que no ---

        [Test]
        public void ConversionIsBlockedWhileChargingAndWithThePowerReady()
        {
            PulsarCaster caster = Pulsar(100);
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Assert.AreEqual(ActionBlock.PowerCharge,
                ConversionRules.CheckConversion(NodeOf(caster).Snapshot(), Faction.Player, NodeType.Star, 20).Block);

            Run(caster, 8f);
            Assert.AreEqual(ActionBlock.PowerCharge,
                ConversionRules.CheckConversion(NodeOf(caster).Snapshot(), Faction.Player, NodeType.Star, 20).Block, "18 #19.");
        }

        [Test]
        public void UpgradingWhileChargingIsAllowed()
        {
            PulsarCaster caster = Pulsar(100);
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Run(caster, 2f);

            Assert.IsTrue(NodeOf(caster).TryUpgrade(Faction.Player).IsAllowed, "18 #18.");
            Assert.AreEqual(PowerChargeState.Charging, caster.State);
        }

        // --- Canal de comandos ---

        [Test]
        public void ThePlayerIssuerChargesAndCancelsOnlyItsOwnPulsars()
        {
            var go = new GameObject("Issuer");
            spawned.Add(go);
            var issuer = go.AddComponent<PlayerCommandIssuer>();
            PulsarCaster mine = Pulsar(100);
            PulsarCaster theirs = Pulsar(100, 1, Faction.Enemy);

            Assert.IsTrue(issuer.ChargePower(NodeOf(mine), PowerType.AbsoluteZero).IsAllowed);
            Assert.AreEqual(ActionBlock.NotOwner, issuer.ChargePower(NodeOf(theirs), PowerType.AbsoluteZero).Block);
            Assert.IsTrue(issuer.CancelCharge(NodeOf(mine)).IsAllowed);
            Assert.AreEqual(PowerChargeState.Idle, mine.State);
        }

        // --- Vista provisional ---

        [Test]
        public void TheBarFillsInWholePixelsInThePowerColour()
        {
            PulsarCaster caster = Pulsar(100);
            var view = caster.gameObject.AddComponent<ProvisionalPulsarView>();
            view.Refresh();
            Assert.IsFalse(view.Back.enabled, "En reposo no hay barra.");

            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Run(caster, 4f);
            view.Refresh();

            Assert.IsTrue(view.Back.enabled);
            Assert.IsTrue(view.Fill.enabled);
            Assert.AreEqual(24f, view.Fill.transform.localScale.x, 1e-4f, "La mitad de 48 px.");
            Assert.AreEqual((Color32)PowerAsset("AbsoluteZero").BeamColor, (Color32)view.Fill.color);
            Assert.AreEqual(7, ProvisionalPulsarView.FilledPixels(0.15f, 48), "Píxeles enteros, hacia abajo.");
        }

        [Test]
        public void ThePausedBarIsHalfTransparent()
        {
            PulsarCaster caster = Pulsar(2);
            var view = caster.gameObject.AddComponent<ProvisionalPulsarView>();
            caster.TryCharge(Faction.Player, PowerType.AbsoluteZero);
            Run(caster, 4f);

            view.Refresh();

            Assert.IsTrue(caster.IsPaused);
            Assert.AreEqual(0.5f, view.Fill.color.a, 1e-6f);
        }
    }
}
