using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Presentation;
using Nova.Systems;
using Nova.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T3-07: rueda de poderes del Púlsar en el panel de clic derecho (13.3 punto 6, 13.5), el modo
    // apuntado (13.1, 13.5) y su atenuación (7.2.1). Se apunta con clic en la ranura brillante
    // (desviación 209) y se cancela con «Cancelar +N» (desviación 210).
    public class PowerWheelTests
    {
        const float Tick = 0.02f;
        const string FontPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";

        readonly List<GameObject> spawned = new List<GameObject>();
        NodeRegistry registry;
        SelectionManager selection;
        PlayerCommandIssuer issuer;
        InputRouter router;
        IgnitionMenuState menu;
        InfoPanelState panel;
        SendLineState sendLine;
        PowerAimState aim;
        PowerBeamSystem beams;
        NodeInfoPanel infoView;
        PulsarPowerWheel wheel;
        ProvisionalAimView aimView;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        static UiText Text =>
            AssetDatabase.LoadAssetAtPath<UiText>("Assets/Data/UiText.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            registry = go.AddComponent<NodeRegistry>();
            beams = go.AddComponent<PowerBeamSystem>();
            var pool = go.AddComponent<ProtonPool>();
            var launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, pool);

            selection = go.AddComponent<SelectionManager>();
            selection.Configure(registry);
            issuer = go.AddComponent<PlayerCommandIssuer>();
            issuer.Configure(launcher);
            menu = go.AddComponent<IgnitionMenuState>();
            panel = go.AddComponent<InfoPanelState>();
            aim = go.AddComponent<PowerAimState>();
            sendLine = go.AddComponent<SendLineState>();
            sendLine.Configure(selection, menu, aim);
            router = go.AddComponent<InputRouter>();
            router.Configure(Balance, selection, issuer, menu, panel, null, sendLine, aim);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            infoView = go.AddComponent<NodeInfoPanel>();
            infoView.Configure(panel, font, Text);
            infoView.Subscribe();
            wheel = go.AddComponent<PulsarPowerWheel>();
            wheel.Configure(panel, aim, font, Text);
            wheel.Subscribe();
            aimView = go.AddComponent<ProvisionalAimView>();
            aimView.Configure(aim, registry);
        }

        [TearDown]
        public void TearDown()
        {
            wheel.Unsubscribe();
            infoView.Unsubscribe();
            aim.End();
            panel.Close();
            selection.Clear();
            registry.Clear();
            foreach (GameObject go in spawned)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            spawned.Clear();
        }

        NodeController Node(string type, Faction faction, int energy, Vector2 position, int level = 1)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, type == "Asteroid" ? 0 : level);
            go.AddComponent<NodeFreeze>().Configure(Balance);
            go.AddComponent<PulsarCaster>().Configure(Balance, new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") }, beams);
            registry.Register(node);
            return node;
        }

        static PulsarCaster Caster(NodeController node) => node.GetComponent<PulsarCaster>();

        static void Charge(NodeController pulsar, int ticks)
        {
            for (int i = 0; i < ticks; i++)
                Caster(pulsar).Tick(Tick);
        }

        void ClickAt(Vector2 world, NodeController node = null)
        {
            router.PressLeft(world, node);
            router.ReleaseLeft(world, node);
        }

        void Click(NodeController node) => ClickAt(node.transform.position, node);

        void RightClick(NodeController node)
        {
            Vector2 at = node != null ? (Vector2)node.transform.position : new Vector2(5f, 5f);
            router.PressRight(at, node);
            router.ReleaseRight(at, node);
        }

        Vector2 SlotAt(int index) => panel.Center + InfoPanelState.WheelSlotOffsets[index];
        Vector2 CancelAt => panel.Center + InfoPanelState.CancelOffset;

        // Púlsar propio de nivel 1 con el panel abierto.
        NodeController OpenPulsar(int level = 1, int energy = 100)
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, energy, new Vector2(-4f, 0f), level);
            RightClick(pulsar);
            Assert.AreSame(pulsar, panel.Node);
            return pulsar;
        }

        // Púlsar con Cero Absoluto listo y el panel abierto.
        NodeController ReadyPulsar()
        {
            NodeController pulsar = OpenPulsar();
            ClickAt(SlotAt(0));
            Charge(pulsar, 400);
            Assert.AreEqual(PowerChargeState.Ready, Caster(pulsar).State);
            return pulsar;
        }

        // --- La rueda en el panel (13.3 punto 6, 13.5) ---

        [Test]
        public void AnOwnPulsarShowsTheThreeSlotsInFixedOrder()
        {
            OpenPulsar(level: 1);

            Assert.IsTrue(wheel.IsVisible);
            Assert.AreEqual("Poderes", wheel.Header);
            Assert.AreEqual("1 · 20", wheel.SlotText(0));
            Assert.AreEqual(PulsarPowerWheel.SlotLook.Enabled, wheel.Slot(0));
            Assert.AreEqual("Nv 2", wheel.SlotText(1), "Candado con el nivel requerido.");
            Assert.AreEqual(PulsarPowerWheel.SlotLook.Locked, wheel.Slot(1));
            Assert.AreEqual("Nv 3", wheel.SlotText(2));
            Assert.IsFalse(wheel.CancelVisible);
        }

        [Test]
        public void ALevelThreePulsarUnlocksAllThree()
        {
            OpenPulsar(level: 3);

            Assert.AreEqual("2 · 35", wheel.SlotText(1));
            Assert.AreEqual("3 · 60", wheel.SlotText(2));
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(PulsarPowerWheel.SlotLook.Enabled, wheel.Slot(i));
        }

        [Test]
        public void UpgradingUnlocksTheNextSlotWithThePanelOpen()
        {
            NodeController pulsar = OpenPulsar(level: 1);
            Assert.IsTrue(pulsar.TryUpgrade(Faction.Player).IsAllowed);
            Assert.AreEqual(PulsarPowerWheel.SlotLook.Enabled, wheel.Slot(1));
        }

        [TestCase("Pulsar", Faction.Enemy)]
        [TestCase("Star", Faction.Player)]
        [TestCase("Magnetar", Faction.Player)]
        public void OnlyAnOwnPulsarHasTheWheel(string type, Faction owner)
        {
            NodeController node = Node(type, owner, 50, new Vector2(3f, 0f));
            RightClick(node);

            Assert.IsTrue(infoView.IsVisible);
            Assert.IsFalse(wheel.IsVisible);
            ClickAt(SlotAt(0));
            Assert.IsFalse(Caster(node).HasPowerCharge, "Sin rueda, su sitio no es un botón.");
        }

        [Test]
        public void TheSlotsSitInsideThePanelOnWholePixels()
        {
            OpenPulsar();
            for (int i = 0; i < 3; i++)
                Assert.Less(Vector2.Distance(SlotAt(i), wheel.SlotPosition(i)), 1e-5f);
            Assert.Less(Vector2.Distance(CancelAt, wheel.CancelPosition), 1e-5f);

            foreach (float v in new[] { InfoPanelState.Height, InfoPanelState.WheelSlotSize.x, InfoPanelState.WheelSlotSize.y,
                         InfoPanelState.RowY(6), InfoPanelState.RowY(7), InfoPanelState.RowY(8), InfoPanelState.WheelSlotOffsets[0].x })
                Assert.AreEqual(Mathf.Round(v * 64f), v * 64f, 1e-4f, "Píxel exacto a 64 PPU.");

            float bottom = InfoPanelState.CancelOffset.y - InfoPanelState.ButtonSize.y / 2f;
            Assert.Greater(bottom, -InfoPanelState.Height / 2f, "La última fila cabe en el panel.");
            float right = InfoPanelState.WheelSlotOffsets[2].x + InfoPanelState.WheelSlotSize.x / 2f;
            Assert.LessOrEqual(right, InfoPanelState.ButtonSize.x / 2f + 1e-5f);
            Assert.AreEqual(-1, panel.WheelSlotAt(panel.Center + InfoPanelState.ButtonOffset), "La mejora es otro blanco.");
        }

        // --- Cargar y cancelar (7.1) ---

        [Test]
        public void ClickingASlotStartsTheCharge()
        {
            NodeController pulsar = OpenPulsar();

            ClickAt(SlotAt(0));

            Assert.AreEqual(PowerChargeState.Charging, Caster(pulsar).State);
            Assert.AreEqual(PowerType.AbsoluteZero, Caster(pulsar).Power.Type);
            Assert.AreEqual(PulsarPowerWheel.SlotLook.Charging, wheel.Slot(0));
            Assert.AreEqual("Cero Absoluto · 0%", wheel.Header);
            Assert.AreSame(pulsar, panel.Node, "El clic en el panel es suyo.");
            Assert.IsFalse(selection.HasSelection);

            Charge(pulsar, 200);
            Assert.AreEqual("Cero Absoluto · 50%", wheel.Header);
        }

        [Test]
        public void WhileChargingTheOtherSlotsAreDisabled()
        {
            NodeController pulsar = OpenPulsar(level: 3);
            ClickAt(SlotAt(0));

            Assert.AreEqual(PulsarPowerWheel.SlotLook.Disabled, wheel.Slot(1));
            ClickAt(SlotAt(2));
            Assert.AreEqual(PowerType.AbsoluteZero, Caster(pulsar).Power.Type, "Un poder a la vez.");
        }

        [Test]
        public void ALockedSlotDoesNothing()
        {
            NodeController pulsar = OpenPulsar(level: 1);
            ClickAt(SlotAt(2));
            Assert.IsFalse(Caster(pulsar).HasPowerCharge);
        }

        [Test]
        public void APausedChargeSaysSo()
        {
            NodeController pulsar = OpenPulsar(energy: 3);
            ClickAt(SlotAt(0));
            Charge(pulsar, 100);

            Assert.IsTrue(Caster(pulsar).IsPaused);
            Assert.AreEqual("Cero Absoluto · pausado", wheel.Header);
        }

        [Test]
        public void CancelShowsTheRefundAndGivesItBack()
        {
            NodeController pulsar = OpenPulsar(energy: 50);
            ClickAt(SlotAt(0));
            Charge(pulsar, 200); // 4 s de 8: 10 invertidos

            Assert.IsTrue(wheel.CancelVisible);
            Assert.AreEqual("Cancelar · +5", wheel.CancelText);

            ClickAt(CancelAt);

            Assert.IsFalse(Caster(pulsar).HasPowerCharge);
            Assert.AreEqual(45, pulsar.Energy, "50 − 10 + floor(10 × 0.5).");
            Assert.IsFalse(wheel.CancelVisible);
            Assert.AreEqual("Poderes", wheel.Header);
        }

        [Test]
        public void AReadyPowerCanBeCancelledToo()
        {
            NodeController pulsar = ReadyPulsar();
            Assert.AreEqual("Cancelar · +10", wheel.CancelText);

            ClickAt(CancelAt);

            Assert.IsFalse(Caster(pulsar).HasPowerCharge, "18 #19.");
        }

        [Test]
        public void AFrozenPulsarCannotCancel()
        {
            NodeController pulsar = ReadyPulsar();
            pulsar.Freeze();

            ClickAt(CancelAt);

            Assert.AreEqual(PowerChargeState.Ready, Caster(pulsar).State, "7.3: congelado, el cuerpo queda inerte.");
        }

        // --- Poder listo y modo apuntado (13.5) ---

        [Test]
        public void AReadyPowerGlowsButDoesNotAimByItself()
        {
            ReadyPulsar();

            Assert.AreEqual(PulsarPowerWheel.SlotLook.Ready, wheel.Slot(0));
            Assert.AreEqual(PowerAsset("AbsoluteZero").BeamColor, wheel.SlotColor(0));
            Assert.AreEqual("Cero Absoluto listo", wheel.Header);
            Assert.IsFalse(aim.IsAiming, "Desviación 209: solo con clic en la ranura.");
        }

        [Test]
        public void ClickingTheGlowingSlotAims()
        {
            NodeController pulsar = ReadyPulsar();

            ClickAt(SlotAt(0));

            Assert.IsTrue(aim.IsAiming);
            Assert.AreSame(pulsar, aim.Pulsar);
            Assert.AreEqual("Cero Absoluto: elige objetivo", wheel.Header);
            Assert.AreEqual(PowerChargeState.Ready, Caster(pulsar).State, "Apuntar no lanza.");
        }

        [Test]
        public void ClickingAValidTargetLaunchesAndStopsAiming()
        {
            NodeController pulsar = ReadyPulsar();
            NodeController enemy = Node("Star", Faction.Enemy, 20, new Vector2(5f, 3f));
            ClickAt(SlotAt(0));

            Click(enemy);

            Assert.AreEqual(1, beams.Active.Count);
            Assert.AreSame(enemy, beams.Active[0].Target);
            Assert.AreEqual(PowerChargeState.Idle, Caster(pulsar).State);
            Assert.IsFalse(aim.IsAiming);
            Assert.AreEqual("Poderes", wheel.Header);
        }

        [Test]
        public void TheNeutralMagnetarIsAValidTargetFromAnyDistance()
        {
            ReadyPulsar();
            NodeController neutral = Node("Magnetar", Faction.Neutral, 50, new Vector2(7f, -4f));
            ClickAt(SlotAt(0));

            Click(neutral);

            Assert.AreEqual(1, beams.Active.Count, "Sin alcance (7.2).");
        }

        [TestCase("Star", Faction.Player)]
        [TestCase("Asteroid", Faction.Neutral)]
        [TestCase("Asteroid", Faction.Enemy)]
        [TestCase("Asteroid", Faction.Player)]
        public void InvalidTargetsAreNotClickable(string type, Faction owner)
        {
            NodeController pulsar = ReadyPulsar();
            NodeController node = Node(type, owner, 10, new Vector2(4f, 3f));
            ClickAt(SlotAt(0));

            Click(node);

            Assert.AreEqual(0, beams.Active.Count);
            Assert.IsTrue(aim.IsAiming, "Se sigue apuntando.");
            Assert.AreEqual(PowerChargeState.Ready, Caster(pulsar).State);
            Assert.IsFalse(menu.IsOpen, "Ni abre el menú de encendido.");
            Assert.IsFalse(selection.HasSelection, "Ni selecciona.");
        }

        [Test]
        public void AimingSuspendsSendingAndTheSendLines()
        {
            NodeController pulsar = ReadyPulsar();
            NodeController star = Node("Star", Faction.Player, 30, new Vector2(-4f, -3f));
            NodeController target = Node("Star", Faction.Player, 0, new Vector2(0f, -3f));
            panel.Close();
            Click(star);
            Assert.IsTrue(selection.HasSelection);
            Assert.AreEqual(1, sendLine.Origins.Count);

            Assert.IsTrue(aim.Begin(pulsar, Faction.Player));
            Assert.AreEqual(0, sendLine.Origins.Count, "13.1: en modo apuntado no hay línea.");
            Click(target);
            router.Step(Balance.DoubleClickBuffer + Tick);

            Assert.AreEqual(30, star.Energy, "El clic no envía.");
            Assert.IsTrue(selection.HasSelection, "Y la selección sigue ahí al salir.");

            aim.End();
            Assert.AreEqual(1, sendLine.Origins.Count);
        }

        [Test]
        public void RightClickCancelsTheAimWithoutLosingTheChargeOrOpeningAPanel()
        {
            NodeController pulsar = ReadyPulsar();
            NodeController enemy = Node("Star", Faction.Enemy, 20, new Vector2(5f, 3f));
            ClickAt(SlotAt(0));

            RightClick(enemy);

            Assert.IsFalse(aim.IsAiming);
            Assert.AreEqual(PowerChargeState.Ready, Caster(pulsar).State, "Sin perder la carga.");
            Assert.AreSame(pulsar, panel.Node, "El clic derecho no abre el panel del enemigo.");

            ClickAt(SlotAt(0));
            Assert.IsTrue(aim.IsAiming, "Se vuelve a apuntar con la ranura.");
        }

        [Test]
        public void EscapeCancelsTheAimBeforeClosingThePanel()
        {
            ReadyPulsar();
            ClickAt(SlotAt(0));

            router.PressEscape();
            Assert.IsFalse(aim.IsAiming);
            Assert.IsTrue(panel.IsOpen);

            router.PressEscape();
            Assert.IsFalse(panel.IsOpen);
        }

        [Test]
        public void CancellingThePowerWhileAimingStopsAiming()
        {
            NodeController pulsar = ReadyPulsar();
            ClickAt(SlotAt(0));

            ClickAt(CancelAt);

            Assert.IsFalse(Caster(pulsar).HasPowerCharge);
            Assert.IsFalse(aim.IsAiming);
        }

        [Test]
        public void LosingThePulsarStopsAiming()
        {
            NodeController pulsar = ReadyPulsar();
            ClickAt(SlotAt(0));

            pulsar.Steal(Faction.Enemy);

            Assert.IsFalse(aim.IsAiming);
            Assert.AreEqual(PowerChargeState.Ready, Caster(pulsar).State, "El poder listo se lo lleva el nuevo dueño (7.5).");
            Assert.IsFalse(wheel.IsVisible, "Y la rueda deja de ser suya.");
        }

        // 17 caso 34: sin ningún nodo encendido que no sea suyo, el poder se queda cargado.
        [Test]
        public void WithNoLegalTargetThePowerStaysChargedIndefinitely()
        {
            NodeController pulsar = ReadyPulsar();
            NodeController own = Node("Star", Faction.Player, 10, new Vector2(0f, 3f));
            NodeController rock = Node("Asteroid", Faction.Neutral, 10, new Vector2(0f, -3f));
            int energy = pulsar.Energy;
            ClickAt(SlotAt(0));

            Click(own);
            Click(rock);
            ClickAt(new Vector2(6f, 4f));
            Charge(pulsar, 50 * 120);

            Assert.AreEqual(PowerChargeState.Ready, Caster(pulsar).State);
            Assert.AreEqual(energy, pulsar.Energy, "Cargado no drena nada más.");
            Assert.IsTrue(aim.IsAiming);
            Assert.AreEqual(0, beams.Active.Count);
        }

        [Test]
        public void AimCannotBeginWithoutAReadyPower()
        {
            NodeController pulsar = OpenPulsar();
            Assert.IsFalse(aim.Begin(pulsar, Faction.Player));
            ClickAt(SlotAt(0));
            Assert.IsFalse(aim.Begin(pulsar, Faction.Enemy));
            Assert.IsFalse(aim.Begin(pulsar, Faction.Player), "Cargando no basta.");
        }

        // --- Atenuación (13.5, 7.2.1) ---

        [Test]
        public void AimingVeilsEverythingThatIsNotAValidTarget()
        {
            NodeController pulsar = ReadyPulsar();
            Node("Star", Faction.Enemy, 20, new Vector2(5f, 3f));            // válido
            Node("Magnetar", Faction.Neutral, 20, new Vector2(5f, -3f));     // válido
            Node("Star", Faction.Player, 20, new Vector2(-4f, -3f));         // propio
            Node("Asteroid", Faction.Neutral, 10, new Vector2(0f, 3f));      // Asteroide
            Node("Asteroid", Faction.Enemy, 10, new Vector2(0f, -3f));       // Asteroide

            aimView.Refresh();
            Assert.AreEqual(0, aimView.VisibleCount, "Sin apuntar, nada atenuado.");

            ClickAt(SlotAt(0));
            aimView.Refresh();
            Assert.AreEqual(4, aimView.VisibleCount, "El Púlsar, la Estrella propia y los dos Asteroides.");

            router.PressEscape();
            aimView.Refresh();
            Assert.AreEqual(0, aimView.VisibleCount);
            Assert.IsNotNull(pulsar);
        }
    }
}
