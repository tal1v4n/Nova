using Nova.Core;
using Nova.Systems;

namespace Nova.Gameplay
{
    // Canal único de comandos (3.1, 15.1 #5, 12.7). Jugador y Enemigo actúan solo por aquí,
    // así que el algoritmo no puede hacer nada que el Jugador no tenga.
    //
    // Crece con las tareas que traen cada acción de 12.2: T1-11 abrió el envío, T1-11b el
    // encendido, T2-03 la mejora, T2-07 la reconversión y T3-02/T3-03 la carga y el lanzamiento
    // de poderes. Un método sin tarea que lo implemente sería una regla sin código detrás.
    public interface INodeCommandIssuer
    {
        // Facción en nombre de la que se emiten los comandos.
        Faction Faction { get; }

        // Envía el 50 % o, con fullSend, el 100 % de la Energía de source hacia target (5.2).
        LaunchBlock Send(NodeController source, NodeController target, bool fullSend);

        // Enciende un Asteroide propio como targetType, nivel 1 (4.1). T1-11b.
        ActionCheck Ignite(NodeController node, NodeType targetType);

        // Sube un nivel un nodo propio encendido, pagando con su Energía (16.2). T2-03.
        ActionCheck Upgrade(NodeController node);

        // Inicia la reconversión de un nodo propio encendido a targetType: paga al iniciar y
        // cambia de tipo a los 3 s (8.1). T2-07.
        ActionCheck Convert(NodeController node, NodeType targetType);

        // Empieza a cargar un poder en un Púlsar propio; la Energía se va invirtiendo sola (7.1). T3-02.
        ActionCheck ChargePower(NodeController node, PowerType power);

        // Cancela la carga o el poder ya cargado de un Púlsar propio y devuelve la mitad de lo
        // invertido (7.1). T3-02.
        ActionCheck CancelCharge(NodeController node);
    }
}
