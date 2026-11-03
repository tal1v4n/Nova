using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Nova.Core;
using Nova.Data;
using Nova.Presentation;

namespace Nova.Editor
{
    // T6-13 y T6-14: deja el audio aprobado listo para MusicDirector y AudioDirector, en cuatro pasos:
    //
    // 1. Copia los archivos del juego a Assets/Audio/: los dos de la música (audio/music/main/ciclo/, S.5.2) a
    //    Music/ y los seis de los poderes (audio/sfx/<sujeto>/v1/, S.5.1) a SFX/. Como Assets/Sprites/ con
    //    ArtBridge, la copia es salida derivada y no se versiona —el repo ya lleva los WAV una vez—; su .meta
    //    lleva un GUID derivado de la ruta para que MusicSettings y SoundBank no pierdan los clips en otro clon.
    // 2. Crea el AudioMixer de S.2.3 si no existe: Master → Music, SFX (World y UI) y Ambience, con el volumen
    //    de cada bus expuesto para el menú de opciones (13.10). Unity no tiene API pública para construir un
    //    mezclador, así que se usa la interna del editor por reflexión; si una versión de Unity la cambia, el
    //    menú lo dice y explica cómo crearlo a mano.
    // 3. Crea o actualiza Assets/Data/Audio/MusicSettings.asset con los dos clips y el bus Music.
    // 4. Crea o actualiza Assets/Data/Audio/SoundBank.asset con los seis sonidos y el bus SFX/World.
    public static class AudioAssets
    {
        const string MusicSource = "audio/music/main/ciclo";
        const string MusicFolder = "Assets/Audio/Music";
        const string SfxFolder = "Assets/Audio/SFX";
        const string MixerPath = "Assets/Audio/NovaMixer.mixer";
        const string DataFolder = "Assets/Data/Audio";
        public const string MusicSettingsPath = DataFolder + "/MusicSettings.asset";
        public const string SoundBankPath = DataFolder + "/SoundBank.asset";

        const string IntroFile = "mus_main_intro.wav";
        const string CycleFile = "mus_main_ciclo.wav";

        // S.5.1: la carpeta de cada poder en audio/sfx/, la del haz y la del impacto. La versión elegida es la v1
        // de los seis (columna Archivo de S.5.1).
        static readonly (PowerType type, string beam, string impact)[] PowerSounds =
        {
            (PowerType.AbsoluteZero, "beam_zero", "pwr_zero"),
            (PowerType.Decay, "beam_decay", "pwr_decay"),
            (PowerType.Wormhole, "beam_wormhole", "pwr_wormhole"),
        };
        const string SfxVersion = "v1";

        // Los buses de S.2.3 y el nombre de su volumen expuesto: (bus, padre, parámetro). El padre null es Master.
        internal static readonly (string group, string parent, string parameter)[] Buses =
        {
            ("Music", null, "MusicVolume"),
            ("SFX", null, "SfxVolume"),
            ("World", "SFX", "SfxWorldVolume"),
            ("UI", "SFX", "SfxUiVolume"),
            ("Ambience", null, "AmbienceVolume"),
        };

        internal const string MasterParameter = "MasterVolume";

        enum Kind { Music, Sfx }

        [MenuItem("Nova/Audio/Importar audio y crear el mezclador")]
        public static void Import()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            // 1. Los WAV: (origen relativo al repo, destino en Assets, tipo).
            var plan = new List<(string source, string target, Kind kind)>
            {
                ($"{MusicSource}/{IntroFile}", $"{MusicFolder}/{IntroFile}", Kind.Music),
                ($"{MusicSource}/{CycleFile}", $"{MusicFolder}/{CycleFile}", Kind.Music),
            };
            var sfx = new Dictionary<string, string>(); // ID → ruta en Assets
            foreach (var (type, beam, impact) in PowerSounds)
            {
                foreach (var (folder, id) in new[] { (beam, SoundRules.BeamId(type)), (impact, SoundRules.ImpactId(type)) })
                {
                    string file = id.ToLowerInvariant().Replace('-', '_') + ".wav"; // SND-WLD-BEAM-ZER → snd_wld_beam_zer.wav
                    string target = $"{SfxFolder}/{file}";
                    plan.Add(($"audio/sfx/{folder}/{SfxVersion}/{file}", target, Kind.Sfx));
                    sfx[id] = target;
                }
            }

