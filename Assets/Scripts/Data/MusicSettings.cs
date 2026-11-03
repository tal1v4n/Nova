using UnityEngine;
using UnityEngine.Audio;

namespace Nova.Data
{
    // La música de los menús, SND-MUS-MAIN (NOVA_Sonido.md, S.5.2 y S.6; T6-13): los dos archivos del juego,
    // la forma del ciclo y los fundidos. La lee MusicDirector. Un solo asset, Assets/Data/Audio/MusicSettings.asset,
    // que rellena Nova → Audio → Importar audio y crear el mezclador; los números se retocan aquí, no en el código.
    [CreateAssetMenu(menuName = "Nova/Music Settings", fileName = "MusicSettings")]
    public sealed class MusicSettings : ScriptableObject
    {
        [Tooltip("audio/music/main/ciclo/mus_main_intro.wav: la intro de la v3 «Señal», 12 s y 6 s de cola. Solo suena al abrir el juego.")]
        [SerializeField] AudioClip intro;

        [Tooltip("audio/music/main/ciclo/mus_main_ciclo.wav: el ciclo v3 → v1 → v2, 360 s, en bucle.")]
        [SerializeField] AudioClip cycle;

        [Tooltip("El bus Music del AudioMixer (S.2.3).")]
        [SerializeField] AudioMixerGroup output;

        [Tooltip("Volumen de las dos AudioSource. La sonoridad la fija el archivo (−18 LUFS); el nivel del bus, el mezclador.")]
        [Range(0f, 1f)]
        [SerializeField] float volume = 1f;

        [Tooltip("Segundo de la intro en que arranca el ciclo, con PlayScheduled: 4 compases a 80 BPM.")]
        [Min(0f)]
        [SerializeField] double cycleStartInIntro = 12.0;

        [Tooltip("Duración de cada versión dentro del ciclo: empiezan a 0, 120 y 240 s.")]
        [Min(1f)]
        [SerializeField] double versionLength = 120.0;

        [Tooltip("Versiones del ciclo, en orden: v3 «Señal», v1 «Órbitas», v2 «Carta estelar».")]
        [Min(1)]
        [SerializeField] int versionCount = 3;

        [Tooltip("Fundido de salida al cargar Game, en segundos de tiempo no escalado (S.7 #6).")]
        [Min(0f)]
        [SerializeField] float fadeOutSeconds = 1.0f;

        [Tooltip("Fundido de entrada al volver de Game, en segundos de tiempo no escalado (S.7 #6).")]
        [Min(0f)]
        [SerializeField] float fadeInSeconds = 1.5f;

        [Tooltip("Margen entre pedir la intro y que suene, para que la intro y el ciclo se programen contra el mismo instante del reloj de audio.")]
        [Min(0f)]
        [SerializeField] double scheduleLead = 0.1;

        public AudioClip Intro => intro;
        public AudioClip Cycle => cycle;
        public AudioMixerGroup Output => output;
        public float Volume => volume;
        public double CycleStartInIntro => cycleStartInIntro;
        public double VersionLength => versionLength;
        public int VersionCount => versionCount;
        public float FadeOutSeconds => fadeOutSeconds;
        public float FadeInSeconds => fadeInSeconds;
        public double ScheduleLead => scheduleLead;

        public bool HasClips => intro != null && cycle != null;

        // Lo usa el menú del editor que importa la música.
        public void Configure(AudioClip introClip, AudioClip cycleClip, AudioMixerGroup musicGroup)
        {
            intro = introClip;
            cycle = cycleClip;
            output = musicGroup;
        }
    }
}
