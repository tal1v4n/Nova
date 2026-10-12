using System;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Systems
{
    // Por qué no sale un protón. None = sale.
    public enum LaunchBlock
    {
        None = 0,
        NotOwner,       // Solo el dueño envía desde su nodo; Neutral no envía nunca
        IsAsteroid,     // 5.2: cualquier nodo con Energía puede enviar, excepto el Asteroide
        SameNode,       // Origen y destino son el mismo nodo
        NothingToSend,  // 5.2: el 50 % de 1 redondea a 0 y el envío no se ejecuta (17, caso 22)
        Converting,     // 8.1: durante la reconversión el nodo no envía
    }

    // Servicio de lanzamiento (15.6): valida, descuenta la Energía del origen y pide un protón
    // al pool. El envío es instantáneo en el origen (5.2): la Energía sale en el mismo tick.
    [DisallowMultipleComponent]
    public sealed class ProtonLauncher : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;
        [SerializeField] ProtonPool pool;

        [Tooltip("Opcional en tests. En partida, los protones se registran para ordenar sus impactos (15.10).")]
        [SerializeField] ProtonRegistry registry;

        // Validación sin efectos: si el envío saldría y con cuánta Carga.
        public static LaunchBlock Check(NodeController source, NodeController target, Faction actor, bool fullSend, out int charge)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            charge = 0;
            if (actor == Faction.Neutral || source.Faction != actor)
                return LaunchBlock.NotOwner;
            if (source.Type == NodeType.Asteroid)
                return LaunchBlock.IsAsteroid;
            if (source == target)
                return LaunchBlock.SameNode;
            if (source.HasState(NodeStateFlags.Converting))
                return LaunchBlock.Converting;

            charge = EnergyMath.SendAmount(source.Energy, fullSend);
            return charge > 0 ? LaunchBlock.None : LaunchBlock.NothingToSend;
        }

        // Envía el 50 % o, con fullSend, el 100 % de la Energía del origen (5.2).
        // Con varios orígenes seleccionados se llama una vez por origen.
        public LaunchBlock TryLaunch(NodeController source, NodeController target, Faction actor, bool fullSend, out Proton proton)
        {
            proton = null;
            LaunchBlock block = Check(source, target, actor, fullSend, out int charge);
            if (block != LaunchBlock.None)
                return block;

            if (balance == null || pool == null)
                throw new InvalidOperationException("ProtonLauncher necesita BalanceConfig y ProtonPool.");

            source.SubtractEnergy(charge);
            proton = pool.Get();
            proton.Launch(source.transform.position, target, source.Faction, charge, balance);
            if (registry != null)
                registry.Register(proton);
            return LaunchBlock.None;
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(BalanceConfig config, ProtonPool protonPool, ProtonRegistry protonRegistry = null)
        {
            balance = config;
            pool = protonPool;
            registry = protonRegistry;
        }
    }
}
