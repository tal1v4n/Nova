using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Input;
using Nova.Utils;
using TMPro;
using UnityEngine;

namespace Nova.UI
{
    // Menú radial de encendido (13.7, T2-02): tres discos alrededor del cursor con el coste
    // dentro y el tipo por fuera. Solo dibuja: el estado es de IgnitionMenuState, el clic lo
    // resuelve InputRouter y cada opción se valida con NodeIgnition.Check, que no tiene efectos.
    //
    // Discos y texto son primitivas de la regla 4 de T.2.
    //
    // T6-09: con PanelArt, la skin final, `ART-UI-IGN-MENU` «Los nodos» (A.6): cada opción es el sprite de nivel 1
    // del nodo que se va a encender, entre cuatro esquinas; sin Energía, tramado; no disponible, solo el contorno.
    // Las opciones salen del centro al abrirse (patrón «Ignición», 320 ms con 80 de escalonado) y las esquinas se
    // cierran con el cursor encima. El nombre va encima de la opción de arriba y debajo de las de abajo, con el
    // coste en su línea (13.7, desviación 381). Se compone con PanelSkin.
    [DefaultExecutionOrder(50)] // UI, 15.10
    [DisallowMultipleComponent]
    public sealed class IgnitionMenu : MonoBehaviour
    {
        // Qué enseña cada opción.
        internal enum OptionLook { Enabled, MissingEnergy, Unavailable }

        // Paleta funcional de 14.4: el Jugador para lo que se puede elegir y el rojo del Enemigo
        // para el coste que no se alcanza.
        static readonly Color32 EnabledColor = new Color32(0x3F, 0xD7, 0xF5, 0xFF);
        static readonly Color32 DisabledColor = new Color32(0x2A, 0x30, 0x42, 0xFF);
        static readonly Color32 CostColor = new Color32(0x0B, 0x0E, 0x1A, 0xFF);
        static readonly Color32 MissingCostColor = new Color32(0xF2, 0x45, 0x6B, 0xFF);
        static readonly Color32 NameColor = new Color32(0xE6, 0xEC, 0xF5, 0xFF);
        static readonly Color32 DisabledNameColor = new Color32(0x8A, 0x93, 0xA6, 0xFF);

        // Jersey 10 (NOVA_Arte_Tecnica.md): 18.67 px por em; TextMeshPro en mundo mide el em en
        // fontSize × 0.1 unidades.
        const float NativeEmPixels = 1400f / 75f;
        const float WorldTextScale = 0.1f;

        // Por encima de nodos y protones.
        const int SortingOrder = 20;

        // Separación entre el borde del disco y el centro de la etiqueta del tipo.
        const float NameGap = 0.25f;

        [SerializeField] IgnitionMenuState menu;

        [Tooltip("Jersey10 Raster56 (T0-06).")]
        [SerializeField] TMP_FontAsset font;

        [Tooltip("Textos de la UI (T.7 #39): de aquí salen los nombres de las opciones.")]
        [SerializeField] UiText text;

        [Tooltip("Opcional (T6-09): el arte del menú. Sin él, primitivas.")]
        [SerializeField] PanelArt art;

        GameObject root;
        SpriteRenderer[] discs;
        TextMeshPro[] costs;
        TextMeshPro[] names;
        readonly OptionLook[] looks = new OptionLook[3];

        NodeController watched;
        bool subscribed;

        // Con arte.
        SpriteRenderer[] optionViews;
        CanvasSprite[] optionCanvases;
        TextMeshPro[] optionLabels;
        readonly float[] hoverSince = { -1f, -1f, -1f };
        readonly int[] drawnKey = { int.MinValue, int.MinValue, int.MinValue };
        float openedAt;
        Vector2 openedCenter;
        NodeController openedNode;

        internal bool HasArt => art != null && art.HasIgnitionMenu;
        internal PixelCanvas OptionCanvas(int option) { EnsureBuilt(); return optionCanvases[option]?.Canvas; }
        internal Vector2 OptionArtPosition(int option) { EnsureBuilt(); return optionViews[option].transform.position; }
        internal TextMeshPro OptionLabel(int option) { EnsureBuilt(); return optionLabels[option]; }

        internal bool IsVisible => root != null && root.activeSelf;
        internal OptionLook LookOf(int option) => looks[option];
        internal TextMeshPro CostLabel(int option) { EnsureBuilt(); return costs[option]; }
        internal Vector2 DiscPosition(int option) { EnsureBuilt(); return discs[option].transform.position; }

        void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(IgnitionMenuState state, TMP_FontAsset fontAsset, UiText uiText, PanelArt panelArt = null)
        {
            Unsubscribe();
            menu = state;
            font = fontAsset;
            text = uiText;
            art = panelArt;
        }

