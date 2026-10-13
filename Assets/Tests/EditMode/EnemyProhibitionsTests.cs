using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
    // T4-06: las cuatro prohibiciones de 12.6. El algoritmo no puede ver protones antes de que se
    // lancen, conocer los poderes del Jugador antes de que su barra sea visible, generar Energía
    // extra, ni ignorar tiempos de carga o de reconversión. Cada una se prueba sobre nodos reales y,
    // donde el tablero basta, sobre un EnemyBoard hecho a mano.
    public class EnemyProhibitionsTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();
        NodeRegistry registry;
        ProtonRegistry protons;
        PowerBeamSystem beams;
        ProtonLauncher launcher;
        EnemyCommandIssuer issuer;
        EnemyController controller;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(string name) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{name}.asset");

        static PowerDefinition[] Powers => new[] { PowerAsset("AbsoluteZero"), PowerAsset("Decay"), PowerAsset("Wormhole") };

        static EnemyProfile Profile(string name) =>
            AssetDatabase.LoadAssetAtPath<EnemyProfile>($"Assets/Data/EnemyProfiles/{name}.asset");

        static EnemyScoring Scoring => AssetDatabase.LoadAssetAtPath<EnemyScoring>("Assets/Data/EnemyScoring.asset");

        static BalanceConfig Balance => AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            Build("Map3", 7);
        }

        [TearDown]
        public void TearDown()
        {
            Clear();
        }

        void Build(string profile, int seed)
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            registry = go.AddComponent<NodeRegistry>();
            protons = go.AddComponent<ProtonRegistry>();
            beams = go.AddComponent<PowerBeamSystem>();
            var pool = go.AddComponent<ProtonPool>();
            launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, pool, protons);
            issuer = go.AddComponent<EnemyCommandIssuer>();
            issuer.Configure(launcher);
            controller = go.AddComponent<EnemyController>();
            controller.Configure(Profile(profile), Scoring, Balance, issuer, Powers, registry, seed, protons);
        }

        void Clear()
        {
            if (registry != null)
                registry.Clear();
            foreach (GameObject go in spawned)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            spawned.Clear();
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
            go.AddComponent<PulsarCaster>().Configure(Balance, Powers, beams);
            registry.Register(node);
            return node;
        }

        EnemyBoard Capture() => EnemyBoard.Capture(Faction.Enemy, registry.All, Balance, Powers, protons);

        // --- 1. Protones: solo los ya lanzados ---

        [Test]
        public void AProtonIsSeenOnlyOnceItHasBeenLaunched()
        {
            NodeController player = Node("Star", Faction.Player, 50, new Vector2(-6f, 0f));
            NodeController target = Node("Star", Faction.Enemy, 10, new Vector2(4f, 0f));
            Node("Star", Faction.Enemy, 40, new Vector2(5f, 0f));
            int index = registry.All.ToList().IndexOf(target);

            EnemyBoard before = Capture();
            Assert.IsEmpty(before.Find(index).Incoming, "El Jugador tiene 50 y un clic de distancia, pero nada ha salido todavía.");
            Assert.IsNull(EnemyThreatResponder.Respond(before, controller.Profile), "Sin protón en vuelo no hay amenaza que defender.");

            Assert.AreEqual(LaunchBlock.None, launcher.TryLaunch(player, target, Faction.Player, false, out Proton proton));

            EnemyBoard after = Capture();
            IReadOnlyList<BoardProton> seen = after.Find(index).Incoming;
            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual(proton.Charge, seen[0].Proton.Charge);
            Assert.AreEqual(EnemyActionKind.Reinforce, EnemyThreatResponder.Respond(after, controller.Profile).Value.Kind,
                "En cuanto sale, sí: el donante llega antes que el protón.");
        }

        // --- 2. Poderes: lo que enseña la barra, desde que aparece ---

        [Test]
        public void APowerIsSeenFromTheMomentItsBarAppears()
        {
            NodeController pulsar = Node("Pulsar", Faction.Player, 60, new Vector2(-4f, 0f));
            var caster = pulsar.GetComponent<PulsarCaster>();

            BoardNode PlayerPulsar() => Capture().Nodes[0];

            Assert.IsNull(PlayerPulsar().ChargingPower, "Sin carga no hay barra, y nada que saber.");
            Assert.IsNull(PlayerPulsar().ReadyPower);

            // 13.5: al iniciar la carga aparece la barra bajo el nodo, también la del rival (7.5).
            Assert.IsTrue(caster.TryCharge(Faction.Player, PowerType.AbsoluteZero).IsAllowed);
            Assert.AreEqual(PowerType.AbsoluteZero, PlayerPulsar().ChargingPower);
            Assert.IsNull(PlayerPulsar().ReadyPower);

            for (int i = 0; i < 1000 && caster.State != PowerChargeState.Ready; i++)
                caster.Tick(Tick);
            Assert.AreEqual(PowerChargeState.Ready, caster.State);
            Assert.IsNull(PlayerPulsar().ChargingPower);
            Assert.AreEqual(PowerType.AbsoluteZero, PlayerPulsar().ReadyPower);

            Assert.IsTrue(caster.TryCancel(Faction.Player).IsAllowed);
            Assert.IsNull(PlayerPulsar().ChargingPower, "Cancelada, la barra se va y el Enemigo deja de verla.");
            Assert.IsNull(PlayerPulsar().ReadyPower);
        }

        [Test]
        public void TheBoardCarriesNothingOfAPowerButWhatTheBarShows()
        {
            // El poder y si está listo: ni a quién va a apuntar el Jugador ni cuánto le falta en
            // segundos, que la barra no enseña.
            string[] properties = typeof(BoardNode).GetProperties().Select(p => p.Name)
                .Where(n => n.Contains("Power")).ToArray();
            CollectionAssert.AreEquivalent(new[] { "ChargingPower", "ReadyPower", "PowerUpMarked" }, properties);
        }

        // --- 3. Energía: ninguna sale de la nada ---

        // Toda la Energía de la partida: la de los nodos, la invertida en cargas y la de los protones
        // en vuelo. Sin generación ni impactos, solo puede bajar.
        int TotalEnergy()
        {
            int total = 0;
            foreach (NodeController node in registry.All)
            {
                total += node.Energy + node.GetComponent<PulsarCaster>().Invested;
                foreach (Proton proton in protons.GetIncoming(node))
                {
                    if (proton != null && proton.InFlight)
                        total += proton.Charge;
                }
            }
            return total;
        }

        [TestCase("Map1", 1)]
        [TestCase("Map2", 2)]
        [TestCase("Map3", 3)]
        public void TheEnemyNeverCreatesEnergy(string profile, int seed)
        {
            Clear();
            Build(profile, seed);
            Node("Star", Faction.Enemy, 40, new Vector2(5f, 0f));
            Node("Pulsar", Faction.Enemy, 60, new Vector2(5f, 3f), level: 3);
            Node("Asteroid", Faction.Enemy, 10, new Vector2(3f, -2f));
            Node("Star", Faction.Player, 12, new Vector2(-5f, 0f));
            Node("Pulsar", Faction.Player, 30, new Vector2(-5f, 3f));
            Node("Asteroid", Faction.Neutral, 10, new Vector2(0f, 2f));
            Node("Magnetar", Faction.Neutral, 30, new Vector2(0f, -3f));

            int executed = 0;
            int before = TotalEnergy();
            for (int cycle = 0; cycle < 40; cycle++)
            {
                controller.Decide();
                if (controller.LastExecuted)
                    executed++;
                // Las cargas avanzan entre ciclos: pasan Energía del Púlsar a la barra, sin crearla.
                foreach (NodeController node in registry.All)
                {
                    for (int t = 0; t < 25; t++)
                        node.GetComponent<PulsarCaster>().Tick(Tick);
                }

                int after = TotalEnergy();
                Assert.LessOrEqual(after, before, $"Ciclo {cycle}: {controller.LastAction}");
                before = after;
            }
            Assert.Greater(executed, 0, "El Enemigo tiene que haber hecho algo para que la prueba diga algo.");
        }

        // Nova.Enemy no toca el tablero más que por los comandos de NodeCommandIssuer, los mismos del
        // Jugador (3.1): de Gameplay y Systems solo lee. Es lo que impide, por construcción, sumarse
        // Energía, saltarse un tiempo o lanzar un protón por otra puerta.
        static readonly HashSet<string> AllowedCalls = new HashSet<string>
        {
            // Lecturas
            "Snapshot", "HasState", "GetIncoming", "Offers",
            // Los comandos de INodeCommandIssuer
            "Send", "Ignite", "Upgrade", "Convert", "ChargePower", "CancelCharge", "LaunchPower",
        };

        [Test]
        public void TheEnemyOnlyActsThroughTheCommands()
        {
            Assembly enemy = typeof(EnemyController).Assembly;
            var violations = new List<string>();

            foreach (Type type in enemy.GetTypes())
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                         | BindingFlags.Static | BindingFlags.DeclaredOnly;
                IEnumerable<MethodBase> methods = type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all));
                foreach (MethodBase method in methods)
                {
                    foreach (MethodBase call in Calls(method))
                    {
                        string ns = call.DeclaringType?.Namespace ?? "";
                        if (!ns.StartsWith("Nova.Gameplay") && !ns.StartsWith("Nova.Systems"))
                            continue;
                        if (call.Name.StartsWith("get_") || AllowedCalls.Contains(call.Name))
                            continue;
                        // El constructor base de EnemyCommandIssuer.
                        if (call.IsConstructor && call.DeclaringType == typeof(NodeCommandIssuer))
                            continue;
                        violations.Add($"{type.Name}.{method.Name} → {call.DeclaringType.Name}.{call.Name}");
                    }
                }
            }

            Assert.IsEmpty(violations);
        }

        // --- 4. Tiempos: de carga y de reconversión ---

        readonly List<BoardNode> board = new List<BoardNode>();

        static int Cap(NodeType type) => type == NodeType.Star ? 50 : type == NodeType.Asteroid ? 10 : 100;

        BoardNode Add(Faction faction, NodeType type, int energy, Vector2 position, int level = 1, bool converting = false,
                      PowerType? charging = null, PowerType? ready = null, BoardProton[] incoming = null)
        {
            var state = new NodeSnapshot(faction, type, energy, Cap(type), false, false,
                type == NodeType.Asteroid ? 0 : level, converting, charging.HasValue || ready.HasValue);
            var node = new BoardNode(board.Count, state, position, type == NodeType.Magnetar ? 1.5f : 0f,
                type == NodeType.Asteroid ? -1 : 10, 20, charging, ready, false, incoming);
            board.Add(node);
            return node;
        }

        EnemyBoard Board() => new EnemyBoard(Faction.Enemy, board, Balance.MagnetarResistance, Balance.IgnitionCost,
            Powers, Balance.ProtonSpeed);

        static BoardProton Hostile(int charge, float distance, int source) =>
            new BoardProton(new IncomingProton(Faction.Player, charge, distance), source);

        [TestCase("Map1")]
        [TestCase("Map2")]
        [TestCase("Map3")]
        public void APowerStillChargingCannotBeLaunched(string profile)
        {
            BoardNode pulsar = Add(Faction.Enemy, NodeType.Pulsar, 60, new Vector2(4f, 0f), level: 3, charging: PowerType.Wormhole);
            Add(Faction.Player, NodeType.Star, 50, new Vector2(-4f, 0f), level: 3);
            Add(Faction.Player, NodeType.Pulsar, 80, new Vector2(-4f, 3f), level: 3);

            List<EnemyAction> candidates = EnemyEvaluator.Candidates(Board(), Profile(profile), Scoring);

            Assert.IsFalse(candidates.Any(a => a.Actor == pulsar.Index && a.Kind == EnemyActionKind.LaunchPower),
                "Hasta el 100 % no hay nada que lanzar.");
            Assert.IsFalse(candidates.Any(a => a.Actor == pulsar.Index && a.Kind == EnemyActionKind.ChargePower),
                "Ni empezar otra carga encima de la que lleva.");

            // El mismo Púlsar con el poder ya cargado, sí: lo que lo impedía era el tiempo.
            board.Clear();
            pulsar = Add(Faction.Enemy, NodeType.Pulsar, 0, new Vector2(4f, 0f), level: 3, ready: PowerType.Wormhole);
            Add(Faction.Player, NodeType.Star, 50, new Vector2(-4f, 0f), level: 3);
            Add(Faction.Player, NodeType.Pulsar, 80, new Vector2(-4f, 3f), level: 3);
            int index = pulsar.Index;
            Assert.IsTrue(EnemyEvaluator.Candidates(Board(), Profile(profile), Scoring)
                .Any(a => a.Actor == index && a.Kind == EnemyActionKind.LaunchPower));
        }

        [Test]
        public void TheDefenceDoesNotFreezeWithAnAbsoluteZeroStillCharging()
        {
            BoardNode attacker = Add(Faction.Player, NodeType.Star, 30, new Vector2(-6f, 0f));
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, incoming: new[] { Hostile(15, 1f, attacker.Index) });
            Add(Faction.Enemy, NodeType.Pulsar, 0, new Vector2(4f, 0f), charging: PowerType.AbsoluteZero);

            Assert.IsNull(EnemyThreatResponder.Respond(Board(), Profile("Map1")), "Cargando no congela, y el mapa 1 no evacúa.");

            board.RemoveAt(2);
            Add(Faction.Enemy, NodeType.Pulsar, 0, new Vector2(4f, 0f), ready: PowerType.AbsoluteZero);
            Assert.AreEqual(EnemyActionKind.LaunchPower, EnemyThreatResponder.Respond(Board(), Profile("Map1")).Value.Kind);
        }

        [TestCase("Map1")]
        [TestCase("Map2")]
        [TestCase("Map3")]
        public void ANodeReconvertingDoesNothingUntilItFinishes(string profile)
        {
            BoardNode converting = Add(Faction.Enemy, NodeType.Pulsar, 50, new Vector2(4f, 0f), level: 3, converting: true);
            Add(Faction.Player, NodeType.Star, 5, new Vector2(-1f, 0f));
            Add(Faction.Player, NodeType.Pulsar, 40, new Vector2(-4f, 3f));

            List<EnemyAction> candidates = EnemyEvaluator.Candidates(Board(), Profile(profile), Scoring);
            Assert.IsFalse(candidates.Any(a => a.Actor == converting.Index),
                "12.6: ni ataca, ni sube, ni carga, ni vuelve a reconvertirse a mitad.");

            // El mismo nodo sin reconvertirse tiene de todo: lo que lo impedía era el tiempo.
            board.Clear();
            converting = Add(Faction.Enemy, NodeType.Pulsar, 50, new Vector2(4f, 0f), level: 3);
            Add(Faction.Player, NodeType.Star, 5, new Vector2(-1f, 0f));
            Add(Faction.Player, NodeType.Pulsar, 40, new Vector2(-4f, 3f));
            int index = converting.Index;
            Assert.IsTrue(EnemyEvaluator.Candidates(Board(), Profile(profile), Scoring).Any(a => a.Actor == index));
        }

        [Test]
        public void ANodeReconvertingNeitherReinforcesNorEvacuates()
        {
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, converting: true, incoming: new[] { Hostile(10, 1f, -1) });
            BoardNode donor = Add(Faction.Enemy, NodeType.Star, 40, new Vector2(1f, 0f), converting: true);

            Assert.IsNull(EnemyThreatResponder.Respond(Board(), Profile("Map2")),
                "El amenazado no puede evacuar y el donante no puede enviar hasta terminar.");

            board.Clear();
            Add(Faction.Enemy, NodeType.Star, 10, Vector2.zero, converting: true, incoming: new[] { Hostile(10, 5f, -1) });
            donor = Add(Faction.Enemy, NodeType.Star, 40, new Vector2(1f, 0f));
            EnemyAction action = EnemyThreatResponder.Respond(Board(), Profile("Map2")).Value;
            Assert.AreEqual(EnemyActionKind.Reinforce, action.Kind, "Reforzar al que se reconvierte sí se puede.");
            Assert.AreEqual(donor.Index, action.Actor);
        }

        [Test]
        public void TheControllerWaitsOutChargesAndReconversions()
        {
            NodeController pulsar = Node("Pulsar", Faction.Enemy, 60, new Vector2(5f, 3f), level: 3);
            NodeController converting = Node("Magnetar", Faction.Enemy, 40, new Vector2(5f, -3f));
            Node("Star", Faction.Enemy, 40, new Vector2(5f, 0f));
            Node("Star", Faction.Player, 20, new Vector2(-5f, 0f));
            Node("Pulsar", Faction.Player, 50, new Vector2(-5f, 3f));
            var caster = pulsar.GetComponent<PulsarCaster>();
            var converter = converting.GetComponent<NodeConverter>();

            Assert.IsTrue(issuer.ChargePower(pulsar, PowerType.AbsoluteZero).IsAllowed);
            Assert.IsTrue(issuer.Convert(converting, NodeType.Star).IsAllowed);
            int magnetarEnergy = converting.Energy;

            // Sin que pase el tiempo, ningún número de ciclos acaba una carga ni una reconversión.
            for (int i = 0; i < 30; i++)
            {
                controller.Decide();
                if (controller.LastAction.HasValue)
                {
                    EnemyAction action = controller.LastAction.Value;
                    int pulsarIndex = registry.All.ToList().IndexOf(pulsar);
                    int convertingIndex = registry.All.ToList().IndexOf(converting);
                    Assert.AreNotEqual(EnemyActionKind.LaunchPower, action.Kind, $"Ciclo {i}");
                    Assert.AreNotEqual(convertingIndex, action.Actor, $"Ciclo {i}: {action}");
                    if (action.Actor == pulsarIndex)
                        Assert.AreNotEqual(EnemyActionKind.ChargePower, action.Kind, $"Ciclo {i}");
                }
            }

            Assert.AreEqual(PowerChargeState.Charging, caster.State);
            Assert.AreEqual(0, caster.Invested);
            Assert.AreEqual(0, beams.Active.Count);
            Assert.IsTrue(converter.IsConverting);
            Assert.AreEqual(0f, converter.Elapsed);
            Assert.AreEqual(NodeType.Magnetar, converting.Type);
            Assert.AreEqual(magnetarEnergy, converting.Energy);
        }

        // --- Lectura de IL para TheEnemyOnlyActsThroughTheCommands ---

        static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null))
            .ToDictionary(o => o.Value);

        // Los métodos a los que llama method: call, callvirt, newobj y ldftn.
        static IEnumerable<MethodBase> Calls(MethodBase method)
        {
            byte[] il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null)
                yield break;

            Type[] typeArgs = method.DeclaringType != null && method.DeclaringType.IsGenericType
                ? method.DeclaringType.GetGenericArguments() : null;
            Type[] methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;

            int i = 0;
            while (i < il.Length)
            {
                short value = il[i] == 0xFE ? unchecked((short)(0xFE00 | il[i + 1])) : il[i];
                i += il[i] == 0xFE ? 2 : 1;
                OpCode op = OpCodesByValue[value];
                if (op.OperandType == OperandType.InlineMethod)
                    yield return method.Module.ResolveMethod(BitConverter.ToInt32(il, i), typeArgs, methodArgs);
                i += OperandSize(op.OperandType, il, i);
            }
        }

        static int OperandSize(OperandType type, byte[] il, int at)
        {
            switch (type)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return 4 + 4 * BitConverter.ToInt32(il, at);
                default:
                    return 4;
            }
        }
    }
}
