"""NOVA - Vistas del idle de los nodos. Sirven para juzgar, no son arte de juego.

    py tools/preview_idle.py

Escribe en art/_preview/idle/:

    estrella_x4.gif      los cinco niveles de la Estrella, Jugador y Enemigo
    pulsar_x4.gif        los tres del Pulsar
    tablero_x2.gif       un trozo de tablero con los cuatro tipos a la vez
    tablero_frames.png   los 32 frames del tablero en tira, para mirarlos quietos

EL TABLERO ES LA REFERENCIA DE `NodeAnimator` (T6-02). Lo que aqui se calcula
es exactamente lo que Unity tiene que reproducir, con las mismas constantes:

    cadencia      12 fps para todo, tambien para el cabeceo: un nodo no se
                  mueve entre dos frames de la rejilla de tiempo del proyecto
                  (contrato, Animacion)
    hoja          Estrella y Pulsar, 16 frames en bucle
    cabeceo       Asteroide 1 px, Magnetar 2 px, vertical, en pixeles enteros
                  y en un ciclo de 32 frames (2.67 s): `round(A sin 2 pi f/32)`
    desfase       cada nodo arranca su ciclo en un frame que sale de su
                  posicion, no del azar: `desfase(px, py)`. Ocho estrellas
                  latiendo al unisono son una sola estrella copiada ocho veces
                  (la misma regla que 14.9 da a las lineas de envio), y un
                  desfase por posicion es determinista: la misma partida se ve
                  igual dos veces y los tests pueden fijarlo

`NodeAnimator.IdlePhase` y `BobPixels` son la traduccion literal de
`desfase` y `cabeceo`; si cambia una, cambia la otra.
"""

import importlib
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, a_rgba
from nova_paleta import FONDO, hex_a_rgb

FPS = 12
HOJA = 16           # frames de una hoja de idle
CICLO_CABECEO = 32  # frames de un cabeceo
CABECEO_PX = {'ast': 1, 'mag': 2}
CELDA = 96
BG = np.array(hex_a_rgb(FONDO), np.uint8)


def desfase(px, py):
    """Frame de arranque de un nodo, por su posicion en pixeles de mundo.

    Hash entero de 32 bits, igual en C#: las posiciones de los niveles caen
    en la rejilla de 1/64 u, asi que px y py son enteros. Devuelve un entero
    sin signo; cada ciclo toma su modulo.

    El finalizador de MurmurHash3 no es adorno: los niveles colocan los nodos
    en multiplos de 0.25 u, que son 16 px, y multiplicar y hacer xor deja los
    cuatro bits bajos a cero. Sin mezclar, `h mod 16` daba 0 en todos los
    nodos del juego y no habia desfase ninguno.
    """
    m = 0xFFFFFFFF
    h = ((px * 73856093) ^ (py * 19349663)) & m
    h ^= h >> 16
    h = (h * 0x85EBCA6B) & m
    h ^= h >> 13
    h = (h * 0xC2B2AE35) & m
    return h ^ (h >> 16)


def cabeceo(prefijo, frame):
    """Desplazamiento vertical entero, en px, del cabeceo en ese frame."""
    a = CABECEO_PX.get(prefijo, 0)
    return int(np.round(a * np.sin(2 * np.pi * frame / CICLO_CABECEO)))


