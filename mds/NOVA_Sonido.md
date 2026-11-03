# NOVA — Sonido y música

> Parte de la especificación de **NOVA**. Documento maestro: [NOVA_GameDesign_Spec.md](./NOVA_GameDesign_Spec.md).
> **Sección S.** Inventario de audio: cada sonido con su ID, cuándo suena, qué evento del código lo dispara, cómo debe sonar y su estado. Es al audio lo que [NOVA_Assets_Diseno.md](./NOVA_Assets_Diseno.md) es al arte.
>
> **Ver también:** poderes del Púlsar en la [sección 7](./NOVA_GameDesign_Spec.md#7-poderes-del-púlsar) del maestro · animaciones que acompañan a cada sonido en [14.5](./NOVA_Estados_Animaciones.md#145-animaciones-puntuales-sin-estado) · `AudioDirector` en [15.8](./NOVA_GameDesign_Spec.md#158-presentation) · tarea de integración `T6-10` en [NOVA_Tracking.md](./NOVA_Tracking.md).

---

## Índice

- [S.0 Cómo usar este documento](#s0-cómo-usar-este-documento)
- [S.1 Stack y herramientas](#s1-stack-y-herramientas)
- [S.2 Normas técnicas](#s2-normas-técnicas)
- [S.3 Convención de nombres y carpetas](#s3-convención-de-nombres-y-carpetas)
- [S.4 Principios del diseño sonoro](#s4-principios-del-diseño-sonoro)
- [S.5 Inventario](#s5-inventario)
  - [S.5.1 Poderes del Púlsar](#s51-poderes-del-púlsar--7)
  - [S.5.2 Música: el tema de NOVA](#s52-música-el-tema-de-nova--138-139)
- [S.6 Integración en Unity](#s6-integración-en-unity)
- [S.7 Decisiones abiertas](#s7-decisiones-abiertas)
- [S.8 Resumen de progreso](#s8-resumen-de-progreso)

---

## S.0 Cómo usar este documento

- **Un sonido, una fila.** Cada sonido del juego tiene una fila en [S.5](#s5-inventario) con su ID. Si no tiene fila, no existe.
- **El ID manda**, igual que en el arte: el código, el generador y el archivo se llaman por él ([S.3](#s3-convención-de-nombres-y-carpetas)).
- **Cada sonido cuelga de un evento que ya existe en el código.** La columna *Evento* dice cuál. Si un sonido necesita un evento nuevo, se apunta en [S.7](#s7-decisiones-abiertas) antes de diseñarlo.
- **El inventario crece por bloques.** Hoy están los poderes del Púlsar (S.5.1) y el tema principal (S.5.2). Cada parte nueva del juego que se sonorice entra como un subapartado `S.5.n` con la misma tabla.

**Estados de un sonido:**

| Icono | Significa |
|---|---|
| ⬜ | Inventariado, sin diseñar |
| 🟡 | Hay versión generada, falta la aprobación del usuario |
| 🟢 | Aprobado por el usuario, falta integrarlo en Unity |
| ✅ | Aprobado e integrado en Unity |

---

## S.1 Stack y herramientas

La idea es la misma que con el arte: **los efectos de sonido se generan con Python**, de forma reproducible, y el archivo de audio es salida derivada que no se edita a mano. La música, que necesita oído y composición, se hace en un DAW.

### S.1.1 Resumen

| Capa | Herramienta | Para qué |
|---|---|---|
| **Motor de audio** | **Unity Audio nativo** (Unity 6.6): `AudioSource`, `AudioMixer`, `Audio Random Container` | Reproducir, mezclar y variar los sonidos en el juego |
| **Generación de SFX** | **Python 3.14** (en `.venv`) + `numpy` + `scipy` + `soundfile` + `pedalboard` + `pyloudnorm` | Sintetizar cada efecto desde un script, con semilla fija |
| **Música** | **Python** (partitura en código y MIDI con `mido`) + **Surge XT** (VST3) y las muestras de **VSCO 2 CE** (por **sfizz**), sin interfaz | Componer y producir los temas ([S.1.4](#s14-música)) |
| **Revisión y retoque** | **Audacity** | Escuchar, ver el espectro y medir antes de aprobar |
| **Entorno de desarrollo** | **VS Code** con la extensión de Python | Escribir los generadores, igual que los de arte |

### S.1.2 Motor: Unity Audio nativo, no middleware

**Elegido: el audio que trae Unity.** NOVA es un juego 2D con decenas de sonidos cortos, no cientos, y una música por pantalla. Lo que necesita lo cubre Unity sin dependencias:

- **`AudioMixer`** para los buses (música, efectos, interfaz) y los volúmenes del menú de opciones.
- **`Audio Random Container`** (Unity 6) para que un sonido repetido no suene idéntico: elige entre variaciones y mueve un poco el tono y el volumen.
- **`AudioSource` en pool** dentro de `AudioDirector`, que ya está previsto en [15.8](./NOVA_GameDesign_Spec.md#158-presentation).

**La alternativa es FMOD Studio**, el estándar de la industria para juegos independientes y gratuito por debajo de su umbral de ingresos. Da música adaptativa por capas, parámetros en tiempo real y una herramienta de mezcla separada. **Se reconsidera solo si** la música pasa a reaccionar a la partida por capas (por ejemplo, más intensidad cuando la barra de poder se inclina) o si el número de eventos se dispara. Cambiar después es posible porque todo pasa por `AudioDirector`: ningún otro script reproduce sonido.

Wwise queda descartado: es potente pero está pensado para equipos grandes y su curva de entrada no compensa en un proyecto de este tamaño.

### S.1.3 Generación de SFX con Python

Encaja con el pipeline que ya existe ([NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md)): un generador por sonido en `tools/`, primitivas compartidas en un módulo común y el resultado en una carpeta de salida.

| Librería | Para qué | Nota |
|---|---|---|
| `numpy` | Osciladores, ruido, envolventes, mezclas: el sonido como un array | 2.5.4, la misma del arte |
| `scipy` (`scipy.signal`) | Filtros paso bajo, paso alto y paso banda, barridos de frecuencia | 1.18.1 |
| `soundfile` | Escribir y leer WAV con la profundidad de bits exacta | 0.14.0 (libsndfile 1.2.2) |
| `pedalboard` (Spotify) | Reverb, delay, chorus, distorsión y compresor de calidad de estudio; también carga plugins VST3 | 0.9.26. Comprobado: instala e importa en Python 3.14. **Su `Reverb` no se usa con transitorios secos**: es un Freeverb y sobre las grietas deja un aleteo metálico regular; la sala la hace `nova_audio.sala`, por convolución con ruido que decae |
| `pyloudnorm` | Medir y normalizar la sonoridad (LUFS) para que todos los SFX convivan | 0.2.0 |

**Todo vive en el entorno virtual del proyecto, nada en la máquina**, con la misma regla que el arte ([NOVA_Arte_Tecnica.md](./NOVA_Arte_Tecnica.md), *Principio*). Python 3.14 es lo único instalado fuera; las versiones están fijadas en `requirements.txt`, uno solo para arte y sonido:

```
py -3.14 -m venv .venv
.venv\Scripts\python -m pip install -r requirements.txt
.venv\Scripts\python tools\gen_snd_<sujeto>_v<n>.py
```

**Para explorar antes de escribir el generador** vale cualquier sintetizador de efectos al estilo *sfxr* (jsfxr, ChipTone): sirven para encontrar el carácter de un sonido en minutos. Lo que sale de ahí es referencia, no asset: el sonido final lo produce el script.

### S.1.4 Música

**La música se escribe en código y la tocan instrumentos de verdad**, como los SFX: reproducible, sin pasar por un DAW. Decidido con el usuario el 2026-11-02 ([S.7](#s7-decisiones-abiertas) #5).

| Pieza | Qué es | Licencia |
|---|---|---|
| `tools/nova_tema.py` | La partitura común de un tema: tempo, forma, armonía, motivo y frases | — |
| `tools/nova_musica.py` | El motor: toca las notas, mezcla, masteriza y corta el bucle | — |
| **Surge XT 1.3.4** | Sintetizador profesional en VST3. Se usan sus **parches de fábrica** | GPL-3 |
| **VSCO 2 Community Edition** | Muestras de orquesta real: cuerdas por secciones (sostenido, pizzicato, trémolo), arpa, piano, órgano, flauta, glockenspiel, timbales | CC0 |
| **sfizz 1.2.3** (`sfizz_render.exe`) | Convierte un MIDI en WAV con un instrumento SFZ | BSD-2 |

**Nada se instala en la máquina.** Los plugins son las ediciones portables y las muestras un clon parcial de la rama SFZ, todo en `audio/_libs/` (fuera de git), y lo baja un script: `.venv\Scripts\python tools\instala_audio_libs.py`. `mido` va en `requirements.txt`.

**Cuatro cosas que hay que saber del motor** (las cuatro están resueltas en `nova_musica.py`):

- **`pedalboard` no lee los `.fxp` de Surge.** El estado VST3 de Surge es un contenedor de JUCE con el mismo bloque `sub3` que va dentro del `.fxp` a partir del byte 60, así que se copia ahí. Surge aplica el parche en el siguiente bloque de audio: cada render va precedido de un bloque de calentamiento.
- **Surge no recibe tempo y asume 120 BPM.** Un delay de negra sonaba cada 0.5 s y no cada 0.75. Al cargar el parche, cada parámetro sincronizado se reescribe en absoluto para el tempo del tema: se suma log2(120/BPM) a los tiempos y se resta a las velocidades.
- **Las muestras de VSCO duran de 7 a 12 s y no tienen bucle.** Las notas más largas se parten en tramos que se encadenan. sfizz corta en el último evento MIDI, así que cada pista lleva un evento mudo al final para conservar las colas.
- **Cada pista renderizada se guarda** en `audio/_cache/` (fuera de git) por el hash de lo que la define. Retocar la mezcla no obliga a volver a tocar nada.

**La mezcla se equilibra midiendo, no a ojo.** Cada pista tiene una sonoridad objetivo dentro de la mezcla. Una curva de dB por sección (`ARCO_BUCLE`) da el arco del viaje. Hay una sala por convolución en estéreo y un master con compresión ligera, un limitador y un **limitador de pico real con anticipación**: el de `pedalboard` dejaba pasar los clics de 1 ms por encima de 0 dBTP.

**REAPER, Vital y Furnace** quedan como opción para retocar a mano. Cada versión deja su MIDI con una pista por voz.

---|---|
| **REAPER** | DAW completo, ligero, licencia barata y evaluación sin límite. Exporta stems y bucles con precisión de muestra, que es lo que piden los bucles de juego |
| **Vital** | Sintetizador de tabla de ondas gratuito: pads espaciales, texturas y bajos |
| **Surge XT** | Sintetizador gratuito y de código abierto, complementa a Vital en sonidos más ásperos y retro |

Si se busca un timbre de consola antigua que case con el pixel art, **Furnace** (tracker gratuito) es la opción; queda como alternativa, no como base, porque la estética de NOVA es pixel art con luz y movimiento suave ([13.12](./NOVA_GameDesign_Spec.md#1312-lenguaje-y-movimiento-de-la-ui)), no una restricción de hardware.

---

## S.2 Normas técnicas

### S.2.1 Formato

| | Fuente (generada o exportada) | Importación en Unity |
|---|---|---|
| **SFX** | WAV, 48 kHz, 16 bits, **mono** | *Decompress On Load*, compresión ADPCM |
| **Música** | WAV, 48 kHz, 24 bits, estéreo | *Streaming*, compresión Vorbis (calidad ~70 %) |

Los SFX van en mono porque la posición en el tablero la da el *panning* de `AudioDirector` ([S.6](#s6-integración-en-unity)), no el archivo.

### S.2.2 Sonoridad

| | Objetivo |
|---|---|
| Pico máximo de cualquier archivo | **−1 dBTP** |
| Música, integrado | **−18 LUFS** |
| SFX | Se normalizan por grupo con `pyloudnorm` y se equilibran en el `AudioMixer`, no en el archivo |

### S.2.3 Mezcla

```
Master
├── Music
├── SFX
│   ├── World      poderes, protones, capturas
│   └── UI         botones, menús
└── Ambience       bucles de fondo
```

Cada bus tiene su volumen expuesto para el menú de opciones ([13.10](./NOVA_GameDesign_Spec.md#1310-menú-de-pausa)).

---

## S.3 Convención de nombres y carpetas

### S.3.1 ID

```
SND-<CATEGORÍA>-<SUJETO>[-<VARIANTE>]
```

Sigue el patrón de los IDs de arte y reutiliza sus sujetos para que un sonido y su imagen se encuentren por el mismo sufijo:

| Arte o VFX | Sonido |
|---|---|
| `ART-WLD-BEAM-ZER` (haz) | `SND-WLD-BEAM-ZER` |
| `FX-PWR-ZER` (impacto) | `SND-PWR-ZER` |

Sufijos de poder, los mismos de todo el proyecto: **`ZER`** Cero Absoluto, **`DEC`** Decaimiento, **`WRM`** Agujero de Gusano.

### S.3.2 Carpetas

| Qué | Dónde |
|---|---|
| Primitivas comunes (osciladores, envolventes, filtros, exportación) | `tools/nova_audio.py` |
| Un generador por sonido y versión | `tools/gen_snd_<sujeto>_v<n>.py` (p. ej. `gen_snd_beam_zero_v1.py`) |
| Salida de los generadores | `audio/sfx/<sujeto>/v<n>/` |
| Generador de un tema y versión | `tools/gen_mus_<tema>_v<n>.py` (p. ej. `gen_mus_main_v1.py`) |
| Salida de la música: intro, bucle, stems y MIDI | `audio/music/<tema>/v<n>/` |
| Proyectos de REAPER, si se retoca a mano | `audio/music/<tema>/reaper/` |
| Instrumentos (plugins y muestras), fuera de git | `audio/_libs/`, los baja `tools/instala_audio_libs.py` |
| Pistas renderizadas en caché, fuera de git | `audio/_cache/` |
| Lo que importa Unity | `Assets/Audio/SFX/` y `Assets/Audio/Music/` |

Como en el arte, **varias versiones pueden convivir**; la elegida la marca la columna *Archivo* de [S.5](#s5-inventario).

---

## S.4 Principios del diseño sonoro

1. **El sonido va por poder, no por facción.** Es la misma regla que el color del haz ([7.2.2](./NOVA_GameDesign_Spec.md#722-el-haz)): el sonido anuncia **qué** va a pasar. Un Cero Absoluto suena igual lo lance el Jugador o el Enemigo. **Quién** lo lanza se oye por el lado del que viene ([S.6](#s6-integración-en-unity)).
2. **Sonido e imagen arrancan en el mismo frame.** Cada sonido cuelga del mismo evento que su animación de [14.5](./NOVA_Estados_Animaciones.md#145-animaciones-puntuales-sin-estado) y no la sobrepasa en mucho.
3. **Lo que no pasa no suena.** Si el haz se disipa sin efecto ([7.2.2](./NOVA_GameDesign_Spec.md#722-el-haz)), suena el haz entero y **no suena ningún impacto**, igual que no hay `FX-PWR-*`. Ese silencio es el aviso.
4. **Cada poder tiene una familia de timbre propia** y no se pisan entre ellos:

| Poder | Familia | Color del haz |
|---|---|---|
| Cero Absoluto | Agudo, cristalino, seco: hielo y frío | `#B8F0FF` |
| Decaimiento | Medio, ruidoso, granuloso: radiación y corrosión | `#7CF23F` |
| Agujero de Gusano | Grave, envolvente, con tirón de tono: gravedad | `#9B5CF6` |

5. **Cortos y sin cola larga.** NOVA es tiempo real y varios poderes pueden coincidir; un sonido con cola larga ensucia la mezcla.

---

## S.5 Inventario

### S.5.1 Poderes del Púlsar — [7](./NOVA_GameDesign_Spec.md#7-poderes-del-púlsar)

**Seis sonidos**: uno por haz y uno por impacto, para cada uno de los tres poderes. **Sirven a los dos bandos**: el Jugador y el Enemigo usan los mismos seis ([S.4](#s4-principios-del-diseño-sonoro) #1).

#### Tabla

| ID | Qué suena | Cuándo | Evento en el código | Duración | Arte que acompaña | Archivo | Estado |
|---|---|---|---|---|---|---|---|
| `SND-WLD-BEAM-ZER` | Haz de Cero Absoluto | Al lanzar | `PowerBeamSystem.OnLaunched`, `Type == AbsoluteZero` | 0.32 s | `ART-WLD-BEAM-ZER` | `audio/sfx/beam_zero/v1/snd_wld_beam_zer.wav` | ✅ |
| `SND-WLD-BEAM-DEC` | Haz de Decaimiento | Al lanzar | `PowerBeamSystem.OnLaunched`, `Type == Decay` | 0.32 s | `ART-WLD-BEAM-DEC` | `audio/sfx/beam_decay/v1/snd_wld_beam_dec.wav` | ✅ |
| `SND-WLD-BEAM-WRM` | Haz de Agujero de Gusano | Al lanzar | `PowerBeamSystem.OnLaunched`, `Type == Wormhole` | 0.33 s | `ART-WLD-BEAM-WRM` | `audio/sfx/beam_wormhole/v1/snd_wld_beam_wrm.wav` | ✅ |
| `SND-PWR-ZER` | Congelamiento del nodo | Al terminar el haz | `PowerBeamSystem.OnLanded`, `Type == AbsoluteZero` | 0.70 s | `FX-PWR-ZER` → `Congelado` | `audio/sfx/pwr_zero/v1/snd_pwr_zer.wav` | ✅ |
| `SND-PWR-DEC` | Infección del nodo | Al terminar el haz | `PowerBeamSystem.OnLanded`, `Type == Decay` | 0.70 s | `FX-PWR-DEC` → `Infectado` | `audio/sfx/pwr_decay/v1/snd_pwr_dec.wav` | ✅ |
| `SND-PWR-WRM` | Robo del nodo | Al terminar el haz | `PowerBeamSystem.OnLanded`, `Type == Wormhole` | 1.00 s | `FX-PWR-WRM` → `Capturado` | `audio/sfx/pwr_wormhole/v1/snd_pwr_wrm.wav` | ✅ |

**Línea de tiempo de un lanzamiento:**

```
clic ─┬─ SND-WLD-BEAM-xxx ─────┐            (0.25 s de haz, 7.2.2)
      │                        ├─ SND-PWR-xxx ─────────────┐   (0.7 s o 1.0 s)
      0 s                    0.25 s                     ~0.95–1.25 s
```

El sonido del haz puede pasarse unos milisegundos de los 0.25 s para fundirse con el impacto, pero el **ataque** del impacto cae justo en `OnLanded`.

#### Fichas de diseño

**`SND-WLD-BEAM-ZER` — Haz de Cero Absoluto**
- *Idea:* un rayo de frío que cruza el tablero.
- *Receta:* barrido descendente de seno agudo (~3 kHz → 1.2 kHz) con un brillo de ruido filtrado paso alto; ataque instantáneo y corte limpio.
- *v1, «El aliento polar»* (`tools/gen_snd_beam_zero_v1.py`, 🟢 aprobada el 2026-11-01). Sigue los tres frames del haz: **nucleación** (un soplo de aire helado y el primer chasquido), **escarcha** (una cascada de celesta de cristal, F#7 E7 C#7 A6 G#6 E6 D6, que baja por dentro del silbido junto al chisporroteo de la cristalización) y **quiebra** (tres grietas pequeñas donde estaban los copos). El silbido es viento por un filtro estrecho que cae de 3 kHz a 1.2 kHz, con un hilo de seno dentro. Ningún tono tiembla: el haz es el único trazo rígido del tablero.

**`SND-PWR-ZER` — Congelamiento**
- *Idea:* el nodo se cristaliza: la red de grietas de `ART-ST-FROZEN`.
- *Receta:* crujido de hielo (ráfagas cortas de ruido filtrado paso banda, aleatorias) sobre un tono metálico que se apaga; final seco, sin cola. Puede rimar con las grietas: varios «cracks» encadenados en los primeros 0.3 s.
- *v1, «El vidrio que se calla»* (`tools/gen_snd_pwr_zero_v1.py`, 🟢 aprobada el 2026-11-01). Las grietas son las trece de `ART-ST-FROZEN` en su orden: **cuatro del tronco** (0, 52, 118 y 205 ms, cada vez menores, con el «piu» dispersivo de un lago helado), **cinco ramas** entre ellas y **dos astillas** que caen con el nodo ya quieto (A7 y D7). Debajo, un acorde de copas de vidrio en Re lidio (D5 A5 E6 G#6 D7) que nace temblando —cada nota es una pareja desafinada— y cuyo batido cae a cero en unos 0.4 s: el sonido deja de moverse, como el idle detenido de `Congelado`, y a los 0.6 s se corta en seco. Un golpe grave de 110 → 42 Hz le da peso sin salir de la familia del poder.
- *Escucha de revisión:* `audio/sfx/_preview/zer_lanzamiento.wav`, el haz y el impacto en `OnLanded` (0.25 s), como suenan en juego. Cada generador deja junto al WAV un `*_espectro.png`.

**`SND-WLD-BEAM-DEC` — Haz de Decaimiento**
- *Idea:* un chorro de radiación inestable.
- *Receta:* zumbido en la banda media con modulación rápida de amplitud (tipo contador Geiger) y ruido granuloso.
- *v1, «La vida media»* (`tools/gen_snd_beam_decay_v1.py`, 🟢 aprobada el 2026-11-02). **Cumple la misma ley que el haz** de `ART-WLD-BEAM-DEC`: la mitad de lo que hay se desintegra en cada frame (1, 1/2, 1/4), con una vida media de un frame (83 ms). **320 átomos**, los de una tesela del haz, y **un clic de contador Geiger por cada uno que se desintegra**: al lanzar, el contador fríe a casi tres mil clics por segundo y al final del haz quedan clics sueltos. **La cadena** es un zumbido de dos hebras a distancia de tritón, con su modulación en anillo y el zumbido de 47 Hz de un balastro, y **ese zumbido también se desintegra**: va en granos de 5 ms y cada grano es un átomo con la misma vida media, así que entra entero y acaba en esquirlas. **Cada frame es un núcleo de la cadena** y baja un semitono: Sol# → Sol → Fa#, un lamento cromático. El Sol# es el tritón del Re del Cero Absoluto. Una de cada ocho desintegraciones suelta su partícula beta, un silbido de 7 a 2 kHz. Abre con una rociada tóxica en banda media y un golpe moderado; el grave se reserva para el Agujero de Gusano.

**`SND-PWR-DEC` — Infección**
- *Idea:* la nube verde se posa sobre el nodo y empieza a corroerlo.
- *Receta:* burbujeo descendente (pulsos de ruido filtrado con tono que baja), chasquidos tipo Geiger que se espacian al final.
- *v1, «El coro que se pudre»* (`tools/gen_snd_pwr_decay_v1.py`, 🟢 aprobada el 2026-11-02). **Es el espejo de `SND-PWR-ZER`.** Allí un acorde nace temblando y se queda quieto; aquí un **coro nace afinado y se pudre**. Cada voz es una pareja que empieza al unísono y se desafina cada vez más, con un batido que crece de 0.3 a 11 Hz. El acorde entero se hunde semitono y medio con el temblor irregular de una cinta que se muere. Las voces son Sol#, Re, La y Mib, con una novena menor y dos tritones, y pasan por formantes de «o» a «u»: no suena a sintetizador, gime. Encima va lo que se ve en el nodo: **la salpicadura** (un golpe húmedo de 140 a 55 Hz, un paso bajo resonante que se cierra y seis chapoteos), **la corrosión** (un chisporroteo de ácido que fríe y efervesce), **el burbujeo que baja** (burbujas de Minnaert cada vez más grandes y lentas, de 1.5 kHz a 250 Hz: el nodo se vuelve viscoso), **el recuento** (70 átomos con una vida media de 100 ms; los clics Geiger se espacian igual que la Energía se escapa sola, 7.4) y **las cinco gotas de «El miasma»** (A.3.5), cinco «plinks» que bajan por las notas del coro hasta Sol#. Debajo, el miasma respira. No acaba en seco como el hielo: se apaga, porque la infección se queda.
- *Escucha de revisión:* `audio/sfx/_preview/dec_lanzamiento.wav`.

**`SND-WLD-BEAM-WRM` — Haz de Agujero de Gusano**
- *Idea:* el espacio se dobla entre los dos nodos.
- *Receta:* grave que sube de tono (~60 → 180 Hz) con *flanger* o *chorus* intenso; sensación de succión.
- *v1, «La garganta»* (`tools/gen_snd_beam_wormhole_v1.py`, 🟢 aprobada el 2026-11-02). Sigue los tres frames de «El desgarro». **Desgarro:** la tela del espacio se rasga, con fibras que se rompen en ráfagas sobre un golpe de presión, y del corte saltan chispas. **Garganta:** por la rasgadura se oye **otro cielo**, un acorde lejano de estrellas (Si, Fa#, Do#, Re#) que no es del tablero y llega a través del mismo peine que el grave, como por dos caminos de largo distinto. **Cierre:** el aire se aspira hacia dentro en un barrido al revés que desemboca en `OnLanded`, y de las estrellas quedan tres destellos. Debajo, durante todo el haz, **el tirón** de la receta: un grave de 60 a 180 Hz, con su suboctava y saturado, por un flanger cuyo peine sube con él.

**`SND-PWR-WRM` — Robo del nodo**
- *Idea:* el nodo es arrastrado al otro bando; es el poder más caro y debe sonar el más grande.
- *Receta:* implosión: barrido de tono hacia abajo y luego un golpe grave con reverb corta, como algo que se cierra. Es el único de los seis que llega a 1.0 s.
- *v1, «La fusión»* (`tools/gen_snd_pwr_wormhole_v1.py`, 🟢 aprobada el 2026-11-02). **El robo ya ha pasado en el primer sample**: `ChangeFaction()` se aplica en `OnLanded`. El sonido cuenta lo que dibuja «El relevo» (A.3.7): un anillo que se cierra, otro que sale y su cruce a los 0.2 s. **La física es la de LIGO**, la onda gravitacional de dos agujeros negros que se funden. **Contacto** (0 ms): el último desgarro y la presión. **Espiral** (0–200 ms): el *chirp* de 35 a 280 Hz con la ley real, f ∝ (t_c − t)^(−3/8), y el aire aspirado hacia el nodo. **Fusión** (200 ms, el cruce de los anillos): el golpe más grande del juego, con un sub de 75 a 26 Hz, cuerpo, chasquido, un destello agudo que es el blanco de `FX-CAP` y el *ringdown* del agujero nuevo. **Implosión** (200–850 ms, la receta): un tono que se hunde de 320 a 40 Hz por un peine que baja, y dentro vuelve el otro cielo del haz, **cayendo al rojo**: el corrimiento al rojo gravitacional baja las estrellas una cuarta mientras se apagan. **Cierre** (800 ms): la boca se cierra con una última aspiración y un golpe sordo en una sala corta. Va a −14.3 LUFS, el más fuerte de los seis: el limitador no deja llegar a −14 sin pasar de −1 dBTP.
- *Escucha de revisión:* `audio/sfx/_preview/wrm_lanzamiento.wav`.

### S.5.2 Música: el tema de NOVA — [13.8](./NOVA_GameDesign_Spec.md#138-menú-principal), [13.9](./NOVA_GameDesign_Spec.md#139-selección-de-nivel)

**Una sola pieza, y la más importante del audio del juego:** es lo primero que oye el jugador, antes de tocar un nodo. Pedida por el usuario el 2026-11-02.

#### Tabla

| ID | Qué suena | Dónde | Cuándo empieza y cuándo para | Duración | Archivo | Estado |
|---|---|---|---|---|---|---|
| `SND-MUS-MAIN` | Tema principal de NOVA | `MainMenu` y `LevelSelect` | Empieza al abrir el juego. **Sigue sin cortarse** entre `MainMenu` y `LevelSelect`. Se funde al **lanzar un nivel** (Continuar/Jugar en el menú, Lanzar en la carta) y **vuelve** al regresar de `Game` a cualquiera de las dos pantallas | Intro 12 s + ciclo de 360 s: **v3 → v1 → v2**, 120 s cada una | `audio/music/main/ciclo/mus_main_intro.wav` y `mus_main_ciclo.wav` | ✅ |

**Suenan las tres versiones, en cadena: v3 «Señal», luego v1 «Órbitas», luego v2 «Carta estelar»**, y el ciclo vuelve a empezar. Lo decidió el usuario el 2026-11-02, después de escucharlas. La intro es la de la v3: su Morse «NOVA» es lo primero que se oye. El conjunto sigue el arco del juego a mayor escala: la señal de la cabina, la luz serena y, por último, la orquesta.

**Se pueden encadenar porque comparten tempo, tonalidad y forma**: cada bucle acaba en Mi/Re y empieza en Re maj9, igual que cuando se repite a sí mismo. El empalme se hace al generar y no en Unity, porque Unity no funde dos archivos con precisión de muestra. `tools/gen_mus_main_ciclo.py` une los tres bucles en un solo archivo de 360.000 s, y el principio de cada versión se funde durante 0.5 s desde el principio de la anterior. Como cada bucle cierra sobre sí mismo sin costura, su principio es justo lo que seguiría a su final: la señal continúa y pasa de una versión a otra. Medido, cada empalme salta entre 0.11 y 0.18 veces el salto normal entre dos muestras, lo mismo que la costura de un bucle.

**Qué cuenta como «lanzar» y «volver».** Lanzar es cualquier carga de la escena `Game`. Volver es cualquier carga de `MainMenu` o `LevelSelect` desde `Game`: **Volver al mapa** en la pausa, la victoria o la derrota (13.10, 13.11). **Reiniciar**, **Reintentar**, **Repetir** y **Siguiente** van de `Game` a `Game` y no la hacen sonar. Dentro de la partida no hay música por ahora: la «música por mapa» es otro bloque (S.8).

#### Forma: intro y bucle

| Parte | Compases (4/4) | A 80 BPM | Muestras a 48 kHz | Para qué |
|---|---|---|---|---|
| **Intro** | 4 | 12.0 s | 576 000 | Solo la **primera vez** que se abre el juego. Es la primera impresión: tiene que enganchar antes de que el jugador lea el menú |
| **Bucle** | 40 | 120.0 s | 5 760 000 | Una versión. El **ciclo** son tres bucles seguidos: 120 compases, 360.0 s, 17 280 000 muestras. Al volver de una partida se entra en el ciclo, no en la intro |

- **El tempo se elige para que el bucle cierre en muestras enteras.** Un compás dura 48000 × 240 / BPM muestras, y eso solo es entero con ciertos tempos: 60, 64, 72, 75, **80**, 90, 96, 100 o 120. Con 84, por ejemplo, el bucle se desplazaría una fracción de muestra en cada vuelta. La propuesta es **80 BPM**, que da compases de 3 s exactos. Cada versión puede usar otro tempo de esa lista, siempre que el bucle mida unos 120 s.
- **¿Por qué 120 s?** El jugador pasa por estas pantallas entre unos segundos y un par de minutos: lee el informe del nivel (13.9) y piensa. Un bucle más corto se nota al segundo paso; uno más largo no se llega a oír entero. A 120 s, quien se quede verá volver el tema como una vuelta y no como un corte.
- **Costura sin clic.** La cola de reverb y de delay del último compás se pliega sobre el principio del bucle: el archivo del bucle ya lleva dentro la cola de su propia vuelta anterior. Se comprueba antes de aprobar, uniendo el final con el principio y midiendo el salto en la costura.
- **Dos archivos en el juego:** `ciclo/mus_main_intro.wav` (la intro de la v3) y `ciclo/mus_main_ciclo.wav`. **La intro dura 18 s**: sus 12 s más 6 s de cola. El ciclo arranca a los 12 s exactos en otra `AudioSource` con `PlayScheduled` (precisión de muestra), en bucle, mientras la cola de la intro se apaga encima (S.6). Cada versión deja además su propio `mus_main_intro.wav` y `mus_main_loop.wav` en su carpeta, que son la materia del ciclo.
- **La intro va con la misma ganancia de master que el bucle**, para que no haya salto de volumen al encadenarse.
- **Formato** (S.2): WAV estéreo, 48 kHz y 24 bits, a **−18 LUFS** integrados y pico ≤ −1 dBTP. En Unity va en *Streaming* con Vorbis.

#### Enfoque: qué tiene que decir

Sale del juego, no de un género:

- **Qué es NOVA.** Estrategia en tiempo real con cuerpos celestes. No se dispara: **la Energía fluye** de un cuerpo a otro y el tablero cambia de color poco a poco. Es un juego de **pensar bajo una presión tranquila**: se gana leyendo el tablero, no por reflejos. La música tiene que ser **calma con dirección**, ni nana ni batalla.
- **Dónde suena.** El menú muestra el fondo del Sistema Interior, con la luz de un sol fuera de cuadro que barre el cielo y una Estrella del Jugador grande. La carta estelar muestra los tres mapas: el Sistema Interior (luz), el Cinturón (materia que fluye) y el Espacio Profundo (el vacío que deriva). El tema recorre ese viaje, **de la luz al fondo del espacio**, y vuelve.
- **El lenguaje visual es un instrumento de a bordo** (13.12): marcos de 4 px, cian para lo que puedes hacer y movimiento suave. La música es la de una **nave que navega**, no la de un ejército. Los timbres pueden tener algo de aparato: pulsos, señales, osciladores.
- **Pixel art con luz, no restricción de hardware** (S.1.4). Se permite un guiño 8-bit, pero no chiptune como base.
- **Debajo de la lectura.** En la carta el jugador lee cifras y mecánicas. Sin voces con letra, sin solos que reclamen atención y con densidad media. Hay que dejar sitio entre 2 y 5 kHz para los sonidos de interfaz que vendrán.
- **Cohesión con lo que ya suena.** Los poderes viven en **Re lidio**: el Cero Absoluto en el acorde de Re lidio, el Decaimiento alrededor de su cuarta aumentada (Sol#) y el Agujero de Gusano con «otro cielo» en Si. **El tema propone Re lidio**, el modo del asombro en el cine espacial. Así, todo el mundo sonoro de NOVA comparte casa, y los poderes se oirán como colores de la misma escala que el menú.
- **Un motivo, el de NOVA.** Una frase corta (4 o 5 notas) que se pueda silbar y que vuelva en cada versión. Si se aprueba, será la semilla de la victoria, la derrota y la música por mapa. Su gesto es el del juego: **una nota que se envía a otra**, un salto que asciende y se posa en la cuarta aumentada, como un protón que llega.
- **Física real como material**, igual que en los SFX:
  - **Resonancias orbitales:** arpegios en razones como la 1:2:4 de las lunas de Júpiter (resonancia de Laplace). Tres patrones que se alinean cada pocos compases.
  - **Púlsares:** el pulso de un púlsar real como metrónomo o textura. PSR B0329+54 late cada 0.714 s.
  - **Sonificaciones de la NASA** como referencia de timbre.

#### Tres versiones

Las tres comparten el motivo, la tonalidad, la forma y la duración. Cambia el carácter, para que la elección sea entre tres ideas y no entre tres mezclas:

| Versión | Carácter | Paleta | Referencias de oído |
|---|---|---|---|
| **v1 «Órbitas»** | Asombro sereno: la luz del Sistema Interior | Pads cálidos de tabla de ondas, arpegios en resonancia orbital, bajo blando y una campana de cristal para el motivo | La electrónica cósmica de los 70–80, los menús tranquilos de los juegos de estrategia espacial |
| **v2 «Carta estelar»** | Cinematográfica y emotiva: el viaje entero | Híbrido orquestal: cuerdas, piano, celesta y un órgano con sintetizadores debajo. El motivo pasa de un instrumento a otro | Las bandas sonoras de ciencia ficción con órgano y cuerdas en modo lidio |
| **v3 «Señal»** | Tensión estratégica contenida: el instrumento de a bordo | Un pulso de púlsar como ritmo, un ostinato de bajo sintético, el motivo en un timbre de aparato, texturas de radio y un guiño 8-bit | La electrónica minimalista de los juegos de estrategia modernos |

**Se escuchan las tres y se elige una**, o una mezcla de ideas que da pie a una v4. La elegida pasa a 🟢 y las otras se quedan en sus carpetas, como en el arte.

#### Cómo se produce

Con el motor de [S.1.4](#s14-música). Cada versión es un generador, `tools/gen_mus_main_v<n>.py`, sobre la partitura común de `tools/nova_tema.py`, y deja en `audio/music/main/v<n>/`:

| Archivo | Qué es | En git |
|---|---|---|
| `mus_main_intro.wav`, `mus_main_loop.wav` | La intro y el bucle de la versión: 48 kHz, 24 bits, estéreo | No (unos 40 MB por versión): son la materia del ciclo |
| `ciclo/mus_main_intro.wav`, `ciclo/mus_main_ciclo.wav` | **Los dos archivos del juego**, de `gen_mus_main_ciclo.py` | **Sí**, porque volver a generarlos exige `audio/_libs/` y no sale idéntico bit a bit (unos 110 MB) |
| `ciclo/mus_main_escucha.wav` | La intro y el ciclo entero, para oír los empalmes | No |
| `mus_main_escucha.wav` | **Para escuchar como en el juego**: la intro y el bucle dos veces seguidas, con la costura a la vista | No |
| `mus_main.mid` | La partitura, una pista por voz, para retocar en un DAW | Sí |
| `mus_main_espectro.png` | El espectrograma de la escucha | Sí |

**El material común** (`nova_tema.py`):

- **Re lidio a 80 BPM.** El motivo es Re → La → Sol# → Mi → Fa#. La respuesta, B → Fa# → Mi → Do# → Re. La llamada «NO-VA» es Re → La.
- **El arco por sección**, igual en las tres: A contenida, B crece, C se recoge, A' es lo más alto y D se calma hasta enlazar con A.
- **Armonía:**
  - A: Re maj9 / Mi sobre Re (el II lidio).
  - B: se mueve por Si m9, Fa# m7 y Sol# m7♭5.
  - C: gira a **Sib maj7#11** (de Mi a Sib, un tritón, por su nota común Mi) y pasa por Fa maj7 y Sol m9 hasta La7.
  - A': vuelve a Re por la dominante.
- **Las órbitas:** tres arpegios en **resonancia de Laplace**, como Io, Europa y Ganímedes. Sus periodos van 1:2:4 (semicorchea, corchea y negra) y recorren el acorde con ciclos de 3, 4 y 5 notas, así que se cruzan y se vuelven a alinear.

**Medidas de las tres versiones:** el bucle mide **120.000 s exactos** y va a **−18.0 LUFS**. El salto en la costura queda entre 0.12 y 0.19 veces el salto normal de la señal entre dos muestras, es decir, inaudible. El arco medido, en LUFS por mitad de sección:

| Versión | A | B | C | A' | D | Pico |
|---|---|---|---|---|---|---|
| v1 | −19.4 / −18.8 | −18.1 / −17.7 | −20.2 / −19.6 | −15.1 / −15.3 | −19.1 / −20.9 | −4.2 dBTP |
| v2 | −21.3 / −20.5 | −17.8 / −16.6 | −18.6 / −19.4 | −14.9 / −14.5 | −22.0 / −23.5 | −4.0 dBTP |
| v3 | −20.0 / −19.5 | −17.9 / −17.1 | −19.3 / −18.5 | −15.5 / −15.2 | −19.1 / −21.9 | −1.1 dBTP |

#### Fichas de las versiones

**v1 «Órbitas»** (`tools/gen_mus_main_v1.py`, 🟢 aprobada el 2026-11-02, **segunda del ciclo**). Solo Surge XT, con doce parches de fábrica.
- **Las órbitas** son el corazón: Diamonds arriba, Nice Pluck 1 en medio y Soft Space Oboe Pops abajo. Entran por capas: Ganímedes en A, Europa desde la segunda frase, las tres en el Cinturón y solo la lenta en el Espacio Profundo.
- **El motivo** va en una campana de cristal (Fantasy Bell), que en C pasa a un seno lejano (Sine Lead) y en A' se dobla una octava arriba con una caja de música.
- **Pads:** uno cálido y ancho (MKS-70 Warm Pad), otro con chispas en la luz (Sparkly) y uno distante para C (Distant).
- **Coro y bajo:** en A' un coro (Ooh) canta el contracanto. El bajo es blando en la luz, en pulso en el Cinturón y un sub profundo en C.

**v2 «Carta estelar»** (`tools/gen_mus_main_v2.py`, 🟢 aprobada el 2026-11-02, **tercera del ciclo**). Una pequeña orquesta real de VSCO, con un pad y un sub de Surge debajo, apenas.
- **El motivo pasa de mano en mano.** En la intro, el órgano abre un pedal de Re, los violines entran en trémolo lejano y la flauta lanza la llamada.
- **A:** el **piano** dice el motivo, solo y cerca, mientras el arpa hace las órbitas.
- **B:** la materia fluye en **pizzicato**, con las tres órbitas en violines, violas y chelos, y la flauta canta los envíos.
- **C:** los **chelos** cantan el motivo aumentado en Sib lidio sobre el órgano, con los violines en trémolo arriba. Un redoble de timbal crece hacia la vuelta.
- **A':** toda la orquesta. Los violines llevan el motivo, el glockenspiel lo dobla, el **órgano pleno** sostiene y las violas cantan el contracanto. El timbal marca la llegada a Re.
- **D:** vuelve el piano.

**v3 «Señal»** (`tools/gen_mus_main_v3.py`, 🟢 aprobada el 2026-11-02, **primera del ciclo, con su intro**). Surge XT más síntesis propia: la cabina que escucha el espacio.
- **El pulso es un púlsar:** cada negra es un latido que cruza el gas interestelar y se **dispersa**, con el agudo llegando antes que el grave. Es la misma física que el «piu» de las grietas del Cero Absoluto, a escala galáctica.
- **«NOVA» en código Morse** (`-. --- ...- .-`), con un tono de telegrafía, abre la intro, cruza el Espacio Profundo y vuelve en la calma.
- **Bajo:** un ostinato de semicorcheas (Square Bass) en el Cinturón y en la vuelta.
- **El motivo** va en un seno de oscilador de prueba (Sine Lead), doblado en A' por una onda cuadrada una octava arriba (Pure Square), el guiño 8-bit.
- **C es solo radio:** estática, un silbido heterodino que se desliza y el púlsar lejos, cada dos compases.

---

## S.6 Integración en Unity

Todo pasa por **`AudioDirector`** (Presentation, [15.8](./NOVA_GameDesign_Spec.md#158-presentation)), construido en **`T6-14`** con los sonidos de los poderes, y la música por **`MusicDirector`**, en **`T6-13`**. Las dos ✅ el 2026-11-02. El resto del audio se suma en `T6-10`. Ningún script de Gameplay ni de Systems reproduce sonido: solo emiten eventos, igual que con las animaciones ([15.9](./NOVA_GameDesign_Spec.md#159-comunicación-entre-capas)).

| Pieza | Capa | Qué hace |
|---|---|---|
| `SoundBank` (ScriptableObject) | Data | Asocia cada ID `SND-*` a su clip o a su `Audio Random Container`, su bus y su volumen base. Guarda también el ancho del tablero, el panorama máximo (0.6), las voces por ID (2) y el pool (12). `Assets/Data/Audio/SoundBank.asset` |
| `AudioDirector` | Presentation | Escucha los eventos, pide el clip a `SoundBank` y lo reproduce desde un pool de `AudioSource`. Vive en el objeto `Systems` de `Game` |
| `SoundRules`, `VoicePool` | Presentation | Lógica pura, para los tests: el ID de cada evento, el panorama y qué voz suena cada sonido |
| `MusicSettings` (ScriptableObject) | Data | Los dos archivos de la música, el bus Music, los tiempos del ciclo y los fundidos. `Assets/Data/Audio/MusicSettings.asset` |
| `MusicDirector`, `MusicFlow` | Presentation | La música de los menús; `MusicFlow` decide, sin `AudioSource`, qué hace en cada cambio de escena |
| `NovaMixer` (`Assets/Audio/NovaMixer.mixer`) | — | El mezclador de [S.2.3](#s23-mezcla). Volúmenes expuestos para el menú de opciones: `MasterVolume`, `MusicVolume`, `SfxVolume`, `SfxWorldVolume`, `SfxUiVolume` y `AmbienceVolume` |

**Importación.** *Nova → Audio → Importar audio y crear el mezclador* copia los archivos elegidos de `audio/` a `Assets/Audio/Music/` y `Assets/Audio/SFX/`, que no se versionan (son salida derivada, como `Assets/Sprites/`); crea el mezclador si falta y rellena `MusicSettings` y `SoundBank`. Escribe el `.meta` de cada WAV entero, con los ajustes de [S.2.1](#s21-formato) y **sin normalizar**: Unity normaliza cada clip por defecto, y la intro y el ciclo saltarían de volumen al encadenarse. Después se regeneran las escenas (*Generar escenas de menú* y *Generar escena Game*).

**Para los poderes, dos suscripciones bastan**, porque `PowerBeam` ya trae el tipo de poder, la facción y las posiciones:

| Evento | Sonido | Posición estéreo |
|---|---|---|
| `PowerBeamSystem.OnLaunched(PowerBeam)` | `SND-WLD-BEAM-<beam.Type>` | `beam.From`, el Púlsar que lanza |
| `PowerBeamSystem.OnLanded(PowerBeam)` | `SND-PWR-<beam.Type>` | `beam.To`, el nodo objetivo |
| `PowerBeamSystem.OnDissipated(PowerBeam)` | Ninguno ([S.4](#s4-principios-del-diseño-sonoro) #3) | — |

**Posición estéreo:** el *panning* se calcula con la `x` del punto sobre el ancho del tablero (15 u), suavizado para que nada suene solo en un oído. Así un poder enemigo que sale de la derecha se oye a la derecha: es como se distingue **quién** lanza sin cambiar el sonido.

**Límite de voces:** como mucho dos instancias del mismo ID a la vez; la tercera corta a la más antigua. Si el pool entero está ocupado, se corta la más antigua de todas.

**Pausa y fin de nivel.** Con la partida congelada (`Time.timeScale = 0`), los sonidos que estén sonando se quedan quietos como el tablero y siguen donde iban al reanudar ([18](./NOVA_GameDesign_Spec.md#18-decisiones-tomadas-por-defecto) #50, decidido con el usuario el 2026-11-02).

**La música no vive en una escena.** `SND-MUS-MAIN` suena en `MainMenu` y en `LevelSelect` sin cortarse entre las dos, así que la reproduce una pieza que sobrevive a los cambios de escena (`DontDestroyOnLoad`) y escucha `SceneManager.activeSceneChanged`, que ya existe: no hace falta ningún evento nuevo.

| Escena que se activa | Qué hace la música |
|---|---|
| `MainMenu` o `LevelSelect`, al abrir el juego | La intro de la v3 (18 s con su cola) y, en otra `AudioSource` con `PlayScheduled` a los 12 s exactos, el ciclo en bucle |
| `MainMenu` ↔ `LevelSelect` | Nada: sigue sonando |
| `Game` | Fundido de salida de **1.0 s** ([S.7](#s7-decisiones-abiertas) #6) |
| `MainMenu` o `LevelSelect`, al volver de `Game` | Entra en el principio de la **versión siguiente** a la que sonaba al lanzar, con un fundido de entrada de 1.5 s ([S.7](#s7-decisiones-abiertas) #6). Las versiones empiezan a 0, 120 y 240 s del ciclo. Si se lanzó durante la intro, sonaba la v3 y entra la v1 |

---

## S.7 Decisiones abiertas

| # | Asunto | Propuesta |
|---|---|---|
| 1 | **Agujero de Gusano y la captura genérica.** El impacto del Agujero de Gusano termina en `Capturado` (`FX-CAP`). Cuando exista un sonido de captura genérico, sonarían los dos a la vez | Que `SND-PWR-WRM` **sustituya** al de captura cuando la captura venga de este poder |
| 2 | **¿Distinguir bando por sonido?** Hoy el bando solo se oye por el *panning* | Mantenerlo así; si en pruebas no se lee, añadir una variación leve de tono por facción, nunca un sonido distinto |
| 3 | **¿Suena el haz disipado?** Hoy suena el haz y nada más | Mantenerlo: el silencio es el aviso, como en el arte |
| 4 | **Motor de audio.** Unity nativo frente a FMOD ([S.1.2](#s12-motor-unity-audio-nativo-no-middleware)) | Unity nativo; se reabre si la música se vuelve adaptativa |
| 5 | ~~**Cómo se produce el tema**~~ | ✅ **Cerrada el 2026-11-02** con el usuario: partitura en Python, Surge XT y VSCO 2 CE por sfizz, todo portable en `audio/_libs/` desde el `.venv` ([S.1.4](#s14-música)). Se entrega el MIDI; las pistas sueltas quedan en la caché y no se publican |
| 6 | ~~**Fundidos de la música, y qué versión suena al volver**~~ | ✅ **Cerrada el 2026-11-02** con el usuario, al empezar `T6-13`: salida en 1.0 s al cargar `Game`; al volver, sin la intro y con 1.5 s de fundido, entra el principio de la versión siguiente a la que sonaba al lanzar (v3 → v1 → v2 → v3), para que cada vuelta al menú traiga una versión distinta ([S.6](#s6-integración-en-unity)) |

---

## S.8 Resumen de progreso

| Bloque | Sonidos | ⬜ | 🟡 | 🟢 | ✅ |
|---|---|---|---|---|---|
| [S.5.1 Poderes del Púlsar](#s51-poderes-del-púlsar--7) | 6 | 0 | 0 | 0 | 6 |
| [S.5.2 Música: el tema de NOVA](#s52-música-el-tema-de-nova--138-139) | 1 | 0 | 0 | 0 | 1 |
| **Total** | **7** | **0** | **0** | **0** | **7** |

**S.5.1 está completo** (2026-11-02): los seis sonidos de los poderes están aprobados y esperan su integración en **`T6-14`**, desbloqueada.

**S.5.2 está aprobado** (2026-11-02): `SND-MUS-MAIN` es el ciclo de las tres versiones, v3 → v1 → v2, con la intro de la v3. Falta integrarlo en Unity según [S.6](#s6-integración-en-unity): importar los dos WAV de `audio/music/main/ciclo/` en `Assets/Audio/Music/` (*Streaming*, Vorbis) y la pieza que sobrevive al cambio de escena y encadena intro y ciclo. Es la tarea **`T6-13`** de [NOVA_Tracking.md](./NOVA_Tracking.md), abierta el 2026-11-02; los sonidos de S.5.1 van en **`T6-14`**.

**Bloques por añadir**, a medida que se definan con el usuario: protones y capturas, Magnetar, nodos (encendido, mejora, reconversión), power-ups, interfaz, fin de nivel, ambiente, música por mapa y los temas cortos de victoria y derrota, que saldrán del motivo de `SND-MUS-MAIN`.
