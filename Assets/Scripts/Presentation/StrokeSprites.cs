using System.Collections.Generic;
using UnityEngine;

namespace Nova.Presentation
{
    // Los trazos de mundo que se montan por piezas como la línea de envío (A.4.6): el arco del Magnetar
    // (T6-05) y el haz de poder (T6-11). El tramo se ancla en la punta y, si no cabe entero, se recorta del
    // lado del nacimiento, donde cae debajo del sprite del nodo.
    static class StrokeSprites
    {
        // Un tramo recortado por (sprite, ancho visible): un sprite nuevo sobre la misma textura, sin copiar
        // píxeles. Los anchos son enteros y no pasan de 256, así que la caché es pequeña.
        static readonly Dictionary<(Sprite, int), Sprite> Cropped = new Dictionary<(Sprite, int), Sprite>();

        // Las `width` columnas del lado de la punta, con el pivote en ese mismo lado, como el tramo entero.
        internal static Sprite Crop(Sprite tile, int width)
        {
            if (Cropped.TryGetValue((tile, width), out Sprite cached) && cached != null)
                return cached;
            Rect rect = tile.rect;
            var cropped = Sprite.Create(tile.texture, new Rect(rect.xMax - width, rect.y, width, rect.height),
                new Vector2(1f, tile.pivot.y / rect.height), tile.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cropped.name = $"{tile.name}_crop{width}";
            Cropped[(tile, width)] = cropped;
            return cropped;
        }
    }
}
