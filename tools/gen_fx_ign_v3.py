"""NOVA - ART-FX-IGN v3, el encendido: «La bengala».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.2 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja, las dos tallas, las rampas de la
brasa, la franja del numero y las comprobaciones- vive en
`gen_fx_ign_v1.py`; aqui solo esta el dibujo.

LA TESIS: LA BRASA ESTALLA EN UNA FUENTE. Los cinco trazos de «El pedernal»
crepitaban hacia arriba esperando; al encender salen disparados de verdad,
desde lo alto del nodo, como una fuente de chispas: suben, se abren y caen
por los dos lados del cuerpo, enmarcandolo. Es fisica de feria: cada chispa
sigue una parabola con gravedad.

  - SALEN (frames 0-2). Cinco chispas en el abanico del pedernal (de -142 a
    -38 grados), en el indice 1, con una estela de dos puntos.
  - SE ABREN (frames 3-5). Llegan arriba del todo, frenan y empiezan a caer
    por fuera del cuerpo, una banda mas apagadas.
  - CAEN (frames 6-7). Bajan por los lados, hasta la altura del numero,
    y se apagan.

Sin rayos en estrella, por lo mismo que el pedernal: lo que nace tambien
puede ser un Pulsar o un Magnetar.

    py tools/gen_fx_ign_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_ign_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_ign_v1 as base
from gen_fx_ign_v1 import C, CELDA, borde, punto

VERSION = 3
NOMBRE  = 'La bengala'

ABANICO = tuple(math.radians(a) for a in (-142, -116, -90, -64, -38))
VEL     = 6.0       # px por frame al salir
GRAV    = 1.6       # px por frame al cuadrado, hacia abajo
EMPUJE  = 1.25      # abre las de los lados: mas velocidad horizontal


def _pos(Rb, a, j, t):
    x0, y0 = C + (Rb - 1.0) * math.cos(a), C + (Rb - 1.0) * math.sin(a)
    vx = VEL * math.cos(a) * EMPUJE
    vy = VEL * math.sin(a) * (0.9 + 0.05 * (j % 2))
    return x0 + vx * t, y0 + vy * t + 0.5 * GRAV * t * t


def dibujar(t, f):
    Rb = borde(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    for j, a in enumerate(ABANICO):
        tt = 0.55 * (f + 1)
        x, y = _pos(Rb, a, j, tt)
        banda = 1 if f < 3 else (2 if f < 6 else 3 + (f == 7))
        punto(idx, x, y, banda)
        if f < 6:
            punto(idx, x + 1, y, banda)
        # la estela: dos puntos de donde venia
        for k, b in ((0.2, 3), (0.4, 4)):
            if f < 7:
                px, py = _pos(Rb, a, j, max(tt - k, 0.0))
                punto(idx, px, py, min(banda + b - 1, 5), solo_vacio=True)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
