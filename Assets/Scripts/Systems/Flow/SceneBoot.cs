using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nova.Systems
{
    // Boot es la escena 0 de Build Settings (15.2) y no enseña nada: pasa al menú principal (13.8).
    [DisallowMultipleComponent]
    public sealed class SceneBoot : MonoBehaviour
    {
        void Start()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameManager.MainMenuScene);
        }
    }
}
