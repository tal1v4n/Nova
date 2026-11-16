"""NOVA - ART-UI-PANEL v3, el panel de clic derecho: «Los modulos».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.3, 13.12 · 14.4

Lo comun -los casos, el texto, el boceto de los botones, las comprobaciones
y las vistas- vive en `gen_panel_v1.py`.

LA TESIS DE LA v3: EL PANEL SE ARMA CON LO QUE PUEDES HACER. No es una caja
con filas: es una pila de MODULOS de a bordo, uno por bloque de 13.3, con
2 px de aire entre ellos. Cada modulo es la placa de 13.12 -relleno
`#11142A`, marco `str_p[6]`- con una LENGUETA de 4 px en el costado que mira
al nodo, y el color de la lengueta dice para que sirve el modulo:

    informacion  el ancla del bando del nodo: de quien es
    mejora y     cian, `str_p[3]`: lo que el jugador puede hacer (13.12)
    reconversion
    poderes      tres tramos de arriba abajo, hielo, `decaimiento` y
                 `gusano`: los tres colores de 14.4, en el orden de la rueda

Un nodo ajeno solo trae el modulo de informacion: su panel mide tres filas y
el del Pulsar propio, cuatro modulos. El TAMANO del panel ya dice cuanto
puedes hacer con ese nodo. Las lenguetas van del lado del nodo -a la
izquierda si el panel se abre a su derecha, y al reves-, asi que la pila
entera mira hacia el. Al abrirse, los modulos entran escalonados, 60 ms
uno tras otro con la curva orbital: el patron «Ignicion» de 13.12.

PIEZAS
    ui_info_panel_module.png   20 x 20, nueve cortes con bordes de 6: la placa
    ui_info_panel_tab.png      teselas de 4 x 1, una fila por lengueta:
                               player, enemy, neutral, accion, poderes
    ui_info_panel.json         medidas, aire, orden y escalonado
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_panel_v1 as base
from gen_panel_v1 import (CASOS, FILA, FONDO, PANEL, RFAC, STR_P, W, colores_de, fila_contenido,
                          nueve, pega, placa_lamina, rgba)
from nova_core import rampa

VERSION = 3
NOMBRE = 'Los modulos'

PZ, BORDE = 20, 6
PADM = 4                       # margen de arriba y abajo de cada modulo
AIRE = 2                       # entre modulos
LENG = 4                       # ancho de la lengueta
EXTRA_IZQ = 4                  # el texto (no los botones) se aparta de la lengueta
SALIENTE = 0
ESCALON_MS = 60
LENGUETAS = ('player', 'enemy', 'neutral', 'accion', 'poderes')


def modulo():
    return placa_lamina(PZ, PZ, STR_P[6])


def lengueta(nombre):
    t = rgba(1, LENG)
    if nombre == 'poderes':
        t = rgba(3, LENG)
        for i, r in enumerate(('hielo', 'decaimiento', 'gusano')):
            t[i, :, :3] = rampa(r)[3]
    elif nombre == 'accion':
        t[0, :, :3] = STR_P[3]
    else:
        t[0, :, :3] = RFAC[nombre][3]
    t[..., 3] = 255
    return t


def lengueta_de(cat, fac):
    return {'info': fac, 'mejora': 'accion', 'reconv': 'accion', 'poderes': 'poderes'}[cat]


def bando_de(fac):
    R = RFAC[fac]
    return {'titulo': R[2] if fac != 'neutral' else R[1]}


def un_modulo(cat, filas, fac, lado):
    h = 2 * PADM + FILA * len(filas)
    img = nueve(modulo(), W, h, BORDE)
    lg = lengueta(lengueta_de(cat, fac))
    alto = h - 8
    # una lengueta lisa se repite; la de poderes se parte en tres tramos
    idx = np.minimum(np.arange(alto) * lg.shape[0] // alto, lg.shape[0] - 1)
    t = lg[idx]
    x_t = 2 if lado == 'izq' else W - 2 - LENG
    img[4:h - 4, x_t:x_t + LENG] = t
    sangria = EXTRA_IZQ if lado == 'izq' else 0
    y = PADM
    for fila in filas:
        texto = fila[0] in ('titulo', 'dato', 'aviso', 'rotulo')
        fila_contenido(img, fila, base.PAD + (sangria if texto else 0), y,
                       W - 2 * base.PAD, bando_de(fac))
        y += FILA
    return img


def panel(caso, lado='izq', f=0, entrada=None):
    """`entrada`: ms desde que se abre, para la vista del escalonado."""
    mods = [un_modulo(cat, filas, caso['fac'], lado) for cat, filas in caso['bloques']]
    h = sum(m.shape[0] for m in mods) + AIRE * (len(mods) - 1)
    out = rgba(h, W)
    y = 0
    for i, m in enumerate(mods):
        if entrada is not None:
            t = (entrada - i * ESCALON_MS) / 320.0
            if t <= 0:
                y += m.shape[0] + AIRE
                continue
            k = orbital(min(1.0, t))
            dx = int(round((1 - k) * 12)) * (-1 if lado == 'izq' else 1)
            pega(out, m, dx, y)
        else:
            pega(out, m, 0, y)
        y += m.shape[0] + AIRE
    return out


def orbital(t):
    import gen_energy_v2 as e2
    return e2.orbital(t)


def piezas():
    d = {'modulo': modulo()}
    for n in LENGUETAS:
        d[f'lengueta_{n}'] = lengueta(n)
    return d


def comprobar_propio():
    fallos = []
    # un nodo ajeno mide un modulo; el Pulsar propio, cuatro
    if len(CASOS['magnetar']['bloques']) != 1:
        fallos.append('el panel de un nodo ajeno no es un modulo')
    return fallos


def exportar(d):
    Image.fromarray(modulo(), 'RGBA').save(os.path.join(d, 'ui_info_panel_module.png'))
    Image.fromarray(np.vstack([lengueta(n) for n in LENGUETAS]), 'RGBA').save(
        os.path.join(d, 'ui_info_panel_tab.png'))
    with open(os.path.join(d, 'ui_info_panel.json'), 'w') as fh:
        json.dump({'width': W, 'padding': base.PAD, 'rowHeight': FILA, 'modulePadding': PADM,
                   'moduleGap': AIRE, 'moduleCell': PZ, 'moduleBorder': BORDE, 'moduleCenterMode': 'tiled',
                   'tabWidth': LENG, 'tabInset': [2, 4], 'textIndentFromTab': EXTRA_IZQ,
                   'tabRows': list(LENGUETAS[:4]) + ['poderes: 3 filas, tramos de arriba abajo'],
                   'tabOf': {'info': 'faccion del nodo', 'mejora': 'accion', 'reconv': 'accion',
                             'poderes': 'poderes'},
                   'enterStaggerMs': ESCALON_MS, 'enterMs': 320, 'enterSlidePx': 12,
                   'note': 'la lengueta lisa en mosaico; la de poderes, sus 3 filas estiradas en 3 tramos iguales; el texto 4 px mas alla de la lengueta, los botones no; un modulo por bloque de 13.3 que tenga filas; alto = 2 * modulePadding + rowHeight '
                           '* filas; la lengueta del lado del nodo, de y 4 a alto - 4, a 2 px del borde; al abrir, '
                           'cada modulo entra 60 ms despues del anterior, deslizandose 12 px desde el nodo con la '
                           'orbital'}, fh, indent=1)


def vistas_extra(d):
    # el escalonado al abrir, cada 40 ms
    import gen_panel_v1 as b
    ims = []
    for ms in range(0, 560, 40):
        c = np.empty((280, 230, 3), np.uint8)
        c[:] = FONDO
        pega(c, panel(CASOS['pulsar'], entrada=ms), 10, 10)
        ims.append(c)
    Image.fromarray(b.ampliar(np.hstack(ims[::2]), 2)).save(os.path.join(d, 'entrada_x2.png'))
    gif = [Image.fromarray(b.ampliar(c, 2)) for c in ims] + [Image.fromarray(b.ampliar(ims[-1], 2))] * 10
    gif[0].save(os.path.join(d, 'entrada_x2.gif'), save_all=True, append_images=gif[1:], duration=40, loop=0)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'str_e', 'neutral', 'hielo', 'decaimiento', 'gusano')
                  | {tuple(PANEL), tuple(FONDO)})
    vistas_extra(os.path.join(base.dir_preview('ui_' + base.ASSET), 'v3'))
