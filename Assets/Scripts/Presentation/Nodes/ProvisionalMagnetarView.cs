using Nova.Core;
using Nova.Gameplay;
using Nova.Systems;
using Nova.Utils;
using UnityEngine;

namespace Nova.Presentation
{
    // T2-04: vista provisional del Magnetar. Un anillo fino con su radio exacto, del color de su
    // facción, y un trazo del centro al protón en cada disparo. Primitivas de la regla 4 de T.2:
    // las sustituyen ART-WLD-RAD-* y ART-WLD-RAD-SHOT en T6-06.
    //
    // A diferencia de A.8 #1, el anillo se ve siempre, también en los Magnetares propios: en las
    // escenas de prueba es lo que permite comprobar el radio a ojo. Solo lee eventos del nodo y
    // de la torreta, nunca escribe en ellos.
    [DefaultExecutionOrder(100)] // Presentation, 15.10
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController), typeof(MagnetarTurret))]
    public sealed class ProvisionalMagnetarView : MonoBehaviour
    {
        const int PixelsPerUnit = 64; // T0-04

        [Tooltip("Grosor del anillo en píxeles.")]
        [SerializeField, Range(1, 3)] int ringThicknessPixels = 1;

        [Tooltip("Grosor del trazo de disparo en píxeles.")]
        [SerializeField, Range(1, 4)] int shotThicknessPixels = 2;

        [Tooltip("Segundos que se ve el trazo de un disparo.")]
        [SerializeField, Range(0.02f, 0.5f)] float shotSeconds = 0.1f;

        NodeController node;
        MagnetarTurret turret;
        SpriteRenderer ring;
        SpriteRenderer shot;
        float shotLeft;
        bool subscribed;

        internal SpriteRenderer Ring { get { EnsureBuilt(); return ring; } }
        internal SpriteRenderer Shot { get { EnsureBuilt(); return shot; } }

        void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        void Start()
        {
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void Update()
        {
            if (shotLeft <= 0f)
                return;
            shotLeft -= Time.deltaTime;
            if (shotLeft <= 0f)
                shot.enabled = false;
        }

        public void Refresh()
        {
            EnsureBuilt();

            bool visible = Node.IsInitialized && Node.Type == NodeType.Magnetar;
            ring.enabled = visible;
            if (!visible)
            {
                shot.enabled = false;
                return;
            }

            int diameter = Mathf.RoundToInt(Node.CurrentLevelData.Radius * 2f * PixelsPerUnit);
            ring.sprite = ProvisionalSelectionView.RingSprite(diameter, ringThicknessPixels);
            Color color = ProvisionalNodeView.ColorOf(Node.Faction);
            color.a = 0.5f;
            ring.color = color;
        }

        internal void Subscribe()
        {
            if (subscribed)
                return;
            Node.OnTypeChanged += HandleTypeChanged;
            Node.OnLevelChanged += HandleLevelChanged;
            Node.OnFactionChanged += HandleFactionChanged;
            Turret.OnShot += HandleShot;
            subscribed = true;
        }

        internal void Unsubscribe()
        {
            if (!subscribed)
                return;
            Node.OnTypeChanged -= HandleTypeChanged;
            Node.OnLevelChanged -= HandleLevelChanged;
            Node.OnFactionChanged -= HandleFactionChanged;
            Turret.OnShot -= HandleShot;
            subscribed = false;
        }

        void HandleTypeChanged(NodeType previous, NodeType current) => Refresh();
        void HandleLevelChanged(int previous, int current) => Refresh();
        void HandleFactionChanged(Faction previous, Faction current) => Refresh();

        // Un trazo recto del centro al protón: un píxel blanco estirado y girado. Es un color
        // liso, así que estirarlo no rompe ninguna rejilla.
        void HandleShot(MagnetarTurret source, Proton target)
        {
            EnsureBuilt();
            Vector2 from = transform.position;
            Vector2 to = target.transform.position;
            Vector2 delta = to - from;

            shot.transform.position = (from + to) / 2f;
            shot.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            shot.transform.localScale = new Vector3(delta.magnitude * PixelsPerUnit, shotThicknessPixels, 1f);
            shot.color = ProvisionalNodeView.ColorOf(Node.Faction);
            shot.enabled = true;
            shotLeft = shotSeconds;
        }

        void EnsureBuilt()
        {
            if (ring != null && shot != null)
                return;

            ring = new GameObject("ProvisionalRadius").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(transform, false);
            ring.sortingOrder = -2; // detrás del nodo y del anillo de selección

            // Hijo del nodo pero colocado en mundo: se mueve y gira con cada disparo.
            shot = new GameObject("ProvisionalShot").AddComponent<SpriteRenderer>();
            shot.transform.SetParent(transform, false);
            shot.sprite = PrimitiveSprites.Rect(1, 1);
            shot.sortingOrder = 5; // por encima de los protones
            shot.enabled = false;
        }

        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
        MagnetarTurret Turret => turret != null ? turret : turret = GetComponent<MagnetarTurret>();
    }
}
