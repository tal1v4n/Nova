using NUnit.Framework;
using Nova.Core;

namespace Nova.Tests
{
    // T1-04. Costes de 16.1, 16.2 y 16.4 y topes de 5.4 escritos a mano a propósito.
    public class ConversionRulesTests
    {
        const int IgnitionCost = 10;

        static NodeSnapshot Asteroid(Faction f, int energy) =>
            new NodeSnapshot(f, NodeType.Asteroid, energy, 10);

        static NodeSnapshot Node(Faction f, NodeType type, int level, int energy, int cap = 100,
                                 bool frozen = false, bool infected = false,
                                 bool converting = false, bool powerCharge = false) =>
            new NodeSnapshot(f, type, energy, cap, frozen, infected, level, converting, powerCharge);

        // --- Encendido (4.1, 13.7) ---

        [TestCase(NodeType.Star)]
        [TestCase(NodeType.Pulsar)]
        [TestCase(NodeType.Magnetar)]
        public void OwnAsteroidWithTenIgnitesToAnyType(NodeType target)
        {
            var check = ConversionRules.CheckIgnition(Asteroid(Faction.Player, 10), Faction.Player, target, IgnitionCost);
            Assert.IsTrue(check.IsAllowed);
            Assert.AreEqual(10, check.Cost);
        }

        // 13.7: con menos de 10 la opción existe, deshabilitada y con el coste en rojo.
        [Test]
        public void AsteroidBelowTenLacksEnergy()
        {
            var check = ConversionRules.CheckIgnition(Asteroid(Faction.Player, 9), Faction.Player, NodeType.Star, IgnitionCost);
            Assert.AreEqual(ActionBlock.InsufficientEnergy, check.Block);
            Assert.AreEqual(10, check.Cost);
        }

        [Test]
        public void IgnitionRequiresOwnership()
        {
            Assert.AreEqual(ActionBlock.NotOwner,
                ConversionRules.CheckIgnition(Asteroid(Faction.Neutral, 10), Faction.Player, NodeType.Star, IgnitionCost).Block);
            Assert.AreEqual(ActionBlock.NotOwner,
                ConversionRules.CheckIgnition(Asteroid(Faction.Enemy, 10), Faction.Player, NodeType.Star, IgnitionCost).Block);
            Assert.AreEqual(ActionBlock.NotOwner,
                ConversionRules.CheckIgnition(Asteroid(Faction.Neutral, 10), Faction.Neutral, NodeType.Star, IgnitionCost).Block);
        }

        [Test]
        public void OnlyAsteroidsIgniteAndNeverIntoAsteroid()
        {
            Assert.AreEqual(ActionBlock.NotAsteroid,
                ConversionRules.CheckIgnition(Node(Faction.Player, NodeType.Star, 1, 50), Faction.Player, NodeType.Pulsar, IgnitionCost).Block);
            Assert.AreEqual(ActionBlock.InvalidTargetType,
                ConversionRules.CheckIgnition(Asteroid(Faction.Player, 10), Faction.Player, NodeType.Asteroid, IgnitionCost).Block);
        }

        // --- Mejora (16.2; 18 #18) ---

        [Test]
        public void UpgradeAllowedWithEnoughOwnEnergy()
        {
            var check = ConversionRules.CheckUpgrade(Node(Faction.Player, NodeType.Star, 1, 20, 50), Faction.Player, 5, 20);
            Assert.IsTrue(check.IsAllowed);
            Assert.AreEqual(20, check.Cost);
        }

        [Test]
        public void UpgradeLacksEnergy()
        {
            Assert.AreEqual(ActionBlock.InsufficientEnergy,
                ConversionRules.CheckUpgrade(Node(Faction.Player, NodeType.Pulsar, 1, 24), Faction.Player, 3, 25).Block);
        }

        // 17, caso 24.
        [TestCase(NodeType.Star, 5, 5)]
        [TestCase(NodeType.Pulsar, 3, 3)]
        [TestCase(NodeType.Magnetar, 5, 5)]
        public void UpgradeBlockedAtMaxLevel(NodeType type, int level, int maxLevel)
        {
            Assert.AreEqual(ActionBlock.MaxLevel,
                ConversionRules.CheckUpgrade(Node(Faction.Player, type, level, 100), Faction.Player, maxLevel, 50).Block);
        }

        [Test]
        public void UpgradeBlockedWhenFrozenOrConverting()
        {
            Assert.AreEqual(ActionBlock.Frozen,
                ConversionRules.CheckUpgrade(Node(Faction.Player, NodeType.Star, 1, 50, frozen: true), Faction.Player, 5, 20).Block);
            Assert.AreEqual(ActionBlock.Converting,
                ConversionRules.CheckUpgrade(Node(Faction.Player, NodeType.Star, 1, 50, converting: true), Faction.Player, 5, 20).Block);
        }

        // 7.4: infectado funciona con normalidad. Y el Púlsar puede mejorar mientras carga.
        [Test]
        public void UpgradeAllowedWhenInfectedOrChargingPower()
        {
            Assert.IsTrue(ConversionRules.CheckUpgrade(
                Node(Faction.Player, NodeType.Star, 1, 50, infected: true), Faction.Player, 5, 20).IsAllowed);
            Assert.IsTrue(ConversionRules.CheckUpgrade(
                Node(Faction.Player, NodeType.Pulsar, 1, 50, powerCharge: true), Faction.Player, 3, 25).IsAllowed);
        }

