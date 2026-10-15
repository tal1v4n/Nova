using System;
using UnityEngine;

namespace Nova.Data
{
    // Calendario de drops de un nivel (9.3, 15.4): la config de drops de LevelDefinition. Los tres
    // niveles de un mapa comparten fila. Con MaxDrops = 0 no cae nada.
    [Serializable]
    public struct DropSchedule
    {
        [Tooltip("Segundos desde el inicio del nivel hasta el primer drop.")]
        [SerializeField, Min(0f)] float firstDrop;

        [Tooltip("Intervalo mínimo entre drops, en segundos. Se cuenta desde que el anterior se recoge o se apaga.")]
        [SerializeField, Min(0f)] float minInterval;

        [Tooltip("Intervalo máximo entre drops, en segundos. El de cada drop sale al azar entre los dos.")]
        [SerializeField, Min(0f)] float maxInterval;

        [Tooltip("Drops por nivel. Uno saltado por falta de Asteroides neutrales no cuenta (17, caso 16).")]
        [SerializeField, Min(0)] int maxDrops;

        public DropSchedule(float first, float min, float max, int count)
        {
            if (first < 0f || min < 0f || max < min)
                throw new ArgumentException($"Calendario de drops imposible: primero {first}, intervalo {min}–{max}.");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "No hay un número negativo de drops.");
            firstDrop = first;
            minInterval = min;
            maxInterval = max;
            maxDrops = count;
        }

        public float FirstDrop => firstDrop;
        public float MinInterval => minInterval;
        public float MaxInterval => maxInterval;
        public int MaxDrops => maxDrops;
    }
}
