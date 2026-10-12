using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Gameplay
{
    // Estados del nodo de 14.1 (15.5): un estado de base excluyente más banderas (T.7 #43).
    // La base se deduce del tipo y la facción: Dormido, SinEncender y Normal. Las banderas las
    // ponen y las quitan los componentes que las provocan —Reconvirtiendo, NodeConverter desde
    // T2-07; Asustado, Congelado e Infectado llegan con T2-08, T3-04 y T3-05— y aquí solo se
    // guardan y se comprueban contra la matriz de 14.6.
    [DisallowMultipleComponent]
    public sealed class NodeStateMachine : MonoBehaviour
    {
        NodeState state = NodeState.Sleeping;
        NodeStateFlags flags;

        public NodeState State => state;
        public NodeStateFlags Flags => flags;

        // (anterior, nuevo). Solo se emite si el estado cambia de verdad.
        public event Action<NodeState, NodeState> OnStateChanged;

        // (anteriores, nuevas). Solo se emite si las banderas cambian de verdad.
        public event Action<NodeStateFlags, NodeStateFlags> OnFlagsChanged;

        public bool Has(NodeStateFlags flag) => flag != NodeStateFlags.None && (flags & flag) == flag;

        // Estado de partida, sin evento y sin banderas.
        public void Initialize(NodeType type, Faction faction)
        {
            state = BaseState(type, faction);
            flags = NodeStateFlags.None;
        }

        // Recalcula el estado de base tras un cambio de facción o de tipo. Las banderas no se
        // tocan: las quita quien las puso.
        public void Evaluate(NodeType type, Faction faction)
        {
            Set(BaseState(type, faction));
        }

        // 14.1: el Asteroide duerme si es Neutral y espera encendido si tiene dueño.
        // Cualquier nodo encendido está en Normal.
        public static NodeState BaseState(NodeType type, Faction faction)
        {
            if (type != NodeType.Asteroid)
                return NodeState.Normal;
            return faction == Faction.Neutral ? NodeState.Sleeping : NodeState.Unignited;
        }

        // Pone o quita una bandera. Devuelve false, sin cambiar nada, si ponerla rompería la
        // matriz de 14.6. Quitar siempre se puede.
        public bool SetFlag(NodeStateFlags flag, bool on)
        {
            if (flag == NodeStateFlags.None || (flag & (flag - 1)) != 0)
                throw new ArgumentException("Una sola bandera por llamada.", nameof(flag));

            if (on && !Has(flag) && !CanCoexist(flags, flag))
                return false;

            NodeStateFlags next = on ? flags | flag : flags & ~flag;
            if (next == flags)
                return true;

            NodeStateFlags previous = flags;
            flags = next;
            OnFlagsChanged?.Invoke(previous, next);
            return true;
        }

        // Matriz de 14.6: todo convive con todo salvo Infectado con Reconvirtiendo. Capturado
        // no está en la matriz porque no bloquea nada (14.1): convive con cualquiera.
        public static bool CanCoexist(NodeStateFlags current, NodeStateFlags added)
        {
            const NodeStateFlags infectedOrConverting = NodeStateFlags.Infected | NodeStateFlags.Converting;
            NodeStateFlags both = current | added;
            return (both & infectedOrConverting) != infectedOrConverting;
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
