using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T2-02: IgnitionMenuState (geometría y ciclo de vida) e IgnitionMenu (lo que enseña cada
    // opción). El clic sobre el menú se prueba en InputTests, que es quien lo resuelve.
    public class IgnitionMenuTests
    {
        const string FontPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";

        readonly List<GameObject> spawned = new List<GameObject>();
        IgnitionMenuState menu;
        IgnitionMenu view;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Menu");
            spawned.Add(go);
            menu = go.AddComponent<IgnitionMenuState>();
            view = go.AddComponent<IgnitionMenu>();
            view.Configure(menu, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath), AssetDatabase.LoadAssetAtPath<UiText>("Assets/Data/UiText.asset"));
            view.Subscribe();
        }

        [TearDown]
        public void TearDown()
        {
            view.Unsubscribe();
            menu.Close();
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
        }

        NodeController Asteroid(int energy, params string[] offered)
        {
            var go = new GameObject("Asteroid");
            spawned.Add(go);
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition("Asteroid"), Faction.Player, energy);
            var targets = new List<NodeDefinition>();
            foreach (string type in offered)
                targets.Add(Definition(type));
            go.AddComponent<NodeIgnition>().Configure(Balance, targets);
            return node;
        }

        // --- Estado ---

        [Test]
        public void OptionsAreTheThreeTypesInTheOrderOf16_2()
        {
            CollectionAssert.AreEqual(new[] { NodeType.Star, NodeType.Pulsar, NodeType.Magnetar }, IgnitionMenuState.Options);
        }

        [Test]
        public void OptionsSitAroundTheCenterWithoutTouchingEachOtherOrTheAsteroid()
        {
            Vector2 center = new Vector2(2f, -1f);
            for (int i = 0; i < IgnitionMenuState.Options.Count; i++)
            {
                Vector2 option = IgnitionMenuState.OptionPosition(center, i);
                Assert.AreEqual(IgnitionMenuState.OptionDistance, Vector2.Distance(center, option), 1e-5f);
                // El trigger del nodo mide 0.5 u (desviación 138): la opción queda por fuera.
                Assert.Greater(Vector2.Distance(center, option) - IgnitionMenuState.OptionRadius, 0.5f);
                for (int j = i + 1; j < IgnitionMenuState.Options.Count; j++)
                    Assert.Greater(Vector2.Distance(option, IgnitionMenuState.OptionPosition(center, j)), 2f * IgnitionMenuState.OptionRadius);
            }
            Assert.Greater(IgnitionMenuState.OptionPosition(center, 0).y, center.y, "La primera opción, la Estrella, va arriba.");
        }

        [Test]
        public void OptionAtFindsEachOptionAndNothingElse()
        {
            menu.Open(Asteroid(10, "Star"), Vector2.zero, Faction.Player);

            for (int i = 0; i < IgnitionMenuState.Options.Count; i++)
                Assert.AreEqual(i, menu.OptionAt(IgnitionMenuState.OptionPosition(Vector2.zero, i)));
            Assert.AreEqual(-1, menu.OptionAt(Vector2.zero), "El centro es el Asteroide, no una opción.");
            Assert.AreEqual(-1, menu.OptionAt(new Vector2(3f, 3f)));
        }

        [Test]
        public void ClosedMenuHasNoOptions()
        {
            Assert.IsFalse(menu.IsOpen);
            Assert.AreEqual(-1, menu.OptionAt(IgnitionMenuState.OptionPosition(Vector2.zero, 0)));
        }

        [Test]
        public void OpenAndCloseNotifyOnce()
        {
            int changes = 0;
            menu.OnChanged += () => changes++;
            NodeController asteroid = Asteroid(10, "Star");

            menu.Open(asteroid, Vector2.zero, Faction.Player);
            menu.Close();
            menu.Close();

            Assert.AreEqual(2, changes);
        }

        [Test]
        public void IgnitionClosesTheMenuByItself()
        {
            NodeController asteroid = Asteroid(10, "Star");
            menu.Open(asteroid, Vector2.zero, Faction.Player);

            asteroid.GetComponent<NodeIgnition>().TryIgnite(Faction.Player, NodeType.Star);

            Assert.IsFalse(menu.IsOpen);
        }

        [Test]
        public void ReopeningOnAnotherAsteroidStopsWatchingTheFirst()
        {
            NodeController first = Asteroid(10, "Star");
            NodeController second = Asteroid(10, "Star");
            menu.Open(first, Vector2.zero, Faction.Player);
            menu.Open(second, Vector2.one, Faction.Player);

            first.GetComponent<NodeIgnition>().TryIgnite(Faction.Player, NodeType.Star);

            Assert.IsTrue(menu.IsOpen, "El encendido del primero ya no cierra el menú del segundo.");
            Assert.AreSame(second, menu.Node);
        }

        // --- Vista ---

        [Test]
        public void ViewIsHiddenWhileClosed()
        {
            view.Refresh();
            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void ViewFollowsOpenAndClose()
        {
            Vector2 center = new Vector2(-2f, 1f);
            menu.Open(Asteroid(10, "Star"), center, Faction.Player);

            Assert.IsTrue(view.IsVisible);
            for (int i = 0; i < IgnitionMenuState.Options.Count; i++)
                Assert.Less(Vector2.Distance(IgnitionMenuState.OptionPosition(center, i), view.DiscPosition(i)), 1e-5f,
                    "El disco dibujado es el blanco del clic.");

            menu.Close();
            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void EnoughEnergyEnablesOfferedTypesOnly()
        {
            menu.Open(Asteroid(10, "Star"), Vector2.zero, Faction.Player);

            Assert.AreEqual(IgnitionMenu.OptionLook.Enabled, view.LookOf(0));
            Assert.AreEqual(IgnitionMenu.OptionLook.Unavailable, view.LookOf(1), "Púlsar: T3-02.");
            Assert.AreEqual(IgnitionMenu.OptionLook.Unavailable, view.LookOf(2), "Magnetar: T2-04.");
            Assert.AreEqual(Balance.IgnitionCost.ToString(), view.CostLabel(0).text);
            Assert.IsFalse(view.CostLabel(1).enabled, "Un tipo que no existe no enseña precio.");
        }

        [Test]
        public void MissingEnergyShowsTheCostInRed()
        {
            menu.Open(Asteroid(7, "Star", "Pulsar", "Magnetar"), Vector2.zero, Faction.Player);

            for (int i = 0; i < IgnitionMenuState.Options.Count; i++)
            {
                Assert.AreEqual(IgnitionMenu.OptionLook.MissingEnergy, view.LookOf(i));
                Assert.IsTrue(view.CostLabel(i).enabled);
                Assert.AreEqual(Balance.IgnitionCost.ToString(), view.CostLabel(i).text);
                Color cost = view.CostLabel(i).color;
                Assert.Greater(cost.r, cost.g, "Coste en rojo (13.7).");
            }
        }

        [Test]
        public void ReachingTheCostWithTheMenuOpenEnablesTheOption()
        {
            NodeController asteroid = Asteroid(9, "Star");
            menu.Open(asteroid, Vector2.zero, Faction.Player);
            Assert.AreEqual(IgnitionMenu.OptionLook.MissingEnergy, view.LookOf(0));

            asteroid.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(Faction.Player, 1), asteroid.Snapshot(), Balance.MagnetarResistance));

            Assert.AreEqual(10, asteroid.Energy);
            Assert.AreEqual(IgnitionMenu.OptionLook.Enabled, view.LookOf(0));
        }
    }
}
