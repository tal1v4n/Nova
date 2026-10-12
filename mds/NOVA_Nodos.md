# NOVA — Personajes (nodos)

> Parte de la especificación de **NOVA**. Documento maestro: [NOVA_GameDesign_Spec.md](./NOVA_GameDesign_Spec.md).
> Corresponde a la **sección 4** del maestro; la numeración `4.x` se conserva.
>
> **Ver también:** estados y animaciones de cada nodo en [NOVA_Estados_Animaciones.md](./NOVA_Estados_Animaciones.md) · arte requerido por nodo y nivel en [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md) · reglas de envío e impacto en las secciones [5](./NOVA_GameDesign_Spec.md#5-economía-la-energía) y [6](./NOVA_GameDesign_Spec.md#6-protones-y-resolución-de-impacto) del maestro · poderes del Púlsar en la [sección 7](./NOVA_GameDesign_Spec.md#7-poderes-del-púlsar) · reconversión en la [sección 8](./NOVA_GameDesign_Spec.md#8-reconversión).

---

## 4. Personajes (nodos)

### 4.0 Tabla comparativa

| | Asteroide | Estrella | Púlsar | Magnetar |
|---|---|---|---|---|
| Nivel | 0 | 1–5 | 1–3 | 1–5 |
| Tope de Energía | 10 (Materia Inerte) | 50 | 100 | 100 |
| Genera Energía | No | **Sí, único** | No | No |
| Envía Energía | No | Sí | Sí | Sí |
| Dispara | No | No | No | Sí |
| Lanza poderes | No | No | **Sí, alcance global** | No |
| Resistencia a captura | ×1 | ×1 | ×1 | **×2** |
| Cuenta en barra de poder | No | Sí | Sí | Sí |

### 4.1 Asteroide (nivel 0)

Roca muerta. Es el estado inicial de todo nodo conquistable y la única fuente de nodos nuevos.

- **Tope:** 10 de Materia Inerte. Un Asteroide neutral siempre está a 10; no regenera porque nunca baja salvo bajo ataque.
- **Captura:** se le restan 10 puntos de Carga. Al llegar a 0 pasa al bando del atacante, sigue siendo nivel 0, y su Energía queda en 0 con tope de 10.
- **Encendido:** un Asteroide propio con **10 de Energía** puede encenderse. El jugador elige Estrella, Púlsar o Magnetar en un menú radial. Consume los 10 y el nodo pasa a **nivel 1 del tipo elegido con 0 de Energía**.
- **No hace nada más.** No genera, no dispara, no puede enviar energía. Un Asteroide propio sin encender es capital muerto.
- **Animación:** `zzz` flotante mientras es Neutral. Un Asteroide propio sin encender muestra un icono parpadeante de "encender" en lugar de `zzz`. Detalle en [14.1](./NOVA_Estados_Animaciones.md#141-estados-del-nodo).

> **Diseño:** el doble coste (10 para capturar + 10 para encender) es intencional. Obliga a que expandirse tenga un precio real y hace que la primera Estrella sea la decisión más importante de la partida.

### 4.2 Estrella

Único generador de Energía del juego. Sin Estrellas, un bando está condenado.

- **Tope:** 50.
- **Genera** Energía de forma continua hasta su tope. Al llegar al tope se detiene (no desperdicia, simplemente no acumula).
- **Envía** Energía a cualquier nodo.
- **No dispara ni lanza poderes.**
- 5 niveles. Cada nivel acelera la generación.

| Nivel | Coste de mejora | Intervalo | Energía/s | Tiempo 0→50 |
|---|---|---|---|---|
| 1 | 10 (encendido) | 2.00 s | 0.50 | 100 s |
| 2 | 20 | 1.60 s | 0.63 | 80 s |
| 3 | 30 | 1.30 s | 0.77 | 65 s |
| 4 | 40 | 1.05 s | 0.95 | 53 s |
| 5 | 50 | 0.85 s | 1.18 | 43 s |

La generación es discreta: suma **+1 de Energía** cada `Intervalo`. Nunca fracciones. Esto evita desincronía entre el número visible y el valor real.

### 4.3 Púlsar

Lanzador de poderes. No genera Energía, pero la almacena y la usa como munición táctica.

- **Tope:** 100.
- **Envía** Energía a cualquier nodo, igual que la Estrella.
- **Carga y lanza** un poder a la vez (ver [sección 7](./NOVA_GameDesign_Spec.md#7-poderes-del-púlsar)).
- **Alcance global.** Un poder cargado puede lanzarse sobre **cualquier nodo encendido del mapa que no sea propio**, sin límite de distancia. El poder no viaja: se entrega por un **haz de 0.25 s** que no puede interceptarse ([7.2.2](./NOVA_GameDesign_Spec.md#722-el-haz)).
- **No apunta a todo.** El objetivo válido es **cualquier nodo encendido que no sea propio** ([7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido)): los del Enemigo y el Magnetar neutral, que es el único nodo Neutral con nivel. **Ningún Asteroide** es objetivo, sea de quien sea; contra un Asteroide solo sirven los protones.
- 3 niveles. Cada nivel desbloquea un poder. El nivel no altera la velocidad de carga, y el alcance no depende del nivel porque no hay alcance.

| Nivel | Coste de mejora | Poder desbloqueado |
|---|---|---|
| 1 | 10 (encendido) | Cero Absoluto |
| 2 | 25 | Decaimiento |
| 3 | 40 | Agujero de Gusano |

Un Púlsar de nivel 3 conserva el acceso a los tres poderes, pero solo puede tener **uno cargado a la vez**.

> **Diseño:** el Púlsar es el único nodo al que la geometría del tablero no afecta. No genera y no dispara, así que moverlo —reconvertirlo en otro sitio— es caro y lento; a cambio, desde donde esté alcanza todo. Es artillería de posición: se protege, no se acerca. Lo que sí lo castiga es que un Agujero de Gusano enemigo se lo lleve con el poder ya cargado dentro ([7.5](./NOVA_GameDesign_Spec.md#75-agujero-de-gusano)).

### 4.4 Magnetar

Defensa de área. Derriba protones en tránsito.

- **Tope:** 100.
- **Envía** Energía a cualquier nodo (decisión del jugador quedarse vulnerable al hacerlo).
- **Dispara** a los protones que entran en su radio. Cada disparo resta **2 puntos de Carga** al protón. Si la Carga llega a 0, el protón desaparece y esa Energía se pierde del juego: no vuelve al emisor ni llega al destino.
- **Resistencia ×2:** cada punto de Carga que impacta sobre un Magnetar le resta solo **0.5** de Energía. Capturar un Magnetar con 50 de Energía requiere 100 de Carga efectiva.
- 5 niveles. Cada nivel mejora radio y cadencia.

| Nivel | Coste de mejora | Radio (u) | Cadencia (disp/s) | Carga media anulada* |
|---|---|---|---|---|
| 1 | 10 (encendido) | 1.5 | 1.33 | ~3 |
| 2 | 20 | 1.75 | 1.87 | ~5 |
| 3 | 30 | 2.0 | 2.53 | ~8 |
| 4 | 40 | 2.25 | 3.33 | ~12 |
| 5 | 50 | 2.5 | 4.27 | ~17 |

\* Estimación para un protón que cruza el diámetro completo a velocidad 2.5 u/s. Es orientativa, no un valor implementado.

> **Radios ×0.75 y cadencia ×4/3 desde el 2026-10-02** ([T.7](./NOVA_Tracking.md#t7-pendientes-abiertos) #35). Los radios eran `2.0 · 2.4 · 2.8 · 3.2 · 3.6 u`, pensados para un tablero de 32 u: en el de 15 u ([T.8](./NOVA_Tracking.md#t8-escala-del-tablero--cerrada)) un N5 cubría el 48 % del ancho de diámetro, y ahora el 33 %. Los cinco caen en píxel exacto a PPU 64 (96, 112, 128, 144 y 160 px). Como el protón no cambió de velocidad, el radio menor por sí solo restaba un 30 % de Carga anulada; la cadencia sube ×4/3 (era `1.0 · 1.4 · 1.9 · 2.5 · 3.2`) para que la defensa valga lo mismo. Los valores viven en `Magnetar.asset` y se afinan jugando en `T2-04`.

**Selección de objetivo:** el Magnetar dispara siempre al protón hostil **más cercano a su propio centro**. Si hay empate, al que lleve más Carga, y si también empatan, al que se lanzó antes ([18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #31). Un protón justo en el borde del radio está dentro. Un solo disparo afecta a un solo protón (sin daño en área).

**Cadencia:** sin objetivo, el arma espera cargada y dispara en cuanto entra uno; no acumula disparos en reposo. Disparando sin pausa, conserva la cadencia exacta de la tabla.

**Cómo se ve el disparo:** como un **arco de plasma**, igual que el filamento que salta de la esfera de un globo de plasma hacia un dedo. Sale del Magnetar, se engancha al protón y lo sigue durante sus 2 frames, en el color del bando del Magnetar (gris si es neutral). Es solo presentación: el −2 de Carga ya se aplicó en el tick del disparo. Detalle en [14.9](./NOVA_Estados_Animaciones.md#149-filamento-de-plasma) y asset en [A.4.1](./NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar).

**A quién dispara, según su bando:**

| Magnetar es de... | Dispara a protones de... |
|---|---|
| **Neutral** | Jugador **y** Enemigo |
| **Jugador** | Enemigo |
| **Enemigo** | Jugador |

Un Magnetar nunca dispara a protones de su propia facción, ni siquiera si el protón va dirigido a un enemigo.

**El Magnetar neutral es el único nodo Neutral al que se le puede lanzar un poder.** Cumple la regla de [7.2.1](./NOVA_GameDesign_Spec.md#721-objetivo-válido) —está encendido y no es de nadie—, así que un Agujero de Gusano se lo lleva entero por 60 de Energía en vez de los 200 de Carga que cuesta por la vía normal, un Cero Absoluto lo calla y un Decaimiento lo vacía. Los dos bandos tienen esa llave. Es lo que impide que un muro neutral bien colocado decida la partida por sí solo, y la razón de que su nivel importe tanto en el diseño de los niveles 5 a 9.

---


---

## 4.5 Resumen de dependencias de arte

Cada tipo y nivel necesita su propio sprite. El inventario completo, con IDs y nombres de archivo, está en [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md).

| Tipo | Variantes de sprite (nivel × facción) | ID de asset |
|---|---|---|
| Asteroide | 1 nivel × 3 facciones = 3 | `ART-NOD-AST-*` |
| Estrella | 5 × 2 = 10 | `ART-NOD-STR-*` |
| Púlsar | 3 × 2 = 6 | `ART-NOD-PUL-*` |
| Magnetar | 5 × 3 = 15 (incluye Neutral, ver 3.2) | `ART-NOD-MAG-*` |

**Total: 34 sprites base de nodo**, sin contar frames de animación.
