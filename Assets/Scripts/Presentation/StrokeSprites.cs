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

        static readonly Dictionary<(Sprite, int), Sprite> CroppedStart = new Dictionary<(Sprite, int), Sprite>();
        static readonly Dictionary<(Sprite, int, int), Sprite> CroppedCorner = new Dictionary<(Sprite, int, int), Sprite>();

        // T6-06, el tramo del lazo: las `width` columnas del principio, con el pivote a la izquierda. El lazo ancla
        // cada lado en la esquina de la que sale la marcha y recorta en la otra (A.4.3).
        internal static Sprite CropStart(Sprite tile, int width)
        {
            if (CroppedStart.TryGetValue((tile, width), out Sprite cached) && cached != null)
                return cached;
            Rect rect = tile.rect;
            var cropped = Sprite.Create(tile.texture, new Rect(rect.x, rect.y, width, rect.height),
                new Vector2(0f, 0.5f), tile.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cropped.name = $"{tile.name}_start{width}";
            CroppedStart[(tile, width)] = cropped;
            return cropped;
        }

        // T6-06, la esquina del lazo en un lazo pequeño: su `width` × `height` de arriba a la izquierda, con el
        // pivote en esa esquina, para que las cuatro no se crucen (`monta` en tools/gen_lasso_v1.py).
        internal static Sprite CropCorner(Sprite corner, int width, int height)
        {
            if (CroppedCorner.TryGetValue((corner, width, height), out Sprite cached) && cached != null)
                return cached;
            Rect rect = corner.rect;
            var cropped = Sprite.Create(corner.texture, new Rect(rect.x, rect.yMax - height, width, height),
                new Vector2(0f, 1f), corner.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cropped.name = $"{corner.name}_{width}x{height}";
            CroppedCorner[(corner, width, height)] = cropped;
            return cropped;
        }
    }
}
