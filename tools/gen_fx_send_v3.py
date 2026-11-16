"""NOVA - ART-FX-SEND v3, el lanzamiento sobre el nodo origen: «La corona».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja, las tallas de «La orbita», la
franja del numero y las comprobaciones- vive en `gen_fx_send_v1.py`; aqui
solo esta el dibujo.

LA TESIS: EL NODO SE ERIZA AL SOLTAR. Como el sol en los dibujos de la
epoca, que no es un circulo sino un circulo con rayos: al enviar, el nodo
saca una corona de rayos cortos que salen del cuerpo y se recogen. No dice
«algo se aleja» (eso ya lo dice el proton, que sale en el mismo tick), sino
«este nodo acaba de gastar».

  - ERIZO (frame 0). Doce rayos de 1 px que salen del borde, uno largo y
    uno corto alternos, claros en la raiz.
  - SUELTA (frames 1-2). Los rayos se despegan del cuerpo, se acortan y se
    apagan una banda por frame, hasta quedar un punto cada uno.

Los rayos van girados 15 grados para que ninguno caiga recto hacia abajo:
los que caerian en la franja del numero no se dibujan.

    py tools/gen_fx_send_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_send_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_send_v1 as base
from gen_fx_send_v1 import CELDA, DX, DY, LIBRE, borde

VERSION = 3
NOMBRE  = 'La corona'

RAYOS   = 12
GIRO    = math.radians(15)
LARGO   = (6.0, 4.0)       # rayo largo y corto en el frame 0


def dibujar(t, f):
    Rb = borde(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    for k in range(RAYOS):
        a = GIRO + 2 * math.pi * k / RAYOS
        ux, uy = math.cos(a), math.sin(a)
        largo = LARGO[k % 2]
        if f == 0:
            r0, r1 = Rb + 1.0, Rb + 1.0 + largo
        elif f == 1:
            r0, r1 = Rb + 3.0, Rb + 3.0 + largo / 2
        else:
            r0, r1 = Rb + 4.5, Rb + 5.5
        lon = DX * ux + DY * uy
        per = np.abs(-DX * uy + DY * ux)
        rayo = (per <= 0.5) & (lon >= r0) & (lon <= r1) & LIBRE
        if f == 0:
            b = np.where(lon - r0 < largo * 0.45, 1, 2)
        else:
            b = np.full(lon.shape, 2 + f)
        idx[rayo & (idx == 0)] = b[rayo & (idx == 0)]
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
