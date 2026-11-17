using System;
using System.Linq;
using Nova.Data;
using UnityEditor;
using UnityEngine;

namespace Nova.Editor
{
    // T6-09: rellena NodeLabelArt y PanelArt (A.6) desde lo que copia ArtBridge en Assets/Sprites/ui/ —la carpeta de
    // cada asset sin su versión— con las medidas y los colores de cada .json. Lo llama el mismo menú que el resto
    // del arte (Nova → Arte → Asignar sprites de nodo). Las piezas se cortan en SpriteImportRules (UiGrids).
    public static partial class NodeArtAssets
    {
        public const string NodeLabelArtPath = "Assets/Data/NodeLabelArt.asset";
        public const string PanelArtPath = "Assets/Data/PanelArt.asset";

        const string EnergyFolder = UiFolder + "/energy_label";
        const string ChargeFolder = UiFolder + "/charge_bar";
        const string InfoPanelFolder = UiFolder + "/info_panel";
        const string ConvertFolder = UiFolder + "/btn_convert";
        const string WheelFolder = UiFolder + "/power_wheel";
        const string IgnitionFolder = UiFolder + "/ignition_menu";

        static readonly string[] PowerNames = { "zero", "decay", "wormhole" };
        static readonly string[] PanelFactionNames = { "player", "enemy", "neutral" };

        // --- Los .json, lo que se lee de ellos ---

        [Serializable] sealed class EnergyExport { public int cellWidth; public int cellHeight; public int[] advance; public int frames; public float fps; public int digitPhaseOffset; public int boxHeight; }

        [Serializable] sealed class PowerHex { public string zero; public string decay; public string wormhole; }

        [Serializable]
        sealed class ChargeExport
        {
            public int width; public int height; public int socketWidth; public int fuseX0; public int fuseLength;
            public int flameWidth; public int flameFront; public int frames; public float fps;
            public string[] powers; public string[] factions;
            public PowerHex readyLight; public int readyLightRow; public float readyLightHalfWidth;
            public int readyLightFrom; public int readyLightTravel;
        }

        [Serializable] sealed class FactionHex { public string player; public string enemy; public string neutral; }
        [Serializable] sealed class PanelTextExport { public FactionHex title; public string text; public string label; public string muted; }

        [Serializable]
        sealed class PanelExport
        {
            public int width; public int padding; public int rowHeight; public string[] rows; public int capTop;
            public int pulseLoopFrames; public float fps; public int titleRuleBlinkFrames; public int dividerStep;
            public int ruleGap; public int ruleLight; public int tetherFromFootprint; public int socketGap;
            public PanelTextExport text;
        }

        [Serializable]
        sealed class ButtonTextExport
        {
            public string normal; public string cost; public string hover; public string hoverCost; public string muted;
            public string missing; public string unavailable; public string onPower;
            public string name; public string nameHover; public string costHover;
        }

        [Serializable]
        sealed class ConvertExport
        {
            public int width; public int height; public int cellHeight; public int pressedDrop; public int capTop; public int padX;
            public int plateBorder; public int innerX0; public int innerX1; public float hoverMs; public string[] plateColumns;
            public string primaryFill; public string pressedFill; public string primaryText; public ButtonTextExport text;
        }

        [Serializable] sealed class PowerColorExport { public string charging; public string paused; public string front; public string light; public string lightEdge; }
        [Serializable] sealed class PowerColorsExport { public PowerColorExport zero; public PowerColorExport decay; public PowerColorExport wormhole; }

        [Serializable]
        sealed class WheelExport
        {
            public int[] slot; public int gap; public int[] cancel; public int cellHeight; public int pressedDrop; public string[] powers;
            public int frames; public float fps; public float hoverMs; public int plateBorder; public string[] plateColumns;
            public int hoverSteps; public PowerColorsExport powerColors; public int lightWidth; public int lightFrom; public int lightTravel;
            public int[] iconAt; public int[] lockAt; public int textRight; public int capTop; public ButtonTextExport text;
        }

