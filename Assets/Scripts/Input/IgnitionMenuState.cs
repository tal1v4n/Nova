using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Input
{
    // Estado del menú radial de encendido (13.7, T2-02): si está abierto, sobre qué Asteroide y
    // dónde. Lo abre y lo cierra InputRouter dentro del tick, y la elección sale por el emisor de
    // comandos como cualquier otra acción; IgnitionMenu (Nova.UI) solo lo dibuja.
    //
    // La geometría vive aquí y no en la vista porque el router resuelve el clic contra ella: el
    // dibujo y el blanco tienen que salir del mismo sitio.
    [DisallowMultipleComponent]
    public sealed class IgnitionMenuState : MonoBehaviour
    {
        // Siempre las tres y en el orden de 16.2 (13.7), en el sentido de las agujas del reloj
        // desde arriba.
        public static readonly IReadOnlyList<NodeType> Options = new[] { NodeType.Star, NodeType.Pulsar, NodeType.Magnetar };

        // Distancia del centro de cada opción al cursor: 64 px de la referencia, fuera del
        // círculo del Asteroide (48 px de diámetro) y de su trigger de 0.5 u.
        public const float OptionDistance = 1f;

        // Radio de cada opción, de dibujo y de clic: un disco de 48 px.
        public const float OptionRadius = 0.375f;

        // Lo que el menú ocupa alrededor del centro con las etiquetas, para no salirse de la cámara.
        public const float Extent = 1.75f;

        NodeController node;

        public bool IsOpen => node != null;
        public NodeController Node => node;
        public Vector2 Center { get; private set; }

        // Facción en nombre de la que se abrió: la vista la usa para validar cada opción.
        public Faction Actor { get; private set; }

        // Abierto, cerrado o reabierto en otro sitio.
        public event Action OnChanged;

        public static Vector2 OptionPosition(Vector2 center, int index)
        {
            float angle = (90f - 120f * index) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * OptionDistance;
        }

        // Opción bajo un punto del mundo, o -1 si no hay ninguna o el menú está cerrado.
        public int OptionAt(Vector2 world)
        {
            if (!IsOpen)
                return -1;
            for (int i = 0; i < Options.Count; i++)
            {
                if ((world - OptionPosition(Center, i)).sqrMagnitude <= OptionRadius * OptionRadius)
                    return i;
            }
            return -1;
        }

        public void Open(NodeController asteroid, Vector2 center, Faction actor)
        {
            if (asteroid == null)
                throw new ArgumentNullException(nameof(asteroid));

            Detach();
            node = asteroid;
            Center = center;
            Actor = actor;
            // Si el Asteroide deja de ser del que abrió o deja de ser Asteroide —capturado, o
            // encendido por la propia elección—, el menú ya no tiene sentido.
            node.OnFactionChanged += HandleFactionChanged;
            node.OnTypeChanged += HandleTypeChanged;
            OnChanged?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            Detach();
            OnChanged?.Invoke();
        }

        void OnDisable()
        {
            Close();
        }

        void Detach()
        {
            if (node == null)
                return;
            node.OnFactionChanged -= HandleFactionChanged;
            node.OnTypeChanged -= HandleTypeChanged;
            node = null;
        }

        void HandleFactionChanged(Faction previous, Faction current) => Close();
        void HandleTypeChanged(NodeType previous, NodeType current) => Close();
    }
}
