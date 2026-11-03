using UnityEditor;
using UnityEngine;

namespace Nova.Editor
{
    // T6-13 y T6-14: ajustes de importación de NOVA_Sonido.md, S.2.1, para lo que entra en Assets/Audio/.
    // La música (Music/) va en Streaming con Vorbis a calidad ~70 %: 360 s de ciclo descomprimidos ocuparían unos
    // 140 MB de memoria. Los SFX (SFX/) van en Decompress On Load con ADPCM y en mono: son cortos, suenan a la vez
    // y el lado lo pone el panorama de AudioDirector, no el archivo.
    // Ninguno se normaliza, pero eso no tiene API: lo fija el .meta que escribe AudioAssets.
    public class AudioImportRules : AssetPostprocessor
    {
        const string MusicRoot = "Assets/Audio/Music/";
        const string SfxRoot = "Assets/Audio/SFX/";
        const float MusicQuality = 0.7f;

        // Subirla reimporta todo lo que pasa por aquí.
        const uint Version = 2;

        public override uint GetVersion() => Version;

        void OnPreprocessAudio()
        {
            bool music = assetPath.StartsWith(MusicRoot);
            if (!music && !assetPath.StartsWith(SfxRoot))
                return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = !music;
            importer.loadInBackground = music;

            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            sample.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            if (music)
                sample.quality = MusicQuality;
            sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = sample;
        }
    }
}
