using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Utils;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-09: el número de Energía del nodo con su arte, `ART-UI-HUD-ENERGY` «El filamento» (A.6): las cifras de
    // la Carga a ×2 trazadas con el plasma del bando, con un pulso blanco que corre por el trazo; lo neutral, quieto
    // en gris. No es TextMeshPro: se compone por sprites con el avance de su .json (NOVA_Arte_Tecnica.md).
    //
    // Va centrado bajo la huella, a NodeLayout.Gap, centrado en el hueco de 20 px que reserva NodeLayout, en la
    // capa del número (BoardOrder.Energy). La cifra k del número, de izquierda a derecha, usa el frame
    // (f + k · desfase) % frames, a 12 fps: la corriente salta de una cifra a otra. Sin arte o sin sprite de nodo
    // no dibuja nada, y el número sigue siendo el TextMeshPro de ProvisionalNodeView.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class EnergyLabelView : MonoBehaviour
    {
        // «100», el número más ancho (tope 100): tres cifras como mucho.
        const int MaxDigits = 3;

        [SerializeField] NodeLabelArt art;

        NodeController node;
        SpriteRenderer view;
        CanvasSprite canvas;
        int drawnEnergy = -1;
        Faction drawnFaction;
        int drawnFrame = -1;
        int drawnFootprint = -1;

        internal SpriteRenderer View { get { EnsureBuilt(); return view; } }
        internal PixelCanvas Canvas { get { EnsureBuilt(); return canvas.Canvas; } }

        // Dibuja el número: hay arte, el nodo está inicializado y su sprite se ve.
        public bool Draws => art != null && art.HasEnergyLabel && Node.IsInitialized && NodeVisual.FootprintPixelsOf(Node) > 0;

        // Para la plantilla de nodo y los tests: el cableado normal es por el Inspector.
        public void Configure(NodeLabelArt labelArt)
        {
            art = labelArt;
            drawnEnergy = -1;
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

            int frame = Mathf.FloorToInt(time * art.DigitFramesPerSecond) % art.DigitFrames;
            int footprint = NodeVisual.FootprintPixelsOf(Node);
            // Lo neutral va quieto: no hay que recomponerlo con el reloj.
            if (Node.Faction == Faction.Neutral)
                frame = 0;
            if (Node.Energy == drawnEnergy && Node.Faction == drawnFaction && frame == drawnFrame && footprint == drawnFootprint)
                return;
            drawnEnergy = Node.Energy;
            drawnFaction = Node.Faction;
            drawnFrame = frame;
            drawnFootprint = footprint;

            Compose(canvas.Canvas, art, Node.Energy, Node.Faction, frame);
            canvas.Upload();

            // La esquina de arriba a la izquierda del lienzo: el número centrado en él y el lienzo centrado en x,
            // con su borde de arriba a media huella + hueco + lo que sobra del alto del hueco.
            int top = NodeLayout.HalfFootprint(footprint) + NodeLayout.Gap + (art.LabelBoxHeight - art.DigitCell.y) / 2;
            view.transform.localPosition = new Vector3(-canvas.Canvas.Width / 2 / (float)PrimitiveSprites.PixelsPerUnit,
                -top / (float)PrimitiveSprites.PixelsPerUnit, 0f);
        }

        // El número en el lienzo, centrado en x (la mitad entera hacia la izquierda, como colocar() del generador).
        internal static void Compose(PixelCanvas canvas, NodeLabelArt art, int energy, Faction faction, int frame)
        {
            canvas.Clear();
            string digits = Mathf.Max(0, energy).ToString();
            int width = 0;
            foreach (char c in digits)
                width += art.Advance(c - '0');
            int x = canvas.Width / 2 - width / 2;
            for (int k = 0; k < digits.Length; k++)
            {
                int digit = digits[k] - '0';
                int f = (frame + k * art.DigitPhaseOffset) % art.DigitFrames;
                canvas.Blit(SpritePixels.Of(art.Digit(faction, digit, f)), x, 0);
                x += art.Advance(digit);
            }
        }

        void EnsureBuilt()
        {
            if (view != null && canvas != null)
                return;
            int width = MaxDigits * (art != null && art.HasEnergyLabel ? art.DigitCell.x : 12);
            int height = art != null && art.HasEnergyLabel ? art.DigitCell.y : 16;
            canvas = new CanvasSprite(width, height, new Vector2(0f, 1f), "EnergyLabel");
            view = new GameObject("EnergyLabel").AddComponent<SpriteRenderer>();
            view.transform.SetParent(transform, false);
            view.sprite = canvas.Sprite;
            view.sortingOrder = BoardOrder.Energy;
            view.enabled = false;
        }

        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
    }
}
