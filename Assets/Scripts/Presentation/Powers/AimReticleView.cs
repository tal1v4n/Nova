using System;
using System.Collections.Generic;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Systems;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-06: la retícula de apuntado, «El cepo» (13.5, A.4.3). En modo apuntado se posa sobre el objetivo válido
    // que hay bajo el cursor (7.2.1), y sobre nada más. Solo lee PowerAimState, SendLineState —el cursor— y el
    // registro de nodos.
    //
    // - Bajo el cursor es dentro de la media huella del nodo, que es el collider con el que se hace clic (18 #23).
    // - En el color del poder que se va a lanzar y con la talla de la huella del objetivo (InputArt).
    // - Al posarse en un objetivo nuevo reproduce la entrada —tres frames a 12 fps— y se queda en el último.
    // - Por encima del nodo y de su número (BoardOrder.AimReticle). Tiempo escalado.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class AimReticleView : MonoBehaviour
    {
        public const float FramesPerSecond = 12f;
        const float PixelsPerUnit = 64f;

        [SerializeField] PowerAimState aim;
        [SerializeField] SendLineState cursor;
        [SerializeField] InputArt art;

        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance.")]
        [SerializeField] NodeRegistry nodes;

        SpriteRenderer reticle;
        NodeController target;
        double sinceLanded;

        public bool HasArt => art != null && art.IsComplete;

        internal NodeController Target => target;
        internal SpriteRenderer Reticle { get { EnsureBuilt(); return reticle; } }

        void LateUpdate()
        {
            Advance(Time.deltaTime);
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(InputArt inputArt, PowerAimState powerAim, SendLineState sendLine, NodeRegistry registry = null)
        {
            art = inputArt;
            aim = powerAim;
            cursor = sendLine;
            nodes = registry;
        }

        // Busca el objetivo bajo el cursor y coloca la retícula. Público para los tests, donde no hay LateUpdate.
        public void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");
            EnsureBuilt();

            NodeController under = HasArt && aim != null && aim.IsAiming && cursor != null ? ValidTargetAt(cursor.Cursor) : null;
            if (under != target)
            {
                target = under;
                sinceLanded = 0d;
            }
            else
            {
                sinceLanded += deltaTime;
            }

            if (target == null)
            {
                reticle.enabled = false;
                return;
            }
            Sprite[] frames = art.AimFrames(aim.Power, art.SizeFor(NodeVisual.FootprintPixelsOf(target)));
            int frame = Math.Min((int)Math.Floor(sinceLanded * FramesPerSecond + 1e-9), frames.Length - 1);
            reticle.sprite = frames[frame];
            Vector3 position = target.transform.position;
            reticle.transform.position = new Vector3(position.x, position.y, 0f);
            reticle.enabled = true;
        }

        // El objetivo válido cuyo collider contiene el punto; si hay dos, el más cercano a su centro.
        NodeController ValidTargetAt(Vector2 point)
        {
            NodeRegistry registry = nodes != null ? nodes : NodeRegistry.Instance;
            if (registry == null)
                return null;
            NodeController best = null;
            float bestDistance = float.MaxValue;
            IReadOnlyList<NodeController> all = registry.All;
            for (int i = 0; i < all.Count; i++)
            {
                NodeController node = all[i];
                if (node == null || !aim.IsValidTarget(node))
                    continue;
                float radius = NodeLayout.HalfFootprint(NodeVisual.FootprintPixelsOf(node)) / PixelsPerUnit;
                float distance = Vector2.Distance(point, node.transform.position);
                if (distance <= radius && distance < bestDistance)
                {
                    best = node;
                    bestDistance = distance;
                }
            }
            return best;
        }

        void EnsureBuilt()
        {
            if (reticle != null)
                return;
            reticle = new GameObject("AimReticle").AddComponent<SpriteRenderer>();
            reticle.transform.SetParent(transform, false);
            reticle.sortingOrder = BoardOrder.AimReticle;
            reticle.enabled = false;
        }
    }
}
