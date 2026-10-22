using System.Collections.Generic;
using Nova.Data;
using Nova.Systems;
using UnityEngine;

namespace Nova.Presentation
{
    // T5-05b: el fondo con movimiento de un mapa (A.4.5, 15.8). Un sprite de 15 × 8.4375 u por
    // debajo de todo —también de los anillos de radio y de la capa 0 de 14.7— con el material de su
    // BackgroundDefinition, que reproduce fotograma(t) de tools/gen_fondos_v2.py.
    //
    // Lleva el tiempo del fondo en double, desde que se enseña: en partida, desde que empieza el
    // nivel. Avanza con el tiempo escalado, así que la pausa y las pantallas de fin lo congelan como
    // al resto del tablero (15.8, Tiempo de la UI y pausa). Cada fotograma reduce la fase de cada capa
    // a su periodo, en double, y se la pasa al material en un MaterialPropertyBlock: el material del
    // asset no cambia nunca.
    //
    // En Game toma el fondo del nivel que cargó LevelLoader; en los menús, el que se le asigne. La
    // ventana es la franja del fondo que enseña, en píxeles con el origen arriba a la izquierda: el
    // tablero entero por defecto, una franja para la carta estelar de 13.9.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class BackgroundView : MonoBehaviour
    {
        // Por debajo de todo lo que dibuja el tablero; la grava y el ovni van justo encima.
        public const int SortingOrder = -100;

        const string SkyName = "Sky";

        static readonly int WindowId = Shader.PropertyToID("_Window");
        static readonly RectInt FullBoard = new RectInt(0, 0, BackgroundDefinition.Width, BackgroundDefinition.Height);

        [Tooltip("El fondo que se enseña si no hay LevelLoader: el de un menú.")]
        [SerializeField] BackgroundDefinition definition;

        [Tooltip("Opcional: en Game, el fondo es el del nivel que carga.")]
        [SerializeField] LevelLoader loader;

        [Tooltip("La franja del fondo que se enseña, en píxeles con el origen arriba a la izquierda.")]
        [SerializeField] RectInt window = new RectInt(0, 0, BackgroundDefinition.Width, BackgroundDefinition.Height);

        SpriteRenderer sky;
        MaterialPropertyBlock block;
        readonly List<float[]> listValues = new List<float[]>();
        BackgroundDefinition built;
        double elapsed;
        bool subscribed;

        public BackgroundDefinition Definition => definition;
        public RectInt Window => window;

        // Segundos de fondo desde que se enseña: la t de fotograma(t).
        public double Elapsed => elapsed;

        internal SpriteRenderer Sky { get { Build(); return sky; } }

        void OnEnable()
        {
            Subscribe();
            if (loader != null && loader.Current != null)
                Show(loader.Current.Background);
            else
                Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void Update()
        {
            Advance(Time.deltaTime);
        }

        // Tiempo escalado: con la partida en pausa llega 0.
        public void Advance(float deltaTime)
        {
            elapsed += deltaTime;
            Refresh();
        }

        // Enseña otro fondo desde t = 0.
        public void Show(BackgroundDefinition background)
        {
            definition = background;
            elapsed = 0.0;
            Refresh();
        }

        // Lleva el fondo a un instante: para los tests, que comparan con los fotogramas de referencia.
        public void Seek(double time)
        {
            elapsed = time;
            Refresh();
        }

        // Para la escena y los tests: el cableado normal es por el Inspector.
        public void Configure(BackgroundDefinition background, LevelLoader levelLoader = null, RectInt? shownWindow = null)
        {
            Unsubscribe();
            definition = background;
            loader = levelLoader;
            window = shownWindow ?? FullBoard;
            elapsed = 0.0;
            built = null;
            if (isActiveAndEnabled)
                Subscribe();
            Refresh();
        }

        // Un punto del fondo en píxeles —los enteros caen en los bordes entre píxeles; el centro del
        // píxel (x, y) es (x + 0.5, y + 0.5)— en el espacio local de esta vista. Lo usa BackgroundFlyby
        // para poner sus sprites a píxel entero.
        public Vector2 PixelToLocal(Vector2 pixel)
        {
            const float ppu = BackgroundDefinition.PixelsPerUnit;
            return new Vector2((pixel.x - window.x - window.width / 2f) / ppu,
                               (window.y + window.height / 2f - pixel.y) / ppu);
        }

        public void Refresh()
        {
            Build();
            if (definition == null)
                return;

            block.SetVector(WindowId, new Vector4(window.x, window.y, window.width, window.height));
            foreach (BackgroundPhase phase in definition.Phases)
                block.SetFloat(phase.Property, (float)phase.Evaluate(elapsed));
            for (int i = 0; i < definition.Lists.Count; i++)
            {
                definition.Lists[i].Evaluate(elapsed, listValues[i]);
                block.SetFloatArray(definition.Lists[i].Property, listValues[i]);
            }
            sky.SetPropertyBlock(block);
        }

        void Build()
        {
            if (sky == null)
            {
                // El generador de la escena ya puede haberlo dejado hecho al configurarla.
                Transform existing = transform.Find(SkyName);
                if (existing == null || !existing.TryGetComponent(out sky))
                {
                    var go = new GameObject(SkyName);
                    go.transform.SetParent(transform, false);
                    sky = go.AddComponent<SpriteRenderer>();
                }
                sky.sortingOrder = SortingOrder;
                block = new MaterialPropertyBlock();
                built = null;
            }
            if (built == definition && sky.enabled == (definition != null))
                return;

            built = definition;
            sky.enabled = definition != null;
            listValues.Clear();
            block.Clear();
            if (definition == null)
                return;
            sky.sprite = definition.Frame;
            sky.sharedMaterial = definition.Material;
            // El sprite mide el tablero entero; una ventana lo encoge a su franja. Escala racional
            // exacta: la franja sigue cayendo en píxeles enteros.
            sky.transform.localScale = new Vector3((float)window.width / BackgroundDefinition.Width,
                                                   (float)window.height / BackgroundDefinition.Height, 1f);
            foreach (BackgroundPhaseList list in definition.Lists)
                listValues.Add(new float[list.Count]);
        }

        void HandleLoaded(LevelDefinition level)
        {
            Show(level.Background);
        }

        void Subscribe()
        {
            if (subscribed || loader == null)
                return;
            loader.OnLoaded += HandleLoaded;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed)
                return;
            if (loader != null)
                loader.OnLoaded -= HandleLoaded;
            subscribed = false;
        }
    }
}