        internal void Subscribe()
        {
            if (subscribed || menu == null)
                return;
            menu.OnChanged += Refresh;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (subscribed && menu != null)
                menu.OnChanged -= Refresh;
            subscribed = false;
            Watch(null);
        }

        public void Refresh()
        {
            EnsureBuilt();

            bool open = menu != null && menu.IsOpen;
            Watch(open ? menu.Node : null);
            root.SetActive(open);
            if (!open)
                return;

            root.transform.position = menu.Center;
            // Se abre de nuevo (otro Asteroide u otro sitio): la entrada vuelve a empezar.
            if (menu.Node != openedNode || menu.Center != openedCenter)
            {
                openedNode = menu.Node;
                openedCenter = menu.Center;
                openedAt = Time.unscaledTime;
            }
            NodeIgnition ignition = menu.Node.GetComponent<NodeIgnition>();
            for (int i = 0; i < IgnitionMenuState.Options.Count; i++)
            {
                ActionCheck check = ignition != null
                    ? ignition.Check(menu.Actor, IgnitionMenuState.Options[i])
                    : new ActionCheck(ActionBlock.Unavailable, 0);
                Paint(i, check);
            }
            bool artOn = HasArt;
            for (int i = 0; i < discs.Length; i++)
            {
                discs[i].enabled = !artOn;
                names[i].enabled = !artOn;
                if (artOn)
                    costs[i].enabled = false;
                optionViews[i].enabled = artOn;
                optionLabels[i].enabled = artOn;
                drawnKey[i] = int.MinValue;
            }
            if (artOn)
                Animate(Time.unscaledTime);
        }

        void LateUpdate()
        {
            if (HasArt && root != null && root.activeSelf && menu != null && menu.IsOpen)
                Animate(Time.unscaledTime);
        }

        // Con arte: la entrada, el encima y el pulsado de cada opción, y su etiqueta.
        void Animate(float now)
        {
            PanelArt.IgnitionPieces p = art.Ignition;
            for (int i = 0; i < optionViews.Length; i++)
            {
                float k = UiMotion.Orbital(Mathf.Clamp01((now - openedAt - i * p.staggerSeconds) / p.enterSeconds));
                Vector2 home = IgnitionMenuState.OptionPosition(Vector2.zero, i);
                optionViews[i].transform.localPosition = home * k;

                OptionLook look = looks[i];
                bool enabled = look == OptionLook.Enabled;
                bool hovered = enabled && menu.HoveredOption == i;
                bool pressed = enabled && menu.IsPressed(i);
                if (!hovered)
                    hoverSince[i] = -1f;
                else if (hoverSince[i] < 0f)
                    hoverSince[i] = now;
                float close = hovered ? Mathf.Clamp01((now - hoverSince[i]) / p.hoverSeconds) : 0f;

                PanelArt.CornerLook? corners = look == OptionLook.Unavailable ? (PanelArt.CornerLook?)null
                    : look == OptionLook.MissingEnergy ? PanelArt.CornerLook.Missing
                    : pressed ? PanelArt.CornerLook.Pressed
                    : hovered ? PanelArt.CornerLook.Hover
                    : PanelArt.CornerLook.Normal;
                PanelArt.NodeLook node = look == OptionLook.Unavailable ? PanelArt.NodeLook.Outline
                    : look == OptionLook.MissingEnergy ? PanelArt.NodeLook.Dithered
                    : PanelArt.NodeLook.Full;

                // La etiqueta: el nombre y el coste en su línea, cuando la opción ha llegado a su sitio.
                PanelArt.ButtonText c = p.text;
                Color32 nameColor = look == OptionLook.Unavailable ? c.unavailable
                    : look == OptionLook.MissingEnergy ? c.muted
                    : hovered || pressed ? c.hover : c.normal;
                Color32 costColor = look == OptionLook.MissingEnergy ? c.missing : hovered || pressed ? c.hoverCost : c.cost;
                string label = "<color=" + PanelSkin.Hex(nameColor) + ">" + names[i].text + "</color>";
                if (look != OptionLook.Unavailable)
                    label += " <color=" + PanelSkin.Hex(costColor) + ">" + costs[i].text + "</color>";
                optionLabels[i].text = label;
                optionLabels[i].enabled = k >= 0.999f;
                // Encima de la opción de arriba y debajo de las de abajo, con la mayúscula a nameGap del círculo de clic.
                float side = i == 0 ? 1f : -1f;
                float gap = (p.nameGap + 5) / (float)PrimitiveSprites.PixelsPerUnit;
                optionLabels[i].transform.localPosition = home + new Vector2(0f, side * (IgnitionMenuState.OptionRadius + gap));

                int key = ((corners.HasValue ? (int)corners.Value : 9) * 3 + (int)node) * 64 + Mathf.RoundToInt(close * 16f) + (pressed ? 32 : 0);
                if (key == drawnKey[i])
                    continue;
                drawnKey[i] = key;
                if (optionCanvases[i] == null)
                {
                    optionCanvases[i] = new CanvasSprite(p.cell, p.cell, new Vector2(0.5f, 0.5f), "IgnitionOption" + i);
                    optionViews[i].sprite = optionCanvases[i].Sprite;
                }
                PanelSkin.ComposeOption(optionCanvases[i].Canvas, p, i, corners, node, pressed, close);
                optionCanvases[i].Upload();
            }
        }

