using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Presentation;
using Nova.UI;
using Nova.Utils;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nova.Tests
{
    // T6-09: la UI de partida con su arte (A.6). Las piezas son sprites sintéticos de colores conocidos: lo que se
    // comprueba es el montaje —dónde cae cada píxel, como en los generadores—, no el dibujo, que es de tools/.
    public class UiArtTests
    {
        const string FontPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";

        static readonly Color32 Frame = new Color32(10, 20, 30, 255);
        static readonly Color32 Panel = new Color32(0x11, 0x14, 0x2A, 255);
        static readonly Color32 Have = new Color32(40, 40, 40, 255);
        static readonly Color32 Missing = new Color32(200, 0, 90, 255);
        static readonly Color32 Sweep = new Color32(250, 250, 250, 255);
        static readonly Color32 Stopped = new Color32(150, 150, 150, 255);

        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
            foreach (Object o in created)
                if (o != null)
                    Object.DestroyImmediate(o);
            created.Clear();
            SpritePixels.ClearCache();
        }

        // Un sprite legible con los píxeles que da pixel(x, y), con la y hacia abajo como en los generadores.
        Sprite Make(int w, int h, Func<int, int, Color32> pixel)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var data = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    data[(h - 1 - y) * w + x] = pixel(x, y);
            texture.SetPixels32(data);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0f, 1f), 64);
            created.Add(texture);
            created.Add(sprite);
            return sprite;
        }

        Sprite Solid(int w, int h, Color32 color) => Make(w, h, (x, y) => color);

        // Una placa de 13.12 de 20 x 20: el marco en la silueta y el relleno dentro.
        Sprite Plate(Color32 frame, Color32 fill) => Make(20, 20, (x, y) =>
            PlateShape.Inside(20, 20, x, y) ? fill : PlateShape.Silhouette(20, 20, x, y) ? frame : default);

        // --- Las primitivas ---

        // La escalera de 13.12 es la de tools/gen_topbar_v1.py: 4 + 4 px, y el interior a 2 px del borde.
        [Test]
        public void PlateShapeIsTheStairOfTheLanguage()
        {
            Assert.IsFalse(PlateShape.Silhouette(94, 20, 3, 0));
            Assert.IsTrue(PlateShape.Silhouette(94, 20, 4, 0));
            Assert.IsFalse(PlateShape.Silhouette(94, 20, 1, 2));
            Assert.IsTrue(PlateShape.Silhouette(94, 20, 2, 2));
            Assert.IsTrue(PlateShape.Silhouette(94, 20, 0, 4));
            Assert.IsFalse(PlateShape.Inside(94, 20, 5, 2));
            Assert.IsTrue(PlateShape.Inside(94, 20, 6, 2));
            Assert.IsFalse(PlateShape.Inside(94, 20, 3, 4));
            Assert.IsTrue(PlateShape.Inside(94, 20, 4, 4));
            Assert.IsTrue(PlateShape.Inside(94, 20, 2, 6));
            Assert.IsFalse(PlateShape.Inside(94, 20, 1, 6));
            Assert.IsTrue(PlateShape.Inside(94, 20, 91, 10), "Simétrica.");
            Assert.IsFalse(PlateShape.Inside(94, 20, 92, 10));
        }

        [Test]
        public void NineSliceKeepsTheBordersAndTilesTheCenterFromTheBorder()
        {
            // Una placa cuyo color dice su columna: el centro se repite de 6 en adelante, en periodos de 8.
            Sprite plate = Make(20, 20, (x, y) => new Color32((byte)x, (byte)y, 0, 255));
            var c = new PixelCanvas(94, 20);
            c.NineSlice(SpritePixels.Of(plate), new RectInt(0, 0, 94, 20), 6);

            Assert.AreEqual(0, c.Get(0, 0).r);
            Assert.AreEqual(19, c.Get(93, 0).r, "El borde derecho es el de la placa.");
            Assert.AreEqual(6, c.Get(6, 5).r);
            Assert.AreEqual(6, c.Get(14, 5).r, "El centro se repite cada 8 desde el borde.");
            Assert.AreEqual(13, c.Get(21, 5).r);
            Assert.AreEqual(14, c.Get(88, 5).r);
        }

        [Test]
        public void TilesFollowAnAbsoluteOrigin()
        {
            Sprite tile = Make(8, 1, (x, y) => new Color32((byte)(x + 1), 0, 0, 255));
            var c = new PixelCanvas(20, 1);
            c.Tile(SpritePixels.Of(tile), new RectInt(5, 0, 10, 1), 0, 0);

            Assert.AreEqual(0, c.Get(4, 0).a, "Fuera del rectángulo no pinta.");
            Assert.AreEqual(6, c.Get(5, 0).r, "La columna 5 del lienzo es la 5 de la tesela.");
            Assert.AreEqual(1, c.Get(8, 0).r);
        }

        // --- UiText ---

        [Test]
        public void SlotAndCancelTextsSplitForTheirTwoColors()
        {
            var text = AssetDatabase.LoadAssetAtPath<UiText>("Assets/Data/UiText.asset");
            Assert.AreEqual(text.Slot(2, 35), text.SlotNumber(2) + "35");
            Assert.AreEqual(text.Cancel(13), text.CancelWord + text.CancelRefund(13));
            StringAssert.StartsWith("+", text.CancelRefund(13), "El «+» va con lo que devuelve, en cian.");
        }

        // --- El alto del panel sigue a las filas (13.3) ---

        readonly List<GameObject> spawned = new List<GameObject>();

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        NodeController Node(string type, Faction faction, int level = 1, bool converter = true, bool caster = false)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, 10, type == "Asteroid" ? 0 : level);
            if (converter)
                go.AddComponent<NodeConverter>();
            if (caster)
                go.AddComponent<PulsarCaster>();
            return node;
        }

        [Test]
        public void TheLayoutFollowsTheNodeAndWhoLooks()
        {
            InfoPanelState.PanelLayout own = InfoPanelState.LayoutFor(Node("Star", Faction.Player), Faction.Player);
            Assert.AreEqual(6, own.Rows, "Información, mejora y las dos de la reconversión.");
            Assert.AreEqual(3, own.UpgradeRow);
            Assert.AreEqual(4, own.ConversionHeaderRow);
            Assert.AreEqual(5, own.ConversionRow);
            CollectionAssert.AreEqual(new[] { 0, 3, 4 }, own.Blocks);
            Assert.AreEqual((2 * 8 + 24 * 6) / 64f, own.Height, 1e-6f);

            InfoPanelState.PanelLayout foreign = InfoPanelState.LayoutFor(Node("Star", Faction.Enemy), Faction.Player);
            Assert.AreEqual(3, foreign.Rows, "Un nodo ajeno, solo la información.");
            Assert.IsFalse(foreign.HasConversion);

            int max = Definition("Star").MaxLevel;
            Assert.AreEqual(4, InfoPanelState.LayoutFor(Node("Star", Faction.Enemy, max), Faction.Player).Rows,
                "NIVEL MÁXIMO es información para todos.");
            Assert.AreEqual(4, InfoPanelState.LayoutFor(Node("Asteroid", Faction.Neutral), Faction.Player).Rows);

            InfoPanelState.PanelLayout pulsar = InfoPanelState.LayoutFor(Node("Pulsar", Faction.Player, caster: true), Faction.Player);
            Assert.AreEqual(9, pulsar.Rows, "El Púlsar propio, con la rueda.");
            Assert.AreEqual(6, pulsar.WheelHeaderRow);
            Assert.AreEqual(7, pulsar.WheelRow);
            Assert.AreEqual(8, pulsar.CancelRow);
            CollectionAssert.AreEqual(new[] { 0, 3, 4, 6 }, pulsar.Blocks);
        }

        // --- Los botones de reconversión («El barrido») ---

        PanelArt.ConversionPieces ConversionPieces()
        {
            Sprite plate = Plate(Frame, Panel);
            return new PanelArt.ConversionPieces
            {
                plate = new[] { plate, plate, plate, plate, plate, plate },
                front = new[] { Make(5, 16, (x, y) => x < 2 ? Sweep : default), Make(5, 16, (x, y) => x == 0 ? Stopped : default) },
                fill = new[] { Solid(8, 8, Have), Solid(8, 8, Missing) },
            };
        }

        [Test]
        public void TheSweepLeavesThePrimaryButtonInsideTheFrame()
        {
            PanelArt.ConversionPieces p = ConversionPieces();
            var c = new PixelCanvas(94, 22);
            PanelSkin.ComposeConversion(c, p, PanelArt.ConversionLook.Hover, 1f, 30, 20);

            Assert.AreEqual(p.primaryFill, c.Get(40, 10), "Barrido entero: primario.");
            Assert.AreEqual(p.primaryFill, c.Get(6, 2));
            Assert.AreEqual(Frame, c.Get(4, 2), "El marco no se toca.");
            Assert.AreEqual(Frame, c.Get(1, 10));

            PanelSkin.ComposeConversion(c, p, PanelArt.ConversionLook.Hover, 0f, 30, 20);
            Assert.AreEqual(Panel, c.Get(40, 10), "Sin barrido, el secundario.");
        }

        [Test]
        public void ThePressedButtonDropsAndLightens()
        {
            PanelArt.ConversionPieces p = ConversionPieces();
            var c = new PixelCanvas(94, 22);
            PanelSkin.ComposeConversion(c, p, PanelArt.ConversionLook.Pressed, 1f, 30, 20);

            Assert.AreEqual(0, c.Get(40, 0).a, "Baja 2 px.");
            Assert.AreEqual(0, c.Get(40, 1).a);
            Assert.AreEqual(Frame, c.Get(40, 2));
            Assert.AreEqual(p.pressedFill, c.Get(40, 12));
        }

        // Sin Energía, el frente se para en Energía / coste y lo que falta va en magenta (13.3, desviación 380).
        [Test]
        public void WithoutEnergyTheFrontStopsWhereTheEnergyReaches()
        {
            PanelArt.ConversionPieces p = ConversionPieces();
            var c = new PixelCanvas(94, 22);
            PanelSkin.ComposeConversion(c, p, PanelArt.ConversionLook.Missing, 0f, 10, 20);

            int xf = p.innerX0 + Mathf.RoundToInt((p.innerX1 - p.innerX0) * 0.5f);
            Assert.AreEqual(Have, c.Get(20, 10), "Detrás, lo que tienes.");
            Assert.AreEqual(Stopped, c.Get(xf, 10), "El frente, parado.");
            Assert.AreEqual(Missing, c.Get(70, 10), "Delante, lo que falta.");
        }

        // --- La rueda («Las teclas») ---

        PanelArt.WheelPieces WheelPieces()
        {
            Sprite plate = Plate(Frame, Panel);
            var plates = new Sprite[PanelArt.Powers * PanelArt.SlotPlates];
            for (int i = 0; i < plates.Length; i++)
                plates[i] = plate;
            Sprite clear = Make(8, 8, (x, y) => default);
            Sprite icon = Solid(12, 12, new Color32(1, 2, 3, 255));
            return new PanelArt.WheelPieces
            {
                plate = plates,
                fill = new[] { clear, clear, clear, clear, clear, clear },
                hover = new[] { clear, clear, clear, clear, clear },
                icons = new[] { icon, icon, icon, icon, icon, icon, icon, icon, icon, icon, icon, icon },
                lockIcon = Solid(7, 9, new Color32(4, 5, 6, 255)),
                charging = new[] { new Color32(70, 0, 0, 255), new Color32(0, 70, 0, 255), new Color32(0, 0, 70, 255) },
                paused = new[] { new Color32(30, 0, 0, 255), new Color32(0, 30, 0, 255), new Color32(0, 0, 30, 255) },
                front = new[] { new Color32(140, 0, 0, 255), new Color32(0, 140, 0, 255), new Color32(0, 0, 140, 255) },
                light = new[] { new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255), new Color32(0, 0, 255, 255) },
                lightEdge = new[] { new Color32(200, 0, 0, 255), new Color32(0, 200, 0, 255), new Color32(0, 0, 200, 255) },
            };
        }

        [Test]
        public void AChargingSlotFillsUpToTheProgress()
        {
            PanelArt.WheelPieces p = WheelPieces();
            var c = new PixelCanvas(60, 22);
            PanelSkin.ComposeSlot(c, p, 2, PanelArt.SlotLook.Charging, 0.5f, false, 0, 0f);

            int filled = Mathf.FloorToInt(56 * 0.5f);
            Assert.AreEqual(p.charging[2], c.Get(20, 15), "Hasta el progreso, el color del poder.");
            Assert.AreEqual(p.front[2], c.Get(1 + filled, 10), "Y en el frente, su columna.");
            Assert.AreEqual(Panel, c.Get(2 + filled + 4, 10), "Delante, la tecla.");

            PanelSkin.ComposeSlot(c, p, 2, PanelArt.SlotLook.Charging, 0.5f, true, 0, 0f);
            Assert.AreEqual(p.paused[2], c.Get(20, 15), "Pausada, más apagada.");
            Assert.AreEqual(p.paused[2], c.Get(1 + filled, 10), "Y sin frente.");
        }

        [Test]
        public void AReadySlotHasALightThatCrossesIt()
        {
            PanelArt.WheelPieces p = WheelPieces();
            var c = new PixelCanvas(60, 22);
            int frame = 16;
            PanelSkin.ComposeSlot(c, p, 0, PanelArt.SlotLook.Ready, 1f, false, frame, 0f);
            int head = Mathf.RoundToInt(frame / 32f * p.lightTravel) + p.lightFrom;

            Assert.AreEqual(p.lightEdge[0], c.Get(head, 10));
            Assert.AreEqual(p.light[0], c.Get(head + 1, 10));
            Assert.AreEqual(p.lightEdge[0], c.Get(head + 2, 10));
        }

        [Test]
        public void ALockedSlotShowsTheLockInsteadOfTheIcon()
        {
            PanelArt.WheelPieces p = WheelPieces();
            var c = new PixelCanvas(60, 22);
            PanelSkin.ComposeSlot(c, p, 1, PanelArt.SlotLook.Locked, 0f, false, 0, 0f);
            Assert.AreEqual(new Color32(4, 5, 6, 255), c.Get(p.lockAt.x, p.lockAt.y));
            PanelSkin.ComposeSlot(c, p, 1, PanelArt.SlotLook.Normal, 0f, false, 0, 0f);
            Assert.AreEqual(new Color32(1, 2, 3, 255), c.Get(p.iconAt.x, p.iconAt.y));
        }

        // --- El menú de encendido («Los nodos») ---

        [Test]
        public void TheCornersCloseOnHoverAndAreGoneWhenUnavailable()
        {
            Sprite corner = Make(7, 7, (x, y) => x == 0 && y == 0 ? Sweep : default);
            Sprite node = Make(72, 72, (x, y) => default);
            var p = new PanelArt.IgnitionPieces
            {
                corner = new[] { corner, corner, corner, corner },
                nodes = new[] { node, node, node, node, node, node, node, node, node },
            };
            var c = new PixelCanvas(72, 72);
            PanelSkin.ComposeOption(c, p, 0, PanelArt.CornerLook.Normal, PanelArt.NodeLook.Full, false, 0f);
            Assert.AreEqual(Sweep, c.Get(12, 12), "La esquina, en el marco de 48.");
            Assert.AreEqual(Sweep, c.Get(59, 59), "Reflejada.");

            PanelSkin.ComposeOption(c, p, 0, PanelArt.CornerLook.Hover, PanelArt.NodeLook.Full, false, 1f);
            Assert.AreEqual(Sweep, c.Get(14, 14), "Encima, se cierra 2 px.");

            PanelSkin.ComposeOption(c, p, 0, null, PanelArt.NodeLook.Outline, false, 0f);
            Assert.AreEqual(0, c.Get(12, 12).a, "No disponible: sin esquinas.");
        }

        // --- El cable del panel («La sonda») ---

        [Test]
        public void TheTetherRunsFromTheNodeEdgeToThePanelSide()
        {
            Color32 body = new Color32(9, 9, 9, 255);
            var p = new PanelArt.PanelPieces
            {
                tether = new[] { Make(8, 3, (x, y) => y == 1 && x % 2 == 0 ? body : default), null, null },
                pulse = new[] { Make(3, 3, (x, y) => new Color32((byte)(x + 1), 0, 0, 255)), null, null },
                socket = new[] { Solid(3, 7, Have), null, null },
            };
            // Nodo de huella 40; el panel empieza 48 px a la derecha, centrado en él.
            var path = new PanelSkin.TetherPath(p, 40, 48, -50, 100, false);
            RectInt b = path.Bounds;
            var c = new PixelCanvas(b.width, b.height);
            PanelSkin.ComposeTether(c, p, 0, path, 0);

            Assert.AreEqual(22, path.X0, "Sale a media huella + 2.");
            Assert.AreEqual(45, path.X1, "Llega a 3 px del panel.");
            Assert.AreEqual(0, path.Ya, "Sin escalón: el panel no se ha desplazado.");
            Assert.AreEqual(body, c.Get(path.X0 - b.x, -b.y), "Un punto cada 2 px.");
            Assert.AreEqual(Have, c.Get(path.X1 - b.x, -b.y), "El enchufe, donde toca el panel.");
        }

        // --- El número y la barra del nodo ---

        NodeLabelArt LabelArt()
        {
            var art = ScriptableObject.CreateInstance<NodeLabelArt>();
            created.Add(art);
            var player = new Sprite[NodeLabelArt.Digits * 32];
            var enemy = new Sprite[NodeLabelArt.Digits * 32];
            var neutral = new Sprite[NodeLabelArt.Digits];
            for (int d = 0; d < NodeLabelArt.Digits; d++)
            {
                for (int f = 0; f < 32; f++)
                {
                    int digit = d, frame = f;
                    player[d * 32 + f] = Make(12, 16, (x, y) => x == 0 && y == 0 ? new Color32((byte)digit, (byte)frame, 1, 255) : default);
                    enemy[d * 32 + f] = player[d * 32 + f];
                }
                neutral[d] = player[d * 32];
            }
            art.ConfigureEnergyLabel(player, enemy, neutral, new Vector2Int(12, 16), new[] { 12, 8, 12, 12, 12, 12, 12, 12, 12, 12 },
                32, 12f, 11, 20);

            var fuse = new Sprite[NodeLabelArt.Powers * 33];
            var flame = new Sprite[2 * NodeLabelArt.Powers * 33];
            Sprite fuseTile = Solid(8, 6, Have);
            Sprite flameCell = Make(5, 6, (x, y) => x == 2 ? Sweep : default);
            for (int i = 0; i < fuse.Length; i++)
                fuse[i] = fuseTile;
            for (int i = 0; i < flame.Length; i++)
                flame[i] = flameCell;
            Sprite socket = Solid(2, 6, Frame);
            Sprite track = Make(2, 6, (x, y) => x == 0 ? Missing : default);
            art.ConfigureChargeBar(new[] { socket, socket }, new[] { track, track }, fuse, flame, 48, 6, 2, 2, 46, 5, 2, 32, 12f,
                new[] { Stopped, Stopped, Stopped }, 2, 1.5f, -3, 52);
            return art;
        }

        [Test]
        public void TheEnergyNumberAdvancesByItsDigitsAndShiftsTheirPhase()
        {
            NodeLabelArt art = LabelArt();
            Assert.IsTrue(art.HasEnergyLabel);
            var c = new PixelCanvas(36, 16);
            EnergyLabelView.Compose(c, art, 100, Faction.Player, 5);

            // «100» mide 8 + 12 + 12 = 32 y va centrado en 36.
            Assert.AreEqual(new Color32(1, 5, 1, 255), c.Get(2, 0), "El 1, en el frame 5.");
            Assert.AreEqual(new Color32(0, 16, 1, 255), c.Get(10, 0), "El primer 0, 11 frames después.");
            Assert.AreEqual(new Color32(0, 27, 1, 255), c.Get(22, 0), "El segundo, 22.");
        }

        [Test]
        public void TheChargeBarBurnsUpToTheInvestedEnergy()
        {
            NodeLabelArt art = LabelArt();
            Assert.IsTrue(art.HasChargeBar);
            var c = new PixelCanvas(48, 6);
            ChargeBarView.Compose(c, art, Faction.Player, PowerType.Wormhole, 30, 60, false, false, 0);

            int n = 46 * 30 / 60;
            Assert.AreEqual(Frame, c.Get(0, 0), "El casquillo del bando.");
            Assert.AreEqual(Have, c.Get(5, 0), "Lo que arde.");
            Assert.AreEqual(Sweep, c.Get(2 + n - 1, 0), "La llama, con su frente en la última columna de la mecha.");
            Assert.AreEqual(Missing, c.Get(40, 0), "Delante, lo que falta.");
            Assert.AreEqual(0, c.Get(41, 0).a);

            ChargeBarView.Compose(c, art, Faction.Player, PowerType.Wormhole, 60, 60, true, false, 16);
            float head = 16 / 32f * 52 - 3;
            Assert.AreEqual(Stopped, c.Get(2 + Mathf.RoundToInt(head), 2), "Lista, la luz recorre la mecha.");
            Assert.AreEqual(Sweep, c.Get(48 - 5 + 2, 0), "Y la llama va pegada al borde derecho.");
        }

        // --- El panel entero, con arte ---

        PanelArt FullPanelArt()
        {
            var art = ScriptableObject.CreateInstance<PanelArt>();
            created.Add(art);
            Sprite three(int w, int h, Color32 c) => Solid(w, h, c);
            var panel = new PanelArt.PanelPieces
            {
                screen = Solid(8, 8, Panel),
                corner = new[] { three(8, 8, Frame), three(8, 8, Frame), three(8, 8, Frame) },
                edge = new[] { three(2, 1, Frame), three(2, 1, Frame), three(2, 1, Frame) },
                tether = new[] { three(8, 3, Have), three(8, 3, Have), three(8, 3, Have) },
                pulse = new[] { three(3, 3, Sweep), three(3, 3, Sweep), three(3, 3, Sweep) },
                socket = new[] { three(3, 7, Frame), three(3, 7, Frame), three(3, 7, Frame) },
                divider = new[] { three(4, 1, Stopped), three(4, 1, Stopped), three(4, 1, Stopped) },
                rule = new[] { three(4, 3, Sweep), three(4, 3, Sweep), three(4, 3, Sweep) },
                titleColor = new[] { Sweep, Missing, Stopped },
            };
            PanelArt.WheelPieces wheel = WheelPieces();
            var ignition = new PanelArt.IgnitionPieces
            {
                corner = new[] { three(7, 7, Sweep), three(7, 7, Sweep), three(7, 7, Sweep), three(7, 7, Sweep) },
                nodes = new Sprite[9],
            };
            for (int i = 0; i < 9; i++)
                ignition.nodes[i] = Solid(72, 72, Have);
            art.Configure(panel, ConversionPieces(), wheel, ignition);
            return art;
        }

        [Test]
        public void ThePanelWithArtIsAsTallAsItsRowsAndDrawsItsButtons()
        {
            PanelArt art = FullPanelArt();
            Assert.IsTrue(art.HasPanel);
            var go = new GameObject("Systems");
            spawned.Add(go);
            var state = go.AddComponent<InfoPanelState>();
            var view = go.AddComponent<NodeInfoPanel>();
            var text = AssetDatabase.LoadAssetAtPath<UiText>("Assets/Data/UiText.asset");
            view.Configure(state, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath), text, art);
            view.Subscribe();

            NodeController star = Node("Star", Faction.Player);
            star.GetComponent<NodeConverter>().Configure(
                AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset"),
                new[] { Definition("Star"), Definition("Pulsar"), Definition("Magnetar") });
            state.Open(star, InfoPanelState.CenterBeside(Vector2.zero, false));

            Assert.IsTrue(view.PanelView.enabled, "Con arte, el panel es su lienzo.");
            Assert.AreEqual(2 * 8 + 24 * 6, view.PanelCanvas.Height, "Seis filas: el alto sigue al contenido.");
            Assert.AreEqual(Frame, view.PanelCanvas.Get(0, 0), "La esquina del bando.");
            Assert.AreEqual(Stopped, view.PanelCanvas.Get(8, 8 + 3 * 24 - 1), "La línea sobre el bloque de la mejora.");
            Assert.IsTrue(view.ConversionArtVisible(0));
            Assert.IsTrue(view.ConversionArtVisible(1));
            Assert.AreEqual(text.TypeName(NodeType.Pulsar) + " · 20", view.ConversionText(0));
            Assert.AreEqual(Sweep, (Color32)view.LineLabel(0).color, "El título, en el color del bando.");

            state.Close();
            view.Unsubscribe();
        }
    }
}
