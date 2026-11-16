using System;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Systems;
using UnityEngine;

namespace Nova.UI
{
    // T6-08: la barra de poder del HUD (13.6) con su arte, «El conducto» (A.6). Enseña el REPARTO de la
    // Energía entre los dos bandos —cian del Jugador desde la izquierda, magenta del Enemigo desde la
    // derecha, sin parte vacía ni neutral—, y es solo informativa. Lee NodeRegistry en cada frame y no
    // acumula estado de juego: lo único que guarda es dónde va el frente mientras viaja.
    //
    // Cuenta la Energía de los nodos encendidos de cada bando: ni la Materia Inerte de los Asteroides
    // —tampoco la de los propios, que no cuentan (NOVA_Nodos.md)— ni la Carga en vuelo (13.6).
    //
    // Montaje (A.6): la carcasa entera; el relleno del Enemigo en mosaico por TODO el interior y el del
    // Jugador encima, de x0 al reparto. Así los dos se leen por coordenada absoluta desde x0 y la trama no
    // salta cuando el frente se mueve. El frente va encima, con su contacto en el reparto, y llega a un
    // reparto nuevo con la curva orbital (13.12). UI de partida: tiempo escalado, en pausa se queda quieta (15.8).
    [DefaultExecutionOrder(50)] // UI, 15.10
    [DisallowMultipleComponent]
    public sealed class PowerBarUI : MonoBehaviour
    {
        // Por encima de todo el tablero y por debajo del menú de encendido (20) y del panel (30).
        internal const int FrameOrder = 15;
        internal const int EnemyOrder = 16;
        internal const int PlayerOrder = 17;
        internal const int FrontOrder = 18;

        static readonly Func<float, float> Orbital = UiMotion.Orbital;

        [SerializeField] HudArt art;
        [SerializeField] NodeRegistry registry;

        Transform root;
        SpriteRenderer frame;
        SpriteRenderer enemy;
        SpriteRenderer player;
        SpriteRenderer front;

        float clock;
        float shown = 0.5f;
        float tweenFrom = 0.5f;
        float tweenTo = 0.5f;
        float tweenTime;
        bool started;

        public bool HasArt => art != null && art.HasPowerBar;

        internal float Target => tweenTo;
        internal float Shown => shown;
        internal int FrontPixel => art.FrontPixel(shown);
        internal int Frame => HasArt ? Mathf.FloorToInt(clock * art.BarFramesPerSecond) : 0;
        internal SpriteRenderer FrameRenderer { get { EnsureBuilt(); return frame; } }
        internal SpriteRenderer EnemyRenderer { get { EnsureBuilt(); return enemy; } }
        internal SpriteRenderer PlayerRenderer { get { EnsureBuilt(); return player; } }
        internal SpriteRenderer FrontRenderer { get { EnsureBuilt(); return front; } }

        // La Energía que cuenta en la barra: la de los nodos encendidos de un bando.
        public static int EnergyOf(NodeRegistry nodes, Faction faction)
        {
            if (nodes == null || faction == Faction.Neutral)
                return 0;
            int total = 0;
            foreach (NodeController node in nodes.GetNodes(faction))
            {
                if (node != null && node.Type != NodeType.Asteroid)
                    total += node.Energy;
            }
            return total;
        }

        // La parte del Jugador sobre la suma de los dos. Sin Energía en ninguno, a medias.
        public static float Share(int playerEnergy, int enemyEnergy)
        {
            int total = Mathf.Max(0, playerEnergy) + Mathf.Max(0, enemyEnergy);
            return total > 0 ? Mathf.Max(0, playerEnergy) / (float)total : 0.5f;
        }

        void Update()
        {
            Advance(Time.deltaTime);
        }

        void OnDisable()
        {
            if (root != null)
                root.gameObject.SetActive(false);
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(HudArt hudArt, NodeRegistry nodes)
        {
            art = hudArt;
            registry = nodes;
        }

        // Un frame de la barra: el reparto de ahora, el viaje del frente y el bucle del plasma.
        internal void Advance(float deltaTime)
        {
            if (!HasArt)
            {
                if (root != null)
                    root.gameObject.SetActive(false);
                return;
            }

            NodeRegistry nodes = registry != null ? registry : NodeRegistry.Instance;
            float target = Share(EnergyOf(nodes, Faction.Player), EnergyOf(nodes, Faction.Enemy));
            if (!started)
            {
                // El primer frame no viaja: la barra entra ya en su sitio.
                shown = tweenFrom = tweenTo = target;
                tweenTime = art.FrontTweenSeconds;
                started = true;
            }
            else if (!Mathf.Approximately(target, tweenTo))
            {
                tweenFrom = shown;
                tweenTo = target;
                tweenTime = 0f;
            }

            clock += Mathf.Max(0f, deltaTime);
            tweenTime += Mathf.Max(0f, deltaTime);
            float duration = art.FrontTweenSeconds;
            float t = duration > 0f ? Mathf.Clamp01(tweenTime / duration) : 1f;
            shown = Mathf.LerpUnclamped(tweenFrom, tweenTo, Orbital(t));

            Layout();
        }

        void Layout()
        {
            EnsureBuilt();
            root.gameObject.SetActive(true);

            int x0 = art.BarX0;
            int x1 = art.BarX1;
            int topPixel = art.BarTop;
            int b = art.FrontPixel(shown);
            int fillFrame = Frame % art.FillFrames;
            int frontFrame = Frame % art.FrontFrames;

            frame.sprite = art.BarFrame;
            Place(frame, 0, topPixel);

            enemy.sprite = art.EnemyFill(fillFrame);
            Place(enemy, x0, topPixel);
            enemy.size = new Vector2(x1 - x0, art.BarHeight) / HudArt.PixelsPerUnit;

            player.sprite = art.PlayerFill(fillFrame);
            Place(player, x0, topPixel);
            player.enabled = b > x0;
            if (player.enabled)
                player.size = new Vector2(b - x0, art.BarHeight) / HudArt.PixelsPerUnit;

            front.sprite = art.Front(frontFrame);
            Place(front, b - art.FrontContact, topPixel);
        }

        // Los sprites del HUD llevan el pivote arriba a la izquierda (SpriteImportRules).
        static void Place(SpriteRenderer renderer, int x, int y)
        {
            Vector2 world = HudArt.ScreenToWorld(x, y);
            renderer.transform.localPosition = new Vector3(world.x, world.y, 0f);
        }

        void EnsureBuilt()
        {
            if (root != null)
                return;
            root = new GameObject("PowerBar").transform;
            root.SetParent(transform, false);
            frame = Piece("Frame", FrameOrder, false);
            enemy = Piece("EnemyFill", EnemyOrder, true);
            player = Piece("PlayerFill", PlayerOrder, true);
            front = Piece("Front", FrontOrder, false);
        }

        SpriteRenderer Piece(string name, int order, bool tiled)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(root, false);
            renderer.sortingOrder = order;
            if (tiled)
            {
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
            }
            return renderer;
        }
    }
}
