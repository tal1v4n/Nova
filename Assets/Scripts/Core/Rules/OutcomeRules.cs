using System;
using System.Collections.Generic;

namespace Nova.Core
{
    // Cómo termina un nivel (10).
    public enum LevelOutcome
    {
        None = 0,         // Sigue la partida
        Victory = 1,      // El Enemigo no posee ningún nodo (10.1)
        Annihilation = 2, // El Jugador no posee ningún nodo (10.2, 1)
        Stalemate = 3,    // El Jugador no puede volver a tener una Estrella (10.2, 2)
    }

    // Un nodo tal como lo lee OutcomeRules: su estado y lo que su dueño podría hacer con él para
    // recuperar una Estrella según las mecánicas del nivel (11.1). Core no conoce componentes: los
    // costes los pone quien construye la lista (WinConditionChecker).
    public readonly struct OutcomeNode
    {
        public readonly NodeSnapshot State;

        // Coste de reconvertirlo en Estrella (16.4), o -1 si el nivel no lo permite.
        public readonly int StarConversionCost;

        // Coste del Agujero de Gusano (16.3) si es un Púlsar que puede cargarlo —nivel suficiente y
        // poder permitido en el nivel—, o -1.
        public readonly int WormholeCost;

        public OutcomeNode(NodeSnapshot state, int starConversionCost = -1, int wormholeCost = -1)
        {
            State = state;
            StarConversionCost = starConversionCost;
            WormholeCost = wormholeCost;
        }
    }

    // Las cinco vías de 10.2 para volver a tener una Estrella, en el orden de su tabla.
    public enum StarRouteKind
    {
        IgniteOwnAsteroid = 0, // Encender un Asteroide propio
        CaptureAsteroid = 1,   // Capturar un Asteroide y encenderlo
        CaptureStar = 2,       // Capturar una Estrella
        Reconvert = 3,         // Reconvertir un nodo propio en Estrella
        StealStar = 4,         // Robar una Estrella con el Agujero de Gusano
    }

    // Por qué una vía no alcanza (13.11).
    public enum StarRouteBlock
    {
        None = 0,      // Alcanza: el Jugador todavía puede volver a tener una Estrella
        Short = 1,     // Cuesta más de lo que tiene: «Cuesta 20 · faltan 6»
        NoneOwned = 2, // No tiene el nodo que la vía necesita: «No tienes ninguno»
        NoneLeft = 3,  // No queda en el tablero el objetivo que la vía necesita
        NotInMap = 4,  // El nivel no permite la mecánica (11.1): «No existe en el mapa 2»
    }

    // Una vía de 10.2 con su cuenta: lo que cuesta la más barata y lo que el Jugador tiene para ella.
    public readonly struct StarRoute
    {
        public readonly StarRouteKind Kind;
        public readonly StarRouteBlock Block;
        public readonly int Cost;
        public readonly int Have;

        public StarRoute(StarRouteKind kind, StarRouteBlock block, int cost = 0, int have = 0)
        {
            Kind = kind;
            Block = block;
            Cost = cost;
            Have = have;
        }

        public bool Reachable => Block == StarRouteBlock.None;
        public int Missing => Math.Max(0, Cost - Have);
    }

