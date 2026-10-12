using System;
using Nova.Core;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Input
{
    // Modo apuntado (13.5, T3-07): qué Púlsar apunta y para qué bando. Lo abre InputRouter con un
    // clic en la ranura brillante de la rueda —nunca solo al completarse la carga (desviación
    // 209)— y lo cierra al lanzar, con clic derecho o con Esc. Las vistas lo leen para atenuar lo
    // que no es objetivo y ocultar las líneas de envío.
    //
    // Se cierra solo cuando el Púlsar deja de tener el poder listo o deja de ser de quien apunta:
    // al lanzar, al cancelar la carga o si se lo capturan.
    [DisallowMultipleComponent]
    public sealed class PowerAimState : MonoBehaviour
    {
        NodeController pulsar;
        PulsarCaster caster;
        Faction launcher;

        public bool IsAiming => pulsar != null;
        public NodeController Pulsar => pulsar;
        public Faction Launcher => launcher;

        // Empieza o termina el apuntado.
        public event Action OnChanged;

        // Empieza a apuntar con el poder listo de node. Devuelve false, sin cambiar nada, si node no
        // es un Púlsar de actor con un poder cargado al 100 %.
        public bool Begin(NodeController node, Faction actor)
        {
            if (node == null || !node.IsInitialized || node.Faction != actor)
                return false;
            if (!node.TryGetComponent(out PulsarCaster nodeCaster) || nodeCaster.State != PowerChargeState.Ready)
                return false;
            if (node == pulsar)
                return true;

            Unwatch();
            pulsar = node;
            caster = nodeCaster;
            launcher = actor;
            caster.OnStateChanged += HandleChargeChanged;
            pulsar.OnFactionChanged += HandleFactionChanged;
            OnChanged?.Invoke();
            return true;
        }

        public void End()
        {
            if (!IsAiming)
                return;
            Unwatch();
            OnChanged?.Invoke();
        }

        // 7.2.1: lo que se resalta y se puede clicar en modo apuntado. Fuera de él, nada.
        public bool IsValidTarget(NodeController target)
        {
            return IsAiming && target != null && target.IsInitialized
                && PowerRules.IsValidTarget(target.Snapshot(), launcher);
        }

        void HandleChargeChanged(PowerChargeState previous, PowerChargeState current)
        {
            if (current != PowerChargeState.Ready)
                End();
        }

        void HandleFactionChanged(Faction previous, Faction current)
        {
            if (current != launcher)
                End();
        }

        void Unwatch()
        {
            if (caster != null)
                caster.OnStateChanged -= HandleChargeChanged;
            if (pulsar != null)
                pulsar.OnFactionChanged -= HandleFactionChanged;
            pulsar = null;
            caster = null;
        }

        void OnDestroy()
        {
            Unwatch();
        }
    }
}
