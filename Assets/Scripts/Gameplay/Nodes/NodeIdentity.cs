using System;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Gameplay
{
    // Tipo actual del nodo y su NodeDefinition (15.5). Cambia al encender y al reconvertir.
    [DisallowMultipleComponent]
    public sealed class NodeIdentity : MonoBehaviour
    {
        NodeDefinition definition;

        public NodeDefinition Definition => definition;
        public NodeType Type => definition != null ? definition.Type : NodeType.Asteroid;

        // (anterior, nuevo). Solo se emite si el tipo cambia de verdad: encendido y reconversión.
        public event Action<NodeType, NodeType> OnTypeChanged;

        // Valor de partida, sin evento. Solo lo llama NodeController.
        public void Initialize(NodeDefinition newDefinition)
        {
            definition = newDefinition != null ? newDefinition : throw new ArgumentNullException(nameof(newDefinition));
        }

        // Encendido y reconversión. Solo lo llama NodeController.
        public void SetDefinition(NodeDefinition newDefinition)
        {
            if (newDefinition == null)
                throw new ArgumentNullException(nameof(newDefinition));

            NodeType previous = Type;
            definition = newDefinition;
            if (previous != newDefinition.Type)
                OnTypeChanged?.Invoke(previous, newDefinition.Type);
        }
    }
}
