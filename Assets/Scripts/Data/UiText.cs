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
        public string Sleeping => sleeping;
        public string IgnitionCost(int cost) => Format(ignitionCostFormat, cost);
        // El coste llega ya escrito: el panel lo pinta en rojo cuando no se alcanza (13.3).
        public string Upgrade(string cost) => Format(upgradeFormat, cost);
        public string MaxLevel => maxLevel;
        public string ConvertHeader => convertHeader;
        // Como en Upgrade, el coste llega ya escrito para poder pintarlo en rojo.
        public string Convert(NodeType target, string cost) => Format(convertFormat, TypeName(target), cost);
        public string Converting(NodeType target) => Format(convertingFormat, TypeName(target));

        static string Format(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
    }
}
