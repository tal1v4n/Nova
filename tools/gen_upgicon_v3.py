"""NOVA - ART-UI-BTN-UPG v3, el icono de mejora sobre el nodo: «El sello».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.12 (botones, patron «Sonar»)

Lo comun -la regla, los estados, el sitio, las comprobaciones y las vistas-
vive en `gen_upgicon_v1.py`.

LA TESIS DE LA v3: UN BOTON DE VERDAD, EN PEQUENO. Un rombo de 13 x 13 con el
lenguaje de 13.12 -relleno `#11142A`, filo cian- y un chevron dentro, que se
porta como el boton primario de la lamina: ACTIVO, filo en el ancla y un
SONAR -el patron de 13.12 para «lo que toca hacer ahora»-, dos parentesis de
luz que salen de los costados del rombo y se apagan al alejarse, cada 1.33
s; ENCIMA, el rombo se llena de cian y el chevron pasa a oscuro, como el
primario de 13.12; PULSADO, igual y 1 px mas abajo. El sonar va solo hacia
los lados: arriba no hay sitio (la barra de arriba) y abajo esta el nodo.

PIEZAS
    ui_upgrade_icon_sheet.png   filas: estado; columnas: 16 frames; celdas
                                de 23 x 16, el rombo centrado
    ui_upgrade_icon.json
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_upgicon_v1 as base
from gen_upgicon_v1 import (CONT, ESTADOS, FONDO, FRAMES, PANEL, STR_P, colores_de, contornea, difumina,
                            rgba)

VERSION = 3
NOMBRE = 'El sello'
R = 6                         # rombo de 13 x 13
CW = 23


def rombo(lleno):
    s = 2 * R + 1
    img = rgba(s, s)
    for y in range(s):
        for x in range(s):
            d = abs(x - R) + abs(y - R)
            if d <= R:
                if d == R:
                    img[y, x, :3] = STR_P[3] if not lleno else STR_P[1]
                else:
                    img[y, x, :3] = STR_P[3] if lleno else PANEL
                img[y, x, 3] = 255
    # el chevron: una V invertida de 7 de ancho, 2 px de grueso
    col = FONDO if lleno else STR_P[1]
    for x in range(-3, 4):
        for g in (0, 1):
            y = R - 1 + abs(x) + g
            img[y, R + x, :3] = col
    return img


def icono(estado, tipo=None, n=None, f=0):
    lleno = estado in ('encima', 'pulsado')
    r = contornea(rombo(lleno))                   # 15 x 15
    out = rgba(16, CW)
    dy = 1 if estado == 'pulsado' else 0
    x0 = (CW - r.shape[1]) // 2
    m = r[..., 3] > 0
    out[dy:dy + 15, x0:x0 + 15][m] = r[m]
    if estado == 'activo':
        # el sonar: dos parentesis que salen de los costados y se apagan
        k = f / FRAMES
        sep = int(k * 4)                          # 0..3 px de distancia
        col = STR_P[2] if k < .4 else STR_P[4] if k < .75 else None
        if col is not None:
            for lado in (-1, 1):
                xb = CW // 2 + lado * (8 + sep)
                for y in range(4, 11):
                    xx = xb + (lado if y in (4, 10) else 0) * -1
                    if 0 <= xx < CW:
                        out[y + dy, xx, :3] = col
                        out[y + dy, xx, 3] = 255
    if estado in ('aviso', 'listo'):
        out = difumina(out)
    return out


def exportar(d):
    Image.fromarray(base.hoja_estados(sys.modules[__name__]), 'RGBA').save(
        os.path.join(d, 'ui_upgrade_icon_sheet.png'))
    with open(os.path.join(d, 'ui_upgrade_icon.json'), 'w') as fh:
        json.dump({'cellWidth': CW, 'cellHeight': 16, 'frames': FRAMES, 'fps': base.FPS,
                   'rows': list(ESTADOS), 'gap': base.GAP, 'hintMargin': 5, 'pivot': 'bottom-center',
                   'hitBox': [15, 15],
                   'note': 'fila = estado, columna = frame; el rombo centrado en la celda; la zona que se pulsa '
                           'es el rombo de 15 x 15, no el sonar'}, fh, indent=1)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE, colores_de('str_p') | {tuple(CONT), tuple(PANEL),
                                                                                tuple(FONDO)})
