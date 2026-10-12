# NOVA — Especificación técnica de diseño y arquitectura

> Juego de conquista de nodos en tiempo real, 2D, Unity. Un jugador contra un algoritmo.
> **Documento maestro.** Versión 1.1 (dividida en varios archivos).
> Referencia de género: *JellyGo!* (AvoxGames). Tema: astrofísica.

---

## Mapa de documentos

| Archivo | Contiene | Numeración |
|---|---|---|
| **NOVA_GameDesign_Spec.md** (este) | Reglas del juego, economía, poderes, niveles, UI, arquitectura, balance y casos borde. | Secciones 1–19 |
| [NOVA_Nodos.md](./NOVA_Nodos.md) | Los 4 tipos de nodo: stats, niveles, costes y comportamiento. | Sección 4 |
| [NOVA_Algoritmo_Enemigo.md](./NOVA_Algoritmo_Enemigo.md) | El algoritmo que controla al Enemigo: ciclo de decisión, puntuación, amenaza, perfiles. | Sección 12 |
| [NOVA_Estados_Animaciones.md](./NOVA_Estados_Animaciones.md) | Estados y animaciones: qué las dispara, sobre quién, cuánto duran, cómo salen. | Sección 14 |
| [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md) | Inventario de arte: cada sprite/VFX/UI con su ID, archivo esperado y estado. | Sección A |
| [NOVA_Tracking.md](./NOVA_Tracking.md) | Seguimiento de desarrollo y prompt de continuación para el **asistente de IA** con el que se trabaja. | Sección T |

Las secciones extraídas **conservan su número original**. Una referencia a «ver 4.4» o «ver 12.5» sigue siendo válida; solo cambia el archivo.

---

## Índice

