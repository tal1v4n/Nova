using Nova.Data;
using Nova.Input;
using Nova.Utils;
using UnityEngine;

namespace Nova.UI
{
    // T6-09: cómo se compone en píxeles la UI de partida con su arte (A.6). Cada función es la de su generador
    // elegido, traducida: panel() y antes_de_pegar() de tools/gen_panel_v2.py («La sonda»), boton() de
    // gen_btnconv_v2.py («El barrido»), ranura() y cancelar_lamina() de gen_wheel_v1.py («Las teclas») y opcion()
    // de gen_ignmenu_v2.py («Los nodos»). Las vistas solo deciden el estado; aquí se dibuja, y los tests pueden
    // mirar los píxeles.
    //
    // Todo en píxeles de la referencia, con el origen arriba a la izquierda. El texto no: va en TextMeshPro.
    internal static class PanelSkin
    {
        // --- El panel de clic derecho («La sonda») ---

        // El fondo en trama, el filo, las esquinas del bando, la raya del título con su testigo y los separadores
        // entre bloques. faction: 0 Jugador, 1 Enemigo, 2 neutral (las filas de las piezas).
        internal static void ComposePanel(PixelCanvas c, PanelArt.PanelPieces p, int faction, InfoPanelState.PanelLayout layout,
            int frame)
        {
            c.Clear();
            int w = p.width;
            int h = layout.HeightPixels;
            c.Tile(SpritePixels.Of(p.screen), new RectInt(0, 0, w, h), 0, 0);

            SpritePixels corner = SpritePixels.Of(p.corner[faction]);
            int e = corner.Width;
            Color32 edge = SpritePixels.Of(p.edge[faction]).Get(0, 0);
            for (int x = e; x < w - e; x += 2)
            {
                c.Set(x, 0, edge);
                c.Set(x, h - 1, edge);
            }
            for (int y = e; y < h - e; y += 2)
            {
                c.Set(0, y, edge);
                c.Set(w - 1, y, edge);
            }
            c.Blit(corner, 0, 0);
            c.Blit(corner, w - e, 0, flipX: true);
            c.Blit(corner, 0, h - e, flipY: true);
            c.Blit(corner, w - e, h - e, flipX: true, flipY: true);

            // La raya bajo el título: a 4 px del pie de la primera fila, hasta 6 px antes del final, y el testigo
            // de 3 x 3 al final, que se enciende los frames f / 8 pares.
            SpritePixels rule = SpritePixels.Of(p.rule[faction]);
            int ruleTop = p.padding + p.rowHeight - 4;
            int ruleWidth = w - 2 * p.padding;
            Color32 line = rule.Get(0, 1);
            for (int x = p.padding; x < p.padding + ruleWidth - 2 * p.ruleLight; x++)
                c.Set(x, ruleTop + 1, line);
            if ((frame / p.blinkFrames) % 2 == 0)
            {
                for (int y = 0; y < p.ruleLight; y++)
                    for (int x = 0; x < p.ruleLight; x++)
                        if (rule.IsSet(1 + x, y))
                            c.Set(p.padding + ruleWidth - p.ruleLight + x, ruleTop + y, rule.Get(1 + x, y));
            }

            // Una línea de puntos encima de cada bloque menos el primero.
            Color32 divider = SpritePixels.Of(p.divider[faction]).Get(0, 0);
            for (int i = 1; i < layout.Blocks.Length; i++)
            {
                int y = p.padding + layout.Blocks[i] * p.rowHeight - 1;
                for (int x = p.padding; x < w - p.padding; x += p.dividerStep)
                    c.Set(x, y, divider);
            }
        }

