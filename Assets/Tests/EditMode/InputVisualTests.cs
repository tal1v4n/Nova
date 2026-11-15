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
    // T6-06: la entrada del Jugador con su arte (A.4.3): la línea de envío, el anillo de selección, el lazo y la
    // retícula de apuntado, y la atenuación de lo que no es objetivo. Con sprites sintéticos, nombrados por lo que
    // son, sobre copias en memoria de los NodeDefinition: no depende de que el arte esté sincronizado.
    public class InputVisualTests
    {
        const float Tick = 0.02f;
        const float Frame = 1f / 12f;
        const float Px = 1f / 64f;

        // Las tallas de tools/gen_select_v1.py (world_select_tallas.json).
        static readonly int[] Radii = { 24, 28, 32, 36, 40 };
        const int Air = 4;

        readonly List<Object> spawned = new List<Object>();
        NodeRegistry registry;
        SelectionManager selection;
        PlayerCommandIssuer issuer;
        InputRouter router;
        SendLineState sendLine;
        PowerAimState aim;
        LassoState lasso;
        PowerBeamSystem beams;
        GameObject systems;
        InputArt art;

        static NodeDefinition Asset(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            systems = new GameObject("Systems");
            spawned.Add(systems);
            registry = systems.AddComponent<NodeRegistry>();
            beams = systems.AddComponent<PowerBeamSystem>();
            var pool = systems.AddComponent<ProtonPool>();
            var launcher = systems.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, pool);
            selection = systems.AddComponent<SelectionManager>();
            selection.Configure(registry);
            issuer = systems.AddComponent<PlayerCommandIssuer>();
            issuer.Configure(launcher);
            var menu = systems.AddComponent<IgnitionMenuState>();
            var panel = systems.AddComponent<InfoPanelState>();
            aim = systems.AddComponent<PowerAimState>();
            sendLine = systems.AddComponent<SendLineState>();
            sendLine.Configure(selection, menu, aim);
            lasso = systems.AddComponent<LassoState>();
            router = systems.AddComponent<InputRouter>();
            router.Configure(Balance, selection, issuer, menu, panel, null, sendLine, aim, null, lasso);
            art = Art();
        }

        [TearDown]
        public void TearDown()
        {
            aim.End();
            selection.Clear();
            registry.Clear();
            foreach (Object o in spawned)
            {
                if (o != null)
                    Object.DestroyImmediate(o);
            }
            spawned.Clear();
        }

        // --- Arte sintético ---

        Sprite Sprite(string name, int w, int h, Vector2 pivot)
        {
            var texture = new Texture2D(w, h);
            spawned.Add(texture);
            Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, w, h), pivot, 64);
            sprite.name = name;
            spawned.Add(sprite);
            return sprite;
        }

        InputArt.Frames Sheet(string name, int frames, int w, int h, Vector2 pivot)
        {
            var sprites = new Sprite[frames];
            for (int i = 0; i < frames; i++)
                sprites[i] = Sprite($"{name}_{i}", w, h, pivot);
            return new InputArt.Frames(sprites);
        }

        InputArt.Frames[] PerSize(string name, int frames, int cell)
        {
            var sheets = new InputArt.Frames[Radii.Length];
            for (int i = 0; i < Radii.Length; i++)
                sheets[i] = Sheet($"{name}_{Radii[i]}", frames, cell, cell, new Vector2(0.5f, 0.5f));
            return sheets;
        }

        InputArt Art()
        {
            var input = ScriptableObject.CreateInstance<InputArt>();
            spawned.Add(input);
            input.Configure(
                Sheet("link_birth", 16, 16, 16, new Vector2(0f, 0.5f)),
                Sheet("link_tile", 16, 256, 16, new Vector2(1f, 0.5f)),
                Sheet("link_tip", 16, 32, 16, new Vector2(0.75f, 0.5f)),
                Air, (int[])Radii.Clone(), PerSize("ring", 16, 96),
                Sprite("lasso_corner", 8, 8, new Vector2(0f, 1f)),
                Sheet("lasso_edge", 8, 8, 1, new Vector2(0f, 0.5f)),
                PerSize("aim_zero", 4, 112), PerSize("aim_decay", 4, 112), PerSize("aim_wormhole", 4, 112));
            return input;
        }

        // Copia en memoria del NodeDefinition real, con un sprite y una huella fija en cada nivel.
        NodeDefinition Definition(string asset, int footprint)
        {
            NodeDefinition definition = Object.Instantiate(Asset(asset));
            spawned.Add(definition);
            var levels = new NodeLevelArt[definition.ArtLevels];
            for (int i = 0; i < levels.Length; i++)
                levels[i] = new NodeLevelArt(Sprite($"{asset}_n", 96, 96, new Vector2(0.5f, 0.5f)),
                    Sprite($"{asset}_p", 96, 96, new Vector2(0.5f, 0.5f)), Sprite($"{asset}_e", 96, 96, new Vector2(0.5f, 0.5f)), footprint);
            definition.ConfigureArt(levels);
            return definition;
        }

        NodeController Node(string asset, Faction faction, int energy, Vector2 position, int footprint = 61, int level = 1)
        {
            var go = new GameObject(asset);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            NodeDefinition definition = Definition(asset, footprint);
            node.Initialize(definition, faction, energy, definition.MaxLevel == 0 ? 0 : level);
            var visual = go.AddComponent<NodeVisual>();
            visual.Subscribe();
            visual.Refresh();
            go.AddComponent<NodeFreeze>().Configure(Balance);
            go.AddComponent<PulsarCaster>().Configure(Balance, new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") }, beams);
            registry.Register(node);
            return node;
        }

        // Un Púlsar propio con el poder listo y en modo apuntado.
        NodeController AimWith(PowerType power)
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 100, new Vector2(-4f, 0f), level: 3);
            Assert.IsTrue(issuer.ChargePower(pulsar, power).IsAllowed);
            for (int i = 0; i < 2000 && pulsar.GetComponent<PulsarCaster>().State != PowerChargeState.Ready; i++)
                pulsar.GetComponent<PulsarCaster>().Tick(Tick);
            Assert.IsTrue(aim.Begin(pulsar, Faction.Player));
            Assert.AreEqual(power, aim.Power);
            return pulsar;
        }

        T View<T>() where T : Component
        {
            var view = systems.AddComponent<T>();
            return view;
        }

        // --- Tallas ---

        // Las huellas de A.2 y la talla que les da talla_de() en tools/gen_select_v1.py.
        [TestCase(39, 24)] // Estrella N1
        [TestCase(41, 28)] // Magnetar N1
        [TestCase(44, 28)] // Púlsar N1
        [TestCase(51, 32)] // Estrella N2
        [TestCase(60, 36)] // Púlsar N2, Magnetar N2-N3
        [TestCase(61, 36)] // Estrella N3
        [TestCase(67, 40)] // Magnetar N4-N5
        [TestCase(68, 40)] // Estrella N4, Púlsar N3
        [TestCase(72, 40)] // Estrella N5
        [TestCase(200, 40)] // más que ninguna: la mayor
        public void TheSizeIsTheFirstThatLeavesTheAir(int footprint, int radius)
        {
            Assert.AreEqual(radius, art.RingRadiusPixels(art.SizeFor(footprint)));
        }

        [Test]
        public void TheArtIsCompleteOnlyWithEverything()
        {
            Assert.IsTrue(art.IsComplete);
            var empty = ScriptableObject.CreateInstance<InputArt>();
            spawned.Add(empty);
            Assert.IsFalse(empty.IsComplete);
        }

        // --- El lazo ---

        [Test]
        public void ALeftDragPublishesTheLassoAndReleasingClearsIt()
        {
            router.PressLeft(new Vector2(0f, 0f), null);
            Assert.IsFalse(lasso.IsActive, "Una pulsación no es un lazo.");
            router.MoveTo(new Vector2(1f, -0.5f));
            Assert.IsTrue(lasso.IsActive);
            Assert.AreEqual(new Vector2(0f, 0f), lasso.From);
            Assert.AreEqual(new Vector2(1f, -0.5f), lasso.To);

            router.ReleaseLeft(new Vector2(1f, -0.5f), null);
            Assert.IsFalse(lasso.IsActive);
        }

        [Test]
        public void AClickIsNeverALasso()
        {
            router.PressLeft(new Vector2(0f, 0f), null);
            router.MoveTo(new Vector2(1f * Px, 1f * Px)); // por debajo del umbral
            Assert.IsFalse(lasso.IsActive);
            router.ReleaseLeft(new Vector2(1f * Px, 1f * Px), null);
            Assert.IsFalse(lasso.IsActive);
        }

        [Test]
        public void TheLassoIsMountedLikeTheReference()
        {
            var view = View<LassoView>();
            view.Configure(art, lasso);
            lasso.Set(new Vector2(0f, 0f), new Vector2(64f * Px, 40f * Px));
            view.Advance(0f);

            Assert.IsTrue(view.IsVisible);
            Assert.AreEqual(new RectInt(0, 0, 65, 41), view.Pixels);
            // 65 - 12 = 53 px por lado horizontal: 7 tramos; 41 - 12 = 29 por lado vertical: 4.
            Assert.AreEqual(2 * 7 + 2 * 4, view.TileCount);
            // Las esquinas, reflejadas desde la de arriba a la izquierda, en las cuatro esquinas de fuera.
            Assert.AreEqual(new Vector3(0f, 41f * Px, 0f), view.Corners[0].transform.localPosition);
            Assert.AreEqual(new Vector3(65f * Px, 41f * Px, 0f), view.Corners[1].transform.localPosition);
            Assert.IsTrue(view.Corners[1].flipX && !view.Corners[1].flipY);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), view.Corners[2].transform.localPosition);
            Assert.IsTrue(view.Corners[2].flipY && !view.Corners[2].flipX);
            Assert.IsTrue(view.Corners[3].flipX && view.Corners[3].flipY);
            // El último tramo de arriba se recorta a lo que queda: 53 = 6 · 8 + 5.
            Assert.AreEqual(5f, view.Tiles[12].sprite.rect.width, "arriba, el séptimo tramo");
        }

        [Test]
        public void ASmallLassoCropsItsCornersSoTheyDoNotCross()
        {
            var view = View<LassoView>();
            view.Configure(art, lasso);
            lasso.Set(Vector2.zero, new Vector2(8f * Px, 4f * Px)); // 9 × 5 px
            view.Advance(0f);
            Assert.AreEqual(new Vector2(5f, 3f), view.Corners[0].sprite.rect.size);
            Assert.AreEqual(0, view.TileCount, "Sin sitio para el tramo.");
        }

        [Test]
        public void TheLassoMarchesAtTwelveFramesPerSecond()
        {
            var view = View<LassoView>();
            view.Configure(art, lasso);
            lasso.Set(Vector2.zero, new Vector2(1f, 1f));
            view.Advance(0f);
            Assert.AreEqual("lasso_edge_0", view.Tiles[0].sprite.name);
            view.Advance(Frame);
            Assert.AreEqual("lasso_edge_1", view.Tiles[0].sprite.name);
            view.Advance(Frame * 7);
            Assert.AreEqual("lasso_edge_0", view.Tiles[0].sprite.name, "8 frames en bucle.");
        }

        [Test]
        public void WithoutArtOrOutsideTheGestureTheLassoIsNotDrawn()
        {
            var view = View<LassoView>();
            view.Configure(art, lasso);
            view.Advance(0f);
            Assert.IsFalse(view.IsVisible, "Sin arrastre no hay lazo.");

            view.Configure(null, lasso);
            lasso.Set(Vector2.zero, Vector2.one);
            view.Advance(0f);
            Assert.IsFalse(view.IsVisible, "Sin arte el lazo no se dibuja: no tenía vista provisional.");
        }

        // --- La línea de envío ---

        [Test]
        public void EachSelectedNodeHasALineThatEndsOnTheCursor()
        {
            NodeController a = Node("Star", Faction.Player, 20, new Vector2(0f, 0f));
            NodeController b = Node("Star", Faction.Player, 20, new Vector2(0f, 2f));
            selection.SelectInRect(new Vector2(-1f, -1f), new Vector2(1f, 3f));
            sendLine.SetCursor(new Vector2(3f, 0f));
            var view = View<SendLineVisual>();
            view.Configure(art, sendLine);
            view.Advance(0f);

            Assert.AreEqual(2, view.VisibleCount);
            Assert.AreEqual(192, view.Lines[0].Length, "3 u del centro al cursor, en píxeles.");
            Assert.AreEqual(new Vector3(3f, 0f, 0f), view.Lines[0].Tip.transform.position);
            Assert.AreEqual(0, view.Lines[0].Frame);
            Assert.AreEqual(SendLineVisual.PhaseStep, view.Lines[1].Frame, "14.9: desfasadas por su orden.");
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
        }

        [Test]
        public void TheLineTileIsCroppedOnTheBirthSide()
        {
            Node("Star", Faction.Player, 20, Vector2.zero);
            selection.SelectInRect(new Vector2(-1f, -1f), new Vector2(1f, 1f));
            sendLine.SetCursor(new Vector2(5f, 0f)); // 320 px: 320 − 24 − 16 = 280 de tramo, 256 + 24
            var view = View<SendLineVisual>();
            view.Configure(art, sendLine);
            view.Advance(0f);
            List<SpriteRenderer> tiles = view.Lines[0].Tiles;
            Assert.AreEqual(256f, tiles[0].sprite.rect.width);
            Assert.AreEqual(24f, tiles[1].sprite.rect.width);
        }

        [Test]
        public void WithArtTheProvisionalLineStepsAside()
        {
            Node("Star", Faction.Player, 20, Vector2.zero);
            selection.SelectInRect(new Vector2(-1f, -1f), new Vector2(1f, 1f));
            sendLine.SetCursor(new Vector2(2f, 0f));
            var provisional = View<ProvisionalSendLineView>();
            provisional.Configure(sendLine);
            provisional.Refresh();
            Assert.AreEqual(1, provisional.VisibleCount, "Sin el arte, la provisional.");

            View<SendLineVisual>().Configure(art, sendLine);
            provisional.Refresh();
            Assert.AreEqual(0, provisional.VisibleCount);
        }

        // --- El anillo de selección ---

        [Test]
        public void EachSelectedNodeHasItsRingWithTheSizeOfItsFootprint()
        {
            Node("Star", Faction.Player, 20, new Vector2(0f, 0f), footprint: 39);
            Node("Magnetar", Faction.Player, 20, new Vector2(3f, 0f), footprint: 67);
            Node("Star", Faction.Player, 20, new Vector2(6f, 0f), footprint: 51); // fuera del lazo
            selection.SelectInRect(new Vector2(-1f, -1f), new Vector2(4f, 1f));
            var view = View<SelectionRingView>();
            view.Configure(art, selection);
            view.Advance(0f);

            Assert.AreEqual(2, view.VisibleCount);
            StringAssert.StartsWith("ring_24_", view.Rings[0].sprite.name);
            StringAssert.StartsWith("ring_40_", view.Rings[1].sprite.name);
            Assert.AreEqual(new Vector3(3f, 0f, 0f), view.Rings[1].transform.position, "En el centro del nodo.");
            Assert.AreEqual(BoardOrder.SelectionRing, view.Rings[0].sortingOrder);
            Assert.Less(BoardOrder.SelectionRing, BoardOrder.Body, "Detrás del nodo.");

            selection.Clear();
            view.Advance(Frame);
            Assert.AreEqual(0, view.VisibleCount);
        }

        [Test]
        public void TwoRingsInDifferentPlacesDoNotTurnTogether()
        {
            Node("Star", Faction.Player, 20, new Vector2(0f, 0f), footprint: 61);
            Node("Star", Faction.Player, 20, new Vector2(1.5f, 0.25f), footprint: 61);
            selection.SelectInRect(new Vector2(-1f, -1f), new Vector2(3f, 1f));
            var view = View<SelectionRingView>();
            view.Configure(art, selection);
            view.Advance(0f);
            // El desfase por posición del idle (A.2.5): cada anillo arranca en su frame.
            for (int i = 0; i < 2; i++)
            {
                uint phase = NodeAnimator.IdlePhase(view.Rings[i].transform.position);
                Assert.AreEqual($"ring_36_{phase % 16}", view.Rings[i].sprite.name);
            }
        }

        [Test]
        public void WithArtTheProvisionalRingStepsAside()
        {
            Node("Star", Faction.Player, 20, Vector2.zero);
            var provisional = View<ProvisionalSelectionView>();
            provisional.Configure(selection);
            View<SelectionRingView>().Configure(art, selection);
            selection.SelectInRect(new Vector2(-1f, -1f), new Vector2(1f, 1f));
            provisional.Refresh();
            Assert.AreEqual(0, provisional.RingCount);
        }

        // --- La retícula de apuntado ---

        [Test]
        public void TheReticleLandsOnlyOnAValidTargetUnderTheCursor()
        {
            AimWith(PowerType.Decay);
            NodeController enemy = Node("Star", Faction.Enemy, 20, new Vector2(2f, 0f), footprint: 61);
            NodeController own = Node("Star", Faction.Player, 20, new Vector2(0f, 2f), footprint: 61);
            var view = View<AimReticleView>();
            view.Configure(art, aim, sendLine, registry);

            sendLine.SetCursor(new Vector2(2.3f, 0.1f)); // dentro de la media huella: 31 px ≈ 0.48 u
            view.Advance(0f);
            Assert.AreSame(enemy, view.Target);
            Assert.IsTrue(view.Reticle.enabled);
            StringAssert.StartsWith("aim_decay_36_", view.Reticle.sprite.name, "Color del poder, talla de la huella.");
            Assert.AreEqual(new Vector3(2f, 0f, 0f), view.Reticle.transform.position);

            sendLine.SetCursor(new Vector2(0f, 2f)); // un nodo propio no es objetivo
            view.Advance(Frame);
            Assert.IsNull(view.Target);
            Assert.IsFalse(view.Reticle.enabled);

            sendLine.SetCursor(new Vector2(2.6f, 0f)); // fuera de la media huella
            view.Advance(Frame);
            Assert.IsFalse(view.Reticle.enabled);
            Assert.IsNotNull(own);
        }

        [Test]
        public void TheReticleEntersAndThenStays()
        {
            AimWith(PowerType.AbsoluteZero);
            Node("Magnetar", Faction.Neutral, 50, new Vector2(2f, 0f), footprint: 41);
            var view = View<AimReticleView>();
            view.Configure(art, aim, sendLine, registry);
            sendLine.SetCursor(new Vector2(2f, 0f));

            var seen = new List<string>();
            view.Advance(0f);
            for (int i = 0; i < 6; i++)
            {
                seen.Add(view.Reticle.sprite.name);
                view.Advance(Frame);
            }
            CollectionAssert.AreEqual(new[] { "aim_zero_28_0", "aim_zero_28_1", "aim_zero_28_2", "aim_zero_28_3", "aim_zero_28_3", "aim_zero_28_3" }, seen);
        }

        [Test]
        public void WithoutAimingThereIsNoReticle()
        {
            Node("Star", Faction.Enemy, 20, new Vector2(2f, 0f));
            var view = View<AimReticleView>();
            view.Configure(art, aim, sendLine, registry);
            sendLine.SetCursor(new Vector2(2f, 0f));
            view.Advance(0f);
            Assert.IsFalse(view.Reticle.enabled);
        }

        // --- La atenuación ---

        [Test]
        public void AimingDimsWhatIsNotATargetAndRestoresIt()
        {
            NodeController pulsar = AimWith(PowerType.Wormhole);
            NodeController enemy = Node("Star", Faction.Enemy, 20, new Vector2(2f, 0f));
            NodeController rock = Node("Asteroid", Faction.Enemy, 5, new Vector2(0f, -2f));
            var view = View<AimDimView>();
            view.Configure(aim, registry);
            view.Refresh();

            Assert.IsTrue(view.IsDimmed(pulsar), "Un nodo propio no es objetivo.");
            Assert.IsTrue(view.IsDimmed(rock), "Ningún Asteroide es objetivo (7.2.1).");
            Assert.IsFalse(view.IsDimmed(enemy));
            Assert.AreEqual(AimDimView.DimColor, pulsar.GetComponent<NodeVisual>().Body.color);
            Assert.AreEqual(Color.white, enemy.GetComponent<NodeVisual>().Body.color);

            aim.End();
            view.Refresh();
            Assert.IsFalse(view.IsDimmed(pulsar));
            Assert.AreEqual(Color.white, pulsar.GetComponent<NodeVisual>().Body.color);
        }

        [Test]
        public void TheProvisionalVeilLeavesTheNodesWithArtToTheDimming()
        {
            AimWith(PowerType.Decay);
            var veil = View<ProvisionalAimView>();
            veil.Configure(aim, registry);
            veil.Refresh();
            Assert.AreEqual(1, veil.VisibleCount, "Sin AimDimView, el velo tapa el Púlsar propio.");

            View<AimDimView>().Configure(aim, registry);
            veil.Refresh();
            Assert.AreEqual(0, veil.VisibleCount);
        }
    }
}
