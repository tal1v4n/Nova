using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Gameplay
{
    // Energía del nodo y su Tope (15.5). Entera, entre 0 y el Tope. El excedente de una
    // suma se descarta (5.3) y una resta nunca baja de 0 (5.2).
    [DisallowMultipleComponent]
    public sealed class NodeEnergy : MonoBehaviour
    {
        int energy;
        int energyCap;

        public int Energy => energy;
        public int EnergyCap => energyCap;

        // (anterior, nueva). Solo se emite si el valor cambia de verdad.
        public event Action<int, int> OnEnergyChanged;

        // Valores de partida, sin evento.
        public void Initialize(int initialEnergy, int cap)
        {
            if (cap < 0)
                throw new ArgumentOutOfRangeException(nameof(cap), cap, "El Tope no puede ser negativo.");
            if (initialEnergy < 0 || initialEnergy > cap)
                throw new ArgumentOutOfRangeException(nameof(initialEnergy), initialEnergy, $"Debe estar entre 0 y {cap}.");

            energyCap = cap;
            energy = initialEnergy;
        }

        public void Add(int amount)
        {
            Set(EnergyMath.AddCapped(energy, amount, energyCap));
        }

        public void Subtract(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "La cantidad no puede ser negativa.");
            Set(Math.Max(0, energy - amount));
        }

        // Cambia el Tope (al encender o reconvertir) sin tocar la Energía; Clamp la recorta después.
        public void SetCap(int cap)
        {
            if (cap < 0)
                throw new ArgumentOutOfRangeException(nameof(cap), cap, "El Tope no puede ser negativo.");
            energyCap = cap;
        }

        // Recorta la Energía al Tope actual (8.1).
        public void Clamp()
        {
            Set(Math.Min(energy, energyCap));
        }

        void Set(int value)
        {
            if (value == energy)
                return;

            int previous = energy;
            energy = value;
            OnEnergyChanged?.Invoke(previous, value);
        }
    }
}