            int copied = 0;
            var reimport = new List<string>();
            foreach (var (sourcePath, targetPath, kind) in plan)
            {
                string source = Path.Combine(projectRoot, sourcePath);
                if (!File.Exists(source))
                {
                    Debug.LogError($"[Nova] No existe {sourcePath}.");
                    return;
                }
                string target = Path.Combine(projectRoot, targetPath);
                bool metaOk = WriteAudioMeta(target, targetPath, kind);
                if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(source).Length &&
                    File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(source))
                {
                    if (!metaOk)
                        reimport.Add(targetPath);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, true);
                copied++;
            }
            AssetDatabase.Refresh();
            foreach (string path in reimport)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var intro = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicFolder}/{IntroFile}");
            var cycle = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicFolder}/{CycleFile}");
            if (intro == null || cycle == null || sfx.Values.Any(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p) == null))
            {
                Debug.LogError("[Nova] Unity no ha importado todos los WAV de Assets/Audio.");
                return;
            }

            // 2. El mezclador.
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                try
                {
                    CreateMixer(MixerPath);
                }
                catch (Exception e)
                {
                    AssetDatabase.DeleteAsset(MixerPath); // si quedó a medias
                    Debug.LogError($"[Nova] No se pudo crear {MixerPath} por código ({e.GetBaseException().Message}). Créalo a mano: " +
                                   "Create → Audio Mixer en Assets/Audio con ese nombre; en la ventana Audio Mixer, grupos Music, SFX (con World y UI dentro) y Ambience bajo Master; " +
                                   $"en cada grupo, clic derecho en Volume → Expose, y renombra los parámetros a {MasterParameter}, " +
                                   string.Join(", ", Buses.Select(b => b.parameter)) + ". Después vuelve a lanzar este menú.");
                    return;
                }
                mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            }
            AudioMixerGroup music = Group(mixer, "Master/Music");
            AudioMixerGroup world = Group(mixer, "Master/SFX/World");

            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets/Data", "Audio");

            // 3. MusicSettings.
            var settings = LoadOrCreate<MusicSettings>(MusicSettingsPath);
            settings.Configure(intro, cycle, music);
            EditorUtility.SetDirty(settings);

            // 4. SoundBank: los seis de los poderes, en SFX/World y a volumen 1 (la sonoridad ya la fija el archivo).
            var bank = LoadOrCreate<SoundBank>(SoundBankPath);
            bank.Configure(sfx.Select(s => new SoundBank.Entry(s.Key, AssetDatabase.LoadAssetAtPath<AudioClip>(s.Value), world)).ToArray());
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Nova] Audio listo: {copied} WAV copiados a Assets/Audio, {MixerPath}, {MusicSettingsPath} y {SoundBankPath}. " +
                      "Genera las escenas con Nova → Fase 5 → Generar escenas de menú y Generar escena Game.");
        }

        static AudioMixerGroup Group(AudioMixer mixer, string path)
        {
            AudioMixerGroup group = mixer != null ? mixer.FindMatchingGroups(path).FirstOrDefault() : null;
            if (group == null)
                Debug.LogWarning($"[Nova] {MixerPath} no tiene el grupo {path}: lo suyo saldrá por Master.");
            return group;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // Assets/Audio/Music/ y SFX/ no versionan sus WAV ni sus .meta. Como en ArtBridge, el GUID sale de la ruta
        // para que los assets que los usan no pierdan los clips en otro clon. Pero un .meta con solo el GUID no vale
        // para audio: Unity 6 lo lee como un AudioImporter de versión 1, «below the supported minimum (6)», e importa
        // con los valores por defecto sin pasar por AudioImportRules. Se escribe entero, con los ajustes de S.2.1, y
        // sobre todo sin normalizar: Unity normaliza por defecto cada clip a su pico, y la intro y el ciclo, con picos
        // distintos, saltarían de volumen al encadenarse (S.5.2), y los SFX perderían su sonoridad de diseño.
        //   Música: Streaming (2) con Vorbis (1) al 70 %.  SFX: Decompress On Load (0) con ADPCM (2), mono.
        // Devuelve true si el .meta ya estaba bien; false si lo ha (re)escrito.
        static bool WriteAudioMeta(string target, string assetPath, Kind kind)
        {
            string meta = target + ".meta";
            if (File.Exists(meta) && File.ReadAllText(meta).Contains("AudioImporter:"))
                return true;

            bool music = kind == Kind.Music;
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(assetPath));
            string guid = string.Concat(hash.Select(b => b.ToString("x2")));
            Directory.CreateDirectory(Path.GetDirectoryName(meta));
            File.WriteAllText(meta,
                "fileFormatVersion: 2\n" +
                $"guid: {guid}\n" +
                "AudioImporter:\n" +
                "  externalObjects: {}\n" +
                "  serializedVersion: 8\n" +
                "  defaultSettings:\n" +
                "    serializedVersion: 2\n" +
                $"    loadType: {(music ? 2 : 0)}\n" +
                "    sampleRateSetting: 0\n" +
                "    sampleRateOverride: 48000\n" +
                $"    compressionFormat: {(music ? 1 : 2)}\n" +
                $"    quality: {(music ? "0.7" : "1")}\n" +
                "    conversionMode: 0\n" +
                $"    preloadAudioData: {(music ? 0 : 1)}\n" +
                "  platformSettingOverrides: {}\n" +
                $"  forceToMono: {(music ? 0 : 1)}\n" +
                "  normalize: 0\n" +
                $"  loadInBackground: {(music ? 1 : 0)}\n" +
                "  ambisonic: 0\n" +
                "  3D: 0\n" +
                "  userData: \n" +
                "  assetBundleName: \n" +
                "  assetBundleVariant: \n");
            return false;
        }

        // La API interna de UnityEditor.Audio: AudioMixerController.CreateMixerControllerAtPath crea el asset con
        // Master y su instantánea; CreateNewGroup y AddChildToParent cuelgan los buses; AddExposedParameter con un
        // AudioGroupParameterPath expone el volumen, y exposedParameters lo renombra.
        static void CreateMixer(string path)
        {
            const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type controllerType = Required(editor.GetType("UnityEditor.Audio.AudioMixerController"), "AudioMixerController");
            Type groupType = Required(editor.GetType("UnityEditor.Audio.AudioMixerGroupController"), "AudioMixerGroupController");
            Type pathType = Required(editor.GetType("UnityEditor.Audio.AudioGroupParameterPath"), "AudioGroupParameterPath");
            Type exposedType = Required(editor.GetType("UnityEditor.Audio.ExposedAudioParameter"), "ExposedAudioParameter");

            MethodInfo create = Required(controllerType.GetMethod("CreateMixerControllerAtPath", Any), "CreateMixerControllerAtPath");
            MethodInfo createGroup = Required(controllerType.GetMethod("CreateNewGroup", Any), "CreateNewGroup");
            MethodInfo addChild = Required(controllerType.GetMethod("AddChildToParent", Any), "AddChildToParent");
            MethodInfo expose = Required(controllerType.GetMethod("AddExposedParameter", Any), "AddExposedParameter");
            PropertyInfo masterProperty = Required(controllerType.GetProperty("masterGroup", Any), "masterGroup");
            PropertyInfo exposedProperty = Required(controllerType.GetProperty("exposedParameters", Any), "exposedParameters");
            MethodInfo volumeGuid = Required(groupType.GetMethod("GetGUIDForVolume", Any), "GetGUIDForVolume");
            FieldInfo exposedGuid = Required(exposedType.GetField("guid", Any), "ExposedAudioParameter.guid");
            FieldInfo exposedName = Required(exposedType.GetField("name", Any), "ExposedAudioParameter.name");

            object controller = create.Invoke(null, new object[] { path });
            object master = masterProperty.GetValue(controller);

            var groups = new System.Collections.Generic.Dictionary<string, object>();
            var parameters = new System.Collections.Generic.List<(object guid, string name)>();

            void Expose(object group, string name)
            {
                object guid = volumeGuid.Invoke(group, null);
                object parameterPath = Activator.CreateInstance(pathType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { group, guid }, null);
                expose.Invoke(controller, new[] { parameterPath });
                parameters.Add((guid, name));
            }

            Expose(master, MasterParameter);
            foreach (var (name, parent, parameter) in Buses)
            {
                object group = createGroup.Invoke(controller, new object[] { name, false });
                addChild.Invoke(controller, new[] { group, parent == null ? master : groups[parent] });
                groups[name] = group;
                Expose(group, parameter);
            }

            // AddExposedParameter les pone un nombre genérico: se renombran por su GUID.
            var exposed = (Array)exposedProperty.GetValue(controller);
            for (int i = 0; i < exposed.Length; i++)
            {
                object entry = exposed.GetValue(i);
                object guid = exposedGuid.GetValue(entry);
                foreach (var (parameterGuid, name) in parameters)
                {
                    if (!Equals(parameterGuid, guid))
                        continue;
                    exposedName.SetValue(entry, name);
                    exposed.SetValue(entry, i);
                }
            }
            exposedProperty.SetValue(controller, exposed);

            EditorUtility.SetDirty((UnityEngine.Object)controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
        }

        static T Required<T>(T member, string name) where T : class
        {
            return member ?? throw new MissingMemberException($"UnityEditor.Audio: falta {name}");
        }
    }
}
