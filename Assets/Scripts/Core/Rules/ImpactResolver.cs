namespace Nova.Core
{
    // Estado de un nodo en un instante, tal como lo leen las reglas de Core. En un impacto,
    // la facción se lee aquí y no al lanzar el protón (17, caso 2).
    public readonly struct NodeSnapshot
    {
        public readonly Faction Faction;
        public readonly NodeType Type;
        public readonly int Energy;
        public readonly int EnergyCap;
        public readonly bool IsFrozen;
        public readonly bool IsInfected;
        public readonly int Level;          // 0 (Asteroide) a 5
        public readonly bool IsConverting;  // Reconvirtiendo (8.1)
        public readonly bool HasPowerCharge; // Púlsar cargando un poder o con uno ya cargado (7.1)

        public NodeSnapshot(Faction faction, NodeType type, int energy, int energyCap,
                            bool isFrozen = false, bool isInfected = false,
                            int level = 0, bool isConverting = false, bool hasPowerCharge = false)
        {
            Faction = faction;
            Type = type;
            Energy = energy;
            EnergyCap = energyCap;
            IsFrozen = isFrozen;
            IsInfected = isInfected;
            Level = level;
            IsConverting = isConverting;
            HasPowerCharge = hasPowerCharge;
        }
    }

    // Un protón al llegar: la facción de su emisor en el lanzamiento (6.1) y su Carga actual.
    public readonly struct ProtonSnapshot
    {
        public readonly Faction Faction;
        public readonly int Charge;

        public ProtonSnapshot(Faction faction, int charge)
        {
            Faction = faction;
            Charge = charge;
        }
    }

    // Lo que el impacto le hace al nodo. Quien lo aplica es NodeController; aquí solo se calcula.
    public readonly struct ImpactOutcome
    {
        public readonly ImpactResult Result;
        public readonly int NewEnergy;
        public readonly Faction NewFaction;
        public readonly bool FactionChanged;
        // Sale de Congelado por deshielo (7.3).
        public readonly bool Unfreezes;
        // Cualquier Energía recibida cura el Decaimiento, sea de la facción que sea (7.4, 14.1).
        public readonly bool CuresInfection;
        // Último emisor que impactó un nodo congelado sin descongelarlo (7.3, lastToucher).
        public readonly Faction? LastToucher;

        public ImpactOutcome(ImpactResult result, int newEnergy, Faction newFaction, bool factionChanged,
                             bool unfreezes, bool curesInfection, Faction? lastToucher)
        {
            Result = result;
            NewEnergy = newEnergy;
            NewFaction = newFaction;
            FactionChanged = factionChanged;
            Unfreezes = unfreezes;
            CuresInfection = curesInfection;
            LastToucher = lastToucher;
        }
    }

    // Resolución del impacto de un protón sobre un nodo: 6.2 y, si el nodo está congelado,
    // 7.3. Función pura: no modifica nada y siempre da la misma salida para la misma entrada.
    public static class ImpactResolver
    {
        public static ImpactOutcome Resolve(ProtonSnapshot proton, NodeSnapshot target, float magnetarResistance)
        {
            return target.IsFrozen
                ? ResolveFrozen(proton, target, magnetarResistance)
                : ResolveNormal(proton, target, magnetarResistance);
        }

        // 6.2.
        static ImpactOutcome ResolveNormal(ProtonSnapshot proton, NodeSnapshot target, float magnetarResistance)
        {
            bool cures = target.IsInfected;

            if (proton.Faction == target.Faction)
            {
                // Absorción 1:1, también en el Magnetar (5.3).
                int absorbed = EnergyMath.AddCapped(target.Energy, proton.Charge, target.EnergyCap);
                return new ImpactOutcome(ImpactResult.Absorbed, absorbed, target.Faction, false,
                                         false, cures, null);
            }

            float multiplier = EnergyMath.HostileMultiplier(target.Type, magnetarResistance);
            int effective = EnergyMath.EffectiveCharge(proton.Charge, multiplier);

            if (effective < target.Energy)
            {
                return new ImpactOutcome(ImpactResult.Damaged, target.Energy - effective, target.Faction, false,
                                         false, cures, null);
            }

            // Captura: el remanente, ya multiplicado, queda en el nodo hasta su Tope.
            int remainder = System.Math.Min(effective - target.Energy, target.EnergyCap);
            return new ImpactOutcome(ImpactResult.Captured, remainder, proton.Faction, true,
                                     false, cures, null);
        }

        // 7.3, ResolveImpact_Frozen. Cualquier facción, incluida la propia, drena el nodo,
        // y el multiplicador del Magnetar se aplica siempre.
        static ImpactOutcome ResolveFrozen(ProtonSnapshot proton, NodeSnapshot target, float magnetarResistance)
        {
            bool cures = target.IsInfected;
            float multiplier = EnergyMath.HostileMultiplier(target.Type, magnetarResistance);
            int effective = EnergyMath.EffectiveCharge(proton.Charge, multiplier);

            if (effective < target.Energy)
            {
                return new ImpactOutcome(ImpactResult.ThawProgress, target.Energy - effective, target.Faction, false,
                                         false, cures, proton.Faction);
            }

            // Deshielo: queda a 0, el remanente se pierde y pasa al bando del emisor.
            return new ImpactOutcome(ImpactResult.ThawedAndCaptured, 0, proton.Faction,
                                     proton.Faction != target.Faction, true, cures, null);
        }
    }
}