        // El cable del borde del nodo al costado del panel, con su pulso y el enchufe (antes_de_pegar). Todo en
        // píxeles relativos al centro del nodo, con la y hacia abajo: panelInnerX es la distancia al costado del
        // panel que mira al nodo, y panelTop y panelHeight lo sitúan en vertical; toLeft, el panel está a la izquierda.
        internal readonly struct TetherPath
        {
            public readonly int X0, X1, Xm, Ya, Side;

            public TetherPath(PanelArt.PanelPieces p, int footprint, int panelInnerX, int panelTop, int panelHeight, bool toLeft)
            {
                Side = toLeft ? -1 : 1;
                X0 = Side * ((footprint + 1) / 2 + p.tetherFromFootprint);
                X1 = Side * (panelInnerX - p.socketGap);
                Ya = Mathf.Max(panelTop + 10, Mathf.Min(0, panelTop + panelHeight - 10));
                Xm = (X0 + X1) / 2;
            }

            // Lo que ocupa, con el enchufe (3 x 7, centrado en la y del cable).
            public RectInt Bounds
            {
                get
                {
                    int left = Mathf.Min(X0, X1) - 3;
                    int right = Mathf.Max(X0, X1) + 3;
                    int top = Mathf.Min(0, Ya) - 3;
                    int bottom = Mathf.Max(0, Ya) + 4;
                    return new RectInt(left, top, right - left, bottom - top);
                }
            }
        }

        // El lienzo mide lo que tether.Bounds; su (0, 0) es la esquina de arriba a la izquierda de Bounds.
        internal static void ComposeTether(PixelCanvas c, PanelArt.PanelPieces p, int faction, TetherPath tether, int frame)
        {
            c.Clear();
            RectInt b = tether.Bounds;
            SpritePixels tile = SpritePixels.Of(p.tether[faction]);
            SpritePixels pulse = SpritePixels.Of(p.pulse[faction]);
            Color32 body = FirstSet(tile);
            Color32 head = pulse.Get(2, 1);
            Color32 tail = pulse.Get(1, 1);

            var path = new System.Collections.Generic.List<Vector2Int>();
            int side = tether.Side;
            for (int x = tether.X0; x != tether.Xm; x += side)
                path.Add(new Vector2Int(x, 0));
            int step = tether.Ya >= 0 ? 1 : -1;
            for (int y = 0; y != tether.Ya; y += step)
                path.Add(new Vector2Int(tether.Xm, y));
            for (int x = tether.Xm; x != tether.X1; x += side)
                path.Add(new Vector2Int(x, tether.Ya));

            float cab = frame / (float)p.pulseFrames * (path.Count + 6) - 3;
            for (int i = 0; i < path.Count; i++)
            {
                float d = cab - i;
                Color32? color = d >= 0 && d < 1 ? head : d >= 1 && d < 3 ? tail : i % 2 == 0 ? body : (Color32?)null;
                if (color.HasValue)
                    c.Set(path[i].x - b.x, path[i].y - b.y, color.Value);
            }

            SpritePixels socket = SpritePixels.Of(p.socket[faction]);
            int sx = side < 0 ? tether.X1 - (socket.Width - 1) : tether.X1;
            c.Blit(socket, sx - b.x, tether.Ya - 3 - b.y, flipX: side < 0);
        }

        static Color32 FirstSet(SpritePixels s)
        {
            for (int y = 0; y < s.Height; y++)
                for (int x = 0; x < s.Width; x++)
                    if (s.IsSet(x, y))
                        return s.Get(x, y);
            return default;
        }

        // --- Los botones de reconversión («El barrido») ---

