using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // Definición de un tipo de nodo (15.4): tope y tabla de niveles. Una instancia por tipo
    // en Assets/Data/Nodes/. El prefab y los sprites por facción llegan con la presentación.
    [CreateAssetMenu(menuName = "Nova/Node Definition", fileName = "NodeDefinition")]
    public sealed class NodeDefinition : ScriptableObject
    {
        [SerializeField] NodeType type;

        [Tooltip("Energía máxima (5.4). Depende del tipo, no del nivel.")]
        [SerializeField, Min(0)] int energyCap;

        [Tooltip("levels[0] es el nivel 1. Vacío en el Asteroide, que siempre es nivel 0 (4.1).")]
        [SerializeField] NodeLevelData[] levels = Array.Empty<NodeLevelData>();

        public NodeType Type => type;
        public int EnergyCap => energyCap;

        // Nivel máximo del tipo: 5 la Estrella y el Magnetar, 3 el Púlsar, 0 el Asteroide.
        public int MaxLevel => levels.Length;

        public NodeLevelData GetLevel(int level)
        {
            if (level < 1 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, $"{type} tiene niveles 1 a {MaxLevel}.");
            return levels[level - 1];
        }

        // Coste de subir al nivel indicado, de 2 al máximo. El nivel 1 es el encendido (4.1).
        public int UpgradeCostTo(int level)
        {
            if (level < 2 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, $"{type} se mejora a los niveles 2 a {MaxLevel}.");
            return levels[level - 1].UpgradeCost;
        }
    }
}
