"""NOVA - ART-UI-BTN-UPG v2, el icono de mejora sobre el nodo: «Los peldanos».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.12 · 16.2 (niveles por tipo)

Lo comun -la regla, los estados, el sitio, las comprobaciones y las vistas-
vive en `gen_upgicon_v1.py`.

LA TESIS DE LA v2: EL ICONO ENSENA QUE NIVEL COMPRAS. No es una flecha
generica: es una escalera de PELDANOS que suben, uno por nivel del tipo
-cinco la Estrella y el Magnetar, tres el Pulsar (16.2)-, de 3 px de ancho y
de 5 a 13 de alto. Los niveles que el nodo ya tiene, llenos en el ancla
cian; los que quedan despues del siguiente, apagados en `str_p[6]`; y el
SIGUIENTE -el que compra el clic- es el que vive: activo, se llena de abajo
arriba en blanco una y otra vez (1.33 s); encima, entero en blanco; pulsado,
toda la escalera baja 1 px. El icono dice a la vez que se puede mejorar,
a que nivel se va y cuantos quedan.

PIEZAS
    ui_upgrade_icon_<tipo>_sheet.png   filas: nivel actual x estado
                                       (aviso, listo, activo, encima,
                                       pulsado); columnas: 16 frames
    ui_upgrade_icon.json
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_upgicon_v1 as base
from gen_upgicon_v1 import (CONT, ESTADOS, FRAMES, MEJORABLES, STR_P, colores_de, contornea, difumina,
                            rgba)

VERSION = 2
NOMBRE = 'Los peldanos'
BW, SEP = 3, 1
MAXNIV = {m[0]: m[5] for m in MEJORABLES}


def escalera(tipo, n, estado, f):
    L = MAXNIV[tipo]
    alto = 3 + 2 * L
    img = rgba(alto, L * BW + (L - 1) * SEP)
    for i in range(1, L + 1):
        h = 3 + 2 * i
        x0 = (i - 1) * (BW + SEP)
        for y in range(alto - h, alto):
            fila_desde_abajo = alto - 1 - y
            if i <= n:
                c = STR_P[1] if y == alto - h else STR_P[3]
            elif i == n + 1:
                if estado in ('encima', 'pulsado'):
                    c = STR_P[0]
                elif estado == 'activo':
                    lleno = int(round((f + 1) / FRAMES * h))
                    c = STR_P[0] if fila_desde_abajo < lleno else STR_P[5]
                else:
                    c = STR_P[4]
            else:
                c = STR_P[6]
            img[y, x0:x0 + BW, :3] = c
            img[y, x0:x0 + BW, 3] = 255
    return img


def icono(estado, tipo='estrella', n=1, f=0):
    a = contornea(escalera(tipo, n, estado, f))
    out = rgba(a.shape[0] + 1, a.shape[1])
    dy = 1 if estado == 'pulsado' else 0
    out[dy:dy + a.shape[0]] = a
    if estado in ('aviso', 'listo'):
        out = difumina(out)
    return out


def comprobar_propio():
    fallos = []
    for tipo, _, _, _, niveles, _ in MEJORABLES:
        if len({icono('activo', tipo, n, 0).tobytes() for n in niveles}) < len(niveles):
            fallos.append(f'{tipo}: dos niveles con el mismo icono')
    return fallos


def exportar(d):
    for tipo, _, _, _, niveles, _ in MEJORABLES:
        filas = [np.hstack([icono(e, tipo, n, f) for f in range(FRAMES)]) for n in niveles for e in ESTADOS]
        Image.fromarray(np.vstack(filas), 'RGBA').save(os.path.join(d, f'ui_upgrade_icon_{tipo}_sheet.png'))
    with open(os.path.join(d, 'ui_upgrade_icon.json'), 'w') as fh:
        json.dump({'frames': FRAMES, 'fps': base.FPS, 'gap': base.GAP, 'hintMargin': 5, 'pivot': 'bottom-center',
                   'states': list(ESTADOS),
                   'sheets': {t: {'levels': list(ns), 'cellWidth': icono('activo', t, ns[0]).shape[1],
                                  'cellHeight': icono('activo', t, ns[0]).shape[0]}
                              for t, _, _, _, ns, _ in MEJORABLES},
                   'note': 'una hoja por tipo; fila = (nivel actual - 1) * 5 + estado; columna = frame; '
                           'centrado sobre la huella, su borde de abajo a gap px de ella'}, fh, indent=1)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE, colores_de('str_p') | {tuple(CONT)})
