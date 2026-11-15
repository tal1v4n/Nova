using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T6-01: el arte de A.2 en los NodeDefinition, NodeVisual (15.8), el collider con la huella del
    // sprite (NodeCollider2D, 18 #23) y lo que queda de la vista provisional junto al sprite.
    public class NodeVisualTests
    {
        const string FontAssetPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";
        const int PixelsPerUnit = 64;

        // Tipo, asset, carpeta de A.1, prefijo de archivo y si existe en Neutral (3.2).
        static readonly (NodeType type, string asset, string folder, string prefix, bool neutral)[] Types =
        {
            (NodeType.Asteroid, "Asteroid", "asteroide", "ast", true),
            (NodeType.Star, "Star", "estrella", "str", false),
            (NodeType.Pulsar, "Pulsar", "pulsar", "pul", false),
            (NodeType.Magnetar, "Magnetar", "magnetar", "mag", true),
        };

        static readonly (Faction faction, string name)[] Factions =
        {
            (Faction.Neutral, "neutral"), (Faction.Player, "player"), (Faction.Enemy, "enemy"),
        };

        readonly List<GameObject> spawned = new List<GameObject>();

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static IEnumerable<int> LevelsOf(NodeDefinition definition)
        {
            if (definition.MaxLevel == 0)
                yield return 0;
            for (int level = 1; level <= definition.MaxLevel; level++)
                yield return level;
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
        }

        NodeController Node(string type, Faction faction, int energy, int level = -1)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, level >= 0 ? level : (type == "Asteroid" ? 0 : 1));
            return node;
        }

        static NodeVisual Visual(NodeController node)
        {
            var visual = node.gameObject.AddComponent<NodeVisual>();
            visual.Subscribe();
            visual.Refresh();
            return visual;
        }

        static string NameOf(NodeController node) =>
            System.Array.Find(Types, t => t.type == node.Type).prefix + $"_lvl{node.Level}_" +
            System.Array.Find(Factions, f => f.faction == node.Faction).name;

        // --- El arte en los NodeDefinition ---

        [Test]
        public void EveryTypeHasItsSpritesForEveryLevelAndFaction()
        {
            foreach (var entry in Types)
            {
                NodeDefinition definition = Definition(entry.asset);
                Assert.IsTrue(definition.HasArt, $"{entry.asset} sin arte: Nova → Arte → Asignar sprites de nodo.");
                foreach (int level in LevelsOf(definition))
                {
                    NodeLevelArt art = definition.GetArt(level);
                    foreach (var faction in Factions)
                    {
                        Sprite sprite = art.SpriteFor(faction.faction);
                        if (faction.faction == Faction.Neutral && !entry.neutral)
                        {
                            Assert.IsNull(sprite, $"{entry.asset} no existe en Neutral (3.2).");
                            continue;
                        }
                        Assert.IsNotNull(sprite, $"{entry.asset} N{level} {faction.name}");
                        Assert.AreEqual($"{entry.prefix}_lvl{level}_{faction.name}", sprite.name, "A.1");
                    }
                }
            }
        }

        // 34 sprites base (A.2): 3 + 10 + 6 + 15.
        [Test]
        public void ThereAreThirtyFourBaseSprites()
        {
            var sprites = new HashSet<Sprite>();
            foreach (var entry in Types)
            {
                NodeDefinition definition = Definition(entry.asset);
                foreach (int level in LevelsOf(definition))
                {
                    foreach (var faction in Factions)
                    {
                        Sprite sprite = definition.GetArt(level).SpriteFor(faction.faction);
                        if (sprite != null)
                            sprites.Add(sprite);
                    }
                }
            }
            Assert.AreEqual(34, sprites.Count);
        }

        // NOVA_Arte_Tecnica.md, *Importación en Unity*: celda de 96, PPU 64, pivote en el centro.
        [Test]
        public void EverySpriteIsACellOfNinetySixWithItsPivotInTheCentre()
        {
            foreach (var entry in Types)
            {
                NodeDefinition definition = Definition(entry.asset);
                foreach (int level in LevelsOf(definition))
                {
                    Sprite sprite = definition.GetArt(level).SpriteFor(Faction.Player);
                    Assert.AreEqual(PixelsPerUnit, sprite.pixelsPerUnit, sprite.name);
                    Assert.AreEqual(new Rect(0, 0, 96, 96), sprite.rect, sprite.name);
                    Assert.AreEqual(new Vector2(48f, 48f), sprite.pivot, sprite.name);
                    Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, sprite.name);
                }
            }
        }

        // La huella llega del generador, y el radio del collider es su mitad: de 0.3 a 0.6 u (18 #23).
        [Test]
        public void TheFootprintIsWhatTheGeneratorMeasuredAndFitsTheContract()
        {
            foreach (var entry in Types)
            {
                var json = AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/Sprites/nodes/{entry.folder}/{entry.prefix}_huella.json");
                Assert.IsNotNull(json, $"Falta {entry.prefix}_huella.json: ejecuta el generador y sincroniza.");
                var export = JsonUtility.FromJson<FootprintExport>(json.text);
                Assert.AreEqual(PixelsPerUnit, export.ppu);

                NodeDefinition definition = Definition(entry.asset);
                CollectionAssert.AreEqual(LevelsOf(definition), export.levels, entry.asset);
                for (int i = 0; i < export.levels.Length; i++)
                {
                    NodeLevelArt art = definition.GetArt(export.levels[i]);
                    Assert.AreEqual(export.footprintPixels[i], art.FootprintPixels, $"{entry.asset} N{export.levels[i]}");
                    Assert.That(art.FootprintRadius, Is.InRange(0.3f, 0.6f), $"{entry.asset} N{export.levels[i]}");
                }
            }
        }

        [System.Serializable]
        sealed class FootprintExport
        {
            public int ppu;
            public int[] levels;
            public int[] footprintPixels;
        }

        [Test]
        public void ArtForALevelTheTypeDoesNotHaveIsRefused()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Definition("Pulsar").GetArt(4));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Definition("Asteroid").GetArt(1));
            Assert.IsFalse(Definition("Asteroid").TryGetArt(1, out _), "Lo que ve un Asteroide que se enciende.");
        }

        // --- NodeVisual ---

        [TestCase("Asteroid", Faction.Neutral, 0)]
        [TestCase("Asteroid", Faction.Player, 0)]
        [TestCase("Star", Faction.Enemy, 3)]
        [TestCase("Pulsar", Faction.Player, 2)]
        [TestCase("Magnetar", Faction.Neutral, 5)]
        public void ItShowsTheSpriteOfItsTypeLevelAndFaction(string type, Faction faction, int level)
        {
            NodeController node = Node(type, faction, 10, level);
            NodeVisual visual = Visual(node);

            Assert.IsTrue(visual.IsShowing);
            Assert.IsTrue(visual.Body.enabled);
            Assert.AreEqual(NameOf(node), visual.Body.sprite.name);
            Assert.AreEqual(NodeVisual.SortingOrder, visual.Body.sortingOrder, "Capa 1 de 14.7.");
            Assert.AreEqual(Vector3.zero, visual.Body.transform.localPosition, "En el centro del nodo.");
        }

        [Test]
        public void AnUninitializedNodeShowsNothing()
        {
            var go = new GameObject("Node");
            spawned.Add(go);
            go.AddComponent<NodeController>();
            var visual = go.AddComponent<NodeVisual>();
            visual.Refresh();

            Assert.IsFalse(visual.IsShowing);
            Assert.IsFalse(visual.Body.enabled);
        }

        // 14.1, Capturado: el sprite cambia en el frame 0, con el mismo nivel.
        [Test]
        public void ACaptureRecoloursItAtOnce()
        {
            NodeController node = Node("Magnetar", Faction.Neutral, 10, 4);
            NodeVisual visual = Visual(node);

            node.Steal(Faction.Enemy);

            Assert.AreEqual("mag_lvl4_enemy", visual.Body.sprite.name);
        }

        [Test]
        public void AnUpgradeChangesTheSprite()
        {
            NodeController node = Node("Star", Faction.Player, 50);
            NodeVisual visual = Visual(node);

            Assert.IsTrue(node.TryUpgrade(Faction.Player).IsAllowed);

            Assert.AreEqual("str_lvl2_player", visual.Body.sprite.name);
        }

        // Encender y reconvertir pasan por ChangeType, que avisa del nivel antes que del tipo: un
        // Asteroide se ve un instante como «Asteroide de nivel 1», que no tiene arte, y no pasa nada.
        [Test]
        public void IgnitingShowsTheNewTypeAtLevelOne()
        {
            NodeController node = Node("Asteroid", Faction.Player, 10);
            NodeVisual visual = Visual(node);

            node.ChangeType(Definition("Star"), 1);

            Assert.AreEqual("str_lvl1_player", visual.Body.sprite.name);
        }

        [Test]
        public void AConversionShowsTheNewTypeAtLevelOne()
        {
            NodeController node = Node("Star", Faction.Enemy, 40, 5);
            NodeVisual visual = Visual(node);

            node.ChangeType(Definition("Pulsar"), 1);

            Assert.AreEqual("pul_lvl1_enemy", visual.Body.sprite.name);
        }

        // Una facción sin sprite (una Estrella neutral, que no existe en partida) no dibuja nada:
        // la vista provisional vuelve a su círculo.
        [Test]
        public void ATypeWithoutThatFactionShowsNothing()
        {
            NodeVisual visual = Visual(Node("Star", Faction.Neutral, 10));
            Assert.IsFalse(visual.IsShowing);
            Assert.IsFalse(visual.Body.enabled);
        }

        // 14.0 #1: la presentación solo lee.
        [Test]
        public void ItNeverWritesToTheNode()
        {
            NodeController node = Node("Pulsar", Faction.Player, 33, 3);
            NodeSnapshot before = node.Snapshot();

            Visual(node).Refresh();

            Assert.AreEqual(before, node.Snapshot());
        }

        // --- NodeCollider2D ---

        static NodeCollider2D Collider(NodeController node)
        {
            node.gameObject.AddComponent<CircleCollider2D>().radius = 0.5f;
            var collider = node.gameObject.AddComponent<NodeCollider2D>();
            collider.Refresh();
            return collider;
        }

        [Test]
        public void TheClickRadiusIsHalfTheFootprintAndFollowsLevelAndType()
        {
            NodeController node = Node("Star", Faction.Player, 50);
            NodeCollider2D collider = Collider(node);
            Assert.AreEqual(Definition("Star").GetArt(1).FootprintPixels / 2f / PixelsPerUnit, collider.Radius, 1e-6f);

            node.TryUpgrade(Faction.Player);
            Assert.AreEqual(Definition("Star").GetArt(2).FootprintRadius, collider.Radius, 1e-6f, "Al mejorar.");

            node.ChangeType(Definition("Magnetar"), 1);
            Assert.AreEqual(Definition("Magnetar").GetArt(1).FootprintRadius, collider.Radius, 1e-6f, "Al reconvertir.");

            node.Steal(Faction.Enemy);
            Assert.AreEqual(Definition("Magnetar").GetArt(1).FootprintRadius, collider.Radius, 1e-6f, "Capturar no lo cambia.");
        }

        [Test]
        public void AnAsteroidIsClickedByItsOwnFootprint()
        {
            NodeCollider2D collider = Collider(Node("Asteroid", Faction.Neutral, 10));
            Assert.AreEqual(Definition("Asteroid").GetArt(0).FootprintRadius, collider.Radius, 1e-6f);
        }

        // --- La vista provisional junto al sprite ---

        ProvisionalNodeView Provisional(NodeController node)
        {
            var view = node.gameObject.AddComponent<ProvisionalNodeView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            view.Subscribe();
            view.Refresh();
            return view;
        }

        // Decidido con el usuario el 2026-10-26: con sprite, el círculo se aparta y el número va bajo
        // la huella, en claro sobre el cielo, sin tapar el cuerpo (18 #48).
        [Test]
        public void WithASpriteTheCircleStepsAsideAndTheEnergyGoesUnderneath()
        {
            NodeController node = Node("Star", Faction.Player, 25, 3);
            Visual(node);
            ProvisionalNodeView view = Provisional(node);

            int footprint = Definition("Star").GetArt(3).FootprintPixels;
            Assert.IsFalse(view.Body.enabled, "El círculo no se dibuja.");
            Assert.IsTrue(view.Label.enabled, "14.7: el número nunca se oculta.");
            Assert.AreEqual("25", view.Label.text);
            Assert.AreEqual(-NodeLayout.LabelCenterPixels(footprint, view.TextScale) / (float)PixelsPerUnit,
                view.Label.transform.localPosition.y, 1e-6f);
            Assert.Greater(NodeLayout.LabelCenterPixels(footprint, view.TextScale) - NodeLayout.NativeCapPixels * view.TextScale / 2,
                footprint / 2, "La mayúscula empieza por debajo de la huella.");
            Assert.AreEqual((Color)new Color32(0xE6, 0xEC, 0xF5, 0xFF), view.Label.color);
        }

        [Test]
        public void TheNumberMovesWithTheFootprintWhenTheLevelChanges()
        {
            NodeController node = Node("Star", Faction.Player, 50);
            Visual(node);
            ProvisionalNodeView view = Provisional(node);
            float before = view.Label.transform.localPosition.y;

            node.TryUpgrade(Faction.Player);

            Assert.Less(view.Label.transform.localPosition.y, before, "La N2 es más grande: el número baja.");
        }

        // Hasta T6-02, los tintes provisionales de 14.7 van sobre el sprite, que parte de blanco.
        [Test]
        public void TheProvisionalTintsGoOnTheSprite()
        {
            NodeController node = Node("Magnetar", Faction.Enemy, 30, 2);
            NodeVisual visual = Visual(node);
            ProvisionalNodeView view = Provisional(node);
            Color32 white = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
            Assert.AreEqual((Color)white, visual.Body.color);

            node.SetStateFlag(NodeStateFlags.Frozen, true);
            view.Refresh();
            Assert.AreEqual((Color)ProvisionalNodeView.Iced(white), visual.Body.color);

            node.SetStateFlag(NodeStateFlags.Frozen, false);
            node.SetStateFlag(NodeStateFlags.Converting, true);
            view.Refresh();
            Assert.AreEqual((Color)ProvisionalNodeView.Dimmed(white), visual.Body.color);
        }

        // La barra de carga del Púlsar va bajo el número, que va bajo la huella.
        [Test]
        public void ThePulsarChargeBarHangsUnderTheNumber()
        {
            NodeController node = Node("Pulsar", Faction.Player, 70, 3);
            var caster = node.gameObject.AddComponent<PulsarCaster>();
            caster.Configure(AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset"),
                new[] { AssetDatabase.LoadAssetAtPath<PowerDefinition>("Assets/Data/Powers/AbsoluteZero.asset") });
            Assert.IsTrue(caster.TryCharge(Faction.Player, PowerType.AbsoluteZero).IsAllowed);
            Visual(node);
            ProvisionalNodeView view = Provisional(node);
            var bar = node.gameObject.AddComponent<ProvisionalPulsarView>();
            bar.Refresh();

            int footprint = Definition("Pulsar").GetArt(3).FootprintPixels;
            float top = -NodeLayout.ChargeBarTopPixels(footprint, view.TextScale) / (float)PixelsPerUnit;
            float labelBottom = view.Label.transform.localPosition.y - NodeLayout.NativeCapPixels * view.TextScale / 2f / PixelsPerUnit;
            Assert.IsTrue(bar.Back.enabled);
            Assert.AreEqual(top - 2f / PixelsPerUnit, bar.Back.transform.localPosition.y, 1e-6f, "Centro de una barra de 4 px.");
            Assert.Less(top, labelBottom, "La barra empieza por debajo del número.");
        }
    }
}
