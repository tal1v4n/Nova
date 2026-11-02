"""NOVA - primitivas de audio para los generadores de SFX.

Ref: NOVA_Sonido.md S.1.3 (stack), S.2 (normas), S.3 (nombres y carpetas).

Todo es un array mono de float64 a 48 kHz. Los generadores componen capas con
estas piezas, las mezclan con `place` y exportan con `export`, que deja el
archivo en la norma de S.2: WAV 48 kHz / 16 bits / mono, pico <= -1 dBTP.

La regla para no sonar a sintetizador: **casi nada es un oscilador desnudo**.
El hielo y el vidrio son cuerpos que resuenan cuando algo los excita, asi que
la pieza base es el *modo*: una sinusoide amortiguada que nace de un golpe
(`modal`). El ruido se esculpe con filtros que se mueven (`svf`), y las
grietas llevan dispersion (`disperse`), que es lo que hace "piu" en un lago
helado: el agudo llega antes que el grave.
"""

import os

import numpy as np
import pyloudnorm as pyln
import soundfile as sf
from PIL import Image
from scipy import signal

SR = 48000
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


# --------------------------------------------------------------------------
# Tiempo y notas
# --------------------------------------------------------------------------

def n(seg):
    """Segundos -> muestras."""
    return int(round(seg * SR))


def tiempo(dur):
    return np.arange(n(dur)) / SR


def nota(nombre):
    """'D6', 'G#6', 'Bb5' -> Hz (La4 = 440)."""
    pc = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
    p = pc[nombre[0]]
    resto = nombre[1:]
    if resto[0] == "#":
        p, resto = p + 1, resto[1:]
    elif resto[0] == "b":
        p, resto = p - 1, resto[1:]
    midi = 12 * (int(resto) + 1) + p
    return 440.0 * 2 ** ((midi - 69) / 12)


def db(x):
    return 10 ** (x / 20)


# --------------------------------------------------------------------------
# Envolventes
# --------------------------------------------------------------------------

def env_ad(dur, ataque, caida, curva=1.0):
    """Ataque lineal y caida exponencial (constante de tiempo `caida`)."""
    t = tiempo(dur)
    e = np.exp(-np.maximum(t - ataque, 0) / caida)
    if ataque > 0:
        e *= np.clip(t / ataque, 0, 1) ** curva
    return e


def env_tramos(dur, puntos):
    """Envolvente por tramos: [(t, nivel), ...], interpolada en dB suave."""
    t = tiempo(dur)
    ts, vs = zip(*puntos)
    return np.interp(t, ts, vs)


def fade(x, dentro=0.0, fuera=0.0):
    """Fundidos coseno al principio y al final."""
    x = x.copy()
    if dentro > 0:
        k = n(dentro)
        x[:k] *= 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, k))
    if fuera > 0:
        k = n(fuera)
        x[-k:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, k))
    return x


def cortar(x, desde, largo):
    """Silencia todo a partir de `desde` con un fundido coseno de `largo`."""
    x = x.copy()
    a, b = n(desde), n(desde + largo)
    b = min(b, len(x))
    if a < len(x):
        k = b - a
        x[a:b] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, k))
        x[b:] = 0
    return x


# --------------------------------------------------------------------------
# Fuentes
# --------------------------------------------------------------------------

def ruido(rng, dur):
    return rng.standard_normal(n(dur))


def ruido_rosa(rng, dur):
    """Ruido 1/f por filtrado del blanco en frecuencia."""
    k = n(dur)
    X = np.fft.rfft(rng.standard_normal(k))
    f = np.fft.rfftfreq(k, 1 / SR)
    f[0] = f[1]
    X /= np.sqrt(f)
    y = np.fft.irfft(X, k)
    return y / (np.std(y) + 1e-12)


