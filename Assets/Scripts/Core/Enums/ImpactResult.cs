namespace Nova.Core
{
    // Resultado de resolver el impacto de un protón (15.3). Lo devuelve ImpactResolver.
    public enum ImpactResult
    {
        Absorbed = 0,          // Mismo bando: la Carga se suma a la Energía, hasta el Tope (6.2)
        Damaged = 1,           // Bando distinto: la Carga efectiva no alcanza la Energía del nodo (6.2)
        Captured = 2,          // Bando distinto: la alcanza y el nodo cambia de facción (6.2)
        ThawProgress = 3,      // Nodo congelado: pierde Energía y sigue congelado (7.3)
        ThawedAndCaptured = 4, // Nodo congelado: llega a 0, se descongela y cambia de facción (7.3)
        // Listado en 15.3, pero ningún pseudocódigo de 6.2 ni de 7.3 lo devuelve y
        // los nodos nunca se destruyen (17, caso 4). Pendiente de definir: ver T.7.
        Destroyed = 5,
    }
}
