using System;

namespace Nova.Core
{
    // Aritmética de la Energía: multiplicadores, redondeos y coste efectivo de captura
    // (5.2, 5.3, 6.2, 6.4). La Energía es entera y todo redondeo es hacia abajo.
    // Los números de balance no viven aquí: el multiplicador de resistencia del
    // Magnetar llega como parámetro desde BalanceConfig (16.1).
    public static class EnergyMath
    {
        // Multiplicador que un impacto hostil aplica a la Carga según el tipo del objetivo (6.2).
        // Solo el Magnetar resiste; entre nodos del mismo bando no se llama (5.3: suma 1:1).
        public static float HostileMultiplier(NodeType targetType, float magnetarResistance)
        {
            return targetType == NodeType.Magnetar ? magnetarResistance : 1f;
        }

        // Carga efectiva de un impacto hostil: floor(charge * multiplier), aplicado una sola vez
        // sobre la Carga total (6.2, notas obligatorias).
        public static int EffectiveCharge(int charge, float multiplier)
        {
            if (charge < 0)
                throw new ArgumentOutOfRangeException(nameof(charge), charge, "La Carga no puede ser negativa.");
            if (multiplier <= 0f)
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "El multiplicador debe ser positivo.");

            return (int)Math.Floor(charge * (double)multiplier);
        }

        // Carga mínima que captura un nodo con esa Energía: la menor c con
        // EffectiveCharge(c, multiplier) >= targetEnergy (6.4). Un protón lleva al menos
        // 1 de Carga, así que un nodo a 0 se captura con 1.
        public static int CaptureCost(int targetEnergy, float multiplier)
        {
            if (targetEnergy < 0)
                throw new ArgumentOutOfRangeException(nameof(targetEnergy), targetEnergy, "La Energía no puede ser negativa.");
            if (multiplier <= 0f)
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "El multiplicador debe ser positivo.");

            // Estimación por división y ajuste con el mismo redondeo que el impacto,
            // para que CaptureCost y EffectiveCharge nunca discrepen por coma flotante.
            int cost = Math.Max(1, (int)Math.Ceiling(targetEnergy / (double)multiplier));
            while (cost > 1 && EffectiveCharge(cost - 1, multiplier) >= targetEnergy)
                cost--;
            while (EffectiveCharge(cost, multiplier) < targetEnergy)
                cost++;
            return cost;
        }

        // Energía que sale del origen en un envío (5.2): el 50 % redondeado hacia abajo,
        // o el 100 % con doble clic. Un resultado de 0 significa que el envío no se ejecuta.
        public static int SendAmount(int sourceEnergy, bool fullSend)
        {
            if (sourceEnergy < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceEnergy), sourceEnergy, "La Energía no puede ser negativa.");

            return fullSend ? sourceEnergy : sourceEnergy / 2;
        }

        // Suma Energía respetando el Tope; el excedente se descarta (5.3).
        public static int AddCapped(int energy, int amount, int energyCap)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "La cantidad no puede ser negativa.");

            return Math.Min(energy + amount, energyCap);
        }
    }
}
