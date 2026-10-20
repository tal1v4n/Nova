using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Systems;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T5-02: los nueve LevelDefinition de Assets/Data/Levels/ contra lo que dice el maestro, no contra
    // la tabla de LevelAssets (Nova.Editor): la composición de 11.2, las mecánicas de 11.1, la
    // Energía de 18 #37 y las reglas del apéndice de niveles y de 11.3. Si un test falla tras
    // regenerar, lo que está mal es la tabla o el apéndice.
    public class LevelAssetsTests
    {
        const float MaxX = 6.75f;
        const float MaxY = 3.25f;
        const float MinSeparation = 1.5f;
        const float NearAsteroid = 2.25f;

        static string PathOf(int number) => $"Assets/Data/Levels/Level{number}.asset";

        // Los assets los escribe un menú, no el test: sin ellos, cada test lo dice en vez de fallar con un null.
        static LevelDefinition Level(int number)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(PathOf(number));
            if (level == null)
                Assert.Fail($"Falta {PathOf(number)}: ejecuta Nova → Fase 5 → Generar los 9 niveles.");
            return level;
        }

        static NodeDefinition Definition(NodeType type) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{type}.asset");

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        // 11.2, fila por fila: Jugador, Enemigo, Asteroides neutrales, neutrales especiales y N.
        static readonly (string player, string enemy, int asteroids, string special, int n, float duration)[] Table =
        {
            ("Star1", "Star1", 3, "", 5, 90f),
            ("Star1", "Star2", 5, "", 7, 110f),
            ("Star1 Asteroid0", "Star2 Magnetar1", 4, "", 8, 130f),
            ("Star1", "Star2 Pulsar1", 6, "", 9, 150f),
            ("Star1 Magnetar1", "Star2 Pulsar1", 6, "Magnetar2", 11, 170f),
            ("Star2", "Star3 Pulsar2 Magnetar2", 6, "Magnetar3", 11, 190f),
            ("Star1 Star1", "Star3 Pulsar3 Magnetar3", 7, "Magnetar3", 13, 210f),
            ("Star2", "Star3 Star3 Pulsar3 Magnetar3", 7, "Magnetar3 Magnetar3", 14, 230f),
            ("Star2 Magnetar1", "Star4 Star4 Pulsar3 Pulsar3 Magnetar4", 7, "Magnetar4 Magnetar4", 16, 240f),
        };

        static IEnumerable<int> Numbers => Enumerable.Range(1, 9);

        static int LevelOf(NodeSpawnData spawn) => spawn.Level > 0 ? spawn.Level : spawn.Type == NodeType.Asteroid ? 0 : 1;

        static string Composition(IEnumerable<NodeSpawnData> spawns) =>
            string.Join(" ", spawns.Select(s => $"{s.Type}{LevelOf(s)}").OrderBy(s => s));

        static string Sorted(string list) => string.Join(" ", list.Split(' ').Where(s => s.Length > 0).OrderBy(s => s));

        [Test]
        public void ThereAreNineLevelsNumberedInOrder()
        {
            foreach (int number in Numbers)
            {
                Assert.AreEqual(number, Level(number).Number);
            }
        }

        [TestCaseSource(nameof(Numbers))]
        public void EachLevelHasTheCompositionOfElevenTwo(int number)
        {
            LevelDefinition level = Level(number);
            var row = Table[number - 1];
            IReadOnlyList<NodeSpawnData> spawns = level.Spawns;

            Assert.AreEqual(row.n, spawns.Count, "N es spawns.Count (11.2).");
            Assert.AreEqual(Sorted(row.player), Composition(spawns.Where(s => s.Faction == Faction.Player)));
            Assert.AreEqual(Sorted(row.enemy), Composition(spawns.Where(s => s.Faction == Faction.Enemy)));
            Assert.AreEqual(row.asteroids, spawns.Count(s => s.Faction == Faction.Neutral && s.Type == NodeType.Asteroid));
            Assert.AreEqual(Sorted(row.special), Composition(spawns.Where(s => s.Faction == Faction.Neutral && s.Type != NodeType.Asteroid)));
            Assert.AreEqual(row.duration, level.TargetDuration);
        }

        [TestCaseSource(nameof(Numbers))]
        public void EachLevelHasTheMechanicsAndProfileOfItsMap(int number)
        {
            LevelDefinition level = Level(number);
            int map = (number - 1) / 3 + 1;

            Assert.AreEqual(map, level.Map);
            Assert.AreEqual($"Level{number}Enemy", level.EnemyProfile != null ? level.EnemyProfile.name : null,
                "Cada nivel tiene su propio perfil (12.5.2).");

            Assert.IsTrue(level.AllowsIgnition(NodeType.Star));
            Assert.IsTrue(level.AllowsIgnition(NodeType.Magnetar));
            Assert.AreEqual(map >= 2, level.AllowsIgnition(NodeType.Pulsar));
            Assert.AreEqual(map >= 2, level.AllowsPower(PowerType.AbsoluteZero));
            Assert.AreEqual(map >= 2, level.AllowsPower(PowerType.Decay));
            Assert.AreEqual(map == 3, level.AllowsPower(PowerType.Wormhole));
            Assert.AreEqual(map == 3, level.AllowsConversion);
            Assert.AreEqual(level.EnemyProfile.UsesConversion, level.AllowsConversion, "11.1 coincide con 12.5.");
        }

        // 9.3, fila del mapa: primer drop, intervalo y máximo por nivel (T5-04).
        static readonly (float first, float min, float max, int count)[] DropTable =
        {
            (25f, 40f, 55f, 3),
            (30f, 50f, 70f, 4),
            (35f, 60f, 85f, 5),
        };

        [TestCaseSource(nameof(Numbers))]
        public void EachLevelHasTheDropScheduleOfItsMap(int number)
        {
            DropSchedule drops = Level(number).Drops;
            var row = DropTable[(number - 1) / 3];
            Assert.AreEqual(row.first, drops.FirstDrop);
            Assert.AreEqual(row.min, drops.MinInterval);
            Assert.AreEqual(row.max, drops.MaxInterval);
            Assert.AreEqual(row.count, drops.MaxDrops);
        }

        // 12.5.2: los perfiles por nivel se ajustan a mano, así que no se comparan con números fijos
        // sino con reglas: valores válidos, un perfil por nivel y una curva que no se vuelve más fácil.

        [TestCaseSource(nameof(Numbers))]
        public void EachLevelProfileHasValidValues(int number)
        {
            EnemyProfile profile = Level(number).EnemyProfile;
            Assert.IsNotNull(profile);
            Assert.GreaterOrEqual(profile.DecisionInterval, 0.02f, "Intervalo de decisión.");
            Assert.GreaterOrEqual(profile.ActionThreshold, 0f, "Umbral.");
            Assert.GreaterOrEqual(profile.AggressionMultiplier, 0f, "Agresividad.");
            Assert.That(profile.DecisionNoise, Is.InRange(0f, 1f), "Ruido.");
            Assert.GreaterOrEqual(profile.GracePeriod, 0f, "Paciencia.");
            Assert.GreaterOrEqual(profile.FullSendMargin, 0f, "Margen del 100 %.");
            Assert.That(profile.FullSendMargin == 0f || profile.FullSendMargin >= 1f, Is.True,
                "Un margen entre 0 y 1 enviaría el 100 % sin llegar a capturar.");
        }

        [Test]
        public void EachLevelHasItsOwnProfile()
        {
            var profiles = Numbers.Select(n => Level(n).EnemyProfile).ToList();
            Assert.AreEqual(9, profiles.Distinct().Count(), "Dos niveles que comparten perfil no se pueden ajustar por separado.");
        }

        // Lo que Nova → Dificultad marca en rojo: de un nivel al siguiente, ninguno de los cinco
        // parámetros con sentido de curva se vuelve más fácil.
        [Test]
        public void TheDifficultyNeverGoesDownFromOneLevelToTheNext()
        {
            var failures = new List<string>();
            for (int n = 2; n <= 9; n++)
            {
                EnemyProfile before = Level(n - 1).EnemyProfile;
                EnemyProfile now = Level(n).EnemyProfile;
                if (now.DecisionInterval > before.DecisionInterval + 1e-4f) failures.Add($"Intervalo sube en el nivel {n}");
                if (now.ActionThreshold > before.ActionThreshold + 1e-4f) failures.Add($"Umbral sube en el nivel {n}");
                if (now.AggressionMultiplier < before.AggressionMultiplier - 1e-4f) failures.Add($"Agresividad baja en el nivel {n}");
                if (now.DecisionNoise > before.DecisionNoise + 1e-4f) failures.Add($"Ruido sube en el nivel {n}");
                if (now.GracePeriod > before.GracePeriod + 1e-4f) failures.Add($"Paciencia sube en el nivel {n}");
            }
            Assert.IsEmpty(failures, string.Join("; ", failures));
        }

        [TestCaseSource(nameof(Numbers))]
        public void EachNodeStartsWithTheEnergyOfEighteenThirtySeven(int number)
        {
            foreach (NodeSpawnData spawn in Level(number).Spawns)
            {
                int cap = Definition(spawn.Type).EnergyCap;
                int expected = spawn.Type == NodeType.Asteroid ? Balance.InertMatter
                    : spawn.Faction == Faction.Neutral ? cap
                    : cap / 2;
                Assert.AreEqual(expected, spawn.InitialEnergy, $"{spawn.Faction} {spawn.Type} en {spawn.Position}");
            }
        }

        [TestCaseSource(nameof(Numbers))]
        public void EachLevelFitsTheBoardWithRoomBetweenNodes(int number)
        {
            IReadOnlyList<NodeSpawnData> spawns = Level(number).Spawns;
            for (int i = 0; i < spawns.Count; i++)
            {
                Vector2 p = spawns[i].Position;
                Assert.LessOrEqual(Mathf.Abs(p.x), MaxX, $"{p}");
                Assert.LessOrEqual(Mathf.Abs(p.y), MaxY, $"{p}");
                for (int j = i + 1; j < spawns.Count; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(p, spawns[j].Position), MinSeparation, $"{p} y {spawns[j].Position}");
            }
        }

        // 11.3: un Asteroide neutral a ≤ 2.25 u de un nodo inicial encendido del Jugador, más cerca de
        // él que de cualquier nodo del Enemigo y fuera del radio de todo Magnetar que no sea suyo.
        [TestCaseSource(nameof(Numbers))]
        public void EachLevelGivesThePlayerAnAsteroidWithinReach(int number)
        {
            IReadOnlyList<NodeSpawnData> spawns = Level(number).Spawns;
            var lit = spawns.Where(s => s.Faction == Faction.Player && s.Type != NodeType.Asteroid).ToList();
            var enemy = spawns.Where(s => s.Faction == Faction.Enemy).ToList();
            var walls = spawns.Where(s => s.Type == NodeType.Magnetar && s.Faction != Faction.Player).ToList();

            bool any = spawns.Where(s => s.Faction == Faction.Neutral && s.Type == NodeType.Asteroid).Any(a =>
            {
                float toPlayer = lit.Min(p => Vector2.Distance(a.Position, p.Position));
                float toEnemy = enemy.Min(e => Vector2.Distance(a.Position, e.Position));
                bool covered = walls.Any(m => Vector2.Distance(a.Position, m.Position) <= Definition(NodeType.Magnetar).GetLevel(LevelOf(m)).Radius);
                return toPlayer <= NearAsteroid && toPlayer < toEnemy && !covered;
            });
            Assert.IsTrue(any);
        }

        [TestCaseSource(nameof(Numbers))]
        public void EachLevelLoads(int number)
        {
            var objects = new List<Object>();
            try
            {
                var systems = new GameObject("Systems");
                objects.Add(systems);
                var registry = systems.AddComponent<NodeRegistry>();
                var template = new GameObject("Node");
                objects.Add(template);
                template.AddComponent<NodeController>();
                template.AddComponent<NodeIgnition>();
                template.AddComponent<NodeConverter>();
                template.AddComponent<PulsarCaster>();
                var loader = systems.AddComponent<LevelLoader>();
                loader.Configure(Balance, template.GetComponent<NodeController>(),
                    new[] { NodeType.Asteroid, NodeType.Star, NodeType.Pulsar, NodeType.Magnetar }.Select(Definition),
                    new[] { "AbsoluteZero", "Decay", "Wormhole" }.Select(n => AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{n}.asset")),
                    registry);

                loader.Load(Level(number));

                Assert.AreEqual(Level(number).Spawns.Count, registry.Count);
                loader.Unload();
                registry.Clear();
            }
            finally
            {
                foreach (Object item in objects)
                {
                    if (item != null)
                        Object.DestroyImmediate(item);
                }
            }
        }
    }
}
