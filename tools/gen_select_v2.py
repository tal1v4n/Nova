"""NOVA - Generador del anillo de seleccion (ART-WLD-SEL), v2: EL ARCO.

Ref: NOVA_Assets_Diseno.md A.4.3 · 13.1 · 14.7
Propuesta alternativa a `gen_select_v1.py` («La orbita»). Las dos conviven y
comparten tallas, franja del numero, comprobaciones y vistas.

    .venv\\Scripts\\python tools\\gen_select_v2.py          las cinco tallas
    .venv\\Scripts\\python tools\\gen_select_v2.py --vista  ademas, las vistas en
                                                         art/_preview/select/v2/

LA PROPUESTA. Lo contrario de la v1: QUIETO. Un arco de 2 px del cian del
Jugador, con la cara de dentro una banda mas clara, abierto por abajo en la
franja del numero, y con las puntas que se apagan en tres pasos hacia el
hueco para que la abertura se lea como un corte hecho a proposito y no como
un anillo roto. Un frame por talla.

Por que quieto puede bastar: la seleccion no es un estado del nodo, es una
decision del jugador que el jugador acaba de tomar; no hace falta llamarle la
atencion sobre ella, solo que este ahi cuando mira. Y en el tablero ya hay
mucho moviendose -idle, lineas de envio, protones-. Lo que paga: con seis
nodos en un lazo se ven seis aros iguales y fijos, y un aro fijo es lo que
mas se parece al anillo del Magnetar, aunque este sea mas pequeno, macizo y
de otro cian.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, rampa

import gen_select_v1 as V1

VERSION = 2
FRAMES = 1

FUERA, DENTRO = 3, 2     # bandas: el pixel de fuera y el de dentro del arco
PUNTAS = (4, 4, 5)       # las tres ultimas columnas de arco antes del hueco


def anillo(R, t=0.0):
    ramp = rampa(V1.RAMPA)
    dx, dy, d, ang = V1._malla()
    out = np.zeros((V1.CELDA, V1.CELDA, 4), np.uint8)
    libre = ~V1.franja_numero(dx, dy)
    arco = (d > R) & (d <= R + 2) & libre

    def poner(m, banda):
        out[m, :3] = ramp[banda]
        out[m, 3] = 255

    poner(arco & (d > R + 1), FUERA)
    poner(arco & (d <= R + 1), DENTRO)
    # puntas: distancia horizontal al borde de la franja, solo en la mitad de abajo
    al_hueco = np.abs(dx) - V1.NUMERO_MEDIO_ANCHO
    for i, banda in enumerate(reversed(PUNTAS)):
        poner(arco & (dy > 0) & (al_hueco > i) & (al_hueco <= i + 1), banda)
    return out


def main():
    raiz = os.path.join(RAIZ, 'art', 'world', V1.ASSET, f'v{VERSION}')
    os.makedirs(raiz, exist_ok=True)
    print(f"{'talla':>6} {'pixeles':>8}")
    for R in V1.TALLAS:
        img = anillo(R)
        dx, dy, d, _ = V1._malla()
        on = img[..., 3] > 0
        if not np.isin(img[..., 3], (0, 255)).all():
            raise SystemExit(f'T{R}: alfa no binario')
        if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
            raise SystemExit(f'T{R}: toca el borde de la celda')
        if (on & (d <= R - 0.5)).any() or (on & V1.franja_numero(dx, dy)).any():
            raise SystemExit(f'T{R}: entra en el radio libre o en la franja del numero')
        Image.fromarray(img, 'RGBA').save(V1.ruta(raiz, R, ''))
        print(f'{R:6d} {int(on.sum()):8d}')
    V1.exporta_tallas(raiz)
    if '--vista' in sys.argv:
        V1.vistas(anillo, VERSION, FRAMES)


if __name__ == '__main__':
    main()
