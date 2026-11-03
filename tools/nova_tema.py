"""NOVA - el tema principal: el material que comparten las tres versiones.

Ref: NOVA_Sonido.md S.5.2. Aqui vive la partitura comun -tempo, forma,
armonia, el motivo de NOVA y sus frases-; cada gen_mus_main_v<n>.py decide
con que instrumentos y con que densidad se toca.

RE LIDIO, la escala de los poderes: el Cero Absoluto suena en su acorde, el
Decaimiento gira en su cuarta aumentada (Sol#) y el Agujero de Gusano abre
otro cielo. El menu es la casa de esos colores.

EL MOTIVO: Re -> La -> Sol# -> Mi -> Fa#. Re se ENVIA a La (un salto de
quinta, el gesto del juego: la Energia que sale de un nodo hacia otro) y se
posa en Sol#, la cuarta aumentada, el color lidio que suena suspendido: un
proton que llega. Ritmo: da-DAA | da-ta-DAA.

LA FORMA es el viaje de la carta estelar (13.9), de la luz al fondo y vuelta:

  intro  4 c.  la primera vez: un pedal que se abre y la llamada Re-La
  A      8 c.  SISTEMA INTERIOR, la luz: el motivo y su respuesta
  B      8 c.  EL CINTURON, materia que fluye: «envios» que suben de quinta
               en quinta, los arpegios orbitales se aceleran
  C      8 c.  ESPACIO PROFUNDO: el giro a Sib (de Mi a Sib, un triton, por
               su nota comun), el motivo aumentado y transportado a Sib lidio
  A'     8 c.  la vuelta: el motivo arriba, el punto mas alto, por La7 -> Re
  D      8 c.  calma: solo la llamada Re-La y su eco; Mi/Re devuelve a A

40 compases de bucle a 80 BPM = 120.0 s exactos (5 760 000 muestras).
"""

import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nova_musica import Nota, Tiempo, desplaza, m  # noqa: E402

T = Tiempo(80)
INTRO_C = 4
BUCLE_C = 40
INTRO_P = INTRO_C * 4          # pulsos
BUCLE_P = BUCLE_C * 4

# --------------------------------------------------------------------------
# Armonia: bajo y voicing (de cerca, para pads y cuerdas) por acorde
# --------------------------------------------------------------------------

ACORDES = {
    "Dmaj9":     ("D2",  ["D3", "A3", "C#4", "E4", "F#4"]),
    "E/D":       ("D2",  ["D3", "B3", "E4", "G#4"]),
    "Bm9":       ("B1",  ["F#3", "A3", "C#4", "D4"]),
    "E/G#":      ("G#2", ["E3", "B3", "E4", "G#4"]),
    "F#m7":      ("F#2", ["F#3", "A3", "C#4", "E4"]),
    "Esus4":     ("E2",  ["E3", "A3", "B3", "E4"]),
    "E":         ("E2",  ["E3", "G#3", "B3", "E4"]),
    "A/C#":      ("C#2", ["C#3", "A3", "C#4", "E4"]),
    "Dmaj7":     ("D2",  ["D3", "A3", "C#4", "F#4"]),
    "G#m7b5":    ("G#2", ["F#3", "B3", "D4", "G#4"]),
    "Amaj7":     ("A1",  ["G#3", "C#4", "E4", "A4"]),
    "Asus2":     ("A2",  ["A3", "B3", "E4", "A4"]),
    "Bbmaj7#11": ("Bb1", ["F3", "A3", "D4", "E4"]),
    "Fmaj7/A":   ("A1",  ["F3", "C4", "E4", "A4"]),
    "Gm9":       ("G1",  ["F3", "A3", "Bb3", "D4"]),
    "A7sus4":    ("A1",  ["G3", "D4", "E4", "A4"]),
    "A7":        ("A1",  ["G3", "C#4", "E4", "A4"]),
}

