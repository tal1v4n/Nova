using System.Collections.Generic;
using UnityEngine;

namespace Nova.Utils
{
    // Primitivas de la regla 4 de T.2 para las vistas provisionales: discos blancos de alfa
    // binario y filtro Point a PPU 64 (T0-04), teñidos por SpriteRenderer.color. Las comparten la
    // presentación y la UI, que no se referencian entre sí. Desaparecen con el arte de la fase 6.
    public static class PrimitiveSprites
    {
        public const int PixelsPerUnit = 64; // T0-04

        static readonly Dictionary<int, Sprite> DiscCache = new Dictionary<int, Sprite>();

        public static Sprite Disc(int diameter)
        {
            if (DiscCache.TryGetValue(diameter, out Sprite cached) && cached != null)
                return cached;

            var texture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false)
            {
                name = $"ProvisionalCircle{diameter}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            float radius = diameter / 2f;
            var pixels = new Color32[diameter * diameter];
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    bool inside = dx * dx + dy * dy <= radius * radius;
                    pixels[y * diameter + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = texture.name;
            DiscCache[diameter] = sprite;
            return sprite;
        }
    }
}
