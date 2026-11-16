"""NOVA - ART-FX-PWR-WRM v3, el impacto de Agujero de Gusano: «El pliegue».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.7 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de cinco tallas, la rampa del gusano,
la caja del numero, convivir con «El relevo» y las comprobaciones- vive en
`gen_fx_pwr_wrm_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL ESPACIO SE DOBLA Y VUELVE. Un agujero de gusano se explica
siempre igual: una hoja de papel con una malla, doblada para que dos puntos
lejanos se toquen. El efecto dibuja esa malla, la que todo el mundo ha visto
en los diagramas de la relatividad, alrededor del nodo: se hunde en un
embudo hacia el, se retuerce al tragarselo y, cuando el nodo ya ha cruzado,
rebota como una tela tensa hasta quedar plana y desaparecer.

  - EL HUNDIMIENTO (frames 0-3). La malla aparece y se hunde hacia el nodo,
    cada vez mas: las lineas se curvan hacia el cuerpo y se aprietan
    contra el, mas claras cuanto mas cerca. Las lineas tambien giran cerca
    del cuerpo -el arrastre de un objeto que rota- y eso es la cizalla.
  - EL REBOTE (frames 4-8). La malla suelta: se abomba hacia fuera, vuelve,
    se abomba menos, como un muelle amortiguado.
  - SE ALISA (frames 9-11). Plana, se apaga a trozos.

La malla no entra en el cuerpo: el cuerpo es la garganta. En las tallas
grandes la garganta se queda en `r 26`, porque entre el borde de su talla y
el de la celda no cabria ni una cuadricula: ahi la malla pisa el filo del
cuerpo, como el embudo visto en perspectiva.

    py tools/gen_fx_pwr_wrm_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_pwr_wrm_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_pwr_wrm_v1 as base
from gen_fx_pwr_wrm_v1 import ANG, BAYER, C, CELDA, DENTRO, R, RUIDO, pinta
from gen_fx_send_v1 import borde

VERSION = 3
NOMBRE  = 'El pliegue'

PASO   = 8.0                            # lado de la cuadricula, px
CAIDA  = 9.0                            # cuanto alcanza el embudo fuera del cuerpo, px

# hundimiento por frame (px), positivo hacia el nodo; luego rebota
HONDO  = (2.0, 5.0, 9.0, 13.0, -6.0, 3.5, -2.0, 1.0, 0.0, 0.0, 0.0, 0.0)
GIRO   = 0.05                           # rad de arrastre por px de hundimiento
APAGA  = (0.0,) * 9 + (0.3, 0.55, 0.8)


GARGANTA = 26.0                         # en las tallas grandes la malla entra hasta aqui


def malla(tt, f):
    rb = min(borde(tt.t), GARGANTA)
    h = HONDO[f]
    fuera = np.clip(R - rb, 0, None)
    perfil = np.exp(-fuera / CAIDA)
    # de donde viene cada pixel en la hoja sin doblar: el embudo trae hacia
    # dentro lo que estaba mas lejos, y el arrastre lo gira
    r_src = R + h * perfil
    a_src = ANG + GIRO * h * perfil
    xs = r_src * np.cos(a_src)
    ys = r_src * np.sin(a_src)
    gx = np.abs(((xs + 0.5 + PASO / 2) % PASO) - PASO / 2)
    gy = np.abs(((ys + 0.5 + PASO / 2) % PASO) - PASO / 2)
    linea = np.minimum(gx, gy) < 0.55
    return linea, perfil, rb


def dibujar(tt, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    linea, perfil, rb = malla(tt, f)
    zona = (R > rb + 0.5) & (R < 42.5)
    linea &= zona & (RUIDO >= APAGA[f])

    # el brillo: cerca de la garganta y cuanto mas hondo, mas claro
    h = abs(HONDO[f])
    cerca = perfil * min(h / 13.0, 1.0)
    banda = np.where(cerca > 0.55, 1, np.where(cerca > 0.3, 2, np.where(cerca > 0.12, 3, 4)))
    if f == 0:
        banda = np.full_like(banda, 4)
    if f >= 8:
        banda = np.full_like(banda, (4, 4, 4, 5)[f - 8] if f < 11 else 5)
    # lejos se trama: la malla se pierde hacia el borde
    lejos = (R > 34) & (BAYER > np.clip((43 - R) / 9, 0, 1))
    linea &= ~lejos
    for b in (5, 4, 3, 2, 1):
        pinta(idx, linea & (banda == b), b, tt)

    # el labio de la garganta, solo mientras se hunde
    if 1 <= f <= 3:
        pinta(idx, (np.abs(R - (rb + 1)) < 0.6) & (BAYER < 0.25 * f), 2, tt)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
