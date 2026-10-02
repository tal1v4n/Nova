using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Gameplay
{
    // Encendido de Asteroide (4.1, 15.5): un Asteroide propio con la Energía del coste pasa a
    // nivel 1 de Estrella, Púlsar o Magnetar. Las reglas son de ConversionRules; aquí se cobra
    // el coste y se cambia el tipo. Con el Tope de 10 y el coste de 10, el nodo nuevo nace a 0.
    //
    // La recogida del power-up que 15.5 le asigna llega con PowerUpSpawner (T5-04).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class NodeIgnition : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;

        [Tooltip("Una NodeDefinition por tipo al que se puede encender: Estrella, Púlsar y Magnetar.")]
        [SerializeField] NodeDefinition[] targets = Array.Empty<NodeDefinition>();

        NodeController node;

        // Validación sin efectos. La usan el comando y, en T2-02, el menú radial para pintar
        // cada opción deshabilitada con el coste en rojo (13.7).
        public ActionCheck Check(Faction actor, NodeType targetType)
        {
            return ConversionRules.CheckIgnition(Node.Snapshot(), actor, targetType, Balance.IgnitionCost);
        }

        public ActionCheck TryIgnite(Faction actor, NodeType targetType)
        {
            ActionCheck check = Check(actor, targetType);
            if (!check.IsAllowed)
                return check;

            NodeDefinition definition = DefinitionFor(targetType);
            Node.SubtractEnergy(check.Cost);
            Node.ChangeType(definition, 1);
            return check;
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config, IEnumerable<NodeDefinition> ignitionTargets)
        {
            balance = config;
            targets = ignitionTargets != null ? new List<NodeDefinition>(ignitionTargets).ToArray() : Array.Empty<NodeDefinition>();
        }

        NodeDefinition DefinitionFor(NodeType type)
        {
            foreach (NodeDefinition definition in targets)
            {
                if (definition != null && definition.Type == type)
                    return definition;
            }
            throw new InvalidOperationException($"{name}: NodeIgnition no tiene la NodeDefinition de {type}.");
        }

        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("NodeIgnition necesita BalanceConfig.");

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
    }
}
