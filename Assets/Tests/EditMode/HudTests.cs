using System.Collections.Generic;
using System.Linq;
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
    // T6-08: el HUD permanente (13.6) con su arte (A.6). La barra de poder enseña el reparto de la Energía
    // de los nodos encendidos de cada bando, montada por coordenada absoluta y con el frente viajando por la
    // curva orbital; la barra de arriba, el nivel, el tiempo de juego y el botón de pausa, cuyo clic es suyo.
    // El arte es de prueba, con las medidas de los .json de verdad y el pivote arriba a la izquierda.
    public class HudTests
    {
        const float Tick = 0.02f;

        readonly List<Object> spawned = new List<Object>();
        NodeRegistry registry;
        HudArt art;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        static UiText Text => AssetDatabase.LoadAssetAtPath<UiText>("Assets/Data/UiText.asset");

        static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Jersey10 Raster56.asset");

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            var go = new GameObject("Systems");
            spawned.Add(go);
            registry = go.AddComponent<NodeRegistry>();
            art = TestArt();
        }

        [TearDown]
        public void TearDown()
        {
            registry.Clear();
            foreach (Object item in spawned)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }
            spawned.Clear();
            Time.timeScale = 1f;
        }

        // --- Arte de prueba ---

        Sprite Cell(string name, int width, int height)
        {
            var texture = new Texture2D(width, height) { filterMode = FilterMode.Point };
            spawned.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0f, 1f), 64, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            spawned.Add(sprite);
            return sprite;
        }

        Sprite[] Frames(string name, int count, int width, int height) =>
            Enumerable.Range(0, count).Select(i => Cell($"{name}_{i}", width, height)).ToArray();

        HudArt TestArt(int snap = 1)
        {
            var hud = ScriptableObject.CreateInstance<HudArt>();
            spawned.Add(hud);
            hud.ConfigurePowerBar(Cell("frame", 960, 12), Frames("player", 32, 64, 12), Frames("enemy", 32, 64, 12),
                Frames("front", 32, 16, 12), 12, 4, 956, snap, 12f, 8);
            var plateTexture = new Texture2D(20, 20) { filterMode = FilterMode.Point };
            spawned.Add(plateTexture);
            Sprite plate = Sprite.Create(plateTexture, new Rect(0, 0, 20, 20), new Vector2(0f, 1f), 64, 0,
                SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
            plate.name = "plate";
            spawned.Add(plate);
            hud.ConfigureTopBar(plate, Frames("pause", 3, 20, 22), 4, 4, 20, 6, 6, 4, 8, 1,
                new Color32(0xB5, 0xBC, 0xCD, 0xFF), new Color32(0xE2, 0xE6, 0xEF, 0xFF), "00:00", new Vector2Int(20, 22));
            return hud;
        }

        NodeController Node(string type, Faction faction, int energy, int level = 1)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, type == "Asteroid" ? 0 : level);
            registry.Register(node);
            return node;
        }

        PowerBarUI Bar(HudArt hudArt = null)
        {
            var bar = registry.gameObject.AddComponent<PowerBarUI>();
            bar.Configure(hudArt ?? art, registry);
            return bar;
        }

        static Vector2 Screen(float x, float y) => HudArt.ScreenToWorld(x, y);

        // --- Lo que cuenta la barra ---

        [Test]
        public void TheBarCountsTheEnergyOfIgnitedNodesOfEachSideOnly()
        {
            Node("Star", Faction.Player, 25);
            Node("Pulsar", Faction.Player, 40);
            Node("Asteroid", Faction.Player, 7);     // propio, sin encender: no cuenta (NOVA_Nodos.md)
            Node("Star", Faction.Enemy, 30);
            Node("Asteroid", Faction.Neutral, 10);   // Materia Inerte (13.6)
            Node("Magnetar", Faction.Neutral, 100);  // el neutral no es un bando

            Assert.AreEqual(65, PowerBarUI.EnergyOf(registry, Faction.Player));
            Assert.AreEqual(30, PowerBarUI.EnergyOf(registry, Faction.Enemy));
            Assert.AreEqual(0, PowerBarUI.EnergyOf(registry, Faction.Neutral));
        }

        [Test]
        public void TheBarIsThePlayersShareOfBothSides()
        {
            Assert.AreEqual(0.25f, PowerBarUI.Share(25, 75), 1e-6f);
            Assert.AreEqual(1f, PowerBarUI.Share(10, 0), 1e-6f);
            Assert.AreEqual(0f, PowerBarUI.Share(0, 10), 1e-6f);
            Assert.AreEqual(0.5f, PowerBarUI.Share(0, 0), 1e-6f, "Sin Energía en ninguno, a medias.");
        }

        [Test]
        public void TheFrontFallsOnTheShareSnappedFromX0()
        {
            Assert.AreEqual(480, art.FrontPixel(0.5f), "El 50 % cae en el centro de la pantalla.");
            Assert.AreEqual(4, art.FrontPixel(0f));
            Assert.AreEqual(956, art.FrontPixel(1f));
            HudArt cells = TestArt(4);
            Assert.AreEqual(4 + 71 * 4, cells.FrontPixel(0.3f), "Con paso 4, de celda en celda.");
        }

        // --- El montaje ---

        [Test]
        public void TheEnemyFillsTheInteriorAndThePlayerCoversItUpToTheFront()
        {
            Node("Star", Faction.Player, 25);
            Node("Pulsar", Faction.Enemy, 75); // tope 100: una Estrella no pasa de 50
            PowerBarUI bar = Bar();

            bar.Advance(0f);

            int front = 4 + Mathf.RoundToInt(0.25f * 952);
            Assert.AreEqual(front, bar.FrontPixel);
            Assert.AreEqual(Screen(0, 528), (Vector2)bar.FrameRenderer.transform.position, "La carcasa, pegada al borde de abajo.");
            Assert.AreEqual(Screen(4, 528), (Vector2)bar.EnemyRenderer.transform.position);
            Assert.AreEqual(952f, bar.EnemyRenderer.size.x * 64f, 1e-3f, "El Enemigo, por todo el interior.");
            Assert.AreEqual(Screen(4, 528), (Vector2)bar.PlayerRenderer.transform.position, "Los dos, desde x0: coordenada absoluta.");
            Assert.AreEqual(front - 4, bar.PlayerRenderer.size.x * 64f, 1e-3f, "El Jugador, de x0 al reparto.");
            Assert.AreEqual(Screen(front - 8, 528), (Vector2)bar.FrontRenderer.transform.position, "El contacto del frente, en el reparto.");
            Assert.AreEqual(SpriteDrawMode.Tiled, bar.PlayerRenderer.drawMode, "En mosaico, no estirado.");
            Assert.Less(bar.EnemyRenderer.sortingOrder, bar.PlayerRenderer.sortingOrder);
            Assert.Less(bar.PlayerRenderer.sortingOrder, bar.FrontRenderer.sortingOrder);
        }

        [Test]
        public void WithoutPlayerEnergyThePlayerFillIsHidden()
        {
            Node("Pulsar", Faction.Enemy, 75); // tope 100: una Estrella no pasa de 50
            PowerBarUI bar = Bar();
            bar.Advance(0f);
            Assert.IsFalse(bar.PlayerRenderer.enabled);
            Assert.AreEqual(4, bar.FrontPixel);
        }

        [Test]
        public void TheFrontTravelsToANewShareWithTheOrbitalCurve()
        {
            NodeController player = Node("Star", Faction.Player, 25);
            Node("Star", Faction.Enemy, 25);
            PowerBarUI bar = Bar();
            bar.Advance(0f);
            Assert.AreEqual(0.5f, bar.Shown, 1e-6f, "El primer frame entra ya en su sitio.");

            player.AddEnergy(25); // 50 contra 25: dos tercios
            bar.Advance(0.08f);
            Assert.AreEqual(2f / 3f, bar.Target, 1e-6f);
            Assert.Greater(bar.Shown, 0.5f);
            Assert.Less(bar.Shown, 2f / 3f, "A medio camino.");
            float early = bar.Shown - 0.5f;
            Assert.Greater(early, (2f / 3f - 0.5f) * 0.25f, "La orbital sale rápido (13.12).");

            bar.Advance(0.5f);
            Assert.AreEqual(2f / 3f, bar.Shown, 1e-6f, "Y se posa.");
        }

        [Test]
        public void ThePlasmaLoopsAtTwelveFramesASecondAndStopsInPause()
        {
            Node("Star", Faction.Player, 25);
            Node("Star", Faction.Enemy, 25);
            PowerBarUI bar = Bar();
            bar.Advance(0f);
            Assert.AreEqual("player_0", bar.PlayerRenderer.sprite.name);

            bar.Advance(3.5f / 12f);
            Assert.AreEqual("player_3", bar.PlayerRenderer.sprite.name);
            Assert.AreEqual("enemy_3", bar.EnemyRenderer.sprite.name);
            Assert.AreEqual("front_3", bar.FrontRenderer.sprite.name);
            bar.Advance(0f); // en pausa, Time.deltaTime es 0
            Assert.AreEqual("player_3", bar.PlayerRenderer.sprite.name);
            bar.Advance(29f / 12f);
            Assert.AreEqual("player_0", bar.PlayerRenderer.sprite.name, "32 frames y vuelta a empezar.");
        }

        [Test]
        public void WithoutArtThereIsNoBar()
        {
            Node("Star", Faction.Player, 25);
            var empty = ScriptableObject.CreateInstance<HudArt>();
            spawned.Add(empty);
            PowerBarUI bar = Bar(empty);
            bar.Advance(0f);
            Assert.IsFalse(bar.HasArt);
            Assert.AreEqual(0, bar.transform.childCount);
        }

        // --- La barra de arriba ---

        [Test]
        public void ThePlatesGrowInStepsOfTheTileAndThePauseButtonSitsTopRight()
        {
            Assert.AreEqual(20, art.PlateWidth(0), "Al menos un paso de centro.");
            Assert.AreEqual(116, art.PlateWidth(100), "100 + 12 de aire − 12 de bordes = 100 de centro → 104, más los dos bordes.");
            Assert.AreEqual(0, (art.PlateWidth(57) - 12) % 8);
            Assert.AreEqual(new RectInt(936, 4, 20, 20), art.PauseRect);
            Assert.AreEqual(new RectInt(936 - 4 - 44, 4, 44, 20), art.TimerRect(44));
            Assert.AreEqual(new RectInt(4, 4, 116, 20), art.LevelRect(116));
        }

        [Test]
        public void ThePauseButtonLightsUpUnderTheCursorAndSinksWhenPressedInside()
        {
            var hud = registry.gameObject.AddComponent<HudState>();
            hud.Configure(art);
            Vector2 inside = Screen(946, 14);
            Vector2 outside = Screen(900, 14);

            hud.SetPointer(outside, false);
            Assert.AreEqual(0, hud.ButtonState);
            hud.SetPointer(inside, false);
            Assert.AreEqual(1, hud.ButtonState, "Encima.");
            hud.SetPointer(inside, true);
            Assert.AreEqual(2, hud.ButtonState, "Pulsado.");
            hud.SetPointer(outside, true);
            Assert.AreEqual(0, hud.ButtonState, "Fuera, ya no se hunde.");
            hud.SetPointer(inside, true);
            Assert.AreEqual(2, hud.ButtonState, "Vuelve a hundirse: la pulsación empezó dentro.");
            hud.SetPointer(outside, false);

            hud.SetPointer(outside, true);
            hud.SetPointer(inside, true);
            Assert.AreEqual(1, hud.ButtonState, "Una pulsación que empezó fuera no lo hunde.");
        }

        InputRouter Router(HudState hud, out LassoState lasso, out SelectionManager selection)
        {
            var go = registry.gameObject;
            var launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, go.AddComponent<ProtonPool>());
            selection = go.AddComponent<SelectionManager>();
            selection.Configure(registry);
            var issuer = go.AddComponent<PlayerCommandIssuer>();
            issuer.Configure(launcher);
            var menu = go.AddComponent<IgnitionMenuState>();
            var panel = go.AddComponent<InfoPanelState>();
            lasso = go.AddComponent<LassoState>();
            var router = go.AddComponent<InputRouter>();
            router.Configure(Balance, selection, issuer, menu, panel, null, null, null, null, lasso, hud);
            return router;
        }

        [Test]
        public void AClickOnThePauseButtonPausesAndNeverReachesTheBoard()
        {
            var hud = registry.gameObject.AddComponent<HudState>();
            hud.Configure(art);
            InputRouter router = Router(hud, out LassoState lasso, out SelectionManager selection);
            int pauses = 0;
            router.OnPauseRequested += () => pauses++;

            router.PressLeft(Screen(946, 14), null);
            router.ReleaseLeft(Screen(947, 15), null);
            Assert.AreEqual(1, pauses, "13.10: el botón de pausa del HUD abre la pausa.");

            // Un arrastre que empieza en el botón no es lazo, y soltarlo fuera no pausa.
            router.PressLeft(Screen(946, 14), null);
            router.MoveTo(Screen(700, 200));
            Assert.IsFalse(lasso.IsActive, "No hay lazo desde el botón.");
            router.ReleaseLeft(Screen(700, 200), null);
            Assert.AreEqual(1, pauses);
            Assert.IsFalse(selection.HasSelection);

            // Sin arte no hay botón: el mismo sitio es tablero.
            hud.Configure(null);
            router.PressLeft(Screen(946, 14), null);
            router.ReleaseLeft(Screen(946, 14), null);
            Assert.AreEqual(1, pauses);
        }

        // Una partida mínima para la barra de arriba: GameManager con el nivel 5 cargado.
        GameManager Game(out MatchRecorder recorder)
        {
            var go = registry.gameObject;
            var protons = go.AddComponent<ProtonRegistry>();
            var template = new GameObject("Node");
            spawned.Add(template);
            template.AddComponent<NodeController>();
            var loader = go.AddComponent<LevelLoader>();
            loader.Configure(Balance, template.GetComponent<NodeController>(),
                new[] { Definition("Asteroid"), Definition("Star"), Definition("Pulsar"), Definition("Magnetar") },
                new PowerDefinition[0], registry);
            var checker = go.AddComponent<WinConditionChecker>();
            checker.Configure(Balance, registry, protons, loader);
            recorder = go.AddComponent<MatchRecorder>();
            recorder.Configure(registry, checker);
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            spawned.Add(level);
            level.Configure(5, AssetDatabase.LoadAssetAtPath<EnemyProfile>("Assets/Data/EnemyProfiles/Map2.asset"), 120f,
                new[]
                {
                    new NodeSpawnData(new Vector2(-5f, 0f), NodeType.Star, Faction.Player, 25, 1),
                    new NodeSpawnData(new Vector2(5f, 0f), NodeType.Star, Faction.Enemy, 25, 1),
                },
                new[] { NodeType.Star }, new PowerType[0], false);
            var game = go.AddComponent<GameManager>();
            game.Configure(loader, checker, recorder, new[] { level }, null, _ => { });
            game.Begin(level);
            return game;
        }

        [Test]
        public void TheTopBarShowsTheLevelAndTheGameTimeOnTheirPlates()
        {
            GameManager game = Game(out MatchRecorder recorder);
            var hud = registry.gameObject.AddComponent<HudState>();
            hud.Configure(art);
            var top = registry.gameObject.AddComponent<HudTopBar>();
            top.Configure(art, hud, game, Font, Text);

            top.Refresh();
            Assert.AreEqual("MAPA 2 · NIVEL 5", top.LevelText);
            Assert.AreEqual("0:00", top.TimerText);
            Assert.AreEqual(Screen(4, 4), (Vector2)top.LevelPlate.transform.position, "Arriba a la izquierda.");
            Assert.AreEqual(0, ((int)Mathf.Round(top.LevelPlate.size.x * 64f) - 12) % 8, "El centro, en pasos de 8.");
            Assert.Greater(top.LevelPlate.size.x * 64f, 12f + 60f, "Cabe el texto.");
            Assert.AreEqual(SpriteDrawMode.Tiled, top.LevelPlate.drawMode, "Nueve cortes con el centro en mosaico.");
            Assert.AreEqual(Screen(936, 4), (Vector2)top.Pause.transform.position, "El botón, arriba a la derecha.");
            Assert.AreEqual(Screen(936 - 4 - top.TimerWidth, 4), (Vector2)top.TimerPlate.transform.position,
                "El cronómetro, a 4 px del botón.");

            for (int i = 0; i < Mathf.RoundToInt(134f / Tick); i++)
                recorder.Tick(Tick);
            top.Refresh();
            Assert.AreEqual("2:14", top.TimerText, "Tiempo de juego de MatchRecorder.");
        }

        [Test]
        public void ThePauseButtonFollowsItsState()
        {
            var hud = registry.gameObject.AddComponent<HudState>();
            hud.Configure(art);
            var top = registry.gameObject.AddComponent<HudTopBar>();
            top.Configure(art, hud, null, Font, Text);

            top.Refresh();
            Assert.AreEqual("pause_0", top.Pause.sprite.name);
            Assert.IsFalse(top.LevelPlate.gameObject.activeSelf, "Sin partida no hay nivel que enseñar.");
            hud.SetPointer(Screen(946, 14), false);
            top.Refresh();
            Assert.AreEqual("pause_1", top.Pause.sprite.name);
            hud.SetPointer(Screen(946, 14), true);
            top.Refresh();
            Assert.AreEqual("pause_2", top.Pause.sprite.name);
        }
    }
}