INTRO = ["Dmaj9", "E/D", "Bbmaj7#11", "E/D"]
SECCIONES = {
    "A":  ["Dmaj9", "E/D", "Dmaj9", "E/D", "Bm9", "E/G#", "F#m7", "Esus4"],
    "B":  ["Bm9", "A/C#", "Dmaj7", "E/D", "F#m7", "G#m7b5", "Amaj7", "E"],
    "C":  ["Bbmaj7#11", "Bbmaj7#11", "Fmaj7/A", "Fmaj7/A", "Gm9", "Gm9", "A7sus4", "A7"],
    "A'": ["Dmaj9", "E/D", "Bm9", "E/G#", "F#m7", "G#m7b5", "Asus2", "E"],
    "D":  ["Dmaj9", "E/D", "Bm9", "E/D", "F#m7", "E/G#", "Asus2", "E/D"],
}
ORDEN = ["A", "B", "C", "A'", "D"]
BUCLE = [a for s in ORDEN for a in SECCIONES[s]]
assert len(BUCLE) == BUCLE_C


def inicio_seccion(nombre):
    """Pulso de inicio de una seccion dentro del bucle."""
    return 32 * ORDEN.index(nombre)


def bajo_de(acorde):
    return m(ACORDES[acorde][0])


def voicing(acorde, octava=0):
    return [m(n) + 12 * octava for n in ACORDES[acorde][1]]


# --------------------------------------------------------------------------
# El motivo y las frases (pulsos relativos al inicio de su seccion)
# --------------------------------------------------------------------------

def frase(*notas, vel=92):
    return [Nota(t, d, m(n), vel) for t, d, n in notas]


MOTIVO = frase((0, 1.5, "D5"), (1.5, 2.5, "A5"), (4, 1.5, "G#5"), (5.5, 0.5, "E5"),
               (6, 2, "F#5"))
RESPUESTA = frase((0, 1.5, "B4"), (1.5, 2.5, "F#5"), (4, 1.5, "E5"), (5.5, 0.5, "C#5"),
                  (6, 2, "D5"))
LLAMADA = frase((0, 1.5, "D5"), (1.5, 6.5, "A5"))     # el «NO-VA»: Re se envia a La

MELODIA = {
    "A": (MOTIVO + desplaza(RESPUESTA, 8)
          + frase((16, 1.5, "F#5"), (17.5, 2.5, "C#6"), (20, 1.5, "B5"), (21.5, 0.5, "G#5"),
                  (22, 2, "A5"), (24, 2, "B5"), (26, 1, "A5"), (27, 1, "G#5"), (28, 4, "E5"))),
    # envios que suben de quinta en quinta: Fa#-Do#, La-Mi, Do#-Sol#, Mi-Si
    "B": (frase((0, 1.5, "F#4"), (1.5, 6.5, "C#5"), (8, 1.5, "A4"), (9.5, 6.5, "E5"),
                (16, 1.5, "C#5"), (17.5, 6.5, "G#5"), (24, 1.5, "E5"), (25.5, 2.5, "B5"),
                (28, 4, "G#5"), vel=84)),
    # el motivo aumentado al doble y en Sib lidio, y la deriva
    "C": (desplaza(MOTIVO, 0, -4, vel=80, escala=2.0)
          + frase((16, 6, "A4"), (22, 2, "G4"), (24, 4, "E4"), (28, 2, "C#5"), (30, 2, "E5"),
                  vel=78)),
    "A'": (desplaza(MOTIVO, 0, vel=104) + desplaza(RESPUESTA, 8, vel=100)
           + frase((16, 1.5, "F#5"), (17.5, 2.5, "C#6"), (20, 1.5, "B5"), (21.5, 0.5, "G#5"),
                   (22, 2, "F#5"), (24, 2, "A5"), (26, 2, "B5"), (28, 4, "G#5"), vel=104)),
    "D": desplaza(LLAMADA, 0, vel=80) + desplaza(LLAMADA, 8, -3, vel=72),
}
MELODIA_INTRO = desplaza(LLAMADA, 8, vel=78)

