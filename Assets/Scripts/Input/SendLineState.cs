using System.Collections.Generic;
using Nova.Core;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Input
{
    // Estado de la línea de envío (13.1, T2-09): desde qué nodos sale y hasta dónde llega. La línea
    // es feedback de entrada, no información de juego, así que solo existe para el Jugador.
    //
    // InputRouter escribe el cursor cada frame y abre o cierra el arrastre derecho dentro del tick;
    // la vista solo lee Origins y Cursor. De qué nodos sale se calcula aquí, en una sola regla:
    //   - con el menú de encendido abierto, de ninguno (y en modo apuntado, desde T3);
    //   - con un arrastre derecho en curso, solo de su origen;
    //   - si no, de cada nodo seleccionado, en el orden de la selección.
    [DisallowMultipleComponent]
    public sealed class SendLineState : MonoBehaviour
    {
        [SerializeField] SelectionManager selection;

        [Tooltip("Opcional: con el menú de encendido abierto no hay línea.")]
        [SerializeField] IgnitionMenuState ignitionMenu;

        readonly List<NodeController> origins = new List<NodeController>();
        NodeController dragOrigin;

        // Punta de todas las líneas: el cursor en el mundo, sin saltar al nodo de debajo.
        public Vector2 Cursor { get; private set; }

        // Origen del arrastre derecho que ya ha superado el umbral, o null.
        public NodeController DragOrigin => dragOrigin;

        // Nodos de los que sale una línea ahora mismo. La lista es la misma instancia en cada
        // llamada: se lee en el frame y no se guarda.
        public IReadOnlyList<NodeController> Origins
        {
            get
            {
                origins.Clear();
                if (ignitionMenu != null && ignitionMenu.IsOpen)
                    return origins;

                if (dragOrigin != null)
                {
                    // Un origen que deja de ser propio a mitad de gesto pierde la línea: soltando
                    // ya no enviaría.
                    if (SelectionManager.CanSelect(dragOrigin, Owner))
                        origins.Add(dragOrigin);
                    return origins;
                }
                if (selection != null)
                    origins.AddRange(selection.Selected);
                return origins;
            }
        }

        public void SetCursor(Vector2 world)
        {
            Cursor = world;
        }

        public void BeginDrag(NodeController origin)
        {
            dragOrigin = origin;
        }

        public void EndDrag()
        {
            dragOrigin = null;
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(SelectionManager selectionManager, IgnitionMenuState menu = null)
        {
            selection = selectionManager;
            ignitionMenu = menu;
        }

        // Hoy solo el Jugador tiene cursor (14.9).
        Faction Owner => selection != null ? selection.Owner : Faction.Player;
    }
}
