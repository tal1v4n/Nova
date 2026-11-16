"""NOVA - ART-FX-PWR-DEC v3, el impacto de Decaimiento: «Las colonias».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.5 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de cinco tallas, la rampa del
decaimiento, la caja del numero, el final en el miasma y las comprobaciones-
vive en `gen_fx_pwr_dec_v1.py`; aqui solo esta el dibujo.

LA TESIS: LA INFECCION SE SIEMBRA. Las otras dos leen el poder como fisica;
esta lo lee como lo que el juego dice que es, una infeccion. El impacto
siembra seis esporas en el borde del nodo y crecen como colonias en una
placa de Petri vista en camara rapida: manchas redondas con el borde
mellado, el frente claro y el centro tramado. Cuando se tocan, maduran,
se descuelgan y gotean, y lo que gotea es el miasma.

  - LA SIEMBRA (frame 0). Seis esporas en el indice 1, cada una con su
    cruz.
  - CRECEN (frames 1-4). Cada colonia avanza a su ritmo; el borde, que es
    lo vivo, va en el indice 2 y el centro se oscurece.
  - SE DESCUELGAN (frames 5-7). Las colonias se aclaran, se escurren hacia
    abajo y se deshacen en la nube.

Seis colonias de tamanos distintos, repartidas sin simetria: con alfa
binario y seis copias iguales se leeria una corona.

    py tools/gen_fx_pwr_dec_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_pwr_dec_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_pwr_dec_v1 as base
from gen_fx_pwr_dec_v1 import (C, CELDA, DENTRO, RUIDO, ULTIMO, debajo, final, parcial,
                               punto)
from gen_fx_send_v1 import BAYER, borde

VERSION = 3
NOMBRE  = 'Las colonias'

_y, _x = np.mgrid[0:CELDA, 0:CELDA]
PX, PY = _x + 0.5, _y + 0.5


def _suave(a, veces=3):
    for _ in range(veces):
        a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5
    return (a - a.min()) / (a.max() - a.min())


MELLA = _suave(np.random.default_rng(23).random((CELDA, CELDA)))

# Cada colonia: (angulo en grados, desfase radial, radio final, velocidad)
# El hueco de abajo es la caja del numero.
COLONIAS = ((-150, -1.0, 10.5, 1.0), (-95, 0.5, 8.0, 0.8), (-35, -0.5, 11.5, 1.1),
            (15, 1.0, 7.0, 0.75), (60, 0.0, 9.0, 0.95), (138, -1.5, 8.5, 0.85))


def _nacimientos(t):
    """El frame (fraccionario) en que cada pixel queda colonizado, o inf, y
    la posicion de cada espora."""
    rb = borde(t)
    T = np.full((CELDA, CELDA), np.inf)
    esporas = []
    for ang, dr, rad, vel in COLONIAS:
        a = math.radians(ang)
        sx, sy = C + (rb + dr) * math.cos(a), C + (rb + dr) * math.sin(a)
        esporas.append((sx, sy))
        d = np.hypot(PX - sx, PY - sy)
        # el borde mellado: el ruido suave adelanta o retrasa el frente
        d = d * (0.75 + 0.5 * MELLA)
        dentro = d < rad
        Ti = np.where(dentro, 0.15 + d / rad * 3.6 / vel, np.inf)
        T = np.minimum(T, Ti)
    return T, esporas


_CACHE = {}


def nacimientos(t):
    if t not in _CACHE:
        _CACHE[t] = _nacimientos(t)
    return _CACHE[t]


def _espora(idx, tt, x, y):
    for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        punto(idx, tt, x - 0.5 + dx, y - 0.5 + dy, 1)
    for dx, dy in ((-1, 0), (2, 0), (0, -1), (1, -1), (0, 2), (1, 2), (-1, 1), (2, 1)):
        punto(idx, tt, x - 0.5 + dx, y - 0.5 + dy, 2)
    for dx, dy in ((-2, -2), (3, -2), (-2, 3), (3, 3)):
        punto(idx, tt, x - 0.5 + dx, y - 0.5 + dy, 4)


def _colonia(T, f, tt, caida=0, ralo=1.0, b_borde=2, b_dentro=(3, 4)):
    """La colonia en el frame f: el borde (lo nacido en el ultimo frame) y el
    centro tramado, escurrido `caida` px hacia abajo y con solo una
    fraccion `ralo` de sus pixeles."""
    idx = np.zeros((CELDA, CELDA), np.int8)
    viva = T <= f
    if not viva.any():
        return idx
    frente = viva & (T > f - 1.0)
    viejo = viva & ~frente
    centro = viejo & (BAYER < 0.62)
    if caida:
        # escurre: cada columna baja mas cuanto mas abajo esta en la celda
        frente = np.roll(frente, caida, 0)
        viejo = np.roll(viejo, caida, 0)
        centro = np.roll(centro, caida, 0)
    keep = RUIDO < ralo
    idx[centro & keep] = b_dentro[1]
    idx[viejo & (T <= f - 2.0) & keep & (BAYER < 0.3)] = b_dentro[0]
    idx[frente & keep] = b_borde
    idx[~(tt.libre & DENTRO)] = 0
    return idx


def dibujar(tt, f):
    if f == base.FRAMES - 1:
        return final(tt)
    T, esporas = nacimientos(tt.t)
    if f == 0:
        idx = np.zeros((CELDA, CELDA), np.int8)
        for x, y in esporas:
            _espora(idx, tt, x, y)
        return idx
    if f <= 4:
        idx = _colonia(T, f, tt)
        if f == 1:
            for x, y in esporas:
                punto(idx, tt, x, y, 1)
                punto(idx, tt, x + 1, y + 1, 1)
        return idx
    k = f - 5                                   # 0, 1, 2: se descuelgan
    idx = _colonia(T, 4.5, tt, caida=(1, 3, 6)[k], ralo=(0.85, 0.5, 0.22)[k],
                   b_borde=(2, 3, 4)[k], b_dentro=((3, 4), (4, 5), (5, 5))[k])
    # gotas que se descuelgan del borde de abajo de cada colonia
    for i, (x, y) in enumerate(esporas):
        if y < C - 6:
            continue
        for j in range(2):
            gx = x + (j * 5 - 2) + (i % 3)
            gy = y + 6 + (2, 5, 9)[k] + 2 * j
            punto(idx, tt, gx, gy, (2, 3, 4)[k])
            punto(idx, tt, gx, gy - 1, (3, 4, 5)[k])
    debajo(idx, parcial(ULTIMO - 3 + k, tt, (0.3, 0.55, 0.8)[k]))
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
