using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Enemy
{
    // Funciones de puntuación del algoritmo del Enemigo (12.3, 12.3.1, 12.3.2, 15.7, T4-02). Estática
    // y sin estado (12.7): misma entrada, misma salida. El ruido lo pone Pick con un System.Random
    // que llega de fuera, con semilla.
    //
    // Genera las candidatas de 12.2 —todas menos reforzar, que solo existe en la defensa de 12.4— y
    // las puntúa en una sola escala para que compitan contra UmbralDeAccion. Las reglas de juego no se
    // repiten aquí: lo que es legal lo dicen ConversionRules, PowerRules y EnergyMath.
    //
    // Sin distancia en los poderes (12.7): ScorePoder y ScoreCarga no leen ninguna posición.
    public static class EnemyEvaluator
    {
        // --- Atacar (12.3) ---

        // Cuánto se envía y qué parte del valor del objetivo cuenta (12.3, «Cuánto se envía»).
        public readonly struct SendPlan
        {
            public readonly int Charge;
            public readonly bool FullSend;
            public readonly float Reach; // Alcance: 1 si captura, Carga / CosteEfectivo si es de desgaste

            public SendPlan(int charge, bool fullSend, float reach)
            {
                Charge = charge;
                FullSend = fullSend;
                Reach = reach;
            }
        }

        // Carga que captura el destino tal como está (6.2, 6.4, 7.3): con la resistencia ×2 si es
        // un Magnetar. Congelado, el multiplicador también aplica.
        public static int EffectiveCost(EnemyBoard board, BoardNode target)
        {
            float multiplier = EnergyMath.HostileMultiplier(target.Type, board.MagnetarResistance);
            return EnergyMath.CaptureCost(target.Energy, multiplier);
        }

        public static SendPlan PlanSend(int sourceEnergy, int effectiveCost, EnemyProfile profile)
        {
            int half = EnergyMath.SendAmount(sourceEnergy, false);
            if (half >= effectiveCost)
                return new SendPlan(half, false, 1f);
            if (profile.AllowsFullSend && sourceEnergy >= effectiveCost * profile.FullSendMargin)
                return new SendPlan(sourceEnergy, true, 1f);
            return new SendPlan(half, false, effectiveCost > 0 ? Mathf.Clamp01((float)half / effectiveCost) : 1f);
        }

        // RiesgoDeRuta: Magnetares hostiles —todo el que no es del que decide, el neutral incluido—
        // cuyo radio corta el segmento origen→destino. Uno congelado o reconvirtiéndose no dispara
        // (7.3, 8.1) y no cuenta (desviación 216).
        public static int HostileMagnetarsOnRoute(EnemyBoard board, BoardNode source, BoardNode target)
        {
            int count = 0;
            foreach (BoardNode node in board.Nodes)
            {
                if (node.Type != NodeType.Magnetar || node.Faction == board.Self || node == source)
                    continue;
                if (node.State.IsFrozen || node.State.IsConverting)
                    continue;
                if (DistanceToSegment(node.Position, source.Position, target.Position) <= node.MagnetarRadius)
                    count++;
            }
            return count;
        }

        // PenalizacionVaciado: queda por debajo del umbral del Tope y algún nodo del rival podría
        // capturarlo tal como queda con un solo envío del 50 % (12.3, «lo expone»).
        public static bool IsExposed(EnemyBoard board, BoardNode node, int remainingEnergy, EnemyScoring scoring)
        {
            if (remainingEnergy >= node.State.EnergyCap * scoring.DrainThreshold)
                return false;

            float multiplier = EnergyMath.HostileMultiplier(node.Type, board.MagnetarResistance);
            int captureCost = EnergyMath.CaptureCost(remainingEnergy, multiplier);
            foreach (BoardNode other in board.Nodes)
            {
                if (other.Faction != board.Opponent || !other.IsLit)
                    continue;
                if (EnergyMath.SendAmount(other.Energy, false) >= captureCost)
                    return true;
            }
            return false;
        }

        public static float DrainPenalty(EnemyBoard board, BoardNode node, int remainingEnergy, EnemyScoring scoring)
        {
            return IsExposed(board, node, remainingEnergy, scoring) ? scoring.DrainPenalty : 0f;
        }

        // Score de 12.3 para un origen propio y un destino ajeno. null si el origen no puede enviar o
        // el CosteEfectivo supera su Energía al 100 %.
        public static EnemyAction? ScoreAttack(EnemyBoard board, BoardNode source, BoardNode target,
                                              EnemyProfile profile, EnemyScoring scoring)
        {
            if (!CanSend(board, source) || target.Faction == board.Self || target == source)
                return null;

            int cost = EffectiveCost(board, target);
            if (cost > source.Energy)
                return null;

            SendPlan plan = PlanSend(source.Energy, cost, profile);
            float bonus = profile.PrioritizesPowerUps && target.PowerUpMarked ? scoring.PowerUpBonus : 0f;
            float risk = scoring.RouteRiskPerMagnetar * HostileMagnetarsOnRoute(board, source, target);
            float drain = DrainPenalty(board, source, source.Energy - plan.Charge, scoring);

            float score = (scoring.TypeValue(target.Type) * plan.Reach + bonus - cost - risk - drain)
                          * profile.AggressionMultiplier;
            return new EnemyAction(EnemyActionKind.Attack, source.Index, target.Index, score, fullSend: plan.FullSend);
        }

        // --- Lanzar poder (12.3.1) ---

        // Candidatos de 12.3.1: todo nodo encendido que no es del que decide. Los Asteroides, fuera.
        public static bool IsPowerCandidate(EnemyBoard board, BoardNode target)
        {
            return PowerRules.IsValidTarget(target.State, board.Self);
        }

        public static float ScorePower(EnemyBoard board, PowerType power, BoardNode target,
                                       EnemyProfile profile, EnemyScoring scoring)
        {
            NodeSnapshot state = target.State;
            float steal = power == PowerType.Wormhole && target.Type == NodeType.Pulsar && target.ReadyPower.HasValue
                ? scoring.StealBonus : 0f;
            float energy = state.EnergyCap > 0 ? (float)state.Energy / state.EnergyCap * scoring.RelativeEnergyWeight : 0f;
            float neutral = target.Faction == Faction.Neutral ? scoring.NeutralAdjustment : 0f;
            bool redundant = (power == PowerType.AbsoluteZero && state.IsFrozen) || (power == PowerType.Decay && state.IsInfected);
            float redundancy = redundant ? scoring.RedundancyPenalty : 0f;

            return (scoring.TypeValue(target.Type) + scoring.AffinityOf(power, target.Type) + steal + energy - neutral - redundancy)
                   * profile.AggressionMultiplier;
        }

        // El mejor objetivo para un poder, o null si no hay ningún candidato (12.3.1, test 6).
        public static EnemyAction? BestPowerTarget(EnemyBoard board, BoardNode pulsar, PowerType power,
                                                  EnemyProfile profile, EnemyScoring scoring)
        {
            EnemyAction? best = null;
            foreach (BoardNode target in board.Nodes)
            {
                if (!IsPowerCandidate(board, target))
                    continue;
                var action = new EnemyAction(EnemyActionKind.LaunchPower, pulsar.Index, target.Index,
                    ScorePower(board, power, target, profile, scoring), power: power);
                if (best == null || Precedes(action, best.Value))
                    best = action;
            }
            return best;
        }

        // --- Economía (12.3.2) ---

        public static float IgnitionNeed(EnemyBoard board, BoardNode asteroid, NodeType type, EnemyScoring scoring)
        {
            switch (type)
            {
                case NodeType.Star:
                    return scoring.StarNeed / (1 + CountOwn(board, NodeType.Star));
                case NodeType.Pulsar:
                    return CountOwn(board, NodeType.Pulsar) == 0 ? scoring.PulsarNeed : 0f;
                case NodeType.Magnetar:
                    return IsFrontline(board, asteroid) ? scoring.MagnetarNeed : 0f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Un Asteroide se enciende a un tipo encendido.");
            }
        }

        public static float ScoreIgnition(EnemyBoard board, BoardNode asteroid, NodeType type, EnemyScoring scoring)
        {
            return scoring.IgnitionBase + IgnitionNeed(board, asteroid, type, scoring);
        }

        // El nodo es, entre los propios, el más cercano a algún nodo del rival (encender Magnetar).
        // Con empate de distancia gana el primero de NodeRegistry.
        public static bool IsFrontline(EnemyBoard board, BoardNode node)
        {
            foreach (BoardNode rival in board.Nodes)
            {
                if (rival.Faction != board.Opponent)
                    continue;
                BoardNode closest = null;
                float closestDistance = float.MaxValue;
                foreach (BoardNode own in board.Nodes)
                {
                    if (own.Faction != board.Self)
                        continue;
                    float distance = (own.Position - rival.Position).sqrMagnitude;
                    if (distance < closestDistance)
                    {
                        closest = own;
                        closestDistance = distance;
                    }
                }
                if (closest == node)
                    return true;
            }
            return false;
        }

        public static float ScoreUpgrade(EnemyBoard board, BoardNode node, EnemyScoring scoring)
        {
            return scoring.UpgradeBase(node.Type) - node.UpgradeCost * scoring.UpgradeCostFactor
                   - DrainPenalty(board, node, node.Energy - node.UpgradeCost, scoring);
        }

        // ScoreCarga: la mitad del mejor ScorePoder, menos la mitad del coste y lo que faltaría para
        // no quedarse parada (7.1). null sin ningún objetivo legal.
        public static float? ScoreCharge(EnemyBoard board, BoardNode pulsar, PowerDefinition power,
                                         EnemyProfile profile, EnemyScoring scoring)
        {
            EnemyAction? best = BestPowerTarget(board, pulsar, power.Type, profile, scoring);
            if (best == null)
                return null;
            return best.Value.Score * scoring.ChargeValueFactor - power.Cost * scoring.ChargeCostFactor
                   - Math.Max(0, power.Cost - pulsar.Energy);
        }

        public static float ScoreConversion(BoardNode node, EnemyScoring scoring)
        {
            return scoring.ConversionBase - node.ConversionCost;
        }

        // --- Lista de candidatas (12.2) ---

        // Todas las candidatas del ciclo, puntuadas y en el orden de desempate de 12.3.3.
        public static List<EnemyAction> Candidates(EnemyBoard board, EnemyProfile profile, EnemyScoring scoring)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (scoring == null)
                throw new ArgumentNullException(nameof(scoring));

            var result = new List<EnemyAction>();
            foreach (BoardNode node in board.Nodes)
            {
                if (node.Faction != board.Self)
                    continue;

                if (node.IsLit)
                {
                    foreach (BoardNode target in board.Nodes)
                    {
                        EnemyAction? attack = ScoreAttack(board, node, target, profile, scoring);
                        if (attack.HasValue)
                            result.Add(attack.Value);
                    }
                }

                AddIgnitions(board, node, scoring, result);
                AddUpgrade(board, node, scoring, result);
                AddPowers(board, node, profile, scoring, result);
            }
            AddConversion(board, profile, scoring, result);

            result.Sort(Order);
            return result;
        }

        // --- Elección (12.1 pasos 4–6, 12.3.3) ---

        // Aplica el ruido a cada candidata, en el orden de Candidates, y devuelve la mejor si supera
        // el umbral; si no, null (acumular). Sin random o con ruido 0, no hay ruido.
        public static EnemyAction? Pick(IReadOnlyList<EnemyAction> candidates, EnemyProfile profile, System.Random random)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            EnemyAction? best = null;
            float noise = profile.DecisionNoise;
            foreach (EnemyAction candidate in candidates)
            {
                float score = candidate.Score;
                if (random != null && noise > 0f)
                    score *= 1f + (float)(random.NextDouble() * 2.0 - 1.0) * noise;
                EnemyAction noisy = candidate.WithScore(score);
                if (best == null || Precedes(noisy, best.Value))
                    best = noisy;
            }
            return best.HasValue && best.Value.Score > profile.ActionThreshold ? best : null;
        }

        // a va antes que b: más puntuación y, con empate, el orden de 12.3.3.
        public static bool Precedes(EnemyAction a, EnemyAction b)
        {
            if (a.Score != b.Score)
                return a.Score > b.Score;
            return Order(a, b) < 0;
        }

        // Orden de 12.3.3 sin la puntuación: acción, nodo que actúa, objetivo y, para encender y
        // poderes, el orden de sus enums.
        public static int Order(EnemyAction a, EnemyAction b)
        {
            int c = a.Kind.CompareTo(b.Kind);
            if (c != 0) return c;
            c = a.Actor.CompareTo(b.Actor);
            if (c != 0) return c;
            c = a.Target.CompareTo(b.Target);
            if (c != 0) return c;
            c = a.NodeType.CompareTo(b.NodeType);
            if (c != 0) return c;
            return a.Power.CompareTo(b.Power);
        }

        // --- Generación por acción ---

        static readonly NodeType[] IgnitionTypes = { NodeType.Star, NodeType.Pulsar, NodeType.Magnetar };

        static void AddIgnitions(EnemyBoard board, BoardNode node, EnemyScoring scoring, List<EnemyAction> result)
        {
            if (node.IsLit)
                return;
            foreach (NodeType type in IgnitionTypes)
            {
                // 11.1: lo que el nivel no permite, el nodo no lo ofrece (T5-01).
                if (!node.OffersIgnition(type) || !ConversionRules.CheckIgnition(node.State, board.Self, type, board.IgnitionCost).IsAllowed)
                    continue;
                result.Add(new EnemyAction(EnemyActionKind.Ignite, node.Index, -1,
                    ScoreIgnition(board, node, type, scoring), nodeType: type));
            }
        }

        static void AddUpgrade(EnemyBoard board, BoardNode node, EnemyScoring scoring, List<EnemyAction> result)
        {
            if (!node.IsLit || node.UpgradeCost < 0)
                return;
            // El nivel máximo ya lo dice UpgradeCost; aquí solo quedan dueño, estado y Energía.
            if (!ConversionRules.CheckUpgrade(node.State, board.Self, node.State.Level + 1, node.UpgradeCost).IsAllowed)
                return;
            result.Add(new EnemyAction(EnemyActionKind.Upgrade, node.Index, -1, ScoreUpgrade(board, node, scoring)));
        }

        static void AddPowers(EnemyBoard board, BoardNode node, EnemyProfile profile, EnemyScoring scoring, List<EnemyAction> result)
        {
            if (node.Type != NodeType.Pulsar)
                return;

            if (node.ReadyPower.HasValue)
            {
                if (node.State.IsFrozen)
                    return;
                foreach (BoardNode target in board.Nodes)
                {
                    if (!IsPowerCandidate(board, target))
                        continue;
                    result.Add(new EnemyAction(EnemyActionKind.LaunchPower, node.Index, target.Index,
                        ScorePower(board, node.ReadyPower.Value, target, profile, scoring), power: node.ReadyPower.Value));
                }
                return;
            }

            foreach (PowerDefinition power in board.Powers)
            {
                if (power == null || !node.OffersPower(power.Type) || !PowerRules.CheckCharge(node.State, board.Self, power.RequiredLevel, power.Cost).IsAllowed)
                    continue;
                float? score = ScoreCharge(board, node, power, profile, scoring);
                if (score.HasValue)
                    result.Add(new EnemyAction(EnemyActionKind.ChargePower, node.Index, -1, score.Value, power: power.Type));
            }
        }

        // Una sola candidata: el nodo propio con más Energía que puede pagar su reconversión a
        // Estrella; con empate, el primero de NodeRegistry (12.3.2).
        static void AddConversion(EnemyBoard board, EnemyProfile profile, EnemyScoring scoring, List<EnemyAction> result)
        {
            if (!profile.UsesConversion || CountOwn(board, NodeType.Star) > 0)
                return;

            BoardNode origin = null;
            foreach (BoardNode node in board.Nodes)
            {
                if (!node.OffersConversion(NodeType.Star)
                    || !ConversionRules.CheckConversion(node.State, board.Self, NodeType.Star, node.ConversionCost).IsAllowed)
                    continue;
                if (origin == null || node.Energy > origin.Energy)
                    origin = node;
            }
            if (origin != null)
                result.Add(new EnemyAction(EnemyActionKind.Convert, origin.Index, -1, ScoreConversion(origin, scoring), nodeType: NodeType.Star));
        }

        // --- Utilidades ---

        // Un origen propio que puede enviar algo (5.2, ProtonLauncher): encendido, ni congelado ni
        // reconvirtiéndose, y con al menos 2 de Energía para que el 50 % no sea 0.
        static bool CanSend(EnemyBoard board, BoardNode source)
        {
            return source.Faction == board.Self && source.IsLit && !source.State.IsFrozen && !source.State.IsConverting
                && EnergyMath.SendAmount(source.Energy, false) > 0;
        }

        static int CountOwn(EnemyBoard board, NodeType type)
        {
            int count = 0;
            foreach (BoardNode node in board.Nodes)
            {
                if (node.Faction == board.Self && node.Type == type)
                    count++;
            }
            return count;
        }

        public static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared <= 0f)
                return Vector2.Distance(point, a);
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
