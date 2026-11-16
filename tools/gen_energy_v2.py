"""NOVA - ART-UI-HUD-ENERGY v2, el numero de Energia: «El contador».

Ref: NOVA_Assets_Diseno.md A.6 · 14.7 (capa 6) · Spec 13.12 · 18 #48

Lo comun -el esqueleto de las cifras, las comprobaciones y las vistas- vive
en `gen_energy_v1.py`.

LA TESIS DE LA v2: EL NUMERO DICE HACIA DONDE VA. Un cuentakilometros de a
bordo. Cada cifra es una columna de un tambor -0 a 9 y otra vez 0- que se
ve por una ventana de 16 px. Cuando la Energia cambia, las columnas que
cambian RUEDAN: hacia arriba al ganar -la cifra nueva entra por abajo- y
hacia abajo al perder -entra por arriba-, con la curva orbital en 160 ms
(13.12: la interfaz fluye). Un nodo que se carga sube a pasitos; uno que
recibe una andanada de protones cae rodando. La tendencia se lee sin
recordar el numero de antes.

LOS RAILES. Encima y debajo de las cifras, a 1 px de aire, un rail de 1 px
en el ancla del bando (`str_p`, `str_e`, `neutral`): el bando del numero
sin tenir las cifras, que van en el blanco de dos bandas de la Carga del
proton -la misma familia, a x2-. Mientras una columna rueda, se enciende
el rail POR DONDE ENTRA la cifra nueva: el de abajo al ganar, el de arriba
al perder. Caja: rail + aire + 16 + aire + rail = 20, justo lo que reserva
`NodeLayout`.

ANCHO FIJO. Un tambor no cambia de ancho: todas las cifras van en celdas de
12, el 1 centrado. «100» mide 36 px, dentro del hueco de 38 (A.4.3), y el
numero no baila al pasar de 19 a 20. Cuando cambia el NUMERO de cifras (9 a
10, 100 a 99) el numero se recentra de golpe: la columna nueva aparece
entera, no rueda desde el vacio.

PIEZAS
    ui_energy_label_drum.png   12 x 176: el tambor, 0..9 y 0, una celda de
                               16 por cifra. La ventana muestra
                               [s * 16, s * 16 + 16), con s continuo y
                               redondeado al pixel
    ui_energy_label_rail.png   tres filas (bando) x dos columnas (quieto,
                               encendido), teselas de 4 x 1
    ui_energy_label.json       medidas, duracion y curva
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_energy_v1 as base
from gen_energy_v1 import (ALTO, CONT, FACCIONES, FONDO, ampliar, celda, colores_de,
                           fila_dis, sobre)
from nova_core import rampa

VERSION = 2
NOMBRE = 'El contador'

COL = 12                         # ancho fijo de columna
CAJA_ALTO = 20
RAIL_Y = (0, CAJA_ALTO - 1)      # arriba y abajo
CELDA_Y = 2
DURACION = 0.16                  # s, la curva orbital
NEUTRAL = rampa('neutral')
RAMPA_FAC = {'player': 'str_p', 'enemy': 'str_e', 'neutral': 'neutral'}
RAIL_QUIETO, RAIL_ENCENDIDO = 3, 1


def cifra(c):
    """Las cifras de la Carga a x2: `neutral[1]` arriba y `neutral[2]` en
    las dos filas de diseno de abajo, centradas en su columna de 12."""
    im = celda(c, lambda y, x, m: NEUTRAL[2] if fila_dis(y) >= 5 else NEUTRAL[1])
    out = np.zeros((ALTO, COL, 4), np.uint8)
    x0 = (COL - im.shape[1]) // 2
    out[:, x0:x0 + im.shape[1]] = im
    return out


TAMBOR = np.vstack([cifra(c) for c in '01234567890'])


def ventana(s):
    """La columna con el tambor en la posicion s (0..10), al pixel."""
    y = int(round(s * ALTO))
    return TAMBOR[y:y + ALTO]


def rail(fac, encendido, w):
    R = rampa(RAMPA_FAC[fac])
    out = np.zeros((1, w, 4), np.uint8)
    out[0, 1:w - 1, :3] = R[RAIL_ENCENDIDO if encendido else RAIL_QUIETO]
    out[0, 1:w - 1, 3] = 255
    return out


def montar(columnas, fac, sube=None):
    """`columnas`: la posicion s de cada tambor. `sube`: True si gana,
    False si pierde, None si esta quieto."""
    w = COL * len(columnas)
    out = np.zeros((CAJA_ALTO, w, 4), np.uint8)
    for i, s in enumerate(columnas):
        out[CELDA_Y:CELDA_Y + ALTO, i * COL:(i + 1) * COL] = ventana(s)
    out[RAIL_Y[0]:RAIL_Y[0] + 1] = rail(fac, sube is False, w)
    out[RAIL_Y[1]:RAIL_Y[1] + 1] = rail(fac, sube is True, w)
    return out


def etiqueta(texto, fac, energia, tope, f=0):
    return montar([int(ch) for ch in texto], fac)


# --- El rodar --------------------------------------------------------------

def orbital(t):
    """cubic-bezier(.2, .8, .2, 1): y para una x dada, por biseccion."""
    def bez(p1, p2, u):
        return 3 * (1 - u) ** 2 * u * p1 + 3 * (1 - u) * u ** 2 * p2 + u ** 3
    lo, hi = 0.0, 1.0
    for _ in range(40):
        mid = (lo + hi) / 2
        if bez(.2, .2, mid) < t:
            lo = mid
        else:
            hi = mid
    return bez(.8, 1, (lo + hi) / 2)


def rodar(a, b, fac, t):
    """El numero pasando de a a b, t segundos despues del cambio. Mismo
    numero de cifras: cada columna rueda de su cifra vieja a la nueva por el
    camino corto en el sentido del cambio."""
    ta, tb = str(a), str(b)
    if len(ta) != len(tb):
        return montar([int(ch) for ch in tb], fac)
    k = orbital(min(1.0, t / DURACION))
    sube = b > a
    cols = []
    for ca, cb in zip(ta, tb):
        da, db = int(ca), int(cb)
        if da == db:
            cols.append(da)
            continue
        if sube:
            fin = db if db > da else db + 10      # 9 -> 0 pasa por el 0 de abajo
            ini = da
        else:
            ini = da if da > db else da + 10      # 0 -> 9 sale del 0 de abajo
            fin = db
        s = ini + (fin - ini) * k
        cols.append(s % 10 if s > 10 else s)
    return montar(cols, fac, sube if k < 1 else None)


def comprobar_propio():
    fallos = []
    if TAMBOR.shape != (ALTO * 11, COL, 4):
        fallos.append(f'tambor de {TAMBOR.shape[1]} x {TAMBOR.shape[0]}')
    if not (TAMBOR[:ALTO] == TAMBOR[-ALTO:]).all():
        fallos.append('el tambor no cierra: el 0 de abajo no es el de arriba')
    if COL * 3 > base.CAJA[1]:
        fallos.append('«100» no cabe en el hueco')
    # rodando, nada se sale de la caja ni deja de ser alfa binario
    for a in range(0, 100):
        for b in (a - 1, a + 1, a - 10, a + 7):
            if 0 <= b <= 100:
                for t in (0, .02, .05, .08, .16):
                    im = rodar(a, b, 'player', t)
                    if im.shape[0] != CAJA_ALTO or im.shape[1] > base.CAJA[1]:
                        fallos.append(f'{a}->{b} se sale de la caja')
    if abs(orbital(0.5) - 0.5) < .2:
        fallos.append('la curva no es la orbital')
    return fallos


def exportar(d):
    Image.fromarray(TAMBOR, 'RGBA').save(os.path.join(d, 'ui_energy_label_drum.png'))
    rails = np.zeros((3, 8, 4), np.uint8)
    for i, fac in enumerate(FACCIONES):
        R = rampa(RAMPA_FAC[fac])
        rails[i, :4, :3] = R[RAIL_QUIETO]
        rails[i, 4:, :3] = R[RAIL_ENCENDIDO]
    rails[..., 3] = 255
    Image.fromarray(rails, 'RGBA').save(os.path.join(d, 'ui_energy_label_rail.png'))
    with open(os.path.join(d, 'ui_energy_label.json'), 'w') as fh:
        json.dump({'columnWidth': COL, 'cellHeight': ALTO, 'drumCells': 11,
                   'boxHeight': CAJA_ALTO, 'digitsTop': CELDA_Y, 'railRows': list(RAIL_Y),
                   'railInset': 1, 'railTile': 4, 'factions': list(FACCIONES),
                   'railStates': ['idle', 'lit'], 'rollSeconds': DURACION,
                   'rollCurve': [0.2, 0.8, 0.2, 1.0],
                   'note': 'cada columna muestra el tambor en [s*16, s*16+16) con s redondeado al pixel; '
                           'al ganar s sube (9 -> 0 por el 0 de abajo, s = 10), al perder baja; '
                           'mientras rueda se enciende el rail de abajo (gana) o el de arriba (pierde); '
                           'si cambia el numero de cifras, se recentra sin rodar'}, fh, indent=1)


def secuencia():
    """Una Estrella que se carga y recibe una andanada: (a, b) por paso."""
    pasos = [(27, 28), (28, 29), (29, 30), (30, 31), (31, 21), (21, 19), (19, 9), (9, 10), (10, 11)]
    fps = 50
    cuadros = []
    for a, b in pasos:
        for i in range(int(.16 * fps)):
            cuadros.append(rodar(a, b, 'player', i / fps))
        for _ in range(int(.5 * fps)):
            cuadros.append(rodar(a, b, 'player', 1))
    return cuadros, fps


def vistas_propias(d, gif):
    # el rodar de 29 a 30 y de 30 a 29, fotograma a fotograma (cada 20 ms)
    filas = []
    for a, b in ((29, 30), (30, 29), (31, 21)):
        fila = []
        for i in range(9):
            c = np.empty((CAJA_ALTO + 4, 30, 3), np.uint8)
            c[:] = FONDO
            sobre(c, rodar(a, b, 'player', i * .02), 3, 2)
            fila.append(c)
        filas.append(np.hstack(fila))
    for fac in ('enemy', 'neutral'):
        fila = []
        for i in range(9):
            c = np.empty((CAJA_ALTO + 4, 30, 3), np.uint8)
            c[:] = FONDO
            sobre(c, rodar(48, 47, fac, i * .02), 3, 2)
            fila.append(c)
        filas.append(np.hstack(fila))
    Image.fromarray(ampliar(np.vstack(filas), 6)).save(os.path.join(d, 'rodar_x6.png'))
    if gif:
        cuadros, fps = secuencia()
        ims = []
        for q in cuadros:
            c = np.empty((CAJA_ALTO + 8, 48, 3), np.uint8)
            c[:] = FONDO
            sobre(c, q, 24 - q.shape[1] // 2, 4)
            ims.append(Image.fromarray(ampliar(c, 6)))
        ims[0].save(os.path.join(d, 'contador_x6.gif'), save_all=True,
                    append_images=ims[1:], duration=1000 // fps, loop=0)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'str_e', 'neutral') | {tuple(CONT)})
