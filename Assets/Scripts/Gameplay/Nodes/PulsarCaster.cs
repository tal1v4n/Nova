using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Gameplay
{
    // Estado de la carga de un Púlsar (7.1).
    public enum PowerChargeState
    {
        Idle = 0,     // Sin poder
        Charging = 1, // Cargando: la Energía baja mientras la barra sube
        Ready = 2,    // Cargado al 100 %, a la espera de lanzarlo (T3-03)
    }

    // Carga de poderes del Púlsar (7.1, 15.5, T3-02). Las reglas son de PowerRules; aquí se lleva
    // el reloj, se cobra la Energía y se devuelve al cancelar. El lanzamiento llega con T3-03.
    //
    // - Un poder a la vez. La Energía del Púlsar baja a la vez que sube la barra: la carga cobra
    //   cada punto cuando le toca, al ritmo coste / tiempo de carga (16.3).
    // - Con la Energía a 0, la carga se pausa donde esté y sigue sola al recibir Energía.
    //   Congelado, también se pausa (7.3, 17 caso 9). Lo invertido no se pierde.
    // - Cargada al 100 %, se mantiene hasta que se lance o se cancele.
    // - Cancelar devuelve la mitad de lo invertido, hacia abajo y sin pasar del Tope (7.1, 16.1).
    // - Capturado a mitad de carga, la carga vuelve a 0 y lo invertido se pierde; con el poder ya
    //   cargado, el nuevo dueño lo hereda intacto (7.5, 17 caso 10).
    //
    // Como StarGenerator y MagnetarTurret, va en todo nodo que pueda llegar a ser Púlsar y solo
    // actúa cuando lo es (desviación 143). El reloj corre en el tick fijo (15.10) en microsegundos.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class PulsarCaster : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        [SerializeField] BalanceConfig balance;

        [Tooltip("Un PowerDefinition por poder que este Púlsar puede cargar.")]
        [SerializeField] PowerDefinition[] powers = Array.Empty<PowerDefinition>();

        NodeController node;
        PowerDefinition power;
        PowerChargeState state;
        int invested;
        long progressMicros;
        bool stalled;
        bool listening;

        public PowerChargeState State => state;
        public bool HasPowerCharge => state != PowerChargeState.Idle;

        // Poder que carga o tiene cargado; null en reposo.
        public PowerDefinition Power => power;

        // Energía ya invertida en la carga.
        public int Invested => invested;

        // La barra de 7.1: energíaInvertida / costeTotal, de 0 a 1.
        public float Progress => power != null ? (float)invested / power.Cost : 0f;

        // Cargando pero parada: sin Energía, o congelada.
        public bool IsPaused => state == PowerChargeState.Charging && (stalled || Node.HasState(NodeStateFlags.Frozen));

        // (anterior, nuevo): empieza, termina, se cancela o se pierde. No salta con cada punto
        // invertido; para la barra está OnProgress.
        public event Action<PowerChargeState, PowerChargeState> OnStateChanged;

        // Cada vez que cambia lo invertido o la carga se pausa o se reanuda.
        public event Action OnProgress;

        // (poder, Energía devuelta) al cancelar a mano.
        public event Action<PowerType, int> OnCancelled;

        // (poder) cuando una captura se lleva la carga a medias (7.5).
        public event Action<PowerType> OnLost;

        // Validación sin efectos. Como en NodeConverter (18 #29), un poder sin PowerDefinition en
        // powers es Unavailable cuando lo demás lo permitiría: subir de nivel no lo arregla.
        public ActionCheck CheckCharge(Faction actor, PowerType type)
        {
            PowerDefinition definition = FindDefinition(type);
            if (definition != null)
                return PowerRules.CheckCharge(Node.Snapshot(), actor, definition.RequiredLevel, definition.Cost);

            ActionCheck rules = PowerRules.CheckCharge(Node.Snapshot(), actor, 1, 1);
            return rules.IsAllowed ? new ActionCheck(ActionBlock.Unavailable, 0) : new ActionCheck(rules.Block, 0);
        }

        public bool Offers(PowerType type) => FindDefinition(type) != null;

        public ActionCheck TryCharge(Faction actor, PowerType type)
        {
            ActionCheck check = CheckCharge(actor, type);
            if (!check.IsAllowed)
                return check;

            power = FindDefinition(type);
            invested = 0;
            progressMicros = 0;
            stalled = false;
            Listen(true);
            SetState(PowerChargeState.Charging);
            return check;
        }

        public ActionCheck CheckCancel(Faction actor) => PowerRules.CheckCancel(Node.Snapshot(), actor);

        // Cancela la carga o el poder cargado y devuelve la parte de 16.1. El Tope lo respeta
        // AddEnergy, como cualquier Energía que entra (5.3).
        public ActionCheck TryCancel(Faction actor)
        {
            ActionCheck check = CheckCancel(actor);
            if (!check.IsAllowed)
                return check;

            PowerType type = power.Type;
            int refund = PowerRules.CancelRefund(invested, Balance.ChargeCancelRefund);
            Clear();
            if (refund > 0)
                Node.AddEnergy(refund);
            OnCancelled?.Invoke(type, refund);
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

            if (state != PowerChargeState.Charging || Node.Type != NodeType.Pulsar)
                return;

            // 7.3: congelado, la carga se queda donde esté.
            if (Node.HasState(NodeStateFlags.Frozen))
                return;

            bool wasStalled = stalled;

            // 7.1: con la Energía agotada, la carga se para donde esté y el reloj no corre.
            if (Node.Energy == 0)
            {
                stalled = true;
                if (!wasStalled)
                    OnProgress?.Invoke();
                return;
            }

            long chargeMicros = ToMicros(power.ChargeTime);
            long next = Math.Min(progressMicros + ToMicros(deltaTime), chargeMicros);
            int due = PowerRules.InvestedAt(power.Cost, next, chargeMicros);
            int pay = Math.Min(due - invested, Node.Energy);

            if (pay > 0)
            {
                invested += pay;
                Node.SubtractEnergy(pay);
            }

            // Si en un mismo tick tocaba más de lo que quedaba, el reloj se queda en el instante
            // que corresponde a lo invertido: al recibir Energía sigue desde ahí (7.1).
            stalled = invested < due;
            progressMicros = stalled ? PowerRules.ProgressFor(invested, power.Cost, chargeMicros) : next;

            if (pay > 0 || stalled != wasStalled)
                OnProgress?.Invoke();

            if (invested >= power.Cost)
                SetState(PowerChargeState.Ready);
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config, IEnumerable<PowerDefinition> available)
        {
            balance = config;
            powers = available != null ? new List<PowerDefinition>(available).ToArray() : Array.Empty<PowerDefinition>();
        }

        // 7.5: capturado a mitad de carga, la carga se pierde; con el poder listo, lo hereda el
        // nuevo dueño tal cual.
        void HandleFactionChanged(Faction previous, Faction current)
        {
            if (state != PowerChargeState.Charging)
                return;
            PowerType type = power.Type;
            Clear();
            OnLost?.Invoke(type);
        }

        void Clear()
        {
            power = null;
            invested = 0;
            progressMicros = 0;
            stalled = false;
            Listen(false);
            SetState(PowerChargeState.Idle);
        }

        void SetState(PowerChargeState value)
        {
            if (value == state)
                return;
            PowerChargeState previous = state;
            state = value;
            OnStateChanged?.Invoke(previous, value);
        }

        // Se escucha solo mientras hay carga, y no en OnEnable, que no se ejecuta en los tests de
        // EditMode.
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

        PowerDefinition FindDefinition(PowerType type)
        {
            foreach (PowerDefinition definition in powers)
            {
                if (definition != null && definition.Type == type)
                    return definition;
            }
            return null;
        }

        static long ToMicros(float seconds) => (long)Math.Round(seconds * MicrosPerSecond);

        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("PulsarCaster necesita BalanceConfig.");

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
    }
}
