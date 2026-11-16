"""NOVA - ART-UI-POWERBAR v3, la barra de poder: «El horizonte».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.6 y 13.12 · NOVA_Arte_Tecnica.md,
regla 7

Lo comun a las tres propuestas -las piezas, el montaje, las comprobaciones y
las vistas- vive en `gen_powerbar_v1.py`; aqui solo esta el dibujo.

LA TESIS: SIN CAJA. Las otras dos meten la energia en un aparato; esta la
deja salir. La barra es el borde inferior de la pantalla encendido como un
amanecer: una linea caliente de lado a lado, la mitad en cada color, y
encima un resplandor en trama Bayer que sube y baja en lenguas, como la
corona de una estrella vista de canto. Por los huecos de la trama se sigue
viendo el cielo, asi que la barra le quita al tablero menos que ninguna,
y el cielo y la energia quedan en el mismo espacio.

  - LA BASE. Las dos filas de abajo, en el ancla de cada bando
    (`plasma_p`, `plasma_e`) con la de encima en el indice 1, sobre una
    franja de `#11142A` de 3 px que la separa del fondo.
  - EL RESPLANDOR. Diez filas en trama, del indice 3 al 5 segun la
    densidad; su altura la mueven dos ondas que viajan hacia el frente,
    asi que las lenguas corren hacia el otro bando.
  - EL FRENTE es una columna de luz blanca que sube por encima del
    resplandor y parpadea, con un halo en trama a los dos lados.
  - LA MITAD. Una muesca de 1 px en la franja oscura marca el 50 %.

Alfa binario y trama, como todo lo que se dibuja encima del tablero: la
excepcion de la regla 7 es para los menus, no para la partida.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_powerbar_v1 as base
from gen_powerbar_v1 import (ALTO, ANCHO, CONTACTO, FRAMES, FRENTE, PANEL,
                             TESELA, Tira, bayer, fase)
from nova_core import rampa

PLASMA = {'jugador': rampa('plasma_p'), 'enemigo': rampa('plasma_e')}
DIR = {'jugador': 1, 'enemigo': -1}
NEUTRAL = rampa('neutral')
X0, X1 = 0, ANCHO
BASE = ALTO - 2                     # filas 10 y 11: la linea caliente
FRANJA = ALTO - 3                   # desde la fila 9, el fondo oscuro


def marco():
    im = np.zeros((ALTO, ANCHO, 4), np.uint8)
    im[FRANJA:, :, :3] = PANEL
    im[FRANJA:, :, 3] = 255
    im[FRANJA, ANCHO // 2 - 1, :3] = NEUTRAL[3]
    return im


def altura(u, f, d):
    """Hasta donde sube el resplandor sobre la base, en filas: 2-8."""
    h = 0.55 + 0.25 * math.sin(fase(u, f, 1, 1, d) + 0.6) \
        + 0.17 * math.sin(fase(u, f, 3, 2, d) + 2.1) \
        + 0.08 * math.sin(fase(u, f, 7, 3, d))
    return 2 + 6 * min(max(h, 0.0), 1.0)


def relleno(fac, f, desfase=0):
    r, d = PLASMA[fac], DIR[fac]
    t = Tira(TESELA)
    for x in range(TESELA):
        u = x + desfase
        t.pon(x, ALTO - 1, r[3])
        t.pon(x, ALTO - 2, r[1] if math.sin(fase(u, f, 2, 3, d)) < 0.9 else r[0])
        top = altura(u, f, d)
        for y in range(0, BASE):
            h = BASE - y                       # 1 justo encima de la base
            if h == 1:                         # tapa la franja: sin agujeros
                t.pon(x, y, r[3])
                continue
            dens = 1.0 - h / (top + 0.5)
            if dens <= 0:
                continue
            dens = dens ** 0.8
            if dens > bayer(u, y):
                b = 3 if dens > 0.62 else (4 if dens > 0.3 else 5)
                t.pon(x, y, r[b])
    return t.rgba()


def frente(f):
    t = Tira(FRENTE)
    pj, pe = PLASMA['jugador'], PLASMA['enemigo']
    # la columna sube entre 7 y 12 filas, al ritmo de 8 frames
    alto = (12, 10, 11, 9, 8, 9, 7, 10)[f % 8] - (1 if f % 32 >= 16 else 0)
    for y in range(ALTO - alto, ALTO):
        t.pon(CONTACTO - 1, y, pj[0])
        t.pon(CONTACTO, y, pe[0])
    # halo en trama a los dos lados, mas denso abajo
    for i in range(1, 7):
        for y in range(ALTO - alto + 1, ALTO):
            dens = (1 - i / 7) * (0.35 + 0.65 * (y - (ALTO - alto)) / alto)
            for x, r in ((CONTACTO - 1 - i, pj), (CONTACTO + i, pe)):
                if dens > bayer(x + f, y):
                    t.pon(x, y, r[1] if dens > 0.55 else r[2])
    return t.rgba()


def piezas():
    return {
        'marco': marco(),
        'relleno': {fac: [relleno(fac, f) for f in range(FRAMES)]
                    for fac in ('jugador', 'enemigo')},
        'frente': [frente(f) for f in range(FRAMES)],
        'dibujo_relleno': relleno,
        'dibujo_frente': frente,
        'x0': X0, 'x1': X1, 'snap': 1,
        'min_frente': 16,
    }


if __name__ == '__main__':
    base.ejecutar(piezas, 3, 'El horizonte',
                  base.colores_de('plasma_p', 'plasma_e', 'neutral') | {tuple(PANEL)})
