using System.Collections.Generic;
using Nova.Gameplay;
using Nova.Input;
using UnityEngine;

namespace Nova.Presentation
{
    // T1-12: anillo provisional alrededor de cada nodo seleccionado. Primitiva de la regla 4 de
    // T.2: desechable, la sustituye ART-WLD-SEL en T6-06. Solo lee OnSelectionChanged.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class ProvisionalSelectionView : MonoBehaviour
    {
        const int PixelsPerUnit = 64; // T0-04

        // Color del Jugador (14.4): hoy solo selecciona el Jugador.
        static readonly Color32 RingColor = new Color32(0x3F, 0xD7, 0xF5, 0xFF);

        static readonly Dictionary<int, Sprite> RingCache = new Dictionary<int, Sprite>();

        [SerializeField] SelectionManager selection;

        [Tooltip("Diámetro exterior del anillo en píxeles: por fuera del círculo de 64 del nodo.")]
        [SerializeField, Range(66, 96)] int diameterPixels = 76;

        [Tooltip("Grosor del anillo en píxeles.")]
        [SerializeField, Range(1, 4)] int thicknessPixels = 2;

        readonly Dictionary<NodeController, GameObject> rings = new Dictionary<NodeController, GameObject>();
        bool subscribed;

        internal int RingCount => rings.Count;
        internal bool HasRing(NodeController node) => node != null && rings.ContainsKey(node);

        void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void OnDestroy()
        {
            foreach (GameObject ring in rings.Values)
            {
                if (ring != null)
                    Destroy(ring);
            }
            rings.Clear();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(SelectionManager selectionManager)
        {
            Unsubscribe();
            selection = selectionManager;
        }

        internal void Subscribe()
        {
            if (subscribed || selection == null)
                return;
            selection.OnSelectionChanged += Refresh;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (!subscribed)
                return;
            if (selection != null)
                selection.OnSelectionChanged -= Refresh;
            subscribed = false;
        }

        public void Refresh()
        {
            if (selection == null)
                return;

            var keep = new HashSet<NodeController>(selection.Selected);

            var gone = new List<NodeController>();
            foreach (NodeController node in rings.Keys)
            {
                if (node == null || !keep.Contains(node))
                    gone.Add(node);
            }
            foreach (NodeController node in gone)
            {
                if (rings[node] != null)
                    DestroyImmediateSafe(rings[node]);
                rings.Remove(node);
            }

            foreach (NodeController node in keep)
            {
                if (node != null && !rings.ContainsKey(node))
                    rings.Add(node, CreateRing(node));
            }
        }

        GameObject CreateRing(NodeController node)
        {
            var ring = new GameObject("ProvisionalSelectionRing");
            ring.transform.SetParent(node.transform, false);
            var renderer = ring.AddComponent<SpriteRenderer>();
            renderer.sprite = RingSprite(diameterPixels, thicknessPixels);
            renderer.color = RingColor;
            renderer.sortingOrder = -1; // detrás del nodo: el anillo asoma por fuera
            return ring;
        }

        static void DestroyImmediateSafe(GameObject go)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        // Anillo blanco de alfa binario y filtro Point, teñido por SpriteRenderer.color.
        internal static Sprite RingSprite(int diameter, int thickness)
        {
            int key = diameter * 16 + thickness;
            if (RingCache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            var texture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false)
            {
                name = $"ProvisionalRing{diameter}x{thickness}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            float outer = diameter / 2f;
            float inner = outer - thickness;
            var pixels = new Color32[diameter * diameter];
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x + 0.5f - outer;
                    float dy = y + 0.5f - outer;
                    float d2 = dx * dx + dy * dy;
                    bool inside = d2 <= outer * outer && d2 > inner * inner;
                    pixels[y * diameter + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = texture.name;
            RingCache[key] = sprite;
            return sprite;
        }
    }
}
