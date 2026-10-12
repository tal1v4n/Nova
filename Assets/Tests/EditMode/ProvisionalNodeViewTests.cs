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
    // T1-06b: vista provisional del nodo (regla 4 de T.2).
    public class ProvisionalNodeViewTests
    {
        const string FontAssetPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";

        GameObject go;
        NodeController node;
        ProvisionalNodeView view;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Node");
            node = go.AddComponent<NodeController>();
            view = go.AddComponent<ProvisionalNodeView>();

            var serialized = new SerializedObject(view);
            serialized.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            view.Unsubscribe();
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ColorsFollowThePalette()
        {
            Assert.AreEqual(new Color32(0x3F, 0xD7, 0xF5, 0xFF), ProvisionalNodeView.ColorOf(Faction.Player));
            Assert.AreEqual(new Color32(0xF2, 0x45, 0x6B, 0xFF), ProvisionalNodeView.ColorOf(Faction.Enemy));
            Assert.AreEqual(new Color32(0x8A, 0x93, 0xA6, 0xFF), ProvisionalNodeView.ColorOf(Faction.Neutral));
        }

        [TestCase(Faction.Player)]
        [TestCase(Faction.Enemy)]
        [TestCase(Faction.Neutral)]
        public void RefreshPaintsFactionAndEnergy(Faction faction)
        {
            node.Initialize(Definition("Star"), faction, 17);
            view.Refresh();

            Assert.AreEqual((Color)ProvisionalNodeView.ColorOf(faction), view.Body.color);
            Assert.AreEqual("17", view.Label.text);
        }

        // Sin Initialize no hay nodo que mostrar.
        [Test]
        public void UninitializedNodeIsHidden()
        {
            view.Refresh();
            Assert.IsFalse(view.Body.enabled);
            Assert.IsFalse(view.Label.enabled);
        }

        [Test]
        public void EnergyChangeUpdatesTheLabel()
        {
            node.Initialize(Definition("Star"), Faction.Player, 10);
            view.Subscribe();
            view.Refresh();

            go.GetComponent<NodeEnergy>().Add(5);
            Assert.AreEqual("15", view.Label.text);

            go.GetComponent<NodeEnergy>().Subtract(15);
            Assert.AreEqual("0", view.Label.text);
        }

        [Test]
        public void FactionChangeRepaintsTheCircle()
        {
            node.Initialize(Definition("Asteroid"), Faction.Neutral, 0);
            view.Subscribe();
            view.Refresh();

            go.GetComponent<NodeOwnership>().ChangeFaction(Faction.Enemy);
            Assert.AreEqual((Color)ProvisionalNodeView.ColorOf(Faction.Enemy), view.Body.color);
        }

        // Solo lee eventos: desuscrita, deja de reaccionar.
        [Test]
        public void UnsubscribedViewStopsReacting()
        {
            node.Initialize(Definition("Star"), Faction.Player, 10);
            view.Subscribe();
            view.Refresh();
            view.Unsubscribe();

            go.GetComponent<NodeEnergy>().Add(5);
            Assert.AreEqual("10", view.Label.text);
        }

        // Suscribirse dos veces no duplica la escucha ni rompe la baja.
        [Test]
        public void SubscribeIsIdempotent()
        {
            node.Initialize(Definition("Star"), Faction.Player, 10);
            view.Subscribe();
            view.Subscribe();
            view.Unsubscribe();

            view.Label.text = "sin tocar";
            go.GetComponent<NodeEnergy>().Add(1);
            Assert.AreEqual("sin tocar", view.Label.text);
        }

        // 64 px a PPU 64 = 1 u, dentro de la huella de 0.6 a 1.2 u (A.8 #4).
        [Test]
        public void CircleIsOneUnitPixelArt()
        {
            Sprite sprite = view.Body.sprite;
            Assert.AreEqual(64f, sprite.pixelsPerUnit);
            Assert.AreEqual(1f, sprite.bounds.size.x, 1e-5f);
            Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
        }

        // Alfa binario (NOVA_Arte_Tecnica.md, Reglas de trabajo): opaco en el centro, vacío en la esquina.
        [Test]
        public void CircleAlphaIsBinary()
        {
            Texture2D texture = ProvisionalNodeView.CircleSprite(64).texture;
            foreach (Color32 pixel in texture.GetPixels32())
                Assert.That(pixel.a == 0 || pixel.a == 255);

            Assert.AreEqual(255, texture.GetPixel(32, 32).a * 255, 0.5f);
            Assert.AreEqual(0, texture.GetPixel(0, 0).a);
        }

        // Múltiplo entero del em nativo de Jersey 10: 2 × 18.67 px = 0.5833 u.
        [Test]
        public void LabelUsesAnIntegerMultipleOfTheNativeSize()
        {
            Assert.AreEqual(2 * 1400f / 75f / 64f / 0.1f, view.Label.fontSize, 1e-4f);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath), view.Label.font);
        }

        // T3-04: congelado, a medio camino del hielo #B8F0FF; gana a Reconvirtiendo (14.7) y vuelve al descongelar.
        [Test]
        public void FrozenTintsTheCircleTowardsIce()
        {
            node.Initialize(Definition("Star"), Faction.Enemy, 10);
            view.Subscribe();
            view.Refresh();

            node.SetStateFlag(NodeStateFlags.Converting, true);
            node.SetStateFlag(NodeStateFlags.Frozen, true);
            Color32 iced = ProvisionalNodeView.Iced(ProvisionalNodeView.ColorOf(Faction.Enemy));
            Assert.AreEqual((Color)iced, view.Body.color);
            Assert.AreEqual(213, iced.r, 1, "Mitad entre 0xF2 y 0xB8.");

            node.SetStateFlag(NodeStateFlags.Frozen, false);
            Assert.AreEqual((Color)ProvisionalNodeView.Dimmed(ProvisionalNodeView.ColorOf(Faction.Enemy)), view.Body.color);
        }

        // T3-05: infectado, un halo verde #7CF23F detrás del círculo, 4 px más ancho por lado.
        [Test]
        public void InfectedShowsAGreenAuraBehindTheCircle()
        {
            node.Initialize(Definition("Star"), Faction.Enemy, 10);
            view.Subscribe();
            view.Refresh();
            Assert.IsFalse(view.Aura.enabled);

            node.SetStateFlag(NodeStateFlags.Infected, true);
            Assert.IsTrue(view.Aura.enabled);
            Assert.AreEqual((Color)new Color32(0x7C, 0xF2, 0x3F, 0xFF), view.Aura.color);
            Assert.Less(view.Aura.sortingOrder, view.Body.sortingOrder);
            Assert.AreEqual(view.Body.sprite.rect.width + 8f, view.Aura.sprite.rect.width);

            node.SetStateFlag(NodeStateFlags.Infected, false);
            Assert.IsFalse(view.Aura.enabled);
        }
    }
}
