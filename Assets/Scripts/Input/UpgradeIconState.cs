using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Systems;
using UnityEngine;

namespace Nova.Input
{
    // Cómo se ve el icono de mejora de un nodo (13.13). Los valores son las filas de ui_upgrade_icon_sheet.
    public enum UpgradeIconLook
    {
        Hidden = -1,
        Hint = 0,     // aviso: de coste − margen a coste − 1, difuminado
        Ready = 1,    // listo: con el coste y el cursor fuera del nodo, difuminado
        Active = 2,   // activo: con el coste y el cursor sobre el nodo, al 100 %; el clic mejora
        Over = 3,     // encima: el cursor sobre el icono activo
        Pressed = 4,  // pulsado: el botón izquierdo bajó sobre el icono y el cursor sigue en él
    }

    // T6-15: el icono de mejora sobre el nodo (13.13, 18 #54) para la entrada. Qué icono enseña cada nodo, dónde
    // está —de HudArt— y qué hace el cursor con él. Lo escribe InputRouter en cada frame, como HudState, y lo lee
    // UpgradeIcon (Nova.Presentation) para dibujarlo. Vive en Nova.Input por lo mismo que InfoPanelState: el
    // router resuelve el clic contra él.
    //
    // «Sobre el nodo» es sobre su huella —el trigger de NodeCollider2D— o sobre su propio icono, para que el icono
    // no se apague al subir el cursor hasta él.
    [DisallowMultipleComponent]
    public sealed class UpgradeIconState : MonoBehaviour
    {
        const float PixelsPerUnit = 64f;

        [SerializeField] HudArt art;
        [SerializeField] BalanceConfig balance;
        [SerializeField] NodeRegistry registry;

        [Tooltip("Facción que ve los iconos: solo salen en sus nodos.")]
        [SerializeField] Faction viewer = Faction.Player;

        bool held;
        NodeController pressedIcon;

        // Sin arte no hay icono, y sin icono no hay mejora: el panel ya no la ofrece (13.3).
        public bool HasIcon => art != null && art.HasUpgradeIcon;

        public Faction Viewer => viewer;
        public HudArt Art => art;

        // El nodo que tiene el cursor encima, por su huella o por su icono; null si ninguno.
        public NodeController HoveredNode { get; private set; }

        // El nodo cuyo icono visible tiene el cursor encima; null si ninguno.
        public NodeController IconUnderCursor { get; private set; }

        int Margin => balance != null ? balance.UpgradeHintMargin : 0;

        // 13.13 sin el cursor sobre el icono: si aparece y cómo, por la Energía del nodo y por si el cursor
        // está sobre el nodo. Solo en un nodo propio que pueda mejorarse: ni al máximo, ni Congelado, ni
        // Reconvirtiendo (18 #18), ni Asteroide, ni del Enemigo o neutral. La validación es la de la mejora.
        public static UpgradeIconLook BaseLook(NodeController node, Faction viewer, int margin, bool hovered)
        {
            if (node == null || !node.IsInitialized)
                return UpgradeIconLook.Hidden;
            ActionCheck check = node.CheckUpgrade(viewer);
            if (check.IsAllowed)
                return hovered ? UpgradeIconLook.Active : UpgradeIconLook.Ready;
            if (check.Block == ActionBlock.InsufficientEnergy && node.Energy >= check.Cost - margin)
                return UpgradeIconLook.Hint;
            return UpgradeIconLook.Hidden;
        }

        // El icono de un nodo, con el cursor tal como lo dejó el último SetPointer.
        public UpgradeIconLook LookOf(NodeController node)
        {
            if (!HasIcon)
                return UpgradeIconLook.Hidden;
            UpgradeIconLook look = BaseLook(node, viewer, Margin, node != null && node == HoveredNode);
            if (look != UpgradeIconLook.Active || node != IconUnderCursor)
                return look;
            return node == pressedIcon && held ? UpgradeIconLook.Pressed : UpgradeIconLook.Over;
        }

        // Dónde va el pivote del icono, abajo al centro: centrado sobre la huella y a gap px de ella. Es lo que
        // comprueba tools/gen_upgicon_v1.py contra los nueve niveles: si cambia uno, cambia el otro.
        public Vector2 Anchor(NodeController node)
        {
            Vector2 center = node.transform.position;
            int footprint = node.IsInitialized && node.Definition.TryGetArt(node.Level, out NodeLevelArt levelArt)
                ? levelArt.FootprintPixels
                : 0;
            int gap = art != null ? art.UpgradeIconGap : 0;
            return center + new Vector2(0f, ((footprint + 1) / 2 + gap) / PixelsPerUnit);
        }

        // La zona que se pulsa, en el mundo: el rombo, centrado en x en la celda y pegado a su borde de arriba. El
        // pivote cae en la columna cell.x / 2 (entera), como en el generador.
        public Rect HitRect(NodeController node)
        {
            Vector2Int cell = art.UpgradeIconCell;
            Vector2Int hit = art.UpgradeIconHitBox;
            float left = -(cell.x / 2) + (cell.x - hit.x) / 2;
            float bottom = cell.y - hit.y;
            return new Rect(Anchor(node) + new Vector2(left, bottom) / PixelsPerUnit, (Vector2)hit / PixelsPerUnit);
        }

        // El nodo cuyo icono se ve y contiene world; con activeOnly, solo si el icono se puede pulsar, es decir,
        // si la mejora se puede pagar: con el cursor sobre el icono, el cursor está sobre el nodo.
        public NodeController IconAt(Vector2 world, bool activeOnly)
        {
            if (!HasIcon || registry == null)
                return null;
            foreach (NodeController node in registry.All)
            {
                UpgradeIconLook look = BaseLook(node, viewer, Margin, true);
                if (look == UpgradeIconLook.Hidden || (activeOnly && look != UpgradeIconLook.Active))
                    continue;
                if (HitRect(node).Contains(world))
                    return node;
            }
            return null;
        }

        // Del router, en Update: el cursor, el nodo bajo él por su huella y si el botón izquierdo está abajo.
        // El pulsado solo cuenta si la pulsación empezó sobre un icono activo.
        public void SetPointer(Vector2 world, NodeController nodeUnderCursor, bool leftHeld)
        {
            IconUnderCursor = IconAt(world, false);
            HoveredNode = IconUnderCursor != null ? IconUnderCursor : nodeUnderCursor;
            if (leftHeld && !held)
                pressedIcon = IconAt(world, true);
            held = leftHeld;
            if (!held)
                pressedIcon = null;
        }

        // Al pausar: el cursor se queda donde estaba, pero ningún icono sigue encima ni pulsado.
        public void Release()
        {
            held = false;
            pressedIcon = null;
            HoveredNode = null;
            IconUnderCursor = null;
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(HudArt hudArt, BalanceConfig config, NodeRegistry nodes, Faction faction = Faction.Player)
        {
            art = hudArt;
            balance = config;
            registry = nodes;
            viewer = faction;
        }
    }
}
