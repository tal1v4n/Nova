"""NOVA - ART-FX-SEND v2, el lanzamiento sobre el nodo origen: «Los tres cometas».

Ref: NOVA_Assets_Diseno.md A.5 y A.4.3 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja, las tallas de «La orbita», la
franja del numero y las comprobaciones- vive en `gen_fx_send_v1.py`; aqui
solo esta el dibujo.

LA TESIS: LOS COMETAS DE LA SELECCION SALEN DISPARADOS. Un nodo seleccionado
lleva tres cometas girando a su alrededor («La orbita», `ART-WLD-SEL`), y esos
tres cometas ya son el gesto del proton: tres quarks girando en su bolsa. Al
enviar, los tres salen despedidos hacia fuera, a la misma distancia a la que
orbitaban. El jugador ve soltarse lo que tenia cargado.

  - SALIDA (frame 0). Tres cabezas de 2 x 2 en el indice 1, pegadas al
    cuerpo, cada una con un arco corto de la superficie encendido detras.
  - VUELO (frames 1-2). Se alejan 4 px por frame, encogen a 1 px y bajan
    de banda, con una estela de trama hacia el nodo.

Van a -90, 20 y 160 grados: arriba y a los dos lados, algo por debajo. Tres y no
cuatro, por lo mismo que el anillo (con cuatro seria una reticula), y en
esos tres angulos ninguno cae en la franja del numero.

    py tools/gen_fx_send_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_send_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_send_v1 as base
from gen_fx_send_v1 import ANG, C, CELDA, DX, DY, LIBRE, R, borde, tramar

VERSION = 2
NOMBRE  = 'Los tres cometas'

ANGULOS = tuple(math.radians(a) for a in (-90, 20, 160))
VUELA   = 4.0     # px por frame
ARCO    = 0.22    # media anchura del arco de superficie, en radianes


def dibujar(t, f):
    Rb = borde(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    for a in ANGULOS:
        ux, uy = math.cos(a), math.sin(a)
        d = Rb + 2.0 + VUELA * f
        lon = DX * ux + DY * uy
        per = np.abs(-DX * uy + DY * ux)
        if f == 0:
            cabeza = (np.abs(lon - d) <= 1.0) & (per <= 1.0)
            idx[cabeza & LIBRE] = 1
            dif = np.abs(np.angle(np.exp(1j * (ANG - a))))
            arco = (dif < ARCO) & (R >= Rb - 0.5) & (R < Rb + 0.5) & (idx == 0)
            idx[arco & LIBRE] = 2
        else:
            cabeza = (np.abs(lon - d) <= 0.5) & (per <= 0.5 + 0.5 * (f == 1))
            idx[cabeza & LIBRE & (idx == 0)] = 1 + f
            dens = np.where((per < 1.0) & (lon > Rb) & (lon < d - 0.5),
                            np.clip((lon - Rb) / (d - Rb), 0, 1) * 0.8 / f, 0)
            tramar(idx, dens * LIBRE, 3, 4, corte=0.5)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
