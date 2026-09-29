using NUnit.Framework;
using Nova.Core;

namespace Nova.Tests
{
    // T1-03. Balance de 16.1 y topes de 5.4 escritos a mano a propósito: se comprueba la especificación.
    public class ImpactResolverTests
    {
        const float MagnetarResistance = 0.5f;
        const int StarCap = 50;
        const int MagnetarCap = 100;
        const int AsteroidCap = 10;

        static ImpactOutcome Hit(Faction protonFaction, int charge, NodeSnapshot target) =>
            ImpactResolver.Resolve(new ProtonSnapshot(protonFaction, charge), target, MagnetarResistance);

        static NodeSnapshot Star(Faction f, int energy, bool frozen = false, bool infected = false) =>
            new NodeSnapshot(f, NodeType.Star, energy, StarCap, frozen, infected);

        static NodeSnapshot Magnetar(Faction f, int energy, bool frozen = false, bool infected = false) =>
            new NodeSnapshot(f, NodeType.Magnetar, energy, MagnetarCap, frozen, infected);

        // --- Absorción (6.2, 5.3) ---

        [Test]
        public void SameFactionAbsorbs()
        {
            var o = Hit(Faction.Player, 15, Star(Faction.Player, 20));
            Assert.AreEqual(ImpactResult.Absorbed, o.Result);
            Assert.AreEqual(35, o.NewEnergy);
            Assert.AreEqual(Faction.Player, o.NewFaction);
            Assert.IsFalse(o.FactionChanged);
        }

        // 17, caso 5: el excedente se descarta.
        [Test]
        public void AbsorptionOverflowIsDiscarded()
        {
            var o = Hit(Faction.Player, 40, Star(Faction.Player, 30));
            Assert.AreEqual(StarCap, o.NewEnergy);
        }

        // 5.3: el Magnetar aliado suma 1:1; la resistencia solo aplica a impactos hostiles.
        [Test]
        public void AlliedMagnetarAbsorbsOneToOne()
        {
            var o = Hit(Faction.Enemy, 30, Magnetar(Faction.Enemy, 10));
            Assert.AreEqual(ImpactResult.Absorbed, o.Result);
            Assert.AreEqual(40, o.NewEnergy);
        }

        // 17, caso 2: la facción se comprueba en el impacto. El snapshot ya trae la facción actual.
        [Test]
        public void NodeThatBecameOwnFactionAbsorbs()
        {
            var o = Hit(Faction.Player, 10, Star(Faction.Player, 0));
            Assert.AreEqual(ImpactResult.Absorbed, o.Result);
        }

        // --- Daño y captura (6.2) ---

        [Test]
        public void HostileChargeBelowEnergyDamages()
        {
            var o = Hit(Faction.Player, 12, Star(Faction.Enemy, 30));
            Assert.AreEqual(ImpactResult.Damaged, o.Result);
            Assert.AreEqual(18, o.NewEnergy);
            Assert.AreEqual(Faction.Enemy, o.NewFaction);
        }

        [Test]
        public void HostileChargeEqualToEnergyCapturesAtZero()
        {
            var o = Hit(Faction.Player, 10, new NodeSnapshot(Faction.Neutral, NodeType.Asteroid, 10, AsteroidCap));
            Assert.AreEqual(ImpactResult.Captured, o.Result);
            Assert.AreEqual(0, o.NewEnergy);
            Assert.AreEqual(Faction.Player, o.NewFaction);
            Assert.IsTrue(o.FactionChanged);
        }

        [Test]
        public void CaptureRemainderIsCappedAtTheTargetCap()
        {
            var o = Hit(Faction.Player, 200, Star(Faction.Enemy, 10));
            Assert.AreEqual(ImpactResult.Captured, o.Result);
            Assert.AreEqual(StarCap, o.NewEnergy);
        }

        // --- Resistencia ×2 del Magnetar (6.2, 6.4, 17 caso 7) ---

        [Test]
        public void MagnetarHalvesHostileDamage()
        {
            var o = Hit(Faction.Player, 30, Magnetar(Faction.Enemy, 50));
            Assert.AreEqual(ImpactResult.Damaged, o.Result);
            Assert.AreEqual(35, o.NewEnergy);
        }

        // 6.2: 120 contra un Magnetar con 50 lo captura y le deja 120*0.5 - 50 = 10.
        [Test]
        public void MagnetarCaptureRemainderIsAlsoHalved()
        {
            var o = Hit(Faction.Player, 120, Magnetar(Faction.Enemy, 50));
            Assert.AreEqual(ImpactResult.Captured, o.Result);
            Assert.AreEqual(10, o.NewEnergy);
        }

        // 6.4: un Magnetar lleno necesita 200; con 199 no cae.
        [Test]
        public void FullMagnetarNeedsTwoHundred()
        {
            Assert.AreEqual(ImpactResult.Damaged, Hit(Faction.Player, 199, Magnetar(Faction.Neutral, 100)).Result);
            Assert.AreEqual(ImpactResult.Captured, Hit(Faction.Player, 200, Magnetar(Faction.Neutral, 100)).Result);
        }

        // 17, caso 7: Carga 1 contra un Magnetar a 0 lo captura y lo deja a 0.
        [Test]
        public void ChargeOneCapturesMagnetarAtZero()
        {
            var o = Hit(Faction.Enemy, 1, Magnetar(Faction.Player, 0));
            Assert.AreEqual(ImpactResult.Captured, o.Result);
            Assert.AreEqual(0, o.NewEnergy);
            Assert.AreEqual(Faction.Enemy, o.NewFaction);
        }

