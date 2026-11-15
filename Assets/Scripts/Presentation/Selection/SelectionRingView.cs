using System;
using System.Collections.Generic;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-06: el anillo de selección con su arte, «La órbita» (A.4.3). Sustituye a ProvisionalSelectionView, que se
    // aparta en cuanto hay InputArt completo. Solo lee la selección.
    //
    // - Uno por nodo seleccionado, en el centro del nodo, con la talla de su huella (InputArt.SizeFor): cambia
    //   si el nodo sube de nivel o se reconvierte estando seleccionado.
    // - Bucle de 16 frames a 12 fps, desfasado por la posición del nodo con la misma regla que el idle
    //   (NodeAnimator.IdlePhase, A.2.5): seis anillos de un lazo no giran en bloque.
    // - Detrás del nodo (capa 0 de 14.7) y encima del radio del Magnetar. No sigue el cabeceo: va con el nodo,
    //   como el collider y el número.
    // - Tiempo escalado, como el tablero: en pausa se queda quieto.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class SelectionRingView : MonoBehaviour
    {
        public const float FramesPerSecond = 12f;
        const float PixelsPerUnit = 64f;

        [SerializeField] SelectionManager selection;
        [SerializeField] InputArt art;

        readonly List<SpriteRenderer> rings = new List<SpriteRenderer>();
        double clock;

        public bool HasArt => art != null && art.IsComplete;

        internal IReadOnlyList<SpriteRenderer> Rings => rings;

        internal int VisibleCount
        {
            get
            {
                int count = 0;
                foreach (SpriteRenderer ring in rings)
                {
                    if (ring.enabled)
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
        public void Configure(InputArt inputArt, SelectionManager selectionManager)
        {
            art = inputArt;
            selection = selectionManager;
        }

        // Avanza el reloj del bucle y coloca los anillos. Público para los tests, donde no hay LateUpdate.
        public void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");
            clock += deltaTime;

            int shown = 0;
            if (HasArt && selection != null)
            {
                int tick = (int)Math.Floor(clock * FramesPerSecond + 1e-9);
                foreach (NodeController node in selection.Selected)
                {
                    if (node == null || !node.IsInitialized)
                        continue;
                    Sprite[] frames = art.RingFrames(art.SizeFor(NodeVisual.FootprintPixelsOf(node)));
                    Vector3 position = node.transform.position;
                    long frame = (tick + (long)NodeAnimator.IdlePhase(position)) % frames.Length;
                    SpriteRenderer ring = RingAt(shown++);
                    ring.sprite = frames[frame];
                    ring.transform.position = new Vector3(position.x, position.y, 0f);
                    ring.enabled = true;
                }
            }
            for (int i = shown; i < rings.Count; i++)
                rings[i].enabled = false;
        }

        SpriteRenderer RingAt(int index)
        {
            while (rings.Count <= index)
            {
                var ring = new GameObject($"SelectionRing{rings.Count}").AddComponent<SpriteRenderer>();
                ring.transform.SetParent(transform, false);
                ring.sortingOrder = BoardOrder.SelectionRing;
                ring.enabled = false;
                rings.Add(ring);
            }
            return rings[index];
        }
    }
}
