using System.Collections.Generic;
using Nova.Systems;
using TMPro;
using UnityEngine;

namespace Nova.Presentation
{
    // T1-12: vista provisional de los protones en vuelo. Un disco pequeño del color de la facción
    // emisora (6.1, 14.3) con su Carga en Jersey 10. Primitiva de la regla 4 de T.2: desechable,
    // la sustituye ProtonVisual en T6-04 con los tres sprites de A.4.2.
    //
    // Solo lee: recorre ProtonRegistry.InFlight en LateUpdate y cuelga un hijo visual de cada
    // protón la primera vez que lo ve. El pool reutiliza el GameObject y su hijo con él.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    public sealed class ProvisionalProtonView : MonoBehaviour
    {
        const int PixelsPerUnit = 64; // T0-04
        const string VisualName = "ProvisionalProton";

        [Tooltip("Opcional: sin él se usa ProtonRegistry.Instance.")]
        [SerializeField] ProtonRegistry registry;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Diámetro del disco en píxeles. Cabe en la celda de 48 del protón (A.4.2).")]
        [SerializeField, Range(8, 48)] int diameterPixels = 24;

        readonly Dictionary<Proton, Visual> visuals = new Dictionary<Proton, Visual>();

        sealed class Visual
        {
            public SpriteRenderer Body;
            public TextMeshPro Label;
        }

        void LateUpdate()
        {
            Refresh();
        }

        public void Refresh()
        {
            ProtonRegistry source = registry != null ? registry : ProtonRegistry.Instance;
            if (source == null)
                return;

            foreach (Proton proton in source.InFlight)
            {
                if (proton == null)
                    continue;
                if (!visuals.TryGetValue(proton, out Visual visual))
                {
                    visual = Build(proton);
                    visuals.Add(proton, visual);
                }
                visual.Body.color = ProvisionalNodeView.ColorOf(proton.Faction);
                visual.Label.text = proton.Charge.ToString();
            }
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(ProtonRegistry protonRegistry, TMP_FontAsset fontAsset)
        {
            registry = protonRegistry;
            font = fontAsset;
        }

        internal bool TryGetVisual(Proton proton, out SpriteRenderer body, out TextMeshPro label)
        {
            if (proton != null && visuals.TryGetValue(proton, out Visual visual))
            {
                body = visual.Body;
                label = visual.Label;
                return true;
            }
            body = null;
            label = null;
            return false;
        }

        Visual Build(Proton proton)
        {
            var root = new GameObject(VisualName);
            root.transform.SetParent(proton.transform, false);

            var body = root.AddComponent<SpriteRenderer>();
            body.sprite = ProvisionalNodeView.CircleSprite(diameterPixels);
            body.sortingOrder = 2;

            var labelObject = new GameObject("Charge");
            labelObject.transform.SetParent(root.transform, false);
            var label = labelObject.AddComponent<TextMeshPro>();
            if (font != null)
                label.font = font;
            label.fontSize = ProvisionalNodeView.FontSizeFor(1);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = new Color32(0x0B, 0x0E, 0x1A, 0xFF); // fondo profundo (14.4)
            label.rectTransform.sizeDelta = new Vector2(diameterPixels, diameterPixels) / PixelsPerUnit;
            label.sortingOrder = 3;

            return new Visual { Body = body, Label = label };
        }
    }
}
