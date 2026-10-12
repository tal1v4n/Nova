using System;

namespace Nova.Core
{
    // Estados del nodo (14.1), en dos piezas (T.7 #43, cerrado el 2026-10-10): un estado de base
    // excluyente, que se deduce del tipo y la facción, más banderas para los que pueden coexistir
    // según la matriz de 14.6. Entre paréntesis, el nombre que usa la especificación.
    //
    // 14.1 define Normal como «ausencia de los demás estados»: aquí es el estado de base de todo
    // nodo encendido, y un nodo está Normal en el sentido de 14.1 cuando además no lleva banderas.
    public enum NodeState
    {
        Sleeping = 0,   // (Dormido) Asteroide neutral
        Unignited = 1,  // (SinEncender) Asteroide del Jugador o del Enemigo
        Normal = 2,     // Nodo encendido
    }

    // Estados que se suman al de base. Las combinaciones que 14.6 prohíbe las rechaza
    // NodeStateMachine; este enum solo las nombra.
    [Flags]
    public enum NodeStateFlags
    {
        None = 0,
        Scared = 1 << 0,     // (Asustado) Amenazado por un protón letal (14.2)
        Frozen = 1 << 1,     // (Congelado) Cero Absoluto (7.3)
        Infected = 1 << 2,   // (Infectado) Decaimiento (7.4)
        Converting = 1 << 3, // (Reconvirtiendo) Reconversión en curso, 3 s (8.1)
        Captured = 1 << 4,   // (Capturado) Transición de 0.4 s tras ChangeFaction()
    }
}
