using System.Collections.Generic;
using System.Linq;
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
    // T6-15: la mejora desde el icono sobre el nodo (13.13, 18 #54). Qué icono enseña cada nodo según su Energía, el
    // coste, el cursor, el bando, el nivel máximo, Congelado y Reconvirtiendo; dónde va; y que el clic sobre el icono
    // activo mejora sin tocar la selección mientras el resto del nodo conserva el clic de 13.1. El arte es de
    // prueba, con las medidas de ui_upgrade_icon.json.
    public class UpgradeIconTests
    {
        const float Tick = 0.02f;
        const int Frames = 16;

        readonly List<Object> spawned = new List<Object>();
        NodeRegistry registry;
        SelectionManager selection;
        InputRouter router;
        InfoPanelState panel;
        UpgradeIconState icons;
        UpgradeIcon view;
        HudArt art;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            registry = go.AddComponent<NodeRegistry>();
            var pool = go.AddComponent<ProtonPool>();
            var launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, pool);
            selection = go.AddComponent<SelectionManager>();
            selection.Configure(registry);
            var issuer = go.AddComponent<PlayerCommandIssuer>();
            issuer.Configure(launcher);
            var menu = go.AddComponent<IgnitionMenuState>();
            panel = go.AddComponent<InfoPanelState>();

            art = TestArt();
            icons = go.AddComponent<UpgradeIconState>();
            icons.Configure(art, Balance, registry);
            router = go.AddComponent<InputRouter>();
            router.Configure(Balance, selection, issuer, menu, panel, upgradeIconState: icons);
            view = go.AddComponent<UpgradeIcon>();
            view.Configure(icons, registry);
        }

        [TearDown]
        public void TearDown()
        {
            selection.Clear();
            registry.Clear();
            foreach (Object item in spawned)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }
            spawned.Clear();
        }

        // --- Arte de prueba: 5 filas de 16 celdas de 23 × 16, pivote abajo al centro ---

        HudArt TestArt()
        {
            var hud = ScriptableObject.CreateInstance<HudArt>();
            spawned.Add(hud);
            var texture = new Texture2D(23, 16) { filterMode = FilterMode.Point };
            spawned.Add(texture);
            Sprite[] cells = Enumerable.Range(0, HudArt.UpgradeIconRows * Frames).Select(i =>
            {
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 23, 16), new Vector2(11f / 23f, 0f), 64, 0, SpriteMeshType.FullRect);
                sprite.name = $"ui_upgrade_icon_sheet_{i / Frames}_{i % Frames}";
                spawned.Add(sprite);
                return sprite;
            }).ToArray();
            hud.ConfigureUpgradeIcon(cells, Frames, 12f, new Vector2Int(23, 16), 2, new Vector2Int(15, 15));
            return hud;
        }

        NodeController Node(string type, Faction faction, int energy, int level = -1, Vector2 position = default)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            if (level < 0)
                node.Initialize(Definition(type), faction, energy);
            else
                node.Initialize(Definition(type), faction, energy, level);
            registry.Register(node);
            return node;
        }

        UpgradeIconLook Look(NodeController node) => icons.LookOf(node);

        Vector2 IconCenter(NodeController node) => icons.HitRect(node).center;

        void Hover(Vector2 world, NodeController nodeUnderCursor = null, bool held = false) =>
            icons.SetPointer(world, nodeUnderCursor, held);

        void ClickAt(Vector2 at, NodeController node = null)
        {
            router.PressLeft(at, node);
            router.ReleaseLeft(at, node);
        }

        void WaitBuffer()
        {
            int ticks = Mathf.CeilToInt((Balance.DoubleClickBuffer + Tick) / Tick);
            for (int i = 0; i < ticks; i++)
                router.Step(Tick);
        }

        // --- Estado: Energía y coste (13.13) ---

        // Estrella de nivel 1: la mejora cuesta 20 (16.2) y el margen es 5 (16.1).
        [TestCase(0, UpgradeIconLook.Hidden)]
        [TestCase(14, UpgradeIconLook.Hidden)]
        [TestCase(15, UpgradeIconLook.Hint)]
        [TestCase(19, UpgradeIconLook.Hint)]
        [TestCase(20, UpgradeIconLook.Ready)]
        [TestCase(50, UpgradeIconLook.Ready)]
        public void EnergyDecidesWhetherTheIconShowsAndHow(int energy, UpgradeIconLook expected)
        {
            Assert.AreEqual(expected, Look(Node("Star", Faction.Player, energy)));
        }

        [Test]
        public void TheHintFollowsTheCostOfEachLevel()
        {
            Assert.AreEqual(UpgradeIconLook.Hint, Look(Node("Pulsar", Faction.Player, 35, 2)), "Púlsar 2 → 3 cuesta 40.");
            Assert.AreEqual(UpgradeIconLook.Hidden, Look(Node("Pulsar", Faction.Player, 34, 2, new Vector2(3f, 0f))));
            Assert.AreEqual(UpgradeIconLook.Ready, Look(Node("Magnetar", Faction.Player, 50, 4, new Vector2(-3f, 0f))), "Magnetar 4 → 5 cuesta 50.");
        }

        [Test]
        public void TheMarginComesFromBalanceConfig()
        {
            NodeController star = Node("Star", Faction.Player, 15);
            Assert.AreEqual(UpgradeIconLook.Hint, UpgradeIconState.BaseLook(star, Faction.Player, 5, false));
            Assert.AreEqual(UpgradeIconLook.Hidden, UpgradeIconState.BaseLook(star, Faction.Player, 4, false));
        }

        // --- Estado: el cursor ---

        [Test]
        public void TheCursorOverTheNodeMakesTheIconActive()
        {
            NodeController star = Node("Star", Faction.Player, 20);

            Hover((Vector2)star.transform.position, star);
            Assert.AreEqual(UpgradeIconLook.Active, Look(star));

            Hover(new Vector2(5f, 5f));
            Assert.AreEqual(UpgradeIconLook.Ready, Look(star), "Sin el cursor, listo y difuminado.");
        }

        [Test]
        public void TheCursorDoesNotWakeTheHint()
        {
            NodeController star = Node("Star", Faction.Player, 17);
            Hover((Vector2)star.transform.position, star);
            Assert.AreEqual(UpgradeIconLook.Hint, Look(star), "Sin el coste, el aviso no se pulsa.");
        }

        [Test]
        public void TheCursorOnTheIconCountsAsOverTheNode()
        {
            NodeController star = Node("Star", Faction.Player, 20);

            Hover(IconCenter(star));

            Assert.AreSame(star, icons.HoveredNode, "Subir el cursor al icono no lo apaga.");
            Assert.AreEqual(UpgradeIconLook.Over, Look(star));
        }

        [Test]
        public void PressingOnTheActiveIconShowsItPressed()
        {
            NodeController star = Node("Star", Faction.Player, 20);
            Hover(IconCenter(star));

            Hover(IconCenter(star), null, true);
            Assert.AreEqual(UpgradeIconLook.Pressed, Look(star));

            Hover(new Vector2(5f, 5f), null, true);
            Assert.AreEqual(UpgradeIconLook.Ready, Look(star), "Fuera del icono deja de estar pulsado.");
        }

        [Test]
        public void APressThatStartsOffTheIconDoesNotPressIt()
        {
            NodeController star = Node("Star", Faction.Player, 20);
            Hover(new Vector2(5f, 5f), null, true);

            Hover(IconCenter(star), null, true);

            Assert.AreEqual(UpgradeIconLook.Over, Look(star));
        }

        [Test]
        public void TheCursorOverAnotherNodeDoesNotActivateThisOne()
        {
            NodeController star = Node("Star", Faction.Player, 20);
            NodeController other = Node("Star", Faction.Player, 20, position: new Vector2(3f, 0f));

            Hover((Vector2)other.transform.position, other);

            Assert.AreEqual(UpgradeIconLook.Ready, Look(star));
            Assert.AreEqual(UpgradeIconLook.Active, Look(other));
        }

        // --- Estado: bando, nivel máximo y bloqueos ---

        [TestCase(Faction.Enemy, "Star")]
        [TestCase(Faction.Neutral, "Magnetar")]
        public void ForeignNodesNeverShowIt(Faction faction, string type)
        {
            NodeController node = Node(type, faction, 50);
            Hover((Vector2)node.transform.position, node);
            Assert.AreEqual(UpgradeIconLook.Hidden, Look(node));
        }

        [Test]
        public void AnOwnAsteroidHasNoIcon()
        {
            Assert.AreEqual(UpgradeIconLook.Hidden, Look(Node("Asteroid", Faction.Player, 10)));
        }

        [TestCase("Star", 5)]
        [TestCase("Pulsar", 3)]
        [TestCase("Magnetar", 5)]
        public void MaxLevelHasNoIcon(string type, int maxLevel)
        {
            Assert.AreEqual(UpgradeIconLook.Hidden, Look(Node(type, Faction.Player, 50, maxLevel)));
        }

        [TestCase(NodeStateFlags.Frozen)]
        [TestCase(NodeStateFlags.Converting)]
        public void FrozenOrConvertingHasNoIcon(NodeStateFlags flag)
        {
            NodeController star = Node("Star", Faction.Player, 50);
            Assert.IsTrue(star.SetStateFlag(flag, true));
            Hover((Vector2)star.transform.position, star);
            Assert.AreEqual(UpgradeIconLook.Hidden, Look(star), "18 #18.");
        }

        [Test]
        public void InfectedStillShowsIt()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            Assert.IsTrue(star.SetStateFlag(NodeStateFlags.Infected, true));
            Assert.AreEqual(UpgradeIconLook.Ready, Look(star), "18 #18: infectado puede mejorar.");
        }

        [Test]
        public void ACapturedNodeLosesItsIcon()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            star.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(Faction.Enemy, 40), star.Snapshot(), Balance.MagnetarResistance));
            Assert.AreEqual(UpgradeIconLook.Hidden, Look(star));
        }

        [Test]
        public void WithoutArtThereIsNoIcon()
        {
            icons.Configure(null, Balance, registry);
            NodeController star = Node("Star", Faction.Player, 50);
            Assert.AreEqual(UpgradeIconLook.Hidden, Look(star));
            Assert.IsNull(icons.IconAt((Vector2)star.transform.position + Vector2.up, false));
        }

        // --- Geometría ---

        [Test]
        public void TheIconSitsCenteredTwoPixelsAboveTheFootprint()
        {
            NodeController star = Node("Star", Faction.Player, 20, position: new Vector2(1f, -1f));
            Assert.IsTrue(star.Definition.TryGetArt(1, out NodeLevelArt levelArt), "La Estrella tiene huella (T6-01).");

            Vector2 anchor = icons.Anchor(star);

            Assert.AreEqual(1f, anchor.x, 1e-6f);
            Assert.AreEqual(-1f + ((levelArt.FootprintPixels + 1) / 2 + 2) / 64f, anchor.y, 1e-6f);
        }

        [Test]
        public void TheHitBoxIsTheDiamondNotTheSonar()
        {
            NodeController star = Node("Star", Faction.Player, 20);
            Vector2 anchor = icons.Anchor(star);

            Rect hit = icons.HitRect(star);

            Assert.AreEqual(15f / 64f, hit.width, 1e-6f);
            Assert.AreEqual(15f / 64f, hit.height, 1e-6f);
            Assert.AreEqual(anchor.x - 7f / 64f, hit.xMin, 1e-6f, "El rombo empieza en la columna 4 de 23; el pivote, en la 11.");
            Assert.AreEqual(anchor.y + 1f / 64f, hit.yMin, 1e-6f, "Pegado arriba en la celda de 16: le sobra una fila abajo.");
        }

        [Test]
        public void TheIconRisesWhenTheFootprintGrows()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            Assert.IsTrue(star.Definition.TryGetArt(1, out NodeLevelArt level1));
            Assert.IsTrue(star.Definition.TryGetArt(2, out NodeLevelArt level2));
            float before = icons.Anchor(star).y;

            Assert.IsTrue(star.TryUpgrade(Faction.Player).IsAllowed);

            Assert.AreEqual(before + ((level2.FootprintPixels + 1) / 2 - (level1.FootprintPixels + 1) / 2) / 64f,
                icons.Anchor(star).y, 1e-6f);
        }

        // --- El clic ---

        [Test]
        public void ClickOnTheActiveIconUpgradesAndCharges()
        {
            NodeController star = Node("Star", Faction.Player, 30);

            ClickAt(IconCenter(star));

            Assert.AreEqual(2, star.Level);
            Assert.AreEqual(10, star.Energy, "16.2: pasar a nivel 2 cuesta 20.");
            Assert.IsFalse(selection.HasSelection, "El clic es del icono: no selecciona.");
        }

        [Test]
        public void ClickOnTheHintDoesNothing()
        {
            NodeController star = Node("Star", Faction.Player, 17);

            ClickAt(IconCenter(star));

            Assert.AreEqual(1, star.Level);
            Assert.AreEqual(17, star.Energy);
        }

        [Test]
        public void ClickOnTheRestOfTheNodeKeepsItsClick()
        {
            NodeController star = Node("Star", Faction.Player, 30);

            ClickAt((Vector2)star.transform.position, star);

            Assert.AreEqual(1, star.Level, "Fuera del rombo, el clic de 13.1.");
            CollectionAssert.AreEqual(new[] { star }, selection.Selected, "Sin selección, el clic elige.");
        }

        [Test]
        public void ClickOnTheIconWithASelectionUpgradesWithoutSendingOrTouchingIt()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            NodeController other = Node("Star", Faction.Player, 30, position: new Vector2(-4f, 0f));
            selection.Select(other);

            ClickAt(IconCenter(star));
            WaitBuffer();

            Assert.AreEqual(2, star.Level);
            Assert.AreEqual(30, other.Energy, "El clic en el icono no es un envío.");
            CollectionAssert.AreEqual(new[] { other }, selection.Selected);
        }

        [Test]
        public void ClickOnTheIconOfASelectedNodeKeepsItSelected()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            selection.Select(star);

            ClickAt(IconCenter(star));

            Assert.AreEqual(2, star.Level);
            Assert.IsTrue(selection.IsSelected(star), "No es el clic de 13.1 sobre un nodo seleccionado.");
        }

        [Test]
        public void ADragFromTheIconIsALasso()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            Vector2 from = IconCenter(star);

            router.PressLeft(from, null);
            router.MoveTo(from + new Vector2(1f, 1f));
            router.ReleaseLeft(from + new Vector2(1f, 1f), null);

            Assert.AreEqual(1, star.Level);
        }

        [Test]
        public void ClickWhereAForeignIconWouldBeDoesNothing()
        {
            NodeController enemy = Node("Star", Faction.Enemy, 50);

            ClickAt(IconCenter(enemy));

            Assert.AreEqual(1, enemy.Level);
            Assert.AreEqual(50, enemy.Energy);
        }

        // --- La vista ---

        [Test]
        public void TheViewDrawsOneIconPerVisibleNodeWithItsRow()
        {
            NodeController ready = Node("Star", Faction.Player, 30);
            NodeController hint = Node("Star", Faction.Player, 16, position: new Vector2(3f, 0f));
            Node("Star", Faction.Enemy, 50, position: new Vector2(-3f, 0f));
            Node("Star", Faction.Player, 0, position: new Vector2(0f, 2f));

            view.Advance(0f);

            Assert.AreEqual(2, view.VisibleCount);
            SpriteRenderer first = view.Icons[0];
            Assert.AreSame(art.UpgradeIcon((int)UpgradeIconLook.Ready, 0), first.sprite);
            Assert.AreEqual(icons.Anchor(ready), (Vector2)first.transform.position);
            Assert.AreEqual(BoardOrder.Energy, first.sortingOrder, "Capa 6 de 14.7.");
            Assert.AreSame(art.UpgradeIcon((int)UpgradeIconLook.Hint, 0), view.Icons[1].sprite);
            Assert.AreEqual(icons.Anchor(hint), (Vector2)view.Icons[1].transform.position);
        }

        [Test]
        public void TheViewLoopsAtTwelveFramesPerSecond()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            Hover((Vector2)star.transform.position, star);

            view.Advance(1f / 12f * 3f);
            Assert.AreSame(art.UpgradeIcon((int)UpgradeIconLook.Active, 3), view.Icons[0].sprite);

            view.Advance(1f / 12f * 13f);
            Assert.AreSame(art.UpgradeIcon((int)UpgradeIconLook.Active, 0), view.Icons[0].sprite, "16 frames en bucle.");
        }

        [Test]
        public void TheIconGoesAwayAfterTheUpgradeUntilTheNextCostIsNear()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            view.Advance(0f);
            Assert.AreEqual(1, view.VisibleCount);

            ClickAt(IconCenter(star));
            view.Advance(0f);

            Assert.AreEqual(10, star.Energy);
            Assert.AreEqual(0, view.VisibleCount, "Con 10 y el nivel 3 a 30, falta más que el margen.");
        }
    }
}
