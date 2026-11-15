"""NOVA - ART-FX-SHOT v3, el destello sobre el proton alcanzado: «El brillo de aerografo».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.3 y 14.5 ·
NOVA_Arte_Tecnica.md, Paleta

Lo comun a las tres propuestas -la hoja, las tallas, el numero que no se
pisa, el color del proton, las cuatro variantes y las comprobaciones- vive en
`gen_fx_shot_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL DESTELLO DE LA EPOCA. La paleta del proyecto es la de una
ilustracion de aerografo de los 70-80 (contrato, *Paleta*), y en esas
portadas todo lo que brilla brilla igual: una estrella de cuatro puntas
finisimas, una larga y otra corta, con un nucleo redondo y otras cuatro
puntas cortas en diagonal. Es el «glint» del cromo y de las naves. Aqui el
golpe es eso: el proton refulge un instante, como si el arco le hubiera
sacado brillo.

  - GOLPE (frame 0). La cruz mayor, larga en un eje (Re + 8 px) y corta en
    el otro (Re + 3), con las puntas afiladas: el indice 1 hasta la
    membrana, el 2 y el 3 hacia la punta, y de 2 px de grueso cerca del
    centro a 1 px en la punta. Las diagonales, cortas, en el 2 y el 3. Un
    resplandor ralo dentro de la bolsa.
  - APAGADO (frame 1). La cruz se encoge a la mitad de lo que sobresalia y
    baja una banda; las diagonales y el resplandor desaparecen, y en las
    puntas de antes quedan dos motas.

Es la unica propuesta que no se lee desde la bolsa sino desde fuera: las
puntas largas son lo que se ve cuando el proton cruza a toda velocidad.
Las cuatro variantes alternan la cruz recta y la cruz en aspa, y en cada una
cual de los dos brazos es el largo.

    py tools/gen_fx_shot_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_shot_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_shot_v1 as base
from gen_fx_shot_v1 import CELDA, DX, DY, R, medidas, numero, tramar

VERSION = 3
NOMBRE  = 'El brillo de aerografo'

LARGA   = 8.0     # cuanto sobresale de la bolsa la punta larga
CORTA   = 3.0     # y la corta
DIAG    = 0.75    # las diagonales, en fraccion del radio de la bolsa


def _brazo(lon, w, recto, largo, grueso_hasta):
    """Mascara de un brazo de la cruz a lo largo de `lon`, con `w` la
    distancia con signo al eje. Recto (horizontal o vertical), el eje pasa
    entre dos filas de pixeles: la punta es la fila de arriba o la de la
    izquierda, del lado de la luz, y cerca del centro van las dos. En
    diagonal el eje pasa por los pixeles: la punta es la diagonal y cerca
    del centro se le suma un vecino a cada lado."""
    a = np.abs(lon)
    if recto:
        fino = (w > -0.6) & (w < 0)
        grueso = np.abs(w) < 0.6
    else:
        fino = np.abs(w) < 0.1
        grueso = np.abs(w) < 0.8
    return (fino | (grueso & (a < grueso_hasta))) & (a <= largo)


def _ejes(giro):
    c, s = math.cos(giro), math.sin(giro)
    return c * DX + s * DY, -s * DX + c * DY


def dibujar(t, f, v):
    Rb, Re = medidas(t)
    libre = ~numero(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    aspa = v // 2 == 1                   # 0: cruz recta, 1: en aspa
    giro = math.pi / 4 if aspa else 0.0
    largo_en_u = v % 2 == 0              # cual de los dos brazos es el largo
    u, w = _ejes(giro)
    sale = LARGA if f == 0 else LARGA / 2
    sale_c = CORTA if f == 0 else CORTA / 2
    lu, lw = (Re + sale, Re + sale_c) if largo_en_u else (Re + sale_c, Re + sale)
    sesgo = 0 if f == 0 else 1
    for lon, per, largo in ((u, w, lu), (w, -u, lw)):
        m = _brazo(lon, per, not aspa, largo, Rb * 0.7) & libre
        a = np.abs(lon)
        b = np.where(a < Re, 1, np.where(a < Re + 0.5 * (largo - Re), 2, 3))
        idx[m] = np.minimum(b[m] + sesgo, 5)
    if f == 0:
        # las diagonales, cortas, de 1 px
        u2, w2 = _ejes(giro + math.pi / 4)
        for lon, per in ((u2, w2), (w2, -u2)):
            a = np.abs(lon)
            m = _brazo(lon, per, aspa, Re * DIAG + 2, 0) & (a > 2) & libre & (idx == 0)
            idx[m] = np.where(a[m] < Re * DIAG * 0.6, 2, 3)
        # el resplandor dentro de la bolsa
        dens = np.clip(1 - R / (Rb + 1), 0, 1) ** 0.8 * 0.3
        tramar(idx, dens * libre, 3, 4, corte=0.3)
    else:
        # dos motas donde estaban las puntas largas
        c, s = math.cos(giro), math.sin(giro)
        for sg in (-1, 1):
            d = Re + LARGA - 1
            px, py = (sg * d * c, sg * d * s) if largo_en_u else (-sg * d * s, sg * d * c)
            x, y = int(math.floor(base.C + px)), int(math.floor(base.C + py))
            if libre[y, x] and idx[y, x] == 0:
                idx[y, x] = 4
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
