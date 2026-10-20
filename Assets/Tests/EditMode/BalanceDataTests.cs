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
            Assert.AreEqual(30f, balance.PowerUpExpiry);
            Assert.AreEqual(3.0f, balance.CascadeDuration);
        }

        // Una sola instancia de BalanceConfig: dos harían ambiguo cuál manda.
        [Test]
        public void ThereIsExactlyOneBalanceConfig()
        {
            Assert.AreEqual(1, AssetDatabase.FindAssets("t:BalanceConfig").Length);
        }

        // --- Poderes (T3-01) ---

        const string PowersFolder = "Assets/Data/Powers";

        static PowerDefinition Power(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"{PowersFolder}/{name}.asset");

        [Test]
        public void ThereIsExactlyOneDefinitionPerPower()
        {
            var types = AssetDatabase.FindAssets("t:PowerDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<PowerDefinition>(AssetDatabase.GUIDToAssetPath(guid)).Type)
                .OrderBy(t => t)
                .ToArray();

            CollectionAssert.AreEqual(new[] { PowerType.AbsoluteZero, PowerType.Decay, PowerType.Wormhole }, types);
        }

        // 7.2 y 16.3. El haz dura 0.25 s en los tres (7.2.2).
        [TestCase("AbsoluteZero", PowerType.AbsoluteZero, 1, 20, 8f)]
        [TestCase("Decay", PowerType.Decay, 2, 35, 12f)]
        [TestCase("Wormhole", PowerType.Wormhole, 3, 60, 16f)]
        public void PowerDefinitionMatchesSpec(string asset, PowerType type, int level, int cost, float chargeTime)
        {
            var power = Power(asset);
            Assert.IsNotNull(power, $"Falta {PowersFolder}/{asset}.asset");
            Assert.AreEqual(type, power.Type);
            Assert.AreEqual(level, power.RequiredLevel);
            Assert.AreEqual(cost, power.Cost);
            Assert.AreEqual(chargeTime, power.ChargeTime);
            Assert.AreEqual(0.25f, power.BeamDuration);
        }

        // 16.3: el drenaje es coste / tiempo de carga, con dos decimales en la tabla.
        [TestCase("AbsoluteZero", 2.50f)]
        [TestCase("Decay", 2.92f)]
        [TestCase("Wormhole", 3.75f)]
        public void DrainRateMatchesSpec(string asset, float drain)
        {
            Assert.AreEqual(drain, Power(asset).DrainRate, 0.005f);
        }

        // 14.4: el haz va en el color del poder, no de la facción.
        [TestCase("AbsoluteZero", 0xB8, 0xF0, 0xFF)]
        [TestCase("Decay", 0x7C, 0xF2, 0x3F)]
        [TestCase("Wormhole", 0x9B, 0x5C, 0xF6)]
        public void BeamColorMatchesThePalette(string asset, int r, int g, int b)
        {
            UnityEngine.Color32 color = Power(asset).BeamColor;
            Assert.AreEqual(new UnityEngine.Color32((byte)r, (byte)g, (byte)b, 0xFF), color);
        }

        // --- Power-ups (9, T5-04) ---

        const string PowerUpsFolder = "Assets/Data/PowerUps";

        [Test]
        public void ThereIsExactlyOneDefinitionPerPowerUp()
        {
            var types = AssetDatabase.FindAssets("t:PowerUpDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<PowerUpDefinition>(AssetDatabase.GUIDToAssetPath(guid)).Type)
                .OrderBy(t => t)
                .ToArray();

            CollectionAssert.AreEqual(new[] { PowerUpType.CosmicRay, PowerUpType.HydrogenCloud }, types);
        }

        // 9.1 y 9.2: +50 % durante 20 s, y 50 % / 50 % en el sorteo.
        [TestCase("CosmicRay", PowerUpType.CosmicRay, PowerUpStat.AttackSpeed)]
        [TestCase("HydrogenCloud", PowerUpType.HydrogenCloud, PowerUpStat.EnergyGeneration)]
        public void PowerUpDefinitionMatchesSpec(string asset, PowerUpType type, PowerUpStat stat)
        {
            var powerUp = AssetDatabase.LoadAssetAtPath<PowerUpDefinition>($"{PowerUpsFolder}/{asset}.asset");
            Assert.IsNotNull(powerUp, $"Falta {PowerUpsFolder}/{asset}.asset");
            Assert.AreEqual(type, powerUp.Type);
            Assert.AreEqual(stat, powerUp.Stat);
            Assert.AreEqual(0.5f, powerUp.Magnitude);
            Assert.AreEqual(20f, powerUp.Duration);
            Assert.AreEqual(1f, powerUp.DropWeight, "Los dos pesos iguales: 50 % / 50 %.");
        }

        // --- Perfiles del Enemigo (12.5, T4-01) ---

        const string ProfilesFolder = "Assets/Data/EnemyProfiles";

        static EnemyProfile Profile(string name) =>
            AssetDatabase.LoadAssetAtPath<EnemyProfile>($"{ProfilesFolder}/{name}.asset");

        // Los tres de referencia de 12.5, uno por mapa, y los nueve por nivel de 12.5.2.
        [Test]
        public void ThereIsOneProfilePerMapAndOnePerLevel()
        {
            var names = AssetDatabase.FindAssets("t:EnemyProfile")
                .Select(guid => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(n => n)
                .ToArray();

            var expected = new[] { "Map1", "Map2", "Map3" }
                .Concat(Enumerable.Range(1, 9).Select(n => $"Level{n}Enemy"))
                .OrderBy(n => n)
                .ToArray();
            CollectionAssert.AreEqual(expected, names);
        }

        // La tabla de 12.5, columna a columna. «Nunca» envía al 100 % es margen 0.
        [TestCase("Map1", 3.0f, 40f, 0.7f, false, false, false, 0f, 0.25f)]
        [TestCase("Map2", 2.0f, 25f, 1.0f, true, false, true, 1.5f, 0.15f)]
        [TestCase("Map3", 1.2f, 10f, 1.3f, true, true, true, 1.2f, 0.05f)]
        public void ProfileMatchesSpec(string asset, float interval, float threshold, float aggression,
                                       bool evacuation, bool conversion, bool powerUps, float margin, float noise)
        {
            EnemyProfile profile = Profile(asset);
            Assert.IsNotNull(profile, $"Falta {ProfilesFolder}/{asset}.asset");
            Assert.AreEqual(interval, profile.DecisionInterval, 1e-5f);
            Assert.AreEqual(threshold, profile.ActionThreshold, 1e-5f);
            Assert.AreEqual(aggression, profile.AggressionMultiplier, 1e-5f);
            Assert.AreEqual(evacuation, profile.UsesEvacuation);
            Assert.AreEqual(conversion, profile.UsesConversion);
            Assert.AreEqual(powerUps, profile.PrioritizesPowerUps);
            Assert.AreEqual(margin, profile.FullSendMargin, 1e-5f);
            Assert.AreEqual(margin > 0f, profile.AllowsFullSend);
            Assert.AreEqual(noise, profile.DecisionNoise, 1e-5f);
        }

        // 12.5: el mapa 1 es el más torpe y el 3 el más afilado, en cada eje a la vez.
        [Test]
        public void ProfilesSharpenFromMapOneToThree()
        {
            EnemyProfile one = Profile("Map1"), two = Profile("Map2"), three = Profile("Map3");
            Assert.Greater(one.DecisionInterval, two.DecisionInterval);
            Assert.Greater(two.DecisionInterval, three.DecisionInterval);
            Assert.Greater(one.ActionThreshold, two.ActionThreshold);
            Assert.Greater(two.ActionThreshold, three.ActionThreshold);
            Assert.Less(one.AggressionMultiplier, two.AggressionMultiplier);
            Assert.Less(two.AggressionMultiplier, three.AggressionMultiplier);
            Assert.Greater(one.DecisionNoise, two.DecisionNoise);
            Assert.Greater(two.DecisionNoise, three.DecisionNoise);
        }

        // 12.1 corre en el tick fijo de 50 Hz (15.10): el intervalo es un número entero de ticks.
        [TestCase("Map1")]
        [TestCase("Map2")]
        [TestCase("Map3")]
        public void DecisionIntervalIsAWholeNumberOfTicks(string asset)
        {
            float ticks = Profile(asset).DecisionInterval / 0.02f;
            Assert.AreEqual(UnityEngine.Mathf.Round(ticks), ticks, 1e-3f);
        }
    }
}
