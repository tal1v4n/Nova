using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // Definición de un tipo de nodo (15.4): tope, tabla de niveles y, desde T6-01, el arte de cada
    // nivel. Una instancia por tipo en Assets/Data/Nodes/. El prefab no vive aquí: todos los tipos
    // comparten Assets/Prefabs/Node.prefab, porque un nodo cambia de tipo sin cambiar de objeto.
    [CreateAssetMenu(menuName = "Nova/Node Definition", fileName = "NodeDefinition")]
    public sealed class NodeDefinition : ScriptableObject
    {
        [SerializeField] NodeType type;

        [Tooltip("Energía máxima (5.4). Depende del tipo, no del nivel.")]
        [SerializeField, Min(0)] int energyCap;

        [Tooltip("Energía que cuesta reconvertir un nodo de este tipo a otro (16.4). Depende del origen, no del destino. 0 en el Asteroide, que no se reconvierte (8.1).")]
        [SerializeField, Min(0)] int conversionCost;

        [Tooltip("levels[0] es el nivel 1. Vacío en el Asteroide, que siempre es nivel 0 (4.1).")]
        [SerializeField] NodeLevelData[] levels = Array.Empty<NodeLevelData>();

        [Tooltip("art[0] es el nivel 1, o el 0 en el Asteroide. Lo rellena Nova → Arte → Asignar sprites de nodo (T6-01).")]
        [SerializeField] NodeLevelArt[] art = Array.Empty<NodeLevelArt>();

        public NodeType Type => type;
        public int EnergyCap => energyCap;
        public int ConversionCost => conversionCost;

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

        // Niveles con sprite: 1 al máximo, o el 0 del Asteroide, que no tiene escalera (4.1).
        public int ArtLevels => Math.Max(1, MaxLevel);

        // Lo hay cuando NodeArtAssets ya lo asignó. Sin él, la presentación cae en la vista provisional.
        public bool HasArt => art.Length == ArtLevels;

        public NodeLevelArt GetArt(int level)
        {
            int index = MaxLevel == 0 ? level : level - 1;
            if (index < 0 || index >= ArtLevels)
                throw new ArgumentOutOfRangeException(nameof(level), level, $"{type} no tiene arte para ese nivel.");
            if (!HasArt)
                throw new InvalidOperationException($"{type} sin arte asignado: Nova → Arte → Asignar sprites de nodo.");
            return art[index];
        }

        // Sin excepciones, para quien escucha eventos: ChangeType aplica el nivel nuevo antes que el
        // tipo, así que un Asteroide que se enciende avisa del nivel 1 cuando aún es Asteroide.
        public bool TryGetArt(int level, out NodeLevelArt levelArt)
        {
            int index = MaxLevel == 0 ? level : level - 1;
            bool found = HasArt && index >= 0 && index < ArtLevels;
            levelArt = found ? art[index] : default;
            return found;
        }

        // Para NodeArtAssets: el editor escribe aquí lo que copia ArtBridge.
        public void ConfigureArt(NodeLevelArt[] levelArt)
        {
            if (levelArt == null || levelArt.Length != ArtLevels)
                throw new ArgumentException($"{type} necesita arte para {ArtLevels} niveles.", nameof(levelArt));
            art = levelArt;
        }
    }
}
