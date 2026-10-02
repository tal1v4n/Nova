using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nova.Input
{
    // Traduce el ratón a intenciones (13.1, 13.2, 15.7). No conoce reglas de juego: qué es un
    // origen legal lo dice SelectionManager y si un envío sale lo dice el ProtonLauncher.
    //
    // El ratón se lee en Update, que es donde el Input System marca los botones de este frame,
    // pero los eventos se encolan y se aplican en FixedUpdate: todo cambio de estado de la partida
    // ocurre dentro del tick de 50 Hz (15.10, 18 #22), a -50, antes que los nodos.
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class InputRouter : MonoBehaviour
    {
        // Capa de física de los nodos (18 #23). La crea Nova.Editor.ProjectLayers.
        public const string NodesLayer = "Nodes";

        // PPU del contrato de arte (A.8 #4, T0-04): un píxel de la referencia 960x540 mide 1/64 u.
        const float PixelsPerUnit = 64f;
        const double MicrosPerSecond = 1_000_000d;

        enum PointerKind { LeftDown, Move, LeftUp, Right, Escape }

        readonly struct PointerEvent
        {
            public readonly PointerKind Kind;
            public readonly Vector2 World;
            public readonly NodeController Node;

            public PointerEvent(PointerKind kind, Vector2 world, NodeController node)
            {
                Kind = kind;
                World = world;
                Node = node;
            }
        }

        [SerializeField] BalanceConfig balance;
        [SerializeField] SelectionManager selection;
        [SerializeField] PlayerCommandIssuer issuer;
        [SerializeField] IgnitionMenuState ignitionMenu;
        [SerializeField] InfoPanelState infoPanel;

        [Tooltip("Opcional: sin ella se usa Camera.main.")]
        [SerializeField] Camera worldCamera;

        readonly Queue<PointerEvent> pending = new Queue<PointerEvent>();
        int nodesMask = -1;

        long nowMicros;

        // Pulsación izquierda en curso.
        bool pressing;
        bool dragging;
        Vector2 pressWorld;
        NodeController pressNode;

        // Buffer del doble clic (13.2).
        bool bufferActive;
        NodeController bufferTarget;
        long bufferDeadline;

        // 18 #27: tras deseleccionar con un clic, el segundo clic de un doble clic sobre el
        // mismo nodo se ignora.
        NodeController ignoredNode;
        long ignoredUntil;

        // Intención que todavía no tiene quien la atienda: el menú de pausa llega con T5-05.
        public event Action OnPauseRequested;

        public bool IsBufferActive => bufferActive;

        void Update()
        {
            Mouse mouse = Mouse.current;
            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (mouse != null && cam != null)
            {
                Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                if (mouse.leftButton.wasPressedThisFrame)
                    pending.Enqueue(new PointerEvent(PointerKind.LeftDown, world, PickNode(world)));
                if (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame)
                    pending.Enqueue(new PointerEvent(PointerKind.Move, world, null));
                if (mouse.leftButton.wasReleasedThisFrame)
                    pending.Enqueue(new PointerEvent(PointerKind.LeftUp, world, PickNode(world)));
                if (mouse.rightButton.wasPressedThisFrame)
                    pending.Enqueue(new PointerEvent(PointerKind.Right, world, PickNode(world)));
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                pending.Enqueue(new PointerEvent(PointerKind.Escape, default, null));
        }

        void FixedUpdate()
        {
            while (pending.Count > 0)
            {
                PointerEvent e = pending.Dequeue();
                switch (e.Kind)
                {
                    case PointerKind.LeftDown: PressLeft(e.World, e.Node); break;
                    case PointerKind.Move: MoveTo(e.World); break;
                    case PointerKind.LeftUp: ReleaseLeft(e.World, e.Node); break;
                    case PointerKind.Right: ClickRight(e.Node); break;
                    case PointerKind.Escape: PressEscape(); break;
                }
            }
            Step(Time.fixedDeltaTime);
        }

        // Nodo bajo un punto del mundo: OverlapPoint contra el trigger de la capa Nodes (18 #23).
        public NodeController PickNode(Vector2 world)
        {
            if (nodesMask < 0)
            {
                nodesMask = LayerMask.GetMask(NodesLayer);
                if (nodesMask == 0)
                    Debug.LogError($"[Nova] No existe la capa de física «{NodesLayer}»: ningún clic encontrará un nodo.", this);
            }
            if (nodesMask == 0)
                return null;

            Collider2D hit = Physics2D.OverlapPoint(world, nodesMask);
            return hit != null ? hit.GetComponentInParent<NodeController>() : null;
        }

        // Los cinco eventos de entrada, ya traducidos a mundo y a nodo. Públicos para los tests,
        // que no tienen ratón; en partida los llama FixedUpdate.

        public void PressLeft(Vector2 world, NodeController node)
        {
            pressing = true;
            dragging = false;
            pressWorld = world;
            pressNode = node;
        }

        public void MoveTo(Vector2 world)
        {
            if (!pressing || dragging)
                return;
            // 13.1: arrastre cuando el cursor se aleja MÁS del umbral; por debajo es un clic.
            float threshold = Balance.DragThreshold / PixelsPerUnit;
            if ((world - pressWorld).sqrMagnitude > threshold * threshold)
                dragging = true;
        }

        public void ReleaseLeft(Vector2 world, NodeController node)
        {
            if (!pressing)
                return;
            MoveTo(world);
            pressing = false;

            // Menú de encendido abierto (13.7): un clic sobre una opción la elige; cualquier otro
            // clic o arrastre lo cierra sin encender y sigue su curso normal.
            if (Menu.IsOpen)
            {
                int option = dragging ? -1 : Menu.OptionAt(pressWorld);
                if (option >= 0)
                {
                    ChooseIgnition(option);
                    return;
                }
                Menu.Close();
            }

            // Panel de clic derecho (13.3): un clic dentro es suyo y no llega a lo que haya
            // debajo; sobre el botón, mejora. Las reglas deciden si se puede: con el botón
            // deshabilitado o ausente, el clic no hace nada.
            if (!dragging && Panel.Contains(pressWorld))
            {
                if (Panel.IsOnButton(pressWorld))
                    Issuer.Upgrade(Panel.Node);
                return;
            }

            if (!dragging)
            {
                Click(pressNode, pressWorld);
                return;
            }

            if (pressNode == null)
            {
                // Arrastre en vacío: lazo, que sustituye a la selección (18 #26).
                Selection.SelectInRect(pressWorld, world);
            }
            else if (SelectionManager.CanSelect(pressNode, Issuer.Faction) && node != null && node != pressNode)
            {
                // Arrastre desde un origen propio: 50 % desde ese único origen, al soltar y sin
                // buffer (13.1, 18 #25). Soltar en vacío o sobre el origen no hace nada.
                Issuer.Send(pressNode, node, false);
            }
            // Un arrastre que empieza sobre un nodo que no es origen legal no hace nada.
        }

        public void ClickRight(NodeController node)
        {
            // 13.7: el clic derecho cierra el menú sin encender, y no hace nada más.
            if (Menu.IsOpen)
            {
                Menu.Close();
                return;
            }

            // Sobre un nodo, cualquiera, abre su panel; la selección no cambia (13.1).
            if (node != null)
            {
                Panel.Open(node, PanelCenterFor(node));
                return;
            }
            // Clic derecho en vacío: cancela la selección y cierra el panel.
            Selection.Clear();
            Panel.Close();
        }

        public void PressEscape()
        {
            // Con el menú abierto, Esc lo cierra en vez de pausar.
            if (Menu.IsOpen)
            {
                Menu.Close();
                return;
            }
            // Y con el panel abierto, lo cierra.
            if (Panel.IsOpen)
            {
                Panel.Close();
                return;
            }
            OnPauseRequested?.Invoke();
        }

        // Avanza el reloj del buffer. En partida, un tick fijo; en los tests, lo que haga falta.
        public void Step(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            nowMicros += ToMicros(deltaTime);
            if (bufferActive && nowMicros >= bufferDeadline)
                FlushBuffer();
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config, SelectionManager selectionManager, PlayerCommandIssuer commandIssuer,
            IgnitionMenuState menu, InfoPanelState panel, Camera cam = null)
        {
            balance = config;
            selection = selectionManager;
            issuer = commandIssuer;
            ignitionMenu = menu;
            infoPanel = panel;
            worldCamera = cam;
        }

        // Clic izquierdo sin arrastre (13.1), con el cursor en world.
        void Click(NodeController node, Vector2 world)
        {
            // 18 #27: el segundo clic de un doble clic sobre un nodo recién deseleccionado se
            // ignora en cualquier modo, también si la deselección dejó la selección vacía.
            if (node != null && node == ignoredNode && nowMicros < ignoredUntil)
            {
                ignoredNode = null;
                return;
            }

            // Clic en vacío: nada, haya o no selección (18 #26).
            if (node == null)
                return;

            Faction player = Issuer.Faction;

            if (!Selection.HasSelection)
            {
                // Sin selección, el clic elige.
                if (SelectionManager.CanSelect(node, player))
                    Selection.Select(node);
                else if (node.IsInitialized && node.Type == NodeType.Asteroid && node.Faction == player)
                {
                    // Abre el menú radial en el cursor (13.7), también sin Energía suficiente:
                    // las opciones salen deshabilitadas con el coste en rojo.
                    Menu.Open(node, KeepInView(world, IgnitionMenuState.Extent, IgnitionMenuState.Extent), player);
                }
                // Sobre un nodo que no es propio, nada.
                return;
            }

            // Con selección, el clic envía; sobre un nodo ya seleccionado, lo saca.
            if (Selection.IsSelected(node))
            {
                Selection.Deselect(node);
                ignoredNode = node;
                ignoredUntil = nowMicros + BufferMicros;
                return;
            }

            // 13.2: solo es doble clic sobre el mismo destino. Un clic en otro destino envía ya
            // el 50 % pendiente y abre un buffer nuevo (18 #24).
            if (bufferActive && node == bufferTarget)
            {
                bufferActive = false;
                bufferTarget = null;
                Issuer.SendFromEach(Selection.Selected, node, true);
                return;
            }

            if (bufferActive)
                FlushBuffer();

            bufferActive = true;
            bufferTarget = node;
            bufferDeadline = nowMicros + BufferMicros;
        }

        // Una opción deshabilitada no hace nada y deja el menú abierto: el jugador sigue viendo
        // cuánto le falta. Si enciende, el cambio de tipo cierra el menú.
        void ChooseIgnition(int option)
        {
            if (Issuer.Ignite(Menu.Node, IgnitionMenuState.Options[option]).IsAllowed)
                Menu.Close();
        }

        // Desplaza un centro lo justo para que lo que ocupa alrededor —la mitad de ancho y de alto
        // dadas— no se salga de la cámara. Sin cámara, en los tests, lo deja donde está.
        Vector2 KeepInView(Vector2 center, float halfWidthUsed, float halfHeightUsed)
        {
            Camera cam = ViewCamera;
            if (cam == null)
                return center;

            Vector2 camera = cam.transform.position;
            float halfHeight = Mathf.Max(0f, cam.orthographicSize - halfHeightUsed);
            float halfWidth = Mathf.Max(0f, cam.orthographicSize * cam.aspect - halfWidthUsed);
            return new Vector2(
                Mathf.Clamp(center.x, camera.x - halfWidth, camera.x + halfWidth),
                Mathf.Clamp(center.y, camera.y - halfHeight, camera.y + halfHeight));
        }

        // El panel se abre a la derecha del nodo, o a la izquierda si a la derecha no cabe, y
        // se desplaza en vertical lo justo para no salirse de la cámara.
        Vector2 PanelCenterFor(NodeController node)
        {
            Vector2 position = node.transform.position;
            Vector2 center = InfoPanelState.CenterBeside(position, false);
            Camera cam = ViewCamera;
            if (cam != null)
            {
                float right = cam.transform.position.x + cam.orthographicSize * cam.aspect;
                if (center.x + InfoPanelState.Width / 2f > right)
                    center = InfoPanelState.CenterBeside(position, true);
            }
            return KeepInView(center, InfoPanelState.Width / 2f, InfoPanelState.Height / 2f);
        }

        // Cámara ortográfica del tablero; null en los tests, donde nada se desplaza.
        Camera ViewCamera
        {
            get
            {
                Camera cam = worldCamera != null ? worldCamera : Camera.main;
                return cam != null && cam.orthographic ? cam : null;
            }
        }

        // Vence el buffer, o lo interrumpe un clic en otro destino: 50 % desde cada origen.
        void FlushBuffer()
        {
            NodeController target = bufferTarget;
            bufferActive = false;
            bufferTarget = null;
            if (target != null)
                Issuer.SendFromEach(Selection.Selected, target, false);
        }

        long BufferMicros => ToMicros(Balance.DoubleClickBuffer);

        static long ToMicros(float seconds) => (long)Math.Round(seconds * MicrosPerSecond);

        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("InputRouter necesita BalanceConfig.");
        SelectionManager Selection => selection != null ? selection : throw new InvalidOperationException("InputRouter necesita SelectionManager.");
        PlayerCommandIssuer Issuer => issuer != null ? issuer : throw new InvalidOperationException("InputRouter necesita PlayerCommandIssuer.");
        IgnitionMenuState Menu => ignitionMenu != null ? ignitionMenu : throw new InvalidOperationException("InputRouter necesita IgnitionMenuState.");
        InfoPanelState Panel => infoPanel != null ? infoPanel : throw new InvalidOperationException("InputRouter necesita InfoPanelState.");
    }
}
