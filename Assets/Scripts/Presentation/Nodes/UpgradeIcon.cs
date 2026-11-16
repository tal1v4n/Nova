using System;
using System.Collections.Generic;
using Nova.Gameplay;
using Nova.Input;
using Nova.Systems;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-15: el icono de mejora sobre el nodo, «El sello» (ART-UI-BTN-UPG, 13.13, A.6). Solo dibuja: qué icono
    // enseña cada nodo y dónde lo dice UpgradeIconState, y el clic lo resuelve InputRouter.
    //
    // - Uno por nodo propio mejorable con el icono a la vista: aviso, listo, activo, encima o pulsado, cada uno
    //   una fila de ui_upgrade_icon_sheet. El difuminado de aviso y listo ya viene tramado en la hoja: el alfa
    //   del proyecto es binario y aquí no se toca la opacidad.
    // - Centrado encima de la huella, a 2 px, con el pivote abajo al centro; sube con el nodo al mejorar.
    // - Capa 6 de 14.7, con la Energía. No sigue el cabeceo: va con el nodo, como el número y el collider.
    // - Bucle de 16 frames a 12 fps —el sonar del activo—, en tiempo escalado: en pausa se queda quieto.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class UpgradeIcon : MonoBehaviour
    {
        [SerializeField] UpgradeIconState state;
        [SerializeField] NodeRegistry registry;

        readonly List<SpriteRenderer> icons = new List<SpriteRenderer>();
        double clock;

        public bool HasArt => state != null && state.HasIcon;

        internal IReadOnlyList<SpriteRenderer> Icons => icons;

        internal int VisibleCount
        {
            get
            {
                int count = 0;
                foreach (SpriteRenderer icon in icons)
                {
                    if (icon.enabled)
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
        public void Configure(UpgradeIconState iconState, NodeRegistry nodes)
        {
            state = iconState;
            registry = nodes;
        }

        // Avanza el reloj del bucle y coloca los iconos. Público para los tests, donde no hay LateUpdate.
        public void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");
            clock += deltaTime;

            int shown = 0;
            if (HasArt && registry != null)
            {
                var art = state.Art;
                long frame = (long)Math.Floor(clock * art.UpgradeIconFramesPerSecond + 1e-9) % art.UpgradeIconFrames;
                foreach (NodeController node in registry.All)
                {
                    UpgradeIconLook look = state.LookOf(node);
                    if (look == UpgradeIconLook.Hidden)
                        continue;
                    SpriteRenderer icon = IconAt(shown++);
                    icon.sprite = art.UpgradeIcon((int)look, (int)frame);
                    Vector2 anchor = state.Anchor(node);
                    icon.transform.position = new Vector3(anchor.x, anchor.y, 0f);
                    icon.enabled = true;
                }
            }
            for (int i = shown; i < icons.Count; i++)
                icons[i].enabled = false;
        }

        SpriteRenderer IconAt(int index)
        {
            while (icons.Count <= index)
            {
                var icon = new GameObject($"UpgradeIcon{icons.Count}").AddComponent<SpriteRenderer>();
                icon.transform.SetParent(transform, false);
                icon.sortingOrder = BoardOrder.UpgradeIcon;
                icon.enabled = false;
                icons.Add(icon);
            }
            return icons[index];
        }
    }
}