        [Serializable]
        sealed class IgnitionExport
        {
            public int cell; public int frame; public int corner; public int cornerInset; public int hoverClose; public int pressedClose;
            public int pressedDrop; public int nameGap; public int costGap; public float enterMs; public float staggerMs; public float hoverMs;
            public string[] options; public string[] cornerColumns; public string[] nodesColumns; public ButtonTextExport text;
        }

        static T LoadJson<T>(string path, ref string error) where T : class
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            T export = json != null ? JsonUtility.FromJson<T>(json.text) : null;
            if (export == null && error == null)
                error = $"Falta {path}: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
            return export;
        }

        // Las celdas de una rejilla de A.6 por filas, en orden de columna: <pieza>_<fila>_<columna>.
        static Sprite[] GridRows(string path, int rows) =>
            Enumerable.Range(0, rows).SelectMany(row => HudRow(path, row)).ToArray();

        static Color32[] PowerColors(PowerColorsExport colors, Func<PowerColorExport, string> pick) =>
            new[] { Hex(pick(colors.zero)), Hex(pick(colors.decay)), Hex(pick(colors.wormhole)) };

        // --- NodeLabelArt: el número y la barra ---

        public static bool ConfigureNodeLabels(NodeLabelArt art, out string error)
        {
            error = null;
            EnergyExport energy = LoadJson<EnergyExport>(EnergyFolder + "/ui_energy_label.json", ref error);
            ChargeExport charge = LoadJson<ChargeExport>(ChargeFolder + "/ui_charge.json", ref error);
            if (error != null)
                return false;
            if (charge.powers == null || !charge.powers.SequenceEqual(PowerNames))
            {
                error = $"{ChargeFolder}/ui_charge.json: los poderes no van en el orden de 16.3 ({string.Join(", ", PowerNames)}).";
                return false;
            }

            art.ConfigureEnergyLabel(
                GridRows(EnergyFolder + "/ui_energy_label_player_sheet.png", NodeLabelArt.Digits),
                GridRows(EnergyFolder + "/ui_energy_label_enemy_sheet.png", NodeLabelArt.Digits),
                HudRow(EnergyFolder + "/ui_energy_label_neutral.png", 0),
                new Vector2Int(energy.cellWidth, energy.cellHeight), energy.advance, energy.frames, energy.fps,
                energy.digitPhaseOffset, energy.boxHeight);
            if (!art.HasEnergyLabel)
            {
                error = $"El número de {EnergyFolder} está incompleto o sin cortar: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
                return false;
            }

            art.ConfigureChargeBar(
                GridRows(ChargeFolder + "/ui_charge_socket.png", 2),
                GridRows(ChargeFolder + "/ui_charge_track.png", 2),
                GridRows(ChargeFolder + "/ui_charge_fuse_sheet.png", NodeLabelArt.Powers),
                GridRows(ChargeFolder + "/ui_charge_flame_sheet.png", 2 * NodeLabelArt.Powers),
                charge.width, charge.height, charge.socketWidth, charge.fuseX0, charge.fuseLength, charge.flameWidth,
                charge.flameFront, charge.frames, charge.fps,
                new[] { Hex(charge.readyLight.zero), Hex(charge.readyLight.decay), Hex(charge.readyLight.wormhole) },
                charge.readyLightRow, charge.readyLightHalfWidth, charge.readyLightFrom, charge.readyLightTravel);
            if (!art.HasChargeBar)
            {
                error = $"La barra de carga de {ChargeFolder} está incompleta o sin cortar: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
                return false;
            }
            return true;
        }

        // --- PanelArt: el panel, los botones, la rueda y el menú ---

