using System.IO;
using System.Linq;
using Nova.Core;
using Nova.Data;
using Nova.Enemy;
using Nova.Gameplay;
using Nova.Input;
using Nova.Presentation;
using Nova.Systems;
using Nova.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Nova.Editor
{
    // Escenas de prueba que se juegan a mano (desviación 144). Cada menú escribe la suya entera,
    // de forma idempotente. Todo lo que no es cableado —tipo, facción, Energía y posición de cada
    // nodo— queda serializado en la escena y se ajusta en el Inspector, no aquí.
    //
    // Fase 1 (T1-12): tres nodos para el criterio de salida, capturar un Asteroide, encenderlo
    // como Estrella y tomar un nodo enemigo. Fase 2 (T2-04): los mismos más un Magnetar enemigo en
    // medio del camino, para ver la defensa, y desde T2-06 un Magnetar neutral apartado.
    //
    // Las dos llevan el menú radial de encendido (T2-02) y el panel de clic derecho (T2-03), con la
    // reconversión desde T2-07. Encendido y reconversión ofrecen los tres tipos desde T3-02, que
    // trae el Púlsar con su carga de poderes.
    //
    // Fase 3 (T3-07): un Púlsar propio de nivel 3 frente a un objetivo de cada tipo, con la rueda de
    // poderes en el panel y el modo apuntado.
    //
    // Fase 4 (T4-04): partida simétrica contra EnemyController con el perfil del mapa 3.
    //
    // Fase 5 (T5-01): la misma partida, pero cargada por LevelLoader desde un LevelDefinition con las
    // mecánicas del mapa 2, más la plantilla de nodo que LevelLoader instancia.
    public static class TestScenes
    {
        const string ScenesFolder = "Assets/Scenes/Tests";
        const string BalancePath = "Assets/Data/BalanceConfig.asset";
        const string NodesFolder = "Assets/Data/Nodes";
        const string FontPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";
        const string TextPath = "Assets/Data/UiText.asset";
        const string ProfilesFolder = "Assets/Data/EnemyProfiles";
        const string ScoringPath = "Assets/Data/EnemyScoring.asset";

        const string NodesLayer = InputRouter.NodesLayer; // 18 #23

        // Radio del trigger de clic: el del círculo provisional de un nodo encendido, 64 px = 1 u
        // (desviación 138). Desde T6-01 es solo el de partida: NodeCollider2D lo cambia por la media
        // huella del sprite en cuanto el nodo se inicializa con su arte asignado.
        const float ColliderRadius = 0.5f;

        static readonly string[] IgnitionTargets = { "Star", "Pulsar", "Magnetar" };

        // T3-02: los tres poderes, en todo nodo que pueda ser Púlsar.
        const string PowersFolder = "Assets/Data/Powers";
        static readonly string[] Powers = { "AbsoluteZero", "Decay", "Wormhole" };

        const string PowerUpsFolder = "Assets/Data/PowerUps"; // T5-04

        // Los tres nodos de la fase 1, sobre el tablero de 15 x 8.4375 u (T.8). El Asteroide queda
        // a 2.12 u de la Estrella del Jugador, dentro de la regla de 2.25 u de 11.3. El Enemigo no
        // actúa hasta la fase 4, pero su Estrella genera.
        static readonly (string name, string type, Faction faction, int energy, int level, Vector2 position)[] Phase1Nodes =
        {
            ("Player Star", "Star", Faction.Player, 30, 0, new Vector2(-5f, 0f)),
            ("Neutral Asteroid", "Asteroid", Faction.Neutral, 10, 0, new Vector2(-3.5f, 1.5f)),
            ("Enemy Star", "Star", Faction.Enemy, 10, 0, new Vector2(5f, 0f)),
        };

        // Fase 2: un Magnetar enemigo de nivel 1 en el centro, sobre la recta entre las dos
        // Estrellas. Un protón que la recorra cruza su radio de 1.5 u entero (3 u, 1.2 s) y pierde
        // de media 3 de Carga (4.4).
        static readonly (string name, string type, Faction faction, int energy, int level, Vector2 position)[] Phase2Nodes =
        {
            ("Player Star", "Star", Faction.Player, 30, 0, new Vector2(-5f, 0f)),
            ("Neutral Asteroid", "Asteroid", Faction.Neutral, 10, 0, new Vector2(-3.5f, 1.5f)),
            ("Enemy Magnetar", "Magnetar", Faction.Enemy, 10, 0, new Vector2(0f, 0f)),
            ("Enemy Star", "Star", Faction.Enemy, 10, 0, new Vector2(5f, 0f)),
            // T2-06: neutral, abajo y fuera de los dos caminos de la Estrella del Jugador. El
            // protón que va hacia él pasa a 2.57 u del Magnetar enemigo: solo le dispara el neutral.
            ("Neutral Magnetar", "Magnetar", Faction.Neutral, 20, 0, new Vector2(0f, -3f)),
        };

        // Fase 3 (T3-07): los tres poderes en Play. Un Púlsar del Jugador de nivel 3 y lleno, que
        // desbloquea las tres ranuras y paga el Agujero de Gusano (60) sin esperar, con una Estrella
        // detrás para reforzarlo. Enfrente, un objetivo de cada tipo: Estrella, Púlsar y Magnetar del
        // Enemigo, y el Magnetar neutral; el Asteroide está para ver que nunca es objetivo (7.2.1).
        // Nivel 0 = el de partida del tipo (PlacedNode).
        static readonly (string name, string type, Faction faction, int energy, int level, Vector2 position)[] Phase3Nodes =
        {
            ("Player Pulsar", "Pulsar", Faction.Player, 100, 3, new Vector2(-5f, 0f)),
            ("Player Star", "Star", Faction.Player, 30, 0, new Vector2(-5f, -2.5f)),
            ("Neutral Asteroid", "Asteroid", Faction.Neutral, 10, 0, new Vector2(-3.5f, 1.5f)),
            ("Enemy Magnetar", "Magnetar", Faction.Enemy, 40, 0, new Vector2(1.5f, 0f)),
            ("Enemy Star", "Star", Faction.Enemy, 30, 0, new Vector2(5f, -1.5f)),
            ("Enemy Pulsar", "Pulsar", Faction.Enemy, 60, 2, new Vector2(5f, 2f)),
            ("Neutral Magnetar", "Magnetar", Faction.Neutral, 60, 0, new Vector2(0f, -3f)),
        };

        // Fase 4 (T4-04): una partida simétrica contra el algoritmo con el perfil del mapa 3, para el
        // criterio de la fase (el algoritmo del mapa 3 vence a un jugador descuidado). Una Estrella por
        // bando, cuatro Asteroides neutrales a repartir y un Magnetar neutral abajo, apartado del centro.
        static readonly (string name, string type, Faction faction, int energy, int level, Vector2 position)[] Phase4Nodes =
        {
            ("Player Star", "Star", Faction.Player, 20, 0, new Vector2(-5.5f, 0f)),
            ("Enemy Star", "Star", Faction.Enemy, 20, 0, new Vector2(5.5f, 0f)),
            ("Neutral Asteroid NW", "Asteroid", Faction.Neutral, 10, 0, new Vector2(-3f, 2f)),
            ("Neutral Asteroid SW", "Asteroid", Faction.Neutral, 10, 0, new Vector2(-3f, -2f)),
            ("Neutral Asteroid NE", "Asteroid", Faction.Neutral, 10, 0, new Vector2(3f, 2f)),
            ("Neutral Asteroid SE", "Asteroid", Faction.Neutral, 10, 0, new Vector2(3f, -2f)),
            ("Neutral Magnetar", "Magnetar", Faction.Neutral, 40, 0, new Vector2(0f, -3f)),
        };

        [MenuItem("Nova/Fase 1/Generar escena de prueba")]
        public static void GeneratePhase1() => Generate("Fase1", Phase1Nodes);

        [MenuItem("Nova/Fase 2/Generar escena de prueba")]
        public static void GeneratePhase2() => Generate("Fase2", Phase2Nodes);

        [MenuItem("Nova/Fase 3/Generar escena de prueba")]
        public static void GeneratePhase3() => Generate("Fase3", Phase3Nodes);

        [MenuItem("Nova/Fase 4/Generar escena de prueba")]
        public static void GeneratePhase4() => Generate("Fase4", Phase4Nodes, "Map3");

        // T5-01: la plantilla de nodo de LevelLoader. Es el nodo de las escenas de prueba sin PlacedNode:
        // LevelLoader lo inicializa y le da las mecánicas del nivel.
        const string PrefabsFolder = "Assets/Prefabs";
        const string NodePrefabPath = PrefabsFolder + "/Node.prefab";

        [MenuItem("Nova/Generar plantilla de nodo")]
        public static void GenerateNodePrefab()
        {
            if (WriteNodePrefab() != null)
                Debug.Log($"[Nova] Plantilla de nodo generada en {NodePrefabPath}.");
        }

        static NodeController WriteNodePrefab()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (balance == null || font == null)
            {
                Debug.LogError($"[Nova] Faltan {BalancePath} o {FontPath}.");
                return null;
            }
            if (LayerMask.NameToLayer(NodesLayer) < 0)
            {
                Debug.LogError($"[Nova] No existe la capa «{NodesLayer}»; recarga el editor para que ProjectLayers la cree.");
                return null;
            }

            GameObject go = BuildNode("Node", Vector2.zero, balance, font,
                System.Array.ConvertAll(IgnitionTargets, Definition), System.Array.ConvertAll(Powers, Power));
            Directory.CreateDirectory(PrefabsFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, NodePrefabPath);
            Object.DestroyImmediate(go);
            return prefab != null ? prefab.GetComponent<NodeController>() : null;
        }

        // Fase 5 (T5-01): la partida de la fase 4 cargada por LevelLoader desde un LevelDefinition, con
        // las mecánicas del mapa 2 (11.1): se enciende el Púlsar, se cargan Cero Absoluto y
        // Decaimiento, y ni el Agujero de Gusano ni la reconversión. El Púlsar del Jugador, de nivel 3
        // y lleno, enseña la ranura del Gusano en gris; el del Enemigo carga los dos poderes del mapa.
        // El nivel es de prueba y vive junto a la escena, no en Assets/Data/Levels/, que es de los
        // nueve de T5-02.
        const string Phase5LevelPath = ScenesFolder + "/Fase5Level.asset";

        static readonly (string type, Faction faction, int energy, int level, Vector2 position)[] Phase5Nodes =
        {
            ("Star", Faction.Player, 25, 0, new Vector2(-5.5f, 0f)),
            ("Pulsar", Faction.Player, 100, 3, new Vector2(-5.5f, 2.5f)),
            ("Star", Faction.Enemy, 25, 0, new Vector2(5.5f, 0f)),
            ("Pulsar", Faction.Enemy, 50, 0, new Vector2(5.5f, -2.5f)),
            ("Asteroid", Faction.Neutral, 10, 0, new Vector2(-3f, 2f)),
            ("Asteroid", Faction.Neutral, 10, 0, new Vector2(-3f, -2f)),
            ("Asteroid", Faction.Neutral, 10, 0, new Vector2(3f, 2f)),
            ("Asteroid", Faction.Neutral, 10, 0, new Vector2(3f, -2f)),
            ("Magnetar", Faction.Neutral, 100, 2, new Vector2(0f, 0f)),
        };

        [MenuItem("Nova/Fase 5/Generar escena de prueba")]
        public static void GeneratePhase5()
        {
            string scenePath = $"{ScenesFolder}/Fase5.unity";

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var text = AssetDatabase.LoadAssetAtPath<UiText>(TextPath);
            var profile = AssetDatabase.LoadAssetAtPath<EnemyProfile>($"{ProfilesFolder}/Map2.asset");
            if (balance == null || font == null || text == null || profile == null)
            {
                Debug.LogError($"[Nova] Faltan {BalancePath}, {FontPath}, {TextPath} o {ProfilesFolder}/Map2.asset.");
                return;
            }

            NodeController template = WriteNodePrefab();
            if (template == null)
                return;

            Directory.CreateDirectory(ScenesFolder);
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Phase5LevelPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(level, Phase5LevelPath);
            }
            var spawns = new System.Collections.Generic.List<NodeSpawnData>();
            foreach (var spec in Phase5Nodes)
                spawns.Add(new NodeSpawnData(spec.position, (NodeType)System.Enum.Parse(typeof(NodeType), spec.type), spec.faction, spec.energy, spec.level));
            level.Configure(4, profile, 150f, spawns,
                new[] { NodeType.Star, NodeType.Pulsar, NodeType.Magnetar },
                new[] { PowerType.AbsoluteZero, PowerType.Decay }, false, LevelAssets.Drops(2)); // T5-04
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera();
            NodeRegistry registry = CreateSystems(balance, font, text, camera, out ProtonRegistry protons, out PowerBeamSystem beams);
            registry.gameObject.AddComponent<WinConditionChecker>().Configure(balance, registry, protons); // T5-03

            // T5-04: power-ups del cielo con el calendario del mapa 2.
            var buffs = registry.gameObject.AddComponent<FactionBuffs>();
            var powerUps = new[] { "CosmicRay", "HydrogenCloud" };
            PowerUpDefinition[] powerUpDefinitions = System.Array.ConvertAll(powerUps,
                n => AssetDatabase.LoadAssetAtPath<PowerUpDefinition>($"{PowerUpsFolder}/{n}.asset"));

            PowerDefinition[] powers = System.Array.ConvertAll(Powers, Power);
            var loaderObject = new GameObject("Level");
            var loader = loaderObject.AddComponent<LevelLoader>();
            loader.Configure(balance, template,
                System.Array.ConvertAll(new[] { "Asteroid", "Star", "Pulsar", "Magnetar" }, Definition), powers, registry, level, buffs);
            EditorUtility.SetDirty(loaderObject);

            var spawner = registry.gameObject.AddComponent<PowerUpSpawner>();
            spawner.Configure(balance, powerUpDefinitions, buffs, registry, 0, loader);
            registry.gameObject.AddComponent<ProvisionalPowerUpView>().Configure(spawner);
            EditorUtility.SetDirty(registry.gameObject);

            CreateEnemy(level.EnemyProfile, registry, protons, balance, powers);

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Nova] Escena de prueba generada en {scenePath}, con el nivel {Phase5LevelPath}. Dale a Play.");
        }

        // T5-05: la escena Game de 15.2, la única que juega los nueve niveles. GameManager carga el
        // nivel que le pidan —el 1 si se le da a Play aquí— y lleva la pausa, el fin de nivel y el
        // reinicio, que vuelve a cargar esta escena. Es la primera escena de Build Settings que no es
        // de prueba; la sobrescribe entera, conservando su GUID.
        const string GameScenePath = "Assets/Scenes/Game.unity";
        const string LevelsFolder = "Assets/Data/Levels";

        [MenuItem("Nova/Fase 5/Generar escena Game")]
        public static void GenerateGame()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var text = AssetDatabase.LoadAssetAtPath<UiText>(TextPath);
            var scoring = AssetDatabase.LoadAssetAtPath<EnemyScoring>(ScoringPath);
            var levels = new LevelDefinition[9];
            for (int i = 0; i < levels.Length; i++)
                levels[i] = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{LevelsFolder}/Level{i + 1}.asset");
            if (balance == null || font == null || text == null || scoring == null || System.Array.IndexOf(levels, null) >= 0)
            {
                Debug.LogError($"[Nova] Faltan {BalancePath}, {FontPath}, {TextPath}, {ScoringPath} o alguno de los nueve niveles de {LevelsFolder} (Nova → Fase 5 → Generar los 9 niveles).");
                return;
            }

            NodeController template = WriteNodePrefab();
            if (template == null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = CreateCamera();

            var flow = new GameObject("Game");
            var game = flow.AddComponent<GameManager>();
            NodeRegistry registry = CreateSystems(balance, font, text, camera, out ProtonRegistry protons, out PowerBeamSystem beams, game);

            var buffs = registry.gameObject.AddComponent<FactionBuffs>();
            PowerDefinition[] powers = System.Array.ConvertAll(Powers, Power);
            var loaderObject = new GameObject("Level");
            var loader = loaderObject.AddComponent<LevelLoader>();
            loader.Configure(balance, template,
                System.Array.ConvertAll(new[] { "Asteroid", "Star", "Pulsar", "Magnetar" }, Definition), powers, registry, null, buffs);

            var checker = registry.gameObject.AddComponent<WinConditionChecker>();
            checker.Configure(balance, registry, protons, loader);

            // T5-05b: el fondo con movimiento del nivel que cargue LevelLoader, con su ovni y su grava.
            var sky = new GameObject("Background");
            sky.AddComponent<BackgroundView>().Configure(null, loader);
            sky.AddComponent<BackgroundFlyby>();
            if (levels[0].Background == null)
                Debug.LogWarning("[Nova] Los niveles no tienen fondo: genera los fondos con Nova → Arte → Generar fondos.");

            var spawner = registry.gameObject.AddComponent<PowerUpSpawner>();
            spawner.Configure(balance, System.Array.ConvertAll(new[] { "CosmicRay", "HydrogenCloud" },
                n => AssetDatabase.LoadAssetAtPath<PowerUpDefinition>($"{PowerUpsFolder}/{n}.asset")), buffs, registry, 0, loader);
            registry.gameObject.AddComponent<ProvisionalPowerUpView>().Configure(spawner);

            // El Enemigo toma el perfil del nivel que cargue GameManager (12.5).
            var enemy = new GameObject("Enemy");
            var enemyIssuer = enemy.AddComponent<EnemyCommandIssuer>();
            enemyIssuer.Configure(registry.GetComponent<ProtonLauncher>());
            enemy.AddComponent<EnemyController>().Configure(levels[0].EnemyProfile, scoring, balance, enemyIssuer, powers, registry, 0, protons, loader);

            var recorder = flow.AddComponent<MatchRecorder>();
            recorder.Configure(registry, checker);
            game.Configure(loader, checker, recorder, levels, levels[0]);
            // T5-06: ganar desbloquea el siguiente nivel.
            flow.AddComponent<ProgressSaver>().Configure(game);
            // Pausa y fin, fieles a la maqueta (T.2 regla 8): uGUI, con su EventSystem y los sprites de A.2.
            flow.AddComponent<PauseMenu>().Configure(game, font, text);
            flow.AddComponent<EndLevelScreen>().Configure(game, font, text, registry, NodeSprites());
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            EditorUtility.SetDirty(flow);
            EditorUtility.SetDirty(registry.gameObject);
            EditorUtility.SetDirty(loaderObject);
            EditorUtility.SetDirty(enemy);
            EditorUtility.SetDirty(sky);
            EditorSceneManager.SaveScene(scene, GameScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Nova] Escena Game generada en {GameScenePath}. Play empieza en el menú; para jugar aquí un nivel, apaga Nova → Empezar Play desde el menú y elige Start Level en GameManager.");
        }

        // T5-06: Boot, el menú principal (13.8) y la carta estelar (13.9). Sobrescribe las tres escenas
        // enteras, conservando su GUID. Los menús enseñan el fondo con movimiento de su mapa debajo del
        // lienzo: el del mapa 1 entero en el menú, y en la carta, una franja de cada mapa bajo su tercio.
        const string BootScenePath = "Assets/Scenes/Boot.unity";
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string LevelSelectScenePath = "Assets/Scenes/LevelSelect.unity";
        const string LinkSheetPath = "Assets/Sprites/world/link/world_link_sheet.png";

        // La maqueta pone las franjas de la carta en (640 · i, 96) con 640 x 624 px de lienzo: a 1080p,
        // 320 x 312 píxeles del arte. La ventana de cada mapa, en píxeles de su fondo.
        static readonly RectInt[] ChartWindows =
        {
            new RectInt(18, 122, 320, 312),
            new RectInt(328, 122, 320, 312),
            new RectInt(638, 122, 320, 312),
        };

        [MenuItem("Nova/Fase 5/Generar escenas de menú")]
        public static void GenerateMenus()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var text = AssetDatabase.LoadAssetAtPath<UiText>(TextPath);
            var levels = new LevelDefinition[9];
            for (int i = 0; i < levels.Length; i++)
                levels[i] = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"{LevelsFolder}/Level{i + 1}.asset");
            if (font == null || text == null || System.Array.IndexOf(levels, null) >= 0)
            {
                Debug.LogError($"[Nova] Faltan {FontPath}, {TextPath} o alguno de los nueve niveles de {LevelsFolder} (Nova → Fase 5 → Generar los 9 niveles).");
                return;
            }
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(LinkSheetPath);
            if (sheet == null)
                Debug.LogWarning($"[Nova] No existe {LinkSheetPath}: sincroniza con Nova → Arte → Sincronizar sprites desde art. Las líneas de plasma saldrán vacías.");
            if (levels[0].Background == null)
                Debug.LogWarning("[Nova] Los niveles no tienen fondo: genera los fondos con Nova → Arte → Generar fondos.");
            Sprite[] sprites = NodeSprites();

            // Boot: solo pasa al menú.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            new GameObject("Boot").AddComponent<SceneBoot>();
            EditorSceneManager.SaveScene(scene, BootScenePath);

            // Menú principal: el fondo del mapa 1 entero, con su ovni.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            var sky = new GameObject("Background");
            sky.AddComponent<BackgroundView>().Configure(levels[0].Background);
            sky.AddComponent<BackgroundFlyby>();
            var menu = new GameObject("MainMenu");
            menu.AddComponent<MainMenuScreen>().Configure(font, text, levels, sprites, sheet);
            CreateMusic();
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            VerifyMusic(MainMenuScenePath);

            // Carta estelar: una franja de cada mapa debajo de su tercio de pantalla.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            for (int map = 0; map < 3; map++)
            {
                var band = new GameObject($"Background{map + 1}");
                band.transform.position = ChartCenter(map);
                band.AddComponent<BackgroundView>().Configure(levels[map * LevelDefinition.LevelsPerMap].Background, null, ChartWindows[map]);
                band.AddComponent<BackgroundFlyby>();
            }
            var chart = new GameObject("LevelSelect");
            chart.AddComponent<LevelSelectScreen>().Configure(font, text, levels, sprites, sheet);
            CreateMusic();
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, LevelSelectScenePath);
            VerifyMusic(LevelSelectScenePath);

            AssetDatabase.Refresh();
            Debug.Log("[Nova] Escenas Boot, MainMenu y LevelSelect generadas. Para el progreso de 13.9, ProgressSaver guarda el máximo en PlayerPrefs (Nova.MaxUnlocked).");
        }

        // T6-13: la música de los menús va en las dos pantallas para que suene se entre por la que se entre; la
        // primera instancia sobrevive a los cambios de escena y la otra se destruye al nacer.
        //
        // MusicSettings se carga aquí, escena a escena, y no al principio con el resto: NewScene descarga los
        // assets que ninguna escena abierta usa, y MusicSettings, que aún no lo usaba ninguna, llegaba descargado.
        const string MusicSettingsPath = AudioAssets.MusicSettingsPath;

        static void CreateMusic()
        {
            var music = AssetDatabase.LoadAssetAtPath<MusicSettings>(MusicSettingsPath);
            if (music == null)
            {
                Debug.LogWarning($"[Nova] No existe {MusicSettingsPath}: importa la música con Nova → Audio → Importar audio y crear el mezclador. {SceneManager.GetActiveScene().name} sale sin música.");
                return;
            }
            new GameObject("MusicDirector").AddComponent<MusicDirector>().Configure(music);
        }

        // Tras guardar, comprueba en el archivo que el MusicDirector está dentro.
        static void VerifyMusic(string scenePath)
        {
            string guid = AssetDatabase.FindAssets("MusicDirector t:MonoScript")
                .FirstOrDefault(g => AssetDatabase.GUIDToAssetPath(g).EndsWith("/MusicDirector.cs"));
            if (guid == null || !File.ReadAllText(scenePath).Contains(guid))
                Debug.LogError($"[Nova] {scenePath}: el MusicDirector no se ha guardado y los menús saldrán sin música.");
        }

        // El centro de la franja de un mapa en el mundo: el lienzo de 1920 x 1080 es el tablero de
        // 15 x 8.4375 u, a 128 px de lienzo por unidad.
        static Vector3 ChartCenter(int map)
        {
            const float CanvasPerUnit = 128f;
            float x = 320f + 640f * map;
            float y = 96f + 312f;
            return new Vector3(x / CanvasPerUnit - 7.5f, 4.21875f - y / CanvasPerUnit, 0f);
        }

        static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        // Los sprites de nodo que ArtBridge copió a Assets/Sprites/nodes (T0-05). Sin sincronizar, la
        // lista sale vacía y las pantallas de fin dejan el hueco de cada icono.
        const string NodeSpritesFolder = "Assets/Sprites/nodes";

        static Sprite[] NodeSprites()
        {
            var sprites = new System.Collections.Generic.List<Sprite>();
            if (!AssetDatabase.IsValidFolder(NodeSpritesFolder))
            {
                Debug.LogWarning($"[Nova] No existe {NodeSpritesFolder}: sincroniza con Nova → Arte → Sincronizar sprites desde art.");
                return sprites.ToArray();
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { NodeSpritesFolder }))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                if (sprite != null)
                    sprites.Add(sprite);
            }
            return sprites.ToArray();
        }

        static void Generate(string sceneName, (string name, string type, Faction faction, int energy, int level, Vector2 position)[] nodes,
            string enemyProfile = null)
        {
            string scenePath = $"{ScenesFolder}/{sceneName}.unity";

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var text = AssetDatabase.LoadAssetAtPath<UiText>(TextPath);
            if (balance == null || font == null || text == null)
            {
                Debug.LogError($"[Nova] Faltan {BalancePath}, {FontPath} o {TextPath}.");
                return;
            }
            if (LayerMask.NameToLayer(NodesLayer) < 0)
            {
                Debug.LogError($"[Nova] No existe la capa «{NodesLayer}»; recarga el editor para que ProjectLayers la cree.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera();
            ProtonRegistry protons;
            PowerBeamSystem beams;
            NodeRegistry registry = CreateSystems(balance, font, text, camera, out protons, out beams);

            NodeDefinition[] ignitionTargets = System.Array.ConvertAll(IgnitionTargets, Definition);
            PowerDefinition[] powers = System.Array.ConvertAll(Powers, Power);
            foreach (var spec in nodes)
                CreateNode(spec.name, Definition(spec.type), spec.faction, spec.energy, spec.level, spec.position, balance, font, ignitionTargets, powers, protons, beams);

            if (enemyProfile != null)
                CreateEnemy(enemyProfile, registry, protons, balance, powers);

            Directory.CreateDirectory(ScenesFolder);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Nova] Escena de prueba generada en {scenePath}. Dale a Play.");
        }

        // T4-04: el Enemigo con el perfil de un mapa. Usa el mismo ProtonLauncher que el Jugador (3.1).
        static void CreateEnemy(string profileName, NodeRegistry registry, ProtonRegistry protons, BalanceConfig balance, PowerDefinition[] powers)
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyProfile>($"{ProfilesFolder}/{profileName}.asset");
            if (profile == null)
            {
                Debug.LogError($"[Nova] Falta {ProfilesFolder}/{profileName}.asset.");
                return;
            }
            CreateEnemy(profile, registry, protons, balance, powers);
        }

        static void CreateEnemy(EnemyProfile profile, NodeRegistry registry, ProtonRegistry protons, BalanceConfig balance, PowerDefinition[] powers)
        {
            var scoring = AssetDatabase.LoadAssetAtPath<EnemyScoring>(ScoringPath);
            if (profile == null || scoring == null)
            {
                Debug.LogError($"[Nova] Falta el perfil del Enemigo o {ScoringPath}.");
                return;
            }

            var go = new GameObject("Enemy");
            var issuer = go.AddComponent<EnemyCommandIssuer>();
            issuer.Configure(registry.GetComponent<ProtonLauncher>());
            go.AddComponent<EnemyController>().Configure(profile, scoring, balance, issuer, powers, registry, 0, protons); // T4-05: ve las amenazas
            EditorUtility.SetDirty(go);
        }

        static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var camera = go.AddComponent<Camera>();
            go.AddComponent<UniversalAdditionalCameraData>();
            go.AddComponent<AudioListener>(); // T6-13: sin él no se oye nada, tampoco el fundido al entrar en Game
            ProjectSetup.ConfigureCamera(camera);
            return camera;
        }

        static NodeRegistry CreateSystems(BalanceConfig balance, TMP_FontAsset font, UiText text, Camera camera, out ProtonRegistry protons, out PowerBeamSystem beams,
            GameManager game = null)
        {
            var go = new GameObject("Systems");

            var registry = go.AddComponent<NodeRegistry>();
            protons = go.AddComponent<ProtonRegistry>();
            beams = go.AddComponent<PowerBeamSystem>(); // T3-03
            var pool = go.AddComponent<ProtonPool>();
            var launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(balance, pool, protons);

            var selection = go.AddComponent<SelectionManager>();
            selection.Configure(registry);
            var issuer = go.AddComponent<PlayerCommandIssuer>();
            issuer.Configure(launcher);
            var menu = go.AddComponent<IgnitionMenuState>();
            var panel = go.AddComponent<InfoPanelState>();
            var aim = go.AddComponent<PowerAimState>(); // T3-07
            var sendLine = go.AddComponent<SendLineState>(); // T2-09
            sendLine.Configure(selection, menu, aim);
            var lasso = go.AddComponent<LassoState>(); // T6-06
            // T6-08: el HUD con su arte. Sin HudArt asignado no hay HUD, y el botón de pausa no existe.
            var hudArt = AssetDatabase.LoadAssetAtPath<HudArt>(NodeArtAssets.HudArtPath);
            var hud = go.AddComponent<HudState>();
            hud.Configure(hudArt);
            var router = go.AddComponent<InputRouter>();
            router.Configure(balance, selection, issuer, menu, panel, camera, sendLine, aim, game, lasso, hud); // T5-05: Esc pausa

            // T6-06: la entrada con su arte. Sin InputArt asignado, las vistas provisionales siguen al mando y el
            // lazo no se dibuja.
            var input = AssetDatabase.LoadAssetAtPath<InputArt>(NodeArtAssets.InputArtPath);
            go.AddComponent<ProvisionalSelectionView>().Configure(selection);
            go.AddComponent<SelectionRingView>().Configure(input, selection);
            go.AddComponent<LassoView>().Configure(input, lasso);
            // T6-05: los anillos de radio. Sin MagnetarArt asignado, el anillo sigue siendo el provisional.
            go.AddComponent<MagnetarRadiusView>().Configure(
                AssetDatabase.LoadAssetAtPath<MagnetarArt>(NodeArtAssets.MagnetarArtPath), registry);
            go.AddComponent<ProvisionalSendLineView>().Configure(sendLine);
            go.AddComponent<SendLineVisual>().Configure(input, sendLine); // T6-06
            go.AddComponent<ProvisionalProtonView>().Configure(protons, font);
            // T6-04: el protón con su arte. Sin ProtonArt asignado, el disco provisional sigue al mando.
            var protonArt = AssetDatabase.LoadAssetAtPath<ProtonArt>(NodeArtAssets.ProtonArtPath);
            var protonVisual = go.AddComponent<ProtonVisual>();
            protonVisual.Configure(protonArt, protons);
            // T6-07: los efectos puntuales de A.5. Sin VfxArt asignado no se dibuja ninguno. El fin de nivel, que
            // se añade después en las escenas que lo tienen, lo encuentra en este mismo objeto.
            go.AddComponent<VFXPool>().Configure(
                AssetDatabase.LoadAssetAtPath<VfxArt>(NodeArtAssets.VfxArtPath), input, protonArt, protonVisual,
                registry, protons, beams);
            go.AddComponent<ProvisionalBeamView>().Configure(beams);
            // T6-11: el haz con su arte. Sin PowerBeamArt asignado, el trazo provisional sigue al mando.
            go.AddComponent<PowerBeamVisual>().Configure(
                AssetDatabase.LoadAssetAtPath<PowerBeamArt>(NodeArtAssets.PowerBeamArtPath), beams);
            // T6-14: los sonidos de los poderes. Se carga aquí, ya con la escena nueva abierta: NewScene descarga
            // los assets que no usa ninguna escena (ver CreateMusic).
            var sounds = AssetDatabase.LoadAssetAtPath<SoundBank>(AudioAssets.SoundBankPath);
            if (sounds == null)
                Debug.LogWarning($"[Nova] No existe {AudioAssets.SoundBankPath}: importa el audio con Nova → Audio → Importar audio y crear el mezclador. La partida saldrá sin sonido.");
            go.AddComponent<AudioDirector>().Configure(sounds, beams);
            go.AddComponent<ProvisionalAimView>().Configure(aim, registry); // T3-07
            go.AddComponent<AimDimView>().Configure(aim, registry); // T6-06
            go.AddComponent<AimReticleView>().Configure(input, aim, sendLine, registry); // T6-06
            go.AddComponent<IgnitionMenu>().Configure(menu, font, text);
            go.AddComponent<NodeInfoPanel>().Configure(panel, font, text);
            go.AddComponent<PulsarPowerWheel>().Configure(panel, aim, font, text); // T3-07
            // T6-08: la barra de poder en todas las escenas; la de arriba, solo con GameManager, que da el nivel y el tiempo.
            go.AddComponent<PowerBarUI>().Configure(hudArt, registry);
            if (game != null)
                go.AddComponent<HudTopBar>().Configure(hudArt, hud, game, font, text);

            EditorUtility.SetDirty(go);
            return registry;
        }

        static void CreateNode(string name, NodeDefinition definition, Faction faction, int energy, int level, Vector2 position,
            BalanceConfig balance, TMP_FontAsset font, NodeDefinition[] ignitionTargets, PowerDefinition[] powers, ProtonRegistry protons, PowerBeamSystem beams)
        {
            GameObject go = BuildNode(name, position, balance, font, ignitionTargets, powers, protons, beams);
            go.AddComponent<PlacedNode>().Configure(definition, faction, energy, level);
            EditorUtility.SetDirty(go);
        }

        // Un nodo con todos sus componentes y sin inicializar: lo que comparten las escenas de prueba
        // y la plantilla de LevelLoader (T5-01). Sin registros, sus componentes usan los Instance.
        static GameObject BuildNode(string name, Vector2 position, BalanceConfig balance, TMP_FontAsset font,
            NodeDefinition[] ignitionTargets, PowerDefinition[] powers, ProtonRegistry protons = null, PowerBeamSystem beams = null)
        {
            var go = new GameObject(name) { layer = LayerMask.NameToLayer(NodesLayer) };
            go.transform.position = position;

            go.AddComponent<NodeController>();
            go.AddComponent<NodeIgnition>().Configure(balance, ignitionTargets);
            go.AddComponent<NodeConverter>().Configure(balance, ignitionTargets); // T2-07: mismos tipos que el encendido
            go.AddComponent<StarGenerator>(); // desviación 143: en todo nodo que pueda ser Estrella
            go.AddComponent<MagnetarTurret>().Configure(balance, protons); // y en todo el que pueda ser Magnetar
            go.AddComponent<PulsarCaster>().Configure(balance, powers, beams); // T3-02: y en todo el que pueda ser Púlsar
            go.AddComponent<NodeScare>().Configure(balance, protons); // T2-08: todo nodo se asusta (14.2)
            go.AddComponent<NodeFreeze>().Configure(balance); // T3-04: todo nodo encendido se puede congelar (7.3)
            go.AddComponent<NodeDecay>().Configure(balance); // T3-05: y se puede infectar (7.4)

            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = ColliderRadius;
            go.AddComponent<NodeCollider2D>(); // T6-01: el radio pasa a ser la huella del sprite (18 #23)

            go.AddComponent<NodeVisual>(); // T6-01: el sprite de tipo, nivel y facción
            var view = go.AddComponent<ProvisionalNodeView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("font").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<ProvisionalMagnetarView>();
            // T6-05: el arco de cada disparo. Sin MagnetarArt asignado, el trazo sigue siendo el provisional.
            // T.7 #56: con ProtonArt, el arco acaba en el borde de la bolsa del protón.
            go.AddComponent<MagnetarShotArc>().Configure(
                AssetDatabase.LoadAssetAtPath<MagnetarArt>(NodeArtAssets.MagnetarArtPath),
                AssetDatabase.LoadAssetAtPath<ProtonArt>(NodeArtAssets.ProtonArtPath));
            go.AddComponent<ProvisionalPulsarView>();
            // T6-02: los estados de 14.1 y el idle. Sin NodeStateArt asignado, los estados siguen provisionales.
            go.AddComponent<NodeAnimator>().Configure(balance,
                AssetDatabase.LoadAssetAtPath<NodeStateArt>(NodeArtAssets.StateArtPath));
            return go;
        }

        static PowerDefinition Power(string name)
        {
            var power = AssetDatabase.LoadAssetAtPath<PowerDefinition>($"{PowersFolder}/{name}.asset");
            if (power == null)
                throw new FileNotFoundException($"Falta {PowersFolder}/{name}.asset");
            return power;
        }

        static NodeDefinition Definition(string type)
        {
            var definition = AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodesFolder}/{type}.asset");
            if (definition == null)
                throw new FileNotFoundException($"Falta {NodesFolder}/{type}.asset");
            return definition;
        }
    }
}
