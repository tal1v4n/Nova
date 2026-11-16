"""NOVA - ART-FX-CAST v3, el lanzamiento de poder: «El anillo».

Ref: NOVA_Assets_Diseno.md A.5 y A.2.3 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de nueve filas, las rampas de los
poderes, la geometria de «El Faro», la caja del numero y las comprobaciones-
vive en `gen_fx_cast_v1.py`; aqui solo esta el dibujo.

LA TESIS: LA BANDA SE DESCARGA. «El Faro» tiene una banda ecuatorial que
dice que gira, y es la unica pieza del Pulsar que no es luz ni cuerpo: es
la maquina. Al lanzar, la maquina suelta lo que tenia: la banda se enciende
en el color del poder y sale disparada como un aro inclinado, en el plano
del ecuador, perpendicular al eje de los conos. Los conos dicen hacia donde
mira el faro; el aro, que algo ha salido de el.

  - SE ENCIENDE (frame 0). Un aro en el sitio de la banda, en el indice 1.
  - SALE (frames 1-3). Se abre 6 px por frame, se adelgaza y se apaga.
  - SE ROMPE (frame 4). Quedan tramos sueltos.

    py tools/gen_fx_cast_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_cast_v3.py --gif    y las animadas
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_cast_v1 as base
import gen_pulsar_v2 as faro
from gen_fx_cast_v1 import BAYER, CELDA, U, V

VERSION = 3
NOMBRE  = 'El anillo'

ABRE    = (0, 6, 12, 18, 23)
GROSOR  = (2.6, 2.2, 1.7, 1.2, 1.0)
BANDA   = (1, 1, 2, 3, 4)
ASPECTO = 0.30                    # el de la banda de «El Faro»


def dibujar(fa, poder, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    a = fa.cfg['ban'] + ABRE[f]
    b = a * ASPECTO
    # el aro esta en el plano del ecuador: eje mayor a lo largo de V
    rho = np.hypot(V / a, U / b)
    dist = (rho - 1) * rho / np.maximum(np.hypot(V / a ** 2, U / b ** 2), 1e-6)
    aro = (np.abs(dist) < GROSOR[f] / 2) & fa.libre
    if f == 4:
        aro &= BAYER > 0.45
    idx[aro] = BANDA[f]
    if f == 0:
        # el halo del encendido, por fuera del aro
        halo = (dist >= GROSOR[0] / 2) & (dist < GROSOR[0] / 2 + 2.5) & fa.libre & (BAYER > 0.5)
        idx[halo & (idx == 0)] = 3
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
