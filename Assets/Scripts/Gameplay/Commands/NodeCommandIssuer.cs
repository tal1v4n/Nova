using System;
using Nova.Core;
using Nova.Systems;
using UnityEngine;

namespace Nova.Gameplay
{
    // Implementación común de INodeCommandIssuer (3.1, 15.1 #5, T4-03). El Jugador y el Enemigo
    // derivan de aquí y solo cambian la facción en cuyo nombre actúan, así que la paridad de 3.1 no
    // es una promesa sino la misma línea de código: ninguno de los dos tiene un comando que el otro
    // no tenga. No decide nada: cada comando pasa al componente que lo valida con las reglas de Core.
    public abstract class NodeCommandIssuer : MonoBehaviour, INodeCommandIssuer
    {
        [SerializeField] ProtonLauncher launcher;

        public abstract Faction Faction { get; }

        public LaunchBlock Send(NodeController source, NodeController target, bool fullSend)
        {
            if (launcher == null)
                throw new InvalidOperationException($"{GetType().Name} necesita un ProtonLauncher.");
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

        public ActionCheck Convert(NodeController node, NodeType targetType)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (!node.TryGetComponent(out NodeConverter converter))
                throw new InvalidOperationException($"{node.name} no tiene NodeConverter.");
            return converter.TryConvert(Faction, targetType);
        }

        public ActionCheck ChargePower(NodeController node, PowerType power)
        {
            return Caster(node).TryCharge(Faction, power);
        }

        public ActionCheck CancelCharge(NodeController node)
        {
            return Caster(node).TryCancel(Faction);
        }

        public ActionCheck LaunchPower(NodeController node, NodeController target)
        {
            return Caster(node).TryLaunch(Faction, target);
        }

        // Para la escena de prueba y los tests: el cableado normal es por el Inspector.
        public void Configure(ProtonLauncher protonLauncher)
        {
            launcher = protonLauncher;
        }

        static PulsarCaster Caster(NodeController node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (!node.TryGetComponent(out PulsarCaster caster))
                throw new InvalidOperationException($"{node.name} no tiene PulsarCaster.");
            return caster;
        }
    }
}
