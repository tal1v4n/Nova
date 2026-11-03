"""NOVA - motor de la musica: partitura en codigo, instrumentos reales, bucle.

Ref: NOVA_Sonido.md S.5.2 (el tema), S.2 (normas), S.7 #5 (como se produce).

La partitura se escribe en Python (notas en pulsos), se exporta a MIDI y se
toca con instrumentos de verdad, cargados sin interfaz desde audio/_libs/
(tools/instala_audio_libs.py):

  - SURGE XT (VST3, via pedalboard) con sus parches de fabrica. pedalboard no
    lee .fxp, pero el estado VST3 de Surge es un contenedor de JUCE con el
    mismo bloque `sub3` que va dentro del .fxp desde el byte 60: se copia ahi.
    Surge aplica el parche en el siguiente bloque de audio, asi que cada
    render va precedido de un bloque de calentamiento.
  - SFIZZ (sfizz_render.exe) con las librerias SFZ de VSCO 2 CE: cuerdas,
    arpa, organo, piano, flauta, glockenspiel, timbales. Muestras reales.

Todo es estereo (2, n) float a 48 kHz. Las pistas se mezclan con ganancia,
panorama y envio a una sala por convolucion; el master se lleva a -18 LUFS
con pico <= -1 dBTP (S.2.2).

EL BUCLE NO TIENE COSTURA (S.5.2). Se renderiza el bucle dos veces seguidas y
se toma la segunda pasada, que ya lleva dentro las colas de la primera. Los
sintes tienen fases y LFO libres, asi que la tercera pasada no es identica a
la segunda: el principio del archivo se funde con la continuacion real del
final (lo que sonaria despues de la ultima muestra), y la costura empalma con
lo que el oido espera.
"""

import os
import re
import struct
import subprocess
import tempfile
from dataclasses import dataclass, field

import mido
import numpy as np
import pyloudnorm as pyln
import soundfile as sf
from scipy import signal

SR = 48000
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LIBS = os.path.join(RAIZ, "audio", "_libs")
SURGE_DIR = os.path.join(LIBS, "plugins", "surge-xt", "Surge Synth Team")
SURGE_VST3 = os.path.join(SURGE_DIR, "Surge XT.vst3", "Contents", "x86_64-win",
                          "Surge XT.vst3")
SURGE_PATCHES = os.path.join(SURGE_DIR, "SurgeXTData", "patches_factory")
SFIZZ = os.path.join(LIBS, "plugins", "sfizz", "bin", "Release", "sfizz_render.exe")
VSCO = os.path.join(LIBS, "muestras", "vsco2ce")


# --------------------------------------------------------------------------
# Notas y tiempo
# --------------------------------------------------------------------------

@dataclass
class Nota:
    t: float          # inicio, en pulsos
    dur: float        # duracion, en pulsos
    midi: int
    vel: int = 90


@dataclass
class Tiempo:
    bpm: float

    @property
    def pulso(self):
        return 60.0 / self.bpm

    def s(self, pulsos):
        return pulsos * self.pulso

    def compas(self, n=1):
        return 4 * n * self.pulso


NOMBRES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def m(nombre):
    """'D5', 'G#4', 'Bb3' -> numero MIDI (C4 = 60)."""
    p = NOMBRES[nombre[0]]
    r = nombre[1:]
    if r[0] == "#":
        p, r = p + 1, r[1:]
    elif r[0] == "b":
        p, r = p - 1, r[1:]
    return 12 * (int(r) + 1) + p


def desplaza(notas, pulsos=0.0, semitonos=0, vel=None, escala=1.0):
    """Copia de una frase movida en el tiempo, transportada o con otra
    velocidad. `escala` alarga los tiempos (aumentacion)."""
    return [Nota(pulsos + n.t * escala, n.dur * escala, n.midi + semitonos,
                 vel if vel is not None else n.vel) for n in notas]


# --------------------------------------------------------------------------
# MIDI
# --------------------------------------------------------------------------

TPB = 960


