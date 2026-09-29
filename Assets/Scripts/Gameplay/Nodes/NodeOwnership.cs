using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Gameplay
{
    // Facción actual del nodo (15.5). Única puerta para cambiar de bando.
    [DisallowMultipleComponent]
    public sealed class NodeOwnership : MonoBehaviour
    {
        Faction faction;

        public Faction Faction => faction;

        // (anterior, nueva). Solo se emite si la facción cambia de verdad.
        public event Action<Faction, Faction> OnFactionChanged;

        // Facción de partida, sin evento: no es un cambio de bando.
        public void SetInitialFaction(Faction initial)
        {
            faction = initial;
        }

        public void ChangeFaction(Faction newFaction)
        {
            if (newFaction == faction)
                return;

            Faction previous = faction;
            faction = newFaction;
            OnFactionChanged?.Invoke(previous, newFaction);
        }
    }
}
