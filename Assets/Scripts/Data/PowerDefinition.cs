using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // Definición de un poder del Púlsar (15.4, 7.2, 16.3). Una instancia por poder en
    // Assets/Data/Powers/. Sin campo de alcance: el alcance es global y no es un parámetro de
    // balance (7.2). El drenaje no se guarda: sale de coste y tiempo de carga (7.1).
    // El VFX llega con la presentación (T6); el color del haz es paleta funcional (14.4).
    [CreateAssetMenu(menuName = "Nova/Power Definition", fileName = "PowerDefinition")]
    public sealed class PowerDefinition : ScriptableObject
    {
        [SerializeField] PowerType type;

        [Tooltip("Nivel mínimo del Púlsar para cargarlo (7.2).")]
        [SerializeField, Range(1, 3)] int requiredLevel = 1;

        [Tooltip("Energía total que se invierte durante la carga (16.3).")]
        [SerializeField, Min(1)] int cost;

        [Tooltip("Segundos de carga con el Púlsar sin pausar (16.3).")]
        [SerializeField, Min(0.01f)] float chargeTime;

        [Tooltip("Segundos del haz, constantes y sin depender de la distancia (7.2.2).")]
        [SerializeField, Min(0.01f)] float beamDuration;

        [Tooltip("Color del haz, por poder y no por facción (7.2.2, 14.4).")]
        [SerializeField] Color beamColor;

        public PowerType Type => type;
        public int RequiredLevel => requiredLevel;
        public int Cost => cost;
        public float ChargeTime => chargeTime;
        public float BeamDuration => beamDuration;
        public Color BeamColor => beamColor;

        // Energía por segundo que la carga quita al Púlsar (7.1, 16.3): las dos curvas están
        // acopladas, así que es el coste repartido en el tiempo de carga.
        public float DrainRate => cost / chargeTime;
    }
}
