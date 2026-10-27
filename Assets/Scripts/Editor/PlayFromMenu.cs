using UnityEditor;
using UnityEditor.SceneManagement;

namespace Nova.Editor
{
    // Play en el editor arranca como el juego compilado: desde Boot, que pasa al menú principal (13.8),
    // sea cual sea la escena abierta. Se apaga en Nova → Empezar Play desde el menú, para probar a mano
    // una escena de Assets/Scenes/Tests o un nivel con el Start Level de GameManager. La elección se
    // guarda por máquina, en EditorPrefs.
    [InitializeOnLoad]
    public static class PlayFromMenu
    {
        const string MenuPath = "Nova/Empezar Play desde el menú";
        const string PrefKey = "Nova.PlayFromMenu";
        const string BootScenePath = "Assets/Scenes/Boot.unity";

        static PlayFromMenu()
        {
            // Tras recompilar, el menú aún no existe: se marca en el primer tick del editor.
            EditorApplication.delayCall += Apply;
        }

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        static void Apply()
        {
            Menu.SetChecked(MenuPath, Enabled);
            EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) : null;
        }
    }
}
