using System.IO;
using Nova.Core;
using Nova.Data;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor
{
    // Los nueve LevelDefinition de Assets/Data/Levels/ (T5-02), escritos de forma idempotente. Los
    // nodos son los del apéndice «Diseño de los 9 niveles» del maestro, y las demás columnas, las de
    // 11.1 y 11.2. Si el diseño cambia, se cambia aquí y en el apéndice, y se vuelve a generar: un
    // asset retocado a mano en el Inspector se pierde al regenerar. LevelAssetsTests compara lo
    // generado con 11.1, 11.2 y 11.3, no con esta tabla.
    public static class LevelAssets
    {
        const string LevelsFolder = "Assets/Data/Levels";
        const string ProfilesFolder = "Assets/Data/EnemyProfiles";

        // Duración objetivo de 11.2, en segundos.
        static readonly float[] Durations = { 90f, 110f, 130f, 150f, 170f, 190f, 210f, 230f, 240f };

        static NodeSpawnData Spawn(NodeType type, Faction faction, int energy, float x, float y, int level = 0) =>
            new NodeSpawnData(new Vector2(x, y), type, faction, energy, level);

        // Energía inicial de 18 #37: Asteroide 10, nodo encendido con la mitad de su Tope, Magnetar
        // neutral lleno.
        static readonly NodeSpawnData[][] Spawns =
        {
            // Nivel 1
            new[]
            {
                Spawn(NodeType.Star, Faction.Player, 25, -5f, 0f, 1),
                Spawn(NodeType.Star, Faction.Enemy, 25, 5f, 0f, 1),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -3.25f, 1f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 3.25f, -1f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 0f, 2f),
            },
            // Nivel 2
            new[]
            {
                Spawn(NodeType.Star, Faction.Player, 25, -5.5f, 0f, 1),
                Spawn(NodeType.Star, Faction.Enemy, 25, 5.5f, 0f, 2),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -3.5f, 0.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 3.5f, -0.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -1.5f, -2.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 1.5f, 2.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 0f, 0f),
            },
            // Nivel 3
            new[]
            {
                Spawn(NodeType.Star, Faction.Player, 25, -5.5f, -0.5f, 1),
                Spawn(NodeType.Asteroid, Faction.Player, 10, -5f, 1.5f),
                Spawn(NodeType.Star, Faction.Enemy, 25, 5.5f, 0.5f, 2),
                Spawn(NodeType.Magnetar, Faction.Enemy, 50, 3.75f, -1.5f, 1),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -3.5f, -1.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 0f, 2.5f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 0f, -2.5f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2.75f, 1.75f),
            },
            // Nivel 4
            new[]
            {
                Spawn(NodeType.Star, Faction.Player, 25, -5.75f, 0f, 1),
                Spawn(NodeType.Star, Faction.Enemy, 25, 5.5f, 1.25f, 2),
                Spawn(NodeType.Pulsar, Faction.Enemy, 50, 5.5f, -1.5f, 1),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -4f, 1.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -3.75f, -1.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -1.25f, 0.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 1.25f, 2.5f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 1.5f, -2.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 3.5f, 0f),
            },
            // Nivel 5
            new[]
            {
                Spawn(NodeType.Star, Faction.Player, 25, -6f, 0.75f, 1),
                Spawn(NodeType.Magnetar, Faction.Player, 50, -4.5f, -1.5f, 1),
                Spawn(NodeType.Star, Faction.Enemy, 25, 5.75f, 1f, 2),
                Spawn(NodeType.Pulsar, Faction.Enemy, 50, 5.5f, -1.75f, 1),
                Spawn(NodeType.Magnetar, Faction.Neutral, 100, 0f, 0f, 2),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -4.25f, 1.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -2.5f, -2.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -1.75f, 2.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2.25f, 2.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2.5f, -2.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 3.75f, -0.25f),
            },
            // Nivel 6
            new[]
            {
                Spawn(NodeType.Star, Faction.Player, 25, -6f, 0f, 2),
                Spawn(NodeType.Star, Faction.Enemy, 25, 5.75f, 0f, 3),
                Spawn(NodeType.Pulsar, Faction.Enemy, 50, 5f, 2.5f, 2),
                Spawn(NodeType.Magnetar, Faction.Enemy, 50, 4f, -1.75f, 2),
                Spawn(NodeType.Magnetar, Faction.Neutral, 100, 0.5f, -0.5f, 3),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -4f, 0.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -3.5f, -2.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -2.75f, 2.5f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -0.75f, 2.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2.5f, 2f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2.5f, -3f),
            },
            // Nivel 7
            new[]
            {
                Spawn(NodeType.Star, Faction.Player, 25, -6f, 1.75f, 1),
                Spawn(NodeType.Star, Faction.Player, 25, -5.5f, -2f, 1),
                Spawn(NodeType.Star, Faction.Enemy, 25, 6f, 0f, 3),
                Spawn(NodeType.Pulsar, Faction.Enemy, 50, 5f, 2.5f, 3),
                Spawn(NodeType.Magnetar, Faction.Enemy, 50, 4f, -1.5f, 3),
                Spawn(NodeType.Magnetar, Faction.Neutral, 100, 0f, 0f, 3),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -4.25f, 2.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -3.75f, -2.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -3.5f, 0.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -1f, 3f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -1f, -3f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2.5f, 3f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2.75f, -3f),
            },
            // Nivel 8
            new[]
            {
                Spawn(NodeType.Star, Faction.Player, 25, -6.25f, 0f, 2),
                Spawn(NodeType.Star, Faction.Enemy, 25, 6f, 1.75f, 3),
                Spawn(NodeType.Star, Faction.Enemy, 25, 6f, -1.75f, 3),
                Spawn(NodeType.Pulsar, Faction.Enemy, 50, 4.5f, 0f, 3),
                Spawn(NodeType.Magnetar, Faction.Enemy, 50, 3.5f, 2.75f, 3),
                Spawn(NodeType.Magnetar, Faction.Neutral, 100, -0.5f, 1.75f, 3),
                Spawn(NodeType.Magnetar, Faction.Neutral, 100, 0.5f, -1.75f, 3),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -4.25f, 0.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -4f, -2f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -2.75f, 3f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -2.5f, -0.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2f, 0.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 1.75f, 3f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2.5f, -3f),
            },
            // Nivel 9
            new[]
            {
                Spawn(NodeType.Star, Faction.Player, 25, -6.25f, 1f, 2),
                Spawn(NodeType.Magnetar, Faction.Player, 50, -5f, -1.75f, 1),
                Spawn(NodeType.Star, Faction.Enemy, 25, 6.25f, 1.75f, 4),
                Spawn(NodeType.Star, Faction.Enemy, 25, 6.25f, -1.75f, 4),
                Spawn(NodeType.Pulsar, Faction.Enemy, 50, 4.75f, 3f, 3),
                Spawn(NodeType.Pulsar, Faction.Enemy, 50, 4.75f, -3f, 3),
                Spawn(NodeType.Magnetar, Faction.Enemy, 50, 4.5f, 0f, 4),
                Spawn(NodeType.Magnetar, Faction.Neutral, 100, 0f, 2f, 4),
                Spawn(NodeType.Magnetar, Faction.Neutral, 100, 0f, -2f, 4),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -4.5f, 2f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -3.25f, -2.75f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -3.5f, 0.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, -2.5f, 3f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2f, 0.25f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2.25f, 3f),
                Spawn(NodeType.Asteroid, Faction.Neutral, 10, 2.5f, -3f),
            },
        };

        public static string PathOf(int number) => $"{LevelsFolder}/Level{number}.asset";

        [MenuItem("Nova/Fase 5/Generar los 9 niveles")]
        public static void Generate()
        {
            Directory.CreateDirectory(LevelsFolder);
            for (int i = 0; i < Spawns.Length; i++)
            {
                int number = i + 1;
                int map = (number - 1) / LevelDefinition.LevelsPerMap + 1;
                var profile = AssetDatabase.LoadAssetAtPath<EnemyProfile>($"{ProfilesFolder}/Map{map}.asset");
                if (profile == null)
                {
                    Debug.LogError($"[Nova] Falta {ProfilesFolder}/Map{map}.asset.");
                    return;
                }

                string path = PathOf(number);
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(level, path);
                }
                level.Configure(number, profile, Durations[i], Spawns[i], IgnitionTypes(map), Powers(map), map == 3, Drops(map));
                EditorUtility.SetDirty(level);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nova] Los 9 niveles generados en {LevelsFolder}.");
        }

        // Mecánicas de 11.1.
        static NodeType[] IgnitionTypes(int map) => map == 1
            ? new[] { NodeType.Star, NodeType.Magnetar }
            : new[] { NodeType.Star, NodeType.Pulsar, NodeType.Magnetar };

        static PowerType[] Powers(int map) => map == 1 ? new PowerType[0]
            : map == 2 ? new[] { PowerType.AbsoluteZero, PowerType.Decay }
            : new[] { PowerType.AbsoluteZero, PowerType.Decay, PowerType.Wormhole };

        // Calendario de drops de 9.3 (T5-04).
        public static DropSchedule Drops(int map) => map == 1 ? new DropSchedule(25f, 40f, 55f, 3)
            : map == 2 ? new DropSchedule(30f, 50f, 70f, 4)
            : new DropSchedule(35f, 60f, 85f, 5);
    }
}
