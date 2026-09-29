# NOVA — Algoritmo del Enemigo

> Parte de la especificación de **NOVA**. Documento maestro: [NOVA_GameDesign_Spec.md](./NOVA_GameDesign_Spec.md).
> Corresponde a la **sección 12** del maestro; la numeración `12.x` se conserva.
>
> Este documento es **algorítmico**: define el comportamiento computable del Enemigo. No contiene reglas de juego ni arte. Las reglas que el algoritmo debe respetar están en el maestro; los nodos sobre los que opera, en [NOVA_Nodos.md](./NOVA_Nodos.md).
>
> **Aquí no hay ningún modelo de inteligencia artificial.** El Enemigo lo controla un algoritmo determinista: una función de puntuación, un umbral y un ruido con semilla inyectable. No aprende, no se entrena, no infiere y no cambia entre partidas; con la misma entrada y la misma semilla produce siempre la misma salida, y eso está exigido en [12.7](#127-contrato-de-implementación). En este proyecto «IA» designa únicamente al **asistente** con el que se escribe el código ([T.1](./NOVA_Tracking.md#t1-prompt-de-continuación)), nunca al oponente.
>
> **Scripts que lo implementan** (ver [15.7](./NOVA_GameDesign_Spec.md#157-input-enemigo-y-ui)): `EnemyController.cs`, `EnemyEvaluator.cs`, `EnemyThreatResponder.cs`, `EnemyCommandIssuer.cs`, `EnemyProfile.cs`.

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
| Reforzar nodo | Un nodo propio está bajo amenaza y otro tiene excedente |
| Cargar poder | Posee un Púlsar sin poder cargado |
| Lanzar poder | Posee un Púlsar con poder cargado y existe **al menos un nodo encendido que no sea suyo** —del Jugador, o el Magnetar neutral— ([7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido)) |
| Reconvertir | Se queda sin Estrellas y tiene un nodo con Energía suficiente |

> **«Dentro del alcance efectivo» se ha eliminado de la primera fila, y es la corrección de un hueco antiguo.** Nunca existió un alcance de envío: [5.2](./NOVA_GameDesign_Spec.md#52-envío) permite a cualquier nodo que no sea Asteroide enviar a **cualquier** otro nodo del mapa, sin límite de distancia, desde la primera versión del documento. La distancia solo encarece el envío indirectamente —el protón tarda más en llegar y cruza más radios de Magnetar—, y eso ya lo mide `RiesgoDeRuta` en 12.3. El filtro real es la Energía, no la geometría.

### 12.3 Función de puntuación (atacar)

```
Score(origen, destino) =
      ValorTipo(destino)              # Estrella 100, Púlsar 80, Magnetar 70, Asteroide 40
    + BonusPowerUp(destino)           # +60 si el Asteroide está marcado con power-up
    - CosteEfectivo(origen, destino)  # energía necesaria, incluida resistencia x2
    - RiesgoDeRuta(origen, destino)   # 6 por cada Magnetar hostil que cubre la trayectoria
    - PenalizacionVaciado(origen)     # 30 si dejar el origen por debajo del 30% lo expone
    * MultiplicadorAgresividad
```

Se descartan de entrada las acciones cuyo `CosteEfectivo` supere la Energía disponible del origen al 100%.

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

### 12.4 Detección de amenaza y defensa

Un nodo propio está **amenazado** si la suma de Carga de los protones hostiles dirigidos a él, aplicando su multiplicador, es mayor o igual que su Energía actual.

Respuesta, en este orden de prioridad:
1. Si un nodo propio tiene Energía suficiente y está a distancia, **reforzar** (enviar el 50%).
2. Si tiene un Púlsar con Cero Absoluto cargado, **congelar el nodo origen del ataque**. Ya no hay condición de alcance: el origen siempre es alcanzable, esté donde esté. Y siempre es objetivo legal, porque [5.2](./NOVA_GameDesign_Spec.md#52-envío) prohíbe que un Asteroide envíe, así que todo atacante es un nodo encendido.
3. Si el refuerzo no llega a tiempo, **evacuar**: enviar toda la Energía del nodo amenazado a otro nodo propio, cediendo un nodo vacío en lugar de un nodo lleno.

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

El **ruido de decisión** altera aleatoriamente la puntuación final. Es el mecanismo principal para que el mapa 1 se sienta torpe sin necesidad de un algoritmo distinto.

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
