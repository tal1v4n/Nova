using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Nova.Data
{
    // Los sonidos de la partida (NOVA_Sonido.md, S.6; T6-14): cada ID SND-* con su clip, su bus del AudioMixer y su
    // volumen base, y los números de cómo se reproducen. Lo lee AudioDirector. Un solo asset,
    // Assets/Data/Audio/SoundBank.asset, que rellena Nova → Audio → Importar audio y crear el mezclador.
    // Por ahora, los seis de los poderes (S.5.1); el resto de S.5 se suma en T6-10.
    [CreateAssetMenu(menuName = "Nova/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("El ID de NOVA_Sonido.md, S.3.1: SND-<CATEGORÍA>-<SUJETO>[-<VARIANTE>].")]
            [SerializeField] string id;
            [SerializeField] AudioClip clip;
            [Tooltip("El bus del AudioMixer (S.2.3).")]
            [SerializeField] AudioMixerGroup output;
            [Range(0f, 1f)]
            [SerializeField] float volume;

            public Entry(string soundId, AudioClip audioClip, AudioMixerGroup group, float baseVolume = 1f)
            {
                id = soundId;
                clip = audioClip;
                output = group;
                volume = baseVolume;
            }

            public string Id => id;
            public AudioClip Clip => clip;
            public AudioMixerGroup Output => output;
            public float Volume => volume;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        [Tooltip("Ancho del tablero en u (2): el panorama va de su borde izquierdo al derecho.")]
        [Min(0.01f)]
        [SerializeField] float boardWidth = 15f;

        [Tooltip("Panorama máximo en un borde del tablero. Menos de 1, para que nada suene solo en un oído (S.6).")]
        [Range(0f, 1f)]
        [SerializeField] float maxPan = 0.6f;

        [Tooltip("Instancias del mismo ID a la vez: la siguiente corta a la más antigua (S.6).")]
        [Min(1)]
        [SerializeField] int maxVoicesPerId = 2;

        [Tooltip("AudioSource del pool de AudioDirector.")]
        [Min(1)]
        [SerializeField] int poolSize = 12;

        public Entry[] Entries => entries ?? Array.Empty<Entry>();
        public float BoardWidth => boardWidth;
        public float MaxPan => maxPan;
        public int MaxVoicesPerId => maxVoicesPerId;
        public int PoolSize => poolSize;

        public bool TryGet(string id, out Entry entry)
        {
            foreach (Entry candidate in Entries)
            {
                if (candidate.Id == id && candidate.Clip != null)
                {
                    entry = candidate;
                    return true;
                }
            }
            entry = default;
            return false;
        }

        // Lo usa el menú del editor que importa el audio.
        public void Configure(Entry[] soundEntries)
        {
            entries = soundEntries ?? Array.Empty<Entry>();
        }
    }
}