        static PanelArt.ButtonText ButtonText(ButtonTextExport t)
        {
            var text = new PanelArt.ButtonText();
            if (t == null)
                return text;
            if (!string.IsNullOrEmpty(t.normal)) text.normal = Hex(t.normal);
            if (!string.IsNullOrEmpty(t.name)) text.normal = Hex(t.name);
            if (!string.IsNullOrEmpty(t.cost)) text.cost = Hex(t.cost);
            if (!string.IsNullOrEmpty(t.hover)) text.hover = Hex(t.hover);
            if (!string.IsNullOrEmpty(t.nameHover)) text.hover = Hex(t.nameHover);
            if (!string.IsNullOrEmpty(t.hoverCost)) text.hoverCost = Hex(t.hoverCost);
            if (!string.IsNullOrEmpty(t.costHover)) text.hoverCost = Hex(t.costHover);
            if (!string.IsNullOrEmpty(t.muted)) text.muted = Hex(t.muted);
            if (!string.IsNullOrEmpty(t.missing)) text.missing = Hex(t.missing);
            if (!string.IsNullOrEmpty(t.unavailable)) text.unavailable = Hex(t.unavailable);
            if (!string.IsNullOrEmpty(t.onPower)) text.onPower = Hex(t.onPower);
            return text;
        }

