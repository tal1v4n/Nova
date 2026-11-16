"""NOVA - ART-FX-IGN v2, el encendido: «El crisol».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.2 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja, las dos tallas, las rampas de la
brasa, la franja del numero y las comprobaciones- vive en
`gen_fx_ign_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL NODO SALE DEL FUEGO. En el tick del encendido la roca desaparece
dentro de una bola de luz, y el nodo nuevo aparece de dentro afuera, como el
metal que se vacia de un crisol. Es la unica de las tres que tapa el relevo
de sprite entero: el jugador no ve la roca convertirse, ve salir el nodo.

  - FUNDE (frame 0). Un disco de trama densa, del indice 1 y 2, cubre el
    cuerpo entero.
  - SE VACIA (frames 1-3). El disco se abre desde el centro y deja ver el
    nodo nuevo, hasta quedar un aro en su borde.
  - CHISPAS (frames 4-7). El aro se apaga y de lo alto suben las cinco
    chispas de «El pedernal», en su mismo abanico, hasta apagarse: la brasa
    que se encendio se despide.

Sin rayos en estrella: un destello isotropo es la firma de la Estrella
(A.3.2), y lo que nace tambien puede ser un Pulsar o un Magnetar.

    py tools/gen_fx_ign_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_ign_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_ign_v1 as base
from gen_fx_ign_v1 import C, CELDA, R, borde, franja, punto, tramar

VERSION = 2
NOMBRE  = 'El crisol'

HUECO   = (0.0, 0.35, 0.65, 0.9)               # radio vaciado, en fraccion del cuerpo
ABANICO = tuple(math.radians(a) for a in (-142, -116, -90, -64, -38))


def dibujar(t, f):
    Rb = borde(t)
    LIBRE = ~franja(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    if f <= 3:
        h = HUECO[f] * Rb
        dens = np.where((R < Rb + 0.5) & (R >= h), 0.95 - 0.12 * f, 0)
        dens = np.where(R >= h, dens * np.clip(1 - (R - Rb) / 1.5, 0, 1), 0)
        tramar(idx, dens * LIBRE, 1 + (f >= 2), 2 + (f >= 2), corte=0.6)
        if f == 3:
            aro = (R >= Rb - 0.5) & (R < Rb + 0.5) & LIBRE
            idx[aro] = 2
    else:
        k = f - 4                                # 0..3
        # el aro se apaga
        if k < 2:
            aro = (R >= Rb - 0.5) & (R < Rb + 0.5) & LIBRE
            dens = np.where(aro, 0.6 - 0.25 * k, 0)
            tramar(idx, dens, 3, 4, corte=0.4)
        # las cinco chispas suben desde arriba, en el abanico del pedernal
        for j, a in enumerate(ABANICO):
            r = Rb + 2.0 + 3.0 * k + (j % 2)
            x, y = C + r * math.cos(a), C + r * math.sin(a)
            banda = min(1 + k, 4)
            punto(idx, x, y, banda)
            if k < 3:
                punto(idx, x - 1.5 * math.cos(a), y - 1.5 * math.sin(a), min(banda + 1, 5))
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
