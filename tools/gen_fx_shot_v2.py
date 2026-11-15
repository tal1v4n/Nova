"""NOVA - ART-FX-SHOT v2, el destello sobre el proton alcanzado: «Los dos cuantos».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.3 y 14.5

Lo comun a las tres propuestas -la hoja, las tallas, el numero que no se
pisa, el color del proton, las cuatro variantes y las comprobaciones- vive en
`gen_fx_shot_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL DESTELLO ES EL DANO. Cada disparo le quita 2 de Carga al proton
(14.3), y la Carga es el plasma que lleva dentro. Asi que el destello no es
una luz generica: son DOS CUANTOS de plasma que el golpe le arranca a la
bolsa, y salen despedidos. El jugador no tiene que leer el numero para saber
cuanto ha perdido: lo ve salir, de dos en dos, como cuenta los quarks.

  - GOLPE (frame 0). La membrana se rasga en dos sitios -un tramo solido de
    la bolsa encendido- y de cada rasgadura asoma un cuanto: una cruz de
    5 px con el centro en el indice 1.
  - FUGA (frame 1). Los dos cuantos se han ido 5 px mas lejos, encogidos a
    una cruz de 3 y una banda mas apagados, con un rastro tramado detras.
    Las rasgaduras quedan en trama rala.

No salen opuestos: a 155 grados uno del otro, para que el par no sea un eje
y no se lea como un adorno simetrico. Las cuatro variantes giran el par de
90 en 90 grados.

    py tools/gen_fx_shot_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_shot_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_shot_v1 as base
from gen_fx_shot_v1 import ANG, CELDA, DX, DY, R, medidas, numero, tramar

VERSION = 2
NOMBRE  = 'Los dos cuantos'

ANGULO  = math.radians(-35)   # el primer cuanto, arriba a la derecha
ABRE    = math.radians(155)   # el segundo, a 155 grados del primero
RASGA   = 0.32                # media anchura de la rasgadura, en radianes
FUGA    = 5.0                 # cuanto se alejan entre el golpe y la fuga


def _cruz(idx, cx, cy, brazo, centro, brazos, libre):
    """Una cruz de pixeles sobre (cx, cy), redondeado a la rejilla."""
    ix, iy = int(math.floor(cx)), int(math.floor(cy))
    for dx, dy in [(0, 0)] + [(s * k, 0) for s in (-1, 1) for k in range(1, brazo + 1)] \
            + [(0, s * k) for s in (-1, 1) for k in range(1, brazo + 1)]:
        x, y = ix + dx, iy + dy
        if 0 <= x < CELDA and 0 <= y < CELDA and libre[y, x]:
            idx[y, x] = centro if (dx, dy) == (0, 0) else brazos


def dibujar(t, f, v):
    Rb, Re = medidas(t)
    libre = ~numero(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    a0 = ANGULO + v * math.pi / 2
    for a in (a0, a0 + ABRE):
        dif = np.angle(np.exp(1j * (ANG - a)))          # distancia angular
        ux, uy = math.cos(a), math.sin(a)
        if f == 0:
            # la rasgadura: un tramo solido de la membrana, de Rb a Re
            tramo = (np.abs(dif) < RASGA) & (R >= Rb - 0.5) & (R < Re + 0.5) & libre
            idx[tramo & (np.abs(dif) < RASGA * 0.5)] = 2
            idx[tramo & (idx == 0)] = 3
            # el cuanto asomando, justo fuera de la bolsa
            d = Re + 2.5
            _cruz(idx, base.C + ux * d, base.C + uy * d, 2, 1, 2, libre)
        else:
            d = Re + 2.5 + FUGA
            # el rastro: de la rasgadura al cuanto, que se apaga hacia atras
            lon = DX * ux + DY * uy
            per = np.abs(-DX * uy + DY * ux)
            dens = np.where((per < 1.2) & (lon > Re) & (lon < d - 1),
                            np.clip((lon - Re) / (d - Re), 0, 1) * 0.75, 0)
            tramar(idx, dens * libre, 3, 4, corte=0.5)
            # lo que queda de la rasgadura
            dens = np.where((np.abs(dif) < RASGA) & (R >= Rb - 0.5) & (R < Re + 0.5),
                            0.3, 0)
            tramar(idx, dens * libre, 4, 5, corte=0.5)
            _cruz(idx, base.C + ux * d, base.C + uy * d, 1, 2, 3, libre)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
