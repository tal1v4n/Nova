"""NOVA - ART-FX-VANISH v3, los protones huerfanos: «Sin bandera».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.4 y 14.5 · Spec 10.1

Lo comun a las tres propuestas -la hoja, las tallas, las 16 variantes, el
numero que se va primero y las comprobaciones- vive en
`gen_fx_vanish_v1.py`; aqui solo esta el dibujo.

LA TESIS: LO PRIMERO QUE PIERDE UN HUERFANO ES SU COLOR. En NOVA el color es
el bando (14.4): cian el Jugador, carmin el Enemigo, gris lo que no es de
nadie. Un proton cuyo bando ya no existe no es de nadie, y eso se ve antes
de que desaparezca: en el primer frame se vuelve gris, el gris de los
neutrales, y solo despues se deshace.

  - SIN BANDO (frame 0). El mismo proton, en el mismo frame de su bucle, con
    sus quarks y sus muelles, pero en la rampa `neutral`.
  - SIN BORDE (frame 1). Una banda mas oscuro y sin contorno.
  - SIN NADA (frames 2-4). Se va comiendo por trama, una banda mas oscuro
    cada dos frames, hasta unas motas.

Es la que mas dice en menos tiempo: el jugador ve que esos protones han
dejado de ser del Enemigo antes de verlos irse, y lo ve en todos a la vez,
como un apagon.

    py tools/gen_fx_vanish_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_vanish_v3.py --gif    y las animadas
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import gen_proton_v2 as pro
import gen_fx_vanish_v1 as base
from gen_fx_vanish_v1 import a_rgba, cuerpo, erosion, fase, oscurecer, rampa, sin_contorno

VERSION = 3
NOMBRE  = 'Sin bandera'
NEUTRAL = rampa('neutral')


def dibujar(ramp, t, f, v):
    p = dict(pro.TALLAS[t])
    lz = cuerpo(NEUTRAL, p, fase(v))
    if f > 0:
        sin_contorno(lz)
    img = a_rgba(lz)
    img = oscurecer(img, NEUTRAL, (f + 1) // 2)
    return erosion(img, f - 1, total=FRAMES_EROSION) if f > 1 else img


FRAMES_EROSION = 4


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv, extra=('neutral',))
