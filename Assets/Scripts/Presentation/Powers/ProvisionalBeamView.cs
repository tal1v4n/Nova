using System.Collections.Generic;
using Nova.Systems;
using Nova.Utils;
using UnityEngine;

namespace Nova.Presentation
{
    // T3-03: haz de poder provisional. Un trazo recto del Púlsar al objetivo, en el color del poder
    // y no de la facción (7.2.2, 14.4), mientras duran sus 0.25 s. Primitiva de la regla 4 de T.2:
    // la sustituyen los ART-WLD-BEAM-* en T6-11. Solo lee PowerBeamSystem.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class ProvisionalBeamView : MonoBehaviour
    {
        const int PixelsPerUnit = 64; // T0-04

        [Tooltip("Opcional: sin él se usa PowerBeamSystem.Instance.")]
        [SerializeField] PowerBeamSystem beams;

        [Tooltip("Grosor del trazo en píxeles.")]
        [SerializeField, Range(1, 6)] int thicknessPixels = 3;

        readonly List<SpriteRenderer> lines = new List<SpriteRenderer>();

        internal IReadOnlyList<SpriteRenderer> Lines => lines;

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

        void LateUpdate()
        {
            Refresh();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(PowerBeamSystem beamSystem)
        {
            beams = beamSystem;
        }

        public void Refresh()
        {
            PowerBeamSystem system = beams != null ? beams : PowerBeamSystem.Instance;
            IReadOnlyList<PowerBeam> active = system != null ? system.Active : null;
            int count = active != null ? active.Count : 0;

            for (int i = 0; i < count; i++)
                Place(LineAt(i), active[i]);
            for (int i = count; i < lines.Count; i++)
                lines[i].enabled = false;
        }

        // Un píxel blanco estirado y girado, como el trazo de disparo del Magnetar.
        void Place(SpriteRenderer line, PowerBeam beam)
        {
            Vector2 delta = beam.To - beam.From;
            if (delta.sqrMagnitude <= 0f)
            {
                line.enabled = false;
                return;
            }
            line.transform.position = (beam.From + beam.To) / 2f;
            line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.transform.localScale = new Vector3(delta.magnitude * PixelsPerUnit, thicknessPixels, 1f);
            line.color = beam.Power.BeamColor;
            line.enabled = true;
        }

        SpriteRenderer LineAt(int index)
        {
            while (lines.Count <= index)
            {
                var line = new GameObject("ProvisionalBeam").AddComponent<SpriteRenderer>();
                line.transform.SetParent(transform, false);
                line.sprite = PrimitiveSprites.Rect(1, 1);
                line.sortingOrder = 6; // por encima de protones y disparos
                line.enabled = false;
                lines.Add(line);
            }
            return lines[index];
        }
    }
}
