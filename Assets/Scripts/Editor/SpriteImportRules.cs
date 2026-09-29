using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nova.Editor
{
    // T0-05: ajustes de importación de NOVA_Arte_Tecnica.md, «Importación en Unity»,
    // aplicados a todo lo que entra en Assets/Sprites/.
    // Las hojas (_sheet) son una tira horizontal de frames cuadrados: la celda mide el alto.
    public class SpriteImportRules : AssetPostprocessor
    {
        const string SpritesRoot = "Assets/Sprites/";
        const string SheetSuffix = "_sheet";
        const int PixelsPerUnit = 64;

        // La hoja más ancha (ART-ST-CONV, 36 frames) mide 3456 px; con el máximo por
        // defecto de 2048 Unity la reduciría y rompería la rejilla.
        const int MaxTextureSize = 8192;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpritesRoot))
                return;

            var importer = (TextureImporter)assetImporter;
            bool isSheet = System.IO.Path.GetFileNameWithoutExtension(assetPath).EndsWith(SheetSuffix);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = isSheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = MaxTextureSize;

            // Pivote Center: en NOVA nada se apoya en un suelo (Bottom no aplica a ningún asset).
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            if (isSheet)
                SliceSheet(importer);
        }

        static void SliceSheet(TextureImporter importer)
        {
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (height <= 0 || width % height != 0)
            {
                Debug.LogError($"[Nova] {importer.assetPath}: {width}x{height} no es una tira de frames cuadrados.");
                return;
            }

            string baseName = System.IO.Path.GetFileNameWithoutExtension(importer.assetPath);
            int frames = width / height;
            var rects = Enumerable.Range(0, frames).Select(i => new SpriteRect
            {
                name = $"{baseName}_{i}",
                rect = new Rect(i * height, 0, height, height),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                // ID derivado del nombre: reimportar no cambia las referencias a cada frame.
                spriteID = StableGuid($"{baseName}_{i}"),
            }).ToArray();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
        }

        static GUID StableGuid(string name)
        {
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(name));
            return new GUID(string.Concat(hash.Select(b => b.ToString("x2"))));
        }
    }
}