    // Victoria, aniquilación y estancamiento (10.1, 10.2). Función pura: los números de balance
    // llegan como parámetro desde BalanceConfig (16.1).
    public static class OutcomeRules
    {
        public const int RouteCount = 5;
        // Victoria antes que derrota: con las reglas de 6 no hay forma de que los dos bandos se queden
        // sin nodos a la vez, porque un nodo solo cambia de bando hacia quien le dispara.
        public static LevelOutcome Evaluate(IReadOnlyList<OutcomeNode> nodes, bool playerProtonsInFlight,
                                            int ignitionCost, float magnetarResistance)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));

            bool player = false;
            bool enemy = false;
            foreach (OutcomeNode node in nodes)
            {
                player |= node.State.Faction == Faction.Player;
                enemy |= node.State.Faction == Faction.Enemy;
            }

            if (!enemy)
                return LevelOutcome.Victory;
            if (!player)
                return LevelOutcome.Annihilation;
            return IsStalemate(nodes, playerProtonsInFlight, ignitionCost, magnetarResistance)
                ? LevelOutcome.Stalemate
                : LevelOutcome.None;
        }

        // 10.2, 2. Sin Estrella y sin ninguna vía para volver a tener una. No se comprueba con
        // protones propios en vuelo, un poder cargando o cargado, o un nodo reconvirtiéndose.
        public static bool IsStalemate(IReadOnlyList<OutcomeNode> nodes, bool playerProtonsInFlight,
                                       int ignitionCost, float magnetarResistance)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));
            if (playerProtonsInFlight)
                return false;

            foreach (OutcomeNode node in nodes)
            {
                NodeSnapshot state = node.State;
                if (state.Faction != Faction.Player)
                    continue;
                if (state.Type == NodeType.Star || state.HasPowerCharge || state.IsConverting)
                    return false;
            }
            return !CanRecoverStar(nodes, ignitionCost, magnetarResistance);
        }

        // Las cinco vías de 10.2, con cuentas optimistas: S es la Energía de todos los nodos
        // encendidos del Jugador, como si llegara entera en un solo envío.
        public static bool CanRecoverStar(IReadOnlyList<OutcomeNode> nodes, int ignitionCost, float magnetarResistance)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));

            int lit = 0;
            foreach (OutcomeNode node in nodes)
            {
                if (node.State.Faction == Faction.Player && node.State.Type != NodeType.Asteroid)
                    lit += node.State.Energy;
            }

            bool enemyStar = false;
            foreach (OutcomeNode node in nodes)
            {
                NodeSnapshot state = node.State;
                if (state.Faction == Faction.Player)
                {
                    // Encender un Asteroide propio.
                    if (state.Type == NodeType.Asteroid && state.Energy + lit >= ignitionCost)
                        return true;
                    // Reconvertir.
                    if (state.Type != NodeType.Asteroid && node.StarConversionCost >= 0 && state.Energy >= node.StarConversionCost)
                        return true;
                    continue;
                }

                int capture = EnergyMath.CaptureCost(state.Energy, EnergyMath.HostileMultiplier(state.Type, magnetarResistance));
                // Capturar un Asteroide y encenderlo.
                if (state.Type == NodeType.Asteroid && lit >= capture + ignitionCost)
                    return true;
                if (state.Type == NodeType.Star)
                {
                    // Capturar una Estrella.
                    if (lit >= capture)
                        return true;
                    enemyStar |= state.Faction == Faction.Enemy;
                }
            }

            // Robar una Estrella del Enemigo con el Agujero de Gusano.
            if (enemyStar)
            {
                foreach (OutcomeNode node in nodes)
                {
                    if (node.State.Faction == Faction.Player && node.WormholeCost >= 0 && lit >= node.WormholeCost)
                        return true;
                }
            }
            return false;
        }

        // Las mismas cinco cuentas de CanRecoverStar, una por vía y con su motivo, para enseñarlas en
        // la derrota por estancamiento (13.11, 18 #41) en vez de tirarlas. Con la mecánica permitida,
        // una vía es alcanzable si y solo si CanRecoverStar la acepta. conversionInLevel y
        // wormholeInLevel son las mecánicas del nivel (11.1): sin ellas, la vía no existe en el mapa.
        public static StarRoute[] StarRoutes(IReadOnlyList<OutcomeNode> nodes, int ignitionCost, float magnetarResistance,
                                             bool conversionInLevel, bool wormholeInLevel)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));

            int lit = 0;
            foreach (OutcomeNode node in nodes)
            {
                if (node.State.Faction == Faction.Player && node.State.Type != NodeType.Asteroid)
                    lit += node.State.Energy;
            }

            int ownAsteroid = -1;           // Energía del Asteroide propio más cargado
            int asteroidCapture = -1;       // Captura del Asteroide no propio más barato
            int starCapture = -1;           // Captura de la Estrella no propia más barata
            int conversionCost = 0;         // La reconversión a la que menos le falta
            int conversionHave = 0;
            bool conversionOwned = false;
            int wormholeCost = -1;          // El Agujero de Gusano más barato de sus Púlsares
            bool enemyStar = false;

            foreach (OutcomeNode node in nodes)
            {
                NodeSnapshot state = node.State;
                if (state.Faction == Faction.Player)
                {
                    if (state.Type == NodeType.Asteroid)
                    {
                        ownAsteroid = Math.Max(ownAsteroid, state.Energy);
                        continue;
                    }
                    if (node.StarConversionCost >= 0
                        && (!conversionOwned || node.StarConversionCost - state.Energy < conversionCost - conversionHave))
                    {
                        conversionOwned = true;
                        conversionCost = node.StarConversionCost;
                        conversionHave = state.Energy;
                    }
                    if (node.WormholeCost >= 0 && (wormholeCost < 0 || node.WormholeCost < wormholeCost))
                        wormholeCost = node.WormholeCost;
                    continue;
                }

                int capture = EnergyMath.CaptureCost(state.Energy, EnergyMath.HostileMultiplier(state.Type, magnetarResistance));
                if (state.Type == NodeType.Asteroid && (asteroidCapture < 0 || capture < asteroidCapture))
                    asteroidCapture = capture;
                if (state.Type == NodeType.Star)
                {
                    if (starCapture < 0 || capture < starCapture)
                        starCapture = capture;
                    enemyStar |= state.Faction == Faction.Enemy;
                }
            }

            var routes = new StarRoute[RouteCount];
            routes[0] = ownAsteroid < 0
                ? new StarRoute(StarRouteKind.IgniteOwnAsteroid, StarRouteBlock.NoneOwned)
                : Counted(StarRouteKind.IgniteOwnAsteroid, ignitionCost, ownAsteroid + lit);
            routes[1] = asteroidCapture < 0
                ? new StarRoute(StarRouteKind.CaptureAsteroid, StarRouteBlock.NoneLeft)
                : Counted(StarRouteKind.CaptureAsteroid, asteroidCapture + ignitionCost, lit);
            routes[2] = starCapture < 0
                ? new StarRoute(StarRouteKind.CaptureStar, StarRouteBlock.NoneLeft)
                : Counted(StarRouteKind.CaptureStar, starCapture, lit);
            routes[3] = !conversionInLevel
                ? new StarRoute(StarRouteKind.Reconvert, StarRouteBlock.NotInMap)
                : !conversionOwned
                    ? new StarRoute(StarRouteKind.Reconvert, StarRouteBlock.NoneOwned)
                    : Counted(StarRouteKind.Reconvert, conversionCost, conversionHave);
            routes[4] = !wormholeInLevel
                ? new StarRoute(StarRouteKind.StealStar, StarRouteBlock.NotInMap)
                : wormholeCost < 0
                    ? new StarRoute(StarRouteKind.StealStar, StarRouteBlock.NoneOwned)
                    : !enemyStar
                        ? new StarRoute(StarRouteKind.StealStar, StarRouteBlock.NoneLeft)
                        : Counted(StarRouteKind.StealStar, wormholeCost, lit);
            return routes;
        }

        static StarRoute Counted(StarRouteKind kind, int cost, int have) =>
            new StarRoute(kind, have >= cost ? StarRouteBlock.None : StarRouteBlock.Short, cost, have);
    }
}
