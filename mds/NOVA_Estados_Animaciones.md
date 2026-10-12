# NOVA — Estados y animaciones

> Parte de la especificación de **NOVA**. Documento maestro: [NOVA_GameDesign_Spec.md](./NOVA_GameDesign_Spec.md).
> Corresponde a la **sección 14** del maestro; la numeración `14.x` se conserva.
>
> Responde a cuatro preguntas por cada animación: **qué la dispara**, **sobre quién**, **cuánto dura** y **cómo termina**. Nada más.
>
> **Ver también:** los nodos en [NOVA_Nodos.md](./NOVA_Nodos.md) · el arte que consume cada animación en [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md) · scripts `NodeStateMachine.cs` y `NodeAnimator.cs` en [15.5](./NOVA_GameDesign_Spec.md#155-gameplay--componentes-de-nodo) y [15.8](./NOVA_GameDesign_Spec.md#158-presentation).

---

## 14.0 Reglas generales

1. **La presentación solo lee.** `NodeAnimator` se suscribe a eventos; nunca modifica Energía, facción ni estado ([15.1](./NOVA_GameDesign_Spec.md#151-principios), principio 3).
2. **Estado ≠ animación puntual.** Un *estado* (14.1) persiste y puede bloquear acciones; una *animación puntual* (14.5) es un efecto que se reproduce y termina, sin efecto de juego.
3. **Todo nodo anima igual, sea del bando que sea.** Jugador, Enemigo y Neutral usan las mismas reglas; solo cambia la paleta. En la práctica esto es literal: una variante de facción es la misma llamada de geometría con otra rampa.
4. **El idle se reparte.** El Asteroide y el Magnetar resuelven su idle por transform en Unity y no generan hoja —un **cabeceo de píxeles enteros**, no un squash: un transform no puede deformar un sprite sin romper la rejilla ([NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), *Animación*)—; Estrella y Púlsar llevan hoja de 16 frames porque tienen que mover algo interno. El Magnetar ha estado en los dos grupos por el mismo criterio: con «La Botella» sus lazos de campo eran su firma, y cerrado en «El Yunque» ([A.2.4](./NOVA_Assets_Diseno.md#a24-magnetar--44)) un cuerpo tallado con esquirlas capturadas no tiene nada interno que mover. El criterio es el motivo, no la lista.
5. **Si el estado no se ve, es un bug.** Cada estado de 14.1 debe ser distinguible de un vistazo sin leer números.

---

## 14.1 Estados del nodo

En código son dos piezas ([T.7](./NOVA_Tracking.md#t7-pendientes-abiertos) #43, cerrado el 2026-10-10). `NodeState` es el **estado de base**, excluyente, y se deduce del tipo y la facción: `Dormido`, `SinEncender` o `Normal`. `NodeStateFlags` son las **banderas** de los que se suman a él: `Asustado`, `Congelado`, `Infectado`, `Reconvirtiendo` y `Capturado`. Pueden coexistir según 14.6, y `NodeStateMachine` rechaza la única pareja prohibida, `Infectado` con `Reconvirtiendo`. En la tabla, «`Normal`» es lo que 14.1 entiende por Normal: base `Normal` y ninguna bandera.

| Estado | Se aplica a | Disparador exacto | Señal visual | Duración | Termina cuando |
|---|---|---|---|---|---|
| `Dormido` | Asteroide Neutral | Es Asteroide y `faction == Neutral` | `zzz` flotante, gris `#8A93A6`, balanceo lento. Tres glifos que **cabecean en su sitio** sin viajar ([A.3.1](./NOVA_Assets_Diseno.md#a31-art-st-sleep--dormido)) | Permanente | Es capturado |
| `SinEncender` | Asteroide del Jugador o del Enemigo | Es Asteroide y `faction != Neutral` | Una **brasa** en el sitio del `zzz`, en color de facción. **Dos lecturas** según `energy`: quieta por debajo de 10, abierta en cinco trazos que **crepitan** al llegar ([A.3.2](./NOVA_Assets_Diseno.md#a32-art-st-ign-y-art-st-ready--sinencender)) | Indefinida | Se enciende ([4.1](./NOVA_Nodos.md#41-asteroide-nivel-0)) |
| `Normal` | Cualquier nodo encendido | Ausencia de los demás estados | Idle propio del tipo | — | Entra otro estado |
| `Asustado` | **Cualquier** nodo: Jugador, Enemigo y Neutral | Ver 14.2 | Ojos muy abiertos ([A.3.3](./NOVA_Assets_Diseno.md#a33-art-st-scare--asustado)) más temblor y salto corto, que son **transform** (14.7, capa 5) | Hasta el impacto | El protón impacta, se destruye o deja de ser letal |
| `Congelado` | Cualquier nodo encendido | Impacto de Cero Absoluto ([7.3](./NOVA_GameDesign_Spec.md#73-cero-absoluto)) | **Una red de grietas** que cruza el nodo, cian `#B8F0FF` **por trama**, idle detenido ([A.3.4](./NOVA_Assets_Diseno.md#a34-art-st-frozen--congelado)) | Hasta el deshielo. **No hay máximo:** los 30 s del auto-deshielo son de *inactividad* y **cada impacto los reinicia** ([7.3](./NOVA_GameDesign_Spec.md#73-cero-absoluto)) | Su Energía llega a 0 por un impacto (deshielo, cambia de bando) o pasan 30 s **sin recibir ninguno** (auto-deshielo, conserva bando y Energía) |
| `Infectado` | Cualquier nodo encendido | Impacto de Decaimiento ([7.4](./NOVA_GameDesign_Spec.md#74-decaimiento)) | **Nube verde** `#7CF23F` **por trama**, con cizalla y sesgada hacia abajo, que respira en 16 frames, más gotas que caen ([A.3.5](./NOVA_Assets_Diseno.md#a35-art-st-decay--infectado)) | Indefinida | Recibe Energía de cualquier facción (cura) o es capturado |
| `Reconvirtiendo` | Estrella, Púlsar o Magnetar propio | Confirmación de reconversión ([8.1](./NOVA_GameDesign_Spec.md#81-reglas)) | **Un frente** en color de facción que barre el nodo, tapa el cambio de sprite y lo descubre siendo otro tipo ([A.3.6](./NOVA_Assets_Diseno.md#a36-art-st-conv--reconvirtiendo)) | 3.0 s exactos | Acaba el temporizador o es capturado |
| `Capturado` | Cualquier nodo | `ChangeFaction()` | **Dos anillos blancos** que se cruzan en el frame central: uno se cierra sobre el nodo y otro sale de él. Cambio de paleta en el frame 0 ([A.3.7](./NOVA_Assets_Diseno.md#a37-art-st-cap--capturado)) | 0.4 s | Acaba el temporizador → `Normal` |

**Nota sobre «cualquier nodo encendido».** El Asteroide **nunca** lo está: encenderlo lo convierte en Estrella, Púlsar o Magnetar ([4.1](./NOVA_Nodos.md#41-asteroide-nivel-0)). Así que `Congelado`, `Infectado` y el buff se aplican a **trece** sprites de nodo y no a catorce, y `Reconvirtiendo` a doce, porque además tiene que ser propio.

**Y hay exactamente un nodo Neutral que sí puede llevarlos: el Magnetar.** `Congelado` e `Infectado` solo los provoca un poder del Púlsar, y [7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido) admite como objetivo **cualquier nodo encendido que no sea propio**, lo que incluye al Magnetar neutral de [3.2](./NOVA_GameDesign_Spec.md#32-nota-sobre-el-magnetar-neutral) y excluye a todos los Asteroides. El conteo de trece no cambia —se cuenta por tipo y nivel, no por facción— pero **sí cambia contra qué hay que verificar los dos tintes**: `ART-ST-FROZEN` y `ART-ST-DECAY` se midieron sobre las rampas de Jugador y Enemigo, y ahora tienen que leerse también sobre la rampa gris del Magnetar neutral. La geometría es idéntica; lo que puede fallar es el contraste, porque el hielo `#B8F0FF` sobre un cuerpo gris `#8A93A6` tiene menos recorrido que sobre uno cian o magenta. Anotado en [NOVA_Tracking.md](./NOVA_Tracking.md), T.7 #29. Solo `Asustado` y `Capturado` van sobre los catorce. No cambia ninguna regla de juego —la condición escrita en la columna *Se aplica a* ya lo decía—, pero sí cambia contra qué se comprueba un overlay, y por eso está recogido en [A.3](./NOVA_Assets_Diseno.md#a3-estados-y-overlays--141) y en `tools/nova_estados.py`.

**Nota sobre «silueta neutra».** Esta columna decía «colapso y reencendido, silueta neutra», y **«neutra» aquí quiere decir sin tipo, no sin bando**. [8.1](./NOVA_GameDesign_Spec.md#81-reglas) es explícito: durante los 3 s el nodo sigue siendo de su dueño, tanto que puede ser capturado, y si lo capturan la reconversión se cancela. Un nodo pintado de gris `#8A93A6` durante tres segundos diría que no es de nadie justo en su momento más vulnerable. Lo que pierde es el tipo, y el tipo se ve en la geometría. Por eso `ART-ST-CONV` es el segundo overlay con variante de facción, después de `ART-ST-IGN` ([A.3.6](./NOVA_Assets_Diseno.md#a36-art-st-conv--reconvirtiendo)).

**Y el colapso no lo dibuja el overlay.** Un overlay solo suma píxeles: con alfa binario no puede borrar el sprite de debajo. Al terminar, el nodo es de otro tipo y de **nivel 1**, así que el animador **cambia de sprite** a mitad de la animación; el trabajo del asset es tapar ese corte, igual que `FX-CAP` tapa el de `ChangeFaction()`.

**Nota:** `Capturado` es un estado de transición de 0.4 s. No bloquea nada; el nodo ya es funcional para su nuevo dueño desde el frame 0. **Y el sprite también cambia en el frame 0**, no al final: el efecto no tapa el cambio de paleta, lo acompaña. Puede permitírselo porque la silueta es idéntica entre facciones en los catorce nodos —14.0 #3, comprobada byte a byte sobre los 34 sprites—, así que `ChangeFaction()` es un recoloreado en el sitio y no deja ningún corte de geometría que esconder. Es la diferencia con `Reconvirtiendo`, que sí lo deja ([A.3.7](./NOVA_Assets_Diseno.md#a37-art-st-cap--capturado)).

**Nota sobre las dos lecturas de `SinEncender`.** Es **un solo estado** de `NodeState`, con dos variantes visuales que el animador elige leyendo `energy >= 10`, la condición de encendido de [4.1](./NOVA_Nodos.md#41-asteroide-nivel-0). No hay estado nuevo, no cambia la matriz de [14.6](#146-matriz-de-compatibilidad) y `NodeStateMachine` no se entera: la presentación solo lee (14.0 #1). El motivo es que el icono parpadeando sobre un asteroide con 3 de Energía es una promesa que el jugador no puede cobrar. El **parpadeo** que antes pedía este estado entero queda reservado a la lectura accionable; la otra es estática, de un frame.

---

## 14.2 Animación de susto

Es la señal de lectura más importante del juego: indica de un vistazo qué está a punto de caer.

**Sobre quién:** todos los nodos, **incluidos los neutrales y los del Enemigo**. Sin excepciones.

**Condición exacta** (evaluada en cada tick de lógica, a 50 Hz en `FixedUpdate` ([15.10](./NOVA_GameDesign_Spec.md#1510-orden-de-ejecución)), por `ThreatCalculator`, el mismo que usa el algoritmo del Enemigo — ver [12.4](./NOVA_Algoritmo_Enemigo.md#124-detección-de-amenaza-y-defensa)):

```
EstaAsustado(nodo):
    para cada protón p en vuelo dirigido a nodo:
        si p.faction == nodo.faction: continuar        # un aliado no asusta
        multiplicador = (nodo.type == Magnetar) ? 0.5 : 1.0
        efectiva = floor(p.charge * multiplicador)
        tiempoLlegada = distancia(p.pos, nodo.pos) / VelocidadProton   # 2.5 u/s, BalanceConfig (16.1)
        si efectiva >= nodo.energy Y tiempoLlegada < UmbralSusto:      # 1.5 s, BalanceConfig (16.1)
            devolver true
    devolver false
```

| Pregunta | Respuesta |
|---|---|
| ¿Cuándo empieza? | En el frame en que la condición pasa a `true` |
| ¿Cuándo termina? | En el frame en que pasa a `false`: el protón impacta, un Magnetar lo derriba, o el nodo recibe refuerzo y deja de ser letal |
| ¿Se acumula? | No. Varios protones letales producen un único susto |
| ¿Tiene efecto de juego? | Ninguno. Es puramente informativo |
| ¿La ve el Enemigo? | No como animación. Su algoritmo consulta `ThreatCalculator` directamente ([12.6](./NOVA_Algoritmo_Enemigo.md#126-prohibiciones)) |

Un nodo puede pasar de `Asustado` a `Normal` sin ser capturado. Es deliberado: enseña al jugador que el refuerzo funcionó.

**En código** (`T2-08`): la condición es `ThreatCalculator.IsScared` y la bandera la pone `NodeScare`, que corre a 0 en el tick fijo. Por eso el susto empieza y termina **un tick (20 ms) después** del hecho que lo provoca ([T.5](./NOVA_Tracking.md#t5-registro), desviación 177). Varios protones que solo **juntos** capturarían no asustan: 14.2 pregunta protón a protón, mientras que la amenaza del algoritmo ([12.4](./NOVA_Algoritmo_Enemigo.md#124-detección-de-amenaza-y-defensa)) sí suma (desviación 179).

**La sacudida va en píxeles enteros**, como todo movimiento por transform de un nodo ([NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), *Animación*): un temblor de 1.5 px produce el mismo hervor que el squash que esa regla prohíbe.

**El susto se dibuja en dos sitios, y conviene no duplicarlos.** Los **ojos** son un asset, `ART-ST-SCARE` ([A.3.3](./NOVA_Assets_Diseno.md#a33-art-st-scare--asustado)); el **temblor y el salto** son la capa 5 de [14.7](#147-prioridad-visual), sacudida de transform, y los hace Unity moviendo el nodo entero. Hornear la sacudida en la hoja sería pagarla dos veces y dejaría el hitbox temblando.

Y el asset **no es un par de ojos, es un ojo**: se instancia dos veces con el desplazamiento entero que cada tipo y nivel declara. Los cuatro nodos colocan sus ojos donde les conviene —la separación va de 9.5 a 16 px— y un overlay de posición fija no aterrizaría en ninguno. La tabla de anclaje está en A.3.3.

---

## 14.3 Protón

| Elemento | Regla |
|---|---|
| Sprite | Circular con estela, color de la facción emisora ([6.1](./NOVA_GameDesign_Spec.md#61-el-protón)) |
| Número de Carga | Siempre visible en el centro, actualizado en tiempo real |
| Escala | **Tres sprites discretos**, no escala continua. La fórmula anterior `0.6 + (carga/100)*0.6` queda anulada: con `Filter Mode: Point` una escala no entera rompe el pixel-perfect. Umbrales en [A.4.2](./NOVA_Assets_Diseno.md#a42-protón-y-haces-de-poder--143). |
| Al ser alcanzado por un Magnetar | Le llega el **arco de plasma** del Magnetar (14.9); destello, sacudida, el número baja de 2 en 2 |
| Al ser destruido | Implosión corta, no deja nada |
| Al impactar | Ver 14.5: absorción, daño o captura |

El protón **no cambia de color** aunque su nodo origen cambie de bando durante el viaje ([17](./NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución), caso 3).

---

## 14.4 Paleta funcional

| Uso | Color |
|---|---|
| Jugador | `#3FD7F5` |
| Enemigo | `#F2456B` |
| Neutral | `#8A93A6` |
| Congelado · haz de Cero Absoluto | `#B8F0FF` |
| Infectado · haz de Decaimiento | `#7CF23F` |
| Haz de Agujero de Gusano | `#9B5CF6` |
| Línea de envío | La del Jugador, `#3FD7F5` |
| Arco del Magnetar | La del bando del Magnetar: Jugador, Enemigo o Neutral |
| Power-up Rayo Cósmico | `#FFD23F` |
| Power-up Nube de Hidrógeno | `#FF8FD0` |
| Fondo profundo | `#0B0E1A` |

> **El haz de poder se colorea por poder, no por facción** ([7.2.2](./NOVA_GameDesign_Spec.md#722-el-haz)). Reutiliza el color del estado que provoca —hielo para Cero Absoluto, verde para Decaimiento— y el morado `gusano`, que el contrato reserva en exclusiva al Agujero de Gusano precisamente para que ese poder no se confunda con nada. Quién lanzó el haz se lee por el extremo del que sale, que es un Púlsar con color de facción; qué va a pasar, por el color del trazo. Con alcance global esa lectura anticipada importa más que antes: el haz puede nacer al otro lado del tablero, lejos de donde el jugador está mirando.

> **El plasma va en color de facción, no en el violeta de un globo real.** El morado puro es de `gusano` y [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md) (*Paleta*) lo reserva al Agujero de Gusano para que no se confunda con nada. Una línea de envío violeta anunciaría un poder que no va a caer. El color de facción además dice lo que importa: de quién es la línea o el arco.

> La paleta completa —22 rampas de 7 tonos— vive en `tools/nova_paleta.py`. Las reglas que la gobiernan están en [NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), sección Paleta. Cada color de esta tabla aparece **literal** en el índice 3 de su rampa: aquí se decide el significado, allí el volumen.

---

## 14.5 Animaciones puntuales (sin estado)

Efectos que se reproducen y terminan. No bloquean nada.

**Cadencia:** 12 fps en todo el proyecto. El número de frames de cada efecto sale de su duración (`ceil(duración × 12)`) y está calculado en [A.5](./NOVA_Assets_Diseno.md#a5-vfx-puntuales--145). Las duraciones de esta tabla son la fuente; los conteos de frames, la consecuencia.

| ID | Evento | Se anima sobre | Disparador | Duración |
|---|---|---|---|---|
| `FX-IGN` | Encendido | El Asteroide propio | `NodeIgnition` consume 10 de Energía | 0.6 s |
| `FX-UPG` | Mejora de nivel | El nodo mejorado | `NodeLevelComponent` sube de nivel | 0.5 s |
| `FX-CAP` | Captura | El nodo capturado | `ChangeFaction()` | 0.4 s (= estado `Capturado`) |
| `FX-SEND` | Lanzamiento | El nodo origen | `ProtonLauncher` descuenta Energía | 0.2 s |
| `FX-ABS` | Absorción aliada | El nodo destino | `ImpactResult.Absorbed` | 0.3 s |
| `FX-DMG` | Daño sin captura | El nodo destino | `ImpactResult.Damaged` | 0.3 s |
| `FX-SHOT` | Disparo del Magnetar | **Arco de plasma** del Magnetar al protón (14.9), más destello sobre el protón | Cada tick de cadencia | Efecto de juego instantáneo; se ve **2 frames (0.17 s)** |
| `FX-KILL` | Protón destruido | El protón | `charge <= 0` | 0.25 s |
| `FX-THAW` | Deshielo | El nodo congelado | Energía llega a 0 o auto-deshielo 30 s | 0.5 s |
| `FX-CURE` | Cura de Decaimiento | El nodo infectado | Recibe Energía de cualquier facción | 0.4 s |
| `FX-CAST` | Lanzamiento de poder | El Púlsar | `PulsarCaster` dispara | 0.4 s |
| `FX-BEAM` | **Haz del poder** | Del Púlsar al nodo objetivo | Mismo frame que `FX-CAST` | 0.25 s, constante |
| `FX-PWR-ZER` | Impacto Cero Absoluto | El nodo objetivo | **Termina el haz** | 0.7 s → estado `Congelado` |
| `FX-PWR-DEC` | Impacto Decaimiento | El nodo objetivo | **Termina el haz** | 0.7 s → estado `Infectado` |
| `FX-PWR-WRM` | Impacto Agujero de Gusano | El nodo objetivo | **Termina el haz** | 1.0 s → `Capturado` |
| `FX-DROP` | Aparición de power-up | Un Asteroide neutral al azar | `PowerUpSpawner` | Halo + icono, hasta recogida o 30 s |
| `FX-BUFF` | Power-up activo | Los nodos de la facción **a los que el power-up afecta** ([A.3.8](./NOVA_Assets_Diseno.md#a38-art-st-buff--power-up-activo)) | Recogida del power-up | 20 s |
| `FX-CASCADE` | Cascada de conquista | Los nodos neutrales restantes, Asteroides y Magnetares | Victoria ([10.1](./NOVA_GameDesign_Spec.md#101-victoria-del-jugador)) | 3 s, de cercano a lejano |
| `FX-VANISH` | Protones huérfanos | Protones del bando derrotado | Su bando pierde el último nodo | 0.4 s |
| `FX-SHAKE` | Sacudida de cámara | La cámara | Captura de un nodo con Energía ≥ 50 | 0.3 s |

**`FX-BEAM` se solapa con `FX-CAST`, no va detrás.** Los dos arrancan en el mismo frame: el fogonazo dura 0.4 s en el Púlsar y el haz 0.25 s entre los dos nodos, así que el efecto cae mientras el Púlsar todavía está retrocediendo. Encadenarlos sumaría 0.65 s entre el clic y la consecuencia, y con alcance global un poder tiene que sentirse inmediato ([18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #15).

**Y su asset no vive en [A.5](./NOVA_Assets_Diseno.md#a5-vfx-puntuales--145).** Un VFX puntual es una hoja de frames que se reproduce **sobre un nodo**, dentro de su celda de 96. El haz no cabe en esa definición: une dos puntos arbitrarios del tablero y puede medir 1 u o 17 u, así que se dibuja estirando un asset de mundo, como la línea de envío. Sus tres archivos están en [A.4.2](./NOVA_Assets_Diseno.md#a42-protón-y-haces-de-poder--143), no en A.5. **Al arco de `FX-SHOT` le pasa lo mismo**: es un trazo estirable entre el Magnetar y el protón y vive en [A.4.1](./NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar). Lo único que queda en A.5 es su destello sobre el protón, `ART-FX-SHOT`. Es la misma excepción que ya tenían `FX-CAP`, `FX-DROP` y `FX-BUFF`, que salen de A.3 y A.4.4.

**Si el haz se disipa, no hay `FX-PWR-*`.** Cuando el objetivo deja de ser válido durante esos 0.25 s ([7.2.2](./NOVA_GameDesign_Spec.md#722-el-haz)), `FX-BEAM` se reproduce entero y **no le sigue ningún impacto**: el haz llega, no pasa nada y la Energía se perdió. Esa ausencia es el feedback, y es deliberada.

**El blanco puro es de `FX-CAP`**, y es el único efecto de esta tabla que lo usa. [8.1](./NOVA_GameDesign_Spec.md#81-reglas) permite que capturen un nodo **mientras se reconvierte**, así que los dos eventos pueden coincidir sobre el mismo sprite: `ART-ST-CONV` destella en color de facción precisamente para dejarle el blanco a este ([A.3.6](./NOVA_Assets_Diseno.md#a36-art-st-conv--reconvirtiendo), [A.3.7](./NOVA_Assets_Diseno.md#a37-art-st-cap--capturado)).

**`FX-BUFF` no va sobre todos los nodos de la facción, aunque el efecto sí.** [9.2](./NOVA_GameDesign_Spec.md#92-tipos) dice a qué afecta cada power-up, y el Asteroide no gana nada con ninguno de los dos: la regla de juego no cambia, cambia sobre qué sprites se dibuja la marca. De ahí sale además que **ningún nodo lleve nunca los dos buffs a la vez**. El reparto exacto y la medida están en [A.3.8](./NOVA_Assets_Diseno.md#a38-art-st-buff--power-up-activo).

**Los quince efectos que quedan por diseñar se diferencian de `FX-CAP`, no al revés.** Cuatro de ellos son destellos cortos sobre un nodo —`FX-DMG`, `FX-ABS`, `FX-KILL`, `FX-UPG`— y la captura es el suceso más importante de los cinco. Por eso `ART-ST-CAP` se cerró en **dos anillos que se cruzan** y no en el flash con onda que esta tabla daba por hecho: un fogonazo habría ocupado el gesto que los otros necesitan. La forma de cada uno se decide en [A.5](./NOVA_Assets_Diseno.md#a5-vfx-puntuales--145).

---

## 14.6 Matriz de compatibilidad

| | `Asustado` | `Congelado` | `Infectado` | `Reconvirtiendo` |
|---|---|---|---|---|
| **`Asustado`** | — | Sí | **Sí** | Sí |
| **`Congelado`** | Sí | — | Sí | **Sí** (la reconversión se pausa) |
| **`Infectado`** | **Sí** | Sí | — | No |
| **`Reconvirtiendo`** | Sí | **Sí** | No | — |

- `Congelado` + `Infectado`: posible. El nodo pierde Energía por Decaimiento mientras está congelado, lo que **acelera su propio deshielo**. Es intencional.
- `Congelado` + `Reconvirtiendo`: posible. Cero Absoluto **sí** alcanza a un nodo que se reconvierte —[7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido) solo excluye Asteroides y nodos propios— y el temporizador de 3 s **se pausa**, igual que la carga de un Púlsar ([17](./NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución), caso 8). Lo que sigue prohibido es **iniciar** una reconversión sobre un nodo congelado o infectado ([8.1](./NOVA_GameDesign_Spec.md#81-reglas)): iniciar y continuar son cosas distintas.
- `Infectado` + `Reconvirtiendo`: imposible, también cuando el Decaimiento llega con la reconversión ya en marcha. Ahí **cede la reconversión**: se cancela como en una captura, el coste se pierde y el nodo queda infectado con su tipo de antes ([7.4](./NOVA_GameDesign_Spec.md#74-decaimiento), cerrado el 2026-10-11).
- `Asustado` es compatible con todo: siempre debe poder avisar.

---

## 14.7 Prioridad visual

Cuando varios estados coexisten, este es el orden de capas de abajo a arriba:

```
0. Trazos de mundo           línea de envío, arco del Magnetar (14.9)
1. Sprite base del nodo (tipo + nivel + facción)
2. Tinte de estado           Congelado > Reconvirtiendo > Normal
3. Aura de estado            Infectado
4. Glifos y overlays         zzz (Dormido), brasa (SinEncender), FX-BUFF
5. Sacudida de transform     Asustado
6. Números y HUD             Energía, barra de carga del Púlsar
7. Efectos puntuales         14.5
```

El número de Energía **nunca se oculta**, en ningún estado. Es la información que el jugador lee más veces por partida.

**La capa 0 va por debajo de los nodos y por encima del fondo y de los anillos de radio.** La línea de envío y el arco del Magnetar salen del **centro** de un nodo, así que dibujados encima le cruzarían la cara. Por debajo, el sprite tapa el tramo inicial y el filamento parece nacer del borde del cuerpo, como sale de la esfera en un globo de plasma. El protón va por encima del arco que le llega: la punta del arco se ve **tocándolo**, no tachándolo.

**Los glifos de estado de la capa 4 no se dibujan sobre el cuerpo, sino fuera de él.** El `zzz` de `Dormido`, la brasa de `SinEncender` y el galón de `FX-BUFF` son símbolos, no tintes: van en el hueco que deja el nodo dentro de su celda y **no pisan un solo píxel del sprite** ([A.3.1](./NOVA_Assets_Diseno.md#a31-art-st-sleep--dormido), [A.3.2](./NOVA_Assets_Diseno.md#a32-art-st-ign-y-art-st-ready--sinencender)). Por eso no necesitan trama —la capa 4 no tiene que dejar ver nada de debajo— y por eso la capa 6 los tapa sin conflicto: no comparten sitio. **Los dos ocupan exactamente el mismo hueco**, así que al capturar una roca neutral el símbolo se releva sin saltar de posición.

Por la misma razón, **los ojos se dibujan los últimos** dentro del sprite del nodo, por encima de cualquier elemento de la capa 1. La cara es la que comunica el estado `Asustado` (14.2) y ningún adorno puede taparla, aunque geométricamente le tocara.

**`ART-ST-SCARE` entra justo ahí, y no en la capa 4.** No es un glifo al lado del cuerpo: **sustituye a los ojos** en el mismo punto del orden en que se dibujan los normales, y los tapa por completo —cero píxeles del ojo viejo asomando, verificado en los 14 nodos ([A.3.3](./NOVA_Assets_Diseno.md#a33-art-st-scare--asustado))—. Dos ojos concéntricos de distinto tamaño se leerían como cuatro. Lo que sí va en la capa 5 es la **sacudida**, que mueve el nodo entero con el overlay ya puesto encima.

**Un tinte de la capa 2 va por encima de los ojos, y por eso el tinte tiene que apartarse.** La capa 2 se dibuja sobre el sprite entero, ojos incluidos, y 14.6 hace `Congelado` compatible con `Asustado`: si el hielo tapa la cara, un nodo congelado y asustado no puede avisar y esa casilla de la matriz es mentira. **No se arregla cambiando este orden** —subir los ojos por encima del tinte dejaría dos ojos flotando sobre un bloque de hielo—: se arregla en la geometría del tinte, dejándole hueco a la cara. `ART-ST-FROZEN` lo hace **cruzando la cara por el pasillo de 4 px que hay entre los dos ojos** en los trece nodos encendidos, y `ART-ST-DECAY` no entrando en la caja de la cara en ningún frame ([A.3.4](./NOVA_Assets_Diseno.md#a34-art-st-frozen--congelado), [A.3.5](./NOVA_Assets_Diseno.md#a35-art-st-decay--infectado)). Los tres tintes que faltan tienen la misma obligación.

**Y las capas 2 y 3 coexisten de verdad, no en teoría.** [14.6](#146-matriz-de-compatibilidad) marca `Congelado` + `Infectado` como «Sí», así que hay partidas en las que el jugador ve el hielo y el aura sobre el mismo nodo. Los dos son trama, y dos campos de Bayer apilados pueden acabar en una sola mancha en la que no se lee ninguno de los dos. **Un tinte nuevo se comprueba encima del anterior, no solo encima del nodo**, y se comprueba mirando: el número de solape dice menos de lo que parece —el aura que menos píxeles le quita al hielo puede ser la que peor se distingue de él— ([A.3.5](./NOVA_Assets_Diseno.md#a35-art-st-decay--infectado)).

**Esta capa 4 tenía razón y A.3 no.** [A.3](./NOVA_Assets_Diseno.md#a3-estados-y-overlays--141) clasificaba el buff entre los overlays que cubren el cuerpo y por tanto se traman; esta sección lo tenía desde el principio entre los glifos. Gana esta: `ART-ST-BUFF` es una marca en la esquina libre de la celda —**23 px medidos en los catorce**—, va sólida y con contorno, y no tiene nada que dejar ver debajo porque debajo no hay nada ([A.3.8](./NOVA_Assets_Diseno.md#a38-art-st-buff--power-up-activo)).

**Los tintes y auras de las capas 2 y 3 se resuelven con dithering Bayer**, no bajando la opacidad: el alfa del proyecto es binario ([NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), regla 7).

---

## 14.8 Dependencias de arte

Ninguna animación de este documento puede implementarse antes que su asset. Los IDs `FX-*` de 14.5 y los estados de 14.1 están mapeados uno a uno en [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md); el estado de cada uno se consulta allí antes de abrir una tarea de la fase 6 en [NOVA_Tracking.md](./NOVA_Tracking.md).

---

## 14.9 Filamento de plasma

Añadido el **2026-10-10**. Dos elementos del tablero comparten el mismo lenguaje visual, el de la energía dentro de un **globo de plasma**: un filamento vivo que sale de un electrodo y busca un punto de contacto. No es un estado ni un `FX-*` sobre un nodo. Es un trazo que une dos puntos del tablero.

| | Línea de envío | Arco del Magnetar |
|---|---|---|
| **Qué la dispara** | Arrastre derecho desde un nodo propio que puede enviar, o selección activa ([13.1](./NOVA_GameDesign_Spec.md#131-ratón)) | Cada disparo de la torreta ([6.3](./NOVA_GameDesign_Spec.md#63-efecto-del-magnetar-sobre-protones), `FX-SHOT`) |
| **Une** | Centro del nodo origen → **cursor** | Centro del Magnetar → **protón** al que dispara |
| **Sobre quién** | Solo el Jugador. El Enemigo no tiene cursor | Magnetares de **los tres bandos**, incluido el neutral |
| **Color** | Rampa del Jugador | Rampa del bando del Magnetar |
| **Duración** | Lo que dure el gesto. **Bucle de 16 frames**, porque no tiene duración fija ([NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), *Animación*) | **2 frames (0.17 s)**, sin bucle: aparece, se retuerce y se apaga |
| **Cómo termina** | Al soltar el botón, al cancelar la selección o al entrar en modo apuntado. Corte seco, sin fundido | Al acabar sus 2 frames |
| **Efecto de juego** | Ninguno | Ninguno: el −2 de Carga ya se aplicó en el tick |

### Cómo se ve

- **Un filamento principal que serpentea** alrededor de la recta entre los dos extremos. Nunca es una recta: una línea quieta se lee como interfaz, y la retorcida se lee como energía. Está **anclado en los dos extremos**: nace fino en el origen, se retuerce en el tramo y se cierra en un **punto de contacto** en la punta, donde está la luz más intensa, como el dedo sobre el cristal.
- **Ramitas** cortas que brotan del filamento y se apagan en uno o dos frames, sin repetir sitio en frames seguidos. Le dan el chisporroteo del globo sin engordar el trazo.
- **Fluye hacia la punta.** La fase de la ondulación avanza del origen al destino, así que la línea dice también **en qué sentido** va a ir la Energía, y el arco, hacia dónde va el disparo.
- **Núcleo de 1 px** en la mitad clara de la rampa, envuelto en un resplandor **por trama Bayer**. Sin alfa parcial y sin bloom: [1.2](./NOVA_GameDesign_Spec.md#12-alcance-cerrado) prohíbe el post-procesado, y el brillo sale del dibujo, no de un efecto de cámara.
- **Sin contorno**, por la misma razón que `veta_luz`: en un trazo de 1 px el contorno no separa, tacha.
- **El ciclo cierra por construcción.** La ondulación es una suma de armónicos con la fase en `t`, de 0 a 2π repartido entre los 16 frames, así que el bucle no salta al reiniciarse ([NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), *Animación*).

### Reglas de la línea de envío

- **Sigue al cursor en cada frame**, en `Update`, porque es presentación (15.10). La longitud cambia continuamente y por eso el asset se compone por piezas —su tramo se repite en mosaico, no se estira— y no se dibuja por longitud ([A.4.6](./NOVA_Assets_Diseno.md#a46-filamento-de-plasma--149)).
- **Varias líneas a la vez** cuando hay selección múltiple, una por nodo seleccionado. Cada una arranca el bucle en un frame distinto, desfasado por su orden en la selección: con un lazo de cuatro nodos, cuatro filamentos ondulando al unísono parecen uno solo copiado cuatro veces.
- **Se ve aunque no se envíe nada**: el gesto se enseña siempre, y si al soltar no hay envío (vacío, el propio origen o Energía insuficiente según [5.2](./NOVA_GameDesign_Spec.md#52-envío)) la línea simplemente desaparece.

### Reglas del arco del Magnetar

- **La punta sigue al protón** durante sus 2 frames. A 2.5 u/s un protón recorre 0.42 u en 0.17 s, y un arco clavado en el punto del disparo se quedaría hablando con el vacío.
- **Si el disparo derriba el protón**, la punta se queda en su última posición y el arco completa sus 2 frames encima del `FX-KILL`. Es lo que dice **quién** lo derribó.
- **Un Magnetar nunca muestra dos arcos a la vez.** A cadencia N5 (4.27 disp/s, 0.23 s entre disparos) no llegan a solaparse. Con el Rayo Cósmico (+50 %, [9.2](./NOVA_GameDesign_Spec.md#92-tipos)) son 6.4 disp/s y 0.16 s, menos que los 0.17 s del arco: en ese caso **el arco nuevo corta al anterior**. Dos Magnetares que disparan al mismo protón sí muestran dos arcos, porque son dos disparos.
- **El Magnetar congelado no muestra arco**, porque no dispara ([7.3](./NOVA_GameDesign_Spec.md#73-cero-absoluto)).