        public static bool ConfigurePanel(PanelArt art, out string error)
        {
            error = null;
            PanelExport panel = LoadJson<PanelExport>(InfoPanelFolder + "/ui_info_panel.json", ref error);
            ConvertExport convert = LoadJson<ConvertExport>(ConvertFolder + "/ui_btn_convert.json", ref error);
            WheelExport wheel = LoadJson<WheelExport>(WheelFolder + "/ui_power_wheel.json", ref error);
            IgnitionExport ignition = LoadJson<IgnitionExport>(IgnitionFolder + "/ui_ignition_menu.json", ref error);
            if (error != null)
                return false;
            if (panel.rows == null || !panel.rows.SequenceEqual(PanelFactionNames) || panel.text?.title == null)
            {
                error = $"{InfoPanelFolder}/ui_info_panel.json: faltan los colores del texto o las filas no son {string.Join(", ", PanelFactionNames)}; regenera con tools/gen_panel_v2.py.";
                return false;
            }
            if (wheel.powers == null || !wheel.powers.SequenceEqual(PowerNames) || wheel.powerColors?.zero == null)
            {
                error = $"{WheelFolder}/ui_power_wheel.json: faltan los colores de los poderes; regenera con tools/gen_wheel_v1.py.";
                return false;
            }

            string Piece(string name) => $"{InfoPanelFolder}/ui_info_panel_{name}.png";
            var panelPieces = new PanelArt.PanelPieces
            {
                screen = AssetDatabase.LoadAssetAtPath<Sprite>(Piece("screen")),
                corner = GridRows(Piece("corner"), PanelArt.Factions),
                edge = GridRows(Piece("edge"), PanelArt.Factions),
                tether = GridRows(Piece("tether"), PanelArt.Factions),
                pulse = GridRows(Piece("pulse"), PanelArt.Factions),
                socket = GridRows(Piece("socket"), PanelArt.Factions),
                divider = GridRows(Piece("divider"), PanelArt.Factions),
                rule = GridRows(Piece("rule"), PanelArt.Factions),
                width = panel.width,
                padding = panel.padding,
                rowHeight = panel.rowHeight,
                capTop = panel.capTop,
                pulseFrames = panel.pulseLoopFrames,
                framesPerSecond = panel.fps,
                blinkFrames = panel.titleRuleBlinkFrames,
                dividerStep = panel.dividerStep,
                ruleGap = panel.ruleGap,
                ruleLight = panel.ruleLight,
                tetherFromFootprint = panel.tetherFromFootprint,
                socketGap = panel.socketGap,
                titleColor = new[] { Hex(panel.text.title.player), Hex(panel.text.title.enemy), Hex(panel.text.title.neutral) },
                textColor = Hex(panel.text.text),
                labelColor = Hex(panel.text.label),
                mutedColor = Hex(panel.text.muted),
            };

            var conversion = new PanelArt.ConversionPieces
            {
                plate = HudRow(ConvertFolder + "/ui_btn_convert_plate.png", 0),
                front = GridRows(ConvertFolder + "/ui_btn_convert_front.png", 2),
                fill = HudRow(ConvertFolder + "/ui_btn_convert_fill.png", 0),
                size = new Vector2Int(convert.width, convert.height),
                cellHeight = convert.cellHeight,
                pressedDrop = convert.pressedDrop,
                capTop = convert.capTop,
                padX = convert.padX,
                plateBorder = convert.plateBorder,
                innerX0 = convert.innerX0,
                innerX1 = convert.innerX1,
                hoverSeconds = convert.hoverMs / 1000f,
                primaryFill = Hex(convert.primaryFill),
                pressedFill = Hex(convert.pressedFill),
                primaryText = Hex(convert.primaryText),
                text = ButtonText(convert.text),
            };

            var wheelPieces = new PanelArt.WheelPieces
            {
                plate = GridRows(WheelFolder + "/ui_power_wheel_plate.png", PanelArt.Powers),
                fill = GridRows(WheelFolder + "/ui_power_wheel_fill.png", PanelArt.Powers),
                hover = HudRow(WheelFolder + "/ui_power_wheel_hover.png", 0),
                icons = GridRows(WheelFolder + "/ui_power_wheel_icons.png", PanelArt.Powers),
                lockIcon = AssetDatabase.LoadAssetAtPath<Sprite>(WheelFolder + "/ui_power_wheel_lock.png"),
                slotSize = new Vector2Int(wheel.slot[0], wheel.slot[1]),
                cancelSize = new Vector2Int(wheel.cancel[0], wheel.cancel[1]),
                gap = wheel.gap,
                cellHeight = wheel.cellHeight,
                pressedDrop = wheel.pressedDrop,
                plateBorder = wheel.plateBorder,
                iconAt = new Vector2Int(wheel.iconAt[0], wheel.iconAt[1]),
                lockAt = new Vector2Int(wheel.lockAt[0], wheel.lockAt[1]),
                textRight = wheel.textRight,
                capTop = wheel.capTop,
                frames = wheel.frames,
                framesPerSecond = wheel.fps,
                hoverSeconds = wheel.hoverMs / 1000f,
                charging = PowerColors(wheel.powerColors, c => c.charging),
                paused = PowerColors(wheel.powerColors, c => c.paused),
                front = PowerColors(wheel.powerColors, c => c.front),
                light = PowerColors(wheel.powerColors, c => c.light),
                lightEdge = PowerColors(wheel.powerColors, c => c.lightEdge),
                lightWidth = wheel.lightWidth,
                lightFrom = wheel.lightFrom,
                lightTravel = wheel.lightTravel,
                text = ButtonText(wheel.text),
            };

            var ignitionPieces = new PanelArt.IgnitionPieces
            {
                corner = HudRow(IgnitionFolder + "/ui_ignition_menu_corner.png", 0),
                nodes = GridRows(IgnitionFolder + "/ui_ignition_menu_nodes.png", 3),
                cell = ignition.cell,
                frame = ignition.frame,
                cornerSize = ignition.corner,
                cornerInset = ignition.cornerInset,
                hoverClose = ignition.hoverClose,
                pressedClose = ignition.pressedClose,
                pressedDrop = ignition.pressedDrop,
                nameGap = ignition.nameGap,
                costGap = ignition.costGap,
                enterSeconds = ignition.enterMs / 1000f,
                staggerSeconds = ignition.staggerMs / 1000f,
                hoverSeconds = ignition.hoverMs / 1000f,
                text = ButtonText(ignition.text),
            };

            art.Configure(panelPieces, conversion, wheelPieces, ignitionPieces);
            if (!art.HasPanel)
            {
                error = $"El panel, los botones o la rueda de {UiFolder} están incompletos o sin cortar: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
                return false;
            }
            if (!art.HasIgnitionMenu)
            {
                error = $"El menú de encendido de {IgnitionFolder} está incompleto o sin cortar: sincroniza con Nova → Arte → Sincronizar sprites desde art.";
                return false;
            }
            return true;
        }
    }
}
