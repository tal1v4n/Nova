"""NOVA - ART-FX-DMG v2, el dano sin captura: «Las esquirlas».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5 · Spec 6.2

Lo comun a las tres propuestas -la hoja, las tallas, el color del que
ataca, las cuatro variantes, la franja del numero y las comprobaciones-
vive en `gen_fx_dmg_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL GOLPE ARRANCA TROZOS. El dano le quita Energia al nodo, y lo que
se ve es eso: pedazos del borde que saltan, como la metralla de un impacto.
No es un aro -ni el que sale ni el que entra-: son siete esquirlas sueltas,
en angulos desiguales, que dan vueltas mientras se alejan.

  - IMPACTO (frame 0). Siete esquirlas de 2 px en el indice 1, en el borde,
    radiales: acaban de saltar.
  - METRALLA (frames 1-3). Se alejan 2.5 px por frame, giran (radiales,
    tangentes, radiales), encogen a 1 px y se apagan una banda por frame.

    py tools/gen_fx_dmg_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_dmg_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_dmg_v1 as base
from gen_fx_dmg_v1 import C, CELDA, borde, punto

VERSION = 2
NOMBRE  = 'Las esquirlas'

ESQUIRLAS = 7
SALTO     = 2.5      # px por frame


def _angulos(t, v):
    rng = np.random.default_rng(500 + 11 * v + t)
    base_a = rng.uniform(0, 2 * math.pi)
    return [base_a + 2 * math.pi * k / ESQUIRLAS + rng.uniform(-0.3, 0.3)
            for k in range(ESQUIRLAS)]


def dibujar(t, f, v):
    Rb = borde(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    for k, a in enumerate(_angulos(t, v)):
        ux, uy = math.cos(a), math.sin(a)
        px, py = -uy, ux
        r = Rb - 1.0 + SALTO * f
        x, y = C + r * ux, C + r * uy
        banda = 1 + f
        if f == 3:
            punto(idx, x, y, 4)
            continue
        # gira: radial en los frames 0 y 2, tangente en el 1 (sentido segun k)
        if f == 1:
            dx, dy = px * (1 if k % 2 else -1), py * (1 if k % 2 else -1)
        else:
            dx, dy = ux, uy
        punto(idx, x, y, banda)
        # en el frame 2 la cola ya va una banda por debajo de la cabeza
        punto(idx, x + dx, y + dy, banda + (f == 2))
        if f == 0:
            punto(idx, x - dx, y - dy, 2)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
