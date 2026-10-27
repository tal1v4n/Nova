using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Systems;
using UnityEngine;

namespace Nova.Presentation
{
    // T6-05: el anillo de radio de cada Magnetar del tablero (A.4.1, ART-WLD-RAD-*), «El susurro».
    // Un anillo por facción y nivel, del archivo de su radio exacto: escalar uno solo daría factores no
    // enteros. Solo lee el registro de nodos.
    //
    // A.8 #1: siempre visible en los Magnetares de las tres facciones. Los propios iban a demanda —al
    // seleccionarlos o pasarles el ratón— y el usuario los quiso siempre a la vista al verlo en Play
    // (2026-10-27). Un Magnetar robado o capturado cambia de anillo con su facción. Congelado o
    // reconvirtiéndose el radio se sigue viendo: dice dónde volverá a disparar, y 14.1 no lo apaga.
    //
    // Es una sola vista para el tablero, que recorre el registro. Cada anillo cuelga de su Magnetar,
    // centrado en él, y va detrás del nodo, del arco y de la línea de envío (14.7).
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class MagnetarRadiusView : MonoBehaviour
    {
        [SerializeField] MagnetarArt art;

        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance.")]
        [SerializeField] NodeRegistry registry;

        readonly Dictionary<NodeController, SpriteRenderer> rings = new Dictionary<NodeController, SpriteRenderer>();
        readonly List<NodeController> stale = new List<NodeController>();

        public bool HasArt => art != null && art.IsComplete;

        internal SpriteRenderer RingOf(NodeController node) =>
            node != null && rings.TryGetValue(node, out SpriteRenderer ring) ? ring : null;

        internal bool IsShowing(NodeController node)
        {
            SpriteRenderer ring = RingOf(node);
            return ring != null && ring.enabled;
        }

        void LateUpdate()
        {
            Sync();
        }

        void OnDestroy()
        {
            foreach (SpriteRenderer ring in rings.Values)
            {
                if (ring != null)
                    Discard(ring);
            }
            rings.Clear();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(MagnetarArt magnetarArt, NodeRegistry nodeRegistry = null)
        {
            art = magnetarArt;
            registry = nodeRegistry;
        }

        // Pone cada anillo al día con su Magnetar. Barato: el tablero no pasa de unas decenas de nodos.
        public void Sync()
        {
            NodeRegistry source = registry != null ? registry : NodeRegistry.Instance;

            stale.Clear();
            foreach (NodeController node in rings.Keys)
            {
                if (node == null || source == null || !source.Contains(node))
                    stale.Add(node);
            }
            foreach (NodeController node in stale)
            {
                if (rings[node] != null)
                    Discard(rings[node]);
                rings.Remove(node);
            }

            if (source == null || !HasArt)
                return;

            IReadOnlyList<NodeController> all = source.All;
            for (int i = 0; i < all.Count; i++)
            {
                NodeController node = all[i];
                Sprite sprite = SpriteFor(node);
                if (sprite == null)
                {
                    if (rings.TryGetValue(node, out SpriteRenderer unused) && unused != null)
                        unused.enabled = false;
                    continue;
                }
                SpriteRenderer ring = Ring(node);
                ring.sprite = sprite;
                ring.enabled = true;
            }
        }

        Sprite SpriteFor(NodeController node)
        {
            if (node == null || !node.IsInitialized || node.Type != NodeType.Magnetar)
                return null;
            return art.RingFor(node.Faction, node.Level);
        }

        // En los tests de EditMode no hay Destroy diferido.
        static void Discard(SpriteRenderer ring)
        {
            if (Application.isPlaying)
                Destroy(ring.gameObject);
            else
                DestroyImmediate(ring.gameObject);
        }

        SpriteRenderer Ring(NodeController node)
        {
            if (rings.TryGetValue(node, out SpriteRenderer ring) && ring != null)
                return ring;
            ring = new GameObject("Radius").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(node.transform, false);
            ring.sortingOrder = BoardOrder.BehindNode;
            rings[node] = ring;
            return ring;
        }
    }
}
