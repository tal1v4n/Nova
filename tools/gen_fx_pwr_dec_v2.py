"""NOVA - ART-FX-PWR-DEC v2, el impacto de Decaimiento: «La vida media».

Ref: NOVA_Assets_Diseno.md A.5, A.3.5 y A.4.2 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de cinco tallas, la rampa del
decaimiento, la caja del numero, el final en el miasma y las comprobaciones-
vive en `gen_fx_pwr_dec_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL HAZ SE QUEDA PUESTO Y SE DESINTEGRA. «La cadena de
desintegracion» llega al nodo y no se apaga: se enrosca a su alrededor como
un collar, las mismas dos hebras en contrafase con un nucleo en cada cruce.
Y desde ahi cumple la ley del haz, la de la vida media: en cada frame se
desintegra la mitad de sus atomos, al azar y sin memoria. Cada atomo que se
va suelta una particula hacia fuera que cae, y lo que cae se queda flotando:
el miasma es literalmente lo que se ha desintegrado.

  - EL COLLAR (frame 0). Entero, con los nucleos en el indice 1.
  - LA VIDA MEDIA (frames 1-5). Quedan 1/2, 1/4, 1/8, 1/16 y 1/32 de los
    atomos; los que se van emiten, y la nube crece con lo emitido.
  - EL MIASMA (frames 6-8). Las ultimas particulas caen dentro de la nube.

El collar no es un aro perfecto, y es a proposito: su radio y el paso de
sus eslabones siguen la onda cizallada del miasma, para que no se lea como
engranaje (contrato, regla 6).

    py tools/gen_fx_pwr_dec_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_pwr_dec_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_pwr_dec_v1 as base
from gen_fx_pwr_dec_v1 import (C, CELDA, RUIDO, ULTIMO, debajo, final, parcial, pinta,
                               punto)
from gen_fx_send_v1 import borde

VERSION = 2
NOMBRE  = 'La vida media'

AMPLITUD = 2.6                      # las hebras en +-2.6 px del eje del collar
ESLABON  = 11.0                     # px de arco por eslabon
EMITE    = 0.45                     # fraccion de atomos cuya particula se ve
VIVE     = 4                        # frames que dura una particula


def _collar(t):
    """Los atomos del collar de la talla t: dict pixel -> (banda, es_nucleo),
    y los nucleos como lista de puntos."""
    rc = borde(t) + 1.0
    k = max(6, round(2 * math.pi * rc / ESLABON))
    atomos = {}
    nucleos = []
    n = 2400
    for i in range(n):
        th = 2 * math.pi * i / n
        fase = k * th + 0.7 * math.sin(3 * th + 1.0)
        r0 = rc + 0.9 * math.sin(2 * th + 0.4)
        for s in (1, -1):
            r = r0 + s * AMPLITUD * math.sin(fase)
            x, y = C + r * math.cos(th), C + r * math.sin(th)
            atomos.setdefault((int(x), int(y)), 3)
    for j in range(2 * k):
        # cruces: sin(fase) = 0, fase = j*pi; se busca th numericamente
        objetivo = j * math.pi
        th = objetivo / k
        for _ in range(8):
            g = k * th + 0.7 * math.sin(3 * th + 1.0) - objetivo
            th -= g / (k + 2.1 * math.cos(3 * th + 1.0))
        r0 = rc + 0.9 * math.sin(2 * th + 0.4)
        nucleos.append((C + r0 * math.cos(th), C + r0 * math.sin(th)))
    return rc, atomos, nucleos


_CACHE = {}


def collar(t):
    if t not in _CACHE:
        _CACHE[t] = _collar(t)
    return _CACHE[t]


def muere(x, y):
    """El frame en que se desintegra el atomo del pixel (x, y): sigue vivo en
    el frame f si su numero es menor que 1/2^f."""
    u = RUIDO[y % CELDA, x % CELDA]
    return int(math.floor(-math.log2(max(u, 1e-6)))) + 1


def particula(x, y, edad):
    """Donde esta, `edad` frames despues de emitida, la particula de (x, y):
    sale hacia fuera y cae."""
    dx, dy = x + 0.5 - C, y + 0.5 - C
    r = math.hypot(dx, dy) or 1.0
    sal = 1.6 * (edad + 1)
    return x + 0.5 + dx / r * sal, y + 0.5 + dy / r * sal + 0.55 * edad * edad


def dibujar(tt, f):
    if f == base.FRAMES - 1:
        return final(tt)
    idx = np.zeros((CELDA, CELDA), np.int8)
    rc, atomos, nucleos = collar(tt.t)

    # los atomos que siguen vivos
    for (x, y), banda in atomos.items():
        if muere(x, y) > f:
            punto(idx, tt, x, y, 2 if f == 0 else banda + (f >= 3))
    for i, (x, y) in enumerate(nucleos):
        if muere(int(x) + 37, int(y) + 11) > f:
            for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
                punto(idx, tt, x - 0.5 + dx, y - 0.5 + dy, 1)
            if f == 0:
                for dx, dy in ((-1, 0), (2, 0), (0, -1), (0, 2), (1, -1), (1, 2), (-1, 1), (2, 1)):
                    punto(idx, tt, x - 0.5 + dx, y - 0.5 + dy, 2)

    # las particulas de los que se han ido
    for (x, y), _ in atomos.items():
        m = muere(x, y)
        edad = f - m
        if 0 <= edad < VIVE and RUIDO[(y * 7) % CELDA, (x * 5) % CELDA] < EMITE:
            px, py = particula(x, y, edad)
            punto(idx, tt, px, py, (1, 2, 3, 4)[edad] + (f >= 6))

    # la nube crece con lo que se ha desintegrado
    if f >= 1:
        s = (0, 0.12, 0.28, 0.45, 0.6, 0.72, 0.84, 0.94)[f]
        debajo(idx, parcial(ULTIMO - (base.FRAMES - 1 - f), tt, s))
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
