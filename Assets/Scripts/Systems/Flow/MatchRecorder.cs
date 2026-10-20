using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Systems
{
    // Datos de la partida en curso para las pantallas de fin (13.11, 15.6, 18 #40, T5-05): tiempo de
    // juego, pico de Energía de la barra de poder del Jugador y cuándo, capturas hechas y perdidas,
    // nodos ganados por la cascada y una muestra por segundo de la curva de poder. Solo en memoria:
    // se pierde al salir del nivel, porque la persistencia es solo el nivel máximo (1.2).
    //
    // Las barras son las de 13.6: la Energía de todos los nodos de cada bando, sin la Materia Inerte
    // de los neutrales. Va a +20, con WinConditionChecker: mide el tablero después de los nodos.
    // Deja de medir al terminar el nivel.
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    public sealed class MatchRecorder : MonoBehaviour
    {
        const long MicrosPerSecond = 1_000_000;

        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance.")]
        [SerializeField] NodeRegistry nodes;

        [Tooltip("Opcional: sin él no se separan las capturas de la cascada ni se para al terminar.")]
        [SerializeField] WinConditionChecker checker;

        readonly List<float> samples = new List<float>();
        readonly List<NodeController> watched = new List<NodeController>();
        readonly Dictionary<NodeController, Action<Faction, Faction>> handlers = new Dictionary<NodeController, Action<Faction, Faction>>();
        bool subscribedToChecker;
        long elapsedMicros;
        long nextSampleMicros;
        long peakMicros;

        public bool IsRecording { get; private set; }

        // Tiempo de juego desde Begin, en segundos.
        public float Elapsed => elapsedMicros / (float)MicrosPerSecond;

        // La proporción del Jugador (PowerCurve.Share), una por segundo de juego desde el segundo 0.
        public IReadOnlyList<float> Samples => samples;

        // La barra del Jugador en su punto más alto, y cuándo.
        public int PeakEnergy { get; private set; }
        public float PeakTime => peakMicros / (float)MicrosPerSecond;

        public int CapturesMade { get; private set; }
        public int CapturesLost { get; private set; }

        // Nodos que la cascada de conquista pasó al Jugador (10.1).
        public int CascadeConquered { get; private set; }

        // La primera muestra tomada con la cascada en marcha, o -1: la curva pinta desde ahí en cian claro.
        public int CascadeSample { get; private set; } = -1;

        void OnEnable()
        {
            SubscribeChecker();
        }

        void OnDisable()
        {
            UnsubscribeChecker();
        }

        void OnDestroy()
        {
            Unwatch();
        }

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        // Empieza a medir el nivel cargado desde cero. Lo llama GameManager al cargarlo.
        public void Begin()
        {
            Unwatch();
            samples.Clear();
            elapsedMicros = 0;
            nextSampleMicros = 0;
            peakMicros = 0;
            PeakEnergy = 0;
            CapturesMade = 0;
            CapturesLost = 0;
            CascadeConquered = 0;
            CascadeSample = -1;

            NodeRegistry registry = Registry;
            if (registry != null)
            {
                foreach (NodeController node in registry.All)
                    Watch(node);
            }
            SubscribeChecker();
            IsRecording = true;
            Measure();
        }

        // Para de medir: los datos se quedan como están.
        public void Stop()
        {
            IsRecording = false;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");
            if (!IsRecording)
                return;

            elapsedMicros += (long)Math.Round(deltaTime * MicrosPerSecond);
            Measure();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(NodeRegistry nodeRegistry = null, WinConditionChecker winChecker = null)
        {
            UnsubscribeChecker();
            nodes = nodeRegistry;
            checker = winChecker;
            SubscribeChecker();
        }

        // Pico de la barra y, en cada segundo entero, una muestra de la curva.
        void Measure()
        {
            NodeRegistry registry = Registry;
            if (registry == null)
                return;

            int player = Bar(registry, Faction.Player);
            if (player > PeakEnergy)
            {
                PeakEnergy = player;
                peakMicros = elapsedMicros;
            }
            while (elapsedMicros >= nextSampleMicros)
            {
                samples.Add(PowerCurve.Share(player, Bar(registry, Faction.Enemy)));
                nextSampleMicros += MicrosPerSecond;
            }
        }

        static int Bar(NodeRegistry registry, Faction faction)
        {
            int sum = 0;
            foreach (NodeController node in registry.GetNodes(faction))
            {
                if (node != null && node.IsInitialized)
                    sum += node.Energy;
            }
            return sum;
        }

        void Watch(NodeController node)
        {
            if (node == null || handlers.ContainsKey(node))
                return;
            Action<Faction, Faction> handler = (previous, current) => Changed(previous, current);
            handlers.Add(node, handler);
            watched.Add(node);
            node.OnFactionChanged += handler;
        }

        void Unwatch()
        {
            foreach (NodeController node in watched)
            {
                if (node != null && handlers.TryGetValue(node, out Action<Faction, Faction> handler))
                    node.OnFactionChanged -= handler;
            }
            watched.Clear();
            handlers.Clear();
        }

        // Lo que pasa al Jugador durante la cascada lo cuenta OnConquered, no es una captura.
        void Changed(Faction previous, Faction current)
        {
            if (!IsRecording || (checker != null && checker.IsCascading))
                return;
            if (current == Faction.Player && previous != Faction.Player)
                CapturesMade++;
            else if (previous == Faction.Player && current != Faction.Player)
                CapturesLost++;
        }

        void Conquered(NodeController node)
        {
            if (!IsRecording)
                return;
            if (CascadeConquered++ == 0)
                CascadeSample = samples.Count;
        }

        void LevelEnded(LevelOutcome outcome)
        {
            if (!IsRecording)
                return;
            Measure();
            Stop();
        }

        void SubscribeChecker()
        {
            if (subscribedToChecker || checker == null)
                return;
            checker.OnConquered += Conquered;
            checker.OnLevelEnded += LevelEnded;
            subscribedToChecker = true;
        }

        void UnsubscribeChecker()
        {
            if (!subscribedToChecker)
                return;
            if (checker != null)
            {
                checker.OnConquered -= Conquered;
                checker.OnLevelEnded -= LevelEnded;
            }
            subscribedToChecker = false;
        }

        NodeRegistry Registry => nodes != null ? nodes : NodeRegistry.Instance;
    }
}
