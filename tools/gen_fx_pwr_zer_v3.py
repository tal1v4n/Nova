"""NOVA - ART-FX-PWR-ZER v3, el impacto de Cero Absoluto: «El copo».

Ref: NOVA_Assets_Diseno.md A.5, A.3.4 y A.4.2 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de cinco tallas, la rampa del hielo,
la caja del numero, el final en el overlay y las comprobaciones- vive en
`gen_fx_pwr_zer_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL HAZ DEJA SU COPO. «El rosario de escarcha» lleva copos de seis
brazos ensartados en su espina; el ultimo se queda en el objetivo. Crece
sobre el nodo, gira un poco al cristalizar y, cuando ya no cabe, se quiebra:
de sus brazos rotos sale la red de «El vidrio». El copo es el poder; la
grieta, el estado que deja.

  - EL COPO (frames 0-3). Nace en el nudo de la red y crece de 4 a 15 px,
    con barbas, girando 5 grados por frame.
  - SE QUIEBRA (frame 4). Del copo quedan las puntas de los brazos y la red
    aparece entera en el indice 1.
  - SE ASIENTA (frames 5-8). Las puntas se apagan, las lineas bajan a sus
    bandas y florece la escarcha hasta el overlay.

    py tools/gen_fx_pwr_zer_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_pwr_zer_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_pwr_zer_v1 as base
from gen_fx_pwr_zer_v1 import NUDO, asentar, copo, final, lineas_del_vidrio

VERSION = 3
NOMBRE  = 'El copo'

RADIO   = (4, 8, 12, 15)
GIRO0   = math.radians(15)


def puntas(idx, tt, banda):
    """Lo que queda del copo roto: el tercio de fuera de cada brazo."""
    g = GIRO0 + math.radians(5 * 4)
    for k in range(6):
        a = g + k * math.pi / 3
        for s in np.linspace(0.68, 1.0, 5):
            tt.punto(idx, NUDO[0] + 16 * s * math.cos(a), NUDO[1] + 16 * s * math.sin(a), banda,
                     solo_vacio=True)


def dibujar(tt, f):
    if f == base.FRAMES - 1:
        return final(tt)
    if f <= 3:
        idx = np.zeros((96, 96), np.int8)
        copo(idx, tt, NUDO[0], NUDO[1], RADIO[f], 1 if f >= 2 else 2,
             giro=GIRO0 + math.radians(5 * f), punta=2)
        return idx
    if f == 4:
        idx = np.zeros((96, 96), np.int8)
        idx[lineas_del_vidrio() & tt.libre] = 1
        puntas(idx, tt, 2)
        return idx
    idx = asentar(tt, (0.2, 0.45, 0.7)[f - 5], 1 if f == 5 else None)
    if f <= 6:
        puntas(idx, tt, (3, 4)[f - 5])
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
