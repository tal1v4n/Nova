using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // Definición de un power-up del cielo (15.4, 9.2). Una instancia por tipo en
    // Assets/Data/PowerUps/. La ventana de 30 s antes de apagarse es del drop, no del tipo, y vive en
    // BalanceConfig; el calendario de drops es del nivel (LevelDefinition.Drops).
    [CreateAssetMenu(menuName = "Nova/Power-Up Definition", fileName = "PowerUpDefinition")]
    public sealed class PowerUpDefinition : ScriptableObject
    {
        [SerializeField] PowerUpType type;

        [Tooltip("Lo que acelera (9.2).")]
        [SerializeField] PowerUpStat stat;

        [Tooltip("Aumento sobre la estadística: 0.5 = +50 % (9.1).")]
        [SerializeField, Min(0f)] float magnitude;

        [Tooltip("Segundos que dura el efecto sobre la facción (9.1).")]
        [SerializeField, Min(0.01f)] float duration;

        [Tooltip("Peso en el sorteo del tipo de cada drop. Con los dos iguales, 50 % / 50 % (9.2).")]
        [SerializeField, Min(0f)] float dropWeight = 1f;

        [Tooltip("Icono flotante sobre el Asteroide marcado (9.1). Llega con la presentación (T6).")]
        [SerializeField] Sprite icon;

        public PowerUpType Type => type;
        public PowerUpStat Stat => stat;
        public float Magnitude => magnitude;
        public float Duration => duration;
        public float DropWeight => dropWeight;
        public Sprite Icon => icon;

        // Factor sobre la velocidad de la estadística: 1.5 con +50 %. El intervalo de la Estrella se
        // divide por él, que es el «× 0.667» de 9.2.
        public float Multiplier => 1f + magnitude;

        // Para los tests: el cableado normal es el asset.
        public void Configure(PowerUpType powerUpType, PowerUpStat affected, float powerUpMagnitude, float seconds, float weight = 1f)
        {
            type = powerUpType;
            stat = affected;
            magnitude = powerUpMagnitude;
            duration = seconds;
            dropWeight = weight;
        }
    }
}
