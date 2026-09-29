namespace Nova.Core
{
    // Estados del nodo (14.1). Entre paréntesis, el nombre que usa la especificación.
    // Son excluyentes salvo las combinaciones de la matriz de 14.6, que resuelve
    // NodeStateMachine; este enum solo los nombra.
    public enum NodeState
    {
        Sleeping = 0,   // (Dormido) Asteroide neutral
        Unignited = 1,  // (SinEncender) Asteroide del Jugador o del Enemigo
        Normal = 2,     // Nodo encendido sin ningún otro estado
        Scared = 3,     // (Asustado) Amenazado por un protón letal (14.2)
        Frozen = 4,     // (Congelado) Cero Absoluto (7.3)
        Infected = 5,   // (Infectado) Decaimiento (7.4)
        Converting = 6, // (Reconvirtiendo) Reconversión en curso, 3 s (8.1)
        Captured = 7,   // (Capturado) Transición de 0.4 s tras ChangeFaction()
    }
}
