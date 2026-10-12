using System.Linq;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using UnityEditor;

namespace Nova.Tests
{
    // T1-05: los assets de balance contienen los valores de la sección 16 (y de 4.x y 5.4).
    // Los números están escritos a mano a propósito: si alguien toca un asset sin tocar la
    // especificación, este test lo dice.
    public class BalanceDataTests
    {
        const string NodesFolder = "Assets/Data/Nodes";
        const string BalancePath = "Assets/Data/BalanceConfig.asset";

        static NodeDefinition Node(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodesFolder}/{name}.asset");

        [Test]
        public void ThereIsExactlyOneDefinitionPerNodeType()
        {
            var types = AssetDatabase.FindAssets("t:NodeDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<NodeDefinition>(AssetDatabase.GUIDToAssetPath(guid)).Type)
                .OrderBy(t => t)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { NodeType.Asteroid, NodeType.Star, NodeType.Pulsar, NodeType.Magnetar }, types);
        }

        // 5.4 y niveles máximos de 4.1–4.4.
        [TestCase("Asteroid", NodeType.Asteroid, 10, 0)]
        [TestCase("Star", NodeType.Star, 50, 5)]
        [TestCase("Pulsar", NodeType.Pulsar, 100, 3)]
        [TestCase("Magnetar", NodeType.Magnetar, 100, 5)]
        public void NodeDefinitionMatchesSpec(string asset, NodeType type, int cap, int maxLevel)
        {
            var node = Node(asset);
            Assert.IsNotNull(node, $"Falta {NodesFolder}/{asset}.asset");
            Assert.AreEqual(type, node.Type);
            Assert.AreEqual(cap, node.EnergyCap);
            Assert.AreEqual(maxLevel, node.MaxLevel);
        }

        // 16.2, niveles 2 en adelante. El nivel 1 es el encendido (T.7 #41).
        [TestCase("Star", new[] { 20, 30, 40, 50 })]
        [TestCase("Pulsar", new[] { 25, 40 })]
        [TestCase("Magnetar", new[] { 20, 30, 40, 50 })]
        public void UpgradeCostsMatchSpec(string asset, int[] costsFromLevel2)
        {
            var node = Node(asset);
            var costs = Enumerable.Range(2, node.MaxLevel - 1).Select(node.UpgradeCostTo).ToArray();
            CollectionAssert.AreEqual(costsFromLevel2, costs);
            Assert.AreEqual(0, node.GetLevel(1).UpgradeCost, "El coste del nivel 1 vive en BalanceConfig.IgnitionCost.");
        }

        // 16.4: el coste depende del tipo de origen. El Asteroide no se reconvierte (8.1).
        [TestCase("Asteroid", 0)]
        [TestCase("Star", 20)]
        [TestCase("Pulsar", 20)]
        [TestCase("Magnetar", 30)]
        public void ConversionCostsMatchSpec(string asset, int cost)
        {
            Assert.AreEqual(cost, Node(asset).ConversionCost);
        }

        // 4.2.
        [Test]
        public void StarGenerationIntervalsMatchSpec()
        {
            var star = Node("Star");
            var intervals = Enumerable.Range(1, star.MaxLevel).Select(l => star.GetLevel(l).GenerationInterval).ToArray();
            CollectionAssert.AreEqual(new[] { 2.00f, 1.60f, 1.30f, 1.05f, 0.85f }, intervals);
        }

        // 4.4, con los radios ×0.75 y la cadencia ×4/3 de T.7 #35.
        [Test]
        public void MagnetarRadiiAndFireRatesMatchSpec()
        {
            var magnetar = Node("Magnetar");
            var levels = Enumerable.Range(1, magnetar.MaxLevel).Select(magnetar.GetLevel).ToArray();
            CollectionAssert.AreEqual(new[] { 1.5f, 1.75f, 2.0f, 2.25f, 2.5f }, levels.Select(l => l.Radius).ToArray());
            CollectionAssert.AreEqual(new[] { 1.33f, 1.87f, 2.53f, 3.33f, 4.27f }, levels.Select(l => l.FireRate).ToArray());
        }

        // 16.1.
        [Test]
        public void BalanceConfigMatchesSpec()
        {
            var balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalancePath);
            Assert.IsNotNull(balance, $"Falta {BalancePath}");
            Assert.AreEqual(2.5f, balance.ProtonSpeed);
            Assert.AreEqual(0.25f, balance.DoubleClickBuffer);
            Assert.AreEqual(4, balance.DragThreshold);
            Assert.AreEqual(2, balance.MagnetarShotDamage);
            Assert.AreEqual(0.5f, balance.MagnetarResistance);
            Assert.AreEqual(10, balance.IgnitionCost);
            Assert.AreEqual(10, balance.InertMatter);
            Assert.AreEqual(3.0f, balance.ConversionDuration);
            Assert.AreEqual(30f, balance.AutoThawTime);
            Assert.AreEqual(2f, balance.DecayRate);
            Assert.AreEqual(1.5f, balance.ScareThreshold);
            Assert.AreEqual(0.5f, balance.ChargeCancelRefund);
        }

        // Una sola instancia de BalanceConfig: dos harían ambiguo cuál manda.
        [Test]
        public void ThereIsExactlyOneBalanceConfig()
        {
            Assert.AreEqual(1, AssetDatabase.FindAssets("t:BalanceConfig").Length);
        }
    }
}
