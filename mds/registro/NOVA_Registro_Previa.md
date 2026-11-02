# NOVA — Registro · Antes de la fase 0 — especificación y arte

> Parte del registro de desarrollo de **NOVA** ([T.5](../NOVA_Tracking.md#t5-registro)). Es historial: **no hace falta leerlo para decidir la siguiente tarea**. Se conserva entero para la memoria del proyecto.

## Tareas y cambios

Una línea por tarea completada. La más reciente arriba.

| Fecha | Tarea | Resultado | Notas / desviaciones de la spec |
|---|---|---|---|
| 2026-09-26 | Cierre de las cuatro contradicciones menores | **Conteos:** `T4-02` decía 8 tests y 12.7 tiene **9**; `T5-07` decía 26 casos y la sección 17 tiene **35**; 14.5 y T.7 #24 decían «catorce» VFX y A.5 tiene **15**. **Congelado:** 14.1 daba «máx. 30 s», pero los 30 s son de *inactividad* y **cada impacto los reinicia**, así que no hay tope; queda escrito además que el Decaimiento ni descongela ni reinicia el contador, solo deja el nodo a un protón de caer. **Caso 8:** un Cero Absoluto **sí** puede congelar un nodo reconvirtiéndose y los 3 s **se pausan**, como ya le pasa a la carga de un Púlsar; §7.2.1 conserva sus dos únicas exclusiones. **Singletons:** el principio 6 de 15.1 decía «los tres registros» y uno de los tres, `GameManager`, no es un registro | 14.6 pasa a «Sí» en `Congelado` × `Reconvirtiendo`. El caso 8 se reescribe sin renumerar. Desviaciones 103–106 abajo |
| 2026-09-26 | Arte: **el idle por transform no podía deformar nada** | El contrato pedía squash and stretch (`s = 1 + 0.07·sin`) resuelto **por transform** en Asteroide y Magnetar. Con `Filter Mode: Point` eso remuestrea un sprite de 67 px entre 62.5 y 71.9 px en cada frame: no respira, hierve. Afectaba a **18 de los 34 sprites de nodo**. El reparto del idle **no cambia** —siguen sin hoja— pero su idle pasa a ser un **cabeceo de píxeles enteros**: 1 px el Asteroide, 2 px el Magnetar. La fórmula `s` se conserva **para los generadores**, que dibujan cada frame en su forma exacta y no remuestrean nada | Regla nueva en el contrato: *un transform traslada, nunca escala un nodo*, y alcanza también a la sacudida de `Asustado`. 0 assets nuevos. Desviaciones 100–102 abajo |
| 2026-09-26 | Arte: **el anillo del Magnetar no cabía en su propio lienzo** | A.4.1 topaba el asset en 384×384 razonando que «3.6 u a PPU 64 son 230 px de radio». El cálculo es correcto pero **230 px es el radio**: el círculo mide **461 px de lado**, así que los niveles 4 y 5 no cabían. El lienzo pasa a **512×512**. Y al comprobarlo salió lo de verdad caro: **un solo anillo no puede servir a cinco radios**, porque escalarlo daría ×0.556, ×0.667, ×0.778 y ×0.889, todos no enteros y todos prohibidos por el mismo argumento que convirtió el protón en tres sprites discretos. A.4.1 pasa de 4 archivos a **16** | Total del inventario 94 → **106**. Contrato actualizado en *Familias exentas de la celda*. T.7 #34 abierta (matriz de variantes) y T.8 punto 6 ampliado. Desviaciones 97–99 abajo |
| 2026-09-26 | Niveles: **la columna `N` de 11.2 no cuadraba** | Cuatro de los nueve niveles tenían un `N` distinto de la suma de sus propias columnas: +1 en el 5, el 7 y el 8, y **+2 en el 9**. Se recalcula `N` respetando el contenido —5, 7, 8, 9, **11**, 11, **13**, **14**, **16**— en vez de recortar nodos, porque `N` es un resumen y las otras columnas son el diseño, y porque las coordenadas siguen sin existir (T.7 #2). La leyenda declara ahora que `N` es **derivada** y que al llegar a `T5-02` será `spawns.Count`: si vuelve a no cuadrar, lo que está mal es `N` | La rampa deja de ser regular: tramo plano en 5–6 y salto de +2 al final. T.7 #28 ampliada a los niveles 6 y 8. Desviaciones 95–96 abajo |
| 2026-09-26 | Controles: **el clic izquierdo depende de si hay selección** | La [sección 13](../NOVA_GameDesign_Spec.md#13-controles-y-ui) daba al clic izquierdo tres significados simultáneos sobre un nodo propio —seleccionar, enviar el 50 % y abrir la rueda del Púlsar— sin ninguna prioridad escrita. Se cierra con una sola regla: **sin selección el clic elige, con selección el clic envía**, y la selección **se mantiene** tras enviar. La rueda de poderes pasa a abrirse solo desde el panel de clic derecho. El menú de encendido, que no tenía forma documentada de abrirse, se coloca en el clic izquierdo del Asteroide, que estaba libre porque un Asteroide nunca es origen de un envío | 13.1 reescrita en tres bloques, **13.7 nueva**, 13.3 #5 y #6 corregidos, 13.5 repuntada. `T2-02` apunta ya a 13.7. Desviaciones 92–94 abajo |
| 2026-09-26 | Estructura: **una sola lista de tareas** | La [sección 19](../NOVA_GameDesign_Spec.md#19-fases-y-criterios-de-salida) del maestro y T.4 enumeraban las dos el trabajo de cada fase, y habían derivado. El §19 se reescribe como ***Fases y criterios de salida***: pierde las viñetas, que remiten a T.4, y conserva los seis criterios en una tabla. T.4 queda como única lista de trabajo. Se corrige de paso el criterio de la fase 6, que remitía a «el documento 2» en vez de a algo comprobable | Ancla del §19 renombrada y actualizadas sus 6 referencias. T.6 pasa a declararse copia de consulta. T.7 #33 cerrado. Desviación 91 abajo |
| 2026-09-26 | Fases: **el encendido se mueve a la fase 1** | La fase 1 se contradecía a sí misma: `T1-12` pedía una escena que demostrase «capturar, **encender** y tomar un nodo enemigo», y `T2-01`, la tarea que implementa `NodeIgnition`, **dependía de `T1-12`**. El corte además estaba mal puesto: `T1-04` ya metía en la fase 1 «validez y coste de encendido» vía `ConversionRules`. `T2-01` pasa a ser **`T1-11b`**, usando la convención de `T3-03b` para no renumerar nada. En la fase 1 el encendido **solo puede ser a Estrella**, porque `MagnetarTurret` es `T2-04` y `PulsarCaster` es `T3-02`: no es un recorte arbitrario sino el único encendido posible en ese punto. `IgnitionMenu` se queda en la fase 2, que es donde por fin hay tres opciones que elegir | Fase 1 pasa de 12 a 13 tareas y fase 2 de 8 a 7; el total sigue en 52. `T2-02` y `T2-03` repuntadas a `T1-11b`. [19](../NOVA_GameDesign_Spec.md#19-fases-y-criterios-de-salida) actualizado en sus dos fases. Desviación 90 abajo |
| 2026-09-26 | Reglas: **un Magnetar a 0 cae con Carga 1** | Revisión de coherencia de los siete `.md`. El caso 7 de la [sección 17](../NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución) afirmaba que un protón de Carga 1 **no** captura un Magnetar vacío y exigía Carga ≥ 2. Es falso: la guarda de [6.2](../NOVA_GameDesign_Spec.md#62-resolución-de-impacto) es `effective < target.energy`, y con los dos a 0 esa comparación es falsa, así que el pseudocódigo normativo **ya caía en la rama de captura**. No se cambia ni el multiplicador 0.5 ni el redondeo hacia abajo: se corrige la prosa que los leyó mal | Caso 7 reescrito, caso 32 y [7.4](../NOVA_GameDesign_Spec.md#74-decaimiento) pasan de «Carga 2» a «Carga 1», y la decisión [18](../NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #9 queda tachada. Desviación 89 abajo |
| 2026-09-20 | Nomenclatura: **el Enemigo deja de llamarse «IA»** | 38 de las 39 apariciones de «IA» designaban al oponente y se sustituyen por **«el algoritmo»** o **«el Enemigo»** según la frase. La 39.ª se conserva y se precisa: en T.1, «IA» es el **asistente** con el que se escribe el código. El término entra por fin en el glosario normativo de la [sección 2](../NOVA_GameDesign_Spec.md#2-glosario-normativo), donde no estaba pese a usarse 39 veces. Renombrados el archivo `NOVA_IA_Algoritmos.md` → **`NOVA_Algoritmo_Enemigo.md`**, los scripts `AIEvaluator` → `EnemyEvaluator`, `AIThreatResponder` → `EnemyThreatResponder`, `AIProfile` → `EnemyProfile`, **`EnemyBrain` → `EnemyController`**, y las carpetas `Scripts/AI/` → `Scripts/Enemy/` y `Data/AIProfiles/` → `Data/EnemyProfiles/` | Ni una línea de C# escrita todavía, así que el renombrado de scripts es gratis. Desviaciones 86–88 abajo |
| 2026-09-20 | Reglas: **corrección del objetivo válido (misma sesión)** | La regla de 7.2.1 pasa de «nodo **enemigo** encendido» a **«nodo encendido no propio»**. El cambio es de una palabra y tiene dos consecuencias: el **Magnetar neutral vuelve a ser objetivo de los tres poderes** —se puede robar con Gusano por 60 en vez de 200 de Carga, callar con Cero Absoluto y vaciar con Decaimiento— y **ningún Asteroide lo es nunca**, que era lo que había que quitar. Una regla en vez de una lista por poder | Casos 30–32 de la sección 17 reescritos y 34–35 renumerados; `AjusteNeutral` añadido a `ScorePoder`; `T.7 #29` cambia de sentido. Desviaciones 83–85 abajo |
| 2026-09-20 | Reglas: **el Púlsar pierde el alcance y el proyectil** | Cuatro cambios encadenados en la [sección 7](../NOVA_GameDesign_Spec.md#7-poderes-del-púlsar). **1)** Los tres poderes pasan a **alcance global**; desaparecen los 7 u / 7 u / 5 u y la columna deja de ser un número. **2)** ~~Objetivo válido = **nodo enemigo encendido**~~ **— corregido el mismo día a «nodo encendido no propio», ver la fila de arriba.** Lo que se mantiene de este punto es la exclusión de los Asteroides de cualquier bando. **3)** El poder deja de ser proyectil: se entrega por un **haz de 0.25 s** no interceptable, con el efecto al terminar y revalidación del objetivo en ese instante. **4)** El algoritmo del Enemigo estrena `ScorePoder` ([12.3.1](../NOVA_Algoritmo_Enemigo.md#1231-función-de-puntuación-lanzar-poder)) porque el alcance era su único filtro de objetivos. Costes y tiempos de carga **sin tocar**, a propósito | 7 casos borde nuevos (17, #27–33), 5 decisiones nuevas (18, #13–17), 1 asset eliminado y 3 sustituidos, 1 tarea de tracking nueva. Desviaciones 76–82 abajo |
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

## Desviaciones

> **Cómo leer esta lista.** Una línea por desviación: **qué cambió** y **dónde vive ahora**. El razonamiento completo está en el documento que se cita, que es el que manda; aquí solo está el rastro de cuándo y por qué se movió. Los números no se reutilizan nunca, ni siquiera cuando una desviación queda revertida.

**Al cerrar las cuatro contradicciones menores (2026-09-26).**

103. **Los tres conteos desviados eran de cosas que habían crecido después.** La sección 17 pasó de 26 a 35 casos al quitarle el alcance al Púlsar, 12.7 de 8 a 9 tests con `ScorePoder`, y A.5 de 14 a 15 VFX. **Ninguno se actualizó donde se citaba.** Es el patrón de la desviación 95 otra vez: un número derivado escrito a mano. → `T4-02`, `T5-07`, [14.5](../NOVA_Estados_Animaciones.md#145-animaciones-puntuales-sin-estado).
104. **«Máx. 30 s» describía mal un temporizador de inactividad.** Los 30 s del auto-deshielo se reinician con cada impacto, así que `Congelado` no tiene duración máxima. **Un tope y un tiempo de espera no son lo mismo**, y la columna *Duración* de 14.1 los confundía. → [14.1](../NOVA_Estados_Animaciones.md#141-estados-del-nodo), [7.3](../NOVA_GameDesign_Spec.md#73-cero-absoluto).
105. **El caso 8 era una tercera exclusión encubierta.** [7.2.1](../NOVA_GameDesign_Spec.md#721-objetivo-válido) dice «dos exclusiones, y no admiten excepción», y el caso 8 añadía una tercera **solo para un poder de los tres**, dejando sin definir qué hacían los otros dos. Se resuelve por el precedente que ya existía: congelar pausa un temporizador, igual que pausa la carga de un Púlsar. **Iniciar y continuar son cosas distintas**, y §8.1 solo prohibía lo primero. → [17](../NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución) caso 8, [8.1](../NOVA_GameDesign_Spec.md#81-reglas), [14.6](../NOVA_Estados_Animaciones.md#146-matriz-de-compatibilidad).
106. **«Los tres registros» eran dos registros y un gestor.** `GameManager` es el tercer singleton y no es un registro, así que la excepción del principio 6 no lo cubría por su propio nombre. → [15.1](../NOVA_GameDesign_Spec.md#151-principios).

**Al arreglar el idle por transform (2026-09-26).**

100. **La regla y su infracción estaban en el mismo párrafo.** El apartado *Animación* define `s` y diez líneas más abajo se la encarga a un transform. El párrafo razona muy bien **qué** animar —«¿tiene algo interno que mover?»— y no llega a preguntarse **si el transform puede hacerlo**. → contrato, *Animación*.
101. **La fórmula no estaba mal; estaba mal quién la ejecutaba.** Dibujada por un generador es geometría calculada y es correcta —la Estrella y el Púlsar la usan así—; ejecutada por un `localScale` es remuestreo. **Una fórmula puede ser válida en un motor e inválida en otro, y el contrato no distinguía los dos.** → contrato, *Animación*.
102. **El reparto del idle pasa a tener dos criterios.** Al de siempre —¿algo interno que mover?, que decide si hace falta hoja— se le añade ¿cabe en una traslación entera?, que decide si el transform puede encargarse. El reparto actual no cambia, pero ahora se sostiene por los dos. De paso queda escrito que la sacudida de `Asustado` va en píxeles enteros, que nadie había dicho. → contrato, [14.0](../NOVA_Estados_Animaciones.md#140-reglas-generales) #4, [14.2](../NOVA_Estados_Animaciones.md#142-animación-de-susto).

**Al corregir el anillo del Magnetar (2026-09-26).**

97. **Un radio escrito donde iba un diámetro.** El número correcto se calculó y se usó para la magnitud equivocada, que es la forma más difícil de detectar: el texto *enseña* la cuenta buena y aun así concluye mal. → [A.4.1](../NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar), contrato *Resolución*.
98. **La prohibición de escalar ya estaba escrita dos veces y aun así se incumplió una tercera.** El protón (A.4.2) y [T.8](../NOVA_Tracking.md#t8-escala-del-tablero--cerrada) razonan los dos que `Filter Mode: Point` no admite factores no enteros, pero A.4.1 pedía un único anillo para cinco radios. **Una regla puede estar bien escrita y mal aplicada**, y eso no lo detecta releer la regla: lo detecta contar. Regla de trabajo nueva: **un asset que existe en varios tamaños es varios archivos, salvo que los factores sean enteros.** → [A.4.1](../NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar).
99. **Tres de los cinco radios no caen en píxel exacto** —2.4 u son 153.6 px—, y queda escrito que el dibujo redondea al píxel y la lógica no. Es inofensivo hoy (0.4 px contra un margen legible de 6.4 px) y desaparecería solo si T.8 recorta los radios a la familia 2.0–3.0. → [A.4.1](../NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar), T.8 punto 6.

**Al cuadrar la tabla de niveles (2026-09-26).**

95. **Tercer dato derivado escrito a mano, tercera contradicción.** Como el caso 7 y como las dos listas de tareas, `N` repetía información que ya estaba en otras columnas y se desvió. Queda declarada derivada en la leyenda de [11.2](../NOVA_GameDesign_Spec.md#112-diseño-de-niveles), con la regla de desempate escrita: manda la suma. **El patrón ya tiene nombre en este proyecto y conviene buscarlo antes que sus síntomas.**
96. **El nivel que vigilábamos no era el peor.** T.7 #28 señalaba el 4 por arrancar con una sola Estrella, pero al contar nodos por bando salió que el 6 y el 8 tienen la misma debilidad y peor proporción: **2.0, 3.0 y 4.0** de nodos Enemigo/Jugador. El orden de revisión pasa a ser 8, 6, 4. → T.7 #28.

**Al desambiguar el clic izquierdo (2026-09-26).**

92. **Un control con tres significados no es un control.** El clic izquierdo sobre un nodo propio seleccionaba (13.1), enviaba el 50 % si ya había selección (13.1, porque un nodo propio también es destino válido) y abría la rueda si era Púlsar (13.5). Se resuelve con **modo**, no con excepciones: la selección activa es la que decide. → [13.1](../NOVA_GameDesign_Spec.md#131-ratón).
93. **El Asteroide tenía un hueco, no una excepción.** No puede enviar ([5.2](../NOVA_GameDesign_Spec.md#52-envío)), así que nunca es origen y su clic izquierdo no estaba asignado a nada. Ahí entra el menú de encendido, que hasta hoy **no tenía forma documentada de abrirse** pese a ser la acción más repetida de la partida. El Púlsar es el caso contrario —sí puede enviar— y por eso su rueda se queda en el panel. → [13.7](../NOVA_GameDesign_Spec.md#137-menú-radial-de-encendido), [13.5](../NOVA_GameDesign_Spec.md#135-rueda-de-poderes-del-púlsar).
94. **Dos erratas de la misma tabla.** El botón de reconversión ofrecía «los tres destinos posibles» y siempre son **dos** ([8.1](../NOVA_GameDesign_Spec.md#81-reglas) prohíbe volver a Asteroide y nadie se reconvierte a su propio tipo), y le faltaba la condición «si es propio» que el caso 23 de la [sección 17](../NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución) ya exigía. → [13.3](../NOVA_GameDesign_Spec.md#133-panel-de-clic-derecho).

**Al dejar una sola lista de tareas (2026-09-26).**

91. **Dos documentos mantenían la misma lista y por eso se contradijeron.** No era que el §19 dijese algo falso: era que añadir o mover una tarea obligaba a editar dos archivos. El §19 conserva el único trabajo que nada más hace —decir cuándo una fase está terminada— y T.4 se queda con el qué. **Un criterio de salida no es una lista de tareas**, y confundirlos fue lo que produjo una fase 1 que no podía cumplir su propio criterio (desviación 90). → [19](../NOVA_GameDesign_Spec.md#19-fases-y-criterios-de-salida), T.4, T.6.

**Al mover el encendido a la fase 1 (2026-09-26).**

90. **Un criterio de salida que su propia fase no podía cumplir.** El de la fase 1 exige encender un Asteroide, pero `NodeIgnition` vivía en la fase 2 y dependía de la última tarea de la fase 1. Se detectó comparando [19](../NOVA_GameDesign_Spec.md#19-fases-y-criterios-de-salida) contra T.4, que **mantienen la misma lista de trabajo por duplicado** y han derivado: once tareas de T.4 no figuran en el §19, y `NodeLevelComponent` aparece en fases distintas en cada uno. El encendido queda corregido; la duplicación sigue abierta (T.7 #33). → T.4 fase 1, [19](../NOVA_GameDesign_Spec.md#19-fases-y-criterios-de-salida).

**Al corregir el Magnetar a 0 (2026-09-26).**

89. **Tres reglas decían que sí y una prosa decía que no.** [6.2](../NOVA_GameDesign_Spec.md#62-resolución-de-impacto) (`0 < 0` es falsa → captura), [6.4](../NOVA_GameDesign_Spec.md#64-casos-de-captura-por-tipo) («cualquier nodo a 0 → Carga 1») y [7.3](../NOVA_GameDesign_Spec.md#73-cero-absoluto) («de Carga 1 o superior») ya coincidían; solo el caso 7 y la decisión 18 #9 sostenían el «Carga ≥ 2», y lo hacían por una resta mal leída. **La resistencia ×2 encarece vaciar un Magnetar, no rematarlo.** → [17](../NOVA_GameDesign_Spec.md#17-casos-borde-y-reglas-de-resolución) caso 7, [18](../NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #9.

**Al retirar «IA» como nombre del oponente (2026-09-20).**

86. **El término más usado del proyecto no estaba en el glosario normativo.** «IA» aparecía 39 veces en cinco archivos y la sección 2 no la definía. → [2](../NOVA_GameDesign_Spec.md#2-glosario-normativo), que ahora tiene `Algoritmo del Enemigo`.
87. **La etiqueta era falsa y el contenido correcto.** Nada hablaba de entrenar ni inferir; 12.7 ya exigía pureza y determinismo. Solo había que cambiar el nombre, no el diseño. → [12.7](../NOVA_Algoritmo_Enemigo.md#127-contrato-de-implementación).
88. **Quedó un uso legítimo y se precisó en vez de borrarlo.** En T.1 «IA» es el asistente, y el prompt lo dice en voz alta para que la distinción se mantenga sola. De paso `EnemyBrain` → `EnemyController`. → T.1, [15.7](../NOVA_GameDesign_Spec.md#157-input-enemigo-y-ui).

**Al corregir el objetivo válido (2026-09-20, misma sesión que el bloque siguiente).**

83. **«Enemigo» y «no propio» no son lo mismo.** La primera redacción dejó fuera al Magnetar neutral, único nodo Neutral con nivel. Corregido a «nodo encendido no propio»: una regla en vez de una lista. → [7.2.1](../NOVA_GameDesign_Spec.md#721-objetivo-válido).
84. **El Asteroide se excluye por el doble coste, no por ser inútil.** Un poder que lo capturase saltaría los 10 + 10 que son el precio de expandirse. → [4.1](../NOVA_Nodos.md#41-asteroide-nivel-0), [7.2.1](../NOVA_GameDesign_Spec.md#721-objetivo-válido).
85. **Admitir objetivos neutrales reabre una verificación de arte cerrada.** Los dos tintes pueden caer ahora sobre la rampa neutral, que nunca se midió. Ningún asset cambia. → T.7 #29, [A.3](../NOVA_Assets_Diseno.md#a3-estados-y-overlays--141).

**Al quitarle el alcance al Púlsar (2026-09-20).**

76. **Quitar el alcance obligó a inventar el objetivo válido.** Era lo único que decía a qué se podía apuntar. Un límite que se quita hay que reponerlo en otro eje; aquí el eje es el tipo de objetivo. → [7.2.1](../NOVA_GameDesign_Spec.md#721-objetivo-válido).
77. **El proyectil no sobrevivió, y no por estética.** A 6 u/s la diagonal son 6.1 s. El haz de 0.25 s es consecuencia aritmética de quitar el límite, no una decisión aparte. → [7.2.2](../NOVA_GameDesign_Spec.md#722-el-haz).
78. **La tercera fila del robo de carga sobrevivió, comprimida.** De «proyectil en vuelo» a «dentro de sus 0.25 s»: casi imposible de provocar, pero sin ella capturar un Púlsar a mitad de haz no tendría resolución escrita. → [7.5](../NOVA_GameDesign_Spec.md#75-agujero-de-gusano).
79. **El algoritmo perdió su filtro barato y hubo que pagarlo con una fórmula.** `ScorePoder` es la primera función añadida a la sección 12 por un cambio de reglas. → [12.3.1](../NOVA_Algoritmo_Enemigo.md#1231-función-de-puntuación-lanzar-poder).
80. **Un asset desapareció y tres cambiaron de naturaleza.** `ART-WLD-RANGE` fuera; los tres proyectiles pasan a haces. El inventario baja de 95 a 94 y el trabajo sube: un haz se estira y se rota. → [A.4.2](../NOVA_Assets_Diseno.md#a42-protón-y-haces-de-poder--143), T.7 #30.
81. **La atenuación del apuntado dejó de ser arte y pasó a ser código.** De «dentro o fuera del círculo» a «válido o no válido», resuelto bajando el color. Una figura menos dentro del tablero. → [A.4.3](../NOVA_Assets_Diseno.md#a43-selección-y-apuntado--131).
82. **La decisión de escala del tablero se abarató sola.** Ya no hay ningún número de poder medido en unidades, así que encoger el tablero no los toca. Quitar una regla puede desbloquear otra decisión abierta. → T.8.

**Al cerrar `ART-ST-BUFF` (2026-09-19).**

71. **El buff no es un tinte: es un glifo.** A.3 y 14.7 se contradecían y gana 14.7. La tabla de casos de A.3 pasa de tres a cuatro: hay un cuarto sitio donde puede vivir un overlay, la esquina de la celda. → [A.3.8](../NOVA_Assets_Diseno.md#a38-art-st-buff--power-up-activo), [14.7](../NOVA_Estados_Animaciones.md#147-prioridad-visual).
72. **El cuadrado libre de la esquina son 23 px** en los catorce nodos, y 33 contando solo Magnetar y Púlsar. A.3.1 midió 20 y dijo que ahí no cabe un `zzz`; no dijo que sí cabe una marca. → [A.3.8](../NOVA_Assets_Diseno.md#a38-art-st-buff--power-up-activo).
73. **`FX-BUFF` no va sobre todos los nodos de la facción, aunque el efecto sí.** El Asteroide no gana nada con ningún power-up. Consecuencia gratis: ningún nodo lleva nunca los dos buffs. → [14.5](../NOVA_Estados_Animaciones.md#145-animaciones-puntuales-sin-estado).
74. **Segundo asset que llega con «16 frames, bucle» y sale estático.** Regla nueva: el número de frames se mide, no se planifica. → contrato, *Animación*.
75. **El color de un overlay de power-up es el del power-up, no el de la facción**, al revés que el halo del drop: allí el icono flotante dice cuál es, y sobre un nodo buffeado no flota nada. → [A.3.8](../NOVA_Assets_Diseno.md#a38-art-st-buff--power-up-activo), [A.4.4](../NOVA_Assets_Diseno.md#a44-power-ups--9).

**Al cerrar `ART-ST-CAP` (2026-09-19).**

66. **La geometría no cambia con la facción en ninguno de los catorce nodos**, comprobado byte a byte sobre los 34 sprites. 14.0 #3 lo decía y nadie lo había medido. → [14.0](../NOVA_Estados_Animaciones.md#140-reglas-generales).
67. **La desviación 63 se parte en dos.** «Entrar desde nada» solo vale para un efecto que se prepara; uno que se dispara arranca en su pico. → [A.3.7](../NOVA_Assets_Diseno.md#a37-art-st-cap--capturado), contrato.
68. **El blanco puro se cobra.** El índice 0 de toda rampa está reservado al especular, y `ART-ST-CAP` es el único overlay del proyecto que lo usa como color propio. → contrato, *Paleta*.
69. **14.1 se precisa, no se corrige.** Conserva el blanco y la onda que sale; quita el relleno del flash y añade un segundo anillo que entra. → [14.1](../NOVA_Estados_Animaciones.md#141-estados-del-nodo).
70. **`FX-CAP` deja de ser el efecto genérico y pasa a ser el específico.** Gastar «flash + onda» en la captura habría dejado sin gesto a los cuatro destellos cortos de A.5. → [A.5](../NOVA_Assets_Diseno.md#a5-vfx-puntuales--145).

**Al cerrar `ART-ST-CONV` (2026-09-19).**

60. **«Silueta neutra» de 14.1 quiere decir sin tipo, no sin bando.** Durante los 3 s el nodo sigue siendo de su dueño —tanto que puede ser capturado—, así que pintarlo gris contradiría a 8.1. → [14.1](../NOVA_Estados_Animaciones.md#141-estados-del-nodo).
61. **`ART-ST-CONV` lleva variante de facción**, segundo overlay que la lleva tras `ART-ST-IGN` y por motivo distinto: aquel marca posesión porque el sprite no lo hace, este porque no puede dejar de marcarla. → [A.8](../NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).
62. **Un overlay aditivo no puede dibujar un colapso.** Con alfa binario solo suma píxeles: la silueta nueva la pone el animador cambiando de sprite y el asset solo tapa el corte. → [A.3.6](../NOVA_Assets_Diseno.md#a36-art-st-conv--reconvirtiendo). *Pendiente: no llegó al contrato (T.7 #32).*
63. **`verifica_tinte` gana un modo transitorio.** Un efecto con duración no se juzga por si cierra, sino por si entra desde nada y sale a nada, y de la cara importa cuántos frames la deja ciega. → [A.3.6](../NOVA_Assets_Diseno.md#a36-art-st-conv--reconvirtiendo).
64. **Primera y única exención al contador de nivel**, porque aquí el nivel está cambiando. Su duración coincide con la del cambio y **no se hereda**. → [A.8](../NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).
65. **El blanco puro queda reservado a `FX-CAP`.** `ART-ST-CONV` destella en color de facción precisamente para dejárselo. → [14.5](../NOVA_Estados_Animaciones.md#145-animaciones-puntuales-sin-estado).

**Al cerrar `ART-ST-DECAY` (2026-09-19).**

55. **El cuerpo común a los trece nodos son 505 px, y la cara se come casi todo:** quedan 228, y 195 de esos 228 están debajo de ella. «Pintar sobre el cuerpo» y «pintar en los trece» solo son compatibles en una franja. → [A.3.5](../NOVA_Assets_Diseno.md#a35-art-st-decay--infectado).
56. **La desviación 49 se queda corta: un tinte registra con TRES cosas.** La tercera son los otros tintes: dos campos de Bayer apilados acaban en una mancha. → [14.6](../NOVA_Estados_Animaciones.md#146-matriz-de-compatibilidad), [A.3.5](../NOVA_Assets_Diseno.md#a35-art-st-decay--infectado).
57. **`verifica_tinte` gana tres medidas**, las tres pedidas por este asset: cierre del bucle, frames distintos de verdad y convivencia con otro overlay. → [A.3.5](../NOVA_Assets_Diseno.md#a35-art-st-decay--infectado).
58. **Y una cuarta, porque una de las que había medía otra cosa.** *Contraste* promediaba solo donde el overlay cae encima del nodo, así que castigaba al que menos lo tapa. → [A.3.5](../NOVA_Assets_Diseno.md#a35-art-st-decay--infectado).
59. **Regla nueva: el ciclo de un aura es radial, no giratorio.** Hacer viajar la fase no cierra el bucle salvo dando una vuelta entera, y eso es un aura que gira. → contrato, *Animación*.

**Al cerrar `ART-ST-FROZEN` (2026-09-19).**

48. **`Congelado` va sobre trece sprites, no catorce.** 14.1 lo limita a nodos encendidos y el Asteroide nunca lo está. Vale igual para `Infectado` y el buff; `Reconvirtiendo` son doce. → [14.1](../NOVA_Estados_Animaciones.md#141-estados-del-nodo).
49. **La desviación 47 era falsa a medias.** Un tinte sí registra: con la cara y con el contador de nivel, que entre las dos ocupan todos los radios de la celda. → [A.3.4](../NOVA_Assets_Diseno.md#a34-art-st-frozen--congelado).
50. **Tercera vara de verificación: `verifica_tinte`**, junto a `verifica` (glifos) y `verifica_ojos` (susto). La vara de un glifo suspendería a un tinte por hacer su trabajo. → [A.3.4](../NOVA_Assets_Diseno.md#a34-art-st-frozen--congelado).
51. **Regla nueva: nada que se dibuje encima de otro dibujo lleva contorno.** Medido sobre «El bloque»: sus aristas se comían el 71 % de un frente de onda del Púlsar. → contrato, *Doble contorno*.
52. **Dos primitivas nuevas, catálogo a 18:** `tramar` (el dithering sacado fuera de cada geometría, para que dos propuestas se midan igual) y `veta_luz` (la línea que no suma huella). → contrato, *Catálogo*.
53. **El ancla funcional no siempre está en el índice 3.** `#B8F0FF` vive en `hielo[2]`. Se corrige el contrato, no la paleta. → contrato, *Paleta*.
54. **14.7 no cambia, y conviene saber por qué.** Un tinte va por encima de los ojos; no se arregla reordenando capas sino dejando hueco a la cara en la geometría del tinte. → [14.7](../NOVA_Estados_Animaciones.md#147-prioridad-visual).

**Al cerrar `ART-ST-SCARE` (2026-09-18).**

43. **Un overlay puede tener geometría fija y posición variable.** La separación entre ojos va de 9.5 a 16.0 px en los 34 sprites: ninguno de posición fija aterriza, y escalarlo está prohibido. → [A.3.3](../NOVA_Assets_Diseno.md#a33-art-st-scare--asustado).
44. **La tabla de anclaje es un entregable, no un comentario.** Vive en `tools/nova_estados.OJOS`. Primera vez que un asset necesita **datos** además de píxeles. → [A.3.3](../NOVA_Assets_Diseno.md#a33-art-st-scare--asustado).
45. **Regla nueva: una hoja cuyos frames no son distintos no es una animación.** Medido antes de decidir, no después. → contrato, *Animación*.
46. **Regla nueva: lo que no cabe en una escala cabe en tallas discretas más desplazamiento entero.** Estaba implícita en el protón; con un segundo usuario sube a *Reglas de trabajo*. → contrato.
47. **A.3 termina de partirse en tres casos**, escrito en su cabecera. ⚠️ Su última frase la desmintió el asset siguiente (ver 49). → [A.3](../NOVA_Assets_Diseno.md#a3-estados-y-overlays--141).

**Al cerrar `ART-ST-IGN` (2026-09-18).**

38. **`SinEncender` pasa a tener dos lecturas y A.3 gana un ID.** El animador elige por `energy >= 10`; `NodeState` y la matriz de 14.6 no se tocan. → [A.3.2](../NOVA_Assets_Diseno.md#a32-art-st-ign-y-art-st-ready--sinencender).
39. **El *parpadeo* de 14.1 se reasigna.** Deja de pedirse para todo `SinEncender` y pasa a ser la firma de la lectura accionable. → [14.1](../NOVA_Estados_Animaciones.md#141-estados-del-nodo).
40. **`art/states/v<n>/` no aguantaba el segundo overlay** —dos v1 caían en la misma carpeta— y pasa a `art/states/<estado>/v<n>/`. Corrige las rutas escritas el mismo día. → [A.1](../NOVA_Assets_Diseno.md#a1-convención-de-nombres).
41. **Las comprobaciones de overlay salen a `tools/nova_estados.py`.** Un asset se comprueba contra lo que va a tener al lado, con la misma vara en todas las propuestas, y el generador falla si algo no pasa. → contrato.
42. **Regla nueva: entre dos lecturas del mismo overlay, la accionable nunca pesa menos que la pasiva.** Se encontró contando, no mirando. → [A.8](../NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).

**Al cerrar `ART-ST-SLEEP` (2026-09-18).**

33. **La convención de propuestas deja de ser solo de nodos.** El generador declara `ESTADO` y `VERSION`. *Corregida por la 40 en la ruta.* → [A.1](../NOVA_Assets_Diseno.md#a1-convención-de-nombres).
34. **La regla de tramado se parte en dos.** Se traman los overlays que cubren el cuerpo; un glifo no cubre nada, compite con el fondo y va sólido con contorno. → [A.3](../NOVA_Assets_Diseno.md#a3-estados-y-overlays--141), contrato.
35. **«Los overlays son independientes del tipo» se matiza, y con medidas.** El mayor hueco libre en esquina son 20 px y un `zzz` legible necesita 33×16, así que `Dormido` se coloca contra la silueta del Asteroide. → [A.3.1](../NOVA_Assets_Diseno.md#a31-art-st-sleep--dormido).
36. **La capa 4 de 14.7 pasa a llamarse «Glifos y overlays»**, y se documenta que un glifo no pisa un solo píxel del sprite. Es lo que evita que choque con la capa 6. → [14.7](../NOVA_Estados_Animaciones.md#147-prioridad-visual).
37. **Un asset de A.3 se verifica contra el sprite de verdad**, abriendo el PNG. Y un glifo contable añade una comprobación propia: que las piezas no se toquen. → [A.3.1](../NOVA_Assets_Diseno.md#a31-art-st-sleep--dormido).

**Al volver el Magnetar a la v1 (2026-09-18).**

29. **El Magnetar vuelve a transform en el reparto del idle**, y con eso la desviación 11 queda revertida: el motivo de la regla no le aplica a un cuerpo tallado. → contrato, [A.2.4](../NOVA_Assets_Diseno.md#a24-magnetar--44).
30. **El contador de nivel del Magnetar pasa a ser posicional.** Efecto lateral documentado: la huella deja de ser monótona (0.64 / 0.94 / 0.94 / 1.05 / 1.05 u). → [A.2.4](../NOVA_Assets_Diseno.md#a24-magnetar--44).
31. **La tabla de ejes de A.8 deja de ser disjunta**, y corrige la 23. → [A.8](../NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).
32. **Se invierte qué primitivas están en uso.** `poliedro` y `veta` pasan a ejercitadas; `linea_dipolar` al otro lado. El catálogo no se toca: se descarta un diseño, no una herramienta. → [A.7](../NOVA_Assets_Diseno.md#a7-resumen-de-progreso).
33 *bis*. **Magnetar y Asteroide comparten ahora simetría e idle**, y *espacio interior* pasa a macizo en los cuatro. `calado` queda como valor de eje libre. *(Publicada originalmente como un segundo «23»; renumerada aquí para deshacer la colisión.)* → [A.8](../NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).

**Al cerrar los fondos (2026-09-18).**

24. **Módulo nuevo `tools/nova_fondo.py`, con 7 primitivas de fondo.** Los fondos son una familia con reglas distintas: solo comparten paleta y matriz de Bayer. → contrato, *Fondos*.
25. **Tres rampas nuevas, la paleta pasa de 20 a 22:** `bg_s1/2/3`. Existen porque todas las demás significan algo; `gusano`, `hielo`, `decaimiento`, `rayo` e `hidrogeno` quedan prohibidas en un fondo. → contrato, *Paleta*.
26. **Presupuesto de contraste**, verificado sobre el PNG: la media de cualquier ventana de 96×96 por debajo del tono más oscuro de un cuerpo de nodo. Se corrigió una vez. → [A.4.5](../NOVA_Assets_Diseno.md#a45-fondos).
27. **En un fondo se modula la cobertura, no el tono.** Una banda que cubre el 100 % de una ventana ya está por encima del presupuesto, por oscura que sea. → [A.4.5](../NOVA_Assets_Diseno.md#a45-fondos).
28. **Regla nueva: nada del fondo puede medir lo que mide un nodo** (0.6–1.2 u). Lo que caiga en esa franja es un nodo hasta que el jugador intente clicarlo. → contrato, *Fondos*.

**Al cerrar el Asteroide (2026-09-18).**

18. **Cuatro primitivas nuevas, catálogo a 16:** `bulto`, `crater`, `esquirla` y `estratos`. Las dos últimas salieron de la propuesta descartada y se quedan. → contrato, *Catálogo*.
19. **La concavidad entra en el contrato como material propio.** Con ella, contorno y sombreado son dos ejes independientes y las tres combinaciones vivas quedan documentadas. → contrato, *Los tres cuerpos*.
20. **Regla nueva: un accidente de superficie modula la banda que hay, no la sustituye.** Pintado en absoluto, un hoyo en la sombra se lee como un bulto. → contrato.
21. **El especular se puede apagar**, única excepción viva a la regla 3: una roca mate no es material pulido. → contrato, *Regla 3*.
22. **A.2.1 se corrige.** La posesión del Asteroide la marca el overlay `ART-ST-IGN`, no el sprite: una variante de facción es la misma geometría con otra rampa. → [A.2.1](../NOVA_Assets_Diseno.md#a21-asteroide--41), [14.0](../NOVA_Estados_Animaciones.md#140-reglas-generales).
23. **La partición de materiales queda cerrada con el cuarto valor:** el Asteroide solo refleja, sin luz propia. *Su segunda mitad la corrigió la 31.* → [A.8](../NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).

**Al cerrar el Magnetar (2026-09-18).**

11. ~~**El Magnetar pasa de transform a hoja de 16 frames.**~~ **Revertida por la 29.** Lo que queda en pie es la regla que la justificaba: el criterio es el motivo, no la lista. → contrato.
12. **Tres primitivas nuevas, catálogo a 12:** `poliedro` (la regla 1 muestreada por cara), `veta` y `linea_dipolar`. → contrato, *Catálogo*.
13. **La partición de materiales pasa a ser regla del contrato:** Estrella luz continua, Púlsar luz tramada, Magnetar materia. Eje de separación de primer orden. → contrato.
14. **Dos reglas de trabajo nuevas:** un nodo no se construye con las piezas de otro, y el material separa tanto como la silueta. → contrato, *Reglas de trabajo*.
15. **A.7 queda desmentida en su propia predicción.** Daba por hecho que el Magnetar heredaría `anillo` y `anillo_orbital`; no heredó ninguna, y fue lo mejor que le pasó. → [A.7](../NOVA_Assets_Diseno.md#a7-resumen-de-progreso).
16. **Tabla de ejes de diseño ocupados en A.8**, para consultarla *antes* de empezar un nodo. Sale de descubrir lo mismo por las malas tres veces. → [A.8](../NOVA_Assets_Diseno.md#a8-decisiones-de-diseño-cerradas).
17. **Restricción geométrica documentada:** cara grande y cinco lazos anidados no caben juntos en la celda de 96. → T.7 #9.

**Al cerrar el Púlsar (2026-09-18).**

7. **`art/nodes/` pasa a una carpeta por nodo y una subcarpeta por propuesta**, y los generadores a `gen_<nodo>_v<n>.py` con el sufijo siempre presente. → [A.1](../NOVA_Assets_Diseno.md#a1-convención-de-nombres).
8. **Dos primitivas nuevas, catálogo a 9:** `esferoide` (y `esfera` pasa a delegar en ella, verificado pixel a pixel) y `cono_trama`. → contrato, *Catálogo*.
9. **Dos reglas de trabajo nuevas:** dos nodos no se separan repintándolos, y una huella se mide sobre el ciclo entero, no sobre el frame 0. → contrato, *Reglas de trabajo*.
10. **`elipse_base_plana` está en el contrato y nunca se implementó.** No se ha tocado. → T.7 #7.

**Paleta, pipeline y Estrella (2026-09-18).**

1. **PPU 64, no 96.** La celda de 96 px pasa a valer 1.5 u. → contrato, *Resolución*.
2. **12 fps fijo**, en vez de «por defecto 16 frames». A.3 y A.5 recalculadas: los valores viejos implicaban entre 4 y 15 fps según el efecto. → contrato, *Animación*.
3. **El protón no escala de forma continua:** tres sprites discretos. Anula la fórmula de 14.3. → [A.4.2](../NOVA_Assets_Diseno.md#a42-protón-y-haces-de-poder--143).
4. **`✅` cambia de significado en A.0:** «generado y disponible en `art/`», no «integrado en Unity». La definición anterior era circular con T.2 #3. → [A.0](../NOVA_Assets_Diseno.md#a0-cómo-usar-este-documento).
5. **Cuatro primitivas nuevas**, y el catálogo de código deja de estar copiado en el `.md`: pasa a `tools/`. → contrato, *Catálogo*.
6. **A.2.2 reescrita.** La Estrella ya no es «corona + protuberancias» sino cuerpo + atmósfera de dos capas con cizalla. → [A.2.2](../NOVA_Assets_Diseno.md#a22-estrella--42).
