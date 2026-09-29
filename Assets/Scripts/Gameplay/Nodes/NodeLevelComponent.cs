using System;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Gameplay
{
    // Nivel actual del nodo y su NodeLevelData (15.5). El Asteroide es siempre nivel 0 (4.1);
    // los tipos encendidos van de 1 a su máximo. Valida la mejora, pero no la paga: eso es T2-03.
    [DisallowMultipleComponent]
    public sealed class NodeLevelComponent : MonoBehaviour
    {
        NodeDefinition definition;
        int level;

        public int Level => level;
        public int MaxLevel => definition != null ? definition.MaxLevel : 0;
        public bool IsMaxLevel => level >= MaxLevel;

        // Estadísticas del nivel actual: intervalo de la Estrella, radio y cadencia del Magnetar.
        public NodeLevelData CurrentLevelData
        {
            get
            {
                if (level == 0)
                    throw new InvalidOperationException("El Asteroide es nivel 0 y no tiene NodeLevelData (4.1).");
                return definition.GetLevel(level);
            }
        }

        // Coste de subir al nivel siguiente (16.2). 0 si ya está al máximo.
        public int NextUpgradeCost => IsMaxLevel ? 0 : definition.UpgradeCostTo(level + 1);

        // (anterior, nuevo). Solo se emite si el nivel cambia de verdad.
        public event Action<int, int> OnLevelChanged;

        // Valores de partida, sin evento.
        public void Initialize(NodeDefinition newDefinition, int initialLevel)
        {
            Validate(newDefinition, initialLevel);
            definition = newDefinition;
            level = initialLevel;
        }

        // Única puerta para cambiar de nivel: mejora (mismo tipo, nivel + 1), encendido
        // (Asteroide → nivel 1 de otro tipo) y fin de reconversión (nivel 1 del tipo nuevo, 8.1).
        public void Apply(NodeDefinition newDefinition, int newLevel)
        {
            Validate(newDefinition, newLevel);
            definition = newDefinition;

            if (newLevel == level)
                return;

            int previous = level;
            level = newLevel;
            OnLevelChanged?.Invoke(previous, newLevel);
        }

        // Validación de mejora. Las reglas están en ConversionRules; aquí solo se aporta el coste.
        public ActionCheck CheckUpgrade(NodeSnapshot node, Faction actor)
        {
            return ConversionRules.CheckUpgrade(node, actor, MaxLevel, NextUpgradeCost);
        }

        static void Validate(NodeDefinition definition, int level)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            int min = definition.MaxLevel == 0 ? 0 : 1;
            if (level < min || level > definition.MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level,
                    $"{definition.Type} tiene niveles {min} a {definition.MaxLevel}.");
        }
    }
}
