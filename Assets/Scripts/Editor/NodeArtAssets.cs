using System;
using Nova.Core;
using Nova.Data;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor
{
    // T6-01: el arte de cada nivel en los cuatro NodeDefinition, desde lo que ArtBridge copia a
    // Assets/Sprites/nodes/: los sprites <prefijo>_lvl<n>_<facción>.png de A.1 y <prefijo>_huella.json,
    // que escribe el generador con la huella sólida de cada nivel. Idempotente: si el arte cambia, se
    // vuelve a generar, sincronizar y asignar, y lo retocado a mano se pierde.
    public static class NodeArtAssets
    {
        public const string SpritesFolder = "Assets/Sprites/nodes";
        public const string NodesFolder = "Assets/Data/Nodes";

        static readonly string[] Factions = { "neutral", "player", "enemy" };

        // Tipo, asset de NodeDefinition, carpeta de A.1 y prefijo de archivo.
        public static readonly (NodeType type, string asset, string folder, string prefix)[] Nodes =
        {
            (NodeType.Asteroid, "Asteroid", "asteroide", "ast"),
            (NodeType.Star, "Star", "estrella", "str"),
            (NodeType.Pulsar, "Pulsar", "pulsar", "pul"),
            (NodeType.Magnetar, "Magnetar", "magnetar", "mag"),
        };

        // Solo el Asteroide y el Magnetar existen en Neutral (3.2): A.2 no dibuja Estrella ni Púlsar grises.
        public static bool HasNeutral(NodeType type) => type == NodeType.Asteroid || type == NodeType.Magnetar;

        public static string SpritePath(string folder, string prefix, int level, string faction) =>
            $"{SpritesFolder}/{folder}/{prefix}_lvl{level}_{faction}.png";

        public static string FootprintPath(string folder, string prefix) => $"{SpritesFolder}/{folder}/{prefix}_huella.json";

        [MenuItem("Nova/Arte/Asignar sprites de nodo")]
        public static void Assign()
        {
            foreach (var entry in Nodes)
            {
                var definition = AssetDatabase.LoadAssetAtPath<NodeDefinition>($"{NodesFolder}/{entry.asset}.asset");
                if (definition == null)
                {
                    Debug.LogError($"[Nova] Falta {NodesFolder}/{entry.asset}.asset.");
                    return;
                }
                NodeLevelArt[] art = Build(definition, entry.folder, entry.prefix, out string error);
                if (art == null)
                {
                    Debug.LogError($"[Nova] {error}");
                    return;
                }
                definition.ConfigureArt(art);
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nova] Sprites y huellas asignados a los {Nodes.Length} tipos de nodo.");
        }

        // El arte que le toca a un NodeDefinition, o null y el motivo. Sin efectos: lo usan también los tests.
        public static NodeLevelArt[] Build(NodeDefinition definition, string folder, string prefix, out string error)
        {
            FootprintExport footprints = ReadFootprints(folder, prefix);
            if (footprints == null)
            {
                error = $"Falta {FootprintPath(folder, prefix)}: ejecuta el generador y sincroniza con Nova → Arte → Sincronizar sprites desde art.";
                return null;
            }
            if (footprints.ppu != NodeLevelArt.PixelsPerUnit || footprints.levels == null || footprints.footprintPixels == null)
            {
                error = $"{FootprintPath(folder, prefix)} no es una huella a PPU {NodeLevelArt.PixelsPerUnit}.";
                return null;
            }

            var art = new NodeLevelArt[definition.ArtLevels];
            for (int i = 0; i < art.Length; i++)
            {
                int level = definition.MaxLevel == 0 ? 0 : i + 1;
                int index = Array.IndexOf(footprints.levels, level);
                if (index < 0 || index >= footprints.footprintPixels.Length)
                {
                    error = $"{FootprintPath(folder, prefix)} no trae la huella del nivel {level}.";
                    return null;
                }

                var sprites = new Sprite[Factions.Length];
                for (int f = 0; f < Factions.Length; f++)
                {
                    if (f == 0 && !HasNeutral(definition.Type))
                        continue;
                    string path = SpritePath(folder, prefix, level, Factions[f]);
                    sprites[f] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprites[f] == null)
                    {
                        error = $"Falta el sprite {path}.";
                        return null;
                    }
                }
                art[i] = new NodeLevelArt(sprites[0], sprites[1], sprites[2], footprints.footprintPixels[index]);
            }

            error = null;
            return art;
        }

        static FootprintExport ReadFootprints(string folder, string prefix)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(FootprintPath(folder, prefix));
            return json != null ? JsonUtility.FromJson<FootprintExport>(json.text) : null;
        }

        // El JSON de tools/nova_core.py, exporta_huellas.
        [Serializable]
        sealed class FootprintExport
        {
            public int ppu;
            public int[] levels;
            public int[] footprintPixels;
        }
    }
}
