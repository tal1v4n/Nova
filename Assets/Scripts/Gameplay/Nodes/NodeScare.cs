using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Data;
using Nova.Systems;
using UnityEngine;

namespace Nova.Gameplay
{
    // Estado Asustado (14.2): en cada tick fijo pregunta a ThreatCalculator si algún protón hostil
    // letal llega antes del umbral de susto, y pone o quita la bandera Scared. Vale para cualquier
    // nodo, de cualquier facción. No tiene efecto de juego: solo avisa.
    //
    // Corre a 0, como los demás componentes de nodo: ve los protones donde quedaron en el tick
    // anterior, antes de que vuelen (+10), y los impactos de ese tick ya resueltos. Así el susto
    // empieza y termina un tick (20 ms) después del hecho que lo provoca.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NodeController))]
    public sealed class NodeScare : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;

        [Tooltip("Opcional: sin él se usa ProtonRegistry.Instance.")]
        [SerializeField] ProtonRegistry protons;

        readonly List<IncomingProton> incoming = new List<IncomingProton>();
        NodeController node;

        public bool IsScared => Node.HasState(NodeStateFlags.Scared);

        void FixedUpdate()
        {
            Tick();
        }

        // Evalúa la condición de 14.2 y actualiza la bandera. Sin registro, nada asusta.
        public void Tick()
        {
            Node.SetStateFlag(NodeStateFlags.Scared, Evaluate());
        }

        // La condición de 14.2 ahora mismo, sin efectos.
        public bool Evaluate()
        {
            ProtonRegistry registry = Registry;
            if (!Node.IsInitialized || registry == null)
                return false;

            CollectIncoming(registry);
            if (incoming.Count == 0)
                return false;

            BalanceConfig config = Balance;
            return ThreatCalculator.IsScared(Node.Snapshot(), incoming,
                config.MagnetarResistance, config.ProtonSpeed, config.ScareThreshold);
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config, ProtonRegistry registry = null)
        {
            balance = config;
            protons = registry;
        }

        void CollectIncoming(ProtonRegistry registry)
        {
            incoming.Clear();
            Vector2 center = transform.position;
            IReadOnlyList<Proton> list = registry.GetIncoming(Node);
            for (int i = 0; i < list.Count; i++)
            {
                Proton proton = list[i];
                if (proton == null || !proton.InFlight)
                    continue;
                float distance = Vector2.Distance(proton.transform.position, center);
                incoming.Add(new IncomingProton(proton.Faction, proton.Charge, distance));
            }
        }

        ProtonRegistry Registry => protons != null ? protons : ProtonRegistry.Instance;

        BalanceConfig Balance => balance != null ? balance : throw new InvalidOperationException("NodeScare necesita BalanceConfig.");

        // Se resuelve a demanda y no en Awake, que no se ejecuta en los tests de EditMode.
        NodeController Node => node != null ? node : node = GetComponent<NodeController>();
    }
}
