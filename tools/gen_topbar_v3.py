"""NOVA - ART-UI-TOPBAR v3, la barra de arriba: «Las lecturas».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.6, 13.10 y 13.12 ·
NOVA_Arte_Tecnica.md, regla 7

Lo comun a las tres propuestas -las piezas, el montaje, las comprobaciones y
las vistas- vive en `gen_topbar_v1.py`; aqui solo esta el dibujo.

LA TESIS: SIN APARATO. El nivel y el tiempo son lecturas, no mandos: no
necesitan una caja. El texto va directamente sobre el cielo, y lo unico que
hay detras es una sombra en trama Bayer del color del espacio (`#0B0E1A`),
densa en el centro y deshecha hacia los bordes, lo justo para que una
estrella del fondo no se meta entre dos letras. El tablero se sigue viendo
alrededor y a traves de ella.

  - LA JERARQUIA por color, no por caja: el nivel en `neutral[2]`, gris,
    secundario (13.12); el tiempo en `neutral[1]`, el claro de los textos.
  - EL BOTON es el unico mando, y por eso el unico que se enciende: el
    icono en `neutral[1]` sobre su sombra; encima, el icono pasa a cian
    (`str_p[2]`) y aparece un anillo en escalera en `str_p[3]`; pulsado,
    el icono baja 2 px.

Alfa binario y trama: esto se dibuja encima del tablero en partida, y la
excepcion de la regla 7 es solo para los menus.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_topbar_v1 as base
from gen_topbar_v1 import (ALTO, BAJA, BORDE, BOTON, CELDA_B, FONDO, PLACA_W,
                           STR_P, TEXTO_Y, bayer, erosion, escalera,
                           icono_pausa, rgba)
from nova_core import rampa

NEUTRAL = rampa('neutral')


def sombra(ancho, alto, borde_x):
    """Densidad: plena en el centro y cayendo en los `borde_x` px de los
    lados y en las 3 filas de arriba y de abajo."""
    rgb = np.zeros((alto, ancho, 3), np.uint8)
    on = np.zeros((alto, ancho), bool)
    for y in range(alto):
        dy = min(y, alto - 1 - y)
        fy = min(1.0, (dy + 0.5) / 4)
        for x in range(ancho):
            dx = min(x, ancho - 1 - x)
            fx = min(1.0, (dx + 0.5) / borde_x)
            if 0.9 * fx * fy > bayer(x, y):
                rgb[y, x] = FONDO
                on[y, x] = True
    return rgb, on


def placa():
    rgb, on = sombra(PLACA_W, ALTO, BORDE)
    # el centro del mosaico tiene que ser denso entero: el borde solo cae
    # en los 6 px de cada lado
    return rgba(rgb, on)


def boton(estado):
    h, w = CELDA_B
    rgb = np.zeros((h, w, 3), np.uint8)
    on = np.zeros((h, w), bool)
    r, s = sombra(BOTON, ALTO, 6)
    rgb[:ALTO], on[:ALTO] = r, s
    if estado == 1:
        sil = escalera(BOTON, ALTO)
        aro = sil & ~erosion(sil, 1)
        rgb[:ALTO][aro] = STR_P[3]
        on[:ALTO] |= aro
    dy = BAJA if estado == 2 else 0
    color = STR_P[2] if estado in (1, 2) else NEUTRAL[1]
    icono_pausa(rgb, on, 6, TEXTO_Y + dy, color)
    return rgba(rgb, on)


def piezas():
    return {'placa': placa(),
            'pausa': [boton(e) for e in range(3)],
            'color_texto': {'nivel': NEUTRAL[2], 'crono': NEUTRAL[1]}}


if __name__ == '__main__':
    base.ejecutar(piezas, 3, 'Las lecturas',
                  base.colores_de('str_p', 'neutral') | {tuple(FONDO)})
