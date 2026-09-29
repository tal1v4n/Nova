using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T1-08: StarGenerator (4.2) sobre el tick fijo de 50 Hz (15.10).
    public class StarGeneratorTests
    {
        const float Tick = 0.02f;

        GameObject go;
        NodeController node;
        StarGenerator generator;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Star");
            node = go.AddComponent<NodeController>();
            generator = go.AddComponent<StarGenerator>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        void Ticks(int count)
        {
            for (int i = 0; i < count; i++)
                generator.Tick(Tick);
        }

        // 15.10 y 18 #22: toda la lógica corre en FixedUpdate a 50 Hz.
        [Test]
        public void ProjectRunsLogicAt50Hz()
        {
            Assert.AreEqual(0.02f, Time.fixedDeltaTime, 1e-7f);
        }

        // Nivel 1: 2.00 s por +1, 100 ticks.
        [Test]
        public void GeneratesOneEnergyPerInterval()
        {
            node.Initialize(Definition("Star"), Faction.Player, 0);

            Ticks(99);
            Assert.AreEqual(0, node.Energy);
            Ticks(1);
            Assert.AreEqual(1, node.Energy);
            Ticks(100);
            Assert.AreEqual(2, node.Energy);
        }

        // «Nunca fracciones» (4.2): cada +1 es un solo evento de una unidad.
        [Test]
        public void EnergyGrowsInWholeSteps()
        {
            node.Initialize(Definition("Star"), Faction.Player, 0);
            var seen = new List<(int, int)>();
            node.OnEnergyChanged += (a, b) => seen.Add((a, b));

            Ticks(300);

            CollectionAssert.AreEqual(new[] { (0, 1), (1, 2), (2, 3) }, seen);
        }

        // Nivel 4: 1.05 s no es múltiplo de 20 ms. El primer +1 cae en el tick 53 (1.06 s) y el
        // sobrante se arrastra, así que el segundo cae en el 105 (2.10 s) y no en el 106.
        [Test]
        public void IntervalsThatAreNotTickMultiplesKeepTheirAverageRate()
        {
            node.Initialize(Definition("Star"), Faction.Player, 0, 4);

            Ticks(52);
            Assert.AreEqual(0, node.Energy);
            Ticks(1);
            Assert.AreEqual(1, node.Energy);
            Ticks(51);
            Assert.AreEqual(1, node.Energy);
            Ticks(1);
            Assert.AreEqual(2, node.Energy);
        }

        // Columna «Tiempo 0→50» de 4.2.
        [TestCase(1, 100f)]
        [TestCase(2, 80f)]
        [TestCase(3, 65f)]
        [TestCase(4, 52.5f)]
        [TestCase(5, 42.5f)]
        public void FillsFromZeroToCapInTheTimeOf4_2(int level, float seconds)
        {
            node.Initialize(Definition("Star"), Faction.Player, 0, level);
            int ticks = Mathf.CeilToInt(seconds / Tick - 1e-3f);

            Ticks(ticks - 1);
            Assert.AreEqual(49, node.Energy);
            Ticks(1);
            Assert.AreEqual(50, node.Energy);
        }

        [Test]
        public void StopsAtTheCap()
        {
            node.Initialize(Definition("Star"), Faction.Player, 50);
            int events = 0;
            node.OnEnergyChanged += (_, __) => events++;

            Ticks(1000);

            Assert.AreEqual(50, node.Energy);
            Assert.AreEqual(0, events);
        }

        // Lleno, el temporizador se detiene y conserva su progreso: al bajar del Tope sigue donde iba.
        [Test]
        public void TimerPausesWhileFullAndResumes()
        {
            node.Initialize(Definition("Star"), Faction.Player, 49);
            var energy = go.GetComponent<NodeEnergy>();

            Ticks(50);                 // 1.00 s de los 2.00 s del nivel 1
            energy.Add(1);             // se llena por un refuerzo
            Ticks(500);
            Assert.AreEqual(1f, generator.Progress, 1e-6f);

            energy.Subtract(1);
            Ticks(49);
            Assert.AreEqual(49, node.Energy);
            Ticks(1);
            Assert.AreEqual(50, node.Energy);
        }

        // Al subir de nivel, el progreso acumulado se mide contra el intervalo nuevo.
        [Test]
        public void LevelChangeUsesTheNewInterval()
        {
            NodeDefinition star = Definition("Star");
            node.Initialize(star, Faction.Player, 0);

            Ticks(80);                 // 1.60 s: aún no llega a los 2.00 s del nivel 1
            Assert.AreEqual(0, node.Energy);

            go.GetComponent<NodeLevelComponent>().Apply(star, 2);   // 1.60 s
            Ticks(1);
            Assert.AreEqual(1, node.Energy);
        }

        [TestCase("Asteroid")]
        [TestCase("Pulsar")]
        [TestCase("Magnetar")]
        public void OnlyAStarGenerates(string type)
        {
            node.Initialize(Definition(type), Faction.Player, 0);
            Ticks(1000);
            Assert.AreEqual(0, node.Energy);
            Assert.AreEqual(0f, generator.Progress);
        }

        [Test]
        public void UninitializedNodeDoesNothing()
        {
            Assert.DoesNotThrow(() => Ticks(10));
            Assert.AreEqual(0f, generator.Progress);
        }

        // Todo nodo funciona igual sea del bando que sea (14.0 #3): la Estrella enemiga genera igual.
        [Test]
        public void EnemyStarGenerates()
        {
            node.Initialize(Definition("Star"), Faction.Enemy, 0);
            Ticks(100);
            Assert.AreEqual(1, node.Energy);
        }

        // «Neutral: no actúa, no genera» (tabla de 3). No debería existir, pero no genera.
        [Test]
        public void NeutralStarDoesNotGenerate()
        {
            node.Initialize(Definition("Star"), Faction.Neutral, 0);
            Ticks(1000);
            Assert.AreEqual(0, node.Energy);
        }

        [Test]
        public void NegativeTimeIsRejected()
        {
            node.Initialize(Definition("Star"), Faction.Player, 0);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => generator.Tick(-Tick));
        }
    }
}
