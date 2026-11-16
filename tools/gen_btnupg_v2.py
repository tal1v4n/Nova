"""NOVA - ART-UI-BTN-UPG v2, el boton de mejora: «Los corchetes».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.3, 13.12

Lo comun -los estados, el texto, las comprobaciones y las vistas- vive en
`gen_btnupg_v1.py`.

LA TESIS DE LA v2: EL BOTON HABLA COMO EL PANEL. «La sonda» no tiene marco
entero, solo esquinas; el boton tampoco: CUATRO CORCHETES de 5 x 5 en
`str_p[4]` sobre la trama del panel, sin relleno. Al poner el raton encima,
los corchetes SE CIERRAN: cada uno alarga sus brazos por el borde hasta
tocar al de enfrente, con la orbital en 160 ms, y el marco queda entero en
el cian de lo que se puede hacer, `str_p[3]`. Pulsado, cerrado, con el
relleno de panel y 2 px mas abajo. Sin Energia, los corchetes en magenta
-lo que falta-; bloqueado, en gris y de puntos. En el nivel maximo no hay
boton: `NIVEL MAXIMO` entre dos filos de puntos, como el filo del panel.

PIEZAS
    ui_btn_upgrade_corner.png   5 x 5, la de arriba a la izquierda, una
                                columna por estado: normal, encima, falta,
                                bloqueado (las otras tres esquinas, espejadas)
    ui_btn_upgrade_edge.png     teselas de 2 x 1 del brazo, por estado
    ui_btn_upgrade.json
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_btnupg_v1 as base
from gen_btnupg_v1 import BAJA, BH, BW, CELDA_H, TY, rotula
from gen_panel_v1 import FONDO, NEU, PANEL, STR_E, STR_P, colores_de, pega, rgba, texto

VERSION = 2
NOMBRE = 'Los corchetes'
ESQ = 5
COLOR = {'normal': STR_P[4], 'encima': STR_P[3], 'pulsado': STR_P[3], 'falta': STR_E[4],
         'bloqueado': NEU[5]}


def esquina(estado):
    c = COLOR[estado]
    e = rgba(ESQ, ESQ)
    for i in range(ESQ):
        if estado != 'bloqueado' or i % 2 == 0:
            e[0, i, :3] = c
            e[i, 0, :3] = c
            e[0, i, 3] = e[i, 0, 3] = 255
    return e


def brazo(estado):
    t = rgba(1, 2)
    t[0, 0, :3] = COLOR[estado]
    t[0, 0, 3] = 255
    if estado != 'bloqueado':
        t[0, 1] = t[0, 0]
    return t


def marco(estado, k):
    """El marco con los brazos alargados una fraccion k (0 corchetes, 1 cerrado)."""
    img = rgba(BH, BW)
    e = esquina(estado)
    br = brazo(estado)[0, 0]
    br2 = brazo(estado)[0, 1]
    lx = ESQ + int(round(k * (BW / 2 - ESQ)))
    ly = ESQ + int(round(k * (BH / 2 - ESQ)))
    for x in range(BW):
        if x < lx or x >= BW - lx:
            c = br if x % 2 == 0 else br2
            img[0, x] = c
            img[BH - 1, x] = c
    for y in range(BH):
        if y < ly or y >= BH - ly:
            c = br if y % 2 == 0 else br2
            img[y, 0] = c
            img[y, BW - 1] = c
    pega(img, e, 0, 0)
    pega(img, e[:, ::-1], BW - ESQ, 0)
    pega(img, e[::-1], 0, BH - ESQ)
    pega(img, e[::-1, ::-1], BW - ESQ, BH - ESQ)
    return img


def boton(estado, coste, energia, f=0, t=1.0):
    img = rgba(CELDA_H, BW)
    if estado == 'maximo':
        rotula(img, 'maximo', coste, 0, centrado=True)
        w = texto('NIVEL MÁXIMO').shape[1]
        x0 = (BW - w) // 2
        for x in list(range(8, x0 - 6, 2)) + list(range(x0 + w + 6, BW - 8, 2)):
            img[TY + 5, x, :3] = NEU[5]
            img[TY + 5, x, 3] = 255
        return img
    dy = BAJA if estado == 'pulsado' else 0
    k = {'normal': 0, 'falta': 0, 'bloqueado': 0, 'pulsado': 1}.get(estado)
    if k is None:
        k = base.pv1_orbital(t)
    m = marco(estado, k)
    if estado == 'pulsado':
        m[1:BH - 1, 1:BW - 1, :3] = PANEL
        m[1:BH - 1, 1:BW - 1, 3] = 255
    pega(img, m, 0, dy)
    falta = max(0, coste - energia)
    viva = estado in ('encima', 'pulsado')
    rotula(img, estado, coste, falta, dy, color_izq=STR_P[0] if viva else None)
    return img


PULSADO_LIBRE = False


def piezas():
    d = {}
    for e in ('normal', 'encima', 'falta', 'bloqueado'):
        d[f'esquina_{e}'] = esquina(e)
        d[f'brazo_{e}'] = brazo(e)
    return d


def exportar(d):
    est = ('normal', 'encima', 'falta', 'bloqueado')
    p = piezas()
    Image.fromarray(np.hstack([p[f'esquina_{e}'] for e in est]), 'RGBA').save(
        os.path.join(d, 'ui_btn_upgrade_corner.png'))
    Image.fromarray(np.hstack([p[f'brazo_{e}'] for e in est]), 'RGBA').save(
        os.path.join(d, 'ui_btn_upgrade_edge.png'))
    with open(os.path.join(d, 'ui_btn_upgrade.json'), 'w') as fh:
        json.dump({'width': BW, 'height': BH, 'cellHeight': CELDA_H, 'pressedDrop': BAJA, 'capTop': TY,
                   'padX': 6, 'cornerSize': ESQ, 'edgeTile': 2,
                   'columns': list(est), 'pressedUses': 'hover', 'pressedFill': '#11142A',
                   'hoverMs': 160, 'hoverCurve': [0.2, 0.8, 0.2, 1.0],
                   'note': 'normal, falta y bloqueado: solo las cuatro esquinas (espejadas); encima: los brazos '
                           'crecen desde cada esquina por los cuatro bordes hasta la mitad con la orbital en 160 ms; '
                           'pulsado: cerrado, relleno de panel por dentro y 2 px abajo; maximo: sin esquinas, el '
                           'texto centrado entre dos filos de un punto cada 2 px en neutral[5], a 6 px del texto'},
                  fh, indent=1)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'str_e', 'neutral') | {tuple(PANEL), tuple(FONDO)})
