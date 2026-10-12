using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Enemy
{
    // Defensa del Enemigo (12.4, 15.7, T4-05): el paso 2 del ciclo de 12.1, que va antes de puntuar.
    // Estática y sin estado, como EnemyEvaluator.
    //
    // Un nodo propio está amenazado si la Carga hostil efectiva que vuela hacia él alcanza su Energía
    // (ThreatCalculator.Assess). Se atiende primero la amenaza que antes captura, y se responde con una
    // sola acción, en este orden:
    //   1. Reforzar con el 50 % de un nodo propio cuyo protón llegue antes que el impacto que captura
    //      («a distancia») y cuyo 50 % cubra Carga hostil − Energía + 1 («suficiente»). El que llega
    //      antes y, con empate, el que tiene más Energía.
    //   2. Congelar el nodo origen del ataque con un Cero Absoluto ya cargado.
    //   3. Evacuar, con Usa evacuación: toda la Energía del amenazado al nodo propio no amenazado más
    //      cercano.
    // Si ninguna cabe para ninguna amenaza, no hay defensa y el ciclo sigue con las candidatas
    // (desviación 226).
    public static class EnemyThreatResponder
    {
        // Un nodo propio amenazado y su evaluación.
        public readonly struct Threat
        {
            public readonly BoardNode Node;
            public readonly ThreatAssessment Assessment;

            public Threat(BoardNode node, ThreatAssessment assessment)
            {
                Node = node;
                Assessment = assessment;
            }
        }

        // Los nodos propios amenazados, del que antes captura al que más tarda; con empate, por el
        // orden de NodeRegistry.
        public static List<Threat> Threats(EnemyBoard board)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));

            var threats = new List<Threat>();
            foreach (BoardNode node in board.Nodes)
            {
                if (node.Faction != board.Self || node.Incoming.Count == 0)
                    continue;
                ThreatAssessment assessment = ThreatCalculator.Assess(node.State, node.IncomingProtons(),
                    board.MagnetarResistance, board.ProtonSpeed);
                if (assessment.IsThreatened)
                    threats.Add(new Threat(node, assessment));
            }
            threats.Sort((a, b) =>
            {
                int byTime = a.Assessment.TimeToCapture.CompareTo(b.Assessment.TimeToCapture);
                return byTime != 0 ? byTime : a.Node.Index.CompareTo(b.Node.Index);
            });
            return threats;
        }

        // La respuesta de este ciclo, o null si no hay amenaza o no hay nada que hacer.
        public static EnemyAction? Respond(EnemyBoard board, EnemyProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            List<Threat> threats = Threats(board);
            foreach (Threat threat in threats)
            {
                EnemyAction? response = RespondTo(board, threat, threats, profile);
                if (response.HasValue)
                    return response;
            }
            return null;
        }

        public static EnemyAction? RespondTo(EnemyBoard board, Threat threat, IReadOnlyList<Threat> threats, EnemyProfile profile)
        {
            BoardNode donor = BestDonor(board, threat);
            if (donor != null)
                return new EnemyAction(EnemyActionKind.Reinforce, donor.Index, threat.Node.Index, 0f);

            EnemyAction? freeze = FreezeAttacker(board, threat);
            if (freeze.HasValue)
                return freeze;

            if (profile.UsesEvacuation)
            {
                BoardNode shelter = EvacuationTarget(board, threat, threats);
                if (shelter != null)
                    return new EnemyAction(EnemyActionKind.Evacuate, threat.Node.Index, shelter.Index, 0f, fullSend: true);
            }
            return null;
        }

        // Lo que un refuerzo tiene que llevar para que el nodo no caiga: Carga hostil − Energía + 1.
        public static int ReinforcementNeeded(Threat threat)
        {
            return Math.Max(1, threat.Assessment.HostileCharge - threat.Node.Energy + 1);
        }

        // 1. El donante que llega antes con lo suficiente. Un nodo congelado no se refuerza: la Energía
        // propia también lo drena (7.3, desviación 225).
        public static BoardNode BestDonor(EnemyBoard board, Threat threat)
        {
            BoardNode target = threat.Node;
            if (target.State.IsFrozen)
                return null;

            int needed = ReinforcementNeeded(threat);
            BoardNode best = null;
            float bestTime = float.MaxValue;
            foreach (BoardNode node in board.Nodes)
            {
                if (node == target || !CanSend(board, node) || EnergyMath.SendAmount(node.Energy, false) < needed)
                    continue;
                float time = ThreatCalculator.TimeToArrival(Vector2.Distance(node.Position, target.Position), board.ProtonSpeed);
                if (time >= threat.Assessment.TimeToCapture)
                    continue;
                if (best == null || time < bestTime || (time == bestTime && node.Energy > best.Energy))
                {
                    best = node;
                    bestTime = time;
                }
            }
            return best;
        }

        // 2. Cero Absoluto ya cargado sobre el nodo del que salió el golpe más fuerte de la amenaza; con
        // empate, el que llega antes. El origen tiene que seguir siendo objetivo válido (7.2.1).
        public static EnemyAction? FreezeAttacker(EnemyBoard board, Threat threat)
        {
            BoardNode pulsar = null;
            foreach (BoardNode node in board.Nodes)
            {
                if (node.Faction == board.Self && node.ReadyPower == PowerType.AbsoluteZero && !node.State.IsFrozen)
                {
                    pulsar = node;
                    break;
                }
            }
            if (pulsar == null)
                return null;

            BoardNode source = AttackSource(board, threat);
            if (source == null)
                return null;
            return new EnemyAction(EnemyActionKind.LaunchPower, pulsar.Index, source.Index, 0f, power: PowerType.AbsoluteZero);
        }

        public static BoardNode AttackSource(EnemyBoard board, Threat threat)
        {
            BoardNode target = threat.Node;
            float multiplier = EnergyMath.HostileMultiplier(target.Type, board.MagnetarResistance);
            BoardProton? strongest = null;
            int strongestCharge = -1;
            foreach (BoardProton proton in target.Incoming)
            {
                if (proton.Proton.Faction == target.Faction || proton.SourceIndex < 0)
                    continue;
                int charge = EnergyMath.EffectiveCharge(proton.Proton.Charge, multiplier);
                if (charge > strongestCharge || (charge == strongestCharge && proton.Proton.Distance < strongest.Value.Proton.Distance))
                {
                    strongest = proton;
                    strongestCharge = charge;
                }
            }
            if (strongest == null)
                return null;

            BoardNode source = board.Find(strongest.Value.SourceIndex);
            return source != null && PowerRules.IsValidTarget(source.State, board.Self) ? source : null;
        }

        // 3. El nodo propio no amenazado más cercano; con empate, el primero de NodeRegistry. null si el
        // amenazado no puede enviar.
        public static BoardNode EvacuationTarget(EnemyBoard board, Threat threat, IReadOnlyList<Threat> threats)
        {
            BoardNode from = threat.Node;
            if (!from.IsLit || from.State.IsFrozen || from.State.IsConverting || from.Energy <= 0)
                return null;

            BoardNode best = null;
            float bestDistance = float.MaxValue;
            foreach (BoardNode node in board.Nodes)
            {
                if (node == from || node.Faction != board.Self || IsThreatened(node, threats))
                    continue;
                float distance = (node.Position - from.Position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = node;
                    bestDistance = distance;
                }
            }
            return best;
        }

        static bool IsThreatened(BoardNode node, IReadOnlyList<Threat> threats)
        {
            foreach (Threat threat in threats)
            {
                if (threat.Node == node)
                    return true;
            }
            return false;
        }

        static bool CanSend(EnemyBoard board, BoardNode node)
        {
            return node.Faction == board.Self && node.IsLit && !node.State.IsFrozen && !node.State.IsConverting
                && EnergyMath.SendAmount(node.Energy, false) > 0;
        }
    }
}
