"""NOVA - ART-FX-VANISH v2, los protones huerfanos: «Uno a uno».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5 · Spec 10.1

Lo comun a las tres propuestas -la hoja, las tallas, las 16 variantes, el
numero que se va primero y las comprobaciones- vive en
`gen_fx_vanish_v1.py`; aqui solo esta el dibujo.

LA TESIS: SE APAGA COMO UNA CASA DE NOCHE, LUZ A LUZ. Un proton tiene tres
cosas que brillan, los tres quarks, y el jugador las ha visto girar toda la
partida. Sin bando no hay nada que las mantenga encendidas, y se apagan de
una en una: primero los dos u, luego el d, que ya era el mas apagado.

  - SUELTA (frame 0). El proton tal cual, sin los muelles: ya no hay nada
    que atar.
  - UNO, DOS, TRES (frames 1-3). En cada frame un quark pasa a ser un disco
    plano en la banda mas oscura, sin luz ni contorno, y el del frame
    anterior desaparece. La bolsa se apaga una banda cada dos frames y se
    va comiendo por trama.
  - EL ULTIMO (frame 4). Solo queda el d apagado, encogido a la mitad, y
    unas motas de la bolsa.

Es la que mas se parece a una cuenta atras, y la unica en la que el
jugador puede seguir algo con la vista hasta el final.

    py tools/gen_fx_vanish_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_vanish_v2.py --gif    y las animadas
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import gen_proton_v2 as pro
import gen_fx_vanish_v1 as base
from gen_fx_vanish_v1 import a_rgba, cuerpo, erosion, fase, oscurecer, sin_contorno

VERSION = 2
NOMBRE  = 'Uno a uno'

# Que quarks faltan y cuales estan apagados en cada frame: 0 y 1 son los u,
# 2 es el d (`gen_proton_v2.quarks`, «uud»).
FUERA    = ((), (), (0,), (0, 1), (0, 1))
APAGADOS = ((), (0,), (1,), (2,), (2,))


def dibujar(ramp, t, f, v):
    p = dict(pro.TALLAS[t])
    lz = cuerpo(ramp, p, fase(v), gluones=False, apagados=APAGADOS[f] + FUERA[f])
    # los que ya estan fuera no se dibujan: se quitan los discos que
    # `cuerpo` pinto para ellos
    if FUERA[f]:
        import numpy as np
        with base.centrado():
            qs = pro.quarks(p, fase(v))
        for j in FUERA[f]:
            x, y = qs[j][0], qs[j][1]
            disco = np.hypot(base._x + 0.5 - x, base._y + 0.5 - y) < p['rq'] + 1.2
            lz['glow'][disco] = False
            lz['lleno'][disco] = False
    if f > 0:
        sin_contorno(lz)
    img = a_rgba(lz)
    img = oscurecer(img, ramp, (f + 1) // 2)
    if f > 0:
        # la bolsa se come por trama; los quarks apagados se quedan enteros
        import numpy as np
        with base.centrado():
            qs = pro.quarks(p, fase(v))
        protegido = np.zeros(img.shape[:2], bool)
        for j in APAGADOS[f]:
            x, y = qs[j][0], qs[j][1]
            # en el ultimo frame el d tambien se va: queda la mitad del disco
            radio = p['rq'] * (0.55 if f == base.FRAMES - 1 else 1.0)
            protegido |= np.hypot(base._x + 0.5 - x, base._y + 0.5 - y) < radio
        com = erosion(img, f)
        com[protegido] = img[protegido]
        img = com
    return img


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
