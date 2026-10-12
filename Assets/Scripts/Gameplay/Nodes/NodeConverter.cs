using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Gameplay
{
    // Reconversión (8.1, 15.5, T2-07): un nodo propio encendido pasa a nivel 1 de otro tipo tras
    // 3 s. Las reglas son de ConversionRules; aquí se cobra, se lleva el reloj y se cambia el tipo.
    //
    // - El coste es el del tipo de origen (16.4, NodeDefinition.ConversionCost) y se paga al iniciar.
    // - Durante los 3 s el nodo sigue siendo del tipo de origen a todos los efectos (18 #20) y
    //   lleva la bandera Reconvirtiendo: no genera, no dispara, no envía y no mejora.
    // - Congelado pausa el reloj y descongelar lo reanuda (8.1, 17 caso 8).
    // - Si lo capturan, la reconversión se cancela, el nodo queda como estaba y el coste se
    //   pierde (18 #21). Un Decaimiento la cancela igual (desviación 203).
    // - Al terminar: nivel 1 del tipo nuevo, con la Energía de ese momento recortada al Tope
    //   nuevo. Lo hace NodeController.ChangeType, que es la misma mitad mecánica del encendido.
    //
    // El reloj corre en el tick fijo de 50 Hz (15.10) en microsegundos enteros, como los demás
    // temporizadores de la partida.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class NodeConverter : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        [SerializeField] BalanceConfig balance;

        [Tooltip("Una NodeDefinition por tipo al que se puede reconvertir: Estrella, Púlsar y Magnetar.")]
        [SerializeField] NodeDefinition[] targets = Array.Empty<NodeDefinition>();

        NodeController node;
        NodeDefinition pending;
        long elapsedMicros;
        bool listening;

        public bool IsConverting => pending != null;

        // Tipo al que se está reconvirtiendo. Solo tiene sentido con IsConverting.
        public NodeType TargetType => pending != null ? pending.Type : Node.Type;

        // Segundos que llevan corridos de los 3, para la presentación y los tests.
        public float Elapsed => (float)(elapsedMicros / MicrosPerSecond);

        // (origen, destino) al iniciar y al terminar. Cancelled: capturado a mitad.
        public event Action<NodeType, NodeType> OnConversionStarted;
        public event Action<NodeType, NodeType> OnConversionFinished;
        public event Action<NodeType, NodeType> OnConversionCancelled;

        // Validación sin efectos. La usan el comando y el panel de clic derecho (13.3), que pinta
        // cada destino deshabilitado con el coste en rojo o como no disponible.
        //
        // Como en NodeIgnition (18 #29), un tipo sin NodeDefinition en targets es Unavailable: el
        // Púlsar hasta T3-02. Va por delante de la Energía porque reunirla no lo arregla.
        public ActionCheck Check(Faction actor, NodeType targetType)
        {
            int cost = Node.IsInitialized ? Node.Definition.ConversionCost : 0;
            ActionCheck rules = ConversionRules.CheckConversion(Node.Snapshot(), actor, targetType, cost);
            bool reachable = rules.IsAllowed || rules.Block == ActionBlock.InsufficientEnergy;
            if (reachable && !Offers(targetType))
                return new ActionCheck(ActionBlock.Unavailable, rules.Cost);
            return rules;
        }

        public bool Offers(NodeType type) => FindDefinition(type) != null;

        public ActionCheck TryConvert(Faction actor, NodeType targetType)
        {
            ActionCheck check = Check(actor, targetType);
            if (!check.IsAllowed)
                return check;

            // El estado propio va antes que la bandera: poner la bandera avisa a quien escucha (el
            // panel se repinta en ese momento), y en ese aviso IsConverting y TargetType ya tienen
            // que decir la verdad.
            pending = FindDefinition(targetType);
            elapsedMicros = 0;
            if (!Node.SetStateFlag(NodeStateFlags.Converting, true))
            {
                pending = null;
                throw new InvalidOperationException($"{name}: 14.6 no admite Reconvirtiendo, y ConversionRules lo dejó pasar.");
            }

            Node.SubtractEnergy(check.Cost);
            Listen(true);
            OnConversionStarted?.Invoke(Node.Type, targetType);
            return check;
        }

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            if (!IsConverting)
                return;

            // 8.1: congelado, el reloj se detiene donde esté y se reanuda al descongelar.
            if (Node.HasState(NodeStateFlags.Frozen))
                return;

            elapsedMicros += ToMicros(deltaTime);
            if (elapsedMicros >= ToMicros(Balance.ConversionDuration))
                Finish();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config, IEnumerable<NodeDefinition> conversionTargets)
        {
            balance = config;
            targets = conversionTargets != null ? new List<NodeDefinition>(conversionTargets).ToArray() : Array.Empty<NodeDefinition>();
        }

        void Finish()
        {
            NodeType from = Node.Type;
            NodeDefinition definition = pending;
            Stop();
            Node.ChangeType(definition, 1);
            OnConversionFinished?.Invoke(from, definition.Type);
        }

        // 8.1: capturado durante la reconversión, se cancela y el nodo queda como estaba. El tipo
        // no llegó a cambiar, así que no hay nada que deshacer; el coste ya pagado se pierde.
        void HandleFactionChanged(Faction previous, Faction current)
        {
            Interrupt();
        }

        // Cancela la reconversión en curso sin devolver el coste: el nodo queda como estaba. Lo
        // provocan la captura y, desde T3-05, un Decaimiento, porque 14.6 no deja convivir
        // Infectado con Reconvirtiendo (desviación 203). Sin reconversión en curso no hace nada.
        public void Interrupt()
        {
            if (!IsConverting)
                return;
            NodeType to = pending.Type;
            Stop();
            OnConversionCancelled?.Invoke(Node.Type, to);
        }

        void Stop()
        {
            pending = null;
            elapsedMicros = 0;
            Listen(false);
            Node.SetStateFlag(NodeStateFlags.Converting, false);
        }

        // Se escucha solo mientras dura la reconversión, y no en OnEnable, que no se ejecuta en
        // los tests de EditMode.
        void Listen(bool on)
        {
            if (on == listening)
                return;
            if (on)
                Node.OnFactionChanged += HandleFactionChanged;
            else
                Node.OnFactionChanged -= HandleFactionChanged;
            listening = on;
        }

        void OnDestroy()
        {
            if (listening && node != null)
                node.OnFactionChanged -= HandleFactionChanged;
            listening = false;
        }

        NodeDefinition FindDefinition(NodeType type)
        {
            foreach (NodeDefinition definition in targets)
            {
                if (definition != null && definition.Type == type)
                    return definition;
            }
            return null;
        }

        static long ToMicros(float seconds) => (long)Math.Round(seconds * MicrosPerSecond);

        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("NodeConverter necesita BalanceConfig.");

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
    }
}
