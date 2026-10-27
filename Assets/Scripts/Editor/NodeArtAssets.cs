using System;
using System.Linq;
using System.Text.RegularExpressions;
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
    //
    // T6-02: también las hojas de idle (<prefijo>_lvl<n>_<facción>_sheet.png, A.2.2 y A.2.3), que son
    // opcionales —el Asteroide y el Magnetar no tienen—, y los nueve overlays de A.3 en NodeStateArt.
    public static class NodeArtAssets
    {
        public const string SpritesFolder = "Assets/Sprites/nodes";
        public const string StatesFolder = "Assets/Sprites/states";
        public const string NodesFolder = "Assets/Data/Nodes";
        public const string StateArtPath = "Assets/Data/NodeStateArt.asset";

        // <hoja>_<n>: el nombre que SpriteImportRules da a cada frame.
        static readonly Regex FrameIndex = new Regex(@"_(\d+)$");

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

        public static string IdleSheetPath(string folder, string prefix, int level, string faction) =>
            $"{SpritesFolder}/{folder}/{prefix}_lvl{level}_{faction}_sheet.png";

        // Los frames de una hoja en orden, o vacío si no existe. Cargar la textura devuelve los sprites en
        // el orden que quiera Unity; el orden bueno es el número del nombre.
        public static Sprite[] LoadFrames(string sheetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
                .Select(sprite => (sprite, match: FrameIndex.Match(sprite.name)))
                .Where(entry => entry.match.Success)
                .OrderBy(entry => int.Parse(entry.match.Groups[1].Value))
                .Select(entry => entry.sprite)
                .ToArray();
        }

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

            NodeStateArt states = LoadOrCreateStateArt();
            if (!ConfigureStates(states, out string stateError))
            {
                Debug.LogError($"[Nova] {stateError}");
                return;
            }
            EditorUtility.SetDirty(states);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nova] Sprites, huellas y hojas de idle asignados a los {Nodes.Length} tipos de nodo, y los overlays de estado a {StateArtPath}.");
        }

        public static NodeStateArt LoadOrCreateStateArt()
        {
            var states = AssetDatabase.LoadAssetAtPath<NodeStateArt>(StateArtPath);
            if (states != null)
                return states;
            states = ScriptableObject.CreateInstance<NodeStateArt>();
            AssetDatabase.CreateAsset(states, StateArtPath);
            return states;
        }

        // Los overlays de A.3 desde Assets/Sprites/states/, con los nombres de archivo de su columna *Archivo*.
        public static bool ConfigureStates(NodeStateArt states, out string error)
        {
            error = null;
            string missing = null;

            Sprite One(string path)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{StatesFolder}/{path}");
                if (sprite == null)
                    missing ??= path;
                return sprite;
            }

            Sprite[] Sheet(string path)
            {
                Sprite[] frames = LoadFrames($"{StatesFolder}/{path}");
                if (frames.Length == 0)
                    missing ??= path;
                return frames;
            }

            states.Configure(
                Sheet("sleep/state_sleep_sheet.png"),
                One("ign/state_unlit_player.png"), One("ign/state_unlit_enemy.png"),
                Sheet("ign/state_ready_player_sheet.png"), Sheet("ign/state_ready_enemy_sheet.png"),
                One("frozen/state_frozen_overlay.png"),
                Sheet("decay/state_decay_sheet.png"),
                Sheet("conv/state_convert_player_sheet.png"), Sheet("conv/state_convert_enemy_sheet.png"),
                Sheet("cap/state_captured_sheet.png"),
                One("buff/state_buff_rayo.png"), One("buff/state_buff_hidrogeno.png"));

            if (missing == null)
                return true;
            error = $"Falta {StatesFolder}/{missing}: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
            return false;
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
                var idle = new Sprite[Factions.Length][];
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
                    idle[f] = LoadFrames(IdleSheetPath(folder, prefix, level, Factions[f]));
                }
                art[i] = new NodeLevelArt(sprites[0], sprites[1], sprites[2], footprints.footprintPixels[index],
                    idle[0], idle[1], idle[2]);
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
