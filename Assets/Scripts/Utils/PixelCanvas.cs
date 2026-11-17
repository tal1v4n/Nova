using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Utils
{
    // T6-09: un lienzo de píxeles en CPU para componer la UI de partida como la componen los generadores de
    // tools/ (A.6): placas de nueve cortes, teselas en mosaico absoluto, máscaras con la escalera de 13.12 y
    // reflejos, con el alfa binario del contrato. El origen está arriba a la izquierda y la y crece hacia
    // abajo, como en los generadores, para que los números de los .json se usen tal cual.
    //
    // Lo que se compone se sube a una textura con CanvasSprite. Las piezas se leen de sus sprites, así que sus
    // texturas tienen que ser legibles: SpriteImportRules las importa con Read/Write en Assets/Sprites/ui/.
    public sealed class PixelCanvas
    {
        readonly Color32[] pixels;

        public int Width { get; }
        public int Height { get; }

        public PixelCanvas(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), $"Un lienzo de {width} x {height} no existe.");
            Width = width;
            Height = height;
            pixels = new Color32[width * height];
        }

        public void Clear() => Array.Clear(pixels, 0, pixels.Length);

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public Color32 Get(int x, int y) => pixels[y * Width + x];

        public bool IsSet(int x, int y) => InBounds(x, y) && pixels[y * Width + x].a != 0;

        public void Set(int x, int y, Color32 color)
        {
            if (!InBounds(x, y))
                return;
            color.a = 255;
            pixels[y * Width + x] = color;
        }

        public void Erase(int x, int y)
        {
            if (InBounds(x, y))
                pixels[y * Width + x] = default;
        }

        // Un rectángulo de un color.
        public void Fill(RectInt rect, Color32 color)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
                for (int x = rect.xMin; x < rect.xMax; x++)
                    Set(x, y, color);
        }

        // Los píxeles opacos de una pieza, con su esquina de arriba a la izquierda en (x, y).
        public void Blit(SpritePixels piece, int x, int y, bool flipX = false, bool flipY = false)
        {
            for (int py = 0; py < piece.Height; py++)
            {
                for (int px = 0; px < piece.Width; px++)
                {
                    Color32 c = piece.Get(flipX ? piece.Width - 1 - px : px, flipY ? piece.Height - 1 - py : py);
                    if (c.a != 0)
                        Set(x + px, y + py, c);
                }
            }
        }

        // Una pieza en mosaico dentro de un rectángulo. La tesela que cae en (x, y) es la de la columna
        // (x − originX) mod ancho: con un origen fijo la trama no salta cuando el rectángulo cambia.
        // `where`, si se da, decide qué píxeles del lienzo se pueden pintar.
        public void Tile(SpritePixels tile, RectInt rect, int originX, int originY, Func<int, int, bool> where = null)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                int ty = Mod(y - originY, tile.Height);
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (where != null && !where(x, y))
                        continue;
                    Color32 c = tile.Get(Mod(x - originX, tile.Width), ty);
                    if (c.a != 0)
                        Set(x, y, c);
                }
            }
        }

        // Nueve cortes con los bordes enteros y el centro en mosaico desde el borde, como Unity con Draw
        // Mode Tiled y como placa() en tools/gen_topbar_v1.py.
        public void NineSlice(SpritePixels plate, RectInt rect, int border)
        {
            int b = border;
            int cw = plate.Width - 2 * b;
            int ch = plate.Height - 2 * b;
            for (int y = 0; y < rect.height; y++)
            {
                int sy = y < b ? y : y >= rect.height - b ? plate.Height - (rect.height - y) : b + (y - b) % ch;
                for (int x = 0; x < rect.width; x++)
                {
                    int sx = x < b ? x : x >= rect.width - b ? plate.Width - (rect.width - x) : b + (x - b) % cw;
                    Color32 c = plate.Get(sx, sy);
                    if (c.a != 0)
                        Set(rect.x + x, rect.y + y, c);
                }
            }
        }

        // Cambia un color por otro en un rectángulo: lo que el generador pinta sobre el relleno de una placa.
        public void Recolor(RectInt rect, Color32 from, Color32 to, Func<int, int, bool> where = null)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (!InBounds(x, y) || (where != null && !where(x, y)))
                        continue;
                    Color32 c = pixels[y * Width + x];
                    if (c.a != 0 && c.r == from.r && c.g == from.g && c.b == from.b)
                        Set(x, y, to);
                }
            }
        }

        // Copia el lienzo a una textura del mismo tamaño, volteándolo: en la textura la fila 0 es la de abajo.
        public void CopyTo(Texture2D texture, Color32[] scratch)
        {
            for (int y = 0; y < Height; y++)
                Array.Copy(pixels, y * Width, scratch, (Height - 1 - y) * Width, Width);
            texture.SetPixels32(scratch);
            texture.Apply(false);
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;
    }

    // Los píxeles de un sprite, con el origen arriba a la izquierda, leídos una vez de su textura.
    public sealed class SpritePixels
    {
        static readonly Dictionary<Sprite, SpritePixels> Cache = new Dictionary<Sprite, SpritePixels>();

        readonly Color32[] pixels;

        public int Width { get; }
        public int Height { get; }

        public SpritePixels(int width, int height, Color32[] topDown)
        {
            Width = width;
            Height = height;
            pixels = topDown;
        }

        public Color32 Get(int x, int y) => pixels[y * Width + x];

        public bool IsSet(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && pixels[y * Width + x].a != 0;

        // La textura del sprite tiene que ser legible (Read/Write). Se cachea por sprite.
        public static SpritePixels Of(Sprite sprite)
        {
            if (sprite == null)
                throw new ArgumentNullException(nameof(sprite));
            if (Cache.TryGetValue(sprite, out SpritePixels cached) && cached != null)
                return cached;

            Rect r = sprite.textureRect;
            int x0 = Mathf.RoundToInt(r.x);
            int y0 = Mathf.RoundToInt(r.y);
            int w = Mathf.RoundToInt(r.width);
            int h = Mathf.RoundToInt(r.height);
            Color32[] all = sprite.texture.GetPixels32();
            int tw = sprite.texture.width;
            var topDown = new Color32[w * h];
            for (int y = 0; y < h; y++)
                Array.Copy(all, (y0 + h - 1 - y) * tw + x0, topDown, y * w, w);
            var result = new SpritePixels(w, h, topDown);
            Cache[sprite] = result;
            return result;
        }

        // Para los tests: un sprite que ya no existe deja de ocupar la caché.
        public static void ClearCache() => Cache.Clear();
    }

    // La silueta de las placas de 13.12: un rectángulo con la esquina en escalera de 4 + 4 px, y su
    // interior, la silueta erosionada 2 px (el marco). Es escalera() y erosion() de tools/gen_topbar_v1.py.
    public static class PlateShape
    {
        public static bool Silhouette(int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return false;
            int ex = Mathf.Min(x, width - 1 - x);
            int top = y;
            int bottom = height - 1 - y;
            bool cut = (top < 2 && ex < 4) || (top < 4 && ex < 2) || (bottom < 2 && ex < 4) || (bottom < 4 && ex < 2);
            return !cut;
        }

        // Dentro del marco de 2 px: la silueta erosionada con un cuadrado de 5 x 5.
        public static bool Inside(int width, int height, int x, int y)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (!Silhouette(width, height, x + dx, y + dy))
                        return false;
            return true;
        }
    }

    // Un sprite cuya textura se reescribe desde un PixelCanvas. Point, sin mipmaps y a 64 px por unidad, con
    // el pivote donde lo pida quien lo coloca (en fracción del tamaño, como Sprite.Create).
    public sealed class CanvasSprite
    {
        readonly Color32[] scratch;

        public PixelCanvas Canvas { get; }
        public Texture2D Texture { get; }
        public Sprite Sprite { get; }

        public CanvasSprite(int width, int height, Vector2 pivot, string name)
        {
            Canvas = new PixelCanvas(width, height);
            scratch = new Color32[width * height];
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
                hideFlags = HideFlags.DontSave,
            };
            Sprite = Sprite.Create(Texture, new Rect(0f, 0f, width, height), pivot, PrimitiveSprites.PixelsPerUnit, 0,
                SpriteMeshType.FullRect);
            Sprite.name = name;
            Sprite.hideFlags = HideFlags.DontSave;
            Upload();
        }

        public void Upload() => Canvas.CopyTo(Texture, scratch);

        public void Destroy()
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(Sprite);
                UnityEngine.Object.Destroy(Texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(Sprite);
                UnityEngine.Object.DestroyImmediate(Texture);
            }
        }
    }
}