def modal(f0, razones, amps, decays, dur, rng, beat=None, ataque=0.0008):
    """Cuerpo resonante golpeado: suma de modos amortiguados.

    razones/amps/decays: un valor por modo (decay en segundos, constante de
    tiempo). Fase inicial aleatoria por modo, para que dos golpes iguales no
    sean el mismo array.

    beat=(hz, tau): cada modo va en pareja con un gemelo desafinado `hz` que
    se apaga con constante `tau`. Es el temblor de un vidrio vivo; cuando el
    desafino llega a cero los dos gemelos quedan en fase y el modo se queda
    *quieto*. Es la forma sonora del cero absoluto: el movimiento se acaba.
    """
    t = tiempo(dur)
    y = np.zeros_like(t)
    for r, a, d in zip(razones, amps, decays):
        f = f0 * r
        if f >= SR * 0.45:
            continue
        ph = rng.uniform(0, 2 * np.pi)
        env = a * np.exp(-t / d)
        if beat is None:
            y += env * np.sin(2 * np.pi * f * t + ph)
        else:
            hz, tau = beat
            hz = hz * rng.uniform(0.8, 1.25)
            # fase acumulada del gemelo: termina en 0 respecto al principal
            extra = 2 * np.pi * hz * tau * (np.exp(-t / tau) - 1)
            y += 0.5 * env * (np.sin(2 * np.pi * f * t + ph)
                              + np.sin(2 * np.pi * f * t + ph + extra))
    if ataque > 0:
        y *= np.clip(t / ataque, 0, 1)
    return y


# Modos de una barra libre de vidrio (laminofono, celesta de cristal) y de un
# cuenco o copa. No son armonicos: por eso suenan a objeto y no a oscilador.
VIDRIO_BARRA = [1.0, 2.756, 5.404, 8.933]
VIDRIO_COPA = [1.0, 2.32, 4.25, 6.63, 9.38]


def campanita(f0, dur, rng, brillo=1.0, largo=0.12, beat=None):
    """Una nota de celesta de cristal: barra de vidrio golpeada."""
    amps = [1.0, 0.38 * brillo, 0.16 * brillo, 0.07 * brillo]
    decays = [largo, largo * 0.45, largo * 0.22, largo * 0.12]
    return modal(f0, VIDRIO_BARRA, amps, decays, dur, rng, beat=beat)


# --------------------------------------------------------------------------
# Filtros
# --------------------------------------------------------------------------

def _sos(tipo, fc, orden=2):
    return signal.butter(orden, fc, btype=tipo, fs=SR, output="sos")


def lp(x, fc, orden=2):
    return signal.sosfilt(_sos("lowpass", fc, orden), x)


def hp(x, fc, orden=2):
    return signal.sosfilt(_sos("highpass", fc, orden), x)


def bp(x, lo, hi, orden=2):
    return signal.sosfilt(_sos("bandpass", [lo, hi], orden), x)


def svf(x, fc, q, salida="bp"):
    """Filtro de estado variable (TPT) con corte y Q que pueden moverse.

    fc y q: escalares o arrays del largo de x. Es el filtro que silba: con Q
    alto, ruido blanco por un paso banda que baja suena a viento entre hielo.
    """
    x = np.asarray(x, float)
    fc = np.broadcast_to(np.asarray(fc, float), x.shape)
    q = np.broadcast_to(np.asarray(q, float), x.shape)
    g = np.tan(np.pi * np.clip(fc, 10, SR * 0.45) / SR)
    k = 1.0 / q
    a1 = 1.0 / (1.0 + g * (g + k))
    a2 = g * a1
    a3 = g * a2
    y = np.empty_like(x)
    ic1 = ic2 = 0.0
    for i in range(len(x)):
        v3 = x[i] - ic2
        v1 = a1[i] * ic1 + a2[i] * v3
        v2 = ic2 + a2[i] * ic1 + a3[i] * v3
        ic1 = 2 * v1 - ic1
        ic2 = 2 * v2 - ic2
        if salida == "bp":
            y[i] = v1
        elif salida == "lp":
            y[i] = v2
        else:
            y[i] = x[i] - k[i] * v1 - v2
    return y


