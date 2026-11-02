using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nova.Editor
{
    // T0-05: ajustes de importación de NOVA_Arte_Tecnica.md, «Importación en Unity»,
    // aplicados a todo lo que entra en Assets/Sprites/.
    // Las hojas (_sheet) son una tira horizontal de frames cuadrados: la celda mide el alto. Las de los
    // filamentos de plasma no —la línea de envío y los tres arcos del Magnetar—: son tres piezas de un
    // trazo en filas (A.4.6), y tienen su propio corte. T6-11: los tres haces de poder (A.4.2) tienen esa
    // misma hoja y se cortan igual.
    //
    // T6-04: las hojas del protón y las cifras de la Carga tampoco son tiras de frames cuadrados, y no
    // llevan el sufijo _sheet porque A.4.2 las nombra sin él. Se cortan por su nombre (ver abajo).
    //
    // T5-05b: las texturas de datos de los fondos con movimiento no son sprites. Los campos y los
    // puntos son números —densidades y fracciones a 8 bits— y se leen en lineal; lo fijo es color y
    // se lee en sRGB, como un sprite. Ninguna se filtra ni se comprime, y el alfa es dato: no se trata
    // como transparencia, que dejaría a Unity tocar el color de los texeles con alfa 0. Los shaders
    // las leen texel a texel (Load), así que filtro y repetición no intervienen.
    public class SpriteImportRules : AssetPostprocessor
    {
        const string SpritesRoot = "Assets/Sprites/";
        const string SheetSuffix = "_sheet";
        const int PixelsPerUnit = 64;

        // bg_section<n>_fields<k>, _points<k> y _static (tools/gen_fondos_v2.py, exporta()).
        static readonly Regex DataTexturePattern = new Regex(@"^bg_section\d+_(fields\d*|points\d*|static)$");

        // La hoja más ancha (ART-ST-CONV, 36 frames) mide 3456 px; con el máximo por
        // defecto de 2048 Unity la reduciría y rompería la rejilla.
        const int MaxTextureSize = 8192;

        // Subirla reimporta todo lo que pasa por aquí. 1: las texturas de datos (T5-05b); 2: todo en 2D;
        // 3: el protón y las cifras (T6-04); 4: el protón en diez tallas.
        public override uint GetVersion() => 4;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpritesRoot))
                return;

            var importer = (TextureImporter)assetImporter;
            // El .meta mínimo de ArtBridge (solo el GUID) lo completa Unity con la forma en cubemap,
            // y con ella no hay Texture2D ni Sprite que cargar. Todo lo de Assets/Sprites/ es 2D.
            importer.textureShape = TextureImporterShape.Texture2D;
            Match data = DataTexturePattern.Match(System.IO.Path.GetFileNameWithoutExtension(assetPath));
            if (data.Success)
            {
                ImportDataTexture(importer, data.Groups[1].Value == "static");
                return;
            }

            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool isSheet = fileName.EndsWith(SheetSuffix) || ProtonSheetPattern.IsMatch(fileName) || fileName == DigitsName;

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

        static void ImportDataTexture(TextureImporter importer, bool isColor)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = isColor;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = MaxTextureSize;
        }

        // Una pieza de un trazo por piezas (NOVA_Assets_Diseno.md, A.4.6): su ancho dentro de la celda y
        // dónde cae su pivote, en fracción de ese ancho.
        readonly struct StrokePiece
        {
            public readonly string Name;
            public readonly int Width;
            public readonly float PivotX;

            public StrokePiece(string name, int width, float pivotX)
            {
                Name = name;
                Width = width;
                PivotX = pivotX;
            }
        }

        // ART-WLD-LINK, ART-WLD-RAD-SHOT y ART-WLD-BEAM-*: tres filas de frames en celdas de 256 × 16, de arriba abajo
        // nacimiento, tramo y punta, cada una pegada al lado izquierdo de su celda (nova_core.Filamento).
        // Pivotes: el nacimiento en el centro del nodo, el tramo en el lado de la punta —se ancla allí y
        // se recorta del lado del nodo— y la punta en el punto de contacto, x = 24, que es el cursor o el
        // protón o el objetivo del haz. La línea tiene 16 frames, el arco 2 y el haz 3: salen del ancho de la hoja.
        static readonly Regex StrokeSheetPattern =
            new Regex(@"^world_(link|radius_shot_(player|enemy|neutral)|beam_(zero|decay|wormhole))_sheet$");
        const int LinkCell = 256;
        const int LinkHeight = 16;
        static readonly StrokePiece[] LinkPieces =
        {
            new StrokePiece("birth", 16, 0f),
            new StrokePiece("tile", 256, 1f),
            new StrokePiece("tip", 32, 24f / 32f),
        };

        // ART-WLD-PRO-P y -E (A.4.2, tools/gen_proton_v2.py): una hoja por bando y talla, de la 1 a la 10, en
        // celdas de 48 y dos filas. La de arriba, los 16 frames del cuerpo; la de abajo, los 4 pasos de la gota
        // y el resto vacío, que no se corta.
        static readonly Regex ProtonSheetPattern = new Regex(@"^world_proton_(player|enemy)_\d+$");
        const int ProtonCell = 48;
        const int ProtonDropSteps = 4;

        // Las cifras de la Carga: diez celdas de 7 × 9, de 0 a 9, con la cifra pegada a la izquierda y su
        // contorno dentro. Pivote abajo a la izquierda: ProtonVisual las coloca en píxeles enteros.
        const string DigitsName = "world_charge_digits";
        const int DigitWidth = 7;
        const int DigitCount = 10;

        static void SliceSheet(TextureImporter importer)
        {
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            string baseName = System.IO.Path.GetFileNameWithoutExtension(importer.assetPath);
            SpriteRect[] rects;
            if (ProtonSheetPattern.IsMatch(baseName))
            {
                if (height != ProtonCell * 2 || width % ProtonCell != 0 || width / ProtonCell < ProtonDropSteps)
                {
                    Debug.LogError($"[Nova] {importer.assetPath}: {width}x{height} no son dos filas de celdas de {ProtonCell}.");
                    return;
                }
                int bodyFrames = width / ProtonCell;
                var center = new Vector2(0.5f, 0.5f);
                rects = Enumerable.Range(0, bodyFrames)
                    .Select(i => Frame($"{baseName}_body_{i}", new Rect(i * ProtonCell, ProtonCell, ProtonCell, ProtonCell), center))
                    .Concat(Enumerable.Range(0, ProtonDropSteps)
                        .Select(i => Frame($"{baseName}_drop_{i}", new Rect(i * ProtonCell, 0, ProtonCell, ProtonCell), center)))
                    .ToArray();
            }
            else if (baseName == DigitsName)
            {
                if (width != DigitWidth * DigitCount)
                {
                    Debug.LogError($"[Nova] {importer.assetPath}: {width}x{height} no son {DigitCount} celdas de {DigitWidth} de ancho.");
                    return;
                }
                rects = Enumerable.Range(0, DigitCount)
                    .Select(i => Frame($"{baseName}_{i}", new Rect(i * DigitWidth, 0, DigitWidth, height), Vector2.zero))
                    .ToArray();
            }
            else if (StrokeSheetPattern.IsMatch(baseName))
            {
                if (width % LinkCell != 0 || height != LinkHeight * LinkPieces.Length)
                {
                    Debug.LogError($"[Nova] {importer.assetPath}: {width}x{height} no son tres filas de celdas de {LinkCell}x{LinkHeight}.");
                    return;
                }
                int linkFrames = width / LinkCell;
                // La fila 0 de la hoja es la de arriba; en la textura, y crece hacia arriba.
                rects = LinkPieces.SelectMany((piece, row) => Enumerable.Range(0, linkFrames).Select(i =>
                    Frame($"{baseName}_{piece.Name}_{i}",
                        new Rect(i * LinkCell, height - (row + 1) * LinkHeight, piece.Width, LinkHeight),
                        new Vector2(piece.PivotX, 0.5f)))).ToArray();
            }
            else
            {
                if (height <= 0 || width % height != 0)
                {
                    Debug.LogError($"[Nova] {importer.assetPath}: {width}x{height} no es una tira de frames cuadrados.");
                    return;
                }
                int frames = width / height;
                rects = Enumerable.Range(0, frames).Select(i =>
                    Frame($"{baseName}_{i}", new Rect(i * height, 0, height, height), new Vector2(0.5f, 0.5f))).ToArray();
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
        }

        static SpriteRect Frame(string name, Rect rect, Vector2 pivot) => new SpriteRect
        {
            name = name,
            rect = rect,
            alignment = pivot == new Vector2(0.5f, 0.5f) ? SpriteAlignment.Center : SpriteAlignment.Custom,
            pivot = pivot,
            // ID derivado del nombre: reimportar no cambia las referencias a cada frame.
            spriteID = StableGuid(name),
        };

        static GUID StableGuid(string name)
        {
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(name));
            return new GUID(string.Concat(hash.Select(b => b.ToString("x2"))));
        }
    }
}