def _eventos(notas, tiempo, canal=0, fin=None):
    ev = []
    for n in notas:
        ev.append((n.t, 1, mido.Message("note_on", note=n.midi, velocity=n.vel,
                                        channel=canal)))
        ev.append((n.t + n.dur, 0, mido.Message("note_off", note=n.midi,
                                                velocity=0, channel=canal)))
    if fin is not None:
        # sfizz_render corta en el ultimo evento: uno mudo al final guarda
        # las colas de las notas
        ev.append((fin, 2, mido.Message("control_change", control=110,
                                        value=0, channel=canal)))
    ev.sort(key=lambda e: (e[0], e[1]))
    return ev


def pista_midi(notas, tiempo, nombre, canal=0, fin=None):
    tr = mido.MidiTrack()
    tr.append(mido.MetaMessage("track_name", name=nombre))
    ultimo = 0
    for t, _, msg in _eventos(notas, tiempo, canal, fin):
        tick = int(round(t * TPB))
        msg.time = tick - ultimo
        tr.append(msg)
        ultimo = tick
    tr.append(mido.MetaMessage("end_of_track", time=0))
    return tr


def exporta_midi(pistas, tiempo, ruta):
    """pistas: {nombre: [Nota]}. Un archivo tipo 1, una pista por voz."""
    f = mido.MidiFile(type=1, ticks_per_beat=TPB)
    meta = mido.MidiTrack()
    meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(tiempo.bpm)))
    meta.append(mido.MetaMessage("time_signature", numerator=4, denominator=4))
    f.tracks.append(meta)
    for i, (nombre, notas) in enumerate(pistas.items()):
        f.tracks.append(pista_midi(notas, tiempo, nombre, canal=i % 16))
    os.makedirs(os.path.dirname(ruta), exist_ok=True)
    f.save(ruta)


# --------------------------------------------------------------------------
# Instrumentos
# --------------------------------------------------------------------------

def _ajusta(x, k):
    """(2, n) recortado o con silencio hasta k muestras."""
    if x.shape[1] >= k:
        return x[:, :k]
    return np.pad(x, ((0, 0), (0, k - x.shape[1])))


def parte_largas(notas, max_pulsos):
    """Parte las notas mas largas que la muestra en tramos que se encadenan.

    Las muestras de VSCO duran de 7 a 12 s y no tienen bucle: una nota mas
    larga se apagaria a mitad. Cada tramo se vuelve a atacar justo cuando
    suelta el anterior, y la cola de suelta tapa el empalme."""
    out = []
    for n in notas:
        t, resto = n.t, n.dur
        while resto > max_pulsos * 1.25:
            out.append(Nota(t, max_pulsos, n.midi, n.vel))
            t += max_pulsos
            resto -= max_pulsos
        out.append(Nota(t, resto, n.midi, n.vel))
    return out


def sfz(instrumento, notas, tiempo, dur_s):
    """Toca `notas` con un .sfz de VSCO (p. ej. 'ViolinEnsSusVib') y
    devuelve (2, dur_s*SR)."""
    clave = ("sfz", 1, instrumento, _clave_notas(notas), tiempo.bpm, round(dur_s, 4))
    return _cacheado(clave, lambda: _sfz_toca(instrumento, notas, tiempo, dur_s))


def _sfz_toca(instrumento, notas, tiempo, dur_s):
    ruta_sfz = os.path.join(VSCO, instrumento + ".sfz")
    with tempfile.TemporaryDirectory() as tmp:
        mid = os.path.join(tmp, "p.mid")
        wav = os.path.join(tmp, "p.wav")
        f = mido.MidiFile(type=1, ticks_per_beat=TPB)
        meta = mido.MidiTrack()
        meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(tiempo.bpm)))
        f.tracks.append(meta)
        fin = dur_s / tiempo.pulso
        f.tracks.append(pista_midi(notas, tiempo, instrumento, fin=fin))
        f.save(mid)
        r = subprocess.run([SFIZZ, "--sfz", ruta_sfz, "--midi", mid, "--wav", wav,
                            "-s", str(SR), "-q", "3", "-p", "128", "--use-eot"],
                           capture_output=True, text=True)
        if r.returncode != 0 or not os.path.exists(wav):
            raise RuntimeError(f"sfizz: {instrumento}: {r.stderr[-400:]}")
        x, sr = sf.read(wav, always_2d=True)
    assert sr == SR
    return _ajusta(x.T.astype(np.float64), int(round(dur_s * SR)))


