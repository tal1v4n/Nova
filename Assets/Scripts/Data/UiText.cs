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

        [Header("Niveles y mapas (11.1)")]
        [Tooltip("Uno por mapa, del 1 al 3.")]
        [SerializeField] string[] mapNames = { "Sistema Interior", "El Cinturón", "Espacio Profundo" };

        [Tooltip("{0} mapa, {1} nivel, {2} nombre del mapa en mayúsculas.")]
        [SerializeField] string levelCaptionFormat = "MAPA {0} · NIVEL {1} · {2}";

        [Tooltip("{0} nombre del mapa, {1} nivel.")]
        [SerializeField] string levelNameFormat = "{0} · {1}";

        [Tooltip("{0} minutos, {1} segundos.")]
        [SerializeField] string clockFormat = "{0}:{1:00}";

        [SerializeField] string conversionMechanic = "Reconversión";

        [Header("Menú de pausa (13.10)")]
        [SerializeField] string pauseTitle = "PAUSA";
        [SerializeField] string pauseSubtitle = "EL SISTEMA ESTÁ DETENIDO · NADA SE MUEVE";
        [SerializeField] string thisLevelHeader = "ESTE NIVEL";
        [SerializeField] string winCondition = "Ganas cuando el Enemigo no tenga ningún nodo.";
        [SerializeField] string loseCondition = "Pierdes sin nodos, o sin Estrella y sin forma de volver a tener una.";
        [SerializeField] string availableHeader = "DISPONIBLE";

        [Tooltip("{0} duración objetivo, {1} tiempo de juego.")]
        [SerializeField] string pauseTimeFormat = "OBJETIVO {0} · VAS {1}";

        [SerializeField] string resume = "CONTINUAR";
        [SerializeField] string restartLevel = "REINICIAR NIVEL";
        [SerializeField] string backToMap = "VOLVER AL MAPA";
        [SerializeField] string confirmRestart = "¿Reiniciar desde cero? Esta partida no se guarda.";
        [SerializeField] string confirmRestartAction = "REINICIAR";
        [SerializeField] string confirmBackToMap = "¿Volver al mapa? Esta partida no se guarda.";
        [SerializeField] string confirmBackToMapAction = "VOLVER";
        [SerializeField] string cancelConfirm = "CANCELAR";
        [SerializeField] string controlsHeader = "CONTROLES";

        [Tooltip("La tecla que lleva el botón Continuar.")]
        [SerializeField] string escapeKey = "Esc";

        [Tooltip("Resumen de 13.1: tecla o gesto, y lo que hace. Mismo número de elementos en las dos listas.")]
        [SerializeField] string[] controlKeys = { "Clic izq.", "Doble izq.", "Arrastre der.", "Arrastre izq.", "Clic der.", "Izq. Asteroide", "Esc" };
        [SerializeField] string[] controlActions =
        {
            "Seleccionar · con selección, <color=#3FD7F5>enviar 50 %</color>", "Enviar <color=#3FD7F5>100 %</color>", "Enviar 50 % desde un solo nodo", "Lazo de selección",
            "Panel del nodo · en vacío, cancelar", "Encender: Estrella, Púlsar o Magnetar", "Cerrar, cancelar apuntado, pausa",
        };

        [Header("Pantallas de fin (13.11)")]
        [SerializeField] string victoryTitle = "SISTEMA CONQUISTADO";
        [SerializeField] string defeatTitle = "SEÑAL PERDIDA";
        [SerializeField] string annihilation = "ANIQUILACIÓN";
        [SerializeField] string stalemate = "ESTANCAMIENTO";
        [SerializeField] string timeStat = "TIEMPO";

        [Tooltip("{0} diferencia con la duración objetivo.")]
        [SerializeField] string underTargetFormat = "{0} bajo el objetivo";
        [SerializeField] string overTargetFormat = "{0} sobre el objetivo";

        [SerializeField] string nodesStat = "NODOS AL FINAL";

        [Tooltip("{0} nodos del Jugador, {1} nodos del nivel.")]
        [SerializeField] string nodesFormat = "{0} / {1}";

        [Tooltip("{0} nodos que llegaron por la cascada de conquista (10.1).")]
        [SerializeField] string cascadeFormat = "{0} por la cascada";

        [SerializeField] string peakStat = "PICO DE ENERGÍA";

        [Tooltip("{0} momento del pico.")]
        [SerializeField] string peakWhenFormat = "a los {0}";

        [SerializeField] string capturesStat = "CAPTURAS";

        [Tooltip("{0} capturas perdidas.")]
        [SerializeField] string lostFormat = "{0} perdidas";

        [SerializeField] string curveHeader = "CURVA DE PODER";

        [Tooltip("Rótulo de la curva en la victoria, que tiene más sitio.")]
        [SerializeField] string curveVictoryHeader = "CURVA DE PODER · LA BARRA INFERIOR, A LO LARGO DE LA PARTIDA";

        [SerializeField] string legendPlayer = "Tú";
        [SerializeField] string legendEnemy = "Enemigo";

        [Tooltip("{0} momento desde el que el Jugador tiene más de la mitad y se queda así.")]
        [SerializeField] string holdMarkFormat = "{0} · el control cambia de manos";

        [Tooltip("{0} momento de la proporción más alta del Jugador.")]
        [SerializeField] string bestMarkFormat = "{0} · tu mejor momento";

        [SerializeField] string unlocked = "DESBLOQUEADO";

        [Tooltip("{0} número del siguiente nivel.")]
        [SerializeField] string nextLevelFormat = "Nivel {0}";

        [SerializeField] string allConquered = "LOS NUEVE SISTEMAS CONQUISTADOS";

        [Tooltip("{0} nodos del Jugador al empezar el siguiente nivel, {1} cuántos tiene el Enemigo.")]
        [SerializeField] string nextStartFormat = "Empiezas con {0} contra {1} nodos.";

        [Tooltip("{0} lista de nodos del Enemigo al empezar el siguiente nivel.")]
        [SerializeField] string nextEnemyFormat = "Enemigo: {0}";

        [Tooltip("{0} lista de neutrales especiales (11.2): los que no son Asteroides.")]
        [SerializeField] string nextNeutralsFormat = "Neutrales: {0}";

        [Tooltip("{0} tipo, {1} nivel, {2} repeticiones (solo si son más de una).")]
        [SerializeField] string nodeEntryFormat = "{0}({1})";
        [SerializeField] string repeatedFormat = "{0}×{1}";

        [SerializeField] string next = "SIGUIENTE";
        [SerializeField] string repeat = "REPETIR";
        [SerializeField] string retry = "REINTENTAR";
        [SerializeField] string whyHeader = "POR QUÉ";

        [Tooltip("{0} Energía que le quedaba al Jugador.")]
        [SerializeField] string stalemateWhyFormat = "Sin Estrella y con {0} de Energía, ya no podías volver a generar.";

        [SerializeField] string annihilationLine = "No te queda ningún nodo.";

        [Tooltip("{0} momento en que cayó el último nodo.")]
        [SerializeField] string annihilationWhyFormat = "Tu último nodo cayó a las {0}.";

        [SerializeField] string routesHeader = "LAS CINCO VÍAS PARA VOLVER A TENER UNA ESTRELLA";

        [Tooltip("Las cinco vías de 10.2, en el orden de StarRouteKind.")]
        [SerializeField] string[] routeNames =
        {
            "Encender un Asteroide propio", "Capturar un Asteroide y encenderlo", "Capturar una Estrella",
            "Reconvertir un nodo", "Robar una Estrella con el Gusano",
        };

        [Tooltip("{0} coste, {1} lo que falta.")]
        [SerializeField] string routeShortFormat = "Cuesta {0} · faltan {1}";
        [SerializeField] string routeNoneOwned = "No tienes ninguno";
        [SerializeField] string routeNoneLeft = "No queda ninguno";

        [Tooltip("{0} número del mapa.")]
        [SerializeField] string routeNotInMapFormat = "No existe en el mapa {0}";

        [Tooltip("Una línea de consejo por mapa, del 1 al 3, para la derrota.")]
        [SerializeField] string[] hints =
        {
            "Pista: el Asteroide más cercano es el primero que hay que encender.",
            "Pista: a partir del mapa 2, no dependas de una sola Estrella.",
            "Pista: el Agujero de Gusano también se lleva un Magnetar neutral entero.",
        };

        [Header("Menú principal (13.8)")]
        [SerializeField] string tagline = "CAPTURA · ENCIENDE · CONQUISTA";
        [SerializeField] string gameTitle = "NOVA";
        [SerializeField] string play = "JUGAR";
        [SerializeField] string continueGame = "CONTINUAR";

        [Tooltip("Debajo de Continuar: {0} mapa, {1} nivel, {2} nombre del mapa.")]
        [SerializeField] string continueFormat = "Mapa {0} · Nivel {1} · {2}";

        [SerializeField] string starMap = "MAPA ESTELAR";
        [SerializeField] string quit = "SALIR";
        [SerializeField] string legendNeutral = "Neutral";

        [Tooltip("La versión en el pie: {0} es Application.version.")]
        [SerializeField] string versionFormat = "v{0}";

        [Header("Carta estelar (13.9)")]
        [SerializeField] string backToMenu = "MENÚ";
        [SerializeField] string starChart = "CARTA ESTELAR";

        [Tooltip("{0} niveles ganados.")]
        [SerializeField] string conqueredFormat = "{0} / 9 conquistados";

        [SerializeField] string mapCapFormat = "MAPA 0{0}";

        [Tooltip("{0} y {1} primer y último nivel; {2} y {3} sus duraciones.")]
        // Guion y no raya: Jersey 10 no tiene «–».
        [SerializeField] string mapRangeFormat = "Niveles {0}-{1} · {2} a {3}";

        [SerializeField] string nodeLevelFormat = "NIVEL {0}";
        [SerializeField] string nextTag = "SIGUIENTE";

        [Tooltip("{0} mapa, {1} su nombre en mayúsculas.")]
        [SerializeField] string reportMapFormat = "MAPA {0} · {1}";

        [SerializeField] string targetDurationFormat = "Duración objetivo {0}";
        [SerializeField] string introducedFormat = "Nuevo: {0}";

        [Tooltip("Las mecánicas que introduce cada mapa (11.1), del 1 al 3.")]
        [SerializeField] string[] mapIntros =
        {
            "envío, captura, Estrella, Magnetar y mejoras",
            "Púlsar, Cero Absoluto, Decaimiento y Magnetar neutral",
            "Agujero de Gusano, reconversión y un algoritmo agresivo",
        };

        [SerializeField] string sideYou = "TÚ";
        [SerializeField] string sideEnemy = "ENEMIGO";
        [SerializeField] string sideNeutrals = "NEUTRALES";

        [Tooltip("El nivel de un nodo en el informe: N2.")]
        [SerializeField] string unitLevelFormat = "N{0}";

        [SerializeField] string unitCountFormat = "×{0}";
        [SerializeField] string ignitionHeader = "ENCENDER";
        [SerializeField] string pulsarPowersHeader = "PODERES DEL PÚLSAR";

        [Tooltip("{0} es Sí o No.")]
        [SerializeField] string conversionHeaderFormat = "RECONVERSIÓN · {0}";

        [SerializeField] string yes = "SÍ";
        [SerializeField] string no = "NO";
        [SerializeField] string launch = "LANZAR";
        [SerializeField] string locked = "BLOQUEADO";

        [Tooltip("{0} es el nivel que hay que ganar para desbloquearlo.")]
        [SerializeField] string winToUnlockFormat = "Gana el nivel {0}";

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

        public string MapName(int map) => Pick(mapNames, map - 1, nameof(map));
        public string LevelCaption(int map, int level) => Format(levelCaptionFormat, map, level, MapName(map).ToUpperInvariant());
        public string LevelName(int map, int level) => Format(levelNameFormat, MapName(map), level);
        public string ConversionMechanic => conversionMechanic;

        // Tiempo de juego como m:ss, por debajo del segundo hacia abajo.
        public string Clock(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return Format(clockFormat, whole / 60, whole % 60);
        }

        public string PauseTitle => pauseTitle;
        public string PauseSubtitle => pauseSubtitle;
        public string ThisLevelHeader => thisLevelHeader;
        public string WinCondition => winCondition;
        public string LoseCondition => loseCondition;
        public string AvailableHeader => availableHeader;
        public string PauseTime(float target, float elapsed) => Format(pauseTimeFormat, Clock(target), Clock(elapsed));
        public string Resume => resume;
        public string RestartLevel => restartLevel;
        public string BackToMap => backToMap;
        public string ConfirmRestart => confirmRestart;
        public string ConfirmRestartAction => confirmRestartAction;
        public string ConfirmBackToMap => confirmBackToMap;
        public string ConfirmBackToMapAction => confirmBackToMapAction;
        public string CancelConfirm => cancelConfirm;
        public string ControlsHeader => controlsHeader;
        public string EscapeKey => escapeKey;
        public int ControlCount => Mathf.Min(controlKeys.Length, controlActions.Length);
        public string ControlKey(int index) => Pick(controlKeys, index, nameof(index));
        public string ControlAction(int index) => Pick(controlActions, index, nameof(index));

        public string VictoryTitle => victoryTitle;
        public string DefeatTitle => defeatTitle;
        public string Cause(LevelOutcome outcome) => outcome == LevelOutcome.Stalemate ? stalemate : annihilation;
        public string TimeStat => timeStat;

        // Diferencia con la duración objetivo: por debajo o por encima.
        public string TargetDelta(float elapsed, float target) =>
            elapsed <= target ? Format(underTargetFormat, Clock(target - elapsed)) : Format(overTargetFormat, Clock(elapsed - target));

        public string NodesStat => nodesStat;
        public string Nodes(int own, int total) => Format(nodesFormat, own, total);
        public string Cascade(int count) => Format(cascadeFormat, count);
        public string PeakStat => peakStat;
        public string PeakWhen(float seconds) => Format(peakWhenFormat, Clock(seconds));
        public string CapturesStat => capturesStat;
        public string Lost(int count) => Format(lostFormat, count);
        public string CurveHeader => curveHeader;
        public string CurveVictoryHeader => curveVictoryHeader;
        public string LegendPlayer => legendPlayer;
        public string LegendEnemy => legendEnemy;
        public string NextStart(string playerNodes, int enemyNodes) => Format(nextStartFormat, playerNodes, enemyNodes);
        public string AnnihilationLine => annihilationLine;
        public string HoldMark(float seconds) => Format(holdMarkFormat, Clock(seconds));
        public string BestMark(float seconds) => Format(bestMarkFormat, Clock(seconds));
        public string Unlocked => unlocked;
        public string NextLevel(int number) => Format(nextLevelFormat, number);
        public string AllConquered => allConquered;
        public string NextEnemy(string nodes) => Format(nextEnemyFormat, nodes);
        public string NextNeutrals(string nodes) => Format(nextNeutralsFormat, nodes);

        // «Estrella(4)×2», como las columnas de 11.2.
        public string NodeEntry(NodeType type, int level, int count)
        {
            string entry = Format(nodeEntryFormat, TypeName(type), level);
            return count > 1 ? Format(repeatedFormat, entry, count) : entry;
        }

        public string Next => next;
        public string Repeat => repeat;
        public string Retry => retry;
        public string WhyHeader => whyHeader;
        public string StalemateWhy(int energy) => Format(stalemateWhyFormat, energy);
        public string AnnihilationWhy(float seconds) => Format(annihilationWhyFormat, Clock(seconds));
        public string RoutesHeader => routesHeader;
        public string RouteName(StarRouteKind kind) => Pick(routeNames, (int)kind, nameof(kind));

        // Por qué una vía no alcanza (13.11); vacío si alcanza.
        public string RouteWhy(StarRoute route, int map)
        {
            switch (route.Block)
            {
                case StarRouteBlock.None: return string.Empty;
                case StarRouteBlock.Short: return Format(routeShortFormat, route.Cost, route.Missing);
                case StarRouteBlock.NoneOwned: return routeNoneOwned;
                case StarRouteBlock.NoneLeft: return routeNoneLeft;
                case StarRouteBlock.NotInMap: return Format(routeNotInMapFormat, map);
                default: throw new ArgumentOutOfRangeException(nameof(route), route.Block, null);
            }
        }

        public string Hint(int map) => Pick(hints, map - 1, nameof(map));

        public string Tagline => tagline;
        public string GameTitle => gameTitle;
        public string Play => play;
        public string ContinueGame => continueGame;
        public string ContinueLine(int map, int level) => Format(continueFormat, map, level, MapName(map));
        public string StarMap => starMap;
        public string Quit => quit;
        public string LegendNeutral => legendNeutral;
        public string Version(string version) => Format(versionFormat, version);

        public string BackToMenu => backToMenu;
        public string StarChart => starChart;
        public string ConqueredCount(int conquered) => Format(conqueredFormat, conquered);
        public string MapCap(int map) => Format(mapCapFormat, map);
        public string MapRange(int first, int last, float firstDuration, float lastDuration) =>
            Format(mapRangeFormat, first, last, Clock(firstDuration), Clock(lastDuration));
        public string NodeLevel(int level) => Format(nodeLevelFormat, level);
        public string NextTag => nextTag;
        public string ReportMap(int map) => Format(reportMapFormat, map, MapName(map).ToUpperInvariant());
        public string TargetDuration(float seconds) => Format(targetDurationFormat, Clock(seconds));
        public string Introduced(int map) => Format(introducedFormat, Pick(mapIntros, map - 1, nameof(map)));
        public string SideYou => sideYou;
        public string SideEnemy => sideEnemy;
        public string SideNeutrals => sideNeutrals;
        public string UnitLevel(int level) => Format(unitLevelFormat, level);
        public string UnitCount(int count) => Format(unitCountFormat, count);
        public string IgnitionHeader => ignitionHeader;
        public string PulsarPowersHeader => pulsarPowersHeader;
        // El valor llega ya escrito: la carta lo pinta en gris cuando el mapa no la permite.
        public string ConversionHeader(string value) => Format(conversionHeaderFormat, value);
        public string YesNo(bool value) => value ? yes : no;
        public string Launch => launch;
        public string Locked => locked;
        public string WinToUnlock(int level) => Format(winToUnlockFormat, level);

        static string Pick(string[] values, int index, string parameter)
        {
            if (values == null || index < 0 || index >= values.Length)
                throw new ArgumentOutOfRangeException(parameter, index, "UiText no tiene ese texto.");
            return values[index];
        }

        static string Format(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
    }
}
