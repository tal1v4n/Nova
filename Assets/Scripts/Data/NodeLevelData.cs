using System;
using UnityEngine;

namespace Nova.Data
{
    // Estadísticas de un nivel de nodo (15.4). Cada tipo usa solo los campos que le tocan:
    // la Estrella el intervalo (4.2), el Magnetar el radio y la cadencia (4.4). El Púlsar
    // solo el coste: su nivel desbloquea poderes, que viven en PowerDefinition (4.3).
    [Serializable]
    public struct NodeLevelData
    {
        [Tooltip("Energía para subir a este nivel (16.2). En el nivel 1 vale 0: " +
                 "ese nivel se alcanza encendiendo, y su coste es BalanceConfig.IgnitionCost.")]
        [SerializeField, Min(0)] int upgradeCost;

        [Tooltip("Estrella: segundos por cada +1 de Energía (4.2). 0 en los demás tipos.")]
        [SerializeField, Min(0f)] float generationInterval;

        [Tooltip("Magnetar: radio de disparo en u (4.4). 0 en los demás tipos.")]
        [SerializeField, Min(0f)] float radius;

        [Tooltip("Magnetar: disparos por segundo (4.4). 0 en los demás tipos.")]
        [SerializeField, Min(0f)] float fireRate;

        public int UpgradeCost => upgradeCost;
        public float GenerationInterval => generationInterval;
        public float Radius => radius;
        public float FireRate => fireRate;
    }
}
