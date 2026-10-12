using System.Collections.Generic;
using Nova.Gameplay;
using Nova.Input;
using Nova.Systems;
using Nova.Utils;
using UnityEngine;

namespace Nova.Presentation
{
    // T3-07: atenuación provisional del modo apuntado (13.5, 7.2.1). Mientras se apunta, cada nodo
    // que no es objetivo válido —los propios y todos los Asteroides— queda tapado por un disco del
    // fondo a media opacidad, y los válidos se ven enteros. No hay círculo de alcance, porque no hay
    // alcance. La atenuación es código y no arte (desviación 81); la retícula ART-WLD-AIM llega con
    // T6-06. Solo lee PowerAimState y NodeRegistry.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class ProvisionalAimView : MonoBehaviour
    {
        // El fondo profundo (14.4) a 60 %: tapa el color de facción sin borrar el nodo.
        static readonly Color32 VeilColor = new Color32(0x0B, 0x0E, 0x1A, 0x99);

        [SerializeField] PowerAimState aim;

        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance.")]
        [SerializeField] NodeRegistry nodes;

        [Tooltip("Diámetro del velo en píxeles: el del círculo provisional de un nodo encendido.")]
        [SerializeField, Range(38, 77)] int diameterPixels = 64;

        readonly List<SpriteRenderer> veils = new List<SpriteRenderer>();

        internal IReadOnlyList<SpriteRenderer> Veils => veils;

        internal int VisibleCount
        {
            get
            {
                int count = 0;
                foreach (SpriteRenderer veil in veils)
                {
                    if (veil.enabled)
                        count++;
                }
                return count;
            }
        }

        void LateUpdate()
        {
            Refresh();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(PowerAimState powerAim, NodeRegistry registry = null)
        {
            aim = powerAim;
            nodes = registry;
        }

        public void Refresh()
        {
            NodeRegistry registry = nodes != null ? nodes : NodeRegistry.Instance;
            int used = 0;
            if (aim != null && aim.IsAiming && registry != null)
            {
                IReadOnlyList<NodeController> all = registry.All;
                for (int i = 0; i < all.Count; i++)
                {
                    NodeController node = all[i];
                    if (node == null || !node.IsInitialized || aim.IsValidTarget(node))
                        continue;
                    SpriteRenderer veil = Veil(used++);
                    Vector3 position = node.transform.position;
                    veil.transform.position = new Vector3(position.x, position.y, veil.transform.position.z);
                    veil.enabled = true;
                }
            }
            for (int i = used; i < veils.Count; i++)
                veils[i].enabled = false;
        }

        SpriteRenderer Veil(int index)
        {
            while (veils.Count <= index)
            {
                var go = new GameObject("ProvisionalAimVeil");
                go.transform.SetParent(transform, false);
                var veil = go.AddComponent<SpriteRenderer>();
                veil.sprite = PrimitiveSprites.Disc(diameterPixels);
                veil.color = VeilColor;
                veil.sortingOrder = 2; // por encima del círculo (0) y de la Energía (1) del nodo
                veils.Add(veil);
            }
            return veils[index];
        }
    }
}
