using System.Collections.Generic;
using Nova.Gameplay;
using Nova.Input;
using Nova.Utils;
using UnityEngine;

namespace Nova.Presentation
{
    // T2-09: línea de envío provisional (13.1). Un trazo recto de color liso del centro de cada
    // origen al cursor, como el trazo de disparo de ProvisionalMagnetarView. Primitiva de la regla 4
    // de T.2: el filamento de plasma de 14.9 (ART-WLD-LINK) lo pone T6-06.
    //
    // Sigue al cursor en cada frame, en Update (15.10). Solo lee SendLineState.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class ProvisionalSendLineView : MonoBehaviour
    {
        const int PixelsPerUnit = 64; // T0-04

        // Color del Jugador (14.4): el Enemigo nunca tiene línea (14.9).
        static readonly Color32 LineColor = new Color32(0x3F, 0xD7, 0xF5, 0xFF);

        [SerializeField] SendLineState state;

        [Tooltip("Grosor del trazo en píxeles.")]
        [SerializeField, Range(1, 4)] int thicknessPixels = 2;

        readonly List<SpriteRenderer> lines = new List<SpriteRenderer>();

        // Trazos visibles ahora mismo.
        internal int VisibleCount
        {
            get
            {
                int count = 0;
                foreach (SpriteRenderer line in lines)
                {
                    if (line.enabled)
                        count++;
                }
                return count;
            }
        }

        internal IReadOnlyList<SpriteRenderer> Lines => lines;

        void LateUpdate()
        {
            Refresh();
        }

        void OnDisable()
        {
            foreach (SpriteRenderer line in lines)
            {
                if (line != null)
                    line.enabled = false;
            }
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(SendLineState sendLine)
        {
            state = sendLine;
        }

        // Un trazo por origen, del centro al cursor; los que sobran se apagan.
        public void Refresh()
        {
            IReadOnlyList<NodeController> origins = state != null ? state.Origins : null;
            int count = origins != null ? origins.Count : 0;
            Vector2 tip = state != null ? state.Cursor : Vector2.zero;

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer line = LineAt(i);
                Place(line, origins[i].transform.position, tip);
            }
            for (int i = count; i < lines.Count; i++)
                lines[i].enabled = false;
        }

        // Un píxel blanco estirado y girado: color liso, así que estirarlo no rompe ninguna rejilla.
        void Place(SpriteRenderer line, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude <= 0f)
            {
                line.enabled = false;
                return;
            }
            line.transform.position = (from + to) / 2f;
            line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(delta.magnitude * PixelsPerUnit, thicknessPixels, 1f);
            line.enabled = true;
        }

        SpriteRenderer LineAt(int index)
        {
            while (lines.Count <= index)
            {
                var line = new GameObject("ProvisionalSendLine").AddComponent<SpriteRenderer>();
                line.transform.SetParent(transform, false);
                line.sprite = PrimitiveSprites.Rect(1, 1);
                line.color = LineColor;
                line.sortingOrder = -1; // detrás del nodo: el trazo asoma desde su borde
                line.enabled = false;
                lines.Add(line);
            }
            return lines[index];
        }
    }
}
