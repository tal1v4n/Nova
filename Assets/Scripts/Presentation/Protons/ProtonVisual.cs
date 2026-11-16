using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Systems;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-04: los protones en vuelo con su arte (14.3, A.4.2, «La bolsa»). Sustituye a ProvisionalProtonView,
    // que se aparta en cuanto hay ProtonArt completo. Solo lee: recorre ProtonRegistry.InFlight y escucha
    // el final de cada vuelo.
    //
    // - La talla sale de la Carga en cada frame (ProtonArt.SizeFor), una cada 5 de Carga: si un Magnetar
    //   la desgasta por debajo de un escalón, el protón encoge en el acto. El color es el del emisor y no cambia (6.1, 17 caso 3).
    // - El cuerpo es un bucle de 16 frames a 12 fps. Cada vuelo arranca en otro frame, para que dos
    //   protones lanzados juntos no giren al unísono (la misma regla que la línea de envío, 14.9).
    // - La Carga se compone con las cifras propias (world_charge_digits), una por sprite y en píxeles
    //   enteros, centrada en la celda de 48 exactamente como la centra el generador.
    // - El cuerpo no gira nunca: la estela son gotas que se sueltan cada pocos píxeles de recorrido, en
    //   píxel entero, y bajan un paso de trama cada 2 frames. Una gota no se ve hasta que sale de la
    //   bolsa, que es hueca; y al acabar el vuelo las que quedan terminan de apagarse donde están.
    // - Todo se coloca en píxeles enteros de la rejilla de 960 × 540: si el cuerpo y las cifras se
    //   redondeasen cada uno por su lado, el número bailaría un píxel dentro de la bolsa.
    //
    // Vive en el objeto Systems y no en cada protón, porque el protón vuelve al pool al llegar y su estela
    // tiene que sobrevivirle.
    //
    // T6-07: cuando un protón muere derribado o se queda huérfano, avisa con OnEnded de dónde estaba, de qué
    // bando y talla era y en qué frame de su bucle iba: ART-FX-KILL y ART-FX-VANISH lo sustituyen sin salto
    // desde ese frame (A.5), y los coloca VFXPool. El protón y su Carga dejan de dibujarse en ese mismo tick.
    // Cómo acaba un vuelo que no llega: derribado por un Magnetar (6.3) o huérfano al perder su bando (10.1).
    public enum ProtonFate
    {
        ShotDown = 0,
        Vanished = 1,
    }

    // Lo que hace falta para sustituir al protón en su sitio (T6-07).
    public readonly struct ProtonEnding
    {
        public readonly ProtonFate Fate;
        public readonly Faction Faction;
        public readonly int Size;       // talla de ProtonArt, de 0 a Sizes − 1
        public readonly int LoopFrame;  // frame del bucle del cuerpo, de 0 a 15
        public readonly Vector3 Position; // en píxel entero, donde estaba el cuerpo

        public ProtonEnding(ProtonFate fate, Faction faction, int size, int loopFrame, Vector3 position)
        {
            Fate = fate;
            Faction = faction;
            Size = size;
            LoopFrame = loopFrame;
            Position = position;
        }
    }

    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class ProtonVisual : MonoBehaviour
    {
        const float PixelsPerUnit = 64f;
        const int HalfCell = 24; // la celda del protón mide 48 (A.4.2)
        const int MaxDigits = 3; // la Carga no pasa de 100 (4.0)
        const int FrameOffsetStep = 5; // primo con 16: los vuelos seguidos reparten los 16 frames

        [Tooltip("Opcional: sin él se usa ProtonRegistry.Instance.")]
        [SerializeField] ProtonRegistry registry;

        [SerializeField] ProtonArt art;

        sealed class Visual
        {
            public Proton Proton;
            public Transform Root;
            public SpriteRenderer Body;
            public readonly SpriteRenderer[] Digits = new SpriteRenderer[MaxDigits];
            public Faction Faction;
            public int Size;
            public int ShownCharge = -1;
            public int FrameOffset;
            public float Age;
            public int DropsReleased;
            public float TraveledPixels;
            public ProtonFate? Fate;
        }

        sealed class Drop
        {
            public SpriteRenderer Renderer;
            public Visual Owner; // null cuando su vuelo ya terminó
            public float DistancePixels;
            public Faction Faction;
            public int Size;
            public float Age;
        }

        readonly Dictionary<Proton, Visual> visuals = new Dictionary<Proton, Visual>();
        readonly Stack<Visual> freeVisuals = new Stack<Visual>();
        readonly List<Drop> drops = new List<Drop>();
        readonly Stack<SpriteRenderer> freeDrops = new Stack<SpriteRenderer>();
        readonly List<Proton> started = new List<Proton>();
        int flights;

        public bool HasArt => art != null && art.IsComplete;

        // Un protón derribado o huérfano, en el tick en que deja de dibujarse (T6-07).
        public event Action<ProtonEnding> OnEnded;

        void LateUpdate()
        {
            Advance(Time.deltaTime);
        }

        void OnDisable()
        {
            foreach (Visual visual in new List<Visual>(visuals.Values))
                End(visual);
            foreach (Drop drop in drops)
                ReleaseDrop(drop);
            drops.Clear();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(ProtonArt protonArt, ProtonRegistry protonRegistry = null)
        {
            art = protonArt;
            registry = protonRegistry;
        }

        // Un paso de presentación. En partida, el tiempo de juego del frame: en pausa no corre nada.
        internal void Advance(float deltaTime)
        {
            if (!HasArt)
                return;

            foreach (Visual visual in visuals.Values)
                visual.Age += deltaTime;
            foreach (Drop drop in drops)
                drop.Age += deltaTime;

            ProtonRegistry source = registry != null ? registry : ProtonRegistry.Instance;
            if (source != null)
            {
                started.Clear();
                foreach (Proton proton in source.InFlight)
                {
                    if (proton != null && proton.InFlight && !visuals.ContainsKey(proton))
                        started.Add(proton);
                }
                foreach (Proton proton in started)
                    Begin(proton);
            }

            foreach (Visual visual in visuals.Values)
                Layout(visual);
            UpdateDrops();
        }

        void Begin(Proton proton)
        {
            Visual visual = freeVisuals.Count > 0 ? freeVisuals.Pop() : Build();
            visual.Proton = proton;
            visual.Faction = proton.Faction;
            visual.ShownCharge = -1;
            visual.FrameOffset = flights++ * FrameOffsetStep;
            visual.Age = 0f;
            visual.DropsReleased = 0;
            visual.TraveledPixels = 0f;
            visual.Root.gameObject.SetActive(true);
            visual.Fate = null;
            visuals.Add(proton, visual);
            proton.OnShotDown += HandleShotDown;
            proton.OnVanished += HandleVanished;
            proton.OnFinished += HandleFinished;
        }

        // Los dos llegan justo antes de OnFinished, con el protón todavía en su sitio.
        void HandleShotDown(Proton proton) => MarkFate(proton, ProtonFate.ShotDown);

        void HandleVanished(Proton proton) => MarkFate(proton, ProtonFate.Vanished);

        void MarkFate(Proton proton, ProtonFate fate)
        {
            if (visuals.TryGetValue(proton, out Visual visual))
                visual.Fate = fate;
        }

        // El frame del bucle que el cuerpo enseña ahora: el de Layout.
        int LoopFrame(Visual visual) =>
            (visual.FrameOffset + Mathf.FloorToInt(visual.Age * art.FramesPerSecond)) % art.FramesFor(visual.Faction, visual.Size).Body.Length;

        // OnFinished llega con el protón todavía en su sitio: se sueltan las gotas que falten hasta ahí y
        // la estela sigue apagándose sola.
        void HandleFinished(Proton proton)
        {
            if (visuals.TryGetValue(proton, out Visual visual))
            {
                visual.TraveledPixels = Vector2.Distance(proton.Origin, proton.transform.position) * PixelsPerUnit;
                ReleaseDrops(visual);
                if (visual.Fate.HasValue)
                {
                    // La talla, la de la Carga que le queda: un derribado la tiene a 0 y cae en la primera.
                    int size = art.SizeFor(Mathf.Max(proton.Charge, 1));
                    OnEnded?.Invoke(new ProtonEnding(visual.Fate.Value, visual.Faction, size, LoopFrame(visual),
                        Snap(proton.transform.position)));
                }
                End(visual);
            }
        }

        void End(Visual visual)
        {
            visual.Proton.OnShotDown -= HandleShotDown;
            visual.Proton.OnVanished -= HandleVanished;
            visual.Proton.OnFinished -= HandleFinished;
            visuals.Remove(visual.Proton);
            foreach (Drop drop in drops)
            {
                if (drop.Owner == visual)
                    drop.Owner = null;
            }
            visual.Proton = null;
            visual.Root.gameObject.SetActive(false);
            freeVisuals.Push(visual);
        }

        Visual Build()
        {
            var root = new GameObject("Proton").transform;
            root.SetParent(transform, false);

            var body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(root, false);
            body.sortingOrder = BoardOrder.Proton;

            var visual = new Visual { Root = root, Body = body };
            for (int i = 0; i < MaxDigits; i++)
            {
                var digit = new GameObject($"Digit{i}").AddComponent<SpriteRenderer>();
                digit.transform.SetParent(root, false);
                digit.sortingOrder = BoardOrder.ProtonCharge;
                visual.Digits[i] = digit;
            }
            return visual;
        }

        void Layout(Visual visual)
        {
            Proton proton = visual.Proton;
            Vector2 position = proton.transform.position;
            visual.Root.position = Snap(position);

            int charge = proton.Charge;
            visual.Size = art.SizeFor(charge);
            Sprite[] body = art.FramesFor(visual.Faction, visual.Size).Body;
            visual.Body.sprite = body[(visual.FrameOffset + Mathf.FloorToInt(visual.Age * art.FramesPerSecond)) % body.Length];

            if (charge != visual.ShownCharge)
                LayoutCharge(visual, charge);

            visual.TraveledPixels = Vector2.Distance(proton.Origin, position) * PixelsPerUnit;
            ReleaseDrops(visual);
        }

        // Las cifras, centradas en la celda de 48 como en tools/gen_proton_v2.py: la caja del número, con
        // su contorno, empieza en round(24 − ancho/2) y su fila de arriba en round(24 − alto/2). Math.Round
        // redondea al par, como round() de Python. El ancho de la caja es 1 + Σ avance: cada cifra avanza
        // su ancho más la columna de contorno que comparte con la siguiente, y la última cierra con 1.
        void LayoutCharge(Visual visual, int charge)
        {
            visual.ShownCharge = charge;
            string text = charge.ToString();
            int height = art.DigitHeight;
            int width = 1;
            foreach (char c in text)
                width += art.Advance(c - '0');

            int left = (int)Math.Round(HalfCell - width / 2.0) - HalfCell;
            int top = (int)Math.Round(HalfCell - height / 2.0);
            int bottom = HalfCell - (top + height);

            int x = left;
            for (int i = 0; i < MaxDigits; i++)
            {
                SpriteRenderer renderer = visual.Digits[i];
                if (i >= text.Length)
                {
                    renderer.enabled = false;
                    continue;
                }
                int value = text[i] - '0';
                renderer.enabled = true;
                renderer.sprite = art.Digit(value);
                renderer.transform.localPosition = new Vector3(x / PixelsPerUnit, bottom / PixelsPerUnit, 0f);
                x += art.Advance(value);
            }
        }

        // Una gota por cada DropSpacingPixels de recorrido, en su punto exacto del camino.
        void ReleaseDrops(Visual visual)
        {
            int spacing = art.DropSpacingPixels;
            int due = Mathf.FloorToInt(visual.TraveledPixels / spacing);
            if (due <= visual.DropsReleased)
                return;

            Proton proton = visual.Proton;
            Vector2 origin = proton.Origin;
            Vector2 direction = ((Vector2)proton.transform.position - origin).normalized;
            while (visual.DropsReleased < due)
            {
                visual.DropsReleased++;
                float distance = visual.DropsReleased * spacing;
                SpriteRenderer renderer = freeDrops.Count > 0 ? freeDrops.Pop() : BuildDrop();
                renderer.transform.position = Snap(origin + direction * (distance / PixelsPerUnit));
                renderer.gameObject.SetActive(true);
                drops.Add(new Drop
                {
                    Renderer = renderer,
                    Owner = visual,
                    DistancePixels = distance,
                    Faction = visual.Faction,
                    Size = visual.Size,
                });
            }
        }

        SpriteRenderer BuildDrop()
        {
            var renderer = new GameObject("Drop").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sortingOrder = BoardOrder.ProtonTrail;
            return renderer;
        }

        void UpdateDrops()
        {
            float stepSeconds = art.DropStepFrames / art.FramesPerSecond;
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                Drop drop = drops[i];
                Sprite[] steps = art.FramesFor(drop.Faction, drop.Size).Drop;
                int step = Mathf.FloorToInt(drop.Age / stepSeconds);
                if (step >= steps.Length)
                {
                    ReleaseDrop(drop);
                    drops.RemoveAt(i);
                    continue;
                }
                drop.Renderer.sprite = steps[step];
                // La bolsa es hueca: dentro de ella la gota taparía la Carga.
                drop.Renderer.enabled = drop.Owner == null ||
                    drop.Owner.TraveledPixels - drop.DistancePixels >= art.BagRadiusPixels(drop.Owner.Size);
            }
        }

        void ReleaseDrop(Drop drop)
        {
            drop.Renderer.gameObject.SetActive(false);
            freeDrops.Push(drop.Renderer);
        }

        static Vector3 Snap(Vector2 position) =>
            new Vector3(Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit, 0f);

        // --- Para los tests ---

        internal bool TryGetVisual(Proton proton, out SpriteRenderer body, out SpriteRenderer[] digits, out Transform root)
        {
            if (proton != null && visuals.TryGetValue(proton, out Visual visual))
            {
                body = visual.Body;
                digits = visual.Digits;
                root = visual.Root;
                return true;
            }
            body = null;
            digits = null;
            root = null;
            return false;
        }

        internal int DropCount => drops.Count;

        internal IEnumerable<SpriteRenderer> Drops
        {
            get
            {
                foreach (Drop drop in drops)
                    yield return drop.Renderer;
            }
        }
    }
}
