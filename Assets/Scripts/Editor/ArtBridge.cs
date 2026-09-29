using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor
{
    // T0-05: copia a Assets/Sprites/ el arte elegido en art/.
    // La versión elegida la marca NOVA_Assets_Diseno.md (A.1), no la carpeta, así que la lista
    // se deriva de sus filas ✅ en vez de mantenerse a mano en un segundo sitio.
    // Assets/Sprites/ es salida derivada: este script la reescribe entera y borra lo que sobra.
    public static class ArtBridge
    {
        const string InventoryPath = "mds/NOVA_Assets_Diseno.md";
        const string ArtRoot = "art";
        const string SpritesRoot = "Assets/Sprites";

        // Ruta de arte dentro de una fila de tabla: ../art/<ruta>.png
        static readonly Regex ArtPathPattern = new Regex(@"\.\./art/([^)\s]+?\.png)");

        // art/<familia>/<asset>/v<n>/<archivo>. La versión desaparece del destino para que
        // cambiar de versión no cambie las rutas (ni los GUID) dentro de Unity.
        static readonly Regex VersionedPattern = new Regex(@"^(?<asset>[^/]+/[^/]+)/v\d+/");

        [MenuItem("Nova/Arte/Sincronizar sprites desde art")]
        public static void Sync()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string inventory = Path.Combine(projectRoot, InventoryPath);
            if (!File.Exists(inventory))
            {
                Debug.LogError($"[Nova] No se encuentra {InventoryPath}.");
                return;
            }

            Dictionary<string, string> plan;
            try
            {
                plan = BuildPlan(File.ReadAllLines(inventory), Path.Combine(projectRoot, ArtRoot));
            }
            catch (InvalidDataException e)
            {
                Debug.LogError($"[Nova] {e.Message}");
                return;
            }

            string spritesRoot = Path.Combine(projectRoot, SpritesRoot);
            int copied = 0, unchanged = 0, removed = 0;

            foreach (var (destination, source) in plan)
            {
                string target = Path.Combine(spritesRoot, destination);
                if (File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(source)))
                {
                    unchanged++;
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, true);
                WriteStableMeta(target, $"{SpritesRoot}/{destination}");
                copied++;
            }

            if (Directory.Exists(spritesRoot))
            {
                foreach (string file in Directory.GetFiles(spritesRoot, "*.png", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(spritesRoot, file).Replace('\\', '/');
                    if (plan.ContainsKey(relative))
                        continue;
                    File.Delete(file);
                    if (File.Exists(file + ".meta"))
                        File.Delete(file + ".meta");
                    removed++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Nova] Sprites sincronizados: {copied} copiados, {unchanged} sin cambios, {removed} eliminados.");
        }

        // Devuelve destino relativo a Assets/Sprites → origen absoluto en art/.
        // Un asset versionado se copia con su carpeta entera (sprite suelto y hoja van juntos);
        // uno sin versión (world/, ui/, bg/) se copia archivo a archivo.
        static Dictionary<string, string> BuildPlan(string[] inventoryLines, string artRoot)
        {
            var plan = new Dictionary<string, string>();
            var chosenVersion = new Dictionary<string, string>();

            foreach (string line in inventoryLines)
            {
                if (!IsDoneRow(line))
                    continue;

                foreach (Match match in ArtPathPattern.Matches(line))
                {
                    string artPath = match.Groups[1].Value;
                    Match versioned = VersionedPattern.Match(artPath);

                    if (!versioned.Success)
                    {
                        AddFile(plan, artRoot, artPath, artPath);
                        continue;
                    }

                    string asset = versioned.Groups["asset"].Value;
                    string versionDir = versioned.Value.TrimEnd('/');
                    if (chosenVersion.TryGetValue(asset, out string previous))
                    {
                        if (previous != versionDir)
                            throw new InvalidDataException(
                                $"El inventario marca dos versiones de {asset}: {previous} y {versionDir}.");
                        continue;
                    }
                    chosenVersion[asset] = versionDir;

                    string sourceDir = Path.Combine(artRoot, versionDir);
                    if (!Directory.Exists(sourceDir))
                        throw new InvalidDataException($"No existe art/{versionDir}.");
                    foreach (string file in Directory.GetFiles(sourceDir, "*.png"))
                        AddFile(plan, artRoot, $"{versionDir}/{Path.GetFileName(file)}",
                            $"{asset}/{Path.GetFileName(file)}");
                }
            }

            return plan;
        }

        static void AddFile(Dictionary<string, string> plan, string artRoot, string artPath, string destination)
        {
            string source = Path.Combine(artRoot, artPath);
            if (!File.Exists(source))
                throw new InvalidDataException($"El inventario cita art/{artPath} y no existe.");
            plan[destination] = source;
        }

        // Assets/Sprites/ no se versiona, sus .meta tampoco: si Unity inventase el GUID, cada
        // clon del repo tendría otro y los prefabs perderían sus sprites. Se escribe un .meta
        // mínimo con un GUID derivado de la ruta y Unity completa el resto al importar.
        static void WriteStableMeta(string target, string assetPath)
        {
            string meta = target + ".meta";
            if (File.Exists(meta))
                return;

            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(assetPath));
            string guid = string.Concat(hash.Select(b => b.ToString("x2")));
            File.WriteAllText(meta, $"fileFormatVersion: 2\nguid: {guid}\n");
        }

        // Fila de tabla con alguna celda exactamente ✅.
        static bool IsDoneRow(string line)
        {
            return line.StartsWith("|") && line.Split('|').Any(cell => cell.Trim() == "✅");
        }
    }
}
