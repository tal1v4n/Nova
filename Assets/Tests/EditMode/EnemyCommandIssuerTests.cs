using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Enemy;
using Nova.Gameplay;
using Nova.Input;
using Nova.Systems;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T4-03: EnemyCommandIssuer (12.7, 15.1 #5). Los comandos son los de NodeCommandIssuer, los mismos
    // que los del Jugador (3.1), y Execute traduce una EnemyAction al que le toca.
    public class EnemyCommandIssuerTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();
        readonly List<NodeController> nodes = new List<NodeController>();
        EnemyCommandIssuer issuer;
        PowerBeamSystem beams;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            beams = go.AddComponent<PowerBeamSystem>();
            var pool = go.AddComponent<ProtonPool>();
            var launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, pool);
            issuer = go.AddComponent<EnemyCommandIssuer>();
            issuer.Configure(launcher);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            spawned.Clear();
            nodes.Clear();
        }

        NodeController Node(string type, Faction faction, int energy, Vector2 position, int level = 1)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, type == "Asteroid" ? 0 : level);
            var types = new[] { Definition("Star"), Definition("Pulsar"), Definition("Magnetar") };
            go.AddComponent<NodeIgnition>().Configure(Balance, types);
            go.AddComponent<NodeConverter>().Configure(Balance, types);
            go.AddComponent<NodeFreeze>().Configure(Balance);
            go.AddComponent<PulsarCaster>().Configure(Balance, new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") }, beams);
            nodes.Add(node);
            return node;
        }

        int I(NodeController node) => nodes.IndexOf(node);

        // --- Paridad (3.1) ---

        [Test]
        public void ItActsForTheEnemy()
        {
            Assert.AreEqual(Faction.Enemy, issuer.Faction);
            Assert.IsInstanceOf<INodeCommandIssuer>(issuer);
        }

        [Test]
        public void BothSidesShareTheSameCommands()
        {
            Assert.IsTrue(typeof(NodeCommandIssuer).IsAssignableFrom(typeof(EnemyCommandIssuer)));
            Assert.IsTrue(typeof(NodeCommandIssuer).IsAssignableFrom(typeof(PlayerCommandIssuer)));

            // Cada método de INodeCommandIssuer lo implementa NodeCommandIssuer, no cada bando.
            foreach (System.Type type in new[] { typeof(EnemyCommandIssuer), typeof(PlayerCommandIssuer) })
            {
                InterfaceMapping map = type.GetInterfaceMap(typeof(INodeCommandIssuer));
                foreach (MethodInfo method in map.TargetMethods.Where(m => m.Name != "get_Faction"))
                    Assert.AreEqual(typeof(NodeCommandIssuer), method.DeclaringType, $"{type.Name}.{method.Name}");
            }

            // Y ningún bando declara un comando propio más allá de su comodidad.
            string[] Declared(System.Type t) => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName).Select(m => m.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Execute" }, Declared(typeof(EnemyCommandIssuer)));
            CollectionAssert.AreEquivalent(new[] { "SendFromEach" }, Declared(typeof(PlayerCommandIssuer)));
        }

        // --- Execute ---

        [TestCase(false, 20)]
        [TestCase(true, 0)]
        public void AttackSendsHalfOrAll(bool fullSend, int left)
        {
            NodeController own = Node("Star", Faction.Enemy, 40, new Vector2(0f, 0f));
            NodeController target = Node("Star", Faction.Player, 10, new Vector2(5f, 0f));

            Assert.IsTrue(issuer.Execute(new EnemyAction(EnemyActionKind.Attack, I(own), I(target), 50f, fullSend: fullSend), nodes));

            Assert.AreEqual(left, own.Energy);
        }

        [Test]
        public void ReinforceAlwaysSendsHalf()
        {
            NodeController donor = Node("Star", Faction.Enemy, 40, new Vector2(0f, 0f));
            NodeController ally = Node("Star", Faction.Enemy, 5, new Vector2(3f, 0f));

            Assert.IsTrue(issuer.Execute(new EnemyAction(EnemyActionKind.Reinforce, I(donor), I(ally), 0f, fullSend: true), nodes));

            Assert.AreEqual(20, donor.Energy);
        }

        [Test]
        public void IgniteUpgradeAndConvertGoThroughTheirComponents()
        {
            NodeController rock = Node("Asteroid", Faction.Enemy, 10, new Vector2(0f, 0f));
            NodeController star = Node("Star", Faction.Enemy, 30, new Vector2(3f, 0f));
            NodeController pulsar = Node("Pulsar", Faction.Enemy, 30, new Vector2(-3f, 0f));

            Assert.IsTrue(issuer.Execute(new EnemyAction(EnemyActionKind.Ignite, I(rock), -1, 0f, nodeType: NodeType.Magnetar), nodes));
            Assert.AreEqual(NodeType.Magnetar, rock.Type);

            Assert.IsTrue(issuer.Execute(new EnemyAction(EnemyActionKind.Upgrade, I(star), -1, 0f), nodes));
            Assert.AreEqual(2, star.Level);

            Assert.IsTrue(issuer.Execute(new EnemyAction(EnemyActionKind.Convert, I(pulsar), -1, 0f, nodeType: NodeType.Star), nodes));
            Assert.IsTrue(pulsar.GetComponent<NodeConverter>().IsConverting);
            Assert.AreEqual(NodeType.Star, pulsar.GetComponent<NodeConverter>().TargetType);
        }

        [Test]
        public void ChargeAndLaunchAPower()
        {
            NodeController pulsar = Node("Pulsar", Faction.Enemy, 60, new Vector2(-4f, 0f));
            NodeController target = Node("Star", Faction.Player, 20, new Vector2(4f, 0f));
            var caster = pulsar.GetComponent<PulsarCaster>();

            Assert.IsTrue(issuer.Execute(new EnemyAction(EnemyActionKind.ChargePower, I(pulsar), -1, 0f, power: PowerType.AbsoluteZero), nodes));
            Assert.AreEqual(PowerChargeState.Charging, caster.State);
            for (int i = 0; i < 400; i++)
                caster.Tick(Tick);

            Assert.IsTrue(issuer.Execute(new EnemyAction(EnemyActionKind.LaunchPower, I(pulsar), I(target), 0f, power: PowerType.AbsoluteZero), nodes));
            Assert.AreEqual(1, beams.Active.Count);
            Assert.AreEqual(Faction.Enemy, beams.Active[0].Launcher);
            Assert.AreSame(target, beams.Active[0].Target);
        }

        // --- Las reglas son las mismas (12.6) ---

        [Test]
        public void TheEnemyCannotCommandNodesThatAreNotItsOwn()
        {
            NodeController player = Node("Star", Faction.Player, 40, new Vector2(0f, 0f));
            NodeController neutral = Node("Magnetar", Faction.Neutral, 40, new Vector2(3f, 0f));
            NodeController target = Node("Star", Faction.Enemy, 10, new Vector2(5f, 0f));

            Assert.IsFalse(issuer.Execute(new EnemyAction(EnemyActionKind.Attack, I(player), I(target), 0f), nodes));
            Assert.IsFalse(issuer.Execute(new EnemyAction(EnemyActionKind.Upgrade, I(player), -1, 0f), nodes));
            Assert.IsFalse(issuer.Execute(new EnemyAction(EnemyActionKind.Attack, I(neutral), I(target), 0f), nodes));
            Assert.AreEqual(40, player.Energy);
            Assert.AreEqual(1, player.Level);
        }

        [Test]
        public void AnActionThatIsNoLongerLegalSimplyDoesNotHappen()
        {
            NodeController star = Node("Star", Faction.Enemy, 30, new Vector2(0f, 0f));
            NodeController pulsar = Node("Pulsar", Faction.Enemy, 60, new Vector2(3f, 0f));
            NodeController target = Node("Star", Faction.Player, 10, new Vector2(5f, 0f));
            star.Freeze();

            Assert.IsFalse(issuer.Execute(new EnemyAction(EnemyActionKind.Attack, I(star), I(target), 0f), nodes), "7.3: congelado no envía.");
            Assert.IsFalse(issuer.Execute(new EnemyAction(EnemyActionKind.LaunchPower, I(pulsar), I(target), 0f), nodes), "Sin poder listo.");
            Assert.IsFalse(issuer.Execute(new EnemyAction(EnemyActionKind.ChargePower, I(pulsar), -1, 0f, power: PowerType.Wormhole), nodes), "Nivel 1.");
            Assert.AreEqual(30, star.Energy);
        }

        [Test]
        public void AnIndexOutsideTheBoardIsAnError()
        {
            Node("Star", Faction.Enemy, 30, new Vector2(0f, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                issuer.Execute(new EnemyAction(EnemyActionKind.Attack, 0, 5, 0f), nodes));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                issuer.Execute(new EnemyAction(EnemyActionKind.Upgrade, -1, -1, 0f), nodes));
        }
    }
}
