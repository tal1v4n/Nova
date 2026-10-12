using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using Nova.Systems;
using UnityEngine;

namespace Nova.Enemy
{
    // Ciclo de decisión del Enemigo (12.1, 12.7, 15.7, T4-04). Cada IntervaloDeDecision del perfil:
    //   1. Foto del tablero (EnemyBoard.Capture sobre NodeRegistry).
    //   2. La defensa ante una amenaza crítica llega con EnemyThreatResponder (T4-05).
    //   3–6. Candidatas de EnemyEvaluator, ruido con semilla, y la mejor si supera el umbral; si no,
    //        nada (acumular).
    //
    // Una acción como máximo por ciclo (12.7). El reloj corre en el tick fijo (15.10) en microsegundos
    // enteros, y el primer ciclo llega al cumplirse el primer intervalo, no en el tick 0. Va a -40,
    // antes que los nodos (15.10): decide sobre el tablero que dejó el tick anterior.
    //
    // Determinismo (12.7): el ruido sale de un System.Random con la semilla del Inspector. Misma
    // partida y misma semilla, mismas decisiones.
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class EnemyController : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        [SerializeField] EnemyProfile profile;
        [SerializeField] EnemyScoring scoring;
        [SerializeField] BalanceConfig balance;
        [SerializeField] EnemyCommandIssuer issuer;

        [Tooltip("Los tres PowerDefinition (16.3): coste y nivel requerido de cada poder.")]
        [SerializeField] PowerDefinition[] powers = Array.Empty<PowerDefinition>();

        [Tooltip("Opcional: sin él se usa NodeRegistry.Instance.")]
        [SerializeField] NodeRegistry nodes;

        [Tooltip("Semilla del ruido de decisión (12.7).")]
        [SerializeField] int seed;

        readonly List<NodeController> snapshot = new List<NodeController>();
        System.Random random;
        long elapsedMicros;

        public EnemyProfile Profile => profile;

        // La última decisión: la acción elegida (null si ninguna superó el umbral) y si las reglas la
        // aceptaron. Para los tests y la depuración.
        public EnemyAction? LastAction { get; private set; }
        public bool LastExecuted { get; private set; }
        public int Cycles { get; private set; }

        // (acción elegida o null, ejecutada) al final de cada ciclo.
        public event Action<EnemyAction?, bool> OnDecided;

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            long interval = ToMicros(Profile != null ? profile.DecisionInterval : 0f);
            if (interval <= 0)
                throw new InvalidOperationException("EnemyController necesita un EnemyProfile con intervalo de decisión.");

            elapsedMicros += ToMicros(deltaTime);
            // Una sola decisión por tick aunque se acumulen intervalos: 12.7, una acción por ciclo.
            if (elapsedMicros < interval)
                return;
            elapsedMicros -= interval;
            Decide();
        }

        // Un ciclo de 12.1 ahora mismo. Devuelve la acción elegida, o null si ninguna supera el umbral.
        public EnemyAction? Decide()
        {
            NodeRegistry registry = Registry;
            if (registry == null)
                throw new InvalidOperationException("EnemyController necesita NodeRegistry.");

            snapshot.Clear();
            snapshot.AddRange(registry.All);
            EnemyBoard board = EnemyBoard.Capture(Issuer.Faction, snapshot, Balance, powers);

            List<EnemyAction> candidates = EnemyEvaluator.Candidates(board, profile, Scoring);
            EnemyAction? chosen = EnemyEvaluator.Pick(candidates, profile, Random);
            bool executed = chosen.HasValue && Issuer.Execute(chosen.Value, snapshot);

            Cycles++;
            LastAction = chosen;
            LastExecuted = executed;
            OnDecided?.Invoke(chosen, executed);
            return chosen;
        }

        // Vuelve a empezar el ruido con otra semilla, y el reloj desde 0.
        public void Reseed(int newSeed)
        {
            seed = newSeed;
            random = new System.Random(seed);
            elapsedMicros = 0;
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(EnemyProfile enemyProfile, EnemyScoring enemyScoring, BalanceConfig config,
                              EnemyCommandIssuer commandIssuer, IEnumerable<PowerDefinition> powerDefinitions,
                              NodeRegistry registry = null, int noiseSeed = 0)
        {
            profile = enemyProfile;
            scoring = enemyScoring;
            balance = config;
            issuer = commandIssuer;
            powers = powerDefinitions != null ? new List<PowerDefinition>(powerDefinitions).ToArray() : Array.Empty<PowerDefinition>();
            nodes = registry;
            Reseed(noiseSeed);
        }

        static long ToMicros(float seconds) => (long)Math.Round(seconds * MicrosPerSecond);

        System.Random Random => random ?? (random = new System.Random(seed));
        NodeRegistry Registry => nodes != null ? nodes : NodeRegistry.Instance;
        EnemyScoring Scoring => scoring != null ? scoring : throw new InvalidOperationException("EnemyController necesita EnemyScoring.");
        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("EnemyController necesita BalanceConfig.");
        EnemyCommandIssuer Issuer => issuer != null ? issuer : throw new InvalidOperationException("EnemyController necesita EnemyCommandIssuer.");
    }
}
