using System.Collections.Generic;
using System.Linq;
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
    // T6-07: los efectos puntuales de A.5 (VFXPool, VfxArt) sobre los eventos de 14.5. El arte es de prueba, con
    // las filas, los frames y las variantes de los .json de verdad, y cada celda se llama
    // <hoja>_<fila>_<variante>_<frame>: lo que se comprueba es qué celda sale, dónde y cuándo. Las huellas de los
    // nodos son las de A.2, así que la talla de «La órbita» es la de partida (Estrella N1 → 24, Asteroide → 28…).
    public class VfxTests
    {
        const float Frame = 1f / 12f;
        const float Tick = 0.02f;
        static readonly int[] Radii = { 24, 28, 32, 36, 40 };
        static readonly string[] Factions = { "player", "enemy" };

        // Huella sólida por nivel, la de los <prefijo>_huella.json de A.2.
        static readonly Dictionary<string, int[]> Footprints = new Dictionary<string, int[]>
        {
            ["Asteroid"] = new[] { 43 },
            ["Star"] = new[] { 39, 51, 61, 68, 72 },
            ["Pulsar"] = new[] { 44, 60, 68 },
            ["Magnetar"] = new[] { 41, 60, 60, 67, 67 },
        };

        readonly List<Object> spawned = new List<Object>();
        readonly List<NodeAnimator> animators = new List<NodeAnimator>();
        readonly Dictionary<string, NodeDefinition> definitions = new Dictionary<string, NodeDefinition>();
        GameObject systems;
        NodeRegistry registry;
        ProtonRegistry protons;
        PowerBeamSystem beams;
        VfxArt art;
        NodeStateArt states;
        VFXPool pool;

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        static PowerDefinition PowerAsset(PowerType type) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{type}.asset");

        [SetUp]
        public void SetUp()
        {
            systems = new GameObject("Systems");
            spawned.Add(systems);
            registry = systems.AddComponent<NodeRegistry>();
            protons = systems.AddComponent<ProtonRegistry>();
            beams = systems.AddComponent<PowerBeamSystem>();
            art = TestArt();
            states = States();
            pool = systems.AddComponent<VFXPool>();
            pool.Configure(art, Input(), ProtonSizes(), null, registry, protons, beams);
            pool.Subscribe();
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
            animators.Clear();
            definitions.Clear();
        }

        // --- Arte de prueba ---

        Texture2D texture;

        Sprite Cell(string name)
        {
            if (texture == null)
            {
                texture = new Texture2D(8, 8);
                spawned.Add(texture);
            }
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 64);
            sprite.name = name;
            spawned.Add(sprite);
            return sprite;
        }

        VfxArt.Sheet Sheet(string name, IEnumerable<string> rowNames, int frames, int variants = 1)
        {
            string[] rows = rowNames.ToArray();
            var cells = new Sprite[rows.Length * variants * frames];
            for (int r = 0; r < rows.Length; r++)
                for (int v = 0; v < variants; v++)
                    for (int f = 0; f < frames; f++)
                        cells[(r * variants + v) * frames + f] = Cell($"{CellPrefix(name, rows[r])}_{v}_{f}");
            return new VfxArt.Sheet(rows, frames, variants, cells);
        }

        // <hoja>_<fila>, sin repetir la hoja cuando la fila ya la lleva: «pwr_zer_24», no «pwr_zer_pwr_zer_24».
        static string CellPrefix(string sheet, string row) => row.StartsWith(sheet + "_") ? row : $"{sheet}_{row}";

        static IEnumerable<string> PerFactionAndRadius() =>
            Factions.SelectMany(f => Radii.Select(r => $"{f}_{r}"));

        static IEnumerable<string> PerRadius(string prefix) => Radii.Select(r => $"{prefix}_{r}");

        static IEnumerable<string> PerFactionAndProtonSize() =>
            Factions.SelectMany(f => Enumerable.Range(1, 10).Select(s => $"{f}_{s}"));

        // Las filas de los .json de A.5.
        VfxArt TestArt()
        {
            var vfx = ScriptableObject.CreateInstance<VfxArt>();
            spawned.Add(vfx);
            string[] upgrades = { "str2", "str3", "str4", "str5", "pul2", "pul3", "mag2", "mag3", "mag4", "mag5" };
            vfx.Configure(
                Sheet("shot", PerFactionAndProtonSize(), 2, 4),
                Sheet("kill", Factions, 3, 16),
                Sheet("vanish", PerFactionAndProtonSize(), 5, 16),
                Sheet("send", PerFactionAndRadius(), 3),
                Sheet("abs", PerFactionAndRadius(), 4),
                Sheet("dmg", PerFactionAndRadius(), 4, 4),
                Sheet("ign", new[] { "player_24", "player_28", "enemy_24", "enemy_28" }, 8),
                Sheet("upg", Factions.SelectMany(f => upgrades.Select(u => $"{f}_{u}")), 6),
                Sheet("thaw", PerRadius("thaw"), 6),
                Sheet("cure", PerRadius("cure"), 5, 16),
                Sheet("cast", new[] { "zero", "decay", "wormhole" }.SelectMany(p => new[] { 1, 2, 3 }.Select(l => $"{p}_{l}")), 5),
                Sheet("pwr_zer", PerRadius("pwr_zer"), 9),
                Sheet("pwr_dec", PerRadius("pwr_dec"), 9),
                Sheet("pwr_wrm", PerRadius("pwr_wrm"), 12),
                Sheet("cascade", PerRadius("cascade"), 12));
            return vfx;
        }

        // De InputArt, el pool solo lee las tallas: cinco radios y 4 px de aire (world_select_tallas.json).
        InputArt Input()
        {
            var input = ScriptableObject.CreateInstance<InputArt>();
            spawned.Add(input);
            var none = new InputArt.Frames(new Sprite[0]);
            input.Configure(none, none, none, 4, (int[])Radii.Clone(), new InputArt.Frames[0], null, none,
                new InputArt.Frames[0], new InputArt.Frames[0], new InputArt.Frames[0]);
            return input;
        }

        // Y de ProtonArt, las diez tallas de 5 de Carga.
        ProtonArt ProtonSizes()
        {
            var proton = ScriptableObject.CreateInstance<ProtonArt>();
            spawned.Add(proton);
            proton.Configure(new ProtonArt.SizeFrames[10], new ProtonArt.SizeFrames[10], new Sprite[10], new int[10], 5,
                new int[10]);
            return proton;
        }

        Sprite[] Frames(string name, int count) => Enumerable.Range(0, count).Select(i => Cell($"{name}_{i}")).ToArray();

        NodeStateArt States()
        {
            var stateArt = ScriptableObject.CreateInstance<NodeStateArt>();
            spawned.Add(stateArt);
            stateArt.Configure(Frames("sleep", 16), Cell("unlit_player"), Cell("unlit_enemy"),
                Frames("ready_player", 16), Frames("ready_enemy", 16), Cell("frozen"), Frames("decay", 16),
                Frames("convert_player", 36), Frames("convert_enemy", 36), Frames("captured", 5),
                Cell("buff_ray"), Cell("buff_hydrogen"));
            return stateArt;
        }

        // Copia del NodeDefinition real con un sprite por nivel y facción y la huella de A.2.
        NodeDefinition Definition(string asset)
        {
            if (definitions.TryGetValue(asset, out NodeDefinition cached))
                return cached;
            NodeDefinition definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{asset}.asset"));
            spawned.Add(definition);
            var levelArt = new NodeLevelArt[definition.ArtLevels];
            for (int i = 0; i < levelArt.Length; i++)
                levelArt[i] = new NodeLevelArt(Cell($"{asset}_{i}_neutral"), Cell($"{asset}_{i}_player"),
                    Cell($"{asset}_{i}_enemy"), Footprints[asset][i]);
            definition.ConfigureArt(levelArt);
            definitions[asset] = definition;
            return definition;
        }

        // Un nodo de partida con lo que el pool escucha: hielo, Decaimiento, sprite y animador.
        NodeController Node(string asset, Faction faction, int energy, int level, Vector2 position)
        {
            var go = new GameObject(asset);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            go.AddComponent<NodeFreeze>().Configure(Balance);
            go.AddComponent<NodeDecay>().Configure(Balance);
            node.Initialize(Definition(asset), faction, energy, level);
            var visual = go.AddComponent<NodeVisual>();
            visual.Subscribe();
            visual.Refresh();
            var animator = go.AddComponent<NodeAnimator>();
            animator.Configure(Balance, states);
            animator.Subscribe();
            animator.Advance(0f);
            animators.Add(animator);
            registry.Register(node);
            return node;
        }

        // Un frame de presentación: el pool y los animadores, con el mismo tiempo.
        void Step(float seconds)
        {
            pool.Advance(seconds);
            foreach (NodeAnimator animator in animators)
                animator.Advance(seconds);
        }

        string[] Shown => pool.Renderers.Where(r => r.enabled).Select(r => r.sprite.name).ToArray();

        SpriteRenderer Showing(string name)
        {
            SpriteRenderer renderer = pool.Renderers.FirstOrDefault(r => r.sprite != null && r.sprite.name == name);
            Assert.IsNotNull(renderer, $"No suena {name}. Suenan: {string.Join(", ", Shown)}");
            return renderer;
        }

        static Transform BodyOf(NodeController node) => node.GetComponent<NodeVisual>().Body.transform;

        // --- VfxArt ---

        [Test]
        public void TheSheetFindsItsRowsByNameAndItsCellsByVariantAndFrame()
        {
            VfxArt.Sheet damage = art.Damage;
            Assert.AreEqual(4, damage.Frames);
            Assert.AreEqual(4, damage.Variants);
            Assert.AreEqual(7, damage.Row("enemy_32"));
            Assert.AreEqual(-1, damage.Row("neutral_32"), "Un neutral no lanza protones.");
            Assert.AreEqual("dmg_enemy_32_2_3", damage.Frame(7, 2, 3).name);
        }

        [Test]
        public void TheArtIsCompleteOnlyWithTheFifteenSheets()
        {
            Assert.IsTrue(art.IsComplete);
            var partial = ScriptableObject.CreateInstance<VfxArt>();
            spawned.Add(partial);
            partial.Configure(art.Shot, art.Kill, art.Vanish, art.Send, art.Absorb, art.Damage, art.Ignite, art.Upgrade,
                art.Thaw, art.Cure, art.Cast, art.PowerZero, art.PowerDecay, art.PowerWormhole, default);
            Assert.IsFalse(partial.IsComplete);
        }

        [Test]
        public void WithoutArtNothingIsDrawn()
        {
            pool.Configure(null, Input(), ProtonSizes(), null, registry, protons, beams);
            NodeController pulsar = Node("Pulsar", Faction.Player, 40, 1, Vector2.zero);
            NodeController target = Node("Star", Faction.Enemy, 40, 1, new Vector2(3f, 0f));
            beams.Launch(pulsar, target, Faction.Player, PowerAsset(PowerType.AbsoluteZero));
            Step(0f);
            Assert.AreEqual(0, pool.ActiveCount);
        }

        // --- Protones sobre nodos: envío, absorción y daño ---

        Proton Send(NodeController source, NodeController target)
        {
            var protonPool = systems.GetComponent<ProtonPool>();
            if (protonPool == null)
                protonPool = systems.AddComponent<ProtonPool>();
            var launcher = systems.GetComponent<ProtonLauncher>();
            if (launcher == null)
                launcher = systems.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, protonPool, protons);
            Assert.AreEqual(LaunchBlock.None, launcher.TryLaunch(source, target, source.Faction, false, out Proton proton));
            return proton;
        }

        void Land(Proton proton)
        {
            for (int i = 0; i < 1000 && proton.InFlight; i++)
            {
                proton.Tick(Tick);
                protons.ResolveArrivals();
            }
            Assert.IsFalse(proton.InFlight);
        }

        [Test]
        public void TheSendRingIsOnTheSourceWithItsFactionAndSize()
        {
            NodeController source = Node("Star", Faction.Player, 40, 1, Vector2.zero);
            NodeController target = Node("Star", Faction.Player, 10, 2, new Vector2(1f, 0f));
            Send(source, target);
            Step(0f);

            SpriteRenderer send = Showing("send_player_24_0_0");
            Assert.AreSame(BodyOf(source), send.transform.parent, "Cuelga del sprite del origen.");
            Assert.AreEqual(Vector3.zero, send.transform.localPosition);
            Assert.AreEqual(BoardOrder.PointEffect, send.sortingOrder);
        }

        [Test]
        public void AnAlliedHitAbsorbsAndAHostileOneThatDoesNotCaptureDamages()
        {
            // El Tope de la Estrella es 50: envía 25 al aliado y luego 12 al enemigo, que tiene 40.
            NodeController source = Node("Star", Faction.Player, 50, 1, Vector2.zero);
            NodeController ally = Node("Star", Faction.Player, 10, 2, new Vector2(1f, 0f));
            NodeController enemy = Node("Star", Faction.Enemy, 40, 1, new Vector2(-1f, 0f));

            Proton toAlly = Send(source, ally);
            Step(0f);
            Land(toAlly);
            SpriteRenderer absorb = Showing("abs_player_32_0_0");
            Assert.AreSame(BodyOf(ally), absorb.transform.parent);

            Proton toEnemy = Send(source, enemy);
            Step(0f);
            Land(toEnemy);
            Assert.AreEqual(Faction.Enemy, enemy.Faction, "Sin captura.");
            string damage = Shown.Single(name => name.StartsWith("dmg_"));
            StringAssert.StartsWith("dmg_player_24_", damage, "La fila del bando del protón y la talla del nodo golpeado.");
            StringAssert.EndsWith("_0", damage);
        }

        [Test]
        public void AHostileHitThatOnlyThawsAFrozenNodeAlsoDamages()
        {
            // T.7 #57: el impacto es ThawProgress y no Damaged, pero le quita Energía igual.
            NodeController source = Node("Star", Faction.Player, 50, 1, Vector2.zero);
            NodeController frozen = Node("Star", Faction.Enemy, 40, 1, new Vector2(-1f, 0f));
            frozen.Freeze();

            Proton hit = Send(source, frozen);
            Step(0f);
            Land(hit);
            Assert.IsTrue(frozen.HasState(NodeStateFlags.Frozen), "Sigue congelado: solo avanza el deshielo.");
            Assert.Less(frozen.Energy, 40);
            string damage = Shown.Single(name => name.StartsWith("dmg_"));
            StringAssert.StartsWith("dmg_player_24_", damage, "La fila del bando del protón y la talla del nodo.");
        }

        [Test]
        public void EffectsRunAtTwelveFramesASecondStopInPauseAndGoBackToThePool()
        {
            NodeController source = Node("Star", Faction.Player, 40, 1, Vector2.zero);
            Send(source, Node("Star", Faction.Player, 10, 1, new Vector2(1f, 0f)));
            Step(0f);
            Showing("send_player_24_0_0");

            Step(Frame);
            Showing("send_player_24_0_1");
            Step(0f);
            Showing("send_player_24_0_1");
            Step(Frame);
            Showing("send_player_24_0_2");
            Step(Frame);
            Assert.AreEqual(0, pool.ActiveCount, "ART-FX-SEND dura 3 frames.");
        }

        // --- Poderes ---

        PowerBeam Launch(NodeController pulsar, NodeController target, PowerType power) =>
            beams.Launch(pulsar, target, Faction.Player, PowerAsset(power));

        void LandBeam()
        {
            for (int i = 0; i < 20; i++)
                beams.Tick(Tick);
        }

        [Test]
        public void TheCastFlashesOnThePulsarWithThePowerAndItsLevel()
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 40, 2, Vector2.zero);
            Launch(pulsar, Node("Star", Faction.Enemy, 40, 1, new Vector2(3f, 0f)), PowerType.Decay);
            SpriteRenderer cast = Showing("cast_decay_2_0_0");
            Assert.AreSame(BodyOf(pulsar), cast.transform.parent);
        }

        [Test]
        public void TheAbsoluteZeroImpactHandsOverToTheIceOverlayOnTheNextFrame()
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 40, 1, Vector2.zero);
            NodeController target = Node("Star", Faction.Enemy, 40, 1, new Vector2(3f, 0f));
            NodeAnimator animator = target.GetComponent<NodeAnimator>();
            Launch(pulsar, target, PowerType.AbsoluteZero);
            LandBeam();

            Assert.IsTrue(target.HasState(NodeStateFlags.Frozen));
            Assert.AreSame(BodyOf(target), Showing("pwr_zer_24_0_0").transform.parent);
            Assert.IsFalse(animator.FrozenTint.enabled, "El impacto acaba en el overlay: no se dibujan los dos.");

            for (int f = 1; f <= 8; f++)
            {
                Step(Frame);
                Showing($"pwr_zer_24_0_{f}");
                Assert.IsFalse(animator.FrozenTint.enabled);
            }
            Step(Frame);
            Assert.IsFalse(Shown.Any(name => name.StartsWith("pwr_zer_")), "9 frames.");
            Assert.IsTrue(animator.FrozenTint.enabled, "En el frame siguiente entra el overlay.");
        }

        [Test]
        public void TheDecayImpactHandsOverToTheAuraAtItsFrameZero()
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 40, 1, Vector2.zero);
            NodeController target = Node("Magnetar", Faction.Neutral, 100, 2, new Vector2(3f, 0f));
            NodeAnimator animator = target.GetComponent<NodeAnimator>();
            Launch(pulsar, target, PowerType.Decay);
            LandBeam();

            Assert.IsTrue(target.HasState(NodeStateFlags.Infected));
            Showing("pwr_dec_36_0_0");
            Assert.IsFalse(animator.InfectedAura.enabled);
            for (int f = 1; f <= 8; f++)
                Step(Frame);
            Showing("pwr_dec_36_0_8");
            Assert.IsFalse(animator.InfectedAura.enabled);

            Step(Frame);
            Assert.IsTrue(animator.InfectedAura.enabled);
            Assert.AreEqual("decay_0", animator.InfectedAura.sprite.name, "El aura entra por su frame 0 (A.5).");
            Step(Frame);
            Assert.AreEqual("decay_1", animator.InfectedAura.sprite.name);
        }

        [Test]
        public void TheWormholeImpactPlaysUnderCapturedFromTheSameFrame()
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 40, 1, Vector2.zero);
            NodeController target = Node("Star", Faction.Enemy, 40, 3, new Vector2(3f, 0f));
            NodeAnimator animator = target.GetComponent<NodeAnimator>();
            Launch(pulsar, target, PowerType.Wormhole);
            LandBeam();

            Assert.AreEqual(Faction.Player, target.Faction);
            Assert.IsTrue(animator.IsCapturePlaying);
            SpriteRenderer impact = Showing("pwr_wrm_36_0_0");
            Assert.Less(impact.sortingOrder, animator.Effect.sortingOrder, "«El relevo» va encima.");
            Assert.IsFalse(Shown.Any(name => name.StartsWith("thaw_") || name.StartsWith("cure_")));
        }

        // --- Estados que acaban ---

        [Test]
        public void TheThawPlaysWhenTheIceGoesBySilenceButNotWhenAWormholeTakesIt()
        {
            NodeController alone = Node("Star", Faction.Enemy, 40, 1, Vector2.zero);
            NodeController stolen = Node("Star", Faction.Enemy, 40, 1, new Vector2(2f, 0f));
            Step(0f);
            alone.Freeze();
            alone.GetComponent<NodeFreeze>().Tick(Balance.AutoThawTime);
            Assert.IsFalse(alone.HasState(NodeStateFlags.Frozen));
            Assert.AreSame(BodyOf(alone), Showing("thaw_24_0_0").transform.parent);

            stolen.Freeze();
            stolen.Infect();
            stolen.Steal(Faction.Player);
            Assert.AreEqual(1, Shown.Count(name => name.StartsWith("thaw_")), "Capturado no es un deshielo.");
            Assert.IsFalse(Shown.Any(name => name.StartsWith("cure_")), "Ni una cura.");
        }

        [Test]
        public void TheCureStartsFromTheFrameTheAuraWasIn()
        {
            NodeController star = Node("Star", Faction.Enemy, 40, 2, Vector2.zero);
            NodeAnimator animator = star.GetComponent<NodeAnimator>();
            star.Infect();
            for (int i = 0; i < 6; i++)
                Step(Frame);
            int loopFrame = animator.InfectedLoopFrame;
            Assert.AreEqual(5, loopFrame);
            Assert.AreEqual($"decay_{loopFrame}", animator.InfectedAura.sprite.name);

            star.ApplyImpact(new ImpactOutcome(ImpactResult.Absorbed, star.Energy + 1, star.Faction, false, false, true, null));
            Assert.IsFalse(star.HasState(NodeStateFlags.Infected));
            Showing($"cure_32_{loopFrame}_0");
        }

        // --- Encendido y mejora ---

        [Test]
        public void IgnitingFlashesTheNewNodeAndUpgradingRaisesItsChevrons()
        {
            NodeController asteroid = Node("Asteroid", Faction.Player, 10, 0, Vector2.zero); // el Tope del Asteroide
            var ignition = asteroid.gameObject.AddComponent<NodeIgnition>();
            ignition.Configure(Balance, new[] { Definition("Star") });
            Step(0f); // el pool ve los nodos en su primer frame, como al cargar el nivel
            Assert.IsTrue(ignition.TryIgnite(Faction.Player, NodeType.Star).IsAllowed);

            Assert.AreEqual(new[] { "ign_player_24_0_0" }, Shown, "Encender deja el nodo en el nivel 1: no es una mejora.");

            asteroid.AddEnergy(1000);
            Assert.IsTrue(asteroid.TryUpgrade(Faction.Player).IsAllowed);
            Showing("upg_player_str2_0_0");
        }

        // --- Sobre el protón ---

        Proton Fly(Faction faction, int charge, Vector2 from)
        {
            var go = new GameObject("Proton");
            spawned.Add(go);
            var proton = go.AddComponent<Proton>();
            // Destino lejano: nunca llega durante el test.
            NodeController far = Node("Star", Faction.Neutral, 40, 1, new Vector2(50f, 0f));
            proton.Launch(from, far, faction, charge, Balance);
            protons.Register(proton);
            return proton;
        }

        [Test]
        public void TheShotFlashesOnTheProtonThatSurvivesAndFollowsIt()
        {
            Proton survivor = Fly(Faction.Player, 12, Vector2.zero);
            pool.QueueShot(survivor);
            survivor.TakeShot(2);
            Step(0f);
            string shot = Shown.Single(name => name.StartsWith("shot_"));
            StringAssert.StartsWith("shot_player_2_", shot, "La talla de la Carga que le queda: 10, la segunda.");

            survivor.Tick(0.1f);
            Step(0f);
            SpriteRenderer renderer = pool.Renderers.Single(r => r.sprite.name.StartsWith("shot_"));
            Vector3 at = survivor.transform.position;
            Assert.AreEqual(Mathf.Round(at.x * 64f) / 64f, renderer.transform.position.x, 1e-5f, "Cuelga del protón.");

            Proton fallen = Fly(Faction.Enemy, 2, new Vector2(0f, 1f));
            pool.QueueShot(fallen);
            fallen.TakeShot(2);
            Step(0f);
            Assert.IsFalse(Shown.Any(name => name.StartsWith("shot_enemy")), "El que cae no destella: lo sustituye KILL.");
        }

        [Test]
        public void KillAndVanishReplaceTheProtonFromTheFrameOfItsLoop()
        {
            var at = new Vector3(1.5f, -0.25f, 0f);
            pool.HandleProtonEnded(new ProtonEnding(ProtonFate.ShotDown, Faction.Enemy, 0, 7, at));
            SpriteRenderer kill = Showing("kill_enemy_7_0");
            Assert.AreEqual(at, kill.transform.position);

            pool.HandleProtonEnded(new ProtonEnding(ProtonFate.Vanished, Faction.Player, 3, 11, at));
            Showing("vanish_player_4_11_0");
        }

        // --- Cascada ---

        [Test]
        public void EveryNodeTheCascadeConquersGetsItsFireworks()
        {
            Node("Star", Faction.Player, 40, 1, Vector2.zero);
            NodeController enemy = Node("Star", Faction.Enemy, 10, 1, new Vector2(2f, 0f));
            Node("Asteroid", Faction.Neutral, 10, 0, new Vector2(1f, 2f));
            Node("Magnetar", Faction.Neutral, 100, 2, new Vector2(4f, 2f));
            var checker = systems.AddComponent<WinConditionChecker>();
            checker.Configure(Balance, registry, protons);
            pool.UseWinChecker(checker);

            enemy.Steal(Faction.Player);
            for (int i = 0; i < Mathf.RoundToInt((Balance.CascadeDuration + 0.1f) / Tick); i++)
                checker.Tick(Tick);

            CollectionAssert.AreEquivalent(new[] { "cascade_28_0_0", "cascade_36_0_0" },
                Shown.Where(name => name.StartsWith("cascade_")).ToArray());
        }
    }
}