        // --- Congelado (7.3) ---

        [Test]
        public void FrozenNodeLosesEnergyAndRecordsLastToucher()
        {
            var o = Hit(Faction.Player, 10, Star(Faction.Enemy, 30, frozen: true));
            Assert.AreEqual(ImpactResult.ThawProgress, o.Result);
            Assert.AreEqual(20, o.NewEnergy);
            Assert.AreEqual(Faction.Enemy, o.NewFaction);
            Assert.IsFalse(o.Unfreezes);
            Assert.AreEqual(Faction.Player, o.LastToucher);
        }

        // 7.3: cualquier facción, incluida la suya, le quita Energía.
        [Test]
        public void FrozenNodeIsDrainedByItsOwnFaction()
        {
            var o = Hit(Faction.Enemy, 10, Star(Faction.Enemy, 30, frozen: true));
            Assert.AreEqual(ImpactResult.ThawProgress, o.Result);
            Assert.AreEqual(20, o.NewEnergy);
        }

        // 7.3: al llegar a 0 se descongela, queda a 0 y pasa al bando del emisor; el remanente se pierde.
        [Test]
        public void FrozenNodeThawsAndChangesFactionAtZero()
        {
            var o = Hit(Faction.Player, 45, Star(Faction.Enemy, 30, frozen: true));
            Assert.AreEqual(ImpactResult.ThawedAndCaptured, o.Result);
            Assert.AreEqual(0, o.NewEnergy);
            Assert.AreEqual(Faction.Player, o.NewFaction);
            Assert.IsTrue(o.FactionChanged);
            Assert.IsTrue(o.Unfreezes);
        }

        [Test]
        public void FrozenNodeAtZeroThawsWithChargeOne()
        {
            var o = Hit(Faction.Player, 1, Magnetar(Faction.Neutral, 0, frozen: true));
            Assert.AreEqual(ImpactResult.ThawedAndCaptured, o.Result);
            Assert.AreEqual(Faction.Player, o.NewFaction);
        }

        // Pseudocódigo de 7.3: el multiplicador del Magnetar también aplica congelado.
        [Test]
        public void FrozenMagnetarHalvesTheDrain()
        {
            var o = Hit(Faction.Player, 30, Magnetar(Faction.Enemy, 50, frozen: true));
            Assert.AreEqual(ImpactResult.ThawProgress, o.Result);
            Assert.AreEqual(35, o.NewEnergy);
        }

        // Su propio dueño lo deshiela y lo conserva: pasa «al bando del emisor», que es el mismo.
        [Test]
        public void OwnFactionThawKeepsTheNode()
        {
            var o = Hit(Faction.Enemy, 30, Star(Faction.Enemy, 30, frozen: true));
            Assert.AreEqual(ImpactResult.ThawedAndCaptured, o.Result);
            Assert.AreEqual(Faction.Enemy, o.NewFaction);
            Assert.IsFalse(o.FactionChanged);
            Assert.IsTrue(o.Unfreezes);
        }

        // --- Cura del Decaimiento (7.4, 17 casos 11 y 12) ---

        [Test]
        public void OwnerCuresInfectionAndAddsEnergy()
        {
            var o = Hit(Faction.Player, 10, Star(Faction.Player, 0, infected: true));
            Assert.AreEqual(ImpactResult.Absorbed, o.Result);
            Assert.AreEqual(10, o.NewEnergy);
            Assert.IsTrue(o.CuresInfection);
        }

        [Test]
        public void InsufficientHostileChargeCuresButKeepsOwner()
        {
            var o = Hit(Faction.Enemy, 5, Star(Faction.Player, 20, infected: true));
            Assert.AreEqual(ImpactResult.Damaged, o.Result);
            Assert.AreEqual(Faction.Player, o.NewFaction);
            Assert.IsTrue(o.CuresInfection);
        }

        [Test]
        public void SufficientHostileChargeCapturesWithoutInfection()
        {
            var o = Hit(Faction.Enemy, 20, Star(Faction.Player, 20, infected: true));
            Assert.AreEqual(ImpactResult.Captured, o.Result);
            Assert.IsTrue(o.CuresInfection);
        }

        [Test]
        public void HealthyNodeIsNotMarkedAsCured()
        {
            Assert.IsFalse(Hit(Faction.Player, 5, Star(Faction.Player, 5)).CuresInfection);
        }

        // --- Resolución secuencial (17, caso 1) ---

        // El segundo protón opera sobre el resultado del primero.
        [Test]
        public void SecondImpactOperatesOnTheFirstResult()
        {
            var target = Star(Faction.Neutral, 10);
            var first = Hit(Faction.Player, 15, target);
            var afterFirst = new NodeSnapshot(first.NewFaction, NodeType.Star, first.NewEnergy, StarCap);
            var second = Hit(Faction.Enemy, 8, afterFirst);

            Assert.AreEqual(ImpactResult.Captured, first.Result);
            Assert.AreEqual(5, first.NewEnergy);
            Assert.AreEqual(ImpactResult.Captured, second.Result);
            Assert.AreEqual(Faction.Enemy, second.NewFaction);
            Assert.AreEqual(3, second.NewEnergy);
        }

        // 15.3: sin efectos secundarios. Misma entrada, misma salida.
        [Test]
        public void ResolveIsPure()
        {
            var target = Magnetar(Faction.Enemy, 50, frozen: true, infected: true);
            var a = Hit(Faction.Player, 33, target);
            var b = Hit(Faction.Player, 33, target);
            Assert.AreEqual(a, b);
            Assert.AreEqual(50, target.Energy);
        }
    }
}
