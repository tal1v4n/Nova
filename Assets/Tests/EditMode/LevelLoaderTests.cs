using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Enemy;
using Nova.Gameplay;
using Nova.Systems;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nova.Tests
{
    // T5-01: LevelDefinition, NodeSpawnData y LevelLoader (15.4, 15.6). Cargar un nivel crea sus
    // nodos en orden, inicializados y registrados, y les da las mecánicas del nivel (11.1, 18 #36),
    // que el Enemigo ve igual que el Jugador.
    public class LevelLoaderTests
    {
        readonly List<Object> spawned = new List<Object>();
        NodeRegistry registry;
        LevelLoader loader;
        GameObject template;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static PowerDefinition[] Powers => new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") };

        static NodeDefinition[] Definitions => new[] { Definition("Asteroid"), Definition("Star"), Definition("Pulsar"), Definition("Magnetar") };

        static EnemyProfile Profile(string name) =>
            AssetDatabase.LoadAssetAtPath<EnemyProfile>($"Assets/Data/EnemyProfiles/{name}.asset");

        static EnemyScoring Scoring => AssetDatabase.LoadAssetAtPath<EnemyScoring>("Assets/Data/EnemyScoring.asset");

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        static readonly NodeType[] AllTypes = { NodeType.Star, NodeType.Pulsar, NodeType.Magnetar };

        [SetUp]
        public void SetUp()
        {
            var systems = new GameObject("Systems");
            spawned.Add(systems);
            registry = systems.AddComponent<NodeRegistry>();

            // La plantilla: lo que escribe «Nova → Generar plantilla de nodo», sin las vistas.
            template = new GameObject("Node");
            spawned.Add(template);
            template.AddComponent<NodeController>();
            template.AddComponent<NodeIgnition>().Configure(Balance, AllTypes.Select(t => Definition(t.ToString())));
            template.AddComponent<NodeConverter>().Configure(Balance, AllTypes.Select(t => Definition(t.ToString())));
            template.AddComponent<NodeFreeze>().Configure(Balance);
            template.AddComponent<PulsarCaster>().Configure(Balance, Powers);

            var go = new GameObject("Level");
            spawned.Add(go);
            loader = go.AddComponent<LevelLoader>();
            loader.Configure(Balance, template.GetComponent<NodeController>(), Definitions, Powers, registry);
        }

        [TearDown]
        public void TearDown()
        {
            loader.Unload();
            registry.Clear();
            foreach (Object item in spawned)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }
            spawned.Clear();
        }

        // Las mecánicas de 11.1 por mapa.
        LevelDefinition Level(int number, params NodeSpawnData[] nodes)
        {
            int map = (number - 1) / 3 + 1;
            NodeType[] ignition = map == 1 ? new[] { NodeType.Star, NodeType.Magnetar } : AllTypes;
            PowerType[] powers = map == 1 ? new PowerType[0]
                : map == 2 ? new[] { PowerType.AbsoluteZero, PowerType.Decay }
                : new[] { PowerType.AbsoluteZero, PowerType.Decay, PowerType.Wormhole };

            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.name = $"Level{number}";
            spawned.Add(level);
            level.Configure(number, Profile($"Map{map}"), 90f, nodes, ignition, powers, map == 3);
            return level;
        }

        static NodeSpawnData Spawn(NodeType type, Faction faction, int energy, float x, float y, int level = 0) =>
            new NodeSpawnData(new Vector2(x, y), type, faction, energy, level);

        // --- LevelDefinition ---

        [TestCase(1, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(6, 2)]
        [TestCase(7, 3)]
        [TestCase(9, 3)]
        public void TheMapComesFromTheLevelNumber(int number, int map)
        {
            Assert.AreEqual(map, Level(number).Map);
        }

        [Test]
        public void ALevelNumberOutsideOneToNineIsRejected()
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            spawned.Add(level);
            Assert.Throws<ArgumentOutOfRangeException>(() => level.Configure(0, null, 0f, null, null, null, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => level.Configure(10, null, 0f, null, null, null, false));
        }

        [Test]
        public void TheDefinitionSaysWhatTheLevelAllows()
        {
            LevelDefinition level = Level(4);
            Assert.IsTrue(level.AllowsIgnition(NodeType.Pulsar));
            Assert.IsTrue(level.AllowsPower(PowerType.Decay));
            Assert.IsFalse(level.AllowsPower(PowerType.Wormhole));
            Assert.IsFalse(level.AllowsConversion);
            Assert.IsFalse(Level(1).AllowsIgnition(NodeType.Pulsar));
        }

        // --- Carga ---

        [Test]
        public void LoadingCreatesEachNodeAsItsSpawnSays()
        {
            LevelDefinition level = Level(2,
                Spawn(NodeType.Star, Faction.Player, 25, -5.5f, 0f),
                Spawn(NodeType.Star, Faction.Enemy, 25, 5.5f, 0f, level: 2),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -3.5f, 0.75f),
                Spawn(NodeType.Magnetar, Faction.Neutral, 100, 0f, 0f, level: 3));

            loader.Load(level);

            Assert.AreSame(level, loader.Current);
            Assert.AreEqual(4, loader.Nodes.Count);
            CollectionAssert.AreEqual(loader.Nodes, registry.All, "Registrados en el orden de sus NodeSpawnData (12.3.3).");

            NodeController player = loader.Nodes[0];
            Assert.IsTrue(player.IsInitialized);
            Assert.AreEqual(new Vector3(-5.5f, 0f, 0f), player.transform.position);
            Assert.AreEqual(NodeType.Star, player.Type);
            Assert.AreEqual(Faction.Player, player.Faction);
            Assert.AreEqual(25, player.Energy);
            Assert.AreEqual(1, player.Level, "0 es el de partida: 1 para un tipo encendido.");
            Assert.AreSame(loader.transform, player.transform.parent);

            Assert.AreEqual(2, loader.Nodes[1].Level);
            Assert.AreEqual(0, loader.Nodes[2].Level, "El Asteroide empieza en 0.");
            Assert.AreEqual(Faction.Neutral, loader.Nodes[3].Faction);
            Assert.AreEqual(100, loader.Nodes[3].Energy);
            Assert.AreEqual(3, loader.Nodes[3].Level);
            Assert.AreEqual(1, registry.CountOf(Faction.Player));
            Assert.AreEqual(2, registry.CountOf(Faction.Neutral));
        }

        [Test]
        public void LoadingAnotherLevelReplacesTheFirst()
        {
            loader.Load(Level(1, Spawn(NodeType.Star, Faction.Player, 25, -5f, 0f), Spawn(NodeType.Star, Faction.Enemy, 25, 5f, 0f)));
            NodeController first = loader.Nodes[0];

            loader.Load(Level(2, Spawn(NodeType.Star, Faction.Player, 25, -5.5f, 0f)));

            Assert.AreEqual(1, registry.Count);
            Assert.AreEqual(1, loader.Nodes.Count);
            Assert.IsTrue(first == null, "El nodo del nivel anterior se destruye.");
            Assert.AreEqual(1, loader.transform.childCount);
        }

        [Test]
        public void ABrokenLevelLeavesThePreviousOneAsItWas()
        {
            LevelDefinition good = Level(1, Spawn(NodeType.Star, Faction.Player, 25, -5f, 0f));
            loader.Load(good);

            LevelDefinition overCap = Level(2, Spawn(NodeType.Star, Faction.Player, 25, -5f, 0f), Spawn(NodeType.Star, Faction.Enemy, 51, 5f, 0f));
            Assert.Throws<InvalidOperationException>(() => loader.Load(overCap), "51 no cabe en el Tope 50 de la Estrella.");
            LevelDefinition overLevel = Level(2, Spawn(NodeType.Asteroid, Faction.Neutral, 10, 0f, 0f, level: 1));
            Assert.Throws<InvalidOperationException>(() => loader.Load(overLevel), "El Asteroide no tiene nivel 1.");

            Assert.AreSame(good, loader.Current);
            Assert.AreEqual(1, registry.Count);
            Assert.AreEqual(1, loader.transform.childCount);
        }

        [Test]
        public void ATypeWithoutDefinitionIsAnError()
        {
            loader.Configure(Balance, template.GetComponent<NodeController>(), new[] { Definition("Star") }, Powers, registry);
            Assert.Throws<InvalidOperationException>(() => loader.Load(Level(1, Spawn(NodeType.Asteroid, Faction.Neutral, 10, 0f, 0f))),
                "Ni el Asteroide como nodo ni el Magnetar del encendido del mapa 1.");
            Assert.AreEqual(0, registry.Count);
        }

        // --- Mecánicas del nivel (11.1) ---

        [Test]
        public void MapOneLightsStarsAndMagnetarsAndNothingElse()
        {
            loader.Load(Level(1,
                Spawn(NodeType.Asteroid, Faction.Player, 10, -5f, 0f),
                Spawn(NodeType.Pulsar, Faction.Player, 100, -5f, 2f, level: 3),
                Spawn(NodeType.Star, Faction.Player, 40, -5f, -2f)));
            NodeController rock = loader.Nodes[0];
            NodeController pulsar = loader.Nodes[1];
            NodeController star = loader.Nodes[2];
            var ignition = rock.GetComponent<NodeIgnition>();

            Assert.IsTrue(ignition.Check(Faction.Player, NodeType.Star).IsAllowed);
            Assert.IsTrue(ignition.Check(Faction.Player, NodeType.Magnetar).IsAllowed);
            Assert.AreEqual(ActionBlock.Unavailable, ignition.Check(Faction.Player, NodeType.Pulsar).Block, "Gris y sin coste (18 #29).");

            var caster = pulsar.GetComponent<PulsarCaster>();
            foreach (PowerType power in new[] { PowerType.AbsoluteZero, PowerType.Decay, PowerType.Wormhole })
                Assert.AreEqual(ActionBlock.Unavailable, caster.CheckCharge(Faction.Player, power).Block,
                    $"{power}: un Púlsar que ya está en el tablero funciona, pero el mapa 1 no tiene poderes.");

            Assert.AreEqual(ActionBlock.Unavailable, star.GetComponent<NodeConverter>().Check(Faction.Player, NodeType.Magnetar).Block,
                "Sin reconversión hasta el mapa 3.");
            Assert.IsTrue(star.TryUpgrade(Faction.Player).IsAllowed, "Las mejoras existen desde el mapa 1.");
        }

        [Test]
        public void MapTwoChargesTwoPowersAndStillDoesNotReconvert()
        {
            loader.Load(Level(5, Spawn(NodeType.Pulsar, Faction.Player, 100, -5f, 0f, level: 3)));
            NodeController pulsar = loader.Nodes[0];
            var caster = pulsar.GetComponent<PulsarCaster>();

            Assert.IsTrue(caster.CheckCharge(Faction.Player, PowerType.AbsoluteZero).IsAllowed);
            Assert.IsTrue(caster.CheckCharge(Faction.Player, PowerType.Decay).IsAllowed);
            Assert.AreEqual(ActionBlock.Unavailable, caster.CheckCharge(Faction.Player, PowerType.Wormhole).Block);
            Assert.AreEqual(ActionBlock.Unavailable, pulsar.GetComponent<NodeConverter>().Check(Faction.Player, NodeType.Star).Block);
        }

        [Test]
        public void MapThreeHasEverything()
        {
            loader.Load(Level(9,
                Spawn(NodeType.Pulsar, Faction.Player, 100, -5f, 0f, level: 3),
                Spawn(NodeType.Asteroid, Faction.Player, 10, -5f, 2f)));
            var caster = loader.Nodes[0].GetComponent<PulsarCaster>();
            var ignition = loader.Nodes[1].GetComponent<NodeIgnition>();

            Assert.IsTrue(caster.CheckCharge(Faction.Player, PowerType.Wormhole).IsAllowed);
            Assert.IsTrue(loader.Nodes[0].GetComponent<NodeConverter>().Check(Faction.Player, NodeType.Star).IsAllowed);
            foreach (NodeType type in AllTypes)
                Assert.IsTrue(ignition.Check(Faction.Player, type).IsAllowed, type.ToString());
        }

        // --- El Enemigo ve las mismas mecánicas (3.1) ---

        EnemyBoard Board() => EnemyBoard.Capture(Faction.Enemy, registry.All, Balance, Powers);

        [Test]
        public void TheEnemyDoesNotLightWhatTheMapDoesNotHave()
        {
            loader.Load(Level(1,
                Spawn(NodeType.Asteroid, Faction.Enemy, 10, 5f, 0f),
                Spawn(NodeType.Star, Faction.Enemy, 25, 5f, 2f),
                Spawn(NodeType.Star, Faction.Player, 25, -5f, 0f)));

            BoardNode rock = Board().Nodes[0];
            Assert.IsTrue(rock.OffersIgnition(NodeType.Star));
            Assert.IsFalse(rock.OffersIgnition(NodeType.Pulsar));

            List<EnemyAction> ignitions = EnemyEvaluator.Candidates(Board(), Profile("Map1"), Scoring)
                .Where(a => a.Kind == EnemyActionKind.Ignite).ToList();
            CollectionAssert.AreEquivalent(new[] { NodeType.Star, NodeType.Magnetar }, ignitions.Select(a => a.NodeType));
        }

        [Test]
        public void TheEnemyChargesOnlyThePowersOfTheMap()
        {
            loader.Load(Level(6,
                Spawn(NodeType.Pulsar, Faction.Enemy, 100, 5f, 0f, level: 3),
                Spawn(NodeType.Star, Faction.Player, 25, -5f, 0f)));

            List<EnemyAction> charges = EnemyEvaluator.Candidates(Board(), Profile("Map2"), Scoring)
                .Where(a => a.Kind == EnemyActionKind.ChargePower).ToList();
            CollectionAssert.AreEquivalent(new[] { PowerType.AbsoluteZero, PowerType.Decay }, charges.Select(a => a.Power),
                "El Agujero de Gusano pide nivel 3 y lo tiene, pero el mapa 2 no lo permite.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheEnemyReconvertsOnlyWhereTheLevelAllowsIt(bool allowed)
        {
            LevelDefinition level = Level(allowed ? 7 : 4,
                Spawn(NodeType.Pulsar, Faction.Enemy, 60, 5f, 0f),
                Spawn(NodeType.Star, Faction.Player, 25, -5f, 0f));
            loader.Load(level);

            // El perfil del mapa 3 reconvierte; quien manda es el nivel.
            bool converts = EnemyEvaluator.Candidates(Board(), Profile("Map3"), Scoring).Any(a => a.Kind == EnemyActionKind.Convert);
            Assert.AreEqual(allowed, converts);
        }

        [Test]
        public void AHandMadeBoardOffersEverything()
        {
            var state = new NodeSnapshot(Faction.Enemy, NodeType.Asteroid, 10, 10);
            var open = new BoardNode(0, state, Vector2.zero);
            var closed = new BoardNode(0, state, Vector2.zero, ignitions: new[] { NodeType.Star }, conversions: new NodeType[0],
                chargeable: new PowerType[0]);

            Assert.IsTrue(open.OffersIgnition(NodeType.Pulsar) && open.OffersConversion(NodeType.Star) && open.OffersPower(PowerType.Wormhole),
                "Los tests de la fase 4 construyen tableros sin nivel: todo disponible.");
            Assert.IsTrue(closed.OffersIgnition(NodeType.Star));
            Assert.IsFalse(closed.OffersIgnition(NodeType.Pulsar));
            Assert.IsFalse(closed.OffersConversion(NodeType.Star));
            Assert.IsFalse(closed.OffersPower(PowerType.AbsoluteZero));
        }
    }
}
