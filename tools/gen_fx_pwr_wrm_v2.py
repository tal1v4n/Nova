"""NOVA - ART-FX-PWR-WRM v2, el impacto de Agujero de Gusano: «El sumidero».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.7 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de cinco tallas, la rampa del gusano,
la caja del numero, convivir con «El relevo» y las comprobaciones- vive en
`gen_fx_pwr_wrm_v1.py`; aqui solo esta el dibujo.

LA TESIS: UN AGUJERO DE GUSANO TIENE DOS BOCAS. Por una entra todo y por la
otra sale todo: un agujero negro pegado a uno blanco. El nodo cae por la
primera y sale por la segunda siendo de otro, y el efecto es las dos bocas
una detras de otra, en el mismo sitio.

  - EL SUMIDERO (frames 0-4). Tres brazos en espiral se enroscan hacia el
    nodo, cada vez mas cerrados y mas claros, como agua por un desague.
    Van por debajo de «El relevo», y su direccion es la del anillo que
    entra.
  - LA GARGANTA (frame 5). Todo cae en un anillo fino pegado al cuerpo, en
    el indice 1.
  - LA FUENTE (frames 6-10). Los brazos salen con el giro contrario y se
    abren y se apagan hacia el borde de la celda.
  - EL POSO (frame 11). Unas motas al borde.

Con cizalla, porque es una atmosfera alrededor del cuerpo: la espiral es
logaritmica, el angulo de cada brazo cambia con el radio (contrato, regla
6), y tres brazos de desigual grosor no se leen como engranaje.

    py tools/gen_fx_pwr_wrm_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_pwr_wrm_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_pwr_wrm_v1 as base
from gen_fx_pwr_wrm_v1 import ANG, BAYER, CELDA, DENTRO, R, RUIDO, pinta, punto
from gen_fx_send_v1 import borde, tramar

VERSION = 2
NOMBRE  = 'El sumidero'

BRAZOS  = 3
TORSION = 3.6                           # cuanto se enrosca el brazo por e-veces de radio
GROSOR  = (1.0, 0.75, 0.55)             # el peso desigual de los tres brazos


def espiral(giro, quiralidad):
    """Densidad de los tres brazos en [0, 1]: espiral logaritmica."""
    fase = ANG - quiralidad * TORSION * np.log(R + 1.0) + giro
    d = np.zeros_like(R)
    for k in range(BRAZOS):
        a = np.cos(fase - 2 * math.pi * k / BRAZOS)
        d = np.maximum(d, np.clip(a, 0, 1) ** (9 / GROSOR[k]) * GROSOR[k] ** 0.3)
    return d


def ventana(r0, r1):
    """Un anillo de r0 a r1 con los bordes suaves."""
    mid, half = (r0 + r1) / 2, max((r1 - r0) / 2, 0.5)
    return np.clip(1 - np.abs(R - mid) / half, 0, 1) ** 0.6


def dibujar(tt, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    rb = borde(tt.t)
    mascara = tt.libre & DENTRO

    if f <= 4:
        # el sumidero: el anillo de brazos se cierra hacia el cuerpo
        r_in = 40 - (40 - (rb - 2)) * (f + 1) / 5
        cola = np.clip(1 - (R - r_in) / (44 - r_in), 0, 1) * (R >= r_in)
        dens = espiral(0.7 * f, +1) * cola ** 0.5 * (0.9 + 0.05 * f)
        b_in, b_out = ((4, 5), (3, 5), (3, 4), (2, 4), (1, 3))[f]
        tramar(idx, dens * mascara, b_in, b_out, corte=0.45)
        return idx

    if f == 5:
        # la garganta: un anillo fino pegado al cuerpo y el ultimo giro
        anillo = (np.abs(R - (rb - 1)) < 0.8)
        pinta(idx, anillo & (RUIDO < 0.8), 1, tt)
        pinta(idx, (np.abs(R - (rb + 1)) < 0.7) & (BAYER < 0.4), 3, tt)
        return idx

    if f <= 10:
        # la fuente: el giro contrario, hacia fuera
        k = f - 6                                     # 0..4
        r_out = (rb + 6) + (44 - (rb + 6)) * k / 4
        cola = np.clip((R - (rb - 1)) / max(r_out - (rb - 1), 1), 0, 1) * (R < r_out)
        dens = espiral(-0.7 * k + 2.0, -1) * cola ** 0.7 * (0.95 - 0.13 * k)
        b_in, b_out = ((1, 3), (2, 4), (3, 4), (3, 5), (4, 5))[k]
        tramar(idx, dens * mascara, b_in, b_out, corte=0.5)
        return idx

    # el poso: unas motas al borde
    motas = (np.abs(R - 41) < 1.2) & (RUIDO < 0.06) & (espiral(-4.2, -1) > 0.2)
    pinta(idx, motas, 5, tt)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
