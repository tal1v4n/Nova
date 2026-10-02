using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Systems;
using Nova.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T2-03: la mejora (NodeController.TryUpgrade, 16.2), su comando, el panel de clic derecho
    // (13.3, 13.4) y los textos de la UI (T.7 #39). El clic sobre el panel se prueba en InputTests.
    public class UpgradeTests
    {
        const string FontPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";

        readonly List<GameObject> spawned = new List<GameObject>();
        InfoPanelState panel;
        NodeInfoPanel view;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        static UiText Text =>
            AssetDatabase.LoadAssetAtPath<UiText>("Assets/Data/UiText.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Panel");
            spawned.Add(go);
            panel = go.AddComponent<InfoPanelState>();
            view = go.AddComponent<NodeInfoPanel>();
            view.Configure(panel, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath), Text);
            view.Subscribe();
        }

        [TearDown]
        public void TearDown()
        {
            view.Unsubscribe();
            panel.Close();
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
        }

        NodeController Node(string type, Faction faction, int energy, int level = -1)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            var node = go.AddComponent<NodeController>();
            if (level < 0)
                node.Initialize(Definition(type), faction, energy);
            else
                node.Initialize(Definition(type), faction, energy, level);
            go.AddComponent<NodeIgnition>().Configure(Balance, new[] { Definition("Star") });
            return node;
        }

        // --- Mejora ---

        // 16.2: cada escalón cuesta lo suyo, y del 1 al 5 suman 140.
        [TestCase("Star", 2, 20)]
        [TestCase("Star", 3, 30)]
        [TestCase("Star", 5, 50)]
        [TestCase("Magnetar", 2, 20)]
        [TestCase("Magnetar", 4, 40)]
        [TestCase("Pulsar", 2, 25)]
        [TestCase("Pulsar", 3, 40)]
        public void UpgradeChargesTheCostOfTheNextLevel(string type, int toLevel, int cost)
        {
            NodeController node = Node(type, Faction.Player, cost, toLevel - 1);

            ActionCheck result = node.TryUpgrade(Faction.Player);

            Assert.IsTrue(result.IsAllowed);
            Assert.AreEqual(cost, result.Cost);
            Assert.AreEqual(toLevel, node.Level);
            Assert.AreEqual(0, node.Energy);
        }

        [Test]
        public void UpgradeKeepsTypeCapFactionAndState()
        {
            NodeController node = Node("Magnetar", Faction.Player, 60);

            node.TryUpgrade(Faction.Player);

            Assert.AreEqual(NodeType.Magnetar, node.Type);
            Assert.AreEqual(100, node.EnergyCap, "El Tope es del tipo, no del nivel.");
            Assert.AreEqual(Faction.Player, node.Faction);
            Assert.AreEqual(NodeState.Normal, node.State);
            Assert.AreEqual(1.75f, node.CurrentLevelData.Radius, 1e-5f, "El nivel nuevo trae su radio.");
        }

        [Test]
        public void UpgradeEmitsLevelAndEnergyOnce()
        {
            NodeController node = Node("Star", Faction.Player, 25);
            int levels = 0, energies = 0;
            node.OnLevelChanged += (a, b) => levels++;
            node.OnEnergyChanged += (a, b) => energies++;

            node.TryUpgrade(Faction.Player);

            Assert.AreEqual(1, levels);
            Assert.AreEqual(1, energies);
        }

        [Test]
        public void NotEnoughEnergyChangesNothing()
        {
            NodeController node = Node("Star", Faction.Player, 19);

            ActionCheck result = node.TryUpgrade(Faction.Player);

            Assert.AreEqual(ActionBlock.InsufficientEnergy, result.Block);
            Assert.AreEqual(20, result.Cost);
            Assert.AreEqual(1, node.Level);
            Assert.AreEqual(19, node.Energy);
        }

        [Test]
        public void MaxLevelCannotBeUpgraded()
        {
            NodeController node = Node("Pulsar", Faction.Player, 100, 3);
            Assert.AreEqual(ActionBlock.MaxLevel, node.TryUpgrade(Faction.Player).Block);
            Assert.AreEqual(100, node.Energy);
        }

        [TestCase(Faction.Enemy)]
        [TestCase(Faction.Neutral)]
        public void OnlyTheOwnerUpgrades(Faction actor)
        {
            NodeController node = Node("Star", Faction.Player, 50);
            Assert.AreEqual(ActionBlock.NotOwner, node.TryUpgrade(actor).Block);
            Assert.AreEqual(1, node.Level);
        }

        [Test]
        public void AsteroidCannotBeUpgraded()
        {
            NodeController node = Node("Asteroid", Faction.Player, 10);
            Assert.AreEqual(ActionBlock.IsAsteroid, node.TryUpgrade(Faction.Player).Block);
        }

        [Test]
        public void IssuerUpgradesAsPlayer()
        {
            var go = new GameObject("Issuer");
            spawned.Add(go);
            var issuer = go.AddComponent<PlayerCommandIssuer>();
            NodeController own = Node("Star", Faction.Player, 20);
            NodeController enemy = Node("Star", Faction.Enemy, 20);

            Assert.IsTrue(issuer.Upgrade(own).IsAllowed);
            Assert.AreEqual(ActionBlock.NotOwner, issuer.Upgrade(enemy).Block);
        }

        // --- Panel ---

        [Test]
        public void PanelIsHiddenWhileClosed()
        {
            view.Refresh();
            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void StarShowsTitleEnergyGenerationAndUpgrade()
        {
            NodeController star = Node("Star", Faction.Player, 25);
            panel.Open(star, Vector2.zero);

            Assert.IsTrue(view.IsVisible);
            Assert.AreEqual("Estrella · Nivel 1", view.Line(0));
            Assert.AreEqual("Energía 25 / 50", view.Line(1));
            Assert.AreEqual("Generación: 0.50 E/s", view.Line(2), "13.4 y 4.2: 1 / 2.00 s.");
            Assert.AreEqual(NodeInfoPanel.ButtonLook.Enabled, view.Button);
            Assert.AreEqual("Mejorar · 20", view.ButtonText);
        }

        [Test]
        public void MagnetarShowsItsFireRate()
        {
            panel.Open(Node("Magnetar", Faction.Player, 0, 5), Vector2.zero);
            Assert.AreEqual("Cadencia: 4.3 disparos/s", view.Line(2));
        }

        [Test]
        public void MissingEnergyShowsTheButtonWithTheCostInRed()
        {
            panel.Open(Node("Star", Faction.Player, 5), Vector2.zero);

            Assert.AreEqual(NodeInfoPanel.ButtonLook.MissingEnergy, view.Button, "13.3: deshabilitado, nunca oculto.");
            StringAssert.Contains("<color=#F2456B>20</color>", view.ButtonText);
        }

        [Test]
        public void MaxLevelReplacesTheButton()
        {
            panel.Open(Node("Star", Faction.Player, 50, 5), Vector2.zero);

            Assert.AreEqual(NodeInfoPanel.ButtonLook.MaxLevel, view.Button);
            Assert.AreEqual("NIVEL MÁXIMO", view.ButtonText);
        }

        [Test]
        public void ForeignNodeHasNoButton()
        {
            panel.Open(Node("Star", Faction.Enemy, 50), Vector2.zero);

            Assert.AreEqual(NodeInfoPanel.ButtonLook.Hidden, view.Button);
            Assert.AreEqual("Estrella · Nivel 1", view.Line(0), "La información sí se ve.");
        }

        [Test]
        public void AsteroidShowsSleepingAndTheIgnitionCost()
        {
            panel.Open(Node("Asteroid", Faction.Neutral, 7), Vector2.zero);

            Assert.AreEqual("Asteroide", view.Line(0));
            Assert.AreEqual("Energía 7 / 10", view.Line(1));
            Assert.AreEqual("DORMIDO", view.Line(2));
            Assert.AreEqual("Encender: 10", view.Line(3));
            Assert.AreEqual(NodeInfoPanel.ButtonLook.Hidden, view.Button);
        }

        [Test]
        public void PanelFollowsTheNodeWhileOpen()
        {
            NodeController star = Node("Star", Faction.Player, 19);
            panel.Open(star, Vector2.zero);
            Assert.AreEqual(NodeInfoPanel.ButtonLook.MissingEnergy, view.Button);

            star.AddEnergy(1);
            Assert.AreEqual(NodeInfoPanel.ButtonLook.Enabled, view.Button, "Llegar al coste habilita el botón.");

            star.TryUpgrade(Faction.Player);
            Assert.AreEqual("Estrella · Nivel 2", view.Line(0));
            Assert.AreEqual("Generación: 0.63 E/s", view.Line(2));
            Assert.AreEqual(NodeInfoPanel.ButtonLook.MissingEnergy, view.Button);
        }

        [Test]
        public void CapturedNodeLosesTheButton()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            panel.Open(star, Vector2.zero);

            star.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(Faction.Enemy, 40), star.Snapshot(), Balance.MagnetarResistance));

            Assert.AreEqual(Faction.Enemy, star.Faction);
            Assert.AreEqual(NodeInfoPanel.ButtonLook.Hidden, view.Button);
        }

        [Test]
        public void TheDrawnButtonIsTheClickTarget()
        {
            Vector2 center = new Vector2(1f, -2f);
            panel.Open(Node("Star", Faction.Player, 30), center);

            Assert.Less(Vector2.Distance(center + InfoPanelState.ButtonOffset, view.ButtonPosition), 1e-5f);
            Assert.IsTrue(panel.IsOnButton(view.ButtonPosition));
        }

        // --- Geometría del panel ---

        [Test]
        public void PanelOpensBesideTheNodeWithoutCoveringIt()
        {
            Vector2 node = new Vector2(-3f, 1f);
            Vector2 right = InfoPanelState.CenterBeside(node, false);
            Vector2 left = InfoPanelState.CenterBeside(node, true);

            Assert.Greater(right.x - InfoPanelState.Width / 2f, node.x + 0.5f, "No tapa el círculo de 64 px.");
            Assert.Less(left.x + InfoPanelState.Width / 2f, node.x - 0.5f);
            Assert.AreEqual(node.y, right.y);
        }

        [Test]
        public void PanelSizesFallOnWholePixels()
        {
            foreach (float size in new[] { InfoPanelState.Width, InfoPanelState.Height, InfoPanelState.ButtonSize.x, InfoPanelState.ButtonSize.y })
                Assert.AreEqual(Mathf.Round(size * 64f), size * 64f, 1e-4f);
        }

        // --- Textos (T.7 #39) ---

        [Test]
        public void TextAssetNamesTheFourTypes()
        {
            Assert.IsNotNull(Text, "Falta Assets/Data/UiText.asset.");
            Assert.AreEqual("Asteroide", Text.TypeName(NodeType.Asteroid));
            Assert.AreEqual("Estrella", Text.TypeName(NodeType.Star));
            Assert.AreEqual("Púlsar", Text.TypeName(NodeType.Pulsar));
            Assert.AreEqual("Magnetar", Text.TypeName(NodeType.Magnetar));
        }

        [Test]
        public void DecimalsUseAPointWhateverTheMachineCulture()
        {
            System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("es-ES");
                Assert.AreEqual("Generación: 0.77 E/s", Text.Generation(1f / 1.3f));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
