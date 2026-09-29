using System;
using System.Collections;
using System.Collections.Generic;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Systems
{
    // Registro de protones en vuelo, indexado por destino (15.6). Lo usan ThreatCalculator y el
    // Magnetar. Singleton de 15.1 #6.
    //
    // Además ordena los impactos de un mismo tick (15.10): los protones avanzan en su FixedUpdate,
    // pero al llegar no resuelven, sino que esperan aquí. Tras todos los FixedUpdate del tick se
    // resuelven por orden de llegada dentro del tick; con empate exacto, primero el de mayor Carga,
    // y si también empatan, el que se lanzó antes. El segundo opera sobre el resultado del primero
    // (17, caso 1).
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class ProtonRegistry : MonoBehaviour
    {
        public static ProtonRegistry Instance { get; private set; }

        static readonly IReadOnlyList<Proton> None = Array.Empty<Proton>();

        readonly List<Proton> inFlight = new List<Proton>();
        readonly Dictionary<NodeController, List<Proton>> byTarget = new Dictionary<NodeController, List<Proton>>();
        readonly Dictionary<Proton, long> launchOrder = new Dictionary<Proton, long>();
        readonly List<Proton> arrivals = new List<Proton>();
        long nextLaunch;

        // En orden de lanzamiento.
        public IReadOnlyList<Proton> InFlight => inFlight;
        public int Count => inFlight.Count;
        public int PendingArrivals => arrivals.Count;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Nova] Hay dos ProtonRegistry en escena; se descarta el segundo.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            StartCoroutine(ResolveAfterFixedUpdate());
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // Protones que vuelan hacia ese nodo, en orden de lanzamiento.
        public IReadOnlyList<Proton> GetIncoming(NodeController target)
        {
            return target != null && byTarget.TryGetValue(target, out List<Proton> list) ? list : None;
        }

        public void Register(Proton proton)
        {
            if (proton == null)
                throw new ArgumentNullException(nameof(proton));
            if (!proton.InFlight)
                throw new InvalidOperationException("Solo se registran protones en vuelo.");
            if (launchOrder.ContainsKey(proton))
                return;

            launchOrder.Add(proton, nextLaunch++);
            inFlight.Add(proton);
            if (!byTarget.TryGetValue(proton.Target, out List<Proton> list))
            {
                list = new List<Proton>();
                byTarget.Add(proton.Target, list);
            }
            list.Add(proton);

            proton.Registry = this;
            proton.OnFinished += Unregister;
        }

        // Lo llama el protón al llegar a su destino.
        internal void QueueArrival(Proton proton)
        {
            if (!launchOrder.ContainsKey(proton))
                throw new InvalidOperationException("Ese protón no está registrado.");
            if (!arrivals.Contains(proton))
                arrivals.Add(proton);
        }

        // Resuelve los impactos pendientes del tick en el orden de 15.10.
        public void ResolveArrivals()
        {
            if (arrivals.Count == 0)
                return;

            var ordered = new List<Proton>(arrivals);
            arrivals.Clear();
            ordered.Sort(CompareArrivals);

            foreach (Proton proton in ordered)
            {
                if (proton.InFlight)
                    proton.Impact();
            }
        }

        int CompareArrivals(Proton a, Proton b)
        {
            int byTime = a.ArrivalOffsetMicros.CompareTo(b.ArrivalOffsetMicros);
            if (byTime != 0)
                return byTime;
            int byCharge = b.Charge.CompareTo(a.Charge);
            if (byCharge != 0)
                return byCharge;
            return launchOrder[a].CompareTo(launchOrder[b]);
        }

        void Unregister(Proton proton)
        {
            proton.OnFinished -= Unregister;
            proton.Registry = null;
            arrivals.Remove(proton);
            inFlight.Remove(proton);
            launchOrder.Remove(proton);

            NodeController target = proton.Target;
            if (target != null && byTarget.TryGetValue(target, out List<Proton> list))
            {
                list.Remove(proton);
                if (list.Count == 0)
                    byTarget.Remove(target);
            }
        }

        IEnumerator ResolveAfterFixedUpdate()
        {
            var wait = new WaitForFixedUpdate();
            while (true)
            {
                yield return wait;
                ResolveArrivals();
            }
        }
    }
}
