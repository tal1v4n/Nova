"""NOVA - ART-FX-KILL v2, el proton destruido: «El confinamiento».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.3 y 14.5

Lo comun a las tres propuestas -la hoja, la talla 1, las 16 variantes que
arrancan del frame del bucle, la implosion medida y las vistas- vive en
`gen_fx_kill_v1.py`; aqui solo esta el dibujo.

LA TESIS: LOS QUARKS NO PUEDEN QUEDARSE SUELTOS. Es la ley mas rara de la
fisica del proton, el confinamiento: si tiras de un quark, el gluon que lo
ata no se afloja, tira mas fuerte. «La bolsa» ya dibuja los gluones como
muelles; aqui se rompen, y lo que pasa despues es lo que dice la fisica:

  - ROTURA (frame 0). La bolsa ya no esta. Los tres quarks siguen en su
    sitio, pero cada muelle se ha partido por la mitad: dos munones en
    zigzag que se recogen hacia sus quarks y, en cada corte, una chispa.
  - CONFINAMIENTO (frame 1). Sin muelles que los separen, los tres quarks
    caen unos sobre otros, encogidos y encendidos, en un racimo de 5 px.
  - NADA (frame 2). Una cruz de 12 px que se apaga.

Es la unica de las tres que cuenta la historia del proton que ya existe:
los mismos quarks, los mismos muelles, en el mismo sitio en que estaban.

    py tools/gen_fx_kill_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_kill_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_kill_v1 as base
from gen_fx_kill_v1 import CELDA, DX, DY, P, R, C, fase, lienzo, quarks, \
    poner_quarks, solido, tramar

VERSION = 2
NOMBRE  = 'El confinamiento'

MUNON   = 0.30      # que fraccion del muelle queda pegada a cada quark
RECOGE  = 1.7       # el muñon se recoge: mas amplitud que el muelle entero


def _munones(lz, ramp, qs, t):
    """Medio muelle a cada lado de cada corte, y una chispa en el corte."""
    n = len(qs)
    for j in range(n):
        a0 = qs[j][2]
        a1 = qs[(j + 1) % n][2]
        if a1 <= a0:
            a1 += 2 * math.pi
        hueco = (P['rq'] + 1.5) / P['R']
        a0, a1 = a0 + hueco, a1 - hueco
        for s in np.linspace(0.0, 1.0, 160):
            if MUNON < s < 1 - MUNON:
                continue
            a = a0 + (a1 - a0) * s
            fz = 2 * math.pi * P['vueltas'] * s - 2 * t
            r = P['R'] + RECOGE * math.sin(fz)
            x, y = int(C + r * math.cos(a)), int(C + r * math.sin(a))
            if not lz['lleno'][y, x]:
                lz['col'][y, x] = ramp[2 if math.cos(fz) > 0 else 4]
                lz['glow'][y, x] = True
        am = (a0 + a1) / 2
        x, y = int(C + P['R'] * math.cos(am)), int(C + P['R'] * math.sin(am))
        lz['col'][y, x] = ramp[1]
        lz['glow'][y, x] = True
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if not lz['lleno'][y + dy, x + dx]:
                lz['col'][y + dy, x + dx] = ramp[3]
                lz['glow'][y + dy, x + dx] = True


def dibujar(ramp, f, v):
    lz = lienzo(CELDA)
    if f == 0:
        qs = quarks(v)
        poner_quarks(lz, ramp, qs, P['rq'])
        _munones(lz, ramp, qs, fase(v))
    elif f == 1:
        poner_quarks(lz, ramp, quarks(v, 0.42), P['rq'] * 0.8)
        dens = np.clip(1 - R / 5.0, 0, 1) * 0.7
        tramar(lz, dens, ramp, banda_int=2, banda_ext=3, corte=0.4)
    else:
        cruz = ((np.abs(DX) < 1) & (np.abs(DY) < 2)) | ((np.abs(DX) < 2) & (np.abs(DY) < 1))
        solido(lz, (np.abs(DX) < 1) & (np.abs(DY) < 1), ramp, 1)
        solido(lz, cruz, ramp, 3)
    return lz


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
