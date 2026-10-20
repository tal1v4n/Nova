using Nova.Core;

namespace Nova.Enemy
{
    // Acciones candidatas de 12.2, en el orden de su tabla: es el primer criterio de desempate (12.3.3).
    public enum EnemyActionKind
    {
        Attack = 0,
        Ignite = 1,
        Upgrade = 2,
        Reinforce = 3,   // Solo como respuesta de 12.4 (T4-05): nunca es candidata
        ChargePower = 4,
        LaunchPower = 5,
        Convert = 6,
        Evacuate = 7,    // Solo como respuesta de 12.4 (T4-05): todo al nodo propio no amenazado más cercano
        Supply = 8,      // Abastecer (12.3.4): el 50 % a un nodo propio que lo necesita para encenderse o cargar
    }

    // Una candidata puntuada (12.3, 12.3.1, 12.3.2). Actor y Target son índices de NodeRegistry
    // (BoardNode.Index); Target es -1 si la acción no tiene objetivo. NodeType es el tipo al que se
    // enciende o se reconvierte, y Power el poder que se carga o se lanza.
    public readonly struct EnemyAction
    {
        public readonly EnemyActionKind Kind;
        public readonly int Actor;
        public readonly int Target;
        public readonly NodeType NodeType;
        public readonly PowerType Power;
        public readonly bool FullSend;
        public readonly float Score;

        public EnemyAction(EnemyActionKind kind, int actor, int target, float score,
                           NodeType nodeType = NodeType.Asteroid, PowerType power = PowerType.AbsoluteZero, bool fullSend = false)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            NodeType = nodeType;
            Power = power;
            FullSend = fullSend;
            Score = score;
        }

        public EnemyAction WithScore(float score) => new EnemyAction(Kind, Actor, Target, score, NodeType, Power, FullSend);

        public override string ToString() => $"{Kind} {Actor}->{Target} {NodeType}/{Power}{(FullSend ? " 100%" : "")} = {Score:0.##}";
    }
}
