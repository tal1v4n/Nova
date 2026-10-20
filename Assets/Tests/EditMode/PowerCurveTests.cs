using NUnit.Framework;
using Nova.Core;

namespace Nova.Tests
{
    // T5-05: PowerCurve (13.11, 18 #40), sin Unity. La proporción del Jugador, las 64 columnas y las
    // dos marcas: el control que cambia de manos en la victoria y el mejor momento en la derrota.
    public class PowerCurveTests
    {
        [TestCase(20, 20, 0.5f)]
        [TestCase(30, 10, 0.75f)]
        [TestCase(0, 10, 0f)]
        [TestCase(0, 0, 0.5f)]
        public void TheShareIsThePlayerOverBothBars(int player, int enemy, float expected)
        {
            Assert.AreEqual(expected, PowerCurve.Share(player, enemy), 1e-6f);
        }

        [Test]
        public void ManySamplesAreAveragedIntoTheColumns()
        {
            var samples = new float[128];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = i % 2 == 0 ? 0f : 1f;
            float[] columns = PowerCurve.Group(samples);
            Assert.AreEqual(PowerCurve.Columns, columns.Length);
            foreach (float column in columns)
                Assert.AreEqual(0.5f, column, 1e-6f, "Dos muestras por columna.");
        }

        [Test]
        public void FewSamplesSpreadOverSeveralColumns()
        {
            float[] columns = PowerCurve.Group(new[] { 0.2f, 0.8f }, 4);
            CollectionAssert.AreEqual(new[] { 0.2f, 0.2f, 0.8f, 0.8f }, columns);
            Assert.AreEqual(0, PowerCurve.ColumnOf(0, 2, 4));
            Assert.AreEqual(2, PowerCurve.ColumnOf(1, 2, 4));
        }

        [Test]
        public void WithoutSamplesThereIsNoCurve()
        {
            Assert.IsEmpty(PowerCurve.Group(new float[0]));
            Assert.AreEqual(-1, PowerCurve.HoldIndex(new float[0]));
            Assert.AreEqual(-1, PowerCurve.BestIndex(new float[0]));
        }

        [Test]
        public void TheHoldMarkIsWhereThePlayerPassesHalfForGood()
        {
            Assert.AreEqual(3, PowerCurve.HoldIndex(new[] { 0.4f, 0.6f, 0.5f, 0.55f, 0.9f }),
                "Pasó de la mitad en la 1, pero volvió a 0.5: no se quedó.");
            Assert.AreEqual(-1, PowerCurve.HoldIndex(new[] { 0.6f, 0.4f }));
        }

        [Test]
        public void TheBestMomentIsTheFirstHighestShare()
        {
            Assert.AreEqual(1, PowerCurve.BestIndex(new[] { 0.4f, 0.7f, 0.2f, 0.7f }));
        }
    }
}
