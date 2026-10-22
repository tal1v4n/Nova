using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Nova.Data;
using Nova.Presentation;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T5-05b: los fondos con movimiento contra lo que exporta tools/gen_fondos_v2.py, que es la
    // referencia. Lo generado (Nova → Arte → Generar fondos) dice lo mismo que el JSON; el material,
    // renderizado de verdad, reproduce los fotogramas de ref/ —en ≥ 99.5 % de los píxeles: lo que
    // queda son empates de coma flotante contra el umbral de Bayer— y cumple el presupuesto de
    // contraste; la pausa congela el fondo; el ovni sale cada 45–90 s y nunca dos a la vez; y la fase
    // de una capa sigue exacta a una hora de partida.
    public class BackgroundTests
    {
        const string SpritesFolder = "Assets/Sprites/bg/fondos";
        const int Width = BackgroundDefinition.Width;
        const int Height = BackgroundDefinition.Height;
        const double MinIdentical = 0.995;

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        float timeScale;

        static IEnumerable<int> Maps => Enumerable.Range(1, 3);

        [SetUp]
        public void SetUp()
        {
            timeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = timeScale;
            foreach (UnityEngine.Object o in created)
            {
                if (o != null)
                    UnityEngine.Object.DestroyImmediate(o);
            }
            created.Clear();
        }

        // Lo exportado y lo generado los escriben un script y un menú, no el test: sin ellos, cada
        // test lo dice en vez de fallar con un null.
        static BackgroundExport Export(int map)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{SpritesFolder}/bg_section{map}.json");
            if (json == null)
                Assert.Fail($"Falta {SpritesFolder}/bg_section{map}.json: `py tools/gen_fondos_v2.py --exporta` y Nova → Arte → Sincronizar sprites desde art.");
            return JsonUtility.FromJson<BackgroundExport>(json.text);
        }

        static BackgroundDefinition Definition(int map)
        {
            string path = $"Assets/Data/Backgrounds/Map{map}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<BackgroundDefinition>(path);
            if (definition == null)
                Assert.Fail($"Falta {path}: ejecuta Nova → Arte → Generar fondos.");
            return definition;
        }

        static string ArtVersionFolder(BackgroundExport data) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "art", "bg", "fondos", $"v{data.version}");

        T Track<T>(T o) where T : UnityEngine.Object
        {
            created.Add(o);
            return o;
        }

        BackgroundView CreateView(BackgroundDefinition definition, out BackgroundFlyby flyby, int seed = 1)
        {
            var go = Track(new GameObject("Background"));
            var view = go.AddComponent<BackgroundView>();
            view.Configure(definition);
            flyby = go.AddComponent<BackgroundFlyby>();
            flyby.Configure(seed);
            return view;
        }

        [Test]
        public void Generated_assets_match_the_export([ValueSource(nameof(Maps))] int map)
        {
            BackgroundExport data = Export(map);
            BackgroundDefinition definition = Definition(map);

            Assert.AreEqual(map, data.map);
            Assert.AreEqual(map, definition.Map);
            Assert.IsNotNull(definition.Frame, "Sin el sprite de t = 0.");
            Assert.AreEqual(new Vector2(15f, 8.4375f), (Vector2)definition.Frame.bounds.size, "El sprite mide el tablero de T.8.");

            Material material = definition.Material;
            Assert.IsNotNull(material);
            Assert.AreEqual($"Nova/Background/Map{map}", material.shader.name);
            foreach (BackgroundExport.TextureEntry entry in data.textures)
            {
                Texture texture = material.GetTexture(entry.property);
                Assert.IsNotNull(texture, entry.property);
                Assert.AreEqual(Path.GetFileNameWithoutExtension(entry.file), texture.name, entry.property);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                Assert.AreEqual(entry.file.Contains("_static"), importer.sRGBTexture, $"{entry.file}: solo lo fijo es color.");
                Assert.IsFalse(importer.alphaIsTransparency, $"{entry.file}: el alfa es dato.");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, entry.file);
            }
            foreach (BackgroundExport.FloatEntry entry in data.floats)
                Assert.AreEqual(entry.value, material.GetFloat(entry.property), 1e-6f, entry.property);

            CollectionAssert.AreEqual(data.phases.Select(p => p.property), definition.Phases.Select(p => p.Property));
            CollectionAssert.AreEqual(data.lists.Select(l => l.property), definition.Lists.Select(l => l.Property));
            Assert.AreEqual(data.rocks.list.Length, definition.Rocks.Count);
            Assert.AreEqual(definition.Rocks.Count, definition.RockFrames.Count, "Un sprite por roca.");
            Assert.AreEqual(data.ufo.frames, definition.UfoFrames.Count, "La hoja del ovni entera.");

            for (int level = 3 * map - 2; level <= 3 * map; level++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"Assets/Data/Levels/Level{level}.asset");
                Assert.IsNotNull(asset, $"Falta Level{level}.");
                Assert.AreSame(definition, asset.Background, $"El nivel {level} lleva el fondo de su mapa.");
            }
        }

        [Test]
        public void Material_reproduces_the_reference_frames_within_the_budget([ValueSource(nameof(Maps))] int map)
        {
            BackgroundExport data = Export(map);
            BackgroundView view = CreateView(Definition(map), out _);

            var target = Track(new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB));
            var cameraObject = Track(new GameObject("Background Camera"));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Height / 2f / BackgroundDefinition.PixelsPerUnit;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            camera.targetTexture = target;
            var rendered = Track(new Texture2D(Width, Height, TextureFormat.RGBA32, false));
            var expected = Track(new Texture2D(2, 2, TextureFormat.RGBA32, false));

            foreach (BackgroundExport.ReferenceEntry reference in data.references)
            {
                view.Seek(reference.t);
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                rendered.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                rendered.Apply();
                RenderTexture.active = previous;

                string file = Path.Combine(ArtVersionFolder(data), reference.file);
                Assert.IsTrue(File.Exists(file), $"Falta {file}.");
                Assert.IsTrue(expected.LoadImage(File.ReadAllBytes(file)));
                Assert.AreEqual(Width, expected.width);

                Color32[] got = rendered.GetPixels32();
                Color32[] want = expected.GetPixels32();
                int identical = 0;
                for (int i = 0; i < got.Length; i++)
                {
                    // Un nivel de tolerancia por canal: la ida y vuelta del color por el espacio lineal.
                    if (Math.Abs(got[i].r - want[i].r) <= 1 && Math.Abs(got[i].g - want[i].g) <= 1 && Math.Abs(got[i].b - want[i].b) <= 1)
                        identical++;
                }
                double share = (double)identical / got.Length;
                Assert.GreaterOrEqual(share, MinIdentical, $"Mapa {map}, t = {reference.t}: {share:P3} de píxeles idénticos.");

                double worst = WorstWindow(got);
                Assert.LessOrEqual(worst, data.budget + 1e-3, $"Mapa {map}, t = {reference.t}: una ventana de 96×96 pasa del presupuesto.");
            }
        }

        // Media de luminancia de la ventana de 96×96 más clara (ventanas() de tools/nova_fondo.py).
        static double WorstWindow(Color32[] pixels)
        {
            const int cell = 96;
            var sum = new double[(Height + 1) * (Width + 1)];
            for (int y = 0; y < Height; y++)
            {
                double row = 0.0;
                for (int x = 0; x < Width; x++)
                {
                    Color32 c = pixels[y * Width + x];
                    row += 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;
                    sum[(y + 1) * (Width + 1) + x + 1] = sum[y * (Width + 1) + x + 1] + row;
                }
            }
            double worst = 0.0;
            for (int y = cell; y <= Height; y++)
            {
                for (int x = cell; x <= Width; x++)
                {
                    double total = sum[y * (Width + 1) + x] - sum[(y - cell) * (Width + 1) + x]
                                 - sum[y * (Width + 1) + x - cell] + sum[(y - cell) * (Width + 1) + x - cell];
                    worst = Math.Max(worst, total / (cell * cell));
                }
            }
            return worst;
        }

        [Test]
        public void Pause_freezes_the_background_and_its_sprites()
        {
            BackgroundView view = CreateView(Definition(2), out BackgroundFlyby flyby);
            view.Seek(12.5);
            flyby.Step(0f);
            Vector3[] rocks = flyby.Rocks.Select(r => r.transform.localPosition).ToArray();

            Time.timeScale = 0f;
            for (int i = 0; i < 10; i++)
            {
                view.Advance(Time.deltaTime);
                flyby.Step(Time.deltaTime);
            }

            Assert.AreEqual(12.5, view.Elapsed, "15.8: con la partida en pausa el fondo no avanza.");
            CollectionAssert.AreEqual(rocks, flyby.Rocks.Select(r => r.transform.localPosition).ToArray(), "Ni la grava.");
        }

        [Test]
        public void Ufo_leaves_every_45_to_90_seconds_and_never_two_at_once([ValueSource(nameof(Maps))] int map)
        {
            BackgroundDefinition definition = Definition(map);
            BackgroundView view = CreateView(definition, out BackgroundFlyby flyby, seed: 20261021 + map);
            const float step = 0.05f;
            var launches = new List<double>();
            double time = 0.0;
            bool wasActive = false;
            int launched = 0;

            while (time < 1800.0)
            {
                view.Advance(step);
                flyby.Step(step);
                time += step;
                if (flyby.UfoLaunches > launched)
                {
                    Assert.IsFalse(wasActive, "Salió un ovni con otro cruzando.");
                    Assert.AreEqual(launched + 1, flyby.UfoLaunches, "Dos ovnis en el mismo paso.");
                    launches.Add(time);
                    launched = flyby.UfoLaunches;
                }
                wasActive = flyby.UfoActive;
            }

            Assert.GreaterOrEqual(launches.Count, 18, "En media hora salen entre 20 y 40.");
            Assert.That(launches[0], Is.InRange(45.0 - step, 90.0 + step), "El primero también espera.");
            for (int i = 1; i < launches.Count; i++)
                Assert.That(launches[i] - launches[i - 1], Is.InRange(45.0 - step, 90.0 + step), $"Salida {i + 1}.");
        }

        [Test]
        public void Ufo_route_and_gravel_follow_the_generator([ValueSource(nameof(Maps))] int map)
        {
            BackgroundExport data = Export(map);
            BackgroundDefinition definition = Definition(map);

            foreach (BackgroundExport.UfoSample sample in data.ufoSamples)
            {
                bool visible = definition.UfoAt(sample.s, out Vector2Int pixel, out int frame);
                Assert.AreEqual(sample.visible, visible, $"s = {sample.s}");
                if (!visible)
                    continue;
                Assert.AreEqual(new Vector2Int(sample.x, sample.y), pixel, $"s = {sample.s}");
                Assert.AreEqual(sample.frame, frame, $"s = {sample.s}");
            }

            foreach (BackgroundExport.ReferenceEntry reference in data.references)
            {
                Assert.AreEqual(reference.rocks.Length / 2, definition.Rocks.Count);
                for (int i = 0; i < definition.Rocks.Count; i++)
                {
                    var expected = new Vector2Int(reference.rocks[2 * i], reference.rocks[2 * i + 1]);
                    Assert.AreEqual(expected, definition.RockAt(i, reference.t), $"Roca {i + 1}, t = {reference.t}.");
                }
            }
        }

        [Test]
        public void Each_layer_phase_stays_exact_after_an_hour([ValueSource(nameof(Maps))] int map)
        {
            const double hour = 3600.0;
            foreach (BackgroundPhase phase in Definition(map).Phases)
            {
                decimal raw = (decimal)phase.Offset + (decimal)phase.Rate * (decimal)hour;
                decimal period = (decimal)phase.Period;
                decimal expected = raw % period;
                if (expected < 0)
                    expected += period;
                float value = (float)phase.Evaluate(hour);
                Assert.That(value, Is.InRange(0f, (float)phase.Period), phase.Property);
                Assert.AreEqual((double)expected, value, 1e-4, $"{phase.Property}: la fase se reduce en double, no en el material.");
            }
        }
    }
}
