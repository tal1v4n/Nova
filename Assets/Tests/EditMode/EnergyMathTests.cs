using System;
using NUnit.Framework;
using Nova.Core;

namespace Nova.Tests
{
    // T1-02. Los valores de balance que aparecen aquí son los de 16.1, escritos a mano
    // a propósito: el test comprueba la especificación, no el asset.
    public class EnergyMathTests
    {
        const float MagnetarResistance = 0.5f; // 16.1

        [TestCase(NodeType.Asteroid, 1f)]
        [TestCase(NodeType.Star, 1f)]
        [TestCase(NodeType.Pulsar, 1f)]
        [TestCase(NodeType.Magnetar, 0.5f)]
        public void OnlyMagnetarResistsHostileImpacts(NodeType type, float expected)
        {
            Assert.AreEqual(expected, EnergyMath.HostileMultiplier(type, MagnetarResistance));
        }

        [TestCase(10, 1f, 10)]
        [TestCase(120, 0.5f, 60)] // 6.2: 120 contra un Magnetar son 60 efectivos
        [TestCase(7, 0.5f, 3)]    // Redondeo hacia abajo
        [TestCase(1, 0.5f, 0)]
        [TestCase(0, 1f, 0)]
        public void EffectiveChargeRoundsDown(int charge, float multiplier, int expected)
        {
            Assert.AreEqual(expected, EnergyMath.EffectiveCharge(charge, multiplier));
        }

        // 6.2: el multiplicador se aplica una vez sobre la Carga total, no disparo a disparo.
        [Test]
        public void MultiplierAppliesOnceOverTotalCharge()
        {
            int once = EnergyMath.EffectiveCharge(3 + 3 + 3, MagnetarResistance);
            int perShot = 3 * EnergyMath.EffectiveCharge(3, MagnetarResistance);
            Assert.AreEqual(4, once);
            Assert.AreNotEqual(perShot, once);
        }

        // Tabla de 6.4.
        [TestCase(10, 1f, 10)]    // Asteroide neutral
        [TestCase(50, 1f, 50)]    // Estrella nivel 5 llena
        [TestCase(100, 1f, 100)]  // Púlsar lleno
        [TestCase(100, 0.5f, 200)] // Magnetar lleno
        [TestCase(0, 1f, 1)]      // Cualquier nodo a 0
        [TestCase(0, 0.5f, 1)]
        public void CaptureCostMatchesSpecTable(int energy, float multiplier, int expected)
        {
            Assert.AreEqual(expected, EnergyMath.CaptureCost(energy, multiplier));
        }

        // CaptureCost es la menor Carga que captura: con una menos, no llega.
        [Test]
        public void CaptureCostIsTheMinimumCapturingCharge()
        {
            float[] multipliers = { 1f, 0.5f, 0.3f, 0.75f, 1.5f };
            foreach (float m in multipliers)
            {
                for (int energy = 0; energy <= 100; energy++)
                {
                    int cost = EnergyMath.CaptureCost(energy, m);
                    Assert.GreaterOrEqual(EnergyMath.EffectiveCharge(cost, m), energy, $"m={m}, E={energy}");
                    if (cost > 1)
                        Assert.Less(EnergyMath.EffectiveCharge(cost - 1, m), energy, $"m={m}, E={energy}");
                }
            }
        }

        // 5.2: 50 % redondeado hacia abajo; el 50 % de 1 es 0 y no se envía.
        [TestCase(50, false, 25)]
        [TestCase(7, false, 3)]
        [TestCase(2, false, 1)]
        [TestCase(1, false, 0)]
        [TestCase(0, false, 0)]
        [TestCase(7, true, 7)]
        [TestCase(1, true, 1)]
        [TestCase(0, true, 0)]
        public void SendAmountFollowsSpec(int energy, bool fullSend, int expected)
        {
            Assert.AreEqual(expected, EnergyMath.SendAmount(energy, fullSend));
        }

        // 5.3: el excedente sobre el Tope se descarta.
        [TestCase(40, 5, 50, 45)]
        [TestCase(40, 30, 50, 50)]
        [TestCase(50, 1, 50, 50)]
        public void AddCappedDiscardsOverflow(int energy, int amount, int cap, int expected)
        {
            Assert.AreEqual(expected, EnergyMath.AddCapped(energy, amount, cap));
        }

        [Test]
        public void NegativeInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EnergyMath.EffectiveCharge(-1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => EnergyMath.EffectiveCharge(1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => EnergyMath.CaptureCost(-1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => EnergyMath.SendAmount(-1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => EnergyMath.AddCapped(0, -1, 50));
        }
    }
}
