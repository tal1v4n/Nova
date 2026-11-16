"""NOVA - ART-FX-CURE v2, la cura de Decaimiento: «El barrido».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.5 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de cinco tallas y dieciseis
variantes, la rampa del decaimiento, la caja del numero, la continuidad con
«El miasma» y las comprobaciones- vive en `gen_fx_cure_v1.py`; aqui solo
esta el dibujo.

LA TESIS: LA CURA LIMPIA DE ARRIBA ABAJO. Un frente de luz baja por el nodo
como un trapo por un cristal: por encima queda limpio, por debajo sigue el
miasma, y en el frente la nube se enciende al irse. Barre en la misma
direccion en que el miasma caia, y se lo lleva por donde se escurria.

  - EMPIEZA (frame 0). La nube entera del frame del bucle, con el frente
    asomando por arriba.
  - BARRE (frames 1-3). El frente baja hasta la caja del numero: una banda de 2 px
    en el indice 1-2 que solo existe donde habia nube, y unas chispas
    limpias que quedan detras.
  - ACABA (frame 4). Del frente queda un rastro ralo, encima del numero.

    py tools/gen_fx_cure_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_cure_v2.py --gif    y las animadas
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_cure_v1 as base
from gen_fx_cure_v1 import BAYER, C, CELDA, DY, VARIANTES, campo, gotas, tramar

VERSION = 2
NOMBRE  = 'El barrido'

INICIO  = -36                              # y del frente en el frame 0, respecto al centro


def frente(tt, f):
    """El frente baja de arriba hasta 3 px encima de la caja del numero, que
    depende de la talla: en la 24 acaba antes."""
    fin = tt.t - 4 - 3 - 4
    return INICIO + (fin - INICIO) * f / 4


def dibujar(tt, v, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    t = v / VARIANTES
    yf = frente(tt, f)
    nube = campo(t) * tt.libre
    # el frente: donde hay nube y a menos de 1.5 px de la linea
    hay = campo(t, 0, 2.6) > BAYER
    en = (np.abs(DY - yf) < 1.5) & hay & tt.libre
    if f == 4:
        en &= ((np.indices(en.shape).sum(axis=0)) % 2 == 0)   # el ultimo rastro, ralo
    idx[en] = (1, 1, 2, 3, 5)[f]
    # detras del frente, una estela corta
    tras = (DY < yf - 1.5) & (DY > yf - 5) & hay & tt.libre & (idx == 0) & (f < 4)
    tramar(idx, np.where(tras, 0.35, 0), 3, 3, corte=0.2)
    # delante, la nube que queda, mas rala al final
    k = (1.0, 1.0, 0.8, 0.5, 0.0)[f]
    tramar(idx, np.where(DY > yf + 1.5, nube * k, 0), 3, 5, corte=0.24)
    for x, y, u in gotas(t):
        if y - C > yf + 1.5 and f < 4:
            tt.punto(idx, x, y, 2 if u < 0.3 else (3 if u < 0.62 else 4))
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
