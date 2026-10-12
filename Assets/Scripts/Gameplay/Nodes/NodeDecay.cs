using System;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Gameplay
{
    // Estado Infectado (7.4, 15.5, T3-05): pone y quita la bandera Infected y resta la Energía
    // del Decaimiento. La cura la calcula ImpactResolver (CuresInfection) y la aplica NodeController.
    //
    // - Lo infecta un haz de Decaimiento al terminar (PowerBeamSystem). Otro sobre un nodo ya
    //   infectado reinicia el efecto, sin duplicar la tasa (17 caso 13).
    // - Pierde BalanceConfig.DecayRate de Energía por segundo, punto a punto: con 2 E/s, uno cada
    //   0.5 s. El reloj no se para en 0 y no guarda puntos para luego: si a 0 le entra Energía que
    //   no es un impacto (la generación de la Estrella), el siguiente punto se la lleva a su hora.
    // - Al llegar a 0 sigue infectado y del mismo dueño (17 caso 11). Funciona con normalidad.
    // - Congelado no lo para, y el Decaimiento no descongela ni reinicia el auto-deshielo (7.3).
    //
    // El reloj corre en el tick fijo (15.10) en microsegundos enteros, como los demás temporizadores.
    // Como NodeFreeze, va en todo nodo: cualquier nodo encendido puede ser objetivo (7.2.1).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class NodeDecay : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        [SerializeField] BalanceConfig balance;

        NodeController node;
        long elapsedMicros;

        public bool IsInfected => Node.HasState(NodeStateFlags.Infected);

        // Progreso hacia el siguiente −1, en segundos.
        public float Progress => (float)(elapsedMicros / MicrosPerSecond);

        // Al infectarse (también al reiniciarse) y al curarse.
        public event Action OnInfected;
        public event Action OnCured;

        // Efecto de Decaimiento. Quien lo llama ya validó el objetivo (7.2.1, 7.2.2) y, si se estaba
        // reconvirtiendo, ya canceló la reconversión: 14.6 no deja convivir las dos banderas.
        public void Infect()
        {
            // El reloj va antes que la bandera (desviación 175).
            elapsedMicros = 0;
            if (!IsInfected && !Node.SetStateFlag(NodeStateFlags.Infected, true))
                throw new InvalidOperationException($"{name}: 14.6 no admite Infectado con Reconvirtiendo.");
            OnInfected?.Invoke();
        }

        // Cura (7.4): cualquier Energía recibida por impacto, de la facción que sea.
        public void Cure()
        {
            if (!IsInfected)
                return;
            elapsedMicros = 0;
            Node.SetStateFlag(NodeStateFlags.Infected, false);
            OnCured?.Invoke();
        }

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            if (!IsInfected)
                return;

            float rate = Balance.DecayRate;
            if (rate <= 0f)
                throw new InvalidOperationException("BalanceConfig.DecayRate debe ser positiva.");
            long interval = (long)Math.Round(MicrosPerSecond / rate);

            elapsedMicros += ToMicros(deltaTime);
            int lost = 0;
            while (elapsedMicros >= interval)
            {
                elapsedMicros -= interval;
                lost++;
            }

            // Hasta 0 (7.4): Subtract no baja de ahí.
            if (lost > 0 && Node.Energy > 0)
                Node.SubtractEnergy(lost);
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config)
        {
            balance = config;
        }

        static long ToMicros(float seconds) => (long)Math.Round(seconds * MicrosPerSecond);

        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("NodeDecay necesita BalanceConfig.");

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
    }
}
