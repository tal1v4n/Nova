using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Presentation;
using Nova.Systems;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T6-05: el anillo de radio (MagnetarRadiusView, ART-WLD-RAD-*) y el arco del disparo
    // (MagnetarShotArc, ART-WLD-RAD-SHOT) de A.4.1 y 14.9. El arte es de prueba, con la forma de las hojas
    // de verdad: anillos de 384 × 384 con el pivote en el centro y tres piezas por frame en celdas de
    // 256 × 16 con los pivotes de SpriteImportRules. Lo que se comprueba es qué sprite sale y dónde.
    public class MagnetarArtTests
    {
        const float Tick = 0.02f;
        const float FrameSeconds = 1f / MagnetarShotArc.FramesPerSecond;

        static readonly Faction[] Factions = { Faction.Neutral, Faction.Player, Faction.Enemy };

        readonly List<Object> spawned = new List<Object>();
        ProtonRegistry protons;
        NodeRegistry registry;
        MagnetarArt art;
        NodeController far;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            protons = go.AddComponent<ProtonRegistry>();
            registry = go.AddComponent<NodeRegistry>();
            art = TestArt();
            // Destino lejano de los protones de prueba: nunca llegan durante un test.
            far = Node("Star", Faction.Neutral, new Vector2(50f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in spawned)
            {
                if (o != null)
                    Object.DestroyImmediate(o);
            }
            spawned.Clear();
        }

        // --- Arte de prueba ---

        MagnetarArt TestArt()
        {
            var magnetar = ScriptableObject.CreateInstance<MagnetarArt>();
            spawned.Add(magnetar);
            var rings = new Dictionary<Faction, Sprite[]>();
            var shots = new Dictionary<Faction, MagnetarArt.ShotFrames>();
            foreach (Faction faction in Factions)
            {
                var levels = new Sprite[MagnetarArt.Levels];
                for (int i = 0; i < levels.Length; i++)
                    levels[i] = TestSprite($"ring_{faction}_{i + 1}", 384, 384, new Rect(0, 0, 384, 384), new Vector2(0.5f, 0.5f));
                rings[faction] = levels;

                var birth = new Sprite[2];
                var tile = new Sprite[2];
                var tip = new Sprite[2];
                var texture = Texture(512, 48);
                for (int f = 0; f < 2; f++)
                {
                    birth[f] = Piece(texture, $"shot_{faction}_birth_{f}", new Rect(f * 256, 32, 16, 16), new Vector2(0f, 0.5f));
                    tile[f] = Piece(texture, $"shot_{faction}_tile_{f}", new Rect(f * 256, 16, 256, 16), new Vector2(1f, 0.5f));
                    tip[f] = Piece(texture, $"shot_{faction}_tip_{f}", new Rect(f * 256, 0, 32, 16), new Vector2(24f / 32f, 0.5f));
                }
                shots[faction] = new MagnetarArt.ShotFrames(birth, tile, tip);
            }
            magnetar.Configure(rings[Faction.Neutral], rings[Faction.Player], rings[Faction.Enemy],
                shots[Faction.Neutral], shots[Faction.Player], shots[Faction.Enemy]);
            return magnetar;
        }

        Texture2D Texture(int width, int height)
        {
            var texture = new Texture2D(width, height) { filterMode = FilterMode.Point };
            spawned.Add(texture);
            return texture;
        }

        Sprite TestSprite(string name, int width, int height, Rect rect, Vector2 pivot) =>
            Piece(Texture(width, height), name, rect, pivot);

        Sprite Piece(Texture2D texture, string name, Rect rect, Vector2 pivot)
        {
            Sprite sprite = Sprite.Create(texture, rect, pivot, 64, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            spawned.Add(sprite);
            return sprite;
        }

        // --- Tablero ---

        NodeController Node(string type, Faction faction, Vector2 position, int level = 1)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, 10, type == "Asteroid" ? 0 : level);
            go.AddComponent<MagnetarTurret>().Configure(Balance, protons);
            registry.Register(node);
            return node;
        }

        MagnetarShotArc Arc(NodeController node, MagnetarArt magnetarArt = null)
        {
            var arc = node.gameObject.AddComponent<MagnetarShotArc>();
            arc.Configure(magnetarArt ?? art);
            arc.Subscribe();
            return arc;
        }

        MagnetarRadiusView Radius()
        {
            var view = registry.gameObject.AddComponent<MagnetarRadiusView>();
            view.Configure(art, registry);
            return view;
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

        // --- MagnetarArt ---

        [Test]
        public void TheArtIsCompleteOnlyWithEveryRingAndEveryShot()
        {
            Assert.IsTrue(art.IsComplete);
            var empty = ScriptableObject.CreateInstance<MagnetarArt>();
            spawned.Add(empty);
            Assert.IsFalse(empty.IsComplete, "Sin asignar, la vista provisional sigue a cargo.");
        }

        [Test]
        public void EachFactionAndLevelHasItsOwnRing()
        {
            foreach (Faction faction in Factions)
            {
                for (int level = 1; level <= MagnetarArt.Levels; level++)
                    Assert.AreEqual($"ring_{faction}_{level}", art.RingFor(faction, level).name);
            }
            Assert.IsNull(art.RingFor(Faction.Enemy, 6));
        }

        // --- Anillo ---

        [Test]
        public void EveryRingIsAlwaysVisible()
        {
            NodeController neutral = Node("Magnetar", Faction.Neutral, new Vector2(-3f, 0f), 3);
            NodeController enemy = Node("Magnetar", Faction.Enemy, new Vector2(3f, 0f), 5);
            NodeController own = Node("Magnetar", Faction.Player, new Vector2(0f, 3f), 2);
            MagnetarRadiusView view = Radius();

            view.Sync();

            Assert.IsTrue(view.IsShowing(neutral), "A.8 #1: el neutral bloquea a los dos bandos.");
            Assert.IsTrue(view.IsShowing(enemy), "A.8 #1: el enemigo es la ruta que hay que esquivar.");
            Assert.IsTrue(view.IsShowing(own), "A.8 #1: el propio también, sin seleccionarlo.");
            Assert.AreEqual("ring_Neutral_3", view.RingOf(neutral).sprite.name);
            Assert.AreEqual("ring_Enemy_5", view.RingOf(enemy).sprite.name);
            Assert.AreEqual("ring_Player_2", view.RingOf(own).sprite.name);
        }

        [Test]
        public void TheRingIsCenteredOnTheMagnetarAndBehindEverything()
        {
            NodeController enemy = Node("Magnetar", Faction.Enemy, new Vector2(1.25f, -2f));
            MagnetarRadiusView view = Radius();

            view.Sync();

            SpriteRenderer ring = view.RingOf(enemy);
            Assert.AreEqual((Vector2)enemy.transform.position, (Vector2)ring.transform.position);
            Assert.AreEqual(BoardOrder.BehindNode, ring.sortingOrder, "14.7: por debajo de la capa 0.");
            Assert.Less(ring.sortingOrder, BoardOrder.Strokes);
        }

        [Test]
        public void UpgradingChangesTheRing()
        {
            NodeController enemy = Node("Magnetar", Faction.Enemy, Vector2.zero);
            MagnetarRadiusView view = Radius();
            view.Sync();
            Assert.AreEqual("ring_Enemy_1", view.RingOf(enemy).sprite.name);

            enemy.AddEnergy(200);
            Assert.IsTrue(enemy.TryUpgrade(Faction.Enemy).IsAllowed);
            view.Sync();

            Assert.AreEqual("ring_Enemy_2", view.RingOf(enemy).sprite.name);
        }

        [Test]
        public void AStolenMagnetarTakesTheRingOfItsNewFaction()
        {
            NodeController neutral = Node("Magnetar", Faction.Neutral, Vector2.zero, 4);
            MagnetarRadiusView view = Radius();
            view.Sync();

            neutral.Steal(Faction.Player);
            view.Sync();

            Assert.IsTrue(view.IsShowing(neutral));
            Assert.AreEqual("ring_Player_4", view.RingOf(neutral).sprite.name);
        }

        [Test]
        public void OnlyMagnetarsHaveARing()
        {
            NodeController star = Node("Star", Faction.Enemy, Vector2.zero);
            NodeController asteroid = Node("Asteroid", Faction.Player, new Vector2(2f, 0f));
            MagnetarRadiusView view = Radius();

            view.Sync();
            Assert.IsFalse(view.IsShowing(star));
            Assert.IsFalse(view.IsShowing(asteroid));

            asteroid.ChangeType(Definition("Magnetar"), 1);
            view.Sync();
            Assert.IsTrue(view.IsShowing(asteroid), "Encendido como Magnetar, ya tiene radio.");
        }

        [Test]
        public void ARingLeavesWithItsMagnetar()
        {
            NodeController enemy = Node("Magnetar", Faction.Enemy, Vector2.zero);
            MagnetarRadiusView view = Radius();
            view.Sync();
            Assert.IsNotNull(view.RingOf(enemy));

            registry.Unregister(enemy);
            view.Sync();

            Assert.IsNull(view.RingOf(enemy));
        }

        [Test]
        public void WithoutArtThereIsNoRing()
        {
            NodeController enemy = Node("Magnetar", Faction.Enemy, Vector2.zero);
            var view = registry.gameObject.AddComponent<MagnetarRadiusView>();
            view.Configure(null, registry);

            view.Sync();

            Assert.IsFalse(view.IsShowing(enemy), "Sin arte, el anillo es el de ProvisionalMagnetarView.");
        }

        // --- Arco ---

        [Test]
        public void EachShotShowsTheArcInTheFactionOfTheMagnetar()
        {
            foreach (Faction faction in Factions)
            {
                NodeController magnetar = Node("Magnetar", faction, new Vector2(0f, 10f * (int)faction));
                MagnetarShotArc arc = Arc(magnetar);
                Faction hostile = faction == Faction.Player ? Faction.Enemy : Faction.Player;
                Fly(hostile, 10, (Vector2)magnetar.transform.position + new Vector2(1f, 0f));

                magnetar.GetComponent<MagnetarTurret>().Tick(Tick);

                Assert.IsTrue(arc.IsShowing, $"{faction}: el arco sale en el disparo.");
                Assert.AreEqual(0, arc.Frame);
                Assert.AreEqual($"shot_{faction}_birth_0", arc.Birth.sprite.name);
                Assert.AreEqual($"shot_{faction}_tip_0", arc.Tip.sprite.name);
                arc.Unsubscribe();
            }
        }

        [Test]
        public void TheArcLastsTwoFramesAtTwelveFps()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, Vector2.zero);
            MagnetarShotArc arc = Arc(magnetar);
            Fly(Faction.Player, 10, new Vector2(1f, 0f));
            magnetar.GetComponent<MagnetarTurret>().Tick(Tick);

            arc.Advance(FrameSeconds * 0.5f);
            Assert.AreEqual(0, arc.Frame);
            arc.Advance(FrameSeconds);
            Assert.AreEqual(1, arc.Frame);
            Assert.AreEqual("shot_Enemy_tip_1", arc.Tip.sprite.name);
            arc.Advance(FrameSeconds);

            Assert.IsFalse(arc.IsShowing, "14.9: 2 frames, 0.17 s, sin bucle.");
            Assert.IsFalse(arc.Root.gameObject.activeSelf);
            arc.Unsubscribe();
        }

        [Test]
        public void TheArcRunsFromTheCenterToTheProtonUnderTheNode()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, Vector2.zero);
            MagnetarShotArc arc = Arc(magnetar);
            Fly(Faction.Player, 10, new Vector2(0f, 1.25f));

            magnetar.GetComponent<MagnetarTurret>().Tick(Tick);

            Assert.AreEqual(Vector3.zero, arc.Birth.transform.position, "Nace en el centro del Magnetar.");
            Assert.AreEqual(0f, Vector2.Distance(new Vector2(0f, 1.25f), arc.Tip.transform.position), 1e-4f,
                "El contacto de la punta, en el protón.");
            Assert.AreEqual(90f, arc.Root.eulerAngles.z, 1e-3f);
            Assert.AreEqual(BoardOrder.Strokes, arc.Tip.sortingOrder, "14.7, capa 0: debajo del nodo.");
            Assert.Less(arc.Tip.sortingOrder, BoardOrder.Body);
            Assert.Less(arc.Tip.sortingOrder, BoardOrder.Proton, "El protón va por encima del arco que le llega.");
            arc.Unsubscribe();
        }

        [Test]
        public void TheTileIsAnchoredAtTheTipAndCroppedAtTheBirth()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, Vector2.zero);
            MagnetarShotArc arc = Arc(magnetar);
            Fly(Faction.Player, 10, new Vector2(1.5f, 0f)); // 96 px

            magnetar.GetComponent<MagnetarTurret>().Tick(Tick);

            SpriteRenderer tile = arc.Tiles[0];
            Assert.IsTrue(tile.enabled);
            Assert.AreEqual(96 - 24 - 16, tile.sprite.rect.width, "Del borde del nacimiento al de la punta.");
            Assert.AreEqual(256f, tile.sprite.rect.xMax, "Se quedan las columnas del lado de la punta.");
            Assert.AreEqual((96 - 24) / 64f, tile.transform.localPosition.x, 1e-5f, "El tramo toca la punta.");
            arc.Unsubscribe();
        }

        [Test]
        public void TheTipFollowsTheProton()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, Vector2.zero);
            MagnetarShotArc arc = Arc(magnetar);
            Proton proton = Fly(Faction.Player, 10, new Vector2(1f, 0f));
            magnetar.GetComponent<MagnetarTurret>().Tick(Tick);

            proton.transform.position = new Vector3(1f, 0.5f, 0f);
            arc.Advance(FrameSeconds);

            Assert.AreEqual(new Vector2(1f, 0.5f), arc.TipPosition, "14.9: la punta sigue al protón.");
            arc.Unsubscribe();
        }

        [Test]
        public void AShotDownProtonKeepsTheTipWhereItFell()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, Vector2.zero);
            MagnetarShotArc arc = Arc(magnetar);
            Proton proton = Fly(Faction.Player, Balance.MagnetarShotDamage, new Vector2(1f, 0f));

            magnetar.GetComponent<MagnetarTurret>().Tick(Tick);
            Assert.IsFalse(proton.InFlight, "El disparo lo derriba.");
            proton.transform.position = new Vector3(-4f, 4f, 0f); // el pool lo reutiliza en otro sitio
            arc.Advance(FrameSeconds);

            Assert.IsTrue(arc.IsShowing, "El arco completa sus 2 frames encima del FX-KILL.");
            Assert.AreEqual(new Vector2(1f, 0f), arc.TipPosition);
            arc.Unsubscribe();
        }

        [Test]
        public void ANewShotCutsThePreviousArc()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, Vector2.zero);
            MagnetarShotArc arc = Arc(magnetar);
            MagnetarTurret turret = magnetar.GetComponent<MagnetarTurret>();
            Proton first = Fly(Faction.Player, 10, new Vector2(1f, 0f));
            turret.Tick(Tick);
            arc.Advance(FrameSeconds);
            Assert.AreEqual(1, arc.Frame);

            // El reloj del arco no avanza mientras la torreta recarga: el segundo disparo llega con el
            // primer arco todavía en su frame 1, como con el Rayo Cósmico (0.16 s entre disparos).
            first.Vanish();
            Proton second = Fly(Faction.Player, 10, new Vector2(0f, 1f));
            int shots = 0;
            turret.OnShot += (m, p) => shots++;
            for (int i = 0; i < 100 && shots == 0; i++)
                turret.Tick(Tick);

            Assert.AreEqual(1, shots);
            Assert.IsTrue(arc.IsShowing);
            Assert.AreEqual(0, arc.Frame, "14.9: el arco nuevo corta al anterior.");
            Assert.AreEqual(new Vector2(0f, 1f), arc.TipPosition);
            arc.Unsubscribe();
        }

        [Test]
        public void TheProvisionalViewStepsAsideWithArt()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, Vector2.zero);
            var provisional = magnetar.gameObject.AddComponent<ProvisionalMagnetarView>();
            provisional.Subscribe();
            MagnetarShotArc arc = Arc(magnetar);
            Fly(Faction.Player, 10, new Vector2(1f, 0f));

            provisional.Refresh();
            magnetar.GetComponent<MagnetarTurret>().Tick(Tick);

            Assert.IsFalse(provisional.Ring.enabled, "El anillo es de MagnetarRadiusView.");
            Assert.IsFalse(provisional.Shot.enabled, "El disparo es de MagnetarShotArc.");
            Assert.IsTrue(arc.IsShowing);
            provisional.Unsubscribe();
            arc.Unsubscribe();
        }

        [Test]
        public void WithoutArtTheProvisionalViewStaysInCharge()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, Vector2.zero);
            var provisional = magnetar.gameObject.AddComponent<ProvisionalMagnetarView>();
            provisional.Subscribe();
            var empty = ScriptableObject.CreateInstance<MagnetarArt>();
            spawned.Add(empty);
            MagnetarShotArc arc = Arc(magnetar, empty);
            Fly(Faction.Player, 10, new Vector2(1f, 0f));

            provisional.Refresh();
            magnetar.GetComponent<MagnetarTurret>().Tick(Tick);

            Assert.IsTrue(provisional.Ring.enabled);
            Assert.IsTrue(provisional.Shot.enabled);
            Assert.IsFalse(arc.IsShowing);
            provisional.Unsubscribe();
            arc.Unsubscribe();
        }
    }
}
