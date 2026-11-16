"""NOVA - ART-FX-ABS v2, la absorcion aliada: «El llenado».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5 · Spec 6.2

Lo comun a las tres propuestas -la hoja, las tallas, la franja del numero y
las comprobaciones, entre ellas que el efecto entre y acabe en el cuerpo-
vive en `gen_fx_abs_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL NODO SE LLENA COMO UN VASO. Lo que hace una absorcion es subir
la Energia, y el numero que la cuenta esta justo debajo del nodo. Asi que la
luz sale de ahi, de los dos lados del numero, y sube por el borde del nodo
hasta arriba, como el liquido por las paredes de un vaso:

  - SUBE (frames 0-2). Dos arcos de 1 px pegados al cuerpo trepan desde
    los lados del numero, con la punta en el indice 1 y la cola apagandose.
    Cada frame llegan un tercio mas arriba.
  - LLENO (frame 3). Se juntan arriba, con una chispa de 3 px donde se
    encuentran, y el arco entero queda una banda mas apagado.

Es la unica de las tres que no entra desde fuera: no cambia de radio, cambia
de altura. Dice «ha subido» mas que «ha llegado».

    py tools/gen_fx_abs_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_abs_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_abs_v1 as base
from gen_fx_abs_v1 import ANG, CELDA, LIBRE, R, borde, send

VERSION = 2
NOMBRE  = 'El llenado'

COLA    = 0.45      # largo de la cola tras la punta, en radianes


def dibujar(t, f):
    Rb = borde(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    r = Rb + 1.5
    # altura en el borde: 0 abajo (el numero), pi arriba
    altura = np.abs(np.angle(np.exp(1j * (ANG - np.pi / 2))))
    # donde sale: el primer angulo libre por encima de la franja del numero
    sale = math.acos(max(-1.0, min(1.0, (send.MEDIO_NUMERO + 1) / r)))
    inicio = math.pi / 2 - sale             # altura del borde de la franja
    techo = inicio + (math.pi - inicio) * (f + 1) / 4
    aro = (R >= r - 0.5) & (R < r + 0.5) & LIBRE
    if f < 3:
        punta = aro & (altura <= techo) & (altura > techo - 0.18)
        cola = aro & (altura <= techo - 0.18) & (altura > techo - 0.18 - COLA)
        resto = aro & (altura <= techo - 0.18 - COLA) & (altura >= inicio - 0.05)
        idx[resto] = 4
        idx[cola] = 3
        idx[punta] = 1
    else:
        idx[aro & (altura >= inicio - 0.05)] = 2
        # la chispa arriba, donde se juntan, hacia dentro: no sale del arco
        dx, dy = base.send.DX, base.send.DY + r - 1.0
        cruz = (((np.abs(dx) <= 1.5) & (np.abs(dy) <= 0.5)) |
                ((np.abs(dx) <= 0.5) & (np.abs(dy) <= 1.5))) & (R < r + 0.5)
        idx[cruz] = 1
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
