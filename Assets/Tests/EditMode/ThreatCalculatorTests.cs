using System;
using NUnit.Framework;
using Nova.Core;

namespace Nova.Tests
{
    // T2-08: ThreatCalculator (15.3), la condición de susto de 14.2 y la amenaza de 12.4.
    // Valores de 16.1: protón a 2.5 u/s, umbral de susto 1.5 s y resistencia del Magnetar ×0.5.
    // A 2.5 u/s, el umbral cae a 3.75 u del centro del nodo.
    public class ThreatCalculatorTests
    {
        const float Speed = 2.5f;
        const float Threshold = 1.5f;
        const float Resistance = 0.5f;

        static NodeSnapshot Node(Faction faction, NodeType type, int energy) =>
            new NodeSnapshot(faction, type, energy, 100, level: type == NodeType.Asteroid ? 0 : 1);

        static IncomingProton P(Faction faction, int charge, float distance) =>
            new IncomingProton(faction, charge, distance);

        static bool Scared(NodeSnapshot node, params IncomingProton[] incoming) =>
            ThreatCalculator.IsScared(node, incoming, Resistance, Speed, Threshold);

        static ThreatAssessment Assess(NodeSnapshot node, params IncomingProton[] incoming) =>
            ThreatCalculator.Assess(node, incoming, Resistance, Speed);

        // --- Tiempo de llegada ---

        [Test]
        public void TimeToArrivalIsDistanceOverSpeed()
        {
            Assert.AreEqual(1.5f, ThreatCalculator.TimeToArrival(3.75f, Speed), 1e-6f);
            Assert.AreEqual(0f, ThreatCalculator.TimeToArrival(0f, Speed));
        }

