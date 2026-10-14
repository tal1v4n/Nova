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

    // Victoria, aniquilación y estancamiento (10.1, 10.2). Función pura: los números de balance
    // llegan como parámetro desde BalanceConfig (16.1).
    public static class OutcomeRules
    {
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
    }
}