# --- Surge XT ---------------------------------------------------------------

_B64 = ".ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+"
_surge = None


def _juce_b64(b):
    """MemoryBlock::toBase64Encoding de JUCE: '<n>.' y 6 bits por caracter,
    del bit menos significativo al mas."""
    bits = np.unpackbits(np.frombuffer(b, np.uint8), bitorder="little")
    bits = np.pad(bits, (0, (-len(bits)) % 6))
    v = bits.reshape(-1, 6) @ (1 << np.arange(6))
    return f"{len(b)}." + "".join(_B64[i] for i in v)


def _plugin_surge():
    global _surge
    if _surge is None:
        from pedalboard import load_plugin
        _surge = load_plugin(SURGE_VST3)
        _surge._nova_base = _surge.raw_state
    return _surge


def patch_surge(nombre):
    """'Pads/MKS-70 Warm Pad' -> ruta del .fxp de fabrica."""
    return os.path.join(SURGE_PATCHES, *nombre.split("/")) + ".fxp"


# Que parametros de cada efecto de Surge son tiempos y cuales velocidades,
# para los que pueden ir sincronizados al tempo. Tipo -> {indice: 't'|'r'};
# '*' vale para todos los del efecto.
_FX_SYNC = {
    1: {0: "t", 1: "t", 6: "r"},     # delay: tiempo izq/der, velocidad de modulacion
    2: {0: "t"},                     # reverb: predelay
    11: {0: "t"},                    # reverb 2: predelay
    9: {0: "t", 1: "r"},             # chorus
    12: {"*": "r"},                  # flanger
    3: {"*": "r"},                   # phaser
    4: {"*": "r"},                   # rotary
}


def _al_tempo(xml, bpm, nombre=""):
    """Quita la sincronizacion al tempo del host y la reescribe en absoluto.

    pedalboard no da tempo al plugin y Surge asume 120 BPM: un delay de
    negra sonaria cada 0.5 s en vez de cada 0.75 s a 80 BPM. En Surge los
    tiempos van en log2(segundos) y las velocidades en log2(Hz), relativos a
    120 BPM cuando estan sincronizados: se suma log2(120/bpm) a un tiempo y
    log2(bpm/120) a una velocidad."""
    tipos = {n: int(v) for n, v in re.findall(r'<fx(\d+)_type type="0" value="(\d+)"', xml)}
    dt = np.log2(120.0 / bpm)

    def cambia(mt):
        etiqueta, valor = mt.group(1), float(mt.group(2))
        fx = re.fullmatch(r"fx(\d+)_p(\d+)", etiqueta)
        if fx:
            tabla = _FX_SYNC.get(tipos.get(fx.group(1), -1), {})
            clase = tabla.get(int(fx.group(2)), tabla.get("*"))
        else:
            clase = "r" if "rate" in etiqueta else "t"
        if clase is None:
            raise ValueError(f"{nombre}: {etiqueta} sincronizado y sin tabla")
        valor += dt if clase == "t" else -dt
        return f'<{etiqueta} type="2" value="{valor:.6f}" temposync="0"'
    return re.sub(r'<(\w+) type="2" value="([-0-9.]+)" temposync="1"', cambia, xml)


