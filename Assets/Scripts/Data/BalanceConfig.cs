using UnityEngine;

namespace Nova.Data
{
    // Parámetros globales de balance (15.4, 16.1). Una sola instancia: Assets/Data/BalanceConfig.asset.
    // Lo que depende del tipo o del nivel vive en NodeDefinition; lo de poderes y power-ups,
    // en PowerDefinition y PowerUpDefinition.
    [CreateAssetMenu(menuName = "Nova/Balance Config", fileName = "BalanceConfig")]
    public sealed class BalanceConfig : ScriptableObject
    {
        [Header("Protones")]
        [Tooltip("u/s, constante (6.1).")]
        [SerializeField, Min(0f)] float protonSpeed;

        [Header("Entrada")]
        [Tooltip("Buffer del doble clic en segundos (13.2).")]
        [SerializeField, Min(0f)] float doubleClickBuffer;

        [Tooltip("Píxeles de la referencia 960x540 que separan un clic de un arrastre (13.1). 4 = 8 px físicos a 1080p.")]
        [SerializeField, Min(0)] int dragThreshold;

        [Header("Magnetar")]
        [Tooltip("Carga que resta cada disparo (6.3).")]
        [SerializeField, Min(0)] int magnetarShotDamage;

        [Tooltip("Multiplicador de un impacto hostil sobre un Magnetar (6.2).")]
        [SerializeField, Range(0.01f, 1f)] float magnetarResistance;

        [Header("Encendido y reconversión")]
        [Tooltip("Energía para encender un Asteroide propio, igual para los tres tipos (4.1).")]
        [SerializeField, Min(0)] int ignitionCost;

        [Tooltip("Energía de un Asteroide neutral (4.1).")]
        [SerializeField, Min(0)] int inertMatter;

        [Tooltip("Duración de la reconversión en segundos (8.1).")]
        [SerializeField, Min(0f)] float conversionDuration;

        [Header("Estados")]
        [Tooltip("Segundos sin impactos para el auto-deshielo (7.3).")]
        [SerializeField, Min(0f)] float autoThawTime;

        [Tooltip("Energía por segundo que pierde un nodo infectado (7.4).")]
        [SerializeField, Min(0f)] float decayRate;

        [Tooltip("Segundos antes del impacto letal en que el nodo se asusta (14.2).")]
        [SerializeField, Min(0f)] float scareThreshold;

        [Header("Púlsar")]
        [Tooltip("Fracción de la Energía invertida que se devuelve al cancelar una carga (7.1).")]
        [SerializeField, Range(0f, 1f)] float chargeCancelRefund;

        [Header("Fin de nivel")]
        [Tooltip("Segundos de la cascada de conquista al ganar (10.1).")]
        [SerializeField, Min(0f)] float cascadeDuration;

        public float ProtonSpeed => protonSpeed;
        public float DoubleClickBuffer => doubleClickBuffer;
        public int DragThreshold => dragThreshold;
        public int MagnetarShotDamage => magnetarShotDamage;
        public float MagnetarResistance => magnetarResistance;
        public int IgnitionCost => ignitionCost;
        public int InertMatter => inertMatter;
        public float ConversionDuration => conversionDuration;
        public float AutoThawTime => autoThawTime;
        public float DecayRate => decayRate;
        public float ScareThreshold => scareThreshold;
        public float ChargeCancelRefund => chargeCancelRefund;
        public float CascadeDuration => cascadeDuration;
    }
}
