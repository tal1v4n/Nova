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
    // T6-07: las quince hojas de A.5 (fx_<efecto>_sheet) tampoco: son rejillas de filas por columnas, y la
    // celda no siempre mide 96 —el protón usa 48 y 64—. La mide el .json que el generador deja al lado, y que
    // ArtBridge copia con la hoja.
    //
    // T6-08: las piezas del HUD (A.6) se colocan por su esquina de arriba a la izquierda, en píxeles de la
    // pantalla de referencia: todas llevan ahí el pivote. La placa de la barra de arriba lleva sus nueve cortes,
    // y las tres hojas —rellenos y frente de la barra de poder y el botón de pausa— son rejillas cuya celda
    // dice el .json del generador, que va en la misma carpeta con otro nombre (ui_power_bar.json, ui_topbar.json).
    //
    // T6-15: el icono de mejora (A.6, ART-UI-BTN-UPG) es una rejilla de celdas de 29 × 20 —fila = estado,
    // columna = frame— que mide ui_upgrade_icon.json, con el pivote abajo al centro: va encima de la huella.
    //
    // T6-09: las piezas del panel, la rueda, el menú de encendido, el número de Energía y la barra de carga (A.6)
    // no se colocan como sprites: las vistas leen sus píxeles y los componen en un PixelCanvas, como los
    // generadores. Se importan legibles (Read/Write) y se cortan en rejillas con la celda de UiGrids —la de sus
    // .json—, con cada celda llamada <pieza>_<fila>_<columna> y el pivote arriba a la izquierda.
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
        // 3: el protón y las cifras (T6-04); 4: el protón en diez tallas; 5: el lazo (T6-06); 6: las hojas de A.5 (T6-07);
        // 7: el HUD (T6-08); 8: el icono de mejora (T6-15); 9: el panel, la rueda, el menú y los números (T6-09).
        public override uint GetVersion() => 9;

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
            bool uiPiece = UiGrids.ContainsKey(fileName) || UiSingles.Contains(fileName);
            bool isSheet = fileName.EndsWith(SheetSuffix) || ProtonSheetPattern.IsMatch(fileName) || fileName == DigitsName
                           || UiGrids.ContainsKey(fileName);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = isSheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = MaxTextureSize;
            // T6-09: las vistas leen los píxeles de estas piezas.
            importer.isReadable = uiPiece;

            // Pivote Center: en NOVA nada se apoya en un suelo (Bottom no aplica a ningún asset). La esquina del
            // lazo (T6-06) es la excepción: se coloca por su esquina de arriba a la izquierda y se refleja allí.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool topLeft = fileName == LassoCornerName || HudPattern.IsMatch(fileName) || uiPiece;
            settings.spriteAlignment = (int)(topLeft ? SpriteAlignment.TopLeft : SpriteAlignment.Center);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            // La placa del HUD: nueve cortes, con el borde de su .json (la escalera más el marco).
            if (fileName == HudPlateName)
            {
                int border = HudExport(assetPath, HudTopBarJson)?.plateBorder ?? 0;
                settings.spriteBorder = new Vector4(border, border, border, border);
            }
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

        // ART-WLD-LASSO (A.4.3, tools/gen_lasso_v1.py): el tramo es una tira de frames de 8 × 1, no cuadrados, con el
        // pivote a la izquierda y centrado en su fila; LassoView lo coloca en mosaico desde la esquina de la que sale.
        const string LassoEdgeName = "world_lasso_edge_sheet";
        const string LassoCornerName = "world_lasso_corner";
        const int LassoTile = 8;

        // Las cifras de la Carga: diez celdas de 7 × 9, de 0 a 9, con la cifra pegada a la izquierda y su
        // contorno dentro. Pivote abajo a la izquierda: ProtonVisual las coloca en píxeles enteros.
        const string DigitsName = "world_charge_digits";
        const int DigitWidth = 7;
        const int DigitCount = 10;

        // ART-FX-* (A.5): fx_<efecto>_sheet.png, una rejilla de celdas cuadradas. Cada frame se llama
        // <hoja>_<fila>_<columna>, con la fila 0 arriba como en el generador; qué es cada fila y cada columna lo
        // dice el mismo .json, y lo lee NodeArtAssets.
        static readonly Regex EffectSheetPattern = new Regex(@"^fx_[a-z_]+_sheet$");

        [System.Serializable]
        sealed class EffectSheetExport
        {
            public int cellSize;
        }

        // La celda de una hoja de A.5, del .json que la acompaña, o 0 si no está.
        static int EffectCellSize(string assetPath)
        {
            string json = System.IO.Path.ChangeExtension(assetPath, ".json");
            if (!System.IO.File.Exists(json))
                return 0;
            EffectSheetExport export = JsonUtility.FromJson<EffectSheetExport>(System.IO.File.ReadAllText(json));
            return export != null ? export.cellSize : 0;
        }

        // ART-UI-POWERBAR y ART-UI-TOPBAR (A.6): todo lo suyo se coloca por la esquina de arriba a la izquierda.
        static readonly Regex HudPattern = new Regex(@"^ui_(power_bar|topbar)_");
        const string HudPlateName = "ui_topbar_plate";
        const string HudPowerBarJson = "ui_power_bar.json";
        const string HudTopBarJson = "ui_topbar.json";

        // Lo que hace falta de los .json de A.6 para cortar las hojas.
        [System.Serializable]
        sealed class HudSheetExport
        {
            public int height;
            public int tileWidth;
            public int frontWidth;
            public int plateBorder;
            public int[] pauseCell;
        }

        static HudSheetExport HudExport(string assetPath, string jsonName)
        {
            string json = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assetPath), jsonName);
            return System.IO.File.Exists(json) ? JsonUtility.FromJson<HudSheetExport>(System.IO.File.ReadAllText(json)) : null;
        }

        // La celda de una hoja del HUD (ancho, alto), o (0, 0) si su .json no está.
        static Vector2Int HudCell(string assetPath, string baseName)
        {
            if (baseName == "ui_topbar_pause_sheet")
            {
                HudSheetExport top = HudExport(assetPath, HudTopBarJson);
                return top?.pauseCell != null && top.pauseCell.Length == 2 ? new Vector2Int(top.pauseCell[0], top.pauseCell[1]) : default;
            }
            HudSheetExport bar = HudExport(assetPath, HudPowerBarJson);
            if (bar == null)
                return default;
            return new Vector2Int(baseName == "ui_power_bar_front_sheet" ? bar.frontWidth : bar.tileWidth, bar.height);
        }

        static readonly Regex HudSheetPattern = new Regex(@"^ui_(power_bar_(fill|front)|topbar_pause)_sheet$");

        // ART-UI-BTN-UPG (T6-15): la hoja y su .json, que no se llama como ella.
        const string UpgradeIconSheetName = "ui_upgrade_icon_sheet";
        const string UpgradeIconJson = "ui_upgrade_icon.json";

        [System.Serializable]
        sealed class UpgradeIconExport
        {
            public int cellWidth;
            public int cellHeight;
        }

        static Vector2Int UpgradeIconCell(string assetPath)
        {
            string json = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assetPath), UpgradeIconJson);
            if (!System.IO.File.Exists(json))
                return default;
            UpgradeIconExport export = JsonUtility.FromJson<UpgradeIconExport>(System.IO.File.ReadAllText(json));
            return export != null ? new Vector2Int(export.cellWidth, export.cellHeight) : default;
        }

        // T6-09: la celda de cada pieza de A.6 que se compone en CPU, la de su .json (ancho, alto). Una por fila y
        // columna: <pieza>_<fila>_<columna>.
        static readonly System.Collections.Generic.Dictionary<string, Vector2Int> UiGrids =
            new System.Collections.Generic.Dictionary<string, Vector2Int>
            {
                // ART-UI-PANEL «La sonda»: una fila por bando (Jugador, Enemigo, neutral).
                { "ui_info_panel_corner", new Vector2Int(8, 8) },
                { "ui_info_panel_edge", new Vector2Int(2, 1) },
                { "ui_info_panel_tether", new Vector2Int(8, 3) },
                { "ui_info_panel_pulse", new Vector2Int(3, 3) },
                { "ui_info_panel_socket", new Vector2Int(3, 7) },
                { "ui_info_panel_divider", new Vector2Int(4, 1) },
                { "ui_info_panel_rule", new Vector2Int(4, 3) },
                // ART-UI-HUD-ENERGY «El filamento»: fila = cifra, columna = frame; lo neutral, una fila.
                { "ui_energy_label_player_sheet", new Vector2Int(12, 16) },
                { "ui_energy_label_enemy_sheet", new Vector2Int(12, 16) },
                { "ui_energy_label_neutral", new Vector2Int(12, 16) },
                // ART-UI-CHARGE «La mecha».
                { "ui_charge_socket", new Vector2Int(2, 6) },
                { "ui_charge_track", new Vector2Int(2, 6) },
                { "ui_charge_fuse_sheet", new Vector2Int(8, 6) },
                { "ui_charge_flame_sheet", new Vector2Int(5, 6) },
                // ART-UI-BTN-CONV «El barrido».
                { "ui_btn_convert_plate", new Vector2Int(20, 20) },
                { "ui_btn_convert_front", new Vector2Int(5, 16) },
                { "ui_btn_convert_fill", new Vector2Int(8, 8) },
                // ART-UI-WHEEL «Las teclas».
                { "ui_power_wheel_plate", new Vector2Int(20, 20) },
                { "ui_power_wheel_fill", new Vector2Int(8, 8) },
                { "ui_power_wheel_hover", new Vector2Int(8, 8) },
                { "ui_power_wheel_icons", new Vector2Int(12, 12) },
                // ART-UI-IGN-MENU «Los nodos».
                { "ui_ignition_menu_corner", new Vector2Int(7, 7) },
                { "ui_ignition_menu_nodes", new Vector2Int(72, 72) },
            };

        // Las piezas de una sola celda.
        static readonly System.Collections.Generic.HashSet<string> UiSingles =
            new System.Collections.Generic.HashSet<string> { "ui_info_panel_screen", "ui_power_wheel_lock" };

        static void SliceSheet(TextureImporter importer)
        {
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            string baseName = System.IO.Path.GetFileNameWithoutExtension(importer.assetPath);
            SpriteRect[] rects;
            if (UiGrids.TryGetValue(baseName, out Vector2Int uiCell))
            {
                if (width % uiCell.x != 0 || height % uiCell.y != 0)
                {
                    Debug.LogError($"[Nova] {importer.assetPath}: {width}x{height} no es una rejilla de celdas de {uiCell.x}x{uiCell.y}.");
                    return;
                }
                int rows = height / uiCell.y;
                int columns = width / uiCell.x;
                var topLeft = new Vector2(0f, 1f);
                rects = Enumerable.Range(0, rows).SelectMany(row => Enumerable.Range(0, columns).Select(column =>
                    Frame($"{baseName}_{row}_{column}", new Rect(column * uiCell.x, height - (row + 1) * uiCell.y, uiCell.x, uiCell.y), topLeft)))
                    .ToArray();
            }
            else if (HudSheetPattern.IsMatch(baseName))
            {
                Vector2Int cell = HudCell(importer.assetPath, baseName);
                if (cell.x <= 0 || cell.y <= 0 || width % cell.x != 0 || height % cell.y != 0)
                {
                    Debug.LogError($"[Nova] {importer.assetPath}: {width}x{height} no es una rejilla de celdas de {cell.x}x{cell.y} (falta o no cuadra su .json).");
                    return;
                }
                int rows = height / cell.y;
                int columns = width / cell.x;
                var topLeft = new Vector2(0f, 1f);
                // <hoja>_<fila>_<columna>, con la fila 0 arriba como en el generador.
                rects = Enumerable.Range(0, rows).SelectMany(row => Enumerable.Range(0, columns).Select(column =>
                    Frame($"{baseName}_{row}_{column}", new Rect(column * cell.x, height - (row + 1) * cell.y, cell.x, cell.y), topLeft)))
                    .ToArray();
            }
            else if (baseName == UpgradeIconSheetName)
            {
                Vector2Int cell = UpgradeIconCell(importer.assetPath);
                if (cell.x <= 0 || cell.y <= 0 || width % cell.x != 0 || height % cell.y != 0)
                {
                    Debug.LogError($"[Nova] {importer.assetPath}: {width}x{height} no es una rejilla de celdas de {cell.x}x{cell.y} (falta o no cuadra {UpgradeIconJson}).");
                    return;
                }
                int rows = height / cell.y;
                int columns = width / cell.x;
                // Abajo al centro, en la columna cell.x / 2 entera, como lo coloca el generador.
                var bottomCenter = new Vector2((cell.x / 2) / (float)cell.x, 0f);
                rects = Enumerable.Range(0, rows).SelectMany(row => Enumerable.Range(0, columns).Select(column =>
                    Frame($"{baseName}_{row}_{column}", new Rect(column * cell.x, height - (row + 1) * cell.y, cell.x, cell.y), bottomCenter)))
                    .ToArray();
            }
            else if (EffectSheetPattern.IsMatch(baseName))
            {
                int cell = EffectCellSize(importer.assetPath);
                if (cell <= 0 || width % cell != 0 || height % cell != 0)
                {
                    Debug.LogError($"[Nova] {importer.assetPath}: {width}x{height} no es una rejilla de celdas de {cell} (falta o no cuadra su .json).");
                    return;
                }
                int rows = height / cell;
                int columns = width / cell;
                var center = new Vector2(0.5f, 0.5f);
                // La fila 0 de la hoja es la de arriba; en la textura, y crece hacia arriba.
                rects = Enumerable.Range(0, rows).SelectMany(row => Enumerable.Range(0, columns).Select(column =>
                    Frame($"{baseName}_{row}_{column}", new Rect(column * cell, height - (row + 1) * cell, cell, cell), center)))
                    .ToArray();
            }
            else if (ProtonSheetPattern.IsMatch(baseName))
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
            else if (baseName == LassoEdgeName)
            {
                if (height != 1 || width % LassoTile != 0)
                {
                    Debug.LogError($"[Nova] {importer.assetPath}: {width}x{height} no son frames de {LassoTile}x1.");
                    return;
                }
                rects = Enumerable.Range(0, width / LassoTile)
                    .Select(i => Frame($"{baseName}_{i}", new Rect(i * LassoTile, 0, LassoTile, 1), new Vector2(0f, 0.5f)))
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
