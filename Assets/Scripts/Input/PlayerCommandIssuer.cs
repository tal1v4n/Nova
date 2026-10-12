using System;
using System.Collections.Generic;
using Nova.Core;
using Nova.Gameplay;
using Nova.Systems;
using UnityEngine;

namespace Nova.Input
{
    // INodeCommandIssuer del Jugador (15.7). No decide nada: los comandos son los de NodeCommandIssuer,
    // los mismos que usa el Enemigo, en nombre de la facción Jugador (3.1, T4-03).
    [DisallowMultipleComponent]
    public sealed class PlayerCommandIssuer : NodeCommandIssuer
    {
        public override Faction Faction => Faction.Player;

        // Con varios orígenes seleccionados, cada uno lanza su propio protón con su propio cálculo
        // (5.2). Los que no pueden enviar se ignoran en silencio (17, caso 22). Devuelve cuántos salieron.
        // Es una comodidad de la selección múltiple, no un comando: son varios Send.
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
    }
}
