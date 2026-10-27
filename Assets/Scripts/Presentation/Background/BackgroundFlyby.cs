using Nova.Data;
using UnityEngine;

namespace Nova.Presentation
{
    // T5-05b: lo que el fondo mueve como objeto (NOVA_Arte_Tecnica.md, «Fondos con movimiento»,
    // regla 7): el ovni de los tres mapas y la grava del Cinturón, como sprites a píxel entero y con
    // sus hojas a 12 fps, justo encima del fondo y por debajo de todo lo demás.
    //
    // El ovni sale uno como mucho, cada 60–120 s contados de salida a salida, con un generador
    // aleatorio propio: nunca el del algoritmo del Enemigo (12.7). Su ruta es la de su mapa
    // (BackgroundDefinition.UfoAt). El lado del primero se sortea y desde ahí se alterna, para que no
    // salga varias veces seguidas por el mismo; la altura se sortea en cada salida, salvo siguiendo
    // la corriente del Cinturón. La grava sigue el tiempo de BackgroundView, así que está donde la
    // pone fotograma(t). Todo avanza con el tiempo escalado: la pausa lo congela.
    //
    // Con una ventana más pequeña que el tablero (la carta estelar de 13.9), un sprite solo se
    // enseña si cabe entero en ella.
    [DefaultExecutionOrder(101)] // Presentation, 15.10, después de BackgroundView
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BackgroundView))]
    public sealed class BackgroundFlyby : MonoBehaviour
    {
        public const int RockSortingOrder = BackgroundView.SortingOrder + 1;
        public const int UfoSortingOrder = BackgroundView.SortingOrder + 2;

        [Tooltip("Semilla del generador del ovni; 0, una distinta en cada partida.")]
        [SerializeField] int seed;

        BackgroundView view;
        BackgroundDefinition shown;
        SpriteRenderer ufo;
        SpriteRenderer[] rocks = new SpriteRenderer[0];
        System.Random random;
        double untilNext;
        double ufoAge = -1.0;
        int launchDirection = 1;
        double launchStartY;

        // Hay un ovni cruzando, y cuántos han salido desde que se enseña este fondo.
        public bool UfoActive => ufoAge >= 0.0;
        public int UfoLaunches { get; private set; }

        // La salida sorteada del ovni en curso, o del último: lado (1, desde la izquierda; -1, desde
        // la derecha) y altura de entrada en píxeles.
        public int LaunchDirection => launchDirection;
        public double LaunchStartY => launchStartY;

        internal SpriteRenderer Ufo => ufo;
        internal SpriteRenderer[] Rocks => rocks;

        void Update()
        {
            Step(Time.deltaTime);
        }

        // Para los tests: el cableado normal es por el Inspector.
        public void Configure(int randomSeed)
        {
            seed = randomSeed;
            shown = null;
        }

        // Tiempo escalado: con la partida en pausa llega 0.
        public void Step(float deltaTime)
        {
            Sync();
            if (shown == null)
                return;

            if (UfoActive)
            {
                ufoAge += deltaTime;
                if (!shown.UfoAt(ufoAge, launchDirection, launchStartY, out _, out _))
                    ufoAge = -1.0;
            }
            untilNext -= deltaTime;
            if (untilNext <= 0.0 && !UfoActive)
            {
                // De salida a salida: el siguiente cuenta desde esta. Lo que el fotograma se pasó del
                // momento justo, el ovni ya lo lleva recorrido.
                ufoAge = -untilNext;
                untilNext += NextInterval();
                launchDirection = UfoLaunches == 0 ? (random.Next(2) == 0 ? 1 : -1) : -launchDirection;
                // Sin margen (un asset generado con un JSON anterior a T.7 #51) se queda en la altura de
                // referencia: con 0 y 0 saldría pegado al borde de arriba.
                double pick = random.NextDouble();
                launchStartY = shown.UfoStartYMax > shown.UfoStartYMin
                    ? shown.UfoStartYMin + pick * (shown.UfoStartYMax - shown.UfoStartYMin)
                    : shown.UfoStartY;
                UfoLaunches++;
            }
            Place();
        }

        // Pone cada sprite donde lo dicen el tiempo del fondo y la edad del ovni.
        void Place()
        {
            double time = view.Elapsed;
            for (int i = 0; i < rocks.Length; i++)
            {
                Vector2Int c = shown.RockAt(i, time);
                // Celda impar: el centro del sprite es el centro de su píxel central.
                Show(rocks[i], new Vector2(c.x + 0.5f, c.y + 0.5f), shown.Rocks[i].Half * 2 + 1);
            }

            if (ufo == null)
                return;
            if (!UfoActive || !shown.UfoAt(ufoAge, launchDirection, launchStartY, out Vector2Int pixel, out int frame))
            {
                ufo.enabled = false;
                return;
            }
            if (frame < shown.UfoFrames.Count)
                ufo.sprite = shown.UfoFrames[frame];
            // Celda par: su centro cae en el borde entre píxeles (pinta_ovni en gen_fondos_v2.py).
            Show(ufo, pixel, shown.UfoCell);
        }

        void Show(SpriteRenderer sprite, Vector2 center, int size)
        {
            sprite.transform.localPosition = view.PixelToLocal(center);
            RectInt window = view.Window;
            bool fullBoard = window.width >= BackgroundDefinition.Width && window.height >= BackgroundDefinition.Height;
            float half = size / 2f;
            sprite.enabled = fullBoard
                || (center.x - half >= window.xMin && center.x + half <= window.xMax
                    && center.y - half >= window.yMin && center.y + half <= window.yMax);
        }

        double NextInterval() =>
            shown.UfoIntervalMin + random.NextDouble() * (shown.UfoIntervalMax - shown.UfoIntervalMin);

        // Rehace los sprites cuando la vista enseña otro fondo, y empieza a contar el primer ovni.
        void Sync()
        {
            if (view == null)
                view = GetComponent<BackgroundView>();
            if (view.Definition == shown && random != null)
                return;

            shown = view.Definition;
            random = seed != 0 ? new System.Random(seed) : new System.Random();
            ufoAge = -1.0;
            UfoLaunches = 0;
            foreach (SpriteRenderer rock in rocks)
            {
                if (rock != null)
                    Discard(rock.gameObject);
            }
            rocks = new SpriteRenderer[0];
            if (shown == null)
            {
                if (ufo != null)
                    ufo.enabled = false;
                return;
            }

            if (ufo == null)
                ufo = Create("Ufo", UfoSortingOrder);
            ufo.enabled = false;
            untilNext = NextInterval();

            rocks = new SpriteRenderer[shown.Rocks.Count];
            for (int i = 0; i < rocks.Length; i++)
            {
                rocks[i] = Create($"Rock {i + 1}", RockSortingOrder);
                if (i < shown.RockFrames.Count)
                    rocks[i].sprite = shown.RockFrames[i];
            }
        }

        SpriteRenderer Create(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sortingOrder = order;
            return sprite;
        }

        static void Discard(GameObject go)
        {
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                DestroyImmediate(go);
        }
    }
}
