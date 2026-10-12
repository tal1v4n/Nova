using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // Pesos de las funciones de puntuación del algoritmo del Enemigo (12.3, 12.3.1, 12.3.2, T4-02).
    // Una sola instancia: Assets/Data/EnemyScoring.asset. Son los mismos para los tres mapas: lo que
    // cambia por mapa está en EnemyProfile (12.5). Viven aquí y no en el código porque todo número
    // de balance va en un ScriptableObject (T.1).
    [CreateAssetMenu(menuName = "Nova/Enemy Scoring", fileName = "EnemyScoring")]
    public sealed class EnemyScoring : ScriptableObject
    {
        // Afinidad de un poder contra cada tipo encendido (12.3.1).
        [Serializable]
        public struct Affinity
        {
            public float star;
            public float pulsar;
            public float magnetar;

            public float Against(NodeType type)
            {
                switch (type)
                {
                    case NodeType.Star: return star;
                    case NodeType.Pulsar: return pulsar;
                    case NodeType.Magnetar: return magnetar;
                    default: return 0f;
                }
            }
        }

        [Header("ValorTipo (12.3, 12.3.1)")]
        [SerializeField] float starValue = 100f;
        [SerializeField] float pulsarValue = 80f;
        [SerializeField] float magnetarValue = 70f;
        [SerializeField] float asteroidValue = 40f;

        [Header("Atacar (12.3)")]
        [Tooltip("BonusPowerUp: Asteroide marcado con power-up.")]
        [SerializeField] float powerUpBonus = 60f;

        [Tooltip("RiesgoDeRuta: por cada Magnetar hostil cuyo radio corta la trayectoria.")]
        [SerializeField] float routeRiskPerMagnetar = 6f;

        [Tooltip("PenalizacionVaciado.")]
        [SerializeField] float drainPenalty = 30f;

        [Tooltip("Fracción del Tope por debajo de la cual el origen queda vaciado.")]
        [SerializeField, Range(0f, 1f)] float drainThreshold = 0.3f;

        [Header("Lanzar poder (12.3.1)")]
        [SerializeField] Affinity absoluteZeroAffinity = new Affinity { star = 20f, pulsar = 40f, magnetar = 30f };
        [SerializeField] Affinity decayAffinity = new Affinity { star = 40f, pulsar = 10f, magnetar = 20f };
        [SerializeField] Affinity wormholeAffinity = new Affinity { star = 30f, pulsar = 20f, magnetar = 50f };

        [Tooltip("BonusRobo: Agujero de Gusano sobre un Púlsar con el poder cargado al 100 %.")]
        [SerializeField] float stealBonus = 60f;

        [Tooltip("EnergiaRelativa: (Energía / Tope) por este peso.")]
        [SerializeField] float relativeEnergyWeight = 40f;

        [Tooltip("AjusteNeutral: objetivo Neutral.")]
        [SerializeField] float neutralAdjustment = 25f;

        [Tooltip("Redundancia: el objetivo ya sufre el estado de ese poder.")]
        [SerializeField] float redundancyPenalty = 100f;

        [Header("Encender (12.3.2)")]
        [SerializeField] float ignitionBase = 50f;

        [Tooltip("Necesidad de Estrella: este valor / (1 + Estrellas propias).")]
        [SerializeField] float starNeed = 40f;

        [Tooltip("Necesidad de Púlsar sin ningún Púlsar propio.")]
        [SerializeField] float pulsarNeed = 30f;

        [Tooltip("Necesidad de Magnetar en el nodo propio más cercano a uno del Jugador.")]
        [SerializeField] float magnetarNeed = 30f;

        [Header("Mejorar (12.3.2)")]
        [SerializeField] float starUpgradeBase = 60f;
        [SerializeField] float pulsarUpgradeBase = 50f;
        [SerializeField] float magnetarUpgradeBase = 40f;

        [Tooltip("Fracción del coste de mejora que se resta.")]
        [SerializeField, Min(0f)] float upgradeCostFactor = 0.5f;

        [Header("Cargar poder (12.3.2)")]
        [Tooltip("Fracción del mejor ScorePoder que vale cargar.")]
        [SerializeField, Min(0f)] float chargeValueFactor = 0.5f;

        [Tooltip("Fracción del coste del poder que se resta.")]
        [SerializeField, Min(0f)] float chargeCostFactor = 0.5f;

        [Header("Reconvertir (12.3.2)")]
        [SerializeField] float conversionBase = 80f;

        public float TypeValue(NodeType type)
        {
            switch (type)
            {
                case NodeType.Star: return starValue;
                case NodeType.Pulsar: return pulsarValue;
                case NodeType.Magnetar: return magnetarValue;
                case NodeType.Asteroid: return asteroidValue;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public float AffinityOf(PowerType power, NodeType target)
        {
            switch (power)
            {
                case PowerType.AbsoluteZero: return absoluteZeroAffinity.Against(target);
                case PowerType.Decay: return decayAffinity.Against(target);
                case PowerType.Wormhole: return wormholeAffinity.Against(target);
                default: throw new ArgumentOutOfRangeException(nameof(power), power, null);
            }
        }

        public float UpgradeBase(NodeType type)
        {
            switch (type)
            {
                case NodeType.Star: return starUpgradeBase;
                case NodeType.Pulsar: return pulsarUpgradeBase;
                case NodeType.Magnetar: return magnetarUpgradeBase;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "El Asteroide no se mejora.");
            }
        }

        public float PowerUpBonus => powerUpBonus;
        public float RouteRiskPerMagnetar => routeRiskPerMagnetar;
        public float DrainPenalty => drainPenalty;
        public float DrainThreshold => drainThreshold;
        public float StealBonus => stealBonus;
        public float RelativeEnergyWeight => relativeEnergyWeight;
        public float NeutralAdjustment => neutralAdjustment;
        public float RedundancyPenalty => redundancyPenalty;
        public float IgnitionBase => ignitionBase;
        public float StarNeed => starNeed;
        public float PulsarNeed => pulsarNeed;
        public float MagnetarNeed => magnetarNeed;
        public float UpgradeCostFactor => upgradeCostFactor;
        public float ChargeValueFactor => chargeValueFactor;
        public float ChargeCostFactor => chargeCostFactor;
        public float ConversionBase => conversionBase;
    }
}