def carga_patch(p, nombre, bpm=None):
    fxp = open(patch_surge(nombre), "rb").read()
    bloque = fxp[60:]
    if bloque[:4] != b"sub3":
        raise ValueError(f"{nombre}: no es un parche de Surge")
    if bpm is not None:
        # cabecera de Surge: 'sub3', tamano del XML y seis tamanos de tabla
        tam_xml = struct.unpack("<I", bloque[4:8])[0]
        xml = bloque[32:32 + tam_xml].decode("latin-1")      # byte a byte, sin perder nada
        nuevo = _al_tempo(xml, bpm, nombre).encode("latin-1")
        bloque = (bloque[:4] + struct.pack("<I", len(nuevo)) + bloque[8:32]
                  + nuevo + bloque[32 + tam_xml:])
    base = p._nova_base
    xml = base[8:].decode("utf-8")
    nuevo = re.sub(r"<IComponent>.*?</IComponent>",
                   "<IComponent>" + _juce_b64(bloque) + "</IComponent>",
                   xml, flags=re.S).encode("utf-8")
    tam = struct.unpack("<I", base[4:8])[0] - (len(base) - 8) + len(nuevo)
    p.raw_state = base[:4] + struct.pack("<I", tam) + nuevo


CACHE = os.path.join(RAIZ, "audio", "_cache")


def _cacheado(clave, fabrica):
    """Guarda cada pista renderizada por el hash de lo que la define
    (instrumento, notas, duracion, tempo y la version del motor). Retocar la
    mezcla no obliga a volver a tocar nada. audio/_cache/ no se versiona."""
    import hashlib
    h = hashlib.sha1(repr(clave).encode()).hexdigest()[:20]
    f = os.path.join(CACHE, h + ".npy")
    if os.path.exists(f):
        return np.load(f)
    x = fabrica()
    os.makedirs(CACHE, exist_ok=True)
    np.save(f, x.astype(np.float32))
    return x


def _clave_notas(notas):
    return tuple((round(n.t, 5), round(n.dur, 5), n.midi, n.vel) for n in notas)


def surge(patch, notas, tiempo, dur_s, ajustes=None, cc=None):
    clave = ("surge", 2, patch, _clave_notas(notas), tiempo.bpm, round(dur_s, 4),
             tuple(sorted((ajustes or {}).items())), tuple(cc or ()))
    return _cacheado(clave, lambda: _surge_toca(patch, notas, tiempo, dur_s, ajustes, cc))


def _surge_toca(patch, notas, tiempo, dur_s, ajustes=None, cc=None):
    """Toca `notas` con un parche de fabrica de Surge XT.

    ajustes: {parametro: valor} sobre el parche (nombres de pedalboard, p.
    ej. 'a_amp_eg_release'), para afinarlo sin tocar el .fxp.
    cc: [(pulso, control, valor)], automatizacion por MIDI CC."""
    p = _plugin_surge()
    carga_patch(p, patch, tiempo.bpm)
    p([], duration=0.25, sample_rate=SR, num_channels=2, reset=False)
    for k, v in (ajustes or {}).items():
        setattr(p, k, v)
    msgs = []
    for t, _, msg in _eventos(notas, tiempo):
        msgs.append((msg.bytes(), tiempo.s(t)))
    for t, c, v in (cc or []):
        msgs.append((bytes([0xB0, c, v]), tiempo.s(t)))
    msgs.sort(key=lambda e: e[1])
    # todo suelta antes del final
    y = p(msgs, duration=dur_s, sample_rate=SR, num_channels=2,
          buffer_size=512, reset=False)
    p([(bytes([0xB0, 123, 0]), 0.0)], duration=0.5, sample_rate=SR,
      num_channels=2, reset=False)          # all notes off: no arrastra notas
    return _ajusta(np.asarray(y, np.float64), int(round(dur_s * SR)))


# --------------------------------------------------------------------------
# Mezcla
# --------------------------------------------------------------------------

def lienzo(dur_s):
    return np.zeros((2, int(round(dur_s * SR))))


def panorama(x, pan):
    """Paneo de potencia constante sobre estereo: pan -1 .. 1."""
    a = (pan + 1) * np.pi / 4
    return np.vstack([x[0] * np.cos(a) * np.sqrt(2), x[1] * np.sin(a) * np.sqrt(2)])


def db(v):
    return 10 ** (v / 20)


def filtro(x, tipo, fc, orden=2):
    sos = signal.butter(orden, fc, btype=tipo, fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=-1)


