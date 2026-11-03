using System.Collections.Generic;
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
    // T6-14: los sonidos de los poderes (NOVA_Sonido.md, S.5.1 y S.6). El ID que corresponde a cada evento y tipo,
    // el panorama y el límite de voces, con SoundRules y VoicePool, que no necesitan AudioSource; y AudioDirector
    // enganchado a un PowerBeamSystem de verdad, mirando qué pide.
    public class SoundTests
    {
        const float Tick = 0.02f;
        const int TicksToLand = 13; // el haz dura 0.25 s y termina en el tick 13 (PowerBeamVisualTests)
        const string BankPath = "Assets/Data/Audio/SoundBank.asset";

        static readonly PowerType[] Powers = { PowerType.AbsoluteZero, PowerType.Decay, PowerType.Wormhole };

        readonly List<Object> spawned = new List<Object>();

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

        // --- IDs (S.3.1, S.5.1) ---

        [TestCase(PowerType.AbsoluteZero, "SND-WLD-BEAM-ZER", "SND-PWR-ZER")]
        [TestCase(PowerType.Decay, "SND-WLD-BEAM-DEC", "SND-PWR-DEC")]
        [TestCase(PowerType.Wormhole, "SND-WLD-BEAM-WRM", "SND-PWR-WRM")]
        public void EachPowerHasItsBeamAndImpactIds(PowerType type, string beam, string impact)
        {
            Assert.AreEqual(beam, SoundRules.BeamId(type));
            Assert.AreEqual(impact, SoundRules.ImpactId(type));
        }

        // --- Panorama (S.6) ---

        [TestCase(0f, 0f)]
        [TestCase(7.5f, 0.6f)]    // borde derecho
        [TestCase(-7.5f, -0.6f)]  // borde izquierdo
        [TestCase(3.75f, 0.3f)]
        [TestCase(20f, 0.6f)]     // fuera del tablero: se queda en el borde
        [TestCase(-20f, -0.6f)]
        public void PanFollowsXAcrossTheBoard(float x, float expected)
        {
            Assert.AreEqual(expected, SoundRules.Pan(x, 15f, 0.6f), 1e-5f);
        }

        [Test]
        public void NothingSoundsInOneEarOnly()
        {
            var bank = ScriptableObject.CreateInstance<SoundBank>();
            spawned.Add(bank);
            Assert.Less(bank.MaxPan, 1f, "S.6: suavizado para que nada suene solo en un oído");
            Assert.Greater(bank.MaxPan, 0f);
            Assert.AreEqual(15f, bank.BoardWidth, 1e-5f, "El tablero mide 15 u (2)");
            Assert.AreEqual(2, bank.MaxVoicesPerId, "S.6: como mucho dos instancias del mismo ID");
        }

        // --- Voces (S.6) ---

        [Test]
        public void AThirdVoiceOfTheSameIdCutsTheOldest()
        {
            var pool = new VoicePool(8, 2);
            var busy = new HashSet<int>();
            bool IsBusy(int v) => busy.Contains(v);

            int first = pool.Acquire("A", IsBusy); busy.Add(first);
            int second = pool.Acquire("A", IsBusy); busy.Add(second);
            int third = pool.Acquire("A", IsBusy);

            Assert.AreNotEqual(first, second);
            Assert.AreEqual(first, third, "La tercera corta a la más antigua");
            int fourth = pool.Acquire("A", IsBusy);
            Assert.AreEqual(second, fourth, "Y la cuarta, a la que ahora es la más antigua");
        }

        [Test]
        public void OtherIdsDoNotCountTowardsTheLimit()
        {
            var pool = new VoicePool(8, 2);
            var busy = new HashSet<int>();
            bool IsBusy(int v) => busy.Contains(v);

            var used = new HashSet<int>();
            foreach (string id in new[] { "A", "A", "B", "B", "C" })
            {
                int voice = pool.Acquire(id, IsBusy);
                busy.Add(voice);
                Assert.IsTrue(used.Add(voice), $"{id}: cada sonido en su propia voz");
            }
        }

        [Test]
        public void AFinishedVoiceIsFreeAgain()
        {
            var pool = new VoicePool(2, 2);
            var busy = new HashSet<int>();
            bool IsBusy(int v) => busy.Contains(v);

            int a = pool.Acquire("A", IsBusy); busy.Add(a);
            int b = pool.Acquire("B", IsBusy); busy.Add(b);
            busy.Remove(a); // A terminó

            Assert.AreEqual(a, pool.Acquire("C", IsBusy));
            Assert.AreEqual("C", pool.IdAt(a));
            Assert.AreEqual("B", pool.IdAt(b));
        }

        [Test]
        public void AFullPoolCutsTheOldestOfAll()
        {
            var pool = new VoicePool(3, 2);
            var busy = new HashSet<int>();
            bool IsBusy(int v) => busy.Contains(v);

            int oldest = pool.Acquire("A", IsBusy); busy.Add(oldest);
            busy.Add(pool.Acquire("B", IsBusy));
            busy.Add(pool.Acquire("C", IsBusy));

            Assert.AreEqual(oldest, pool.Acquire("D", IsBusy));
        }

        // --- AudioDirector con PowerBeamSystem ---

        static NodeDefinition Definition(string name) =>
            AssetDatabase.LoadAssetAtPath<NodeDefinition>($"Assets/Data/Nodes/{name}.asset");

        static PowerDefinition PowerAsset(PowerType type) =>
            AssetDatabase.LoadAssetAtPath<PowerDefinition>($"Assets/Data/Powers/{type}.asset");

        static BalanceConfig Balance =>
            AssetDatabase.LoadAssetAtPath<BalanceConfig>("Assets/Data/BalanceConfig.asset");

        NodeController Node(string type, Faction faction, Vector2 position)
        {
            var go = new GameObject(type);
            spawned.Add(go);
            go.transform.position = position;
            var node = go.AddComponent<NodeController>();
            go.AddComponent<NodeFreeze>().Configure(Balance);
            go.AddComponent<NodeDecay>().Configure(Balance);
            node.Initialize(Definition(type), faction, 40, 1);
            return node;
        }

        SoundBank TestBank()
        {
            var bank = ScriptableObject.CreateInstance<SoundBank>();
            spawned.Add(bank);
            var entries = new List<SoundBank.Entry>();
            foreach (PowerType type in Powers)
            {
                foreach (string id in new[] { SoundRules.BeamId(type), SoundRules.ImpactId(type) })
                {
                    AudioClip clip = AudioClip.Create(id, 4800, 1, 48000, false);
                    spawned.Add(clip);
                    entries.Add(new SoundBank.Entry(id, clip, null));
                }
            }
            bank.Configure(entries.ToArray());
            return bank;
        }

        (PowerBeamSystem beams, List<(string id, float pan)> played) Rig()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            var beams = go.AddComponent<PowerBeamSystem>();
            var director = go.AddComponent<AudioDirector>();
            director.Configure(TestBank(), beams);
            director.Subscribe(); // EditMode no ejecuta OnEnable
            var played = new List<(string id, float pan)>();
            director.OnPlayed += (id, pan) => played.Add((id, pan));
            return (beams, played);
        }

        [Test]
        public void LaunchSoundsTheBeamFromThePulsarAndLandingTheImpactAtTheTarget()
        {
            foreach (PowerType type in Powers)
            {
                var (beams, played) = Rig();
                NodeController pulsar = Node("Pulsar", Faction.Player, new Vector2(-6f, 0f));
                NodeController target = Node("Star", Faction.Enemy, new Vector2(6f, 1f));

                beams.Launch(pulsar, target, Faction.Player, PowerAsset(type));
                Assert.AreEqual(1, played.Count, $"{type}: al lanzar suena el haz");
                Assert.AreEqual(SoundRules.BeamId(type), played[0].id);
                Assert.Less(played[0].pan, 0f, "El haz suena del lado del Púlsar, a la izquierda");

                for (int i = 0; i < TicksToLand; i++)
                    beams.Tick(Tick);
                Assert.AreEqual(2, played.Count, $"{type}: al caer suena el impacto");
                Assert.AreEqual(SoundRules.ImpactId(type), played[1].id);
                Assert.Greater(played[1].pan, 0f, "El impacto suena del lado del objetivo, a la derecha");
            }
        }

        [Test]
        public void TheSameSoundsWhicheverFactionLaunches()
        {
            // S.4 #1: el sonido va por poder; quién lanza solo se oye por el lado.
            var (beams, played) = Rig();
            NodeController enemyPulsar = Node("Pulsar", Faction.Enemy, new Vector2(6f, 0f));
            NodeController target = Node("Star", Faction.Player, new Vector2(-6f, 0f));

            beams.Launch(enemyPulsar, target, Faction.Enemy, PowerAsset(PowerType.Decay));

            Assert.AreEqual(SoundRules.BeamId(PowerType.Decay), played[0].id);
            Assert.Greater(played[0].pan, 0f, "Un poder enemigo que sale de la derecha se oye a la derecha");
        }

        [Test]
        public void ADissipatedBeamHasNoImpactSound()
        {
            var (beams, played) = Rig();
            NodeController pulsar = Node("Pulsar", Faction.Player, new Vector2(-4f, 0f));
            NodeController target = Node("Star", Faction.Enemy, new Vector2(4f, 0f));
            PowerBeam beam = beams.Launch(pulsar, target, Faction.Player, PowerAsset(PowerType.Wormhole));
            var dissipated = new List<PowerBeam>();
            beams.OnDissipated += dissipated.Add;

            // El objetivo pasa a ser del lanzador antes de que termine el haz (17 caso 27).
            beam.Target.ApplyImpact(ImpactResolver.Resolve(
                new ProtonSnapshot(Faction.Player, beam.Target.Energy * 2 + 1), beam.Target.Snapshot(), Balance.MagnetarResistance));
            for (int i = 0; i < TicksToLand; i++)
                beams.Tick(Tick);

            Assert.AreEqual(1, dissipated.Count);
            Assert.AreEqual(1, played.Count, "S.4 #3: suena el haz entero y ningún impacto");
            Assert.AreEqual(SoundRules.BeamId(PowerType.Wormhole), played[0].id);
        }

        [Test]
        public void TheDirectorFreezesWithTheGame()
        {
            var go = new GameObject("Systems");
            spawned.Add(go);
            var director = go.AddComponent<AudioDirector>();
            director.Configure(TestBank(), go.AddComponent<PowerBeamSystem>());

            director.SetFrozen(true);
            Assert.IsTrue(director.IsFrozen, "Pausa o fin de nivel: los sonidos se quedan quietos");
            director.SetFrozen(false);
            Assert.IsFalse(director.IsFrozen, "Al reanudar siguen donde iban");
        }

        // --- El asset ---

        [Test]
        public void SoundBankHasTheSixPowerSounds()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            Assert.IsNotNull(bank, $"Falta {BankPath}: Nova → Audio → Importar audio y crear el mezclador");

            foreach (PowerType type in Powers)
            {
                foreach (string id in new[] { SoundRules.BeamId(type), SoundRules.ImpactId(type) })
                {
                    Assert.IsTrue(bank.TryGet(id, out SoundBank.Entry entry), $"{id}: falta en SoundBank o sin clip");
                    AudioClip clip = entry.Clip;
                    Assert.AreEqual(48000, clip.frequency, id);
                    Assert.AreEqual(1, clip.channels, $"{id}: los SFX van en mono (S.2.1)");
                    Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, clip.loadType, $"{id}: Decompress On Load (S.2.1)");

                    var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                    Assert.AreEqual(AudioCompressionFormat.ADPCM, importer.defaultSampleSettings.compressionFormat, $"{id}: ADPCM (S.2.1)");
                    var serialized = new SerializedObject(importer);
                    SerializedProperty normalize = serialized.FindProperty("m_Normalize") ?? serialized.FindProperty("normalize");
                    Assert.IsNotNull(normalize);
                    Assert.IsFalse(normalize.boolValue, $"{id}: sin normalizar");

                    Assert.IsNotNull(entry.Output, $"{id}: sale por el bus SFX/World (S.2.3)");
                    Assert.AreEqual("World", entry.Output.name, id);
                }
            }
        }
    }
}
