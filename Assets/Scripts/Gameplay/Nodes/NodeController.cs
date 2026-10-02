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
        bool listening;

        public NodeType Type => Identity.Type;
        public NodeDefinition Definition => Identity.Definition;
        public Faction Faction => Ownership.Faction;
        public int Energy => EnergyComponent.Energy;
        public int EnergyCap => EnergyComponent.EnergyCap;
        public int Level => LevelComponent.Level;
        public NodeLevelData CurrentLevelData => LevelComponent.CurrentLevelData;
        public NodeState State => StateMachine.State;
        public bool IsInitialized => Identity.Definition != null;

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

        // El nodo tal como lo leen las reglas de Core. Congelado, infectado, reconversión y
        // carga de poder llegan con las tareas que los provocan.
        public NodeSnapshot Snapshot()
        {
            return new NodeSnapshot(Faction, Type, Energy, EnergyCap, level: Level);
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
        // el nodo conserva tipo y nivel: solo cambian facción y Energía. Deshielo, cura y
        // lastToucher llegan con los estados que los usan (T3-04, T3-05).
        public void ApplyImpact(ImpactOutcome outcome)
        {
            if (outcome.FactionChanged)
                Ownership.ChangeFaction(outcome.NewFaction);

            int delta = outcome.NewEnergy - Energy;
            if (delta > 0)
                EnergyComponent.Add(delta);
            else if (delta < 0)
                EnergyComponent.Subtract(-delta);
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
    }
}
