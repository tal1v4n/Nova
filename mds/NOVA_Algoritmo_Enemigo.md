# NOVA — Algoritmo del Enemigo

> Parte de la especificación de **NOVA**. Documento maestro: [NOVA_GameDesign_Spec.md](./NOVA_GameDesign_Spec.md).
> Corresponde a la **sección 12** del maestro; la numeración `12.x` se conserva.
>
> Este documento es **algorítmico**: define el comportamiento computable del Enemigo. No contiene reglas de juego ni arte. Las reglas que el algoritmo debe respetar están en el maestro; los nodos sobre los que opera, en [NOVA_Nodos.md](./NOVA_Nodos.md).
>
> **Aquí no hay ningún modelo de inteligencia artificial.** El Enemigo lo controla un algoritmo determinista: una función de puntuación, un umbral y un ruido con semilla inyectable. No aprende, no se entrena, no infiere y no cambia entre partidas; con la misma entrada y la misma semilla produce siempre la misma salida, y eso está exigido en [12.7](#127-contrato-de-implementación). En este proyecto «IA» designa únicamente al **asistente** con el que se escribe el código ([T.1](./NOVA_Tracking.md#t1-prompt-de-continuación)), nunca al oponente.
>
> **Scripts que lo implementan** (ver [15.7](./NOVA_GameDesign_Spec.md#157-input-enemigo-y-ui)): `EnemyController.cs`, `EnemyEvaluator.cs`, `EnemyThreatResponder.cs`, `EnemyCommandIssuer.cs`, `EnemyProfile.cs` y `EnemyScoring.cs`, que guarda los pesos de las fórmulas de 12.3 (todo número de balance va en un ScriptableObject).

---

## 12. Algoritmo del Enemigo

El Enemigo no es un jugador humano **ni un modelo entrenado**. Es un algoritmo que evalúa el estado del tablero y ejecuta las mismas acciones legales que el jugador.

### 12.1 Ciclo de decisión

```
cada IntervaloDeDecision segundos:
    1. Snapshot del tablero (NodeRegistry, ver 15.6)
    2. Si hay una amenaza crítica -> responder (defensa)
    3. Si no, generar lista de acciones candidatas
    4. Puntuar cada candidata
    5. Ejecutar la mejor si su puntuación supera UmbralDeAccion
    6. Si ninguna supera el umbral, no hacer nada (acumular)
```

El algoritmo **nunca actúa más de una vez por ciclo**. Esto lo hace legible y evita ráfagas inhumanas.

### 12.2 Acciones candidatas

| Acción | Cuándo se considera |
|---|---|
| Atacar nodo | Existe un nodo no propio y el origen tiene Energía suficiente para el envío |
| Encender Asteroide | Posee un Asteroide con ≥ 10 de Energía |
| Mejorar nodo | Un nodo propio tiene Energía ≥ coste de mejora |
| Reforzar nodo | **Solo como respuesta de [12.4](#124-detección-de-amenaza-y-defensa)**, que va antes de puntuar: no compite en la lista ni tiene puntuación propia |
| Cargar poder | Posee un Púlsar sin poder cargado |
| Lanzar poder | Posee un Púlsar con poder cargado y existe **al menos un nodo encendido que no sea suyo** —del Jugador, o el Magnetar neutral— ([7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido)) |
| Reconvertir | Se queda sin Estrellas y tiene un nodo con Energía suficiente |
| Abastecer | Un nodo propio necesita Energía para lo siguiente que le toca —un Asteroide para encenderse, un Púlsar para su poder— y otro nodo propio que no sea Púlsar puede enviarle ([12.3.4](#1234-estrategia-expandir-abastecer-y-no-repetir-envíos)) |

**Atacar un nodo Neutral es Expandir** y puntúa como economía ([12.3.4](#1234-estrategia-expandir-abastecer-y-no-repetir-envíos)): sigue siendo la acción *Atacar* de la tabla, con su lugar en el orden de desempate, pero con otra fórmula.

> **«Dentro del alcance efectivo» se ha eliminado de la primera fila, y es la corrección de un hueco antiguo.** Nunca existió un alcance de envío: [5.2](./NOVA_GameDesign_Spec.md#52-envío) permite a cualquier nodo que no sea Asteroide enviar a **cualquier** otro nodo del mapa, sin límite de distancia, desde la primera versión del documento. La distancia solo encarece el envío indirectamente —el protón tarda más en llegar y cruza más radios de Magnetar—, y eso ya lo mide `RiesgoDeRuta` en 12.3. El filtro real es la Energía, no la geometría.

### 12.3 Función de puntuación (atacar)

```
Score(origen, destino) =
    (
          ValorTipo(destino) * Alcance       # Estrella 100, Púlsar 80, Magnetar 70, Asteroide 40
        + BonusPowerUp(destino)              # +60 si el Asteroide está marcado con power-up
        - CosteEfectivo(origen, destino)     # energía necesaria, incluida resistencia x2
        - RiesgoDeRuta(origen, destino)      # 6 por cada Magnetar hostil que cubre la trayectoria
        - PenalizacionVaciado(origen)        # 30 si dejar el origen por debajo del 30% lo expone
    )
    * MultiplicadorAgresividad
```

**El multiplicador afecta a la suma entera**, como en 12.3.1. Una versión anterior lo escribía sin paréntesis y, por precedencia, solo multiplicaba `PenalizacionVaciado` (T.7 #48, cerrado el 2026-10-11).

Se descartan de entrada las acciones cuyo `CosteEfectivo` supere la Energía disponible del origen al 100%.

**Cuánto se envía y `Alcance`.** El ataque envía el 50 % salvo que haga falta más ([5.2](./NOVA_GameDesign_Spec.md#52-envío)):

1. Si `floor(Energía del origen × 0.5) ≥ CosteEfectivo`, envía el **50 %** y `Alcance = 1`.
2. Si no, y el perfil lo permite (*Envía al 100 %*, [12.5](#125-perfiles-por-mapa)), envía el **100 %** cuando `Energía del origen ≥ CosteEfectivo × Margen`, y `Alcance = 1`. El margen cubre los disparos de los Magnetares por el camino: con 1.2 el mapa 3 se arriesga más que el mapa 2 con 1.5.
3. Si no, envía el **50 % como ataque de desgaste**, que no captura: `Alcance = Carga enviada / CosteEfectivo`, de 0 a 1.

**Los demás términos:**

- **`PenalizacionVaciado`** se aplica cuando el origen queda por debajo del 30 % de su Tope **y** algún nodo del Jugador podría capturarlo tal como queda con un solo envío del 50 %. Eso es lo que significa «lo expone»: si nadie puede castigarlo, vaciarlo no cuesta nada.
- **`BonusPowerUp`** vale 0 si el perfil no tiene *Prioriza power-ups*.
- **`RiesgoDeRuta`**: un Magnetar «cubre la trayectoria» si el segmento origen→destino corta su radio. Es **hostil** si no es del Enemigo, y eso incluye al Magnetar neutral, que dispara a los dos bandos ([3.2](./NOVA_GameDesign_Spec.md#32-nota-sobre-el-magnetar-neutral)).

### 12.3.1 Función de puntuación (lanzar poder)

El alcance global del Púlsar ([7.2](./NOVA_GameDesign_Spec.md#72-tabla-de-poderes)) le quita al algoritmo su único filtro barato: antes descartaba por distancia, ahora **todo nodo encendido que no sea suyo es candidato**, los del Jugador y el Magnetar neutral. Hace falta una puntuación propia, porque la de atacar no sirve: un poder no gasta Energía en el momento de lanzarse —ya la gastó al cargarse— y por tanto no tiene `CosteEfectivo` ni `PenalizacionVaciado`.

```
ScorePoder(poder, objetivo) =
    (
          ValorTipo(objetivo)                  # Estrella 100, Púlsar 80, Magnetar 70
        + Afinidad(poder, objetivo)            # ver tabla
        + BonusRobo(poder, objetivo)           # +60 si es Gusano sobre un Púlsar cargado al 100%
        + EnergiaRelativa(objetivo)            # (energia / tope) * 40
        - AjusteNeutral(objetivo)              # 25 si el objetivo es Neutral
        - Redundancia(poder, objetivo)         # 100 si el objetivo ya sufre ese mismo estado
    )
    * MultiplicadorAgresividad
```

**Candidatos.** Todos los nodos de nivel ≥ 1 que no sean del Enemigo: los del Jugador y el Magnetar neutral. **Los Asteroides quedan fuera antes de puntuar**, sean de quien sean: no son objetivo legal, así que no entran en la lista. Si la lista queda vacía, la acción *Lanzar poder* no se genera y el algoritmo conserva el poder cargado, que no caduca ([7.1](./NOVA_GameDesign_Spec.md#71-regla-de-carga)).

**`AjusteNeutral`.** Un objetivo Neutral resta 25, y no es una penalización arbitraria: quitarle un nodo al rival vale más que ganar uno que el rival tampoco tenía. La diferencia se compensa sola cuando de verdad conviene, porque un Magnetar neutral lleno suma `EnergiaRelativa` al máximo y, si el poder es el Gusano, una `Afinidad` de +50. El resultado es que el Enemigo roba el muro **cuando está lleno y le estorba**, no por sistema.

**Afinidad.** Cada poder vale más contra un tipo:

| Poder | vs. Estrella | vs. Púlsar | vs. Magnetar | Razón |
|---|---|---|---|---|
| **Cero Absoluto** | +20 | **+40** | +30 | Congelar un Púlsar le pausa la carga ([7.3](./NOVA_GameDesign_Spec.md#73-cero-absoluto)); es lo único que frena un contraataque de poder. |
| **Decaimiento** | **+40** | +10 | +20 | Ataca al único generador del juego, y la Estrella no puede reponer 2 E/s ni a nivel 5 ([4.2](./NOVA_Nodos.md#42-estrella)). |
| **Agujero de Gusano** | +30 | +20 | **+50** | Ignora la resistencia ×2, así que sobre un Magnetar lleno ahorra 200 de Carga: es el mayor ahorro posible del juego. |

**Redundancia.** Cero Absoluto sobre un nodo ya Congelado y Decaimiento sobre uno ya Infectado restan 100: son legales ([17](./NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución), caso 13) pero desperdician la carga. El Agujero de Gusano nunca es redundante.

**El Magnetar neutral entra en la fórmula como un Magnetar más**, con su `ValorTipo` de 70, su afinidad por poder y su `AjusteNeutral`. No necesita reglas propias, y eso es deliberado: en cuanto el algoritmo tuviera un caso especial para él, [3.1](./NOVA_GameDesign_Spec.md#31-regla-de-paridad) dejaría de cumplirse en espíritu aunque se cumpliera en la letra.

**Umbral.** `ScorePoder` compite en la misma lista que las demás candidatas de 12.2 y se somete al mismo `UmbralDeAccion` y al mismo ruido de 12.5. Un Enemigo con poder cargado y sin buenos objetivos **espera**, que es exactamente lo que haría un jugador.

### 12.3.2 Funciones de puntuación (encender, mejorar, cargar poder y reconvertir)

Las demás acciones de 12.2 puntúan en la misma escala para poder competir contra `UmbralDeAccion` (T.7 #48, cerrado el 2026-10-11). Reforzar no tiene función: solo existe como respuesta de 12.4.

**Encender.** Una candidata por cada tipo que el Asteroide puede ser:

```
ScoreEncender(asteroide, tipo) = 50 + Necesidad(tipo)

Necesidad(Estrella) = 40 / (1 + EstrellasPropias)    # 40 sin ninguna, 20 con una, 13 con dos
Necesidad(Púlsar)   = 30 si no tiene ningún Púlsar, si no 0
Necesidad(Magnetar) = 30 si el Asteroide es el nodo propio más cercano a algún nodo del Jugador, si no 0
```

Un Asteroide propio sin encender es capital muerto ([4.1](./NOVA_Nodos.md#41-asteroide-nivel-0)): encender supera siempre el umbral y la necesidad decide el tipo. Qué tipos ofrece cada mapa es T.7 #46.

**Mejorar.**

```
ScoreMejorar(nodo) = BaseMejora(tipo) - coste / 2 - PenalizacionVaciado(nodo tras pagar)

BaseMejora: Estrella 60, Púlsar 50, Magnetar 40
```

Una Estrella de nivel 1 a 2 puntúa 50 y de 4 a 5, 35; un Magnetar de 1 a 2, 30. `PenalizacionVaciado` es la de 12.3, con la Energía que le queda al nodo después de pagar.

**Cargar poder.** Una candidata por cada poder que el nivel del Púlsar desbloquea:

```
ScoreCarga(pulsar, poder) =
      max(ScorePoder(poder, objetivo)) / 2    # sobre los candidatos de 12.3.1; 0 si no hay ninguno
    - coste / 2
    - max(0, coste - Energía del Púlsar)       # una carga que se quedaría parada (7.1)
```

Cargar vale la mitad de lo que valdría lanzar ese poder ahora, menos su precio. Sin ningún objetivo legal la candidata no se genera.

**Reconvertir.** Solo sin Estrellas propias y con *Usa reconversión* en el perfil:

```
ScoreReconvertir(nodo) = 80 - coste    # el destino es siempre Estrella
```

El origen es el nodo propio con más Energía que puede pagarla y no tiene un poder en carga ni cargado ([8.1](./NOVA_GameDesign_Spec.md#81-reglas)). Puntúa 60 o 50: es supervivencia y supera siempre el umbral.

**El multiplicador de agresividad** solo afecta a atacar al rival, lanzar poder y cargar poder, que hereda `ScorePoder`. Encender, mejorar, reconvertir, expandir y abastecer son economía.

### 12.3.4 Estrategia: expandir, abastecer y no repetir envíos

Añadido el 2026-10-20 tras la primera prueba de juego del flujo completo. Con 12.3–12.3.2 tal cual, el Enemigo **solo sabía atacar**, y no por diseño sino por tres huecos de las fórmulas:

1. **Capturar un Asteroide neutral no llegaba al umbral del mapa 1.** `(40 − 10) × 0.7 = 21`, contra un `UmbralDeAccion` de 40: el Enemigo del mapa 1 no se expandía nunca. Lo único que superaba el umbral era capturar una Estrella del Jugador recién vaciada, `(100 − 13) × 0.7 = 61`: castigaba cada envío del Jugador y no hacía nada más.
2. **Un Asteroide capturado se quedaba muerto.** El 50 % de una Estrella con 25 es 12, que captura un Asteroide de 10 y le deja 2. Ninguna acción de 12.2 le enviaba Energía a un nodo propio salvo el refuerzo de 12.4, que solo responde a amenazas: nunca llegaba a 10 y no se encendía.
3. **Repetía el envío que ya estaba en el aire.** El coste se calculaba con la Energía del destino, sin mirar los protones propios que ya iban hacia él: cada ciclo volvía a mandar el mismo ataque mientras el primero volaba.

**Expandir** (atacar un nodo Neutral):

```
ScoreExpandir(origen, destino) =
      (ValorTipo(destino) + NecesidadExpansion) * Alcance
    + BonusPowerUp(destino)
    - CostePendiente(destino)
    - RiesgoDeRuta(origen, destino)
    - PenalizacionVaciado(origen)
    - 2 * Distancia(origen, destino)

NecesidadExpansion = 40 / NodosPropios          # 40 con uno, 20 con dos, 10 con cuatro
```

Sin `MultiplicadorAgresividad`: tomar un nodo que nadie tiene es crecer, no agredir. La distancia **sí** entra aquí, y no contradice 12.7: la prohibición es de los poderes, y un protón que va lejos tarda más y deja al rival más tiempo para disputar el Asteroide. Con ella el Enemigo toma primero lo que tiene a su lado. En el nivel 1, al empezar, el Asteroide de su lado puntúa 66, el central 59 y el del Jugador 53.

**Abastecer** (enviar el 50 % a un nodo propio):

```
ScoreAbastecer(origen, destino) = Base(destino) * min(1, Carga / Faltante) - PenalizacionVaciado(origen)

Base: Asteroide 70, Púlsar 45
Faltante(Asteroide) = coste de encendido − Energía − Carga propia en vuelo
Faltante(Púlsar)    = coste del poder que carga (o, sin carga, del más caro que su nivel permite)
                      − Energía − Carga propia en vuelo
```

Solo se genera con `Faltante > 0`, para un Asteroide que el nivel deja encender y un Púlsar sin poder listo ni reconversión en curso. **Un Púlsar no abastece**: su Energía es munición. El envío es siempre el 50 % ([5.2](./NOVA_GameDesign_Spec.md#52-envío)): lo que sobre al tope se pierde, como para el Jugador.

**Carga en vuelo.** `CostePendiente = CosteEfectivo − Carga propia que ya vuela hacia el destino`. Si es ≤ 0 la candidata no se genera: el ataque que ya va en camino basta. Es lo que el Jugador ve en pantalla, así que no rompe 12.6.

El resultado es un ciclo que se lee como un plan: tomar el Asteroide cercano, alimentarlo, encenderlo (Estrella, luego Púlsar o Magnetar según 12.3.2), mejorar, cargar poderes y atacar cuando hay una apertura.

### 12.3.3 Ruido y empates

- **Ruido.** Cada candidata se multiplica por `1 + U(-r, +r)`, con `r` el *Ruido de decisión* del perfil, **antes** de compararla con el umbral. `U` sale del `System.Random` con semilla de [12.7](#127-contrato-de-implementación).
- **Empates.** Dos candidatas con la misma puntuación final se ordenan por el orden de acciones de la tabla de 12.2, después por el orden de `NodeRegistry` del nodo que actúa y después por el del objetivo. Sin esta regla el determinismo de 12.7 dependería del orden en que se generan las candidatas.

### 12.4 Detección de amenaza y defensa

Un nodo propio está **amenazado** si la suma de Carga de los protones hostiles dirigidos a él, aplicando su multiplicador, es mayor o igual que su Energía actual.

Respuesta, en este orden de prioridad:
1. Si un nodo propio tiene Energía suficiente y está a distancia, **reforzar** (enviar el 50%). *A distancia*: su protón llega antes que el primer impacto que capturaría el nodo amenazado, comparando su tiempo de vuelo con el de `ThreatCalculator`. *Suficiente*: `floor(Energía del donante × 0.5) ≥ Carga hostil efectiva − Energía del amenazado + 1`. Entre varios donantes, el que llega antes y, con empate, el que tiene más Energía.
2. Si tiene un Púlsar con Cero Absoluto cargado, **congelar el nodo origen del ataque**. Ya no hay condición de alcance: el origen siempre es alcanzable, esté donde esté. Y siempre es objetivo legal, porque [5.2](./NOVA_GameDesign_Spec.md#52-envío) prohíbe que un Asteroide envíe, así que todo atacante es un nodo encendido.
3. Si el refuerzo no llega a tiempo, **evacuar**: enviar toda la Energía del nodo amenazado a otro nodo propio, cediendo un nodo vacío en lugar de un nodo lleno. El destino es el nodo propio no amenazado más cercano. Solo con *Usa evacuación* en el perfil.

La evacuación es lo que separa a un Enemigo que se defiende de uno que regala nodos llenos. Debe implementarse.

### 12.5 Perfiles por mapa

`EnemyProfile` es un ScriptableObject (ver [15.4](./NOVA_GameDesign_Spec.md#154-data--scriptableobjects)). Un perfil por mapa.

| Parámetro | Mapa 1 | Mapa 2 | Mapa 3 |
|---|---|---|---|
| Intervalo de decisión | 3.0 s | 2.0 s | 1.2 s |
| Umbral de acción | 40 | 25 | 10 |
| Multiplicador de agresividad | 0.7 | 1.0 | 1.3 |
| Usa evacuación | No | Sí | Sí |
| Usa reconversión | No | No | Sí |
| Prioriza power-ups | No | Sí | Sí |
| Envía al 100% | Nunca | Si el margen es > 1.5× | Si el margen es > 1.2× |
| Ruido de decisión | ±25% | ±15% | ±5% |

El **ruido de decisión** altera aleatoriamente la puntuación final ([12.3.3](#1233-ruido-y-empates)). Es el mecanismo principal para que el mapa 1 se sienta torpe sin necesidad de un algoritmo distinto.

*Envía al 100 %* guarda el **margen** de [12.3](#123-función-de-puntuación-atacar): el 100 % solo sale si la Energía del origen es al menos `CosteEfectivo × margen`. «Nunca» es no tener margen. *Prioriza power-ups* activa `BonusPowerUp`; sin él vale 0.

### 12.5.1 Paciencia por nivel

Durante los primeros segundos de la partida el Enemigo **no ataca al Jugador ni le lanza poderes**. Todo lo demás sigue: expandir, abastecer, encender, mejorar, cargar poderes y la defensa de 12.4. Es la ventana en la que el Jugador aprende sin que le quiten la Estrella al primer envío. Es un parámetro más de `EnemyProfile` (`gracePeriod`), en el perfil de cada nivel de 12.5.2. Valores iniciales:

| Nivel | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|
| Paciencia | 45 s | 30 s | 20 s | 15 s | 10 s | 5 s | 0 | 0 | 0 |

Añadida el 2026-10-20: el nivel 1 resultaba casi imposible de ganar en la prueba de juego, porque el Enemigo capturaba la Estrella del Jugador en cuanto este la vaciaba para tomar su primer Asteroide (12.3.4, hueco 1). Los valores son una propuesta, **pendiente de prueba de juego**.

### 12.5.2 Ajuste por nivel

Añadido el 2026-10-20 para afinar la curva jugando. **Cada nivel tiene su propio perfil**, `Assets/Data/EnemyProfiles/Level{n}Enemy.asset`, que es el que referencia su `LevelDefinition`. Los tres perfiles de la tabla de 12.5, `Map1`–`Map3`, se quedan como **valores de referencia**: los usan los tests de 12.7 y de ellos nacen los de nivel.

- **Nova → Dificultad** (`DifficultyWindow`) enseña los nueve perfiles en una tabla, un nivel por columna. Edita los assets con Undo; en Play, el Enemigo los lee en cada ciclo, así que el cambio se nota en la partida en curso y se conserva al salir. Marca en rojo la celda de un nivel más fácil que el anterior, y tiene *Copiar →* al nivel siguiente, *Restablecer* a 12.5 y *Interpolar* en línea recta entre dos niveles. *Reconvierte* no se edita: lo fija el nivel (11.1), igual para los dos bandos.
- **Overlay de depuración** (`EnemyDebugOverlay`, **F1** en Play, solo en el Editor y en builds de desarrollo): perfil en uso, si está paciente u hostil, las seis mejores candidatas del último ciclo con su puntuación ya con ruido —en verde las que superan el umbral— y las últimas decisiones.
- **Generar los 9 niveles** crea `Level{n}Enemy` si falta, como copia de su mapa con la Paciencia de 12.5.1, y **nunca lo sobrescribe**.
- **Los tests no fijan sus números**, solo reglas: valores válidos, un perfil distinto por nivel, *Reconvierte* = lo que permite el nivel, y que de un nivel al siguiente ni el intervalo, ni el umbral, ni el ruido, ni la Paciencia suban, ni la agresividad baje. Una celda en rojo en la ventana es un test en rojo.

### 12.6 Prohibiciones

El algoritmo no puede: ver protones antes de que se lancen, conocer los poderes que el jugador está cargando antes de que la barra sea visible, generar Energía extra, ni ignorar tiempos de carga o de reconversión.

---


---

## 12.7 Contrato de implementación

| Requisito | Regla |
|---|---|
| Canal único | Toda acción sale por `INodeCommandIssuer` ([15.1](./NOVA_GameDesign_Spec.md#151-principios), principio 5). |
| Pureza | `EnemyEvaluator` es estático y sin estado: misma entrada → misma salida (salvo el ruido, que recibe una semilla). |
| Determinismo testeable | El ruido de decisión usa un `System.Random` con semilla inyectable, no `UnityEngine.Random`. **Es lo que hace que el Enemigo sea un algoritmo y no una caja negra:** misma entrada más misma semilla, misma partida, siempre. |
| Una acción por ciclo | `EnemyController` ejecuta como máximo un comando por `IntervaloDeDecision`. |
| Sin distancia en los poderes | Ni `ScorePoder` ni `EnemyThreatResponder` pueden leer la posición del objetivo para decidir si lanzan. El alcance es global ([7.2](./NOVA_GameDesign_Spec.md#72-tabla-de-poderes)) y un Enemigo que "se acerque para lanzar" estaría obedeciendo una regla que ya no existe. |
| Sin arte | Ninguna tarea del algoritmo depende de assets. La fase 4 del tracking es desbloqueable siempre. |

### Tests mínimos exigidos

1. `EnemyEvaluator` prefiere una Estrella nivel 3 a un Asteroide cuando ambos son alcanzables.
2. Descarta un objetivo cuyo `CosteEfectivo` supera la Energía del origen al 100%.
3. `RiesgoDeRuta` penaliza 6 por cada Magnetar hostil cuyo radio cruza el segmento origen→destino.
4. Con perfil de mapa 1, dos ejecuciones con semillas distintas pueden elegir acciones distintas; con la misma semilla, no.
5. `EnemyThreatResponder` evacúa (no refuerza) cuando el refuerzo no llega antes del impacto.
6. `ScorePoder` no genera ni un solo candidato cuando los únicos nodos del Jugador son Asteroides, aunque el Púlsar tenga un poder cargado y los tenga a 1 u.
7. `ScorePoder` elige el mismo objetivo con el Púlsar pegado a él que con el Púlsar en la esquina opuesta del tablero: la distancia no entra en la fórmula.
8. Con Cero Absoluto cargado y dos objetivos idénticos, uno ya Congelado y otro no, elige el que no lo está (`Redundancia`).
9. Un Magnetar neutral lleno entra en la lista de candidatos de `ScorePoder`; un Asteroide neutral con 10 de Materia Inerte, no.
10. `Score` aplica `MultiplicadorAgresividad` a la suma entera: con 0.7 y con 1.3, la misma candidata puntúa 0.7 y 1.3 veces lo que puntúa con 1.0.
11. Un ataque cuyo 50 % no alcanza el `CosteEfectivo` envía el 100 % con el margen del perfil, y el 50 % de desgaste con `Alcance < 1` sin él.
12. `PenalizacionVaciado` no se aplica si ningún nodo del Jugador podría capturar el origen tal como queda.
13. `RiesgoDeRuta` cuenta el Magnetar neutral y no cuenta los Magnetares del Enemigo.
14. Sin Estrellas, `ScoreEncender` elige Estrella; con dos Estrellas y sin Púlsar, Púlsar.
15. Con el mismo ruido y dos candidatas empatadas, gana siempre la misma, por el orden de 12.3.3.
16. Expandir no lleva `MultiplicadorAgresividad`, prefiere el Asteroide más cercano y, con el perfil del mapa 1, supera el umbral.
17. `NecesidadExpansion` es 40 con un nodo propio y 10 con cuatro.
18. Abastece un Asteroide propio con 2 de Energía y deja de hacerlo cuando lo que ya vuela hacia él cubre lo que falta.
19. Abastece un Púlsar parado a media carga con lo que cuesta su poder, y un Púlsar nunca abastece.
20. Un ataque cuya Carga ya vuela hacia el destino solo cuenta lo que falta, y no se genera si ya lo cubre.
21. Durante la Paciencia (12.5.1) no ataca al Jugador ni le lanza poderes, y sí se expande y mejora.
22. En el nivel 1, al empezar, la primera acción es tomar el Asteroide de su lado.
