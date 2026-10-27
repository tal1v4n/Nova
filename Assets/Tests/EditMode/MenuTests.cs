using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Nova.Data;
using Nova.Systems;
using Nova.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nova.Tests
{
    // T5-06: el menú principal (13.8) y la carta estelar (13.9) leen el progreso y los nueve niveles,
    // y cargan la escena que toca. El progreso va en memoria: la suite no toca PlayerPrefs.
    public class MenuTests
    {
        sealed class MemoryStore : IProgressStore
        {
            public int Value = ProgressSaver.FirstLevel;
            public int Read(int fallback) => Value;
            public void Write(int value) => Value = value;
        }

        readonly List<Object> spawned = new List<Object>();
        readonly List<string> loadedScenes = new List<string>();
        MemoryStore store;
        int quits;

        static UiText Text => AssetDatabase.LoadAssetAtPath<UiText>("Assets/Data/UiText.asset");

        static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Jersey10 Raster56.asset");

        static Texture2D Sheet => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/world/link/world_link_sheet.png");

        static LevelDefinition[] Levels =>
            Enumerable.Range(1, 9).Select(n => AssetDatabase.LoadAssetAtPath<LevelDefinition>($"Assets/Data/Levels/Level{n}.asset")).ToArray();

        [SetUp]
        public void SetUp()
        {
            store = new MemoryStore();
            ProgressSaver.Store = store;
            loadedScenes.Clear();
            quits = 0;
            GameManager.Request(null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in spawned)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }
            spawned.Clear();
            ProgressSaver.Store = null;
            GameManager.Request(null);
        }

        MainMenuScreen Menu(int max)
        {
            store.Value = max;
            var go = new GameObject("MainMenu");
            spawned.Add(go);
            var menu = go.AddComponent<MainMenuScreen>();
            menu.Configure(Font, Text, Levels, null, Sheet, loadedScenes.Add, () => quits++);
            menu.Build();
            menu.Finish();
            return menu;
        }

        LevelSelectScreen Chart(int max)
        {
            store.Value = max;
            var go = new GameObject("LevelSelect");
            spawned.Add(go);
            var chart = go.AddComponent<LevelSelectScreen>();
            chart.Configure(Font, Text, Levels, null, Sheet, loadedScenes.Add);
            chart.Build();
            chart.Finish();
            return chart;
        }

        // --- Menú principal ---

        [Test]
        public void TheFirstTimeContinueIsPlayWithoutSubtitle()
        {
            MainMenuScreen menu = Menu(1);
            Assert.AreEqual(3, menu.OptionCount);
            Assert.AreEqual(Text.Play, menu.OptionLabel(0));
            Assert.IsNull(menu.OptionSubtitle(0));
            Assert.AreEqual(Text.StarMap, menu.OptionLabel(1));
            Assert.AreEqual(Text.Quit, menu.OptionLabel(2));
            Assert.AreEqual(0, menu.Active, "Al entrar, la primera.");
        }

        [Test]
        public void ContinueNamesTheMaxUnlockedAndLoadsItInGame()
        {
            MainMenuScreen menu = Menu(5);
            Assert.AreEqual(Text.ContinueGame, menu.OptionLabel(0));
            Assert.AreEqual(Text.ContinueLine(2, 5), menu.OptionSubtitle(0));

            menu.Choose(MainMenuScreen.Choice.Continue);
            CollectionAssert.AreEqual(new[] { GameManager.GameScene }, loadedScenes, "Directo a Game, sin pasar por la carta.");
            Assert.AreEqual(5, GameManager.Requested.Number);
        }

        [Test]
        public void WithTheNineWonContinueLoadsTheNinth()
        {
            MainMenuScreen menu = Menu(ProgressSaver.AllConquered);
            Assert.AreEqual(Text.ContinueLine(3, 9), menu.OptionSubtitle(0));
            menu.Choose(MainMenuScreen.Choice.Continue);
            Assert.AreEqual(9, GameManager.Requested.Number);
        }

        [Test]
        public void StarMapOpensTheChartAndQuitQuits()
        {
            MainMenuScreen menu = Menu(3);
            menu.Choose(MainMenuScreen.Choice.StarMap);
            CollectionAssert.AreEqual(new[] { GameManager.LevelSelectScene }, loadedScenes);
            menu.Choose(MainMenuScreen.Choice.Quit);
            Assert.AreEqual(1, quits);
        }

        [Test]
        public void TheActiveOptionFollowsTheCursorAndKeepsTheLast()
        {
            MainMenuScreen menu = Menu(3);
            float before = menu.Line.Length;
            menu.Hover(2);
            Assert.AreEqual(2, menu.Active);
            menu.Finish();
            Assert.AreNotEqual(before, menu.Line.Length, "La línea va a la opción activa.");
            menu.Hover(-1);
            Assert.AreEqual(2, menu.Active, "Sin cursor encima, la última que lo estuvo.");
        }

        // --- Carta estelar ---

        [Test]
        public void TheChartDrawsWonNextAndLockedFromTheMax()
        {
            LevelSelectScreen chart = Chart(5);
            for (int i = 1; i <= 4; i++)
                Assert.AreEqual(LevelSelectScreen.NodeState.Conquered, chart.StateOf(i), $"Nivel {i}");
            Assert.AreEqual(LevelSelectScreen.NodeState.Next, chart.StateOf(5));
            for (int i = 6; i <= 9; i++)
                Assert.AreEqual(LevelSelectScreen.NodeState.Locked, chart.StateOf(i), $"Nivel {i}");
            Assert.AreEqual(Text.ConqueredCount(4), chart.ConqueredText);
            Assert.IsTrue(chart.SegmentLit(4), "Plasma hasta el siguiente.");
            Assert.IsFalse(chart.SegmentLit(5), "Discontinua después.");
        }

        [Test]
        public void TheReportStartsOnTheNextAndLaunchesIt()
        {
            LevelSelectScreen chart = Chart(5);
            Assert.AreEqual(5, chart.Selected);
            Assert.IsTrue(chart.CanLaunch);
            CollectionAssert.Contains(chart.ReportTexts, Text.ReportMap(2));
            CollectionAssert.DoesNotContain(chart.ReportTexts, Text.Introduced(2), "Nuevo: solo en el primer nivel del mapa.");

            chart.Launch();
            CollectionAssert.AreEqual(new[] { GameManager.GameScene }, loadedScenes);
            Assert.AreEqual(5, GameManager.Requested.Number);
        }

        [Test]
        public void ALockedLevelCanBeReadButNotLaunched()
        {
            LevelSelectScreen chart = Chart(5);
            chart.Select(7);
            Assert.AreEqual(7, chart.Selected);
            Assert.IsFalse(chart.CanLaunch);
            CollectionAssert.Contains(chart.ReportTexts, Text.Locked);
            CollectionAssert.Contains(chart.ReportTexts, Text.WinToUnlock(5));
            CollectionAssert.Contains(chart.ReportTexts, Text.Introduced(3));
            CollectionAssert.Contains(chart.ReportTexts, Text.ConversionHeader(Text.YesNo(true)));

            chart.Launch();
            CollectionAssert.IsEmpty(loadedScenes);
            Assert.IsNull(GameManager.Requested);
        }

        [Test]
        public void AnOldLevelCanBeReplayed()
        {
            LevelSelectScreen chart = Chart(5);
            chart.Select(1);
            Assert.IsTrue(chart.CanLaunch);
            CollectionAssert.Contains(chart.ReportTexts, Text.Introduced(1));
            CollectionAssert.Contains(chart.ReportTexts, Text.ConversionHeader(Text.YesNo(false)));
            chart.Launch();
            Assert.AreEqual(1, GameManager.Requested.Number);
        }

        [Test]
        public void WithTheNineWonTheChartSelectsTheNinth()
        {
            LevelSelectScreen chart = Chart(ProgressSaver.AllConquered);
            Assert.AreEqual(9, chart.Selected);
            Assert.AreEqual(Text.ConqueredCount(9), chart.ConqueredText);
            for (int i = 1; i <= 9; i++)
                Assert.AreEqual(LevelSelectScreen.NodeState.Conquered, chart.StateOf(i));
            for (int j = 1; j <= 8; j++)
                Assert.IsTrue(chart.SegmentLit(j));
        }

        [Test]
        public void BackToMenuLoadsTheMainMenu()
        {
            LevelSelectScreen chart = Chart(2);
            chart.BackToMenu();
            CollectionAssert.AreEqual(new[] { GameManager.MainMenuScene }, loadedScenes);
        }

        // --- La fuente ---

        // Un carácter que Jersey 10 no tiene sale como un cuadro, sin error: le pasó a ◂, ▸, ▪ y –.
        [Test]
        public void EveryUiTextFitsJersey10()
        {
            TMP_FontAsset font = Font;
            UiText text = Text;
            var placeholder = new System.Text.RegularExpressions.Regex(@"\{\d+(:[^}]*)?\}");
            var missing = new List<string>();
            foreach (System.Reflection.FieldInfo field in typeof(UiText).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                object value = field.GetValue(text);
                IEnumerable<string> values = value is string one ? new[] { one } : value as string[] ?? new string[0];
                foreach (string s in values)
                {
                    foreach (char c in placeholder.Replace(s ?? string.Empty, string.Empty))
                    {
                        if (!char.IsWhiteSpace(c) && !font.HasCharacter(c, false, false))
                            missing.Add($"{field.Name}: «{c}» (U+{(int)c:X4})");
                    }
                }
            }
            CollectionAssert.IsEmpty(missing);
        }

        // --- La línea de plasma en un lienzo ---

        [Test]
        public void ThePlasmaLineAnchorsItsTilesAtTheTip()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            spawned.Add(go);
            // 600 px de lienzo son 300 del arte: nacimiento, la tesela entera que toca la punta, la
            // recortada junto al nacimiento y la punta.
            UiPlasma line = UiPlasma.Create(go.transform, Sheet, new Vector2(100f, 100f), new Vector2(700f, 100f));
            Assert.AreEqual(300f, line.Length);
            Assert.AreEqual(4, line.PieceCount);

            line.Step(1f / UiPlasma.Fps + 0.001f);
            if (Sheet != null)
                Assert.AreEqual(1, line.Frame, "El bucle corre a 12 fps.");
        }
    }
}
