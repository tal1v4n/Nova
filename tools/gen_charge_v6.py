"""NOVA - ART-UI-CHARGE v6, la barra de carga del Pulsar: «Las bengalas».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 7.1, 14.4

Lo comun a la ronda -la barra con remate, la comparativa- vive en
`gen_charge_v4.py`; el sitio y las comprobaciones, en `gen_charge_v1.py`.

LA TESIS DE LA v6: LISTA CHISPORROTEA. Cargando, solo arde la llama, en el
frente. Lista, ARDE LA BARRA ENTERA: la mecha se vuelve una varilla del
color del poder (filas 2-3, indices 3 y 2) y por toda ella, en las seis
filas, saltan CHISPAS blancas y claras -doce por frame, en sitios
distintos cada frame-, como una bengala encendida de punta a punta. Es lo
que mas se mueve de la ronda, y lo que mas se ve en la periferia.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_charge_v1 as base
from gen_charge_v1 import ALTO_MAX, FACCIONES, FRAMES, PODERES, coste, hash2, pon, rampa_poder, rgba, tesela
import gen_charge_v4 as ronda
from gen_charge_v4 import ANCHO, CAP, LARGO, X0

VERSION = 6
NOMBRE = 'Las bengalas'
CHISPAS = 12


def varilla(p):
    R = rampa_poder(p)
    t = rgba(ALTO_MAX, 8)
    for x in range(8):
        pon(t, 2, x, R[2])
        pon(t, 3, x, R[3])
    return t


def chispas(p, f):
    """Las chispas de un frame: (y, x relativa a la mecha, color)."""
    R = rampa_poder(p)
    out = []
    for k in range(CHISPAS):
        h = hash2(f % FRAMES, k, 23)
        x = h % LARGO
        y = (h >> 6) % ALTO_MAX
        out.append((y, x, R[0] if k % 3 else R[1]))
    return out


def pinta_lista(img, p, fac, f):
    t = tesela(LARGO, varilla(p))
    sub = img[:, X0:X0 + LARGO]
    m = t[..., 3] > 0
    sub[m] = t[m]
    for y, x, c in chispas(p, f):
        img[y, X0 + x, :3] = c
        img[y, X0 + x, 3] = 255
    ronda.estrella(img, p, f)


def barra(p, inv, fac, pausada=False, f=0):
    return ronda.barra_con_remate(p, inv, fac, pausada, f, pinta_lista)


def comprobar_propio():
    fallos = ronda.comprobar_remate(sys.modules[__name__])
    for p in PODERES:
        vistos = {barra(p, coste(p), 'player', False, f).tobytes() for f in range(FRAMES)}
        if len(vistos) < FRAMES // 2:
            fallos.append(f'{p}: las chispas se repiten demasiado')
        if not (barra(p, coste(p), 'player', False, 0) == barra(p, coste(p), 'player', False, FRAMES)).all():
            fallos.append(f'{p}: el bucle no cierra')
    return fallos


def exportar(d):
    # Las chispas no son una tesela: van horneadas, un frame por columna y una fila por poder (44 x 6).
    hoja = []
    for p in PODERES:
        fila = []
        for f in range(FRAMES):
            im = rgba(ALTO_MAX, LARGO)
            t = tesela(LARGO, varilla(p))
            m = t[..., 3] > 0
            im[m] = t[m]
            for y, x, c in chispas(p, f):
                im[y, x, :3] = c
                im[y, x, 3] = 255
            fila.append(im)
        hoja.append(np.hstack(fila))
    ronda.exportar_comun(d, {
        'ready': 'sparkles', 'readyCell': [LARGO, ALTO_MAX], 'readyRows': list(PODERES),
        'note': 'cargando, como la v3 con la mecha de fuseLength; lista: la mecha se quita y entre casquillo y remate '
                'va la celda del frame f de ui_charge_ready_sheet (la varilla con sus chispas), y la llama lista en '
                'readyFlameX; el remate siempre'},
        {'ui_charge_ready_sheet.png': np.vstack(hoja)})


if __name__ == '__main__':
    ronda.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                   base.colores_de('str_p', 'str_e', 'hielo', 'decaimiento', 'gusano'))
