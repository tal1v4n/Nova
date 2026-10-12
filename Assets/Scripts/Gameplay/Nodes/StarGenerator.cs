using System;
using Nova.Core;
using UnityEngine;

namespace Nova.Gameplay
{
    // Generación de la Estrella (4.2, 15.5): +1 de Energía por cada Intervalo de su nivel,
    // nunca fracciones, y parada al llegar al Tope. Corre en el tick fijo de 50 Hz (15.10).
    //
    // El tiempo se acumula en microsegundos enteros y el sobrante pasa al intervalo siguiente:
    // los intervalos de 1.05 s y 0.85 s no son múltiplos de 20 ms, y así conservan su tasa media
    // exacta sin la deriva de sumar floats.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class StarGenerator : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        NodeController node;
        long elapsedMicros;

        // Progreso hacia el siguiente +1, en segundos. Solo lectura, para tests y depuración.
        public float Progress => (float)(elapsedMicros / MicrosPerSecond);

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            // Un Neutral no genera (tabla de 3): solo existe como Asteroide o Magnetar,
            // pero la guarda no depende de que el mapa lo respete.
            if (!Node.IsInitialized || Node.Type != NodeType.Star || Node.Faction == Faction.Neutral)
            {
                elapsedMicros = 0;
                return;
            }

            // Lleno: el temporizador se detiene y conserva su progreso (4.2, «no acumula»).
            // Reconvirtiendo tampoco genera (8.1), y Congelado lo parará cuando exista (T3-04, 15.5).
            if (Node.Energy >= Node.EnergyCap || Node.HasState(NodeStateFlags.Converting))
                return;

            long interval = ToMicros(Node.CurrentLevelData.GenerationInterval);
            if (interval <= 0)
                throw new InvalidOperationException($"Estrella nivel {Node.Level} sin intervalo de generación.");

            elapsedMicros += ToMicros(deltaTime);

            int generated = 0;
            int room = Node.EnergyCap - Node.Energy;
            while (elapsedMicros >= interval && generated < room)
            {
                elapsedMicros -= interval;
                generated++;
            }

            if (generated > 0)
                Node.AddEnergy(generated);
        }

        static long ToMicros(float seconds) => (long)Math.Round(seconds * MicrosPerSecond);

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
    }
}
