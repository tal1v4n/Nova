"""NOVA - ART-FX-KILL v3, el proton destruido: «El brillo que se cierra».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.3 y 14.5

Lo comun a las tres propuestas -la hoja, la talla 1, las 16 variantes que
arrancan del frame del bucle, la implosion medida y las vistas- vive en
`gen_fx_kill_v1.py`; aqui solo esta el dibujo.

LA TESIS: EL MISMO BRILLO, AL REVES. El proton que sobrevive a un disparo
refulge con la estrella de aerografo de `ART-FX-SHOT` («El brillo de
aerografo», v3), que se abre hacia fuera. El que no sobrevive refulge con
la misma estrella, pero cerrandose: el jugador aprende un solo gesto y lo
lee en los dos sentidos. Abierta, ha aguantado; cerrada, se ha ido.

  - SUCCION (frame 0). Los quarks ya no estan: cada uno es una estela de
    1 px que se enrosca de donde estaba al centro, clara en la punta de
    dentro. La
    membrana sigue, rala, como el contorno de lo que se va a cerrar.
  - CRUZ (frame 1). La estrella de cuatro puntas, en aspa o recta segun la
    variante, de 5 px de brazo y con el nucleo en el indice 1.
  - NADA (frame 2). La misma estrella con brazos de 1 px, apagada: 8 px.

    py tools/gen_fx_kill_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_kill_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_kill_v1 as base
from gen_fx_kill_v1 import CELDA, DX, DY, P, R, C, lienzo, quarks, solido, tramar

VERSION = 3
NOMBRE  = 'El brillo que se cierra'

GIRO    = 1.4       # cuanto se enrosca cada estela hasta el centro, en radianes


def _estrella(lz, ramp, brazo, aspa, b_nucleo, b_brazo):
    """Nucleo de 2 x 2 y cuatro brazos de `brazo` px, rectos o en aspa."""
    nucleo = (np.abs(DX) < 1) & (np.abs(DY) < 1)
    if aspa:
        brazos = (np.abs(np.abs(DX) - np.abs(DY)) < 0.1) & (np.abs(DX) < brazo + 0.6)
    else:
        brazos = (((np.abs(DX) < 1) & (np.abs(DY) < brazo + 1)) |
                  ((np.abs(DY) < 1) & (np.abs(DX) < brazo + 1)))
    solido(lz, nucleo, ramp, b_nucleo)
    solido(lz, brazos & ~nucleo, ramp, b_brazo)


def dibujar(ramp, f, v):
    lz = lienzo(CELDA)
    aspa = (v // 4) % 2 == 1
    if f == 0:
        # En espiral y no recta: tres radios rectos al centro son un logotipo
        # (el de Mercedes), y la curva se lee como lo que es, una succion.
        for x, y, _, _ in quarks(v):
            ux, uy = x - C, y - C
            largo = math.hypot(ux, uy)
            a = math.atan2(uy, ux)
            for k in np.linspace(0.0, 1.0, 32):
                ak = a + GIRO * (1 - k)
                px, py = C + largo * k * math.cos(ak), C + largo * k * math.sin(ak)
                ix, iy = int(math.floor(px)), int(math.floor(py))
                if k < 0.18:
                    continue
                lz['col'][iy, ix] = ramp[1 if k < 0.45 else (2 if k < 0.75 else 3)]
                lz['glow'][iy, ix] = True
        rb = P['bolsa']
        dens = np.clip(1 - np.abs(R - rb) / 1.6, 0, 1) * 0.45
        tramar(lz, dens, ramp, banda_int=3, banda_ext=4, corte=0.4)
    elif f == 1:
        _estrella(lz, ramp, 4, aspa, 1, 2)
    else:
        _estrella(lz, ramp, 1, aspa, 2, 4)
    return lz


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
