using System;
using System.Collections.Generic;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-06: la línea de envío con su arte, «El Hilo Vivo» v2 (14.9, A.4.6). Sustituye a ProvisionalSendLineView,
    // que se aparta en cuanto hay InputArt completo. Solo lee SendLineState.
    //
    // - Del centro de cada origen al cursor, una por origen, en cada frame (14.9: sigue al cursor, en Update).
    // - Bucle de 16 frames a 12 fps. Cada línea arranca desfasada por su orden en la selección (14.9): con un
    //   lazo de cuatro nodos, cuatro filamentos al unísono parecerían uno copiado cuatro veces.
    // - Se monta como el haz de poder: nacimiento en el origen, punta con el contacto en el cursor y el tramo en
    //   mosaico desde la punta, recortado en el borde del nacimiento. Capa 0 de 14.7, debajo de los nodos.
    // - Tiempo escalado, como el tablero: en pausa la entrada se suspende y la línea no existe.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class SendLineVisual : MonoBehaviour
    {
        public const float FramesPerSecond = 12f;

        // Frames de desfase entre una línea y la siguiente de la selección: primo con 16, así que hasta dieciséis
        // líneas no repiten frame.
        public const int PhaseStep = 5;

        const float PixelsPerUnit = 64f;

        // Geometría de world_link_sheet (A.4.6), la misma que la del haz.
        const int BirthPixels = 16;
        const int TilePixels = 256;
        const int ContactPixels = 24;

        [SerializeField] SendLineState state;
        [SerializeField] InputArt art;

        internal sealed class Line
        {
            public Transform Root;
            public SpriteRenderer Birth;
            public SpriteRenderer Tip;
            public readonly List<SpriteRenderer> Tiles = new List<SpriteRenderer>();
            public int Frame;
            public int Length;
        }

        readonly List<Line> lines = new List<Line>();
        double clock;

        public bool HasArt => art != null && art.IsComplete;

        internal IReadOnlyList<Line> Lines => lines;

        internal int VisibleCount
        {
            get
            {
                int count = 0;
                foreach (Line line in lines)
                {
                    if (line.Root.gameObject.activeSelf)
                        count++;
                }
                return count;
            }
        }

        void LateUpdate()
        {
            Advance(Time.deltaTime);
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(InputArt inputArt, SendLineState sendLine)
        {
            art = inputArt;
            state = sendLine;
        }

        // Avanza el reloj del bucle y coloca las líneas. Público para los tests, donde no hay LateUpdate.
        public void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");
            clock += deltaTime;

            IReadOnlyList<NodeController> origins = HasArt && state != null ? state.Origins : null;
            int shown = 0;
            if (origins != null)
            {
                int tick = (int)Math.Floor(clock * FramesPerSecond + 1e-9);
                for (int i = 0; i < origins.Count; i++)
                {
                    if (Place(LineAt(shown), origins[i].transform.position, state.Cursor, FrameOf(tick, i, art.LinkFrames)))
                        shown++;
                }
            }
            for (int i = shown; i < lines.Count; i++)
                lines[i].Root.gameObject.SetActive(false);
        }

        // El frame de la línea `index` de la selección en el tick `tick` del reloj.
        public static int FrameOf(int tick, int index, int frameCount) =>
            ((tick + index * PhaseStep) % frameCount + frameCount) % frameCount;

        bool Place(Line line, Vector2 from, Vector2 to, int frame)
        {
            Vector2 delta = to - from;
            int length = Mathf.RoundToInt(delta.magnitude * PixelsPerUnit);
            if (length <= 0)
                return false;

            line.Frame = frame;
            line.Length = length;
            line.Root.gameObject.SetActive(true);
            line.Root.position = new Vector3(from.x, from.y, 0f);
            line.Root.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.Birth.sprite = art.LinkBirth[frame];
            line.Tip.sprite = art.LinkTip[frame];
            line.Tip.transform.localPosition = new Vector3(length / PixelsPerUnit, 0f, 0f);

            // El tramo, de la punta hacia el origen: teselas enteras y la última recortada en el borde del
            // nacimiento, que cae debajo del sprite del nodo. Una línea muy corta no tiene tramo.
            Sprite tile = art.LinkTile[frame];
            int used = 0;
            for (int right = length - ContactPixels; right > BirthPixels; right -= TilePixels)
            {
                int width = Mathf.Min(TilePixels, right - BirthPixels);
                SpriteRenderer piece = TileAt(line, used++);
                piece.sprite = width == TilePixels ? tile : StrokeSprites.Crop(tile, width);
                piece.transform.localPosition = new Vector3(right / PixelsPerUnit, 0f, 0f);
                piece.enabled = true;
            }
            for (int i = used; i < line.Tiles.Count; i++)
                line.Tiles[i].enabled = false;
            return true;
        }

        Line LineAt(int index)
        {
            while (lines.Count <= index)
            {
                var root = new GameObject($"SendLine{lines.Count}").transform;
                root.SetParent(transform, false);
                var line = new Line { Root = root };
                line.Birth = Piece(root, "Birth");
                line.Tip = Piece(root, "Tip");
                root.gameObject.SetActive(false);
                lines.Add(line);
            }
            return lines[index];
        }

        static SpriteRenderer TileAt(Line line, int index)
        {
            while (line.Tiles.Count <= index)
                line.Tiles.Add(Piece(line.Root, $"Tile{line.Tiles.Count}"));
            return line.Tiles[index];
        }

        static SpriteRenderer Piece(Transform root, string name)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(root, false);
            renderer.sortingOrder = BoardOrder.Strokes; // capa 0 de 14.7
            return renderer;
        }
    }
}
