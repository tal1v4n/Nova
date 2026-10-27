using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nova.UI
{
    // La línea de envío (ART-WLD-LINK, 14.9, A.4.6) dentro de un lienzo de menú: el cursor del menú
    // principal (13.8) y los tramos ganados de la carta estelar (13.9). Es la misma hoja que el
    // tablero, a ×2 —el ×2 del arte a 1080p, 13.12— y montada igual: nacimiento en el origen, punta
    // con el contacto en el destino y el tramo anclado en la punta, recortado del lado del origen,
    // que es donde va el sprite del nodo encima.
    //
    // Cada pieza es una RawImage que recorta su rect de la hoja; el bucle de 16 frames corre a 12 fps
    // con tiempo no escalado, desfasado por `phase` para que dos líneas no ondulen al unísono.
    [DisallowMultipleComponent]
    internal sealed class UiPlasma : MonoBehaviour
    {
        // tools/gen_link_v1.py: celdas de 256 × 16 en tres filas —nacimiento, tramo y punta— y 16
        // frames. La punta tiene el contacto en x = 24 y 8 px de luz detrás.
        public const int Cell = 256;
        public const int Height = 16;
        public const int Birth = 16;
        public const int Tile = 256;
        public const int Tip = 32;
        public const int Contact = 24;
        public const int Frames = 16;
        public const float Fps = 12f;
        public const float Scale = 2f;

        sealed class Piece
        {
            public RawImage Image;
            public int Row;
            public int From;
            public int Width;
        }

        Texture2D sheet;
        RectTransform rect;
        readonly List<Piece> pieces = new List<Piece>();
        int phase;
        float time;
        int shownFrame = -1;

        // Largo del origen al contacto, en píxeles del arte.
        public float Length { get; private set; }
        public int PieceCount => pieces.Count;
        public int Frame => shownFrame;

        // Una línea de `from` a `to`, en coordenadas de la maqueta (origen arriba a la izquierda).
        public static UiPlasma Create(Transform parent, Texture2D sheet, Vector2 from, Vector2 to, int phase = 0)
        {
            RectTransform rect = UiKit.Node(parent, "Plasma", 0f, 0f, 10f, Height * Scale);
            var line = rect.gameObject.AddComponent<UiPlasma>();
            line.sheet = sheet;
            line.rect = rect;
            line.phase = phase;
            line.Set(from, to);
            return line;
        }

        // Rehace la línea entre dos puntos. Los extremos caen en píxeles del arte: múltiplos de 2.
        public void Set(Vector2 from, Vector2 to)
        {
            from = Snap(from);
            to = Snap(to);
            Vector2 d = to - from;
            Length = Mathf.Round(d.magnitude / Scale);
            // El eje del trazo pasa por los dos puntos; el pivote es el origen, a media altura.
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(from.x, -from.y);
            rect.sizeDelta = new Vector2((Length + Tip - Contact) * Scale, Height * Scale);
            rect.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            Build();
            shownFrame = -1;
            Paint();
        }

        void Update()
        {
            time += Time.unscaledDeltaTime;
            Paint();
        }

        // Para los tests: avanza el bucle sin Update.
        public void Step(float deltaTime)
        {
            time += deltaTime;
            Paint();
        }

        // Las piezas de tools/gen_link_v1.py, linea(): nacimiento en 0, punta con el contacto en
        // Length y teselas enteras de la punta hacia atrás; la primera, recortada por su izquierda.
        void Build()
        {
            foreach (Piece piece in pieces)
            {
                if (piece.Image != null)
                    Discard(piece.Image.gameObject);
            }
            pieces.Clear();

            int length = (int)Length;
            int tipX = length - Contact;
            int x = tipX - Tile;
            var tiles = new List<(int at, int from, int width)>();
            while (x + Tile > Birth)
            {
                int cut = Mathf.Max(0, Birth - x);
                tiles.Add((x + cut, cut, Tile - cut));
                x -= Tile;
            }
            for (int i = tiles.Count - 1; i >= 0; i--)
                Add(1, tiles[i].at, tiles[i].from, tiles[i].width);
            Add(0, 0, 0, Birth);
            Add(2, tipX, 0, Tip);
        }

        void Add(int row, int at, int from, int width)
        {
            RectTransform node = UiKit.Node(rect, "Piece", at * Scale, 0f, width * Scale, Height * Scale);
            var image = node.gameObject.AddComponent<RawImage>();
            image.texture = sheet;
            image.raycastTarget = false;
            if (sheet == null)
                image.color = new Color(0f, 0f, 0f, 0f);
            pieces.Add(new Piece { Image = image, Row = row, From = from, Width = width });
        }

        void Paint()
        {
            int frame = ((int)Mathf.Floor(time * Fps) + phase) % Frames;
            if (frame < 0)
                frame += Frames;
            if (frame == shownFrame || sheet == null)
            {
                shownFrame = frame;
                return;
            }
            shownFrame = frame;
            float w = sheet.width;
            float h = sheet.height;
            foreach (Piece piece in pieces)
            {
                // La fila 0 de la hoja es la de arriba; en UV, v crece hacia arriba.
                float v = (h - (piece.Row + 1) * Height) / h;
                piece.Image.uvRect = new Rect((frame * Cell + piece.From) / w, v, piece.Width / w, Height / h);
            }
        }

        static Vector2 Snap(Vector2 point) => new Vector2(Mathf.Round(point.x / Scale) * Scale, Mathf.Round(point.y / Scale) * Scale);

        static void Discard(GameObject go)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }
    }
}
