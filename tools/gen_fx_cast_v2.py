"""NOVA - ART-FX-CAST v2, el lanzamiento de poder: «La vuelta».

Ref: NOVA_Assets_Diseno.md A.5 y A.2.3 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de nueve filas, las rampas de los
poderes, la geometria de «El Faro», la caja del numero y las comprobaciones-
vive en `gen_fx_cast_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL FARO DA UNA VUELTA. Un pulsar es una estrella de neutrones que
gira, y lo que el universo ve de el son sus dos conos barriendo el cielo. En
el idle el eje esta quieto; al lanzar, los conos dan media vuelta -que con
dos conos es el cielo entero- en el color del poder, dejando detras una
estela como la de una pantalla de radar. El poder sale hacia cualquier
sitio, y el barrido dice justo eso: a todas partes.

  - BARRE (frames 0-3). Los dos conos, en el indice 1 en su eje, giran 45
    grados por frame. Detras, la posicion anterior se queda una banda mas
    apagada y rala.
  - VUELVE (frame 4). Los conos llegan a su eje de siempre y solo queda la
    estela del ultimo tramo.

    py tools/gen_fx_cast_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_cast_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_cast_v1 as base
import gen_pulsar_v2 as faro
from gen_fx_cast_v1 import CELDA, DX, DY, tramar

VERSION = 2
NOMBRE  = 'La vuelta'

GIRO    = math.radians(45)


def cono_girado(fa, giro, k):
    ang = faro.PHI + giro
    ux, uy = math.cos(ang), math.sin(ang)
    u = DX * ux + DY * uy
    v = -DX * uy + DY * ux
    a = np.abs(u)
    largo = fa.cfg['largo']
    dentro = (a > fa.cfg['ry']) & (a < largo) & (np.abs(v) < a * faro.SEMIANCHO * 0.8)
    eje = 1 - np.clip(np.abs(v) / np.maximum(a * faro.SEMIANCHO * 0.8, 1e-6), 0, 1)
    lejos = 1 - np.clip((a - fa.cfg['ry']) / (largo - fa.cfg['ry']), 0, 1)
    return np.where(dentro, (0.3 + 0.7 * eje) * (0.45 + 0.55 * lejos) * k, 0) * fa.libre


def dibujar(fa, poder, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    # en el frame 4 los conos ya estan en su eje: no se dibujan, el idle los pone
    if f < 4:
        tramar(idx, cono_girado(fa, GIRO * f, 1.0), 1 if f < 2 else 2, 3, corte=0.55)
    if f >= 1:
        k = (0, 0.45, 0.4, 0.35, 0.3)[f]
        tramar(idx, cono_girado(fa, GIRO * (f - 1), k), 3 if f < 3 else 4, 5, corte=0.35)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