        // 17, caso 23: sobre un nodo ajeno no hay mejora ni reconversión.
        [Test]
        public void UpgradeAndConversionRequireOwnership()
        {
            var enemyStar = Node(Faction.Enemy, NodeType.Star, 1, 50);
            Assert.AreEqual(ActionBlock.NotOwner, ConversionRules.CheckUpgrade(enemyStar, Faction.Player, 5, 20).Block);
            Assert.AreEqual(ActionBlock.NotOwner, ConversionRules.CheckConversion(enemyStar, Faction.Player, NodeType.Pulsar, 20).Block);
        }

        [Test]
        public void AsteroidCannotUpgradeOrConvert()
        {
            var asteroid = Asteroid(Faction.Player, 10);
            Assert.AreEqual(ActionBlock.IsAsteroid, ConversionRules.CheckUpgrade(asteroid, Faction.Player, 5, 20).Block);
            Assert.AreEqual(ActionBlock.IsAsteroid, ConversionRules.CheckConversion(asteroid, Faction.Player, NodeType.Star, 20).Block);
        }

        // --- Reconversión (8.1, 8.2) ---

        // 8.3: Magnetar nivel 4 con 45 → Estrella: paga 30 y queda con 15 (< tope 50).
        [Test]
        public void SpecExampleMagnetarToStar()
        {
            var magnetar = Node(Faction.Player, NodeType.Magnetar, 4, 45);
            var check = ConversionRules.CheckConversion(magnetar, Faction.Player, NodeType.Star, 30);
            Assert.IsTrue(check.IsAllowed);
            Assert.AreEqual(15, ConversionRules.EnergyAfterConversion(magnetar.Energy - check.Cost, 50));
        }

        // 8.1: la Energía sobrante se recorta al tope del tipo nuevo.
        [Test]
        public void EnergyIsClippedToTheNewCap()
        {
            Assert.AreEqual(50, ConversionRules.EnergyAfterConversion(80, 50));
            Assert.AreEqual(0, ConversionRules.EnergyAfterConversion(0, 50));
        }

        [Test]
        public void CannotConvertToSameTypeOrAsteroid()
        {
            var star = Node(Faction.Player, NodeType.Star, 2, 50, 50);
            Assert.AreEqual(ActionBlock.InvalidTargetType, ConversionRules.CheckConversion(star, Faction.Player, NodeType.Star, 20).Block);
            Assert.AreEqual(ActionBlock.InvalidTargetType, ConversionRules.CheckConversion(star, Faction.Player, NodeType.Asteroid, 20).Block);
        }

        // 8.1: no se inicia congelado, infectado, con poder en carga o cargado (18 #19), ni ya reconvirtiéndose.
        [Test]
        public void ConversionBlockedByStates()
        {
            Assert.AreEqual(ActionBlock.Frozen, ConversionRules.CheckConversion(
                Node(Faction.Player, NodeType.Star, 1, 50, frozen: true), Faction.Player, NodeType.Pulsar, 20).Block);
            Assert.AreEqual(ActionBlock.Infected, ConversionRules.CheckConversion(
                Node(Faction.Player, NodeType.Star, 1, 50, infected: true), Faction.Player, NodeType.Pulsar, 20).Block);
            Assert.AreEqual(ActionBlock.PowerCharge, ConversionRules.CheckConversion(
                Node(Faction.Player, NodeType.Pulsar, 1, 50, powerCharge: true), Faction.Player, NodeType.Star, 20).Block);
            Assert.AreEqual(ActionBlock.Converting, ConversionRules.CheckConversion(
                Node(Faction.Player, NodeType.Star, 1, 50, converting: true), Faction.Player, NodeType.Pulsar, 20).Block);
        }

        [Test]
        public void ConversionLacksEnergy()
        {
            var check = ConversionRules.CheckConversion(Node(Faction.Player, NodeType.Magnetar, 1, 29), Faction.Player, NodeType.Star, 30);
            Assert.AreEqual(ActionBlock.InsufficientEnergy, check.Block);
            Assert.AreEqual(30, check.Cost);
        }

        // 17, caso 18: sin Estrellas pero con 80 en un Magnetar, puede reconvertirlo por 30.
        [Test]
        public void MagnetarWithEightyCanConvertForThirty()
        {
            Assert.IsTrue(ConversionRules.CheckConversion(
                Node(Faction.Player, NodeType.Magnetar, 3, 80), Faction.Player, NodeType.Star, 30).IsAllowed);
        }

        // La falta de Energía se informa solo si no hay otro motivo: la UI la pinta en rojo.
        [Test]
        public void InsufficientEnergyIsReportedLast()
        {
            var frozenPoor = Node(Faction.Player, NodeType.Star, 1, 0, frozen: true);
            Assert.AreEqual(ActionBlock.Frozen, ConversionRules.CheckConversion(frozenPoor, Faction.Player, NodeType.Pulsar, 20).Block);
            Assert.AreEqual(ActionBlock.Frozen, ConversionRules.CheckUpgrade(frozenPoor, Faction.Player, 5, 20).Block);
        }
    }
}
