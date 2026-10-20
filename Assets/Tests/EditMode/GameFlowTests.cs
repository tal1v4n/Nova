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
using Object = UnityEngine.Object;

namespace Nova.Tests
{
    // T5-05: el flujo del nivel (10.3, 13.10, 13.11, 15.8). GameManager carga, pausa con la escala a 0,
    // congela al terminar y sale de Game con la escala devuelta a 1; MatchRecorder mide la partida;
    // InputRouter se suspende fuera de partida; el menú de pausa confirma lo que tira la partida y
    // las pantallas de fin enseñan sus datos y, en el estancamiento, las cinco vías.
    public class GameFlowTests
    {
        const float Tick = 0.02f;

        readonly List<Object> spawned = new List<Object>();
        readonly List<string> loadedScenes = new List<string>();
        readonly List<float> scaleAtLoad = new List<float>();
        readonly List<LevelOutcome> ended = new List<LevelOutcome>();
        NodeRegistry registry;
        LevelLoader loader;
        WinConditionChecker checker;
        MatchRecorder recorder;
        GameManager game;
        GameObject template;
        LevelDefinition[] levels;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static PowerDefinition[] Powers => new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") };

        static NodeDefinition[] Definitions => new[] { Definition("Asteroid"), Definition("Star"), Definition("Pulsar"), Definition("Magnetar") };

        static EnemyProfile Profile(int map) =>
            AssetDatabase.LoadAssetAtPath<EnemyProfile>($"Assets/Data/EnemyProfiles/Map{map}.asset");

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        static UiText Text => AssetDatabase.LoadAssetAtPath<UiText>("Assets/Data/UiText.asset");

        static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Jersey10 Raster56.asset");

        static readonly NodeType[] AllTypes = { NodeType.Star, NodeType.Pulsar, NodeType.Magnetar };

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            var systems = new GameObject("Systems");
            spawned.Add(systems);
            registry = systems.AddComponent<NodeRegistry>();
            var protons = systems.AddComponent<ProtonRegistry>();

            template = new GameObject("Node");
            spawned.Add(template);
            template.AddComponent<NodeController>();
            template.AddComponent<NodeIgnition>().Configure(Balance, AllTypes.Select(t => Definition(t.ToString())));
            template.AddComponent<NodeConverter>().Configure(Balance, AllTypes.Select(t => Definition(t.ToString())));
            template.AddComponent<NodeFreeze>().Configure(Balance);
            template.AddComponent<NodeDecay>().Configure(Balance);
            template.AddComponent<PulsarCaster>().Configure(Balance, Powers);

            loader = systems.AddComponent<LevelLoader>();
            loader.Configure(Balance, template.GetComponent<NodeController>(), Definitions, Powers, registry);
            checker = systems.AddComponent<WinConditionChecker>();
            checker.Configure(Balance, registry, protons, loader);
            recorder = systems.AddComponent<MatchRecorder>();
            recorder.Configure(registry, checker);

            // Por defecto, una partida que sigue: una Estrella por bando y un Asteroide neutral.
            levels = new LevelDefinition[9];
            for (int i = 0; i < levels.Length; i++)
                levels[i] = Level(i + 1, Star(Faction.Player, 20, -5f), Star(Faction.Enemy, 20, 5f), Asteroid(-3f));

            game = systems.AddComponent<GameManager>();
            loadedScenes.Clear();
            scaleAtLoad.Clear();
            game.Configure(loader, checker, recorder, levels, null, scene =>
            {
                loadedScenes.Add(scene);
                scaleAtLoad.Add(Time.timeScale);
            });
            ended.Clear();
            game.OnLevelEnded += ended.Add;
        }