        // hover: de 0 a 1 en los 160 ms del barrido; ya con la curva orbital no, se le aplica aquí.
        internal static void ComposeConversion(PixelCanvas c, PanelArt.ConversionPieces p, PanelArt.ConversionLook look,
            float hover, int energy, int cost)
        {
            c.Clear();
            int w = p.size.x;
            int h = p.size.y;
            int dy = look == PanelArt.ConversionLook.Pressed ? p.pressedDrop : 0;
            c.NineSlice(SpritePixels.Of(p.plate[(int)look]), new RectInt(0, dy, w, h), p.plateBorder);
            bool Inside(int x, int y) => PlateShape.Inside(w, h, x, y - dy);
            var body = new RectInt(0, dy, w, h);

            switch (look)
            {
                case PanelArt.ConversionLook.Hover:
                case PanelArt.ConversionLook.Pressed:
                {
                    bool pressed = look == PanelArt.ConversionLook.Pressed;
                    int xf = pressed ? p.innerX1 : p.innerX0 + Mathf.RoundToInt((p.innerX1 - p.innerX0) * UiMotion.Orbital(Mathf.Clamp01(hover)));
                    Color32 fill = pressed ? p.pressedFill : p.primaryFill;
                    for (int y = body.yMin; y < body.yMax; y++)
                        for (int x = 0; x < xf; x++)
                            if (Inside(x, y))
                                c.Set(x, y, fill);
                    if (!pressed && xf < p.innerX1)
                    {
                        SpritePixels front = SpritePixels.Of(p.front[0]);
                        for (int y = 0; y < front.Height; y++)
                            for (int x = 0; x < front.Width; x++)
                                if (front.IsSet(x, y) && Inside(xf - 2 + x, dy + 2 + y))
                                    c.Set(xf - 2 + x, dy + 2 + y, front.Get(x, y));
                    }
                    break;
                }
                case PanelArt.ConversionLook.Missing:
                {
                    float frac = cost > 0 ? Mathf.Min(1f, energy / (float)cost) : 0f;
                    int xf = p.innerX0 + Mathf.RoundToInt((p.innerX1 - p.innerX0) * frac);
                    c.Tile(SpritePixels.Of(p.fill[0]), body, 0, 0, (x, y) => x < xf && Inside(x, y));
                    c.Tile(SpritePixels.Of(p.fill[1]), body, 0, 0, (x, y) => x > xf && Inside(x, y));
                    if (xf > p.innerX0 && xf < p.innerX1)
                    {
                        SpritePixels front = SpritePixels.Of(p.front[1]);
                        for (int y = 0; y < front.Height; y++)
                            for (int x = 0; x < front.Width; x++)
                                if (front.IsSet(x, y) && Inside(xf + x, dy + 2 + y))
                                    c.Set(xf + x, dy + 2 + y, front.Get(x, y));
                    }
                    break;
                }
            }
        }

        // --- La rueda («Las teclas») ---

        internal static void ComposeSlot(PixelCanvas c, PanelArt.WheelPieces p, int power, PanelArt.SlotLook look,
            float progress, bool paused, int frame, float hover)
        {
            c.Clear();
            int w = p.slotSize.x;
            int h = p.slotSize.y;
            bool pressed = look == PanelArt.SlotLook.Pressed || look == PanelArt.SlotLook.ReadyPressed;
            int dy = pressed ? p.pressedDrop : 0;
            var body = new RectInt(0, dy, w, h);
            c.NineSlice(SpritePixels.Of(p.Plate(power, look)), body, p.plateBorder);
            bool Inside(int x, int y) => PlateShape.Inside(w, h, x, y - dy);

            switch (look)
            {
                case PanelArt.SlotLook.Hover:
                case PanelArt.SlotLook.Pressed:
                {
                    int steps = p.hover.Length;
                    float t = Mathf.Clamp01(hover);
                    int step = look == PanelArt.SlotLook.Pressed ? steps : Mathf.Max(1, Mathf.RoundToInt(t * steps));
                    int filled = Mathf.RoundToInt((w - 4) * (look == PanelArt.SlotLook.Pressed ? 1f : UiMotion.Orbital(t)));
                    c.Tile(SpritePixels.Of(p.hover[step - 1]), body, 0, dy, (x, y) => x < 2 + filled && Inside(x, y));
                    break;
                }
                case PanelArt.SlotLook.Charging:
                {
                    int filled = Mathf.FloorToInt((w - 4) * Mathf.Min(progress, 0.999f));
                    Color32 baseColor = paused ? p.paused[power] : p.charging[power];
                    for (int y = body.yMin; y < body.yMax; y++)
                        for (int x = 0; x < 2 + filled; x++)
                            if (Inside(x, y))
                                c.Set(x, y, baseColor);
                    c.Tile(SpritePixels.Of(p.Fill(power, paused)), body, 0, dy, (x, y) => x < 2 + filled && Inside(x, y));
                    if (!paused && filled > 0)
                        for (int y = body.yMin; y < body.yMax; y++)
                            if (Inside(1 + filled, y))
                                c.Set(1 + filled, y, p.front[power]);
                    break;
                }
                case PanelArt.SlotLook.Ready:
                case PanelArt.SlotLook.ReadyHover:
                case PanelArt.SlotLook.ReadyPressed:
                {
                    int head = Mathf.RoundToInt((frame % p.frames) / (float)p.frames * p.lightTravel) + p.lightFrom;
                    for (int x = head; x < head + p.lightWidth; x++)
                        for (int y = body.yMin; y < body.yMax; y++)
                            if (x >= 0 && x < w && Inside(x, y))
                                c.Set(x, y, x == head + 1 ? p.light[power] : p.lightEdge[power]);
                    break;
                }
            }

            if (look == PanelArt.SlotLook.Locked)
            {
                c.Blit(SpritePixels.Of(p.lockIcon), p.lockAt.x, p.lockAt.y + dy);
            }
            else
            {
                PanelArt.IconLook icon = look == PanelArt.SlotLook.Blocked ? PanelArt.IconLook.Grey
                    : look == PanelArt.SlotLook.Unavailable ? PanelArt.IconLook.Outline
                    : look >= PanelArt.SlotLook.Ready ? PanelArt.IconLook.Ready
                    : PanelArt.IconLook.Color;
                c.Blit(SpritePixels.Of(p.Icon(power, icon)), p.iconAt.x, p.iconAt.y + dy);
            }
        }