# Contracanto para la vuelta (A'): notas largas por debajo del motivo
CONTRACANTO = frase((0, 4, "F#4"), (4, 4, "G#4"), (8, 4, "F#4"), (12, 4, "E4"),
                    (16, 4, "C#4"), (20, 4, "D4"), (24, 4, "E4"), (28, 4, "B3"), vel=80)


def melodia_bucle(secciones=ORDEN):
    """La melodia de todo el bucle (pulsos desde el inicio del bucle)."""
    out = []
    for s in secciones:
        out += desplaza(MELODIA[s], inicio_seccion(s))
    return out


# --------------------------------------------------------------------------
# Generadores de acompanamiento
# --------------------------------------------------------------------------

def acordes_notas(lista, vel=70, octava=0, ligar=True, desde=0.0):
    """Un acorde por compas; los repetidos se ligan para no reatacar."""
    out = []
    i = 0
    while i < len(lista):
        j = i
        while ligar and j + 1 < len(lista) and lista[j + 1] == lista[i]:
            j += 1
        for n in voicing(lista[i], octava):
            out.append(Nota(desde + 4 * i, 4 * (j - i + 1), n, vel))
        i = j + 1
    return out


def bajo_notas(lista, patron="redonda", vel=84, octava=0, desde=0.0, rng=None):
    """Bajo por compas. patron: 'redonda', 'blancas', 'corcheas', 'pulso'
    (negra con puntillo + corchea + blanca, el pulso de un nodo) u
    'ostinato' (semicorcheas raiz-quinta-octava)."""
    out = []
    for i, a in enumerate(lista):
        r = bajo_de(a) + 12 * octava
        t0 = desde + 4 * i
        if patron == "redonda":
            out.append(Nota(t0, 4, r, vel))
        elif patron == "blancas":
            out += [Nota(t0, 2, r, vel), Nota(t0 + 2, 2, r + (12 if i % 2 else 0), vel - 8)]
        elif patron == "corcheas":
            out += [Nota(t0 + k * 0.5, 0.45, r, vel - (0 if k % 2 == 0 else 14)) for k in range(8)]
        elif patron == "pulso":
            out += [Nota(t0, 1.5, r, vel), Nota(t0 + 1.5, 0.5, r, vel - 16),
                    Nota(t0 + 2, 2, r + 12 if i % 2 else r, vel - 6)]
        elif patron == "ostinato":
            fig = [0, 7, 12, 7, 0, 7, 12, 14, 0, 7, 12, 7, 0, 12, 7, 12]
            out += [Nota(t0 + k * 0.25, 0.22, r + fig[k], vel - (0 if k % 4 == 0 else 18))
                    for k in range(16)]
    return out


def orbitas(lista, desde=0.0, vel=(66, 60, 56), registros=(2, 1, 0), rng=None):
    """Tres arpegios en RESONANCIA DE LAPLACE, como Io, Europa y Ganimedes:
    sus periodos van 1:2:4 (semicorchea, corchea, negra) y recorren el
    acorde con ciclos de 3, 4 y 5 notas, asi que se cruzan y se vuelven a
    alinear como tres lunas. Devuelve (io, europa, ganimedes)."""
    lunas = []
    for paso, ciclo, reg, v in zip((0.25, 0.5, 1.0), (3, 4, 5), registros, vel):
        out = []
        k = 0
        for i, a in enumerate(lista):
            tonos = sorted(voicing(a, reg))
            tonos = tonos + [tonos[0] + 12, tonos[1] + 12]
            for s in range(int(4 / paso)):
                n = tonos[[0, 2, 1, 3, 4][k % ciclo] % len(tonos)]
                acento = 10 if s % int(1 / paso if paso < 1 else 1) == 0 else 0
                out.append(Nota(desde + 4 * i + s * paso, paso * 0.9, n, v + acento))
                k += 1
        lunas.append(out)
    return tuple(lunas)


