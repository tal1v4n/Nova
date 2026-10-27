using Nova.Core;
using UnityEngine;

namespace Nova.Systems
{
    // Dónde vive el nivel máximo desbloqueado. En el juego, PlayerPrefs; en los tests, memoria, para
    // que la suite no toque el progreso de quien juega en el mismo editor.
    public interface IProgressStore
    {
        int Read(int fallback);
        void Write(int value);
    }

    // El progreso (1.2, 13.9, 15.6, T5-06): el nivel máximo desbloqueado, de 1 a 10, y nada más. El 10
    // es «los nueve ganados». Ganar un nivel desbloquea el siguiente; nunca se pierde lo ganado.
    //
    // En Game escucha OnLevelEnded de GameManager (desviación 245); el menú principal y la carta
    // estelar solo leen.
    [DisallowMultipleComponent]
    public sealed class ProgressSaver : MonoBehaviour
    {
        public const int FirstLevel = 1;
        public const int LastLevel = 9;
        public const int AllConquered = 10;
        public const string Key = "Nova.MaxUnlocked";

        sealed class PlayerPrefsStore : IProgressStore
        {
            public int Read(int fallback) => PlayerPrefs.GetInt(Key, fallback);

            public void Write(int value)
            {
                PlayerPrefs.SetInt(Key, value);
                PlayerPrefs.Save();
            }
        }

        static IProgressStore store = new PlayerPrefsStore();

        [SerializeField] GameManager game;

        bool subscribed;

        // Para los tests: null vuelve a PlayerPrefs.
        public static IProgressStore Store
        {
            get => store;
            set => store = value ?? new PlayerPrefsStore();
        }

        // El nivel máximo desbloqueado, de 1 a 10.
        public static int MaxUnlocked => Mathf.Clamp(store.Read(FirstLevel), FirstLevel, AllConquered);

        // Los niveles ganados: el máximo menos 1 (13.9).
        public static int Conquered => MaxUnlocked - 1;

        // El que carga Continuar (13.8): el máximo, o el 9 con los nueve ganados.
        public static int ContinueLevel => Mathf.Min(MaxUnlocked, LastLevel);

        // Sube el máximo hasta `level`; nunca lo baja.
        public static void Unlock(int level)
        {
            int value = Mathf.Clamp(level, FirstLevel, AllConquered);
            if (value > MaxUnlocked)
                store.Write(value);
        }

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        // Para la escena y los tests: el cableado normal es por el Inspector.
        public void Configure(GameManager flow)
        {
            Unsubscribe();
            game = flow;
            Subscribe();
        }

        internal void Subscribe()
        {
            if (subscribed || game == null)
                return;
            game.OnLevelEnded += LevelEnded;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (subscribed && game != null)
                game.OnLevelEnded -= LevelEnded;
            subscribed = false;
        }

        // Ganar el nivel n desbloquea el n + 1; ganar el 9 guarda el 10.
        void LevelEnded(LevelOutcome outcome)
        {
            if (outcome == LevelOutcome.Victory && game.Level != null)
                Unlock(game.Level.Number + 1);
        }
    }
}
