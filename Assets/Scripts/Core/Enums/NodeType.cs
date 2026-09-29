namespace Nova.Core
{
    // Tipo de nodo (glosario, sección 2; NOVA_Nodos.md).
    public enum NodeType
    {
        Asteroid = 0, // Asteroide: siempre nivel 0 (4.1)
        Star = 1,     // Estrella (4.2)
        Pulsar = 2,   // Púlsar (4.3)
        Magnetar = 3, // Magnetar (4.4)
    }
}