        [Test]
        public void TimeToArrivalRejectsBadInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ThreatCalculator.TimeToArrival(-1f, Speed));
            Assert.Throws<ArgumentOutOfRangeException>(() => ThreatCalculator.TimeToArrival(1f, 0f));
        }

        // --- Susto (14.2) ---

        [Test]
        public void ALethalHostileProtonInsideTheThresholdScares()
        {
            Assert.IsTrue(Scared(Node(Faction.Player, NodeType.Star, 10), P(Faction.Enemy, 10, 3f)));
        }

        [Test]
        public void ExactlyAtTheThresholdDoesNotScare()
        {
            // 14.2: tiempoLlegada < UmbralSusto, estricto. 3.75 u / 2.5 u/s = 1.5 s exactos.
            Assert.IsFalse(Scared(Node(Faction.Player, NodeType.Star, 10), P(Faction.Enemy, 10, 3.75f)));
            Assert.IsTrue(Scared(Node(Faction.Player, NodeType.Star, 10), P(Faction.Enemy, 10, 3.7f)));
        }

        [Test]
        public void ANonLethalProtonDoesNotScare()
        {
            Assert.IsFalse(Scared(Node(Faction.Player, NodeType.Star, 10), P(Faction.Enemy, 9, 1f)));
        }

        [Test]
        public void ChargeEqualToEnergyIsLethal()
        {
            // 6.2: con efectiva >= Energía, captura.
            Assert.IsTrue(ThreatCalculator.IsLethal(P(Faction.Enemy, 10, 1f), Node(Faction.Player, NodeType.Star, 10), Resistance));
        }

        [Test]
        public void AnAllyNeverScares()
        {
            Assert.IsFalse(Scared(Node(Faction.Player, NodeType.Star, 1), P(Faction.Player, 50, 0.5f)));
        }

        [TestCase(Faction.Player, Faction.Enemy)]
        [TestCase(Faction.Enemy, Faction.Player)]
        [TestCase(Faction.Neutral, Faction.Player)]
        [TestCase(Faction.Neutral, Faction.Enemy)]
        public void EveryFactionCanBeScared(Faction owner, Faction attacker)
        {
            Assert.IsTrue(Scared(Node(owner, NodeType.Star, 10), P(attacker, 10, 1f)));
        }

        [Test]
        public void TheMagnetarHalvesTheCharge()
        {
            NodeSnapshot magnetar = Node(Faction.Enemy, NodeType.Magnetar, 10);
            Assert.IsFalse(Scared(magnetar, P(Faction.Player, 19, 1f)), "floor(19 × 0.5) = 9 < 10.");
            Assert.IsTrue(Scared(magnetar, P(Faction.Player, 20, 1f)), "floor(20 × 0.5) = 10.");
        }

        [Test]
        public void SeveralNonLethalProtonsDoNotAddUpToAScare()
        {
            // 14.2 pregunta protón a protón: el susto avisa de un golpe que captura él solo.
            NodeSnapshot node = Node(Faction.Player, NodeType.Star, 10);
            Assert.IsFalse(Scared(node, P(Faction.Enemy, 6, 1f), P(Faction.Enemy, 6, 1.2f)));
        }

        [Test]
        public void ALethalProtonFarAwayWaitsWhileANearOneScares()
        {
            NodeSnapshot node = Node(Faction.Player, NodeType.Star, 10);
            Assert.IsFalse(Scared(node, P(Faction.Enemy, 30, 10f)));
            Assert.IsTrue(Scared(node, P(Faction.Enemy, 30, 10f), P(Faction.Enemy, 10, 2f)));
        }

        [Test]
        public void AnEmptyNodeIsScaredByAnyHostileProton()
        {
            Assert.IsTrue(Scared(Node(Faction.Player, NodeType.Asteroid, 0), P(Faction.Enemy, 1, 1f)));
        }

        [Test]
        public void NoIncomingProtonsNoScare()
        {
            Assert.IsFalse(Scared(Node(Faction.Player, NodeType.Star, 0)));
        }

        [Test]
        public void IsScaredRejectsBadInput()
        {
            NodeSnapshot node = Node(Faction.Player, NodeType.Star, 10);
            Assert.Throws<ArgumentNullException>(() => ThreatCalculator.IsScared(node, null, Resistance, Speed, Threshold));
            Assert.Throws<ArgumentOutOfRangeException>(() => ThreatCalculator.IsScared(node, new IncomingProton[0], Resistance, Speed, -1f));
        }

        // --- Amenaza (12.4) ---

        [Test]
        public void NoHostileProtonsIsSafe()
        {
            ThreatAssessment threat = Assess(Node(Faction.Player, NodeType.Star, 10), P(Faction.Player, 40, 1f));

            Assert.IsFalse(threat.IsThreatened);
            Assert.AreEqual(0, threat.HostileCharge, "Un refuerzo no cuenta como Carga hostil.");
            Assert.IsTrue(float.IsPositiveInfinity(threat.TimeToCapture));
        }

        [Test]
        public void HostileChargeAddsUpAcrossProtons()
        {
            // 12.4 suma: dos protones de 6 amenazan un nodo de 10 aunque ninguno lo asuste.
            ThreatAssessment threat = Assess(Node(Faction.Player, NodeType.Star, 10),
                P(Faction.Enemy, 6, 5f), P(Faction.Enemy, 6, 2.5f));

            Assert.IsTrue(threat.IsThreatened);
            Assert.AreEqual(12, threat.HostileCharge);
            Assert.AreEqual(2f, threat.TimeToCapture, 1e-6f, "Cae con el segundo en llegar, a 5 u.");
        }

        [Test]
        public void TimeToCaptureFollowsArrivalOrderNotListOrder()
        {
            ThreatAssessment threat = Assess(Node(Faction.Player, NodeType.Star, 10),
                P(Faction.Enemy, 20, 7.5f), P(Faction.Enemy, 10, 2.5f));

            Assert.AreEqual(1f, threat.TimeToCapture, 1e-6f, "El de 10 llega antes y ya captura.");
        }

        [Test]
        public void NotEnoughHostileChargeIsNotAThreat()
        {
            ThreatAssessment threat = Assess(Node(Faction.Player, NodeType.Star, 20),
                P(Faction.Enemy, 6, 1f), P(Faction.Enemy, 6, 2f), P(Faction.Player, 30, 3f));

            Assert.IsFalse(threat.IsThreatened);
            Assert.AreEqual(12, threat.HostileCharge);
            Assert.IsTrue(float.IsPositiveInfinity(threat.TimeToCapture));
        }

        [Test]
        public void TheMagnetarRoundsEachHitOnItsOwn()
        {
            // Cada golpe redondea por su cuenta (6.2): 3 + 3 de Carga son 1 + 1, no floor(6 × 0.5) = 3.
            ThreatAssessment threat = Assess(Node(Faction.Enemy, NodeType.Magnetar, 3),
                P(Faction.Player, 3, 1f), P(Faction.Player, 3, 2f));

            Assert.IsFalse(threat.IsThreatened);
            Assert.AreEqual(2, threat.HostileCharge);
        }

        [Test]
        public void AssessRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ThreatCalculator.Assess(Node(Faction.Player, NodeType.Star, 10), null, Resistance, Speed));
        }
    }
}
