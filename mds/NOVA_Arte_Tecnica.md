# NOVA_Arte_Tecnica.md

Referencia del método con el que se genera el arte del proyecto. Este
documento describe **cómo** se produce cada sprite. El catálogo de qué
sprites hacen falta vive en los documentos de diseño.

> **Implementación:** `tools/nova_core.py` (primitivas de nodo),
> `tools/nova_fondo.py` (primitivas de fondo) y `tools/nova_paleta.py`
> (rampas). Este documento es el contrato; ese código es la única
> implementación. Si discrepan, manda el documento y se corrige el código.
> Cada generador de asset es un `tools/gen_<nodo>_v<n>.py` que solo compone
> primitivas y una tabla de niveles: **nunca reimplementa el modelo de luz**.
> El sufijo de versión va siempre, haya una propuesta o tres.

---

## Principio

Los sprites no se dibujan: se calculan. Cada asset es una función que
describe geometría y devuelve una matriz de píxeles. El PNG es una salida
derivada, nunca la fuente de verdad.

**Reparto de responsabilidades del stack:**

- **NumPy** hace todo el trabajo real. La imagen es un array
  `(alto, ancho, 3)` y el sombreado se resuelve con operaciones sobre la
  matriz completa, sin bucles por píxel.
- **Pillow** solo convierte el array final a PNG. Aparece en dos líneas:
  el import y el `save`.

**Regla dura: `ImageDraw` no se usa nunca.** Sus primitivas dibujan formas
con relleno uniforme y borde duro, sin control del sombreado. Toda la
geometría se resuelve con máscaras booleanas de NumPy.

---

## De dónde viene el volumen

El sprite trabaja sobre una rejilla, pero la forma se lee redonda. Eso se
consigue con siete reglas que se aplican siempre, en este orden. Las cinco
primeras gobiernan el cuerpo; las dos últimas, todo lo que lo rodea.

### 1. Normal fingida de esfera

Para cada píxel dentro de la forma se calcula la normal de superficie como
si fuera una esfera, y se ilumina con un vector de luz fijo para todo el
proyecto.

```python
nx = (x + 0.5 - cx) / r
ny = (y + 0.5 - cy) / r
e  = nx*nx + ny*ny          # dentro de la forma si e <= 1
nz = np.sqrt(np.clip(1 - e, 0, 1))
d  = nx*L[0] + ny*L[1] + nz*L[2]    # iluminacion difusa
```

Esto es lo que produce el degradado continuo de luz a sombra sobre una
superficie curva. Sin este paso, cualquier cosa que se dibuje se ve plana.

### 2. Cuantización en bandas

El valor continuo de luz se recorta en escalones discretos. El número de
bandas es **la palanca principal de redondez**: más bandas, transición más
suave y aspecto menos escalonado.

```python
UMBRALES = [0.08, 0.28, 0.48, 0.68, 0.86]   # 7 tonos
banda = 6 - np.digitize(d, UMBRALES)         # 1 = mas claro, 6 = mas oscuro
```

Base del proyecto: **7 tonos**. Para sprites grandes (jefes, retratos) se
puede subir a 9 o 10 añadiendo umbrales intermedios. Bajar de 7 hace que la
forma se vea facetada.

### 3. Punto especular

```python
s = nx*HV[0] + ny*HV[1] + nz*HV[2]
banda = np.where(s > 0.972, 0, banda)   # tono 0, reservado al brillo
```

Es el reflejo que hace que la superficie se lea como material y no como una
silueta coloreada.

### 4. Luz de rebote

```python
banda = np.where((e > 0.80) & (d < 0.30), np.minimum(banda, 4), banda)
```

Cerca del borde y en zona de sombra, la banda se aclara. Simula la luz que
rebota del entorno. Sin esta regla el lado oscuro se cierra en negro y la
forma se ve muerta y pegada al fondo.

### 5. Doble contorno

**Interno.** Cuando una pieza se dibuja encima de otra, los píxeles de su
propio borde se pintan con el color de contorno. Es lo que permite que
varias esferas apiladas se lean como piezas separadas en vez de fundirse en
una mancha.

```python
borde = dentro & (e > 0.86) & lienzo["lleno"] & (lienzo["id"] != oid)
```

**Externo.** Al final, todo píxel vacío adyacente a un píxel lleno se pinta
con el color de contorno. Define la silueta contra el fondo.

### 6. Cizalla en todo lo que rodea a un cuerpo

Coronas, auras y atmósferas se describen como una onda sobre el ángulo. Si
esa onda se evalúa en el ángulo del píxel, la figura resultante es
**invariante bajo rotación de 360/n grados**, que es la definición de un
engranaje. El ojo lo lee como pieza mecánica por mucho que se corrija el
color.

La onda se evalúa siempre en el ángulo **desfasado en función del radio**:

```python
avance = np.clip((rad - base) / (r_ext - base), 0.0, 1.6)
ang    = ang - cizalla * avance
```

Cada lengua se curva hacia atrás al alejarse. Desaparece el eje de simetría
y, al animar, la atmósfera fluye en vez de girar rígida. Valor útil: 0.3
para capas pegadas al cuerpo, 0.7–0.8 para capas externas. Por encima de 1.0
las lenguas se afinan y la figura pasa a leerse como pelaje.

Cuando se apilan dos capas de atmósfera, van con **fase desplazada media
longitud de onda**. Si coinciden se suman en las mismas lenguas y vuelve el
patrón regular, solo que más grueso.

### 7. Degradados por trama, nunca por alfa

El alfa es binario, así que un resplandor no puede hacerse con
transparencia parcial. Se hace con **dithering ordenado Bayer 8×8**: se
compara una densidad continua contra la matriz de umbrales y se enciende el
píxel si la supera.

Estos píxeles van a una capa aparte del lienzo, `glow`, que **no genera
contorno exterior**. Si generaran contorno, cada punto de la trama saldría
perfilado en negro y el resplandor parecería suciedad.

