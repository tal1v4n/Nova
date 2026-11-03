using System;
using Nova.Data;
using Nova.Systems;
using UnityEngine;

namespace Nova.Presentation
{
    // Los sonidos de la partida (NOVA_Sonido.md, S.6; T6-14). Escucha los eventos de juego y reproduce los SND-* de
    // SoundBank desde un pool de AudioSource; ningún script de Gameplay ni de Systems reproduce sonido (15.9).
    // Por ahora, los poderes del Púlsar (S.5.1):
    //
    //   PowerBeamSystem.OnLaunched   SND-WLD-BEAM-<tipo>   en beam.From, el Púlsar que lanza
    //   PowerBeamSystem.OnLanded     SND-PWR-<tipo>        en beam.To, el objetivo
    //   PowerBeamSystem.OnDissipated nada: el silencio es el aviso (S.4 #3)
    //
    // El sonido va por poder y no por facción (S.4 #1): quién lanza se oye por el lado, con el panorama de la x
    // sobre el ancho del tablero (SoundRules.Pan). Como mucho dos voces por ID (VoicePool).
    //
    // Con la partida congelada —pausa o fin de nivel, Time.timeScale = 0 (15.8)— los sonidos en curso se quedan
    // quietos como el tablero y siguen donde iban al reanudar. Decidido con el usuario el 2026-11-02.
    //
    // Vive en el objeto Systems de Game, como PowerBeamVisual.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour
    {
        [Tooltip("Opcional: sin él se usa PowerBeamSystem.Instance.")]
        [SerializeField] PowerBeamSystem beams;

        [SerializeField] SoundBank bank;

        AudioSource[] sources;
        VoicePool voices;
        PowerBeamSystem subscribed;
        bool frozen;

        // Para los tests: cada sonido que se pide, con su panorama.
        internal event Action<string, float> OnPlayed;

        internal bool IsFrozen => frozen;

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(SoundBank soundBank, PowerBeamSystem beamSystem)
        {
            Unsubscribe();
            bank = soundBank;
            beams = beamSystem;
            if (isActiveAndEnabled)
                Subscribe();
        }

        void OnEnable() => Subscribe();

        // PowerBeamSystem fija su Instance en Awake; si este OnEnable llegó antes, se engancha aquí.
        void Start() => Subscribe();

        void OnDisable() => Unsubscribe();

        void Update() => SetFrozen(Time.timeScale == 0f);

        internal void Subscribe()
        {
            PowerBeamSystem system = beams != null ? beams : PowerBeamSystem.Instance;
            if (system == null || system == subscribed)
                return;
            Unsubscribe();
            subscribed = system;
            subscribed.OnLaunched += HandleLaunched;
            subscribed.OnLanded += HandleLanded;
        }

        void Unsubscribe()
        {
            if (subscribed == null)
                return;
            subscribed.OnLaunched -= HandleLaunched;
            subscribed.OnLanded -= HandleLanded;
            subscribed = null;
        }

        void HandleLaunched(PowerBeam beam) => Play(SoundRules.BeamId(beam.Type), beam.From.x);

        void HandleLanded(PowerBeam beam) => Play(SoundRules.ImpactId(beam.Type), beam.To.x);

        internal void Play(string id, float x)
        {
            if (bank == null || !bank.TryGet(id, out SoundBank.Entry entry))
                return;
            float pan = SoundRules.Pan(x, bank.BoardWidth, bank.MaxPan);
            OnPlayed?.Invoke(id, pan);

            EnsurePool();
            int voice = voices.Acquire(id, IsBusy);
            AudioSource source = sources[voice];
            source.Stop();
            source.clip = entry.Clip;
            source.outputAudioMixerGroup = entry.Output;
            source.volume = entry.Volume;
            source.panStereo = pan;
            source.Play();
            if (frozen)
                source.Pause();
        }

        // Pausa o reanuda todo lo que suena, una vez por cambio.
        internal void SetFrozen(bool value)
        {
            if (value == frozen)
                return;
            frozen = value;
            if (sources == null)
                return;
            foreach (AudioSource source in sources)
            {
                if (source.clip == null)
                    continue;
                if (frozen)
                    source.Pause();
                else
                    source.UnPause();
            }
        }

        // Ocupada si suena o si está en pausa con el sonido a medias.
        bool IsBusy(int voice)
        {
            AudioSource source = sources[voice];
            return source.isPlaying || (frozen && source.clip != null && source.timeSamples > 0);
        }

        void EnsurePool()
        {
            if (sources != null)
                return;
            sources = new AudioSource[bank.PoolSize];
            for (int i = 0; i < sources.Length; i++)
            {
                var go = new GameObject($"Voice{i}");
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                sources[i] = source;
            }
            voices = new VoicePool(sources.Length, bank.MaxVoicesPerId);
        }
    }
}
