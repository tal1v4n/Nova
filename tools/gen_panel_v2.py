"""NOVA - ART-UI-PANEL v2, el panel de clic derecho: «La sonda».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.3, 13.12 · 14.9 (filamento)

Lo comun -los casos, el texto, el boceto de los botones, las comprobaciones
y las vistas- vive en `gen_panel_v1.py`.

LA TESIS DE LA v2: UNA LECTURA, NO UNA CAJA. El panel es lo que devuelve una
sonda lanzada al nodo: un CABLE DE PLASMA sale del borde del nodo, en el
plasma de su bando, y llega a la lectura; un pulso lo recorre del nodo al
panel cada 2.67 s (el patron «Flujo» de 13.12). La lectura no tiene marco
entero: CUATRO ESQUINAS de 2 px en el ancla del bando y, entre ellas, un
filo de puntos. El fondo es una TRAMA casi opaca del color del espacio con
lineas de barrido cada 4 px en el relleno de panel: deja ver el tablero lo
justo para que el panel parezca proyectado, sin perder el texto. Bajo el
titulo, una raya del bando con un testigo que parpadea: la lectura esta
viva y se repinta (18 #30).

    cable     teselas de 8 x 3 por bando, 32 frames: puntos cada 2 px en el
              indice 4 del plasma y el pulso -indices 0 y 1- de 3 px
    enchufe   3 x 7 en el ancla del bando, donde el cable toca el panel
    trama     tesela de 8 x 8: `#0B0E1A` donde la Bayer pasa de 12/64 y
              `#11142A` en las lineas de barrido
    esquina   8 x 8, la de arriba a la izquierda; las otras tres, espejadas
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_panel_v1 as base
from gen_panel_v1 import (CASOS, FACCIONES, FILA, FONDO, PAD, PANEL, RFAC, W, colores_de,
                          fila_contenido, filas_de, pega, rgba)
from nova_core import BAYER8, rampa

VERSION = 2
NOMBRE = 'La sonda'
FRAMES = 32
SALIENTE = 0
UMBRAL = 12                  # sobre 64: la trama deja pasar ~19 % del tablero
ESQ = 8
PLASMA = {'player': rampa('plasma_p'), 'enemy': rampa('plasma_e'), 'neutral': rampa('neutral')}


def bando_de(fac):
    R = RFAC[fac]
    return {'titulo': R[2] if fac != 'neutral' else R[1]}


def trama():
    t = rgba(8, 8)
    for y in range(8):
        for x in range(8):
            if BAYER8[y, x] * 64 >= UMBRAL:          # BAYER8 va de 0 a 63/64
                t[y, x, :3] = PANEL if y % 4 == 0 else FONDO
                t[y, x, 3] = 255
    return t


def esquina(fac):
    R = RFAC[fac]
    e = rgba(ESQ, ESQ)
    e[0:2, :, :3] = R[3]
    e[:, 0:2, :3] = R[3]
    e[0:2, :, 3] = 255
    e[:, 0:2, 3] = 255
    e[0, 0, :3] = R[1]
    return e


def filo(fac):
    """Tesela de 2 x 1 del filo entre esquinas: un punto de cada dos."""
    t = rgba(1, 2)
    t[0, 0, :3] = RFAC[fac][5]
    t[0, 0, 3] = 255
    return t


def cable(fac, f):
    """Tesela de 8 x 3 del cable, frame f. El pulso va hacia +x."""
    P = PLASMA[fac]
    t = rgba(3, 8)
    for x in range(0, 8, 2):
        t[1, x, :3] = P[4]
        t[1, x, 3] = 255
    return t


def pulso(fac):
    """El pulso: 3 x 3, cabeza en el indice 0 y cola en el 1."""
    P = PLASMA[fac]
    t = rgba(3, 3)
    t[1, 0, :3] = P[2]
    t[1, 1, :3] = P[1]
    t[1, 2, :3] = P[0]
    t[0, 2, :3] = P[2]
    t[2, 2, :3] = P[2]
    t[..., 3] = np.where(t[..., :3].any(-1), 255, 0)
    return t


def enchufe(fac):
    R = RFAC[fac]
    t = rgba(7, 3)
    t[:, 0, :3] = R[3]
    t[0, :, :3] = R[3]
    t[6, :, :3] = R[3]
    t[2:5, 1, :3] = R[1]
    t[..., 3] = np.where(t[..., :3].any(-1), 255, 0)
    return t


def raya(fac, w, f):
    """La raya bajo el titulo, con el testigo que parpadea al final."""
    R = RFAC[fac]
    t = rgba(3, w)
    t[1, :w - 6, :3] = R[4]
    t[1, :w - 6, 3] = 255
    if (f // 8) % 2 == 0:
        t[0:3, w - 3:w, :3] = R[2]
        t[0:3, w - 3:w, 3] = 255
    return t


def panel(caso, lado='izq', f=0):
    fac = caso['fac']
    n = filas_de(caso)
    h = PAD + FILA * n + PAD
    img = rgba(h, W)
    tr = np.tile(trama(), (-(-h // 8), W // 8, 1))[:h, :W]
    img[:] = tr
    # el filo entre las esquinas
    fl = filo(fac)
    for x in range(ESQ, W - ESQ, 2):
        img[0, x] = fl[0, 0]
        img[h - 1, x] = fl[0, 0]
    for y in range(ESQ, h - ESQ, 2):
        img[y, 0] = fl[0, 0]
        img[y, W - 1] = fl[0, 0]
    e = esquina(fac)
    pega(img, e, 0, 0)
    pega(img, e[:, ::-1], W - ESQ, 0)
    pega(img, e[::-1], 0, h - ESQ)
    pega(img, e[::-1, ::-1], W - ESQ, h - ESQ)
    bando = bando_de(fac)
    y = PAD
    for i, (cat, filas) in enumerate(caso['bloques']):
        if i > 0:
            for x in range(PAD, W - PAD, 4):
                img[y - 1, x, :3] = RFAC[fac][6] if fac != 'enemy' else RFAC[fac][5]
                img[y - 1, x, 3] = 255
        for j, fila in enumerate(filas):
            fila_contenido(img, fila, PAD, y, W - 2 * PAD, bando)
            if i == 0 and j == 0:
                pega(img, raya(fac, W - 2 * PAD, f), PAD, y + FILA - 4)
            y += FILA
    return img


def antes_de_pegar(lienzo, caso, cx, cy, x, y, w, h, izquierda, f):
    """El cable, del borde del nodo al costado del panel, y el enchufe."""
    import gen_energy_v1 as e1
    fac = caso['fac']
    carpeta, v, pre = base.SPR[caso['tipo']]
    hu = e1.huella(carpeta, pre, caso['nivel'])
    cy = int(cy)
    ya = max(y + 10, min(cy, y + h - 10))          # donde toca el panel
    x0 = int(cx) + (hu + 1) // 2 + 2 if not izquierda else int(cx) - (hu + 1) // 2 - 2
    x1 = x - 3 if not izquierda else x + w + 3
    paso = 1 if x1 >= x0 else -1
    P = PLASMA[fac]
    camino = []
    xm = (x0 + x1) // 2
    for xx in range(x0, xm, paso):
        camino.append((cy, xx))
    for yy in range(cy, ya, 1 if ya >= cy else -1):
        camino.append((yy, xm))
    for xx in range(xm, x1, paso):
        camino.append((ya, xx))
    L = len(camino)
    cab = f / FRAMES * (L + 6) - 3
    for i, (yy, xx) in enumerate(camino):
        d = cab - i
        if 0 <= d < 1:
            c = P[0]
        elif 1 <= d < 3:
            c = P[1]
        elif i % 2 == 0:
            c = P[4]
        else:
            continue
        lienzo[yy, xx] = c
    en = enchufe(fac) if not izquierda else enchufe(fac)[:, ::-1]
    pega(lienzo, en, x1 if not izquierda else x1 - 2, ya - 3)


def piezas():
    d = {'trama': trama()}
    for fac in FACCIONES:
        d[f'esquina_{fac}'] = esquina(fac)
        d[f'filo_{fac}'] = filo(fac)
        d[f'cable_{fac}'] = cable(fac, 0)
        d[f'pulso_{fac}'] = pulso(fac)
        d[f'enchufe_{fac}'] = enchufe(fac)
        d[f'raya_{fac}'] = raya(fac, 32, 0)
    return d


def comprobar_propio():
    fallos = []
    t = trama()
    cubre = (t[..., 3] > 0).mean()
    if not .75 <= cubre <= .9:
        fallos.append(f'la trama cubre el {cubre:.0%}')
    return fallos


def exportar(d):
    p = piezas()
    Image.fromarray(p['trama'], 'RGBA').save(os.path.join(d, 'ui_info_panel_screen.png'))
    for clave, nombre in (('esquina', 'corner'), ('filo', 'edge'), ('cable', 'tether'),
                          ('pulso', 'pulse'), ('enchufe', 'socket')):
        Image.fromarray(np.vstack([p[f'{clave}_{f}'] for f in FACCIONES]), 'RGBA').save(
            os.path.join(d, f'ui_info_panel_{nombre}.png'))
    with open(os.path.join(d, 'ui_info_panel.json'), 'w') as fh:
        json.dump({'width': W, 'padding': PAD, 'rowHeight': FILA, 'rows': list(FACCIONES),
                   'screenTile': 8, 'cornerSize': ESQ, 'edgeStep': 2, 'tetherStep': 2,
                   'pulseLoopFrames': FRAMES, 'fps': 12, 'titleRuleBlinkFrames': 8,
                   'note': 'fondo: la tesela de 8 en mosaico; esquinas espejadas; filo un punto cada 2 px; '
                           'cable del borde del nodo (media huella + 2) al costado del panel, en escalon si el '
                           'panel se desplaza en vertical, un punto cada 2 px y el pulso de 3 px recorriendolo '
                           'del nodo al panel en 32 frames; enchufe a 3 px del panel, en la y del cable'}, fh, indent=1)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'str_e', 'neutral', 'plasma_p', 'plasma_e') | {tuple(PANEL), tuple(FONDO)})