def disperse(x, etapas=200, a=-0.7):
    """Cadena de pasatodos de primer orden: retrasa el grave mas que el agudo.

    Un clic que pasa por aqui sale como un chirrido descendente. Es el sonido
    del hielo en tension (el "piu" de un lago helado): la onda viaja por la
    placa y cada frecuencia llega a su tiempo. Retraso en DC por etapa:
    (1-a)/(1+a) muestras.
    """
    b_, a_ = [a, 1.0], [1.0, a]
    y = x
    for _ in range(etapas):
        y = signal.lfilter(b_, a_, y)
    return y


# --------------------------------------------------------------------------
# Hielo
# --------------------------------------------------------------------------

def grieta(rng, talla=1.0, piu=0.0, dur=0.2):
    """Una grieta de hielo: chasquido + resonancia del bloque + "piu".

    talla 0.2 (astilla) .. 1.5 (grieta grande del tronco): mas talla, mas
    grave, mas larga y con mas cuerpo. piu 0..1: cuanto chirrido dispersivo
    lleva detras, el sonido del hielo en tension.
    """
    k = n(dur)
    t = np.arange(k) / SR
    y = np.zeros(k)
    # el chasquido: ruido de 1-2 ms, muy brillante
    clic = rng.standard_normal(k) * np.exp(-t / (0.00035 + 0.0005 * talla))
    y += hp(clic, 1500)
    # el bloque que resuena: modos inarmonicos, sin relacion entre si
    nmod = 5
    fs = np.exp(rng.uniform(np.log(1700), np.log(9500), nmod)) / talla ** 0.6
    for f in fs:
        d = rng.uniform(0.004, 0.018) * talla
        y += (0.18 * rng.uniform(0.4, 1.0) * np.exp(-t / d)
              * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.3)))
    # el crujido: material que se rompe, banda media
    cr = rng.standard_normal(k) * np.exp(-t / (0.004 * talla + 0.002))
    cr *= 1 + 0.8 * (rng.random(k) > 0.85)  # granulado
    y += 0.55 * talla * bp(cr, 600, 3800)
    # el "piu": el mismo golpe recorriendo la placa
    if piu > 0:
        imp = np.zeros(k)
        imp[0] = 1.0
        imp[1:24] = rng.standard_normal(23) * 0.3
        ch = disperse(imp, etapas=int(90 + 160 * talla), a=-0.72)
        ch = bp(ch, 350, 7000)
        ch *= np.exp(-t / (0.03 * talla + 0.01))
        y += piu * 2.2 * ch / (np.max(np.abs(ch)) + 1e-12) * 0.5
    return y / (np.max(np.abs(y)) + 1e-12)


def escarcha(rng, dur, densidad, banda=(5500, 15000)):
    """Chisporroteo de cristalizacion: granos de 1 ms al azar.

    densidad: funcion t -> granos por segundo (array del largo de dur)."""
    k = n(dur)
    p = np.clip(densidad / SR, 0, 1)
    marcas = rng.random(k) < p
    y = np.zeros(k)
    y[marcas] = rng.standard_normal(marcas.sum()) * rng.uniform(0.2, 1.0, marcas.sum())
    g = np.exp(-np.arange(n(0.0012)) / (SR * 0.00025))
    y = np.convolve(y, g)[:k]
    return bp(y, *banda)


def sala(x, rng, cola=0.08, mezcla=0.18, brillo=9000):
    """Sala pequena por convolucion con ruido que decae.

    No se usa el Reverb de pedalboard (Freeverb): con transitorios secos
    como las grietas, sus peines dejan un aleteo metalico a intervalos
    regulares que se ve en el espectrograma como rayas verticales. Un IR de
    ruido es difuso desde el primer sample. `cola` es la constante de
    tiempo; nada de esto pasa de unas decenas de ms (S.4 #5).
    """
    k = n(cola * 6)
    t = np.arange(k) / SR
    ir = rng.standard_normal(k) * np.exp(-t / cola)
    ir *= np.clip(t / 0.004, 0, 1)          # 4 ms antes de la primera reflexion
    ir = lp(hp(ir, 250), brillo)
    ir /= np.sqrt(np.sum(ir ** 2))
    mojado = signal.fftconvolve(x, ir)[:len(x)]
    return x + mezcla * mojado


