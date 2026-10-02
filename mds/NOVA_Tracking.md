# NOVA — Tracking de desarrollo

> Parte de la especificación de **NOVA**. Documento maestro: [NOVA_GameDesign_Spec.md](./NOVA_GameDesign_Spec.md).
> **Sección T.** Este archivo es el estado vivo del proyecto. Se actualiza en cada tarea completada.
>
> Cubre **solo desarrollo**. El arte se sigue en [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md) y no se planifica aquí.

---

## T.1 Prompt de continuación

Pega esto en un chat nuevo junto con los siete archivos `.md` del proyecto:

```
Estoy desarrollando NOVA, un juego 2D en Unity. Toda la especificación está en
los archivos adjuntos:

- NOVA_GameDesign_Spec.md       reglas, economía, poderes, niveles, UI, arquitectura
- NOVA_Nodos.md                 los 4 tipos de nodo
- NOVA_Algoritmo_Enemigo.md     el algoritmo que controla al Enemigo
- NOVA_Estados_Animaciones.md   estados y animaciones
- NOVA_Assets_Diseno.md         inventario de arte y su estado
- NOVA_Arte_Tecnica.md          contrato del pipeline: primitivas y reglas
- NOVA_Tracking.md              estado del desarrollo (este es el que manda)

El arte se genera con Python (NumPy + Pillow). Las primitivas estan en
tools/nova_core.py, la paleta en tools/nova_paleta.py y un generador por
propuesta en tools/gen_<nodo>_v<n>.py. Los PNG viven en
art/nodes/<nodo>/v<n>/ y son salida derivada: no se editan a mano. Un nodo
puede tener varias versiones conviviendo; la elegida la marca la columna
"Archivos" de NOVA_Assets_Diseno.md, no la carpeta.

Tu tarea:
0. Mira T.7. Hay decisiones abiertas. Si la tarea que toca depende de una
   decision abierta, dilo antes de empezar. T.8 (escala del tablero) quedo
   CERRADA el 2026-09-28 en 15 x 8.4375 u (Pixel Perfect 960x540, x2 a
   1080p); no la reabras.
   El proyecto es Unity 6.6 (6000.6.0f1), plantilla Universal 2D v7.0.0,
   PC Windows 1080p, Input System nuevo y CERO post-procesado.
1. Lee T.3 y T.4 de NOVA_Tracking.md y dime cuál es la SIGUIENTE tarea pendiente,
   respetando el orden de fases y las dependencias.
2. Antes de proponerla, comprueba su columna "Arte" en T.4. Si depende de un
   ART-* que no está ✅ en NOVA_Assets_Diseno.md, NO la propongas: decláralo
   BLOQUEADA, dime exactamente qué asset falta, y pasa a la siguiente tarea
   desbloqueada. Nunca sustituyas arte pendiente por arte improvisado.
3. Implementa esa tarea completa: los scripts de C# que pide, respetando la
   arquitectura de la sección 15 y el glosario de la sección 2.
4. Al terminar, dame la línea actualizada de T.4 y del registro T.5 para que
   yo la pegue en el archivo.

Reglas:
- Una sola tarea por respuesta. No adelantes trabajo de tareas posteriores.
- No inventes reglas de juego. Si la especificación no cubre un caso, para y
  pregúntame.
- El Enemigo lo controla un ALGORITMO determinista, no un modelo entrenado. No
  escribas "IA" para referirte a el: se llama "el algoritmo" o "el Enemigo".
  En este proyecto "IA" eres tu, el asistente que lee esto.
- Todo número de balance va en un ScriptableObject, nunca escrito en el código.
- Nombra las clases y variables exactamente como el glosario de la sección 2.

Empieza diciéndome cuál es la siguiente tarea y por qué.
```

---

## T.2 Reglas del ciclo de trabajo

| # | Regla |
|---|---|
| 1 | **Una tarea por sesión.** Terminar, marcar ✅ y actualizar T.5 antes de abrir la siguiente. |
| 2 | **El orden de fases es obligatorio.** No se empieza una fase sin cumplir el criterio de salida de la anterior (T.6). Dentro de una fase, se respetan las dependencias de T.4. |
| 3 | **Bloqueo por arte.** Una tarea con `ART-*` en su columna *Arte* solo se inicia si ese asset está ✅ en [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md). Si no lo está, se marca 🔴 BLOQUEADA y se salta. |
| 4 | **Primitivas en fases 1–5.** Círculos de color y texto plano son válidos para probar lógica. No cuentan como arte y no se marcan ✅ en el inventario. |
| 5 | **Fase 6 es solo integración de arte.** Si su asset falta, la tarea no existe todavía. No se improvisa. |
| 6 | **La especificación manda.** Si el código y el `.md` se contradicen, se corrige el código. Si el `.md` está mal, se corrige el `.md` primero y se anota en T.5. |
| 7 | **Definición de hecho:** compila sin errores, sus tests pasan, no rompe los criterios de salida anteriores, y T.4 + T.5 están actualizados. |

Estados: ⬜ pendiente · 🟨 en curso · ✅ hecha · 🔴 bloqueada.

---

## T.3 Estado global

