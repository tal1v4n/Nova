using System;
using System.Collections.Generic;

namespace Nova.Core
{
    // Un protón en vuelo visto desde su destino: la facción de su emisor (6.1), su Carga actual
    // y la distancia que le queda hasta el centro del nodo. Core no conoce posiciones: la
    // distancia la mide quien construye la lista (NodeScare, el algoritmo del Enemigo en T4).
    public readonly struct IncomingProton
    {
        public readonly Faction Faction;
        public readonly int Charge;
        public readonly float Distance;

        public IncomingProton(Faction faction, int charge, float distance)
        {
            Faction = faction;
            Charge = charge;
            Distance = distance;
        }
    }

    // Respuesta de ThreatCalculator.Assess para un nodo.
    public readonly struct ThreatAssessment
    {
        public static readonly ThreatAssessment Safe =
            new ThreatAssessment(false, float.PositiveInfinity, 0);

        // 12.4: la Carga hostil efectiva que vuela hacia el nodo alcanza su Energía.
        public readonly bool IsThreatened;
        // Segundos hasta el impacto que lo captura. Infinito si no está amenazado.
        public readonly float TimeToCapture;
        // Suma de la Carga efectiva de los protones hostiles, letales o no.
        public readonly int HostileCharge;

        public ThreatAssessment(bool isThreatened, float timeToCapture, int hostileCharge)
        {
            IsThreatened = isThreatened;
            TimeToCapture = timeToCapture;
            HostileCharge = hostileCharge;
        }
    }

    // Amenaza sobre un nodo (15.3): si va a caer y en cuánto tiempo. Lo usan el estado Asustado
    // (14.2) y el algoritmo del Enemigo (12.4), que nunca ve la animación sino esta función (12.6).
    // Función pura: los números de balance llegan como parámetro desde BalanceConfig (16.1).
    public static class ThreatCalculator
    {
        // Segundos que tarda en llegar un protón a esa distancia, a velocidad constante (6.1).
        public static float TimeToArrival(float distance, float protonSpeed)
        {
            if (distance < 0f)
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "La distancia no puede ser negativa.");
            if (protonSpeed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(protonSpeed), protonSpeed, "La velocidad del protón debe ser positiva.");

            return distance / protonSpeed;
        }

        // Carga con la que el protón golpearía al nodo: 0 si es aliado, que suma y no resta (5.3).
        public static int HostileCharge(IncomingProton proton, NodeSnapshot node, float magnetarResistance)
        {
            if (proton.Faction == node.Faction)
                return 0;
            float multiplier = EnergyMath.HostileMultiplier(node.Type, magnetarResistance);
            return EnergyMath.EffectiveCharge(proton.Charge, multiplier);
        }

        // 14.2: el protón, él solo, captura el nodo tal como está ahora.
        public static bool IsLethal(IncomingProton proton, NodeSnapshot node, float magnetarResistance)
        {
            return proton.Faction != node.Faction
                && HostileCharge(proton, node, magnetarResistance) >= node.Energy;
        }

        // EstaAsustado de 14.2: algún protón hostil letal llega antes del umbral de susto.
        // Varios protones letales dan un único susto, y varios que solo juntos lo serían no
        // asustan: el susto avisa de un golpe concreto.
        public static bool IsScared(NodeSnapshot node, IReadOnlyList<IncomingProton> incoming,
                                    float magnetarResistance, float protonSpeed, float scareThreshold)
        {
            if (incoming == null)
                throw new ArgumentNullException(nameof(incoming));
            if (scareThreshold < 0f)
                throw new ArgumentOutOfRangeException(nameof(scareThreshold), scareThreshold, "El umbral de susto no puede ser negativo.");

            for (int i = 0; i < incoming.Count; i++)
            {
                IncomingProton proton = incoming[i];
                if (IsLethal(proton, node, magnetarResistance)
                    && TimeToArrival(proton.Distance, protonSpeed) < scareThreshold)
                    return true;
            }
            return false;
        }

        // 12.4: amenazado si la suma de la Carga hostil efectiva alcanza la Energía del nodo. El
        // tiempo es el de llegada del protón que completa esa suma, con los hostiles en orden de
        // llegada: los golpes se restan uno tras otro (15.10) y cada uno redondea por su cuenta (6.2).
        // Los refuerzos en vuelo no se descuentan: 12.4 los deja fuera.
        public static ThreatAssessment Assess(NodeSnapshot node, IReadOnlyList<IncomingProton> incoming,
                                              float magnetarResistance, float protonSpeed)
        {
            if (incoming == null)
                throw new ArgumentNullException(nameof(incoming));

            // Con la clave del orden de la lista, para que el orden sea estable con la misma distancia.
            var ordered = new List<KeyValuePair<int, IncomingProton>>();
            for (int i = 0; i < incoming.Count; i++)
            {
                if (incoming[i].Faction != node.Faction)
                    ordered.Add(new KeyValuePair<int, IncomingProton>(i, incoming[i]));
            }
            if (ordered.Count == 0)
                return ThreatAssessment.Safe;

            ordered.Sort((a, b) =>
            {
                int byDistance = a.Value.Distance.CompareTo(b.Value.Distance);
                return byDistance != 0 ? byDistance : a.Key.CompareTo(b.Key);
            });

            int total = 0;
            float timeToCapture = float.PositiveInfinity;
            foreach (KeyValuePair<int, IncomingProton> entry in ordered)
            {
                total += HostileCharge(entry.Value, node, magnetarResistance);
                if (float.IsPositiveInfinity(timeToCapture) && total >= node.Energy)
                    timeToCapture = TimeToArrival(entry.Value.Distance, protonSpeed);
            }

            bool threatened = !float.IsPositiveInfinity(timeToCapture);
            return new ThreatAssessment(threatened, timeToCapture, total);
        }
    }
}
