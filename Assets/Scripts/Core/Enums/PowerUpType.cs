namespace Nova.Core
{
    // Power-ups del cielo (9.2).
    public enum PowerUpType
    {
        CosmicRay = 0,     // Rayo Cósmico: +50 % velocidad de ataque
        HydrogenCloud = 1, // Nube de Hidrógeno: +50 % generación de Energía
    }

    // La estadística que acelera un power-up (9.2, 15.4). La consulta cada componente de nodo.
    public enum PowerUpStat
    {
        AttackSpeed = 0,      // Cadencia del Magnetar y velocidad de carga del Púlsar
        EnergyGeneration = 1, // Intervalo de la Estrella
    }
}