| Fase | Nombre | Tareas | ✅ | Estado | Criterio de salida |
|---|---|---|---|---|---|
| 0 | Proyecto | 6 | 6 | ✅ | El proyecto abre en Unity, `Nova.Core` compila sin referenciar `UnityEngine` y un test vacío corre en EditMode |
| 1 | Núcleo jugable | 14 | 14 | ✅ | Capturar un Asteroide, encenderlo como Estrella y tomar un nodo enemigo |
| 2 | Tipos y defensa | 7 | 1 | 🟨 | Los cuatro tipos funcionan y el nivel 3 es jugable |
| 3 | Poderes | 8 | 0 | ⬜ | Los tres poderes funcionan y pasan los casos 8–14 y 27–35 de la sección 17 |
| 4 | Algoritmo del Enemigo | 6 | 0 | ⬜ | El algoritmo del mapa 3 vence a un jugador descuidado |
| 5 | Flujo y contenido | 7 | 0 | ⬜ | Los 9 niveles son completables de principio a fin |
| 6 | Presentación | 11 | 0 | 🔴 | 46 de 106 assets listos. **`T6-01`, `T6-02` y `T6-03` ya no están bloqueadas por arte**; las otras ocho sí ([A.7](./NOVA_Assets_Diseno.md#a7-resumen-de-progreso)) |
| | **Total** | **59** | **21** | | |

**Siguiente tarea:** `T2-03`, mejoras de nivel y `NodeInfoPanel` de clic derecho, con el botón deshabilitado y el coste en rojo ([13.3](./NOVA_GameDesign_Spec.md#133-panel-de-clic-derecho), [13.4](./NOVA_GameDesign_Spec.md#134-traducción-de-velocidad-de-ataque)). Antes hay que decidir [T.7](#t7-pendientes-abiertos) #39, el idioma: el panel es la primera UI con texto de verdad. Ya existe la intención `InputRouter.OnInfoRequested` (desviación 137) y la validación `NodeController.CheckUpgrade` (`T1-07`).

---

## T.4 Tareas

`Arte` = dependencia bloqueante de [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md). `Ref` = sección de la especificación que define la tarea.

### Fase 0 — Proyecto

Creada el **2026-09-28**. `T.4` empezaba en `T1-01` dando por hecho un proyecto Unity con carpetas, *assembly definitions* y cámara, y **no existía ninguno**: el repositorio solo tenía `tools/`, `art/` y `mds/`. Ninguna de estas seis tareas depende de arte.

**`T0-03` no es opcional.** El principio 4 de [15.1](./NOVA_GameDesign_Spec.md#151-principios) exige que `ImpactResolver`, `EnergyMath` y `ConversionRules` sean testeables sin abrir Unity, y eso solo se cumple si `Nova.Core` es un ensamblado que **no referencia `UnityEngine`**. Sin esa separación, `T1-02` ya no puede cumplir su propia definición de hecho ([T.2](#t2-reglas-del-ciclo-de-trabajo) #7).

| ID | Tarea | Ref | Depende de | Arte | Estado |
|---|---|---|---|---|---|
| T0-01 | Crear el proyecto en Unity **6.6 (6000.6.0f1)** con la plantilla **Universal 2D v7.0.0**. Player Settings: nombre, compañía, plataforma **PC Windows**, resolución **1920×1080**, color de fondo `#0B0E1A`. Quitar todo post-procesado del *2D Renderer* | [1.2](./NOVA_GameDesign_Spec.md#12-alcance-cerrado) | — | No | ✅ |
| T0-02 | Estructura de carpetas de [15.2](./NOVA_GameDesign_Spec.md#152-estructura-de-carpetas), las cuatro escenas vacías, `.gitignore` de Unity, *Asset Serialization: Force Text* y *Visible Meta Files* | [15.2](./NOVA_GameDesign_Spec.md#152-estructura-de-carpetas) | T0-01 | No | ✅ |
| T0-03 | *Assembly definitions*: `Nova.Core` **sin referencia a `UnityEngine`**, más `Nova.Data`, `Nova.Gameplay`, `Nova.Enemy`, `Nova.Input`, `Nova.Systems`, `Nova.UI`, `Nova.Presentation` y `Nova.Tests` (EditMode, con Unity Test Framework). Las referencias entre ellas respetan la regla de oro de [15.9](./NOVA_GameDesign_Spec.md#159-comunicación-entre-capas): **ninguna flecha apunta hacia arriba** | [15.1](./NOVA_GameDesign_Spec.md#151-principios), [15.9](./NOVA_GameDesign_Spec.md#159-comunicación-entre-capas) | T0-02 | No | ✅ |
| T0-04 | Cámara con **Pixel Perfect Camera**: PPU 64, resolución de referencia **960×540**, `size = 4.21875` (×2 exacto a 1080p). *Upscale Render Texture* apagado, *Pixel Snapping* encendido | [2](./NOVA_GameDesign_Spec.md#2-glosario-normativo), [T.8](#t8-escala-del-tablero--cerrada) | T0-01 | No | ✅ |
| T0-05 | Puente `art/` → `Assets/Sprites/`: script de copia y un `.preset` de importación con *Point*, *Compression None*, PPU 64 y pivote. `Assets/Sprites/` es carpeta generada y no se edita a mano ([NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), *Reglas de trabajo*) | [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md) | T0-02 | No | ✅ |
| T0-06 | TextMeshPro con **Jersey 10** (atlas *Raster* a 56 pt, 3 px de atlas por píxel de diseño), importada una sola vez para números de nodo, paneles y menús | [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md) | T0-02 | No | ✅ |

### Fase 1 — Núcleo jugable

| ID | Tarea | Ref | Depende de | Arte | Estado |
|---|---|---|---|---|---|
| T1-01 | Enums de `Core`: `Faction`, `NodeType`, `NodeState`, `PowerType`, `ImpactResult` | [15.3](./NOVA_GameDesign_Spec.md#153-core--reglas-puras-sin-monobehaviour) | — | No | ✅ |
| T1-02 | `EnergyMath` + tests: multiplicadores, redondeos, coste efectivo de captura | [6.2](./NOVA_GameDesign_Spec.md#62-resolución-de-impacto), [6.4](./NOVA_GameDesign_Spec.md#64-casos-de-captura-por-tipo) | T1-01 | No | ✅ |
| T1-03 | `ImpactResolver` + tests: absorción, daño, captura, resistencia ×2 | [6.2](./NOVA_GameDesign_Spec.md#62-resolución-de-impacto) | T1-02 | No | ✅ |
| T1-04 | `ConversionRules` + tests: validez y coste de encendido, mejora, reconversión | [8](./NOVA_GameDesign_Spec.md#8-reconversión), [16.2](./NOVA_GameDesign_Spec.md#162-costes-de-nivel) | T1-01 | No | ✅ |
| T1-05 | ScriptableObjects `NodeDefinition`, `NodeLevelData`, `BalanceConfig` + assets con los valores de la sección 16 | [15.4](./NOVA_GameDesign_Spec.md#154-data--scriptableobjects), [16](./NOVA_GameDesign_Spec.md#16-tablas-de-balance-consolidadas) | T1-01 | No | ✅ |
| T1-06 | `NodeController` + `NodeIdentity`, `NodeOwnership`, `NodeEnergy` | [15.5](./NOVA_GameDesign_Spec.md#155-gameplay--componentes-de-nodo) | T1-05 | No | ✅ |
| T1-06b | **Vista provisional** de nodo: círculo coloreado por facción (cian `#3FD7F5`, magenta `#F2456B`, gris `#8A93A6`) con su Energía encima en Jersey 10. Solo lee eventos de `NodeController`. Primitiva de la regla 4 de [T.2](#t2-reglas-del-ciclo-de-trabajo): desechable, la sustituye `NodeVisual` en `T6-01` | [T.2](#t2-reglas-del-ciclo-de-trabajo) #4 | T1-06, T0-06 | No | ✅ |
| T1-07 | `NodeLevelComponent` y `NodeStateMachine` (solo `Normal`, `Dormido`, `SinEncender`) | [14.1](./NOVA_Estados_Animaciones.md#141-estados-del-nodo) | T1-06 | No | ✅ |
| T1-08 | `StarGenerator`: +1 de Energía por intervalo, tope 50, parada al llenarse | [4.2](./NOVA_Nodos.md#42-estrella) | T1-06 | No | ✅ |
| T1-09 | `ProtonLauncher`, `Proton`, `ProtonPool`: envío 50 % / 100 %, velocidad 2.5 u/s | [5.2](./NOVA_GameDesign_Spec.md#52-envío), [6.1](./NOVA_GameDesign_Spec.md#61-el-protón) | T1-03, T1-06 | No | ✅ |
| T1-10 | `NodeRegistry` y `ProtonRegistry` + orden de ejecución de 15.10 | [15.6](./NOVA_GameDesign_Spec.md#156-systems--escena), [15.10](./NOVA_GameDesign_Spec.md#1510-orden-de-ejecución) | T1-06 | No | ✅ |
| T1-11 | `InputRouter`, `SelectionManager`, `PlayerCommandIssuer` con buffer de doble clic de 0.25 s | [13.1](./NOVA_GameDesign_Spec.md#131-ratón), [13.2](./NOVA_GameDesign_Spec.md#132-resolución-del-doble-clic) | T1-09 | No | ✅ |
| T1-11b | `NodeIgnition`: Asteroide propio con 10 de Energía → nivel 1 con 0. En esta fase **solo a Estrella**: `MagnetarTurret` es T2-04 y `PulsarCaster` es T3-02, así que los otros dos tipos todavía no existen. Sin menú radial —ese es `T2-02`—: el clic izquierdo enciende a Estrella directamente | [4.1](./NOVA_Nodos.md#41-asteroide-nivel-0) | T1-04, T1-11 | No | ✅ |
| T1-12 | Escena de prueba con 3 nodos: capturar, encender y tomar un nodo enemigo | — | T1-11b | No | ✅ |

### Fase 2 — Tipos y defensa

| ID | Tarea | Ref | Depende de | Arte | Estado |
|---|---|---|---|---|---|
| T2-02 | `IgnitionMenu`: menú radial de 3 opciones en el cursor, abierto por clic izquierdo sobre Asteroide propio | [13.7](./NOVA_GameDesign_Spec.md#137-menú-radial-de-encendido) | T1-11b | No | ✅ |
| T2-03 | Mejoras de nivel y `NodeInfoPanel` de clic derecho, con botón deshabilitado y coste en rojo | [13.3](./NOVA_GameDesign_Spec.md#133-panel-de-clic-derecho), [13.4](./NOVA_GameDesign_Spec.md#134-traducción-de-velocidad-de-ataque) | T1-11b | No | ⬜ |
| T2-04 | `MagnetarTurret`: radio, cadencia, objetivo más cercano al centro, −2 de Carga | [4.4](./NOVA_Nodos.md#44-magnetar), [6.3](./NOVA_GameDesign_Spec.md#63-efecto-del-magnetar-sobre-protones) | T1-12 | No | ⬜ |
| T2-05 | Resistencia ×2 en impactos hostiles y 1:1 en refuerzos aliados | [5.3](./NOVA_GameDesign_Spec.md#53-recepción), [6.2](./NOVA_GameDesign_Spec.md#62-resolución-de-impacto) | T2-04 | No | ⬜ |
| T2-06 | Magnetar neutral disparando a ambos bandos | [3.2](./NOVA_GameDesign_Spec.md#32-nota-sobre-el-magnetar-neutral), [4.4](./NOVA_Nodos.md#44-magnetar) | T2-04 | No | ⬜ |
| T2-07 | `NodeConverter`: reconversión de 3 s con ventana de vulnerabilidad | [8.1](./NOVA_GameDesign_Spec.md#81-reglas) | T1-04, T2-03 | No | ⬜ |
| T2-08 | `ThreatCalculator` + estado `Asustado` | [14.2](./NOVA_Estados_Animaciones.md#142-animación-de-susto) | T1-10 | No | ⬜ |

### Fase 3 — Poderes

| ID | Tarea | Ref | Depende de | Arte | Estado |
|---|---|---|---|---|---|
| T3-01 | `PowerDefinition` + assets de los tres poderes | [16.3](./NOVA_GameDesign_Spec.md#163-poderes) | T2-08 | No | ⬜ |
| T3-02 | `PulsarCaster`: carga acoplada a la Energía, pausa, cancelación con devolución del 50 % | [7.1](./NOVA_GameDesign_Spec.md#71-regla-de-carga) | T3-01 | No | ⬜ |
| T3-03 | `PowerBeam`: haz de 0.25 s, **alcance global**, no interceptable, revalidación del objetivo al terminar | [7.2.2](./NOVA_GameDesign_Spec.md#722-el-haz) | T3-02 | No | ⬜ |
| T3-03b | Filtro de objetivo válido: **nodo encendido no propio**. Incluye el Magnetar neutral; excluye todo Asteroide | [7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido) | T3-03 | No | ⬜ |
| T3-04 | Cero Absoluto: congelación, deshielo invertido, auto-deshielo de 30 s | [7.3](./NOVA_GameDesign_Spec.md#73-cero-absoluto) | T3-03 | No | ⬜ |
| T3-05 | Decaimiento: −2 E/s, permanencia en 0, cura, no acumulable | [7.4](./NOVA_GameDesign_Spec.md#74-decaimiento) | T3-03 | No | ⬜ |
| T3-06 | Agujero de Gusano: captura íntegra, ignora ×2, robo de carga en sus 3 ventanas | [7.5](./NOVA_GameDesign_Spec.md#75-agujero-de-gusano) | T3-03 | No | ⬜ |
| T3-07 | `PulsarPowerWheel` + barra de carga visible también en el Púlsar enemigo | [13.5](./NOVA_GameDesign_Spec.md#135-rueda-de-poderes-del-púlsar) | T3-06 | No | ⬜ |

### Fase 4 — Algoritmo del Enemigo

| ID | Tarea | Ref | Depende de | Arte | Estado |
|---|---|---|---|---|---|
| T4-01 | `EnemyProfile` + los tres assets de perfil | [12.5](./NOVA_Algoritmo_Enemigo.md#125-perfiles-por-mapa) | T3-07 | No | ⬜ |
| T4-02 | `EnemyEvaluator` + los **9** tests de 12.7, incluida `ScorePoder` | [12.3](./NOVA_Algoritmo_Enemigo.md#123-función-de-puntuación-atacar), [12.3.1](./NOVA_Algoritmo_Enemigo.md#1231-función-de-puntuación-lanzar-poder), [12.7](./NOVA_Algoritmo_Enemigo.md#127-contrato-de-implementación) | T4-01 | No | ⬜ |
| T4-03 | `EnemyCommandIssuer` sobre `INodeCommandIssuer` | [12.7](./NOVA_Algoritmo_Enemigo.md#127-contrato-de-implementación), [15.1](./NOVA_GameDesign_Spec.md#151-principios) | T4-01 | No | ⬜ |
| T4-04 | `EnemyController`: ciclo de decisión, una acción por ciclo, ruido con semilla | [12.1](./NOVA_Algoritmo_Enemigo.md#121-ciclo-de-decisión), [12.2](./NOVA_Algoritmo_Enemigo.md#122-acciones-candidatas) | T4-02, T4-03 | No | ⬜ |
| T4-05 | `EnemyThreatResponder`: refuerzo, congelación y evacuación | [12.4](./NOVA_Algoritmo_Enemigo.md#124-detección-de-amenaza-y-defensa) | T4-04 | No | ⬜ |
| T4-06 | Verificación de las prohibiciones de 12.6 | [12.6](./NOVA_Algoritmo_Enemigo.md#126-prohibiciones) | T4-05 | No | ⬜ |

### Fase 5 — Flujo y contenido

| ID | Tarea | Ref | Depende de | Arte | Estado |
|---|---|---|---|---|---|
| T5-01 | `LevelDefinition`, `NodeSpawnData` y `LevelLoader` | [15.4](./NOVA_GameDesign_Spec.md#154-data--scriptableobjects) | T4-06 | No | ⬜ |
| T5-02 | Los 9 assets de nivel con la tabla de 11.2 | [11.2](./NOVA_GameDesign_Spec.md#112-diseño-de-niveles) | T5-01 | No | ⬜ |
| T5-03 | `WinConditionChecker`: victoria, aniquilación, estancamiento, cascada de conquista | [10](./NOVA_GameDesign_Spec.md#10-condiciones-de-victoria-y-derrota) | T5-01 | No | ⬜ |
| T5-04 | `PowerUpSpawner` + `FactionBuffs` con el calendario de 9.3 | [9](./NOVA_GameDesign_Spec.md#9-power-ups-del-cielo) | T5-01 | No | ⬜ |
| T5-05 | `GameManager`, pausa, reinicio de nivel, `EndLevelScreen` | [10.3](./NOVA_GameDesign_Spec.md#103-fin-de-nivel) | T5-03 | No | ⬜ |
| T5-06 | `ProgressSaver` y pantalla de selección de nivel | [1.2](./NOVA_GameDesign_Spec.md#12-alcance-cerrado) | T5-05 | No | ⬜ |
| T5-07 | Suite de aceptación con los **35** casos de la sección 17 | [17](./NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución) | T5-06 | No | ⬜ |

### Fase 6 — Presentación 🔴

Ninguna tarea de esta fase puede iniciarse hoy. Se desbloquean una a una según avance [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md).

> **`ART-NOD-*` está completo: 34 de 34.** Los cuatro tipos de nodo tienen sus sprites, así que `T6-01` deja de estar 🔴 y pasa a ⬜: lo único que la retiene ya es su dependencia de código (`T5-07`), como cualquier otra tarea. Es la primera tarea de la fase 6 que existe de verdad.
>
> **`ART-ST-*` está completo: 9 de 9.** Con A.2 y A.3 cerradas, `T6-02` y `T6-03` dejan de estar bloqueadas por arte y pasan a ⬜: lo único que las retiene ya es la cadena de código `T6-01 → T6-02 → T6-03`. Son las tres primeras tareas de la fase 6 que existen de verdad.
>
> Las otras ocho siguen 🔴, y el orden en que se desbloquean lo marca [A.7](./NOVA_Assets_Diseno.md#a7-resumen-de-progreso). Lo siguiente que más desbloquea es A.4.1 (el radio del Magnetar, que abre `T6-05` y es además legibilidad de fase 2) y A.4.2, que ahora abre **dos** tareas: el protón abre `T6-04` y los tres haces de poder abren `T6-11`.
>
> **`T6-11` es nueva y no existía por un descuido, no por el cambio de reglas.** La integración del proyectil de poder tampoco tenía tarea cuando era proyectil: `T3-03` lo daba por hecho con Arte «No», que es cierto para la lógica y falso para la presentación. Al convertirse en haz —con estiramiento y rotación, que no hace ningún otro asset— el hueco se volvió imposible de ignorar.

| ID | Tarea | Ref | Depende de | Arte | Estado |
|---|---|---|---|---|---|
| T6-01 | `NodeVisual`: sprite por tipo, nivel y facción | [15.8](./NOVA_GameDesign_Spec.md#158-presentation) | T5-07 | `ART-NOD-*` **34/34 ✅** | ⬜ |
| T6-02 | `NodeAnimator`: estados de 14.1 | [14.1](./NOVA_Estados_Animaciones.md#141-estados-del-nodo) | T6-01 | `ART-ST-*` **9/9 ✅** | ⬜ |
| T6-03 | Animación de susto sobre los tres bandos | [14.2](./NOVA_Estados_Animaciones.md#142-animación-de-susto) | T6-02 | `ART-ST-SCARE` ✅ | ⬜ |
| T6-04 | `ProtonVisual`: número de Carga y escala | [14.3](./NOVA_Estados_Animaciones.md#143-protón) | T6-01 | `ART-WLD-PRO-*` | 🔴 |
| T6-05 | Radio del Magnetar en pantalla | [A.4.1](./NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar) | T6-01 | `ART-WLD-RAD-*` | 🔴 |
| T6-06 | Feedback de selección, lazo, arrastre y apuntado, con atenuación de objetivos no válidos | [13.1](./NOVA_GameDesign_Spec.md#131-ratón), [13.5](./NOVA_GameDesign_Spec.md#135-rueda-de-poderes-del-púlsar) | T6-01 | `ART-WLD-SEL`, `ART-WLD-LASSO`, `ART-WLD-LINK`, `ART-WLD-AIM` | 🔴 |
| T6-07 | `VFXPool` con las 15 animaciones puntuales | [14.5](./NOVA_Estados_Animaciones.md#145-animaciones-puntuales-sin-estado) | T6-02 | `ART-FX-*` (15) | 🔴 |
| T6-08 | `PowerBarUI` y HUD permanente | [13.6](./NOVA_GameDesign_Spec.md#136-hud-permanente) | T5-07 | `ART-UI-POWERBAR`, `ART-UI-TOPBAR` | 🔴 |
| T6-09 | Skin final de paneles, rueda de poderes y pantallas de fin | [13.3](./NOVA_GameDesign_Spec.md#133-panel-de-clic-derecho) | T6-08 | `ART-UI-*` (13) | 🔴 |
| T6-10 | `AudioDirector` y `CameraShake` | [15.8](./NOVA_GameDesign_Spec.md#158-presentation) | T6-07 | Audio (sin inventariar) | 🔴 |
| T6-11 | Haz de poder en pantalla: tres piezas, estiramiento y rotación | [A.4.2](./NOVA_Assets_Diseno.md#a42-protón-y-haces-de-poder--143), [7.2.2](./NOVA_GameDesign_Spec.md#722-el-haz) | T6-01 | `ART-WLD-BEAM-*` (3) | 🔴 |

---

## T.5 Registro

Una línea por tarea completada. La más reciente arriba.

| Fecha | Tarea | Resultado | Notas / desviaciones de la spec |
|---|---|---|---|
| 2026-10-02 | **T2-02** | `IgnitionMenuState` en `Nova.Input`: abierto o no, Asteroide, centro, actor y geometría —tres opciones a 1 u del cursor en el orden de 16.2, Estrella arriba, discos de 0.375 u de radio que son a la vez dibujo y blanco del clic—; se cierra solo si el Asteroide cambia de facción o de tipo. `InputRouter`: el clic sobre Asteroide propio sin selección **abre el menú** en el cursor, desplazado lo justo para no salirse de la cámara; un clic sobre una opción enciende ese tipo por `PlayerCommandIssuer.Ignite`; cualquier otro clic o arrastre lo cierra y sigue su curso; clic derecho y `Esc` solo lo cierran. `IgnitionMenu` en `Nova.UI` (a +50): tres discos con el coste dentro y el tipo por fuera, en cian si se puede, con el coste en rojo si falta Energía y en gris sin coste si el tipo no existe todavía; se repinta al cambiar la Energía. `NodeIgnition.Offers` y `ActionBlock.Unavailable`. `Nova.Utils.PrimitiveSprites` con el disco provisional, que `ProvisionalNodeView` pasa a usar. La escena de prueba gana el menú y sus nodos solo ofrecen la Estrella. `IgnitionMenuTests` (12 tests); `InputTests` cambia los 2 tests de encendido directo por 11 del menú, 14 casos; `IgnitionTests` cambia el error de configuración por 3 tests de disponibilidad; `Phase1CriterionTests` enciende por el menú | Suite completa en verde en el Test Runner de Unity y menú probado a mano en Play con la escena regenerada: se abre con el coste en rojo por debajo de 10, enciende a Estrella y enseña Púlsar y Magnetar en gris. Desviaciones 149–153 abajo |
| 2026-10-02 | Balance: **radios del Magnetar ×0.75 y cadencia ×4/3** | Decidido con el usuario. Radios `1.5 · 1.75 · 2.0 · 2.25 · 2.5 u` (96 a 160 px, todos exactos) y cadencia `1.33 · 1.87 · 2.53 · 3.33 · 4.27` disp/s, para conservar la Carga anulada de [4.4](./NOVA_Nodos.md#44-magnetar) con el protón a la misma velocidad: el radio menor por sí solo la recortaba un 30 %. `Magnetar.asset` y `BalanceDataTests` al día; el lienzo de los anillos de A.4.1 baja de 512 a 384 | T.7 #35 cerrada. Los 15 sprites `ART-NOD-MAG-*` no cambian (A.2.4). Desviación 149 abajo |
| 2026-10-02 | **T1-12** | Menú `Nova → Fase 1 → Generar escena de prueba` (`Assets/Scripts/Editor/Phase1TestScene.cs`, idempotente) que escribe `Assets/Scenes/Tests/Fase1.unity`: cámara Pixel Perfect de `T0-04` (reutiliza `ProjectSetup.ConfigureCamera`), objeto `Systems` con los dos registros, pool, lanzador, selección, emisor y router cableados, y tres nodos —Estrella del Jugador (30) en (−5, 0), Asteroide neutral (10) en (−3.5, 1.5), Estrella del Enemigo (10) en (5, 0)— con `NodeIgnition`, `StarGenerator`, trigger de 0.5 u en la capa `Nodes` y vista provisional. `PlacedNode` (`Systems/Scenes/`, a −90) inicializa y registra cada nodo al cargar. Vistas provisionales nuevas: `ProvisionalSelectionView` (anillo cian de 76 px detrás del nodo seleccionado) y `ProvisionalProtonView` (disco de 24 px del color de la facción emisora con su Carga); `ProvisionalNodeView` dibuja el Asteroide a 48 px y el nodo encendido a 64. `Phase1CriterionTests`: 5 tests, entre ellos el criterio de salida entero con clics simulados | Suite completa en verde en el Test Runner de Unity y **criterio de salida jugado a mano en Play** (evidencia: `mds/evidencias/evidencia1.png`, local y fuera de git): captura del Asteroide, encendido a Estrella con clic simple sin selección, lazo de las dos Estrellas y toma de la enemiga con doble clic; anillo de selección y protones visibles. **La fase 1 queda cerrada.** Los protones de 24 px se leen pequeños con el Game view a 0.51x; a 1080p real son 48 px y su sprite final es de A.4.2 (`T6-04`), así que no se tocan. Desviaciones 144–148 abajo |
| 2026-09-29 | **T1-11b** | `NodeIgnition` en `Assets/Scripts/Gameplay/Nodes/`: `Check` sin efectos y `TryIgnite`, con las reglas de `ConversionRules.CheckIgnition` (`T1-04`) y el coste de `BalanceConfig.IgnitionCost`; cobra el coste y llama a `NodeController.ChangeType(definición, 1)`, la mitad mecánica que reutilizará la reconversión (`T2-07`): nivel, tipo, Tope nuevo con recorte y estado de base. `NodeIdentity` emite `OnTypeChanged`, reenviado por `NodeController`; `Initialize` sigue sin eventos. `INodeCommandIssuer` gana `Ignite(nodo, tipo)` y `PlayerCommandIssuer` lo implementa. `InputRouter`: el clic izquierdo sobre un Asteroide propio sin selección **enciende a Estrella directamente**. `IgnitionTests`: 15 tests, 19 casos; `InputTests` pasa a 33 tests, 37 casos | Suite completa en verde en el Test Runner de Unity. Desviaciones 140–143 abajo |
| 2026-09-29 | **T1-11** | Tres scripts en `Assets/Scripts/Input/` (`Nova.Input`, que pasa a referenciar `Unity.InputSystem`) y el canal de comandos. `INodeCommandIssuer` en `Gameplay/Commands/` con `Faction` y `Send` (T.7 #42). `PlayerCommandIssuer`: `Send` por `ProtonLauncher` en nombre del Jugador y `SendFromEach`, un protón por origen y los que no llegan ignorados en silencio (17, caso 22). `SelectionManager`: solo orígenes legales (propio, no Asteroide), `Select`, `Deselect`, `Clear`, lazo `SelectInRect` que sustituye, y baja automática al cambiar de facción. `InputRouter` a −50: lee el ratón en `Update`, **encola y aplica en `FixedUpdate`**; clic, doble clic por buffer en microsegundos enteros, arrastre, lazo, clic derecho y `Esc` según 13.1, 13.2 y 18 #24–27; *picking* por `Physics2D.OverlapPoint` en la capa `Nodes` (18 #23). `BalanceConfig` gana `DragThreshold` = 4. `Nova.Editor.ProjectLayers` crea la capa `Nodes` en el índice 8 al cargar el editor. `InputTests`: 31 tests, 35 casos, y `RegistryTests` fija el −50 del router | Suite completa en verde en el Test Runner de Unity; la capa `Nodes` quedó creada en el índice 8 de `TagManager.asset`. Desviaciones 136–139 abajo |
| 2026-09-29 | Controles: **cuatro huecos de 13.1 y 13.2 cerrados antes de `T1-11`** | Decididos con el usuario. **Doble clic** solo sobre el mismo nodo: un clic en otro destino envía ya el 50% pendiente y abre buffer nuevo. **Arrastre** a partir de 4 px de la referencia (8 px a 1080p), envía al soltar y sin buffer. **Lazo** sustituye a la selección; **clic izquierdo en vacío** no hace nada. **Nodo capturado** sale solo de la selección; **doble clic sobre un nodo seleccionado** lo deselecciona y no envía | 13.1 y 13.2 reescritas, 16.1 gana *Umbral de arrastre*, [18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #24–27 nuevas. T.7 #42–48 abiertos en la misma revisión |
| 2026-09-29 | Revisión de coherencia antes de `T1-11` | Correcciones sin decisión de diseño: **15.2** deja de decir «sin UnityEngine salvo tipos básicos» y declara `Editor/` (`Nova.Editor`) y el `.asmref` de `Systems/`; **15.10** fija el orden por `[DefaultExecutionOrder]`, añade el tercer desempate y dice dónde se resuelven los impactos; **17** caso 1 con el tercer desempate; **6.2** cambia el ejemplo imposible de 120 contra 50 por 100 contra 40; distancias de antes de T.8 corregidas en **7.2.2**, **18** #15, **14.5** y **A.4.2**; **11.3** remite al orden 8, 6, 4 de T.7 #28; **13.1** aclara que el 75 % es de dos clics separados más que el buffer; **14.2** pasa de «cada frame» a cada tick y lee velocidad y umbral de `BalanceConfig`; **A.6** `ART-UI-BTN-CONV` con dos destinos; **A.1** y **A.7** con la ruta y la primitiva al día; **T6-06** gana `ART-WLD-LINK`; T.7 #32 cerrado y tabla de T.7 reparada | Desviaciones 132–135 abajo |
| 2026-09-28 | **T1-10** | Dos registros en `Assets/Scripts/Systems/Registries/`, singletons de 15.1 #6. `NodeRegistry`: nodos en orden de registro e indexados por facción, reindexados al escuchar `OnFactionChanged`; `All`, `GetNodes`, `CountOf`, `Contains`, `Register`, `Unregister`, `Clear`. Solo admite nodos ya inicializados: los registra quien los crea (`T1-12`, `LevelLoader` en `T5-01`). `ProtonRegistry`: protones en vuelo indexados por destino (`GetIncoming`), baja automática por el nuevo `Proton.OnFinished`. **Orden de impactos del mismo tick** (heredado de `T1-09`): el protón que llega se encola y el registro resuelve tras todos los `FixedUpdate` (`WaitForFixedUpdate`), por `ArrivalOffsetMicros`, después Carga mayor, después orden de lanzamiento. Orden de ejecución de 15.10 con `[DefaultExecutionOrder]`: registros −100, nodo 0, `Proton` +10, `ProvisionalNodeView` +100. `ProtonLauncher` registra lo que lanza; `Proton` gana `Tick`. `RegistryTests`: 14 tests, 24 casos. Suite completa: **221 tests** en verde | **Tercer criterio de desempate añadido:** con llegada y Carga iguales, primero el que se lanzó antes; 15.10 solo fija los dos primeros y sin el tercero el resultado dependería del orden interno de Unity. **La resolución vive en `ProtonRegistry`, no en `Proton`:** el protón sigue moviéndose a +10, pero los impactos del tick se resuelven al final, así que `WinConditionChecker` (+20, `T5-03`) los verá en el tick siguiente. El orden de ejecución se fija por atributo y no en *Project Settings*. Un test fallaba por olvidar el Tope 10 del Asteroide en el remanente de una captura (6.2); era el test, no el código |
| 2026-09-28 | **T1-09** | Tres scripts en `Assets/Scripts/Systems/Protons/` (`Nova.Systems`, compilado en `Nova.Gameplay` por la `.asmref`). `ProtonLauncher`: `TryLaunch` envía el 50 % hacia abajo o el 100 %, descuenta en el mismo tick y bloquea con `LaunchBlock` (`NotOwner`, `IsAsteroid`, `SameNode`, `NothingToSend`); `Check` valida sin efectos, para el caso 22 de 17. `Proton`: línea recta de centro a centro a `ProtonSpeed`, con la duración del vuelo fijada al lanzar en microsegundos enteros; conserva la facción del lanzamiento (17, caso 3), resuelve contra el destino al llegar (caso 2) con `ImpactResolver`, emite `OnImpact` y vuelve al pool. `Step` e `Impact` van separados. `ProtonPool`: pila de protones desactivados. `NodeController` gana `SubtractEnergy` y `ApplyImpact(ImpactOutcome)`. `ProtonTests`: 22 tests, 24 casos. Suite completa: **197 tests** en verde | **Orden de impactos en el mismo tick sin resolver:** cada protón resuelve en su `FixedUpdate`; pasa a `T1-10`. El ejemplo de 6.2 (120 contra un Magnetar con 50) no se puede producir con un envío —el tope máximo es 100—, así que el test usa 100 contra 40 → 10. Congelado y Reconvirtiendo aún no bloquean el envío (`T3-04`, `T2-07`). No hay vista provisional de protón: si `T1-12` la necesita, se decide allí |
| 2026-09-28 | **T1-08 y cierre de T.7 #36** | Decisión de paso de tiempo: toda la lógica de juego en `FixedUpdate` a **50 Hz**, la presentación en `Update`; escrita en [15.10](./NOVA_GameDesign_Spec.md#1510-orden-de-ejecución) y en [18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #22. `StarGenerator` en `Assets/Scripts/Gameplay/Nodes/`: +1 de Energía por `GenerationInterval` del nivel, nunca fracciones, con el tiempo en microsegundos enteros y el sobrante arrastrado al intervalo siguiente —el N4 a 1.05 s da sus +1 en los ticks 53, 105 y 158—. Solo genera si es Estrella y no es Neutral. `NodeController` gana `CurrentLevelData` y `AddEnergy`. `StarGeneratorTests`: 13 tests, 19 casos, incluidos los cinco tiempos 0→50 de 4.2 y el `Fixed Timestep` del proyecto | **Interpretación de 4.2:** «al llegar al tope se detiene» se lee como **pausa que conserva el progreso**, no como reinicio; solo se nota si un refuerzo llena la Estrella a mitad de intervalo. «Neutral no genera» (tabla de 3) se aplica como guarda aunque una Estrella neutral no debería existir. Congelado parará la generación en `T3-04` y el buff de 9 la acelerará en `T5-04` |
| 2026-09-28 | **T1-07** | Dos componentes en `Assets/Scripts/Gameplay/Nodes/`. `NodeLevelComponent`: nivel (0 el Asteroide, 1 al máximo los encendidos, fuera de rango lanza), `CurrentLevelData`, `NextUpgradeCost` (0 al máximo) y `Apply(definición, nivel)` como única puerta —mejora, encendido y fin de reconversión—, que emite `OnLevelChanged(anterior, nuevo)` solo si el nivel cambia. `CheckUpgrade` delega en `ConversionRules.CheckUpgrade`. `NodeStateMachine`: estado de base de 14.1 deducido de tipo y facción (`Sleeping`, `Unignited`, `Normal`), `Evaluate` emite `OnStateChanged` solo si cambia. `NodeController` los requiere, expone `Level`, `State`, sus eventos y `CheckUpgrade(actor)`; `Snapshot()` lleva el nivel; `Initialize` gana sobrecarga con nivel (por defecto 0 el Asteroide y 1 el resto) y escucha `OnFactionChanged` para reevaluar el estado. `NodeLevelAndStateTests`: 22 tests, 36 casos | Pagar la mejora queda para `T2-03`; encender (`Apply` + `Evaluate`) para `T1-11b`. La escucha de facción se engancha en `Initialize` y no en `OnEnable`, que no corre en EditMode. Los estados con timer y la matriz de 14.6 llegan con las tareas que los provocan |
| 2026-09-28 | **T1-06b** | `ProvisionalNodeView` en `Assets/Scripts/Presentation/Nodes/` (`Nova.Presentation`): disco de 64 px (1 u a PPU 64) generado por código, alfa binario y filtro *Point*, teñido con la paleta de 14.4, y la Energía centrada en Jersey 10 a 2× nativo (`fontSize` 5.833) en `#0B0E1A`. Solo escucha `OnFactionChanged` y `OnEnergyChanged`; se oculta si el nodo no está inicializado. `ProvisionalNodeViewTests`: 12 tests | `Nova.Presentation` pasa a referenciar `Unity.TextMeshPro`. `Initialize` no emite eventos, así que la vista se pinta en `OnEnable` y `Start`: quien crea el nodo lo inicializa antes de su primer frame. Desechable: la sustituye `NodeVisual` en `T6-01` |
| 2026-09-28 | **T1-06 y cierre de T0-06** | Cuatro componentes en `Assets/Scripts/Gameplay/Nodes/` (`Nova.Gameplay`). `NodeIdentity`: tipo y `NodeDefinition`. `NodeOwnership`: facción; `ChangeFaction` es la única puerta y emite `OnFactionChanged(anterior, nueva)`. `NodeEnergy`: `Add` con el excedente descartado, `Subtract` sin bajar de 0, `SetCap` y `Clamp`; emite `OnEnergyChanged(anterior, nueva)`. Los dos eventos solo saltan si el valor cambia. `NodeController`: `Initialize`, `Snapshot()` y reenvío de los eventos, sin reglas. `NodeComponentsTests`: 12 tests. `Jersey10 Raster56.asset` generado: la **fase 0 queda cerrada** | Aplicar un `ImpactOutcome` se deja para `T1-09`, que es donde llega el protón. Nace `T1-06b` (desviación 131) |
| 2026-09-28 | **T1-05** | `NodeLevelData`, `NodeDefinition` y `BalanceConfig` en `Assets/Scripts/Data/`. Cinco assets con los valores de 4.2–4.4, 5.4, 16.1 y 16.2: `Assets/Data/Nodes/{Asteroid,Star,Pulsar,Magnetar}.asset` y `Assets/Data/BalanceConfig.asset`. `BalanceDataTests` compara cada asset con la especificación, y comprueba que hay una definición por tipo y un solo `BalanceConfig` | T.7 #41 cerrada. Magnetar con los radios vigentes mientras #35 siga abierta. Desviaciones 128–130 abajo |
| 2026-09-28 | **T1-04** | `ConversionRules` en `Core/Rules`: `CheckIgnition`, `CheckUpgrade` y `CheckConversion` devuelven un `ActionCheck` (motivo de bloqueo + coste) y `EnergyAfterConversion` recorta al tope nuevo. La falta de Energía se informa la última, que es el único caso que la UI pinta en rojo (13.3, 13.7). Los costes entran por parámetro. `NodeSnapshot` gana `Level`, `IsConverting` y `HasPowerCharge`. `ConversionRulesTests`: 22 tests, incluidos el ejemplo de 8.3 y los casos 18, 23 y 24 de la sección 17 | Cuatro huecos de 8.1 cerrados con el usuario y escritos en [18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #18–21. T.7 #41 abierta. Desviaciones 125–127 abajo |
| 2026-09-28 | **T1-03** | `ImpactResolver` en `Core/Rules`: función pura sobre `ProtonSnapshot` + `NodeSnapshot` → `ImpactOutcome` (resultado, Energía y facción nuevas, deshielo, cura, `lastToucher`), siguiendo los pseudocódigos de 6.2 y 7.3. `ImpactResolverTests`: 23 tests, incluidos los casos 1, 2, 5, 7, 11 y 12 de la sección 17 | Nunca devuelve `Destroyed` (T.7 #40). Desviación 123 abajo |
| 2026-09-28 | **T1-02** | `EnergyMath` en `Core/Rules`: `HostileMultiplier`, `EffectiveCharge`, `CaptureCost`, `SendAmount` y `AddCapped`. El multiplicador del Magnetar entra por parámetro porque `Core` no lee ScriptableObjects. `EnergyMathTests`: tabla de 6.4, ejemplo de 6.2, casos de 5.2 y minimalidad de `CaptureCost` | Desviación 124 abajo |
| 2026-09-28 | **T1-01 y cierre de T0-04** | Cinco enums en `Assets/Scripts/Core/Enums/` (`Nova.Core`). La guarda de `ArchitectureTests` deja de omitirse y `CoreEnumsTests` fija los valores de `Faction` y el número de valores de cada enum. `T0-04` aplicada en las cuatro escenas: 960×540, `size = 4.21875`, *Grid Snapping: Pixel Snapping*. `T0-06` sigue 🟨: la primera ejecución del menú solo importó los recursos esenciales de TextMeshPro | Desviaciones 121–122 abajo. T.7 #40 abierta |
| 2026-09-28 | **T0-01 a T0-05, reajuste de T.8 a 15 u y cambio de fuente** | Proyecto configurado desde `Nova → Fase 0 → Aplicar configuración del proyecto` (`Assets/Scripts/Editor/ProjectSetup.cs`, idempotente): fondo `#0B0E1A`, `m_PostProcessData` retirado del 2D Renderer, Pixel Perfect en las 4 escenas y Build Settings Boot → MainMenu → LevelSelect → Game. `ArchitectureTests`: test vacío en verde y guarda de `Nova.Core` sin `UnityEngine` (omitida hasta T1-01). `Nova → Arte → Sincronizar sprites desde art` (`ArtBridge.cs` + `SpriteImportRules.cs`): 60 sprites. **T.8 se reajusta a 15 × 8.4375 u** (opción C) y **la fuente pasa de Pixelify Sans a Jersey 10**. `T0-04` y `T0-06` quedan 🟨 hasta aplicarse en el editor | Desviaciones 112–120 abajo. 11.3 pasa de 3 u a 2.25 u; T.7 #35 se vuelve urgente |
| 2026-09-28 | **Decisiones de plataforma y cierre de T.8** | Cuatro decisiones que no estaban en ningún `.md` y que el proyecto necesitaba para existir. **Motor:** Unity **6.6 (6000.6.0f1)**, plantilla **Universal 2D v7.0.0**; 1.2 decía «URP 2D o Built-in», que no es una decisión. Se elige URP porque en Unity 6 la **Pixel Perfect Camera viaja dentro de ella** y ya no hay paquete aparte, y se prohíbe expresamente el post-procesado, que deshace el dithering. **Plataforma:** PC Windows 1080p, que es lo que los controles de 13.1 asumían sin decirlo. **Entrada:** Input System nuevo. **Escala:** [T.8](#t8-escala-del-tablero--cerrada) se cierra en **20 × 11.25 u** | Los tres `ART-BG-*` vuelven a ⬜ y A.7 baja de 49 a 46 ✅ sin que el total de 106 se mueva. Nace la **fase 0** (6 tareas): el proyecto Unity no existía y T.4 empezaba dando por hecho que sí. Total 52 → 58. T.7 #12 cerrada; #35 a #39 abiertas. Desviaciones 107–111 abajo |
| 2026-09-26 | Cierre de las cuatro contradicciones menores | **Conteos:** `T4-02` decía 8 tests y 12.7 tiene **9**; `T5-07` decía 26 casos y la sección 17 tiene **35**; 14.5 y T.7 #24 decían «catorce» VFX y A.5 tiene **15**. **Congelado:** 14.1 daba «máx. 30 s», pero los 30 s son de *inactividad* y **cada impacto los reinicia**, así que no hay tope; queda escrito además que el Decaimiento ni descongela ni reinicia el contador, solo deja el nodo a un protón de caer. **Caso 8:** un Cero Absoluto **sí** puede congelar un nodo reconvirtiéndose y los 3 s **se pausan**, como ya le pasa a la carga de un Púlsar; §7.2.1 conserva sus dos únicas exclusiones. **Singletons:** el principio 6 de 15.1 decía «los tres registros» y uno de los tres, `GameManager`, no es un registro | 14.6 pasa a «Sí» en `Congelado` × `Reconvirtiendo`. El caso 8 se reescribe sin renumerar. Desviaciones 103–106 abajo |
| 2026-09-26 | Arte: **el idle por transform no podía deformar nada** | El contrato pedía squash and stretch (`s = 1 + 0.07·sin`) resuelto **por transform** en Asteroide y Magnetar. Con `Filter Mode: Point` eso remuestrea un sprite de 67 px entre 62.5 y 71.9 px en cada frame: no respira, hierve. Afectaba a **18 de los 34 sprites de nodo**. El reparto del idle **no cambia** —siguen sin hoja— pero su idle pasa a ser un **cabeceo de píxeles enteros**: 1 px el Asteroide, 2 px el Magnetar. La fórmula `s` se conserva **para los generadores**, que dibujan cada frame en su forma exacta y no remuestrean nada | Regla nueva en el contrato: *un transform traslada, nunca escala un nodo*, y alcanza también a la sacudida de `Asustado`. 0 assets nuevos. Desviaciones 100–102 abajo |
| 2026-09-26 | Arte: **el anillo del Magnetar no cabía en su propio lienzo** | A.4.1 topaba el asset en 384×384 razonando que «3.6 u a PPU 64 son 230 px de radio». El cálculo es correcto pero **230 px es el radio**: el círculo mide **461 px de lado**, así que los niveles 4 y 5 no cabían. El lienzo pasa a **512×512**. Y al comprobarlo salió lo de verdad caro: **un solo anillo no puede servir a cinco radios**, porque escalarlo daría ×0.556, ×0.667, ×0.778 y ×0.889, todos no enteros y todos prohibidos por el mismo argumento que convirtió el protón en tres sprites discretos. A.4.1 pasa de 4 archivos a **16** | Total del inventario 94 → **106**. Contrato actualizado en *Familias exentas de la celda*. T.7 #34 abierta (matriz de variantes) y T.8 punto 6 ampliado. Desviaciones 97–99 abajo |
| 2026-09-26 | Niveles: **la columna `N` de 11.2 no cuadraba** | Cuatro de los nueve niveles tenían un `N` distinto de la suma de sus propias columnas: +1 en el 5, el 7 y el 8, y **+2 en el 9**. Se recalcula `N` respetando el contenido —5, 7, 8, 9, **11**, 11, **13**, **14**, **16**— en vez de recortar nodos, porque `N` es un resumen y las otras columnas son el diseño, y porque las coordenadas siguen sin existir (T.7 #2). La leyenda declara ahora que `N` es **derivada** y que al llegar a `T5-02` será `spawns.Count`: si vuelve a no cuadrar, lo que está mal es `N` | La rampa deja de ser regular: tramo plano en 5–6 y salto de +2 al final. T.7 #28 ampliada a los niveles 6 y 8. Desviaciones 95–96 abajo |
| 2026-09-26 | Controles: **el clic izquierdo depende de si hay selección** | La [sección 13](./NOVA_GameDesign_Spec.md#13-controles-y-ui) daba al clic izquierdo tres significados simultáneos sobre un nodo propio —seleccionar, enviar el 50 % y abrir la rueda del Púlsar— sin ninguna prioridad escrita. Se cierra con una sola regla: **sin selección el clic elige, con selección el clic envía**, y la selección **se mantiene** tras enviar. La rueda de poderes pasa a abrirse solo desde el panel de clic derecho. El menú de encendido, que no tenía forma documentada de abrirse, se coloca en el clic izquierdo del Asteroide, que estaba libre porque un Asteroide nunca es origen de un envío | 13.1 reescrita en tres bloques, **13.7 nueva**, 13.3 #5 y #6 corregidos, 13.5 repuntada. `T2-02` apunta ya a 13.7. Desviaciones 92–94 abajo |
| 2026-09-26 | Estructura: **una sola lista de tareas** | La [sección 19](./NOVA_GameDesign_Spec.md#19-fases-y-criterios-de-salida) del maestro y T.4 enumeraban las dos el trabajo de cada fase, y habían derivado. El §19 se reescribe como ***Fases y criterios de salida***: pierde las viñetas, que remiten a T.4, y conserva los seis criterios en una tabla. T.4 queda como única lista de trabajo. Se corrige de paso el criterio de la fase 6, que remitía a «el documento 2» en vez de a algo comprobable | Ancla del §19 renombrada y actualizadas sus 6 referencias. T.6 pasa a declararse copia de consulta. T.7 #33 cerrado. Desviación 91 abajo |
| 2026-09-26 | Fases: **el encendido se mueve a la fase 1** | La fase 1 se contradecía a sí misma: `T1-12` pedía una escena que demostrase «capturar, **encender** y tomar un nodo enemigo», y `T2-01`, la tarea que implementa `NodeIgnition`, **dependía de `T1-12`**. El corte además estaba mal puesto: `T1-04` ya metía en la fase 1 «validez y coste de encendido» vía `ConversionRules`. `T2-01` pasa a ser **`T1-11b`**, usando la convención de `T3-03b` para no renumerar nada. En la fase 1 el encendido **solo puede ser a Estrella**, porque `MagnetarTurret` es `T2-04` y `PulsarCaster` es `T3-02`: no es un recorte arbitrario sino el único encendido posible en ese punto. `IgnitionMenu` se queda en la fase 2, que es donde por fin hay tres opciones que elegir | Fase 1 pasa de 12 a 13 tareas y fase 2 de 8 a 7; el total sigue en 52. `T2-02` y `T2-03` repuntadas a `T1-11b`. [19](./NOVA_GameDesign_Spec.md#19-fases-y-criterios-de-salida) actualizado en sus dos fases. Desviación 90 abajo |
| 2026-09-26 | Reglas: **un Magnetar a 0 cae con Carga 1** | Revisión de coherencia de los siete `.md`. El caso 7 de la [sección 17](./NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución) afirmaba que un protón de Carga 1 **no** captura un Magnetar vacío y exigía Carga ≥ 2. Es falso: la guarda de [6.2](./NOVA_GameDesign_Spec.md#62-resolución-de-impacto) es `effective < target.energy`, y con los dos a 0 esa comparación es falsa, así que el pseudocódigo normativo **ya caía en la rama de captura**. No se cambia ni el multiplicador 0.5 ni el redondeo hacia abajo: se corrige la prosa que los leyó mal | Caso 7 reescrito, caso 32 y [7.4](./NOVA_GameDesign_Spec.md#74-decaimiento) pasan de «Carga 2» a «Carga 1», y la decisión [18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #9 queda tachada. Desviación 89 abajo |
| 2026-09-20 | Nomenclatura: **el Enemigo deja de llamarse «IA»** | 38 de las 39 apariciones de «IA» designaban al oponente y se sustituyen por **«el algoritmo»** o **«el Enemigo»** según la frase. La 39.ª se conserva y se precisa: en T.1, «IA» es el **asistente** con el que se escribe el código. El término entra por fin en el glosario normativo de la [sección 2](./NOVA_GameDesign_Spec.md#2-glosario-normativo), donde no estaba pese a usarse 39 veces. Renombrados el archivo `NOVA_IA_Algoritmos.md` → **`NOVA_Algoritmo_Enemigo.md`**, los scripts `AIEvaluator` → `EnemyEvaluator`, `AIThreatResponder` → `EnemyThreatResponder`, `AIProfile` → `EnemyProfile`, **`EnemyBrain` → `EnemyController`**, y las carpetas `Scripts/AI/` → `Scripts/Enemy/` y `Data/AIProfiles/` → `Data/EnemyProfiles/` | Ni una línea de C# escrita todavía, así que el renombrado de scripts es gratis. Desviaciones 86–88 abajo |
| 2026-09-20 | Reglas: **corrección del objetivo válido (misma sesión)** | La regla de 7.2.1 pasa de «nodo **enemigo** encendido» a **«nodo encendido no propio»**. El cambio es de una palabra y tiene dos consecuencias: el **Magnetar neutral vuelve a ser objetivo de los tres poderes** —se puede robar con Gusano por 60 en vez de 200 de Carga, callar con Cero Absoluto y vaciar con Decaimiento— y **ningún Asteroide lo es nunca**, que era lo que había que quitar. Una regla en vez de una lista por poder | Casos 30–32 de la sección 17 reescritos y 34–35 renumerados; `AjusteNeutral` añadido a `ScorePoder`; `T.7 #29` cambia de sentido. Desviaciones 83–85 abajo |
| 2026-09-20 | Reglas: **el Púlsar pierde el alcance y el proyectil** | Cuatro cambios encadenados en la [sección 7](./NOVA_GameDesign_Spec.md#7-poderes-del-púlsar). **1)** Los tres poderes pasan a **alcance global**; desaparecen los 7 u / 7 u / 5 u y la columna deja de ser un número. **2)** ~~Objetivo válido = **nodo enemigo encendido**~~ **— corregido el mismo día a «nodo encendido no propio», ver la fila de arriba.** Lo que se mantiene de este punto es la exclusión de los Asteroides de cualquier bando. **3)** El poder deja de ser proyectil: se entrega por un **haz de 0.25 s** no interceptable, con el efecto al terminar y revalidación del objetivo en ese instante. **4)** El algoritmo del Enemigo estrena `ScorePoder` ([12.3.1](./NOVA_Algoritmo_Enemigo.md#1231-función-de-puntuación-lanzar-poder)) porque el alcance era su único filtro de objetivos. Costes y tiempos de carga **sin tocar**, a propósito | 7 casos borde nuevos (17, #27–33), 5 decisiones nuevas (18, #13–17), 1 asset eliminado y 3 sustituidos, 1 tarea de tracking nueva. Desviaciones 76–82 abajo |
| 2026-09-19 | Arte: `ART-ST-BUFF` (v1 elegida) | «El galón» desde `tools/gen_state_buff_v1.py` → `art/states/buff/v1/`. **Dos archivos de 1 frame**, uno por power-up: un galón de dos chevrones en la esquina libre de la celda. Se descartaron dos propuestas completas («El temblor» y «El sobrecalentado»), que se conservan. **Cierra A.3: 9 de 9** | A.3 completa. `T6-02` y `T6-03` dejan de estar bloqueadas por arte. Desviaciones 71–75 abajo |
| 2026-09-19 | Arte: `ART-ST-CAP` (v2 elegida) | «El relevo» desde `tools/gen_state_cap_v2.py` → `art/states/cap/v2/`. **Una hoja de 5 frames 480×96**: dos anillos blancos que se cruzan en el frame central, uno entrando y otro saliendo. Sin variante de facción. Se descartaron dos propuestas completas («El fogonazo» y «El sello»), que se conservan | A.3 pasa de 7 a 8 de 9. Desviaciones 66–70 abajo |
| 2026-09-19 | Arte: `ART-ST-CONV` (v2 elegida) | «La colada» desde `tools/gen_state_conv_v2.py` → `art/states/conv/v2/`. **Dos hojas de 36 frames 3456×96**, una por facción: el primer asset de A.3 que no es un estado sino un tránsito de 3.0 s. Se descartaron dos propuestas completas («El torno» y «El apagón»), que se conservan | A.3 pasa de 6 a 7 de 9. Desviaciones 60–65 abajo |
| 2026-09-19 | Arte: `ART-ST-DECAY` (v2 elegida) | «El miasma» desde `tools/gen_state_decay_v2.py` → `art/states/decay/v2/`. **Hoja de 16 frames 1536×96**, el primer tinte animado: nube con cizalla sesgada hacia abajo más cinco gotas. Se descartaron dos propuestas completas («El goteo» y «El enjambre»), que se conservan | A.3 pasa de 5 a 6 de 9. Desviaciones 55–59 abajo |
| 2026-09-19 | Arte: `ART-ST-FROZEN` (v3 elegida) | «El vidrio» desde `tools/gen_state_frozen_v3.py` → `art/states/frozen/v3/`. **Un archivo de 1 frame**: una red de 13 segmentos que cruza el nodo, con la escarcha brotando de la propia grieta, verificado sobre los 13 congelables con la vara nueva `verifica_tinte`. Se descartaron dos propuestas completas («La escarcha» y «El bloque»), que se conservan. **«El bloque» llegó a estar elegida y documentada**; se cambió el mismo día por ser la primera figura de contorno recto y regular dentro del tablero | A.3 pasa de 4 a 5 de 9. Desviaciones 48–54 abajo |
| 2026-09-18 | Arte: `ART-ST-SCARE` (v1 elegida) | «Los ojos como platos» desde `tools/gen_state_scare_v1.py` → `art/states/scare/v1/`. **Dos archivos de 1 frame** —un ojo en dos tallas— que cubren los 14 nodos con la tabla de anclaje de `nova_estados.OJOS`. Se descartó una v2 completa («La pupila dilatada»), que se conserva | A.3 pasa de 3 a 4 de 9. Desviaciones 43–47 abajo |
| 2026-09-18 | Arte: `ART-ST-IGN` + `ART-ST-READY` (v2 elegida) | «El pedernal» desde `tools/gen_state_ign_v2.py` → `art/states/ign/v2/`. **Cuatro archivos**: dos lecturas (`energy < 10` / `>= 10`) × dos facciones. Se descartó una v1 completa («El interruptor»), que se conserva | A.3 pasa de 8 IDs a 9 y de 1 a 3 hechos. Desviaciones 38–42 abajo |
| 2026-09-18 | Arte: `ART-ST-SLEEP` (v2 elegida) | Primer asset de A.3. Hoja de 16 frames 1536×96 desde `tools/gen_state_sleep_v2.py`, hoy en `art/states/sleep/v2/` (la carpeta se movió el mismo día, desviación 40). Se descartó una v1 completa («La escalera»), que se conserva | A.3 estaba entonces en 1 de 8. Desviaciones 33–37 abajo, con la 33 corregida |
| 2026-09-18 | Arte: Magnetar, vuelta a la v1 | El diseño cerrado pasa de «La Botella» (`v2`) a **«El Yunque»** (`tools/gen_magnetar_v1.py` → `art/nodes/magnetar/v1/`). Los 15 `ART-NOD-MAG-*` siguen en 15: mismos IDs, mismos nombres de archivo, otra carpeta. La v2 se conserva descartada | Revierte la desviación 11 y corrige la 23. Desviaciones 29–32 abajo |
| 2026-09-18 | Arte: los 3 assets de power-up | `ART-UI-PU-RAY`, `ART-UI-PU-HYD` y `ART-WLD-DROP-HALO` desde `tools/gen_powerups_v1.py`. A.4.4 cerrada. Estrena `anillo`, la última primitiva sin usar | Pedidos para la sección ITEMS de la plantilla de GDD. Cierra T.7 #11 |
| 2026-09-18 | Arte: los 3 fondos de mapa | `ART-BG-01/02/03`, 2048×1152, desde `tools/gen_fondos_v1.py` sobre un módulo nuevo `tools/nova_fondo.py`. A.4.5 cerrada | Pedidos fuera de orden para una entrega de documento. Desviaciones 24–28 abajo |
| 2026-09-18 | Arte: Asteroide (v1 elegida) | Los 3 sprites de `ART-NOD-AST-00*` desde `tools/gen_asteroide_v1.py`. Se descartó una v2 completa, que se conserva. **Con esto `ART-NOD-*` queda completo: 34/34** | A.2.1 pasa de tabla vacía a diseño cerrado y `T6-01` se desbloquea. Desviaciones 18–23 abajo |
| 2026-09-18 | Arte: Magnetar (v2 elegida) | Los 15 sprites de `ART-NOD-MAG-*`, tres facciones, desde `tools/gen_magnetar_v2.py`, más sus 15 hojas de idle de 16 frames. Se descartó una v1 completa, que se conserva. **Entrada histórica: la elección se revirtió el mismo día, ver la fila de arriba** | A.2.4 pasa de tabla vacía a diseño cerrado. Desviaciones 11–17 abajo, con la 11 revertida y la 23 corregida |
| 2026-09-18 | Arte: Púlsar (v2 elegida) | Los 6 sprites de `ART-NOD-PUL-*` desde `tools/gen_pulsar_v2.py`. Se descartó una v1 completa, que se conserva | A.2.3 pasa de boceto a diseño cerrado. Desviaciones 7–10 abajo |
| 2026-09-18 | Arte: paleta, pipeline y Estrella | 20 rampas, 8 primitivas y los 10 sprites de `ART-NOD-STR-*` | Ver abajo. No es una tarea de T.4: el arte no se planifica aquí. |
| — | — | Proyecto de código sin iniciar | — |

> **Cómo leer esta lista.** Una línea por desviación: **qué cambió** y **dónde vive ahora**. El razonamiento completo está en el documento que se cita, que es el que manda; aquí solo está el rastro de cuándo y por qué se movió. Los números no se reutilizan nunca, ni siquiera cuando una desviación queda revertida.

**Al implementar `T2-02` (2026-10-02).**

149. **La cadencia del Magnetar cambia con el radio.** T.7 #35 solo hablaba de radios, pero con el protón a 2.5 u/s un radio ×0.75 deja al protón ×0.75 del tiempo dentro, y la Carga anulada de 4.4 caía de ~18 a ~13 en el N5. Se sube la cadencia ×4/3 para que la defensa valga lo mismo; se afina jugando en `T2-04`. → [4.4](./NOVA_Nodos.md#44-magnetar), [A.4.1](./NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar).
150. **El estado del menú vive en `Nova.Input` y no en `Nova.UI`.** 15.7 solo tenía `IgnitionMenu`, en UI. Pero la elección es un comando que tiene que salir por `PlayerCommandIssuer` dentro del tick, y la UI no puede escribir estado (15.1 #3). El router resuelve el clic contra `IgnitionMenuState` y la vista solo lo dibuja; la geometría está en el estado para que dibujo y blanco no puedan divergir. → [15.7](./NOVA_GameDesign_Spec.md#157-input-enemigo-y-ui).
151. **`Nova.UI` pasa a referenciar `Nova.Input`, `Nova.Utils` y TextMeshPro**, como hizo `Nova.Presentation` en la desviación 147: lee eventos, no crea ciclo. La UI valida cada opción con `NodeIgnition.Check`, que no tiene efectos y ya lo anunciaba su comentario; no llama a `NodeController` (15.9).
152. **Nace `Nova.Utils.PrimitiveSprites`.** El disco provisional vivía en `ProvisionalNodeView` y el menú lo necesitaba desde `Nova.UI`, que no debe depender de una vista desechable de `Nova.Presentation`. Desaparece con el arte de la fase 6, igual que sus usuarios. → [15.2](./NOVA_GameDesign_Spec.md#152-estructura-de-carpetas).
153. **Dos decisiones de interacción nuevas**, [18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #28 y #29: qué hace un clic que cierra el menú, y cómo se ven los tipos que la partida no tiene. Los nombres de las opciones (`Estrella`, `Púlsar`, `Magnetar`) son el primer texto de UI escrito: viven en un campo serializado de `IgnitionMenu` y entran en T.7 #39. → [13.7](./NOVA_GameDesign_Spec.md#137-menú-radial-de-encendido).

**Al implementar `T1-12` (2026-09-29).**

144. **La escena de prueba no es una de las cuatro de 15.2.** Vive en `Assets/Scenes/Tests/Fase1.unity`, fuera de *Build Settings*, y la escribe un menú de editor en vez de montarse a mano: un `.unity` no se puede revisar en un diff, y así cualquier clon la regenera igual. → [15.2](./NOVA_GameDesign_Spec.md#152-estructura-de-carpetas).
145. **`PlacedNode` hace, en pequeño, lo que hará `LevelLoader`.** El estado del nodo no se serializa, así que alguien tiene que llamar a `Initialize` y `Register` al cargar. Tipo, facción, Energía y posición de los tres nodos son **datos de la escena de prueba**, no balance: viven serializados en ella y se cambian en el Inspector; la tabla del generador solo da los valores de partida. `T5-01` lo sustituye por `NodeSpawnData`.
146. **Las dos vistas provisionales que faltaban nacen aquí**, como anticipaban la desviación 139 (selección) y la nota de `T1-09` (protón): sin ellas la escena no se puede jugar. Y el Asteroide provisional pasa a ser más pequeño que un nodo encendido, porque con el mismo círculo encender no se veía. Las tres son de la regla 4 de T.2 y las sustituyen `T6-01`, `T6-04` y `T6-06`.
147. **`Nova.Presentation` pasa a referenciar `Nova.Input`** para que el anillo lea `SelectionManager`. Es lectura de eventos, como exige el principio 3 de 15.1, y no crea ciclo: `Nova.Input` no conoce la presentación. `Nova.Editor` referencia ya los ensamblados del juego, así que `ProjectLayers` lee el nombre de la capa de `InputRouter.NodesLayer` en vez de duplicarlo.
148. **El criterio de salida se comprueba dos veces:** en `Phase1CriterionTests`, que monta en código los mismos componentes y nodos que la escena y lo recorre con clics simulados en el orden de 15.10, y a mano en Play. El test garantiza que la lógica llega; la partida a mano, que se puede jugar con el ratón.

**Al implementar `T1-11b` (2026-09-29).**

140. **«Solo a Estrella» es una restricción de la entrada, no de la regla.** `NodeIgnition` enciende a los tres tipos, como dice 4.1, y sus tests lo cubren; lo que la fase 1 no tiene es cómo elegir. El clic va siempre a Estrella hasta que `T2-02` ponga el menú radial en su sitio. Un Púlsar o un Magnetar encendidos hoy serían nodos válidos que almacenan y envían, pero sin `PulsarCaster` ni `MagnetarTurret`.
141. **`OnIgnitionRequested` desaparece del router.** La desviación 137 lo dejaba como intención sin atender; ahora la atiende el propio router llamando a `Ignite(nodo, Estrella)`. En `T2-02` esa llamada se sustituye por abrir `IgnitionMenu`. Sin Energía suficiente el clic no hace nada: el menú, cuando exista, se abrirá igual con las opciones deshabilitadas (13.7).
142. **Cada `NodeIgnition` guarda las `NodeDefinition` a las que puede encender.** 15.4 no tiene un catálogo de tipos y crear uno sería un script de datos nuevo; el prefab del nodo (`T1-12`, `LevelLoader` en `T5-01`) las recibe por el Inspector. Si `T2-07` o `T5-01` necesitan la misma búsqueda, es el momento de sacarla a un catálogo.
143. **`StarGenerator` tendrá que estar en todo nodo que pueda llegar a ser Estrella**, es decir, en todos salvo el Magnetar neutral: el encendido y la reconversión cambian el tipo, no los componentes. Ya comprueba el tipo en cada tick, así que en un nodo que no es Estrella no hace nada. 15.5 dice «solo en Estrella» por su efecto, no por su presencia. Lo aplica el prefab de `T1-12`.

**Al implementar `T1-11` (2026-09-29).**

136. **La entrada del Jugador también corre en el tick fijo.** 15.10 pone `InputRouter` a −50 dentro del orden de ejecución, pero 18 #22 deja en `Update` solo lo que lee. El ratón se muestrea en `Update` —es donde el Input System marca los botones del frame— y los eventos se aplican en `FixedUpdate`, así que ningún envío cambia Energía fuera del tick. Coste: hasta un frame de latencia. → [15.10](./NOVA_GameDesign_Spec.md#1510-orden-de-ejecución).
137. **El router emite intenciones que todavía nadie atiende:** `OnIgnitionRequested` (clic en Asteroide propio sin selección, `T1-11b`), `OnInfoRequested` (clic derecho sobre nodo, `T2-03`) y `OnPauseRequested` (`Esc`, `T5-05`). Detectarlas es de esta tarea; atenderlas, no.
138. **El radio del trigger no tiene dónde vivir todavía.** 18 #23 lo ata a la huella del sprite (0.3–0.6 u), que es arte de `T6-01`. `T1-11` resuelve el clic contra el collider que tenga el nodo; quien monte la escena de `T1-12` le pondrá el radio del disco provisional, 0.5 u.
139. **No hay feedback de selección.** 15.7 se lo asigna a `SelectionManager`, pero es `ART-WLD-SEL` (`T6-06`); aquí solo se emite `OnSelectionChanged`. Si `T1-12` necesita ver la selección para ser jugable, nace una vista provisional como la desviación 131 hizo con el nodo.

**Al revisar los `.md` antes de `T1-11` (2026-09-29).**

132. **Tres desviaciones decían «→ 15.2» y 15.2 no se había tocado.** La 112 (`Nova.Editor`), la 113 (`Systems` dentro de `Nova.Gameplay`) y la 114 (`Core` sin `UnityEngine`) apuntaban a una sección que seguía diciendo lo contrario. Lo mismo con 15.10 tras `T1-10`: el tercer desempate y el orden por atributo vivían solo en T.5. **Anotar una desviación no es aplicarla.** → [15.2](./NOVA_GameDesign_Spec.md#152-estructura-de-carpetas), [15.10](./NOVA_GameDesign_Spec.md#1510-orden-de-ejecución), [17](./NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución) caso 1.
133. **El cambio de escala dejó distancias viejas en cuatro sitios.** T.8 listó qué tocar al cerrar, pero la lista se escribió para 32 → 20 u y no se repasó al pasar a 15 u: «35 u», «36.7 u» y «6.1 s» seguían en 7.2.2, 14.5, A.4.2 y 18 #15. → esos cuatro.
134. **`ART-WLD-LINK` no lo pedía ninguna tarea.** Estaba en A.4.3 y ninguna fila de T.4 lo consumía, así que el criterio de salida de la fase 6 («todos los assets integrados») no se podía cumplir con las tareas escritas. → `T6-06`.
135. **T.7 #32 llevaba abierto algo que ya estaba hecho** y la tabla de T.7 estaba partida en dos por una línea en blanco: de la fila #18 en adelante no se renderizaba como tabla. → T.7.

**Al empezar la fase 1 (2026-09-28).**

121. **Los valores de `NodeState` están en español en 14.1 y `T1-07`, y 15.2 exige identificadores en inglés.** Se aplica 15.2: `Sleeping`, `Unignited`, `Normal`, `Scared`, `Frozen`, `Infected`, `Converting` y `Captured`, con el nombre de 14.1 en el comentario de cada valor. Lo mismo para `NodeType` (`Asteroid`, `Star`, `Pulsar`, `Magnetar`) y `PowerType` (`AbsoluteZero`, `Decay`, `Wormhole`). → `Assets/Scripts/Core/Enums/`.
122. **`ImpactResult.Destroyed` figura en 15.3, pero ningún pseudocódigo lo devuelve**, y los nodos nunca se destruyen (17, caso 4). El valor existe sin significado. → T.7 #40.
123. **Un impacto sobre un nodo `Congelado` + `Infectado` también cura la infección.** 7.4 y 14.1 dicen «cualquier facción que le envíe Energía», sin excepciones, pero ninguno de los dos cita el caso congelado. Se aplica al pie de la letra. → [7.4](./NOVA_GameDesign_Spec.md#74-decaimiento).
124. **El 50 % del envío está fijo en el código.** No figura en las tablas de la sección 16: 5.2 lo trata como regla, no como balance. Si pasa a ser ajustable, necesita una fila en 16.1. → `EnergyMath.SendAmount`.
125. **8.1 tenía cuatro huecos que `ConversionRules` necesitaba cerrados:** quién paga la mejora y qué la bloquea, si un poder *ya cargado* impide reconvertir, en qué momento cambia el tipo y qué pasa con el coste si capturan el nodo a mitad. Se cierran con el usuario. → [8.1](./NOVA_GameDesign_Spec.md#81-reglas), [18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #18–21.
126. **El coste de encendido está escrito dos veces:** como valor global en 16.1 y como fila «1 (encendido)» por tipo en 16.2. `ConversionRules` lo recibe como un único valor. → T.7 #41.
127. **`Core` necesita tipos que 15.3 no lista.** `NodeSnapshot`, `ProtonSnapshot` e `ImpactOutcome` son la entrada y la salida que 15.3 pide a `ImpactResolver` («snapshot», «deltas»). `ActionBlock` y `ActionCheck` son la salida de `ConversionRules`: la UI necesita distinguir «no se puede» de «falta Energía». Viven en los archivos de la regla que los usa, sin scripts nuevos. → [15.3](./NOVA_GameDesign_Spec.md#153-core--reglas-puras-sin-monobehaviour).
128. **`NodeDefinition` nace sin prefab ni sprites por facción**, aunque 15.4 los lista. Todavía no existe el prefab, y los sprites son integración de arte de la fase 6: definir ahora su estructura (varían por nivel y por facción) sería adelantar `T6-01`. El «nº de niveles» no se guarda aparte: es la longitud del array (`MaxLevel`), para que los dos datos no puedan contradecirse. → [15.4](./NOVA_GameDesign_Spec.md#154-data--scriptableobjects).
129. **`BalanceConfig` guarda cuatro valores de 16.1 que 15.4 no le asignaba:** coste de encendido (#41), Materia Inerte, umbral de susto y devolución al cancelar una carga. La duración del haz y los tres parámetros de power-ups se quedan para `PowerDefinition` y `PowerUpDefinition`, que es donde 15.4 los pone. → [15.4](./NOVA_GameDesign_Spec.md#154-data--scriptableobjects).
130. **Los assets se escriben en YAML con `.meta` de GUID fijo**, no con un menú de editor. Un menú que los generase llevaría los números escritos en C#, que es justo lo que prohíbe la sección 16. Desde ahora se editan en el inspector de Unity, y `BalanceDataTests` avisa si se separan de la especificación.
131. **Ninguna tarea de la fase 1 dibujaba nada.** `T1-12` pedía una escena donde capturar y encender, `T1-09` protones que vuelan y `T1-11` clics sobre nodos, pero la primera vista de nodo era `NodeVisual`, en `T6-01`, detrás de `T5-07`. Nace **`T1-06b`**, una vista provisional con primitivas, siguiendo la convención de `T1-11b` para no renumerar. Fase 1 pasa de 13 a 14 tareas y el total de 58 a 59.

**Al montar la fase 0 (2026-09-28).**

112. **Hace falta un ensamblado de editor que 15.2 no declara.** `Nova.Editor` (`Assets/Scripts/Editor/`, solo Editor) aloja la configuración de la fase 0, el puente de arte y el font asset. `PixelPerfectCamera` vive en `Unity.RenderPipelines.Universal.2D.Runtime`, no en el ensamblado principal de la URP. → [15.2](./NOVA_GameDesign_Spec.md#152-estructura-de-carpetas).
113. **`Gameplay` y `Systems` se necesitaban mutuamente.** `NodeController` lanza por `ProtonLauncher`, `MagnetarTurret` lee `ProtonRegistry`, `PulsarCaster` crea `PowerBeam`, y `NodeRegistry` guarda `NodeController`: como ensamblados separados formaban un ciclo. `Systems/` sigue siendo carpeta y espacio de nombres `Nova.Systems`, pero compila dentro de `Nova.Gameplay` mediante `Nova.Systems.asmref`. → [15.2](./NOVA_GameDesign_Spec.md#152-estructura-de-carpetas), [15.9](./NOVA_GameDesign_Spec.md#159-comunicación-entre-capas).
114. **`Core` «sin UnityEngine salvo tipos básicos» (15.2) contradice «sin referencia a `UnityEngine`» (T0-03).** Manda T0-03, que es lo que hace comprobable el principio 4: en `Core` no hay `Vector2`, y `ThreatCalculator` usará un tipo propio. Pendiente de corregir el comentario de 15.2.
115. **Un `.preset` no podía cumplir T0-05.** No distingue Single de Multiple por el nombre, no trocea hojas y solo se aplica si se registra en el Preset Manager. Se sustituye por un `AssetPostprocessor` sobre `Assets/Sprites/`. Máximo de textura 8192: `ART-ST-CONV` mide 3456 px y con el 2048 por defecto Unity la habría reducido sin avisar. → T0-05.
116. **La lista de versiones elegidas no se duplica.** El puente lee las filas ✅ del inventario, que A.1 declara dueño de la versión buena, y falla si una fila cita un archivo inexistente o si dos filas eligen versiones distintas del mismo asset. → [A.1](./NOVA_Assets_Diseno.md#a1-convención-de-nombres).
117. **Una carpeta sin versionar no puede tener GUIDs aleatorios.** `Assets/Sprites/` y sus `.meta` están en el `.gitignore`: cada clon habría asignado GUIDs distintos y los prefabs de la fase 6 habrían perdido sus sprites. El puente escribe cada `.meta` con un GUID derivado de la ruta, y cada frame de hoja con un ID derivado de su nombre.
118. **El «×1.5 a 1080p» de T.8 no existía.** La Pixel Perfect Camera solo amplía por enteros; con los ajustes de `T0-04` una pantalla 1080p enseñaba 30 × 16.9 u con el tablero de 20 u en el centro. Se reajusta a **15 × 8.4375 u**, referencia 960 × 540, ×2 exacto. La tabla de *Tamaños candidatos* ya decía que 20 u no era entero a 1080p; **lo que no se preguntó es qué hace la cámara con un factor que no es entero**, y la respuesta es: no hacerlo. → [T.8](#t8-escala-del-tablero--cerrada), [2](./NOVA_GameDesign_Spec.md#2-glosario-normativo), [16.1](./NOVA_GameDesign_Spec.md#161-globales), [11.3](./NOVA_GameDesign_Spec.md#113-curva-de-dificultad), [A.4.5](./NOVA_Assets_Diseno.md#a45-fondos).
119. **Pixelify Sans no tiene tamaño nativo.** El contrato pedía usar la fuente «a múltiplos enteros de su tamaño nativo», y medida sobre el TTF **ninguna** de sus ~17 000 coordenadas cae en una rejilla: parece pixel art sin estar dibujada sobre píxeles. Se comparan cinco fuentes OFL en rejilla (`art/_preview/fuentes/`) y se elige **Jersey 10**: 100 % en rejilla, mayúscula de 10 px, tildes limpias. La elección anterior se hizo **por cobertura de caracteres**, que era necesaria pero no suficiente. → [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), *Importación en Unity*.
120. **T0-06 figuraba bloqueada por «falta elegir la fuente»** cuando NOVA_Arte_Tecnica.md ya la había elegido. El mismo patrón de las desviaciones 95 y 103: un dato escrito en dos sitios y actualizado en uno.

**Al fijar plataforma y cerrar T.8 (2026-09-28).**

107. **«URP 2D o Built-in» nunca fue una decisión, y llevaba en 1.2 desde el principio.** Una alternativa escrita en la fila *Motor* de la tabla de alcance **cerrado** es una contradicción con el título de la tabla. Se resolvió cuando hubo que crear el proyecto de verdad, que es cuando una disyuntiva se paga. → [1.2](./NOVA_GameDesign_Spec.md#12-alcance-cerrado).
108. **Tres cosas que los documentos daban por supuestas y no decían en ninguna parte:** la plataforma, el sistema de entrada y la versión del motor. Los controles de [13.1](./NOVA_GameDesign_Spec.md#131-ratón) —clic derecho, doble clic, arrastre, lazo, `Esc`— **solo existen en un PC con ratón**, así que la plataforma estaba decidida de hecho desde la primera versión y nadie la escribió. **Lo que el diseño implica y el documento calla se descubre al construir, no al releer.** → [1.2](./NOVA_GameDesign_Spec.md#12-alcance-cerrado).
109. **La primera decisión de diseño que desmarca arte terminado.** Los tres fondos estaban ✅ y vuelven a ⬜ por un cambio de lienzo. No se pierde diseño —las tres ideas y las tres reglas se conservan— pero sí hay que reajustar todo lo que en `nova_fondo.py` está **medido en píxeles**: `sigma`, la comba de S2 y la grava dejan de medir lo que medían en unidades de mundo al pasar el lienzo a 0.625×. **Un generador es barato de relanzar y caro de reencuadrar.** → [A.4.5](./NOVA_Assets_Diseno.md#a45-fondos), [A.7](./NOVA_Assets_Diseno.md#a7-resumen-de-progreso).
110. **La lista de tareas no incluía crear el proyecto.** `T.4` abría en `T1-01`, los enums de `Core`, sobre un Unity que no existía; el repositorio solo tenía `tools/`, `art/` y `mds/`. El hueco es el mismo patrón de la desviación 90 —un criterio de salida cuyas tareas no podían cumplirlo— pero un escalón más abajo: **la fase 1 no podía empezar, no solo terminar.** De paso queda escrito que `Nova.Core` necesita un ensamblado propio sin `UnityEngine`, sin el cual el principio 4 de [15.1](./NOVA_GameDesign_Spec.md#151-principios) es una intención y no una propiedad comprobable. → T.4, fase 0.
111. **Una regla de diseño escrita en unidades absolutas no sobrevive a un cambio de escala.** [11.3](./NOVA_GameDesign_Spec.md#113-curva-de-dificultad) pedía un Asteroide «a menos de 5 u», que en 32 u era el 15.6 % del ancho y en 20 u habría sido el 25 %: la misma frase, una regla distinta. Pasa a **3 u**, conservando la proporción. **Lo que la regla quería decir era «cerca en pantalla», y eso se escribe en porcentaje o se revisa a mano cada vez.** → [11.3](./NOVA_GameDesign_Spec.md#113-curva-de-dificultad).

**Al cerrar las cuatro contradicciones menores (2026-09-26).**

103. **Los tres conteos desviados eran de cosas que habían crecido después.** La sección 17 pasó de 26 a 35 casos al quitarle el alcance al Púlsar, 12.7 de 8 a 9 tests con `ScorePoder`, y A.5 de 14 a 15 VFX. **Ninguno se actualizó donde se citaba.** Es el patrón de la desviación 95 otra vez: un número derivado escrito a mano. → `T4-02`, `T5-07`, [14.5](./NOVA_Estados_Animaciones.md#145-animaciones-puntuales-sin-estado).
104. **«Máx. 30 s» describía mal un temporizador de inactividad.** Los 30 s del auto-deshielo se reinician con cada impacto, así que `Congelado` no tiene duración máxima. **Un tope y un tiempo de espera no son lo mismo**, y la columna *Duración* de 14.1 los confundía. → [14.1](./NOVA_Estados_Animaciones.md#141-estados-del-nodo), [7.3](./NOVA_GameDesign_Spec.md#73-cero-absoluto).
105. **El caso 8 era una tercera exclusión encubierta.** [7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido) dice «dos exclusiones, y no admiten excepción», y el caso 8 añadía una tercera **solo para un poder de los tres**, dejando sin definir qué hacían los otros dos. Se resuelve por el precedente que ya existía: congelar pausa un temporizador, igual que pausa la carga de un Púlsar. **Iniciar y continuar son cosas distintas**, y §8.1 solo prohibía lo primero. → [17](./NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución) caso 8, [8.1](./NOVA_GameDesign_Spec.md#81-reglas), [14.6](./NOVA_Estados_Animaciones.md#146-matriz-de-compatibilidad).
106. **«Los tres registros» eran dos registros y un gestor.** `GameManager` es el tercer singleton y no es un registro, así que la excepción del principio 6 no lo cubría por su propio nombre. → [15.1](./NOVA_GameDesign_Spec.md#151-principios).

**Al arreglar el idle por transform (2026-09-26).**

100. **La regla y su infracción estaban en el mismo párrafo.** El apartado *Animación* define `s` y diez líneas más abajo se la encarga a un transform. El párrafo razona muy bien **qué** animar —«¿tiene algo interno que mover?»— y no llega a preguntarse **si el transform puede hacerlo**. → contrato, *Animación*.
101. **La fórmula no estaba mal; estaba mal quién la ejecutaba.** Dibujada por un generador es geometría calculada y es correcta —la Estrella y el Púlsar la usan así—; ejecutada por un `localScale` es remuestreo. **Una fórmula puede ser válida en un motor e inválida en otro, y el contrato no distinguía los dos.** → contrato, *Animación*.
102. **El reparto del idle pasa a tener dos criterios.** Al de siempre —¿algo interno que mover?, que decide si hace falta hoja— se le añade ¿cabe en una traslación entera?, que decide si el transform puede encargarse. El reparto actual no cambia, pero ahora se sostiene por los dos. De paso queda escrito que la sacudida de `Asustado` va en píxeles enteros, que nadie había dicho. → contrato, [14.0](./NOVA_Estados_Animaciones.md#140-reglas-generales) #4, [14.2](./NOVA_Estados_Animaciones.md#142-animación-de-susto).

**Al corregir el anillo del Magnetar (2026-09-26).**

97. **Un radio escrito donde iba un diámetro.** El número correcto se calculó y se usó para la magnitud equivocada, que es la forma más difícil de detectar: el texto *enseña* la cuenta buena y aun así concluye mal. → [A.4.1](./NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar), contrato *Resolución*.
98. **La prohibición de escalar ya estaba escrita dos veces y aun así se incumplió una tercera.** El protón (A.4.2) y [T.8](#t8-escala-del-tablero--cerrada) razonan los dos que `Filter Mode: Point` no admite factores no enteros, pero A.4.1 pedía un único anillo para cinco radios. **Una regla puede estar bien escrita y mal aplicada**, y eso no lo detecta releer la regla: lo detecta contar. Regla de trabajo nueva: **un asset que existe en varios tamaños es varios archivos, salvo que los factores sean enteros.** → [A.4.1](./NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar).
99. **Tres de los cinco radios no caen en píxel exacto** —2.4 u son 153.6 px—, y queda escrito que el dibujo redondea al píxel y la lógica no. Es inofensivo hoy (0.4 px contra un margen legible de 6.4 px) y desaparecería solo si T.8 recorta los radios a la familia 2.0–3.0. → [A.4.1](./NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar), T.8 punto 6.

**Al cuadrar la tabla de niveles (2026-09-26).**

95. **Tercer dato derivado escrito a mano, tercera contradicción.** Como el caso 7 y como las dos listas de tareas, `N` repetía información que ya estaba en otras columnas y se desvió. Queda declarada derivada en la leyenda de [11.2](./NOVA_GameDesign_Spec.md#112-diseño-de-niveles), con la regla de desempate escrita: manda la suma. **El patrón ya tiene nombre en este proyecto y conviene buscarlo antes que sus síntomas.**
96. **El nivel que vigilábamos no era el peor.** T.7 #28 señalaba el 4 por arrancar con una sola Estrella, pero al contar nodos por bando salió que el 6 y el 8 tienen la misma debilidad y peor proporción: **2.0, 3.0 y 4.0** de nodos Enemigo/Jugador. El orden de revisión pasa a ser 8, 6, 4. → T.7 #28.

**Al desambiguar el clic izquierdo (2026-09-26).**

92. **Un control con tres significados no es un control.** El clic izquierdo sobre un nodo propio seleccionaba (13.1), enviaba el 50 % si ya había selección (13.1, porque un nodo propio también es destino válido) y abría la rueda si era Púlsar (13.5). Se resuelve con **modo**, no con excepciones: la selección activa es la que decide. → [13.1](./NOVA_GameDesign_Spec.md#131-ratón).
93. **El Asteroide tenía un hueco, no una excepción.** No puede enviar ([5.2](./NOVA_GameDesign_Spec.md#52-envío)), así que nunca es origen y su clic izquierdo no estaba asignado a nada. Ahí entra el menú de encendido, que hasta hoy **no tenía forma documentada de abrirse** pese a ser la acción más repetida de la partida. El Púlsar es el caso contrario —sí puede enviar— y por eso su rueda se queda en el panel. → [13.7](./NOVA_GameDesign_Spec.md#137-menú-radial-de-encendido), [13.5](./NOVA_GameDesign_Spec.md#135-rueda-de-poderes-del-púlsar).
94. **Dos erratas de la misma tabla.** El botón de reconversión ofrecía «los tres destinos posibles» y siempre son **dos** ([8.1](./NOVA_GameDesign_Spec.md#81-reglas) prohíbe volver a Asteroide y nadie se reconvierte a su propio tipo), y le faltaba la condición «si es propio» que el caso 23 de la [sección 17](./NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución) ya exigía. → [13.3](./NOVA_GameDesign_Spec.md#133-panel-de-clic-derecho).

**Al dejar una sola lista de tareas (2026-09-26).**

91. **Dos documentos mantenían la misma lista y por eso se contradijeron.** No era que el §19 dijese algo falso: era que añadir o mover una tarea obligaba a editar dos archivos. El §19 conserva el único trabajo que nada más hace —decir cuándo una fase está terminada— y T.4 se queda con el qué. **Un criterio de salida no es una lista de tareas**, y confundirlos fue lo que produjo una fase 1 que no podía cumplir su propio criterio (desviación 90). → [19](./NOVA_GameDesign_Spec.md#19-fases-y-criterios-de-salida), T.4, T.6.

**Al mover el encendido a la fase 1 (2026-09-26).**

90. **Un criterio de salida que su propia fase no podía cumplir.** El de la fase 1 exige encender un Asteroide, pero `NodeIgnition` vivía en la fase 2 y dependía de la última tarea de la fase 1. Se detectó comparando [19](./NOVA_GameDesign_Spec.md#19-fases-y-criterios-de-salida) contra T.4, que **mantienen la misma lista de trabajo por duplicado** y han derivado: once tareas de T.4 no figuran en el §19, y `NodeLevelComponent` aparece en fases distintas en cada uno. El encendido queda corregido; la duplicación sigue abierta (T.7 #33). → T.4 fase 1, [19](./NOVA_GameDesign_Spec.md#19-fases-y-criterios-de-salida).

**Al corregir el Magnetar a 0 (2026-09-26).**

89. **Tres reglas decían que sí y una prosa decía que no.** [6.2](./NOVA_GameDesign_Spec.md#62-resolución-de-impacto) (`0 < 0` es falsa → captura), [6.4](./NOVA_GameDesign_Spec.md#64-casos-de-captura-por-tipo) («cualquier nodo a 0 → Carga 1») y [7.3](./NOVA_GameDesign_Spec.md#73-cero-absoluto) («de Carga 1 o superior») ya coincidían; solo el caso 7 y la decisión 18 #9 sostenían el «Carga ≥ 2», y lo hacían por una resta mal leída. **La resistencia ×2 encarece vaciar un Magnetar, no rematarlo.** → [17](./NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución) caso 7, [18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #9.

**Al retirar «IA» como nombre del oponente (2026-09-20).**

86. **El término más usado del proyecto no estaba en el glosario normativo.** «IA» aparecía 39 veces en cinco archivos y la sección 2 no la definía. → [2](./NOVA_GameDesign_Spec.md#2-glosario-normativo), que ahora tiene `Algoritmo del Enemigo`.
87. **La etiqueta era falsa y el contenido correcto.** Nada hablaba de entrenar ni inferir; 12.7 ya exigía pureza y determinismo. Solo había que cambiar el nombre, no el diseño. → [12.7](./NOVA_Algoritmo_Enemigo.md#127-contrato-de-implementación).
88. **Quedó un uso legítimo y se precisó en vez de borrarlo.** En T.1 «IA» es el asistente, y el prompt lo dice en voz alta para que la distinción se mantenga sola. De paso `EnemyBrain` → `EnemyController`. → T.1, [15.7](./NOVA_GameDesign_Spec.md#157-input-enemigo-y-ui).

**Al corregir el objetivo válido (2026-09-20, misma sesión que el bloque siguiente).**

83. **«Enemigo» y «no propio» no son lo mismo.** La primera redacción dejó fuera al Magnetar neutral, único nodo Neutral con nivel. Corregido a «nodo encendido no propio»: una regla en vez de una lista. → [7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido).
84. **El Asteroide se excluye por el doble coste, no por ser inútil.** Un poder que lo capturase saltaría los 10 + 10 que son el precio de expandirse. → [4.1](./NOVA_Nodos.md#41-asteroide-nivel-0), [7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido).
85. **Admitir objetivos neutrales reabre una verificación de arte cerrada.** Los dos tintes pueden caer ahora sobre la rampa neutral, que nunca se midió. Ningún asset cambia. → T.7 #29, [A.3](./NOVA_Assets_Diseno.md#a3-estados-y-overlays--141).

**Al quitarle el alcance al Púlsar (2026-09-20).**

76. **Quitar el alcance obligó a inventar el objetivo válido.** Era lo único que decía a qué se podía apuntar. Un límite que se quita hay que reponerlo en otro eje; aquí el eje es el tipo de objetivo. → [7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido).
77. **El proyectil no sobrevivió, y no por estética.** A 6 u/s la diagonal son 6.1 s. El haz de 0.25 s es consecuencia aritmética de quitar el límite, no una decisión aparte. → [7.2.2](./NOVA_GameDesign_Spec.md#722-el-haz).
78. **La tercera fila del robo de carga sobrevivió, comprimida.** De «proyectil en vuelo» a «dentro de sus 0.25 s»: casi imposible de provocar, pero sin ella capturar un Púlsar a mitad de haz no tendría resolución escrita. → [7.5](./NOVA_GameDesign_Spec.md#75-agujero-de-gusano).
79. **El algoritmo perdió su filtro barato y hubo que pagarlo con una fórmula.** `ScorePoder` es la primera función añadida a la sección 12 por un cambio de reglas. → [12.3.1](./NOVA_Algoritmo_Enemigo.md#1231-función-de-puntuación-lanzar-poder).
80. **Un asset desapareció y tres cambiaron de naturaleza.** `ART-WLD-RANGE` fuera; los tres proyectiles pasan a haces. El inventario baja de 95 a 94 y el trabajo sube: un haz se estira y se rota. → [A.4.2](./NOVA_Assets_Diseno.md#a42-protón-y-haces-de-poder--143), T.7 #30.
81. **La atenuación del apuntado dejó de ser arte y pasó a ser código.** De «dentro o fuera del círculo» a «válido o no válido», resuelto bajando el color. Una figura menos dentro del tablero. → [A.4.3](./NOVA_Assets_Diseno.md#a43-selección-y-apuntado--131).
82. **La decisión de escala del tablero se abarató sola.** Ya no hay ningún número de poder medido en unidades, así que encoger el tablero no los toca. Quitar una regla puede desbloquear otra decisión abierta. → T.8.

**Al cerrar `ART-ST-BUFF` (2026-09-19).**

71. **El buff no es un tinte: es un glifo.** A.3 y 14.7 se contradecían y gana 14.7. La tabla de casos de A.3 pasa de tres a cuatro: hay un cuarto sitio donde puede vivir un overlay, la esquina de la celda. → [A.3.8](./NOVA_Assets_Diseno.md#a38-art-st-buff--power-up-activo), [14.7](./NOVA_Estados_Animaciones.md#147-prioridad-visual).
72. **El cuadrado libre de la esquina son 23 px** en los catorce nodos, y 33 contando solo Magnetar y Púlsar. A.3.1 midió 20 y dijo que ahí no cabe un `zzz`; no dijo que sí cabe una marca. → [A.3.8](./NOVA_Assets_Diseno.md#a38-art-st-buff--power-up-activo).
73. **`FX-BUFF` no va sobre todos los nodos de la facción, aunque el efecto sí.** El Asteroide no gana nada con ningún power-up. Consecuencia gratis: ningún nodo lleva nunca los dos buffs. → [14.5](./NOVA_Estados_Animaciones.md#145-animaciones-puntuales-sin-estado).
74. **Segundo asset que llega con «16 frames, bucle» y sale estático.** Regla nueva: el número de frames se mide, no se planifica. → contrato, *Animación*.
75. **El color de un overlay de power-up es el del power-up, no el de la facción**, al revés que el halo del drop: allí el icono flotante dice cuál es, y sobre un nodo buffeado no flota nada. → [A.3.8](./NOVA_Assets_Diseno.md#a38-art-st-buff--power-up-activo), [A.4.4](./NOVA_Assets_Diseno.md#a44-power-ups--9).

**Al cerrar `ART-ST-CAP` (2026-09-19).**

66. **La geometría no cambia con la facción en ninguno de los catorce nodos**, comprobado byte a byte sobre los 34 sprites. 14.0 #3 lo decía y nadie lo había medido. → [14.0](./NOVA_Estados_Animaciones.md#140-reglas-generales).
67. **La desviación 63 se parte en dos.** «Entrar desde nada» solo vale para un efecto que se prepara; uno que se dispara arranca en su pico. → [A.3.7](./NOVA_Assets_Diseno.md#a37-art-st-cap--capturado), contrato.
68. **El blanco puro se cobra.** El índice 0 de toda rampa está reservado al especular, y `ART-ST-CAP` es el único overlay del proyecto que lo usa como color propio. → contrato, *Paleta*.
69. **14.1 se precisa, no se corrige.** Conserva el blanco y la onda que sale; quita el relleno del flash y añade un segundo anillo que entra. → [14.1](./NOVA_Estados_Animaciones.md#141-estados-del-nodo).
70. **`FX-CAP` deja de ser el efecto genérico y pasa a ser el específico.** Gastar «flash + onda» en la captura habría dejado sin gesto a los cuatro destellos cortos de A.5. → [A.5](./NOVA_Assets_Diseno.md#a5-vfx-puntuales--145).

**Al cerrar `ART-ST-CONV` (2026-09-19).**

60. **«Silueta neutra» de 14.1 quiere decir sin tipo, no sin bando.** Durante los 3 s el nodo sigue siendo de su dueño —tanto que puede ser capturado—, así que pintarlo gris contradiría a 8.1. → [14.1](./NOVA_Estados_Animaciones.md#141-estados-del-nodo).
61. **`ART-ST-CONV` lleva variante de facción**, segundo overlay que la lleva tras `ART-ST-IGN` y por motivo distinto: aquel marca posesión porque el sprite no lo hace, este porque no puede dejar de marcarla. → [A.8](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).
62. **Un overlay aditivo no puede dibujar un colapso.** Con alfa binario solo suma píxeles: la silueta nueva la pone el animador cambiando de sprite y el asset solo tapa el corte. → [A.3.6](./NOVA_Assets_Diseno.md#a36-art-st-conv--reconvirtiendo). *Pendiente: no llegó al contrato (T.7 #32).*
63. **`verifica_tinte` gana un modo transitorio.** Un efecto con duración no se juzga por si cierra, sino por si entra desde nada y sale a nada, y de la cara importa cuántos frames la deja ciega. → [A.3.6](./NOVA_Assets_Diseno.md#a36-art-st-conv--reconvirtiendo).
64. **Primera y única exención al contador de nivel**, porque aquí el nivel está cambiando. Su duración coincide con la del cambio y **no se hereda**. → [A.8](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).
65. **El blanco puro queda reservado a `FX-CAP`.** `ART-ST-CONV` destella en color de facción precisamente para dejárselo. → [14.5](./NOVA_Estados_Animaciones.md#145-animaciones-puntuales-sin-estado).

**Al cerrar `ART-ST-DECAY` (2026-09-19).**

55. **El cuerpo común a los trece nodos son 505 px, y la cara se come casi todo:** quedan 228, y 195 de esos 228 están debajo de ella. «Pintar sobre el cuerpo» y «pintar en los trece» solo son compatibles en una franja. → [A.3.5](./NOVA_Assets_Diseno.md#a35-art-st-decay--infectado).
56. **La desviación 49 se queda corta: un tinte registra con TRES cosas.** La tercera son los otros tintes: dos campos de Bayer apilados acaban en una mancha. → [14.6](./NOVA_Estados_Animaciones.md#146-matriz-de-compatibilidad), [A.3.5](./NOVA_Assets_Diseno.md#a35-art-st-decay--infectado).
57. **`verifica_tinte` gana tres medidas**, las tres pedidas por este asset: cierre del bucle, frames distintos de verdad y convivencia con otro overlay. → [A.3.5](./NOVA_Assets_Diseno.md#a35-art-st-decay--infectado).
58. **Y una cuarta, porque una de las que había medía otra cosa.** *Contraste* promediaba solo donde el overlay cae encima del nodo, así que castigaba al que menos lo tapa. → [A.3.5](./NOVA_Assets_Diseno.md#a35-art-st-decay--infectado).
59. **Regla nueva: el ciclo de un aura es radial, no giratorio.** Hacer viajar la fase no cierra el bucle salvo dando una vuelta entera, y eso es un aura que gira. → contrato, *Animación*.

**Al cerrar `ART-ST-FROZEN` (2026-09-19).**

48. **`Congelado` va sobre trece sprites, no catorce.** 14.1 lo limita a nodos encendidos y el Asteroide nunca lo está. Vale igual para `Infectado` y el buff; `Reconvirtiendo` son doce. → [14.1](./NOVA_Estados_Animaciones.md#141-estados-del-nodo).
49. **La desviación 47 era falsa a medias.** Un tinte sí registra: con la cara y con el contador de nivel, que entre las dos ocupan todos los radios de la celda. → [A.3.4](./NOVA_Assets_Diseno.md#a34-art-st-frozen--congelado).
50. **Tercera vara de verificación: `verifica_tinte`**, junto a `verifica` (glifos) y `verifica_ojos` (susto). La vara de un glifo suspendería a un tinte por hacer su trabajo. → [A.3.4](./NOVA_Assets_Diseno.md#a34-art-st-frozen--congelado).
51. **Regla nueva: nada que se dibuje encima de otro dibujo lleva contorno.** Medido sobre «El bloque»: sus aristas se comían el 71 % de un frente de onda del Púlsar. → contrato, *Doble contorno*.
52. **Dos primitivas nuevas, catálogo a 18:** `tramar` (el dithering sacado fuera de cada geometría, para que dos propuestas se midan igual) y `veta_luz` (la línea que no suma huella). → contrato, *Catálogo*.
53. **El ancla funcional no siempre está en el índice 3.** `#B8F0FF` vive en `hielo[2]`. Se corrige el contrato, no la paleta. → contrato, *Paleta*.
54. **14.7 no cambia, y conviene saber por qué.** Un tinte va por encima de los ojos; no se arregla reordenando capas sino dejando hueco a la cara en la geometría del tinte. → [14.7](./NOVA_Estados_Animaciones.md#147-prioridad-visual).

**Al cerrar `ART-ST-SCARE` (2026-09-18).**

43. **Un overlay puede tener geometría fija y posición variable.** La separación entre ojos va de 9.5 a 16.0 px en los 34 sprites: ninguno de posición fija aterriza, y escalarlo está prohibido. → [A.3.3](./NOVA_Assets_Diseno.md#a33-art-st-scare--asustado).
44. **La tabla de anclaje es un entregable, no un comentario.** Vive en `tools/nova_estados.OJOS`. Primera vez que un asset necesita **datos** además de píxeles. → [A.3.3](./NOVA_Assets_Diseno.md#a33-art-st-scare--asustado).
45. **Regla nueva: una hoja cuyos frames no son distintos no es una animación.** Medido antes de decidir, no después. → contrato, *Animación*.
46. **Regla nueva: lo que no cabe en una escala cabe en tallas discretas más desplazamiento entero.** Estaba implícita en el protón; con un segundo usuario sube a *Reglas de trabajo*. → contrato.
47. **A.3 termina de partirse en tres casos**, escrito en su cabecera. ⚠️ Su última frase la desmintió el asset siguiente (ver 49). → [A.3](./NOVA_Assets_Diseno.md#a3-estados-y-overlays--141).

**Al cerrar `ART-ST-IGN` (2026-09-18).**

38. **`SinEncender` pasa a tener dos lecturas y A.3 gana un ID.** El animador elige por `energy >= 10`; `NodeState` y la matriz de 14.6 no se tocan. → [A.3.2](./NOVA_Assets_Diseno.md#a32-art-st-ign-y-art-st-ready--sinencender).
39. **El *parpadeo* de 14.1 se reasigna.** Deja de pedirse para todo `SinEncender` y pasa a ser la firma de la lectura accionable. → [14.1](./NOVA_Estados_Animaciones.md#141-estados-del-nodo).
40. **`art/states/v<n>/` no aguantaba el segundo overlay** —dos v1 caían en la misma carpeta— y pasa a `art/states/<estado>/v<n>/`. Corrige las rutas escritas el mismo día. → [A.1](./NOVA_Assets_Diseno.md#a1-convención-de-nombres).
41. **Las comprobaciones de overlay salen a `tools/nova_estados.py`.** Un asset se comprueba contra lo que va a tener al lado, con la misma vara en todas las propuestas, y el generador falla si algo no pasa. → contrato.
42. **Regla nueva: entre dos lecturas del mismo overlay, la accionable nunca pesa menos que la pasiva.** Se encontró contando, no mirando. → [A.8](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).

**Al cerrar `ART-ST-SLEEP` (2026-09-18).**

33. **La convención de propuestas deja de ser solo de nodos.** El generador declara `ESTADO` y `VERSION`. *Corregida por la 40 en la ruta.* → [A.1](./NOVA_Assets_Diseno.md#a1-convención-de-nombres).
34. **La regla de tramado se parte en dos.** Se traman los overlays que cubren el cuerpo; un glifo no cubre nada, compite con el fondo y va sólido con contorno. → [A.3](./NOVA_Assets_Diseno.md#a3-estados-y-overlays--141), contrato.
35. **«Los overlays son independientes del tipo» se matiza, y con medidas.** El mayor hueco libre en esquina son 20 px y un `zzz` legible necesita 33×16, así que `Dormido` se coloca contra la silueta del Asteroide. → [A.3.1](./NOVA_Assets_Diseno.md#a31-art-st-sleep--dormido).
36. **La capa 4 de 14.7 pasa a llamarse «Glifos y overlays»**, y se documenta que un glifo no pisa un solo píxel del sprite. Es lo que evita que choque con la capa 6. → [14.7](./NOVA_Estados_Animaciones.md#147-prioridad-visual).
37. **Un asset de A.3 se verifica contra el sprite de verdad**, abriendo el PNG. Y un glifo contable añade una comprobación propia: que las piezas no se toquen. → [A.3.1](./NOVA_Assets_Diseno.md#a31-art-st-sleep--dormido).

**Al volver el Magnetar a la v1 (2026-09-18).**

29. **El Magnetar vuelve a transform en el reparto del idle**, y con eso la desviación 11 queda revertida: el motivo de la regla no le aplica a un cuerpo tallado. → contrato, [A.2.4](./NOVA_Assets_Diseno.md#a24-magnetar--44).
30. **El contador de nivel del Magnetar pasa a ser posicional.** Efecto lateral documentado: la huella deja de ser monótona (0.64 / 0.94 / 0.94 / 1.05 / 1.05 u). → [A.2.4](./NOVA_Assets_Diseno.md#a24-magnetar--44).
31. **La tabla de ejes de A.8 deja de ser disjunta**, y corrige la 23. → [A.8](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).
32. **Se invierte qué primitivas están en uso.** `poliedro` y `veta` pasan a ejercitadas; `linea_dipolar` al otro lado. El catálogo no se toca: se descarta un diseño, no una herramienta. → [A.7](./NOVA_Assets_Diseno.md#a7-resumen-de-progreso).
33 *bis*. **Magnetar y Asteroide comparten ahora simetría e idle**, y *espacio interior* pasa a macizo en los cuatro. `calado` queda como valor de eje libre. *(Publicada originalmente como un segundo «23»; renumerada aquí para deshacer la colisión.)* → [A.8](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).

**Al cerrar los fondos (2026-09-18).**

24. **Módulo nuevo `tools/nova_fondo.py`, con 7 primitivas de fondo.** Los fondos son una familia con reglas distintas: solo comparten paleta y matriz de Bayer. → contrato, *Fondos*.
25. **Tres rampas nuevas, la paleta pasa de 20 a 22:** `bg_s1/2/3`. Existen porque todas las demás significan algo; `gusano`, `hielo`, `decaimiento`, `rayo` e `hidrogeno` quedan prohibidas en un fondo. → contrato, *Paleta*.
26. **Presupuesto de contraste**, verificado sobre el PNG: la media de cualquier ventana de 96×96 por debajo del tono más oscuro de un cuerpo de nodo. Se corrigió una vez. → [A.4.5](./NOVA_Assets_Diseno.md#a45-fondos).
27. **En un fondo se modula la cobertura, no el tono.** Una banda que cubre el 100 % de una ventana ya está por encima del presupuesto, por oscura que sea. → [A.4.5](./NOVA_Assets_Diseno.md#a45-fondos).
28. **Regla nueva: nada del fondo puede medir lo que mide un nodo** (0.6–1.2 u). Lo que caiga en esa franja es un nodo hasta que el jugador intente clicarlo. → contrato, *Fondos*.

**Al cerrar el Asteroide (2026-09-18).**

18. **Cuatro primitivas nuevas, catálogo a 16:** `bulto`, `crater`, `esquirla` y `estratos`. Las dos últimas salieron de la propuesta descartada y se quedan. → contrato, *Catálogo*.
19. **La concavidad entra en el contrato como material propio.** Con ella, contorno y sombreado son dos ejes independientes y las tres combinaciones vivas quedan documentadas. → contrato, *Los tres cuerpos*.
20. **Regla nueva: un accidente de superficie modula la banda que hay, no la sustituye.** Pintado en absoluto, un hoyo en la sombra se lee como un bulto. → contrato.
21. **El especular se puede apagar**, única excepción viva a la regla 3: una roca mate no es material pulido. → contrato, *Regla 3*.
22. **A.2.1 se corrige.** La posesión del Asteroide la marca el overlay `ART-ST-IGN`, no el sprite: una variante de facción es la misma geometría con otra rampa. → [A.2.1](./NOVA_Assets_Diseno.md#a21-asteroide--41), [14.0](./NOVA_Estados_Animaciones.md#140-reglas-generales).
23. **La partición de materiales queda cerrada con el cuarto valor:** el Asteroide solo refleja, sin luz propia. *Su segunda mitad la corrigió la 31.* → [A.8](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).

**Al cerrar el Magnetar (2026-09-18).**

11. ~~**El Magnetar pasa de transform a hoja de 16 frames.**~~ **Revertida por la 29.** Lo que queda en pie es la regla que la justificaba: el criterio es el motivo, no la lista. → contrato.
12. **Tres primitivas nuevas, catálogo a 12:** `poliedro` (la regla 1 muestreada por cara), `veta` y `linea_dipolar`. → contrato, *Catálogo*.
13. **La partición de materiales pasa a ser regla del contrato:** Estrella luz continua, Púlsar luz tramada, Magnetar materia. Eje de separación de primer orden. → contrato.
14. **Dos reglas de trabajo nuevas:** un nodo no se construye con las piezas de otro, y el material separa tanto como la silueta. → contrato, *Reglas de trabajo*.
15. **A.7 queda desmentida en su propia predicción.** Daba por hecho que el Magnetar heredaría `anillo` y `anillo_orbital`; no heredó ninguna, y fue lo mejor que le pasó. → [A.7](./NOVA_Assets_Diseno.md#a7-resumen-de-progreso).
16. **Tabla de ejes de diseño ocupados en A.8**, para consultarla *antes* de empezar un nodo. Sale de descubrir lo mismo por las malas tres veces. → [A.8](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).
17. **Restricción geométrica documentada:** cara grande y cinco lazos anidados no caben juntos en la celda de 96. → T.7 #9.

**Al cerrar el Púlsar (2026-09-18).**

7. **`art/nodes/` pasa a una carpeta por nodo y una subcarpeta por propuesta**, y los generadores a `gen_<nodo>_v<n>.py` con el sufijo siempre presente. → [A.1](./NOVA_Assets_Diseno.md#a1-convención-de-nombres).
8. **Dos primitivas nuevas, catálogo a 9:** `esferoide` (y `esfera` pasa a delegar en ella, verificado pixel a pixel) y `cono_trama`. → contrato, *Catálogo*.
9. **Dos reglas de trabajo nuevas:** dos nodos no se separan repintándolos, y una huella se mide sobre el ciclo entero, no sobre el frame 0. → contrato, *Reglas de trabajo*.
10. **`elipse_base_plana` está en el contrato y nunca se implementó.** No se ha tocado. → T.7 #7.

**Paleta, pipeline y Estrella (2026-09-18).**

1. **PPU 64, no 96.** La celda de 96 px pasa a valer 1.5 u. → contrato, *Resolución*.
2. **12 fps fijo**, en vez de «por defecto 16 frames». A.3 y A.5 recalculadas: los valores viejos implicaban entre 4 y 15 fps según el efecto. → contrato, *Animación*.
3. **El protón no escala de forma continua:** tres sprites discretos. Anula la fórmula de 14.3. → [A.4.2](./NOVA_Assets_Diseno.md#a42-protón-y-haces-de-poder--143).
4. **`✅` cambia de significado en A.0:** «generado y disponible en `art/`», no «integrado en Unity». La definición anterior era circular con T.2 #3. → [A.0](./NOVA_Assets_Diseno.md#a0-cómo-usar-este-documento).
5. **Cuatro primitivas nuevas**, y el catálogo de código deja de estar copiado en el `.md`: pasa a `tools/`. → contrato, *Catálogo*.
6. **A.2.2 reescrita.** La Estrella ya no es «corona + protuberancias» sino cuerpo + atmósfera de dos capas con cizalla. → [A.2.2](./NOVA_Assets_Diseno.md#a22-estrella--42).

---

## T.6 Criterios de salida por fase

**Los define la [sección 19](./NOVA_GameDesign_Spec.md#19-fases-y-criterios-de-salida) del maestro**, que es su único dueño; esta tabla es una copia de consulta y se actualiza desde allí, nunca al revés. Una fase no se cierra sin cumplirlos.

| Fase | Criterio |
|---|---|
| 0 | El proyecto abre en Unity, `Nova.Core` compila sin referenciar `UnityEngine` y un test vacío corre en EditMode |
| 1 | Se puede capturar un Asteroide, encenderlo como Estrella y tomar un nodo enemigo |
| 2 | Los cuatro tipos son funcionales y el nivel 3 es jugable |
| 3 | Los tres poderes funcionan y pasan los casos 8–14 y 27–35 de la sección 17 |
| 4 | El algoritmo del mapa 3 vence a un jugador descuidado |
| 5 | Los 9 niveles son completables de principio a fin |
| 6 | Todos los assets de [A.7](./NOVA_Assets_Diseno.md#a7-resumen-de-progreso) están ✅ e integrados en Unity |

---

## T.7 Pendientes abiertos

| # | Asunto | Dónde se resuelve |
|---|---|---|
| 1 | ~~5 decisiones de diseño visual sin cerrar~~ | ✅ Cerradas el 2026-09-18. [A.8](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas) |
| 2 | Coordenadas exactas de los 9 niveles | Apéndice del maestro. Sin empezar |
| 3 | Audio: no hay inventario equivalente al de A | Pendiente de crear |
| 4 | Estilo de la UI: paneles, botones y estados | Hay paleta y tipografía, falta el lenguaje visual. Bloquea A.6 |
| 5 | Empaquetado en atlas | El pivote está decidido; el atlas no |
| 6 | Hojas de idle sin ID de inventario | Quedan **dos** familias, no tres: Estrella y Púlsar tienen el idle diseñado y sus generadores animan por `t`, pero falta volcarlas a `_sheet.png` y ninguna tiene ID. El Magnetar sale de este pendiente: resuelve su idle por transform (A.2.4) y las 15 hojas generadas son de la v2 descartada |
| 7 | `elipse_base_plana` documentada y no implementada | Está en el catálogo de [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md) pero no en `tools/nova_core.py`. Ningún asset la ha necesitado: en NOVA nada se apoya en un suelo. Decidir si se implementa o si sale del contrato |
| 8 | Versiones descartadas | **Quince propuestas completas** con ficha, cada una con su generador y su PNG, conservadas por A.1. La ficha de cada una —qué perdió y bajo qué condición vuelve— está en los ítems #10 y #14 a #27 de esta misma tabla; **no se listan aquí para no mantener dos listas de lo mismo**. Borrarlas es una decisión pendiente, no un olvido |
| 9 | ~~Cara grande XOR cinco lazos anidados~~ | ✅ **Deja de aplicar al nodo vivo.** La desigualdad sigue siendo cierta —cinco lazos necesitan ~4 px de separación, 16 px de radio para los cuatro huecos, y con el exterior topado en 38 px (1.2 u) el interior no pasa de 22 px, lo que deja el cuerpo en ≤ 14 px de semieje—, pero quien la pagaba era la v2. «El Yunque» no anida nada y su cuerpo llega a 17.5 px de semieje horizontal. Vuelve a la mesa solo si vuelve la v2 (#14) |
| 10 | Asteroide v2, reabrible bajo condición | Perdió porque `L` es global: sus estratos salen a la misma diagonal en todos los asteroides de la pantalla y ocho iguales leen como papel pintado. Vuelve a la mesa si 11.2 acaba colocando dos o tres asteroides por nivel en vez de ocho, o si se aceptan varias variantes de estratificación **como sprites distintos** (A.2.1 pasaría de 3 archivos a 6 o 9) |
| 11 | ~~Iconos de los dos power-ups~~ | ✅ Cerrado el 2026-09-18. [A.4.4](./NOVA_Assets_Diseno.md#a44-power-ups--9) |
| 12 | ~~**Escala del tablero: 32×18 u es demasiado grande**~~ | ✅ **Cerrada el 2026-09-28** en 20 × 11.25 u y **reajustada el mismo día a 15 × 8.4375 u**, porque la Pixel Perfect Camera no hace ×1.5. Ver [T.8](#t8-escala-del-tablero--cerrada) |
| 35 | ~~Los radios del Magnetar, a decidir~~ | ✅ **Cerrado el 2026-10-02** con el usuario: familia ×0.75, `1.5 · 1.75 · 2.0 · 2.25 · 2.5 u`, y cadencia ×4/3, `1.33 · 1.87 · 2.53 · 3.33 · 4.27` disp/s, para conservar la Carga anulada (desviación 149). [4.4](./NOVA_Nodos.md#44-magnetar), [A.4.1](./NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar) y `Magnetar.asset`. Texto original: **Actualizado el 2026-09-28 al reajustar T.8:** en 15 u el N5 de hoy (3.6 u) cubre el **48 %** del ancho de diámetro y la familia `2.0…3.0` de abajo todavía el **40 %**. Una familia ×0.75 conserva la proporción que tenían a 20 u y sigue en píxel exacto: `1.5 · 1.75 · 2.0 · 2.25 · 2.5 u` son 96, 112, 128, 144 y 160 px, con el N5 al **33 %** y en un lienzo de 320. Es balance, así que se decide, no se deduce. Texto original: el punto 6 de *Al cerrarla, tocar* de [T.8](#t8-escala-del-tablero--cerrada) señala una familia que arregla tres cosas de golpe: `2.0 · 2.25 · 2.5 · 2.75 · 3.0 u` son 128, 144, 160, 176 y 192 px de radio, **los cinco en píxel exacto**; el N5 vuelve a caber en un lienzo de 384 en vez de 512 y su diámetro baja del 36 % al 30 % del ancho del tablero nuevo. Hoy los radios son `2.0 · 2.4 · 2.8 · 3.2 · 3.6` y **tres de los cinco no caen en píxel redondo** (2.4 u son 153.6 px). **No se toca todavía**: cambia [4.4](./NOVA_Nodos.md#44-magnetar), que es balance, y no bloquea nada hasta que se diseñe A.4.1. Decidirlo antes de generar los 16 anillos |
| 36 | ~~Determinismo del algoritmo y el paso de tiempo~~ | ✅ **Cerrado el 2026-09-28** antes de `T1-08`: toda la lógica de juego corre en `FixedUpdate` a **50 Hz** (`Fixed Timestep = 0.02`) y la presentación en `Update`. Los temporizadores cuyo intervalo no es múltiplo de 20 ms acumulan en enteros y arrastran el sobrante. [15.10](./NOVA_GameDesign_Spec.md#1510-orden-de-ejecución) y [18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #22. Texto original: [12.7](./NOVA_Algoritmo_Enemigo.md#127-contrato-de-implementación) exigía determinismo y ningún documento fijaba el paso de tiempo; con `deltaTime` variable la partida dependía de los FPS |
| 37 | ~~Física 2D, capas y *picking*~~ | ✅ **Cerrado el 2026-09-28** antes de `T1-11`: clic con `Physics2D.OverlapPoint` sobre la capa **`Nodes`**, contra el `CircleCollider2D` *trigger* de `NodeCollider2D` con el radio de la huella (0.3–0.6 u); lazo sin física, por centros dentro del rectángulo consultando `NodeRegistry`; `Nodes` es la única capa nueva. [15.5](./NOVA_GameDesign_Spec.md#155-gameplay--componentes-de-nodo) y [18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #23. Texto original: 15.5 declaraba el collider pero no cómo se resolvía el clic, qué capas existían ni cómo iba el lazo |
| 38 | **`MainMenu`, `LevelSelect` y el menú de pausa, sin diseñar** | Las dos escenas están declaradas en [15.2](./NOVA_GameDesign_Spec.md#152-estructura-de-carpetas) y `T5-06` pide la pantalla de selección, pero **ninguna sección del maestro las describe**. Del menú de pausa solo se sabe que lo abre `Esc` ([13.1](./NOVA_GameDesign_Spec.md#131-ratón)) y que desde él se reinicia el nivel ([10.3](./NOVA_GameDesign_Spec.md#103-fin-de-nivel)). Falta su contenido. Bloquea `T5-05` y `T5-06` |
| 39 | **Idioma y localización** | Toda la UI escrita en los `.md` está en español —`NIVEL MÁXIMO`, `DORMIDO`, `Siguiente`, `Repetir`— y **no hay decisión escrita** sobre si el juego es solo en español o se prevé inglés. Si va a haber dos idiomas, los textos no pueden nacer escritos a mano en los prefabs. Se decide antes de `T2-03`. **`T2-02` ya escribió los primeros:** los nombres de las tres opciones del menú de encendido, en un campo serializado de `IgnitionMenu` |
| 40 | **`ImpactResult.Destroyed` sin definir** | Aparece en 15.3, pero ni [6.2](./NOVA_GameDesign_Spec.md#62-resolución-de-impacto) ni [7.3](./NOVA_GameDesign_Spec.md#73-cero-absoluto) lo devuelven y los nodos nunca se destruyen (17, caso 4). Lo único que se destruye es un protón derribado por un Magnetar (6.3, caso 6), que **no impacta**. Opciones: quitarlo del enum, o darle ese significado y que lo emita quien derriba el protón. Se decide antes de `T2-04` (`MagnetarTurret`) |
| 41 | ~~Coste de encendido duplicado~~ | ✅ **Cerrado el 2026-09-28.** Un único `IgnitionCost` en `BalanceConfig`, y la fila «1 (encendido)» de [16.2](./NOVA_GameDesign_Spec.md#162-costes-de-nivel) queda como referencia. En las `NodeDefinition` el nivel 1 lleva coste 0 |
| 42 | ~~`INodeCommandIssuer` no está definido~~ | ✅ **Cerrado el 2026-09-29 en `T1-11`:** vive en `Nova.Gameplay` (`Gameplay/Commands/`), que es lo que referencian tanto `Nova.Input` como `Nova.Enemy`, y está anotado en [15.5](./NOVA_GameDesign_Spec.md#155-gameplay--componentes-de-nodo). Nace con `Faction` y `Send`; **cada acción de 12.2 entra con la tarea que la implementa** (`T1-11b`, `T2-03`, `T2-07`, `T3-02`, `T3-03`), porque un método sin código detrás sería adelantar trabajo. Texto original: lo citaban 3.1, 15.1 #5, 15.7 y 12.7 sin decir sus métodos ni su ensamblado |
| 43 | **`NodeState` es un solo valor y 14.6 permite hasta cuatro estados a la vez** | `Congelado` + `Infectado` + `Asustado` + `Reconvirtiendo` pueden coexistir, y un enum excluyente no puede representarlos. Hoy no molesta (`T1-07` solo tiene estados de base). Decidir antes de `T2-08` si pasa a `[Flags]`, a un estado base más banderas, o a varias máquinas |
| 44 | **La victoria deja Magnetares neutrales vivos** | 1.1 y 10.1 dicen que «ningún nodo puede quedar Neutral al final», pero la cascada de conquista solo convierte **Asteroides** neutrales. Un Magnetar neutral sin tomar queda Neutral en la pantalla de victoria. Se decide antes de `T5-03` |
| 45 | **El estancamiento no equivale a lo que dice** | 10.2 lo define como «sin Estrella y suma < 10, es decir, no puede encender ni reconvertir», pero reconvertir cuesta 20 o 30: sin Estrella, sin Asteroide propio y con 15 en un Magnetar no se puede hacer nada y no es derrota. Tampoco dice si una carga **en curso** cuenta como «poder cargado». Se decide antes de `T5-03` |
| 46 | **11.1 introduce mecánicas por mapa y 13.7 no las bloquea** | El Púlsar «se introduce» en el mapa 2 y el Gusano en el 3, pero el menú de encendido ofrece «siempre las tres» opciones: en el nivel 1 el Jugador puede encender un Púlsar y subirlo a nivel 3. O 11.1 describe solo lo que trae el Enemigo, o hace falta un bloqueo por nivel en `LevelDefinition`. Se decide antes de `T5-02` |
| 47 | **11.2 no da la Energía inicial de ningún nodo** | `NodeSpawnData` la pide (15.4) y la tabla solo da tipo y nivel. Va con las coordenadas (#2), antes de `T5-02` |
| 48 | **La sección 12 solo puntúa dos de sus siete acciones** | 12.3 puntúa atacar y 12.3.1 lanzar poder; encender, mejorar, reforzar, cargar poder y reconvertir no tienen fórmula y no pueden competir contra `UmbralDeAccion`. Además quedan sin definir el «margen» de *Envía al 100 %* (12.5), «a distancia» y «suficiente» del refuerzo (12.4), «lo expone» de `PenalizacionVaciado`, cómo actúa *Prioriza power-ups* y si el Magnetar neutral cuenta como «hostil» en `RiesgoDeRuta`. Y en 12.3 falta un paréntesis: tal como está escrita, `MultiplicadorAgresividad` solo multiplica `PenalizacionVaciado`. Bloquea `T4-02` |
| 28 | **Niveles 4, 6 y 8: el Jugador arranca con un solo nodo** | Con alcance global, el Enemigo puede congelar o infectar la única Estrella del Jugador desde el primer segundo y sin acercarse ([11.3](./NOVA_GameDesign_Spec.md#113-curva-de-dificultad)). No está roto sobre el papel —el poder tarda 8 s en cargarse y el Jugador puede haber encendido ya un segundo nodo—, pero es **lo primero que hay que mirar** en cuanto la fase 5 sea jugable. **Y el nivel 4 no es el peor de los tres**, cosa que se vio al cuadrar la tabla de [11.2](./NOVA_GameDesign_Spec.md#112-diseño-de-niveles): la proporción de nodos iniciales Enemigo/Jugador es **2.0 en el nivel 4, 3.0 en el 6 y 4.0 en el 8**, y en el 6 el Enemigo ya tiene un Púlsar(2) y en el 8 un Púlsar(3). El orden de revisión es 8, 6, 4. Si alguno resulta injusto, se corrige dando un segundo nodo inicial al Jugador en [11.2](./NOVA_GameDesign_Spec.md#112-diseño-de-niveles), **no** devolviéndole el alcance al Púlsar |
| 29 | **Los dos tintes sin verificar sobre la rampa neutral** | `ART-ST-FROZEN` y `ART-ST-DECAY` se midieron con `verifica_tinte` sobre las rampas de Jugador y Enemigo. Desde que el Magnetar neutral es objetivo de poder ([7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido)) pueden caer también sobre la **neutral**, la más apagada de las tres: el hielo `#B8F0FF` sobre un cuerpo gris `#8A93A6` tiene menos recorrido que sobre cian o magenta. Ningún asset cambia y la geometría es la misma; falta **volver a pasar la vara** sobre `mag_lvl1..5_neutral.png`. Los dos siguen ✅ mientras no se demuestre lo contrario y el riesgo está acotado a cinco sprites |
| 31 | **El Magnetar neutral cambia de dueño a mitad de partida** | Con el Gusano cuesta 60 de Energía en vez de 200 de Carga, y con Decaimiento se vacía en 50 s. Los dos bandos tienen esa llave, así que el muro de los niveles 5–9 ya no es permanente: hay que colocarlo donde robarlo sea **interesante y no decisivo**, y eso se decide al diseñar las coordenadas (#2) |
| 30 | **El haz es el primer asset que se estira y se rota** | Nada en `tools/nova_core.py` produce hoy una pieza de longitud variable: las 18 primitivas dibujan dentro de una celda. Los tres `ART-WLD-BEAM-*` necesitan un generador que saque **sección transversal + tramo repetible**, y su integración (`T6-11`) necesita rotación por `transform` sin romper el `Filter Mode: Point`. Es trabajo de pipeline que no estaba previsto en A.4.2 cuando eran proyectiles |
| 32 | ~~Una regla de overlay que no llegó al contrato~~ | ✅ **Cerrado el 2026-09-29 al revisar los `.md`:** la regla ya estaba en [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), *Reglas de trabajo* («Un overlay no puede quitar nada, así que lo que hay que quitar lo quita el animador»); lo que no se había hecho era cerrar este punto. Texto original: la desviación 62 cerró que un overlay aditivo no puede dibujar un colapso —con alfa binario solo suma píxeles, así que la silueta nueva la pone el animador cambiando de sprite— y dijo que eso «hay que decirlo en el contrato». Vive en [A.3.6](./NOVA_Assets_Diseno.md#a36-art-st-conv--reconvirtiendo) y en [A.8](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas), pero **no está en [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md)**. Detectado al comprimir el registro. Es una línea en *Reglas de trabajo*; no bloquea nada |
| 33 | ~~El §19 y T.4 mantienen la misma lista de trabajo por duplicado~~ | ✅ **Cerrado el 2026-09-26.** El §19 pasa a llamarse *Fases y criterios de salida* y conserva **solo los criterios**; sus viñetas remiten a T.4, que queda como única lista de trabajo. Se detectó al corregir la desviación 90, que fue causada exactamente por esta duplicación: once tareas de T.4 no figuraban en el §19 y `NodeLevelComponent` aparecía en fases distintas en cada uno. De paso se corrigió el criterio de la fase 6, que remitía a «el documento 2». **Queda una duplicación menor y vigilada:** los criterios viven también en T.3 y T.6 de este documento, pero las dos son ahora copias declaradas con el §19 como dueño |
| 34 | **La matriz de variantes del anillo del Magnetar** | A.4.1 quedó cerrada en **16 archivos** —IDLE, HOVER y NEUTRAL ×5 niveles, más SHOT— al descubrirse que un solo anillo no puede servir a cinco radios sin escalado no entero. Pero esos tres nombres mezclan **estado** (reposo / hover) con **facción** (neutral), y [A.8 #1](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas) exige el anillo **siempre visible en neutrales y enemigos** y **a demanda en los propios**: eso son tres rampas × dos estados, no tres archivos. Si la matriz acaba siendo completa serían 30 anillos en vez de 15. **Se decide al diseñar A.4.1**, y el total de [A.7](./NOVA_Assets_Diseno.md#a7-resumen-de-progreso) se moverá con ella |
| 13 | El documento no tiene número de versión | La plantilla de GDD exige un campo *Versión* y un *Historial de versiones*, y pide explícitamente **un número, no una fecha**. El proyecto lleva el registro T.5 pero no numera los `.md`. Decidir el esquema (`0.x` mientras no haya build jugable) y dónde vive: cabecera del maestro |
| 14 | Magnetar v2, reabrible bajo condición | «El Yunque» cuenta mal a 1× a partir de tres esquirlas: son el elemento más pequeño del sprite (A.2.4, *Lo que se paga*). La segunda lectura del nivel es el radio de A.4.1, siempre visible en neutrales y enemigos. **Si con el radio en pantalla el nivel sigue sin contarse en juego**, la v2 está entera en `art/nodes/magnetar/v2/` y vuelve a la mesa con su coste conocido: la cara más pequeña de los cuatro (#9) y 15 hojas de idle que producir |
| 15 | `ART-ST-SLEEP` v1, reabrible bajo condición | «El vaivén» no cuenta una historia: tres glifos que solo cabecean pueden leerse como un cartel pegado encima en vez de como el bicho respirando ([A.3.1](./NOVA_Assets_Diseno.md#a31-art-st-sleep--dormido)). **Si en juego el `zzz` parece UI y no aliento**, la v1 («La escalera», con relevo y traslación) está entera en `art/states/sleep/v1/`, con su coste conocido: apaga un glifo de golpe una vez por vuelta y por Z |
| 16 | `ART-ST-IGN` v1, reabrible bajo condición | «El pedernal» pesa la mitad que «El interruptor» —143–196 px contra 306 constantes— y una chispa se acerca más a la firma de la Estrella que un anillo ([A.3.2](./NOVA_Assets_Diseno.md#a32-art-st-ign-y-art-st-ready--sinencender)). **Si entre ocho asteroides el icono no se ve o se confunde con una Estrella pequeña**, la v1 está entera en `art/states/ign/v1/`, con su coste conocido: sería el primer símbolo de interfaz dentro del tablero |
| 17 | `ART-ST-SCARE` v2, reabrible bajo condición | «Los ojos como platos» pone mucha esclerótica y poca pupila, y eso empuja al bicho hacia el dibujo animado ([A.3.3](./NOVA_Assets_Diseno.md#a33-art-st-scare--asustado)). **Si en juego los cuatro nodos asustados parecen de otra serie**, la v2 («La pupila dilatada») está entera en `art/states/scare/v2/`: mismo anclaje, mismas tallas, misma quietud, solo cambia la pupila |
| 18 | `ART-ST-FROZEN` v1, reabrible bajo condición | «La escarcha» es la propuesta más barata en huella —317 px— y la única que deja la cara **entera** en los trece. Pierde porque sus agujas blancas alrededor del Magnetar se confunden con una esquirla más, y una esquirla es el contador de nivel de ese nodo. Está entera en `art/states/frozen/v1/` |
| 19 | **`ART-ST-FROZEN` v2, reabrible bajo condición** | «El bloque» es un prisma de seis caras, y es **la más legible de las tres a 1×**: 91.5 de contraste sobre la Estrella N5, contra 73.4 de la elegida. Llegó a estar elegida y documentada. Pierde por ser la primera figura de contorno **recto y regular** dentro del tablero, que es el argumento con el que se descartó la v1 de `ART-ST-IGN` ([A.3.4](./NOVA_Assets_Diseno.md#a34-art-st-frozen--congelado)). **Si en juego «El vidrio» no se ve sobre una Estrella grande, o se confunde con las vetas del Magnetar**, la v2 está entera en `art/states/frozen/v2/`, con su coste conocido: 1278 px de huella y el riesgo de leerse como un recuadro de UI |
| 20 | `ART-ST-DECAY` v1, reabrible bajo condición | «El goteo» no le pone aura al nodo: le pone una costra en la franja común bajo la cara y siete gotas cayendo. Es la más barata (460 px), la que mejor convive con el hielo y la que menos se parece a una corona de Estrella. Pierde por presencia: es una mancha discreta y `Infectado` es un estado indefinido que el jugador tiene que ver para curarlo. **Si «El miasma» resulta ruidoso junto al hielo o se confunde con la corona de la Estrella**, está entera en `art/states/decay/v1/` |
| 22 | `ART-ST-CONV` v1, reabrible bajo condición | «El torno» desmonta el nodo con tres anillos que caen hacia el centro y salen después. Es el gesto que 14.1 describe palabra por palabra —colapso a un punto y reencendido desde él— y deja la cara ciega 5–6 frames, uno menos que la elegida. Pierde por composición: anillos concéntricos son **simetría radial *n*-fold centrada**, la firma de la Estrella, y una de cada tres reconversiones acaba en Estrella. **Si en juego «La colada» se lee como un efecto de menú**, está entera en `art/states/conv/v1/` |
| 24 | `ART-ST-CAP` v1, reabrible bajo condición | «El fogonazo» es la lectura literal de 14.1: núcleo blanco que llena el nodo y onda que sale. Es **con diferencia la más contundente en visión periférica**, que es lo que pide el suceso más importante que le puede pasar a un nodo. Pierde por dos cosas medidas: tapa el sprite al 69–85 % y deja la cara ciega 2 frames de 5 —en el Asteroide se lo come entero—, y gasta en la captura el gesto genérico que los quince VFX de A.5 iban a necesitar. **Si en juego la captura pasa desapercibida**, está entera en `art/states/cap/v1/` |
| 26 | `ART-ST-BUFF` v2, reabrible bajo condición | «El temblor» son seis trazos horizontales que corren a los lados del nodo. Es la única de las tres que **dice tempo**, que es lo que un +50 % de cadencia significa, y usa el eje horizontal, que no ocupa ningún nodo ni ningún otro overlay. Da 16 frames distintos de 16. Pierde porque sus trazos viven en la franja donde el Magnetar guarda sus esquirlas y porque cuesta 228 px contra 87. **Si en juego el galón parece UI y no estado**, está entera en `art/states/buff/v2/` |
| 27 | `ART-ST-BUFF` v3, descartada por medida | «El sobrecalentado» es la lectura que A.3 daba por hecha: un aura tramada pegada al cuerpo que late al doble. **No es reabrible sin rediseñarla:** sobre un nodo infectado y buffeado a la vez, su aro punteado y la nube de `Infectado` se funden en un moteado único (`art/_preview/estados/buff_vs_infectado.png`). Es la misma construcción con otro color. Se conserva en `art/states/buff/v3/` por A.1, y por la medida que salió de ella (desviación 74) |
| 25 | `ART-ST-CAP` v3, reabrible bajo condición | «El sello» es un solo anillo que se cierra sobre el nodo y engorda al llegar. Es la que menos tapa (39–47 %) y **la única que no deja la cara ciega ni un frame**, y el gesto «algo se cierra sobre un nodo» no lo ocupa nada más en el proyecto. Pierde porque **contradice 14.1**: no tiene expansión ninguna, y elegirla obligaría a cambiar el texto en vez de precisarlo. Está entera en `art/states/cap/v3/` |
| 23 | `ART-ST-CONV` v3, reabrible bajo condición | «El apagón» es la lectura literal de 14.1: el nodo se apaga a gris y vuelve. Es **la más barata de las tres** —una sola hoja en vez de dos, y solo **2 frames de cara ciega** de 36—. Pierde porque durante casi tres segundos el nodo parece Neutral y no lo es (desviación 60). Si algún día se acepta esa lectura, está entera en `art/states/conv/v3/` |
| 21 | `ART-ST-DECAY` v3, reabrible bajo condición | «El enjambre» son trece motas cayendo por carriles, sin campo continuo: el **mejor contraste de las nueve propuestas de A.3** (74.9–121.1) y el que menos contador de nivel se lleva (95.6 %). Pierde porque a 1× hay que ir a buscarlo, y porque sus motas de 3 px se pierden dentro de la trama del hielo. Está entera en `art/states/decay/v3/` |

---

## T.8 Escala del tablero — **cerrada**

> Abierta el 2026-09-18. **Cerrada el 2026-09-28: opción A, tablero de 20 × 11.25 u. Reajustada el mismo día a 15 × 8.4375 u** — ver *Reajuste a 15 u*, justo debajo, que **manda sobre el resto de esta sección**.
> Comparación montada: `art/_preview/escala/32u_vs_20u.png`.
>
> ### Reajuste a 15 u
>
> **Lo que falló.** La tabla de *Tamaños candidatos* da para 20 u un «×1.5 a 1080p», y la Pixel Perfect Camera **no escala por factores no enteros**: calcula `zoom = min(1080 / 720, 1920 / 1280)` en enteros, que da **1**, y con *Upscale Render Texture* apagado —como pide `T0-04`— fija `size = 1080 / (2 · 64) = 8.4375`. A 1080p, la plataforma objetivo, se veían **30 × 16.9 u de mundo** con el tablero de 20 u ocupando los dos tercios centrales: los nodos volvían a leerse pequeños, que era lo que T.8 venía a arreglar. Se vio al aplicar `T0-04`, leyendo `PixelPerfectCameraInternal.cs` de la URP 17.
>
> **Las cuatro salidas**, simuladas a 1920 × 1080 con sprites reales en `art/_preview/escala/camara_1080p/`: **A** *Upscale RT* a ×1.5 (filas de píxel desiguales, prohibido por el contrato), **B** *windowbox* (bordes negros, 44 % de la pantalla), **C** referencia 960 × 540 a ×2 exacto, **D** dejarlo como estaba. **Se elige C.**
>
> **La decisión vigente.** Tablero de **15 × 8.4375 u**, Pixel Perfect con referencia **960 × 540**, `size = 4.21875`, fondos de **960 × 540 px** a PPU 64. Escala **×2 a 1080p y ×4 a 2160p**, enteros los dos; a 1440p es ×2.67 y la cámara hará ×2 enseñando algo de mundo alrededor, que se acepta porque 1440p no es plataforma objetivo. El nodo pasa al **10 %** del ancho. **Ningún sprite cambia.**
>
> **Lo que arrastra**, medido en 15 u:
>
> | | 20 u | **15 u** |
> |---|---|---|
> | Nodo | 7.5 % del ancho | **10 %** |
> | Magnetar N5, radio 3.6 u | 36 % de diámetro | **48 %** |
> | Familia `2.0…3.0 u` (T.7 #35), N5 | 30 % | **40 %** |
> | Protón cruzando el tablero a 2.5 u/s | 8.0 s | **6.0 s** |
> | Diagonal (longitud máxima del haz) | 22.9 u | **17.2 u** |
> | Regla del Asteroide cercano de 11.3 | 3 u | **2.25 u** |
>
> El Magnetar es lo único que se vuelve urgente: con los radios de hoy un N5 cubre casi medio tablero. Queda en [T.7 #35](#t7-pendientes-abiertos), que es balance y no escala.
>
> El resto de esta sección se conserva **tal cual**, 20 u incluidas, porque es el razonamiento que llevó hasta aquí.
>
> **La decisión.** El tablero pasa de 32 × 18 u a **20 × 11.25 u**, con cámara ortográfica `size = 5.625` y arte de fondo de **1280 × 720 px** a PPU 64. El nodo pasa del 4.7 % al **7.5 %** del ancho. Escala **×1.5 a 1080p, ×2 a 1440p y ×3 a 2160p**, y la plataforma objetivo es PC Windows a 1080p.
>
> **Se cerró antes de tiempo, y a propósito.** El plan era decidirlo tras ver la escena de `T1-12`, pero cerrarlo hoy cuesta **tres fondos regenerados por una constante** y no cuesta nada más: hay 0 líneas de C#, 0 ScriptableObjects y 0 coordenadas de nivel. Cada tarea que pase sin decidirlo hace la marcha atrás más cara, y el artefacto caro —las coordenadas de los 9 niveles (T.7 #2)— es justo el que estaba esperando a esta decisión.
>
> **Lo que queda por hacer, del checklist de abajo:** el punto 1 (regenerar los fondos, con sus constantes en píxeles reajustadas) y el punto 6, que se convierte en [T.7 #35](#t7-pendientes-abiertos) porque es una decisión de balance y no de escala. Los puntos 2, 3, 4, 5 y 7 ya están aplicados en los `.md`.
>
> El análisis que sigue se conserva **tal cual**, porque es el razonamiento que sostiene la decisión: un «por qué» olvidado se reabre solo.

### El diagnóstico

Los nodos se leen pequeños. **No es un problema de resolución, es una proporción en unidades de mundo:** el tablero mide 32 u y un nodo mide ~1 u, así que ocupa el **4.7 % del ancho**. Subir la resolución del arte no cambia eso — solo lo haría más nítido, igual de pequeño.

### Por qué escalar en Unity no lo arregla

1. **Un zoom no cambia una proporción.** Escalando nodos y fondo por el mismo factor, el nodo sigue siendo el 4.7 % del ancho. El zoom mueve todo a la vez; lo que molesta es una relación, y una relación es invariante al zoom.
2. **Escalar solo los nodos rompe la rejilla.** Con `Filter Mode: Point` una escala no entera produce filas de píxeles de distinto grosor dentro del mismo sprite. **Ya pasó en este proyecto:** es exactamente por lo que el protón dejó de escalar de forma continua y pasó a tres sprites discretos ([A.4.2](./NOVA_Assets_Diseno.md#a42-protón-y-haces-de-poder--143), que anula la fórmula de 14.3). Y aun a escala entera, un nodo de píxeles 2×2 sobre un fondo de píxeles 1×1 se ve mal: el grano del cielo mediría la mitad.
3. **La salida legítima acaba en el mismo sitio.** Bajar el PPU de los nodos *es* escalar en Unity y es gratis para el arte, pero obliga a regenerar el fondo para compartir rejilla. **Ninguna ruta evita regenerar el fondo**, que por suerte es una constante.

Lo que sí conviene hacer con la escala de Unity: **probar**. En la escena de `T1-12`, subir el Transform de los nodos a 1.5× o 2× y mirar. No es *shippable* y desalinea la rejilla, pero dice en dos minutos qué proporción gusta, y esa es la que luego se hornea en unidades.

### Las tres opciones

| | Qué se hace | Coste de arte | Riesgo |
|---|---|---|---|
| **A** *(recomendada)* | Encoger el tablero en unidades | Regenerar los 3 fondos: **una constante**. Cero cambios en sprites de nodo | Cambia el balance relativo (ver abajo). **No toca A.8 #4** |
| **B** | Bajar el PPU, dejar el tablero en 32 u | Regenerar los 3 fondos igualmente | **Rompe [A.8 #4](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas)**: el anillo de un Magnetar N1 apenas sobresaldría de su propio sprite. Arrastra rebalanceo de 4.4 y 16 |
| **C** | Regenerar los sprites en celda mayor (192 px) | Rehacer 34 sprites y 15 hojas | **No cambia la proporción**, solo la nitidez. Descartada |

### Tamaños candidatos

El ancho del tablero decide además a qué pantallas se escala sin temblor. A PPU 64:

| Tablero | Arte | 1080p | 1440p | 2160p | Nodo (% del ancho) |
|---|---|---|---|---|---|
| 32 × 18 *(actual)* | 2048×1152 | 0.94× | 1.25× | 1.875× | 4.7 % |
| 30 × 16.875 | 1920×1080 | **1×** | 1.33× | **2×** | 5.0 % |
| 24 × 13.5 | 1536×864 | 1.25× | 1.67× | 2.5× | 6.3 % |
| **20 × 11.25** *(propuesta)* | **1280×720** | 1.5× | **2×** | **3×** | **7.5 %** |
| 15 × 8.4375 | 960×540 | **2×** | 2.67× | **4×** | 10.0 % |

**El tamaño actual no escala entero a ninguna resolución común.** Es un fallo latente que hoy no duele porque no hay juego corriendo.

A 15 u el nodo es el más grande y es limpio a 1080p, pero un Magnetar N5 (radio 3.6 u) cubriría el 48 % del tablero de diámetro: habría que recortar los radios de [4.4](./NOVA_Nodos.md#44-magnetar).

### Qué cambia si se adopta A con 20 × 11.25 u

Encoger el tablero hace relativamente más grande **todo lo medido en unidades**, no solo los nodos:

| | 32 u | 20 u |
|---|---|---|
| Nodo | 4.7 % del ancho | **7.5 %** |
| Magnetar N5, radio 3.6 u | 22 % del ancho de diámetro | 36 % |
| ~~Alcance de poder 5 u / 7 u~~ | — | — | *(los poderes ya no tienen alcance: [7.2](./NOVA_GameDesign_Spec.md#72-tabla-de-poderes))* |
| Protón cruzando el tablero a 2.5 u/s | 12.8 s | 8.0 s |

Ninguno es un fallo: son decisiones de balance, y **no hay un solo nivel diseñado todavía**. La relación huella ↔ radio del Magnetar no se toca, que es justo lo que A.8 #4 protege.

### Por qué la ventana es ahora

| | Existe hoy |
|---|---|
| Líneas de C# | **0** |
| ScriptableObjects con los números de balance | **0** (es `T1-05`) |
| Coordenadas de los 9 niveles | **0** (T.7 #2) ← *el artefacto caro* |
| Sprites de nodo afectados | **0** |
| Fondos a regenerar | 3, una constante en `tools/nova_fondo.py` |

### Al cerrarla, tocar

1. `tools/nova_fondo.py`: `ANCHO, ALTO`. Regenerar con `gen_fondos_v1.py` y comprobar que el presupuesto sigue en verde — las constantes de `sigma`, `comba` y tamaño de grava están en píxeles y habrá que reajustarlas al lienzo nuevo.
2. [A.8 #4](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas): la fila de huella y PPU.
3. [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), *Resolución* y *Fondos*: el tamaño del lienzo de fondo.
4. [A.4.5](./NOVA_Assets_Diseno.md#a45-fondos): resolución de los tres `ART-BG-*`.
5. El maestro, donde diga «32 × 18 u».
6. Revisar, sin cambiarlos todavía, los números de [4.4](./NOVA_Nodos.md#44-magnetar) (radios). **Si se recortan, hay una familia que resuelve tres cosas de golpe:** `2.0 · 2.25 · 2.5 · 2.75 · 3.0 u` son 128, 144, 160, 176 y 192 px de radio, **los cinco en píxel exacto**, el N5 vuelve a caber en un lienzo de 384 y a 20 u de tablero el radio máximo baja del 36 % al 30 % del ancho. Hoy tres de los cinco radios no caen en píxel redondo y el N5 necesita 512 ([A.4.1](./NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar)) y la regla de 11.3 del Asteroide a menos de 5 u. **Los alcances de poder salen de esta lista:** desde el alcance global del Púlsar ([7.2](./NOVA_GameDesign_Spec.md#72-tabla-de-poderes)) no hay ningún número de poder medido en unidades de mundo, así que encoger el tablero ya no los toca. La decisión de escala es un problema menos que antes.

7. La longitud máxima del haz de poder ([A.4.2](./NOVA_Assets_Diseno.md#a42-protón-y-haces-de-poder--143)) es la **diagonal** del tablero: 36.7 u hoy, 22.9 u con 20 × 11.25. No cambia el asset —se estira— pero sí la cota que hay que probar.
