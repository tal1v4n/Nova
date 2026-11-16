"""NOVA - ART-UI-POWERBAR v2, la barra de poder: «El instrumento».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.6 y 13.12

Lo comun a las tres propuestas -las piezas, el montaje, las comprobaciones y
las vistas- vive en `gen_powerbar_v1.py`; aqui solo esta el dibujo.

LA TESIS: UN INDICADOR DE A BORDO. 13.12 pide que la UI sea un instrumento,
y esta barra lo es al pie de la letra: una regleta de celdas de 3 px con
1 px de separacion, como los segmentos de un vumetro. Cada bando enciende
las suyas en su color de interfaz (`str_p` y `str_e`, el ancla en el
indice 3) y las vacias quedan en `mag_p[6]`, apagadas pero visibles. Sobre
la carcasa, tres marcas en el 25, el 50 y el 75 %; la del medio, mas clara,
es la que importa: «mas de la mitad» (13.11).

  - EL FLUJO. Un pulso de luz recorre las celdas hacia el frente, una celda
    cada dos frames, uno por tesela: la energia empuja sin moverse la
    barra.
  - EL FRENTE es la AGUJA: una raya clara de lado a lado de la carcasa en
    la separacion entre las dos ultimas celdas, y a cada lado la celda que
    manda, que late en blanco a contratiempo de la del otro bando.

El reparto se ajusta a la rejilla de celdas (`snap` = 4): el frente avanza
de celda en celda, como un instrumento, y nunca parte una por la mitad.
Es la mas quieta de las tres y la que menos le quita al tablero.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_powerbar_v1 as base
from gen_powerbar_v1 import (ALTO, ANCHO, CONTACTO, FRAMES, FRENTE, PANEL,
                             TESELA, Tira, escalera, fase)
from nova_core import rampa

UI = {'jugador': rampa('str_p'), 'enemigo': rampa('str_e')}
DIR = {'jugador': 1, 'enemigo': -1}
MARCO = rampa('str_p')[6]
VACIA = rampa('mag_p')[6]          # '#1D2144', la raya de la lamina de UI
NEUTRAL = rampa('neutral')
X0, X1 = 4, ANCHO - 4
PASO = 4                            # celda de 3 px y 1 de separacion
C0, C1 = 3, 9                       # filas de las celdas


def marco():
    silueta, dentro = escalera(ANCHO, ALTO)
    im = np.zeros((ALTO, ANCHO, 4), np.uint8)
    im[silueta & ~dentro, :3] = MARCO
    im[dentro, :3] = PANEL
    im[silueta, 3] = 255
    for x in range(X0, X1):
        if (x - X0) % PASO < 3:
            im[C0:C1, x, :3] = VACIA
    largo = X1 - X0
    for q, color in ((0.25, NEUTRAL[4]), (0.5, NEUTRAL[2]), (0.75, NEUTRAL[4])):
        x = X0 + int(largo * q) - 1        # en la separacion entre dos celdas
        im[0:2, x, :3] = color
        im[ALTO - 2:ALTO, x, :3] = color
    return im


def relleno(fac, f, desfase=0):
    r, d = UI[fac], DIR[fac]
    t = Tira(TESELA)
    for x in range(TESELA):
        u = x + desfase
        if u % PASO == 3:
            continue
        k = u - u % PASO + 1.5               # el centro de la celda
        pulso = math.cos(fase(k, f, 1, 1, d))
        luz = 2 if pulso > 0.92 else (1 if pulso > 0.6 else 0)
        for y in range(C0, C1):
            if y == C0:
                b = 2 - (1 if luz == 2 else 0)
            elif y == C1 - 1:
                b = 4 - luz
            else:
                b = 3 - luz
            t.pon(x, y, r[b])
    return t.rgba()


def frente(f):
    t = Tira(FRENTE)
    ritmo = (0, 0, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2)   # 1.33 s
    for fac, x0 in (('jugador', CONTACTO - PASO), ('enemigo', CONTACTO)):
        r = UI[fac]
        b = ritmo[(f + (8 if fac == 'enemigo' else 0)) % 16]
        for x in range(x0, x0 + 3):
            for y in range(C0, C1):
                t.pon(x, y, r[b] if y < C1 - 1 else r[b + 1])
    aguja = NEUTRAL[1]
    for y in range(ALTO):
        t.pon(CONTACTO - 1, y, aguja)
    # la punta de la aguja asoma un pixel mas clara en los extremos, y la
    # cabeza sube y baja una fila cada 16 frames: el frame 0 y el 16 se
    # distinguen por ella
    cab = 0 if f % 32 < 16 else 1
    t.pon(CONTACTO - 1, cab, NEUTRAL[0])
    t.pon(CONTACTO - 1, ALTO - 1 - cab, NEUTRAL[0])
    return t.rgba()


def piezas():
    return {
        'marco': marco(),
        'relleno': {fac: [relleno(fac, f) for f in range(FRAMES)]
                    for fac in ('jugador', 'enemigo')},
        'frente': [frente(f) for f in range(FRAMES)],
        'dibujo_relleno': relleno,
        'dibujo_frente': frente,
        'x0': X0, 'x1': X1, 'snap': PASO,
        # el pulso salta de celda cada dos frames y la aguja se queda quieta:
        # se repiten a proposito
        'min_relleno': FRAMES // 2, 'min_frente': 8,
    }


if __name__ == '__main__':
    base.ejecutar(piezas, 2, 'El instrumento',
                  base.colores_de('str_p', 'str_e', 'neutral')
                  | {tuple(VACIA), tuple(PANEL)})
