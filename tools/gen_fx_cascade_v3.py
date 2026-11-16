"""NOVA - ART-FX-CASCADE v3, la cascada de conquista: «Las luciernagas».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5 · Spec 10.1

Lo comun a las tres propuestas -un efecto por nodo, la hoja de cinco
tallas, la rampa del Jugador, la caja del numero, convivir con «El relevo»
y las comprobaciones- vive en `gen_fx_cascade_v1.py`; aqui solo esta el
dibujo.

LA TESIS: NO SE CONQUISTA, SE ACUDE. Las otras dos celebran o conectan; esta
es la mas tranquila. La partida ya esta ganada y nadie se resiste: los
nodos que quedan no se toman, se llenan. Un enjambre de luces del color del
Jugador llega desde el aire alrededor, cada una por su curva y parpadeando
a su ritmo, como luciernagas, y se posa en el nodo. Con varios nodos en
cadena, el mapa se va encendiendo como un prado al anochecer.

  - APARECEN (frame 0). Dieciocho luces al borde de la celda, algunas ya
    en el indice 1.
  - ACUDEN (frames 1-8). Cada una se acerca por una curva, a su velocidad,
    y parpadea: encendida en el indice 1 o 2, a media luz en el 3 o 4.
  - SE POSAN (al llegar). Un destello en cruz en el borde del cuerpo y se
    apaga en tres frames.

    py tools/gen_fx_cascade_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_cascade_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_cascade_v1 as base
from gen_fx_cascade_v1 import C, CELDA, punto
from gen_fx_send_v1 import borde

VERSION = 3
NOMBRE  = 'Las luciernagas'


def _luces():
    rng = np.random.default_rng(61)
    out = []
    n = 18
    base_a = rng.random() * 2 * math.pi
    for i in range(n):
        a0 = base_a + 2 * math.pi * i / n + rng.uniform(-0.15, 0.15)
        r0 = rng.uniform(34, 41)
        giro = rng.choice((-1, 1)) * rng.uniform(0.4, 0.9)
        llega = int(rng.choice((5, 6, 6, 7, 7, 8, 8, 9)))
        fase = int(rng.integers(0, 3))
        out.append((a0, r0, giro, llega, fase))
    return out


LUCES = _luces()


def donde(tt, a0, r0, giro, llega, f):
    s = min(f / llega, 1.0)
    e = 1 - (1 - s) ** 1.6                       # llegan frenando
    r1 = max(borde(tt.t) - 2.0, 6.0)
    r = r0 + (r1 - r0) * e
    a = a0 + giro * e + 0.08 * math.sin(f * 1.7 + a0 * 3)
    return C + r * math.cos(a), C + r * math.sin(a)


def dibujar(tt, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    for a0, r0, giro, llega, fase in LUCES:
        if f < llega:
            x, y = donde(tt, a0, r0, giro, llega, f)
            encendida = (f + fase) % 3 != 2
            b = (1 if (f + fase) % 3 == 0 else 2) if encendida else 4
            punto(idx, tt, x, y, b)
            if encendida:
                punto(idx, tt, x + 1, y, b + 1)
                punto(idx, tt, x, y + 1, b + 1)
                punto(idx, tt, x + 1, y + 1, min(b + 2, 5))
            # la estela: donde estaba el frame anterior, muy tenue
            if f >= 1:
                xp, yp = donde(tt, a0, r0, giro, llega, f - 1)
                punto(idx, tt, xp, yp, 5)
        else:
            k = f - llega                        # se posa
            if k > 2:
                continue
            x, y = donde(tt, a0, r0, giro, llega, llega)
            if k == 0:
                for dx, dy, b in ((0, 0, 1), (1, 0, 2), (-1, 0, 2), (0, 1, 2), (0, -1, 2),
                                  (2, 0, 3), (-2, 0, 3), (0, 2, 3), (0, -2, 3)):
                    punto(idx, tt, x + dx, y + dy, b)
            elif k == 1:
                for dx, dy, b in ((0, 0, 2), (1, 0, 4), (-1, 0, 4), (0, 1, 4), (0, -1, 4)):
                    punto(idx, tt, x + dx, y + dy, b)
            else:
                punto(idx, tt, x, y, 5)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
