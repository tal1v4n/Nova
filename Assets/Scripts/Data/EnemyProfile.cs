using UnityEngine;

namespace Nova.Data
{
    // Perfil del algoritmo del Enemigo (12.5, 15.4, T4-01): los ocho parámetros de la tabla de 12.5 y
    // la Paciencia de 12.5.1. En Assets/Data/EnemyProfiles/ hay dos juegos: Map1–Map3, los valores de
    // referencia de 12.5 que usan los tests, y Level1Enemy–Level9Enemy, uno por nivel, que referencia
    // cada LevelDefinition y que se ajustan a mano en la ventana Nova → Dificultad (12.5.2). Lo leen
    // EnemyController, EnemyEvaluator y EnemyThreatResponder en cada ciclo: un cambio en Play se nota
    // en la partida en curso.
    //
    // Solo datos: qué hace cada parámetro está en 12.3, 12.3.2, 12.3.3 y 12.4 (T.7 #48).
    [CreateAssetMenu(menuName = "Nova/Enemy Profile", fileName = "EnemyProfile")]
    public sealed class EnemyProfile : ScriptableObject
    {
        [Tooltip("Segundos entre dos decisiones (12.1). Una acción como máximo por ciclo.")]
        [SerializeField, Min(0.02f)] float decisionInterval;

        [Tooltip("UmbralDeAccion: la mejor candidata solo se ejecuta si lo supera (12.1).")]
        [SerializeField, Min(0f)] float actionThreshold;

        [Tooltip("Multiplica la puntuación de atacar, lanzar poder y cargar poder (12.3, 12.3.2).")]
        [SerializeField, Min(0f)] float aggressionMultiplier = 1f;

        [Tooltip("Paso 3 de la defensa (12.4): ceder un nodo vacío en vez de uno lleno.")]
        [SerializeField] bool usesEvacuation;

        [Tooltip("Reconvierte un nodo en Estrella cuando se queda sin ninguna (12.3.2).")]
        [SerializeField] bool usesConversion;

        [Tooltip("Activa BonusPowerUp (12.3); sin él vale 0.")]
        [SerializeField] bool prioritizesPowerUps;

        [Tooltip("Margen de «Envía al 100 %» (12.3): el 100 % solo sale si Energía ≥ CosteEfectivo × margen. 0 = nunca.")]
        [SerializeField, Min(0f)] float fullSendMargin;

        [Tooltip("Ruido de decisión r: cada candidata se multiplica por 1 + U(−r, +r) (12.3.3).")]
        [SerializeField, Range(0f, 1f)] float decisionNoise;

        [Tooltip("Paciencia (12.5.1): segundos de partida sin atacar al Jugador ni lanzarle poderes; solo se desarrolla.")]
        [SerializeField, Min(0f)] float gracePeriod;

        public float DecisionInterval => decisionInterval;
        public float ActionThreshold => actionThreshold;
        public float AggressionMultiplier => aggressionMultiplier;
        public bool UsesEvacuation => usesEvacuation;
        public bool UsesConversion => usesConversion;
        public bool PrioritizesPowerUps => prioritizesPowerUps;
        public float FullSendMargin => fullSendMargin;
        public float DecisionNoise => decisionNoise;
        public float GracePeriod => gracePeriod;

        // «Nunca» en la tabla de 12.5 es no tener margen.
        public bool AllowsFullSend => fullSendMargin > 0f;
    }
}
