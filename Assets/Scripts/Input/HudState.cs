using Nova.Data;
using UnityEngine;

namespace Nova.Input
{
    // T6-08: el botón de pausa del HUD (13.6, 13.10) para la entrada. Dónde está —de HudArt— y qué hace el
    // ratón con él: encima o pulsado. Lo escribe InputRouter en cada frame, como el cursor de la línea de
    // envío, y lo lee HudTopBar para elegir el estado del botón. Vive en Nova.Input por lo mismo que
    // InfoPanelState: el router resuelve el clic contra él.
    [DisallowMultipleComponent]
    public sealed class HudState : MonoBehaviour
    {
        [SerializeField] HudArt art;

        bool held;
        bool pressStartedInside;

        // Sin arte no hay botón: nada lo contiene.
        public bool HasButton => art != null && art.HasTopBar;

        public Rect PauseButtonWorld => HasButton ? HudArt.ScreenToWorld(art.PauseRect) : default;

        public bool Hovered { get; private set; }

        // Pulsado: el botón izquierdo bajó dentro del botón, sigue abajo y el cursor sigue dentro.
        public bool Pressed => held && pressStartedInside && Hovered;

        // 0 normal, 1 encima y 2 pulsado: las columnas de ui_topbar_pause_sheet.
        public int ButtonState => Pressed ? 2 : Hovered ? 1 : 0;

        public bool Contains(Vector2 world) => HasButton && PauseButtonWorld.Contains(world);

        // Del router, en Update: dónde está el cursor y si el botón izquierdo está abajo.
        public void SetPointer(Vector2 world, bool leftHeld)
        {
            Hovered = Contains(world);
            if (leftHeld && !held)
                pressStartedInside = Hovered;
            held = leftHeld;
            if (!held)
                pressStartedInside = false;
        }

        // Al pausar: el cursor se queda donde estaba, pero el botón vuelve a su estado normal.
        public void Release()
        {
            held = false;
            pressStartedInside = false;
            Hovered = false;
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(HudArt hudArt)
        {
            art = hudArt;
        }
    }
}
