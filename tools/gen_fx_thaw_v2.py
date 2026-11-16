"""NOVA - ART-FX-THAW v2, el deshielo: «La rotura».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.4 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de cinco tallas, la rampa del hielo,
la caja del numero, la continuidad con «El vidrio» y las comprobaciones-
vive en `gen_fx_thaw_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL VIDRIO SALTA. «El vidrio» dijo que el hielo de NOVA no es una
capa, es una grieta. Lo que acaba una grieta no es el calor: es que el
vidrio termine de romperse. El nodo se sacude el hielo de encima como quien
rompe la costra, y los trece trozos de la red salen despedidos.

  - CRUJE (frame 0). Toda la red se enciende en el indice 1, el tronco y las
    ramas, con la escarcha subida una banda: el ultimo instante del vidrio
    es el mas brillante.
  - SALTA (frames 1-4). Cada segmento es una esquirla: sale en la direccion
    de su punto medio desde el centro del nodo, 2, 5, 9 y 13 px, gira un
    poco y se acorta mientras se apaga.
  - BRILLA (frame 5). Queda un destello de 1 px donde va cada esquirla.

    py tools/gen_fx_thaw_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_thaw_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_thaw_v1 as base
from gen_fx_thaw_v1 import C, CELDA, linea, overlay_idx, segmentos

VERSION = 2
NOMBRE  = 'La rotura'

SALE    = (0, 2, 5, 9, 13, 16)
QUEDA   = (1.0, 0.85, 0.65, 0.5, 0.35, 0)   # fraccion de la esquirla que sigue entera
GIRO    = (0, 0.06, 0.14, 0.24, 0.34, 0)
BANDA   = (1, 1, 2, 3, 4, 5)


def dibujar(tt, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    if f == 0:
        o = overlay_idx()
        trama = (o > 0) & ~np.any([linea(a, b) for a, b, _ in segmentos()], axis=0)
        idx[trama & tt.libre] = 2
        for a, b, _ in segmentos():
            idx[linea(a, b) & tt.libre] = 1
        return idx
    for j, (a, b, _) in enumerate(segmentos()):
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        ang = math.atan2(my - C, mx - C)
        ox, oy = math.cos(ang) * SALE[f], math.sin(ang) * SALE[f]
        if f == 5:
            tt.punto(idx, mx + ox, my + oy, 4 if j % 2 else 5)
            continue
        g = GIRO[f] * (1 if j % 2 else -1)
        c, s = math.cos(g), math.sin(g)
        q = QUEDA[f] / 2

        def p(x, y):
            dx, dy = x - mx, y - my
            return (mx + ox + (dx * c - dy * s) * q * 2, my + oy + (dx * s + dy * c) * q * 2)
        m = linea(p(*a), p(*b), 0.6) & tt.libre
        idx[m & (idx == 0)] = BANDA[f]
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
