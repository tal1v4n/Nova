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

T6-15, AJUSTE DEL USUARIO (2026-11-16, al verlo en Play). El rombo pasa de
13 a 17 (19 x 19 con el contorno): costaba acertarle. El RELLENO CIAN y el
SONAR salen en cuanto la mejora se puede pagar, sin el cursor: LISTO es el
rombo lleno tramado al 50 % con el sonar NITIDO encima; ACTIVO, el mismo al
100 %. ENCIMA marca el borde: el contorno pasa a blanco (`str_p[0]`), y solo
existe sobre un icono que se puede pulsar. El AVISO sigue igual: rombo vacio
tramado, sin sonar, porque no se puede hacer nada con el.

PIEZAS
    ui_upgrade_icon_sheet.png   filas: estado; columnas: 16 frames; celdas
                                de 29 x 20, el rombo centrado
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
R = 8                         # rombo de 17 x 17; 19 x 19 con el contorno
S = 2 * R + 3
CW = 29                       # el rombo y el sonar a los dos lados
CH = S + 1                    # y una fila para que el pulsado baje
ALTO_MAX = CH
ANCHO_MAX = CW


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
    # el chevron: una V invertida de 9 de ancho, 2 px de grueso, sin tocar el filo
    col = FONDO if lleno else STR_P[1]
    for x in range(-4, 5):
        for g in (0, 1):
            y = R - 2 + abs(x) + g
            img[y, R + x, :3] = col
    return img


def contorno_claro(img):
    """El contorno del encima: el mismo anillo de 1 px, en blanco."""
    out = img.copy()
    m = (out[..., 3] > 0) & np.all(out[..., :3] == CONT, axis=-1)
    out[m, :3] = STR_P[0]
    return out


def sonar(out, f, dy):
    """Dos parentesis de luz que salen de los costados del rombo y se apagan."""
    k = f / FRAMES
    sep = int(k * 4)                              # 0..3 px de distancia
    col = STR_P[2] if k < .4 else STR_P[4] if k < .75 else None
    if col is None:
        return
    c = S // 2
    for lado in (-1, 1):
        xb = CW // 2 + lado * (c + 1 + sep)
        for y in range(c - 4, c + 5):
            xx = xb - (lado if y in (c - 4, c + 4) else 0)
            if 0 <= xx < CW:
                out[y + dy, xx, :3] = col
                out[y + dy, xx, 3] = 255


def icono(estado, tipo=None, n=None, f=0):
    r = contornea(rombo(estado != 'aviso'))       # S x S
    if estado in ('encima', 'pulsado'):
        r = contorno_claro(r)
    out = rgba(CH, CW)
    dy = 1 if estado == 'pulsado' else 0
    x0 = (CW - S) // 2
    m = r[..., 3] > 0
    out[dy:dy + S, x0:x0 + S][m] = r[m]
    if estado in ('aviso', 'listo'):
        out = difumina(out)
    # el sonar va despues del difuminado: en listo sale nitido sobre el rombo tramado
    if estado in ('listo', 'activo'):
        sonar(out, f, dy)
    return out


def exportar(d):
    Image.fromarray(base.hoja_estados(sys.modules[__name__]), 'RGBA').save(
        os.path.join(d, 'ui_upgrade_icon_sheet.png'))
    with open(os.path.join(d, 'ui_upgrade_icon.json'), 'w') as fh:
        json.dump({'cellWidth': CW, 'cellHeight': CH, 'frames': FRAMES, 'fps': base.FPS,
                   'rows': list(ESTADOS), 'gap': base.GAP, 'hintMargin': 5, 'pivot': 'bottom-center',
                   'hitBox': [S, S],
                   'note': f'fila = estado, columna = frame; el rombo centrado en x y pegado arriba en la celda; '
                           f'la zona que se pulsa es el rombo de {S} x {S}, no el sonar'}, fh, indent=1)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE, colores_de('str_p') | {tuple(CONT), tuple(PANEL),
                                                                                tuple(FONDO)})
