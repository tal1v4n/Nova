"""NOVA - ART-FX-THAW v3, el deshielo: «La soldadura».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.4 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de cinco tallas, la rampa del hielo,
la caja del numero, la continuidad con «El vidrio» y las comprobaciones-
vive en `gen_fx_thaw_v1.py`; aqui solo esta el dibujo.

LA TESIS: LA GRIETA SE CIERRA. Si congelar es agrietar, descongelar es
soldar. La red se recoge por donde crecio: las astillas se van, las ramas
vuelven a su nudo en el tronco y el tronco se cierra como una cremallera,
de las puntas hacia el nudo central. El frente que avanza es el punto mas
caliente, en el indice 1: es una soldadura, no un borrado. Donde se cierra
la ultima puntada sale un soplo de vaho.

  - LA ESCARCHA SE VA (frame 0). Quedan las lineas, con la punta de cada
    rama ya encendida.
  - LAS RAMAS VUELVEN (frames 1-2). Se recogen hacia su nudo; las astillas
    se apagan.
  - SE CIERRA (frames 3-4). El tronco se cierra de los dos extremos hacia
    el nudo de enmedio.
  - EL VAHO (frame 5). Un soplo de trama que sube desde la ultima puntada.

    py tools/gen_fx_thaw_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_thaw_v3.py --gif    y las animadas
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_thaw_v1 as base
import gen_state_frozen_v3 as vidrio
from gen_fx_thaw_v1 import CELDA, linea, tramar, _PX, _PY

VERSION = 3
NOMBRE  = 'La soldadura'

RAMA   = (1.0, 0.55, 0.15, 0, 0, 0)       # fraccion de cada rama que queda
TRONCO = (1.0, 1.0, 0.85, 0.45, 0.12, 0)  # fraccion de cada mitad del tronco
NUDO   = vidrio.TRONCO[2]                 # el de enmedio: (47, 43)


def _tramo(idx, tt, pts, frac, banda, desde_final=False):
    """Lo que queda de una polilinea que se recoge hacia su primer punto, con
    el frente en el indice 1."""
    segs = list(zip(pts[:-1], pts[1:]))
    largos = [np.hypot(b[0] - a[0], b[1] - a[1]) for a, b in segs]
    resto = frac * sum(largos)
    punta = None
    for (a, b), L in zip(segs, largos):
        if resto <= 0:
            break
        s = min(resto / L, 1.0)
        fin = (a[0] + (b[0] - a[0]) * s, a[1] + (b[1] - a[1]) * s)
        idx[linea(a, fin) & tt.libre & (idx == 0)] = banda
        punta = fin
        resto -= L
    if punta is not None and frac < 1:
        tt.punto(idx, punta[0], punta[1], 1)
        tt.punto(idx, punta[0] + 1, punta[1], 1)
    return punta


def dibujar(tt, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    # el tronco, en dos mitades que se recogen hacia el nudo de enmedio
    arriba = list(vidrio.TRONCO[:3])[::-1]
    abajo = list(vidrio.TRONCO[2:])
    if f < 5:
        for mitad in (arriba, abajo):
            _tramo(idx, tt, mitad, TRONCO[f], 1 if f == 0 else 2)
        for rama in vidrio.RAMAS:
            if RAMA[f] > 0:
                p = _tramo(idx, tt, list(rama), RAMA[f], 2 if f == 0 else 3)
                if f == 0 and p is not None:
                    tt.punto(idx, p[0], p[1], 1)
        if f <= 1:
            for a, b in vidrio.ASTILLAS:
                idx[linea(a, b) & tt.libre & (idx == 0)] = 2 if f == 0 else 4
    if f >= 4:
        # el soplo de vaho que sube de la ultima puntada
        sube = (f - 4) * 4 + 3
        cx, cy = NUDO[0], NUDO[1] - sube
        r = np.hypot(_PX - cx, (_PY - cy) * 1.3)
        dens = np.clip(1 - r / (4.5 + (f - 4) * 2.5), 0, 1) ** 0.7 * (0.85 if f == 4 else 0.45)
        tramar(idx, dens * tt.libre, 3 if f == 4 else 4, 4 if f == 4 else 5, corte=0.4)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
