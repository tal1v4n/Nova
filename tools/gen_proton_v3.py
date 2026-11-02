"""NOVA - ART-WLD-PRO-P y ART-WLD-PRO-E v3, el proton: «El engaste».

Ref: NOVA_Assets_Diseno.md A.4.2 · NOVA_Estados_Animaciones.md 14.3 · Spec 6.1

Lo sencillo de la v1 y lo de proton de la v2, con menos de las dos: un aro
de plasma redondo y ajustado a la Carga, con los tres quarks -u, u, d-
engarzados en el como piedras en un anillo, girando. Seis hojas,
`art/world/proton/v3/world_proton_<faccion>_<talla>.png`, en celdas de 48:

    fila 0  cuerpo   16 frames, bucle   los quarks recorren el aro
    fila 1  gotas     4 frames          la estela, la de la v1

Y las cifras de la v2, que se conservan (pedido del usuario, 2026-11-01).

  - MENOS PIEZAS. La v2 tenia bolsa, quarks y muelles, y a 1x eran muchas
    lineas de 1 px. Aqui hay un aro solido y tres cuentas: dos materiales y
    una sola lectura. Los gluones son el propio aro.
  - AJUSTADO AL NUMERO. El aro es el circulo mas pequeno que deja libre el
    numero mas ancho de su talla, con su contorno: 20, 26 y 30 px de cuerpo.
    Frente a los 33-43 de la v2, el proton vuelve a ser mas pequeno que
    cualquier nodo.
  - UUD. Los dos u en el tono claro y con `calor`, el d una banda apagado.
    Sobresalen del aro hacia fuera y llevan contorno interno, asi que se
    cuentan: tres. Dan una vuelta por bucle; cada uno se ilumina con la L
    global donde esta, asi que gira el contenido y no el sprite.
  - EL LATIDO. El resplandor tramado de fuera se ensancha y se encoge con
    frecuencia entera: el proton respira mientras vuela.

    py tools/gen_proton_v3.py          hojas, cifras y vistas fijas
    py tools/gen_proton_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import (CONTORNO, RAIZ, a_rgba, dir_preview, esfera, lienzo,
                       rampa, tramar, tubo_curva, _malla)
import gen_proton_v1 as v1
import gen_proton_v2 as v2

ASSET   = 'proton'
VERSION = 3

CELDA, FRAMES, GOTAS, PASO_F, FPS = 48, 16, v1.GOTAS, v1.PASO_F, 12
C = CELDA / 2
FACCIONES = v1.FACCIONES
UMBRAL_CARGA = v1.UMBRAL_CARGA

# ri: radio interior del aro · g: semigrosor · rq: radio de un quark
# halo: alcance del resplandor · sesgo: bandas del aro
TALLAS = {
    's': dict(ri=6.8,  g=1.3, rq=2.6, halo=2.6, sesgo=2, cifras=('8',)),
    'm': dict(ri=9.8,  g=1.6, rq=3.1, halo=3.0, sesgo=1, cifras=('48', '38', '49')),
    'l': dict(ri=11.6, g=1.8, rq=3.4, halo=3.4, sesgo=1, cifras=('100', '98', '88')),
}
OID_ARO, OID_Q = 10, 20
TRAMOS = 24


def aro(lz, p, ramp):
    rc = p['ri'] + p['g']
    for i in range(TRAMOS):
        a0, a1 = 2 * math.pi * i / TRAMOS, 2 * math.pi * (i + 1) / TRAMOS
        tubo_curva(lz, v1._arco(p['ri'], p['ri'], p['g'], a0, a1), p['g'], ramp,
                   OID_ARO, muestras=12, sesgo_banda=p['sesgo'],
                   contorno_interno=False)


def cuerpo(ramp, talla, t):
    p = TALLAS[talla]
    lz = lienzo(CELDA)
    rc = p['ri'] + p['g']

    # el latido: el resplandor de fuera, mas ancho y mas denso cada medio bucle
    dx, dy = _malla(lz, C, C)
    r = np.hypot(dx, dy)
    lat = 0.5 + 0.5 * math.cos(2 * t)
    fuera = r - (rc + p['g'])
    alcance = p['halo'] * (0.75 + 0.25 * lat)
    dens = np.clip(1 - fuera / alcance, 0, 1) ** 1.4 * (0.55 + 0.35 * lat)
    dens[fuera < 0] = 0
    tramar(lz, dens, ramp, banda_int=2, banda_ext=4, corte=0.5)

    aro(lz, p, ramp)
    # los quarks: un poco hacia fuera del eje del aro, para que el ojo quede libre
    for j, sabor in enumerate('uud'):
        a = -math.pi / 2 + t + 2 * math.pi * j / 3
        x, y = C + (rc + 1.0) * math.cos(a), C + (rc + 1.0) * math.sin(a)
        if sabor == 'u':
            esfera(lz, x, y, p['rq'], ramp, OID_Q + j, calor=1.4)
        else:
            esfera(lz, x, y, p['rq'], ramp, OID_Q + j, apagado=1.0)
    lz['glow'] &= ~lz['lleno']
    return lz


def hoja(ramp, talla):
    filas = [np.hstack([a_rgba(cuerpo(ramp, talla, 2 * np.pi * f / FRAMES))
                        for f in range(FRAMES)])]
    g = [a_rgba(v1.gota(ramp, talla, k)) for k in range(GOTAS)]
    g += [np.zeros((CELDA, CELDA, 4), np.uint8)] * (FRAMES - GOTAS)
    filas.append(np.hstack(g))
    return np.vstack(filas)


def comprobar(img, ramp, talla, fac):
    alfa = img[..., 3]
    assert set(np.unique(alfa)) <= {0, 255}, 'alfa no binario'
    validos = {tuple(c) for c in ramp} | {tuple(CONTORNO)}
    assert {tuple(c) for c in img[alfa > 0][:, :3]} <= validos, \
        f'{fac} {talla}: color fuera de rampa'
    cuerpos = [img[:CELDA, f * CELDA:(f + 1) * CELDA] for f in range(FRAMES)]
    for f, cel in enumerate(cuerpos):
        a = cel[..., 3] > 0
        assert not (a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any()), \
            f'{fac} {talla} frame {f}: toca el borde'
        oscuro = np.all(cel[..., :3] == CONTORNO, axis=-1)
        for s in TALLAS[talla]['cifras']:
            for ddx in (0, 1):
                for ddy in (0, 1):
                    # el contorno del aro puede tocar el del numero; nada mas
                    tapa = v2.caja_numero(s, ddx, ddy) & a & ~oscuro
                    assert not tapa.any(), \
                        f'{fac} {talla} frame {f}: algo tapa «{s}» ({tapa.sum()} px)'
    assert len({c.tobytes() for c in cuerpos}) == FRAMES, f'{fac} {talla}: frames repetidos'
    assert np.array_equal(a_rgba(cuerpo(ramp, talla, 2 * np.pi)), cuerpos[0]), \
        f'{fac} {talla}: el bucle no cierra'
    sol = np.any([(c[..., 3] > 0) for c in cuerpos], axis=0)
    ys, xs = np.where(sol)
    return xs.max() - xs.min() + 1


# --- Vistas: las de la v2, con estas hojas -------------------------------------

def tablero(hojas, k, fondo):
    tab = fondo.copy()
    for fac, carga, o, d, k0 in v1.ENVIOS:
        t = v1.talla_de(carga)
        h = hojas[fac, t]
        largo = math.hypot(d[0] - o[0], d[1] - o[1])
        ux, uy = (d[0] - o[0]) / largo, (d[1] - o[1]) / largo
        s = ((k - k0) * v1.VEL) % (largo + 120)
        if s > largo:
            continue
        p = TALLAS[t]
        hueco = p['ri'] + 2 * p['g']     # el ojo es hueco: la gota nace fuera
        r = v1.PASO_GOTA * math.floor((s - hueco) / v1.PASO_GOTA)
        while r > 0:
            edad = int((s - r) / v1.VEL) // PASO_F
            if edad >= GOTAS:
                break
            v1.sobre(tab, h[CELDA:, edad * CELDA:(edad + 1) * CELDA],
                     round(o[0] + ux * r), round(o[1] + uy * r))
            r -= v1.PASO_GOTA
        f = k % FRAMES
        v1.sobre(tab, v2.con_numero(h[:CELDA, f * CELDA:(f + 1) * CELDA], str(carga)),
                 round(o[0] + ux * s), round(o[1] + uy * s))
    return tab


def vistas(hojas, gif):
    d = os.path.join(dir_preview(ASSET), f'v{VERSION}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(v2.hoja_contacto(hojas)).save(os.path.join(d, 'proton_hoja_x4.png'))
    Image.fromarray(v2.escala(hojas)).save(os.path.join(d, 'proton_escala_x3.png'))
    fondo = v1.abrir('bg', 'fondos', 'v2', 'bg_section1.png')[..., :3].copy()
    tab = tablero(hojas, 22, fondo)
    Image.fromarray(tab).save(os.path.join(d, 'proton_tablero.png'))
    Image.fromarray(v1.ampliar(tab, 2)).save(os.path.join(d, 'proton_tablero_x2.png'))
    if gif:
        esc = [Image.fromarray(tablero(hojas, k, fondo)) for k in range(96)]
        esc[0].save(os.path.join(d, 'proton_tablero.gif'), save_all=True,
                    append_images=esc[1:], duration=round(1000 / FPS), loop=0)
        zoom = [Image.fromarray(v1.ampliar(np.asarray(e)[120:420, 240:720], 2)) for e in esc]
        zoom[0].save(os.path.join(d, 'proton_tablero_x2.gif'), save_all=True,
                     append_images=zoom[1:], duration=round(1000 / FPS), loop=0)
        bucle = []
        for f in range(FRAMES):
            fila = np.empty((CELDA, CELDA * 6, 3), np.uint8)
            fila[:] = v2.BG
            for i, (fac, t) in enumerate(hojas):
                v2.pegar(fila, v2.con_numero(hojas[fac, t][:CELDA, f * CELDA:(f + 1) * CELDA],
                                             v2.MUESTRA[t]), i)
            bucle.append(Image.fromarray(v1.ampliar(fila, 4)))
        bucle[0].save(os.path.join(d, 'proton_bucle_x4.gif'), save_all=True,
                      append_images=bucle[1:], duration=round(1000 / FPS), loop=0)
    print('vistas en', d)


def main():
    destino = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    os.makedirs(destino, exist_ok=True)
    v2.hoja_cifras(destino)
    hojas = {}
    for fac, ramp_n in FACCIONES.items():
        ramp = rampa(ramp_n)
        for t in TALLAS:
            img = hoja(ramp, t)
            ancho = comprobar(img, ramp, t, fac)
            Image.fromarray(img, 'RGBA').save(
                os.path.join(destino, f'world_proton_{fac}_{t}.png'))
            hojas[fac, t] = img
            print(f'{fac:>6} {t}  cuerpo {ancho} px  carga {UMBRAL_CARGA[t][0]}-{UMBRAL_CARGA[t][1]}')
    vistas(hojas, '--gif' in sys.argv)


if __name__ == '__main__':
    main()
