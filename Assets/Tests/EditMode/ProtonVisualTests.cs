using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Presentation;
using Nova.Systems;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T6-04: el protón con su arte (ProtonVisual, ART-WLD-PRO-*, A.4.2 y 14.3). El arte es de prueba, con
    // la forma de las hojas de verdad: diez tallas, una cada 5 de Carga, en celdas de 48 con el pivote en el
    // centro, 16 frames de cuerpo y 4 pasos de gota por bando y talla, y diez cifras de 7 × 9 con el pivote
    // abajo a la izquierda. El avance y los radios de bolsa son los de los .json del generador. Lo que se
    // comprueba es qué sprite sale y en qué píxel.
    public class ProtonVisualTests
    {
        const float PixelsPerUnit = 64f;
        const float Frame = 1f / 12f;
        const float Epsilon = 1e-4f;
        const int Sizes = 10;
        const int ChargePerSize = 5;
        static readonly int[] Advance = { 6, 4, 6, 6, 6, 6, 6, 6, 6, 6 };
        static readonly int[] BagRadius = { 12, 13, 14, 15, 16, 18, 19, 20, 21, 22 };

        readonly List<Object> spawned = new List<Object>();
        ProtonRegistry protons;
        ProtonArt art;
        ProtonVisual view;
        NodeController far;
        float speedPixels;

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            protons = go.AddComponent<ProtonRegistry>();
            art = TestArt();
            view = go.AddComponent<ProtonVisual>();
            view.Configure(art, protons);
            speedPixels = Balance.ProtonSpeed * PixelsPerUnit;

            // Destino lejano de los protones de prueba: nunca llegan durante un test.
            var target = new GameObject("Far");
            spawned.Add(target);
            target.transform.position = new Vector2(50f, 0f);
            far = target.AddComponent<NodeController>();
            far.Initialize(AssetDatabase.LoadAssetAtPath<NodeDefinition>("Assets/Data/Nodes/Star.asset"), Faction.Neutral, 10, 1);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in spawned)
            {
                if (o != null)
                    Object.DestroyImmediate(o);
            }
            spawned.Clear();
        }

        // --- Arte de prueba ---

        ProtonArt TestArt(bool complete = true)
        {
            var proton = ScriptableObject.CreateInstance<ProtonArt>();
            spawned.Add(proton);
            if (!complete)
                return proton;

            ProtonArt.SizeFrames[] SizeSet(string faction) => Enumerable.Range(0, Sizes).Select(size =>
            {
                var texture = Texture(768, 96);
                Sprite[] body = Enumerable.Range(0, 16)
                    .Select(i => Piece(texture, $"{faction}_{size}_body_{i}", new Rect(i * 48, 48, 48, 48), new Vector2(0.5f, 0.5f)))
                    .ToArray();
                Sprite[] drop = Enumerable.Range(0, 4)
                    .Select(i => Piece(texture, $"{faction}_{size}_drop_{i}", new Rect(i * 48, 0, 48, 48), new Vector2(0.5f, 0.5f)))
                    .ToArray();
                return new ProtonArt.SizeFrames(body, drop);
            }).ToArray();

            var digitTexture = Texture(70, 9);
            Sprite[] digits = Enumerable.Range(0, 10)
                .Select(i => Piece(digitTexture, $"digit_{i}", new Rect(i * 7, 0, 7, 9), Vector2.zero))
                .ToArray();
            proton.Configure(SizeSet("player"), SizeSet("enemy"), digits, (int[])Advance.Clone(), ChargePerSize,
                (int[])BagRadius.Clone());
            return proton;
        }

        Texture2D Texture(int width, int height)
        {
            var texture = new Texture2D(width, height) { filterMode = FilterMode.Point };
            spawned.Add(texture);
            return texture;
        }

        Sprite Piece(Texture2D texture, string name, Rect rect, Vector2 pivot)
        {
            Sprite sprite = Sprite.Create(texture, rect, pivot, 64, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            spawned.Add(sprite);
            return sprite;
        }

        // --- Vuelo ---

        Proton Fly(Faction faction, int charge, Vector2 at = default)
        {
            var go = new GameObject("Proton");
            spawned.Add(go);
            var proton = go.AddComponent<Proton>();
            proton.Launch(at, far, faction, charge, Balance);
            protons.Register(proton);
            return proton;
        }

        // Avanza el protón tantos píxeles de su camino y luego la vista.
        void Move(Proton proton, float pixels)
        {
            proton.Step(pixels / speedPixels);
        }

        SpriteRenderer Body(Proton proton)
        {
            Assert.IsTrue(view.TryGetVisual(proton, out SpriteRenderer body, out _, out _), "El protón no tiene vista.");
            return body;
        }

        SpriteRenderer[] Digits(Proton proton)
        {
            Assert.IsTrue(view.TryGetVisual(proton, out _, out SpriteRenderer[] digits, out _));
            return digits;
        }

        int BodyFrame(Proton proton, int size) =>
            System.Array.IndexOf(art.FramesFor(proton.Faction, size).Body, Body(proton).sprite);

        static Vector2Int Pixels(Vector3 position) =>
            new Vector2Int(Mathf.RoundToInt(position.x * PixelsPerUnit), Mathf.RoundToInt(position.y * PixelsPerUnit));

        // --- ProtonArt ---

        [Test]
        public void TheArtIsCompleteOnlyWithEveryFrameAndEveryDigit()
        {
            Assert.IsTrue(art.IsComplete);
            Assert.IsFalse(TestArt(complete: false).IsComplete);
        }

        [Test]
        public void TheSizeGrowsEveryFiveChargeUpToFifty()
        {
            Assert.AreEqual(Sizes, art.Sizes);
            Assert.AreEqual(0, art.SizeFor(1));
            Assert.AreEqual(0, art.SizeFor(5));
            Assert.AreEqual(1, art.SizeFor(6));
            Assert.AreEqual(1, art.SizeFor(10));
            Assert.AreEqual(2, art.SizeFor(11));
            Assert.AreEqual(8, art.SizeFor(45));
            Assert.AreEqual(9, art.SizeFor(46));
            Assert.AreEqual(9, art.SizeFor(50));
            Assert.AreEqual(9, art.SizeFor(51), "De 50 para arriba, la talla mayor.");
            Assert.AreEqual(9, art.SizeFor(100));
        }

        // --- Cuerpo ---

        [Test]
        public void EachFactionAndSizeShowsItsOwnBody()
        {
            Proton small = Fly(Faction.Player, 7);
            Proton medium = Fly(Faction.Enemy, 42);
            Proton large = Fly(Faction.Player, 100);
            view.Advance(0f);

            Assert.Contains(Body(small).sprite, art.FramesFor(Faction.Player, 1).Body);
            Assert.Contains(Body(medium).sprite, art.FramesFor(Faction.Enemy, 8).Body);
            Assert.Contains(Body(large).sprite, art.FramesFor(Faction.Player, 9).Body);
        }

        [Test]
        public void TheBodyLoopsSixteenFramesAtTwelveFps()
        {
            Proton proton = Fly(Faction.Player, 7);
            view.Advance(0f);
            int first = BodyFrame(proton, 1);

            view.Advance(Frame + Epsilon);
            Assert.AreEqual((first + 1) % 16, BodyFrame(proton, 1));

            for (int i = 0; i < 15; i++)
                view.Advance(Frame);
            Assert.AreEqual(first, BodyFrame(proton, 1), "El bucle cierra a los 16 frames.");
        }

        [Test]
        public void ProtonsLaunchedTogetherDoNotSpinInUnison()
        {
            Proton a = Fly(Faction.Player, 7);
            Proton b = Fly(Faction.Player, 7);
            view.Advance(0f);

            Assert.AreNotEqual(BodyFrame(a, 1), BodyFrame(b, 1));
        }

        [Test]
        public void TheProtonSitsOnWholePixelsAboveTheNodes()
        {
            Proton proton = Fly(Faction.Player, 42, new Vector2(0.3f, 0.1f));
            view.Advance(0f);
            Assert.IsTrue(view.TryGetVisual(proton, out SpriteRenderer body, out SpriteRenderer[] digits, out Transform root));

            Vector3 snapped = root.position * PixelsPerUnit;
            Assert.AreEqual(Mathf.Round(snapped.x), snapped.x, 1e-3f);
            Assert.AreEqual(Mathf.Round(snapped.y), snapped.y, 1e-3f);
            Assert.AreEqual(new Vector2Int(19, 6), Pixels(root.position));
            Assert.AreEqual(BoardOrder.Proton, body.sortingOrder);
            Assert.Greater(body.sortingOrder, BoardOrder.Energy, "El protón pasa por encima de los números de los nodos.");
            Assert.Greater(digits[0].sortingOrder, body.sortingOrder, "La Carga va encima del protón.");
        }

        // --- La Carga ---

        [Test]
        public void TheChargeIsComposedWithTheDigitsCenteredLikeTheGenerator()
        {
            Proton one = Fly(Faction.Player, 7);
            Proton two = Fly(Faction.Player, 42);
            Proton three = Fly(Faction.Enemy, 100);
            view.Advance(0f);

            // Caja de 1 + Σ avance px, desde round(24 − ancho/2) y la fila de arriba en round(19.5) = 20:
            // la de abajo cae 5 px por debajo del centro.
            AssertDigits(one, new[] { 7 }, new[] { -4 });
            AssertDigits(two, new[] { 4, 2 }, new[] { -6, 0 });
            AssertDigits(three, new[] { 1, 0, 0 }, new[] { -8, -4, 2 });
        }

        void AssertDigits(Proton proton, int[] values, int[] x)
        {
            SpriteRenderer[] digits = Digits(proton);
            for (int i = 0; i < digits.Length; i++)
            {
                if (i >= values.Length)
                {
                    Assert.IsFalse(digits[i].enabled, $"Carga {proton.Charge}: sobra la cifra {i}.");
                    continue;
                }
                Assert.IsTrue(digits[i].enabled);
                Assert.AreSame(art.Digit(values[i]), digits[i].sprite, $"Carga {proton.Charge}, cifra {i}.");
                Assert.AreEqual(new Vector2Int(x[i], -5), Pixels(digits[i].transform.localPosition),
                    $"Carga {proton.Charge}, cifra {i}.");
            }
        }

        [Test]
        public void AShotRewritesTheChargeAndShrinksTheProtonOneStep()
        {
            Proton proton = Fly(Faction.Player, 11);
            view.Advance(0f);
            Assert.Contains(Body(proton).sprite, art.FramesFor(Faction.Player, 2).Body);

            proton.TakeShot(2);
            view.Advance(0f);

            Assert.Contains(Body(proton).sprite, art.FramesFor(Faction.Player, 1).Body,
                "De 11 a 9 cruza el escalón de 10: baja una talla.");
            AssertDigits(proton, new[] { 9 }, new[] { -4 });
        }

        // --- Estela ---

        [Test]
        public void DropsAreReleasedEveryFewPixelsAlongThePath()
        {
            Proton proton = Fly(Faction.Player, 7);
            view.Advance(0f);
            Move(proton, 70f + Epsilon);
            view.Advance(0f);

            Assert.AreEqual(10, view.DropCount, "Una gota cada 7 px.");
            Vector2Int[] positions = view.Drops.Select(d => Pixels(d.transform.position)).OrderBy(p => p.x).ToArray();
            for (int i = 0; i < positions.Length; i++)
                Assert.AreEqual(new Vector2Int(7 * (i + 1), 0), positions[i]);
            Assert.IsTrue(view.Drops.All(d => d.sortingOrder == BoardOrder.ProtonTrail));
            Assert.Less(BoardOrder.ProtonTrail, BoardOrder.Body, "La estela pasa por debajo de los nodos.");
        }

        [Test]
        public void ADropStaysHiddenUntilItLeavesTheBag()
        {
            Proton proton = Fly(Faction.Player, 7);
            view.Advance(0f);
            Move(proton, 14f + Epsilon);
            view.Advance(0f);
            Assert.AreEqual(2, view.DropCount);
            Assert.IsTrue(view.Drops.All(d => !d.enabled), "A 7 y 0 px del protón, dentro de su bolsa de 13.");

            Move(proton, 6f);
            view.Advance(0f);
            Assert.AreEqual(1, view.Drops.Count(d => d.enabled), "La primera queda a 13 px: ya ha salido.");
        }

        [Test]
        public void ADropFadesInFourStepsOfTwoFrames()
        {
            Proton proton = Fly(Faction.Player, 7);
            view.Advance(0f);
            Move(proton, 7f + Epsilon);
            view.Advance(0f);
            Sprite[] steps = art.FramesFor(Faction.Player, 1).Drop;
            SpriteRenderer drop = view.Drops.Single();

            Assert.AreSame(steps[0], drop.sprite);
            view.Advance(Frame + Epsilon);
            Assert.AreSame(steps[0], drop.sprite, "Cada paso dura 2 frames.");
            view.Advance(Frame);
            Assert.AreSame(steps[1], drop.sprite);
            view.Advance(2 * Frame);
            Assert.AreSame(steps[2], drop.sprite);
            view.Advance(2 * Frame);
            Assert.AreSame(steps[3], drop.sprite);
            view.Advance(2 * Frame);
            Assert.AreEqual(0, view.DropCount, "A los 8 frames, 0.67 s, la gota desaparece.");
        }

        [Test]
        public void TheTrailOutlivesTheProton()
        {
            Proton proton = Fly(Faction.Enemy, 42);
            view.Advance(0f);
            Move(proton, 14f + Epsilon);
            view.Advance(0f);

            proton.Vanish();
            view.Advance(0f);

            Assert.IsFalse(view.TryGetVisual(proton, out _, out _, out _), "El cuerpo se va con el protón.");
            Assert.AreEqual(2, view.DropCount);
            Assert.IsTrue(view.Drops.All(d => d.enabled), "Sin bolsa que las tape, las gotas que quedan se ven.");

            view.Advance(8 * Frame + Epsilon);
            Assert.AreEqual(0, view.DropCount);
        }

        [Test]
        public void APausedFrameChangesNothing()
        {
            Proton proton = Fly(Faction.Player, 7);
            view.Advance(0f);
            Move(proton, 30f);
            view.Advance(0f);
            Sprite body = Body(proton).sprite;
            Sprite[] drops = view.Drops.Select(d => d.sprite).ToArray();

            view.Advance(0f);
            view.Advance(0f);

            Assert.AreSame(body, Body(proton).sprite);
            CollectionAssert.AreEqual(drops, view.Drops.Select(d => d.sprite).ToArray());
        }

        [Test]
        public void APooledProtonStartsAFreshFlight()
        {
            Proton proton = Fly(Faction.Player, 7);
            view.Advance(0f);
            proton.Vanish();
            view.Advance(0f);

            proton.Launch(Vector2.zero, far, Faction.Enemy, 60, Balance);
            protons.Register(proton);
            view.Advance(0f);

            Assert.Contains(Body(proton).sprite, art.FramesFor(Faction.Enemy, 9).Body,
                "El color y la talla son los del vuelo nuevo.");
            AssertDigits(proton, new[] { 6, 0 }, new[] { -6, 0 });
        }

        // --- La vista provisional ---

        [Test]
        public void TheProvisionalViewStepsAsideWithArt()
        {
            var provisional = protons.gameObject.AddComponent<ProvisionalProtonView>();
            provisional.Configure(protons, null);
            Proton proton = Fly(Faction.Player, 7);

            provisional.Refresh();

            Assert.IsFalse(provisional.TryGetVisual(proton, out _, out _), "Con arte, ProtonVisual dibuja el protón.");
        }

        [Test]
        public void WithoutArtTheProvisionalViewStaysInCharge()
        {
            view.Configure(TestArt(complete: false), protons);
            var provisional = protons.gameObject.AddComponent<ProvisionalProtonView>();
            provisional.Configure(protons, null);
            Proton proton = Fly(Faction.Player, 7);

            provisional.Refresh();
            view.Advance(0f);

            Assert.IsTrue(provisional.TryGetVisual(proton, out SpriteRenderer body, out _));
            Assert.IsTrue(body.gameObject.activeSelf);
            Assert.IsFalse(view.TryGetVisual(proton, out _, out _, out _));
        }
    }
}
