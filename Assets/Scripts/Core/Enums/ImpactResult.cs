namespace Nova.Core
{
    // Resultado de resolver el impacto de un protón que llega a un nodo (15.3). Lo devuelve
    // ImpactResolver. Un protón derribado por un Magnetar no llega y no tiene resultado aquí:
    // lo avisa Proton.OnShotDown (6.3, T.7 #40).
    public enum ImpactResult
    {
        Absorbed = 0,          // Mismo bando: la Carga se suma a la Energía, hasta el Tope (6.2)
        Damaged = 1,           // Bando distinto: la Carga efectiva no alcanza la Energía del nodo (6.2)
        Captured = 2,          // Bando distinto: la alcanza y el nodo cambia de facción (6.2)
        ThawProgress = 3,      // Nodo congelado: pierde Energía y sigue congelado (7.3)
        ThawedAndCaptured = 4, // Nodo congelado: llega a 0, se descongela y cambia de facción (7.3)
    }
}
