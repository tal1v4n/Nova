using System;
using NUnit.Framework;
using Nova.Core;

namespace Nova.Tests
{
    // T1-01: los valores que la especificación fija por número o por lista cerrada.
    public class CoreEnumsTests
    {
        // Sección 3: Faction { Neutral = 0, Player = 1, Enemy = 2 }. El Jugador siempre es la 1.
        [Test]
        public void FactionValuesMatchSpec()
        {
            Assert.AreEqual(0, (int)Faction.Neutral);
            Assert.AreEqual(1, (int)Faction.Player);
            Assert.AreEqual(2, (int)Faction.Enemy);
        }

        [TestCase(typeof(Faction), 3)]      // Sección 3
        [TestCase(typeof(NodeType), 4)]     // Glosario: Asteroide, Estrella, Púlsar, Magnetar
        [TestCase(typeof(NodeState), 8)]    // 14.1
        [TestCase(typeof(PowerType), 3)]    // Sección 7
        [TestCase(typeof(ImpactResult), 5)] // 15.3, sin Destroyed (T.7 #40)
        public void EnumHasExactlyTheSpecifiedValues(Type enumType, int expected)
        {
            Assert.AreEqual(expected, Enum.GetValues(enumType).Length);
        }
    }
}
