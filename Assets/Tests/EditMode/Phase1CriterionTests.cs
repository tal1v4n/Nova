using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Presentation;
using Nova.Systems;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T1-12: la escena de la fase 1 montada en código, con los mismos componentes y los mismos
    // tres nodos que genera Nova → Fase 1 → Generar escena de prueba. Recorre el criterio de salida
    // de la fase 1 (19) con clics de ratón simulados: capturar un Asteroide, encenderlo como
    // Estrella y tomar un nodo enemigo. Cada tick imita el orden de 15.10: router (−50), nodos (0),
    // protones (+10) y resolución de impactos tras todos los FixedUpdate.
    public class Phase1CriterionTests
    {
        const float Tick = 0.02f;
        const string FontPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";

        readonly List<GameObject> spawned = new List<GameObject>();
        NodeRegistry registry;
        ProtonRegistry protons;
        SelectionManager selection;
        InputRouter router;
        ProvisionalSelectionView selectionView;
        ProvisionalProtonView protonView;

        NodeController playerStar;
        NodeController asteroid;
        NodeController enemyStar;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            registry = go.AddComponent<NodeRegistry>();
            protons = go.AddComponent<ProtonRegistry>();
            var pool = go.AddComponent<ProtonPool>();
            var launcher = go.AddComponent<ProtonLauncher>();
            launcher.Configure(Balance, pool, protons);
            selection = go.AddComponent<SelectionManager>();
            selection.Configure(registry);
            var issuer = go.AddComponent<PlayerCommandIssuer>();
            issuer.Configure(launcher);
            router = go.AddComponent<InputRouter>();
            router.Configure(Balance, selection, issuer);
            selectionView = go.AddComponent<ProvisionalSelectionView>();
            selectionView.Configure(selection);
            selectionView.Subscribe();
            protonView = go.AddComponent<ProvisionalProtonView>();
            protonView.Configure(protons, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));

            // Los tres nodos de Phase1TestScene.
            playerStar = Place("Player Star", "Star", Faction.Player, 30, new Vector2(-5f, 0f));
            asteroid = Place("Neutral Asteroid", "Asteroid", Faction.Neutral, 10, new Vector2(-3.5f, 1.5f));
            enemyStar = Place("Enemy Star", "Star", Faction.Enemy, 10, new Vector2(5f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            selectionView.Unsubscribe();
            selection.Clear();
            registry.Clear();
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
        }

        NodeController Place(string name, string type, Faction faction, int energy, Vector2 position)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<NodeController>();
            var placed = go.AddComponent<PlacedNode>();
            placed.Configure(Definition(type), faction, energy);
            go.AddComponent<NodeIgnition>().Configure(Balance, new[] { Definition("Star"), Definition("Pulsar"), Definition("Magnetar") });
            go.AddComponent<StarGenerator>();
            return placed.Place(registry);
        }

        // Un tick fijo de la partida, en el orden de 15.10.
        void Step()
        {
            router.Step(Tick);
            foreach (NodeController node in registry.All)
                node.GetComponent<StarGenerator>().Tick(Tick);
            foreach (Proton proton in protons.InFlight.ToList())
                proton.Tick(Tick);
            protons.ResolveArrivals();
        }

        void Run(float seconds)
        {
            int ticks = Mathf.CeilToInt(seconds / Tick);
            for (int i = 0; i < ticks; i++)
                Step();
        }

        // Hasta que no quede ningún protón en vuelo ni envío pendiente en el buffer.
        void Settle()
        {
            for (int i = 0; i < 5000 && (router.IsBufferActive || protons.Count > 0); i++)
                Step();
            Assert.IsFalse(router.IsBufferActive || protons.Count > 0, "Algo sigue en vuelo tras 100 s.");
        }

        void Click(NodeController node)
        {
            Vector2 at = node != null ? (Vector2)node.transform.position : new Vector2(0f, -4f);
            router.PressLeft(at, node);
            router.ReleaseLeft(at, node);
        }

        void DoubleClick(NodeController node)
        {
            Click(node);
            Step();
            Click(node);
        }

        [Test]
        public void TheSceneStartsAsTheCriterionNeeds()
        {
            Assert.AreEqual(3, registry.Count);
            Assert.AreEqual(NodeState.Sleeping, asteroid.State);
            Assert.AreEqual(NodeState.Normal, playerStar.State);
            Assert.AreEqual(1, registry.CountOf(Faction.Enemy));
            Assert.LessOrEqual(Vector2.Distance(playerStar.transform.position, asteroid.transform.position), 2.25f,
                "11.3: un Asteroide a menos de 2.25 u de la posición inicial del Jugador.");
        }

        [Test]
        public void CaptureIgniteAndTakeTheEnemyNode()
        {
            // 1. Capturar el Asteroide: seleccionar la Estrella y hacer clic en él.
            Click(playerStar);
            Assert.IsTrue(selection.IsSelected(playerStar));
            Click(asteroid);
            Settle();

            Assert.AreEqual(Faction.Player, asteroid.Faction, "Capturado.");
            Assert.AreEqual(NodeType.Asteroid, asteroid.Type, "Capturar conserva tipo y nivel (6.2).");
            Assert.AreEqual(NodeState.Unignited, asteroid.State);

            // 2. Llevarlo a 10 con refuerzos: con selección, un Asteroide propio recibe (13.1).
            for (int i = 0; i < 20 && asteroid.Energy < Balance.IgnitionCost; i++)
            {
                if (playerStar.Energy < 2)
                {
                    Run(2f);
                    continue;
                }
                Click(asteroid);
                Settle();
            }
            Assert.AreEqual(Balance.IgnitionCost, asteroid.Energy);

            // 3. Encenderlo como Estrella: cancelar la selección y hacer clic en él (13.1, T1-11b).
            router.ClickRight(null);
            Assert.IsFalse(selection.HasSelection);
            Click(asteroid);

            Assert.AreEqual(NodeType.Star, asteroid.Type, "Encendido.");
            Assert.AreEqual(1, asteroid.Level);
            Assert.AreEqual(Faction.Player, asteroid.Faction);
            NodeController secondStar = asteroid;

            // 4. Tomar la Estrella enemiga: lazo sobre las dos Estrellas y doble clic en ella,
            // repetido cada vez que las dos se han recargado.
            Assert.AreNotEqual(Faction.Player, enemyStar.Faction);
            for (int wave = 0; wave < 20 && enemyStar.Faction != Faction.Player; wave++)
            {
                Run(20f);
                router.PressLeft(new Vector2(-6f, -1f), null);
                router.ReleaseLeft(new Vector2(-3f, 2f), null);
                CollectionAssert.AreEquivalent(new[] { playerStar, secondStar }, selection.Selected);
                DoubleClick(enemyStar);
                Settle();
                router.ClickRight(null);
            }

            Assert.AreEqual(Faction.Player, enemyStar.Faction, "Nodo enemigo tomado: criterio de salida de la fase 1.");
            Assert.AreEqual(0, registry.CountOf(Faction.Enemy));
            Assert.AreEqual(3, registry.CountOf(Faction.Player));
        }

        [Test]
        public void SelectionShowsARingOnlyAroundSelectedNodes()
        {
            NodeController other = Place("Second Star", "Star", Faction.Player, 10, new Vector2(-5f, -2f));

            Click(playerStar);
            Assert.IsTrue(selectionView.HasRing(playerStar));
            Assert.AreEqual(1, selectionView.RingCount);

            selection.Select(other);
            Assert.AreEqual(2, selectionView.RingCount);

            router.ClickRight(null);
            Assert.AreEqual(0, selectionView.RingCount);
        }

        [Test]
        public void ProtonInFlightShowsItsFactionAndCharge()
        {
            Click(playerStar);
            Click(enemyStar);
            Run(Balance.DoubleClickBuffer + Tick);
            Proton proton = protons.InFlight.Single();

            protonView.Refresh();

            Assert.IsTrue(protonView.TryGetVisual(proton, out SpriteRenderer body, out TextMeshPro label));
            Assert.AreEqual((Color)ProvisionalNodeView.ColorOf(Faction.Player), body.color);
            Assert.AreEqual("15", label.text);
            Assert.AreSame(proton.transform, body.transform.parent, "El disco viaja con el protón.");
        }

        [Test]
        public void IgnitionEnlargesTheProvisionalCircle()
        {
            var view = asteroid.gameObject.AddComponent<ProvisionalNodeView>();
            view.Subscribe();
            view.Refresh();
            float asteroidSize = view.Body.sprite.bounds.size.x;

            asteroid.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(Faction.Player, 20), asteroid.Snapshot(), Balance.MagnetarResistance));
            asteroid.GetComponent<NodeIgnition>().TryIgnite(Faction.Player, NodeType.Star);

            Assert.Less(asteroidSize, view.Body.sprite.bounds.size.x, "Un Asteroide se dibuja más pequeño que un nodo encendido.");
            Assert.AreEqual(1f, view.Body.sprite.bounds.size.x, 1e-5f);
            view.Unsubscribe();
        }
    }
}