        // «Cancelar · +N»: el secundario de 13.12 con la placa de la rueda; normal, encima, pulsado o bloqueado.
        internal static void ComposeCancel(PixelCanvas c, PanelArt.WheelPieces p, bool hover, bool pressed, bool blocked)
        {
            c.Clear();
            PanelArt.SlotLook plate = blocked ? PanelArt.SlotLook.Blocked
                : hover || pressed ? PanelArt.SlotLook.Hover
                : PanelArt.SlotLook.Normal;
            int dy = pressed && !blocked ? p.pressedDrop : 0;
            c.NineSlice(SpritePixels.Of(p.Plate(0, plate)), new RectInt(0, dy, p.cancelSize.x, p.cancelSize.y), p.plateBorder);
        }

        // --- El menú de encendido («Los nodos») ---

        // close: de 0 a 1 en los 160 ms en que se cierran las esquinas; la curva orbital se aplica aquí.
        internal static void ComposeOption(PixelCanvas c, PanelArt.IgnitionPieces p, int option, PanelArt.CornerLook? corners,
            PanelArt.NodeLook node, bool pressed, float close)
        {
            c.Clear();
            // El nodo: 1 px arriba en reposo y pulsado pressedDrop px más abajo.
            c.Blit(SpritePixels.Of(p.Node(option, node)), 0, -1 + (pressed ? p.pressedDrop : 0));
            if (corners == null)
                return;

            int closing = corners == PanelArt.CornerLook.Pressed ? p.pressedClose
                : corners == PanelArt.CornerLook.Hover ? Mathf.RoundToInt(p.hoverClose * UiMotion.Orbital(Mathf.Clamp01(close)))
                : 0;
            int o = (p.cell - p.frame) / 2;
            int a = o + closing;
            int b = o + p.frame - closing - p.cornerSize;
            SpritePixels corner = SpritePixels.Of(p.corner[(int)corners.Value]);
            c.Blit(corner, a, a);
            c.Blit(corner, b, a, flipX: true);
            c.Blit(corner, a, b, flipY: true);
            c.Blit(corner, b, b, flipX: true, flipY: true);
        }

        // El color de un Color32 como etiqueta de TextMeshPro.
        internal static string Hex(Color32 c) => $"#{c.r:X2}{c.g:X2}{c.b:X2}";
    }
}
