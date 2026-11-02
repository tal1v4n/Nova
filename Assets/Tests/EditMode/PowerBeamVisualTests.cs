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
    // T6-11: el haz de poder con su arte (PowerBeamVisual, ART-WLD-BEAM-*), de 7.2.2 y A.4.2. El arte es de
    // prueba, con la forma de las hojas de verdad: tres piezas por frame en celdas de 256 × 16 con los
    // pivotes de SpriteImportRules. Lo que se comprueba es qué sprite sale y dónde. El haz dura 0.25 s y
    // termina en el tick 13 (0.26 s).
    public class PowerBeamVisualTests
    {
        const float Tick = 0.02f;
        const int TicksToLand = 13;
        const int Frames = 3;

        static readonly PowerType[] Powers = { PowerType.AbsoluteZero, PowerType.Decay, PowerType.Wormhole };

        readonly List<Object> spawned = new List<Object>();
        PowerBeamSystem beams;
        PowerBeamArt art;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(PowerType type) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{type}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            beams = go.AddComponent<PowerBeamSystem>();
            art = TestArt();
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

        PowerBeamArt TestArt()
        {
            var beamArt = ScriptableObject.CreateInstance<PowerBeamArt>();
            spawned.Add(beamArt);
            var frames = new Dictionary<PowerType, PowerBeamArt.BeamFrames>();
            foreach (PowerType power in Powers)
            {
                var birth = new Sprite[Frames];
                var tile = new Sprite[Frames];
                var tip = new Sprite[Frames];
                var texture = new Texture2D(256 * Frames, 48) { filterMode = FilterMode.Point };
                spawned.Add(texture);
                for (int f = 0; f < Frames; f++)
                {
                    birth[f] = Piece(texture, $"beam_{power}_birth_{f}", new Rect(f * 256, 32, 16, 16), new Vector2(0f, 0.5f));
                    tile[f] = Piece(texture, $"beam_{power}_tile_{f}", new Rect(f * 256, 16, 256, 16), new Vector2(1f, 0.5f));
                    tip[f] = Piece(texture, $"beam_{power}_tip_{f}", new Rect(f * 256, 0, 32, 16), new Vector2(24f / 32f, 0.5f));
                }
                frames[power] = new PowerBeamArt.BeamFrames(birth, tile, tip);
            }
            beamArt.Configure(frames[PowerType.AbsoluteZero], frames[PowerType.Decay], frames[PowerType.Wormhole]);
            return beamArt;
        }

        Sprite Piece(Texture2D texture, string name, Rect rect, Vector2 pivot)
        {
            Sprite sprite = Sprite.Create(texture, rect, pivot, 64, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            spawned.Add(sprite);
            return sprite;
        }

        // --- Tablero ---

        NodeController Node(string type, Faction faction, Vector2 position)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            go.AddComponent<NodeFreeze>().Configure(Balance); // los haces de Cero Absoluto congelan al caer
            go.AddComponent<NodeDecay>().Configure(Balance);
            node.Initialize(Definition(type), faction, 40, 1);
            return node;
        }

        PowerBeam Launch(PowerType power, Vector2 from, Vector2 to)
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, from);
            NodeController target = Node("Star", Faction.Enemy, to);
            return beams.Launch(pulsar, target, Faction.Player, PowerAsset(power));
        }

        PowerBeamVisual View(PowerBeamArt beamArt = null)
        {
            var view = beams.gameObject.AddComponent<PowerBeamVisual>();
            view.Configure(beamArt ?? art, beams);
            return view;
        }

        void Run(int ticks)
        {
            for (int i = 0; i < ticks; i++)
                beams.Tick(Tick);
        }

        // --- Arte ---

        [Test]
        public void TheArtIsCompleteOnlyWithTheThreeBeams()
        {
            Assert.IsTrue(art.IsComplete);
            var partial = ScriptableObject.CreateInstance<PowerBeamArt>();
            spawned.Add(partial);
            partial.Configure(art.For(PowerType.AbsoluteZero), art.For(PowerType.Decay), default);
            Assert.IsFalse(partial.IsComplete);
        }

        [Test]
        public void EachPowerShowsItsOwnBeamWhateverTheFaction()
        {
            PowerBeamVisual view = View();
            for (int i = 0; i < Powers.Length; i++)
                Launch(Powers[i], new Vector2(0f, 2f * i), new Vector2(4f, 2f * i));

            view.Refresh();

            Assert.AreEqual(Powers.Length, view.VisibleCount);
            for (int i = 0; i < Powers.Length; i++)
            {
                Assert.AreEqual($"beam_{Powers[i]}_birth_0", view.Strokes[i].Birth.sprite.name,
                    "7.2.2: el color es el del poder, no el de la facción.");
                Assert.AreEqual($"beam_{Powers[i]}_tip_0", view.Strokes[i].Tip.sprite.name);
            }
        }

        // --- Tiempo ---

        [Test]
        public void TheBeamPlaysThreeFramesAtTwelveFpsWithoutLooping()
        {
            PowerBeamVisual view = View();
            Launch(PowerType.Decay, Vector2.zero, new Vector2(4f, 0f));

            var seen = new List<int>();
            for (int tick = 0; tick < TicksToLand; tick++)
            {
                view.Refresh();
                Assert.AreEqual(1, view.VisibleCount, $"Tick {tick}: el haz se ve mientras dura.");
                int frame = view.Strokes[0].Frame;
                if (seen.Count == 0 || seen[seen.Count - 1] != frame)
                    seen.Add(frame);
                Run(1);
            }

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, seen, "Entra, está y se apaga: nunca vuelve al 0.");
            view.Refresh();
            Assert.AreEqual(0, view.VisibleCount, "Al terminar, el trazo desaparece.");
        }

        [Test]
        public void TheFrameFollowsTheBeamClock()
        {
            PowerBeamVisual view = View();
            PowerBeam beam = Launch(PowerType.AbsoluteZero, Vector2.zero, new Vector2(4f, 0f));

            Run(4); // 0.08 s
            view.Refresh();
            Assert.AreEqual(0, view.Strokes[0].Frame);
            Run(1); // 0.10 s
            view.Refresh();
            Assert.AreEqual(1, view.Strokes[0].Frame);
            StringAssert.StartsWith("beam_AbsoluteZero_tile_1", view.Strokes[0].Tiles[0].sprite.name);
            Run(4); // 0.18 s
            view.Refresh();
            Assert.AreEqual(2, view.Strokes[0].Frame);

            // En pausa no corre el tick fijo: el haz se queda en su frame.
            view.Refresh();
            view.Refresh();
            Assert.AreEqual(2, view.Strokes[0].Frame);
            Assert.IsFalse(beam.IsFinished);
        }

        [Test]
        public void ADissipatedBeamPlaysTheSameAsOneThatLands()
        {
            PowerBeamVisual view = View();
            PowerBeam beam = Launch(PowerType.Wormhole, Vector2.zero, new Vector2(4f, 0f));
            var dissipated = new List<PowerBeam>();
            beams.OnDissipated += dissipated.Add;

            Run(6);
            // El objetivo pasa a ser del lanzador antes de que termine el haz (17 caso 27).
            beam.Target.ApplyImpact(ImpactResolver.Resolve(
                new ProtonSnapshot(Faction.Player, beam.Target.Energy * 2 + 1), beam.Target.Snapshot(), Balance.MagnetarResistance));
            view.Refresh();
            Assert.AreEqual(1, view.VisibleCount, "7.2.2: la validez se comprueba al terminar, no antes.");

            Run(TicksToLand - 6);
            view.Refresh();
            Assert.AreEqual(1, dissipated.Count);
            Assert.AreEqual(0, view.VisibleCount);
        }

        // --- Geometría ---

        [Test]
        public void TheBeamRunsFromThePulsarToTheTargetUnderTheNodes()
        {
            PowerBeamVisual view = View();
            Launch(PowerType.Decay, new Vector2(1f, 1f), new Vector2(1f, 4.5f));

            view.Refresh();
            PowerBeamVisual.Stroke stroke = view.Strokes[0];

            Assert.AreEqual(new Vector3(1f, 1f, 0f), stroke.Birth.transform.position, "Nace en el centro del Púlsar.");
            Assert.AreEqual(0f, Vector2.Distance(new Vector2(1f, 4.5f), stroke.Tip.transform.position), 1e-4f,
                "El contacto de la punta, en el centro del objetivo.");
            Assert.AreEqual(90f, stroke.Root.eulerAngles.z, 1e-3f);
            Assert.AreEqual(BoardOrder.Strokes, stroke.Birth.sortingOrder, "14.7, capa 0: debajo de los nodos.");
            Assert.AreEqual(BoardOrder.Strokes, stroke.Tip.sortingOrder);
            Assert.AreEqual(BoardOrder.Strokes, stroke.Tiles[0].sortingOrder);
            Assert.Less(stroke.Tip.sortingOrder, BoardOrder.Body);
        }

        [Test]
        public void TheBeamRotatesFreely()
        {
            PowerBeamVisual view = View();
            Launch(PowerType.AbsoluteZero, new Vector2(2f, 0f), new Vector2(-1f, -3f));

            view.Refresh();

            Assert.AreEqual(225f, view.Strokes[0].Root.eulerAngles.z, 1e-3f);
            Assert.AreEqual(0f, Vector2.Distance(new Vector2(-1f, -3f), view.Strokes[0].Tip.transform.position), 0.01f);
        }

        [Test]
        public void TheTileIsAnchoredAtTheTipAndCroppedAtTheBirth()
        {
            PowerBeamVisual view = View();
            Launch(PowerType.Wormhole, Vector2.zero, new Vector2(1.5f, 0f)); // 96 px

            view.Refresh();
            PowerBeamVisual.Stroke stroke = view.Strokes[0];

            Assert.AreEqual(1, EnabledTiles(stroke));
            SpriteRenderer tile = stroke.Tiles[0];
            Assert.AreEqual(96 - 24 - 16, tile.sprite.rect.width, "Del borde del nacimiento al de la punta.");
            Assert.AreEqual(256f, tile.sprite.rect.xMax, "Se quedan las columnas del lado de la punta.");
            Assert.AreEqual((96 - 24) / 64f, tile.transform.localPosition.x, 1e-5f, "El tramo toca la punta.");
        }

        [Test]
        public void ALongBeamRepeatsTheTileInsteadOfStretchingIt()
        {
            PowerBeamVisual view = View();
            // La diagonal del tablero, 15 × 8.4375 u: 17.2 u, unos 1101 px.
            Vector2 to = new Vector2(15f, 8.4375f);
            Launch(PowerType.Decay, Vector2.zero, to);

            view.Refresh();
            PowerBeamVisual.Stroke stroke = view.Strokes[0];
            int length = Mathf.RoundToInt(to.magnitude * 64f);
            int tileSpan = length - 24 - 16;

            Assert.AreEqual(Mathf.CeilToInt(tileSpan / 256f), EnabledTiles(stroke));
            int covered = 0;
            for (int i = 0; i < stroke.Tiles.Count; i++)
            {
                if (!stroke.Tiles[i].enabled)
                    continue;
                Assert.AreEqual(Vector3.one, stroke.Tiles[i].transform.localScale, "T.7 #30: el tramo no se estira.");
                Assert.AreEqual((length - 24 - 256 * i) / 64f, stroke.Tiles[i].transform.localPosition.x, 1e-4f,
                    "Las teselas van pegadas, de la punta hacia el Púlsar.");
                covered += (int)stroke.Tiles[i].sprite.rect.width;
            }
            Assert.AreEqual(tileSpan, covered, "Sin huecos ni solapes entre nacimiento y punta.");
        }

        [Test]
        public void ABeamShorterThanItsEndsHasNoTile()
        {
            PowerBeamVisual view = View();
            Launch(PowerType.AbsoluteZero, Vector2.zero, new Vector2(0.5f, 0f)); // 32 px < 16 + 24

            view.Refresh();

            Assert.AreEqual(1, view.VisibleCount);
            Assert.AreEqual(0, EnabledTiles(view.Strokes[0]));
        }

        [Test]
        public void SeveralBeamsAtOnceEachHaveTheirStroke()
        {
            PowerBeamVisual view = View();
            Launch(PowerType.AbsoluteZero, Vector2.zero, new Vector2(4f, 0f));
            Run(3);
            Launch(PowerType.Decay, new Vector2(0f, 2f), new Vector2(-4f, 2f));

            view.Refresh();
            Assert.AreEqual(2, view.VisibleCount);
            Assert.AreEqual(0f, view.Strokes[0].Root.eulerAngles.z, 1e-3f);
            Assert.AreEqual(180f, view.Strokes[1].Root.eulerAngles.z, 1e-3f);

            Run(TicksToLand - 3); // el primero termina, el segundo sigue
            view.Refresh();
            Assert.AreEqual(1, view.VisibleCount);
            Assert.AreEqual("beam_Decay_birth_2", view.Strokes[0].Birth.sprite.name);
        }

        // --- Vista provisional ---

        [Test]
        public void TheProvisionalViewStepsAsideWithArt()
        {
            var provisional = beams.gameObject.AddComponent<ProvisionalBeamView>();
            provisional.Configure(beams);
            PowerBeamVisual view = View();
            Launch(PowerType.AbsoluteZero, Vector2.zero, new Vector2(4f, 0f));

            provisional.Refresh();
            view.Refresh();

            Assert.AreEqual(0, provisional.VisibleCount, "El haz es de PowerBeamVisual.");
            Assert.AreEqual(1, view.VisibleCount);
        }

        [Test]
        public void WithoutArtTheProvisionalViewStaysInCharge()
        {
            var provisional = beams.gameObject.AddComponent<ProvisionalBeamView>();
            provisional.Configure(beams);
            var empty = ScriptableObject.CreateInstance<PowerBeamArt>();
            spawned.Add(empty);
            PowerBeamVisual view = View(empty);
            Launch(PowerType.AbsoluteZero, Vector2.zero, new Vector2(4f, 0f));

            provisional.Refresh();
            view.Refresh();

            Assert.AreEqual(1, provisional.VisibleCount);
            Assert.AreEqual(0, view.VisibleCount);
        }

        static int EnabledTiles(PowerBeamVisual.Stroke stroke)
        {
            int count = 0;
            foreach (SpriteRenderer tile in stroke.Tiles)
            {
                if (tile.enabled)
                    count++;
            }
            return count;
        }
    }
}
