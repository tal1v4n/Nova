using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Gameplay;
using Nova.Systems;
using UnityEngine;

namespace Nova.Input
{
    // INodeCommandIssuer del Jugador (15.7). No decide nada: pasa cada comando al mismo servicio
    // que usará el Enemigo, en nombre de la facción Jugador (3.1).
    [DisallowMultipleComponent]
    public sealed class PlayerCommandIssuer : MonoBehaviour, INodeCommandIssuer
    {
        [SerializeField] ProtonLauncher launcher;

        public Faction Faction => Faction.Player;

        public LaunchBlock Send(NodeController source, NodeController target, bool fullSend)
        {
            if (launcher == null)
                throw new InvalidOperationException("PlayerCommandIssuer necesita un ProtonLauncher.");
            return launcher.TryLaunch(source, target, Faction, fullSend, out _);
        }

        public ActionCheck Ignite(NodeController node, NodeType targetType)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (!node.TryGetComponent(out NodeIgnition ignition))
                throw new InvalidOperationException($"{node.name} no tiene NodeIgnition.");
            return ignition.TryIgnite(Faction, targetType);
        }

        public ActionCheck Upgrade(NodeController node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            return node.TryUpgrade(Faction);
        }

        // Con varios orígenes seleccionados, cada uno lanza su propio protón con su propio cálculo
        // (5.2). Los que no pueden enviar se ignoran en silencio (17, caso 22). Devuelve cuántos salieron.
        public int SendFromEach(IReadOnlyList<NodeController> sources, NodeController target, bool fullSend)
        {
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));

            // Copia: un envío no cambia la selección hoy, pero la lista es de otro componente.
            var origins = new List<NodeController>(sources);
            int launched = 0;
            foreach (NodeController source in origins)
            {
                if (source != null && Send(source, target, fullSend) == LaunchBlock.None)
                    launched++;
            }
            return launched;
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(ProtonLauncher protonLauncher)
        {
            launcher = protonLauncher;
        }
    }
}
