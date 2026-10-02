using System.IO;
using Nova.Core;
using Nova.Data;
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
    // medio del camino, para ver la defensa.
    //
    // Las dos llevan el menú radial de encendido (T2-02) y el panel de clic derecho (T2-03). El
    // menú ofrece los tipos que ya existen: Estrella y, desde T2-04, Magnetar; el Púlsar llega con
    // T3-02 y hasta entonces sale deshabilitado. Al cerrar esa tarea se añade a IgnitionTargets.
    public static class TestScenes
    {
        const string ScenesFolder = "Assets/Scenes/Tests";
        const string BalancePath = "Assets/Data/BalanceConfig.asset";
        const string NodesFolder = "Assets/Data/Nodes";
        const string FontPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";
        const string TextPath = "Assets/Data/UiText.asset";

        const string NodesLayer = InputRouter.NodesLayer; // 18 #23

        // Radio del trigger de clic: el del círculo provisional de un nodo encendido, 64 px = 1 u
        // (desviación 138). El Asteroide provisional es más pequeño, pero conserva el mismo blanco.
        const float ColliderRadius = 0.5f;

        static readonly string[] IgnitionTargets = { "Star", "Magnetar" };

        // Los tres nodos de la fase 1, sobre el tablero de 15 x 8.4375 u (T.8). El Asteroide queda
        // a 2.12 u de la Estrella del Jugador, dentro de la regla de 2.25 u de 11.3. El Enemigo no
        // actúa hasta la fase 4, pero su Estrella genera.
        static readonly (string name, string type, Faction faction, int energy, Vector2 position)[] Phase1Nodes =
        {
            ("Player Star", "Star", Faction.Player, 30, new Vector2(-5f, 0f)),
            ("Neutral Asteroid", "Asteroid", Faction.Neutral, 10, new Vector2(-3.5f, 1.5f)),
            ("Enemy Star", "Star", Faction.Enemy, 10, new Vector2(5f, 0f)),
        };

        // Fase 2: un Magnetar enemigo de nivel 1 en el centro, sobre la recta entre las dos
        // Estrellas. Un protón que la recorra cruza su radio de 1.5 u entero (3 u, 1.2 s) y pierde
        // de media 3 de Carga (4.4).
        static readonly (string name, string type, Faction faction, int energy, Vector2 position)[] Phase2Nodes =
        {
            ("Player Star", "Star", Faction.Player, 30, new Vector2(-5f, 0f)),
            ("Neutral Asteroid", "Asteroid", Faction.Neutral, 10, new Vector2(-3.5f, 1.5f)),
            ("Enemy Magnetar", "Magnetar", Faction.Enemy, 10, new Vector2(0f, 0f)),
            ("Enemy Star", "Star", Faction.Enemy, 10, new Vector2(5f, 0f)),
        };

        [MenuItem("Nova/Fase 1/Generar escena de prueba")]
        public static void GeneratePhase1() => Generate("Fase1", Phase1Nodes);

        [MenuItem("Nova/Fase 2/Generar escena de prueba")]
        public static void GeneratePhase2() => Generate("Fase2", Phase2Nodes);

        static void Generate(string sceneName, (string name, string type, Faction faction, int energy, Vector2 position)[] nodes)
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
            NodeRegistry registry = CreateSystems(balance, font, text, camera, out protons);

            NodeDefinition[] ignitionTargets = System.Array.ConvertAll(IgnitionTargets, Definition);
            foreach (var spec in nodes)
                CreateNode(spec.name, Definition(spec.type), spec.faction, spec.energy, spec.position, balance, font, ignitionTargets, protons);

            Directory.CreateDirectory(ScenesFolder);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Nova] Escena de prueba generada en {scenePath}. Dale a Play.");
        }

        static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var camera = go.AddComponent<Camera>();
            go.AddComponent<UniversalAdditionalCameraData>();
            ProjectSetup.ConfigureCamera(camera);
            return camera;
        }

        static NodeRegistry CreateSystems(BalanceConfig balance, TMP_FontAsset font, UiText text, Camera camera, out ProtonRegistry protons)
        {
            var go = new GameObject("Systems");

            var registry = go.AddComponent<NodeRegistry>();
            protons = go.AddComponent<ProtonRegistry>();
            var pool = go.AddComponent<ProtonPool>();
            var launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(balance, pool, protons);

            var selection = go.AddComponent<SelectionManager>();
            selection.Configure(registry);
            var issuer = go.AddComponent<PlayerCommandIssuer>();
            issuer.Configure(launcher);
            var menu = go.AddComponent<IgnitionMenuState>();
            var panel = go.AddComponent<InfoPanelState>();
            var router = go.AddComponent<InputRouter>();
            router.Configure(balance, selection, issuer, menu, panel, camera);

            go.AddComponent<ProvisionalSelectionView>().Configure(selection);
            go.AddComponent<ProvisionalProtonView>().Configure(protons, font);
            go.AddComponent<IgnitionMenu>().Configure(menu, font, text);
            go.AddComponent<NodeInfoPanel>().Configure(panel, font, text);

            EditorUtility.SetDirty(go);
            return registry;
        }

        static void CreateNode(string name, NodeDefinition definition, Faction faction, int energy, Vector2 position,
            BalanceConfig balance, TMP_FontAsset font, NodeDefinition[] ignitionTargets, ProtonRegistry protons)
        {
            var go = new GameObject(name) { layer = LayerMask.NameToLayer(NodesLayer) };
            go.transform.position = position;

            go.AddComponent<NodeController>();
            go.AddComponent<PlacedNode>().Configure(definition, faction, energy);
            go.AddComponent<NodeIgnition>().Configure(balance, ignitionTargets);
            go.AddComponent<StarGenerator>(); // desviación 143: en todo nodo que pueda ser Estrella
            go.AddComponent<MagnetarTurret>().Configure(balance, protons); // y en todo el que pueda ser Magnetar

            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = ColliderRadius;

            var view = go.AddComponent<ProvisionalNodeView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("font").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<ProvisionalMagnetarView>();

            EditorUtility.SetDirty(go);
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
