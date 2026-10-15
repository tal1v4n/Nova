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
    // Recogida del power-up (9.1, T5-04): PowerUpSpawner marca un Asteroide neutral y el primer
    // bando que lo encienda se lleva el efecto. La marca sobrevive a las capturas, porque para
    // encenderlo hay que poseerlo antes; la quita el encendido o el spawner al apagarse.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class NodeIgnition : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;

        [Tooltip("Una NodeDefinition por tipo al que se puede encender: Estrella, Púlsar y Magnetar.")]
        [SerializeField] NodeDefinition[] targets = Array.Empty<NodeDefinition>();

        NodeController node;
        PowerUpDefinition powerUp;

        // El power-up que espera sobre este Asteroide, o null.
        public PowerUpDefinition PowerUp => powerUp;
        public bool IsMarked => powerUp != null;

        // (bando que enciende, power-up) al encender un Asteroide marcado. Lo atiende PowerUpSpawner.
        public event Action<Faction, PowerUpDefinition> OnPowerUpCollected;

        // Validación sin efectos. La usan el comando y el menú radial (T2-02), que pinta cada
        // opción deshabilitada con el coste en rojo (13.7) o como no disponible.
        //
        // Un tipo sin NodeDefinition en targets es Unavailable: el menú enseña siempre las tres
        // opciones, pero las de tipos que todavía no existen —Magnetar hasta T2-04, Púlsar hasta
        // T3-02— salen deshabilitadas. Va por delante de la Energía porque reunir 10 no lo arregla.
        public ActionCheck Check(Faction actor, NodeType targetType)
        {
            ActionCheck rules = ConversionRules.CheckIgnition(Node.Snapshot(), actor, targetType, Balance.IgnitionCost);
            bool reachable = rules.IsAllowed || rules.Block == ActionBlock.InsufficientEnergy;
            if (reachable && !Offers(targetType))
                return new ActionCheck(ActionBlock.Unavailable, rules.Cost);
            return rules;
        }

        public bool Offers(NodeType type) => FindDefinition(type) != null;

        public ActionCheck TryIgnite(Faction actor, NodeType targetType)
        {
            ActionCheck check = Check(actor, targetType);
            if (!check.IsAllowed)
                return check;

            NodeDefinition definition = DefinitionFor(targetType);
            Node.SubtractEnergy(check.Cost);
            Node.ChangeType(definition, 1);

            if (powerUp != null)
            {
                PowerUpDefinition collected = powerUp;
                powerUp = null;
                OnPowerUpCollected?.Invoke(actor, collected);
            }
            return check;
        }

        // Un power-up cae sobre este Asteroide (9.1). Solo sobre uno neutral: un Asteroide propio no
        // recibe power-ups (17, caso 26).
        public void Mark(PowerUpDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (!Node.IsInitialized || Node.Type != NodeType.Asteroid || Node.Faction != Faction.Neutral)
                throw new InvalidOperationException($"{name}: un power-up solo cae sobre un Asteroide neutral.");
            powerUp = definition;
        }

        // El power-up se apaga sin que nadie lo recoja (9.1), o el nivel termina.
        public void Unmark()
        {
            powerUp = null;
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config, IEnumerable<NodeDefinition> ignitionTargets)
        {
            balance = config;
            targets = ignitionTargets != null ? new List<NodeDefinition>(ignitionTargets).ToArray() : Array.Empty<NodeDefinition>();
        }

        NodeDefinition DefinitionFor(NodeType type)
        {
            NodeDefinition definition = FindDefinition(type);
            if (definition == null)
                throw new InvalidOperationException($"{name}: NodeIgnition no tiene la NodeDefinition de {type}.");
            return definition;
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

        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("NodeIgnition necesita BalanceConfig.");

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
    }
}
