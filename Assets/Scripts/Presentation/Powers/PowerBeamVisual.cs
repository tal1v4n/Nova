using System;
using System.Collections.Generic;
using Nova.Data;
using Nova.Systems;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-11: el haz de poder con su arte (7.2.2, A.4.2): «El rosario de escarcha», «La cadena de
    // desintegración» y «El desgarro». Sustituye a ProvisionalBeamView, que se aparta en cuanto hay
    // PowerBeamArt completo. Solo lee: recorre PowerBeamSystem.Active en LateUpdate y no escribe en nada.
    //
    // - Del centro del Púlsar al centro del objetivo, los dos extremos fijados al lanzar (PowerBeam).
    // - La hoja es la del poder, no la de la facción (7.2.2, 14.4).
    // - 3 frames a 12 fps, sin bucle. El frame sale del reloj del propio haz, que corre en el tick fijo:
    //   en pausa se queda quieto, y el último frame es el que está en pantalla cuando el efecto cae o el
    //   haz se disipa (7.2.2). Al terminar, PowerBeamSystem lo saca de Active y el trazo desaparece.
    // - Capa 0 de 14.7, por debajo de los nodos: el sprite del Púlsar tapa el nacimiento y el del
    //   objetivo la punta, y el haz parece salir del borde de uno y entrar en el otro.
    //
    // Se monta como el arco del Magnetar (Filamento.linea): nacimiento en el Púlsar, punta con el
    // contacto en el objetivo y el tramo en mosaico desde la punta hacia el Púlsar, con la última tesela
    // recortada en el borde del nacimiento. El haz no se estira (T.7 #30): un haz de 17 u son cuatro
    // teselas y media. Gira con el transform; el trazo es simétrico al píxel respecto a su eje (A.4.2).
    //
    // Vive en el objeto Systems, como ProtonVisual: un haz no es de ningún nodo.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class PowerBeamVisual : MonoBehaviour
    {
        public const float FramesPerSecond = 12f; // 7.2.2: 3 frames en 0.25 s
        const float PixelsPerUnit = 64f;
        const double MicrosPerSecond = 1_000_000d;

        // Geometría de la hoja (A.4.2, la de world_link_sheet): el nacimiento mide 16 px y el tramo 256, y
        // el contacto de la punta cae en su x = 24.
        const int BirthPixels = 16;
        const int TilePixels = 256;
        const int ContactPixels = 24;

        [Tooltip("Opcional: sin él se usa PowerBeamSystem.Instance.")]
        [SerializeField] PowerBeamSystem beams;

        [SerializeField] PowerBeamArt art;

        internal sealed class Stroke
        {
            public Transform Root;
            public SpriteRenderer Birth;
            public SpriteRenderer Tip;
            public readonly List<SpriteRenderer> Tiles = new List<SpriteRenderer>();
            public int Frame;
        }

        readonly List<Stroke> strokes = new List<Stroke>();

        // Con arte, ProvisionalBeamView se aparta.
        public bool HasArt => art != null && art.IsComplete;

        internal IReadOnlyList<Stroke> Strokes => strokes;

        internal int VisibleCount
        {
            get
            {
                int count = 0;
                foreach (Stroke stroke in strokes)
                {
                    if (stroke.Root.gameObject.activeSelf)
                        count++;
                }
                return count;
            }
        }

        void LateUpdate()
        {
            Refresh();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(PowerBeamArt beamArt, PowerBeamSystem beamSystem)
        {
            art = beamArt;
            beams = beamSystem;
        }

        public void Refresh()
        {
            PowerBeamSystem system = beams != null ? beams : PowerBeamSystem.Instance;
            IReadOnlyList<PowerBeam> active = HasArt && system != null ? system.Active : null;
            int shown = 0;
            if (active != null)
            {
                foreach (PowerBeam beam in active)
                {
                    if (Place(StrokeAt(shown), beam))
                        shown++;
                }
            }
            for (int i = shown; i < strokes.Count; i++)
                strokes[i].Root.gameObject.SetActive(false);
        }

        // El frame del haz según su propio reloj: 0 al lanzar y el último hasta que termina.
        internal static int FrameOf(PowerBeam beam, int frameCount)
        {
            int frame = (int)Math.Floor(beam.ElapsedMicros / MicrosPerSecond * FramesPerSecond);
            return Mathf.Clamp(frame, 0, frameCount - 1);
        }

        bool Place(Stroke stroke, PowerBeam beam)
        {
            Vector2 from = beam.From;
            Vector2 delta = beam.To - from;
            // Todo en píxeles enteros a lo largo del haz: la punta cae en el píxel del objetivo.
            int length = Mathf.RoundToInt(delta.magnitude * PixelsPerUnit);
            if (length <= 0)
                return false;

            PowerBeamArt.BeamFrames frames = art.For(beam.Type);
            int frame = FrameOf(beam, frames.FrameCount);
            stroke.Frame = frame;
            stroke.Root.gameObject.SetActive(true);
            stroke.Root.position = new Vector3(from.x, from.y, 0f);
            stroke.Root.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            stroke.Birth.sprite = frames.Birth[frame];
            stroke.Birth.transform.localPosition = Vector3.zero;
            stroke.Tip.sprite = frames.Tip[frame];
            stroke.Tip.transform.localPosition = new Vector3(length / PixelsPerUnit, 0f, 0f);

            // El tramo, de la punta hacia el Púlsar: teselas enteras y la última recortada en el borde del
            // nacimiento. Un haz más corto que nacimiento y punta juntos no tiene tramo.
            Sprite tile = frames.Tile[frame];
            int used = 0;
            for (int right = length - ContactPixels; right > BirthPixels; right -= TilePixels)
            {
                int width = Mathf.Min(TilePixels, right - BirthPixels);
                SpriteRenderer piece = TileAt(stroke, used++);
                piece.sprite = width == TilePixels ? tile : StrokeSprites.Crop(tile, width);
                piece.transform.localPosition = new Vector3(right / PixelsPerUnit, 0f, 0f);
                piece.enabled = true;
            }
            for (int i = used; i < stroke.Tiles.Count; i++)
                stroke.Tiles[i].enabled = false;
            return true;
        }

        Stroke StrokeAt(int index)
        {
            while (strokes.Count <= index)
            {
                // Hijo de Systems pero orientado en mundo: cada haz gira hacia su objetivo.
                var root = new GameObject($"PowerBeam{strokes.Count}").transform;
                root.SetParent(transform, false);
                var stroke = new Stroke { Root = root };
                stroke.Birth = Piece(root, "Birth");
                stroke.Tip = Piece(root, "Tip");
                root.gameObject.SetActive(false);
                strokes.Add(stroke);
            }
            return strokes[index];
        }

        static SpriteRenderer TileAt(Stroke stroke, int index)
        {
            while (stroke.Tiles.Count <= index)
                stroke.Tiles.Add(Piece(stroke.Root, $"Tile{stroke.Tiles.Count}"));
            return stroke.Tiles[index];
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
