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
    //
    // T6-05: y el arte de mundo del Magnetar en MagnetarArt —sus 15 anillos de radio y sus tres arcos de
    // disparo, de Assets/Sprites/world/radius/ y world/shot/ (A.4.1)—.
    //
    // T6-04: y el del protón en ProtonArt —las veinte hojas de A.4.2, diez tallas por bando, y las cifras
    // de la Carga, de Assets/Sprites/world/proton/—.
    //
    // T6-11: y el de los haces de poder en PowerBeamArt —las tres hojas de A.4.2, una por poder, de
    // Assets/Sprites/world/beam_zero/, beam_decay/ y beam_wormhole/—.
    //
    // T6-07: y el de los efectos puntuales en VfxArt —las quince hojas de A.5, de Assets/Sprites/fx/—.
    public static class NodeArtAssets
    {
        public const string SpritesFolder = "Assets/Sprites/nodes";
        public const string StatesFolder = "Assets/Sprites/states";
        public const string NodesFolder = "Assets/Data/Nodes";
        public const string StateArtPath = "Assets/Data/NodeStateArt.asset";
        public const string WorldFolder = "Assets/Sprites/world";
        public const string MagnetarArtPath = "Assets/Data/MagnetarArt.asset";
        public const string ProtonArtPath = "Assets/Data/ProtonArt.asset";
        public const string PowerBeamArtPath = "Assets/Data/PowerBeamArt.asset";
        public const string InputArtPath = "Assets/Data/InputArt.asset";
        public const string VfxArtPath = "Assets/Data/VfxArt.asset";
        public const string EffectsFolder = "Assets/Sprites/fx";
        public const string HudArtPath = "Assets/Data/HudArt.asset";
        public const string UiFolder = "Assets/Sprites/ui";

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

            MagnetarArt magnetar = LoadOrCreate<MagnetarArt>(MagnetarArtPath);
            if (!ConfigureMagnetar(magnetar, out string magnetarError))
            {
                Debug.LogError($"[Nova] {magnetarError}");
                return;
            }
            EditorUtility.SetDirty(magnetar);

            ProtonArt proton = LoadOrCreate<ProtonArt>(ProtonArtPath);
            if (!ConfigureProton(proton, out string protonError))
            {
                Debug.LogError($"[Nova] {protonError}");
                return;
            }
            EditorUtility.SetDirty(proton);

            PowerBeamArt beams = LoadOrCreate<PowerBeamArt>(PowerBeamArtPath);
            if (!ConfigureBeams(beams, out string beamError))
            {
                Debug.LogError($"[Nova] {beamError}");
                return;
            }
            EditorUtility.SetDirty(beams);

            InputArt input = LoadOrCreate<InputArt>(InputArtPath);
            if (!ConfigureInput(input, out string inputError))
            {
                Debug.LogError($"[Nova] {inputError}");
                return;
            }
            EditorUtility.SetDirty(input);

            VfxArt effects = LoadOrCreate<VfxArt>(VfxArtPath);
            if (!ConfigureVfx(effects, out string vfxError))
            {
                Debug.LogError($"[Nova] {vfxError}");
                return;
            }
            EditorUtility.SetDirty(effects);

            HudArt hud = LoadOrCreate<HudArt>(HudArtPath);
            if (!ConfigureHud(hud, out string hudError))
            {
                Debug.LogError($"[Nova] {hudError}");
                return;
            }
            EditorUtility.SetDirty(hud);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nova] Sprites, huellas y hojas de idle asignados a los {Nodes.Length} tipos de nodo, los overlays de estado a {StateArtPath}, el radio y el arco del Magnetar a {MagnetarArtPath}, el protón a {ProtonArtPath}, los haces de poder a {PowerBeamArtPath}, la línea, la selección, el lazo y la retícula a {InputArtPath} y los quince efectos a {VfxArtPath} y el HUD a {HudArtPath}.");
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static string RingPath(string faction, int level) => $"{WorldFolder}/radius/world_radius_{faction}_lvl{level}.png";

        public static string ShotSheetPath(string faction) => $"{WorldFolder}/shot/world_radius_shot_{faction}_sheet.png";

        // Los anillos y los arcos de A.4.1 desde Assets/Sprites/world/, con los nombres de su columna *Archivo*.
        // Los frames de cada pieza del arco son los que corta SpriteImportRules: <hoja>_<pieza>_<n>.
        public static bool ConfigureMagnetar(MagnetarArt magnetar, out string error)
        {
            error = null;
            string missing = null;

            Sprite[] Rings(string faction)
            {
                var rings = new Sprite[MagnetarArt.Levels];
                for (int level = 1; level <= rings.Length; level++)
                {
                    rings[level - 1] = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath(faction, level));
                    if (rings[level - 1] == null)
                        missing ??= RingPath(faction, level);
                }
                return rings;
            }

            MagnetarArt.ShotFrames Shot(string faction)
            {
                string path = ShotSheetPath(faction);
                Sprite[] Piece(string piece) => LoadFrames(path)
                    .Where(sprite => sprite.name.Contains($"_{piece}_")).ToArray();
                var shot = new MagnetarArt.ShotFrames(Piece("birth"), Piece("tile"), Piece("tip"));
                if (!shot.IsComplete)
                    missing ??= path;
                return shot;
            }

            magnetar.Configure(Rings("neutral"), Rings("player"), Rings("enemy"),
                Shot("neutral"), Shot("player"), Shot("enemy"));

            if (missing == null)
                return true;
            error = $"Falta {missing} o no está cortada: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
            return false;
        }

        // La carpeta y el nombre de cada haz, tal como los copia ArtBridge (A.4.2, columna *Archivo*).
        public static string BeamSheetPath(string name) => $"{WorldFolder}/beam_{name}/world_beam_{name}_sheet.png";

        // Los tres haces de A.4.2. Los frames de cada pieza son los que corta SpriteImportRules, con la hoja
        // de la línea de envío: <hoja>_<pieza>_<n>.
        public static bool ConfigureBeams(PowerBeamArt beams, out string error)
        {
            error = null;
            string missing = null;

            PowerBeamArt.BeamFrames Beam(string name)
            {
                string path = BeamSheetPath(name);
                Sprite[] frames = LoadFrames(path);
                Sprite[] Piece(string piece) => frames.Where(sprite => sprite.name.Contains($"_{piece}_")).ToArray();
                var beam = new PowerBeamArt.BeamFrames(Piece("birth"), Piece("tile"), Piece("tip"));
                if (!beam.IsComplete)
                    missing ??= path;
                return beam;
            }

            beams.Configure(Beam("zero"), Beam("decay"), Beam("wormhole"));

            if (missing == null)
                return true;
            error = $"Falta {missing} o no está cortada: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
            return false;
        }

        // T6-06: el arte de la entrada (A.4.3), tal como lo copia ArtBridge.
        public const string LinkSheetPath = WorldFolder + "/link/world_link_sheet.png";
        public const string SelectSizesJsonPath = WorldFolder + "/select/world_select_tallas.json";
        public static string RingSheetPath(int radius) => $"{WorldFolder}/select/world_select_{radius}_sheet.png";
        public const string LassoCornerPath = WorldFolder + "/lasso/world_lasso_corner.png";
        public const string LassoEdgePath = WorldFolder + "/lasso/world_lasso_edge_sheet.png";
        public static string AimSheetPath(string power, int radius) => $"{WorldFolder}/aim/world_aim_{power}_{radius}_sheet.png";

        // La línea de envío, las tallas del anillo de selección, las piezas del lazo y las quince hojas de la
        // retícula. Cuántas tallas hay y su radio vienen de world_select_tallas.json; la retícula usa las mismas.
        public static bool ConfigureInput(InputArt input, out string error)
        {
            error = null;
            string missing = null;

            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(SelectSizesJsonPath);
            SelectSizesExport tallas = json != null ? JsonUtility.FromJson<SelectSizesExport>(json.text) : null;
            if (tallas?.radios == null || tallas.radios.Length == 0 || tallas.aire < 0)
            {
                error = $"Falta {SelectSizesJsonPath} o no está completo: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
                return false;
            }

            InputArt.Frames Sheet(string path)
            {
                var frames = new InputArt.Frames(LoadFrames(path));
                if (!frames.IsComplete)
                    missing ??= path;
                return frames;
            }

            Sprite[] link = LoadFrames(LinkSheetPath);
            InputArt.Frames LinkPiece(string piece)
            {
                var frames = new InputArt.Frames(link.Where(sprite => sprite.name.Contains($"_{piece}_")).ToArray());
                if (!frames.IsComplete)
                    missing ??= LinkSheetPath;
                return frames;
            }

            InputArt.Frames[] PerSize(System.Func<int, string> path) =>
                tallas.radios.Select(radius => Sheet(path(radius))).ToArray();

            var corner = AssetDatabase.LoadAssetAtPath<Sprite>(LassoCornerPath);
            if (corner == null)
                missing ??= LassoCornerPath;

            input.Configure(LinkPiece("birth"), LinkPiece("tile"), LinkPiece("tip"),
                tallas.aire, tallas.radios, PerSize(RingSheetPath),
                corner, Sheet(LassoEdgePath),
                PerSize(r => AimSheetPath("zero", r)), PerSize(r => AimSheetPath("decay", r)),
                PerSize(r => AimSheetPath("wormhole", r)));

            if (missing == null)
                return true;
            error = $"Falta {missing} o no está cortada: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
            return false;
        }

        // El JSON de tools/gen_select_v1.py, exporta_tallas.
        [Serializable]
        sealed class SelectSizesExport
        {
            public int ppu;
            public int aire;
            public int[] radios;
        }

        // T6-08: las piezas del HUD (A.6), tal como las copia ArtBridge: ui/power_bar/ y ui/topbar/, con su .json.
        public const string PowerBarFolder = UiFolder + "/power_bar";
        public const string TopBarFolder = UiFolder + "/topbar";

        [Serializable]
        sealed class PowerBarExport
        {
            public int height;
            public int x0;
            public int x1;
            public int snap;
            public int frames;
            public float fps;
            public int frontContact;
        }

        [Serializable]
        sealed class TopBarExport
        {
            public int margin;
            public int top;
            public int height;
            public int plateBorder;
            public int padX;
            public int gap;
            public int plateWidthStep;
            public int letterSpacing;
            public string levelText;
            public string timerText;
            public string timerTemplate;
            public int[] pauseCell;
        }

        // Las celdas <hoja>_<fila>_<columna> de una fila, en orden de columna.
        static Sprite[] HudRow(string sheetPath, int row)
        {
            return AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
                .Select(sprite => (sprite, match: CellIndex.Match(sprite.name)))
                .Where(entry => entry.match.Success && int.Parse(entry.match.Groups[1].Value) == row)
                .OrderBy(entry => int.Parse(entry.match.Groups[2].Value))
                .Select(entry => entry.sprite)
                .ToArray();
        }

        static Color32 Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? (Color32)color : new Color32(255, 255, 255, 255);
        }

        public static bool ConfigureHud(HudArt hud, out string error)
        {
            error = null;
            string barJsonPath = PowerBarFolder + "/ui_power_bar.json";
            string topJsonPath = TopBarFolder + "/ui_topbar.json";
            var barJson = AssetDatabase.LoadAssetAtPath<TextAsset>(barJsonPath);
            var topJson = AssetDatabase.LoadAssetAtPath<TextAsset>(topJsonPath);
            PowerBarExport bar = barJson != null ? JsonUtility.FromJson<PowerBarExport>(barJson.text) : null;
            TopBarExport top = topJson != null ? JsonUtility.FromJson<TopBarExport>(topJson.text) : null;
            if (bar == null || top == null || top.pauseCell == null || top.pauseCell.Length != 2)
            {
                error = $"Falta {(bar == null ? barJsonPath : topJsonPath)}: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
                return false;
            }

            string fillPath = PowerBarFolder + "/ui_power_bar_fill_sheet.png";
            string frontPath = PowerBarFolder + "/ui_power_bar_front_sheet.png";
            string pausePath = TopBarFolder + "/ui_topbar_pause_sheet.png";
            hud.ConfigurePowerBar(AssetDatabase.LoadAssetAtPath<Sprite>(PowerBarFolder + "/ui_power_bar_frame.png"),
                HudRow(fillPath, 0), HudRow(fillPath, 1), HudRow(frontPath, 0), bar.height, bar.x0, bar.x1, bar.snap,
                bar.fps, bar.frontContact);
            hud.ConfigureTopBar(AssetDatabase.LoadAssetAtPath<Sprite>(TopBarFolder + "/ui_topbar_plate.png"),
                HudRow(pausePath, 0), top.margin, top.top, top.height, top.plateBorder, top.padX, top.gap,
                top.plateWidthStep, top.letterSpacing, Hex(top.levelText), Hex(top.timerText), top.timerTemplate,
                new Vector2Int(top.pauseCell[0], top.pauseCell[1]));

            if (!hud.HasPowerBar || hud.FillFrames != bar.frames)
            {
                error = $"La barra de poder de {PowerBarFolder} está incompleta o sin cortar: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
                return false;
            }
            if (!hud.HasTopBar)
            {
                error = $"La barra de arriba de {TopBarFolder} está incompleta o sin cortar: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
                return false;
            }
            return true;
        }

        // T6-07: la hoja de un efecto de A.5 y su .json, tal como los copia ArtBridge: fx/<carpeta>/fx_<carpeta>_sheet.
        public static string EffectSheetPath(string folder) => $"{EffectsFolder}/{folder}/fx_{folder}_sheet.png";
        public static string EffectJsonPath(string folder) => $"{EffectsFolder}/{folder}/fx_{folder}_sheet.json";

        // <hoja>_<fila>_<columna>: el nombre que SpriteImportRules da a cada celda de una hoja de A.5.
        static readonly Regex CellIndex = new Regex(@"_(\d+)_(\d+)$");

        // Una hoja de A.5: las filas, los frames y las variantes de su .json, y las celdas en el orden de la rejilla.
        public static VfxArt.Sheet LoadEffectSheet(string folder, out string missing)
        {
            missing = null;
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(EffectJsonPath(folder));
            EffectExport export = json != null ? JsonUtility.FromJson<EffectExport>(json.text) : null;
            if (export?.rows == null || export.rows.Length == 0 || export.frames < 1)
            {
                missing = EffectJsonPath(folder);
                return default;
            }
            int variants = Math.Max(export.variants, 1);
            int columns = export.frames * variants;
            var cells = new Sprite[export.rows.Length * columns];
            foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(EffectSheetPath(folder)).OfType<Sprite>())
            {
                Match match = CellIndex.Match(sprite.name);
                if (!match.Success)
                    continue;
                int row = int.Parse(match.Groups[1].Value);
                int column = int.Parse(match.Groups[2].Value);
                if (row < export.rows.Length && column < columns)
                    cells[row * columns + column] = sprite;
            }
            var sheet = new VfxArt.Sheet(export.rows, export.frames, variants, cells);
            if (!sheet.IsComplete)
                missing = EffectSheetPath(folder);
            return sheet;
        }

        // Las quince hojas de A.5, en el orden de su tabla.
        public static bool ConfigureVfx(VfxArt effects, out string error)
        {
            error = null;
            string missing = null;

            VfxArt.Sheet Sheet(string folder)
            {
                VfxArt.Sheet sheet = LoadEffectSheet(folder, out string gap);
                missing ??= gap;
                return sheet;
            }

            effects.Configure(Sheet("shot"), Sheet("kill"), Sheet("vanish"), Sheet("send"), Sheet("abs"), Sheet("dmg"),
                Sheet("ign"), Sheet("upg"), Sheet("thaw"), Sheet("cure"), Sheet("cast"), Sheet("pwr_zer"),
                Sheet("pwr_dec"), Sheet("pwr_wrm"), Sheet("cascade"));

            if (missing == null)
                return true;
            error = $"Falta {missing} o no está cortada: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
            return false;
        }

        // Lo que VfxArt necesita del .json de cada generador de A.5 (exporta()).
        [Serializable]
        sealed class EffectExport
        {
            public int cellSize;
            public int frames;
            public int variants;
            public string[] rows;
        }

        // La talla va de 1 a N en el nombre del archivo, y de 0 a N − 1 en ProtonArt.
        public static string ProtonSheetPath(string faction, int size) => $"{WorldFolder}/proton/world_proton_{faction}_{size}.png";

        public const string SizesJsonPath = WorldFolder + "/proton/world_proton_tallas.json";
        public const string DigitsPath = WorldFolder + "/proton/world_charge_digits.png";
        public const string DigitsJsonPath = WorldFolder + "/proton/world_charge_digits.json";

        // Las hojas y las cifras de A.4.2 desde Assets/Sprites/world/proton/. Los frames son los que corta
        // SpriteImportRules: <hoja>_body_<n>, <hoja>_drop_<n> y world_charge_digits_<n>. Cuántas tallas hay,
        // su escalón de Carga y el radio de cada bolsa vienen de world_proton_tallas.json.
        public static bool ConfigureProton(ProtonArt proton, out string error)
        {
            error = null;
            string missing = null;

            var sizesJson = AssetDatabase.LoadAssetAtPath<TextAsset>(SizesJsonPath);
            SizesExport tallas = sizesJson != null ? JsonUtility.FromJson<SizesExport>(sizesJson.text) : null;
            if (tallas == null || tallas.sizes < 1 || tallas.chargePerSize < 1 ||
                tallas.bagRadiusPixels == null || tallas.bagRadiusPixels.Length != tallas.sizes)
            {
                error = $"Falta {SizesJsonPath} o no está completo: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
                return false;
            }

            ProtonArt.SizeFrames[] Sizes(string faction)
            {
                var sizes = new ProtonArt.SizeFrames[tallas.sizes];
                for (int i = 0; i < sizes.Length; i++)
                {
                    string path = ProtonSheetPath(faction, i + 1);
                    Sprite[] frames = LoadFrames(path);
                    sizes[i] = new ProtonArt.SizeFrames(
                        frames.Where(sprite => sprite.name.Contains("_body_")).ToArray(),
                        frames.Where(sprite => sprite.name.Contains("_drop_")).ToArray());
                    if (!sizes[i].IsComplete)
                        missing ??= path;
                }
                return sizes;
            }

            Sprite[] digits = LoadFrames(DigitsPath);
            if (digits.Length != ProtonArt.Digits)
                missing ??= DigitsPath;
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(DigitsJsonPath);
            DigitsExport export = json != null ? JsonUtility.FromJson<DigitsExport>(json.text) : null;
            if (export?.advance == null || export.advance.Length != ProtonArt.Digits)
                missing ??= DigitsJsonPath;

            proton.Configure(Sizes("player"), Sizes("enemy"), digits, export?.advance,
                tallas.chargePerSize, tallas.bagRadiusPixels);

            if (missing == null)
                return true;
            error = $"Falta {missing} o no está cortada: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
            return false;
        }

        // Los JSON de tools/gen_proton_v2.py: exporta_tallas y hoja_cifras.
        [Serializable]
        sealed class SizesExport
        {
            public int sizes;
            public int chargePerSize;
            public int[] bagRadiusPixels;
        }

        [Serializable]
        sealed class DigitsExport
        {
            public int cellWidth;
            public int cellHeight;
            public int[] advance;
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
