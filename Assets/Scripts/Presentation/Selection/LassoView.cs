using System;
using System.Collections.Generic;
using Nova.Data;
using Nova.Input;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-06: el lazo, «El marco» (A.4.3). Un rectángulo del punto de pulsación al cursor que vive lo que dura el
    // arrastre izquierdo. Solo lee LassoState. Sin InputArt no se dibuja: el lazo no tenía vista provisional.
    //
    // Se monta con dos piezas, como `monta()` de tools/gen_lasso_v1.py, que es la referencia:
    //   - la esquina de arriba a la izquierda (8 × 8, pivote en esa esquina), reflejada para las otras tres; en
    //     un lazo de menos de 16 px de lado se recorta a medio lazo para que las cuatro no se crucen;
    //   - el tramo de 8 × 1 (pivote a la izquierda, centrado en su fila) en mosaico por cada lado, girado para
    //     los verticales. Cada lado arranca en la esquina de la que sale la marcha —arriba desde la izquierda,
    //     la derecha desde arriba, abajo desde la derecha, la izquierda desde abajo— y recorta en la otra.
    // Reflejar y girar 90° son exactos al píxel. Todo cae en la rejilla de 64 px por unidad.
    //
    // 8 frames a 12 fps, en tiempo escalado.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class LassoView : MonoBehaviour
    {
        public const float FramesPerSecond = 12f;
        public const int CornerPixels = 8;  // lado de la pieza de esquina
        public const int ArmPixels = 6;     // la esquina dibuja hasta aquí; el tramo empieza después
        public const int TilePixels = 8;
        const float PixelsPerUnit = 64f;

        [SerializeField] LassoState state;
        [SerializeField] InputArt art;

        readonly SpriteRenderer[] corners = new SpriteRenderer[4];
        readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();
        Transform root;
        double clock;
        int usedTiles;

        public bool HasArt => art != null && art.IsComplete;

        internal bool IsVisible => root != null && root.gameObject.activeSelf;
        internal IReadOnlyList<SpriteRenderer> Corners => corners;
        internal int TileCount => usedTiles;
        internal IReadOnlyList<SpriteRenderer> Tiles => tiles;

        // El rectángulo dibujado, en píxeles de mundo (columnas y filas incluidas), para los tests.
        internal RectInt Pixels { get; private set; }

        void LateUpdate()
        {
            Advance(Time.deltaTime);
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(InputArt inputArt, LassoState lasso)
        {
            art = inputArt;
            state = lasso;
        }

        // Avanza el reloj y coloca el lazo. Público para los tests, donde no hay LateUpdate.
        public void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");
            clock += deltaTime;
            EnsureBuilt();

            if (!HasArt || state == null || !state.IsActive)
            {
                root.gameObject.SetActive(false);
                return;
            }
            root.gameObject.SetActive(true);

            int xMin = Pixel(Mathf.Min(state.From.x, state.To.x));
            int xMax = Pixel(Mathf.Max(state.From.x, state.To.x));
            int yMin = Pixel(Mathf.Min(state.From.y, state.To.y));
            int yMax = Pixel(Mathf.Max(state.From.y, state.To.y));
            int width = xMax - xMin + 1;
            int height = yMax - yMin + 1;
            Pixels = new RectInt(xMin, yMin, width, height);

            Sprite[] edge = art.LassoEdge;
            int frame = (int)(Math.Floor(clock * FramesPerSecond + 1e-9) % edge.Length);
            Sprite tile = edge[frame];
            usedTiles = 0;

            // Lados. Las coordenadas son de la esquina de abajo a la izquierda de cada píxel, y el pivote del
            // tramo cae en el centro de su fila; de ahí los + 0.5.
            int across = width - 2 * ArmPixels;
            for (int k = 0; k < across; k += TilePixels)
            {
                int run = Mathf.Min(TilePixels, across - k);
                Lay(tile, run, xMin + ArmPixels + k, yMax + 0.5f, 0f, false);           // arriba, hacia +x
                Lay(tile, run, xMax + 1 - ArmPixels - k, yMin + 0.5f, 0f, true);        // abajo, hacia −x
            }
            int down = height - 2 * ArmPixels;
            for (int k = 0; k < down; k += TilePixels)
            {
                int run = Mathf.Min(TilePixels, down - k);
                Lay(tile, run, xMax + 0.5f, yMax + 1 - ArmPixels - k, -90f, false);     // derecha, hacia −y
                Lay(tile, run, xMin + 0.5f, yMin + ArmPixels + k, 90f, false);          // izquierda, hacia +y
            }
            for (int i = usedTiles; i < tiles.Count; i++)
                tiles[i].enabled = false;

            // Esquinas, encima de los lados, reflejadas desde la de arriba a la izquierda.
            int cw = Mathf.Min(CornerPixels, (width + 1) / 2);
            int ch = Mathf.Min(CornerPixels, (height + 1) / 2);
            Sprite corner = cw == CornerPixels && ch == CornerPixels
                ? art.LassoCorner : StrokeSprites.CropCorner(art.LassoCorner, cw, ch);
            PlaceCorner(0, corner, xMin, yMax + 1, false, false);
            PlaceCorner(1, corner, xMax + 1, yMax + 1, true, false);
            PlaceCorner(2, corner, xMin, yMin, false, true);
            PlaceCorner(3, corner, xMax + 1, yMin, true, true);
        }

        static int Pixel(float world) => Mathf.FloorToInt(world * PixelsPerUnit);

        void Lay(Sprite tile, int run, float x, float y, float angle, bool flipX)
        {
            SpriteRenderer piece = TileAt(usedTiles++);
            piece.sprite = run == TilePixels ? tile : StrokeSprites.CropStart(tile, run);
            piece.flipX = flipX;
            piece.transform.localPosition = new Vector3(x / PixelsPerUnit, y / PixelsPerUnit, 0f);
            piece.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            piece.enabled = true;
        }

        void PlaceCorner(int index, Sprite sprite, int x, int y, bool flipX, bool flipY)
        {
            SpriteRenderer corner = corners[index];
            corner.sprite = sprite;
            corner.flipX = flipX;
            corner.flipY = flipY;
            corner.transform.localPosition = new Vector3(x / PixelsPerUnit, y / PixelsPerUnit, 0f);
        }

        SpriteRenderer TileAt(int index)
        {
            while (tiles.Count <= index)
                tiles.Add(Piece($"Edge{tiles.Count}"));
            return tiles[index];
        }

        SpriteRenderer Piece(string name)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(root, false);
            renderer.sortingOrder = BoardOrder.Lasso;
            return renderer;
        }

        void EnsureBuilt()
        {
            if (root != null)
                return;
            root = new GameObject("Lasso").transform;
            root.SetParent(transform, false);
            for (int i = 0; i < corners.Length; i++)
                corners[i] = Piece($"Corner{i}");
            root.gameObject.SetActive(false);
        }
    }
}
