using System;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Gameplay
{
    // Orquestador del nodo (15.5). No contiene reglas: expone la API pública del nodo y
    // delega en sus componentes y en Core.Rules. Es el único que conoce a los demás componentes.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeIdentity), typeof(NodeOwnership), typeof(NodeEnergy))]
    [RequireComponent(typeof(NodeLevelComponent), typeof(NodeStateMachine))]
    public sealed class NodeController : MonoBehaviour
    {
        NodeIdentity identity;
        NodeOwnership ownership;
        NodeEnergy energy;
        NodeLevelComponent levelComponent;
        NodeStateMachine stateMachine;
        PulsarCaster caster;
        NodeFreeze freezer;
        NodeDecay decay;
        NodeConverter converter;
        bool listening;

        public NodeType Type => Identity.Type;
        public NodeDefinition Definition => Identity.Definition;
        public Faction Faction => Ownership.Faction;
        public int Energy => EnergyComponent.Energy;
        public int EnergyCap => EnergyComponent.EnergyCap;
        public int Level => LevelComponent.Level;
        public NodeLevelData CurrentLevelData => LevelComponent.CurrentLevelData;
        public NodeState State => StateMachine.State;
        public NodeStateFlags StateFlags => StateMachine.Flags;
        public bool IsInitialized => Identity.Definition != null;

        public bool HasState(NodeStateFlags flag) => StateMachine.Has(flag);

        // Reenvío de los eventos de los componentes: quien escucha al nodo no necesita conocerlos.
        public event Action<Faction, Faction> OnFactionChanged
        {
            add => Ownership.OnFactionChanged += value;
            remove => Ownership.OnFactionChanged -= value;
        }

        public event Action<int, int> OnEnergyChanged
        {
            add => EnergyComponent.OnEnergyChanged += value;
            remove => EnergyComponent.OnEnergyChanged -= value;
        }

        public event Action<int, int> OnLevelChanged
        {
            add => LevelComponent.OnLevelChanged += value;
            remove => LevelComponent.OnLevelChanged -= value;
        }

        public event Action<NodeState, NodeState> OnStateChanged
        {
            add => StateMachine.OnStateChanged += value;
            remove => StateMachine.OnStateChanged -= value;
        }

        public event Action<NodeStateFlags, NodeStateFlags> OnStateFlagsChanged
        {
            add => StateMachine.OnFlagsChanged += value;
            remove => StateMachine.OnFlagsChanged -= value;
        }

        public event Action<NodeType, NodeType> OnTypeChanged
        {
            add => Identity.OnTypeChanged += value;
            remove => Identity.OnTypeChanged -= value;
        }

        // Estado de partida. Lo llama quien crea el nodo (LevelLoader, T5-01; hoy la escena de prueba).
        // Sin nivel explícito, el Asteroide empieza en 0 y los tipos encendidos en 1.
        public void Initialize(NodeDefinition definition, Faction faction, int initialEnergy)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            Initialize(definition, faction, initialEnergy, definition.MaxLevel == 0 ? 0 : 1);
        }

        public void Initialize(NodeDefinition definition, Faction faction, int initialEnergy, int initialLevel)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            LevelComponent.Initialize(definition, initialLevel);
            Identity.Initialize(definition);
            Ownership.SetInitialFaction(faction);
            EnergyComponent.Initialize(initialEnergy, definition.EnergyCap);
            StateMachine.Initialize(definition.Type, faction);

            // Se escucha aquí y no en OnEnable, que no se ejecuta en los tests de EditMode.
            if (!listening)
            {
                Ownership.OnFactionChanged += HandleFactionChanged;
                listening = true;
            }
        }

        // El nodo tal como lo leen las reglas de Core. Los estados salen de las banderas de
        // NodeStateMachine, y la carga de poder, de PulsarCaster si el nodo lo lleva (T3-02).
        public NodeSnapshot Snapshot()
        {
            return new NodeSnapshot(Faction, Type, Energy, EnergyCap,
                isFrozen: HasState(NodeStateFlags.Frozen),
                isInfected: HasState(NodeStateFlags.Infected),
                level: Level,
                isConverting: HasState(NodeStateFlags.Converting),
                hasPowerCharge: Caster != null && Caster.HasPowerCharge);
        }

        // Pone o quita una bandera de estado (14.1). La llaman los componentes que provocan cada
        // estado, que son los que conocen sus reglas. Devuelve false si 14.6 no la admite.
        public bool SetStateFlag(NodeStateFlags flag, bool on)
        {
            return StateMachine.SetFlag(flag, on);
        }

        // Suma Energía con el excedente descartado (5.3). La usan la generación de la Estrella
        // y, desde T1-09, los refuerzos.
        public void AddEnergy(int amount)
        {
            EnergyComponent.Add(amount);
        }

        // Resta Energía sin bajar de 0 (5.2). La usa el envío de protones.
        public void SubtractEnergy(int amount)
        {
            EnergyComponent.Subtract(amount);
        }

        // Aplica lo que ImpactResolver calculó para un protón que llega (6.2). Al capturar,
        // el nodo conserva tipo y nivel: solo cambian facción y Energía.
        //
        // Congelado (7.3, T3-04): el deshielo quita la bandera antes de cambiar de bando, como en
        // el pseudocódigo, así que quien escucha la captura ve ya un nodo normal del dueño nuevo.
        // Un impacto que no lo descongela reinicia el reloj del auto-deshielo. lastToucher no se
        // guarda: el bando que gana en el deshielo es el del protón que lo provoca, que es por
        // definición el último emisor.
        //
        // Infectado (7.4, T3-05): cualquier impacto lo cura, y la cura va también antes del cambio
        // de bando: el que captura un nodo infectado se lo lleva sano.
        public void ApplyImpact(ImpactOutcome outcome)
        {
            if (outcome.Unfreezes)
                Thaw();
            else if (HasState(NodeStateFlags.Frozen) && Freezer != null)
                Freezer.NotifyImpact();

            if (outcome.CuresInfection)
                Cure();

            if (outcome.FactionChanged)
                Ownership.ChangeFaction(outcome.NewFaction);

            int delta = outcome.NewEnergy - Energy;
            if (delta > 0)
                EnergyComponent.Add(delta);
            else if (delta < 0)
                EnergyComponent.Subtract(-delta);
        }

        // Efecto de Cero Absoluto (7.3, T3-04). Lo llama PowerBeamSystem al terminar el haz, con el
        // objetivo ya revalidado. Exige NodeFreeze, que lleva el reloj del auto-deshielo.
        public void Freeze()
        {
            if (Freezer == null)
                throw new InvalidOperationException($"{name}: Cero Absoluto sobre un nodo sin NodeFreeze.");
            Freezer.Freeze();
        }

        // Sin NodeFreeze la bandera solo la puede haber puesto un test a mano; el deshielo la quita igual.
        void Thaw()
        {
            if (Freezer != null)
                Freezer.Thaw();
            else
                StateMachine.SetFlag(NodeStateFlags.Frozen, false);
        }

        // Efecto de Decaimiento (7.4, T3-05). Lo llama PowerBeamSystem al terminar el haz, con el
        // objetivo ya revalidado. Si el nodo se estaba reconvirtiendo, la reconversión se cancela
        // primero: 14.6 no deja convivir Infectado con Reconvirtiendo (desviación 203).
        public void Infect()
        {
            if (Decay == null)
                throw new InvalidOperationException($"{name}: Decaimiento sobre un nodo sin NodeDecay.");
            if (HasState(NodeStateFlags.Converting))
            {
                if (Converter == null)
                    throw new InvalidOperationException($"{name}: Reconvirtiendo sin NodeConverter.");
                Converter.Interrupt();
            }
            Decay.Infect();
        }

        // Efecto de Agujero de Gusano (7.5, T3-06). Lo llama PowerBeamSystem al terminar el haz, con el
        // objetivo ya revalidado. Cambia de bando conservando Energía, tipo y nivel, sin pasar por
        // ImpactResolver: no hay Carga, así que la resistencia ×2 no entra. Quita Congelado e Infectado
        // antes del cambio de bando, como el deshielo y la cura (desviaciones 200 y 206).
        //
        // El robo de carga sale solo del cambio de bando: PulsarCaster pierde la carga a medias y
        // conserva el poder listo, y un haz ya lanzado sigue siendo de quien lo lanzó (PowerBeam).
        // Una reconversión en curso se cancela como en cualquier captura (NodeConverter).
        public void Steal(Faction thief)
        {
            if (thief == Faction.Neutral)
                throw new ArgumentException("Un Agujero de Gusano siempre lo lanza un bando.", nameof(thief));

            if (HasState(NodeStateFlags.Frozen))
            {
                if (Freezer != null)
                    Freezer.ClearByWormhole();
                else
                    StateMachine.SetFlag(NodeStateFlags.Frozen, false);
            }
            Cure();
            Ownership.ChangeFaction(thief);
        }

        // Como Thaw: sin NodeDecay, la bandera la ha puesto un test a mano y la cura la quita igual.
        void Cure()
        {
            if (Decay != null)
                Decay.Cure();
            else
                StateMachine.SetFlag(NodeStateFlags.Infected, false);
        }

        // Cambia el nodo a otro tipo y nivel conservando facción y Energía, recortada al Tope
        // nuevo (8.1). Es la mitad mecánica del encendido (NodeIgnition, 4.1) y del final de una
        // reconversión (NodeConverter, T2-07): quien lo llama ya validó la acción y cobró su coste.
        public void ChangeType(NodeDefinition definition, int level)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (!IsInitialized)
                throw new InvalidOperationException($"{name}: ChangeType antes de Initialize.");

            // Nivel primero: valida el par tipo/nivel antes de tocar nada más.
            LevelComponent.Apply(definition, level);
            Identity.SetDefinition(definition);
            EnergyComponent.SetCap(definition.EnergyCap);
            EnergyComponent.Clamp();
            StateMachine.Evaluate(definition.Type, Faction);
        }

        // Validación de mejora (15.5), sin efectos. La usa también el panel de clic derecho para
        // pintar el botón deshabilitado con el coste en rojo (13.3).
        public ActionCheck CheckUpgrade(Faction actor)
        {
            return LevelComponent.CheckUpgrade(Snapshot(), actor);
        }

        // Mejora (16.2, T2-03): cobra el coste de la Energía del propio nodo y sube un nivel del
        // mismo tipo. Tope y estado no cambian: el Tope es del tipo, no del nivel.
        public ActionCheck TryUpgrade(Faction actor)
        {
            ActionCheck check = CheckUpgrade(actor);
            if (!check.IsAllowed)
                return check;

            EnergyComponent.Subtract(check.Cost);
            LevelComponent.Apply(Definition, Level + 1);
            return check;
        }

        void OnDestroy()
        {
            if (listening && ownership != null)
                ownership.OnFactionChanged -= HandleFactionChanged;
            listening = false;
        }

        // 14.1: capturar un Asteroide Neutral lo pasa de Dormido a SinEncender.
        void HandleFactionChanged(Faction previous, Faction current)
        {
            StateMachine.Evaluate(Type, current);
        }

        // Los componentes se resuelven a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeIdentity Identity => identity != null ? identity : identity = GetComponent<NodeIdentity>();
        NodeOwnership Ownership => ownership != null ? ownership : ownership = GetComponent<NodeOwnership>();
        NodeEnergy EnergyComponent => energy != null ? energy : energy = GetComponent<NodeEnergy>();
        NodeLevelComponent LevelComponent => levelComponent != null ? levelComponent : levelComponent = GetComponent<NodeLevelComponent>();
        NodeStateMachine StateMachine => stateMachine != null ? stateMachine : stateMachine = GetComponent<NodeStateMachine>();

        // Opcional: solo lo llevan los nodos que pueden ser Púlsar (T3-02).
        PulsarCaster Caster => caster != null ? caster : caster = GetComponent<PulsarCaster>();

        // Opcional en los tests; en partida lo lleva todo nodo (T3-04).
        NodeFreeze Freezer => freezer != null ? freezer : freezer = GetComponent<NodeFreeze>();

        // Opcional en los tests; en partida lo lleva todo nodo (T3-05).
        NodeDecay Decay => decay != null ? decay : decay = GetComponent<NodeDecay>();

        // Opcional: solo para cancelar una reconversión al infectar (T3-05).
        NodeConverter Converter => converter != null ? converter : converter = GetComponent<NodeConverter>();
    }
}
