using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Enemy;
using Nova.Gameplay;
using Nova.Systems;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T5-04: power-ups del cielo (9). FactionBuffs, el calendario de PowerUpSpawner con los casos
    // 15, 16, 17 y 26 de la sección 17, la recogida al encender y el +50 % en la Estrella, el
    // Magnetar y el Púlsar.
    public class PowerUpTests
    {
        const float Tick = 0.02f;
        const int TicksPerSecond = 50;

        readonly List<GameObject> spawned = new List<GameObject>();
        NodeRegistry registry;
        FactionBuffs buffs;
        PowerUpSpawner spawner;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static PowerUpDefinition PowerUpAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerUpDefinition>($"Assets/Data/PowerUps/{name}.asset");

        static PowerUpDefinition CosmicRay => PowerUpAsset("CosmicRay");
        static PowerUpDefinition HydrogenCloud => PowerUpAsset("HydrogenCloud");

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        // La fila del mapa 1 de 9.3.
        static readonly DropSchedule MapOne = new DropSchedule(25f, 40f, 55f, 3);

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            registry = go.AddComponent<NodeRegistry>();
            buffs = go.AddComponent<FactionBuffs>();
            spawner = go.AddComponent<PowerUpSpawner>();
            spawner.Configure(Balance, new[] { CosmicRay, HydrogenCloud }, buffs, registry, 7);
        }

        [TearDown]
        public void TearDown()
        {
            registry.Clear();
            foreach (GameObject go in spawned)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            spawned.Clear();
        }

        NodeController Node(string type, Faction faction, int energy, int level = 0)
        {
            var go = new GameObject($"{faction} {type} {spawned.Count}");
            spawned.Add(go);
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, type == "Asteroid" ? 0 : Mathf.Max(1, level));
            go.AddComponent<NodeIgnition>().Configure(Balance, new[] { Definition("Star"), Definition("Pulsar"), Definition("Magnetar") });
            node.UseBuffs(buffs);
            registry.Register(node);
            return node;
        }

        // Un tablero con dos Asteroides neutrales y nada más que pueda recibir un drop.
        void Board()
        {
            Node("Star", Faction.Player, 25, 1);
            Node("Star", Faction.Enemy, 25, 1);
            Node("Asteroid", Faction.Player, 10);
            Node("Asteroid", Faction.Neutral, 10);
            Node("Asteroid", Faction.Neutral, 10);
            Node("Magnetar", Faction.Neutral, 20, 1);
        }

        void Run(float seconds) => RunTicks(Mathf.RoundToInt(seconds * TicksPerSecond));

        void RunTicks(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                buffs.Tick(Tick);
                spawner.Tick(Tick);
            }
        }

        // --- FactionBuffs (9.1) ---

        [Test]
        public void ABuffSpeedsItsStatForItsFactionOnly()
        {
            buffs.Apply(Faction.Player, CosmicRay);

            Assert.AreEqual(1.5f, buffs.Multiplier(Faction.Player, PowerUpStat.AttackSpeed), 1e-6f);
            Assert.AreEqual(1f, buffs.Multiplier(Faction.Player, PowerUpStat.EnergyGeneration), "El Rayo no toca la generación (9.2).");
            Assert.AreEqual(1f, buffs.Multiplier(Faction.Enemy, PowerUpStat.AttackSpeed), "Solo la facción que lo recoge.");
            Assert.AreEqual(1f, buffs.Multiplier(Faction.Neutral, PowerUpStat.AttackSpeed));
        }

        [Test]
        public void ABuffLastsTwentySeconds()
        {
            var expired = new List<PowerUpType>();
            buffs.OnExpired += (f, t) => expired.Add(t);
            buffs.Apply(Faction.Enemy, HydrogenCloud);

            for (int i = 0; i < 20 * TicksPerSecond - 1; i++)
                buffs.Tick(Tick);
            Assert.IsTrue(buffs.IsActive(Faction.Enemy, PowerUpType.HydrogenCloud));

            buffs.Tick(Tick);
            Assert.IsFalse(buffs.IsActive(Faction.Enemy, PowerUpType.HydrogenCloud));
            Assert.AreEqual(1f, buffs.Multiplier(Faction.Enemy, PowerUpStat.EnergyGeneration));
            CollectionAssert.AreEqual(new[] { PowerUpType.HydrogenCloud }, expired);
        }

        // 17, caso 17.
        [Test]
        public void TheSecondOfTheSameTypeRestartsTheTimerWithoutStacking()
        {
            buffs.Apply(Faction.Player, HydrogenCloud);
            for (int i = 0; i < 15 * TicksPerSecond; i++)
                buffs.Tick(Tick);

            buffs.Apply(Faction.Player, HydrogenCloud);

            Assert.AreEqual(20f, buffs.Remaining(Faction.Player, PowerUpType.HydrogenCloud), 1e-4f);
            Assert.AreEqual(1.5f, buffs.Multiplier(Faction.Player, PowerUpStat.EnergyGeneration), 1e-6f, "No acumula magnitud.");
        }

        [Test]
        public void BothTypesCanBeActiveAtOnce()
        {
            buffs.Apply(Faction.Player, CosmicRay);
            buffs.Apply(Faction.Player, HydrogenCloud);

            Assert.AreEqual(1.5f, buffs.Multiplier(Faction.Player, PowerUpStat.AttackSpeed), 1e-6f);
            Assert.AreEqual(1.5f, buffs.Multiplier(Faction.Player, PowerUpStat.EnergyGeneration), 1e-6f);
        }

        [Test]
        public void NeutralNeverCollects()
        {
            Assert.Throws<System.ArgumentException>(() => buffs.Apply(Faction.Neutral, CosmicRay));
        }

        // --- Calendario (9.1, 9.3) ---

        [Test]
        public void TheFirstDropFallsOnANeutralAsteroidAtItsTime()
        {
            Board();
            spawner.Begin(MapOne);

            RunTicks(25 * TicksPerSecond - 1);
            Assert.IsNull(spawner.Marked);

            RunTicks(1);
            NodeController marked = spawner.Marked;
            Assert.IsNotNull(marked, "25 s en el mapa 1.");
            Assert.AreEqual(NodeType.Asteroid, marked.Type);
            Assert.AreEqual(Faction.Neutral, marked.Faction);
            Assert.IsTrue(marked.GetComponent<NodeIgnition>().IsMarked);
            Assert.AreEqual(1, spawner.Dropped);
        }

        // 17, caso 15.
        [Test]
        public void NobodyIgnitesItAndItGoesOutAfterThirtySeconds()
        {
            Board();
            var expired = new List<NodeController>();
            spawner.OnExpired += (n, p) => expired.Add(n);
            spawner.Begin(MapOne);
            Run(25f);
            NodeController marked = spawner.Marked;

            RunTicks(30 * TicksPerSecond - 1);
            Assert.AreSame(marked, spawner.Marked, "Uno solo a la vez, y sigue ahí.");

            RunTicks(1);
            Assert.IsNull(spawner.Marked);
            Assert.IsFalse(marked.GetComponent<NodeIgnition>().IsMarked);
            CollectionAssert.AreEqual(new[] { marked }, expired);
            Assert.That(spawner.TimeRemaining, Is.InRange(40f, 55f), "El siguiente, a 40–55 s del apagado (9.3).");
        }

        [Test]
        public void WhoeverIgnitesTheMarkedAsteroidBuffsTheirWholeFaction()
        {
            Board();
            spawner.Begin(MapOne);
            Run(25f);
            NodeController marked = spawner.Marked;
            PowerUpDefinition powerUp = spawner.MarkedPowerUp;

            // Para encenderlo hay que poseerlo: la marca sobrevive a la captura.
            marked.Steal(Faction.Enemy);
            Assert.IsTrue(marked.GetComponent<NodeIgnition>().IsMarked);

            Assert.IsTrue(marked.GetComponent<NodeIgnition>().TryIgnite(Faction.Enemy, NodeType.Star).IsAllowed);

            Assert.IsTrue(buffs.IsActive(Faction.Enemy, powerUp.Type));
            Assert.IsFalse(buffs.IsActive(Faction.Player, powerUp.Type));
            Assert.IsNull(spawner.Marked);
            Assert.That(spawner.TimeRemaining, Is.InRange(40f, 55f), "El intervalo se cuenta desde la recogida.");
        }

        // 17, caso 16.
        [Test]
        public void WithoutNeutralAsteroidsTheDropIsSkippedAndDoesNotCount()
        {
            Node("Star", Faction.Player, 25, 1);
            Node("Star", Faction.Enemy, 25, 1);
            Node("Asteroid", Faction.Player, 10);
            int skipped = 0;
            spawner.OnSkipped += () => skipped++;
            spawner.Begin(MapOne);

            Run(25f);

            Assert.AreEqual(1, skipped);
            Assert.AreEqual(0, spawner.Dropped);
            Assert.IsTrue(spawner.IsRunning);
            Assert.That(spawner.TimeRemaining, Is.InRange(40f, 55f), "El temporizador vuelve a empezar.");
        }

        [Test]
        public void NoMoreDropsThanTheMaximum()
        {
            Board();
            spawner.Begin(new DropSchedule(1f, 1f, 1f, 2));

            Run(1f + 30f + 1f + 30f);

            Assert.AreEqual(2, spawner.Dropped);
            Assert.IsFalse(spawner.IsRunning);
            Run(60f);
            Assert.AreEqual(2, spawner.Dropped);
            Assert.IsNull(spawner.Marked);
        }

        [Test]
        public void TheSameSeedDropsTheSameWay()
        {
            Board();
            var drops = new List<string>();
            spawner.OnDropped += (n, p) => drops.Add($"{n.name} {p.Type}");

            spawner.Begin(MapOne);
            Run(25f + 3 * (30f + 55f));
            var one = new List<string>(drops);
            drops.Clear();
            spawner.Begin(MapOne);
            Run(25f + 3 * (30f + 55f));

            Assert.AreEqual(3, one.Count, "Tres drops en el mapa 1, ninguno recogido.");
            CollectionAssert.AreEqual(one, drops, "Begin resiembra: misma semilla, mismos Asteroides y tipos.");
        }

        [Test]
        public void BothTypesCanFall()
        {
            Board();
            var types = new HashSet<PowerUpType>();
            for (int seed = 0; seed < 40; seed++)
            {
                spawner.Configure(Balance, new[] { CosmicRay, HydrogenCloud }, buffs, registry, seed);
                spawner.Begin(new DropSchedule(0.02f, 1f, 1f, 1));
                RunTicks(1);
                types.Add(spawner.MarkedPowerUp.Type);
            }
            Assert.AreEqual(2, types.Count, "50 % / 50 % (9.2).");
        }

        // 17, caso 26.
        [Test]
        public void AnOwnAsteroidNeverReceivesAPowerUp()
        {
            NodeController own = Node("Asteroid", Faction.Player, 10);
            Assert.Throws<System.InvalidOperationException>(() => own.GetComponent<NodeIgnition>().Mark(CosmicRay));
        }

        // 12.3: BonusPowerUp lee la marca del tablero.
        [Test]
        public void TheEnemySeesTheMarkedAsteroid()
        {
            NodeController asteroid = Node("Asteroid", Faction.Neutral, 10);
            Assert.IsFalse(BoardNode.From(0, asteroid).PowerUpMarked);

            asteroid.GetComponent<NodeIgnition>().Mark(HydrogenCloud);
            Assert.IsTrue(BoardNode.From(0, asteroid).PowerUpMarked);
        }

        // --- +50 % (9.2) ---

        // Nube de Hidrógeno: el intervalo de 2 s del nivel 1 pasa a 1.33 s, 67 ticks en vez de 100.
        [Test]
        public void HydrogenCloudMakesTheStarGenerateAtTwoThirdsOfTheInterval()
        {
            NodeController star = Node("Star", Faction.Player, 0, 1);
            var generator = star.gameObject.AddComponent<StarGenerator>();
            buffs.Apply(Faction.Player, HydrogenCloud);

            for (int i = 0; i < 66; i++)
                generator.Tick(Tick);
            Assert.AreEqual(0, star.Energy);
            generator.Tick(Tick);
            Assert.AreEqual(1, star.Energy);
        }

        [Test]
        public void TheEnemyStarIsNotSpedUpByThePlayerCloud()
        {
            NodeController star = Node("Star", Faction.Enemy, 0, 1);
            var generator = star.gameObject.AddComponent<StarGenerator>();
            buffs.Apply(Faction.Player, HydrogenCloud);

            for (int i = 0; i < 99; i++)
                generator.Tick(Tick);
            Assert.AreEqual(0, star.Energy);
            generator.Tick(Tick);
            Assert.AreEqual(1, star.Energy);
        }

        // Rayo Cósmico: la cadencia del Magnetar sube un 50 %.
        [TestCase(false)]
        [TestCase(true)]
        public void CosmicRayRaisesTheMagnetarFireRate(bool buffed)
        {
            var systems = new GameObject("Protons");
            spawned.Add(systems);
            var protons = systems.AddComponent<ProtonRegistry>();
            NodeController far = Node("Star", Faction.Neutral, 0, 1);
            far.transform.position = new Vector3(50f, 0f, 0f);
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 10, 1);
            var turret = magnetar.gameObject.AddComponent<MagnetarTurret>();
            turret.Configure(Balance, protons);
            var go = new GameObject("Proton");
            spawned.Add(go);
            var proton = go.AddComponent<Proton>();
            proton.Launch(new Vector2(0.5f, 0f), far, Faction.Player, 100, Balance);
            protons.Register(proton);
            if (buffed)
                buffs.Apply(Faction.Enemy, CosmicRay);
            int shots = 0;
            turret.OnShot += (m, p) => shots++;

            const int ticks = 500;
            for (int i = 0; i < ticks; i++)
                turret.Tick(Tick);

            long interval = (long)System.Math.Round(1_000_000d / Definition("Magnetar").GetLevel(1).FireRate);
            long step = buffed ? 30_000L : 20_000L;
            Assert.AreEqual(1 + (ticks - 1) * step / interval, shots);
        }

        // Rayo Cósmico: el Cero Absoluto carga en 5.33 s en vez de 8, con el drenaje acelerado a la par.
        [Test]
        public void CosmicRaySpeedsUpThePulsarCharge()
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 50, 1);
            var caster = pulsar.gameObject.AddComponent<PulsarCaster>();
            caster.Configure(Balance, new[] { PowerAsset("AbsoluteZero") });
            buffs.Apply(Faction.Player, CosmicRay);
            Assert.IsTrue(caster.TryCharge(Faction.Player, PowerType.AbsoluteZero).IsAllowed);

            for (int i = 0; i < 266; i++)
                caster.Tick(Tick);
            Assert.AreEqual(PowerChargeState.Charging, caster.State);
            caster.Tick(Tick);
            Assert.AreEqual(PowerChargeState.Ready, caster.State);
            Assert.AreEqual(30, pulsar.Energy, "Cobra lo mismo, 20, en menos tiempo.");
        }
    }
}
