using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Systems;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-05: el arco de plasma de cada disparo del Magnetar (14.9, ART-WLD-RAD-SHOT). Escucha
    // MagnetarTurret.OnShot y no escribe en nada: el −2 de Carga ya se aplicó en el tick.
    //
    // - 2 frames a 12 fps (0.17 s), sin bucle, en la rampa de la facción que tenía al disparar.
    // - La punta sigue al protón durante sus 2 frames. Si el disparo lo derriba —o termina por otra
    //   cosa—, se queda en su última posición y el arco acaba sus frames ahí (14.9).
    // - Un disparo nuevo corta el arco anterior: un Magnetar nunca enseña dos (14.9, Rayo Cósmico).
    // - El arco acaba en el borde de la bolsa del protón, no en su centro (T.7 #56): el último píxel de
    //   la punta cae en ProtonArt.BagRadiusPixels de su talla, y el trazo no cruza las cifras de la Carga.
    //   Sin ProtonArt, el contacto sigue en el centro del protón, como en el provisional.
    // - Capa 0 de 14.7: por debajo del nodo y del protón, por encima del fondo y del anillo de radio.
    //
    // El trazo se monta como lo genera tools/nova_core.py (Filamento.linea): nacimiento en el centro,
    // punta con el contacto en el protón y el tramo anclado en la punta, recortado del lado del nodo,
    // donde cae debajo de su sprite. El tramo mide 256 px y un arco no pasa de ~190, así que casi
    // nunca hace falta más de uno; si hiciera falta, se repite hacia el nodo. Gira con el transform.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController), typeof(MagnetarTurret))]
    public sealed class MagnetarShotArc : MonoBehaviour
    {
        public const float FramesPerSecond = 12f; // 14.5
        const float PixelsPerUnit = 64f;

        // Geometría de la hoja (A.4.6): el nacimiento mide 16 px y el tramo 256, y el contacto de la
        // punta cae en su x = 24.
        const int BirthPixels = 16;
        const int TilePixels = 256;
        const int ContactPixels = 24;
        const int TipPixels = 32; // de x = 24 a 31 hay dibujo: la cruz del golpe sobresale 8 px del contacto

        [SerializeField] MagnetarArt art;
        [Tooltip("Para acabar el arco en el borde de la bolsa del protón (T.7 #56). Sin él, acaba en su centro.")]
        [SerializeField] ProtonArt protonArt;

        NodeController node;
        MagnetarTurret turret;
        Transform root;
        SpriteRenderer birth;
        SpriteRenderer tip;
        readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();

        MagnetarArt.ShotFrames frames;
        Proton target;
        Vector2 tipWorld;
        int bagPixels;
        float elapsed;
        bool showing;
        bool subscribed;

        // Con arte, ProvisionalMagnetarView se aparta.
        public bool HasArt => art != null && art.IsComplete;

        internal bool IsShowing => showing;
        internal int Frame => showing ? Mathf.FloorToInt(elapsed * FramesPerSecond) : -1;
        internal Vector2 TipPosition => tipWorld;
        internal int BagPixels => bagPixels;
        internal SpriteRenderer Birth { get { EnsureBuilt(); return birth; } }
        internal SpriteRenderer Tip { get { EnsureBuilt(); return tip; } }
        internal IReadOnlyList<SpriteRenderer> Tiles => tiles;
        internal Transform Root { get { EnsureBuilt(); return root; } }

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
            Hide();
        }

        void Update()
        {
            if (showing)
                Advance(Time.deltaTime);
        }

        // Para la plantilla de nodo y los tests: el cableado normal es por el Inspector.
        public void Configure(MagnetarArt magnetarArt, ProtonArt protonSizes = null)
        {
            art = magnetarArt;
            protonArt = protonSizes;
        }

        internal void Subscribe()
        {
            if (subscribed)
                return;
            Turret.OnShot += HandleShot;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (!subscribed)
                return;
            Turret.OnShot -= HandleShot;
            subscribed = false;
        }

        // Avanza el reloj del arco. En partida, el tiempo de juego de cada frame: en pausa no corre.
        internal void Advance(float deltaTime)
        {
            if (!showing)
                return;
            elapsed += deltaTime;
            if (target != null)
                Track(target);
            int frame = Frame;
            if (frame >= frames.FrameCount)
            {
                Hide();
                return;
            }
            Layout(frame);
        }

        void HandleShot(MagnetarTurret source, Proton proton)
        {
            if (!HasArt || proton == null)
                return;
            Follow(proton);
            frames = art.ShotFor(Node.Faction);
            elapsed = 0f;
            showing = true;
            Layout(0);
        }

        void Follow(Proton proton)
        {
            Release();
            target = proton;
            Track(proton);
            target.OnFinished += HandleTargetFinished;
        }

        // La punta va al protón y la bolsa, a la talla de su Carga de ahora: si el disparo lo encoge, el
        // arco lo sigue. Un protón ya sin Carga conserva la última bolsa que tuvo.
        void Track(Proton proton)
        {
            tipWorld = proton.transform.position;
            if (protonArt != null && protonArt.Sizes > 0 && proton.Charge > 0)
                bagPixels = protonArt.BagRadiusPixels(protonArt.SizeFor(proton.Charge));
            else if (protonArt == null || protonArt.Sizes == 0)
                bagPixels = 0;
        }

        // El protón termina —derribado por este disparo, por otro, o al llegar—: la punta se queda
        // donde estaba. OnFinished llega con el protón todavía en su sitio.
        void HandleTargetFinished(Proton proton)
        {
            if (proton != target)
                return;
            tipWorld = proton.transform.position;
            Release();
        }

        void Release()
        {
            if (target != null)
                target.OnFinished -= HandleTargetFinished;
            target = null;
        }

        void Hide()
        {
            Release();
            showing = false;
            if (root != null)
                root.gameObject.SetActive(false);
        }

        void Layout(int frame)
        {
            EnsureBuilt();
            root.gameObject.SetActive(true);

            Vector2 from = transform.position;
            Vector2 delta = tipWorld - from;
            root.position = new Vector3(from.x, from.y, transform.position.z);
            root.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            // Todo en píxeles enteros a lo largo del arco. Sin bolsa, el contacto cae en el píxel del protón;
            // con ella, la punta se retira para que su último píxel toque el borde de la bolsa (T.7 #56).
            int length = Mathf.RoundToInt(delta.magnitude * PixelsPerUnit);
            if (bagPixels > 0)
                length = Mathf.Max(0, length - bagPixels - (TipPixels - ContactPixels));
            int tipLeft = length - ContactPixels;

            birth.sprite = frames.Birth[frame];
            birth.transform.localPosition = Vector3.zero;
            tip.sprite = frames.Tip[frame];
            tip.transform.localPosition = new Vector3(length / PixelsPerUnit, 0f, 0f);

            // El tramo, de la punta hacia el nodo: teselas enteras y la última recortada en el borde
            // del nacimiento. Un arco más corto que nacimiento y punta juntos no tiene tramo.
            Sprite tile = frames.Tile[frame];
            int used = 0;
            for (int right = tipLeft; right > BirthPixels; right -= TilePixels)
            {
                int width = Mathf.Min(TilePixels, right - BirthPixels);
                SpriteRenderer piece = TileRenderer(used++);
                piece.sprite = width == TilePixels ? tile : StrokeSprites.Crop(tile, width);
                piece.transform.localPosition = new Vector3(right / PixelsPerUnit, 0f, 0f);
                piece.enabled = true;
            }
            for (int i = used; i < tiles.Count; i++)
                tiles[i].enabled = false;
        }

        SpriteRenderer TileRenderer(int index)
        {
            while (tiles.Count <= index)
                tiles.Add(Piece($"Tile{tiles.Count}"));
            return tiles[index];
        }

        void EnsureBuilt()
        {
            if (root != null)
                return;
            // Hijo del nodo pero orientado en mundo: gira hacia el protón en cada frame.
            root = new GameObject("ShotArc").transform;
            root.SetParent(transform, false);
            birth = Piece("Birth");
            tip = Piece("Tip");
            root.gameObject.SetActive(false);
        }

        SpriteRenderer Piece(string name)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(root, false);
            renderer.sortingOrder = BoardOrder.Strokes; // capa 0 de 14.7
            return renderer;
        }

        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
        MagnetarTurret Turret => turret != null ? turret : turret = GetComponent<MagnetarTurret>();
    }
}