MORSE = {"N": "-.", "O": "---", "V": "...-", "A": ".-"}


def morse_nova(nota, desde=0.0, unidad=0.25, vel=78):
    """«NOVA» en codigo Morse: punto = 1 unidad, raya = 3, hueco entre
    senales 1, entre letras 3. 39 unidades: menos de dos compases y medio
    en semicorcheas. La senal de la v3."""
    out = []
    t = desde
    for i, letra in enumerate("NOVA"):
        for j, s in enumerate(MORSE[letra]):
            d = unidad * (1 if s == "." else 3)
            out.append(Nota(t, d * 0.92, nota, vel))
            t += d + unidad
        t += 2 * unidad
    return out


def humaniza(notas, rng, ms=8, vel=5):
    """Pequenas desviaciones de tiempo y fuerza, para instrumentos tocados."""
    dp = ms / 1000 / T.pulso
    return [Nota(max(0.0, n.t + rng.uniform(-dp, dp)), n.dur, n.midi,
                 int(np.clip(n.vel + rng.integers(-vel, vel + 1), 1, 127))) for n in notas]


# --------------------------------------------------------------------------
# Linea de tiempo del render (S.5.2: intro aparte, bucle dos veces y algo)
# --------------------------------------------------------------------------

COLA_S = 6.0          # cola de la intro y margen del render


def linea_bucle(notas_bucle, extra_compases=2):
    """Coloca el bucle dos veces seguidas y el principio de una tercera,
    para cortar el archivo de la segunda pasada (nova_musica.corta_bucle).
    Pulsos desde 0 = inicio de la primera pasada."""
    out = list(notas_bucle)
    out += desplaza(notas_bucle, BUCLE_P)
    out += [n for n in desplaza(notas_bucle, 2 * BUCLE_P) if n.t < 2 * BUCLE_P + 4 * extra_compases]
    return out


DUR_RENDER_BUCLE = T.s(2 * BUCLE_P) + T.compas(2) + COLA_S
DUR_RENDER_INTRO = T.s(INTRO_P) + COLA_S


# --------------------------------------------------------------------------
# Dinamica: el arco del viaje (dB sobre la mezcla, antes del master)
# --------------------------------------------------------------------------

# (segundo dentro del bucle, dB). Las capas ya dan parte del arco; esto lo
# subraya. El final (D) acaba donde empieza A, para que la vuelta no salte.
ARCO_BUCLE = [(0.0, -3.0), (22.0, -2.0),            # A, la luz: contenida
              (24.0, -1.0), (46.0, 0.0),            # B, el Cinturon: crece
              (48.0, -1.5), (66.0, -1.5), (71.0, 0.5),   # C, recogida y subida
              (72.0, 1.5), (94.0, 2.0),             # A', lo mas alto
              (96.5, -1.0), (120.0, -3.0)]          # D, calma hasta A
ARCO_INTRO = [(0.0, -9.0), (9.0, -1.5), (12.0, -3.0), (18.0, -3.0)]


def aplica_arco(x, puntos, periodo=None):
    """Multiplica `x` (2, n) por la curva de dB. Con `periodo` la curva se
    repite (las dos pasadas del bucle y su continuacion)."""
    t = np.arange(x.shape[1]) / 48000
    if periodo:
        t = t % periodo
    ts, vs = zip(*puntos)
    g = 10 ** (np.interp(t, ts, vs) / 20)
    return x * g


def en(notas, *secciones):
    """Las notas cuyo inicio cae en esas secciones del bucle."""
    rangos = [(inicio_seccion(s), inicio_seccion(s) + 32) for s in secciones]
    return [n for n in notas if any(a <= n.t < b for a, b in rangos)]


def compases(notas, desde, hasta):
    """Las notas entre dos compases del bucle (1 = el primero, inclusive)."""
    return [n for n in notas if 4 * (desde - 1) <= n.t < 4 * hasta]
