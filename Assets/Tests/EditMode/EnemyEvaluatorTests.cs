using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Enemy;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T4-02: EnemyEvaluator (12.3, 12.3.1, 12.3.2, 12.3.3) y los tests mínimos de 12.7 que le tocan:
    // todos menos el 5, que es de EnemyThreatResponder (T4-05). El tablero se escribe a mano, sin
    // escena: el evaluador es puro.
    public class EnemyEvaluatorTests
    {
        static EnemyProfile Profile(string name) =>
            AssetDatabase.LoadAssetAtPath<EnemyProfile>($"Assets/Data/EnemyProfiles/{name}.asset");

        static EnemyScoring Scoring =>
            AssetDatabase.LoadAssetAtPath<EnemyScoring>("Assets/Data/EnemyScoring.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        static PowerDefinition Power(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static EnemyProfile Map1 => Profile("Map1");
        static EnemyProfile Map2 => Profile("Map2");
        static EnemyProfile Map3 => Profile("Map3");

        static int Cap(NodeType type) => type == NodeType.Star ? 50 : type == NodeType.Asteroid ? 10 : 100;

        // Coste del nivel siguiente (16.2) y de reconversión (16.4).
        static int UpgradeCost(NodeType type, int level)
        {
            if (type == NodeType.Asteroid)
                return -1;
            int[] costs = type == NodeType.Pulsar ? new[] { 25, 40 } : new[] { 20, 30, 40, 50 };
            return level - 1 < costs.Length ? costs[level - 1] : -1;
        }

        static int ConversionCost(NodeType type) => type == NodeType.Magnetar ? 30 : type == NodeType.Asteroid ? 0 : 20;

        readonly List<BoardNode> nodes = new List<BoardNode>();

        [SetUp]
        public void SetUp()
        {
            nodes.Clear();
        }

        BoardNode Add(Faction faction, NodeType type, int energy, Vector2 position, int level = 1,
                      bool frozen = false, bool infected = false, bool converting = false,
                      PowerType? charging = null, PowerType? ready = null, bool powerUp = false)
        {
            int actualLevel = type == NodeType.Asteroid ? 0 : level;
            var state = new NodeSnapshot(faction, type, energy, Cap(type), frozen, infected, actualLevel, converting,
                charging.HasValue || ready.HasValue);
            float radius = type == NodeType.Magnetar ? 1.25f + 0.25f * actualLevel : 0f; // 4.4: 1.5 a 2.5 u
            var node = new BoardNode(nodes.Count, state, position, radius, UpgradeCost(type, actualLevel),
                ConversionCost(type), charging, ready, powerUp);
            nodes.Add(node);
            return node;
        }

        EnemyBoard Board() => new EnemyBoard(Faction.Enemy, nodes, Balance.MagnetarResistance, Balance.IgnitionCost,
            new[] { Power("AbsoluteZero"), Power("Decay"), Power("Wormhole") });

        static EnemyAction? Best(EnemyBoard board, EnemyProfile profile) =>
            EnemyEvaluator.Pick(EnemyEvaluator.Candidates(board, profile, Scoring), profile, null);

        // --- Los datos ---

        [Test]
        public void TheScoringAssetHoldsTheWeightsOfTheSpec()
        {
            EnemyScoring s = Scoring;
            Assert.IsNotNull(s, "Falta Assets/Data/EnemyScoring.asset");
            Assert.AreEqual(100f, s.TypeValue(NodeType.Star));
            Assert.AreEqual(80f, s.TypeValue(NodeType.Pulsar));
            Assert.AreEqual(70f, s.TypeValue(NodeType.Magnetar));
            Assert.AreEqual(40f, s.TypeValue(NodeType.Asteroid));
            Assert.AreEqual(60f, s.PowerUpBonus);
            Assert.AreEqual(6f, s.RouteRiskPerMagnetar);
            Assert.AreEqual(30f, s.DrainPenalty);
            Assert.AreEqual(0.3f, s.DrainThreshold, 1e-6f);
            Assert.AreEqual(40f, s.AffinityOf(PowerType.AbsoluteZero, NodeType.Pulsar));
            Assert.AreEqual(40f, s.AffinityOf(PowerType.Decay, NodeType.Star));
            Assert.AreEqual(50f, s.AffinityOf(PowerType.Wormhole, NodeType.Magnetar));
            Assert.AreEqual(20f, s.AffinityOf(PowerType.AbsoluteZero, NodeType.Star));
            Assert.AreEqual(10f, s.AffinityOf(PowerType.Decay, NodeType.Pulsar));
            Assert.AreEqual(20f, s.AffinityOf(PowerType.Wormhole, NodeType.Pulsar));
            Assert.AreEqual(60f, s.StealBonus);
            Assert.AreEqual(40f, s.RelativeEnergyWeight);
            Assert.AreEqual(25f, s.NeutralAdjustment);
            Assert.AreEqual(100f, s.RedundancyPenalty);
            Assert.AreEqual(50f, s.IgnitionBase);
            Assert.AreEqual(60f, s.UpgradeBase(NodeType.Star));
            Assert.AreEqual(50f, s.UpgradeBase(NodeType.Pulsar));
            Assert.AreEqual(40f, s.UpgradeBase(NodeType.Magnetar));
            Assert.AreEqual(80f, s.ConversionBase);
            Assert.AreEqual(40f, s.ExpansionNeed);
            Assert.AreEqual(2f, s.ExpansionDistanceWeight);
            Assert.AreEqual(70f, s.SupplyAsteroidBase);
            Assert.AreEqual(45f, s.SupplyPulsarBase);
        }

        // --- Tests mínimos de 12.7 ---

        // 1.
        [Test]
        public void PrefersALevelThreeStarToAnAsteroid()
        {
            Add(Faction.Enemy, NodeType.Star, 50, new Vector2(0f, 0f));
            BoardNode star = Add(Faction.Player, NodeType.Star, 10, new Vector2(5f, 0f), level: 3);
            Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(0f, 3f));

            EnemyAction? best = Best(Board(), Map2);

            Assert.IsTrue(best.HasValue);
            Assert.AreEqual(EnemyActionKind.Attack, best.Value.Kind);
            Assert.AreEqual(star.Index, best.Value.Target);
            Assert.AreEqual(90f, best.Value.Score, 1e-4f, "100 − 10 de coste.");
        }

        // 2.
        [Test]
        public void DiscardsATargetItCannotPayAtFullSend()
        {
            BoardNode own = Add(Faction.Enemy, NodeType.Star, 20, new Vector2(0f, 0f));
            BoardNode magnetar = Add(Faction.Player, NodeType.Magnetar, 15, new Vector2(5f, 0f));
            EnemyBoard board = Board();

            Assert.AreEqual(30, EnemyEvaluator.EffectiveCost(board, magnetar), "Resistencia ×2.");
            Assert.IsNull(EnemyEvaluator.ScoreAttack(board, own, magnetar, Map3, Scoring));
            Assert.IsFalse(EnemyEvaluator.Candidates(board, Map3, Scoring).Any(a => a.Kind == EnemyActionKind.Attack));
        }

        // 3.
        [Test]
        public void RouteRiskCostsSixPerHostileMagnetarCrossingTheRoute()
        {
            BoardNode own = Add(Faction.Enemy, NodeType.Star, 50, new Vector2(0f, 0f));
            BoardNode target = Add(Faction.Player, NodeType.Star, 10, new Vector2(6f, 0f));
            EnemyBoard clear = Board();
            float clean = EnemyEvaluator.ScoreAttack(clear, own, target, Map2, Scoring).Value.Score;

            Add(Faction.Player, NodeType.Magnetar, 10, new Vector2(3f, 1f));   // a 1 u del segmento, radio 1.5
            Add(Faction.Player, NodeType.Magnetar, 10, new Vector2(3f, 3f));   // a 3 u: fuera
            Add(Faction.Player, NodeType.Magnetar, 10, new Vector2(3f, -1.4f)); // a 1.4 u: dentro
            EnemyBoard board = Board();

            Assert.AreEqual(2, EnemyEvaluator.HostileMagnetarsOnRoute(board, own, target));
            Assert.AreEqual(clean - 12f, EnemyEvaluator.ScoreAttack(board, own, target, Map2, Scoring).Value.Score, 1e-4f);
        }

        // 4.
        [Test]
        public void TheSameSeedPicksTheSameActionAndDifferentSeedsCanDiffer()
        {
            var candidates = new List<EnemyAction>
            {
                new EnemyAction(EnemyActionKind.Attack, 0, 1, 100f),
                new EnemyAction(EnemyActionKind.Attack, 0, 2, 95f),
            };

            var picked = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                EnemyAction? first = EnemyEvaluator.Pick(candidates, Map1, new System.Random(seed));
                EnemyAction? again = EnemyEvaluator.Pick(candidates, Map1, new System.Random(seed));
                Assert.AreEqual(first.Value.Target, again.Value.Target, $"Semilla {seed}: misma entrada, misma salida.");
                Assert.AreEqual(first.Value.Score, again.Value.Score);
                picked.Add(first.Value.Target);
            }
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, picked, "Con ±25 %, semillas distintas eligen distinto.");
        }

        // 6.
        [Test]
        public void NoPowerCandidateWhenTheOnlyPlayerNodesAreAsteroids()
        {
            BoardNode pulsar = Add(Faction.Enemy, NodeType.Pulsar, 50, new Vector2(0f, 0f), ready: PowerType.AbsoluteZero);
            Add(Faction.Player, NodeType.Asteroid, 10, new Vector2(1f, 0f));
            Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(-1f, 0f));
            EnemyBoard board = Board();

            Assert.IsNull(EnemyEvaluator.BestPowerTarget(board, pulsar, PowerType.AbsoluteZero, Map3, Scoring));
            Assert.IsFalse(EnemyEvaluator.Candidates(board, Map3, Scoring).Any(a => a.Kind == EnemyActionKind.LaunchPower));
        }

        // 7.
        [Test]
        public void DistanceDoesNotChangeThePowerTarget()
        {
            var picks = new List<int>();
            var scores = new List<float>();
            foreach (Vector2 at in new[] { new Vector2(4.5f, 0f), new Vector2(-7f, -4f) })
            {
                nodes.Clear();
                Add(Faction.Player, NodeType.Star, 40, new Vector2(5f, 0f));
                Add(Faction.Player, NodeType.Pulsar, 30, new Vector2(5f, 3f));
                Add(Faction.Player, NodeType.Magnetar, 60, new Vector2(5f, -3f));
                BoardNode pulsar = Add(Faction.Enemy, NodeType.Pulsar, 50, at, ready: PowerType.Decay);

                EnemyAction best = EnemyEvaluator.BestPowerTarget(Board(), pulsar, PowerType.Decay, Map2, Scoring).Value;
                picks.Add(best.Target);
                scores.Add(best.Score);
            }
            Assert.AreEqual(picks[0], picks[1]);
            Assert.AreEqual(scores[0], scores[1]);
        }

        // 8.
        [Test]
        public void AbsoluteZeroPicksTheTargetThatIsNotAlreadyFrozen()
        {
            BoardNode pulsar = Add(Faction.Enemy, NodeType.Pulsar, 50, new Vector2(0f, 0f), ready: PowerType.AbsoluteZero);
            Add(Faction.Player, NodeType.Star, 30, new Vector2(5f, 1f), frozen: true);
            BoardNode warm = Add(Faction.Player, NodeType.Star, 30, new Vector2(5f, -1f));

            EnemyAction best = EnemyEvaluator.BestPowerTarget(Board(), pulsar, PowerType.AbsoluteZero, Map2, Scoring).Value;

            Assert.AreEqual(warm.Index, best.Target);
        }

        // 9.
        [Test]
        public void AFullNeutralMagnetarIsAPowerCandidateAndANeutralAsteroidIsNot()
        {
            Add(Faction.Enemy, NodeType.Pulsar, 50, new Vector2(0f, 0f), ready: PowerType.Wormhole, level: 3);
            BoardNode wall = Add(Faction.Neutral, NodeType.Magnetar, 100, new Vector2(3f, 0f));
            BoardNode rock = Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(-3f, 0f));
            EnemyBoard board = Board();

            Assert.IsTrue(EnemyEvaluator.IsPowerCandidate(board, wall));
            Assert.IsFalse(EnemyEvaluator.IsPowerCandidate(board, rock));
            List<EnemyAction> launches = EnemyEvaluator.Candidates(board, Map3, Scoring)
                .Where(a => a.Kind == EnemyActionKind.LaunchPower).ToList();
            Assert.AreEqual(1, launches.Count);
            Assert.AreEqual(wall.Index, launches[0].Target);
            // 70 + 50 de afinidad + 40 de Energía − 25 de neutral, × 1.3.
            Assert.AreEqual((70f + 50f + 40f - 25f) * 1.3f, launches[0].Score, 1e-3f);
        }

        // 10.
        [Test]
        public void AggressionMultipliesTheWholeSum()
        {
            BoardNode own = Add(Faction.Enemy, NodeType.Star, 20, new Vector2(0f, 0f));
            BoardNode target = Add(Faction.Player, NodeType.Star, 8, new Vector2(5f, 0f));
            Add(Faction.Player, NodeType.Star, 30, new Vector2(5f, 3f)); // puede castigar el vaciado
            EnemyBoard board = Board();

            float one = EnemyEvaluator.ScoreAttack(board, own, target, Map1, Scoring).Value.Score;
            float two = EnemyEvaluator.ScoreAttack(board, own, target, Map2, Scoring).Value.Score;
            float three = EnemyEvaluator.ScoreAttack(board, own, target, Map3, Scoring).Value.Score;

            Assert.AreEqual(100f - 8f - 30f, two, 1e-4f, "Valor − coste − vaciado.");
            Assert.AreEqual(two * 0.7f, one, 1e-3f);
            Assert.AreEqual(two * 1.3f, three, 1e-3f);
        }

        // 11.
        [Test]
        public void AFullSendNeedsTheMarginAndWithoutItTheAttackOnlyWearsDown()
        {
            BoardNode own = Add(Faction.Enemy, NodeType.Star, 40, new Vector2(0f, 0f));
            BoardNode target = Add(Faction.Player, NodeType.Star, 30, new Vector2(5f, 0f));
            EnemyBoard board = Board();

            EnemyEvaluator.SendPlan three = EnemyEvaluator.PlanSend(40, 30, Map3);
            Assert.IsTrue(three.FullSend, "40 ≥ 30 × 1.2.");
            Assert.AreEqual(40, three.Charge);
            Assert.AreEqual(1f, three.Reach);

            EnemyEvaluator.SendPlan two = EnemyEvaluator.PlanSend(40, 30, Map2);
            Assert.IsFalse(two.FullSend, "40 < 30 × 1.5.");
            Assert.AreEqual(20, two.Charge);
            Assert.AreEqual(20f / 30f, two.Reach, 1e-5f);

            Assert.IsFalse(EnemyEvaluator.PlanSend(59, 30, Map1).FullSend, "El mapa 1 nunca envía al 100 %.");

            Assert.IsTrue(EnemyEvaluator.ScoreAttack(board, own, target, Map3, Scoring).Value.FullSend);
            EnemyAction wear = EnemyEvaluator.ScoreAttack(board, own, target, Map2, Scoring).Value;
            Assert.IsFalse(wear.FullSend);
            Assert.AreEqual(100f * 20f / 30f - 30f, wear.Score, 1e-3f, "Alcance 2/3; el origen queda con 20 de 50: no vaciado.");
        }

        // 12.
        [Test]
        public void TheDrainPenaltyNeedsSomeoneWhoCanPunishIt()
        {
            BoardNode own = Add(Faction.Enemy, NodeType.Star, 20, new Vector2(0f, 0f));
            Add(Faction.Player, NodeType.Star, 5, new Vector2(5f, 0f));
            Add(Faction.Player, NodeType.Asteroid, 10, new Vector2(5f, 2f));
            EnemyBoard harmless = Board();

            Assert.IsFalse(EnemyEvaluator.IsExposed(harmless, own, 10, Scoring), "Nadie con un 50 % que llegue a 10.");
            Assert.AreEqual(0f, EnemyEvaluator.DrainPenalty(harmless, own, 10, Scoring));

            Add(Faction.Player, NodeType.Star, 22, new Vector2(5f, -2f));
            EnemyBoard threatening = Board();
            Assert.IsTrue(EnemyEvaluator.IsExposed(threatening, own, 10, Scoring));
            Assert.IsFalse(EnemyEvaluator.IsExposed(threatening, own, 15, Scoring), "15 de 50 no baja del 30 %.");
        }

        // 13.
        [Test]
        public void RouteRiskCountsTheNeutralMagnetarAndNotItsOwn()
        {
            BoardNode own = Add(Faction.Enemy, NodeType.Star, 50, new Vector2(0f, 0f));
            BoardNode target = Add(Faction.Player, NodeType.Star, 10, new Vector2(6f, 0f));
            Add(Faction.Neutral, NodeType.Magnetar, 20, new Vector2(2f, 0.5f));
            Add(Faction.Enemy, NodeType.Magnetar, 20, new Vector2(4f, 0.5f));

            Assert.AreEqual(1, EnemyEvaluator.HostileMagnetarsOnRoute(Board(), own, target));
        }

        // 14.
        [Test]
        public void IgnitionPicksAStarWithoutStarsAndAPulsarWithTwoStarsAndNone()
        {
            Add(Faction.Enemy, NodeType.Asteroid, 10, new Vector2(0f, 0f));
            Add(Faction.Player, NodeType.Star, 10, new Vector2(6f, 0f));
            EnemyAction alone = Best(Board(), Map2).Value;
            Assert.AreEqual(EnemyActionKind.Ignite, alone.Kind);
            Assert.AreEqual(NodeType.Star, alone.NodeType, "50 + 40.");

            nodes.Clear();
            Add(Faction.Enemy, NodeType.Asteroid, 10, new Vector2(-6f, 0f));
            Add(Faction.Enemy, NodeType.Star, 0, new Vector2(2f, 1f));
            Add(Faction.Enemy, NodeType.Star, 0, new Vector2(2f, -1f));
            Add(Faction.Player, NodeType.Star, 10, new Vector2(6f, 0f));
            EnemyAction later = Best(Board(), Map2).Value;
            Assert.AreEqual(EnemyActionKind.Ignite, later.Kind);
            Assert.AreEqual(NodeType.Pulsar, later.NodeType, "50 + 30 contra 50 + 13.3 y 50.");
        }

        // 15.
        [Test]
        public void TiesResolveByTheOrderOfTwelveThreeThree()
        {
            var tied = new List<EnemyAction>
            {
                new EnemyAction(EnemyActionKind.Upgrade, 0, -1, 50f),
                new EnemyAction(EnemyActionKind.Attack, 3, 5, 50f),
                new EnemyAction(EnemyActionKind.Attack, 1, 7, 50f),
                new EnemyAction(EnemyActionKind.Attack, 1, 6, 50f),
            };
            for (int i = 0; i < 5; i++)
            {
                EnemyAction best = EnemyEvaluator.Pick(tied, Map2, null).Value;
                Assert.AreEqual(EnemyActionKind.Attack, best.Kind, "Primero el orden de 12.2.");
                Assert.AreEqual(1, best.Actor, "Después el nodo que actúa.");
                Assert.AreEqual(6, best.Target, "Y después el objetivo.");
                tied.Reverse();
            }
        }

        // --- Funciones de 12.3.2 ---

        [TestCase(NodeType.Star, 1, 50f)]
        [TestCase(NodeType.Star, 4, 35f)]
        [TestCase(NodeType.Magnetar, 1, 30f)]
        [TestCase(NodeType.Pulsar, 1, 37.5f)]
        public void UpgradeScores(NodeType type, int level, float expected)
        {
            BoardNode node = Add(Faction.Enemy, type, 50, Vector2.zero, level: level);
            Assert.AreEqual(expected, EnemyEvaluator.ScoreUpgrade(Board(), node, Scoring), 1e-4f);
        }

        [Test]
        public void UpgradingIntoExposureIsPenalised()
        {
            BoardNode star = Add(Faction.Enemy, NodeType.Star, 30, Vector2.zero); // paga 20, queda con 10
            Add(Faction.Player, NodeType.Star, 30, new Vector2(5f, 0f));
            Assert.AreEqual(60f - 10f - 30f, EnemyEvaluator.ScoreUpgrade(Board(), star, Scoring), 1e-4f);
        }

        [Test]
        public void UpgradeIsACandidateOnlyWhenItCanBePaidAndTheNodeIsFree()
        {
            Add(Faction.Enemy, NodeType.Star, 19, Vector2.zero);
            Add(Faction.Enemy, NodeType.Star, 50, new Vector2(0f, 2f), frozen: true);
            Add(Faction.Enemy, NodeType.Star, 50, new Vector2(0f, 4f), level: 5);
            Assert.IsFalse(EnemyEvaluator.Candidates(Board(), Map2, Scoring).Any(a => a.Kind == EnemyActionKind.Upgrade));
        }

        [Test]
        public void ChargeIsWorthHalfTheBestLaunchMinusHalfItsCost()
        {
            BoardNode pulsar = Add(Faction.Enemy, NodeType.Pulsar, 50, Vector2.zero, level: 2);
            Add(Faction.Player, NodeType.Star, 50, new Vector2(5f, 0f));
            EnemyBoard board = Board();

            // Decaimiento sobre una Estrella llena: 100 + 40 + 40 = 180.
            Assert.AreEqual(180f / 2f - 35f / 2f, EnemyEvaluator.ScoreCharge(board, pulsar, Power("Decay"), Map2, Scoring).Value, 1e-3f);
            List<EnemyAction> charges = EnemyEvaluator.Candidates(board, Map2, Scoring)
                .Where(a => a.Kind == EnemyActionKind.ChargePower).ToList();
            CollectionAssert.AreEquivalent(new[] { PowerType.AbsoluteZero, PowerType.Decay }, charges.Select(a => a.Power),
                "El Gusano pide nivel 3.");
        }

        [Test]
        public void AChargeThatWouldStallPaysWhatItLacks()
        {
            BoardNode pulsar = Add(Faction.Enemy, NodeType.Pulsar, 12, Vector2.zero);
            Add(Faction.Player, NodeType.Star, 50, new Vector2(5f, 0f));

            // Cero Absoluto sobre la Estrella: 100 + 20 + 40 = 160; faltan 8 de los 20.
            Assert.AreEqual(160f / 2f - 10f - 8f, EnemyEvaluator.ScoreCharge(Board(), pulsar, Power("AbsoluteZero"), Map2, Scoring).Value, 1e-3f);
        }

        [Test]
        public void NoChargeWithoutTargetsOrWithAPowerAlreadyCharging()
        {
            Add(Faction.Enemy, NodeType.Pulsar, 50, Vector2.zero);
            Add(Faction.Player, NodeType.Asteroid, 10, new Vector2(5f, 0f));
            Assert.IsFalse(EnemyEvaluator.Candidates(Board(), Map2, Scoring).Any(a => a.Kind == EnemyActionKind.ChargePower));

            nodes.Clear();
            Add(Faction.Enemy, NodeType.Pulsar, 50, Vector2.zero, charging: PowerType.AbsoluteZero);
            Add(Faction.Player, NodeType.Star, 50, new Vector2(5f, 0f));
            List<EnemyAction> all = EnemyEvaluator.Candidates(Board(), Map2, Scoring);
            Assert.IsFalse(all.Any(a => a.Kind == EnemyActionKind.ChargePower || a.Kind == EnemyActionKind.LaunchPower));
        }

        [Test]
        public void TheStealBonusIsForAWormholeOnAReadyPulsar()
        {
            Add(Faction.Enemy, NodeType.Pulsar, 50, Vector2.zero, level: 3, ready: PowerType.Wormhole);
            BoardNode loaded = Add(Faction.Player, NodeType.Pulsar, 0, new Vector2(5f, 0f), ready: PowerType.Decay);
            BoardNode empty = Add(Faction.Player, NodeType.Pulsar, 0, new Vector2(5f, 2f));
            EnemyBoard board = Board();

            float withPower = EnemyEvaluator.ScorePower(board, PowerType.Wormhole, loaded, Map2, Scoring);
            float without = EnemyEvaluator.ScorePower(board, PowerType.Wormhole, empty, Map2, Scoring);
            Assert.AreEqual(60f, withPower - without, 1e-4f);
            Assert.AreEqual(without, EnemyEvaluator.ScorePower(board, PowerType.AbsoluteZero, loaded, Map2, Scoring) + 20f - 40f, 1e-4f,
                "Solo el Gusano roba.");
        }

        [Test]
        public void ConversionOnlyWithoutStarsAndWithTheProfile()
        {
            Add(Faction.Enemy, NodeType.Magnetar, 40, Vector2.zero);
            BoardNode pulsar = Add(Faction.Enemy, NodeType.Pulsar, 60, new Vector2(0f, 2f));
            EnemyBoard board = Board();

            Assert.IsFalse(EnemyEvaluator.Candidates(board, Map2, Scoring).Any(a => a.Kind == EnemyActionKind.Convert), "Map2 no reconvierte.");
            List<EnemyAction> conversions = EnemyEvaluator.Candidates(board, Map3, Scoring).Where(a => a.Kind == EnemyActionKind.Convert).ToList();
            Assert.AreEqual(1, conversions.Count, "Una sola candidata.");
            Assert.AreEqual(pulsar.Index, conversions[0].Actor, "El que más Energía tiene.");
            Assert.AreEqual(NodeType.Star, conversions[0].NodeType);
            Assert.AreEqual(80f - 20f, conversions[0].Score);

            Add(Faction.Enemy, NodeType.Star, 0, new Vector2(0f, 4f));
            Assert.IsFalse(EnemyEvaluator.Candidates(Board(), Map3, Scoring).Any(a => a.Kind == EnemyActionKind.Convert), "Con una Estrella, no.");
        }

        // --- Estrategia (12.3.4) ---

        // Un protón propio de esa Carga que ya vuela hacia target.
        static BoardNode WithIncoming(BoardNode node, int charge) =>
            new BoardNode(node.Index, node.State, node.Position, node.MagnetarRadius, node.UpgradeCost, node.ConversionCost,
                node.ChargingPower, node.ReadyPower, node.PowerUpMarked,
                new[] { new BoardProton(new IncomingProton(Faction.Enemy, charge, 2f), 0) });

        // 16.
        [Test]
        public void ExpandingIsEconomyAndPrefersTheCloserAsteroid()
        {
            BoardNode own = Add(Faction.Enemy, NodeType.Star, 30, new Vector2(5f, 0f));
            BoardNode near = Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(3f, 0f));
            BoardNode far = Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(-1f, 0f));
            EnemyBoard board = Board();

            float one = EnemyEvaluator.ScoreAttack(board, own, near, Map1, Scoring).Value.Score;
            float three = EnemyEvaluator.ScoreAttack(board, own, near, Map3, Scoring).Value.Score;
            Assert.AreEqual(40f + 40f - 10f - 2f * 2f, one, 1e-4f, "Valor + necesidad con un nodo − coste − distancia.");
            Assert.AreEqual(one, three, 1e-4f, "Expandir no lleva MultiplicadorAgresividad.");
            Assert.Greater(one, EnemyEvaluator.ScoreAttack(board, own, far, Map1, Scoring).Value.Score);

            EnemyAction best = Best(board, Map1).Value;
            Assert.AreEqual(EnemyActionKind.Attack, best.Kind, "Con el perfil del mapa 1, expandir supera el umbral de 40.");
            Assert.AreEqual(near.Index, best.Target);
        }

        // 17.
        [Test]
        public void ExpansionNeedFallsWithEveryOwnNode()
        {
            Add(Faction.Enemy, NodeType.Star, 30, Vector2.zero);
            Assert.AreEqual(40f, EnemyEvaluator.ExpansionNeed(Board(), Scoring));
            Add(Faction.Enemy, NodeType.Asteroid, 0, new Vector2(0f, 2f));
            Add(Faction.Enemy, NodeType.Magnetar, 10, new Vector2(0f, -2f));
            Add(Faction.Enemy, NodeType.Star, 10, new Vector2(2f, 0f));
            Assert.AreEqual(10f, EnemyEvaluator.ExpansionNeed(Board(), Scoring));
        }

        // 18.
        [Test]
        public void ItSuppliesItsOwnAsteroidUntilItCanBeIgnited()
        {
            BoardNode star = Add(Faction.Enemy, NodeType.Star, 40, new Vector2(5f, 0f));
            BoardNode rock = Add(Faction.Enemy, NodeType.Asteroid, 2, new Vector2(3f, 0f));
            EnemyBoard board = Board();

            Assert.AreEqual(8, EnemyEvaluator.SupplyNeed(board, rock));
            Assert.AreEqual(70f, EnemyEvaluator.ScoreSupply(board, star, rock, Scoring).Value, 1e-4f, "20 cubren los 8 que faltan.");
            EnemyAction best = Best(board, Map1).Value;
            Assert.AreEqual(EnemyActionKind.Supply, best.Kind);
            Assert.AreEqual(rock.Index, best.Target);

            nodes[rock.Index] = WithIncoming(rock, 8);
            Assert.AreEqual(0, EnemyEvaluator.SupplyNeed(Board(), nodes[rock.Index]), "Lo que ya vuela lo cubre.");
            Assert.IsFalse(EnemyEvaluator.Candidates(Board(), Map1, Scoring).Any(a => a.Kind == EnemyActionKind.Supply));
        }

        // 19.
        [Test]
        public void ItSuppliesAStalledPulsarButAPulsarNeverSupplies()
        {
            BoardNode star = Add(Faction.Enemy, NodeType.Star, 40, new Vector2(5f, 0f));
            BoardNode pulsar = Add(Faction.Enemy, NodeType.Pulsar, 0, new Vector2(5f, 2f), level: 2, charging: PowerType.Decay);
            Add(Faction.Enemy, NodeType.Asteroid, 0, new Vector2(3f, 0f));
            EnemyBoard board = Board();

            Assert.AreEqual(35, EnemyEvaluator.SupplyNeed(board, pulsar), "Lo que cuesta el Decaimiento que carga.");
            Assert.AreEqual(45f * 20f / 35f, EnemyEvaluator.ScoreSupply(board, star, pulsar, Scoring).Value, 1e-3f);
            Assert.IsNull(EnemyEvaluator.ScoreSupply(board, pulsar, nodes[2], Scoring));

            nodes.Clear();
            Add(Faction.Enemy, NodeType.Star, 40, Vector2.zero);
            BoardNode full = Add(Faction.Enemy, NodeType.Pulsar, 30, new Vector2(0f, 2f));
            Assert.AreEqual(0, EnemyEvaluator.SupplyNeed(Board(), full), "A nivel 1 solo pide los 20 del Cero Absoluto.");
        }

        // 20.
        [Test]
        public void ItDoesNotRepeatAnAttackThatIsAlreadyInTheAir()
        {
            BoardNode own = Add(Faction.Enemy, NodeType.Star, 40, new Vector2(5f, 0f));
            BoardNode rock = Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(3f, 0f));
            Assert.IsTrue(EnemyEvaluator.ScoreAttack(Board(), own, rock, Map1, Scoring).HasValue);

            nodes[rock.Index] = WithIncoming(rock, 6);
            EnemyAction rest = EnemyEvaluator.ScoreAttack(Board(), own, nodes[rock.Index], Map1, Scoring).Value;
            Assert.AreEqual(40f + 40f - 4f - 4f, rest.Score, 1e-4f, "Solo cuenta lo que falta: 10 − 6.");

            nodes[rock.Index] = WithIncoming(rock, 10);
            Assert.IsNull(EnemyEvaluator.ScoreAttack(Board(), own, nodes[rock.Index], Map1, Scoring));
        }

        // 21.
        [Test]
        public void WithoutHostilityItDevelopsAndLeavesThePlayerAlone()
        {
            Add(Faction.Enemy, NodeType.Star, 50, new Vector2(5f, 0f));
            Add(Faction.Enemy, NodeType.Pulsar, 50, new Vector2(5f, 2f), ready: PowerType.AbsoluteZero);
            Add(Faction.Player, NodeType.Star, 2, new Vector2(-5f, 0f));
            Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(3f, 0f));
            EnemyBoard board = Board();

            List<EnemyAction> patient = EnemyEvaluator.Candidates(board, Map1, Scoring, hostile: false);
            Assert.IsFalse(patient.Any(a => (a.Kind == EnemyActionKind.Attack || a.Kind == EnemyActionKind.LaunchPower) && a.Target == 2));
            Assert.IsTrue(patient.Any(a => a.Kind == EnemyActionKind.Attack && a.Target == 3), "Expandir sí.");
            Assert.IsTrue(patient.Any(a => a.Kind == EnemyActionKind.Upgrade), "Mejorar sí.");

            List<EnemyAction> hostile = EnemyEvaluator.Candidates(board, Map1, Scoring);
            Assert.IsTrue(hostile.Any(a => a.Kind == EnemyActionKind.Attack && a.Target == 2));
            Assert.IsTrue(hostile.Any(a => a.Kind == EnemyActionKind.LaunchPower && a.Target == 2));
        }

        // 22. El nivel 1 tal como empieza: el primer paso es tomar el Asteroide de su lado.
        [Test]
        public void OnLevelOneItOpensByTakingItsOwnAsteroid()
        {
            Add(Faction.Player, NodeType.Star, 25, new Vector2(-5f, 0f));
            Add(Faction.Enemy, NodeType.Star, 25, new Vector2(5f, 0f));
            Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(-3.25f, 1f));
            BoardNode own = Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(3.25f, -1f));
            Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(0f, 2f));

            EnemyAction best = Best(Board(), Map1).Value;
            Assert.AreEqual(EnemyActionKind.Attack, best.Kind);
            Assert.AreEqual(own.Index, best.Target);
        }

        [Test]
        public void EconomyIsNotMultipliedByAggression()
        {
            Add(Faction.Enemy, NodeType.Asteroid, 10, Vector2.zero);
            Add(Faction.Enemy, NodeType.Star, 50, new Vector2(0f, 2f));
            EnemyBoard board = Board();

            float Score(EnemyProfile p, EnemyActionKind kind) =>
                EnemyEvaluator.Candidates(board, p, Scoring).Where(a => a.Kind == kind).Max(a => a.Score);

            Assert.AreEqual(Score(Map1, EnemyActionKind.Ignite), Score(Map3, EnemyActionKind.Ignite));
            Assert.AreEqual(Score(Map1, EnemyActionKind.Upgrade), Score(Map3, EnemyActionKind.Upgrade));
        }

        // --- Elección (12.1) ---

        [Test]
        public void ACandidateMustBeatTheThresholdStrictly()
        {
            var atThreshold = new List<EnemyAction> { new EnemyAction(EnemyActionKind.Attack, 0, 1, 25f) };
            Assert.IsNull(EnemyEvaluator.Pick(atThreshold, Map2, null), "25 no supera 25: acumula.");
            var above = new List<EnemyAction> { new EnemyAction(EnemyActionKind.Attack, 0, 1, 25.01f) };
            Assert.IsTrue(EnemyEvaluator.Pick(above, Map2, null).HasValue);
            Assert.IsNull(EnemyEvaluator.Pick(new List<EnemyAction>(), Map2, null));
        }

        [Test]
        public void NothingActsForAnotherFactionOrFromAFrozenNode()
        {
            Add(Faction.Player, NodeType.Star, 50, Vector2.zero);
            Add(Faction.Enemy, NodeType.Star, 50, new Vector2(4f, 0f), frozen: true, level: 5);
            Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(4f, 3f));

            List<EnemyAction> all = EnemyEvaluator.Candidates(Board(), Map3, Scoring);
            Assert.IsEmpty(all, "Congelado no envía, no mejora (nivel 5 y además congelado) y no hay más nodos propios.");
        }

        [Test]
        public void CandidatesComeInTheTieBreakOrder()
        {
            Add(Faction.Enemy, NodeType.Star, 50, Vector2.zero);
            Add(Faction.Enemy, NodeType.Asteroid, 10, new Vector2(0f, 2f));
            Add(Faction.Player, NodeType.Star, 10, new Vector2(5f, 0f));
            Add(Faction.Neutral, NodeType.Asteroid, 10, new Vector2(5f, 3f));

            List<EnemyAction> all = EnemyEvaluator.Candidates(Board(), Map2, Scoring);
            for (int i = 1; i < all.Count; i++)
                Assert.LessOrEqual(EnemyEvaluator.Order(all[i - 1], all[i]), 0, $"{all[i - 1]} antes que {all[i]}");
        }

        [Test]
        public void DistanceToSegmentIsMeasuredToTheSegmentNotTheLine()
        {
            Assert.AreEqual(1f, EnemyEvaluator.DistanceToSegment(new Vector2(3f, 1f), Vector2.zero, new Vector2(6f, 0f)), 1e-5f);
            Assert.AreEqual(1f, EnemyEvaluator.DistanceToSegment(new Vector2(7f, 0f), Vector2.zero, new Vector2(6f, 0f)), 1e-5f);
            Assert.AreEqual(5f, EnemyEvaluator.DistanceToSegment(new Vector2(-3f, 4f), Vector2.zero, new Vector2(6f, 0f)), 1e-5f);
        }
    }
}