def ir_sala(rng, rt60=2.8, predelay=0.02, oscuridad=0.5, ancho=1.0, dur=None):
    """Respuesta de una sala grande, estereo y decorrelada.

    Ruido que decae con rt60 distinto por banda (el agudo se apaga antes,
    `oscuridad` 0..1), primeras reflexiones dispersas y un predelay. Es una
    reverb de convolucion sin aleteo, difusa desde el primer milisegundo."""
    dur = dur or rt60 * 1.3
    k = int(dur * SR)
    t = np.arange(k) / SR
    ir = np.zeros((2, k))
    bandas = [(None, 300, 1.15), (300, 2500, 1.0), (2500, 7000, 0.75 - 0.3 * oscuridad),
              (7000, None, 0.5 - 0.3 * oscuridad)]
    for c in range(2):
        r = rng.standard_normal(k)
        acc = np.zeros(k)
        for lo, hi, f in bandas:
            if lo is None:
                b = filtro(r, "lowpass", hi)
            elif hi is None:
                b = filtro(r, "highpass", lo)
            else:
                b = filtro(r, "bandpass", [lo, hi])
            acc += b * np.exp(-6.91 * t / (rt60 * f))
        ir[c] = acc
    # primeras reflexiones
    for _ in range(24):
        ti = predelay + rng.uniform(0.0, 0.08)
        i = int(ti * SR)
        g = rng.uniform(0.2, 0.6) * np.exp(-ti / 0.06)
        ir[rng.integers(0, 2), i] += g * rng.choice([-1, 1]) * 6
    ir *= np.clip((t - predelay) / 0.01, 0, 1)
    medio = ir.mean(0)
    ir = medio + ancho * (ir - medio)
    return ir / np.sqrt(np.sum(ir ** 2) / 2)


def convolucion(x, ir):
    return np.vstack([signal.fftconvolve(x[c], ir[c])[:x.shape[1]] for c in range(2)])


@dataclass
class Bus:
    """Una mezcla: pistas con ganancia, panorama y envio a la sala."""
    dur_s: float
    pistas: dict = field(default_factory=dict)

    def pon(self, nombre, x, gan_db=0.0, pan=0.0, sala=0.2):
        self.pistas[nombre] = (x, gan_db, pan, sala)

    def mezcla(self, ir):
        seco = lienzo(self.dur_s)
        envio = lienzo(self.dur_s)
        for x, g, p, s in self.pistas.values():
            y = panorama(x, p) * db(g)
            seco += y
            envio += y * s
        return seco + convolucion(envio, ir)


def master(x, lufs=-18.0, techo=-1.0, graves=30):
    """Paso alto suave, compresion de bus ligera, sonoridad y techo."""
    from pedalboard import Compressor, Limiter, Pedalboard
    x = filtro(x, "highpass", graves, orden=2)
    pb = Pedalboard([Compressor(threshold_db=-20, ratio=1.6, attack_ms=30,
                                release_ms=250)])
    x = pb(x.astype(np.float32), SR).astype(np.float64)
    x = x * db(lufs - sonoridad(x))
    if pico_real(x) > techo:
        lim = Limiter(threshold_db=techo - 1.0, release_ms=80)
        x = lim(x.astype(np.float32), SR).astype(np.float64)
        x = x * db(lufs - sonoridad(x))
    tp = pico_real(x)
    if tp > techo:
        x = x * db(techo - tp)
    return x


def sonoridad(x):
    return pyln.Meter(SR).integrated_loudness(x.T)


def pico_real(x):
    up = signal.resample_poly(x, 4, 1, axis=-1)
    return 20 * np.log10(np.max(np.abs(up)) + 1e-12)


# --------------------------------------------------------------------------
# Bucle
# --------------------------------------------------------------------------

