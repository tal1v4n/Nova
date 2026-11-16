"""NOVA - ART-UI-IGN-MENU v2, el menu radial de encendido: «Los nodos».

Ref: NOVA_Assets_Diseno.md A.6 (y A.2, los sprites de nivel 1) · Spec 13.7,
13.12, 16.1, 18 #29, #36

Lo comun -la geometria, los estados, la entrada, las comprobaciones y las
vistas- vive en `gen_ignmenu_v1.py`.

LA TESIS DE LA v2: CADA OPCION ES LO QUE VAS A TENER. En vez de un
pictograma, la opcion ensena el SPRITE REAL del nodo de nivel 1 del
Jugador (A.2: la Estrella v1, el Pulsar v2 y el Magnetar v1), recortado a
una celda de 72 x 72 -sus huellas de nivel 1 miden 39, 44 y 41, y el
Pulsar llega a 65 de ancho con los haces-. No hay tecla: cuatro ESQUINAS
de 7 x 7, como las de «La sonda», enmarcan el circulo de clic, 48 x 48, y
los haces del Pulsar asoman por fuera. El
coste no va dentro, que taparia el nodo: va en la linea del nombre,
`Estrella 10`.

    normal      el nodo entero, esquinas en `str_p[5]`
    encima      las esquinas se cierran 2 px sobre el nodo (orbital,
                160 ms) y se encienden en `str_p[2]`
    pulsado     cerradas 3 px, en `str_p[1]`, y el nodo baja 2 px
    falta       el nodo tramado al 50 % con la Bayer -aun no es tuyo- y las
                esquinas en gris; el coste, en magenta
    nodisp      solo el contorno del nodo en `neutral[4]`, sin esquinas ni
                coste

PIEZAS
    ui_ignition_menu_corner.png     la esquina de arriba a la izquierda, una
                                    columna por estado (normal, encima,
                                    pulsado, falta); se refleja para las otras
    ui_ignition_menu.json
    Los nodos son los sprites de nivel 1 de A.2; la trama y el contorno los
    hace el shader de sprite o se exportan aqui:
    ui_ignition_menu_nodes.png      fila = tipo; columnas: entero, tramado,
                                    contorno (72 x 72)
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_ignmenu_v1 as base
from gen_ignmenu_v1 import (BAJA, ESTADOS, NOMBRE, TIPOS, ancho, json_comun, nombre_xy)
import gen_panel_v1 as pv1
from gen_panel_v1 import FONDO, NEU, PANEL, STR_E, STR_P, colores_de, pega, pinta, rgba
from nova_core import BAYER8, RAIZ

VERSION = 2
NOMBRE_V = 'Los nodos'
LADO = 48                        # el marco de las esquinas: el circulo de clic
CELDA = 72                       # el Pulsar mide 65 de ancho con sus haces
ESQ = 7
SPRITE = {'estrella': ('estrella', 1, 'str'), 'pulsar': ('pulsar', 2, 'pul'), 'magnetar': ('magnetar', 1, 'mag')}


def sprite(tipo):
    carpeta, v, pre = SPRITE[tipo]
    im = np.asarray(Image.open(os.path.join(RAIZ, 'art', 'nodes', carpeta, f'v{v}',
                                            f'{pre}_lvl1_player.png')).convert('RGBA'))
    h, w = im.shape[:2]
    return im[(h - CELDA) // 2:(h + CELDA) // 2, (w - CELDA) // 2:(w + CELDA) // 2].copy()


def tramado(img):
    out = img.copy()
    for y in range(img.shape[0]):
        for x in range(img.shape[1]):
            if BAYER8[y % 8, x % 8] >= 0.5:
                out[y, x, 3] = 0
    out[out[..., 3] == 0] = 0
    return out


def contorno(img):
    sil = img[..., 3] > 0
    borde = sil & ~pv1.erosion(sil, 1)
    out = rgba(*img.shape[:2])
    out[borde, :3] = NEU[4]
    out[borde, 3] = 255
    return out


def esquina(estado):
    c = {'normal': STR_P[5], 'encima': STR_P[2], 'pulsado': STR_P[1], 'falta': NEU[5]}[estado]
    e = rgba(ESQ, ESQ)
    e[:2, :, :3] = c
    e[:, :2, :3] = c
    e[:2, :, 3] = 255
    e[:, :2, 3] = 255
    return e


def opcion(estado, tipo, energia, f=0, t=1.0):
    img = rgba(CELDA, CELDA)
    o = (CELDA - LADO) // 2
    dy = BAJA if estado == 'pulsado' else 0
    s = sprite(tipo)
    if estado == 'falta':
        s = tramado(s)
    elif estado == 'nodisp':
        s = contorno(s)
    pega(img, s, 0, -1 + dy)
    if estado == 'nodisp':
        return img
    cierra = {'encima': int(round(2 * base.orbital(t))), 'pulsado': 3}.get(estado, 0)
    e = esquina(estado)
    a, b = o + cierra, o + LADO - cierra - ESQ
    for flip_y, y in ((False, a), (True, b)):
        for flip_x, x in ((False, a), (True, b)):
            pz = e[::-1] if flip_y else e
            pz = pz[:, ::-1] if flip_x else pz
            pega(img, pz, x, y)
    return img


def rotulo(img, i, tipo, estado, c):
    """El nombre y, a su derecha, el coste: `Estrella 10`."""
    nx, ny = nombre_xy(i)
    nombre = NOMBRE[tipo]
    coste = '' if estado == 'nodisp' else str(base.COSTE)
    w = ancho(nombre) + (4 + ancho(coste) if coste else 0)
    x = int(round(c + nx)) - w // 2
    y = int(round(c + ny)) - 5
    color = {'normal': base.C_NOMBRE, 'encima': STR_P[0], 'pulsado': STR_P[0],
             'falta': base.C_GRIS, 'nodisp': base.C_APAGADO}[estado]
    pinta(img, nombre, x, y, color)
    if coste:
        cc = {'falta': base.C_FALTA, 'encima': STR_P[1], 'pulsado': STR_P[1]}.get(estado, base.C_COSTE)
        pinta(img, coste, x + ancho(nombre) + 4, y, cc)


def piezas():
    d = {f'esquina_{e}': esquina(e) for e in ('normal', 'encima', 'pulsado', 'falta')}
    d['nodos'] = np.vstack([np.hstack([sprite(t), tramado(sprite(t)), contorno(sprite(t))]) for t in TIPOS])
    return d


def comprobar_propio():
    fallos = []
    for t in TIPOS:
        s = sprite(t)
        if (s[0, :, 3] > 0).any() or (s[-1, :, 3] > 0).any() or (s[:, 0, 3] > 0).any() or (s[:, -1, 3] > 0).any():
            fallos.append(f'el recorte de {CELDA} corta el sprite de {t}')
    # el nombre con el coste cabe en el Extent
    for i, t in enumerate(TIPOS):
        nx, _ = nombre_xy(i)
        if abs(nx) + (ancho(NOMBRE[t]) + 4 + ancho('10')) / 2 > base.EXTENT:
            fallos.append(f'«{NOMBRE[t]} 10» se sale')
    return fallos


def exportar(d):
    p = piezas()
    Image.fromarray(np.hstack([p[f'esquina_{e}'] for e in ('normal', 'encima', 'pulsado', 'falta')]),
                    'RGBA').save(os.path.join(d, 'ui_ignition_menu_corner.png'))
    Image.fromarray(p['nodos'], 'RGBA').save(os.path.join(d, 'ui_ignition_menu_nodes.png'))
    j = json_comun()
    j.update({'cell': CELDA, 'frame': LADO, 'corner': ESQ, 'cornerColumns': ['normal', 'hover', 'pressed', 'missing'],
              'cornerInset': 1, 'hoverClose': 2, 'pressedClose': 3,
              'nodesRows': list(TIPOS), 'nodesColumns': ['full', 'dithered', 'outline'],
              'nodeSprites': {t: f'art/nodes/{c}/v{v}/{p}_lvl1_player.png' for t, (c, v, p) in SPRITE.items()},
              'costInLabel': True, 'costGap': 4,
              'note': 'el nodo de 48 x 48 centrado en la opcion (1 px arriba; pulsado, 1 px abajo); cuatro '
                      'esquinas reflejadas a cornerInset del borde de la celda, que se cierran hoverClose px '
                      'con la orbital en 160 ms al pasar el raton y pressedClose al pulsar; falta: el nodo '
                      'dithered y la esquina missing; nodisp: el nodo outline, sin esquinas; el coste va en '
                      'la linea del nombre, a costGap px'})
    with open(os.path.join(d, 'ui_ignition_menu.json'), 'w', encoding='utf-8') as fh:
        json.dump(j, fh, indent=1, ensure_ascii=False)


def permitidos():
    s = colores_de('str_p', 'str_e', 'neutral') | {tuple(PANEL), tuple(FONDO)}
    for t in TIPOS:
        im = sprite(t)
        s |= {tuple(int(v) for v in c) for c in im[..., :3][im[..., 3] > 0]}
    return s


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE_V, permitidos())
