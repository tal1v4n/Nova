"""NOVA - ART-FX-DMG v3, el dano sin captura: «El diente de sierra».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5 · Spec 6.2

Lo comun a las tres propuestas -la hoja, las tallas, el color del que
ataca, las cuatro variantes, la franja del numero y las comprobaciones-
vive en `gen_fx_dmg_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL BORDE SE ERIZA DE DOLOR. Es el dibujo con el que los comics de la
epoca pintan un golpe: el contorno de lo golpeado deja de ser liso y se
vuelve una sierra. Aqui el borde del nodo se cine de dientes, en el color
del que ataca, y se deshace:

  - GOLPE (frame 0). Un aro de 16 dientes de 3 px de alto pegado al cuerpo,
    en el indice 1.
  - DESHACE (frames 1-3). Los dientes crecen y se separan 1 px por frame,
    se apagan una banda y se rompen, hasta quedar unas puntas sueltas.

Comparte la forma de aro con el envio y la absorcion, pero no la linea:
aquellos son lisos, este es una sierra. Las variantes giran los dientes.

    py tools/gen_fx_dmg_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_dmg_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_dmg_v1 as base
from gen_fx_dmg_v1 import C, CELDA, borde, punto

VERSION = 3
NOMBRE  = 'El diente de sierra'

DIENTES = 16
ALTO    = (3.0, 4.0, 4.0, 3.0)
QUEDA   = (1.0, 0.7, 0.4, 0.15)     # fraccion de la sierra que sigue entera


def dibujar(t, f, v):
    Rb = borde(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    fase = v / 4.0
    rng = np.random.default_rng(700 + v)
    corte = rng.uniform(0, 2 * math.pi, 3)
    for th in np.linspace(0, 2 * math.pi, 1400, endpoint=False):
        u = (DIENTES * th / (2 * math.pi) + fase) % 1.0
        tri = 1 - abs(2 * u - 1)
        ruido = (math.sin(3 * th + corte[0]) + 0.7 * math.sin(7 * th + corte[1])
                 + 0.5 * math.sin(11 * th + corte[2])) / 2.2
        if (ruido + 1) / 2 > QUEDA[f]:
            continue
        r = Rb - 1.0 + f + ALTO[f] * tri
        punto(idx, C + r * math.cos(th), C + r * math.sin(th), 1 + f)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
