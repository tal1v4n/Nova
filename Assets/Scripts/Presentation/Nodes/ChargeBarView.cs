using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Utils;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-09: la barra de carga bajo el Púlsar con su arte, `ART-UI-CHARGE` «La mecha» (A.6, 7.1, 7.5). Arde una
    // mecha con el dibujo del haz del poder hacia una llama; delante, lo que falta en la pista del bando, y
    // detrás, el casquillo del bando. Pausada (sin Energía o Congelado), la mecha se para y la llama queda en
    // brasa; lista, la mecha entera, la llama se hace estrella y una luz la recorre. Se ve en los Púlsares de los
    // dos bandos: la barra llena del Enemigo es el contrajuego entero.
    //
    // Va bajo el número de Energía, centrada (NodeLayout.ChargeBarTopPixels), en BoardOrder.ChargeBar. Se compone
    // como barra() de tools/gen_charge_v3.py. Sin arte o sin sprite de nodo no dibuja nada, y queda la barra
    // provisional de ProvisionalPulsarView.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController), typeof(PulsarCaster))]
    public sealed class ChargeBarView : MonoBehaviour
    {
        // El número va a ×2 de la mayúscula nativa: el hueco de 20 px (NodeLayout).
        const int LabelTextScale = 2;

        [SerializeField] NodeLabelArt art;

        NodeController node;
        PulsarCaster caster;
        SpriteRenderer view;
        CanvasSprite canvas;
        int drawnKey = int.MinValue;

        internal SpriteRenderer View { get { EnsureBuilt(); return view; } }
        internal PixelCanvas Canvas { get { EnsureBuilt(); return canvas.Canvas; } }

        public bool HasArt => art != null && art.HasChargeBar;

        // Dibuja la barra: hay arte, el sprite del nodo se ve, es un Púlsar y tiene una carga o un poder listo.
        public bool Draws => HasArt && Node.IsInitialized && Node.Type == NodeType.Pulsar && Caster.HasPowerCharge
                             && Caster.Power != null && NodeVisual.FootprintPixelsOf(Node) > 0;

        public void Configure(NodeLabelArt labelArt)
        {
            art = labelArt;
            drawnKey = int.MinValue;
        }

        void LateUpdate()
        {
            Refresh(Time.time);
        }

        void OnDestroy()
        {
            canvas?.Destroy();
        }

        public void Refresh(float time)
        {
            EnsureBuilt();
            bool draws = Draws;
            view.enabled = draws;
            if (!draws)
                return;

            int frame = Mathf.FloorToInt(time * art.ChargeFramesPerSecond) % art.ChargeFrames;
            bool ready = Caster.State == PowerChargeState.Ready;
            bool paused = Caster.IsPaused;
            int footprint = NodeVisual.FootprintPixelsOf(Node);
            int key = (((frame * 211 + Caster.Invested) * 31 + (int)Caster.Power.Type) * 4 + (ready ? 2 : 0) + (paused ? 1 : 0)) * 7
                      + (int)Node.Faction + footprint * 100003;
            if (key == drawnKey)
                return;
            drawnKey = key;

            Compose(canvas.Canvas, art, Node.Faction, Caster.Power.Type, Caster.Invested, Caster.Power.Cost, ready, paused, frame);
            canvas.Upload();

            int top = NodeLayout.ChargeBarTopPixels(footprint, LabelTextScale);
            view.transform.localPosition = new Vector3(-art.ChargeWidth / 2 / (float)PrimitiveSprites.PixelsPerUnit,
                -top / (float)PrimitiveSprites.PixelsPerUnit, 0f);
        }

        // barra() de gen_charge_v3: la pista por todo el largo, la mecha desde fuseX0 tapándola, la llama en su
        // frente y, lista, la luz por la fila de la mecha.
        internal static void Compose(PixelCanvas c, NodeLabelArt art, Faction faction, PowerType power, int invested,
            int cost, bool ready, bool paused, int frame)
        {
            c.Clear();
            int x0 = art.FuseX0;
            int length = art.FuseLength;
            int height = art.ChargeHeight;
            c.Blit(SpritePixels.Of(art.Socket(faction)), 0, 0);
            c.Tile(SpritePixels.Of(art.Track(faction)), new RectInt(x0, 0, length, height), x0, 0);

            int n = ready ? length : cost > 0 ? length * Mathf.Max(0, invested) / cost : 0;
            if (n > 0)
            {
                for (int y = 0; y < height; y++)
                    for (int x = x0; x < x0 + n; x++)
                        c.Erase(x, y);
                int fuseFrame = paused && !ready ? art.ChargeFrames : frame;
                c.Tile(SpritePixels.Of(art.Fuse(power, fuseFrame)), new RectInt(x0, 0, n, height), x0, 0);
                if (ready)
                {
                    float head = frame / (float)art.ChargeFrames * art.ReadyLightTravel + art.ReadyLightFrom;
                    int row = art.ReadyLightRow;
                    for (int x = 0; x < n; x++)
                        if (Mathf.Abs(x - head) < art.ReadyLightHalfWidth && c.IsSet(x0 + x, row))
                            c.Set(x0 + x, row, art.ReadyLight(power));
                }
            }

            if (invested > 0 || ready)
            {
                Sprite flame = ready ? art.Flame(power, true, frame)
                    : art.Flame(power, false, paused ? art.ChargeFrames : frame);
                int fx = ready ? art.ChargeWidth - art.FlameWidth : x0 + n - 1 - art.FlameFront;
                c.Blit(SpritePixels.Of(flame), fx, 0);
            }
        }

        void EnsureBuilt()
        {
            if (view != null && canvas != null)
                return;
            int width = art != null ? art.ChargeWidth : 48;
            int height = art != null ? art.ChargeHeight : 6;
            canvas = new CanvasSprite(width, height, new Vector2(0f, 1f), "ChargeBar");
            view = new GameObject("ChargeBar").AddComponent<SpriteRenderer>();
            view.transform.SetParent(transform, false);
            view.sprite = canvas.Sprite;
            view.sortingOrder = BoardOrder.ChargeBar;
            view.enabled = false;
        }

        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
        PulsarCaster Caster => caster != null ? caster : caster = GetComponent<PulsarCaster>();
    }
}