1. [Concepto y alcance](#1-concepto-y-alcance)
2. [Glosario normativo](#2-glosario-normativo)
3. [Facciones](#3-facciones)
4. Personajes (nodos) → **[NOVA_Nodos.md](./NOVA_Nodos.md)**
5. [Economía: la Energía](#5-economía-la-energía)
6. [Protones y resolución de impacto](#6-protones-y-resolución-de-impacto)
7. [Poderes del Púlsar](#7-poderes-del-púlsar)
8. [Reconversión](#8-reconversión)
9. [Power-ups del cielo](#9-power-ups-del-cielo)
10. [Condiciones de victoria y derrota](#10-condiciones-de-victoria-y-derrota)
11. [Progresión: 3 mapas × 3 niveles](#11-progresión-3-mapas--3-niveles)
12. Algoritmo del Enemigo → **[NOVA_Algoritmo_Enemigo.md](./NOVA_Algoritmo_Enemigo.md)**
13. [Controles y UI](#13-controles-y-ui)
14. Feedback visual y estados → **[NOVA_Estados_Animaciones.md](./NOVA_Estados_Animaciones.md)**
15. [Arquitectura en Unity](#15-arquitectura-en-unity)
16. [Tablas de balance consolidadas](#16-tablas-de-balance-consolidadas)
17. [Casos borde y reglas de resolución](#17-casos-borde-y-reglas-de-resolución)
18. [Decisiones tomadas por defecto](#18-decisiones-tomadas-por-defecto)
19. [Fases y criterios de salida](#19-fases-y-criterios-de-salida)

---

## 1. Concepto y alcance

### 1.1 Pitch

El jugador controla un conjunto de cuerpos celestes en un sistema estelar. Cada cuerpo almacena **Energía**. Enviar Energía de un cuerpo a otro lanza un **Protón** que viaja por el espacio. Si el Protón llega a un cuerpo aliado, lo recarga. Si llega a un cuerpo rival o neutral, le resta Energía; cuando la Energía del objetivo llega a cero, el cuerpo cambia de bando.

El objetivo de cada nivel es que no quede ningún cuerpo del Enemigo ni ningún Asteroide neutral.

### 1.2 Alcance cerrado

| Aspecto | Decisión |
|---|---|
| Jugadores | 1 humano contra 1 algoritmo. Sin multijugador, sin red. |
| Dimensión | 2D, vista cenital ortográfica. |
| Motor | **Unity 6.6 (6000.6.0f1)**, plantilla **Universal 2D v7.0.0** (URP con 2D Renderer). Sin física de Rigidbody para el gameplay. |
| Plataforma | **PC Windows, 1080p.** Solo ratón, más `Esc`. Sin táctil, sin mando. |
| Entrada | **Input System nuevo** (`com.unity.inputsystem`). Aislado en `InputRouter` ([15.7](#157-input-enemigo-y-ui)). |
| Post-procesado | **Ninguno.** Un bloom o un tonemapper deshacen el dithering Bayer y el alfa binario del contrato de arte. La URP se usa por el 2D Renderer y la Pixel Perfect Camera, no por sus efectos. |
| Niveles | 9 (3 mapas × 3 niveles). |
| Tipos de nodo | 4 (Asteroide, Estrella, Púlsar, Magnetar). |
| Progresión entre niveles | Ninguna. Cada nivel arranca desde cero, igual que en JellyGo. |
| Persistencia | Solo el nivel máximo desbloqueado. |
| Duración objetivo por nivel | 90 s (mapa 1) a 240 s (mapa 3). |

### 1.3 Fuera de alcance en v1.0

Multijugador, economía entre partidas, árbol de habilidades global, más de 4 tipos de nodo, niveles con scroll de cámara (todos los niveles caben en una pantalla fija).

---

## 2. Glosario normativo

Estos términos son los que deben usarse en el código, sin sinónimos. Cualquier ambigüedad aquí se traduce en bugs.

| Término | Significado exacto | Identificador en código |
|---|---|---|
| **Nodo** | Cualquier cuerpo celeste del mapa, de cualquier tipo y bando. | `Node` |
| **Tipo de nodo** | Asteroide, Estrella, Púlsar o Magnetar. | `NodeType` |
| **Bando / Facción** | Neutral, Jugador o Enemigo. | `Faction` |
| **Energía** | Recurso único. Se almacena en nodos y viaja en protones. Entero. | `Energy` |
| **Materia Inerte** | La Energía de un Asteroide neutral. No es utilizable ni contable en la barra de poder. Siempre vale 10. | `InertMatter` |
| **Tope** | Energía máxima que un nodo puede almacenar. Depende del tipo, no del nivel. | `EnergyCap` |
| **Protón** | Proyectil único generado por un envío. Lleva una **Carga**. | `Proton` |
| **Carga** | Energía que transporta un Protón. Se muestra como número sobre el proyectil. | `Charge` |
| **Nivel de nodo** | 0 (Asteroide) a 5. Determina las estadísticas del nodo. | `NodeLevel` |
| **Encendido** | Convertir un Asteroide propio de nivel 0 en un nodo de nivel 1 de otro tipo. | `Ignition` |
| **Reconversión** | Cambiar un nodo ya encendido a otro tipo. Lo devuelve a nivel 1. | `Conversion` |
| **Mejora** | Subir un nivel dentro del mismo tipo. | `Upgrade` |
| **Poder** | Habilidad del Púlsar. Cero Absoluto, Decaimiento o Agujero de Gusano. | `Power` |
| **Haz** | Trazo recto del Púlsar a su objetivo por el que se entrega un Poder. Dura 0.25 s, no viaja y no es interceptable. Sustituye al antiguo proyectil de poder. | `PowerBeam` |
| **Línea de envío** | Filamento de plasma en color de facción que sale de un nodo propio y **sigue al cursor** mientras el Jugador apunta un envío: durante el arrastre derecho y con una selección activa ([13.1](#131-ratón)). Solo presentación: no tiene efecto de juego y desaparece al soltar. No es el protón ni lo sustituye. | `SendLine` |
| **Arco del Magnetar** | Filamento de plasma del Magnetar al protón al que dispara. Es la presentación del disparo de [6.3](#63-efecto-del-magnetar-sobre-protones): el daño se aplica en el tick, el arco solo lo dibuja ([14.9](./NOVA_Estados_Animaciones.md#149-filamento-de-plasma)). | `MagnetarShotArc` |
| **Nodo encendido** | Nodo de nivel ≥ 1: Estrella, Púlsar o Magnetar, de la facción que sea. Un Asteroide nunca lo está. Junto con «no propio», es la condición que define a qué puede apuntar un Poder ([7.2.1](#721-objetivo-válido)). | `IsIgnited` |
| **Cadencia** | Disparos por segundo del Magnetar. | `FireRate` |
| **Velocidad de ataque** | Término visible en la UI. Se traduce distinto según tipo (ver 13.4). | `AttackSpeed` |
| **Algoritmo del Enemigo** | El código que decide las acciones del Enemigo. Es un algoritmo determinista con ruido sembrado ([12.7](./NOVA_Algoritmo_Enemigo.md#127-contrato-de-implementación)), **no un modelo entrenado**: no aprende, no infiere y no cambia entre partidas. En prosa se abrevia «el algoritmo». **No se le llama «la IA»**, que en este proyecto designa al asistente con el que se escribe el código ([T.1](./NOVA_Tracking.md#t1-prompt-de-continuación)). | `EnemyController` |
| **Barra de poder** | Barra inferior, comparativa e informativa. | `PowerBar` |

**Unidad de distancia:** unidad de mundo de Unity (u). El área jugable mide **15 × 8.4375 u** y coincide con una cámara ortográfica de `size = 4.21875` en 16:9.

> Cerrado el **2026-09-28**. Antes eran 32 × 18 u con `size = 9`; el motivo del cambio y las opciones evaluadas están en [T.8](./NOVA_Tracking.md#t8-escala-del-tablero--cerrada). A PPU 64 el tablero son **960 × 540 px de arte**, que escala **×2 a 1080p y ×4 a 2160p**, los dos enteros. El nodo pasa de ocupar el 4.7 % del ancho al **10 %**.
>
> **El mismo día se cerró primero en 20 × 11.25 u y se corrigió a 15 u.** La Pixel Perfect Camera solo amplía por factores enteros, así que el «×1.5 a 1080p» de 20 u no existía: a 1080p se veían 30 × 16.9 u de mundo con el tablero en el centro ([T.8](./NOVA_Tracking.md#t8-escala-del-tablero--cerrada), *Reajuste a 15 u*).

---

## 3. Facciones

```
Faction { Neutral = 0, Player = 1, Enemy = 2 }
```

| Facción | Color primario | Control | Notas |
|---|---|---|---|
| **Jugador** | Cian `#3FD7F5` | Ratón del usuario | Siempre es la facción 1. |
| **Enemigo** | Magenta `#F2456B` | `EnemyController` (algoritmo) | Ejecuta exactamente las mismas acciones legales que el jugador. Sin ventajas ocultas de reglas; solo varían sus parámetros de decisión. |
| **Neutral** | Gris `#8A93A6` | Nadie | Solo existe como Asteroide nivel 0. No actúa, no genera, no dispara, salvo el caso del Magnetar neutral descrito en [4.4](./NOVA_Nodos.md#44-magnetar). |

### 3.1 Regla de paridad

El Enemigo y el Jugador comparten el mismo conjunto de acciones legales y la misma tabla de costes. Todo comando de ambos pasa por la misma interfaz (`INodeCommandIssuer`). Está prohibido que el algoritmo modifique energía, niveles o estados por vías que el jugador no tenga.

### 3.2 Nota sobre el Magnetar neutral

Un Magnetar puede existir en estado Neutral únicamente si empieza el nivel así por diseño de mapa (`LevelDefinition`). Un Magnetar capturado nunca vuelve a Neutral. Un Magnetar neutral **sí dispara**, y dispara a protones de ambas facciones. Es el único nodo neutral con comportamiento activo, y es una herramienta central de diseño de niveles: bloquea corredores para los dos bandos hasta que alguien lo tome.

---

## 4. Personajes (nodos)

> **Movido a [NOVA_Nodos.md](./NOVA_Nodos.md).** La numeración se conserva: toda referencia a «4.x» de este documento apunta a ese archivo.

## 5. Economía: la Energía

### 5.1 Regla fundamental

**Solo la Estrella genera Energía. Sin excepciones.** Púlsar, Magnetar y Asteroide almacenan Energía recibida y la gastan, pero nunca la producen.

Consecuencias de diseño que deben respetarse:
- La Energía total de un bando solo puede crecer si tiene al menos una Estrella.
- Las capturas no crean Energía: el defensor pierde y el atacante gasta.
- Los protones derribados destruyen Energía permanentemente.

### 5.2 Envío

Cualquier nodo con Energía > 0, excepto el Asteroide, puede enviar.

| Acción | Resultado |
|---|---|
| Clic izquierdo sobre origen, clic izquierdo sobre destino | Envía el **50%** de la Energía del origen (redondeo hacia abajo). |
| Clic izquierdo sobre origen, **doble** clic sobre destino | Envía el **100%** de la Energía del origen. |

- El envío es **instantáneo** en el origen: la Energía sale del nodo y entra en el protón en el mismo frame.
- Con varios orígenes seleccionados, cada uno lanza su propio protón con su propio cálculo.
- Un envío del 50% de 1 de Energía redondea a 0 y **no se ejecuta**. Se requiere Energía ≥ 2 para el envío al 50%, y ≥ 1 para el envío al 100%.
- Un nodo puede quedar en 0 de Energía sin morir, sin cambiar de bando y sin perder nivel.

### 5.3 Recepción

| Caso | Efecto |
|---|---|
| Destino de la misma facción | Suma la Carga a su Energía. El excedente sobre el tope **se descarta**. |
| Destino de la misma facción, tipo Magnetar | Suma **1:1**. La resistencia ×2 solo aplica a impactos hostiles. |
| Destino de otra facción o neutral | Resta (ver sección 6). |

### 5.4 Topes

| Tipo | Tope |
|---|---|
| Asteroide neutral | 10 (Materia Inerte, no utilizable) |
| Asteroide propio | 10 |
| Estrella | 50 |
| Púlsar | 100 |
| Magnetar | 100 |

El tope no cambia con el nivel. Es una propiedad del tipo.

---

## 6. Protones y resolución de impacto

### 6.1 El protón

- Cada envío genera **un único protón**, no un enjambre. Su Carga es la Energía enviada.
- Muestra su Carga como número pequeño sobre el sprite. El número se actualiza en tiempo real si un Magnetar lo va desgastando.
- **Velocidad:** 2.5 u/s, constante, en línea recta desde el centro del origen al centro del destino.
- El tamaño del sprite cambia con la Carga en **tres escalones discretos** (ver 14.3), pero eso es solo presentación.
- Un protón en vuelo **conserva la facción de su emisor en el momento del lanzamiento**, aunque el nodo origen cambie de bando durante el viaje.
- Un protón no puede ser redirigido tras lanzarse.
- Los protones no colisionan entre sí.

### 6.2 Resolución de impacto

Pseudocódigo normativo. Es la única fuente de verdad para `ImpactResolver`.

```
ResolveImpact(proton, target):

    if proton.faction == target.faction:
        target.energy = min(target.energy + proton.charge, target.cap)
        return ABSORBED

    # Impacto hostil
    multiplier = (target.type == Magnetar) ? 0.5 : 1.0
    effective  = floor(proton.charge * multiplier)

    if effective < target.energy:
        target.energy -= effective
        return DAMAGED

    # Captura
    remainder = effective - target.energy
    target.energy = min(remainder, target.cap)
    ChangeFaction(target, proton.faction)
    return CAPTURED
```

Notas obligatorias:
- El redondeo del multiplicador es **hacia abajo**, y se aplica una sola vez sobre la Carga total, no disparo a disparo.
- En una captura, el remanente también está afectado por el multiplicador. Un protón de 100 contra un Magnetar con 40 lo captura y le deja `100*0.5 - 40 = 10`. (El ejemplo anterior, 120 contra 50, no se puede producir: ningún tope pasa de 100, así que ningún protón lleva más de 100.)
- Al capturar, el nodo **conserva su tipo y su nivel**. Solo cambia de facción.
- Un nodo capturado a 0 de Energía sigue siendo un nodo válido del nuevo bando.

### 6.3 Efecto del Magnetar sobre protones

```
OnMagnetarShot(proton):
    proton.charge -= 2
    if proton.charge <= 0:
        DestroyProton(proton)   # La Energía se pierde. No vuelve al emisor.
```

Varios Magnetares que cubran el mismo tramo de trayectoria disparan de forma independiente y acumulativa.

El disparo se ve como un **arco de plasma** del Magnetar al protón ([14.9](./NOVA_Estados_Animaciones.md#149-filamento-de-plasma)). El arco es solo presentación: el −2 de Carga se aplica en el tick del disparo, no cuando el arco llega, y el arco nunca retrasa ni alarga el efecto.

### 6.4 Casos de captura por tipo

| Objetivo | Energía | Carga necesaria |
|---|---|---|
| Asteroide neutral | 10 | 10 |
| Estrella nivel 5 llena | 50 | 50 |
| Púlsar lleno | 100 | 100 |
| Magnetar lleno | 100 | **200** |
| Cualquier nodo a 0 | 0 | 1 |

---

## 7. Poderes del Púlsar

### 7.1 Regla de carga

- El Púlsar carga **un solo poder a la vez**.
- Al iniciar la carga, la Energía del Púlsar **baja progresivamente** mientras la barra de carga del poder sube. Las dos curvas están acopladas: la barra refleja `energíaInvertida / costeTotal`.
- Si la Energía del Púlsar se agota antes de completar la carga, la carga **se pausa** en el porcentaje alcanzado y se reanuda automáticamente cuando el Púlsar reciba Energía. Lo ya invertido no se pierde.
- Una vez cargado al 100%, el poder **se mantiene indefinidamente** hasta que se lance. Durante ese tiempo no se puede cargar otro poder.
- La carga puede **cancelarse manualmente**. Se recupera el **50%** de lo invertido, redondeado hacia abajo, respetando el tope.

### 7.2 Tabla de poderes

| Poder | Nivel req. | Coste | Tiempo de carga | Drenaje | Alcance | Objetivos válidos |
|---|---|---|---|---|---|---|
| **Cero Absoluto** | 1 | 20 | 8 s | 2.5 E/s | **Global** | Nodo encendido no propio |
| **Decaimiento** | 2 | 35 | 12 s | 2.9 E/s | **Global** | Nodo encendido no propio |
| **Agujero de Gusano** | 3 | 60 | 16 s | 3.75 E/s | **Global** | Nodo encendido no propio |

**El Púlsar no tiene alcance.** Cualquier nodo encendido que no sea propio es un objetivo legal, esté donde esté. La distancia no limita, no encarece y no retrasa: el coste, el tiempo de carga y el drenaje de esta tabla son idénticos para un objetivo pegado al Púlsar y para uno en la esquina opuesta del tablero.

Los tres poderes comparten alcance **y objetivo**, así que el nivel del Púlsar desbloquea **qué** puede lanzar, nunca **hasta dónde** ni **contra quién**.

### 7.2.1 Objetivo válido

Los tres poderes comparten una sola regla:

> **Objetivo válido = cualquier nodo encendido que no sea propio.**

Se descompone en dos condiciones, más una tercera de tiempo:

1. **No es propio.** Vale un nodo del Enemigo y vale un nodo Neutral.
2. **Está encendido**: es Estrella, Púlsar o Magnetar, es decir, nivel ≥ 1.
3. Sigue cumpliendo 1 y 2 cuando el haz termina ([7.2.2](#722-el-haz)).

De ahí salen exactamente **dos** exclusiones, y no admiten excepción:

| No es objetivo | Por qué |
|---|---|
| Cualquier **Asteroide**, de la facción que sea: neutral, tuyo o del Enemigo | Es nivel 0: no genera, no dispara, no envía y no carga. **No hay nada que congelar, que infectar ni que robar.** Un Asteroide se toma con protones, que para eso cuestan 10, y se enciende con otros 10 ([4.1](./NOVA_Nodos.md#41-asteroide-nivel-0)). Permitir que un poder lo capturase saltaría ese doble coste, que es el precio de expandirse y la decisión más importante de la partida. |
| Cualquier nodo **propio** | No hay poderes de apoyo en NOVA. Los tres hacen daño o roban. |

**El Magnetar neutral sí es objetivo, y es el único nodo Neutral que lo es.** No por una excepción escrita para él, sino porque cumple la regla: está encendido y no es tuyo. [3.2](#32-nota-sobre-el-magnetar-neutral) lo deja claro —es el único nodo Neutral con nivel y con comportamiento activo; todo lo demás Neutral es un Asteroide de nivel 0—. Así que un Agujero de Gusano puede robarlo íntegro, un Cero Absoluto puede callarlo y un Decaimiento puede vaciarlo. Es la respuesta cara pero existente al muro que bloquea un corredor, y la tienen **los dos bandos por igual**.

Mientras el cursor está en modo apuntado, todo lo que no cumple la regla se muestra **atenuado** y no es clicable. La atenuación ya no depende de la distancia, porque la distancia dejó de ser un límite: depende del nivel y de la facción.

### 7.2.2 El haz

El poder **no es un proyectil y no viaja**. Al lanzarlo sale del Púlsar un **haz** recto hasta el nodo objetivo.

| Parámetro | Valor |
|---|---|
| Duración del haz | **0.25 s** (3 frames a 12 fps), constante |
| Momento del efecto | Al **terminar** el haz, nunca al lanzarlo |
| Interceptable | **No.** Ningún Magnetar puede cortarlo. Solo los protones son derribables. |
| Color | El del **poder**, no el de la facción ([14.4](./NOVA_Estados_Animaciones.md#144-paleta-funcional)) |

**La duración no depende de la distancia.** Un objetivo a 1 u y otro a 17 u —la diagonal del tablero de 15 × 8.4375 u mide 17.2— reciben el efecto exactamente 0.25 s después del clic. Es la contrapartida de haber quitado el alcance: si el poder viajase como el antiguo proyectil de 6 u/s, cruzar esa diagonal costaría casi 3 s, y a la velocidad de un protón, casi 7; el poder dejaría de sentirse un poder.

**La validez se comprueba dos veces:** al lanzar y al terminar el haz. Si en esos 0.25 s el objetivo deja de cumplir [7.2.1](#721-objetivo-válido) —porque un protón propio lo capturó primero y ya es del lanzador— **el haz se disipa sin efecto y la Energía invertida se pierde**. Es la misma lógica que el caso 2 de la [sección 17](#17-casos-borde-y-reglas-de-resolución): la facción se comprueba al resolver, no al lanzar.

El color por poder es lo que hace legible el lanzamiento a distancia: el haz anuncia **qué** va a pasar antes de que pase, y **quién** lo lanza se lee por el extremo del que sale, que es un Púlsar con color de facción.

### 7.3 Cero Absoluto

Congela el nodo objetivo. Representa un enfriamiento al cero absoluto: el cuerpo queda inerte. Objetivos válidos, los de [7.2.1](#721-objetivo-válido): cualquier Estrella, Púlsar o Magnetar que no sea propio, **incluido el Magnetar neutral**, que mientras esté congelado deja de disparar a los dos bandos.

**Mientras está congelado, el nodo:**
- No genera Energía (Estrella).
- No dispara (Magnetar).
- No carga ni lanza poderes (Púlsar). Una carga en curso queda **pausada**, no se pierde.
- No puede enviar Energía.
- No puede mejorarse ni **iniciar** una reconversión. Una que ya estuviera en curso **se pausa** y se reanuda al descongelar ([8.1](#81-reglas)).
- **Sí puede recibir impactos.**

**Deshielo.** Es la regla especial que invierte el sentido de recibir Energía:

> Un nodo congelado pierde Energía cuando **cualquier** facción, incluida la suya, le envía Energía. Cuando su Energía llega a 0, el nodo se descongela y pasa al bando del **último emisor que impactó**.

```
ResolveImpact_Frozen(proton, target):
    multiplier = (target.type == Magnetar) ? 0.5 : 1.0
    effective  = floor(proton.charge * multiplier)

    if effective < target.energy:
        target.energy -= effective
        target.lastToucher = proton.faction
        return THAW_PROGRESS

    target.energy = 0
    Unfreeze(target)
    ChangeFaction(target, proton.faction)
    return THAWED_AND_CAPTURED
```

- Un nodo congelado que ya está a **0 de Energía** se descongela y cambia de bando con el siguiente protón que lo alcance, de Carga 1 o superior.
- El remanente de Carga se pierde: un nodo descongelado siempre queda a 0.
- **Auto-deshielo de seguridad:** si un nodo congelado permanece 30 s sin recibir ningún impacto, se descongela solo, conservando su facción y su Energía. Evita nodos muertos permanentes que bloqueen la condición de victoria.
- **Los 30 s son de inactividad, no un máximo.** Cada impacto **reinicia el contador**, así que un nodo golpeado en el segundo 29 sigue congelado pasado el 59. No hay tope superior a la duración del estado: lo termina el deshielo o el silencio, nunca el reloj por sí solo.
- **El Decaimiento no descongela, y no reinicia el contador.** Un nodo `Congelado` + `Infectado` ([14.6](./NOVA_Estados_Animaciones.md#146-matriz-de-compatibilidad)) pierde 2 E/s, pero el deshielo vive dentro de `ResolveImpact_Frozen` y solo lo dispara un **impacto**. Al llegar a 0 el nodo sigue congelado y a 0, es decir **a un protón de Carga 1 de cambiar de bando**: eso es lo que 14.6 quiere decir con que el Decaimiento «acelera su propio deshielo». Lo abarata, no lo dispara. Y como el Decaimiento no es un impacto, si nadie le tira nada el nodo acaba auto-descongelándose a los 30 s, con su dueño, a 0 de Energía y todavía infectado.

### 7.4 Decaimiento

Infecta el nodo objetivo con decaimiento radiactivo. Su Energía cae sola. Objetivos válidos, los de [7.2.1](#721-objetivo-válido): cualquier Estrella, Púlsar o Magnetar que no sea propio, **incluido el Magnetar neutral**.

- **Tasa:** −2 de Energía por segundo, hasta 0.
- Al llegar a 0, el nodo **permanece infectado** a 0 de Energía y sigue perteneciendo a su dueño.
- Un nodo infectado sigue funcionando con normalidad: la Estrella genera (y pierde más de lo que genera), el Magnetar dispara, el Púlsar carga.
- **Cura:** cualquier facción que le envíe Energía elimina la infección. Se resuelve como un impacto normal (sección 6.2), es decir:
  - Si lo cura su propio dueño, recupera Energía y pierde la infección.
  - Si lo cura otra facción y la Carga basta para vaciarlo, cambia de bando sin infección.
  - Si lo cura otra facción y la Carga no basta, pierde la infección pero sigue siendo del dueño original.
- **Sobre un nodo Neutral**, que no tiene dueño que lo cure, la infección se queda: baja a 0 y ahí sigue, infectado y disparando si es Magnetar, hasta que alguien le envíe Energía. Es el atajo lento contra el muro —35 de coste drenan sus 100 en 50 s y luego basta Carga 1 para tomarlo ([17](#17-casos-borde-y-reglas-de-resolución), caso 7)— y es **intencional**: cuesta medio minuto de exposición y lo puede hacer también el Enemigo.
- No es acumulable: un segundo Decaimiento sobre un nodo ya infectado **reinicia** el efecto, no duplica la tasa.
- Visual: aura verde pulsante `#7CF23F` y partículas descendentes. Un contador visible de la Energía cayendo.

### 7.5 Agujero de Gusano

Cambia de bando el nodo objetivo **conservando su Energía, su tipo y su nivel**. Es el poder definitivo y por eso es el más caro.

- Funciona sobre cualquier **nodo encendido que no sea propio** —Estrella, Púlsar o Magnetar— a cualquier nivel y con cualquier Energía, incluidos un Magnetar lleno y el **Magnetar neutral**. **No** funciona sobre ningún Asteroide, sea de quien sea ([7.2.1](#721-objetivo-válido)).
- **Robar el Magnetar neutral es su mejor uso, y el más caro.** Ignora la resistencia ×2, así que se lleva entero un muro que por la vía de los protones costaría **200 de Carga**. Por 60 de Energía y 16 s de carga, es el único cambio de 200 por 60 que existe en el juego.
- **Ignora la resistencia ×2 del Magnetar.** Un Magnetar con 100 de Energía cambia de bando íntegro.
- Elimina los estados Congelado y Decaimiento del objetivo al capturarlo.

**Robo de carga (regla táctica clave).** Si el objetivo es un Púlsar enemigo que está cargando o tiene cargado un poder:

| Momento del impacto | Resultado |
|---|---|
| Carga en curso (1–99%) | El Púlsar cambia de bando y la carga **se resetea a 0%**. La Energía invertida se pierde. |
| Carga al 100%, poder aún no lanzado | El Púlsar cambia de bando **con el poder cargado intacto**, utilizable de inmediato por el nuevo dueño. |
| Haz ya lanzado (dentro de sus 0.25 s) | El haz **completa su efecto** a favor de quien lo lanzó, aunque su Púlsar ya sea del otro bando cuando se resuelva. El Púlsar cambia de bando vacío. |

Esta ventana es intencional y debe ser legible en pantalla: la barra de carga del Púlsar enemigo es visible para el jugador en todo momento.

**Con alcance global, esa lectura es el contrajuego entero.** Un Púlsar enemigo cargado al 100 % amenaza el tablero completo y no hay distancia a la que ponerse a salvo; lo único que queda es verle la barra llena y robársela antes de que la gaste. Por eso `ART-UI-CHARGE` es visible también en el Púlsar enemigo y no es negociable.

---

## 8. Reconversión

Permite cambiar un nodo ya encendido a otro tipo. No existe en JellyGo; es propia de este juego.

### 8.1 Reglas

- Se paga con la **Energía del propio nodo**, que debe tenerla disponible en ese momento.
- El nodo vuelve a **nivel 1** del nuevo tipo.
- Conserva la Energía sobrante, recortada al tope del nuevo tipo.
- Dura **3 segundos** con una animación de colapso y reencendido.
- **El coste se paga al iniciar; el tipo, el nivel y el tope cambian al terminar.** Durante los 3 s el nodo sigue siendo del tipo de origen a todos los efectos —un Magnetar reconvirtiéndose conserva su resistencia ×2 y su tope de 100— y al acabar pasa a nivel 1 del tipo nuevo, con la Energía que tenga en ese momento recortada al tope nuevo ([18](#18-decisiones-tomadas-por-defecto) #20).
- Durante esos 3 s el nodo **no genera, no dispara, no envía y no puede mejorarse**, pero **sí puede ser impactado y capturado**. Si es capturado durante la animación, la reconversión se cancela y el nodo queda como estaba antes de iniciarla, con el nuevo dueño. **El coste pagado se pierde**: la Energía del nodo es la que deja la captura ([6.2](#62-resolución-de-impacto); [18](#18-decisiones-tomadas-por-defecto) #21).
- **No se puede iniciar** si el nodo está Congelado, Infectado, cargando un poder, **con un poder ya cargado** o ya reconvirtiéndose. Un Púlsar con el poder listo tiene que lanzarlo o cancelarlo antes: si no, el poder desaparecería con el tipo ([18](#18-decisiones-tomadas-por-defecto) #19).
- **Una reconversión ya empezada sí puede congelarse, y se pausa.** Cero Absoluto es legal sobre un nodo reconvirtiéndose —no hay más exclusiones que las dos de [7.2.1](#721-objetivo-válido)— y su efecto sobre el temporizador de 3 s es el mismo que sobre la carga de un Púlsar: **se detiene donde esté y se reanuda al descongelar**, sin perder lo invertido ([7.3](#73-cero-absoluto), [17](#17-casos-borde-y-reglas-de-resolución) caso 8). Iniciar y continuar son cosas distintas: lo primero es una acción del dueño, lo segundo un reloj que ya corre.
- Un nodo **no puede volver a Asteroide**. La reconversión es solo entre Estrella, Púlsar y Magnetar.

### 8.2 Costes

| Desde | Hacia | Coste |
|---|---|---|
| Estrella | Púlsar o Magnetar | 20 |
| Púlsar | Estrella o Magnetar | 20 |
| Magnetar | Estrella o Púlsar | 30 |

El Magnetar cuesta más de desmontar porque su resistencia ×2 lo hace más caro de perder por la vía normal.

### 8.3 Ejemplo

Un Magnetar nivel 4 con 45 de Energía se reconvierte a Estrella: paga 30, queda como **Estrella nivel 1 con 15 de Energía** (15 < tope 50, no hay recorte). Ha perdido 3 niveles de inversión. La reconversión es una maniobra de emergencia, no una rutina.

---

## 9. Power-ups del cielo

### 9.1 Funcionamiento

Un power-up cae sobre un **Asteroide neutral elegido al azar** y lo marca con un halo luminoso y un icono flotante. **El primer bando que encienda ese Asteroide recibe el efecto.**

- Solo puede haber **un Asteroide marcado a la vez**.
- Si nadie lo enciende en **30 s**, el power-up se apaga y arranca el temporizador del siguiente.
- Si no queda ningún Asteroide neutral cuando toca un drop, el drop se **salta** y el temporizador se reinicia.
- El efecto se aplica a **toda la facción**, no solo al nodo encendido.
- **Duración:** 20 s.
- **Magnitud:** +50%.
- Dos power-ups del mismo tipo no se acumulan: el segundo **reinicia el temporizador** a 20 s.
- Los dos tipos sí pueden estar activos a la vez.

### 9.2 Tipos

| Power-up | Icono | Efecto | Afecta a |
|---|---|---|---|
| **Rayo Cósmico** | Rayo | +50% velocidad de ataque | Cadencia del Magnetar y velocidad de carga del Púlsar |
| **Nube de Hidrógeno** | Nube con cruz | +50% generación de Energía | Intervalo de la Estrella (se multiplica por 0.667) |

Probabilidad: 50% / 50%.

### 9.3 Calendario de drops

| Mapa | Primer drop | Intervalo | Máximo por nivel |
|---|---|---|---|
| 1 (niveles 1–3) | 25 s | 40–55 s aleatorio | 3 |
| 2 (niveles 4–6) | 30 s | 50–70 s aleatorio | 4 |
| 3 (niveles 7–9) | 35 s | 60–85 s aleatorio | 5 |

El intervalo se cuenta desde que el power-up anterior es recogido o expira, no desde que aparece.

---

## 10. Condiciones de victoria y derrota

### 10.1 Victoria del Jugador

Se cumple cuando **el Enemigo no posee ningún nodo**.

**Cascada de conquista:** al cumplirse, todos los Asteroides neutrales restantes pasan automáticamente al Jugador en una animación de 3 s, de más cercano a más lejano. Esto satisface la regla de que ningún nodo puede quedar Neutral al final de la partida, sin obligar al jugador a limpiar manualmente el mapa cuando el resultado ya está decidido.

Los protones del Enemigo en vuelo cuando pierde su último nodo **se desvanecen** sin impactar.

### 10.2 Derrota del Jugador

Cualquiera de estas dos:

1. **Aniquilación:** el Jugador no posee ningún nodo.
2. **Estancamiento:** el Jugador no posee ninguna Estrella **y** la suma de Energía de todos sus nodos es **menor que 10**, es decir, no puede encender ni reconvertir nada. Se comprueba solo si tampoco tiene protones en vuelo ni poderes cargados.

El estancamiento evita partidas zombi en las que el jugador está vivo pero es matemáticamente incapaz de volver a crecer.

### 10.3 Fin de nivel

| Resultado | Acción |
|---|---|
| Victoria | Pantalla de victoria. Se desbloquea el siguiente nivel. Botón "Siguiente" y "Repetir". |
| Derrota | Pantalla de derrota. Botón "Reintentar" que **reinicia el nivel desde cero**, y "Volver al mapa". |

El jugador puede reiniciar el nivel en cualquier momento desde el menú de pausa. No hay guardado a mitad de nivel.

---

## 11. Progresión: 3 mapas × 3 niveles

### 11.1 Estructura

| Mapa | Nombre | Niveles | Mecánicas introducidas |
|---|---|---|---|
| 1 | Sistema Interior | 1–3 | Envío, captura, Estrella, Magnetar, mejoras |
| 2 | El Cinturón | 4–6 | Púlsar, Cero Absoluto, Decaimiento, Magnetar neutral |
| 3 | Espacio Profundo | 7–9 | Agujero de Gusano, reconversión, algoritmo agresivo |

### 11.2 Diseño de niveles

**`N` es una columna derivada, no un presupuesto.** Es la suma de las cuatro siguientes —nodos del Jugador, nodos del Enemigo, `Ast` y neutrales especiales— y los Magnetares neutrales **cuentan**, porque son nodos. Al llegar a `T5-02` será literalmente `spawns.Count` de cada `LevelDefinition` ([15.4](#154-data--scriptableobjects)), así que si alguna vez no cuadra, lo que está mal es `N`. `Ast` = asteroides neutrales al inicio. Los nodos iniciales de cada bando se indican como tipo(nivel).

| Nivel | N | Jugador inicia con | Enemigo inicia con | Ast | Neutrales especiales | Duración objetivo |
|---|---|---|---|---|---|---|
| 1 | 5 | Estrella(1) | Estrella(1) | 3 | — | 90 s |
| 2 | 7 | Estrella(1) | Estrella(2) | 5 | — | 110 s |
| 3 | 8 | Estrella(1), Asteroide | Estrella(2), Magnetar(1) | 4 | — | 130 s |
| 4 | 9 | Estrella(1) | Estrella(2), Púlsar(1) | 6 | — | 150 s |
| 5 | 11 | Estrella(1), Magnetar(1) | Estrella(2), Púlsar(1) | 6 | 1 Magnetar(2) neutral | 170 s |
| 6 | 11 | Estrella(2) | Estrella(3), Púlsar(2), Magnetar(2) | 6 | 1 Magnetar(3) neutral | 190 s |
| 7 | 13 | Estrella(1), Estrella(1) | Estrella(3), Púlsar(3), Magnetar(3) | 7 | 1 Magnetar(3) neutral | 210 s |
| 8 | 14 | Estrella(2) | Estrella(3)×2, Púlsar(3), Magnetar(3) | 7 | 2 Magnetar(3) neutrales | 230 s |
| 9 | 16 | Estrella(2), Magnetar(1) | Estrella(4)×2, Púlsar(3)×2, Magnetar(4) | 7 | 2 Magnetar(4) neutrales | 240 s |

### 11.3 Curva de dificultad

La dificultad sube por tres vías simultáneas, todas parametrizadas en `LevelDefinition`:

1. **Ventaja inicial del Enemigo:** más nodos y de mayor nivel.
2. **Calidad del algoritmo:** intervalo de decisión más corto y mejor evaluación (ver [sección 12](./NOVA_Algoritmo_Enemigo.md)).
3. **Geometría del mapa:** más Magnetares neutrales bloqueando rutas y mayores distancias entre los nodos del jugador.

Ningún nivel debe ser imposible: en todos, el jugador debe tener al menos un Asteroide neutral a menos de **2.25 u** de su posición inicial, alcanzable antes de que el Enemigo llegue a él.

> **Eran 5 u, luego 3 u y son 2.25 u desde el 2026-09-28**, porque la regla está escrita en unidades y el tablero encogió de 32 u a 20 u y, el mismo día, a 15 u ([T.8](./NOVA_Tracking.md#t8-escala-del-tablero--cerrada)). 5 u sobre 32 eran el 15.6 % del ancho; 2.25 u sobre 15 son el 15 %. Se conserva la proporción, que es lo que la regla quería decir: **cerca en pantalla, no cerca en números**.

**La tercera vía ya no afecta a los poderes.** Desde que el Púlsar tiene alcance global ([7.2](#72-tabla-de-poderes)), la geometría del mapa solo encarece los protones: un Púlsar amenaza el tablero entero desde el primer segundo, y alejarse de él no protege de nada. Eso obliga a dos cosas al diseñar los 9 niveles:

- **A partir del nivel 4, el Jugador no debería depender de una sola Estrella.** El Enemigo arranca con Púlsar(1) y puede congelarla o infectarla sin moverse ni acercarse. El nivel 4 de [11.2](#112-diseño-de-niveles) —Jugador con una única Estrella(1) contra Estrella(2) + Púlsar(1)— es uno de los tres casos a vigilar en la primera prueba de juego, y no el peor: el orden de revisión es 8, 6, 4 ([T.7](./NOVA_Tracking.md#t7-pendientes-abiertos) #28). Si alguno resulta injusto, se corrige dándole un segundo nodo inicial, no devolviéndole el alcance al Púlsar.
- **El Magnetar neutral sigue siendo el muro, pero tiene llave.** Es el único nodo Neutral que un poder puede tocar ([7.2.1](#721-objetivo-válido)): un Agujero de Gusano se lo lleva entero por 60, y un Decaimiento lo vacía en 50 s. Los dos bandos tienen esa llave, así que al diseñar los niveles 5 a 9 hay que contar con que **un Magnetar neutral cambia de dueño a mitad de partida**, y colocarlo donde eso sea interesante en vez de decisivo.

---

## 12. Algoritmo del Enemigo

> **Movido a [NOVA_Algoritmo_Enemigo.md](./NOVA_Algoritmo_Enemigo.md).** La numeración se conserva: toda referencia a «12.x» de este documento apunta a ese archivo.

## 13. Controles y UI

### 13.1 Ratón

El clic izquierdo significa una cosa u otra **según haya o no una selección activa**. Todo lo demás depende de esa regla, así que va primero:

> **Sin selección, el clic izquierdo elige. Con selección, el clic izquierdo envía.**

**Sin selección activa**

| Entrada | Acción |
|---|---|
| **Clic izquierdo** sobre nodo propio que puede enviar —Estrella, Púlsar o Magnetar— | Lo selecciona |
| **Clic izquierdo** sobre **Asteroide propio** | Abre el menú radial de encendido ([13.7](#137-menú-radial-de-encendido)) |
| **Clic izquierdo** sobre nodo no propio | Nada |

**Con selección activa**

| Entrada | Acción |
|---|---|
| **Clic izquierdo** sobre cualquier nodo **no seleccionado** | Envía el **50%** desde **cada** origen seleccionado ([5.2](#52-envío)). Si el destino es propio, es un refuerzo; si no, un ataque |
| **Doble clic izquierdo** sobre cualquier nodo no seleccionado | Envía el **100%** |
| **Clic izquierdo** sobre un nodo **ya seleccionado** | Lo saca de la selección. Un segundo clic sobre ese mismo nodo dentro del buffer de doble clic **se ignora**: un doble clic sobre un nodo seleccionado lo deselecciona y no envía nada |
| **Clic izquierdo** en vacío | Nada. La selección solo se cancela con clic derecho en vacío |

**Siempre**

| Entrada | Acción |
|---|---|
| **Arrastre derecho** desde nodo propio que puede enviar hasta destino | Envío al 50% **desde ese único origen**, sea cual sea la selección activa. En cuanto el cursor supera el umbral de arrastre aparece la **línea de envío**, que lo sigue hasta soltar. Se envía **al soltar** sobre el destino, **sin pasar por el buffer** de doble clic. Soltar en vacío o sobre el propio origen no envía nada y la línea desaparece |
| **Arrastre izquierdo** (lazo), empiece donde empiece | Selección múltiple de nodos propios **que puedan enviar**. Los Asteroides quedan fuera: no son orígenes legales ([5.2](#52-envío)). El lazo **sustituye** a la selección activa, no se suma a ella. Empezarlo sobre un nodo ya no envía nada: el envío por arrastre es del botón derecho |
| **Clic derecho** sobre cualquier nodo | Abre el panel de información y mejoras ([13.3](#133-panel-de-clic-derecho)) |
| **Clic derecho** en vacío | Cancela la selección y cierra el panel |
| **Esc** | Menú de pausa |

**Clic o arrastre.** Una pulsación pasa a ser arrastre cuando el cursor se aleja más de **4 px de la referencia de 960 × 540** —8 px físicos a 1080p— del punto donde se pulsó ([16.1](#161-globales)). Por debajo es un clic. La regla es la misma para los dos botones.

**El clic derecho actúa al soltar, no al pulsar.** Hasta que el botón se suelta no se sabe si es un clic —panel o cancelar la selección— o un arrastre que envía. Si hubo arrastre, el panel no se abre y la selección no se cancela, se suelte donde se suelte. Un arrastre derecho que empieza sobre un nodo que no puede enviar —ajeno o Asteroide— o en vacío no dibuja línea y no hace nada al soltar. Las dos excepciones de siempre actúan al pulsar y se tragan la pulsación: el modo apuntado, que se cancela, y el menú de encendido, que se cierra ([13.7](#137-menú-radial-de-encendido)). Ninguna de las dos empieza un arrastre.

**La línea de envío.** Es el feedback de «desde dónde va a salir esto», y se dibuja en dos situaciones:

| Situación | Líneas | Terminan en |
|---|---|---|
| **Arrastre derecho** desde un nodo propio que puede enviar | Una, desde ese origen | El cursor |
| **Selección activa**, sin arrastre en curso | Una **desde cada nodo seleccionado** | El cursor |

- Va del centro del origen al cursor y **sigue al ratón en cada frame**; no salta al centro del nodo que hay debajo. El nodo destino se reconoce por estar bajo la punta.
- Se dibuja **aunque no se llegue a enviar nada**: al arrastrar hacia el vacío, o desde un origen con menos Energía de la que pide [5.2](#52-envío). El envío sigue sus reglas al soltar; la línea solo enseña el gesto.
- Durante un arrastre derecho **se ocultan las líneas de la selección**, porque ese envío sale de un único origen. Vuelven al soltar.
- Con una selección viva hay líneas **siempre**, también mientras se arrastra el lazo, y desaparecen al cancelarla con clic derecho en vacío. Un nodo que sale de la selección pierde su línea en el mismo frame.
- **No se dibuja** en modo apuntado ([13.5](#135-rueda-de-poderes-del-púlsar)), donde manda la retícula, ni con el menú de encendido abierto.
- Su aspecto, un filamento de plasma en color de facción, está en [14.9](./NOVA_Estados_Animaciones.md#149-filamento-de-plasma). **El Enemigo nunca la tiene**: el algoritmo no usa cursor, y la línea es feedback de entrada, no información de juego ([3.1](#31-regla-de-paridad) habla de acciones legales, no de lo que ve cada uno).

**Un nodo que deja de ser propio sale de la selección solo**, en el mismo tick en que cambia de facción. Si la selección queda vacía, se vuelve al modo *sin selección*.

**La selección se mantiene tras enviar.** Un lazo de cuatro nodos sirve para varias oleadas sin rehacerlo, y dos clics simples sobre el mismo destino, **separados más que el buffer de doble clic** ([13.2](#132-resolución-del-doble-clic)), acumulan 50% + 50% = 75%. Dentro del buffer son un doble clic y envían el 100%. Se cancela con clic derecho en vacío, nunca sola.

La contrapartida está asumida: con una selección viva **no se puede cambiar de origen con un clic**, porque ese clic envía. Primero se cancela con clic derecho y luego se elige. Es el precio de que reforzar un nodo propio cueste un clic y no un gesto aparte, y reforzar es la primera respuesta defensiva que existe ([12.4](./NOVA_Algoritmo_Enemigo.md#124-detección-de-amenaza-y-defensa)), disponible igual para los dos bandos.

Por el mismo motivo, **con una selección activa un Asteroide propio recibe Energía en vez de abrir su menú**, que es justo lo que hace falta para llevarlo a 10. Para encenderlo se cancela la selección primero.

**El modo apuntado suspende esta tabla.** Mientras un Púlsar tiene un poder cargado y el cursor apunta ([13.5](#135-rueda-de-poderes-del-púlsar)), el clic izquierdo lanza sobre un objetivo válido y el **clic derecho cancela el apuntado sin abrir ningún panel**. Es la única excepción y tiene prioridad sobre todo lo anterior.

### 13.2 Resolución del doble clic

Clic simple y doble clic comparten el primer evento, así que el envío **no se ejecuta en el primer clic**. Se usa un buffer. 0.25 s de latencia es imperceptible y evita el bug de enviar 50% + 50% = 75% acumulado.

**Solo es doble clic si los dos clics caen en el mismo nodo.** Si el segundo clic cae en **otro** destino antes de que venza el buffer, el primero no se pierde: su 50% se envía en ese momento y el segundo destino abre su propio buffer ([18](#18-decisiones-tomadas-por-defecto) #24).

```
OnClickDestino(destino):
    if bufferActivo and destino == destinoPendiente:
        CancelarBuffer()
        Enviar(destino, 100%)
    else:
        if bufferActivo:
            CancelarBuffer()
            Enviar(destinoPendiente, 50%)
        destinoPendiente = destino
        bufferActivo = true
        EsperarSegundos(0.25)   # cancelable
        Enviar(destino, 50%)
        bufferActivo = false
```

El arrastre derecho no pasa por aquí: envía al soltar ([13.1](#131-ratón)).

### 13.3 Panel de clic derecho

Contiene, en este orden:

1. Tipo y **nivel** actual (`Estrella · Nivel 3`).
2. Energía actual / tope.
3. **Velocidad de ataque** (ver 13.4).
4. Botón de **mejora** con su coste, si el nodo no está al nivel máximo y si es propio. Si está al máximo, se muestra `NIVEL MÁXIMO` y el botón desaparece.
5. Botón de **reconversión** con **los dos destinos posibles** y su coste, **si es propio**. Son dos y no tres: [8.1](#81-reglas) prohíbe volver a Asteroide, y un nodo no se reconvierte a su propio tipo. Los tres sí son correctos en el menú de *encendido*, que es otra cosa ([13.7](#137-menú-radial-de-encendido)).
6. Si es un Púlsar propio: rueda de poderes ([13.5](#135-rueda-de-poderes-del-púlsar)). **Este panel es el único sitio desde el que se abre.**

El botón de mejora se muestra **deshabilitado y con el coste en rojo** cuando no hay Energía suficiente, nunca oculto. El jugador necesita saber cuánto le falta.

Cómo se comporta ([18](#18-decisiones-tomadas-por-defecto) #30, desde `T2-03`):

- Se abre **a la derecha del nodo**, o a la izquierda si a la derecha no cabe, sin tapar su círculo. Lo abre el clic derecho sobre **cualquier** nodo, propio o no: de uno ajeno se ve la información, sin botones. Se abre **al soltar** y solo si no hubo arrastre; un arrastre derecho envía y no abre nada ([13.1](#131-ratón)).
- `NIVEL MÁXIMO` sale en cualquier nodo que esté al máximo, sea de quien sea: es información.
- Sigue abierto mientras se juega y se repinta al cambiar la Energía, el nivel, el tipo o el dueño del nodo; si lo capturan, pierde el botón. Lo cierran el clic derecho en vacío y `Esc`; el clic derecho sobre otro nodo lo lleva a ese nodo.
- Un clic izquierdo **dentro** del panel es suyo y no llega a lo que haya debajo; el botón mejora sin tocar la selección y sin enviar nada. Fuera del panel, el clic izquierdo sigue su curso normal.
- La reconversión (punto 5) llegó con `T2-07`: una fila «Reconvertir» con dos botones, uno por destino, con su coste, solo en nodos propios. Mientras dura, los botones dejan sitio a «Reconvirtiendo a …» ([18](#18-decisiones-tomadas-por-defecto) #33). La rueda (punto 6) llega con `T3-07`.

### 13.4 Traducción de "velocidad de ataque"

Es un único campo en la UI que significa cosas distintas según el tipo:

| Tipo | Se muestra como |
|---|---|
| Estrella | Generación: `X.XX E/s` |
| Magnetar | Cadencia: `X.X disparos/s` |
| Púlsar | Carga: `X.XX E/s` |
| Asteroide | El panel solo muestra `DORMIDO` y el coste de encendido |

Los decimales van con **punto**, como en esta tabla, sea cual sea la configuración regional de la máquina. La línea del Púlsar enseña el drenaje del poder que carga o tiene cargado (16.3), y en reposo queda vacía ([T.7](./NOVA_Tracking.md#t7-pendientes-abiertos) #49, cerrado en `T3-02`).

### 13.5 Rueda de poderes del Púlsar

La rueda vive en el **panel de clic derecho** del Púlsar ([13.3](#133-panel-de-clic-derecho), punto 6) y muestra tres ranuras en orden fijo: **1 Cero Absoluto · 2 Decaimiento · 3 Agujero de Gusano**. Las no desbloqueadas aparecen con candado y el nivel requerido.

> **No la abre el clic izquierdo**, aunque una versión anterior de esta sección lo dijera. El clic izquierdo sobre un Púlsar propio ya significa seleccionarlo o enviarle Energía ([13.1](#131-ratón)), y el Púlsar **sí puede ser origen de un envío** ([4.3](./NOVA_Nodos.md#43-púlsar)): quitarle esa entrada para colgarle la rueda dejaría al único nodo con 100 de tope sin forma de repartirlos con un clic. El Asteroide es el caso contrario y por eso sí tiene acción propia en el clic izquierdo ([13.7](#137-menú-radial-de-encendido)).

- Clic en una ranura: inicia la carga. Aparece una barra bajo el nodo.
- Con un poder cargado, la ranura brilla y el cursor pasa a modo apuntado.
- En modo apuntado se resaltan **todos los nodos encendidos que no son propios**, sin importar la distancia: los del Enemigo y el Magnetar neutral. Se atenúa lo demás, que son los nodos propios y los Asteroides de cualquier bando ([7.2.1](#721-objetivo-válido)).
- **No se dibuja ningún círculo de alcance**, porque no hay alcance que dibujar.
- Clic en un objetivo válido: lanza. Clic derecho: cancela el apuntado sin perder la carga.

### 13.6 HUD permanente

| Elemento | Posición | Contenido |
|---|---|---|
| Barra de poder | Inferior, ancho completo | Dos colores. Jugador crece desde la izquierda, Enemigo desde la derecha. **Solo informativa, sin ningún efecto de juego.** |
| Cronómetro | Superior derecha | Tiempo transcurrido |
| Nivel | Superior izquierda | `Mapa 2 · Nivel 5` |
| Pausa | Superior derecha | Botón |

La barra de poder suma la Energía de todos los nodos de cada bando. **No cuenta** la Materia Inerte de los Asteroides neutrales ni la Carga de los protones en vuelo.


### 13.7 Menú radial de encendido

Lo abre el **clic izquierdo sobre un Asteroide propio**, sin selección activa ([13.1](#131-ratón)), y sale en el cursor.

**Por qué ahí y no en el panel.** Un Asteroide **no puede enviar nunca** ([5.2](#52-envío)), así que no es un origen legal y seleccionarlo no significaría nada: su clic izquierdo estaba libre. No es una excepción a la regla de 13.1, es el único nodo al que esa regla no le asigna nada. Encender es además la acción más repetida de la partida, y cobrarla a dos clics sería cobrarla caro.

Tres opciones, siempre las mismas y siempre las tres, en el orden de [16.2](#162-costes-de-nivel):

| Opción | Coste | Resultado |
|---|---|---|
| Estrella | 10 | Estrella nivel 1 con 0 de Energía |
| Púlsar | 10 | Púlsar nivel 1 con 0 de Energía |
| Magnetar | 10 | Magnetar nivel 1 con 0 de Energía |

- Con `energy < 10` el menú **se abre igual**, con las tres opciones deshabilitadas y el coste en rojo. Es el mismo criterio que el botón de mejora de [13.3](#133-panel-de-clic-derecho): el jugador necesita saber cuánto le falta, y ocultarlo no se lo dice.
- Clic derecho o clic en vacío: cierra el menú sin encender.
- Con el menú abierto, cualquier otro clic izquierdo —sobre un nodo— o un arrastre izquierdo **lo cierra sin encender y sigue su curso**: un clic sobre una Estrella propia la selecciona, y un lazo selecciona. El botón derecho y `Esc` **solo** lo cierran: no abren el panel, no pausan y **no empiezan un arrastre derecho**, así que no dibujan línea de envío ([18](#18-decisiones-tomadas-por-defecto) #28).
- Una opción deshabilitada no hace nada al pulsarla y **deja el menú abierto**, para que se siga viendo cuánto falta.
- Un tipo que la partida todavía no tiene sale **deshabilitado, en gris y sin coste** ([18](#18-decisiones-tomadas-por-defecto) #29). Durante el desarrollo es el Magnetar hasta `T2-04` y el Púlsar hasta `T3-02`.
- El panel de clic derecho de un Asteroide sigue siendo informativo: `DORMIDO` y el coste de encendido ([13.4](#134-traducción-de-velocidad-de-ataque)).

El estado `SinEncender` ya avisa de cuál de las dos lecturas toca sin abrir nada: la brasa está quieta por debajo de 10 y crepita al llegar ([14.1](./NOVA_Estados_Animaciones.md#141-estados-del-nodo)).

---

## 14. Feedback visual y estados

> **Movido a [NOVA_Estados_Animaciones.md](./NOVA_Estados_Animaciones.md).** La numeración se conserva: toda referencia a «14.x» de este documento apunta a ese archivo.
> Los assets que estas animaciones consumen están inventariados en [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md).

## 15. Arquitectura en Unity

### 15.1 Principios

1. **Los datos no son MonoBehaviour.** Todo el balance vive en ScriptableObjects. Cambiar un número no requiere tocar código.
2. **Un script, una responsabilidad.** Si un script necesita la palabra "y" para describirse, hay que partirlo.
3. **La presentación solo lee.** Ningún script de `Presentation` o `UI` modifica estado de juego. Se suscriben a eventos.
4. **La lógica de reglas es estática y pura.** `ImpactResolver`, `EnergyMath` y `ConversionRules` no tienen estado ni referencias a escena, y son testeables sin abrir Unity.
5. **Jugador y Enemigo usan el mismo canal.** Ambos emiten comandos a través de `INodeCommandIssuer`. Es imposible que el algoritmo haga algo ilegal porque no existe la vía.
6. **Sin `Find`, sin `SendMessage`, sin singletons salvo tres**, declarados en [15.6](#156-systems--escena): `NodeRegistry`, `ProtonRegistry` y `GameManager`. Los dos primeros son registros; el tercero **no lo es**, es el flujo del nivel, y por eso decir «los tres registros» dejaba fuera al único singleton que no lo es.

### 15.2 Estructura de carpetas

**Un solo repositorio.** `Assets/`, `art/`, `tools/`, `mds/` y `entregables_uni/`
son carpetas hermanas de la misma raíz, versionadas juntas. Es lo que esta sección
daba por supuesto al citar a la vez `Assets/Sprites/` y `tools/nova_core.py`, y quedó
escrito el 2026-09-28, cuando el proyecto Unity se creó fuera del repositorio y hubo
que fusionarlo. `Assets/Sprites/` es **salida derivada de `art/`** y está en el
`.gitignore`: lo que se versiona es el generador, no el PNG.

**Espacio de nombres raíz: `Nova`**, y cada ensamblado lleva el suyo —`Nova.Core`,
`Nova.Gameplay`, `Nova.Enemy`…— coincidiendo con el nombre del `.asmdef` y con la
carpeta. Los identificadores van en inglés, como manda el glosario de la
[sección 2](#2-glosario-normativo); los comentarios, en español.

```
Assets/
├── Scripts/
│   ├── Core/                 # C# puro, SIN referencia a UnityEngine (T0-03)
│   │   ├── Enums/
│   │   ├── Rules/
│   │   └── Events/
│   ├── Data/                 # ScriptableObjects
│   ├── Gameplay/
│   │   ├── Nodes/
│   │   ├── Combat/
│   │   ├── Powers/
│   │   └── Economy/
│   ├── Enemy/
│   ├── Input/
│   ├── Systems/              # Nova.Systems.asmref: compila dentro de Nova.Gameplay
│   ├── UI/
│   ├── Presentation/
│   ├── Utils/                # PrimitiveSprites: discos de las vistas provisionales (T2-02)
│   └── Editor/               # Nova.Editor, solo Editor: configuración, puente de arte, fuente
├── Data/                     # Instancias .asset de los SO
│   ├── Nodes/
│   ├── Powers/
│   ├── Levels/
│   └── EnemyProfiles/
├── Prefabs/
├── Art/
├── Audio/
└── Scenes/
    ├── Boot.unity
    ├── MainMenu.unity
    ├── LevelSelect.unity
    ├── Game.unity            # Escena única reutilizada por los 9 niveles
    └── Tests/                # Escenas de prueba, fuera de Build Settings (Fase1.unity, T1-12; Fase2.unity, T2-04)
```

### 15.3 Core — reglas puras (sin MonoBehaviour)

| Script | Responsabilidad única |
|---|---|
| `Faction.cs` | Enum de facciones |
| `NodeType.cs` | Enum de tipos |
| `NodeState.cs` | Dos enums ([T.7](./NOVA_Tracking.md#t7-pendientes-abiertos) #43): `NodeState`, el estado de base excluyente (`Sleeping`, `Unignited`, `Normal`), y `NodeStateFlags`, las banderas de los estados que se suman a él (`Scared`, `Frozen`, `Infected`, `Converting`, `Captured`). Las parejas que [14.6](./NOVA_Estados_Animaciones.md#146-matriz-de-compatibilidad) prohíbe las rechaza `NodeStateMachine` |
| `PowerType.cs` | Enum de poderes |
| `ImpactResult.cs` | Enum: Absorbed, Damaged, Captured, ThawProgress, ThawedAndCaptured. Es lo que pasa cuando un protón **llega**; el derribado por un Magnetar no llega y lo avisa el propio protón (`Proton.OnShotDown`, [T.7](./NOVA_Tracking.md#t7-pendientes-abiertos) #40) |
| `EnergyMath.cs` | Multiplicadores, redondeos, cálculo de coste efectivo de captura |
| `ImpactResolver.cs` | Resolución de impacto de protón, incluido el caso congelado. Entrada: snapshot. Salida: `ImpactResult` + deltas. **Sin efectos secundarios.** |
| `ConversionRules.cs` | Validez y coste de encendido, mejora y reconversión |
| `ThreatCalculator.cs` | Dado un nodo y los protones en vuelo, dice si está amenazado y en cuánto tiempo. Usado por el algoritmo **y** por el estado `Asustado`. |
| `GameEventChannels.cs` | Definición de los canales de evento |

### 15.4 Data — ScriptableObjects

| Script | Contenido |
|---|---|
| `NodeDefinition.cs` | Tipo, tope, **coste de reconversión** ([16.4](#164-reconversión): depende del tipo de origen), nº de niveles, array de `NodeLevelData`, prefab, sprites por facción |
| `NodeLevelData.cs` | `[Serializable] struct`: coste de mejora, intervalo de generación, radio, cadencia |
| `PowerDefinition.cs` | Tipo, coste, tiempo de carga, duración del haz, nivel requerido, color del haz ([14.4](./NOVA_Estados_Animaciones.md#144-paleta-funcional)) y, con la fase 6, VFX. El drenaje se calcula como coste / tiempo de carga (`T3-01`). **Sin campo de alcance:** el alcance es global y por tanto no es un parámetro de balance ([7.2](#72-tabla-de-poderes)). |
| `BalanceConfig.cs` | Velocidad del protón, daño por disparo del Magnetar, multiplicador del Magnetar, buffer de doble clic, duración de reconversión, auto-deshielo, tasa de decaimiento, **coste de encendido, Materia Inerte, umbral de susto y devolución al cancelar una carga** (los cuatro, de 16.1) |
| `LevelDefinition.cs` | Lista de `NodeSpawnData`, referencia a `EnemyProfile`, config de drops, duración objetivo |
| `NodeSpawnData.cs` | `[Serializable] struct`: posición, tipo, nivel, facción, energía inicial |
| `PowerUpDefinition.cs` | Tipo, magnitud, duración, icono, estadística afectada |
| `EnemyProfile.cs` | Los 8 parámetros de la tabla [12.5](./NOVA_Algoritmo_Enemigo.md#125-perfiles-por-mapa) |
| `UiText.cs` | **Todos los textos de la UI**, en español: nombres de tipo, formatos del panel, `DORMIDO`, `NIVEL MÁXIMO`… Una instancia, `Assets/Data/UiText.asset`. El juego es solo en español, pero ningún texto se escribe en un script o un prefab ([T.7](./NOVA_Tracking.md#t7-pendientes-abiertos) #39) |

### 15.5 Gameplay — componentes de nodo

Todos viven en el mismo GameObject. `NodeController` es el único que los conoce entre sí.

| Script | Responsabilidad única |
|---|---|
| `NodeController.cs` | Orquestador. **No contiene reglas.** Expone la API pública del nodo y delega. |
| `NodeIdentity.cs` | Tipo actual y `NodeDefinition` asociada |
| `NodeOwnership.cs` | Facción actual. Emite `OnFactionChanged`. Única puerta para cambiar de bando. |
| `NodeEnergy.cs` | Valor, tope, `Add`, `Subtract`, `Clamp`. Emite `OnEnergyChanged`. |
| `NodeLevelComponent.cs` | Nivel actual, aplicación de `NodeLevelData`, validación de mejora |
| `NodeStateMachine.cs` | Estados de [14.1](./NOVA_Estados_Animaciones.md#141-estados-del-nodo), sus timers y sus bloqueos mutuos |
| `StarGenerator.cs` | Solo en Estrella. Temporizador de generación. Se desactiva si el estado lo bloquea. |
| `MagnetarTurret.cs` | Solo en Magnetar. Detección por radio, selección de objetivo, cadencia. |
| `PulsarCaster.cs` | Solo en Púlsar. Carga, pausa, cancelación, lanzamiento, robo de carga. |
| `NodeIgnition.cs` | Encendido de Asteroide, incluida la recogida del power-up |
| `NodeConverter.cs` | Reconversión y su ventana de vulnerabilidad de 3 s |
| `NodeScare.cs` | En todo nodo. Cada tick pregunta a `ThreatCalculator` con los protones de `ProtonRegistry` y pone o quita la bandera `Scared` ([14.2](./NOVA_Estados_Animaciones.md#142-animación-de-susto), `T2-08`) |
| `NodeFreeze.cs` | En todo nodo. Pone y quita la bandera `Frozen` y lleva el reloj del auto-deshielo: 30 s sin impactos, reiniciado por cada impacto ([7.3](#73-cero-absoluto), `T3-04`). El deshielo por impacto lo calcula `ImpactResolver` y lo aplica `NodeController` |
| `INodeCommandIssuer.cs` | Canal único de comandos de Jugador y Enemigo (3.1, 15.1 #5), en `Gameplay/Commands/`. Expone la facción y un método por acción de [12.2](./NOVA_Algoritmo_Enemigo.md#122-acciones-candidatas), y **crece con la tarea que implementa cada acción**: `Send` desde `T1-11` |
| `NodeCollider2D` | Trigger circular, solo para input y detección. Sin Rigidbody. Es un `CircleCollider2D` *trigger* en la capa de física **`Nodes`**, con el radio de la huella sólida del nodo (0.3–0.6 u, [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), *Huella del nodo*). El clic se resuelve con `Physics2D.OverlapPoint` sobre esa capa ([18](#18-decisiones-tomadas-por-defecto) #23). |

### 15.6 Systems — escena

| Script | Responsabilidad única |
|---|---|
| `GameManager.cs` | Flujo del nivel: carga, inicio, pausa, fin. Único singleton de flujo. |
| `NodeRegistry.cs` | Registro vivo de todos los nodos indexado por facción. Consultado por el algoritmo, la victoria y la barra de poder. **Singleton.** |
| `ProtonRegistry.cs` | Registro de protones en vuelo indexado por destino. Lo usan `ThreatCalculator` y el Magnetar. **Singleton.** |
| `LevelLoader.cs` | Instancia el mapa desde `LevelDefinition` |
| `ProtonLauncher.cs` | Servicio de lanzamiento: valida, descuenta y pide un protón al pool |
| `ProtonPool.cs` | Object pooling de protones |
| `Proton.cs` | Movimiento, Carga, notificación de impacto. **No resuelve el impacto**, llama a `ImpactResolver`. |
| `PowerBeam.cs` | Haz de 0.25 s del Púlsar al objetivo: datos del haz (poder, extremos, facción del lanzador). **No se mueve ni colisiona:** origen y destino se conocen desde el lanzamiento. |
| `PowerBeamSystem.cs` | Reloj de los haces a +10. Revalida el objetivo al terminar y, según el resultado, aplica el efecto o lo disipa ([7.2.2](#722-el-haz)); ordena los que terminan en el mismo tick (17 caso 29). **Singleton** (`T3-03`). |
| `PowerUpSpawner.cs` | Temporizadores, elección de Asteroide, expiración |
| `FactionBuffs.cs` | Buffs activos por facción y su consulta por los componentes |
| `WinConditionChecker.cs` | Victoria, derrota, estancamiento, cascada de conquista |
| `ProgressSaver.cs` | Nivel máximo desbloqueado en `PlayerPrefs` |

### 15.7 Input, Enemigo y UI

| Script | Responsabilidad única |
|---|---|
| `InputRouter.cs` | Traduce ratón a intenciones: clic y lazo con el izquierdo, clic y arrastre de envío con el derecho, que se resuelve al soltar. Buffer de doble clic. **No conoce reglas.** |
| `SendLineState.cs` | Estado de las líneas de envío: de qué orígenes salen y dónde está el cursor. Lo escribe `InputRouter` a partir del arrastre derecho y de la selección; vive en `Nova.Input` por lo mismo que `IgnitionMenuState` |
| `SendLine.cs` | Dibuja las líneas de envío ([13.1](#131-ratón), [14.9](./NOVA_Estados_Animaciones.md#149-filamento-de-plasma)). Solo lee `SendLineState` |
| `SelectionManager.cs` | Conjunto de nodos seleccionados, lazo, feedback de selección |
| `PlayerCommandIssuer.cs` | Implementa `INodeCommandIssuer` para el Jugador |
| `EnemyController.cs` | Ciclo de decisión de [12.1](./NOVA_Algoritmo_Enemigo.md#121-ciclo-de-decisión) |
| `EnemyEvaluator.cs` | Función de puntuación de [12.3](./NOVA_Algoritmo_Enemigo.md#123-función-de-puntuación-atacar). Pura, testeable. |
| `EnemyThreatResponder.cs` | Refuerzo, congelación y evacuación de [12.4](./NOVA_Algoritmo_Enemigo.md#124-detección-de-amenaza-y-defensa) |
| `EnemyCommandIssuer.cs` | Implementa `INodeCommandIssuer` para el Enemigo |
| `NodeHUD.cs` | Número de Energía sobre el nodo |
| `InfoPanelState.cs` | Estado del panel de clic derecho: abierto o no, de qué nodo, dónde y la geometría del botón. Lo abre y lo cierra `InputRouter`; vive en `Nova.Input` por lo mismo que `IgnitionMenuState` |
| `NodeInfoPanel.cs` | Dibuja el panel de clic derecho. Solo lee `InfoPanelState` y valida el botón con `NodeController.CheckUpgrade` |
| `PulsarPowerWheel.cs` | Rueda de poderes |
| `IgnitionMenuState.cs` | Estado del menú radial de encendido: abierto o no, sobre qué Asteroide, dónde y la geometría de sus opciones. Lo abre y lo cierra `InputRouter`; vive en `Nova.Input` porque el router resuelve el clic contra él |
| `IgnitionMenu.cs` | Dibuja el menú radial de encendido. Solo lee `IgnitionMenuState` y valida cada opción con `NodeIgnition.Check` |
| `PowerBarUI.cs` | Barra inferior. Lee de `NodeRegistry`, no acumula estado. |
| `EndLevelScreen.cs` | Victoria y derrota |

### 15.8 Presentation

| Script | Responsabilidad única |
|---|---|
| `NodeVisual.cs` | Sprite correcto según tipo, nivel y facción |
| `NodeAnimator.cs` | Animaciones de estado de [14.1](./NOVA_Estados_Animaciones.md#141-estados-del-nodo) |
| `ProtonVisual.cs` | Número de Carga y escala |
| `MagnetarShotArc.cs` | Arco de plasma de cada disparo del Magnetar ([14.9](./NOVA_Estados_Animaciones.md#149-filamento-de-plasma)). Escucha `MagnetarTurret.OnShot` |
| `VFXPool.cs` | Pool de efectos |
| `AudioDirector.cs` | Reacción sonora a eventos |
| `CameraShake.cs` | Sacudida en capturas grandes |

### 15.9 Comunicación entre capas

```
InputRouter ──► PlayerCommandIssuer ──┐
                                      ├──► NodeController ──► Core.Rules
EnemyController  ──► EnemyCommandIssuer ───┘           │
                                                  ▼
                                          GameEventChannels
                                                  │
                        ┌─────────────────────────┼─────────────────────┐
                        ▼                         ▼                     ▼
                  Presentation                   UI            WinConditionChecker
```

**Regla de oro:** las flechas nunca apuntan hacia arriba. `Presentation` y `UI` jamás llaman a `NodeController`.

### 15.10 Orden de ejecución

Se fija con el atributo `[DefaultExecutionOrder]` en cada script, no en *Project Settings → Script Execution Order*:

```
-100  NodeRegistry, ProtonRegistry
 -50  InputRouter
 -40  EnemyController
   0  NodeController y componentes de nodo
  10  Proton, PowerBeam
  20  WinConditionChecker
  50  UI
 100  Presentation
```

Los impactos se resuelven en el orden en que los protones llegan dentro del mismo tick. Con empate exacto, el de mayor Carga primero; con llegada y Carga iguales, el que se lanzó antes. El protón que llega se encola y `ProtonRegistry` resuelve los impactos del tick **después de todos los `FixedUpdate`**, así que `WinConditionChecker` los ve en el tick siguiente (`T1-10`).

**Paso de tiempo: toda la lógica de juego corre en `FixedUpdate` a 50 Hz** (`Fixed Timestep = 0.02` en *Project Settings → Time*). Generación de la Estrella, vuelo del protón, disparo del Magnetar, carga del Púlsar, temporizadores de estado y ciclo de decisión del Enemigo avanzan con `Time.fixedDeltaTime` y nunca con `Time.deltaTime`; `Presentation` y `UI` siguen en `Update`, porque solo leen. Es lo que permite cumplir el determinismo de [12.7](./NOVA_Algoritmo_Enemigo.md#127-contrato-de-implementación): con `deltaTime` variable, la misma semilla daba partidas distintas según los FPS. Un temporizador cuyo intervalo no es múltiplo de 20 ms —la Estrella N4 a 1.05 s— acumula el tiempo en enteros y arrastra el sobrante al siguiente intervalo, así que conserva su tasa media exacta. Cerrado el 2026-09-28 ([18](#18-decisiones-tomadas-por-defecto) #22, [T.7](./NOVA_Tracking.md#t7-pendientes-abiertos) #36).

---

## 16. Tablas de balance consolidadas

Estos valores deben vivir en `BalanceConfig.asset` y en las `NodeDefinition`. No deben aparecer escritos a mano en ningún script.

### 16.1 Globales

| Parámetro | Valor |
|---|---|
| Velocidad del protón | 2.5 u/s |
| Alcance de los poderes | **Global** (sin límite) |
| Duración del haz de poder | 0.25 s |
| Daño del Magnetar por disparo | 2 de Carga |
| Multiplicador de resistencia del Magnetar | 0.5 |
| Buffer de doble clic | 0.25 s |
| Umbral de arrastre | 4 px de la referencia 960 × 540 (8 px a 1080p), el mismo para los dos botones |
| Duración de la reconversión | 3.0 s |
| Auto-deshielo | 30 s |
| Tasa de Decaimiento | 2 E/s |
| Duración de los power-ups | 20 s |
| Magnitud de los power-ups | +50% |
| Ventana de expiración del power-up | 30 s |
| Umbral de susto | 1.5 s |
| Área jugable | **15 × 8.4375 u** |
| Cámara ortográfica | `size = 4.21875` (16:9), Pixel Perfect con referencia 960 × 540 |
| Arte de fondo | 960 × 540 px a PPU 64 |
| Coste de encendido | 10 |
| Materia Inerte del Asteroide | 10 |
| Devolución al cancelar carga | 50% |

### 16.2 Costes de nivel

| Nivel | Estrella | Púlsar | Magnetar |
|---|---|---|---|
| 1 (encendido) | 10 | 10 | 10 |
| 2 | 20 | 25 | 20 |
| 3 | 30 | 40 | 30 |
| 4 | 40 | — | 40 |
| 5 | 50 | — | 50 |
| **Total a nivel máx.** | **150** | **75** | **150** |

**La fila «1 (encendido)» es de referencia.** El coste de encendido es uno solo para los tres tipos y vive en `BalanceConfig` (16.1, *Coste de encendido*). En las `NodeDefinition` el nivel 1 lleva coste 0: a ese nivel no se llega mejorando ([T.7](./NOVA_Tracking.md#t7-pendientes-abiertos) #41, cerrado el 2026-09-28).

### 16.3 Poderes

| Poder | Coste | Carga | Drenaje | Alcance | Objetivo |
|---|---|---|---|---|---|
| Cero Absoluto | 20 | 8 s | 2.50 E/s | Global | Nodo encendido no propio |
| Decaimiento | 35 | 12 s | 2.92 E/s | Global | Nodo encendido no propio |
| Agujero de Gusano | 60 | 16 s | 3.75 E/s | Global | Nodo encendido no propio |

**Los costes no se tocaron al quitar el alcance.** Es una decisión consciente ([18](#18-decisiones-tomadas-por-defecto) #13): el Púlsar sale reforzado y el ajuste, si hace falta, se hará sobre el diseño de los niveles de [11.2](#112-diseño-de-niveles) antes que sobre esta tabla.

### 16.4 Reconversión

| Desde | Coste |
|---|---|
| Estrella | 20 |
| Púlsar | 20 |
| Magnetar | 30 |

Vive en `NodeDefinition` y no en `BalanceConfig`, porque es una propiedad del tipo de origen, como el Tope (`T2-07`).

---

## 17. Casos borde y reglas de resolución

Cada fila es un test de aceptación.

| # | Caso | Resolución |
|---|---|---|
| 1 | Dos protones de facciones distintas impactan el mismo nodo en el mismo frame | Se resuelven secuencialmente por orden de llegada; con empate exacto, primero el de mayor Carga, y con Carga igual, el que se lanzó antes ([15.10](#1510-orden-de-ejecución)). El segundo opera sobre el resultado del primero. |
| 2 | Un protón llega a un nodo que ya cambió a su propia facción | Se resuelve como absorción, no como ataque. La facción se comprueba **en el impacto**, no en el lanzamiento. |
| 3 | El nodo origen cambia de bando con protones en vuelo | Los protones conservan la facción del emisor en el momento del lanzamiento y completan su viaje. |
| 4 | El nodo destino deja de existir | Imposible: los nodos nunca se destruyen, solo cambian de facción. |
| 5 | Un protón excede el tope del destino aliado | El excedente se descarta. No rebota ni se redistribuye. |
| 6 | Un Magnetar reduce un protón exactamente a 0 | El protón se destruye. No impacta. La Energía se pierde. |
| 7 | Un protón de Carga 1 impacta un Magnetar con 0 de Energía | **Lo captura**, y queda a 0 de Energía. `effective = floor(1 * 0.5) = 0`, pero la condición de daño de [6.2](#62-resolución-de-impacto) es `effective < target.energy`, y `0 < 0` es **falsa**: se resuelve por la rama de captura con `remainder = 0`. La resistencia ×2 encarece **vaciar** un Magnetar, no rematarlo. Coherente con [6.4](#64-casos-de-captura-por-tipo) —cualquier nodo a 0 cae con Carga 1— y con [7.3](#73-cero-absoluto), que ya lo decía literalmente para el nodo congelado a 0. |
| 8 | Se congela un nodo que está reconvirtiéndose | **Permitido.** Se congela, y los 3 s de la reconversión **quedan pausados**; se reanudan al descongelar. No hay inmunidad: [7.2.1](#721-objetivo-válido) tiene **dos** exclusiones y esta habría sido una tercera, aplicada además a un solo poder de los tres. Es el mismo trato que ya recibe la carga de un Púlsar congelado ([7.3](#73-cero-absoluto)). Lo que **no** cambia es que una reconversión no se puede **iniciar** sobre un nodo ya congelado ([8.1](#81-reglas)). |
| 9 | Se congela un Púlsar a mitad de carga | La carga se pausa. Al descongelarse, se reanuda desde donde estaba. |
| 10 | Un Púlsar es capturado con un poder cargado | Ver 7.5: si la carga está al 100%, el nuevo dueño lo hereda. Si no, se resetea. |
| 11 | Un nodo infectado llega a 0 y sigue infectado | Correcto. Permanece en 0 y del mismo dueño hasta que alguien lo cure o lo capture. |
| 12 | Un nodo infectado es curado por su propio dueño | Pierde la infección y suma la Energía recibida. |
| 13 | Se lanza Decaimiento sobre un nodo ya infectado | Reinicia el efecto. No acumula la tasa. |
| 14 | Se lanza Agujero de Gusano sobre un nodo enemigo congelado | Lo captura y elimina el congelamiento. |
| 15 | Un power-up cae y nadie enciende el Asteroide | Expira a los 30 s. Arranca el siguiente temporizador. |
| 16 | Un power-up debe caer pero no hay Asteroides neutrales | El drop se salta. El temporizador se reinicia. No cuenta contra el máximo por nivel. |
| 17 | Dos Nubes de Hidrógeno consecutivas en el mismo bando | La segunda reinicia la duración a 20 s. No acumula magnitud. |
| 18 | El jugador se queda sin Estrellas pero con 80 de Energía en un Magnetar | Puede reconvertirlo por 30. No hay derrota por estancamiento. |
| 19 | El jugador se queda sin Estrellas y con 6 de Energía total | Derrota por estancamiento, siempre que no tenga protones en vuelo ni poderes cargados. |
| 20 | Un Magnetar neutral dispara al último protón antes de la victoria | Se resuelve normalmente. La victoria se comprueba después. |
| 21 | El Enemigo pierde su último nodo con protones en vuelo | Los protones se desvanecen. Comienza la cascada de conquista. |
| 22 | Selección múltiple con nodos a 1 de Energía | Los que no llegan al mínimo de envío se ignoran silenciosamente. Los demás envían. |
| 23 | Clic derecho sobre un nodo enemigo | Muestra tipo, nivel, Energía y velocidad de ataque. **No** muestra botones de mejora ni reconversión. |
| 24 | Un nodo alcanza el nivel máximo | El botón de mejora se sustituye por `NIVEL MÁXIMO`. |
| 25 | Envío al 100% desde un nodo que está siendo atacado | Legal. Es la maniobra de evacuación, disponible también para el jugador. |
| 26 | Un Asteroide propio recibe un power-up marcado | Imposible: los power-ups solo caen sobre Asteroides **neutrales**. |
| 27 | Se lanza un poder y, antes de que termine el haz, el objetivo pasa a ser propio del lanzador | El haz **se disipa sin efecto**. La Energía invertida se pierde y el poder no vuelve a estar cargado ([7.2.2](#722-el-haz)). |
| 28 | El Púlsar que lanzó es capturado durante los 0.25 s del haz | El haz **completa su efecto a favor de quien lo lanzó**. El Púlsar cambia de bando vacío ([7.5](#75-agujero-de-gusano), tabla de robo de carga). |
| 29 | Dos Púlsares rivales se lanzan un poder el mismo frame, cada uno contra el Púlsar del otro | Los dos haces duran lo mismo, así que se resuelven por orden de lanzamiento; con empate exacto, primero el del Púlsar con más Energía. El segundo opera sobre el resultado del primero y puede disiparse por el caso 27. |
| 30 | Se lanza Agujero de Gusano sobre un Magnetar neutral lleno | **Lo captura íntegro**, con sus 100 de Energía y su nivel. Es objetivo válido porque está encendido y no es propio ([7.2.1](#721-objetivo-válido)), e ignora la resistencia ×2. Cambia 200 de Carga por 60 de Energía. |
| 31 | Se lanza Cero Absoluto sobre un Magnetar neutral | Se congela y **deja de disparar a los dos bandos** mientras dure. El deshielo sigue [7.3](#73-cero-absoluto) sin cambios: cualquier facción que le envíe Energía lo drena, y al llegar a 0 pasa al bando del último emisor. Es la única forma de abrir un corredor sin pagar el muro entero. |
| 32 | Se lanza Decaimiento sobre un Magnetar neutral | Se infecta y pierde 2 E/s. Al llegar a 0 **sigue Neutral, sigue infectado y sigue disparando**: no tiene dueño que lo cure ([7.4](#74-decaimiento)). A partir de ahí basta Carga 1 para capturarlo (caso 7). Es lento y visible, y está disponible para los dos bandos. |
| 33 | Se intenta apuntar un poder a un Asteroide, propio, neutral o enemigo | No es objetivo válido ([7.2.1](#721-objetivo-válido)). **Ningún Asteroide lo es nunca**, sea de quien sea y tenga la Energía que tenga. |
| 34 | El Jugador tiene un poder cargado y no queda en el mapa ningún nodo encendido que no sea suyo | No hay objetivo legal y el poder **se queda cargado indefinidamente** ([7.1](#71-regla-de-carga)). No es un bloqueo: los Asteroides que queden se toman con protones y la partida termina igual. |
| 35 | Un Magnetar intenta derribar un haz de poder que cruza su radio | Imposible. El haz no es un protón y no es interceptable ([7.2.2](#722-el-haz)). |

---

## 18. Decisiones tomadas por defecto

Reglas que no estaban en el diseño original y que se añadieron para cerrar huecos. Todas son revisables sin afectar a la arquitectura.

| # | Regla añadida | Motivo |
|---|---|---|
| 1 | Auto-deshielo a los 30 s | Evita nodos congelados permanentemente que bloqueen la victoria |
| 2 | Cascada de conquista al ganar | Cumple "ningún nodo queda Neutral" sin obligar a limpiar el mapa manualmente |
| 3 | Buffer de 0.25 s en el doble clic | Sin él, el doble clic enviaría 75% en lugar de 100% |
| 4 | Los power-ups afectan a toda la facción | Aplicado solo al nodo encendido, el power-up sería inútil la mayoría de las veces |
| 5 | Daño del Magnetar = 2 por disparo | Ajusta la curva para que el nivel 1 sea testimonial y el nivel 5 decisivo |
| 6 | Un Magnetar aliado recibe Energía 1:1 | La resistencia ×2 penalizaría reforzar tu propia defensa |
| 7 | Derrota por estancamiento | Evita partidas matemáticamente perdidas que no terminan |
| 8 | Devolución del 50% al cancelar una carga | Permite rectificar sin castigo total |
| 9 | ~~Un Magnetar a 0 requiere Carga ≥ 2~~ → **un Magnetar a 0 cae con Carga 1** | **Nunca fue una decisión: era un error de cálculo.** Se leyó `floor(1 * 0.5) = 0` como «no pasa nada», pero la guarda de [6.2](#62-resolución-de-impacto) compara `effective < target.energy` y `0 < 0` es falsa, así que el pseudocódigo normativo **ya lo capturaba**. Corregido en [17](#17-casos-borde-y-reglas-de-resolución), caso 7. El multiplicador 0.5 y su redondeo hacia abajo **no se tocan** |
| 10 | Ruido de decisión | Hace torpe al Enemigo del mapa 1 sin escribir un algoritmo distinto |
| 11 | Evacuación | Sin ella el Enemigo regala nodos llenos y parece tonto |
| 12 | Un nodo no puede volver a Asteroide | No aporta nada y complica el menú de reconversión |
| 13 | Alcance **global** para los tres poderes, sin subir costes | El límite de rango obligaba a acercar el Púlsar, que es justo el nodo que menos debe moverse: no genera y no dispara. Con alcance global el Púlsar es artillería de posición, que es lo que su ficción pide. Los costes de [16.3](#163-poderes) se dejan intactos a propósito: el desequilibrio, si aparece, se corrige en el diseño de niveles |
| 14 | Objetivo de un poder = **cualquier nodo encendido no propio**; ningún Asteroide lo es nunca | Una sola regla en vez de una lista por poder. Excluye al Asteroide por dos motivos: no tiene nada que congelar, infectar ni robar, y capturarlo con un poder saltaría el doble coste de 10 + 10 que es el precio de expandirse ([4.1](./NOVA_Nodos.md#41-asteroide-nivel-0)). El Magnetar neutral **sí** entra, no por excepción sino porque cumple la regla: es el único nodo Neutral con nivel |
| 15 | El poder se entrega por **haz de 0.25 s**, no por proyectil | Con alcance global, un proyectil a 6 u/s tardaba 6.1 s en cruzar la diagonal del tablero de 32 u, y todavía casi 3 s en la de 17.2 u del tablero actual. El haz da una ventana de resolución mínima pero real, y la hace independiente de la distancia |
| 16 | El haz se disipa si el objetivo deja de ser válido | Coherente con el caso 2 de la [sección 17](#17-casos-borde-y-reglas-de-resolución): la facción se comprueba al resolver, no al lanzar |
| 17 | El haz se colorea por **poder**, no por facción | El color anuncia qué efecto va a caer antes de que caiga; quién lo lanza ya se lee por el extremo del que sale, que es un Púlsar con color de facción |
| 18 | La **mejora** se paga con la Energía del propio nodo y solo la impiden `Congelado` ([7.3](#73-cero-absoluto)) y `Reconvirtiendo` ([8.1](#81-reglas)) | Ningún documento decía quién la pagaba. Es el mismo criterio que la reconversión. Un nodo infectado «sigue funcionando con normalidad» ([7.4](#74-decaimiento)) y un Púlsar cargando puede mejorar: no hay nada que perder al subir de nivel dentro del mismo tipo. Cerrado el 2026-09-28 en `T1-04` |
| 19 | Un Púlsar **con un poder ya cargado** tampoco puede iniciar una reconversión, no solo mientras carga | 8.1 solo prohibía «cargando un poder». Si se permitiera con el poder listo, el poder desaparecería con el tipo. Para reconvertir hay que lanzarlo o cancelarlo antes. Cerrado el 2026-09-28 en `T1-04` |
| 20 | La reconversión **se paga al iniciar** y el tipo, el nivel y el tope **cambian al terminar** | Durante los 3 s el nodo sigue siendo del tipo de origen: conserva resistencia y tope. Es lo que hace coherente que una captura lo deje «como estaba antes de iniciarla» ([8.1](#81-reglas)): no hay cambio que deshacer. Cerrado el 2026-09-28 en `T1-04` |
| 21 | Si capturan un nodo que se reconvierte, **el coste pagado se pierde** | El nodo recupera tipo y nivel anteriores con la Energía que deja la captura ([6.2](#62-resolución-de-impacto)). Devolverlo regalaría Energía al captor. Mismo criterio que los protones derribados: la Energía gastada no vuelve. Cerrado el 2026-09-28 en `T1-04` |
| 22 | La lógica de juego corre en **`FixedUpdate` a 50 Hz**; la presentación, en `Update` | [12.7](./NOVA_Algoritmo_Enemigo.md#127-contrato-de-implementación) exige «misma entrada más misma semilla, misma partida, siempre», y ningún documento fijaba el paso de tiempo: con `deltaTime` variable la partida dependía de los FPS. Se eligió el tick fijo de Unity frente a un tick propio con acumulador porque no añade ningún sistema. Detalle en [15.10](#1510-orden-de-ejecución). Cerrado el 2026-09-28 antes de `T1-08` |
| 23 | **Picking:** el clic sobre un nodo se resuelve con `Physics2D.OverlapPoint` en la capa **`Nodes`**, contra el `CircleCollider2D` *trigger* de `NodeCollider2D` con el radio de la huella (0.3–0.6 u). **El lazo no usa física:** pide a `NodeRegistry` los nodos propios que pueden enviar y toma los que tienen el centro dentro del rectángulo arrastrado, Asteroides fuera ([13.1](#131-ratón)). `Nodes` es la única capa nueva: protones y haces no llevan collider, porque no colisionan ([6.1](#61-el-protón)) y el Magnetar los encuentra por `ProtonRegistry` | 15.5 declaraba el collider pero no cómo se resolvía el clic ni el lazo. Se mantiene lo ya escrito —trigger sin Rigidbody— frente a la alternativa sin física, que obligaba a corregir 15.5. Cerrado el 2026-09-28 antes de `T1-11` ([T.7](./NOVA_Tracking.md#t7-pendientes-abiertos) #37) |
| 24 | Un segundo clic en **otro** destino dentro del buffer **envía ya el 50% pendiente** y abre un buffer nuevo; solo es doble clic sobre el mismo nodo | 13.2 no decía qué pasaba. Descartar el primer envío castigaría al jugador que reparte rápido entre dos destinos. Cerrado el 2026-09-29 antes de `T1-11` |
| 25 | **Umbral de arrastre de 4 px** de la referencia (8 px a 1080p); el arrastre envía al soltar, **sin buffer** | 13.1 no separaba clic de arrastre. Un arrastre no puede ser doble clic, así que esperar 0.25 s solo añadiría latencia. El valor va en `BalanceConfig`. Cerrado el 2026-09-29 antes de `T1-11`. **Desde el 2026-10-10 el arrastre que envía es el del botón derecho** (#32); umbral y envío sin buffer no cambian |
| 26 | El lazo **sustituye** a la selección; el clic izquierdo en vacío **no hace nada** | Mantiene que la selección solo la cancela el clic derecho en vacío (13.1) y que un lazo siempre da el mismo resultado, haya lo que haya seleccionado. Cerrado el 2026-09-29 antes de `T1-11` |
| 27 | Un nodo que deja de ser propio **sale solo de la selección**; un doble clic sobre un nodo seleccionado **lo deselecciona y no envía** | Sin lo primero, la selección guardaría nodos del rival como orígenes. Sin lo segundo, deseleccionar con doble clic lanzaría un 50% sin querer contra el nodo recién soltado. Cerrado el 2026-09-29 antes de `T1-11` |
| 28 | Con el menú de encendido abierto, un clic izquierdo fuera de las opciones o un arrastre izquierdo **lo cierra y sigue su curso**; el botón derecho y `Esc` **solo** lo cierran, y el derecho no empieza un arrastre de envío (#32) | 13.7 decía qué cerraba el menú, pero no qué más hacía ese clic. Que siga su curso evita un clic perdido al querer seleccionar otra cosa; el clic derecho y `Esc` se consumen porque abrir el panel o pausar al cerrar un menú sorprende. Decidido en `T2-02` |
| 29 | Las opciones del menú de encendido de un tipo que la partida no tiene salen **deshabilitadas, en gris y sin coste**, distintas de las que solo esperan Energía | 13.7 da siempre las tres opciones. En la fase 2 el Magnetar no existe hasta `T2-04` ni el Púlsar hasta `T3-02`, y encenderlos ya daría nodos a medias. Se resuelve con datos: cada `NodeIgnition` ofrece los tipos cuya `NodeDefinition` tiene, y el bloqueo `ActionBlock.Unavailable` va por delante de la Energía. Es también la vía de T.7 #46 si hace falta un bloqueo por nivel. Decidido con el usuario en `T2-02` |
| 30 | El panel de clic derecho se abre **junto al nodo**, también en nodos ajenos (sin botones); **se queda abierto** hasta el clic derecho en vacío o `Esc`, y un clic izquierdo dentro es suyo | 13.3 decía qué contiene, no dónde ni cuándo se cierra. Abierto mientras se juega, porque mejorar varias veces seguidas es lo normal; que se trague el clic evita mandar un protón al nodo que hay debajo del botón. Decidido en `T2-03` |
| 31 | Torreta del Magnetar: con empate en distancia **y** en Carga dispara al protón **lanzado antes**; un protón a distancia **igual** al radio está dentro; el arma **espera cargada** en reposo y dispara en cuanto entra un objetivo, sin acumular disparos, y disparando sin pausa conserva su cadencia exacta | 4.4 solo daba el primer desempate y no decía nada del borde ni de qué hace el arma sin objetivo. El tercer desempate es el mismo que el de los impactos (15.10); el borde incluido es el que se dibuja (A.4.1); y un arma que acumulase disparos en reposo vaciaría de golpe un protón al entrar. Decidido en `T2-04` |
| 32 | El envío por arrastre pasa al **botón derecho**, con una **línea de envío** que sigue al cursor; el clic derecho actúa **al soltar**; el arrastre izquierdo es **siempre lazo** | Pedido por el usuario el 2026-10-10: la línea tiene que verse desde que el ratón se aleja del nodo, también si al final no se envía nada. El panel no puede abrirse al pulsar, porque en ese momento todavía no se sabe si es clic o arrastre. El arrastre izquierdo deja de enviar para no tener dos gestos para lo mismo, y así el lazo puede empezar encima de un nodo. Revisable: devolverle el envío al izquierdo no toca nada más que `InputRouter` |
| 33 | Reconversión en el panel: **dos botones** en el orden de [16.2](#162-costes-de-nivel), con el destino y el coste. Un tipo que la partida no tiene sale **gris y sin coste**, como en el menú de encendido (#29). Mientras dura, los botones dejan sitio a «Reconvirtiendo a …». Un botón bloqueado por algo que **no es la Energía** —reconvirtiendo, congelado— sale deshabilitado **sin el coste en rojo** | 13.3 decía qué contiene el punto 5, no cómo se ve. El rojo de 13.3 significa «te falta Energía»: pintarlo en un Mejorar bloqueado por la reconversión le diría al jugador que junte Energía, y no le serviría de nada. Decidido en `T2-07` |

---

## 19. Fases y criterios de salida

**La lista de tareas de cada fase no vive aquí: vive en [T.4](./NOVA_Tracking.md#t4-tareas).** Esta sección define **cuándo una fase está terminada**; T.4 define **qué hay que hacer para terminarla**, con IDs, dependencias, bloqueo por arte y estado.

Antes las dos enumeraban el mismo trabajo y derivaron: once tareas acabaron existiendo solo en T.4 y `NodeLevelComponent` figuraba en fases distintas en cada documento. De ahí salió que la fase 1 tuviera un criterio de salida que su propia lista de tareas no podía cumplir ([T.7 #33](./NOVA_Tracking.md#t7-pendientes-abiertos)). Una lista, un dueño.

Ninguna fase se cierra sin cumplir su criterio, y el orden de fases es obligatorio ([T.2](./NOVA_Tracking.md#t2-reglas-del-ciclo-de-trabajo) #2). En las fases 1 a 5 las primitivas grises son arte válido; en la 6 no ([T.2](./NOVA_Tracking.md#t2-reglas-del-ciclo-de-trabajo) #4 y #5).

| Fase | Nombre | Criterio de salida |
|---|---|---|
| 0 | Proyecto | El proyecto abre en Unity, `Nova.Core` compila sin referenciar `UnityEngine` y un test vacío corre en EditMode. Creada el 2026-09-28: no existía proyecto y [T.4](./NOVA_Tracking.md#t4-tareas) empezaba dando por hecho que sí. |
| 1 | Núcleo jugable | Se puede capturar un Asteroide, encenderlo como Estrella y tomar un nodo enemigo. |
| 2 | Tipos y defensa | Los cuatro tipos son funcionales y el nivel 3 es jugable. |
| 3 | Poderes | Los tres poderes funcionan y pasan los casos 8–14 y 27–35 de la [sección 17](#17-casos-borde-y-reglas-de-resolución). |
| 4 | Algoritmo del Enemigo | El algoritmo del mapa 3 vence a un jugador descuidado. |
| 5 | Flujo y contenido | Los 9 niveles son completables de principio a fin. |
| 6 | Presentación | Todos los assets de [A.7](./NOVA_Assets_Diseno.md#a7-resumen-de-progreso) están ✅ **e integrados en Unity**. |

> **El criterio de la fase 6 estaba descolgado** y se corrige aquí: remitía a «el documento 2», que hoy se llama [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md) y además describe el *pipeline*, no un criterio de cierre. Se adopta el que ya usaba [T.6](./NOVA_Tracking.md#t6-criterios-de-salida-por-fase), que sí es comprobable.

---

## Apéndice — Pendientes para el documento 2

> El inventario de qué arte falta, con IDs y nombres de archivo, vive ahora en [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md). El documento 2 solo debe aportar el *cómo* (pipeline y estilo), no el *qué*.

| Pendiente | Estado |
|---|---|
| Paleta completa y rampas de color | ✅ `tools/nova_paleta.py`, reglas en el contrato |
| Tamaño de sprite por tipo y nivel | ✅ Huella 0.6–1.2 u, celda 96 px = 1.5 u, PPU 64 ([A.8](./NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas) #4) |
| Número de frames por animación | ✅ 12 fps fijo, `frames = ceil(duración × 12)` |
| Pipeline de generación en Python | ✅ [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md) + `tools/` |
| Convención de nombres de assets | ✅ [A.1](./NOVA_Assets_Diseno.md#a1-convención-de-nombres) |
| Tipografía | ✅ Una pixel font libre en TextMeshPro |
| Atlas y pivotes | ⬜ Pivote decidido (Bottom / Center); el empaquetado en atlas, no |
| Estilo de la UI | ⬜ Solo está la paleta; falta el lenguaje de paneles y botones |
| Diseño de los 9 niveles con coordenadas exactas | ⬜ Sin empezar. **Desbloqueado el 2026-09-28**: la escala del tablero ya está cerrada en 15 × 8.4375 u ([T.8](./NOVA_Tracking.md#t8-escala-del-tablero--cerrada)) |
