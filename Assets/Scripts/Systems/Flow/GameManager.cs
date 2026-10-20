using System;
using Nova.Core;
using Nova.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nova.Systems
{
    // En qué punto del flujo está el nivel.
    public enum GameFlowState
    {
        Playing = 0, // La partida corre, también durante la cascada de conquista
        Paused = 1,  // Menú de pausa abierto (13.10)
        Ended = 2,   // Pantalla de victoria o de derrota (13.11)
    }

    // Flujo del nivel (10.3, 15.6, T5-05): carga, inicio, pausa y fin. Singleton de flujo de 15.1 #6.
    //
    // Pausar es Time.timeScale = 0, y solo lo toca este script (15.8, Tiempo de la UI y pausa, 18 #42):
    // con toda la lógica en FixedUpdate, la partida entera se detiene sin que ningún sistema sepa que
    // hay pausa. La misma congelación se usa al terminar el nivel: en la victoria, después de la
    // cascada de conquista, que corre en tiempo de juego (10.3).
    //
    // Reiniciar, Repetir, Reintentar y Siguiente vuelven a cargar la escena Game con el nivel pedido;
    // Volver al mapa carga LevelSelect. En los dos casos la escala vuelve a 1 ANTES de cargar:
    // timeScale es global y sobrevive al cambio de escena. Desbloquear el siguiente nivel al ganar es
    // de ProgressSaver (T5-06), que escuchará OnLevelEnded.
    //
    // Va a -95: después de los registros (-100), que fijan su Instance en Awake, y antes que
    // LevelLoader (-90) y cualquier script que lea los nodos, porque carga el nivel en su Awake.
    [DefaultExecutionOrder(-95)]
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        public const string GameScene = "Game";
        public const string LevelSelectScene = "LevelSelect";

        public static GameManager Instance { get; private set; }

        // El nivel que la próxima escena Game tiene que cargar. Sobrevive al cambio de escena.
        static LevelDefinition requested;

        [Tooltip("Los nueve niveles, en orden (11.2): de aquí sale el siguiente.")]
        [SerializeField] LevelDefinition[] levels = Array.Empty<LevelDefinition>();

        [Tooltip("El nivel que se carga si nadie ha pedido otro: al darle a Play en Game desde el editor.")]
        [SerializeField] LevelDefinition startLevel;

        [SerializeField] LevelLoader loader;
        [SerializeField] WinConditionChecker checker;

        [Tooltip("Opcional: sin él no hay datos para las pantallas de fin.")]
        [SerializeField] MatchRecorder recorder;

        Action<string> loadScene = SceneManager.LoadScene;
        bool subscribed;

        public GameFlowState State { get; private set; }
        public LevelOutcome Outcome { get; private set; }

        // El nivel en juego y el que va después, o null en el nivel 9.
        public LevelDefinition Level { get; private set; }
        public LevelDefinition NextLevel => Level != null ? LevelNumbered(Level.Number + 1) : null;

        public WinConditionChecker Checker => checker;
        public MatchRecorder Recorder => recorder;

        public event Action<GameFlowState> OnStateChanged;

        // Al terminar el nivel, con la partida ya congelada.
        public event Action<LevelOutcome> OnLevelEnded;

        // Pide el nivel que cargará la siguiente escena Game. Lo usarán el menú principal y la selección (T5-06).
        public static void Request(LevelDefinition level)
        {
            requested = level;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Nova] Hay dos GameManager en escena; se descarta el segundo.", this);
                Destroy(this);
                return;
            }
            Instance = this;

            LevelDefinition level = requested != null ? requested : startLevel;
            requested = null;
            if (level != null)
                Begin(level);
        }

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void OnDestroy()
        {
            if (Instance != this)
                return;
            Instance = null;
            // Salir de Play con la partida en pausa no deja el editor congelado.
            Time.timeScale = 1f;
        }

        // Carga un nivel y empieza la partida desde cero.
        public void Begin(LevelDefinition level)
        {
            if (level == null)
                throw new ArgumentNullException(nameof(level));
            if (loader == null || checker == null)
                throw new InvalidOperationException("GameManager necesita LevelLoader y WinConditionChecker.");

            Subscribe();
            loader.Load(level);
            checker.Restart();
            if (recorder != null)
                recorder.Begin();
            Level = level;
            Outcome = LevelOutcome.None;
            Time.timeScale = 1f;
            SetState(GameFlowState.Playing);
        }

        // Abre la pausa: congela la partida. Solo con la partida corriendo.
        public void Pause()
        {
            if (State != GameFlowState.Playing)
                return;
            Time.timeScale = 0f;
            SetState(GameFlowState.Paused);
        }

        public void Resume()
        {
            if (State != GameFlowState.Paused)
                return;
            Time.timeScale = 1f;
            SetState(GameFlowState.Playing);
        }

        // Reiniciar nivel, Repetir y Reintentar (10.3): el mismo nivel desde cero.
        public void Restart()
        {
            if (Level != null)
                Leave(GameScene, Level);
        }

        // Siguiente (10.3). En el nivel 9 no hay siguiente y es Volver al mapa (13.11).
        public void Next()
        {
            LevelDefinition next = NextLevel;
            if (next != null)
                Leave(GameScene, next);
            else
                BackToMap();
        }

        public void BackToMap()
        {
            Leave(LevelSelectScene, null);
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector. sceneLoader
        // sustituye a SceneManager.LoadScene, que en EditMode no se puede llamar.
        public void Configure(LevelLoader levelLoader, WinConditionChecker winChecker, MatchRecorder matchRecorder = null,
                              LevelDefinition[] allLevels = null, LevelDefinition firstLevel = null, Action<string> sceneLoader = null)
        {
            Unsubscribe();
            loader = levelLoader;
            checker = winChecker;
            recorder = matchRecorder;
            levels = allLevels ?? Array.Empty<LevelDefinition>();
            startLevel = firstLevel;
            loadScene = sceneLoader ?? SceneManager.LoadScene;
            Subscribe();
        }

        // El nivel que pide una escena Game, si hay alguno. Para los tests.
        public static LevelDefinition Requested => requested;

        void Leave(string scene, LevelDefinition level)
        {
            Time.timeScale = 1f;
            requested = level;
            loadScene(scene);
        }

        void End(LevelOutcome outcome)
        {
            if (State == GameFlowState.Ended)
                return;
            Outcome = outcome;
            Time.timeScale = 0f;
            SetState(GameFlowState.Ended);
            OnLevelEnded?.Invoke(outcome);
        }

        void SetState(GameFlowState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
        }

        // Uno de los nueve por su número, o null si no está.
        public LevelDefinition LevelNumbered(int number)
        {
            foreach (LevelDefinition level in levels)
            {
                if (level != null && level.Number == number)
                    return level;
            }
            return null;
        }

        void Subscribe()
        {
            if (subscribed || checker == null)
                return;
            checker.OnLevelEnded += End;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed)
                return;
            if (checker != null)
                checker.OnLevelEnded -= End;
            subscribed = false;
        }
    }
}