        void OnDestroy()
        {
            if (optionCanvases != null)
                foreach (CanvasSprite c in optionCanvases)
                    c?.Destroy();
        }

        void Paint(int i, ActionCheck check)
        {
            OptionLook look = check.IsAllowed ? OptionLook.Enabled
                : check.Block == ActionBlock.InsufficientEnergy ? OptionLook.MissingEnergy
                : OptionLook.Unavailable;
            looks[i] = look;

            discs[i].color = look == OptionLook.Enabled ? EnabledColor : DisabledColor;
            costs[i].text = check.Cost.ToString();
            costs[i].color = look == OptionLook.MissingEnergy ? MissingCostColor : CostColor;
            // Un tipo que todavía no existe no tiene precio que enseñar: solo el nombre, apagado.
            costs[i].enabled = look != OptionLook.Unavailable;
            names[i].color = look == OptionLook.Enabled ? NameColor : DisabledNameColor;
        }

        // La Energía del Asteroide cambia con el menú abierto: genera, recibe o le atacan.
        void Watch(NodeController node)
        {
            if (watched == node)
                return;
            if (watched != null)
                watched.OnEnergyChanged -= HandleEnergyChanged;
            watched = node;
            if (watched != null)
                watched.OnEnergyChanged += HandleEnergyChanged;
        }

        void HandleEnergyChanged(int previous, int current) => Refresh();

        void EnsureBuilt()
        {
            if (root != null)
                return;

            root = new GameObject("IgnitionMenu");
            root.transform.SetParent(transform, false);

            int count = IgnitionMenuState.Options.Count;
            discs = new SpriteRenderer[count];
            costs = new TextMeshPro[count];
            names = new TextMeshPro[count];

            int diameter = Mathf.RoundToInt(IgnitionMenuState.OptionRadius * 2f * PrimitiveSprites.PixelsPerUnit);
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = IgnitionMenuState.OptionPosition(Vector2.zero, i);

                var disc = new GameObject($"Option{i}");
                disc.transform.SetParent(root.transform, false);
                disc.transform.localPosition = offset;
                discs[i] = disc.AddComponent<SpriteRenderer>();
                discs[i].sprite = PrimitiveSprites.Disc(diameter);
                discs[i].sortingOrder = SortingOrder;

                costs[i] = Label(disc.transform, Vector2.zero, 2, diameter);

                // La etiqueta va hacia fuera, para no tapar el Asteroide del centro.
                Vector2 outward = offset.normalized * (IgnitionMenuState.OptionRadius + NameGap);
                names[i] = Label(disc.transform, outward, 1, diameter * 3);
                names[i].text = text != null ? text.TypeName(IgnitionMenuState.Options[i]) : IgnitionMenuState.Options[i].ToString();
            }
            optionViews = new SpriteRenderer[count];
            optionCanvases = new CanvasSprite[count];
            optionLabels = new TextMeshPro[count];
            for (int i = 0; i < count; i++)
            {
                optionViews[i] = new GameObject("OptionArt" + i).AddComponent<SpriteRenderer>();
                optionViews[i].transform.SetParent(root.transform, false);
                optionViews[i].sortingOrder = SortingOrder;
                optionViews[i].enabled = false;
                optionLabels[i] = Label(root.transform, Vector2.zero, 1, diameter * 3);
                optionLabels[i].richText = true;
                optionLabels[i].enabled = false;
            }

            root.SetActive(false);
        }

        TextMeshPro Label(Transform parent, Vector2 position, int scale, int widthPixels)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var label = go.AddComponent<TextMeshPro>();
            if (font != null)
                label.font = font;
            else
                Debug.LogWarning($"[Nova] {name}: IgnitionMenu sin font asset; asigna Jersey10 Raster56.", this);
            // Múltiplo entero del em nativo: un tamaño intermedio se interpola.
            label.fontSize = NativeEmPixels * scale / PrimitiveSprites.PixelsPerUnit / WorldTextScale;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(widthPixels, IgnitionMenuState.OptionRadius * 2f * PrimitiveSprites.PixelsPerUnit) / PrimitiveSprites.PixelsPerUnit;
            label.sortingOrder = SortingOrder + 1;
            return label;
        }
    }
}
