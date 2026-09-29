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

        // Solo lo llama NodeController.
        public void SetDefinition(NodeDefinition newDefinition)
        {
            definition = newDefinition != null ? newDefinition : throw new ArgumentNullException(nameof(newDefinition));
        }
    }
}