**Excepción: la UI fuera del tablero** (2026-10-14, con el usuario;
[18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #44). En los
menús y las pantallas de fin —halos tras un sprite, el brillo que recorre un
botón, velos que entran y salen— el degradado **puede ser de alfa**, porque es
luz de la interfaz y se mueve en continuo
([13.12](./NOVA_GameDesign_Spec.md#1312-lenguaje-y-movimiento-de-la-ui)).
**Dentro del tablero no hay excepción**: nodos, overlays, VFX, protones, haces
y fondos siguen con trama y alfa binario. Y los sprites del tablero que se
reutilizan en un menú siguen siendo los mismos PNG: la excepción vale para lo
que la UI dibuja alrededor de ellos, no para ellos.

---

## Catálogo de primitivas

Están en `tools/nova_core.py`. **Se reutilizan siempre.** Ningún generador
de asset vuelve a implementar el modelo de luz.

### El lienzo

`lienzo(S)` devuelve un diccionario con cuatro capas:

| Capa | Qué es |
|---|---|
| `col` | `(S, S, 3)` uint8. El color. |
| `lleno` | Superficie sólida. Genera contorno exterior y participa del contorno interno. |
| `glow` | Trama luminosa. Se ve, pero **no genera contorno**. |
| `id` | Identificador de pieza por píxel. Es lo que permite el contorno interno. |

`finalizar(lz)` traza el contorno exterior sobre `lleno` y devuelve
`lleno | borde | glow` como máscara de alfa. `guardar(lz, ruta)` escribe el
PNG. `a_rgba(lz)` devuelve el array sin escribir a disco, para previsualizar.

### Primitivas de volumen

| Primitiva | Para qué | Notas |
|---|---|---|
| `esferoide(lz, cx, cy, rx, ry, phi, ramp, oid, calor=0, apagado=0)` | Cuerpos que no son bolas | Elipsoide inclinado. **No duplica el modelo de luz:** calcula la normal en el espacio local de la esfera unidad y la rota de vuelta a pantalla, así que sigue iluminándose con la `L` global de la regla 1. Un cuerpo que gira rápido se achata por su ecuador; el achatamiento y la inclinación son dos ejes de diseño gratis para separar nodos que si no comparten silueta circular. |
| `esfera(lz, cx, cy, r, ramp, oid, calor=0, apagado=0)` | Cuerpos, cabezas, ojos, bulbos | Caso particular de `esferoide` con los dos semiejes iguales, y **delega en ella**. `calor` aclara las bandas hacia el centro con caída radial; `apagado` oscurece de forma uniforme. Con esos dos escalares, un nodo apagado y uno de núcleo blanco son **la misma llamada con la misma rampa**. El especular se aplica después del `calor`, para que un cuerpo apagado conserve brillo. |
| `elipse_base_plana(lz, cx, cy_base, rx, ry, ramp, oid, filete)` | Cuerpo tipo gota o gelatina | Cúpula redonda con base asentada. Pivote `Bottom`. ⚠️ **Documentada pero no implementada.** Ningún asset de NOVA la ha necesitado todavía: aquí nada se apoya en un suelo. Ver T.7 en [NOVA_Tracking.md](./NOVA_Tracking.md). |
| `tubo_gota(lz, p, r0, r1, ramp, oid, muestras, sesgo_banda)` | Apéndices, llamaradas, colas | Bézier cúbica con punta bulbosa. El radio crece hasta el 80 % del recorrido y luego se redondea. |
| `tubo_curva(lz, p, grosor, ramp, oid, ...)` | Anillos, cables, arcos cerrados | Grosor constante, **sin afilado**. Un anillo hecho con `tubo_gota` se lee como un collar de cuentas. Lleva contorno interno propio. |
| `poliedro(lz, cx, cy, verts, ramp, oid, mesa, sesgo_mesa, aristas, especular)` | Cuerpos tallados, cristales, escombro angular | Mesa frontal plana más un anillo de caras de talle. **La regla 1 muestreada por cara**, no por píxel: misma `L`, mismos umbrales, mismo rebote. Ver *Materia facetada*. Lleva contorno interno propio. |
| `veta(lz, p0, p1, grosor, ramp, banda, oid)` | Grietas, fracturas incandescentes, filones | Segmento sólido de banda constante. Una fractura en una corteza es materia incandescente, no trama, y cuenta para la huella como el resto del cuerpo. |
| `bulto(lz, cx, cy, r, ramp, oid, perfil, calor, apagado, especular)` | Rocas, cantos rodados, masas orgánicas | Contorno irregular por armónicos sobre el ángulo, `r_lim(a) = r·(1 + Σ ampₖ·cos(k·a + faseₖ))`, y sombreado en el radio **normalizado** `rad/r_lim`. Esa división es toda la primitiva: hace que el terminador siga al contorno. Con `perfil` vacío es exactamente `esfera`. `especular=False` lo deja mate. |
| `esquirla(lz, cx, cy, verts, ramp, oid, ...)` | Fragmentos, piedra partida | Contorno **recto** con sombreado **curvo**. El límite sale de la función soporte del polígono, que exige convexidad. Ver *Los tres cuerpos*. |
| `crater(lz, cx, cy, r, ramp, oid_cuerpo, hondura, borde, tope)` | Hoyos, picaduras, impactos | Depresión **cóncava** con reborde levantado: la regla 1 con la normal del plano invertida. Ver *Concavidad*. Modula, no sustituye. |
| `estratos(lz, cx, cy, ang, grosores, deltas, oid_cuerpo, ramp, ...)` | Capas, vetas sedimentarias, bandeado | Capas paralelas que cruzan un cuerpo y las corta la silueta sin doblarse a seguirla. Es el único principio de composición **sin centro** del proyecto. Modula, no sustituye. |

### Primitivas de atmósfera y entorno

| Primitiva | Para qué | Notas |
|---|---|---|
| `corona_radial(lz, cx, cy, r_int, r_ext, picos, fase, ramp, oid, amp, dureza, armonicos, cizalla, sesgo_banda)` | Coronas, cromosferas, auras | Se dibuja **antes** del cuerpo. Aplica las reglas 6 y 7 de arriba. El gradiente radial se mide **desde el limbo**, no desde el centro: medido desde el centro, toda la franja visible caería en el tramo oscuro de la rampa y la corona se vería plana. |
| `tramar(lz, dens, ramp, banda_int, banda_ext, corte, capa)` | Cualquier forma resuelta por trama | El paso final que `halo_trama` y `cono_trama` tenían cableado cada una dentro de su geometría: comparar un campo de densidad continuo contra la matriz de Bayer. Se saca aparte para que una forma nueva —un tinte de estado, que no es ni anillo ni cono— no tenga que traerse su propio dithering, y para que dos propuestas del mismo asset se midan con la misma vara. Escribe en `glow` por defecto, que es la capa que no genera contorno. |
| `halo_trama(lz, cx, cy, ramp, r0, grosor, banda_int, banda_ext, gamma, corona, fase)` | Resplandor exterior | Dithering Bayer 8×8 sobre la capa `glow`. Con `corona=<perfil>` el resplandor sigue la misma onda que la corona y abraza las lenguas; sin él es un anillo centrado en `r0`. |
| `anillo(lz, cx, cy, r_int, r_ext, ramp, banda, oid)` | Annulus sólido | Ondas de choque, radios de alcance, aros de selección. |
| `anillo_orbital(lz, cx, cy, a, b, phi, grosor, ramp, oid, mitad, sesgo_banda)` | Órbitas, discos de acreción, frentes de onda | Elipse inclinada partida en cuatro cuartos Bézier. **Se llama dos veces por anillo**: `mitad='atras'` antes del cuerpo y `mitad='delante'` después. Con **una sola** mitad deja de ser un anillo y pasa a ser un arco abierto, que es el dibujo de un frente que se aleja. |
| `veta_luz(lz, p0, p1, grosor, ramp, banda)` | Aristas y grietas **sobre un sprite ya dibujado** | La misma línea que `veta`, pero en `glow` y por tanto **sin contorno**. Ver *Lo que se dibuja encima de otro dibujo no lleva contorno*. |
| `linea_dipolar(lz, cx, cy, R, eje, ramp, oid, g_polo, g_ecu, r_min, k_eje, k_ecu, onda_*)` | Líneas de campo, jaulas, lazos cerrados | `r(t) = R·sin²t`, la línea de campo cerrada de un dipolo. **El grosor sigue a `\|B\|` acotado:** grueso donde las líneas aprietan (polos), fino donde se abren (ecuador). Ni `tubo_curva` (grosor constante) ni `tubo_gota` (afila a punta) pueden engordar según una magnitud física, y ese gradiente es la información: de grosor constante, cinco lazos anidados son cinco rayas concéntricas. `r_min` recorta el tramo que converge en el centro —todas las líneas de un dipolo pasan por él— para que lo tape el cuerpo. `k_eje` / `k_ecu` escalan la curva: el lazo crudo mide 2R por 0.77R, y esa proporción de 2.6 a 1 desborda el ecuador de la celda antes de que la altura llegue a la mitad. `onda_*` hace viajar un realce **que solo aclara**: centrado en cero, el campo entero parpadea en vez de pulsar. |
| `cono_trama(lz, cx, cy, phi, largo, semiancho, ramp, r0, banda_int, banda_ext, gamma_eje, gamma_lat, doble)` | Haces, focos, conos de luz | Dithering Bayer sobre `glow`: **es luz, no materia**. No marca `lleno`, no genera contorno y no engorda el hitbox de clic. Es el contrapunto material de `corona_radial`, y es lo que permite que dos nodos con la misma paleta dejen de parecerse. Los dos gamma van **por separado**: con uno solo, la densidad es el producto de dos factores menores que 1 y el haz desaparece —0.5 × 0.5 elevado a 1.6 es 0.12, por debajo de casi toda la matriz de Bayer—. `gamma_eje` < 1 mantiene el haz denso casi hasta la punta; `gamma_lat` > 1 deja el borde del cono nítido. |

### Los tres cuerpos

Contorno y sombreado son dos ejes independientes, y sus combinaciones no son
intercambiables: cada una dibuja un material distinto.

| | contorno | sombreado | lee como |
|---|---|---|---|
| `poliedro` | recto | caras planas | cristal tallado |
| `bulto` | suave | curvo | canto rodado |
| `esquirla` | **recto** | **curvo** | **piedra rota** |

La tercera casilla es la que dibuja un fragmento, y hace falta: una piedra
que se parte no gana caras planas por dentro, gana un **borde** recto donde
la fractura la cortó, y por debajo sigue siendo la misma masa redondeada.
Con `poliedro` sale un cristal —la fractura se propaga a todo el volumen— y
con `bulto` sale un guijarro, sin fractura en ninguna parte.

### Concavidad

Todo lo anterior abulta. Un cráter no, y esa es la única diferencia: su
pared mira hacia dentro, así que su normal apunta al lado contrario que la
de una cúpula en el mismo sitio.

```python
n_xy = -direccion_radial * hondura * u      # el signo es toda la primitiva
n_z  = 1
```

El resultado es que **se ilumina la pared del fondo y se oscurece la de
delante**. Es lo mismo que hace que una foto de la Luna girada 180 grados
convierta los cráteres en cúpulas: el ojo solo tiene la dirección de la luz
para decidir si algo entra o sale. No hay modelo nuevo, es la misma `L`
global y las mismas bandas.

El **reborde** no es adorno. Sin él el cuenco es una mancha de color; con
él, cada cráter lleva su media luna clara y su media luna oscura
enfrentadas, que es la firma que el ojo reconoce como hoyo.

### Un accidente de superficie modula, no sustituye

`crater` y `estratos` no pintan una banda: leen la banda que ya tiene cada
píxel y la **desplazan**. La búsqueda inversa contra la rampa da esa banda, y
lo que no sea un tono de la rampa —el contorno— se queda fuera y no se toca.

Pintando bandas absolutas, un accidente colocado en la mitad en sombra del
cuerpo sale **más claro que lo que lo rodea** y se lee invertido: el hoyo
parece un bulto. El error no se ve en el lado iluminado, solo en el oscuro,
que es donde nadie mira hasta que ya está hecho. Modulando, el accidente
funciona en cualquier punto del cuerpo, sobre cualquier rampa de facción y
encima de otro accidente.

El desplazamiento va **acotado y simétrico**. Sin acotar, la pared en sombra
se va a la banda 6 y la iluminada a la 1 —tres bandas contra dos—, así que
el mismo accidente pesa más por su lado oscuro y la hilera entera parece
iluminada desde otro sitio. La cota sustituye además a la luz de rebote de
la regla 4: en un hoyo de 6 px, el rebote se come el contraste entero y el
cráter desaparece.

### Lo que se dibuja encima de otro dibujo no lleva contorno

La regla 5 da por hecho que el contorno **separa**: es lo que permite que
varias esferas apiladas se lean como piezas y no como una mancha. Eso vale
dentro de un sprite, donde el contorno cae entre dos piezas que el mismo
generador ha puesto. No vale para un **overlay**, que es un PNG pegado
encima de un sprite ya terminado y sobre el que no tiene ninguna autoridad.

Ahí el contorno deja de separar y pasa a **tachar**. Todo lo que marca
`lleno` se lleva contorno exterior al finalizar, así que una línea de 1 px
sale con 1 px negro a cada lado y ocupa 3, en negro, encima del dibujo de
otro. Se midió diseñando `ART-ST-FROZEN`
([NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md), A.3.4): en la propuesta
«El bloque», las doce aristas de su prisma dibujadas con `veta` se llevaban
por delante el **71 %** de un frente de onda del Púlsar —33 px de relleno y
35 de contorno sobre una pieza de 142— y con él, el contador de nivel del
nodo. La propuesta se descartó por otra razón, pero el número vale para
cualquier overlay y la que quedó elegida es casi toda línea.

Es el mismo argumento que la regla 7 da para la capa `glow`: si el
resplandor generara contorno, cada punto de la trama saldría perfilado en
negro y parecería suciedad. Así que **un overlay dibuja en `glow`**: `veta`
sigue siendo materia incandescente y sigue llevando contorno, porque cuenta
para la huella de su cuerpo; `veta_luz` es la misma línea para cuando no hay
huella que sumar, solo un dibujo ajeno que respetar.

Un **glifo** que vive fuera del cuerpo es el caso contrario y sigue yendo
sólido y con contorno —el `zzz` de `Dormido`, la brasa de `SinEncender`—:
ahí no hay dibujo debajo que tachar, hay fondo contra el que recortarse.

### Materia facetada

Las siete reglas de arriba describen **superficies curvas y continuas**: la normal fingida de esfera se evalúa píxel a píxel y el resultado es un degradado suave. Es el material de la Estrella y del Púlsar.

Un cuerpo colapsado no es eso. En `poliedro` la **regla 1 se muestrea una vez por cara**: misma `L` global, mismos umbrales, mismo rebote, misma rampa. No es un modelo de luz nuevo, es el mismo evaluado con otra frecuencia. Lo que cambia es la lectura: planos duros en vez de degradado, materia en vez de atmósfera.

Dos detalles sin los cuales no funciona:

- **La normal va en el punto medio de la arista exterior, no en el centroide de la cara.** Tomada en el centroide, todas las caras caen a ~0.65 del radio, la condición de luz de rebote (`e > 0.80`) no se cumple nunca y el lado en sombra se cierra entero en la banda 6: un cuerpo muerto pegado al fondo, que es justo lo que prohíbe la regla 4.
- **El especular es una esquina, no una cara.** Encendiendo la cara entera, en un talle de caras desiguales la más ancha se lleva el brillo y sale una cuña blanca del tamaño de media figura: deja de leerse como reflejo y se lee como una pieza pegada encima.

`sesgo_mesa` sesga **solo la cara frontal**. Su normal es `(0,0,1)` exacta, o sea `d = L.z = 0.66`, que cae a un pelo del umbral 0.68 y la deja en la banda 3 justo por debajo del corte. Como es la cara más grande, es ella la que fija el valor percibido del cuerpo entero: un cuerpo que debería leerse claro sale medio apagado por un margen de 0.02. Subirla una banda corrige ese filo, no falsea la luz.

### Armónicos

`ARMONICOS` define el perfil angular por defecto de las coronas: tres
cosenos con peso y desfase propios. Un solo coseno produce puntas idénticas
y equiespaciadas; sumando armónicos aparecen lenguas largas y cortas
alternadas, que es lo que lee como fuego.

---

## Profundidad

Un cuerpo no tiene por qué ser un círculo. `esferoide` permite achatarlo e
inclinarlo, y **la inclinación es una señal de diseño de primer orden**: una
composición organizada alrededor de un eje oblicuo no se confunde con una
organizada alrededor de su centro, por mucho que compartan paleta y tamaño.

Para que un elemento parezca pasar por delante o por detrás de otro se
combinan dos señales, y ambas son necesarias:

1. **Orden de dibujo.** Lo de atrás primero, lo de delante después.
2. **Escala.** Lo de delante se dibuja con radio mayor que lo de atrás.

En trayectorias orbitales la profundidad sale del propio parámetro:
`z = sin(angulo)`. Si `z < 0` el elemento va detrás y más pequeño; si
`z >= 0`, delante y más grande. Las trayectorias visibles se parten en dos
mitades: la trasera se dibuja antes del cuerpo central y la delantera
después.

---

## Animación

- Los frames son el mismo objeto evaluado en instantes distintos de una
  función continua. No se redibujan.
- El parámetro `t` recorre exactamente 0 a 2π repartido entre N frames. El
  bucle cierra sin salto por construcción, para cualquier N.
- **Cadencia fija de 12 fps.** El número de frames de una animación no se
  elige: sale de su duración. `frames = ceil(duracion_en_segundos × 12)`.
  Un efecto de 0.4 s son 5 frames; uno de 0.7 s, 9. Esto es lo que evita que
  un efecto corra a 15 fps y otro a 4, que era la incoherencia anterior.
- Los **bucles idle**, que no tienen duración fija, son de **16 frames**.
- El pivote nunca se desplaza, porque todas las formas se construyen
  alrededor del mismo centro de celda.
- Ciclo idle de criaturas: squash and stretch con volumen conservado.
  `s = 1 + 0.07*sin(2πt)`, altura `× s`, anchura `÷ sqrt(s)`. **Esta fórmula
  solo la puede ejecutar un generador**, que dibuja cada frame en su forma
  exacta. Ver la regla siguiente.
- **Un transform traslada; nunca escala un nodo.** `s` produce alturas de
  71.9 px y 62.5 px sobre un sprite de 67 px, y con `Filter Mode: Point` eso
  remuestrea el sprite en cada frame duplicando y soltando filas distintas:
  no es un nodo respirando, es un nodo hirviendo. Es la misma prohibición que
  convirtió el protón en tres sprites discretos y con la que T.8 descarta
  arreglar la escala del tablero desde Unity, aplicada al último sitio donde
  faltaba. **Todo movimiento por transform de un nodo es una traslación de
  píxeles enteros**, y eso incluye la sacudida de `Asustado`
  ([14.7](./NOVA_Estados_Animaciones.md#147-prioridad-visual), capa 5).
- **Reparto del idle.** El nodo cuyo idle es solo deformación —**Asteroide** y
  **Magnetar**— lo resuelve **por transform en Unity** y no genera hoja, así
  que su idle **no es un squash sino un cabeceo**: 1 px el Asteroide, 2 px el
  Magnetar, ciclo lento y desplazamiento entero. Los que tienen que mover algo
  interno (**Estrella y Púlsar**) llevan hoja de 16 frames y sí usan `s`,
  porque allí la deformación la dibuja el generador y no hay remuestreo. Una
  corona congelada delata que el sprite es estático.

  **El reparto no cambia, pero su motivo son ahora dos preguntas y no una.**
  La primera sigue siendo «¿tiene algo interno que mover?», y decide si hace
  falta una hoja. La segunda es «¿lo que se mueve cabe en una traslación
  entera?», y decide si el transform puede encargarse. Un nodo que necesitara
  deformarse sin tener nada interno que mover llevaría hoja igual, solo por
  la segunda.

  El Magnetar ha estado en los dos grupos, y siempre por el mismo criterio.
  Con «La Botella» los lazos de campo eran su firma y un campo congelado
  delataba el sprite igual que una corona; cerrado en «El Yunque»
  ([NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md), A.2.4), un cuerpo tallado
  con esquirlas capturadas no tiene nada interno que mover y vuelve a
  transform. **El criterio es el motivo, no la lista.** La lista se actualiza
  cuando un nodo cambia de naturaleza; si un diseño futuro del Asteroide acaba
  teniendo algo interno que mover, se mueve él también.

**Un ciclo cierra por construcción o no cierra.** Si una animación reparte N
elementos a lo largo de un recorrido, sus posiciones se derivan del propio
recorrido —N tramos iguales— y no se escriben a mano: en un ciclo cada
elemento ocupa el sitio del siguiente y el conjunto final es idéntico al
inicial, sea cual sea el número de frames. Con posiciones escritas a mano el
avance y el recorrido no son conmensurables y el bucle salta al reiniciarse.

Hoja de sprites: tira horizontal, `ancho = celda × n_frames`.

```python
hoja = np.hstack([np.asarray(Image.open(f)) for f in sorted(frames)])
Image.fromarray(hoja, "RGBA").save("animacion.png")
```

---

## Paleta

Cada material es una rampa ordenada de más claro a más oscuro. El índice 0
se reserva al especular. La paleta completa vive en
**`tools/nova_paleta.py`**, en el diccionario `RAMPAS`. Aquí solo van las
reglas que la gobiernan.

**Dirección: retro-futurista cósmico.** Tres reglas duras:

1. **Ancla funcional.** Toda rampa ligada a un color de
   [14.4](./NOVA_Estados_Animaciones.md#144-paleta-funcional) contiene ese
   hex **literal** en su índice 3. `#3FD7F5` está en `str_p[3]`, `#F2456B`
   en `str_e[3]`, `#7CF23F` en `decaimiento[3]`. La paleta funcional del
   maestro no se reinterpreta: se hereda. **El índice puede ser el 2 cuando
   la rampa es clara de suyo:** `#B8F0FF` vive en `hielo[2]`, porque por
   encima de ese tono al hielo solo le quedan el blanco y el especular, y
   forzarlo al 3 habría oscurecido la rampa entera para cumplir un número.
   Lo que no se negocia es que el hex esté **literal** y en la mitad clara;
   cuál de los dos índices lo guarda lo dice `tools/nova_paleta.py`.
2. **Luz cálida, sombra violeta.** El índice 0-1 tiende al blanco; el 5-6
   converge siempre a la familia índigo/ciruela (`#1D2144` … `#3D1440`). En
   una ilustración de esa época la sombra nunca es negra, es morada. Es lo
   que hace que veinte rampas distintas se lean como una sola paleta.
3. **Nada toca el fondo.** Ningún índice 6 baja de la luminancia de
   `#0B0E1A`, para que la silueta no se funda con el espacio.

Los tipos se separan por temperatura dentro de su facción: Estrella
saturada, Púlsar más eléctrico, Magnetar desaturado y metálico, Asteroide
pardo. El único morado puro del juego es `gusano` (`#9B5CF6`), reservado al
Agujero de Gusano para que ese poder no se confunda con nada.

Un color nuevo se añade como rampa completa. Nunca como valor suelto.

Una variante cromática de una criatura existente es una entrada nueva en
este diccionario: misma geometría, misma llamada, otra rampa.

---

## Resolución

Celda base **96×96 px = 1.5 u de mundo**, o sea **PPU 64**. Ítems pequeños e
iconos: 48×48. Jefes o retratos: 128×128 con 9 o 10 bandas.

La suavidad percibida depende de dos factores combinados: resolución de la
celda y número de bandas de luz. Subir solo uno de los dos no basta.

No mezclar escalas dentro de una misma familia de assets.

### Huella del nodo

Dentro de la celda de 96, la **huella sólida** de un nodo va de **0.6 u a
1.2 u** según tipo y nivel. Es la medida que define el hitbox de clic.

El resplandor de `halo_trama` **no cuenta** para esa huella: es luz, no
superficie, y no debe ampliar el área clicable. Los generadores reportan las
dos medidas por separado y avisan si algún nivel toca el borde de la celda.

### Familias exentas de la celda

Cuatro familias de asset no caben en 96×96 y tienen resolución propia. No
violan la regla de no mezclar escalas porque **no comparten familia** con
los nodos:

| Familia | Resolución | Motivo |
|---|---|---|
| `art/bg/*` | **960×540** | Ver *Fondos*. Son 15 × 8.4375 u a PPU 64: el tablero entero ([T.8](./NOVA_Tracking.md#t8-escala-del-tablero--cerrada), cerrada el 2026-09-28). Eran 2048×1152 cuando el tablero medía 32 × 18 u, y llegaron a pedirse a 1280×720 durante unas horas, con el tablero de 20 u. |
| `art/world/world_radius_*` | Hasta 384×384 | El radio del Magnetar mide 1.5–2.5 u, y 2.5 u a PPU 64 son 160 px **de radio**: 320 px de lado ([A.4.1](./NOVA_Assets_Diseno.md#a41-radio-de-alcance-del-magnetar)). **Un archivo por nivel**, porque escalar uno solo daría factores no enteros. |
| `art/world/world_proton_*` | 48×48 | Ver abajo. |
| Trazos estirables: `world_beam_*`, `world_link_sheet`, `world_radius_shot_*` | **16 px de alto**, largo variable | Unen dos puntos arbitrarios del tablero, de 1 u a 17.2 u, así que se dibujan por piezas (nacimiento, tramo y punta) y no por longitud. El haz estira su tramo; el filamento de plasma lo **repite en mosaico**, porque una onda estirada pierde la rejilla ([NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md), A.4.2 y A.4.6). |

**El protón no escala de forma continua.** [14.3](./NOVA_Estados_Animaciones.md#143-protón)
pedía escala `0.6`–`1.4`, pero con `Filter Mode: Point` una escala no entera
rompe el pixel-perfect y produce filas de píxeles de distinto grosor dentro
del mismo sprite. Se sustituye por **tres sprites discretos** de 48×48
—pequeño, medio, grande— seleccionados por umbral de Carga.

---

## Fondos

Los fondos son una familia aparte y viven en `tools/nova_fondo.py`. No
comparten nada con las primitivas de nodo salvo la paleta y la matriz de
Bayer: allí hay cuerpos dentro de una celda de 96 px con normal, bandas,
contorno y huella; aquí hay campos de densidad de 960×540 px que se
resuelven a color por trama. Son dos familias con reglas distintas.

**El presupuesto de contraste.** La media de luminancia de cualquier ventana
de 96×96 px —la celda de un nodo— tiene que quedar por debajo del **tono más
oscuro de un cuerpo de nodo**, el índice 6 de las rampas de cuerpo. Si el
fondo local lo supera, el lado en sombra de un nodo pasa a ser más oscuro que
el cielo que lo rodea: deja de leerse como objeto iluminado y se lee como un
agujero recortado. El tope se calcula de la paleta, no se escribe a mano.

**Se modula la cobertura, no el tono.** En una celda de 96 px un degradado
puede recorrer la rampa entera porque la pieza es pequeña y va sobre fondo
liso. Un fondo ocupa la pantalla: si una banda cubre el 100 % de una ventana,
esa ventana ya está por encima del presupuesto por oscura que sea la banda.
Así que lo que se modula es **cuántos píxeles se encienden**, y la cobertura
máxima de cada banda se despeja del presupuesto:

```
media = c · lum(banda) + (1 - c) · lum(fondo)  ≤  TOPE
```

Para el índice 6 de una rampa de fondo salen unos dos tercios de cobertura;
para el 5, algo menos de la mitad; para el 4, la cuarta parte. De ahí sale la
única forma que tiene un fondo de NOVA de verse rico sin estorbar: **mucha
trama muy oscura, poca trama media y acentos contados**.

**Solo rampas sin significado.** `bg_s1`, `bg_s2` y `bg_s3` existen porque
todas las demás rampas llevan encima un significado de juego, y un fondo
pintado con una de ellas le miente al jugador durante toda la partida.
`gusano`, `hielo`, `decaimiento`, `rayo` e `hidrogeno` están **prohibidas en
un fondo**: son las cinco que hay que reconocer al instante.

**Nada del fondo mide lo que mide un nodo.** La huella de un nodo va de 0.6 a
1.2 u. Cualquier objeto del fondo dentro de esa franja es un nodo a ojos del
jugador hasta que intente clicarlo. O muy por debajo, o muy por encima.

**La viñeta va al revés**, mínima en el centro: en casi cualquier juego la
viñeta lleva la mirada al centro, y aquí el centro es el tablero.

| Primitiva | Para qué |
|---|---|
| `lienzo`, `luminancia` | Base y medida |
| `ruido(rng, celdas, octavas, cresta)` | fBm en [0,1]. Con `cresta`, filamentos en vez de manchas |
| `disco`, `banda`, `vineta` | Campos de densidad: caída radial, franja combada, viñeta invertida |
| `cobertura(ramp, banda, parte)` | Despeja del presupuesto la fracción máxima de píxeles de una banda |
| `tramar(img, dens, ramp, capas)` | Trama por cobertura. `capas` = `(banda, desde, parte)` |
| `silueta(img, dens, ramp, filo, hacia)` | Cuerpo lejano: masa a color de fondo y solo el canto a la luz |
| `estrellas(img, rng, n, ramp, factor)` | Campo determinista en tres escalones de brillo |
| `informe(nombre, img)` | Verifica el presupuesto sobre el PNG terminado |

## Importación en Unity

**Versión y plantilla:** Unity **6.6 (6000.6.0f1)**, plantilla **Universal 2D v7.0.0** (URP con 2D Renderer). La URP se elige porque en Unity 6 el componente **Pixel Perfect Camera** viaja dentro de ella y ya no existe como paquete aparte. **Sin post-procesado de ningún tipo:** bloom, tonemapping o antialiasing deshacen el dithering Bayer y el alfa binario sobre los que está construido todo este documento.

| Ajuste | Valor |
|---|---|
| Sprite Mode | Single (estático) / Multiple (animado) |
| Slice | Grid By Cell Size, igual a la celda |
| Pixels Per Unit | **64** (celda de 96 px = 1.5 u) |
| Filter Mode | Point (no filter) |
| Compression | None |
| Pivot | Bottom para lo que se apoya, Center para el resto |

Números de juego (vida, puntaje, valores) no se hornean en el sprite: van
en un objeto hijo con TextMeshPro. La sombra de suelo es un sprite aparte.

**Tipografía: Jersey 10**, licencia **OFL**, una sola familia para todo
—números sobre el nodo, paneles y menús—, importada a TextMeshPro desde
`Assets/Art/Fonts/`. Tiene `á é í ó ú ñ ü ¡ ¿`, y la UI de
[13](./NOVA_GameDesign_Spec.md#13-controles-y-ui) está escrita en español.
Mezclar dos familias en un juego de este tamaño se nota si no se hace muy bien.

**Y se usa a múltiplos enteros de su tamaño nativo.** Una pixel font renderizada
a un tamaño intermedio se interpola, y entonces el texto es lo único borroso de
una pantalla dibujada entera con `Filter Mode: Point`. Es la misma regla que
prohíbe escalar un sprite por un factor no entero, aplicada al texto.

**Por eso no es Pixelify Sans**, que fue la elegida primero el 2026-09-28 y se
cambió el mismo día: medida sobre el TTF, **ninguna** de sus ~17 000
coordenadas cae en una rejilla, así que no tiene tamaño nativo y la regla de
arriba no se le puede aplicar. Jersey 10 tiene el **100 %** de sus puntos en
una rejilla de 75 unidades sobre un em de 1400: **18.67 px por em**, con una
mayúscula de 10 px, la más legible a 1:1 de las cinco fuentes en rejilla que
se compararon (`art/_preview/fuentes/`). Como 18.67 no es entero, el atlas de
TextMeshPro se genera a **56 pt**, donde cada píxel de diseño son 3 del atlas
exactos, y los textos usan tamaños múltiplos de 18.67.

---

## Reglas de trabajo

- Reutilizar las primitivas de este documento. No reimplementar el modelo de
  luz en cada asset.
- No usar `ImageDraw`, ni antialiasing, ni transparencia parcial. El alfa es
  binario.
- El vector de luz `L` es global e inmutable. Ningún asset lo cambia.
- `Assets/Sprites/` es una carpeta generada. No se edita a mano.
- Un generador por propuesta, `gen_<asset>_v<n>.py`, con el sufijo de versión
  siempre presente. Un generador de nodo declara `NODO` y `VERSION` y escribe
  con `dir_nodo(nodo, version)` en `art/nodes/<nodo>/v<n>/`; uno de overlay de
  estado declara `ESTADO` y `VERSION` y escribe con `dir_estado(estado,
  version)` en `art/states/<estado>/v<n>/` (ver A.1 y A.3 en
  [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md)). Las dos rutas llevan el
  nombre del asset **antes** de la versión: sin él, la v1 de un overlay y la
  v1 de otro caerían en la misma carpeta y «v1» dejaría de querer decir nada.
  La versión elegida la marca ese documento, no la carpeta.
- Al proponer un sprite, mostrar también una previsualización a escala 1×
  **sobre el fondo real** `#0B0E1A`. Lo que importa es cómo se lee en el
  juego, no ampliado y no sobre blanco.
- **Una hoja cuyos frames no son distintos no es una animación.** Antes de
  hornear un bucle hay que contar cuántas imágenes distintas produce de verdad
  a su tamaño: un elemento de pocos píxeles se come el movimiento por
  cuantización. `ART-ST-SCARE` mide 6×8 px y los tres canales que este
  proyecto usa a tamaño pequeño —desplazar, escalar y cambiar de banda— se
  quedaban entre 2 y 9 frames distintos de 16, así que es de un frame
  ([NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md), A.3.3). Se mide antes de
  decidir, no después de dibujar.
- **Lo que no cabe en una escala, cabe en tallas discretas más desplazamiento
  entero.** `Filter Mode: Point` prohíbe escalar por un factor no entero, así
  que un asset que tiene que encajar en varios sitios de distinto tamaño se
  resuelve con unas pocas tallas fijas y un offset en píxeles enteros. Lo
  estrenó el protón (A.4.2) y lo reutiliza el overlay de susto para aterrizar
  en los ojos de los cuatro nodos (A.3.3). Es la alternativa barata a
  multiplicar variantes del sprite.
- **Un asset se comprueba contra lo que va a tener al lado, no contra sí
  mismo.** Un sprite de nodo se valida solo —borde de celda, capa `glow`,
  cierre del bucle—, pero un overlay se valida además contra **otro asset**,
  el sprite sobre el que se aplica, y un glifo contable contra sus propias
  piezas: tres `Z` que se tocan no se cuentan. Esas comprobaciones viven en
  `tools/nova_estados.py` y son las mismas para todas las propuestas de un
  mismo asset; medir cada una a su manera es no compararlas. El generador
  **falla** si alguna no pasa, en vez de imprimir un aviso que nadie lee.
- **Lo que cubre el cuerpo se trama; lo que vive fuera de él, no.** Un tinte o
  un aura de estado se dibuja sobre un sprite ya opaco y tiene que dejarlo
  ver: dithering Bayer, nunca opacidad parcial. Un **glifo** de estado —el
  `zzz` de `Dormido`, el icono de `SinEncender`— no cubre nada, compite con el
  fondo y va **sólido y con contorno**; tramado sería un borrón. La regla del
  alfa binario no cambia: cambia contra qué se está compitiendo.
- **El número de frames de un overlay se mide, no se planifica.** El
  inventario puede escribir «16 frames, bucle» en una fila antes de que el
  asset exista, pero la rejilla decide después: de los nueve overlays de
  A.3, **dos llegaron con bucle planificado y salieron estáticos** —el ojo
  de `Asustado`, que a 6×8 px da entre 2 y 9 frames distintos de 16, y el
  galón del buff, que en 23 px de esquina da 7—. Y hay un caso más sutil:
  **doblar la frecuencia de un latido divide por dos los frames distintos**,
  porque un seno de periodo 1/2 sobre 16 frames repite a los 8; se
  compensa sumando un segundo canal de periodo completo
  ([NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md), A.3.3 y A.3.8).
- **Un overlay se mide por lo que deja ver, no por lo que dibuja.** Un
  sprite de nodo se valida contra sí mismo y un glifo contra el hueco que
  ocupa, pero un **tinte** —lo que cubre el cuerpo entero— se valida contra
  las dos lecturas que no puede tapar: la **cara**, porque la matriz de
  [14.6](./NOVA_Estados_Animaciones.md#146-matriz-de-compatibilidad) lo hace
  convivir con `Asustado` y el susto «siempre debe poder avisar», y el
  **contador de nivel**, porque el nivel se lee solo en el sprite. Y por una
  tercera cosa, que no es tapar sino verse: su **contraste** contra el nodo
  que tiene debajo, porque un tinte del color de la criatura no se ve, y un
  estado que no se ve es un bug (14.0 #5). Las tres viven en
  `verifica_tinte`, en `tools/nova_estados.py`.
- **Un ciclo de aura es radial, no giratorio.** Hacer viajar la fase de la
  onda angular es lo que hace fluir a una atmósfera, pero en un bucle no
  cierra: con armónicos distintos, un desfase devuelve el mismo dibujo solo
  cuando es múltiplo de 2π para todos a la vez, o sea cuando la figura ha
  dado una vuelta entera. Una vuelta en 16 frames a 12 fps es 1.3 s por
  revolución, y eso ya no es una atmósfera que fluye: es un aura que gira,
  que es lo que la regla 6 quiere evitar. El ciclo va en el **radio** y la
  **densidad**, desfasados entre sí
  ([NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md), A.3.5).
- **Un efecto con duración sale a nada; que entre desde nada depende de si
  se prepara o se dispara.** Un bucle se juzga por si cierra; un efecto que
  empieza y acaba, por sus extremos. El **último** frame tiene que estar
  vacío siempre: después viene el estado que lo rodea y un efecto cortado a
  media intensidad hace un salto. El **primero** solo si el efecto *se
  prepara*, como `Reconvirtiendo`, que tarda 3 s en pasar. Uno que *se
  dispara* —`Capturado`, 0.4 s, lanzado por un suceso instantáneo— tiene que
  **arrancar en su pico**: el frame 0 es el más brillante, porque el suceso
  ya ha ocurrido. Con 5 frames, exigirle entrada sería gastar el 20 % del
  asset en no decir nada. Los frames distintos se cuentan sin los vacíos que
  el efecto tenga por contrato, no sin los dos siempre
  ([NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md), A.3.6 y A.3.7).
- **Un overlay no puede quitar nada, así que lo que hay que quitar lo quita
  el animador.** Con alfa binario un PNG que se pega encima solo suma
  píxeles: no borra el sprite de debajo ni lo recolorea. Cuando un estado
  pide que el nodo *cambie* —`Reconvirtiendo` acaba siendo otro tipo y de
  nivel 1—, el cambio es un **cambio de sprite** y el trabajo del asset es
  taparlo mientras ocurre. Para taparlo hay que cubrir la unión de todos los
  sprites a los que se aplica, y esa unión se mide
  ([NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md), A.3.6).
- **Un overlay se comprueba también encima de los otros overlays.** La
  matriz de [14.6](./NOVA_Estados_Animaciones.md#146-matriz-de-compatibilidad)
  no es teórica: `Congelado` + `Infectado` es una casilla «Sí» y los dos son
  trama. Dos campos de Bayer apilados pueden acabar en una mancha en la que
  no se lee ninguno de los dos, y el número de solape no lo dice —el que
  menos píxeles quita puede ser el que peor se distingue—. Se mira en la
  hoja de contacto, con los dos puestos.
- **Antes de dibujar un overlay, contar sobre cuántos nodos va de verdad.**
  «Se aplica sobre cualquier sprite» casi nunca es cierto: `Congelado`,
  `Infectado` y el buff van sobre nodos **encendidos**, y el Asteroide nunca
  lo está, así que son trece sprites y no catorce. La diferencia cambia
  contra qué se comprueba y qué huecos quedan libres.
- Una corona, aura o atmósfera sin cizalla es un error, no una variante.
- Un degradado suave se hace con trama, nunca bajando el alfa.
- **Dos nodos no se separan repintándolos.** Si comparten silueta, simetría,
  material de apéndice y composición, se seguirán pareciendo con cualquier
  paleta. Separarlos es cambiar de construcción, y la hoja donde se comprueba
  es `art/_preview/contraste.png`, con todos los diseños juntos y a 1×: en su
  hoja propia cada nodo se ve consigo mismo y siempre parece distinto. El
  inventario de ejes ya ocupados está en
  [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md), A.8, y se consulta antes
  de empezar un nodo, no después.
- **Un nodo no se construye con las piezas de otro.** Reutilizar las
  primitivas es obligatorio; heredar el *reparto* de primitivas de un nodo
  vecino es lo contrario. El Púlsar descartó una v1 entera por compartir
  construcción con la Estrella, y el Magnetar se diseñó sin tocar ni
  `corona_radial` ni `anillo_orbital` precisamente por eso. Si el nodo nuevo
  pide un material que no existe, se escribe la primitiva.
- **El material es un eje de separación de primer orden**, igual que la
  silueta. El reparto quedó así, y está cerrado: Estrella **luz continua**
  (banda sólida y halo), Púlsar **luz tramada** (`glow`), Magnetar
  **materia que irradia** —ni un píxel de `glow`, silueta y hitbox son la
  misma figura— y Asteroide **materia que solo refleja**, sin una sola
  fuente de luz propia. Cualquier asset nuevo se comprueba contra la tabla
  de ejes de [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md), A.8.
- **El especular se apaga cuando el material no es pulido.** La regla 3 lo
  da por hecho, y para casi todo es correcto: es lo que hace que una
  superficie se lea como material y no como silueta coloreada. Pero una roca
  lijada por mil millones de años de micrometeoritos es **mate**, y un punto
  blanco encima la convierte en canica. Es la única excepción viva a la
  regla 3, va por parámetro explícito (`especular=False`) y no por omisión,
  y se justifica en el generador que la usa.
- **Un cuerpo alargado no lleva especular redondo.** El punto de brillo se
  estira con la forma hasta ser un manchón en mitad de la pieza y se lee
  como un agujero. En un cuerpo así el brillo va en una **arista**, no en la
  superficie.
- **Una huella se mide sobre el ciclo entero**, no sobre el frame 0. Si el
  sprite mueve algo, el máximo no cae en el estático.
