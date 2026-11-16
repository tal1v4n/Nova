"""NOVA - ART-FX-UPG v2, la mejora de nivel: «Los galones».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de veinte filas, las rampas, la caja
del numero y las comprobaciones- vive en `gen_fx_upg_v1.py`; aqui solo esta
el dibujo.

LA TESIS: EL EFECTO DICE EL NIVEL. Una mejora sube un rango, y el rango se
lleva en la manga: tantos galones como el nivel nuevo -dos, tres, cuatro o
cinco- suben por el cuerpo del nodo y salen por arriba. A.2 admite que la
Estrella N3 y la N4 se parecen a 1x (*Riesgo conocido*): aqui el jugador
cuenta el nivel en el instante en que cambia, sin abrir el panel.

  - EL GOLPE (frame 0). La pila entera aparece en el centro del cuerpo,
    apretada, en el indice 1, con un aro de trama en el borde nuevo.
  - SUBEN (frames 1-4). La pila sube 3-4 px por frame y se apaga una banda
    cada frame desde el 3. Cada galon va contorneado arriba y abajo en el
    indice 5, para leerse sobre la luz del cuerpo.
  - SE VAN (frame 5). Quedan las puntas de los dos ultimos, ralas.

    py tools/gen_fx_upg_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_upg_v2.py --gif    y las animadas
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_upg_v1 as base
from gen_fx_upg_v1 import C, CELDA, R, tramar

VERSION = 2
NOMBRE  = 'Los galones'

MEDIO   = 6                    # medio ancho del galon: 13 px
PASO    = 4                    # px entre galones: contorno, 2 de luz, contorno
SUBIDA  = (0, 3, 6, 10, 14, 18)
LUZ     = (1, 1, 1, 2, 3, 4)


def galon(idx, s, cx, cy, luz, sombra, rala=None):
    """Un galon en V invertida, de 2 px de grueso con la sombra debajo.
    `cy` es la fila del vertice."""
    for dx in range(-MEDIO, MEDIO + 1):
        y = cy + abs(dx)
        for k, b in ((-1, sombra), (0, luz), (1, luz), (2, sombra)):
            x, yy = int(cx + dx), int(y + k)
            if rala is not None and (x + yy) % rala:
                continue
            if 0 <= x < CELDA and 0 <= yy < CELDA and s.libre[yy, x]:
                if idx[yy, x] == 0 or b < idx[yy, x] or b == luz:
                    idx[yy, x] = b


def dibujar(s, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    n = s.n
    # la pila, centrada en el cuerpo y subiendo
    y0 = C - ((n - 1) * PASO + MEDIO) / 2 - SUBIDA[f]
    for j in range(n):
        y = round(y0 + j * PASO)
        if f == 5:
            if j < n - 2:
                continue
            galon(idx, s, C, y, 4, 5, rala=2)
        else:
            galon(idx, s, C, y, LUZ[f], 5)
    if f <= 1:
        aro = (R >= s.despues - 0.5) & (R < s.despues + 1.5) & s.libre
        dens = np.where(aro, (0.6, 0.3)[f], 0)
        tramar(idx, dens, 2, 3, corte=0.45)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
