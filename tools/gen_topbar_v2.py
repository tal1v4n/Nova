"""NOVA - ART-UI-TOPBAR v2, la barra de arriba: «Las pestanas».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.6, 13.10 y 13.12

Lo comun a las tres propuestas -las piezas, el montaje, las comprobaciones y
las vistas- vive en `gen_topbar_v1.py`; aqui solo esta el dibujo.

LA TESIS: COLGADAS DEL BORDE. Las placas no flotan: salen del borde de
arriba de la pantalla, como pestanas de un archivador, sin marco arriba y
con la esquina en escalera solo abajo. Asi el HUD de arriba y la carcasa de
abajo enmarcan el tablero entre los dos bordes, y lo de arriba ocupa 20 px
en vez de 24.

  - EL FILO. La pestana tiene el marco de `str_p[6]` a los lados y abajo
    una raya de 2 px en `str_p[5]`, un punto mas viva: es el borde que se
    ve desde el tablero.
  - EL BOTON es una pestana mas. Encima, la raya de abajo pasa al cian del
    Jugador (`str_p[3]`), porque es lo que se puede hacer (13.12). Pulsado,
    la pestana baja 2 px -se estira, porque esta colgada- y se llena de
    cian con el icono en oscuro: el boton primario de la lamina.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_topbar_v1 as base
from gen_topbar_v1 import (ALTO, BAJA, BOTON, CELDA_B, PANEL, PLACA_W, STR_P,
                           TEXTO_Y, escalera, icono_pausa, rgba)


def pestana(ancho, alto, lado, filo, relleno):
    sil = escalera(ancho, alto, abajo=True, arriba=False)
    y, x = np.mgrid[0:alto, 0:ancho]
    # el interior: dos px de marco a los lados y abajo, siguiendo la escalera
    dentro = sil.copy()
    p = np.pad(sil, 2, constant_values=False)
    p[:2, :] = True                       # arriba no hay marco: sigue la pantalla
    p[:2, :2] = False
    p[:2, -2:] = False
    for dy in range(-2, 3):
        for dx in range(-2, 3):
            dentro &= p[2 + dy:2 + dy + alto, 2 + dx:2 + dx + ancho]
    rgb = np.zeros((alto, ancho, 3), np.uint8)
    rgb[sil] = lado
    rgb[sil & (y >= alto - 2)] = filo
    rgb[dentro] = relleno
    return rgb, sil


def placa():
    rgb, sil = pestana(PLACA_W, ALTO, STR_P[6], STR_P[5], PANEL)
    return rgba(rgb, sil)


def boton(estado):
    h, w = CELDA_B
    rgb = np.zeros((h, w, 3), np.uint8)
    on = np.zeros((h, w), bool)
    if estado == 2:
        r, s = pestana(BOTON, ALTO + BAJA, STR_P[4], STR_P[2], STR_P[3])
        rgb[:] = r
        on[:] = s
        icono_pausa(rgb, on, 6, TEXTO_Y + BAJA, PANEL)
    else:
        filo = STR_P[3] if estado == 1 else STR_P[5]
        r, s = pestana(BOTON, ALTO, STR_P[6], filo, PANEL)
        rgb[:ALTO] = r
        on[:ALTO] = s
        icono_pausa(rgb, on, 6, TEXTO_Y, STR_P[0] if estado == 1 else STR_P[1])
    return rgba(rgb, on)


def piezas():
    return {'placa': placa(),
            'pausa': [boton(e) for e in range(3)],
            'color_texto': {'nivel': STR_P[1], 'crono': STR_P[1]},
            'arriba': 0}


if __name__ == '__main__':
    base.ejecutar(piezas, 2, 'Las pestanas',
                  base.colores_de('str_p') | {tuple(PANEL)})
