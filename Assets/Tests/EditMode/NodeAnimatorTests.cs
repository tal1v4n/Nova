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
    // T6-02: NodeAnimator, los estados de 14.1 y el idle de los cuatro nodos con la coreografía de A.2.5.
    //
    // La lógica se prueba con sprites sintéticos, nombrados por lo que son, sobre copias en memoria de los
    // NodeDefinition: no depende de que el arte esté sincronizado. Lo que sí depende —las 16 hojas de idle
    // y NodeStateArt asignados— va al final, como en NodeVisualTests.
    public class NodeAnimatorTests
    {
        const string BalancePath = "Assets/Data/BalanceConfig.asset";
        const string StateArtPath = "Assets/Data/NodeStateArt.asset"; // NodeArtAssets.StateArtPath

        // Tipo, asset y prefijo de A.1.
        static readonly (NodeType type, string asset, string prefix)[] Types =
        {
            (NodeType.Asteroid, "Asteroid", "ast"), (NodeType.Star, "Star", "str"),
            (NodeType.Pulsar, "Pulsar", "pul"), (NodeType.Magnetar, "Magnetar", "mag"),
        };
        const float Frame = 1f / NodeAnimator.FramesPerSecond;

        readonly List<Object> spawned = new List<Object>();

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

        static NodeDefinition Asset(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);

        Sprite Sprite(string name)
        {
            var texture = new Texture2D(96, 96);
            spawned.Add(texture);
            Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 96, 96), new Vector2(0.5f, 0.5f), 64);
            sprite.name = name;
            spawned.Add(sprite);
            return sprite;
        }

        Sprite[] Sheet(string name, int frames)
        {
            var sheet = new Sprite[frames];
            for (int i = 0; i < frames; i++)
                sheet[i] = Sprite($"{name}_{i}");
            return sheet;
        }

        // Copia en memoria del NodeDefinition real con arte sintético: <prefijo>_lvl<n>_<facción>, y hoja de
        // 16 frames solo si el tipo la tiene.
        NodeDefinition Definition(string asset, string prefix, bool idleSheet)
        {
            NodeDefinition definition = Object.Instantiate(Asset(asset));
            spawned.Add(definition);
            var art = new NodeLevelArt[definition.ArtLevels];
            for (int i = 0; i < art.Length; i++)
            {
                int level = definition.MaxLevel == 0 ? 0 : i + 1;
                Sprite[] Idle(string faction) => idleSheet ? Sheet($"{prefix}_lvl{level}_{faction}_sheet", 16) : null;
                art[i] = new NodeLevelArt(Sprite($"{prefix}_lvl{level}_neutral"), Sprite($"{prefix}_lvl{level}_player"),
                    Sprite($"{prefix}_lvl{level}_enemy"), 60, Idle("neutral"), Idle("player"), Idle("enemy"));
            }
            definition.ConfigureArt(art);
            return definition;
        }

        NodeDefinition Star => Definition("Star", "str", true);
        NodeDefinition Pulsar => Definition("Pulsar", "pul", true);
        NodeDefinition Asteroid => Definition("Asteroid", "ast", false);
        NodeDefinition Magnetar => Definition("Magnetar", "mag", false);

        NodeStateArt States()
        {
            var states = ScriptableObject.CreateInstance<NodeStateArt>();
            spawned.Add(states);
            states.Configure(Sheet("sleep", 16), Sprite("unlit_player"), Sprite("unlit_enemy"),
                Sheet("ready_player", 16), Sheet("ready_enemy", 16), Sprite("frozen"), Sheet("decay", 16),
                Sheet("convert_player", 36), Sheet("convert_enemy", 36), Sheet("captured", 5),
                Sprite("buff_ray"), Sprite("buff_hydrogen"));
            return states;
        }

        NodeController Node(NodeDefinition definition, Faction faction, int energy, int level, Vector2 position = default)
        {
            var go = new GameObject(definition.Type.ToString());
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(definition, faction, energy, level);
            return node;
        }

        NodeAnimator Animate(NodeController node, NodeStateArt states)
        {
            var visual = node.gameObject.AddComponent<NodeVisual>();
            visual.Subscribe();
            visual.Refresh();
            var animator = node.gameObject.AddComponent<NodeAnimator>();
            animator.Configure(Balance, states);
            animator.Subscribe();
            animator.Advance(0f);
            return animator;
        }

        static SpriteRenderer Body(NodeAnimator animator) => animator.GetComponent<NodeVisual>().Body;

        static int BodyPixelsY(NodeAnimator animator) => Mathf.RoundToInt(Body(animator).transform.localPosition.y * 64f);

        // --- La coreografía de A.2.5 ---

        // Valores de desfase() en tools/preview_idle.py: si cambia una, cambia la otra.
        [TestCase(78, 80, 1065925834u)]
        [TestCase(-352, 0, 3300432055u)]
        [TestCase(-128, -64, 2126431979u)]
        [TestCase(0, 0, 0u)]
        public void ThePhaseIsTheReferenceHashOfThePosition(int px, int py, uint expected)
        {
            Assert.AreEqual(expected, NodeAnimator.IdlePhase(new Vector3(px / 64f, py / 64f, 0f)));
        }

        // Los niveles colocan los nodos en múltiplos de 0.25 u: sin mezclar el hash, todos salían en fase 0.
        [Test]
        public void NodesOnTheQuarterUnitGridDoNotAllStartTogether()
        {
            var phases = new HashSet<uint>();
            for (int x = -6; x <= 6; x++)
                phases.Add(NodeAnimator.IdlePhase(new Vector3(x * 0.25f, 0.5f, 0f)) % NodeAnimator.IdleFrames);
            Assert.Greater(phases.Count, 6, "Trece nodos en fila, casi todos en una fase distinta.");
        }

        [Test]
        public void OnlyTheAsteroidAndTheMagnetarBobAndOnlyByWholePixels()
        {
            var asteroid = new HashSet<int>();
            var magnetar = new HashSet<int>();
            for (int frame = 0; frame < NodeAnimator.BobCycleFrames; frame++)
            {
                asteroid.Add(NodeAnimator.BobPixels(NodeType.Asteroid, frame));
                magnetar.Add(NodeAnimator.BobPixels(NodeType.Magnetar, frame));
                Assert.AreEqual(0, NodeAnimator.BobPixels(NodeType.Star, frame), "La Estrella lleva hoja.");
                Assert.AreEqual(0, NodeAnimator.BobPixels(NodeType.Pulsar, frame), "El Púlsar lleva hoja.");
            }
            CollectionAssert.AreEquivalent(new[] { -1, 0, 1 }, asteroid, "1 px (A.2.5).");
            CollectionAssert.AreEquivalent(new[] { -2, -1, 0, 1, 2 }, magnetar, "2 px (A.2.5).");
            Assert.AreEqual(0, NodeAnimator.BobPixels(NodeType.Magnetar, 0), "El ciclo arranca en reposo.");
        }

        // --- Idle ---

        [Test]
        public void AStarPlaysItsSheetAtTwelveFramesPerSecondFromItsPhase()
        {
            var position = new Vector2(78 / 64f, 80 / 64f);
            NodeAnimator animator = Animate(Node(Star, Faction.Player, 20, 2, position), States());
            int start = (int)(NodeAnimator.IdlePhase(position) % 16);

            Assert.AreEqual($"str_lvl2_player_sheet_{start}", Body(animator).sprite.name);
            animator.Advance(Frame);
            Assert.AreEqual($"str_lvl2_player_sheet_{(start + 1) % 16}", Body(animator).sprite.name);
            animator.Advance(Frame * 15);
            Assert.AreEqual($"str_lvl2_player_sheet_{start}", Body(animator).sprite.name, "16 frames y vuelve.");
        }

        [Test]
        public void BetweenTwoTicksTheFrameDoesNotChange()
        {
            NodeAnimator animator = Animate(Node(Pulsar, Faction.Enemy, 20, 3), States());
            string first = Body(animator).sprite.name;
            animator.Advance(Frame * 0.9f);
            Assert.AreEqual(first, Body(animator).sprite.name, "12 fps, no 60.");
        }

        [Test]
        public void TwoStarsInDifferentPlacesDoNotBeatTogether()
        {
            NodeAnimator a = Animate(Node(Star, Faction.Player, 20, 1, new Vector2(-2f, 1f)), States());
            NodeAnimator b = Animate(Node(Star, Faction.Player, 20, 1, new Vector2(2.5f, -1f)), States());
            Assert.AreNotEqual(Body(a).sprite.name.Substring(Body(a).sprite.name.LastIndexOf('_')),
                Body(b).sprite.name.Substring(Body(b).sprite.name.LastIndexOf('_')));
        }

        [Test]
        public void AMagnetarBobsTheSpriteButNotTheNode()
        {
            NodeAnimator animator = Animate(Node(Magnetar, Faction.Neutral, 100, 3, new Vector2(1f, 1f)), States());
            var seen = new HashSet<int>();
            for (int i = 0; i < NodeAnimator.BobCycleFrames; i++)
            {
                seen.Add(BodyPixelsY(animator));
                Assert.AreEqual(Body(animator).transform.localPosition.y * 64f, BodyPixelsY(animator), 1e-4f, "Píxeles enteros.");
                animator.Advance(Frame);
            }
            CollectionAssert.AreEquivalent(new[] { -2, -1, 0, 1, 2 }, seen);
            Assert.AreEqual(new Vector3(1f, 1f, 0f), animator.transform.position, "El collider no cabecea.");
            Assert.AreEqual("mag_lvl3_neutral", Body(animator).sprite.name, "Sin hoja: el sprite base.");
        }

        [Test]
        public void FrozenHoldsTheIdleWhereItWas()
        {
            NodeController node = Node(Star, Faction.Player, 20, 4);
            NodeAnimator animator = Animate(node, States());
            animator.Advance(Frame * 5);
            string held = Body(animator).sprite.name;

            node.SetStateFlag(NodeStateFlags.Frozen, true);
            animator.Advance(2f);
            Assert.AreEqual(held, Body(animator).sprite.name, "«Idle detenido» (14.1).");
            Assert.IsTrue(animator.FrozenTint.enabled);
            Assert.AreEqual("frozen", animator.FrozenTint.sprite.name);

            node.SetStateFlag(NodeStateFlags.Frozen, false);
            animator.Advance(Frame);
            Assert.AreNotEqual(held, Body(animator).sprite.name, "Al deshelar sigue desde donde se quedó.");
            Assert.IsFalse(animator.FrozenTint.enabled);
        }

        [Test]
        public void TheBoardStopsInThePause()
        {
            NodeAnimator animator = Animate(Node(Star, Faction.Enemy, 20, 3), States());
            string before = Body(animator).sprite.name;
            animator.Advance(0f); // Time.deltaTime con Time.timeScale = 0
            Assert.AreEqual(before, Body(animator).sprite.name);
        }

        // --- El susto (T6-03, 14.2): capa 5 de 14.7, sin asset ---

        static int BodyPixelsX(NodeAnimator animator) => Mathf.RoundToInt(Body(animator).transform.localPosition.x * 64f);

        [Test]
        public void TheScareJumpsFromItsPeakAndShakesOneWholePixelEachWay()
        {
            Assert.AreEqual(new Vector2Int(1, 3), NodeAnimator.ScarePixels(0), "Arranca en el pico del salto.");
            Assert.AreEqual(new Vector2Int(-1, 2), NodeAnimator.ScarePixels(1));
            Assert.AreEqual(new Vector2Int(1, 1), NodeAnimator.ScarePixels(2));
            for (int frame = NodeAnimator.ScareHopFrames; frame < 40; frame++)
            {
                Vector2Int offset = NodeAnimator.ScarePixels(frame);
                Assert.AreEqual(0, offset.y, "Tras el salto, solo el temblor.");
                Assert.AreEqual((frame & 1) == 0 ? 1 : -1, offset.x);
            }
            Assert.Throws<System.ArgumentOutOfRangeException>(() => NodeAnimator.ScarePixels(-1));
        }

        [Test]
        public void AScaredStarJumpsShakesAndSettlesWithoutMovingTheNode()
        {
            NodeController node = Node(Star, Faction.Player, 10, 2, new Vector2(1f, 1f));
            NodeAnimator animator = Animate(node, States());
            Assert.AreEqual(Vector3.zero, Body(animator).transform.localPosition, "La Estrella no cabecea.");

            node.SetStateFlag(NodeStateFlags.Scared, true);
            for (int frame = 0; frame < 8; frame++)
            {
                animator.Advance(frame == 0 ? 0f : Frame);
                Vector2Int expected = NodeAnimator.ScarePixels(frame);
                Assert.AreEqual(expected.x, BodyPixelsX(animator), $"Temblor, frame {frame}.");
                Assert.AreEqual(expected.y, BodyPixelsY(animator), $"Salto, frame {frame}.");
                Assert.AreEqual(Body(animator).transform.localPosition.x * 64f, BodyPixelsX(animator), 1e-4f, "Píxeles enteros.");
            }
            Assert.AreEqual(new Vector3(1f, 1f, 0f), node.transform.position, "El collider no tiembla.");

            node.SetStateFlag(NodeStateFlags.Scared, false);
            animator.Advance(Frame);
            Assert.IsFalse(animator.IsScarePlaying);
            Assert.AreEqual(Vector3.zero, Body(animator).transform.localPosition);
        }

        [Test]
        public void TheScareAddsToTheBob()
        {
            NodeController node = Node(Magnetar, Faction.Neutral, 100, 3, new Vector2(1f, 1f));
            NodeAnimator animator = Animate(node, States());
            animator.Advance(Frame * 3);
            int bob = BodyPixelsY(animator);

            node.SetStateFlag(NodeStateFlags.Scared, true);
            animator.Advance(0f);
            Assert.AreEqual(bob + NodeAnimator.ScarePixels(0).y, BodyPixelsY(animator));
            Assert.AreEqual(NodeAnimator.ScarePixels(0).x, BodyPixelsX(animator));
        }

        [Test]
        public void AFrozenNodeStillShakesWhenScared()
        {
            NodeController node = Node(Star, Faction.Enemy, 10, 2);
            node.SetStateFlag(NodeStateFlags.Frozen, true);
            NodeAnimator animator = Animate(node, States());

            node.SetStateFlag(NodeStateFlags.Scared, true);
            var seen = new HashSet<int>();
            for (int frame = 0; frame < 6; frame++)
            {
                animator.Advance(frame == 0 ? 0f : Frame);
                seen.Add(BodyPixelsX(animator));
            }
            CollectionAssert.AreEquivalent(new[] { -1, 1 }, seen, "14.6: Congelado y Asustado conviven y el susto se ve.");
        }

        [Test]
        public void TheScareStopsInThePauseAndANewOneJumpsAgain()
        {
            NodeController node = Node(Pulsar, Faction.Player, 10, 1);
            NodeAnimator animator = Animate(node, States());
            node.SetStateFlag(NodeStateFlags.Scared, true);
            animator.Advance(0f);
            animator.Advance(Frame * 5);
            Vector3 held = Body(animator).transform.localPosition;
            animator.Advance(0f); // Time.deltaTime con Time.timeScale = 0
            Assert.AreEqual(held, Body(animator).transform.localPosition);

            node.SetStateFlag(NodeStateFlags.Scared, false);
            animator.Advance(Frame);
            node.SetStateFlag(NodeStateFlags.Scared, true);
            animator.Advance(Frame);
            Assert.AreEqual(NodeAnimator.ScarePixels(0).y, BodyPixelsY(animator), "Un susto nuevo vuelve a saltar.");
        }

        [Test]
        public void WithTheAnimatorTheEnergyNumberDoesNotShake()
        {
            NodeController node = Node(Star, Faction.Player, 10, 2);
            NodeAnimator animator = Animate(node, States());
            var view = node.gameObject.AddComponent<ProvisionalNodeView>();
            view.Subscribe();
            view.Refresh();
            Vector3 label = view.Label.transform.localPosition;

            node.SetStateFlag(NodeStateFlags.Scared, true);
            animator.Advance(0f);

            Assert.AreEqual(label, view.Label.transform.localPosition, "El número no se mueve (A.2.5).");
            Assert.AreNotEqual(Vector3.zero, Body(animator).transform.localPosition, "El sprite sí.");
            view.Unsubscribe();
        }

        // --- Estados ---

        [Test]
        public void ASleepingAsteroidShowsItsZzzAndAnOwnedOneItsEmber()
        {
            NodeController node = Node(Asteroid, Faction.Neutral, 5, 0);
            NodeAnimator animator = Animate(node, States());
            StringAssert.StartsWith("sleep_", animator.Glyph.sprite.name);

            node.Steal(Faction.Player);
            animator.Advance(0f);
            Assert.AreEqual("unlit_player", animator.Glyph.sprite.name, "Con menos de 10, la brasa quieta.");

            node.AddEnergy(Balance.IgnitionCost - 5);
            animator.Advance(0f);
            StringAssert.StartsWith("ready_player_", animator.Glyph.sprite.name, "Con 10, la brasa abierta.");
        }

        [Test]
        public void AnInfectedNodeShowsTheCloud()
        {
            NodeController node = Node(Pulsar, Faction.Player, 20, 1);
            NodeAnimator animator = Animate(node, States());
            Assert.IsFalse(animator.InfectedAura.enabled);

            node.SetStateFlag(NodeStateFlags.Infected, true);
            animator.Advance(0f);
            StringAssert.StartsWith("decay_", animator.InfectedAura.sprite.name);
        }

        [Test]
        public void ACapturePlaysItsFiveFramesOverTheNewColourAndEnds()
        {
            NodeController node = Node(Magnetar, Faction.Neutral, 10, 2);
            NodeAnimator animator = Animate(node, States());

            node.Steal(Faction.Enemy);
            Assert.AreEqual("captured_0", animator.Effect.sprite.name, "Arranca en su pico (A.3.7).");
            Assert.AreEqual("mag_lvl2_enemy", Body(animator).sprite.name, "Con la paleta nueva desde el frame 0.");

            for (int i = 1; i < 5; i++)
            {
                animator.Advance(Frame);
                Assert.AreEqual($"captured_{i}", animator.Effect.sprite.name);
            }
            animator.Advance(Frame);
            Assert.IsFalse(animator.Effect.enabled, "0.4 s y se acaba.");
            Assert.IsFalse(animator.IsCapturePlaying);
        }

        [Test]
        public void AConversionFollowsTheConverterClockAndSwapsTheSpriteAtTheCut()
        {
            NodeDefinition star = Star;
            NodeDefinition pulsar = Pulsar;
            NodeController node = Node(star, Faction.Player, 40, 2); // Tope 50; reconvertir cuesta 20
            var converter = node.gameObject.AddComponent<NodeConverter>();
            converter.Configure(Balance, new[] { pulsar });
            NodeAnimator animator = Animate(node, States());

            Assert.IsTrue(converter.TryConvert(Faction.Player, NodeType.Pulsar).IsAllowed);
            animator.Advance(0f);
            Assert.AreEqual("convert_player_0", animator.ConvertTint.sprite.name);
            Assert.AreEqual("str_lvl2_player", Body(animator).sprite.name, "Sin idle mientras se reconvierte.");

            converter.Tick(15 * Frame + 0.001f);
            animator.Advance(0f);
            Assert.AreEqual("convert_player_15", animator.ConvertTint.sprite.name);
            Assert.AreEqual("str_lvl2_player", Body(animator).sprite.name);

            converter.Tick(Frame);
            animator.Advance(0f);
            Assert.AreEqual("convert_player_16", animator.ConvertTint.sprite.name);
            Assert.AreEqual("pul_lvl1_player", Body(animator).sprite.name, "El corte, tapado (A.3.6).");
            Assert.AreEqual(NodeType.Star, node.Type, "La lógica sigue siendo Estrella (18 #20).");

            node.SetStateFlag(NodeStateFlags.Frozen, true);
            animator.Advance(1f);
            Assert.AreEqual("convert_player_16", animator.ConvertTint.sprite.name, "Congelado pausa el reloj (8.1).");
        }

        [Test]
        public void TheBuffMarkGoesOnlyOnTheTypesItHelps()
        {
            var buffsObject = new GameObject("Buffs");
            spawned.Add(buffsObject);
            var buffs = buffsObject.AddComponent<FactionBuffs>();
            NodeController star = Node(Star, Faction.Player, 20, 1, new Vector2(-2f, 0f));
            NodeController magnetar = Node(Magnetar, Faction.Player, 20, 1, new Vector2(2f, 0f));
            NodeController rock = Node(Asteroid, Faction.Player, 5, 0, new Vector2(0f, 2f));
            foreach (NodeController node in new[] { star, magnetar, rock })
                node.UseBuffs(buffs);
            NodeAnimator starAnimator = Animate(star, States());
            NodeAnimator magnetarAnimator = Animate(magnetar, States());
            NodeAnimator rockAnimator = Animate(rock, States());

            buffs.Apply(Faction.Player, AssetDatabase.LoadAssetAtPath<PowerUpDefinition>("Assets/Data/PowerUps/CosmicRay.asset"));
            foreach (NodeAnimator animator in new[] { starAnimator, magnetarAnimator, rockAnimator })
                animator.Advance(0f);
            Assert.IsFalse(starAnimator.Buff.enabled, "El Rayo no acelera a la Estrella.");
            Assert.AreEqual("buff_ray", magnetarAnimator.Buff.sprite.name);
            Assert.IsFalse(rockAnimator.Buff.enabled, "El Asteroide no gana nada (A.3.8).");

            buffs.Apply(Faction.Player, AssetDatabase.LoadAssetAtPath<PowerUpDefinition>("Assets/Data/PowerUps/HydrogenCloud.asset"));
            starAnimator.Advance(0f);
            Assert.AreEqual("buff_hydrogen", starAnimator.Buff.sprite.name);
        }

        // 14.7: tintes, aura y glifos entre el sprite y el número; Capturado por encima de todo eso.
        [Test]
        public void TheLayersAreThoseOfFourteenSeven()
        {
            NodeAnimator animator = Animate(Node(Star, Faction.Player, 20, 1), States());
            int[] order =
            {
                Body(animator).sortingOrder, animator.ConvertTint.sortingOrder, animator.FrozenTint.sortingOrder,
                animator.InfectedAura.sortingOrder, animator.Glyph.sortingOrder, BoardOrder.Energy,
                animator.Effect.sortingOrder,
            };
            for (int i = 1; i < order.Length; i++)
                Assert.Less(order[i - 1], order[i], $"Capa {i}");
            Assert.AreEqual(animator.Glyph.sortingOrder, animator.Buff.sortingOrder, "El buff es un glifo (14.7, capa 4).");
        }

        [Test]
        public void TheOverlaysHangFromTheSpriteSoTheyMoveWithIt()
        {
            NodeAnimator animator = Animate(Node(Star, Faction.Player, 20, 1), States());
            foreach (SpriteRenderer layer in new[] { animator.ConvertTint, animator.FrozenTint, animator.InfectedAura,
                         animator.Glyph, animator.Buff, animator.Effect })
                Assert.AreSame(Body(animator).transform, layer.transform.parent, layer.name);
        }

        [Test]
        public void WithoutStateArtItOnlyAnimatesTheIdle()
        {
            NodeController node = Node(Star, Faction.Player, 20, 1);
            NodeAnimator animator = Animate(node, null);
            node.SetStateFlag(NodeStateFlags.Frozen, true);
            animator.Advance(0f);
            Assert.IsFalse(animator.DrawsStates);
            Assert.IsFalse(animator.FrozenTint.enabled);
            StringAssert.StartsWith("str_lvl1_player_sheet_", Body(animator).sprite.name);
        }

        // 14.0 #1: la presentación solo lee.
        [Test]
        public void ItNeverWritesToTheNode()
        {
            NodeController node = Node(Pulsar, Faction.Player, 33, 3);
            node.SetStateFlag(NodeStateFlags.Infected, true);
            NodeSnapshot before = node.Snapshot();
            NodeAnimator animator = Animate(node, States());

            animator.Advance(3f);

            Assert.AreEqual(before, node.Snapshot());
        }

        // --- El arte real, una vez sincronizado y asignado (Nova → Arte) ---

        // 16 hojas de idle (A.2.2, A.2.3): las tienen la Estrella y el Púlsar, en sus dos facciones y todos
        // sus niveles; el Asteroide y el Magnetar, ninguna.
        [Test]
        public void TheStarAndThePulsarHaveTheirSixteenIdleSheets()
        {
            int sheets = 0;
            foreach (var entry in Types)
            {
                NodeDefinition definition = Asset(entry.asset);
                bool expected = entry.type == NodeType.Star || entry.type == NodeType.Pulsar;
                for (int i = 0; i < definition.ArtLevels; i++)
                {
                    int level = definition.MaxLevel == 0 ? 0 : i + 1;
                    foreach (Faction faction in new[] { Faction.Neutral, Faction.Player, Faction.Enemy })
                    {
                        Sprite[] idle = definition.GetArt(level).IdleFor(faction);
                        if (!expected || definition.GetArt(level).SpriteFor(faction) == null)
                        {
                            Assert.IsEmpty(idle, $"{entry.asset} N{level} {faction}: idle por transform.");
                            continue;
                        }
                        Assert.AreEqual(NodeAnimator.IdleFrames, idle.Length, $"{entry.asset} N{level} {faction}");
                        string sheet = $"{entry.prefix}_lvl{level}_{faction.ToString().ToLowerInvariant()}_sheet";
                        for (int f = 0; f < idle.Length; f++)
                            Assert.AreEqual($"{sheet}_{f}", idle[f].name, "En orden.");
                        Assert.AreEqual(definition.GetArt(level).SpriteFor(faction).rect.size, idle[0].rect.size, "Celda de 96.");
                        sheets++;
                    }
                }
            }
            Assert.AreEqual(16, sheets);
        }

        [Test]
        public void TheStateArtIsAssigned()
        {
            var states = AssetDatabase.LoadAssetAtPath<NodeStateArt>(StateArtPath);
            Assert.IsNotNull(states, "Nova → Arte → Asignar sprites de nodo.");
            Assert.IsTrue(states.IsComplete);
            Assert.AreEqual(16, states.Sleep.Length, "ART-ST-SLEEP");
            Assert.AreEqual(16, states.ReadyFor(Faction.Player).Length, "ART-ST-READY");
            Assert.AreEqual(16, states.Decay.Length, "ART-ST-DECAY");
            Assert.AreEqual(36, states.ConvertFor(Faction.Enemy).Length, "ART-ST-CONV");
            Assert.AreEqual(5, states.Captured.Length, "ART-ST-CAP");
        }
    }
}
