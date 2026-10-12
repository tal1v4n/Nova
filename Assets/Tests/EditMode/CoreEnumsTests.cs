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
        [TestCase(typeof(NodeState), 3)]    // 14.1, estados de base (T.7 #43)
        [TestCase(typeof(NodeStateFlags), 6)] // 14.1, los cinco que se suman más None
        [TestCase(typeof(PowerType), 3)]    // Sección 7
        [TestCase(typeof(ImpactResult), 5)] // 15.3, sin Destroyed (T.7 #40)
        public void EnumHasExactlyTheSpecifiedValues(Type enumType, int expected)
        {
            Assert.AreEqual(expected, Enum.GetValues(enumType).Length);
        }

        // T.7 #43: cada estado que se suma es un bit propio, para que convivan los que 14.6 admite.
        [Test]
        public void StateFlagsAreSingleBits()
        {
            foreach (NodeStateFlags flag in Enum.GetValues(typeof(NodeStateFlags)))
            {
                int value = (int)flag;
                if (flag == NodeStateFlags.None)
                    Assert.AreEqual(0, value);
                else
                    Assert.AreEqual(0, value & (value - 1), $"{flag} no es un solo bit.");
            }
        }
    }
}
