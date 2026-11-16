"""NOVA - ART-FX-PWR-ZER v2, el impacto de Cero Absoluto: «El golpe de frio».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.4 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de cinco tallas, la rampa del hielo,
la caja del numero, el final en el overlay y las comprobaciones- vive en
`gen_fx_pwr_zer_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL FRIO CAE DE GOLPE. El cero absoluto es la ausencia de
movimiento, y lo que se para no se para poco a poco. Una onda de cristales
se cierra sobre el nodo desde fuera, como la escarcha de un parabrisas
vista en camara rapida, y cuando lo toca el vidrio se raja entero en el
mismo frame. Despues solo queda enfriarse.

  - SE CIERRA (frames 0-3). Un aro de trama con seis cristales encima, del
    borde de la celda al borde del nodo, cada vez mas claro.
  - SE RAJA (frame 4). Toda la red a la vez, en el indice 1, con un vaho de
    trama alrededor de las lineas.
  - SE ENFRIA (frames 5-8). Las lineas bajan a sus bandas y la escarcha
    florece hasta el overlay.

    py tools/gen_fx_pwr_zer_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_pwr_zer_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_pwr_zer_v1 as base
from gen_fx_pwr_zer_v1 import (BAYER, C, CELDA, asentar, copo, final, lineas_del_vidrio,
                               tramar)
from gen_fx_send_v1 import R, borde

VERSION = 2
NOMBRE  = 'El golpe de frio'


def dibujar(tt, f):
    if f == base.FRAMES - 1:
        return final(tt)
    if f >= 5:
        return asentar(tt, (0.15, 0.35, 0.65)[f - 5], 1 if f == 5 else None)
    idx = np.zeros((CELDA, CELDA), np.int8)
    if f == 4:
        lin = lineas_del_vidrio() & tt.libre
        idx[lin] = 1
        cerca = np.zeros_like(lin)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                cerca |= np.roll(np.roll(lin, dy, 0), dx, 1)
        idx[cerca & ~lin & (BAYER < 0.5) & tt.libre] = 2
        return idx
    Rb = borde(tt.t)
    r = 44 - (44 - (Rb + 2)) * f / 3
    aro = (np.abs(R - r) < 1.6) & tt.libre
    tramar(idx, np.where(aro, (0.45, 0.6, 0.75, 0.9)[f], 0), (3, 2, 2, 1)[f], (4, 3, 3, 2)[f],
           corte=0.5)
    for k in range(6):
        a = math.radians(-75 + 60 * k + 12 * f)
        copo(idx, tt, C + r * math.cos(a), C + r * math.sin(a), 3, (2, 2, 1, 1)[f])
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
