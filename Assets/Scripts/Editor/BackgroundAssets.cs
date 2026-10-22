using System;
using System.IO;
using System.Linq;
using Nova.Data;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor
{
    // T5-05b: los tres fondos con movimiento, desde lo que exporta `py tools/gen_fondos_v2.py
    // --exporta` y ArtBridge copia a Assets/Sprites/bg/fondos/. Por mapa, un material con su shader
    // (Assets/Art/Shaders/BackgroundMap{n}.shader), sus texturas de datos y sus constantes, y un
    // BackgroundDefinition con las fases, la ruta del ovni y la grava, los dos en
    // Assets/Data/Backgrounds/. Idempotente: si el generador cambia, se vuelve a exportar,
    // sincronizar y generar, y lo retocado a mano se pierde. Al terminar, cada nivel recibe el fondo
    // de su mapa.
    public static class BackgroundAssets
    {
        public const string Folder = "Assets/Data/Backgrounds";
        public const string SpritesFolder = "Assets/Sprites/bg/fondos";
        public const int Maps = 3;

        public static string PathOf(int map) => $"{Folder}/Map{map}.asset";
        public static string MaterialPathOf(int map) => $"{Folder}/Map{map}.mat";
        public static string ExportPathOf(int map) => $"{SpritesFolder}/bg_section{map}.json";
        public static string ShaderOf(int map) => $"Nova/Background/Map{map}";

        [MenuItem("Nova/Arte/Generar fondos")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            for (int map = 1; map <= Maps; map++)
            {
                if (!GenerateMap(map))
                    return;
            }
            AssetDatabase.SaveAssets();
            LevelAssets.AssignBackgrounds();
            Debug.Log($"[Nova] Los {Maps} fondos generados en {Folder} y asignados a los niveles.");
        }

        // El JSON de un mapa, o null si ArtBridge todavía no lo ha copiado.
        public static BackgroundExport Read(int map)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(ExportPathOf(map));
            return json != null ? JsonUtility.FromJson<BackgroundExport>(json.text) : null;
        }

        static bool GenerateMap(int map)
        {
            BackgroundExport data = Read(map);
            if (data == null)
            {
                Debug.LogError($"[Nova] Falta {ExportPathOf(map)}: exporta con `py tools/gen_fondos_v2.py --exporta` y sincroniza con Nova → Arte → Sincronizar sprites desde art.");
                return false;
            }
            if (data.map != map || data.width != BackgroundDefinition.Width || data.height != BackgroundDefinition.Height)
            {
                Debug.LogError($"[Nova] {ExportPathOf(map)} no es el mapa {map} a {BackgroundDefinition.Width}x{BackgroundDefinition.Height}.");
                return false;
            }
            Shader shader = Shader.Find(ShaderOf(map));
            if (shader == null)
            {
                Debug.LogError($"[Nova] No se encuentra el shader {ShaderOf(map)}.");
                return false;
            }

            Material material = WriteMaterial(map, shader, data);
            if (material == null)
                return false;

            var frame = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/{data.frame}");
            if (frame == null)
            {
                Debug.LogError($"[Nova] Falta el sprite {SpritesFolder}/{data.frame}.");
                return false;
            }

            var definition = AssetDatabase.LoadAssetAtPath<BackgroundDefinition>(PathOf(map));
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BackgroundDefinition>();
                AssetDatabase.CreateAsset(definition, PathOf(map));
            }

            definition.Configure(map, material, frame,
                data.phases.Select(p => new BackgroundPhase(p.property, p.rate, p.period, p.offset)).ToArray(),
                data.lists.Select(l => new BackgroundPhaseList(l.property, l.rates, l.offsets, l.period)).ToArray(),
                data.axis);

            BackgroundExport.UfoEntry ufo = data.ufo;
            Sprite[] ufoFrames = Sheet(ufo.sheet);
            if (ufoFrames.Length != ufo.frames)
                Debug.LogWarning($"[Nova] {ufo.sheet}: {ufoFrames.Length} frames importados de {ufo.frames}.");
            definition.ConfigureUfo(ufoFrames, ufo.frames, ufo.fps, ufo.cell, ufo.speed, ufo.sway, ufo.swayPeriod,
                ufo.intervalMin, ufo.intervalMax, ufo.direction, ufo.startY, ufo.slope, ufo.surf, ufo.surfOffset);

            BackgroundExport.RocksEntry rocks = data.rocks;
            bool hasRocks = rocks != null && rocks.list != null && rocks.list.Length > 0;
            definition.ConfigureRocks(hasRocks ? Sheet(rocks.sheet) : Array.Empty<Sprite>(),
                hasRocks ? rocks.list.Select(r => new BackgroundRock(r.half, r.dy, r.x0, r.speed)).ToArray() : Array.Empty<BackgroundRock>());

            EditorUtility.SetDirty(definition);
            return true;
        }

        static Material WriteMaterial(int map, Shader shader, BackgroundExport data)
        {
            string path = MaterialPathOf(map);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;

            foreach (BackgroundExport.TextureEntry entry in data.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SpritesFolder}/{entry.file}");
                if (texture == null)
                {
                    Debug.LogError($"[Nova] Falta {SpritesFolder}/{entry.file}: sincroniza con Nova → Arte → Sincronizar sprites desde art.");
                    return null;
                }
                material.SetTexture(entry.property, texture);
            }
            foreach (BackgroundExport.FloatEntry entry in data.floats)
                material.SetFloat(entry.property, entry.value);
            foreach (BackgroundExport.VectorEntry entry in data.vectors)
                material.SetVector(entry.property, new Vector4(entry.value[0], entry.value[1], entry.value[2], entry.value[3]));
            foreach (BackgroundExport.ColorEntry entry in data.colors)
            {
                if (!ColorUtility.TryParseHtmlString(entry.value, out Color color))
                {
                    Debug.LogError($"[Nova] {ExportPathOf(map)}: {entry.property} = {entry.value} no es un color.");
                    return null;
                }
                material.SetColor(entry.property, color);
            }
            material.SetVector("_Window", new Vector4(0f, 0f, BackgroundDefinition.Width, BackgroundDefinition.Height));
            EditorUtility.SetDirty(material);
            return material;
        }

        // Los frames de una hoja, en orden: SpriteImportRules los nombra <hoja>_<i>.
        static Sprite[] Sheet(string file)
        {
            string path = $"{SpritesFolder}/{file}";
            string prefix = Path.GetFileNameWithoutExtension(file) + "_";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .Where(s => s.name.StartsWith(prefix))
                .OrderBy(s => int.Parse(s.name.Substring(prefix.Length)))
                .ToArray();
        }
    }
}
