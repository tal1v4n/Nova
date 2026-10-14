using System;
using Nova.Core;
using Nova.Data;
using Nova.Gameplay;
using UnityEngine;

namespace Nova.Systems
{
    // Un protón en vuelo (6.1, 15.6): movimiento, Carga y notificación de impacto.
    // No resuelve el impacto: se lo pide a ImpactResolver y lo aplica el nodo destino.
    // Avanza en el tick fijo de 50 Hz (15.10), en línea recta y a velocidad constante.
    //
    // La duración del vuelo se fija al lanzar, en microsegundos enteros, y la posición se
    // interpola: sumar 0.05 u por tick en floats podía retrasar la llegada un tick.
    //
    // Registrado en ProtonRegistry, al llegar no resuelve: espera a que el registro ordene los
    // impactos del tick (15.10). Sin registro, resuelve en el acto.
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class Proton : MonoBehaviour
    {
        const double MicrosPerSecond = 1_000_000d;

        NodeController target;
        BalanceConfig balance;
        Vector2 origin;
        long flightMicros;
        long elapsedMicros;
        Faction faction;
        int charge;
        bool inFlight;

        public NodeController Target => target;

        // Nodo del que salió, si lo lanzó ProtonLauncher. Lo lee la defensa del Enemigo para congelar
        // el origen del ataque (12.4, T4-05). Puede haber cambiado de bando desde el lanzamiento.
        public NodeController Source { get; internal set; }
        // Facción del emisor en el lanzamiento; no cambia aunque el origen cambie de bando (6.1).
        public Faction Faction => faction;
        public int Charge => charge;
        public bool InFlight => inFlight;

        // (protón, destino, resultado). Se emite justo después de aplicar el impacto.
        public event Action<Proton, NodeController, ImpactOutcome> OnImpact;

        // Fin del vuelo por cualquier motivo, con Target todavía asignado.
        public event Action<Proton> OnFinished;

        // Derribado por un Magnetar (6.3): no llega, no impacta y su Energía se pierde. Se emite
        // justo antes de OnFinished, con el protón todavía en su sitio.
        public event Action<Proton> OnShotDown;

        // Desvanecido al ganar el Jugador (17, caso 21): no llega ni impacta. Se emite justo antes de
        // OnFinished, con el protón todavía en su sitio.
        public event Action<Proton> OnVanished;

        // Microsegundos dentro del tick en que llegó: el criterio de orden de 15.10.
        public long ArrivalOffsetMicros { get; private set; }

        // Lo asigna ProtonPool: a dónde vuelve el protón al terminar.
        internal ProtonPool Pool { get; set; }

        // Lo asigna ProtonRegistry al registrarlo.
        internal ProtonRegistry Registry { get; set; }

        public void Launch(Vector2 from, NodeController destination, Faction emitter, int protonCharge, BalanceConfig config)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (protonCharge < 1)
                throw new ArgumentOutOfRangeException(nameof(protonCharge), protonCharge, "Un protón lleva al menos 1 de Carga.");

            if (config.ProtonSpeed <= 0f)
                throw new InvalidOperationException("BalanceConfig.ProtonSpeed debe ser positiva.");

            origin = from;
            Source = null;
            transform.position = new Vector3(from.x, from.y, transform.position.z);
            target = destination;
            double distance = Vector2.Distance(from, destination.transform.position);
            flightMicros = (long)Math.Round(distance / config.ProtonSpeed * MicrosPerSecond);
            elapsedMicros = 0;
            ArrivalOffsetMicros = 0;
            faction = emitter;
            charge = protonCharge;
            balance = config;
            inFlight = true;
        }

        void FixedUpdate()
        {
            Tick(Time.fixedDeltaTime);
        }

        // Un tick de vuelo: avanza y, si llega, se pone en la cola del registro o resuelve.
        public void Tick(float deltaTime)
        {
            if (!inFlight || !Step(deltaTime))
                return;

            if (Registry != null)
                Registry.QueueArrival(this);
            else
                Impact();
        }

        // Avanza un tick hacia el centro del destino. Devuelve true si ha llegado.
        public bool Step(float deltaTime)
        {
            if (!inFlight)
                return false;
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "El tiempo no retrocede.");

            long remaining = flightMicros - elapsedMicros;
            long tick = (long)Math.Round(deltaTime * MicrosPerSecond);
            if (remaining > 0 && remaining <= tick)
                ArrivalOffsetMicros = remaining;

            elapsedMicros = Math.Min(elapsedMicros + tick, flightMicros);
            float t = flightMicros > 0 ? (float)((double)elapsedMicros / flightMicros) : 1f;
            Vector2 next = Vector2.Lerp(origin, target.transform.position, t);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            return elapsedMicros >= flightMicros;
        }

        // Resuelve contra el destino tal como está al llegar: la facción se comprueba en el
        // impacto, no en el lanzamiento (17, caso 2). Después el protón vuelve al pool.
        public void Impact()
        {
            if (!inFlight)
                throw new InvalidOperationException("El protón no está en vuelo.");

            ImpactOutcome outcome = ImpactResolver.Resolve(
                new ProtonSnapshot(faction, charge), target.Snapshot(), balance.MagnetarResistance);

            NodeController destination = target;
            destination.ApplyImpact(outcome);
            Finish();
            OnImpact?.Invoke(this, destination, outcome);
        }

        // Un disparo de Magnetar (6.3): resta damage de Carga y, si llega a 0, el protón
        // desaparece sin impactar (17, caso 6). Devuelve true si lo derriba.
        public bool TakeShot(int damage)
        {
            if (!inFlight)
                throw new InvalidOperationException("El protón no está en vuelo.");
            if (damage < 0)
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "Un disparo no suma Carga.");

            charge = Math.Max(0, charge - damage);
            if (charge > 0)
                return false;

            OnShotDown?.Invoke(this);
            Finish();
            return true;
        }

        // El Enemigo pierde su último nodo con este protón en vuelo (17, caso 21): desaparece sin
        // impactar. Lo llama WinConditionChecker al cumplirse la victoria (T5-03).
        public void Vanish()
        {
            if (!inFlight)
                throw new InvalidOperationException("El protón no está en vuelo.");

            OnVanished?.Invoke(this);
            Finish();
        }

        void Finish()
        {
            inFlight = false;
            OnFinished?.Invoke(this);
            target = null;
            balance = null;
            Source = null;

            if (Pool != null)
                Pool.Release(this);
        }
    }
}
