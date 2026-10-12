using System;
using System.Globalization;
using Nova.Core;
using UnityEngine;

namespace Nova.Data
{
    // Textos de la UI (T.7 #39): el juego es solo en español, pero ningún texto se escribe a mano
    // en un script o un prefab. Una sola instancia: Assets/Data/UiText.asset. Si algún día hay
    // otro idioma, es otro asset con los mismos campos.
    //
    // Los formatos usan el punto decimal de 13.4 (`X.XX E/s`) con la cultura invariante, para no
    // depender de la configuración regional de la máquina.
    [CreateAssetMenu(menuName = "Nova/UI Text", fileName = "UiText")]
    public sealed class UiText : ScriptableObject
    {
        [Header("Tipos de nodo")]
        [SerializeField] string asteroid = "Asteroide";
        [SerializeField] string star = "Estrella";
        [SerializeField] string pulsar = "Púlsar";
        [SerializeField] string magnetar = "Magnetar";

        [Header("Panel de clic derecho (13.3, 13.4)")]
        [Tooltip("{0} tipo, {1} nivel.")]
        [SerializeField] string titleFormat = "{0} · Nivel {1}";

        [Tooltip("{0} Energía, {1} Tope.")]
        [SerializeField] string energyFormat = "Energía {0} / {1}";

        [Tooltip("{0} Energía por segundo de la Estrella.")]
        [SerializeField] string generationFormat = "Generación: {0:0.00} E/s";

        [Tooltip("{0} disparos por segundo del Magnetar.")]
        [SerializeField] string fireRateFormat = "Cadencia: {0:0.0} disparos/s";

        [Tooltip("{0} drenaje del poder que carga o tiene cargado el Púlsar (T.7 #49).")]
        [SerializeField] string chargeFormat = "Carga: {0:0.00} E/s";

        [SerializeField] string sleeping = "DORMIDO";

        [Tooltip("{0} coste de encendido.")]
        [SerializeField] string ignitionCostFormat = "Encender: {0}";

        [Tooltip("{0} coste de la mejora.")]
        [SerializeField] string upgradeFormat = "Mejorar · {0}";

        [SerializeField] string maxLevel = "NIVEL MÁXIMO";

        [Tooltip("Rótulo de la fila de reconversión (13.3 punto 5).")]
        [SerializeField] string convertHeader = "Reconvertir";

        [Tooltip("{0} tipo de destino, {1} coste.")]
        [SerializeField] string convertFormat = "{0} · {1}";

        [Tooltip("{0} tipo de destino. Sustituye a los dos botones mientras dura la reconversión.")]
        [SerializeField] string convertingFormat = "Reconvirtiendo a {0}";

        [Header("Poderes y rueda del Púlsar (7.2, 13.5)")]
        [SerializeField] string absoluteZero = "Cero Absoluto";
        [SerializeField] string decay = "Decaimiento";
        [SerializeField] string wormhole = "Agujero de Gusano";

        [Tooltip("Rótulo de la rueda en reposo.")]
        [SerializeField] string powersHeader = "Poderes";

        [Tooltip("{0} poder, {1} porcentaje cargado.")]
        [SerializeField] string chargingFormat = "{0} · {1}%";

        [Tooltip("{0} poder. Carga parada sin Energía o congelada (7.1).")]
        [SerializeField] string pausedFormat = "{0} · pausado";

        [Tooltip("{0} poder cargado al 100 %: su ranura brilla.")]
        [SerializeField] string readyFormat = "{0} listo";

        [Tooltip("{0} poder. En modo apuntado.")]
        [SerializeField] string aimingFormat = "{0}: elige objetivo";

        [Tooltip("{0} número de ranura (1 a 3), {1} coste.")]
        [SerializeField] string slotFormat = "{0} · {1}";

        [Tooltip("{0} nivel requerido. Ranura con candado (13.5).")]
        [SerializeField] string slotLockedFormat = "Nv {0}";

        [Tooltip("{0} Energía que devuelve (7.1).")]
        [SerializeField] string cancelFormat = "Cancelar · +{0}";

        public string TypeName(NodeType type)
        {
            switch (type)
            {
                case NodeType.Asteroid: return asteroid;
                case NodeType.Star: return star;
                case NodeType.Pulsar: return pulsar;
                case NodeType.Magnetar: return magnetar;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public string Title(NodeType type, int level) => Format(titleFormat, TypeName(type), level);
        public string EnergyLine(int energy, int cap) => Format(energyFormat, energy, cap);
        public string Generation(float energyPerSecond) => Format(generationFormat, energyPerSecond);
        public string FireRate(float shotsPerSecond) => Format(fireRateFormat, shotsPerSecond);
        public string Charge(float energyPerSecond) => Format(chargeFormat, energyPerSecond);
        public string Sleeping => sleeping;
        public string IgnitionCost(int cost) => Format(ignitionCostFormat, cost);
        // El coste llega ya escrito: el panel lo pinta en rojo cuando no se alcanza (13.3).
        public string Upgrade(string cost) => Format(upgradeFormat, cost);
        public string MaxLevel => maxLevel;
        public string ConvertHeader => convertHeader;
        // Como en Upgrade, el coste llega ya escrito para poder pintarlo en rojo.
        public string Convert(NodeType target, string cost) => Format(convertFormat, TypeName(target), cost);
        public string Converting(NodeType target) => Format(convertingFormat, TypeName(target));

        public string PowerName(PowerType power)
        {
            switch (power)
            {
                case PowerType.AbsoluteZero: return absoluteZero;
                case PowerType.Decay: return decay;
                case PowerType.Wormhole: return wormhole;
                default: throw new ArgumentOutOfRangeException(nameof(power), power, null);
            }
        }

        public string PowersHeader => powersHeader;
        public string Charging(PowerType power, int percent) => Format(chargingFormat, PowerName(power), percent);
        public string Paused(PowerType power) => Format(pausedFormat, PowerName(power));
        public string Ready(PowerType power) => Format(readyFormat, PowerName(power));
        public string Aiming(PowerType power) => Format(aimingFormat, PowerName(power));
        // Sin rojo: empezar a cargar no exige Energía (desviación 191).
        public string Slot(int number, int cost) => Format(slotFormat, number, cost);
        public string SlotLocked(int requiredLevel) => Format(slotLockedFormat, requiredLevel);
        public string Cancel(int refund) => Format(cancelFormat, refund);

        static string Format(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
    }
}
