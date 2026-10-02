using Nova.Core;
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
    // Discos y texto son primitivas de la regla 4 de T.2; la skin final es de T6-09.
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

        [Tooltip("Nombre de cada opción, en el orden de IgnitionMenuState.Options. Pendiente de T.7 #39 (idioma).")]
        [SerializeField] string[] optionNames = { "Estrella", "Púlsar", "Magnetar" };

        GameObject root;
        SpriteRenderer[] discs;
        TextMeshPro[] costs;
        TextMeshPro[] names;
        readonly OptionLook[] looks = new OptionLook[3];

        NodeController watched;
        bool subscribed;

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
        public void Configure(IgnitionMenuState state, TMP_FontAsset fontAsset)
        {
            Unsubscribe();
            menu = state;
            font = fontAsset;
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
            NodeIgnition ignition = menu.Node.GetComponent<NodeIgnition>();
            for (int i = 0; i < IgnitionMenuState.Options.Count; i++)
            {
                ActionCheck check = ignition != null
                    ? ignition.Check(menu.Actor, IgnitionMenuState.Options[i])
                    : new ActionCheck(ActionBlock.Unavailable, 0);
                Paint(i, check);
            }
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
                names[i].text = i < optionNames.Length ? optionNames[i] : IgnitionMenuState.Options[i].ToString();
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