def corta_bucle(x, inicio_s, largo_s, funde_s=0.25):
    """El bucle de una mezcla renderizada con el bucle dos veces y algo mas.

    x cubre [.., inicio + 2*largo + funde]. Devuelve [inicio+largo,
    inicio+2*largo) con su principio fundido desde x[inicio+2*largo ...],
    que es lo que de verdad sigue al final: la costura empalma sola."""
    a = int(round((inicio_s + largo_s) * SR))
    n = int(round(largo_s * SR))
    f = int(round(funde_s * SR))
    bucle = x[:, a:a + n].copy()
    cont = x[:, a + n:a + n + f]
    w = np.sin(np.linspace(0, np.pi / 2, f)) ** 2
    bucle[:, :f] = bucle[:, :f] * w + cont * (1 - w)
    return bucle


def salto_costura(bucle):
    """Cuanto salta la senal al volver del final al principio, frente al
    salto tipico entre dos muestras seguidas (1.0 = indistinguible)."""
    d = np.abs(np.diff(bucle, axis=1))
    tipico = np.percentile(d, 99) + 1e-12
    costura = np.abs(bucle[:, 0] - bucle[:, -1]).max()
    return costura / tipico


def exporta(x, ruta, bits=24):
    os.makedirs(os.path.dirname(ruta), exist_ok=True)
    sf.write(ruta, x.T.astype(np.float32), SR, subtype=f"PCM_{bits}")
    return {"dur": x.shape[1] / SR, "lufs": sonoridad(x), "dbtp": pico_real(x)}


def ruta(*partes):
    return os.path.join(RAIZ, *partes)


# --------------------------------------------------------------------------
# Produccion de un tema con intro y bucle (S.5.2)
# --------------------------------------------------------------------------

@dataclass
class Voz:
    """Una pista de la mezcla. `nivel` es su sonoridad objetivo en LUFS
    dentro de la mezcla antes del master: el equilibrio se fija por oido
    medido, no por ganancias a ciegas."""
    nombre: str
    audio: np.ndarray
    nivel: float
    pan: float = 0.0
    sala: float = 0.2


def equilibra(voces, dur_s, ir):
    bus = Bus(dur_s)
    for v in voces:
        medida = sonoridad(v.audio) if np.abs(v.audio).max() > 1e-6 else -120
        gan = 0.0 if medida < -100 else v.nivel - medida
        if gan > 24:
            print(f"  AVISO {v.nombre}: casi muda ({medida:.1f} LUFS), +{gan:.0f} dB")
        bus.pon(v.nombre, v.audio, gan, v.pan, v.sala)
    return bus.mezcla(ir)


