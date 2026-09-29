using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nova.Editor
{
    // Aplica los ajustes de la fase 0 que viven en escenas y assets de render
    // (T0-01, T0-02 y T0-04). Es idempotente: se puede relanzar sin efectos acumulados.
    public static class ProjectSetup
    {
        const string Renderer2DPath = "Assets/Settings/Renderer2D.asset";

        // Orden de Build Settings: Boot es la escena 0 (15.2).
        static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Boot.unity",
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/LevelSelect.unity",
            "Assets/Scenes/Game.unity",
        };

        // T0-01: color de fondo #0B0E1A.
        static readonly Color32 BackgroundColor = new Color32(0x0B, 0x0E, 0x1A, 0xFF);

        // T0-04 y T.8: PPU 64, referencia 960x540 (x2 exacto a 1080p), size = 540 / (2 * 64) = 4.21875.
        const int AssetsPPU = 64;
        const int RefResolutionX = 960;
        const int RefResolutionY = 540;
        const float OrthographicSize = 4.21875f;

        [MenuItem("Nova/Fase 0/Aplicar configuración del proyecto")]
        public static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string previousScene = EditorSceneManager.GetActiveScene().path;

            RemovePostProcessing();
            foreach (string path in ScenePaths)
                ConfigureSceneCamera(path);
            EditorBuildSettings.scenes = ScenePaths
                .Select(p => new EditorBuildSettingsScene(p, true))
                .ToArray();

            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(previousScene))
                EditorSceneManager.OpenScene(previousScene);

            Debug.Log("[Nova] Configuración de la fase 0 aplicada.");
        }

        // Sin PostProcessData el 2D Renderer no puede ejecutar ningún efecto (1.2: cero post-procesado).
        static void RemovePostProcessing()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Renderer2DPath);
            if (rendererData == null)
            {
                Debug.LogError($"[Nova] No se encuentra {Renderer2DPath}.");
                return;
            }

            var serialized = new SerializedObject(rendererData);
            serialized.FindProperty("m_PostProcessData").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
        }

        static void ConfigureSceneCamera(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var camera = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
                .FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (camera == null)
            {
                Debug.LogError($"[Nova] {scenePath} no tiene una cámara con la etiqueta MainCamera.");
                return;
            }

            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;

            if (camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
                cameraData.renderPostProcessing = false;

            if (!camera.TryGetComponent(out PixelPerfectCamera pixelPerfect))
                pixelPerfect = camera.gameObject.AddComponent<PixelPerfectCamera>();

            pixelPerfect.assetsPPU = AssetsPPU;
            pixelPerfect.refResolutionX = RefResolutionX;
            pixelPerfect.refResolutionY = RefResolutionY;
            // PixelSnapping = Upscale Render Texture apagado y Pixel Snapping encendido.
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
