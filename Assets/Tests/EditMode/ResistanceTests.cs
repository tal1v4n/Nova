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
    // T2-05: resistencia ×2 del Magnetar en impactos hostiles y 1:1 en refuerzos aliados (5.3,
    // 6.2, 6.4). Las reglas son de ImpactResolver desde T1-03 y sus tests las prueban sobre
    // instantáneas; estos lanzan protones de verdad, por ProtonLauncher y ProtonRegistry, contra un
    // nodo Magnetar de escena, y comprueban que la resistencia sale de BalanceConfig.
    public class ResistanceTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();
        readonly List<Object> created = new List<Object>();
        ProtonRegistry protons;
        ProtonLauncher launcher;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            UseBalance(Balance);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
            foreach (Object o in created)
                Object.DestroyImmediate(o);
            created.Clear();
        }

        void UseBalance(BalanceConfig config)
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            protons = go.AddComponent<ProtonRegistry>();
            launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(config, go.AddComponent<ProtonPool>(), protons);
        }

        NodeController Node(string type, Faction faction, int energy, Vector2 position)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy);
            return node;
        }

        // Envía el 100 % de source contra target y deja que llegue, en el orden de 15.10.
        void SendAll(NodeController source, NodeController target, Faction actor)
        {
            Assert.AreEqual(LaunchBlock.None, launcher.TryLaunch(source, target, actor, true, out _));
            for (int i = 0; i < 1000 && protons.Count > 0; i++)
            {
                foreach (Proton proton in protons.InFlight.ToList())
                    proton.Tick(Tick);
                protons.ResolveArrivals();
            }
            Assert.AreEqual(0, protons.Count, "El protón no llegó.");
        }

        [Test]
        public void HostileProtonDoesHalfDamageToAMagnetar()
        {
            NodeController source = Node("Star", Faction.Player, 30, new Vector2(-3f, 0f));
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 50, Vector2.zero);

            SendAll(source, magnetar, Faction.Player);

            Assert.AreEqual(Faction.Enemy, magnetar.Faction);
            Assert.AreEqual(35, magnetar.Energy, "30 de Carga quitan 15 (6.2).");
        }

        [Test]
        public void TheSameProtonDoesFullDamageToAStar()
        {
            NodeController source = Node("Star", Faction.Player, 30, new Vector2(-3f, 0f));
            NodeController star = Node("Star", Faction.Enemy, 50, Vector2.zero);

            SendAll(source, star, Faction.Player);

            Assert.AreEqual(20, star.Energy, "Sin resistencia, 1:1.");
        }

        [Test]
        public void AlliedReinforcementIsOneToOne()
        {
            NodeController source = Node("Star", Faction.Player, 30, new Vector2(-3f, 0f));
            NodeController magnetar = Node("Magnetar", Faction.Player, 40, Vector2.zero);

            SendAll(source, magnetar, Faction.Player);

            Assert.AreEqual(70, magnetar.Energy, "5.3: el refuerzo aliado no pasa por la resistencia.");
        }

        // 6.4: un Magnetar lleno necesita 200 de Carga. Un Púlsar lleno (100) le quita la mitad;
        // el segundo lo iguala y lo captura, con 0 de remanente (6.2: captura si effective >= energy).
        [Test]
        public void FullMagnetarNeedsTwoHundred()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 100, Vector2.zero);
            NodeController a = Node("Pulsar", Faction.Player, 100, new Vector2(-3f, 0f));
            NodeController b = Node("Pulsar", Faction.Player, 99, new Vector2(0f, 3f));
            NodeController c = Node("Star", Faction.Player, 2, new Vector2(3f, 0f));

            SendAll(a, magnetar, Faction.Player);
            Assert.AreEqual(50, magnetar.Energy);

            SendAll(b, magnetar, Faction.Player);
            Assert.AreEqual(Faction.Enemy, magnetar.Faction, "199 en total no bastan.");
            Assert.AreEqual(1, magnetar.Energy, "99 × 0.5 = 49.5, que se redondea a 49 (6.2).");

            SendAll(c, magnetar, Faction.Player);
            Assert.AreEqual(Faction.Player, magnetar.Faction, "2 × 0.5 = 1 iguala la Energía y lo captura.");
            Assert.AreEqual(0, magnetar.Energy);
        }

        // 6.2: el remanente de una captura también va multiplicado.
        [Test]
        public void CaptureRemainderIsHalvedToo()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 10, Vector2.zero);
            NodeController source = Node("Pulsar", Faction.Player, 40, new Vector2(-3f, 0f));

            SendAll(source, magnetar, Faction.Player);

            Assert.AreEqual(Faction.Player, magnetar.Faction);
            Assert.AreEqual(10, magnetar.Energy, "40 × 0.5 − 10 = 10.");
        }

        // 16.1: la resistencia es un dato. Con otro BalanceConfig, otro resultado.
        [Test]
        public void ResistanceComesFromBalanceConfig()
        {
            BalanceConfig custom = Object.Instantiate(Balance);
            created.Add(custom);
            var serialized = new SerializedObject(custom);
            serialized.FindProperty("magnetarResistance").floatValue = 0.25f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UseBalance(custom);

            NodeController source = Node("Star", Faction.Player, 40, new Vector2(-3f, 0f));
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 50, Vector2.zero);

            SendAll(source, magnetar, Faction.Player);

            Assert.AreEqual(40, magnetar.Energy, "40 × 0.25 = 10 de daño.");
        }
    }
}
