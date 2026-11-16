"""NOVA - ART-FX-ABS v3, la absorcion aliada: «Los tres quarks».

Ref: NOVA_Assets_Diseno.md A.5 y A.4.2 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja, las tallas, la franja del numero y
las comprobaciones, entre ellas que el efecto entre y acabe en el cuerpo-
vive en `gen_fx_abs_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL NODO SE BEBE EL PROTON. El proton que llega son tres quarks en
una bolsa («La bolsa», A.4.2). Al tocar un nodo propio la bolsa se abre y
los tres quarks entran en el, en espiral, uno por cada tercio de arriba: es
la misma historia que el jugador ve en el proton, contada hasta el final.

  - LLEGAN (frames 0-2). Tres motas que se enroscan hacia el cuerpo desde
    10 px, cada frame 3 px mas cerca y una banda mas claras, con una estela
    corta detras.
  - ENTRAN (frame 3). Donde toca cada una, un arco corto de la superficie
    se enciende en el indice 1.

Las tres vienen de la mitad de arriba (a -150, -90 y -30 grados, y giran
10 grados por frame), para que ninguna cruce el numero.

    py tools/gen_fx_abs_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_abs_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_abs_v1 as base
from gen_fx_abs_v1 import ANG, C, CELDA, LIBRE, R, borde

VERSION = 3
NOMBRE  = 'Los tres quarks'

ANGULOS = tuple(math.radians(a) for a in (-150, -90, -30))
GIRO    = math.radians(-10)       # por frame
DIST    = (10.0, 7.0, 4.0)        # del cuerpo, frames 0-2
ARCO    = 0.2


def _punto(idx, x, y, banda):
    ix, iy = int(math.floor(x)), int(math.floor(y))
    if LIBRE[iy, ix] and (idx[iy, ix] == 0 or idx[iy, ix] > banda):
        idx[iy, ix] = banda


def dibujar(t, f):
    Rb = borde(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    for a0 in ANGULOS:
        if f < 3:
            a = a0 + GIRO * f
            d = Rb + DIST[f]
            x, y = C + d * math.cos(a), C + d * math.sin(a)
            banda = 3 - f
            # la mota: 1 px en el frame 0, 2 x 2 despues
            _punto(idx, x, y, banda)
            if f > 0:
                # crece hacia el nodo, no hacia fuera: el efecto entra
                sx, sy = -math.copysign(1, math.cos(a)), -math.copysign(1, math.sin(a))
                for dx, dy in ((sx, 0), (0, sy), (sx, sy)):
                    _punto(idx, x + dx, y + dy, banda)
            # la estela: de donde venia
            if f > 0:
                ap, dp = a0 + GIRO * (f - 1), Rb + DIST[f - 1]
                for k in np.linspace(0.2, 0.5, 3):
                    _punto(idx, C + (d + (dp - d) * k) * math.cos(a + (ap - a) * k),
                           C + (d + (dp - d) * k) * math.sin(a + (ap - a) * k), 4)
        else:
            a = a0 + GIRO * 3
            dif = np.abs(np.angle(np.exp(1j * (ANG - a))))
            arco = (dif < ARCO) & (R >= Rb + 0.5) & (R < Rb + 1.5) & LIBRE
            idx[arco & (dif < ARCO * 0.5)] = 1
            idx[arco & (idx == 0)] = 2
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
