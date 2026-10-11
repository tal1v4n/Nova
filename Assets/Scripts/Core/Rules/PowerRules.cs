using System;

namespace Nova.Core
{
    // Carga de poderes del Púlsar (7.1, 7.2). Validez de cargar y cancelar, y la aritmética entera
    // de la carga: cuánta Energía va invertida en cada momento y cuánta vuelve al cancelar.
    // Los costes y tiempos llegan de PowerDefinition y la devolución de BalanceConfig (16.1).
    public static class PowerRules
    {
        // Empezar a cargar: un Púlsar propio, con el nivel que el poder pide (7.2), sin otro poder
        // cargando o cargado (7.1), no congelado (7.3) y no reconvirtiéndose. La Energía no se
        // exige: la carga la va tomando y se pausa si se acaba (7.1). Infectado no lo impide:
        // funciona con normalidad (7.4).
        public static ActionCheck CheckCharge(NodeSnapshot node, Faction actor, int requiredLevel, int cost)
        {
            if (cost < 1)
                throw new ArgumentOutOfRangeException(nameof(cost), cost, "Un poder cuesta al menos 1.");

            if (!ConversionRules.IsOwner(node, actor))
                return new ActionCheck(ActionBlock.NotOwner, cost);
            if (node.Type != NodeType.Pulsar)
                return new ActionCheck(ActionBlock.NotPulsar, cost);
            if (node.Level < requiredLevel)
                return new ActionCheck(ActionBlock.LevelTooLow, cost);
            if (node.IsFrozen)
                return new ActionCheck(ActionBlock.Frozen, cost);
            if (node.IsConverting)
                return new ActionCheck(ActionBlock.Converting, cost);
            if (node.HasPowerCharge)
                return new ActionCheck(ActionBlock.PowerCharge, cost);

            return new ActionCheck(ActionBlock.None, cost);
        }

        // Cancelar (7.1): una carga en curso o un poder ya cargado (18 #19), del propio Púlsar.
        // Congelado tampoco cancela: el cuerpo queda inerte (7.3). El coste es 0: cancelar devuelve.
        public static ActionCheck CheckCancel(NodeSnapshot node, Faction actor)
        {
            if (!ConversionRules.IsOwner(node, actor))
                return new ActionCheck(ActionBlock.NotOwner, 0);
            if (!node.HasPowerCharge)
                return new ActionCheck(ActionBlock.NoPowerCharge, 0);
            if (node.IsFrozen)
                return new ActionCheck(ActionBlock.Frozen, 0);

            return new ActionCheck(ActionBlock.None, 0);
        }

        // 7.2.1: objetivo válido = cualquier nodo encendido que no sea del lanzador. Vale el
        // Magnetar neutral y ningún Asteroide, sea de quien sea. Es la misma regla para los tres
        // poderes, y la misma al lanzar y al terminar el haz (7.2.2).
        public static bool IsValidTarget(NodeSnapshot target, Faction launcher)
        {
            return target.Faction != launcher && target.Type != NodeType.Asteroid;
        }

        // Lanzar (7.2.2): el Púlsar propio con el poder cargado al 100 %, no congelado (7.3), sobre
        // un objetivo válido. El coste ya se pagó al cargar: lanzar no cuesta nada más.
        public static ActionCheck CheckLaunch(NodeSnapshot pulsar, Faction actor, bool powerReady, NodeSnapshot target)
        {
            if (!ConversionRules.IsOwner(pulsar, actor))
                return new ActionCheck(ActionBlock.NotOwner, 0);
            if (pulsar.Type != NodeType.Pulsar)
                return new ActionCheck(ActionBlock.NotPulsar, 0);
            if (!powerReady)
                return new ActionCheck(ActionBlock.NotReady, 0);
            if (pulsar.IsFrozen)
                return new ActionCheck(ActionBlock.Frozen, 0);
            if (!IsValidTarget(target, actor))
                return new ActionCheck(ActionBlock.InvalidTarget, 0);

            return new ActionCheck(ActionBlock.None, 0);
        }

        // Energía invertida tras progressMicros de carga sin pausa, de chargeMicros en total:
        // las dos curvas están acopladas (7.1), redondeando hacia abajo.
        public static int InvestedAt(int cost, long progressMicros, long chargeMicros)
        {
            RequireTimes(progressMicros, chargeMicros);
            if (progressMicros >= chargeMicros)
                return cost;
            return (int)(cost * progressMicros / chargeMicros);
        }

        // Primer instante de la carga en que ya van invested de Energía invertidos. Es donde se
        // queda parada una carga pausada (7.1), para seguir desde ahí al recibir Energía.
        public static long ProgressFor(int invested, int cost, long chargeMicros)
        {
            if (invested < 0 || invested > cost)
                throw new ArgumentOutOfRangeException(nameof(invested), invested, $"Entre 0 y {cost}.");
            RequireTimes(0, chargeMicros);
            return (invested * chargeMicros + cost - 1) / cost;
        }

        // Lo que vuelve al cancelar (7.1): la fracción de lo invertido, redondeada hacia abajo.
        // El Tope lo respeta quien la suma.
        public static int CancelRefund(int invested, float fraction)
        {
            if (invested < 0)
                throw new ArgumentOutOfRangeException(nameof(invested), invested, "No puede ser negativo.");
            if (fraction < 0f || fraction > 1f)
                throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "Entre 0 y 1.");
            return (int)Math.Floor(invested * (double)fraction);
        }

        static void RequireTimes(long progressMicros, long chargeMicros)
        {
            if (chargeMicros <= 0)
                throw new ArgumentOutOfRangeException(nameof(chargeMicros), chargeMicros, "La carga dura algo.");
            if (progressMicros < 0)
                throw new ArgumentOutOfRangeException(nameof(progressMicros), progressMicros, "El tiempo no retrocede.");
        }
    }
}