def limita_pico_real(x, techo=-1.0, anticipa_ms=1.5, suelta_ms=60.0):
    """Limitador de pico real con anticipacion.

    El de pedalboard mira las muestras y llega tarde a un transitorio de
    1 ms (el clic de un pulsar): deja pasar picos entre muestras por encima
    de 0 dBTP. Este mide el pico sobremuestreado x4, calcula la ganancia que
    haria falta en cada muestra, la adelanta `anticipa_ms` (un minimo
    deslizante) y la suelta despacio. Solo actua donde hace falta."""
    lim = db(techo - 0.1)
    up = np.abs(signal.resample_poly(x, 4, 1, axis=-1)).max(0)
    pk = up[: (len(up) // 4) * 4].reshape(-1, 4).max(1)
    pk = np.pad(pk, (0, x.shape[1] - len(pk)), constant_values=pk[-1] if len(pk) else 0)
    g = np.minimum(1.0, lim / (pk + 1e-12))
    k = max(1, int(anticipa_ms / 1000 * SR))
    from scipy.ndimage import minimum_filter1d
    g = minimum_filter1d(g, size=2 * k + 1, mode="nearest")
    a = np.exp(-1.0 / (suelta_ms / 1000 * SR))
    from scipy.signal import lfilter
    # suelta: la ganancia vuelve hacia 1 con una constante de tiempo; nunca
    # sube por encima de lo que pide el minimo (ataque instantaneo)
    sal = g.copy()
    for _ in range(2):
        suav = lfilter([1 - a], [1, -a], sal, zi=[sal[0] * a])[0]
        sal = np.minimum(g, suav)
    return x * sal


def _master_con(x, ganancia, techo=-1.0, graves=30):
    from pedalboard import Compressor, Limiter, Pedalboard
    x = filtro(x, "highpass", graves, orden=2)
    pb = Pedalboard([Compressor(threshold_db=-20, ratio=1.6, attack_ms=30, release_ms=250)])
    x = pb(x.astype(np.float32), SR).astype(np.float64) * db(ganancia)
    lim = Limiter(threshold_db=techo - 1.0, release_ms=80)
    x = lim(x.astype(np.float32), SR).astype(np.float64)
    return limita_pico_real(x, techo)


def produce_tema(carpeta, prefijo, mezcla_intro, mezcla_bucle, tiempo, intro_s, bucle_s,
                 lufs=-18.0, techo=-1.0, cola_intro=6.0):
    """mezcla_intro/mezcla_bucle: estereo ya mezclado (lineas de nova_tema).
    Masteriza el bucle a `lufs` y aplica la MISMA ganancia a la intro, corta
    el bucle sin costura y exporta intro, bucle y una escucha como en juego."""
    # ganancia del master medida sobre la segunda pasada del bucle
    prueba = _master_con(mezcla_bucle, 0.0, techo)
    a, b = int((bucle_s) * SR), int((2 * bucle_s) * SR)
    gan = lufs - sonoridad(prueba[:, a:b])
    for _ in range(3):       # el limitador come algo: se corrige y se repite
        mb = _master_con(mezcla_bucle, gan, techo)
        err = lufs - sonoridad(mb[:, a:b])
        gan += err
        if abs(err) < 0.1:
            break
    mb = _master_con(mezcla_bucle, gan, techo)
    mi = _master_con(mezcla_intro, gan, techo)
    bucle = corta_bucle(mb, 0.0, bucle_s)
    intro = mi[:, :int((intro_s + cola_intro) * SR)]
    os.makedirs(carpeta, exist_ok=True)
    r_intro = exporta(intro, os.path.join(carpeta, f"{prefijo}_intro.wav"))
    r_bucle = exporta(bucle, os.path.join(carpeta, f"{prefijo}_loop.wav"))
    # escucha: como sonara en juego, intro y el bucle dos veces encadenado
    k0 = int(intro_s * SR)
    esc = np.zeros((2, k0 + 2 * bucle.shape[1]))
    esc[:, :intro.shape[1]] += intro
    esc[:, k0:k0 + bucle.shape[1]] += bucle
    esc[:, k0 + bucle.shape[1]:] += bucle
    esc *= db(min(0.0, techo - pico_real(esc)))
    exporta(esc, os.path.join(carpeta, f"{prefijo}_escucha.wav"), bits=16)
    return {"intro": r_intro, "bucle": r_bucle, "costura": salto_costura(bucle),
            "ganancia_master": gan, "bucle_audio": bucle, "escucha": esc}


def espectrograma_tema(x, ruta_png, fmax=12000, alto=360, px_por_seg=12):
    """Espectrograma de un tema largo, en escala de frecuencia logaritmica."""
    from PIL import Image
    mono = x.mean(0)
    f, t, Z = signal.stft(mono, SR, nperseg=4096, noverlap=4096 - 1024)
    mag = 20 * np.log10(np.abs(Z) + 1e-9)
    ejes = np.geomspace(40, fmax, alto)
    filas = np.array([mag[np.argmin(np.abs(f - e))] for e in ejes])[::-1]
    filas = np.clip((filas - (filas.max() - 70)) / 70, 0, 1)
    ancho = max(200, int(len(mono) / SR * px_por_seg))
    img = Image.fromarray((filas * 255).astype(np.uint8)).resize((ancho, alto), Image.BILINEAR)
    a = np.asarray(img, float) / 255
    rgb = np.stack([np.clip(a * 2.0 - 1.0, 0, 1), np.clip(a * 1.9 - 0.45, 0, 1),
                    np.clip(a * 1.3 + 0.05, 0, 1)], -1)
    Image.fromarray((rgb * 255).astype(np.uint8)).save(ruta_png)