def _hoja(ruta):
    im = np.asarray(Image.open(ruta).convert('RGBA'))
    return [im[:, i * CELDA:(i + 1) * CELDA] for i in range(im.shape[1] // CELDA)]


def _pegar(lienzo, rgba, cx, cy):
    """Pega una celda con su centro en (cx, cy), alfa binario."""
    h, w = rgba.shape[:2]
    x0, y0 = cx - w // 2, cy - h // 2
    sub = lienzo[y0:y0 + h, x0:x0 + w]
    m = rgba[..., 3] > 0
    sub[m] = rgba[..., :3][m]


def _ampliar(im, k):
    return np.repeat(np.repeat(im, k, 0), k, 1)


def _guardar_gif(frames, ruta, k):
    ims = [Image.fromarray(_ampliar(f, k), 'RGB') for f in frames]
    ims[0].save(ruta, save_all=True, append_images=ims[1:], duration=int(1000 / FPS),
                loop=0, optimize=False, disposal=1)
    print('escrito', ruta)


def gif_nodo(destino, modulo, gen_nombre, k=4):
    """Todos los niveles de un nodo, Jugador arriba y Enemigo abajo."""
    m = importlib.import_module(modulo)
    gen = getattr(m, gen_nombre)
    niveles = sorted(m.NIVELES)
    hueco = 8
    W = len(niveles) * (CELDA + hueco) + hueco
    H = 2 * (CELDA + hueco) + hueco
    frames = []
    for f in range(HOJA):
        lz = np.empty((H, W, 3), np.uint8)
        lz[:] = BG
        for j, fac in enumerate(('player', 'enemy')):
            for i, n in enumerate(niveles):
                cel = a_rgba(gen(n, fac, t=f / HOJA))
                _pegar(lz, cel, hueco + i * (CELDA + hueco) + CELDA // 2,
                       hueco + j * (CELDA + hueco) + CELDA // 2)
        frames.append(lz)
    _guardar_gif(frames, destino, k)


# Un trozo del tablero de 480 x 270 px (la mitad del de 960 x 540, 7.5 x 4.2 u).
# (tipo, prefijo/carpeta, nivel, faccion, x, y en px desde la esquina, zzz)
TABLERO = [
    ('estrella', 'str', 2, 'player', 78, 80, False),
    ('estrella', 'str', 5, 'enemy', 402, 86, False),
    ('estrella', 'str', 3, 'player', 150, 200, False),
    ('pulsar', 'pul', 1, 'player', 64, 196, False),
    ('pulsar', 'pul', 3, 'enemy', 330, 200, False),
    ('asteroide', 'ast', 0, 'neutral', 196, 64, True),
    ('asteroide', 'ast', 0, 'neutral', 262, 120, True),
    ('asteroide', 'ast', 0, 'neutral', 292, 44, True),
    ('magnetar', 'mag', 3, 'neutral', 240, 210, False),
    ('magnetar', 'mag', 5, 'enemy', 420, 210, False),
]

VERSION = {'estrella': 1, 'pulsar': 2, 'asteroide': 1, 'magnetar': 1}


def tablero(d, k=2):
    sleep = _hoja(os.path.join(RAIZ, 'art', 'states', 'sleep', 'v2', 'state_sleep_sheet.png'))
    nodos = []
    for nodo, pre, nivel, fac, x, y, zzz in TABLERO:
        base = os.path.join(RAIZ, 'art', 'nodes', nodo, f'v{VERSION[nodo]}', f'{pre}_lvl{nivel}_{fac}')
        hoja = os.path.join(base + '_sheet.png')
        celdas = _hoja(hoja) if pre in ('str', 'pul') else [np.asarray(Image.open(base + '.png').convert('RGBA'))]
        nodos.append((pre, celdas, x, y, desfase(x, y), zzz))

    # Se pinta con un margen de una celda y se recorta: una celda pegada al
    # borde se sale del lienzo, y en el tablero de verdad tambien se saldria.
    W, H, M = 480, 270, CELDA
    frames = []
    for f in range(CICLO_CABECEO):
        lz = np.empty((H + 2 * M, W + 2 * M, 3), np.uint8)
        lz[:] = BG
        for pre, celdas, x, y, h, zzz in nodos:
            cel = celdas[(f + h) % len(celdas)]
            dy = -cabeceo(pre, (f + h) % CICLO_CABECEO)   # y de imagen crece hacia abajo
            _pegar(lz, cel, M + x, M + y + dy)
            if zzz:
                _pegar(lz, sleep[(f + h) % len(sleep)], M + x, M + y + dy)
        frames.append(lz[M:M + H, M:M + W].copy())

    _guardar_gif(frames, os.path.join(d, 'tablero_x2.gif'), k)
    tira = np.concatenate([np.concatenate(frames[i:i + 4], 1) for i in range(0, len(frames), 4)], 0)
    Image.fromarray(tira, 'RGB').save(os.path.join(d, 'tablero_frames.png'))
    print('escrito', os.path.join(d, 'tablero_frames.png'))


def main():
    d = os.path.join(RAIZ, 'art', '_preview', 'idle')
    os.makedirs(d, exist_ok=True)
    gif_nodo(os.path.join(d, 'estrella_x4.gif'), 'gen_estrella_v1', 'estrella')
    gif_nodo(os.path.join(d, 'pulsar_x4.gif'), 'gen_pulsar_v2', 'pulsar')
    tablero(d)


if __name__ == '__main__':
    main()
