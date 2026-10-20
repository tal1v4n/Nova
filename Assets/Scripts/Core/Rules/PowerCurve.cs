using System;
using System.Collections.Generic;

namespace Nova.Core
{
    // La curva de poder de las pantallas de fin (13.11, 18 #40): la proporción del Jugador sobre la
    // suma de las dos barras de 13.6, una muestra por segundo de juego, dibujada en columnas de
    // ancho fijo. Función pura: las muestras las guarda MatchRecorder.
    public static class PowerCurve
    {
        public const int Columns = 64;

        // Proporción del Jugador. Con las dos barras a 0 no hay nadie delante: la mitad.
        public static float Share(int playerEnergy, int enemyEnergy)
        {
            if (playerEnergy < 0 || enemyEnergy < 0)
                throw new ArgumentOutOfRangeException(nameof(playerEnergy), "La Energía de una barra no es negativa.");
            int total = playerEnergy + enemyEnergy;
            return total == 0 ? 0.5f : (float)playerEnergy / total;
        }

        // Agrupa las muestras en columns columnas: cada columna es la media de su tramo. Con menos
        // muestras que columnas, una muestra ocupa varias columnas seguidas. Sin muestras, nada.
        public static float[] Group(IReadOnlyList<float> samples, int columns = Columns)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            if (columns <= 0)
                throw new ArgumentOutOfRangeException(nameof(columns), columns, "Hace falta al menos una columna.");
            if (samples.Count == 0)
                return Array.Empty<float>();

            var result = new float[columns];
            for (int c = 0; c < columns; c++)
            {
                Range(c, samples.Count, columns, out int start, out int end);
                float sum = 0f;
                for (int i = start; i < end; i++)
                    sum += samples[i];
                result[c] = sum / (end - start);
            }
            return result;
        }

        // La columna en la que cae una muestra: la primera cuyo tramo la contiene.
        public static int ColumnOf(int sample, int sampleCount, int columns = Columns)
        {
            if (sample < 0 || sample >= sampleCount)
                throw new ArgumentOutOfRangeException(nameof(sample), sample, "La muestra no existe.");
            for (int c = 0; c < columns; c++)
            {
                Range(c, sampleCount, columns, out int start, out int end);
                if (sample >= start && sample < end)
                    return c;
            }
            return columns - 1;
        }

        // Victoria: la muestra desde la que el Jugador tiene más de la mitad y se queda así hasta el
        // final, o -1 si la última no pasa de la mitad.
        public static int HoldIndex(IReadOnlyList<float> samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            int index = -1;
            for (int i = samples.Count - 1; i >= 0 && samples[i] > 0.5f; i--)
                index = i;
            return index;
        }

        // Derrota: el mejor momento del Jugador, su proporción más alta; con empate, el primero. -1 sin muestras.
        public static int BestIndex(IReadOnlyList<float> samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            int best = -1;
            for (int i = 0; i < samples.Count; i++)
            {
                if (best < 0 || samples[i] > samples[best])
                    best = i;
            }
            return best;
        }

        // Tramo [start, end) de la columna c: nunca vacío.
        static void Range(int c, int count, int columns, out int start, out int end)
        {
            start = (int)((long)c * count / columns);
            end = (int)((long)(c + 1) * count / columns);
            if (end <= start)
                end = start + 1;
        }
    }
}
