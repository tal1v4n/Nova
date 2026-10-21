"""NOVA - Hoja de contacto. Sirve para juzgar color, no para producir arte.

Monta los sprites sobre el fondo real (#0B0E1A) a escala 1x y ampliada,
porque lo que importa es como se lee en el juego, no ampliado.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_paleta import FONDO, RAMPAS, hex_a_rgb
from nova_core import a_rgba, dir_preview

# (nodo, version, modulo). Anadir una propuesta es anadir una linea: las
# versiones de un mismo nodo conviven hasta que se elige una.
DISENOS = [
    ('asteroide', 1, 'gen_asteroide_v1'),
    ('asteroide', 2, 'gen_asteroide_v2'),
    ('estrella', 1, 'gen_estrella_v1'),
    ('pulsar',   1, 'gen_pulsar_v1'),
    ('pulsar',   2, 'gen_pulsar_v2'),
    ('magnetar', 1, 'gen_magnetar_v1'),
    ('magnetar', 2, 'gen_magnetar_v2'),
]

BG = np.array(hex_a_rgb(FONDO), np.uint8)


def sobre_fondo(rgba):
    """Composicion sobre el fondo del juego. El alfa es binario."""
    a = rgba[..., 3:4] > 0
    return np.where(a, rgba[..., :3], BG).astype(np.uint8)


def rejilla(celdas, cols, escala, hueco=8):
    h, w, _ = celdas[0].shape
    filas = (len(celdas) + cols - 1) // cols
    W = cols * w + (cols + 1) * hueco
    H = filas * h + (filas + 1) * hueco
    hoja = np.tile(BG, (H, W, 1))
    for i, c in enumerate(celdas):
        f, k = divmod(i, cols)
        y = hueco + f * (h + hueco)
        x = hueco + k * (w + hueco)
        hoja[y:y + h, x:x + w] = c
    if escala > 1:
        hoja = np.repeat(np.repeat(hoja, escala, 0), escala, 1)
    return hoja


def apilar(bloques, hueco=16):
    W = max(b.shape[1] for b in bloques)
    alto = sum(b.shape[0] for b in bloques) + hueco * (len(bloques) + 1)
    out = np.tile(BG, (alto, W, 1))
    y = hueco
    for b in bloques:
        x = (W - b.shape[1]) // 2
        out[y:y + b.shape[0], x:x + b.shape[1]] = b
        y += b.shape[0] + hueco
    return out


def hoja_nodo(destino, gen, niveles, facciones=('player', 'enemy')):
    """1x arriba, 4x abajo. El 1x es el que decide: es el tamano de juego."""
    celdas = [sobre_fondo(a_rgba(gen(n, fac)))
              for fac in facciones for n in sorted(niveles)]
    cols = len(niveles)
    x1 = rejilla(celdas, cols, 1, hueco=6)
    x4 = rejilla(celdas, cols, 4, hueco=6)
    Image.fromarray(apilar([x1, x4]), 'RGB').save(destino)
    print('escrito', destino)


def hoja_idle(destino, gen, nivel, faccion='player', frames=16, paso=2):
    """Tira del ciclo idle, para juzgar el barrido y que el bucle cierre."""
    celdas = [sobre_fondo(a_rgba(gen(nivel, faccion, t=i / frames)))
              for i in range(0, frames, paso)]
    Image.fromarray(rejilla(celdas, frames // paso, 3, hueco=4),
                    'RGB').save(destino)
    print('escrito', destino)


def hoja_contraste(destino, disenos, escala=3):
    """Todos los disenos juntos, una fila por cada uno.

    Es la unica hoja que responde a la pregunta que importa: "esto se parece
    demasiado a aquello". Dos nodos nunca se comparan en sus propias hojas,
    donde cada uno se ve consigo mismo y siempre parece distinto; se comparan
    lado a lado y al tamano al que se juegan.
    """
    import importlib
    filas = []
    for nodo, _ver, mod in disenos:
        m = importlib.import_module(mod)
        gen = getattr(m, nodo)
        celdas = [sobre_fondo(a_rgba(gen(n, 'player'))) for n in sorted(m.NIVELES)]
        filas.append(rejilla(celdas, len(celdas), 1, hueco=4))
        filas.append(rejilla(celdas, len(celdas), escala, hueco=4))
    Image.fromarray(apilar(filas), 'RGB').save(destino)
    print('escrito', destino)


# Tableros de muestra: la mezcla de nodos que pide 11.2 para cada seccion.
# No son niveles reales -las coordenadas siguen pendientes, T.7 #2-, son la
# comprobacion de que el fondo deja leer lo que va encima, que es lo unico
# que se le pide a un fondo.
TABLEROS = {
    1: [('asteroide', 0, 'neutral', 260, 520), ('estrella', 1, 'player', 520, 300),
        ('asteroide', 0, 'player', 900, 700), ('estrella', 2, 'enemy', 1480, 380),
        ('magnetar', 1, 'enemy', 1680, 760), ('asteroide', 0, 'neutral', 1180, 180)],
    2: [('asteroide', 0, 'neutral', 300, 760), ('estrella', 2, 'player', 600, 560),
        ('pulsar', 1, 'player', 980, 840), ('magnetar', 2, 'neutral', 1120, 420),
        ('estrella', 3, 'enemy', 1560, 300), ('pulsar', 2, 'enemy', 1740, 640)],
    3: [('estrella', 2, 'player', 300, 640), ('magnetar', 1, 'player', 620, 880),
        ('asteroide', 0, 'neutral', 980, 420), ('magnetar', 4, 'neutral', 1180, 700),
        ('pulsar', 3, 'enemy', 1560, 280), ('estrella', 4, 'enemy', 1720, 820)],
}

GEN_NODO = {'asteroide': 'gen_asteroide_v1', 'estrella': 'gen_estrella_v1',
            'pulsar': 'gen_pulsar_v2', 'magnetar': 'gen_magnetar_v2'}


def hojas_fondo(out):
    """Los tres fondos de la v1 solos y con su tablero de muestra encima.
    Los de la v2 se mueven y sus vistas las escribe `gen_fondos_v2.py`."""
    import importlib
    from nova_core import RAIZ
    d = dir_preview('fondos')
    gen = {k: getattr(importlib.import_module(m), k) for k, m in GEN_NODO.items()}
    for n, piezas in TABLEROS.items():
        bg = np.asarray(Image.open(
            os.path.join(RAIZ, 'art', 'bg', 'fondos', 'v1', f'bg_section{n}.png'))).copy()
        solo = Image.fromarray(bg).resize((1024, 576), Image.NEAREST)
        solo.save(os.path.join(d, f's{n}.png'))
        for nodo, niv, fac, x, y in piezas:
            sp = a_rgba(gen[nodo](niv, fac))
            h, w = sp.shape[:2]
            m = sp[..., 3:4] > 0
            bg[y:y + h, x:x + w] = np.where(m, sp[..., :3], bg[y:y + h, x:x + w])
        Image.fromarray(bg).save(os.path.join(d, f's{n}_tablero.png'))
        print('escrito', os.path.join(d, f's{n}_tablero.png'))


def hoja_paleta(destino, alto=40, ancho=40):
    nombres = list(RAMPAS)
    H = len(nombres) * alto
    W = 7 * ancho
    img = np.zeros((H, W, 3), np.uint8)
    for i, n in enumerate(nombres):
        for j, h in enumerate(RAMPAS[n]):
            img[i * alto:(i + 1) * alto, j * ancho:(j + 1) * ancho] = hex_a_rgb(h)
    Image.fromarray(img, 'RGB').save(destino)
    print('escrito', destino, '|', ' / '.join(nombres))


if __name__ == '__main__':
    out = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(out, exist_ok=True)
    import importlib
    for nodo, ver, mod in DISENOS:
        m = importlib.import_module(mod)
        gen = getattr(m, nodo)
        d = dir_preview(nodo)
        hoja_nodo(os.path.join(d, f'v{ver}.png'), gen, m.NIVELES)
        # Solo los nodos con hoja de 16 frames tienen ciclo que juzgar. Los
        # que resuelven su idle por transform devuelven el mismo lienzo para
        # cualquier `t`, y una tira de 8 celdas identicas no informa de nada.
        if getattr(m, 'IDLE', True):
            hoja_idle(os.path.join(d, f'v{ver}_idle.png'), gen, max(m.NIVELES))
        if getattr(m, 'FACCIONES', None):
            hoja_nodo(os.path.join(d, f'v{ver}_facciones.png'), gen, m.NIVELES,
                      facciones=m.FACCIONES)
    hoja_contraste(os.path.join(out, 'contraste.png'), DISENOS)
    hoja_paleta(os.path.join(out, 'preview_paleta.png'))
    hojas_fondo(out)
