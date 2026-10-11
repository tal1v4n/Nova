using System;

namespace Nova.Core
{
    // Por qué no se puede ejecutar una acción de nivel o de tipo. None = se puede.
    // InsufficientEnergy se comprueba la última: es el único motivo con el que la UI
    // enseña la opción deshabilitada con el coste en rojo (13.3, 13.7).
    public enum ActionBlock
    {
        None = 0,
        NotOwner,           // Solo el dueño actúa sobre su nodo; Neutral no actúa nunca
        NotAsteroid,        // Encender exige un Asteroide (4.1)
        IsAsteroid,         // Mejorar o reconvertir exige un nodo encendido
        InvalidTargetType,  // Nunca a Asteroide, y no se reconvierte al mismo tipo (8.1)
        MaxLevel,           // Ya está al nivel máximo de su tipo
        Frozen,             // 7.3
        Infected,           // 8.1
        PowerCharge,        // Púlsar cargando un poder o con uno cargado (8.1)
        Converting,         // Ya reconvirtiéndose (8.1)
        InsufficientEnergy,
        Unavailable,        // El nodo no ofrece ese tipo: aún no existe en esta fase (NodeIgnition, T2-02)
        NotPulsar,          // Solo un Púlsar carga poderes (4.3, T3-02)
        LevelTooLow,        // El nivel del Púlsar no desbloquea ese poder (7.2)
        NoPowerCharge,      // No hay carga que cancelar (7.1)
    }

    // Resultado de validar una acción: si se puede y cuánto cuesta.
    public readonly struct ActionCheck
    {
        public readonly ActionBlock Block;
        public readonly int Cost;

        public ActionCheck(ActionBlock block, int cost)
        {
            Block = block;
            Cost = cost;
        }

        public bool IsAllowed => Block == ActionBlock.None;
    }

    // Validez y coste de encendido, mejora y reconversión (4.1, 8, 16.2, 16.4).
    // Los costes llegan como parámetro desde BalanceConfig y NodeDefinition; aquí no hay
    // números de balance. Todo se paga con la Energía del propio nodo.
    public static class ConversionRules
    {
        // Encendido (4.1): Asteroide propio con Energía suficiente, a Estrella, Púlsar o Magnetar.
        // Resultado: nivel 1 del tipo elegido con node.Energy - cost, que con el Tope de 10 es 0.
        public static ActionCheck CheckIgnition(NodeSnapshot node, Faction actor, NodeType targetType, int ignitionCost)
        {
            RequireNonNegative(ignitionCost, nameof(ignitionCost));

            if (!IsOwner(node, actor))
                return new ActionCheck(ActionBlock.NotOwner, ignitionCost);
            if (node.Type != NodeType.Asteroid)
                return new ActionCheck(ActionBlock.NotAsteroid, ignitionCost);
            if (targetType == NodeType.Asteroid)
                return new ActionCheck(ActionBlock.InvalidTargetType, ignitionCost);

            return WithEnergy(node, ignitionCost);
        }

        // Mejora: sube un nivel dentro del mismo tipo. upgradeCost es el coste del nivel
        // siguiente (16.2). Solo la impiden Congelado (7.3) y Reconvirtiendo (8.1): un nodo
        // infectado funciona con normalidad (7.4) y un Púlsar cargando puede mejorar.
        public static ActionCheck CheckUpgrade(NodeSnapshot node, Faction actor, int maxLevel, int upgradeCost)
        {
            RequireNonNegative(upgradeCost, nameof(upgradeCost));

            if (!IsOwner(node, actor))
                return new ActionCheck(ActionBlock.NotOwner, upgradeCost);
            if (node.Type == NodeType.Asteroid)
                return new ActionCheck(ActionBlock.IsAsteroid, upgradeCost);
            if (node.Level >= maxLevel)
                return new ActionCheck(ActionBlock.MaxLevel, upgradeCost);
            if (node.IsFrozen)
                return new ActionCheck(ActionBlock.Frozen, upgradeCost);
            if (node.IsConverting)
                return new ActionCheck(ActionBlock.Converting, upgradeCost);

            return WithEnergy(node, upgradeCost);
        }

        // Reconversión (8.1): entre Estrella, Púlsar y Magnetar, nunca al mismo tipo.
        // conversionCost depende del tipo de origen (16.4) y se paga al iniciar. Lo aplica
        // NodeConverter (T2-07).
        public static ActionCheck CheckConversion(NodeSnapshot node, Faction actor, NodeType targetType, int conversionCost)
        {
            RequireNonNegative(conversionCost, nameof(conversionCost));

            if (!IsOwner(node, actor))
                return new ActionCheck(ActionBlock.NotOwner, conversionCost);
            if (node.Type == NodeType.Asteroid)
                return new ActionCheck(ActionBlock.IsAsteroid, conversionCost);
            if (targetType == NodeType.Asteroid || targetType == node.Type)
                return new ActionCheck(ActionBlock.InvalidTargetType, conversionCost);
            if (node.IsFrozen)
                return new ActionCheck(ActionBlock.Frozen, conversionCost);
            if (node.IsInfected)
                return new ActionCheck(ActionBlock.Infected, conversionCost);
            if (node.HasPowerCharge)
                return new ActionCheck(ActionBlock.PowerCharge, conversionCost);
            if (node.IsConverting)
                return new ActionCheck(ActionBlock.Converting, conversionCost);

            return WithEnergy(node, conversionCost);
        }

        // Al terminar la reconversión: la Energía que tenga en ese momento, recortada al
        // Tope del tipo nuevo (8.1). El nivel pasa a 1.
        public static int EnergyAfterConversion(int energyAtEnd, int newTypeEnergyCap)
        {
            RequireNonNegative(energyAtEnd, nameof(energyAtEnd));
            return Math.Min(energyAtEnd, newTypeEnergyCap);
        }

        internal static bool IsOwner(NodeSnapshot node, Faction actor) =>
            actor != Faction.Neutral && node.Faction == actor;

        static ActionCheck WithEnergy(NodeSnapshot node, int cost) =>
            new ActionCheck(node.Energy >= cost ? ActionBlock.None : ActionBlock.InsufficientEnergy, cost);

        static void RequireNonNegative(int value, string name)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(name, value, "No puede ser negativo.");
        }
    }
}
