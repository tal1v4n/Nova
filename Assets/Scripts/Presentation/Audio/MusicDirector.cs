using Nova.Data;
using Nova.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nova.Presentation
{
    // La música de los menús, SND-MUS-MAIN (NOVA_Sonido.md, S.5.2 y S.6; T6-13). Vive en MainMenu y en
    // LevelSelect, pero la primera instancia sobrevive a los cambios de escena (DontDestroyOnLoad) y las demás
    // se destruyen al nacer: así suena sin cortarse entre las dos pantallas. Escucha
    // SceneManager.activeSceneChanged y le pregunta a MusicFlow qué hacer; aquí solo se ejecuta la orden.
    //
    // Dos AudioSource: la intro (18 s, con su cola) y el ciclo en bucle, programado con PlayScheduled a los
    // 12 s exactos de la intro, con precisión de muestra, mientras la cola de la intro se apaga encima.
    //
    // Los fundidos van en tiempo no escalado (15.8, Tiempo de la UI y pausa): al cargar Game la escala ya
    // vuelve a 1, pero la música no tiene por qué saberlo.
    [DisallowMultipleComponent]
    public sealed class MusicDirector : MonoBehaviour
    {
        static MusicDirector instance;

        static readonly string[] MenuScenes = { GameManager.MainMenuScene, GameManager.LevelSelectScene };

        [SerializeField] MusicSettings settings;

        AudioSource intro;
        AudioSource cycle;
        MusicFlow flow;

        // Ganancia del fundido, de 0 a 1, que multiplica el volumen de MusicSettings.
        float gain = 1f;
        float fadeFrom;
        float fadeTo;
        float fadeDuration;
        float fadeElapsed;
        bool fading;

        public void Configure(MusicSettings musicSettings) => settings = musicSettings;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            if (settings == null || !settings.HasClips)
            {
                Debug.LogWarning("[Nova] MusicDirector sin MusicSettings o sin clips: importa la música con Nova → Audio → Importar audio y crear el mezclador, y vuelve a generar las escenas de menú.");
                enabled = false;
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            intro = CreateSource("Intro", settings.Intro, false);
            cycle = CreateSource("Ciclo", settings.Cycle, true);
            flow = new MusicFlow(GameManager.GameScene, MenuScenes, settings.VersionLength, settings.VersionCount);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        // La escena en la que nace ya está activa: activeSceneChanged puede haber llegado antes que Awake.
        // Si llega después, MusicFlow la ignora, porque la música ya suena.
        void Start() => Execute(flow.OnSceneActivated(SceneManager.GetActiveScene().name, CyclePosition()));

        void OnDestroy()
        {
            if (instance != this)
                return;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            instance = null;
        }

        void OnActiveSceneChanged(Scene previous, Scene next) => Execute(flow.OnSceneActivated(next.name, CyclePosition()));

        void Update()
        {
            if (!fading)
                return;

            fadeElapsed += Time.unscaledDeltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(fadeElapsed / fadeDuration) : 1f;
            SetGain(Mathf.Lerp(fadeFrom, fadeTo, t));
            if (t < 1f)
                return;

            fading = false;
            if (fadeTo <= 0f)
            {
                intro.Stop();
                cycle.Stop();
            }
        }

        void Execute(MusicCue cue)
        {
            switch (cue.Kind)
            {
                case MusicCueKind.StartWithIntro:
                    StartWithIntro();
                    break;
                case MusicCueKind.FadeOut:
                    Fade(0f, settings.FadeOutSeconds);
                    break;
                case MusicCueKind.ResumeCycle:
                    ResumeCycle(cue.CycleTime);
                    break;
            }
        }

        void StartWithIntro()
        {
            intro.Stop();
            cycle.Stop();
            fading = false;
            SetGain(1f);

            double start = AudioSettings.dspTime + settings.ScheduleLead;
            cycle.timeSamples = 0;
            intro.PlayScheduled(start);
            cycle.PlayScheduled(start + settings.CycleStartInIntro);
        }

        void ResumeCycle(double cycleTime)
        {
            intro.Stop();
            cycle.Stop();
            SetGain(0f);

            int sample = (int)System.Math.Round(cycleTime * cycle.clip.frequency);
            cycle.timeSamples = Mathf.Clamp(sample, 0, cycle.clip.samples - 1);
            cycle.Play();
            Fade(1f, settings.FadeInSeconds);
        }

        void Fade(float target, float seconds)
        {
            fadeFrom = gain;
            fadeTo = target;
            fadeDuration = seconds;
            fadeElapsed = 0f;
            fading = true;
        }

        void SetGain(float value)
        {
            gain = value;
            intro.volume = settings.Volume * gain;
            cycle.volume = settings.Volume * gain;
        }

        // El segundo del ciclo que suena. Programado y aún en la intro, el ciclo está en su muestra 0.
        double CyclePosition()
        {
            if (cycle == null || !cycle.isPlaying || cycle.clip == null)
                return 0.0;
            return cycle.timeSamples / (double)cycle.clip.frequency;
        }

        AudioSource CreateSource(string label, AudioClip clip, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = settings.Output;
            source.volume = settings.Volume;
            return source;
        }
    }
}
