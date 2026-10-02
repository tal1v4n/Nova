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
    // T2-04: MagnetarTurret (4.4, 6.3), el derribo de Proton y la vista provisional del radio.
    // Los protones de estos tests no se mueven salvo que el test los avance: la torreta dispara
    // a donde están, como en el tick real, donde corre (0) antes de que vuelen (+10).
    public class MagnetarTests
    {
        const float Tick = 0.02f;

        readonly List<GameObject> spawned = new List<GameObject>();
        ProtonRegistry protons;
        NodeController far;

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            protons = go.AddComponent<ProtonRegistry>();
            // Destino lejano de los protones de prueba: nunca llegan durante un test.
            far = Node("Star", Faction.Neutral, 0, new Vector2(50f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                Object.DestroyImmediate(go);
            spawned.Clear();
        }

        NodeController Node(string type, Faction faction, int energy, Vector2 position, int level = 1)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            node.Initialize(Definition(type), faction, energy, type == "Asteroid" ? 0 : level);
            go.AddComponent<MagnetarTurret>().Configure(Balance, protons);
            return node;
        }

        MagnetarTurret Magnetar(Faction faction, Vector2 position = default, int level = 1)
        {
            return Node("Magnetar", faction, 10, position, level).GetComponent<MagnetarTurret>();
        }

        Proton Fly(Faction faction, int charge, Vector2 at)
        {
            var go = new GameObject("Proton");
            spawned.Add(go);
            var proton = go.AddComponent<Proton>();
            proton.Launch(at, far, faction, charge, Balance);
            protons.Register(proton);
            return proton;
        }

        static void Run(MagnetarTurret turret, int ticks)
        {
            for (int i = 0; i < ticks; i++)
                turret.Tick(Tick);
        }

        // --- Disparo ---

        [Test]
        public void ShootsAHostileProtonInsideItsRadius()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            Proton proton = Fly(Faction.Player, 10, new Vector2(1f, 0f));
            Proton shotAt = null;
            turret.OnShot += (m, p) => shotAt = p;

            turret.Tick(Tick);

            Assert.AreSame(proton, shotAt);
            Assert.AreEqual(10 - Balance.MagnetarShotDamage, proton.Charge, "6.3: −2 de Carga por disparo.");
            Assert.IsTrue(proton.InFlight);
        }

        [Test]
        public void IgnoresProtonsOutsideItsRadius()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            Proton proton = Fly(Faction.Player, 10, new Vector2(1.6f, 0f));

            Run(turret, 50);

            Assert.AreEqual(10, proton.Charge, "Nivel 1: radio 1.5 u.");
        }

        [Test]
        public void TheEdgeOfTheRadiusCounts()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            Proton proton = Fly(Faction.Player, 10, new Vector2(0f, 1.5f));

            turret.Tick(Tick);

            Assert.AreEqual(8, proton.Charge);
        }

        [Test]
        public void NeverShootsItsOwnFaction()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            Proton own = Fly(Faction.Enemy, 10, new Vector2(0.5f, 0f));

            Run(turret, 50);

            Assert.AreEqual(10, own.Charge, "4.4: ni siquiera si va hacia un enemigo.");
        }

        [Test]
        public void PlayerMagnetarShootsEnemyProtons()
        {
            MagnetarTurret turret = Magnetar(Faction.Player);
            Proton enemy = Fly(Faction.Enemy, 10, new Vector2(0.5f, 0f));
            Proton own = Fly(Faction.Player, 10, new Vector2(0.2f, 0f));

            turret.Tick(Tick);

            Assert.AreEqual(8, enemy.Charge);
            Assert.AreEqual(10, own.Charge);
        }

        // --- Magnetar neutral (3.2, T2-06) ---

        [TestCase(Faction.Player)]
        [TestCase(Faction.Enemy)]
        public void NeutralMagnetarShootsBothSides(Faction side)
        {
            MagnetarTurret turret = Magnetar(Faction.Neutral);
            Proton proton = Fly(side, 10, new Vector2(0.5f, 0f));

            turret.Tick(Tick);

            Assert.AreEqual(8, proton.Charge);
        }

        [Test]
        public void NeutralMagnetarPicksByDistanceWhateverTheSide()
        {
            MagnetarTurret turret = Magnetar(Faction.Neutral);
            Proton player = Fly(Faction.Player, 20, new Vector2(1f, 0f));
            Proton enemy = Fly(Faction.Enemy, 5, new Vector2(0f, 0.5f));

            turret.Tick(Tick);

            Assert.AreEqual(3, enemy.Charge, "El más cercano, sea del bando que sea.");
            Assert.AreEqual(20, player.Charge);
        }

        [Test]
        public void NeutralMagnetarWearsDownBothSidesInTurn()
        {
            MagnetarTurret turret = Magnetar(Faction.Neutral);
            Proton player = Fly(Faction.Player, 2, new Vector2(0.3f, 0f));
            Proton enemy = Fly(Faction.Enemy, 10, new Vector2(0.9f, 0f));
            int shots = 0;
            turret.OnShot += (m, p) => shots++;

            Run(turret, 100);

            Assert.IsFalse(player.InFlight, "Primero derriba al más cercano.");
            Assert.AreEqual(10 - 2 * (shots - 1), enemy.Charge, "Y sigue con el otro bando.");
            Assert.Greater(shots, 1);
        }

        // 3.2: un Magnetar capturado nunca vuelve a Neutral, y deja de disparar a su nuevo dueño.
        [Test]
        public void CapturedNeutralMagnetarSparesItsNewOwner()
        {
            MagnetarTurret turret = Magnetar(Faction.Neutral);
            NodeController node = turret.GetComponent<NodeController>();
            node.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(Faction.Enemy, 40), node.Snapshot(), Balance.MagnetarResistance));
            Assert.AreEqual(Faction.Enemy, node.Faction);

            Proton enemy = Fly(Faction.Enemy, 10, new Vector2(0.3f, 0f));
            Proton player = Fly(Faction.Player, 10, new Vector2(0.6f, 0f));
            turret.Tick(Tick);

            Assert.AreEqual(10, enemy.Charge);
            Assert.AreEqual(8, player.Charge);
        }

        [TestCase("Star")]
        [TestCase("Asteroid")]
        public void OtherTypesNeverShoot(string type)
        {
            MagnetarTurret turret = Node(type, Faction.Enemy, 10, Vector2.zero).GetComponent<MagnetarTurret>();
            Proton proton = Fly(Faction.Player, 10, new Vector2(0.5f, 0f));

            Run(turret, 50);

            Assert.AreEqual(10, proton.Charge);
        }

        // --- Objetivo ---

        [Test]
        public void ShootsTheProtonNearestToItsCenter()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            Proton outer = Fly(Faction.Player, 30, new Vector2(1.2f, 0f));
            Proton inner = Fly(Faction.Player, 5, new Vector2(0f, -0.4f));

            turret.Tick(Tick);

            Assert.AreEqual(3, inner.Charge);
            Assert.AreEqual(30, outer.Charge);
        }

        [Test]
        public void TieOnDistanceGoesToTheHigherCharge()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            Proton small = Fly(Faction.Player, 5, new Vector2(1f, 0f));
            Proton big = Fly(Faction.Player, 9, new Vector2(-1f, 0f));

            turret.Tick(Tick);

            Assert.AreEqual(7, big.Charge);
            Assert.AreEqual(5, small.Charge);
        }

        [Test]
        public void FullTieGoesToTheFirstLaunched()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            Proton first = Fly(Faction.Player, 6, new Vector2(1f, 0f));
            Proton second = Fly(Faction.Player, 6, new Vector2(0f, 1f));

            turret.Tick(Tick);

            Assert.AreEqual(4, first.Charge);
            Assert.AreEqual(6, second.Charge);
        }

        // --- Derribo (6.3, 17 caso 6) ---

        [Test]
        public void ReachingZeroShootsTheProtonDown()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            Proton proton = Fly(Faction.Player, 2, new Vector2(0.5f, 0f));
            bool shotDown = false;
            proton.OnShotDown += p => shotDown = true;
            bool impacted = false;
            proton.OnImpact += (p, n, o) => impacted = true;

            turret.Tick(Tick);

            Assert.IsTrue(shotDown);
            Assert.IsFalse(proton.InFlight);
            Assert.IsFalse(impacted, "No impacta: la Energía se pierde.");
            Assert.AreEqual(0, protons.Count, "Sale del registro.");
            Assert.AreEqual(0, far.Energy, "El destino no recibe nada.");
        }

        [Test]
        public void ChargeOfOneIsShotDownToo()
        {
            Proton proton = Fly(Faction.Player, 1, new Vector2(0.5f, 0f));
            Assert.IsTrue(proton.TakeShot(2));
            Assert.AreEqual(0, proton.Charge);
        }

        [Test]
        public void OneShotPerReloadEvenAfterADownedProton()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            Fly(Faction.Player, 2, new Vector2(0.2f, 0f));
            Proton next = Fly(Faction.Player, 10, new Vector2(0.8f, 0f));

            turret.Tick(Tick);
            Assert.AreEqual(10, next.Charge, "Un disparo por vez: el segundo espera a la recarga.");
            Assert.IsFalse(turret.IsReady);
        }

        // --- Cadencia ---

        // Disparando sin pausa, el primer disparo sale en el primer tick y los siguientes cada
        // 1/cadencia, con el sobrante arrastrado en microsegundos enteros.
        [TestCase(1, 500)]
        [TestCase(5, 500)]
        [TestCase(3, 137)]
        public void ContinuousFireKeepsTheExactRate(int level, int ticks)
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy, Vector2.zero, level);
            Proton proton = Fly(Faction.Player, 100, new Vector2(0.5f, 0f));
            int shots = 0;
            turret.OnShot += (m, p) => shots++;

            Run(turret, ticks);

            long interval = (long)System.Math.Round(1_000_000d / Definition("Magnetar").GetLevel(level).FireRate);
            long elapsedAfterFirst = (ticks - 1) * 20_000L;
            Assert.AreEqual(1 + elapsedAfterFirst / interval, shots);
            Assert.AreEqual(100 - 2 * shots, proton.Charge);
        }

        [Test]
        public void IdleTimeDoesNotStoreShots()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            Run(turret, 500);
            Assert.IsTrue(turret.IsReady);

            Proton a = Fly(Faction.Player, 10, new Vector2(0.3f, 0f));
            Proton b = Fly(Faction.Player, 10, new Vector2(0.6f, 0f));
            turret.Tick(Tick);

            Assert.AreEqual(8, a.Charge);
            Assert.AreEqual(10, b.Charge, "Diez segundos en reposo no dan diez disparos de golpe.");
        }

        [Test]
        public void SeveralMagnetarsShootIndependently()
        {
            MagnetarTurret left = Magnetar(Faction.Enemy, new Vector2(-1f, 0f));
            MagnetarTurret right = Magnetar(Faction.Enemy, new Vector2(1f, 0f));
            Proton proton = Fly(Faction.Player, 10, Vector2.zero);

            left.Tick(Tick);
            right.Tick(Tick);

            Assert.AreEqual(6, proton.Charge, "6.3: independientes y acumulativos.");
        }

        // --- Nivel y dueño ---

        [Test]
        public void UpgradeWidensTheRadius()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            NodeController node = turret.GetComponent<NodeController>();
            Proton proton = Fly(Faction.Player, 10, new Vector2(1.7f, 0f));
            Assert.IsNull(turret.FindTarget());

            node.AddEnergy(20);
            node.TryUpgrade(Faction.Enemy);
            turret.Tick(Tick);

            Assert.AreEqual(8, proton.Charge, "Nivel 2: radio 1.75 u.");
        }

        [Test]
        public void CapturedMagnetarChangesSides()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            NodeController node = turret.GetComponent<NodeController>();
            node.ApplyImpact(ImpactResolver.Resolve(new ProtonSnapshot(Faction.Player, 40), node.Snapshot(), Balance.MagnetarResistance));
            Assert.AreEqual(Faction.Player, node.Faction);

            Proton player = Fly(Faction.Player, 10, new Vector2(0.3f, 0f));
            Proton enemy = Fly(Faction.Enemy, 10, new Vector2(0.6f, 0f));
            turret.Tick(Tick);

            Assert.AreEqual(10, player.Charge);
            Assert.AreEqual(8, enemy.Charge);
        }

        [Test]
        public void IgnitedMagnetarStartsShooting()
        {
            NodeController asteroid = Node("Asteroid", Faction.Enemy, 10, Vector2.zero);
            Proton proton = Fly(Faction.Player, 10, new Vector2(0.5f, 0f));

            asteroid.ChangeType(Definition("Magnetar"), 1);
            asteroid.GetComponent<MagnetarTurret>().Tick(Tick);

            Assert.AreEqual(8, proton.Charge);
        }

        // --- Vista provisional ---

        [TestCase(1, 1.5f)]
        [TestCase(5, 2.5f)]
        public void RingMeasuresTheExactRadius(int level, float radius)
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy, Vector2.zero, level);
            var view = turret.gameObject.AddComponent<ProvisionalMagnetarView>();
            view.Refresh();

            Assert.IsTrue(view.Ring.enabled);
            Assert.AreEqual(radius * 2f, view.Ring.sprite.bounds.size.x, 1e-5f, "El borde exterior del anillo es el radio (A.4.1).");
        }

        [Test]
        public void RingAppearsOnIgnitionAndGrowsWithTheLevel()
        {
            NodeController node = Node("Asteroid", Faction.Player, 10, Vector2.zero);
            var view = node.gameObject.AddComponent<ProvisionalMagnetarView>();
            view.Subscribe();
            view.Refresh();
            Assert.IsFalse(view.Ring.enabled);

            node.ChangeType(Definition("Magnetar"), 1);
            Assert.IsTrue(view.Ring.enabled);
            float level1 = view.Ring.sprite.bounds.size.x;

            node.AddEnergy(20);
            node.TryUpgrade(Faction.Player);
            Assert.Greater(view.Ring.sprite.bounds.size.x, level1);
            view.Unsubscribe();
        }

        [Test]
        public void EachShotDrawsATrace()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            var view = turret.gameObject.AddComponent<ProvisionalMagnetarView>();
            view.Subscribe();
            Fly(Faction.Player, 10, new Vector2(1f, 0f));

            turret.Tick(Tick);

            Assert.IsTrue(view.Shot.enabled);
            Assert.AreEqual(new Vector3(0.5f, 0f, 0f), view.Shot.transform.position, "Del centro al protón.");
            view.Unsubscribe();
        }

        // --- En vuelo: la trayectoria entera ---

        // Un protón que cruza el radio de un Magnetar de nivel 1 de lado a lado, en el orden de
        // 15.10: torreta (0), protón (+10), impactos al final.
        [Test]
        public void ProtonCrossingTheRadiusArrivesWithLessCharge()
        {
            MagnetarTurret turret = Magnetar(Faction.Enemy);
            NodeController target = Node("Star", Faction.Enemy, 0, new Vector2(5f, 0f));
            var go = new GameObject("Proton");
            spawned.Add(go);
            var proton = go.AddComponent<Proton>();
            proton.Launch(new Vector2(-5f, 0f), target, Faction.Player, 20, Balance);
            protons.Register(proton);
            int shots = 0;
            turret.OnShot += (m, p) => shots++;
            int arrived = -1;
            proton.OnImpact += (p, n, o) => arrived = p.Charge;

            for (int i = 0; i < 500 && proton.InFlight; i++)
            {
                turret.Tick(Tick);
                proton.Tick(Tick);
                protons.ResolveArrivals();
            }

            Assert.AreEqual(2, shots, "3 u de radio a 2.5 u/s son 1.2 s: el disparo de entrada y otro a los 0.75 s.");
            Assert.AreEqual(16, arrived);
            Assert.AreEqual(Faction.Player, target.Faction, "16 de Carga contra 0 de Energía: lo captura.");
            Assert.AreEqual(16, target.Energy);
        }
    }
}