        [TearDown]
        public void TearDown()
        {
            loader.Unload();
            registry.Clear();
            foreach (Object item in spawned)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }
            spawned.Clear();
            Time.timeScale = 1f;
        }

        // Las mecánicas de 11.1 por mapa.
        LevelDefinition Level(int number, params NodeSpawnData[] nodes)
        {
            int map = (number - 1) / 3 + 1;
            NodeType[] ignition = map == 1 ? new[] { NodeType.Star, NodeType.Magnetar } : AllTypes;
            PowerType[] powers = map == 1 ? new PowerType[0]
                : map == 2 ? new[] { PowerType.AbsoluteZero, PowerType.Decay }
                : new[] { PowerType.AbsoluteZero, PowerType.Decay, PowerType.Wormhole };

            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.name = $"Level{number}";
            spawned.Add(level);
            level.Configure(number, Profile(map), 90f + 20f * (number - 1), nodes, ignition, powers, map == 3);
            return level;
        }

        static NodeSpawnData Star(Faction faction, int energy, float x, int level = 1) =>
            new NodeSpawnData(new Vector2(x, 0f), NodeType.Star, faction, energy, level);

        static NodeSpawnData Asteroid(float x, Faction faction = Faction.Neutral) =>
            new NodeSpawnData(new Vector2(x, 2f), NodeType.Asteroid, faction, 10);

        void Run(float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / Tick); i++)
            {
                checker.Tick(Tick);
                recorder.Tick(Tick);
            }
        }

        NodeController NodeOf(Faction faction, NodeType type) =>
            registry.GetNodes(faction).First(n => n.Type == type);

        // Victoria: el Enemigo sin nodos, y la cascada de 3 s con el Asteroide neutral.
        void Win()
        {
            NodeController enemy = NodeOf(Faction.Enemy, NodeType.Star);
            enemy.Steal(Faction.Player);
            Run(Balance.CascadeDuration + 0.1f);
        }

        // --- GameManager ---

        [Test]
        public void BeginLoadsTheLevelAndPlays()
        {
            game.Begin(levels[0]);
            Assert.AreSame(levels[0], loader.Current);
            Assert.AreEqual(3, registry.Count);
            Assert.AreEqual(GameFlowState.Playing, game.State);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreSame(levels[1], game.NextLevel);
        }

        [Test]
        public void PausingIsTheTimeScaleAtZero()
        {
            game.Begin(levels[0]);
            game.Pause();
            Assert.AreEqual(GameFlowState.Paused, game.State);
            Assert.AreEqual(0f, Time.timeScale, "15.8: la partida entera se para sin que ningún sistema lo sepa.");

            game.Resume();
            Assert.AreEqual(GameFlowState.Playing, game.State);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void TheVictoryFreezesTheGameAfterTheCascade()
        {
            game.Begin(levels[0]);
            NodeOf(Faction.Enemy, NodeType.Star).Steal(Faction.Player);
            Run(1f);
            Assert.AreEqual(GameFlowState.Playing, game.State, "La cascada corre en tiempo de juego (10.3).");

            Run(Balance.CascadeDuration);
            Assert.AreEqual(GameFlowState.Ended, game.State);
            Assert.AreEqual(LevelOutcome.Victory, game.Outcome);
            Assert.AreEqual(0f, Time.timeScale);
            CollectionAssert.AreEqual(new[] { LevelOutcome.Victory }, ended);
        }

        [Test]
        public void TheDefeatFreezesTheGameAtOnce()
        {
            game.Begin(levels[0]);
            NodeOf(Faction.Player, NodeType.Star).Steal(Faction.Enemy);
            Run(Tick);
            Assert.AreEqual(GameFlowState.Ended, game.State);
            Assert.AreEqual(LevelOutcome.Annihilation, game.Outcome);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void TheEndScreenCannotBePaused()
        {
            game.Begin(levels[0]);
            Win();
            game.Pause();
            Assert.AreEqual(GameFlowState.Ended, game.State);
        }

        [Test]
        public void RestartLoadsGameWithTheSameLevelAndTheScaleBackAtOne()
        {
            game.Begin(levels[4]);
            game.Pause();
            game.Restart();
            CollectionAssert.AreEqual(new[] { GameManager.GameScene }, loadedScenes);
            CollectionAssert.AreEqual(new[] { 1f }, scaleAtLoad, "15.8: timeScale sobrevive al cambio de escena.");
            Assert.AreSame(levels[4], GameManager.Requested);
        }

        [Test]
        public void NextLoadsTheNextLevel()
        {
            game.Begin(levels[0]);
            Win();
            game.Next();
            CollectionAssert.AreEqual(new[] { GameManager.GameScene }, loadedScenes);
            Assert.AreSame(levels[1], GameManager.Requested);
            Assert.AreEqual(1f, scaleAtLoad[0]);
        }

        [Test]
        public void AfterTheNinthNextGoesBackToTheMap()
        {
            game.Begin(levels[8]);
            Assert.IsNull(game.NextLevel);
            game.Next();
            CollectionAssert.AreEqual(new[] { GameManager.LevelSelectScene }, loadedScenes);
            Assert.IsNull(GameManager.Requested);
        }

        [Test]
        public void BackToMapLoadsLevelSelect()
        {
            game.Begin(levels[0]);
            game.Pause();
            game.BackToMap();
            CollectionAssert.AreEqual(new[] { GameManager.LevelSelectScene }, loadedScenes);
            CollectionAssert.AreEqual(new[] { 1f }, scaleAtLoad);
        }

        // --- MatchRecorder ---

        [Test]
        public void TheRecorderTakesOneSamplePerSecondOfPlay()
        {
            game.Begin(levels[0]);
            Assert.AreEqual(1, recorder.Samples.Count, "La muestra del segundo 0.");
            Assert.AreEqual(0.5f, recorder.Samples[0], 1e-6f, "20 contra 20.");

            Run(2f);
            Assert.AreEqual(3, recorder.Samples.Count);
            Assert.AreEqual(2f, recorder.Elapsed, 1e-4f);
        }

        [Test]
        public void TheRecorderKeepsThePeakOfThePlayerBar()
        {
            game.Begin(levels[0]);
            Run(1f);
            NodeOf(Faction.Player, NodeType.Star).AddEnergy(15);
            Run(1f);
            NodeOf(Faction.Player, NodeType.Star).SubtractEnergy(30);
            Run(1f);
            Assert.AreEqual(35, recorder.PeakEnergy);
            Assert.AreEqual(1f + Tick, recorder.PeakTime, 1e-4f, "El primer tick que lo ve.");
            Assert.AreEqual(PowerCurve.Share(5, 20), recorder.Samples[recorder.Samples.Count - 1], 1e-6f);
        }

        [Test]
        public void TheRecorderCountsCapturesMadeAndLost()
        {
            levels[0] = Level(1, Star(Faction.Player, 20, -5f), Star(Faction.Enemy, 20, 5f), Star(Faction.Enemy, 20, 3f), Asteroid(-3f));
            game.Begin(levels[0]);
            NodeOf(Faction.Enemy, NodeType.Star).Steal(Faction.Player);
            NodeOf(Faction.Neutral, NodeType.Asteroid).Steal(Faction.Player);
            registry.GetNodes(Faction.Player).First(n => n.Type == NodeType.Star).Steal(Faction.Enemy);
            Assert.AreEqual(2, recorder.CapturesMade);
            Assert.AreEqual(1, recorder.CapturesLost);
        }

        [Test]
        public void WhatTheCascadeConvertsIsNotACapture()
        {
            game.Begin(levels[0]);
            Win();
            Assert.AreEqual(1, recorder.CascadeConquered, "El Asteroide neutral.");
            Assert.AreEqual(1, recorder.CapturesMade, "Solo la Estrella del Enemigo; el Asteroide llegó por la cascada.");
        }

        [Test]
        public void TheRecorderStopsWhenTheLevelEnds()
        {
            game.Begin(levels[0]);
            NodeOf(Faction.Player, NodeType.Star).Steal(Faction.Enemy);
            Run(Tick);
            float at = recorder.Elapsed;
            Run(1f);
            Assert.IsFalse(recorder.IsRecording);
            Assert.AreEqual(at, recorder.Elapsed);
        }

        // --- InputRouter, con la pausa (15.8) ---

        InputRouter Router()
        {
            var go = new GameObject("Input");
            spawned.Add(go);
            var pool = go.AddComponent<ProtonPool>();
            var launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, pool);
            var selection = go.AddComponent<SelectionManager>();
            selection.Configure(registry);
            var issuer = go.AddComponent<PlayerCommandIssuer>();
            issuer.Configure(launcher);
            var menu = go.AddComponent<IgnitionMenuState>();
            var panel = go.AddComponent<InfoPanelState>();
            var sendLine = go.AddComponent<SendLineState>();
            sendLine.Configure(selection, menu);
            var router = go.AddComponent<InputRouter>();
            router.Configure(Balance, selection, issuer, menu, panel, null, sendLine, null, game);
            return router;
        }

        [Test]
        public void EscapeWithNothingOpenPauses()
        {
            game.Begin(levels[0]);
            InputRouter router = Router();
            router.PressEscape();
            Assert.AreEqual(GameFlowState.Paused, game.State);
            Assert.IsTrue(router.IsSuspended);

            game.Resume();
            Assert.IsFalse(router.IsSuspended);
        }

        [Test]
        public void PausingDropsAHalfDonePressWithoutActing()
        {
            game.Begin(levels[0]);
            InputRouter router = Router();
            InfoPanelState panel = router.GetComponent<InfoPanelState>();
            NodeController star = NodeOf(Faction.Player, NodeType.Star);

            router.PressRight(star.transform.position, star);
            game.Pause();
            game.Resume();
            router.ReleaseRight(star.transform.position, star);
            Assert.IsFalse(panel.IsOpen, "Sin la suspensión, soltar sería un clic derecho y abriría el panel.");

            router.PressRight(star.transform.position, star);
            router.ReleaseRight(star.transform.position, star);
            Assert.IsTrue(panel.IsOpen, "Después, la entrada vuelve a funcionar.");
        }

        // --- Menú de pausa (13.10) ---

        PauseMenu Pause()
        {
            var go = new GameObject("PauseMenu");
            spawned.Add(go);
            var menu = go.AddComponent<PauseMenu>();
            menu.Configure(game, Font, Text);
            return menu;
        }

        [Test]
        public void ThePauseMenuOpensAndClosesWithThePause()
        {
            game.Begin(levels[0]);
            PauseMenu menu = Pause();
            Assert.IsFalse(menu.IsVisible);
            game.Pause();
            Assert.IsTrue(menu.IsVisible);
            menu.Choose(PauseMenu.Choice.Resume);
            Assert.IsFalse(menu.IsVisible);
            Assert.AreEqual(GameFlowState.Playing, game.State);
        }

        [Test]
        public void RestartingAsksFirst()
        {
            game.Begin(levels[0]);
            PauseMenu menu = Pause();
            game.Pause();

            menu.Choose(PauseMenu.Choice.Restart);
            Assert.AreEqual(PauseMenu.Asking.Restart, menu.Pending);
            Assert.AreEqual(Text.ConfirmRestart, menu.PromptText);
            Assert.IsEmpty(loadedScenes, "Todavía no.");

            menu.Choose(PauseMenu.Choice.Confirm);
            CollectionAssert.AreEqual(new[] { GameManager.GameScene }, loadedScenes);
        }

        [Test]
        public void GoingBackToTheMapAsksFirst()
        {
            game.Begin(levels[0]);
            PauseMenu menu = Pause();
            game.Pause();

            menu.Choose(PauseMenu.Choice.BackToMap);
            Assert.AreEqual(Text.ConfirmBackToMap, menu.PromptText);
            Assert.IsEmpty(loadedScenes);

            menu.Choose(PauseMenu.Choice.Confirm);
            CollectionAssert.AreEqual(new[] { GameManager.LevelSelectScene }, loadedScenes);
        }

        [Test]
        public void EscapeCancelsTheConfirmationAndThenResumes()
        {
            game.Begin(levels[0]);
            PauseMenu menu = Pause();
            game.Pause();
            menu.Choose(PauseMenu.Choice.Restart);

            menu.PressEscape();
            Assert.AreEqual(PauseMenu.Asking.None, menu.Pending);
            Assert.AreEqual(GameFlowState.Paused, game.State, "El primer Esc solo cancela.");

            menu.PressEscape();
            Assert.AreEqual(GameFlowState.Playing, game.State);
        }

        [Test]
        public void TheConfirmationTakesThePlaceOfTheTwoButtons()
        {
            game.Begin(levels[0]);
            PauseMenu menu = Pause();
            game.Pause();
            Assert.IsTrue(menu.IsActive(PauseMenu.Choice.Resume));
            Assert.IsTrue(menu.IsActive(PauseMenu.Choice.Restart));
            Assert.IsTrue(menu.IsActive(PauseMenu.Choice.BackToMap));
            Assert.IsFalse(menu.IsActive(PauseMenu.Choice.Confirm), "Oculta, no responde.");

            menu.Choose(PauseMenu.Choice.Restart);
            Assert.IsTrue(menu.IsActive(PauseMenu.Choice.Resume), "Continuar no se va.");
            Assert.IsFalse(menu.IsActive(PauseMenu.Choice.Restart));
            Assert.IsFalse(menu.IsActive(PauseMenu.Choice.BackToMap));
            Assert.IsTrue(menu.IsActive(PauseMenu.Choice.Confirm));
            Assert.IsTrue(menu.IsActive(PauseMenu.Choice.Cancel));

            menu.Choose(PauseMenu.Choice.Cancel);
            Assert.IsTrue(menu.IsActive(PauseMenu.Choice.Restart));
            Assert.IsFalse(menu.IsActive(PauseMenu.Choice.Confirm));
        }

        // --- Pantallas de fin (13.11) ---

        EndLevelScreen EndScreen()
        {
            var go = new GameObject("EndLevelScreen");
            spawned.Add(go);
            var screen = go.AddComponent<EndLevelScreen>();
            screen.Configure(game, Font, Text, registry);
            return screen;
        }

        static IEnumerable<int> Ids(EndLevelScreen screen) => screen.Choices.Select(c => (int)c);

        [Test]
        public void TheVictoryShowsTheDataAndTheNextLevel()
        {
            game.Begin(levels[0]);
            EndLevelScreen end = EndScreen();
            Win();

            Assert.IsTrue(end.IsVisible);
            Assert.AreEqual(LevelOutcome.Victory, end.Shown);
            CollectionAssert.Contains(end.Lines, Text.VictoryTitle);
            CollectionAssert.Contains(end.Lines, Text.Nodes(3, 3));
            CollectionAssert.Contains(end.Lines, Text.Cascade(1));
            CollectionAssert.Contains(end.Lines, Text.NextLevel(2));
            CollectionAssert.Contains(end.Lines, Text.NextStart(Text.NodeEntry(NodeType.Star, 1, 1), 1));
            CollectionAssert.AreEquivalent(new[] { (int)EndLevelScreen.Choice.Next, (int)EndLevelScreen.Choice.Repeat }, Ids(end));

            end.Choose(EndLevelScreen.Choice.Next);
            Assert.AreSame(levels[1], GameManager.Requested);
        }

        // Maqueta «Victoria» (2026-10-20): la fila de abajo —curva y siguiente nivel— deja hasta el borde
        // inferior el mismo margen que hay arriba, 72 px, y la Estrella de la cabecera no se sale por arriba.
        [TestCase(0)]
        [TestCase(8)]
        public void TheVictoryLeavesTheSameMarginBelowAsAbove(int index)
        {
            game.Begin(levels[index]);
            EndLevelScreen end = EndScreen();
            Win();
            // Las tarjetas entran subiendo 32 px (Rise): se mide donde acaban, no a mitad de la entrada.
            end.GetComponentInChildren<UiMotion>(true).Finish();

            RectTransform[] all = end.GetComponentsInChildren<RectTransform>(true);
            float Bottom(string name)
            {
                RectTransform card = all.First(r => r.name == name);
                return -card.anchoredPosition.y + card.sizeDelta.y;
            }
            Assert.LessOrEqual(Bottom("NextLevel"), 1080f - 72f, "Tarjeta del siguiente nivel.");
            Assert.LessOrEqual(Bottom("PowerCurve"), 1080f - 72f, "Tarjeta de la curva.");
            Assert.GreaterOrEqual(-all.First(r => r.name == "Star").anchoredPosition.y, 0f, "La Estrella cabe en la pantalla.");
        }

        [Test]
        public void AfterTheNinthTheNineSystemsAreConquered()
        {
            game.Begin(levels[8]);
            EndLevelScreen end = EndScreen();
            Win();

            CollectionAssert.Contains(end.Lines, Text.AllConquered);
            CollectionAssert.AreEquivalent(new[] { (int)EndLevelScreen.Choice.BackToMap, (int)EndLevelScreen.Choice.Repeat }, Ids(end));
        }

        [Test]
        public void TheStalemateShowsTheFiveRoutes()
        {
            // Mapa 2: sin reconversión ni Gusano. Un Magnetar propio con 5 y nada que encender.
            levels[3] = Level(4, new NodeSpawnData(new Vector2(-5f, 0f), NodeType.Magnetar, Faction.Player, 5, 1),
                Star(Faction.Enemy, 30, 5f));
            game.Begin(levels[3]);
            EndLevelScreen end = EndScreen();
            Run(Tick);

            Assert.AreEqual(LevelOutcome.Stalemate, end.Shown);
            Assert.AreEqual(OutcomeRules.RouteCount, checker.Routes.Count);
            CollectionAssert.Contains(end.Lines, Text.Cause(LevelOutcome.Stalemate));
            CollectionAssert.Contains(end.Lines, Text.StalemateWhy(5));
            foreach (StarRoute route in checker.Routes)
            {
                CollectionAssert.Contains(end.Lines, Text.RouteName(route.Kind));
                CollectionAssert.Contains(end.Lines, Text.RouteWhy(route, 2));
            }
            Assert.AreEqual(StarRouteBlock.NotInMap, checker.Routes[(int)StarRouteKind.Reconvert].Block);
            Assert.AreEqual(StarRouteBlock.NotInMap, checker.Routes[(int)StarRouteKind.StealStar].Block);
            CollectionAssert.Contains(end.Lines, Text.Hint(2));
            CollectionAssert.AreEquivalent(new[] { (int)EndLevelScreen.Choice.Retry, (int)EndLevelScreen.Choice.BackToMap }, Ids(end));
        }

        [Test]
        public void RetryingStartsTheSameLevelAgain()
        {
            game.Begin(levels[0]);
            EndLevelScreen end = EndScreen();
            NodeOf(Faction.Player, NodeType.Star).Steal(Faction.Enemy);
            Run(Tick);

            CollectionAssert.Contains(end.Lines, Text.Cause(LevelOutcome.Annihilation));
            end.Choose(EndLevelScreen.Choice.Retry);
            CollectionAssert.AreEqual(new[] { GameManager.GameScene }, loadedScenes);
            Assert.AreSame(levels[0], GameManager.Requested);
        }
    }
}
