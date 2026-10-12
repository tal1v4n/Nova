using System.Collections.Generic;
using NUnit.Framework;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Systems;
using Nova.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nova.Tests
{
    // T2-07: NodeConverter (8.1, 16.4, 18 #20 y #21), su comando, lo que el nodo deja de hacer
    // mientras se reconvierte y los dos destinos del panel de clic derecho (13.3 punto 5).
    public class ConversionTests
    {
        const float Tick = 0.02f;
        const string FontPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";

        // 3.0 s a 50 Hz (16.1).
        const int ConversionTicks = 150;

        readonly List<GameObject> spawned = new List<GameObject>();
        ProtonRegistry protons;
        InfoPanelState panel;
        NodeInfoPanel view;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        static UiText Text =>
            AssetDatabase.LoadAssetAtPath<UiText>("Assets/Data/UiText.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            protons = go.AddComponent<ProtonRegistry>();
            panel = go.AddComponent<InfoPanelState>();
            view = go.AddComponent<NodeInfoPanel>();
            view.Configure(panel, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath), Text);
            view.Subscribe();
        }

        [TearDown]
        public void TearDown()
        {
            view.Unsubscribe();
            panel.Close();
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
        }

        // Nodo con los componentes de la escena de prueba. Por defecto el Púlsar no se ofrece,
        // como en la fase 2 (18 #29); withPulsar lo añade.
        NodeController Node(string type, Faction faction, int energy, int level = 1, Vector2 position = default, bool withPulsar = false)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, type == "Asteroid" ? 0 : level);
            var targets = withPulsar
                ? new[] { Definition("Star"), Definition("Pulsar"), Definition("Magnetar") }
                : new[] { Definition("Star"), Definition("Magnetar") };
            go.AddComponent<NodeConverter>().Configure(Balance, targets);
            go.AddComponent<StarGenerator>();
            go.AddComponent<MagnetarTurret>().Configure(Balance, protons);
            return node;
        }

        static NodeConverter Converter(NodeController node) => node.GetComponent<NodeConverter>();

        static void Run(NodeController node, int ticks)
        {
            NodeConverter converter = Converter(node);
            for (int i = 0; i < ticks; i++)
                converter.Tick(Tick);
        }

        // --- Iniciar ---

        // 8.1 y 16.4: el coste es el del tipo de origen y se paga al iniciar; el tipo no cambia todavía.
        [TestCase("Star", "Magnetar", 20)]
        [TestCase("Magnetar", "Star", 30)]
        [TestCase("Pulsar", "Star", 20)]
        public void StartingPaysTheOriginCostAndKeepsTheType(string from, string to, int cost)
        {
            NodeController node = Node(from, Faction.Player, 40);
            NodeType fromType = node.Type;

            ActionCheck result = Converter(node).TryConvert(Faction.Player, Definition(to).Type);

            Assert.IsTrue(result.IsAllowed);
            Assert.AreEqual(cost, result.Cost);
            Assert.AreEqual(40 - cost, node.Energy);
            Assert.AreEqual(fromType, node.Type, "18 #20: durante los 3 s sigue siendo del tipo de origen.");
            Assert.IsTrue(node.HasState(NodeStateFlags.Converting));
            Assert.IsTrue(Converter(node).IsConverting);
        }

        [Test]
        public void MagnetarKeepsItsResistanceWhileConverting()
        {
            NodeController magnetar = Node("Magnetar", Faction.Player, 70);
            Converter(magnetar).TryConvert(Faction.Player, NodeType.Star);

            ImpactOutcome hit = ImpactResolver.Resolve(new ProtonSnapshot(Faction.Enemy, 20), magnetar.Snapshot(), Balance.MagnetarResistance);

            Assert.AreEqual(30, hit.NewEnergy, "40 - floor(20 × 0.5): sigue siendo Magnetar.");
            Assert.AreEqual(100, magnetar.EnergyCap);
        }

        [Test]
        public void NotEnoughEnergyChangesNothing()
        {
            NodeController magnetar = Node("Magnetar", Faction.Player, 29);

            ActionCheck result = Converter(magnetar).TryConvert(Faction.Player, NodeType.Star);

            Assert.AreEqual(ActionBlock.InsufficientEnergy, result.Block);
            Assert.AreEqual(30, result.Cost);
            Assert.AreEqual(29, magnetar.Energy);
            Assert.IsFalse(magnetar.HasState(NodeStateFlags.Converting));
        }

        [TestCase(Faction.Enemy)]
        [TestCase(Faction.Neutral)]
        public void OnlyTheOwnerConverts(Faction actor)
        {
            NodeController star = Node("Star", Faction.Player, 50);
            Assert.AreEqual(ActionBlock.NotOwner, Converter(star).TryConvert(actor, NodeType.Magnetar).Block);
            Assert.AreEqual(50, star.Energy);
        }

        // El Magnetar neutral no tiene dueño que lo reconvierta.
        [Test]
        public void NeutralMagnetarCannotBeConverted()
        {
            NodeController magnetar = Node("Magnetar", Faction.Neutral, 100);
            Assert.AreEqual(ActionBlock.NotOwner, Converter(magnetar).TryConvert(Faction.Player, NodeType.Star).Block);
        }

        [Test]
        public void NeverToTheSameTypeNorToAsteroid()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            Assert.AreEqual(ActionBlock.InvalidTargetType, Converter(star).TryConvert(Faction.Player, NodeType.Star).Block);
            Assert.AreEqual(ActionBlock.InvalidTargetType, Converter(star).TryConvert(Faction.Player, NodeType.Asteroid).Block);
        }

        [Test]
        public void AsteroidIsNotConverted()
        {
            NodeController rock = Node("Asteroid", Faction.Player, 10);
            Assert.AreEqual(ActionBlock.IsAsteroid, Converter(rock).TryConvert(Faction.Player, NodeType.Star).Block);
        }

        // 18 #29: un tipo que la partida aún no tiene no se ofrece, y va por delante de la Energía.
        [Test]
        public void TypeNotYetInTheGameIsUnavailable()
        {
            NodeController poor = Node("Star", Faction.Player, 5);
            NodeController rich = Node("Star", Faction.Player, 50);

            Assert.AreEqual(ActionBlock.Unavailable, Converter(poor).Check(Faction.Player, NodeType.Pulsar).Block);
            Assert.AreEqual(ActionBlock.Unavailable, Converter(rich).TryConvert(Faction.Player, NodeType.Pulsar).Block);
            Assert.AreEqual(50, rich.Energy);
        }

        [Test]
        public void CannotStartTwice()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            Converter(star).TryConvert(Faction.Player, NodeType.Magnetar);

            Assert.AreEqual(ActionBlock.Converting, Converter(star).TryConvert(Faction.Player, NodeType.Magnetar).Block);
            Assert.AreEqual(30, star.Energy, "No cobra dos veces.");
        }

        // 8.1: no se puede iniciar congelado ni infectado.
        [TestCase(NodeStateFlags.Frozen, ActionBlock.Frozen)]
        [TestCase(NodeStateFlags.Infected, ActionBlock.Infected)]
        public void StatesThatBlockTheStart(NodeStateFlags state, ActionBlock expected)
        {
            NodeController star = Node("Star", Faction.Player, 50);
            star.SetStateFlag(state, true);

            Assert.AreEqual(expected, Converter(star).TryConvert(Faction.Player, NodeType.Magnetar).Block);
            Assert.AreEqual(50, star.Energy);
        }

        // --- Terminar ---

        [Test]
        public void FinishesAfterExactlyThreeSeconds()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            Converter(star).TryConvert(Faction.Player, NodeType.Magnetar);

            Run(star, ConversionTicks - 1);
            Assert.AreEqual(NodeType.Star, star.Type, "A 2.98 s todavía no.");

            Run(star, 1);
            Assert.AreEqual(NodeType.Magnetar, star.Type);
            Assert.IsFalse(star.HasState(NodeStateFlags.Converting));
            Assert.IsFalse(Converter(star).IsConverting);
        }

        // 8.1: nivel 1 del tipo nuevo, y la Energía de ese momento recortada al Tope nuevo.
        [Test]
        public void EndsAtLevelOneWithTheEnergyClampedToTheNewCap()
        {
            NodeController magnetar = Node("Magnetar", Faction.Player, 100, level: 4);
            Converter(magnetar).TryConvert(Faction.Player, NodeType.Star);
            Assert.AreEqual(70, magnetar.Energy);

            Run(magnetar, ConversionTicks);

            Assert.AreEqual(NodeType.Star, magnetar.Type);
            Assert.AreEqual(1, magnetar.Level);
            Assert.AreEqual(50, magnetar.EnergyCap);
            Assert.AreEqual(50, magnetar.Energy, "70 recortado al Tope de la Estrella.");
            Assert.AreEqual(Faction.Player, magnetar.Faction);
            Assert.AreEqual(NodeState.Normal, magnetar.State);
        }

        // La Energía que entra durante los 3 s cuenta: se recorta al terminar, no al iniciar.
        [Test]
        public void EnergyReceivedWhileConvertingIsKept()
        {
            NodeController star = Node("Star", Faction.Player, 20);
            Converter(star).TryConvert(Faction.Player, NodeType.Magnetar);
            star.AddEnergy(40);

            Run(star, ConversionTicks);

            Assert.AreEqual(NodeType.Magnetar, star.Type);
            Assert.AreEqual(40, star.Energy, "Tope de la Estrella (50) mientras era Estrella: 0 + 40.");
        }

        // Quien escucha la bandera lee el conversor en ese mismo aviso: el panel lo hace. Al ponerla
        // ya tiene que estar convirtiendo, y al quitarla ya no.
        [Test]
        public void TheFlagEventSeesTheConverterUpToDate()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            NodeConverter converter = Converter(star);
            var seen = new List<string>();
            star.OnStateFlagsChanged += (a, b) =>
                seen.Add($"{(b & NodeStateFlags.Converting) != 0}:{converter.IsConverting}:{converter.TargetType}");

            converter.TryConvert(Faction.Player, NodeType.Magnetar);
            Run(star, ConversionTicks);

            CollectionAssert.AreEqual(new[] { "True:True:Magnetar", "False:False:Star" }, seen);
        }

        [Test]
        public void EventsTellTheStartAndTheEnd()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            var seen = new List<string>();
            Converter(star).OnConversionStarted += (a, b) => seen.Add($"start {a}>{b}");
            Converter(star).OnConversionFinished += (a, b) => seen.Add($"end {a}>{b}");

            Converter(star).TryConvert(Faction.Player, NodeType.Magnetar);
            Run(star, ConversionTicks);

            CollectionAssert.AreEqual(new[] { "start Star>Magnetar", "end Star>Magnetar" }, seen);
        }

        // --- Capturado a mitad (8.1, 18 #21) ---

        [Test]
        public void CaptureCancelsAndTheCostIsLost()
        {
            NodeController star = Node("Star", Faction.Player, 30, level: 3);
            NodeType cancelledTo = NodeType.Asteroid;
            Converter(star).OnConversionCancelled += (from, to) => cancelledTo = to;
            Converter(star).TryConvert(Faction.Player, NodeType.Magnetar);
            Run(star, ConversionTicks / 2);

            star.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(Faction.Enemy, 15), star.Snapshot(), Balance.MagnetarResistance));

            Assert.AreEqual(Faction.Enemy, star.Faction);
            Assert.AreEqual(5, star.Energy, "15 contra los 10 que dejó el coste: el coste no vuelve.");
            Assert.AreEqual(NodeType.Star, star.Type, "Queda como estaba antes de iniciarla.");
            Assert.AreEqual(3, star.Level);
            Assert.IsFalse(star.HasState(NodeStateFlags.Converting));
            Assert.AreEqual(NodeType.Magnetar, cancelledTo);

            Run(star, ConversionTicks);
            Assert.AreEqual(NodeType.Star, star.Type, "El reloj no sigue para el nuevo dueño.");
        }

        [Test]
        public void ADamagingHitDoesNotCancel()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            Converter(star).TryConvert(Faction.Player, NodeType.Magnetar);

            star.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(Faction.Enemy, 10), star.Snapshot(), Balance.MagnetarResistance));
            Run(star, ConversionTicks);

            Assert.AreEqual(NodeType.Magnetar, star.Type);
            Assert.AreEqual(20, star.Energy);
        }

        // --- Congelado pausa (8.1, 17 caso 8) ---

        [Test]
        public void FreezingPausesTheClockAndThawingResumesIt()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            Converter(star).TryConvert(Faction.Player, NodeType.Magnetar);
            Run(star, 100);

            Assert.IsTrue(star.SetStateFlag(NodeStateFlags.Frozen, true), "14.6: Congelado convive con Reconvirtiendo.");
            Run(star, 500);
            Assert.AreEqual(NodeType.Star, star.Type);
            Assert.AreEqual(2f, Converter(star).Elapsed, 1e-4f, "El reloj se detiene donde estaba.");

            star.SetStateFlag(NodeStateFlags.Frozen, false);
            Run(star, ConversionTicks - 100);
            Assert.AreEqual(NodeType.Magnetar, star.Type, "Sin perder lo ya corrido.");
        }

        // --- Lo que no hace mientras se reconvierte (8.1) ---

        [Test]
        public void DoesNotGenerate()
        {
            NodeController star = Node("Star", Faction.Player, 30);
            Converter(star).TryConvert(Faction.Player, NodeType.Magnetar);
            StarGenerator generator = star.GetComponent<StarGenerator>();

            for (int i = 0; i < ConversionTicks - 10; i++)
                generator.Tick(Tick);

            Assert.AreEqual(10, star.Energy, "2.8 s a 0.5 E/s habrían dado +1.");
        }

        [Test]
        public void DoesNotShoot()
        {
            NodeController magnetar = Node("Magnetar", Faction.Enemy, 50);
            NodeController far = Node("Star", Faction.Neutral, 0, position: new Vector2(50f, 0f));
            Converter(magnetar).TryConvert(Faction.Enemy, NodeType.Star);

            var go = new GameObject("Proton");
            spawned.Add(go);
            var proton = go.AddComponent<Proton>();
            proton.Launch(new Vector2(1f, 0f), far, Faction.Player, 10, Balance);
            protons.Register(proton);

            MagnetarTurret turret = magnetar.GetComponent<MagnetarTurret>();
            Assert.IsNull(turret.FindTarget());
            for (int i = 0; i < 50; i++)
                turret.Tick(Tick);

            Assert.AreEqual(10, proton.Charge);
        }

        [Test]
        public void DoesNotSend()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            NodeController other = Node("Star", Faction.Enemy, 10, position: new Vector2(3f, 0f));
            Converter(star).TryConvert(Faction.Player, NodeType.Magnetar);

            Assert.AreEqual(LaunchBlock.Converting, ProtonLauncher.Check(star, other, Faction.Player, false, out _));
        }

        [Test]
        public void DoesNotUpgrade()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            Converter(star).TryConvert(Faction.Player, NodeType.Magnetar);

            Assert.AreEqual(ActionBlock.Converting, star.TryUpgrade(Faction.Player).Block);
            Assert.AreEqual(1, star.Level);
        }

        // --- Comando ---

        [Test]
        public void IssuerConvertsAsPlayer()
        {
            var go = new GameObject("Issuer");
            spawned.Add(go);
            var issuer = go.AddComponent<PlayerCommandIssuer>();
            NodeController own = Node("Star", Faction.Player, 20);
            NodeController enemy = Node("Star", Faction.Enemy, 20);

            Assert.IsTrue(issuer.Convert(own, NodeType.Magnetar).IsAllowed);
            Assert.AreEqual(ActionBlock.NotOwner, issuer.Convert(enemy, NodeType.Magnetar).Block);
        }

        // --- Panel (13.3 punto 5) ---

        [Test]
        public void PanelShowsTheTwoDestinationsWithTheirCost()
        {
            panel.Open(Node("Star", Faction.Player, 30, withPulsar: true), Vector2.zero);

            Assert.AreEqual("Reconvertir", view.Line(4));
            Assert.AreEqual(NodeInfoPanel.ButtonLook.Enabled, view.ConversionButton(0));
            Assert.AreEqual("Púlsar · 20", view.ConversionText(0));
            Assert.AreEqual(NodeInfoPanel.ButtonLook.Enabled, view.ConversionButton(1));
            Assert.AreEqual("Magnetar · 20", view.ConversionText(1));
        }

        [Test]
        public void PanelPaintsAMissingCostInRed()
        {
            panel.Open(Node("Magnetar", Faction.Player, 10), Vector2.zero);

            Assert.AreEqual(NodeInfoPanel.ButtonLook.MissingEnergy, view.ConversionButton(0));
            Assert.AreEqual("Estrella · <color=#F2456B>30</color>", view.ConversionText(0));
        }

        [Test]
        public void PanelGreysOutATypeNotYetInTheGame()
        {
            panel.Open(Node("Star", Faction.Player, 30), Vector2.zero);

            Assert.AreEqual(NodeInfoPanel.ButtonLook.Unavailable, view.ConversionButton(0));
            Assert.AreEqual("Púlsar", view.ConversionText(0), "Sin coste, como en el menú de encendido.");
        }

        [Test]
        public void PanelReplacesTheButtonsWhileConverting()
        {
            NodeController star = Node("Star", Faction.Player, 50);
            panel.Open(star, Vector2.zero);

            Converter(star).TryConvert(Faction.Player, NodeType.Magnetar);

            Assert.AreEqual("Reconvirtiendo a Magnetar", view.Line(4));
            Assert.AreEqual(NodeInfoPanel.ButtonLook.Hidden, view.ConversionButton(0));
            Assert.AreEqual(NodeInfoPanel.ButtonLook.Hidden, view.ConversionButton(1));
            Assert.AreEqual(NodeInfoPanel.ButtonLook.Disabled, view.Button, "Mejorar está bloqueado, y no por la Energía.");
            Assert.AreEqual("Mejorar · 20", view.ButtonText, "Sin rojo.");

            Run(star, ConversionTicks);

            Assert.AreEqual("Magnetar · Nivel 1", view.Line(0));
            Assert.AreEqual("Reconvertir", view.Line(4));
            Assert.AreEqual("Estrella · 30", view.ConversionText(0));
        }

        [Test]
        public void ForeignNodeHasNoConversion()
        {
            panel.Open(Node("Star", Faction.Enemy, 50), Vector2.zero);

            Assert.IsNull(view.Line(4));
            Assert.AreEqual(NodeInfoPanel.ButtonLook.Hidden, view.ConversionButton(0));
            Assert.AreEqual(NodeInfoPanel.ButtonLook.Hidden, view.ConversionButton(1));
        }

        [Test]
        public void TheDrawnConversionButtonsAreTheClickTargets()
        {
            Vector2 center = new Vector2(1f, -2f);
            panel.Open(Node("Star", Faction.Player, 30), center);

            for (int i = 0; i < 2; i++)
            {
                Assert.Less(Vector2.Distance(center + InfoPanelState.ConversionOffsets[i], view.ConversionPosition(i)), 1e-5f);
                Assert.AreEqual(i, panel.ConversionOptionAt(view.ConversionPosition(i)));
            }
            Assert.AreEqual(-1, panel.ConversionOptionAt(center + InfoPanelState.ButtonOffset), "El botón de mejora es otro blanco.");
        }

        [Test]
        public void PanelGeometryFallsOnWholePixelsAndInsideThePanel()
        {
            Vector2 size = InfoPanelState.ConversionButtonSize;
            foreach (float v in new[] { InfoPanelState.Height, size.x, size.y, InfoPanelState.RowY(3), InfoPanelState.RowY(5),
                         InfoPanelState.ConversionOffsets[0].x, InfoPanelState.ConversionOffsets[1].x })
                Assert.AreEqual(Mathf.Round(v * 64f), v * 64f, 1e-4f);

            float bottom = InfoPanelState.ConversionOffsets[0].y - size.y / 2f;
            Assert.Greater(bottom, -InfoPanelState.Height / 2f, "La última fila cabe en el panel.");
            float gap = (InfoPanelState.ConversionOffsets[1].x - size.x / 2f) - (InfoPanelState.ConversionOffsets[0].x + size.x / 2f);
            Assert.AreEqual(4f, gap * 64f, 1e-4f, "4 px entre los dos botones.");
        }

        [TestCase(NodeType.Star, NodeType.Pulsar, NodeType.Magnetar)]
        [TestCase(NodeType.Pulsar, NodeType.Star, NodeType.Magnetar)]
        [TestCase(NodeType.Magnetar, NodeType.Star, NodeType.Pulsar)]
        public void DestinationsAreTheOtherTwoTypesInOrder(NodeType from, NodeType first, NodeType second)
        {
            CollectionAssert.AreEqual(new[] { first, second }, InfoPanelState.ConversionTargets(from));
        }
    }
}
