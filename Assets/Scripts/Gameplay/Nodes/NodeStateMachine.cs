using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Gameplay
{
    // Estados del nodo de 14.1 (15.5). Hoy solo los de base, que se deducen del tipo y la
    // facción: Dormido, SinEncender y Normal. Los demás, con sus timers y la matriz de 14.6,
    // llegan con las tareas que los provocan (T2-07, T2-08, T3-04, T3-05).
    [DisallowMultipleComponent]
    public sealed class NodeStateMachine : MonoBehaviour
    {
        NodeState state = NodeState.Sleeping;

        public NodeState State => state;

        // (anterior, nuevo). Solo se emite si el estado cambia de verdad.
        public event Action<NodeState, NodeState> OnStateChanged;

        // Estado de partida, sin evento.
        public void Initialize(NodeType type, Faction faction)
        {
            state = BaseState(type, faction);
        }

        // Recalcula el estado de base tras un cambio de facción o de tipo.
        public void Evaluate(NodeType type, Faction faction)
        {
            Set(BaseState(type, faction));
        }

        // 14.1: el Asteroide duerme si es Neutral y espera encendido si tiene dueño.
        // Cualquier nodo encendido está en Normal mientras no entre otro estado.
        public static NodeState BaseState(NodeType type, Faction faction)
        {
            if (type != NodeType.Asteroid)
                return NodeState.Normal;
            return faction == Faction.Neutral ? NodeState.Sleeping : NodeState.Unignited;
        }

        void Set(NodeState value)
        {
            if (value == state)
                return;

            NodeState previous = state;
            state = value;
            OnStateChanged?.Invoke(previous, value);
        }
    }
}
