"""NOVA - ART-UI-CHARGE v5, la barra de carga del Pulsar: «Los cierres».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 7.1, 13.12 (patron «Sonar»), 14.4

Lo comun a la ronda -la barra con remate, la comparativa- vive en
`gen_charge_v4.py`; el sitio y las comprobaciones, en `gen_charge_v1.py`.

LA TESIS DE LA v5: LISTA ES UNA CAJA QUE SE CIERRA. Cargando, la barra es
la de la v3, con el casquillo a la izquierda y el remate a la derecha. Al
llegar al final, los dos se UNEN: un marco de 1 px del color del poder
(indice 2) corre por arriba y por abajo de casquillo a remate y encierra la
mecha, que sigue ardiendo. Y por el marco da vueltas una LUZ blanca de 3 px
-el «Sonar» de 13.12, «lo que toca hacer ahora»-, una vuelta cada 2.67 s.
Una barra cargando es una linea; una lista es un rectangulo.
"""

import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_charge_v1 as base
from gen_charge_v1 import ALTO_MAX, FACCIONES, FRAMES, PODERES, coste, rampa_poder, rgba, tesela
import gen_charge_v3 as v3
import gen_charge_v4 as ronda
from gen_charge_v4 import ANCHO, CAP, LARGO, X0

VERSION = 5
NOMBRE = 'Los cierres'


def perimetro():
    """El marco: la fila de arriba de izquierda a derecha y la de abajo de vuelta."""
    arriba = [(0, x) for x in range(X0, X0 + LARGO)]
    abajo = [(ALTO_MAX - 1, x) for x in range(X0 + LARGO - 1, X0 - 1, -1)]
    return arriba + abajo


def pinta_lista(img, p, fac, f):
    R = rampa_poder(p)
    m = tesela(LARGO, v3.mecha(p, f))
    sub = img[:, X0:X0 + LARGO]
    mm = m[..., 3] > 0
    sub[mm] = m[mm]
    camino = perimetro()
    cab = (f % FRAMES) / FRAMES * len(camino)
    for i, (y, x) in enumerate(camino):
        d = (cab - i) % len(camino)
        c = R[0] if d < 1 else R[1] if d < 3 else R[2]
        img[y, x, :3] = c
        img[y, x, 3] = 255
    ronda.estrella(img, p, f)


def barra(p, inv, fac, pausada=False, f=0):
    return ronda.barra_con_remate(p, inv, fac, pausada, f, pinta_lista)


def comprobar_propio():
    fallos = ronda.comprobar_remate(sys.modules[__name__])
    for p in PODERES:
        b = barra(p, coste(p), 'player', False, 0)
        if not (b[0, X0:X0 + LARGO, 3] > 0).all() or not (b[ALTO_MAX - 1, X0:X0 + LARGO, 3] > 0).all():
            fallos.append(f'{p}: lista no cierra el marco')
        c = barra(p, coste(p) - 1, 'player', False, 0)
        if (c[0, X0:X0 + LARGO, 3] > 0).all():
            fallos.append(f'{p}: casi ya tiene marco')
    return fallos


def exportar(d):
    R = {p: rampa_poder(p) for p in PODERES}
    hexa = lambda c: '#%02X%02X%02X' % tuple(int(v) for v in c)
    ronda.exportar_comun(d, {
        'ready': 'frame', 'readyFrameRows': [0, ALTO_MAX - 1],
        'readyFrame': {p: hexa(R[p][2]) for p in PODERES},
        'readyLight': {p: hexa(R[p][0]) for p in PODERES},
        'readyLightTail': {p: hexa(R[p][1]) for p in PODERES},
        'note': 'cargando, como la v3 con la mecha de fuseLength; lista: la mecha entera que corre, un marco de 1 px '
                'en readyFrame por las filas 0 y 5 de fuseX0 a fuseX0 + fuseLength, y una luz que lo recorre (arriba '
                'de izquierda a derecha y abajo de vuelta): la cabeza en f / frames del camino, en readyLight, y 2 px '
                'de cola en readyLightTail; la llama lista en readyFlameX; el remate siempre'}, {})


if __name__ == '__main__':
    ronda.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                   base.colores_de('str_p', 'str_e', 'hielo', 'decaimiento', 'gusano'))
