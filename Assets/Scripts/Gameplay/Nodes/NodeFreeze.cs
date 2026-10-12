using System;
using Nova.Core;
using Nova.Data;
using UnityEngine;

namespace Nova.Gameplay
{
    // Por qué se descongela un nodo (7.3, 7.5).
    public enum ThawReason
    {
        Impact = 0,   // Deshielo: un impacto lo deja a 0
        Silence = 1,  // Auto-deshielo: 30 s sin impactos
        Wormhole = 2, // Lo captura un Agujero de Gusano (T3-06)
    }

    // Estado Congelado (7.3, 15.5, T3-04): pone y quita la bandera Frozen y lleva el reloj del
    // auto-deshielo. Lo que el nodo deja de hacer congelado lo comprueba cada componente leyendo
    // la bandera; el deshielo por impacto lo calcula ImpactResolver y lo aplica NodeController.
    //
    // - Lo congela un haz de Cero Absoluto al terminar (PowerBeamSystem). Un nodo ya congelado
    //   que recibe otro sigue congelado y el reloj vuelve a 0: el haz también es un impacto (14.1).
    // - Cada impacto de protón reinicia el reloj. Con 30 s seguidos sin ninguno se descongela solo,
    //   conservando facción y Energía. No hay máximo: lo termina el deshielo o el silencio.
    // - El Decaimiento no es un impacto: ni descongela ni reinicia el reloj (T3-05).
    //
    // El reloj corre en el tick fijo (15.10) en microsegundos enteros, como los demás temporizadores.
    // Como NodeScare, va en todo nodo: cualquier nodo encendido puede ser objetivo (7.2.1).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class NodeFreeze : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        [SerializeField] BalanceConfig balance;

        NodeController node;
        long idleMicros;

        public bool IsFrozen => Node.HasState(NodeStateFlags.Frozen);

        // Segundos sin impactos desde que se congeló o desde el último impacto.
        public float IdleTime => (float)(idleMicros / MicrosPerSecond);

        // Al congelarse, y al descongelarse con su motivo.
        public event Action OnFrozen;
        public event Action<ThawReason> OnThawed;

        // Efecto de Cero Absoluto. Quien lo llama ya validó el objetivo (7.2.1, 7.2.2).
        public void Freeze()
        {
            // El reloj va antes que la bandera: quien escuche la bandera lee IdleTime ya a 0 (desviación 175).
            idleMicros = 0;
            if (IsFrozen)
                return;
            if (!Node.SetStateFlag(NodeStateFlags.Frozen, true))
                throw new InvalidOperationException($"{name}: 14.6 no admite Congelado.");
            OnFrozen?.Invoke();
        }

        // Un protón ha impactado el nodo congelado sin descongelarlo: el reloj vuelve a 0.
        public void NotifyImpact()
        {
            if (IsFrozen)
                idleMicros = 0;
        }

        // Deshielo por impacto (7.3): la Energía y la facción las aplica NodeController.
        public void Thaw()
        {
            Unfreeze(ThawReason.Impact);
        }

        // Captura por Agujero de Gusano (7.5): elimina el congelamiento. El cambio de bando lo hace NodeController.
        public void ClearByWormhole()
        {
            Unfreeze(ThawReason.Wormhole);
        }

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            if (!IsFrozen)
                return;

            idleMicros += ToMicros(deltaTime);
            if (idleMicros >= ToMicros(Balance.AutoThawTime))
                Unfreeze(ThawReason.Silence);
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config)
        {
            balance = config;
        }

        void Unfreeze(ThawReason reason)
        {
            if (!IsFrozen)
                return;
            idleMicros = 0;
            Node.SetStateFlag(NodeStateFlags.Frozen, false);
            OnThawed?.Invoke(reason);
        }

        static long ToMicros(float seconds) => (long)Math.Round(seconds * MicrosPerSecond);

        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("NodeFreeze necesita BalanceConfig.");

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
    }
}
