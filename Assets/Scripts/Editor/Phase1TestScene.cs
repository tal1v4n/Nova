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
    // T1-12: genera la escena de prueba de la fase 1 con tres nodos, para comprobar su criterio
    // de salida jugando: capturar un Asteroide, encenderlo como Estrella y tomar un nodo enemigo.
    // Es idempotente: sobrescribe la escena entera cada vez. Todo lo que no es cableado —tipo,
    // facción, Energía y posición de cada nodo— queda serializado en la escena y se ajusta en el
    // Inspector, no aquí.
    //
    // Desde T2-02 lleva también el menú radial de encendido. Los nodos solo ofrecen la Estrella:
    // el Magnetar llega con T2-04 y el Púlsar con T3-02, y hasta entonces el menú los enseña
    // deshabilitados. Al cerrar cada una de esas tareas se añade aquí su NodeDefinition.
    public static class Phase1TestScene
    {
        const string ScenePath = "Assets/Scenes/Tests/Fase1.unity";
        const string BalancePath = "Assets/Data/BalanceConfig.asset";
        const string NodesFolder = "Assets/Data/Nodes";
        const string FontPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";

        const string NodesLayer = InputRouter.NodesLayer; // 18 #23

        // Radio del trigger de clic: el del círculo provisional de un nodo encendido, 64 px = 1 u
        // (desviación 138). El Asteroide provisional es más pequeño, pero conserva el mismo blanco.
        const float ColliderRadius = 0.5f;

        // Los tres nodos de la prueba, sobre el tablero de 15 x 8.4375 u (T.8). El Asteroide queda
        // a 2.12 u de la Estrella del Jugador, dentro de la regla de 2.25 u de 11.3. El Enemigo no
        // actúa hasta la fase 4, pero su Estrella genera.
        static readonly (string name, string type, Faction faction, int energy, Vector2 position)[] Nodes =
        {
            ("Player Star", "Star", Faction.Player, 30, new Vector2(-5f, 0f)),
            ("Neutral Asteroid", "Asteroid", Faction.Neutral, 10, new Vector2(-3.5f, 1.5f)),
            ("Enemy Star", "Star", Faction.Enemy, 10, new Vector2(5f, 0f)),
        };

        [MenuItem("Nova/Fase 1/Generar escena de prueba")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (balance == null || font == null)
            {
                Debug.LogError($"[Nova] Faltan {BalancePath} o {FontPath}.");
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
            NodeRegistry registry = CreateSystems(balance, font, camera, out protons);

            NodeDefinition[] ignitionTargets = { Definition("Star") };
            foreach (var spec in Nodes)
                CreateNode(spec.name, Definition(spec.type), spec.faction, spec.energy, spec.position, balance, font, ignitionTargets);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Nova] Escena de prueba de la fase 1 generada en {ScenePath}. Dale a Play.");
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

        static NodeRegistry CreateSystems(BalanceConfig balance, TMP_FontAsset font, Camera camera, out ProtonRegistry protons)
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
            var router = go.AddComponent<InputRouter>();
            router.Configure(balance, selection, issuer, menu, camera);

            go.AddComponent<ProvisionalSelectionView>().Configure(selection);
            go.AddComponent<ProvisionalProtonView>().Configure(protons, font);
            go.AddComponent<IgnitionMenu>().Configure(menu, font);

            EditorUtility.SetDirty(go);
            return registry;
        }

        static void CreateNode(string name, NodeDefinition definition, Faction faction, int energy, Vector2 position,
            BalanceConfig balance, TMP_FontAsset font, NodeDefinition[] ignitionTargets)
        {
            var go = new GameObject(name) { layer = LayerMask.NameToLayer(NodesLayer) };
            go.transform.position = position;

            go.AddComponent<NodeController>();
            go.AddComponent<PlacedNode>().Configure(definition, faction, energy);
            go.AddComponent<NodeIgnition>().Configure(balance, ignitionTargets);
            go.AddComponent<StarGenerator>(); // desviación 143: en todo nodo que pueda ser Estrella

            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = ColliderRadius;

            var view = go.AddComponent<ProvisionalNodeView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("font").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();

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
