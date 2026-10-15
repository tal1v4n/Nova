using System;
using System.Collections.Generic;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // Un nivel de los nueve (11, 15.4, T5-01): sus nodos de partida, el perfil del Enemigo, la
    // duración objetivo y las mecánicas que el nivel permite (11.1, 18 #36), las mismas para los
    // dos bandos. Los nueve assets viven en Assets/Data/Levels/ (T5-02); los instancia LevelLoader.
    // La config de drops de 15.4 es el calendario de 9.3 (T5-04), que lee PowerUpSpawner.
    [CreateAssetMenu(menuName = "Nova/Level Definition", fileName = "Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        public const int LevelsPerMap = 3;

        [Tooltip("1 a 9 (11.2). El mapa sale de aquí: niveles 1–3, mapa 1; 4–6, mapa 2; 7–9, mapa 3.")]
        [SerializeField, Range(1, 9)] int number = 1;

        [Tooltip("Perfil del algoritmo del mapa (12.5).")]
        [SerializeField] EnemyProfile enemyProfile;

        [Tooltip("Duración objetivo en segundos (11.2). Referencia de diseño: ninguna regla la usa.")]
        [SerializeField, Min(0f)] float targetDuration;

        [Tooltip("Nodos de partida, en el orden en que se registran (NodeRegistry, desempate de 12.3.3).")]
        [SerializeField] List<NodeSpawnData> spawns = new List<NodeSpawnData>();

        [Header("Mecánicas del nivel (11.1)")]
        [Tooltip("Tipos a los que se puede encender un Asteroide.")]
        [SerializeField] NodeType[] ignitionTypes = Array.Empty<NodeType>();

        [Tooltip("Poderes que un Púlsar puede cargar.")]
        [SerializeField] PowerType[] powers = Array.Empty<PowerType>();

        [Tooltip("Si un nodo encendido se puede reconvertir (8).")]
        [SerializeField] bool allowsConversion;

        [Header("Power-ups del cielo (9.3)")]
        [SerializeField] DropSchedule drops;

        public int Number => number;
        public int Map => (number - 1) / LevelsPerMap + 1;
        public EnemyProfile EnemyProfile => enemyProfile;
        public float TargetDuration => targetDuration;
        public IReadOnlyList<NodeSpawnData> Spawns => spawns;
        public IReadOnlyList<NodeType> IgnitionTypes => ignitionTypes;
        public IReadOnlyList<PowerType> Powers => powers;
        public bool AllowsConversion => allowsConversion;
        public DropSchedule Drops => drops;

        public bool AllowsIgnition(NodeType type) => Array.IndexOf(ignitionTypes, type) >= 0;
        public bool AllowsPower(PowerType type) => Array.IndexOf(powers, type) >= 0;

        // Para los tests y el generador de la escena de prueba: el cableado normal es por el Inspector.
        public void Configure(int levelNumber, EnemyProfile profile, float duration, IEnumerable<NodeSpawnData> nodes,
                              IEnumerable<NodeType> ignitions, IEnumerable<PowerType> allowedPowers, bool conversion,
                              DropSchedule dropSchedule = default)
        {
            if (levelNumber < 1 || levelNumber > 9)
                throw new ArgumentOutOfRangeException(nameof(levelNumber), levelNumber, "Los niveles van del 1 al 9.");
            number = levelNumber;
            enemyProfile = profile;
            targetDuration = duration;
            spawns = nodes != null ? new List<NodeSpawnData>(nodes) : new List<NodeSpawnData>();
            ignitionTypes = ignitions != null ? new List<NodeType>(ignitions).ToArray() : Array.Empty<NodeType>();
            powers = allowedPowers != null ? new List<PowerType>(allowedPowers).ToArray() : Array.Empty<PowerType>();
            allowsConversion = conversion;
            drops = dropSchedule;
        }
    }
}
