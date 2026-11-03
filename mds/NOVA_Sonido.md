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
- [S.6 Integración en Unity](#s6-integración-en-unity)
- [S.7 Decisiones abiertas](#s7-decisiones-abiertas)
- [S.8 Resumen de progreso](#s8-resumen-de-progreso)

---

## S.0 Cómo usar este documento

- **Un sonido, una fila.** Cada sonido del juego tiene una fila en [S.5](#s5-inventario) con su ID. Si no tiene fila, no existe.
- **El ID manda**, igual que en el arte: el código, el generador y el archivo se llaman por él ([S.3](#s3-convención-de-nombres-y-carpetas)).
- **Cada sonido cuelga de un evento que ya existe en el código.** La columna *Evento* dice cuál. Si un sonido necesita un evento nuevo, se apunta en [S.7](#s7-decisiones-abiertas) antes de diseñarlo.
- **El inventario crece por bloques.** Hoy solo está el de los poderes del Púlsar. Cada parte nueva del juego que se sonorice entra como un subapartado `S.5.n` con la misma tabla.

**Estados de un sonido:**

| Icono | Significa |
|---|---|
| ⬜ | Inventariado, sin diseñar |
| 🟡 | Hay versión generada, falta la aprobación del usuario |
| 🟢 | Aprobado por el usuario, falta integrarlo en Unity (`T6-10`) |
| ✅ | Aprobado e integrado en Unity |

---

## S.1 Stack y herramientas

La idea es la misma que con el arte: **los efectos de sonido se generan con Python**, de forma reproducible, y el archivo de audio es salida derivada que no se edita a mano. La música, que necesita oído y composición, se hace en un DAW.

### S.1.1 Resumen

| Capa | Herramienta | Para qué |
|---|---|---|
| **Motor de audio** | **Unity Audio nativo** (Unity 6.6): `AudioSource`, `AudioMixer`, `Audio Random Container` | Reproducir, mezclar y variar los sonidos en el juego |
| **Generación de SFX** | **Python 3.14** (en `.venv`) + `numpy` + `scipy` + `soundfile` + `pedalboard` + `pyloudnorm` | Sintetizar cada efecto desde un script, con semilla fija |
| **Música** | **REAPER** + sintetizadores **Vital** y **Surge XT** | Componer y producir los temas |
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

| Herramienta | Por qué |
|---|---|
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
| Proyectos de REAPER | `audio/music/<tema>/` |
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
| `SND-WLD-BEAM-ZER` | Haz de Cero Absoluto | Al lanzar | `PowerBeamSystem.OnLaunched`, `Type == AbsoluteZero` | 0.32 s | `ART-WLD-BEAM-ZER` | `audio/sfx/beam_zero/v1/snd_wld_beam_zer.wav` | 🟢 |
| `SND-WLD-BEAM-DEC` | Haz de Decaimiento | Al lanzar | `PowerBeamSystem.OnLaunched`, `Type == Decay` | 0.32 s | `ART-WLD-BEAM-DEC` | `audio/sfx/beam_decay/v1/snd_wld_beam_dec.wav` | 🟢 |
| `SND-WLD-BEAM-WRM` | Haz de Agujero de Gusano | Al lanzar | `PowerBeamSystem.OnLaunched`, `Type == Wormhole` | ~0.3 s | `ART-WLD-BEAM-WRM` | — | ⬜ |
| `SND-PWR-ZER` | Congelamiento del nodo | Al terminar el haz | `PowerBeamSystem.OnLanded`, `Type == AbsoluteZero` | 0.70 s | `FX-PWR-ZER` → `Congelado` | `audio/sfx/pwr_zero/v1/snd_pwr_zer.wav` | 🟢 |
| `SND-PWR-DEC` | Infección del nodo | Al terminar el haz | `PowerBeamSystem.OnLanded`, `Type == Decay` | 0.70 s | `FX-PWR-DEC` → `Infectado` | `audio/sfx/pwr_decay/v1/snd_pwr_dec.wav` | 🟢 |
| `SND-PWR-WRM` | Robo del nodo | Al terminar el haz | `PowerBeamSystem.OnLanded`, `Type == Wormhole` | ~1.0 s | `FX-PWR-WRM` → `Capturado` | — | ⬜ |

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

**`SND-PWR-WRM` — Robo del nodo**
- *Idea:* el nodo es arrastrado al otro bando; es el poder más caro y debe sonar el más grande.
- *Receta:* implosión: barrido de tono hacia abajo y luego un golpe grave con reverb corta, como algo que se cierra. Es el único de los seis que llega a 1.0 s.

---

## S.6 Integración en Unity

Todo pasa por **`AudioDirector`** (Presentation, [15.8](./NOVA_GameDesign_Spec.md#158-presentation)), que se construye en **`T6-10`**. Ningún script de Gameplay ni de Systems reproduce sonido: solo emiten eventos, igual que con las animaciones ([15.9](./NOVA_GameDesign_Spec.md#159-comunicación-entre-capas)).

| Pieza | Capa | Qué hace |
|---|---|---|
| `SoundBank` (ScriptableObject) | Data | Asocia cada ID `SND-*` a su clip o a su `Audio Random Container`, su bus y su volumen base |
| `AudioDirector` | Presentation | Escucha los eventos, pide el clip a `SoundBank` y lo reproduce desde un pool de `AudioSource` |

**Para los poderes, dos suscripciones bastan**, porque `PowerBeam` ya trae el tipo de poder, la facción y las posiciones:

| Evento | Sonido | Posición estéreo |
|---|---|---|
| `PowerBeamSystem.OnLaunched(PowerBeam)` | `SND-WLD-BEAM-<beam.Type>` | `beam.From`, el Púlsar que lanza |
| `PowerBeamSystem.OnLanded(PowerBeam)` | `SND-PWR-<beam.Type>` | `beam.To`, el nodo objetivo |
| `PowerBeamSystem.OnDissipated(PowerBeam)` | Ninguno ([S.4](#s4-principios-del-diseño-sonoro) #3) | — |

**Posición estéreo:** el *panning* se calcula con la `x` del punto sobre el ancho del tablero (15 u), suavizado para que nada suene solo en un oído. Así un poder enemigo que sale de la derecha se oye a la derecha: es como se distingue **quién** lanza sin cambiar el sonido.

**Límite de voces:** como mucho dos instancias del mismo ID a la vez; la tercera corta a la más antigua.

---

## S.7 Decisiones abiertas

| # | Asunto | Propuesta |
|---|---|---|
| 1 | **Agujero de Gusano y la captura genérica.** El impacto del Agujero de Gusano termina en `Capturado` (`FX-CAP`). Cuando exista un sonido de captura genérico, sonarían los dos a la vez | Que `SND-PWR-WRM` **sustituya** al de captura cuando la captura venga de este poder |
| 2 | **¿Distinguir bando por sonido?** Hoy el bando solo se oye por el *panning* | Mantenerlo así; si en pruebas no se lee, añadir una variación leve de tono por facción, nunca un sonido distinto |
| 3 | **¿Suena el haz disipado?** Hoy suena el haz y nada más | Mantenerlo: el silencio es el aviso, como en el arte |
| 4 | **Motor de audio.** Unity nativo frente a FMOD ([S.1.2](#s12-motor-unity-audio-nativo-no-middleware)) | Unity nativo; se reabre si la música se vuelve adaptativa |

---

## S.8 Resumen de progreso

| Bloque | Sonidos | ⬜ | 🟡 | 🟢 | ✅ |
|---|---|---|---|---|---|
| [S.5.1 Poderes del Púlsar](#s51-poderes-del-púlsar--7) | 6 | 2 | 0 | 4 | 0 |
| **Total** | **6** | **2** | **0** | **4** | **0** |

**Bloques por añadir**, a medida que se definan con el usuario: protones y capturas, Magnetar, nodos (encendido, mejora, reconversión), power-ups, interfaz, fin de nivel, ambiente y música por mapa.