# --------------------------------------------------------------------------
# Mezcla y salida
# --------------------------------------------------------------------------

def lienzo(dur):
    return np.zeros(n(dur))


def place(buf, x, t0, gain=1.0):
    """Suma `x` en `buf` a partir de t0 segundos (recorta lo que sobre)."""
    i = n(t0)
    if i >= len(buf):
        return buf
    k = min(len(x), len(buf) - i)
    buf[i:i + k] += gain * x[:k]
    return buf


def pico_real(x):
    """Pico real aproximado (sobremuestreo x4), en dBTP."""
    up = signal.resample_poly(x, 4, 1)
    return 20 * np.log10(np.max(np.abs(up)) + 1e-12)


def lufs(x):
    """Sonoridad integrada. Los SFX son mas cortos que el bloque de 400 ms
    de la norma, asi que se mide con bloques de 100 ms."""
    m = pyln.Meter(SR, block_size=0.1)
    return m.integrated_loudness(x)


def normaliza(x, lufs_obj, techo=-1.0):
    """Lleva a `lufs_obj`; si el pico real pasa de `techo`, baja hasta el.

    Antes de rendirse al techo, un limitador rapido recorta solo los
    primeros milisegundos de los chasquidos (unos pocos dB que el oido no
    nota en un transitorio de 1 ms) para que la sonoridad llegue al
    objetivo."""
    from pedalboard import Limiter
    x = x * db(lufs_obj - lufs(x))
    if pico_real(x) > techo:
        lim = Limiter(threshold_db=techo - 1.5, release_ms=30)
        x = lim(x.astype(np.float32), SR).astype(float)
        x = x * db(lufs_obj - lufs(x))
    tp = pico_real(x)
    if tp > techo:
        x = x * db(techo - tp)
    return x


def ruta(*partes):
    p = os.path.join(RAIZ, *partes)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    return p


def export(x, path):
    """WAV 48 kHz / 16 bits / mono (S.2.1). Devuelve las medidas."""
    sf.write(path, x.astype(np.float32), SR, subtype="PCM_16")
    return {"dur": len(x) / SR, "lufs": lufs(x), "dbtp": pico_real(x)}


def espectrograma(x, path, fmax=16000, alto=320, px_por_seg=900):
    """PNG del espectrograma (frecuencia lineal, dB), para revisar a ojo."""
    f, t, Z = signal.stft(x, SR, nperseg=1024, noverlap=1024 - 96)
    m = 20 * np.log10(np.abs(Z) + 1e-9)
    m = m[f <= fmax]
    m = np.clip((m - (m.max() - 80)) / 80, 0, 1)
    ancho = max(64, int(len(x) / SR * px_por_seg))
    img = Image.fromarray((m[::-1] * 255).astype(np.uint8))
    img = img.resize((ancho, alto), Image.BILINEAR)
    a = np.asarray(img, float) / 255
    # rampa hielo: negro -> azul -> cian -> blanco
    r = np.clip(a * 2.2 - 1.2, 0, 1)
    g = np.clip(a * 1.6 - 0.4, 0, 1)
    b = np.clip(a * 1.4, 0, 1)
    rgb = (np.stack([r, g, b], -1) * 255).astype(np.uint8)
    # forma de onda debajo
    onda = np.zeros((90, ancho, 3), np.uint8)
    xs = np.array_split(np.abs(x), ancho)
    pk = np.array([c.max() if len(c) else 0 for c in xs])
    pk = pk / (pk.max() + 1e-12)
    for i, v in enumerate(pk):
        h = int(v * 44)
        onda[45 - h:45 + h + 1, i] = (184, 240, 255)
    Image.fromarray(np.vstack([rgb, onda])).save(path)
