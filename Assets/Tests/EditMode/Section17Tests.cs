using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;

namespace Nova.Tests
{
    // T5-07: la suite de aceptación de la sección 17. Cada fila de la tabla es un test de aceptación,
    // y el test que la prueba lleva su número con [Section17]. Los casos viven junto al sistema que
    // prueban, no copiados aquí; la categoría «Seccion17» los junta en el Test Runner.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class Section17Attribute : CategoryAttribute
    {
        public const string Category = "Seccion17";

        public Section17Attribute(params int[] cases) : base(Category)
        {
            Cases = cases;
        }

        public int[] Cases { get; }
    }

    // Comprueba la suite contra la tabla de la spec, no contra un número escrito a mano
    // (desviación 103): si la sección 17 gana un caso, este test falla hasta que alguien lo pruebe.
    public class Section17Tests
    {
        static readonly Regex CaseRow = new Regex(@"^\|\s*(\d+)\s*\|");

        // Los números de la tabla de la sección 17, en el orden en que aparecen.
        static List<int> SpecCases()
        {
            string path = Path.Combine(Application.dataPath, "..", "mds", "NOVA_GameDesign_Spec.md");
            Assert.IsTrue(File.Exists(path), $"No está la spec en {path}.");

            var cases = new List<int>();
            bool inside = false;
            foreach (string line in File.ReadLines(path))
            {
                if (line.StartsWith("## "))
                {
                    if (inside)
                        break;
                    inside = line.StartsWith("## 17.");
                    continue;
                }
                if (!inside)
                    continue;

                Match match = CaseRow.Match(line);
                if (match.Success)
                    cases.Add(int.Parse(match.Groups[1].Value));
            }
            return cases;
        }

        static IEnumerable<MethodInfo> TaggedMethods() =>
            typeof(Section17Attribute).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                .Where(m => m.GetCustomAttribute<Section17Attribute>() != null);

        static string Name(MethodInfo method) => $"{method.DeclaringType.Name}.{method.Name}";

        [Test]
        public void TheSpecNumbersItsCasesFromOneWithoutGaps()
        {
            List<int> cases = SpecCases();
            Assert.IsNotEmpty(cases, "La tabla de la sección 17 no tiene filas.");
            CollectionAssert.AreEqual(Enumerable.Range(1, cases.Count), cases);
        }

        [Test]
        public void EveryCaseHasATest()
        {
            var covered = new HashSet<int>(TaggedMethods().SelectMany(m => m.GetCustomAttribute<Section17Attribute>().Cases));
            int[] missing = SpecCases().Where(c => !covered.Contains(c)).ToArray();
            Assert.IsEmpty(missing, "Casos de la sección 17 sin test: " + string.Join(", ", missing));
        }

        [Test]
        public void NoTestNamesACaseTheSpecDoesNotHave()
        {
            var cases = new HashSet<int>(SpecCases());
            string[] wrong = TaggedMethods()
                .Where(m => m.GetCustomAttribute<Section17Attribute>().Cases.Length == 0
                         || m.GetCustomAttribute<Section17Attribute>().Cases.Any(c => !cases.Contains(c)))
                .Select(Name).ToArray();
            Assert.IsEmpty(wrong, "Sin caso o con un caso que no existe: " + string.Join(", ", wrong));
        }

        // Una etiqueta sobre un método que NUnit no corre daría el caso por probado sin probarlo.
        [Test]
        public void EveryTaggedMethodIsATest()
        {
            string[] notTests = TaggedMethods()
                .Where(m => !m.GetCustomAttributes().Any(a => a is ISimpleTestBuilder || a is ITestBuilder))
                .Select(Name).ToArray();
            Assert.IsEmpty(notTests, "Etiquetados sin [Test] ni [TestCase]: " + string.Join(", ", notTests));
        }
    }
}
