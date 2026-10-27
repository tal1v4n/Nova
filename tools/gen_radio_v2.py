"""NOVA - Generador del anillo de radio del Magnetar (ART-WLD-RAD-*), v2: EL SUSURRO.

Ref: NOVA_Assets_Diseno.md A.4.1 · NOVA_Nodos.md 4.4 · A.8 #1
Propuesta alternativa a `gen_radio_v1.py` («El cerco»). Las dos conviven.

    py tools/gen_radio_v2.py          los 15 anillos
    py tools/gen_radio_v2.py --vista  ademas, las vistas en art/_preview/radio/v2/

EL PROBLEMA QUE RESUELVE. La v1 dibujaba demasiado: un borde continuo y un
cerco de limaduras en tres o cuatro Magnetares a la vez compiten con los nodos,
que son el juego. El anillo es informacion de fondo; tiene que estar ahi cuando
se busca y no pedir atencion cuando no.

COMO SE BAJA EL BRILLO SIN ALFA. El proyecto no tiene alfa parcial ni
post-procesado, asi que la intensidad no se puede bajar: se REPARTE. El borde
sigue siendo la linea de 1 px cuyo lado de fuera es el radio exacto, pero se
enciende un pixel de cada dos recorriendola en angulo. A 1x el ojo integra la
alternancia y la lee como una linea a media luz, y la frontera sigue en su
sitio al pixel.

Tres piezas, todas minimas:

    borde       1 px a medio tono, en una banda oscura de la rampa
    velo        4 px por dentro, trama muy rala y aun mas oscura: dice de que
                lado esta la zona, nada mas
    nivel       un punto solido de 2 x 2 por cada esquirla, por fuera del borde
                y en el angulo en que flota la esquirla del sprite (A.2.4). Es
                lo unico que va lleno, porque es lo unico que hay que contar

El del Jugador es una banda mas claro que los ajenos, no a pleno brillo: se ve
a demanda y no hace falta que grite. Mismo dibujo en las tres facciones.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import BAYER8, RAIZ, rampa

import gen_radio_v1 as V1

ASSET = V1.ASSET
VERSION = 2

LADO, C, PPU, RADIO_U = V1.LADO, V1.C, V1.PPU, V1.RADIO_U

# rampa del propio Magnetar; bandas del borde, del velo y del punto de nivel
FACCIONES = {
    'neutral': dict(ramp='mag_n', borde=4, velo=5, nivel=3),
    'enemy':   dict(ramp='mag_e', borde=4, velo=5, nivel=3),
    'player':  dict(ramp='mag_p', borde=3, velo=4, nivel=2),
}

VELO_ANCHO = 4       # px por dentro del borde
VELO_DENS = 0.16     # densidad maxima, junto al borde
NIVEL_D = 3.5        # centro del punto de nivel, px por fuera del borde


def _malla():
    y, x = np.mgrid[0:LADO, 0:LADO]
    dx, dy = x + 0.5 - C, y + 0.5 - C
    return dx, dy, np.hypot(dx, dy), np.arctan2(dy, dx)


def borde_completo(R, d):
    """Los pixeles cuyo centro cae en (R-1, R]: su lado de fuera es el radio."""
    return (d > R - 1) & (d <= R)


def medio_tono(borde, ang):
    """Uno de cada dos pixeles del borde, recorrido en angulo.

    Alternar por paridad de x + y no sirve: en los tramos diagonales del
    circulo todos los pixeles tienen la misma paridad y la linea se encenderia
    entera o se apagaria entera. En angulo la alternancia es la misma en
    cualquier tramo.
    """
    ys, xs = np.nonzero(borde)
    orden = np.argsort(ang[ys, xs])
    m = np.zeros_like(borde)
    m[ys[orden[::2]], xs[orden[::2]]] = True
    return m


def anillo(nivel, faccion):
    """RGBA (LADO, LADO) del anillo de un nivel y una faccion."""
    cfg = FACCIONES[faccion]
    ramp = rampa(cfg['ramp'])
    R = RADIO_U[nivel] * PPU
    dx, dy, d, ang = _malla()
    out = np.zeros((LADO, LADO, 4), np.uint8)

    def poner(m, banda):
        out[m, :3] = ramp[banda]
        out[m, 3] = 255

    # velo: trama rala que se apaga hacia dentro
    t = np.clip((d - (R - 1 - VELO_ANCHO)) / VELO_ANCHO, 0, 1)
    umbral = np.tile(BAYER8, (LADO // 8, LADO // 8))
    poner((d <= R - 1) & (VELO_DENS * t ** 1.5 > umbral), cfg['velo'])

    poner(medio_tono(borde_completo(R, d), ang), cfg['borde'])

    # nivel: un punto de 2 x 2 por esquirla, alineado a la rejilla
    for a in V1.angulos_esquirla(nivel):
        px = int(math.floor(C + math.cos(a) * (R + NIVEL_D) - 0.5))
        py = int(math.floor(C + math.sin(a) * (R + NIVEL_D) - 0.5))
        out[py:py + 2, px:px + 2, :3] = ramp[cfg['nivel']]
        out[py:py + 2, px:px + 2, 3] = 255

    return out


def verifica(img, nivel):
    """Medio tono de verdad: la mitad del borde, sin huecos de mas de un pixel."""
    R = RADIO_U[nivel] * PPU
    a = img[..., 3] > 0
    if a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any():
        raise SystemExit(f'N{nivel}: toca el borde del lienzo')
    _, _, d, ang = _malla()
    borde = borde_completo(R, d)
    encendidos = a & borde
    if abs(encendidos.sum() * 2 - borde.sum()) > 1:
        raise SystemExit(f'N{nivel}: el borde no esta a medio tono')
    if (a & (d > R) & (d < R + 1.5)).any():
        raise SystemExit(f'N{nivel}: algo pasa del radio pegado al borde')
    # recorrido en angulo, nunca dos apagados seguidos
    ys, xs = np.nonzero(borde)
    on = encendidos[ys, xs][np.argsort(ang[ys, xs])]
    if (~on & ~np.roll(on, 1)).any():
        raise SystemExit(f'N{nivel}: el borde tiene un hueco de dos pixeles')
    return int(encendidos.sum()), int(a.sum())


def main():
    raiz = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    os.makedirs(raiz, exist_ok=True)
    print(f"{'':10} {'radio':>6} {'borde':>6} {'pixeles':>8}")
    for fac in FACCIONES:
        for n in RADIO_U:
            img = anillo(n, fac)
            borde, total = verifica(img, n)
            Image.fromarray(img, 'RGBA').save(V1.ruta(fac, n, raiz))
            print(f'{fac:>7} N{n} {RADIO_U[n] * PPU:6.0f} {borde:6d} {total:8d}')
    if '--vista' in sys.argv:
        V1.vistas(anillo, VERSION)


if __name__ == '__main__':
    main()
