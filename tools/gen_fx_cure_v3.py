"""NOVA - ART-FX-CURE v3, la cura de Decaimiento: «La efervescencia».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.5 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de cinco tallas y dieciseis
variantes, la rampa del decaimiento, la caja del numero, la continuidad con
«El miasma» y las comprobaciones- vive en `gen_fx_cure_v1.py`; aqui solo
esta el dibujo.

LA TESIS: LA INFECCION CAIA; LA CURA SUBE. Como el agua oxigenada sobre una
herida, la cura burbujea: el miasma se recoge en burbujas que suben por el
nodo y revientan. «El miasma» esta sesgado hacia abajo y gotea, porque la
Energia se escapa; aqui todo va hacia arriba, y el jugador lee el cambio
de sentido antes que el dibujo.

  - HIERVE (frame 0). La nube del frame del bucle y, dentro, nueve burbujas
    que nacen en su mitad de abajo.
  - SUBEN (frames 1-2). La nube se aclara y se va; las burbujas crecen y
    suben, cada una a su ritmo.
  - REVIENTAN (frames 3-4). Cada burbuja estalla en cuatro gotas en cruz,
    y quedan unas motas.

    py tools/gen_fx_cure_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_cure_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_cure_v1 as base
from gen_fx_cure_v1 import C, CELDA, VARIANTES, campo, tramar
from gen_fx_thaw_v1 import _PX, _PY

VERSION = 3
NOMBRE  = 'La efervescencia'

BURBUJAS = 9
SUBE     = (0, 5, 11, 17, 20)
RADIO    = (1.2, 1.7, 2.2, 0, 0)


def semillas(v):
    """Nueve burbujas en la mitad de abajo de la nube, distintas por variante."""
    rng = np.random.default_rng(300 + v)
    out = []
    for j in range(BURBUJAS):
        a = math.radians(rng.uniform(10, 170) if j % 3 else rng.uniform(-20, 200))
        r = rng.uniform(21, 30)
        out.append((C + r * math.cos(a), C + r * math.sin(a), rng.uniform(0.75, 1.25)))
    return out


def burbuja(idx, tt, x, y, r, banda):
    d = np.hypot(_PX - x, _PY - y)
    aro = (np.abs(d - r) < 0.55) & tt.libre & (idx == 0)
    idx[aro] = banda
    tt.punto(idx, x - r * 0.4, y - r * 0.4, 1)           # el brillo


def dibujar(tt, v, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    t = v / VARIANTES
    for x, y, vel in semillas(v):
        yy = y - SUBE[f] * vel
        if f < 3:
            burbuja(idx, tt, x, yy, RADIO[f], 2 if f < 2 else 3)
        elif f == 3:
            for dx, dy in ((-2, 0), (2, 0), (0, -2), (0, 2)):
                tt.punto(idx, x + dx, yy + dy, 3)
        else:
            tt.punto(idx, x, yy, 5)
    k = (1.0, 0.55, 0.25, 0, 0)[f]
    if k:
        tramar(idx, campo(t, 0, k) * tt.libre, 3 if f == 0 else 4, 5, corte=0.24 * k)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
