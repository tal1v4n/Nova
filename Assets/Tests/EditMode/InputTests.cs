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
    // T1-11: InputRouter, SelectionManager y PlayerCommandIssuer (13.1, 13.2, 18 #23–27).
    // Sin ratón: los eventos entran ya traducidos a mundo y nodo, como los deja FixedUpdate.
    public class InputTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();
        NodeRegistry registry;
        SelectionManager selection;
        PlayerCommandIssuer issuer;
        InputRouter router;
        IgnitionMenuState menu;
        InfoPanelState panel;
        SendLineState sendLine;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Input");
            spawned.Add(go);
            registry = go.AddComponent<NodeRegistry>();
            var pool = go.AddComponent<ProtonPool>();
            var launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, pool);

            selection = go.AddComponent<SelectionManager>();
            selection.Configure(registry);
            issuer = go.AddComponent<PlayerCommandIssuer>();
            issuer.Configure(launcher);
            menu = go.AddComponent<IgnitionMenuState>();
            panel = go.AddComponent<InfoPanelState>();
            sendLine = go.AddComponent<SendLineState>();
            sendLine.Configure(selection, menu);
            router = go.AddComponent<InputRouter>();
            router.Configure(Balance, selection, issuer, menu, panel, null, sendLine);
        }

        [TearDown]
        public void TearDown()
        {
            selection.Clear();
            registry.Clear();
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
            go.AddComponent<NodeIgnition>().Configure(Balance, new[] { Definition("Star"), Definition("Pulsar"), Definition("Magnetar") });
            registry.Register(node);
            return node;
        }

        // Clic sin movimiento sobre un nodo (o en vacío con null).
        void Click(NodeController node)
        {
            Vector2 at = node != null ? (Vector2)node.transform.position : new Vector2(5f, 5f);
            router.PressLeft(at, node);
            router.ReleaseLeft(at, node);
        }

        // Clic sobre la opción del menú radial que enciende a type.
        void ChooseOption(NodeType type)
        {
            Vector2 at = IgnitionMenuState.OptionPosition(menu.Center, OptionIndex(type));
            router.PressLeft(at, null);
            router.ReleaseLeft(at, null);
        }

        static int OptionIndex(NodeType type)
        {
            for (int i = 0; i < IgnitionMenuState.Options.Count; i++)
            {
                if (IgnitionMenuState.Options[i] == type)
                    return i;
            }
            throw new System.ArgumentException(type.ToString());
        }

        void Wait(float seconds)
        {
            int ticks = Mathf.CeilToInt(seconds / Tick);
            for (int i = 0; i < ticks; i++)
                router.Step(Tick);
        }

        void WaitBuffer() => Wait(Balance.DoubleClickBuffer + Tick);

        // --- Sin selección, el clic elige ---

        [TestCase("Star")]
        [TestCase("Pulsar")]
        [TestCase("Magnetar")]
        public void ClickOnOwnSenderSelectsIt(string type)
        {
            NodeController node = Node(type, Faction.Player, 10);
            Click(node);
            CollectionAssert.AreEqual(new[] { node }, selection.Selected);
        }

        [TestCase(Faction.Enemy, "Star")]
        [TestCase(Faction.Neutral, "Asteroid")]
        [TestCase(Faction.Neutral, "Magnetar")]
        public void ClickOnForeignNodeWithoutSelectionDoesNothing(Faction faction, string type)
        {
            NodeController node = Node(type, faction, 10);

            Click(node);
            WaitBuffer();

            Assert.IsFalse(selection.HasSelection);
            Assert.AreEqual(type, node.Definition.name, "Un clic sobre un nodo ajeno no lo enciende.");
            Assert.AreEqual(10, node.Energy);
        }

        // --- Menú radial de encendido (13.7, T2-02) ---

        [TestCase(10)]
        [TestCase(9)]
        public void ClickOnOwnAsteroidWithoutSelectionOpensTheMenuWithoutIgniting(int energy)
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, energy);

            Click(asteroid);

            Assert.IsTrue(menu.IsOpen, "13.7: el menú se abre también sin Energía suficiente.");
            Assert.AreSame(asteroid, menu.Node);
            Assert.AreEqual((Vector2)asteroid.transform.position, menu.Center, "Sale en el cursor.");
            Assert.AreEqual(Faction.Player, menu.Actor);
            Assert.AreEqual(NodeType.Asteroid, asteroid.Type, "Abrir no enciende.");
            Assert.AreEqual(energy, asteroid.Energy);
            Assert.IsFalse(selection.HasSelection, "Un Asteroide no se selecciona.");
        }

        [TestCase(NodeType.Star)]
        [TestCase(NodeType.Pulsar)]
        [TestCase(NodeType.Magnetar)]
        public void ChoosingAnOptionIgnitesThatTypeAndClosesTheMenu(NodeType type)
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);

            Click(asteroid);
            ChooseOption(type);

            Assert.AreEqual(type, asteroid.Type);
            Assert.AreEqual(1, asteroid.Level);
            Assert.AreEqual(0, asteroid.Energy);
            Assert.IsFalse(menu.IsOpen);
            Assert.IsFalse(selection.HasSelection, "Encender no selecciona.");
        }

        [Test]
        public void ChoosingWithoutEnoughEnergyDoesNothingAndKeepsTheMenuOpen()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 9);

            Click(asteroid);
            ChooseOption(NodeType.Star);

            Assert.AreEqual(NodeType.Asteroid, asteroid.Type);
            Assert.AreEqual(9, asteroid.Energy);
            Assert.IsTrue(menu.IsOpen, "La opción deshabilitada no cierra: el jugador sigue viendo cuánto le falta.");
        }

        [Test]
        public void UnavailableTypeIsNotIgnited()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);
            asteroid.GetComponent<NodeIgnition>().Configure(Balance, new[] { Definition("Star") });

            Click(asteroid);
            ChooseOption(NodeType.Magnetar);

            Assert.AreEqual(NodeType.Asteroid, asteroid.Type);
            Assert.AreEqual(10, asteroid.Energy);
            Assert.IsTrue(menu.IsOpen);
        }

        [Test]
        public void ClickOnEmptyClosesTheMenuWithoutIgniting()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);

            Click(asteroid);
            Click(null);

            Assert.IsFalse(menu.IsOpen);
            Assert.AreEqual(NodeType.Asteroid, asteroid.Type);
            Assert.AreEqual(10, asteroid.Energy);
        }

        [Test]
        public void RightClickClosesTheMenuAndDoesNothingElse()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);

            Click(asteroid);
            router.ClickRight(asteroid);

            Assert.IsFalse(menu.IsOpen);
            Assert.IsFalse(panel.IsOpen, "El clic derecho que cierra el menú no abre el panel.");
            Assert.AreEqual(NodeType.Asteroid, asteroid.Type);
        }

        [Test]
        public void EscapeClosesTheMenuInsteadOfPausing()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);
            bool paused = false;
            router.OnPauseRequested += () => paused = true;

            Click(asteroid);
            router.PressEscape();

            Assert.IsFalse(menu.IsOpen);
            Assert.IsFalse(paused);
        }

        [Test]
        public void ClickOnAnotherNodeClosesTheMenuAndActsNormally()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);
            NodeController star = Node("Star", Faction.Player, 10, new Vector2(-4f, 0f));

            Click(asteroid);
            Click(star);

            Assert.IsFalse(menu.IsOpen);
            CollectionAssert.AreEqual(new[] { star }, selection.Selected, "El clic sigue su curso: sin selección, elige.");
            Assert.AreEqual(NodeType.Asteroid, asteroid.Type);
        }

        [Test]
        public void ClickOnAnOptionWinsOverANodeUnderneath()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);
            Vector2 starOption = IgnitionMenuState.OptionPosition(asteroid.transform.position, OptionIndex(NodeType.Star));
            NodeController star = Node("Star", Faction.Player, 10, starOption);

            Click(asteroid);
            router.PressLeft(starOption, star);
            router.ReleaseLeft(starOption, star);

            Assert.AreEqual(NodeType.Star, asteroid.Type);
            Assert.IsFalse(selection.HasSelection);
        }

        [Test]
        public void LassoClosesTheMenu()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);
            NodeController star = Node("Star", Faction.Player, 10, new Vector2(-4f, 0f));

            Click(asteroid);
            router.PressLeft(new Vector2(-5f, -1f), null);
            router.ReleaseLeft(new Vector2(-3f, 1f), null);

            Assert.IsFalse(menu.IsOpen);
            CollectionAssert.AreEqual(new[] { star }, selection.Selected);
        }

        [Test]
        public void CapturedAsteroidClosesTheMenu()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);

            Click(asteroid);
            asteroid.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(Faction.Enemy, 30), asteroid.Snapshot(), Balance.MagnetarResistance));

            Assert.AreEqual(Faction.Enemy, asteroid.Faction);
            Assert.IsFalse(menu.IsOpen);
        }

        [Test]
        public void IgnitedAsteroidIsSelectableOnTheNextClick()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);

            Click(asteroid);
            ChooseOption(NodeType.Star);
            WaitBuffer();
            Click(asteroid);

            CollectionAssert.AreEqual(new[] { asteroid }, selection.Selected);
        }

        [Test]
        public void ClickOnEmptyWithoutSelectionDoesNothing()
        {
            Click(null);
            Assert.IsFalse(selection.HasSelection);
        }

        // --- Con selección, el clic envía (13.1, 13.2) ---

        [Test]
        public void ClickSendsHalfOnlyAfterTheBuffer()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            NodeController target = Node("Asteroid", Faction.Neutral, 10, new Vector2(3f, 0f));
            Click(source);

            Click(target);
            Wait(Balance.DoubleClickBuffer - Tick);
            Assert.AreEqual(40, source.Energy, "El envío no se ejecuta en el primer clic (13.2).");
            Assert.IsTrue(router.IsBufferActive);

            Wait(2 * Tick);
            Assert.AreEqual(20, source.Energy);
            Assert.IsFalse(router.IsBufferActive);
        }

        [Test]
        public void DoubleClickOnSameTargetSendsEverything()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            NodeController target = Node("Asteroid", Faction.Neutral, 10, new Vector2(3f, 0f));
            Click(source);

            Click(target);
            Wait(0.1f);
            Click(target);

            Assert.AreEqual(0, source.Energy);
            Assert.IsFalse(router.IsBufferActive);
            WaitBuffer();
            Assert.AreEqual(0, source.Energy, "Un doble clic no deja un 50 % pendiente detrás.");
        }

        [Test]
        public void TwoSeparateClicksAccumulateSeventyFivePercent()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            NodeController target = Node("Asteroid", Faction.Neutral, 10, new Vector2(3f, 0f));
            Click(source);

            Click(target);
            WaitBuffer();
            Click(target);
            WaitBuffer();

            Assert.AreEqual(10, source.Energy, "50 % + 50 % = 75 % (13.1).");
        }

        [Test]
        public void ClickOnAnotherTargetInsideTheBufferFlushesTheFirst()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            NodeController first = Node("Asteroid", Faction.Neutral, 10, new Vector2(3f, 0f));
            NodeController second = Node("Asteroid", Faction.Neutral, 10, new Vector2(-3f, 0f));
            Click(source);

            Click(first);
            Wait(0.1f);
            Click(second);
            Assert.AreEqual(20, source.Energy, "18 #24: el 50 % pendiente sale al hacer clic en otro destino.");
            Assert.IsTrue(router.IsBufferActive, "El segundo destino abre su propio buffer.");

            WaitBuffer();
            Assert.AreEqual(10, source.Energy);
        }

        [Test]
        public void EverySelectedOriginLaunchesItsOwnProton()
        {
            NodeController a = Node("Star", Faction.Player, 40);
            NodeController b = Node("Magnetar", Faction.Player, 30, new Vector2(1f, 0f));
            NodeController dry = Node("Pulsar", Faction.Player, 1, new Vector2(2f, 0f));
            NodeController target = Node("Star", Faction.Enemy, 10, new Vector2(5f, 0f));
            selection.Select(a);
            selection.Select(b);
            selection.Select(dry);

            Click(target);
            WaitBuffer();

            Assert.AreEqual(20, a.Energy);
            Assert.AreEqual(15, b.Energy);
            Assert.AreEqual(1, dry.Energy, "17, caso 22: el que no llega al mínimo se ignora en silencio.");
        }

        [Test]
        public void ClickOnOwnNodeWithSelectionReinforcesIt()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            NodeController ally = Node("Magnetar", Faction.Player, 0, new Vector2(3f, 0f));
            Click(source);

            Click(ally);
            WaitBuffer();

            Assert.AreEqual(20, source.Energy);
            CollectionAssert.AreEqual(new[] { source }, selection.Selected, "Un clic con selección envía, no cambia de origen.");
        }

        [Test]
        public void ClickOnOwnAsteroidWithSelectionSendsInsteadOfIgniting()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            NodeController asteroid = Node("Asteroid", Faction.Player, 0, new Vector2(3f, 0f));
            Click(source);

            Click(asteroid);
            WaitBuffer();

            Assert.AreEqual(NodeType.Asteroid, asteroid.Type, "13.1: con selección, un Asteroide propio recibe en vez de encenderse.");
            Assert.IsFalse(menu.IsOpen, "13.7: el menú solo se abre sin selección.");
            Assert.AreEqual(20, source.Energy);
        }

        [Test]
        public void SelectionSurvivesSending()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            NodeController target = Node("Asteroid", Faction.Neutral, 10, new Vector2(3f, 0f));
            Click(source);
            Click(target);
            WaitBuffer();

            Assert.IsTrue(selection.IsSelected(source));
        }

        [Test]
        public void ClickOnSelectedNodeDeselectsIt()
        {
            NodeController a = Node("Star", Faction.Player, 40);
            NodeController b = Node("Star", Faction.Player, 40, new Vector2(1f, 0f));
            selection.Select(a);
            selection.Select(b);

            Click(a);

            CollectionAssert.AreEqual(new[] { b }, selection.Selected);
            WaitBuffer();
            Assert.AreEqual(40, b.Energy, "Deseleccionar no envía nada.");
        }

        [Test]
        public void DoubleClickOnSelectedNodeDeselectsAndSendsNothing()
        {
            NodeController a = Node("Star", Faction.Player, 40);
            NodeController b = Node("Star", Faction.Player, 40, new Vector2(1f, 0f));
            selection.Select(a);
            selection.Select(b);

            Click(a);
            Wait(0.1f);
            Click(a);
            WaitBuffer();

            CollectionAssert.AreEqual(new[] { b }, selection.Selected);
            Assert.AreEqual(40, a.Energy);
            Assert.AreEqual(40, b.Energy, "18 #27: el segundo clic se ignora y no envía a a.");
        }

        [Test]
        public void DoubleClickOnOnlySelectedNodeDoesNotReselectIt()
        {
            NodeController a = Node("Star", Faction.Player, 40);
            selection.Select(a);

            Click(a);
            Wait(0.1f);
            Click(a);

            Assert.IsFalse(selection.HasSelection);
        }

        [Test]
        public void ClickOnSelectedNodeAfterTheBufferIsANormalClick()
        {
            NodeController a = Node("Star", Faction.Player, 40);
            selection.Select(a);

            Click(a);
            WaitBuffer();
            Click(a);

            Assert.IsTrue(selection.IsSelected(a), "Pasado el buffer, sin selección el clic vuelve a elegir.");
        }

        [Test]
        public void ClickOnEmptyWithSelectionDoesNothing()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            Click(source);

            Click(null);
            WaitBuffer();

            Assert.IsTrue(selection.IsSelected(source), "18 #26: solo el clic derecho en vacío cancela.");
            Assert.AreEqual(40, source.Energy);
        }

        // --- Clic derecho y Esc ---

        [Test]
        public void RightClickOnEmptyClearsSelection()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            Click(source);

            router.ClickRight(null);

            Assert.IsFalse(selection.HasSelection);
        }

        [Test]
        public void RightClickOnNodeOpensItsPanelAndKeepsSelection()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            NodeController enemy = Node("Star", Faction.Enemy, 10, new Vector2(3f, 0f));
            Click(source);

            router.ClickRight(enemy);

            Assert.IsTrue(panel.IsOpen);
            Assert.AreSame(enemy, panel.Node);
            Assert.AreEqual(InfoPanelState.CenterBeside(enemy.transform.position, false), panel.Center, "A la derecha del nodo.");
            Assert.IsTrue(selection.IsSelected(source));
        }

        // --- Panel de clic derecho y mejora (13.3, T2-03) ---

        // Clic sin movimiento en un punto del mundo, sin nodo debajo.
        void ClickAt(Vector2 at, NodeController node = null)
        {
            router.PressLeft(at, node);
            router.ReleaseLeft(at, node);
        }

        Vector2 UpgradeButton => panel.Center + InfoPanelState.ButtonOffset;

        [Test]
        public void RightClickOnAnotherNodeMovesThePanel()
        {
            NodeController first = Node("Star", Faction.Player, 10);
            NodeController second = Node("Star", Faction.Enemy, 10, new Vector2(0f, 3f));

            router.ClickRight(first);
            router.ClickRight(second);

            Assert.AreSame(second, panel.Node);
        }

        [Test]
        public void RightClickOnEmptyClosesThePanel()
        {
            NodeController star = Node("Star", Faction.Player, 10);
            router.ClickRight(star);

            router.ClickRight(null);

            Assert.IsFalse(panel.IsOpen);
        }

        [Test]
        public void EscapeClosesThePanelInsteadOfPausing()
        {
            NodeController star = Node("Star", Faction.Player, 10);
            bool paused = false;
            router.OnPauseRequested += () => paused = true;
            router.ClickRight(star);

            router.PressEscape();

            Assert.IsFalse(panel.IsOpen);
            Assert.IsFalse(paused);
        }

        [Test]
        public void UpgradeButtonUpgradesAndCharges()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            router.ClickRight(star);

            ClickAt(UpgradeButton);

            Assert.AreEqual(2, star.Level);
            Assert.AreEqual(10, star.Energy, "16.2: pasar a nivel 2 cuesta 20.");
            Assert.IsTrue(panel.IsOpen, "El panel sigue abierto para seguir mejorando.");
        }

        [Test]
        public void UpgradeButtonWithoutEnoughEnergyDoesNothing()
        {
            NodeController star = Node("Star", Faction.Player, 19);
            router.ClickRight(star);

            ClickAt(UpgradeButton);

            Assert.AreEqual(1, star.Level);
            Assert.AreEqual(19, star.Energy);
        }

        [Test]
        public void UpgradeButtonOnAForeignNodeDoesNothing()
        {
            NodeController enemy = Node("Star", Faction.Enemy, 50);
            router.ClickRight(enemy);

            ClickAt(UpgradeButton);

            Assert.AreEqual(1, enemy.Level);
            Assert.AreEqual(50, enemy.Energy);
        }

        [Test]
        public void ClickInsideThePanelDoesNotReachTheNodeUnderneath()
        {
            NodeController star = Node("Star", Faction.Player, 10);
            router.ClickRight(star);
            NodeController hidden = Node("Star", Faction.Player, 10, panel.Center);
            Vector2 inside = panel.Center + new Vector2(0f, InfoPanelState.Height / 4f);
            Assert.IsFalse(panel.IsOnButton(inside));

            ClickAt(inside, hidden);

            Assert.IsFalse(selection.HasSelection, "El clic es del panel.");
        }

        [Test]
        public void UpgradeButtonDoesNotSendWithASelection()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            NodeController other = Node("Star", Faction.Player, 30, new Vector2(-4f, 0f));
            Click(other);
            router.ClickRight(star);

            ClickAt(UpgradeButton);
            WaitBuffer();

            Assert.AreEqual(2, star.Level);
            Assert.AreEqual(30, other.Energy, "El clic en el botón no es un envío.");
            Assert.IsTrue(selection.IsSelected(other));
        }

        // --- Reconversión desde el panel (13.3 punto 5, T2-07) ---

        Vector2 ConversionButton(int index) => panel.Center + InfoPanelState.ConversionOffsets[index];

        NodeController Convertible(string type, Faction faction, int energy)
        {
            NodeController node = Node(type, faction, energy);
            node.gameObject.AddComponent<NodeConverter>().Configure(Balance, new[] { Definition("Star"), Definition("Magnetar") });
            return node;
        }

        [Test]
        public void ConversionButtonStartsTheConversion()
        {
            NodeController star = Convertible("Star", Faction.Player, 30);
            router.ClickRight(star);

            ClickAt(ConversionButton(1)); // Estrella: Púlsar, Magnetar

            Assert.IsTrue(star.HasState(NodeStateFlags.Converting));
            Assert.AreEqual(NodeType.Magnetar, star.GetComponent<NodeConverter>().TargetType);
            Assert.AreEqual(10, star.Energy, "16.4: desde Estrella cuesta 20.");
            Assert.IsTrue(panel.IsOpen);
        }

        [Test]
        public void UnavailableDestinationDoesNothing()
        {
            NodeController star = Convertible("Star", Faction.Player, 30);
            router.ClickRight(star);

            ClickAt(ConversionButton(0)); // Púlsar, que la fase 2 no tiene

            Assert.IsFalse(star.HasState(NodeStateFlags.Converting));
            Assert.AreEqual(30, star.Energy);
        }

        [Test]
        public void ConversionButtonOnAForeignNodeDoesNothing()
        {
            NodeController enemy = Convertible("Star", Faction.Enemy, 50);
            router.ClickRight(enemy);

            ClickAt(ConversionButton(1));

            Assert.IsFalse(enemy.HasState(NodeStateFlags.Converting));
            Assert.AreEqual(50, enemy.Energy);
        }

        [Test]
        public void ConversionAreaWithoutConverterDoesNothing()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            router.ClickRight(star);

            Assert.DoesNotThrow(() => ClickAt(ConversionButton(1)));
            Assert.AreEqual(30, star.Energy);
        }

        [Test]
        public void ClicksOutsideThePanelKeepWorking()
        {
            NodeController star = Node("Star", Faction.Player, 10, new Vector2(-4f, 0f));
            router.ClickRight(Node("Star", Faction.Enemy, 10, new Vector2(4f, 0f)));

            Click(star);

            Assert.IsTrue(selection.IsSelected(star));
            Assert.IsTrue(panel.IsOpen, "Solo lo cierran el clic derecho en vacío y Esc.");
        }

        [Test]
        public void EscapeRequestsPause()
        {
            bool requested = false;
            router.OnPauseRequested += () => requested = true;
            router.PressEscape();
            Assert.IsTrue(requested);
        }

        // --- Arrastre y lazo ---

        static float Threshold => Balance.DragThreshold / 64f;

        [Test]
        public void MovingUpToTheThresholdIsStillAClick()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            Vector2 at = source.transform.position;

            router.PressLeft(at, source);
            router.ReleaseLeft(at + new Vector2(Threshold, 0f), source);

            Assert.IsTrue(selection.IsSelected(source));
        }

        // --- Arrastre derecho (13.1, T2-09) ---

        static readonly Vector2 Empty = new Vector2(5f, 5f);

        // Pulsación derecha sobre un nodo (o en vacío con null), arrastre hasta to y suelta allí.
        void RightDrag(NodeController from, Vector2 to, NodeController dropOn)
        {
            router.PressRight(from != null ? (Vector2)from.transform.position : Empty, from);
            router.MoveTo(to);
            router.ReleaseRight(to, dropOn);
        }

        void RightClick(NodeController node)
        {
            Vector2 at = node != null ? (Vector2)node.transform.position : Empty;
            router.PressRight(at, node);
            router.ReleaseRight(at, node);
        }

        [Test]
        public void RightDragFromOwnNodeSendsHalfOnReleaseWithoutBuffer()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            NodeController target = Node("Asteroid", Faction.Neutral, 10, new Vector2(3f, 0f));

            router.PressRight(source.transform.position, source);
            router.MoveTo(new Vector2(1.5f, 0f));
            Assert.AreEqual(40, source.Energy, "Nada sale antes de soltar.");
            router.ReleaseRight(target.transform.position, target);

            Assert.AreEqual(20, source.Energy, "18 #25: sale al soltar.");
            Assert.IsFalse(router.IsBufferActive);
            Assert.IsFalse(selection.HasSelection, "Un arrastre no selecciona.");
            Assert.IsFalse(panel.IsOpen, "Un arrastre no abre el panel.");
        }

        [Test]
        public void RightDragSendsOnlyFromItsOriginWhateverTheSelection()
        {
            NodeController selected = Node("Star", Faction.Player, 40);
            NodeController origin = Node("Magnetar", Faction.Player, 40, new Vector2(0f, 2f));
            NodeController target = Node("Star", Faction.Enemy, 10, new Vector2(3f, 0f));
            selection.Select(selected);

            RightDrag(origin, target.transform.position, target);

            Assert.AreEqual(20, origin.Energy);
            Assert.AreEqual(40, selected.Energy);
            CollectionAssert.AreEqual(new[] { selected }, selection.Selected);
        }

        [Test]
        public void RightDragReleasedOnEmptyOrOnItsOriginDoesNothing()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            NodeController other = Node("Star", Faction.Player, 40, new Vector2(-4f, 0f));
            selection.Select(other);

            RightDrag(source, Empty, null);
            router.PressRight(source.transform.position, source);
            router.MoveTo(new Vector2(1f, 1f));
            router.ReleaseRight(source.transform.position, source);

            Assert.AreEqual(40, source.Energy);
            Assert.IsFalse(panel.IsOpen, "Hubo arrastre: ni panel ni cancelación, se suelte donde se suelte.");
            Assert.IsTrue(selection.IsSelected(other));
        }

        [Test]
        public void RightDragFromForeignNodeAsteroidOrEmptyDoesNothing()
        {
            NodeController enemy = Node("Star", Faction.Enemy, 40);
            NodeController asteroid = Node("Asteroid", Faction.Player, 10, new Vector2(0f, 2f));
            NodeController target = Node("Star", Faction.Player, 0, new Vector2(3f, 0f));
            NodeController selected = Node("Star", Faction.Player, 40, new Vector2(-4f, 0f));
            selection.Select(selected);

            RightDrag(enemy, target.transform.position, target);
            RightDrag(asteroid, target.transform.position, target);
            RightDrag(null, target.transform.position, target);

            Assert.AreEqual(40, enemy.Energy);
            Assert.AreEqual(10, asteroid.Energy);
            Assert.AreEqual(0, target.Energy);
            Assert.IsFalse(panel.IsOpen);
            Assert.IsTrue(selection.IsSelected(selected), "Un arrastre en vacío no cancela la selección.");
        }

        [Test]
        public void RightClickActsOnReleaseNotOnPress()
        {
            NodeController star = Node("Star", Faction.Player, 10);

            router.PressRight(star.transform.position, star);
            Assert.IsFalse(panel.IsOpen, "Al pulsar todavía no se sabe si es clic o arrastre.");

            router.ReleaseRight(star.transform.position, star);
            Assert.IsTrue(panel.IsOpen);
            Assert.AreSame(star, panel.Node);
        }

        [Test]
        public void RightMovingUpToTheThresholdIsStillAClick()
        {
            NodeController star = Node("Star", Faction.Player, 10);
            Vector2 at = star.transform.position;

            router.PressRight(at, star);
            router.ReleaseRight(at + new Vector2(Threshold, 0f), null);

            Assert.IsTrue(panel.IsOpen, "La regla del umbral es la misma para los dos botones.");
            Assert.AreSame(star, panel.Node, "El clic es del nodo donde se pulsó.");
        }

        [Test]
        public void RightClickOnEmptyStillClearsSelectionAndPanel()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            Click(source);
            RightClick(source);

            RightClick(null);

            Assert.IsFalse(selection.HasSelection);
            Assert.IsFalse(panel.IsOpen);
        }

        [Test]
        public void RightPressWithTheMenuOpenClosesItAndIsSwallowed()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);
            NodeController target = Node("Star", Faction.Player, 0, new Vector2(3f, 0f));
            Click(asteroid);
            Assert.IsTrue(menu.IsOpen);

            router.PressRight(asteroid.transform.position, asteroid);
            Assert.IsFalse(menu.IsOpen, "13.7: lo cierra al pulsar.");

            router.MoveTo(target.transform.position);
            router.ReleaseRight(target.transform.position, target);

            Assert.IsFalse(panel.IsOpen, "La pulsación era del menú: al soltar no pasa nada.");
            Assert.IsNull(sendLine.DragOrigin);
        }

        // --- Arrastre izquierdo: siempre lazo ---

        [Test]
        public void LeftDragFromANodeIsALassoAndSendsNothing()
        {
            NodeController source = Node("Star", Faction.Player, 40);
            NodeController other = Node("Star", Faction.Player, 40, new Vector2(1f, 1f));
            NodeController enemy = Node("Star", Faction.Enemy, 10, new Vector2(4f, 0f));

            router.PressLeft(source.transform.position, source);
            router.MoveTo(new Vector2(1.5f, 1.5f));
            router.ReleaseLeft(new Vector2(1.5f, 1.5f), null);

            CollectionAssert.AreEquivalent(new[] { source, other }, selection.Selected);
            Assert.AreEqual(40, source.Energy, "18 #32: el arrastre izquierdo ya no envía.");

            router.PressLeft(enemy.transform.position, enemy);
            router.ReleaseLeft(new Vector2(5f, 1f), null);

            Assert.IsFalse(selection.HasSelection, "Un lazo sin nodos propios deja la selección vacía.");
            Assert.AreEqual(10, enemy.Energy);
        }

        // --- Línea de envío (13.1, T2-09) ---

        [Test]
        public void WithoutSelectionThereIsNoLine()
        {
            Node("Star", Faction.Player, 40);
            CollectionAssert.IsEmpty(sendLine.Origins);
        }

        [Test]
        public void ASelectionDrawsALineFromEachSelectedNode()
        {
            NodeController a = Node("Star", Faction.Player, 40);
            NodeController b = Node("Magnetar", Faction.Player, 40, new Vector2(0f, 2f));
            selection.Select(a);
            selection.Select(b);

            CollectionAssert.AreEqual(new[] { a, b }, sendLine.Origins, "En el orden de la selección.");

            selection.Deselect(a);
            CollectionAssert.AreEqual(new[] { b }, sendLine.Origins, "Sale de la selección y pierde su línea.");

            RightClick(null);
            CollectionAssert.IsEmpty(sendLine.Origins, "Cancelar la selección quita las líneas.");
        }

        [Test]
        public void ARightDragShowsOnlyItsOriginOnceItPassesTheThreshold()
        {
            NodeController selected = Node("Star", Faction.Player, 40);
            NodeController origin = Node("Magnetar", Faction.Player, 40, new Vector2(0f, 2f));
            selection.Select(selected);

            router.PressRight(origin.transform.position, origin);
            router.MoveTo((Vector2)origin.transform.position + new Vector2(Threshold, 0f));
            CollectionAssert.AreEqual(new[] { selected }, sendLine.Origins, "Por debajo del umbral es un clic.");

            router.MoveTo(new Vector2(2f, 2f));
            CollectionAssert.AreEqual(new[] { origin }, sendLine.Origins, "Las líneas de la selección se ocultan.");
            Assert.AreSame(origin, sendLine.DragOrigin);

            router.ReleaseRight(Empty, null);
            CollectionAssert.AreEqual(new[] { selected }, sendLine.Origins, "Al soltar vuelven.");
            Assert.IsNull(sendLine.DragOrigin);
        }

        [Test]
        public void ARightDragFromAnIllegalOriginDrawsNoLine()
        {
            NodeController enemy = Node("Star", Faction.Enemy, 40);
            NodeController asteroid = Node("Asteroid", Faction.Player, 10, new Vector2(0f, 2f));

            router.PressRight(enemy.transform.position, enemy);
            router.MoveTo(new Vector2(2f, 0f));
            Assert.IsNull(sendLine.DragOrigin);
            CollectionAssert.IsEmpty(sendLine.Origins);
            router.ReleaseRight(new Vector2(2f, 0f), null);

            router.PressRight(asteroid.transform.position, asteroid);
            router.MoveTo(new Vector2(2f, 2f));
            Assert.IsNull(sendLine.DragOrigin);
            router.ReleaseRight(new Vector2(2f, 2f), null);
        }

        [Test]
        public void AnOriginThatChangesSidesMidDragLosesItsLine()
        {
            NodeController origin = Node("Star", Faction.Player, 0);

            router.PressRight(origin.transform.position, origin);
            router.MoveTo(new Vector2(2f, 0f));
            Assert.AreEqual(1, sendLine.Origins.Count);

            origin.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(Faction.Enemy, 5), origin.Snapshot(), Balance.MagnetarResistance));

            CollectionAssert.IsEmpty(sendLine.Origins);
        }

        [Test]
        public void TheOpenIgnitionMenuHidesTheLines()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10);
            NodeController star = Node("Star", Faction.Player, 40, new Vector2(-4f, 0f));
            Click(asteroid);
            selection.Select(star);

            CollectionAssert.IsEmpty(sendLine.Origins);

            menu.Close();
            CollectionAssert.AreEqual(new[] { star }, sendLine.Origins);
        }

        [Test]
        public void TheLineEndsAtTheCursor()
        {
            sendLine.SetCursor(new Vector2(1.25f, -2f));
            Assert.AreEqual(new Vector2(1.25f, -2f), sendLine.Cursor);
        }

        [Test]
        public void TheProvisionalViewDrawsOneStrokeFromEachOriginToTheCursor()
        {
            NodeController a = Node("Star", Faction.Player, 40);
            NodeController b = Node("Star", Faction.Player, 40, new Vector2(0f, 2f));
            var view = new GameObject("SendLineView").AddComponent<ProvisionalSendLineView>();
            spawned.Add(view.gameObject);
            view.Configure(sendLine);
            selection.Select(a);
            selection.Select(b);
            sendLine.SetCursor(new Vector2(4f, 0f));

            view.Refresh();

            Assert.AreEqual(2, view.VisibleCount);
            SpriteRenderer first = view.Lines[0];
            Assert.AreEqual(new Vector3(2f, 0f, 0f), first.transform.position, "En el punto medio.");
            Assert.AreEqual(4f * 64f, first.transform.localScale.x, 1e-3f, "4 u de largo, en píxeles.");

            selection.Clear();
            view.Refresh();
            Assert.AreEqual(0, view.VisibleCount);
        }

        [Test]
        public void LassoSelectsOwnSendersInsideAndReplacesTheSelection()
        {
            NodeController outside = Node("Star", Faction.Player, 10, new Vector2(-5f, 0f));
            NodeController star = Node("Star", Faction.Player, 10, new Vector2(1f, 1f));
            NodeController pulsar = Node("Pulsar", Faction.Player, 10, new Vector2(2f, 0f));
            NodeController edge = Node("Magnetar", Faction.Player, 10, new Vector2(3f, 2f));
            Node("Asteroid", Faction.Player, 10, new Vector2(1.5f, 1.5f));
            Node("Star", Faction.Enemy, 10, new Vector2(2f, 1f));
            Node("Magnetar", Faction.Neutral, 10, new Vector2(2.5f, 0.5f));
            selection.Select(outside);

            router.PressLeft(new Vector2(3f, 2f), null);
            router.MoveTo(new Vector2(2f, 1f));
            router.ReleaseLeft(new Vector2(0f, 0f), null);

            CollectionAssert.AreEquivalent(new[] { star, pulsar, edge }, selection.Selected);
        }

        // --- Selección que deja de ser propia (18 #27) ---

        [Test]
        public void CapturedNodeLeavesTheSelection()
        {
            NodeController a = Node("Star", Faction.Player, 0);
            NodeController b = Node("Star", Faction.Player, 10, new Vector2(1f, 0f));
            selection.Select(a);
            selection.Select(b);
            int changes = 0;
            selection.OnSelectionChanged += () => changes++;

            a.ApplyImpact(ImpactResolver.Resolve(
                new ProtonSnapshot(Faction.Enemy, 5), a.Snapshot(), Balance.MagnetarResistance));

            Assert.AreEqual(Faction.Enemy, a.Faction);
            CollectionAssert.AreEqual(new[] { b }, selection.Selected);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void SelectRejectsIllegalOrigins()
        {
            Assert.IsFalse(selection.Select(Node("Asteroid", Faction.Player, 10)));
            Assert.IsFalse(selection.Select(Node("Star", Faction.Enemy, 10)));
            Assert.IsFalse(selection.Select(Node("Magnetar", Faction.Neutral, 10)));
            Assert.IsFalse(selection.HasSelection);
        }

        // --- Canal de comandos ---

        [Test]
        public void PlayerIssuerActsOnlyAsPlayer()
        {
            NodeController enemy = Node("Star", Faction.Enemy, 40);
            NodeController target = Node("Star", Faction.Player, 0, new Vector2(3f, 0f));

            Assert.AreEqual(Faction.Player, issuer.Faction);
            Assert.AreEqual(LaunchBlock.NotOwner, issuer.Send(enemy, target, false));
            Assert.AreEqual(40, enemy.Energy);
        }

        // --- Picking (18 #23) ---

        [Test]
        public void NodesLayerExists()
        {
            Assert.GreaterOrEqual(LayerMask.NameToLayer(InputRouter.NodesLayer), 0,
                "La crea Nova.Editor.ProjectLayers al cargar el editor.");
        }

        [Test]
        public void PickNodeFindsTheTriggerOnTheNodesLayer()
        {
            NodeController node = Node("Star", Faction.Player, 10, new Vector2(2f, 1f));
            node.gameObject.layer = LayerMask.NameToLayer(InputRouter.NodesLayer);
            var trigger = node.gameObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.5f;

            NodeController other = Node("Star", Faction.Player, 10, new Vector2(-2f, 1f));
            other.gameObject.AddComponent<CircleCollider2D>().radius = 0.5f;   // capa Default: invisible al clic
            Physics2D.SyncTransforms();

            Assert.AreSame(node, router.PickNode(new Vector2(2.4f, 1f)));
            Assert.IsNull(router.PickNode(new Vector2(2.6f, 1f)));
            Assert.IsNull(router.PickNode(new Vector2(-2f, 1f)));
        }
    }
}
