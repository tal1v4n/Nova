"""NOVA - Generador del lazo de seleccion (ART-WLD-LASSO), v2: EL VELO.

Ref: NOVA_Assets_Diseno.md A.4.3 · 13.1 · 13.12
Propuesta alternativa a `gen_lasso_v1.py` («El marco»). Comparten esquina,
montaje y vistas.

    .venv\\Scripts\\python tools\\gen_lasso_v2.py          las piezas
    .venv\\Scripts\\python tools\\gen_lasso_v2.py --vista  ademas, las vistas en
                                                        art/_preview/lasso/v2/

LA PROPUESTA. Quieta, y con area. La linea es continua, una banda mas oscura
que los trazos de la v1 (`STR_P[4]`), con las mismas esquinas en escalera
mas claras, y por DENTRO un velo de trama Bayer muy rala (`STR_P[5]`, el 10 %
de los pixeles): los velos de 13.12 a la escala del tablero. El velo va
DETRAS de los nodos, asi que lo que cubre se ve sin manchar: dice «esta zona»,
que es lo que el lazo selecciona, en vez de «este borde».

Tres piezas: la esquina de la v1, un tramo de 8 x 1 continuo y una tesela de
velo de 8 x 8 que Unity repite anclada a la rejilla del mundo, no al
rectangulo: si se anclara al rectangulo, la trama nadaria al crecer el lazo.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import BAYER8, RAIZ, rampa

import gen_lasso_v1 as V1

VERSION = 2
FRAMES = 1
B_LINEA = 4
B_VELO = 5
VELO = 0.10


def tramo(f=0):
    ramp = rampa(V1.RAMPA)
    out = np.zeros((1, V1.PERIODO, 4), np.uint8)
    out[0, :, :3] = ramp[B_LINEA]
    out[0, :, 3] = 255
    return out


def velo():
    """RGBA (8, 8): la tesela de trama del interior."""
    ramp = rampa(V1.RAMPA)
    out = np.zeros((8, 8, 4), np.uint8)
    m = BAYER8 < VELO
    out[m, :3] = ramp[B_VELO]
    out[m, 3] = 255
    return out


def velo_en(ancho, alto):
    """El interior con el velo, sin el borde: lo que va detras de los nodos."""
    t = velo()
    out = np.tile(t, (alto // 8 + 1, ancho // 8 + 1, 1))[:alto, :ancho].copy()
    out[[0, -1], :] = 0
    out[:, [0, -1]] = 0
    return out


def monta(ancho, alto, f=0):
    return V1.monta(ancho, alto, 0, tramo_fn=tramo)


monta.detras = velo_en


def main():
    raiz = os.path.join(RAIZ, 'art', 'world', V1.ASSET, f'v{VERSION}')
    V1.guarda(raiz, tramo, FRAMES, V1.esquina)
    Image.fromarray(velo(), 'RGBA').save(os.path.join(raiz, 'world_lasso_veil.png'))
    print(f'esquina, tramo continuo y velo de 8x8 al {int((velo()[..., 3] > 0).mean() * 100)} %')
    if '--vista' in sys.argv:
        V1.vistas(monta, VERSION, FRAMES)


if __name__ == '__main__':
    main()
