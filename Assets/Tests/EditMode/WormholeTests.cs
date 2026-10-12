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
    // T3-06: Agujero de Gusano (7.5). La captura íntegra, que ignora la resistencia ×2 y quita
    // Congelado e Infectado, y el robo de carga en sus tres ventanas, con los casos 10, 14, 28 y 30
    // de 17.
    public class WormholeTests
    {
        const float Tick = 0.02f;

        // 0.25 s de haz: termina en el tick 13 (desviación 196).
        const int BeamTicks = 13;

        readonly List<GameObject> spawned = new List<GameObject>();
        ProtonRegistry protons;
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
            protons = go.AddComponent<ProtonRegistry>();
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

        // Nodo con los componentes de la escena de prueba.
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

        static PulsarCaster Caster(NodeController node) => node.GetComponent<PulsarCaster>();

        // Carga del todo el poder en un Púlsar con Energía de sobra.
        static void ChargeFully(NodeController pulsar, PowerType power)
        {
            PulsarCaster caster = Caster(pulsar);
            Assert.IsTrue(caster.TryCharge(pulsar.Faction, power).IsAllowed);
            for (int i = 0; i < 1000 && caster.State != PowerChargeState.Ready; i++)
                caster.Tick(Tick);
            Assert.AreEqual(PowerChargeState.Ready, caster.State);
        }

        // Púlsar de nivel 3 con el Agujero de Gusano listo.
        NodeController WormholePulsar(Faction faction, int energy = 100, Vector2 position = default)
        {
            NodeController pulsar = Node("Pulsar", faction, energy, position, level: 3);
            ChargeFully(pulsar, PowerType.Wormhole);
            return pulsar;
        }

        void Run(int ticks)
        {
            for (int i = 0; i < ticks; i++)
                beams.Tick(Tick);
        }

        // Lanza el Gusano listo de pulsar sobre target y espera a que caiga.
        void Steal(NodeController pulsar, NodeController target)
        {
            Assert.IsTrue(Caster(pulsar).TryLaunch(pulsar.Faction, target).IsAllowed);
            Run(BeamTicks);
        }

        // --- Captura íntegra (7.5) ---

        [Test]
        public void ItChangesSidesKeepingEnergyTypeAndLevel()
        {
            NodeController thief = WormholePulsar(Faction.Player, position: new Vector2(-4f, 0f));
            NodeController star = Node("Star", Faction.Enemy, 37, new Vector2(4f, 0f), level: 4);

            Assert.IsTrue(Caster(thief).TryLaunch(Faction.Player, star).IsAllowed);
            Run(BeamTicks - 1);
            Assert.AreEqual(Faction.Enemy, star.Faction, "El efecto llega al terminar el haz (7.2.2).");
            Run(1);

            Assert.AreEqual(Faction.Player, star.Faction);
            Assert.AreEqual(37, star.Energy);
            Assert.AreEqual(NodeType.Star, star.Type);
            Assert.AreEqual(4, star.Level);
            Assert.AreEqual(1, landed.Count);
        }

        // 17 caso 30.
        [Test]
        public void ItTakesAFullNeutralMagnetarWhole()
        {
            NodeController thief = WormholePulsar(Faction.Enemy, position: new Vector2(-4f, 0f));
            NodeController magnetar = Node("Magnetar", Faction.Neutral, 100, new Vector2(4f, 0f), level: 3);

            Steal(thief, magnetar);

            Assert.AreEqual(Faction.Enemy, magnetar.Faction);
            Assert.AreEqual(100, magnetar.Energy, "Ignora la resistencia ×2: no hay Carga que dividir.");
            Assert.AreEqual(3, magnetar.Level);
        }

        [Test]
        public void ItWorksOnANodeAtZero()
        {
            NodeController thief = WormholePulsar(Faction.Player, position: new Vector2(-4f, 0f));
            NodeController star = Node("Star", Faction.Enemy, 0, new Vector2(4f, 0f));

            Steal(thief, star);

            Assert.AreEqual(Faction.Player, star.Faction);
            Assert.AreEqual(0, star.Energy);
        }

        [Test]
        public void AStolenMagnetarNowShootsItsOldOwner()
        {
            NodeController thief = WormholePulsar(Faction.Player, position: new Vector2(-4f, 0f));
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 50, new Vector2(4f, 0f));
            Steal(thief, magnetar);

            var turret = magnetar.GetComponent<MagnetarTurret>();
            Assert.IsTrue(turret.IsHostile(Faction.Enemy));
            Assert.IsFalse(turret.IsHostile(Faction.Player));
        }

        [Test]
        public void StealingWithoutABandIsRejected()
        {
            NodeController star = Node("Star", Faction.Enemy, 10);
            Assert.Throws<System.ArgumentException>(() => star.Steal(Faction.Neutral));
        }

        // --- Quita los estados (7.5) ---

        // 17 caso 14.
        [Test]
        public void ItCapturesAFrozenNodeAndRemovesTheFreeze()
        {
            NodeController thief = WormholePulsar(Faction.Player, position: new Vector2(-4f, 0f));
            NodeController star = Node("Star", Faction.Enemy, 30, new Vector2(4f, 0f));
            star.Freeze();
            var reasons = new List<ThawReason>();
            star.GetComponent<NodeFreeze>().OnThawed += reasons.Add;
            bool frozenAtCapture = true;
            star.OnFactionChanged += (previous, current) => frozenAtCapture = star.HasState(NodeStateFlags.Frozen);

            Steal(thief, star);

            Assert.AreEqual(Faction.Player, star.Faction);
            Assert.AreEqual(30, star.Energy, "Íntegro: el deshielo no entra, no hay impacto.");
            Assert.IsFalse(star.HasState(NodeStateFlags.Frozen));
            Assert.IsFalse(frozenAtCapture, "El congelamiento se quita antes del cambio de bando.");
            CollectionAssert.AreEqual(new[] { ThawReason.Wormhole }, reasons);
        }

        [Test]
        public void ItRemovesTheInfection()
        {
            NodeController thief = WormholePulsar(Faction.Player, position: new Vector2(-4f, 0f));
            NodeController magnetar = Node("Magnetar", Faction.Neutral, 60, new Vector2(4f, 0f));
            magnetar.Infect();
            magnetar.Freeze();

            Steal(thief, magnetar);

            Assert.AreEqual(Faction.Player, magnetar.Faction);
            Assert.IsFalse(magnetar.HasState(NodeStateFlags.Infected));
            Assert.IsFalse(magnetar.HasState(NodeStateFlags.Frozen));
            magnetar.GetComponent<NodeDecay>().Tick(1f);
            Assert.AreEqual(60, magnetar.Energy, "Ya no decae.");
        }

        [Test]
        public void ItCancelsAConversionInProgress()
        {
            NodeController thief = WormholePulsar(Faction.Player, position: new Vector2(-4f, 0f));
            NodeController star = Node("Star", Faction.Enemy, 50, new Vector2(4f, 0f));
            var converter = star.GetComponent<NodeConverter>();
            converter.TryConvert(Faction.Enemy, NodeType.Magnetar);

            Steal(thief, star);

            Assert.AreEqual(Faction.Player, star.Faction);
            Assert.IsFalse(converter.IsConverting, "8.1: capturado a mitad, la reconversión se cancela.");
            Assert.AreEqual(NodeType.Star, star.Type);
            Assert.AreEqual(30, star.Energy, "El coste pagado se pierde.");
        }

        // --- Robo de carga (7.5) ---

        [Test]
        public void AChargeInProgressIsResetAndLost()
        {
            NodeController thief = WormholePulsar(Faction.Player, position: new Vector2(-4f, 0f));
            NodeController pulsar = Node("Pulsar", Faction.Enemy, 50, new Vector2(4f, 0f), level: 2);
            PulsarCaster victim = Caster(pulsar);
            victim.TryCharge(Faction.Enemy, PowerType.Decay);
            for (int i = 0; i < 300; i++)
                victim.Tick(Tick);
            Assert.AreEqual(PowerChargeState.Charging, victim.State);
            int energy = pulsar.Energy;

            Steal(thief, pulsar);

            Assert.AreEqual(Faction.Player, pulsar.Faction);
            Assert.AreEqual(PowerChargeState.Idle, victim.State, "La carga se resetea a 0 %.");
            Assert.AreEqual(0f, victim.Progress);
            Assert.AreEqual(energy, pulsar.Energy, "La Energía invertida se pierde; la que le quedaba, no.");
        }

        // 17 caso 10.
        [Test]
        public void AReadyPowerChangesSidesIntactAndIsUsableAtOnce()
        {
            NodeController thief = WormholePulsar(Faction.Player, position: new Vector2(-4f, 0f));
            NodeController pulsar = Node("Pulsar", Faction.Enemy, 100, new Vector2(4f, 0f), level: 2);
            ChargeFully(pulsar, PowerType.Decay);
            NodeController enemyStar = Node("Star", Faction.Enemy, 20, new Vector2(0f, 3f));

            Steal(thief, pulsar);

            PulsarCaster stolen = Caster(pulsar);
            Assert.AreEqual(PowerChargeState.Ready, stolen.State);
            Assert.AreEqual(PowerType.Decay, stolen.Power.Type);
            Assert.IsTrue(stolen.TryLaunch(Faction.Player, enemyStar).IsAllowed, "Utilizable de inmediato por el nuevo dueño.");
            Assert.AreEqual(ActionBlock.NotOwner, stolen.CheckCharge(Faction.Enemy, PowerType.AbsoluteZero).Block);
        }

        [Test]
        public void AFrozenPulsarWithAReadyPowerIsStolenThawedAndCanLaunch()
        {
            NodeController thief = WormholePulsar(Faction.Player, position: new Vector2(-4f, 0f));
            NodeController pulsar = Node("Pulsar", Faction.Enemy, 100, new Vector2(4f, 0f));
            ChargeFully(pulsar, PowerType.AbsoluteZero);
            pulsar.Freeze();
            NodeController enemyStar = Node("Star", Faction.Enemy, 20, new Vector2(0f, 3f));

            Steal(thief, pulsar);

            Assert.IsTrue(Caster(pulsar).TryLaunch(Faction.Player, enemyStar).IsAllowed);
        }

        // 17 caso 28, con el Gusano como causa de la captura.
        [Test]
        public void ABeamAlreadyLaunchedCompletesForItsLauncherAndThePulsarChangesSidesEmpty()
        {
            NodeController victimPulsar = Node("Pulsar", Faction.Enemy, 100, new Vector2(4f, 0f));
            ChargeFully(victimPulsar, PowerType.AbsoluteZero);
            NodeController playerStar = Node("Star", Faction.Player, 20, new Vector2(0f, -3f));
            NodeController thief = WormholePulsar(Faction.Player, position: new Vector2(-4f, 0f));

            Assert.IsTrue(Caster(victimPulsar).TryLaunch(Faction.Enemy, playerStar).IsAllowed);
            Run(1);
            Assert.IsTrue(Caster(thief).TryLaunch(Faction.Player, victimPulsar).IsAllowed);
            Run(BeamTicks);

            Assert.AreEqual(Faction.Player, victimPulsar.Faction);
            Assert.AreEqual(PowerChargeState.Idle, Caster(victimPulsar).State, "Cambia de bando vacío.");
            Assert.IsTrue(playerStar.HasState(NodeStateFlags.Frozen), "El haz del Enemigo completa su efecto.");
            Assert.AreEqual(2, landed.Count);
            Assert.AreEqual(Faction.Enemy, landed[0].Launcher);
        }

        // 17 caso 29 con el Gusano: dos Púlsares se roban el uno al otro el mismo tick. Los dos haces
        // siguen siendo válidos —cada objetivo sigue sin ser del lanzador del haz que le llega—, así
        // que los dos Púlsares acaban intercambiados.
        [Test]
        public void TwoWormholesTheSameTickSwapThePulsars()
        {
            NodeController strong = WormholePulsar(Faction.Player, 100, new Vector2(-4f, 0f)); // 40 tras cargar
            NodeController weak = WormholePulsar(Faction.Enemy, 80, new Vector2(4f, 0f));      // 20 tras cargar

            Caster(strong).TryLaunch(Faction.Player, weak);
            Caster(weak).TryLaunch(Faction.Enemy, strong);
            Run(BeamTicks);

            Assert.AreEqual(2, landed.Count);
            Assert.AreEqual(Faction.Player, landed[0].Launcher, "Primero el del Púlsar con más Energía.");
            Assert.AreEqual(Faction.Player, weak.Faction);
            Assert.AreEqual(Faction.Enemy, strong.Faction);
        }

        // Caso 27 con el Gusano: el segundo haz apunta a un nodo que el primero ya le dio a su lanzador.
        [Test]
        public void AWormholeOnANodeAlreadyStolenForTheLauncherDissipates()
        {
            NodeController first = WormholePulsar(Faction.Player, 100, new Vector2(-4f, 0f));
            NodeController second = WormholePulsar(Faction.Player, 100, new Vector2(-4f, 2f));
            NodeController star = Node("Star", Faction.Enemy, 20, new Vector2(4f, 0f));

            Caster(first).TryLaunch(Faction.Player, star);
            Caster(second).TryLaunch(Faction.Player, star);
            Run(BeamTicks);

            Assert.AreEqual(Faction.Player, star.Faction);
            Assert.AreEqual(1, landed.Count);
            Assert.AreEqual(1, dissipated.Count, "Lo invertido en el segundo se pierde.");
        }
    }
}
