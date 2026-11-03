using NUnit.Framework;
using Nova.Data;
using Nova.Presentation;
using Nova.Systems;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T6-13: la música de los menús (NOVA_Sonido.md, S.5.2, S.6 y S.7 #6). Qué hace en cada cambio de escena
    // y qué versión del ciclo toca al volver de una partida, con MusicFlow, que no necesita AudioSource.
    public class MusicTests
    {
        const string Menu = GameManager.MainMenuScene;
        const string Chart = GameManager.LevelSelectScene;
        const string Game = GameManager.GameScene;

        const string SettingsPath = "Assets/Data/Audio/MusicSettings.asset";

        static MusicFlow NewFlow() => new MusicFlow(Game, new[] { Menu, Chart }, 120.0, 3);

        static MusicCue Enter(MusicFlow flow, string scene, double cyclePosition = 0.0) =>
            flow.OnSceneActivated(scene, cyclePosition);

        [TestCase(Menu)]
        [TestCase(Chart)]
        public void Opening_the_game_on_a_menu_plays_the_intro(string scene)
        {
            Assert.AreEqual(MusicCueKind.StartWithIntro, Enter(NewFlow(), scene).Kind);
        }

        [Test]
        public void Menu_and_chart_do_not_cut_the_music()
        {
            var flow = NewFlow();
            Enter(flow, Menu);

            Assert.AreEqual(MusicCueKind.None, Enter(flow, Chart, 30.0).Kind);
            Assert.AreEqual(MusicCueKind.None, Enter(flow, Menu, 60.0).Kind);
            Assert.AreEqual(MusicCueKind.None, Enter(flow, Chart, 200.0).Kind);
            Assert.IsTrue(flow.IsPlaying);
        }

        [Test]
        public void The_same_scene_twice_does_nothing()
        {
            // MusicDirector trata en Start la escena en la que nace, y activeSceneChanged puede volver a avisar.
            var flow = NewFlow();
            Enter(flow, Menu);

            Assert.AreEqual(MusicCueKind.None, Enter(flow, Menu).Kind);
        }

        [TestCase(Menu)]
        [TestCase(Chart)]
        public void Launching_a_level_fades_out(string from)
        {
            var flow = NewFlow();
            Enter(flow, from);

            Assert.AreEqual(MusicCueKind.FadeOut, Enter(flow, Game, 40.0).Kind);
            Assert.IsFalse(flow.IsPlaying);
        }

        [Test]
        public void Game_to_game_keeps_silence()
        {
            // Reiniciar, Reintentar, Repetir y Siguiente (S.5.2).
            var flow = NewFlow();
            Enter(flow, Menu);
            Enter(flow, Game, 40.0);

            Assert.AreEqual(MusicCueKind.None, Enter(flow, Game).Kind);
            Assert.AreEqual(MusicCueKind.None, Enter(flow, Game).Kind);
        }

        // Las versiones empiezan a 0 (v3), 120 (v1) y 240 s (v2), y al volver entra la siguiente (S.7 #6).
        [TestCase(0.0, 120.0)]    // lanzado durante la intro: sonaba la v3
        [TestCase(50.0, 120.0)]   // v3 → v1
        [TestCase(119.9, 120.0)]
        [TestCase(120.0, 240.0)]  // v1 → v2
        [TestCase(239.0, 240.0)]
        [TestCase(240.0, 0.0)]    // v2 → v3: el ciclo vuelve a empezar
        [TestCase(359.9, 0.0)]
        public void Returning_enters_the_next_version_without_intro(double launchedAt, double expected)
        {
            var flow = NewFlow();
            Enter(flow, Chart);
            Enter(flow, Game, launchedAt);

            MusicCue cue = Enter(flow, Chart);

            Assert.AreEqual(MusicCueKind.ResumeCycle, cue.Kind);
            Assert.AreEqual(expected, cue.CycleTime, 1e-9);
            Assert.IsTrue(flow.IsPlaying);
        }

        [TestCase(Menu)]
        [TestCase(Chart)]
        public void Returning_to_either_screen_resumes(string to)
        {
            var flow = NewFlow();
            Enter(flow, Chart);
            Enter(flow, Game, 10.0);

            Assert.AreEqual(MusicCueKind.ResumeCycle, Enter(flow, to).Kind);
        }

        [Test]
        public void Each_return_brings_a_different_version()
        {
            var flow = NewFlow();
            Enter(flow, Menu);

            Enter(flow, Game, 30.0);
            double first = Enter(flow, Chart).CycleTime;
            Enter(flow, Game, first + 5.0);
            double second = Enter(flow, Chart).CycleTime;
            Enter(flow, Game, second + 5.0);
            double third = Enter(flow, Chart).CycleTime;

            Assert.AreEqual(new[] { 120.0, 240.0, 0.0 }, new[] { first, second, third });
        }

        [Test]
        public void The_intro_only_sounds_once()
        {
            var flow = NewFlow();
            Enter(flow, Menu);
            Enter(flow, Game, 70.0);

            Assert.AreNotEqual(MusicCueKind.StartWithIntro, Enter(flow, Menu).Kind);
        }

        [Test]
        public void Other_scenes_do_nothing()
        {
            // Boot y las escenas de prueba no tocan la música; empezar en Game (desde el editor) tampoco.
            var flow = NewFlow();

            Assert.AreEqual(MusicCueKind.None, Enter(flow, "Boot").Kind);
            Assert.AreEqual(MusicCueKind.None, Enter(flow, Game).Kind);
            Assert.AreEqual(MusicCueKind.None, Enter(flow, "Fase5").Kind);
            Assert.AreEqual(MusicCueKind.StartWithIntro, Enter(flow, Menu).Kind);
            Assert.AreEqual(MusicCueKind.None, Enter(flow, "Fase5").Kind);
            Assert.IsTrue(flow.IsPlaying);
        }

        [TestCase(-1.0, 0)]
        [TestCase(double.NaN, 0)]
        [TestCase(360.0, 0)]
        [TestCase(480.0, 1)]
        [TestCase(1000.0, 2)]
        public void Version_wraps_around_the_cycle(double position, int expected)
        {
            Assert.AreEqual(expected, NewFlow().VersionAt(position));
        }

        [Test]
        public void Settings_default_to_the_spec()
        {
            var settings = ScriptableObject.CreateInstance<MusicSettings>();
            try
            {
                Assert.AreEqual(12.0, settings.CycleStartInIntro, 1e-9, "El ciclo arranca a los 12 s de la intro (S.5.2)");
                Assert.AreEqual(120.0, settings.VersionLength, 1e-9, "Cada versión dura 120 s (S.5.2)");
                Assert.AreEqual(3, settings.VersionCount, "v3 → v1 → v2 (S.5.2)");
                Assert.AreEqual(1.0f, settings.FadeOutSeconds, 1e-6f, "Fundido de salida al lanzar (S.7 #6)");
                Assert.AreEqual(1.5f, settings.FadeInSeconds, 1e-6f, "Fundido de entrada al volver (S.7 #6)");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Settings_asset_has_the_two_game_files()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MusicSettings>(SettingsPath);
            Assert.IsNotNull(settings, $"Falta {SettingsPath}: Nova → Audio → Importar audio y crear el mezclador");
            Assert.IsTrue(settings.HasClips, "Faltan los clips: vuelve a lanzar Nova → Audio → Importar audio y crear el mezclador");

            AudioClip intro = settings.Intro;
            AudioClip cycle = settings.Cycle;
            Assert.AreEqual(48000, cycle.frequency);
            Assert.AreEqual(2, cycle.channels);
            Assert.AreEqual(settings.VersionLength * settings.VersionCount * cycle.frequency, cycle.samples, 0.5,
                "El ciclo mide 360.000 s exactos");
            Assert.AreEqual(18.0 * intro.frequency, intro.samples, 0.5, "La intro dura 18 s: 12 de intro y 6 de cola");
            foreach (AudioClip clip in new[] { intro, cycle })
            {
                Assert.AreEqual(AudioClipLoadType.Streaming, clip.loadType, $"{clip.name}: la música va en Streaming (S.2.1)");
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                Assert.AreEqual(AudioCompressionFormat.Vorbis, importer.defaultSampleSettings.compressionFormat, $"{clip.name}: Vorbis (S.2.1)");
                // En el .meta se llama normalize, pero el YAML quita el prefijo m_ del nombre serializado.
                var serialized = new SerializedObject(importer);
                SerializedProperty normalize = serialized.FindProperty("m_Normalize") ?? serialized.FindProperty("normalize");
                Assert.IsNotNull(normalize, "El AudioImporter ya no tiene la propiedad m_Normalize");
                Assert.IsFalse(normalize.boolValue,
                    $"{clip.name}: sin normalizar, o la intro y el ciclo saltan de volumen al encadenarse (S.5.2)");
            }

            Assert.IsNotNull(settings.Output, "La música sale por el bus Music del mezclador (S.2.3)");
            Assert.AreEqual("Music", settings.Output.name);
        }
    }
}
