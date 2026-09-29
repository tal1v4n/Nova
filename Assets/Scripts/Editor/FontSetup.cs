using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Nova.Editor
{
    // T0-06: font asset de TextMeshPro para Jersey 10 (NOVA_Arte_Tecnica.md, «Importación en Unity»).
    // Jersey 10 dibuja sobre una rejilla de 75 unidades en un em de 1400: 18.67 px por em.
    // A 56 pt cada píxel de diseño son exactamente 3 del atlas, así que el atlas es nítido
    // y se muestra a tamaños múltiplos de 18.67 sin interpolar.
    public static class FontSetup
    {
        const string FontPath = "Assets/Art/Fonts/Jersey10-Regular.ttf";
        const string FontAssetPath = "Assets/Art/Fonts/Jersey10 Raster56.asset";
        const string BitmapShader = "TextMeshPro/Mobile/Bitmap";
        const string EssentialResources = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        const int SamplingPointSize = 56;
        const int AtlasPadding = 3; // un píxel de diseño
        const int AtlasSize = 1024;

        // ASCII imprimible más lo que exige la UI en español y los signos del HUD.
        static readonly string Characters =
            new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray())
            + "¡¿ÁÉÍÓÚÑÜáéíóúñü·×";

        [MenuItem("Nova/Fase 0/Crear font asset de Jersey 10")]
        public static void Create()
        {
            // Los shaders de TMP viven en sus recursos esenciales, que la plantilla no importa.
            if (Shader.Find(BitmapShader) == null)
            {
                UnityEditor.AssetPackage.Package.Import(System.IO.Path.GetFullPath(EssentialResources), false);
                Debug.LogWarning("[Nova] Importados los recursos esenciales de TextMeshPro. " +
                                 "Espera a que termine la importación y vuelve a lanzar este menú.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null)
            {
                Debug.LogWarning($"[Nova] {FontAssetPath} ya existe. Regenerarlo cambiaría su GUID; " +
                                 "bórralo a mano si de verdad quieres rehacerlo.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogError($"[Nova] No se encuentra {FontPath}.");
                return;
            }

            // Se crea dinámico para poder añadir caracteres y se congela como estático:
            // el juego no genera glifos en tiempo de ejecución.
            var fontAsset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding,
                GlyphRenderMode.RASTER, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            fontAsset.TryAddCharacters(Characters, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[Nova] Jersey 10 no tiene estos caracteres: «{missing}».");
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            Texture2D atlas = fontAsset.atlasTexture;
            atlas.filterMode = FilterMode.Point;
            atlas.name = "Jersey10 Atlas";
            fontAsset.material.name = "Jersey10 Material";

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Nova] Creado {FontAssetPath}: {fontAsset.characterTable.Count} caracteres a " +
                      $"{SamplingPointSize} pt en un atlas {atlas.width}x{atlas.height}.");
        }
    }
}
