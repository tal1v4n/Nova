using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Gameplay;
using Nova.Systems;
using UnityEngine;

namespace Nova.Enemy
{
    // INodeCommandIssuer del Enemigo (12.7, 15.1 #5, 15.7, T4-03). Los comandos son los de
    // NodeCommandIssuer, los mismos que tiene el Jugador (3.1): el algoritmo no tiene ningún atajo, y
    // lo que es legal lo deciden las mismas reglas.
    //
    // Execute traduce una EnemyAction —índices de NodeRegistry— al comando que le toca. Lo usan
    // EnemyController (T4-04) y la defensa de EnemyThreatResponder (T4-05).
    [DisallowMultipleComponent]
    public sealed class EnemyCommandIssuer : NodeCommandIssuer
    {
        public override Faction Faction => Faction.Enemy;

        // Ejecuta la acción sobre los nodos de nodes, en el orden de NodeRegistry con el que se
        // construyó el EnemyBoard. Devuelve si las reglas la aceptaron: el tablero puede haber
        // cambiado desde que se puntuó, y entonces la acción simplemente no sale.
        public bool Execute(EnemyAction action, IReadOnlyList<NodeController> nodes)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));

            NodeController actor = At(nodes, action.Actor);
            switch (action.Kind)
            {
                case EnemyActionKind.Attack:
                    return Send(actor, At(nodes, action.Target), action.FullSend) == LaunchBlock.None;
                case EnemyActionKind.Reinforce:
                    // 12.4: siempre el 50 %.
                    return Send(actor, At(nodes, action.Target), false) == LaunchBlock.None;
                case EnemyActionKind.Evacuate:
                    // 12.4: toda la Energía, cediendo un nodo vacío en vez de uno lleno (T4-05).
                    return Send(actor, At(nodes, action.Target), true) == LaunchBlock.None;
                case EnemyActionKind.Ignite:
                    return Ignite(actor, action.NodeType).IsAllowed;
                case EnemyActionKind.Upgrade:
                    return Upgrade(actor).IsAllowed;
                case EnemyActionKind.ChargePower:
                    return ChargePower(actor, action.Power).IsAllowed;
                case EnemyActionKind.LaunchPower:
                    return LaunchPower(actor, At(nodes, action.Target)).IsAllowed;
                case EnemyActionKind.Convert:
                    return Convert(actor, action.NodeType).IsAllowed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, null);
            }
        }

        static NodeController At(IReadOnlyList<NodeController> nodes, int index)
        {
            if (index < 0 || index >= nodes.Count || nodes[index] == null)
                throw new ArgumentOutOfRangeException(nameof(index), index, "La acción apunta a un nodo que no está en el tablero.");
            return nodes[index];
        }
    }
}
