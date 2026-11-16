"""NOVA - ART-UI-BTN-CONV v2, el boton de reconversion: «El barrido».

Ref: NOVA_Assets_Diseno.md A.6 (y A.3.6, `ART-ST-CONV` «El barrido») ·
Spec 8.1, 8.2, 13.3 (punto 5), 13.12 (botones), 18 #29, #33

Lo comun -los estados, el texto, las comprobaciones y las vistas- vive en
`gen_btnconv_v1.py`.

LA TESIS DE LA v2: EL BOTON HACE LO QUE ANUNCIA. Reconvertir es, en el
tablero, un frente que barre el nodo y lo descubre siendo otro
(`ART-ST-CONV`). El boton hace lo mismo consigo: en reposo es el secundario
de 13.12, con el FRENTE aparcado en su borde izquierdo -una raya de 1 px en
`str_p[5]`-; encima, el frente lo BARRE de izquierda a derecha con la
orbital en 160 ms -una columna de luz de 2 px con una estela en trama- y
deja detras el boton PRIMARIO, relleno cian con el texto oscuro: el boton
se convierte en otro mientras se ofrece. Pulsado, el primario se aclara y
baja 2 px.

    falta       el frente se queda PARADO donde llega tu Energia,
                `Energia / coste` del ancho: detras, lo que tienes, en una
                trama gris; delante, lo que falta, en una trama magenta. El
                coste en magenta.
    bloqueado   gris, sin frente, con la trama diagonal: cerrado, no vacio
    nodisp      el marco en trazos y solo el tipo

PIEZAS
    ui_btn_convert_plate.png    20 x 20, nueve cortes, una columna por
                                estado (el frente aparcado va en el borde
                                izquierdo del normal)
    ui_btn_convert_front.png    el frente, 5 x 16: fila 0 el que barre
                                (cian), fila 1 el parado de falta (gris)
    ui_btn_convert_fill.png     teselas de 8 x 8: lo que tienes (gris) y lo
                                que falta (magenta)
    ui_btn_convert.json
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_btnconv_v1 as base
from gen_btnconv_v1 import (BAJA, BH, BW, CELDA_H, ESTADOS, dentro_de, faltan, json_comun, rotula,
                            trazos)
import gen_panel_v1 as pv1
from gen_panel_v1 import FONDO, NEU, PANEL, STR_E, STR_P, colores_de, pega, placa_lamina, rgba
from nova_core import BAYER8

VERSION = 2
NOMBRE = 'El barrido'
PZ, BORDE = 20, 6
X0, X1 = 2, BW - 2               # el interior, dentro del marco de 2
FH = BH - 4                      # alto del frente: el interior
ESTELA = 3


def tecla(estado):
    marco = {'normal': STR_P[6], 'encima': STR_P[4], 'pulsado': STR_P[1],
             'falta': NEU[5], 'bloqueado': NEU[5], 'nodisp': NEU[5]}[estado]
    p = placa_lamina(PZ, PZ, marco)
    sil = pv1.escalera(PZ, PZ)
    dentro = dentro_de(PZ, PZ)
    if estado == 'normal':
        p[4:PZ - 4, 3, :3] = STR_P[5]          # el frente, aparcado
    if estado == 'bloqueado':
        for y in range(PZ):
            for x in range(PZ):
                if dentro[y, x] and (x + y) % 4 == 0:
                    p[y, x, :3] = NEU[5]
    if estado == 'nodisp':
        p[(sil & ~dentro) & ~trazos(sil), :3] = PANEL
    return p


def frente(tipo):
    """5 x FH: 2 px de columna y ESTELA px de trama por delante."""
    f = rgba(FH, 2 + ESTELA)
    if tipo == 'barre':
        f[:, 0, :3], f[:, 1, :3] = STR_P[1], STR_P[0]
        for y in range(FH):
            for i in range(ESTELA):
                if BAYER8[y % 8, i] < (0.5, 0.25, 0.1)[i]:
                    f[y, 2 + i, :3] = STR_P[3]
    else:                                      # parado: lo que tienes acaba aqui
        f[:, 0, :3] = NEU[2]
    f[..., 3] = np.where(f[..., :3].any(-1), 255, 0)
    return f


def trama(tipo):
    t = rgba(8, 8)
    for y in range(8):
        for x in range(8):
            if tipo == 'tienes' and BAYER8[y, x] < 0.25:
                t[y, x, :3] = NEU[4]
            elif tipo == 'falta' and BAYER8[y, x] < 0.125:
                t[y, x, :3] = STR_E[5]
    t[..., 3] = np.where(t[..., :3].any(-1), 255, 0)
    return t


def mosaico(t, h, w):
    return np.tile(t, (h // 8 + 1, w // 8 + 1, 1))[:h, :w]


def boton(estado, destino, coste, energia, f=0, t=1.0):
    import gen_energy_v2 as e2
    img = rgba(CELDA_H, BW)
    dy = BAJA if estado == 'pulsado' else 0
    cuerpo = pv1.nueve(tecla(estado), BW, BH, BORDE)
    dentro = dentro_de()
    oscuro = None
    if estado in ('encima', 'pulsado'):
        xf = X1 if estado == 'pulsado' else X0 + int(round((X1 - X0) * e2.orbital(t)))
        lleno = dentro.copy()
        lleno[:, xf:] = False
        cuerpo[lleno, :3] = STR_P[2] if estado == 'pulsado' else STR_P[3]
        if estado == 'encima' and xf < X1:
            fr = frente('barre')
            m = np.zeros_like(dentro)
            capa = rgba(BH, BW)
            pega(capa, fr, xf - 2, 2)
            m = dentro & (capa[..., 3] > 0)
            cuerpo[m] = capa[m]
        oscuro = lleno
    if estado == 'falta':
        frac = 0 if coste <= 0 else min(1.0, energia / coste)
        xf = X0 + int(round((X1 - X0) * frac))
        tienes = dentro.copy()
        tienes[:, xf:] = False
        resto = dentro.copy()
        resto[:, :xf + 1] = False
        tt, tf = mosaico(trama('tienes'), BH, BW), mosaico(trama('falta'), BH, BW)
        m = tienes & (tt[..., 3] > 0)
        cuerpo[m] = tt[m]
        m = resto & (tf[..., 3] > 0)
        cuerpo[m] = tf[m]
        if X0 < xf < X1:
            capa = rgba(BH, BW)
            pega(capa, frente('parado'), xf, 2)
            m = dentro & (capa[..., 3] > 0)
            cuerpo[m] = capa[m]
    pega(img, cuerpo, 0, dy)
    claro = estado in ('encima', 'pulsado')
    rotula(img, estado, destino, coste, dy,
           ci=STR_P[0] if claro else None, cd=STR_P[1] if claro else None, oscuro=oscuro)
    return img


def piezas():
    d = {f'tecla_{e}': tecla(e) for e in ESTADOS}
    d['frente_barre'], d['frente_parado'] = frente('barre'), frente('parado')
    d['trama_tienes'], d['trama_falta'] = trama('tienes'), trama('falta')
    return d


def comprobar_propio():
    fallos = []
    # a mitad del barrido hay cian detras y secundario delante
    im = boton('encima', 'magnetar', 30, 45, t=0.15)
    mitad = im[CELDA_H // 2]
    if not ((mitad[:, :3] == STR_P[3]).all(-1).any() and (mitad[:, :3] == PANEL).all(-1).any()):
        fallos.append('el barrido no deja dos mitades')
    # acabado el barrido, el boton es primario entero
    if (boton('encima', 'magnetar', 30, 45, t=1.0)[..., :3] == PANEL).all(-1)[4:BH - 4, 6:BW - 6].sum() < 50:
        fallos.append('el primario no lleva el texto oscuro')
    return fallos


def exportar(d):
    p = piezas()
    Image.fromarray(np.hstack([p[f'tecla_{e}'] for e in ESTADOS]), 'RGBA').save(
        os.path.join(d, 'ui_btn_convert_plate.png'))
    Image.fromarray(np.vstack([p['frente_barre'], p['frente_parado']]), 'RGBA').save(
        os.path.join(d, 'ui_btn_convert_front.png'))
    Image.fromarray(np.hstack([p['trama_tienes'], p['trama_falta']]), 'RGBA').save(
        os.path.join(d, 'ui_btn_convert_fill.png'))
    j = json_comun()
    j.update({'plateCell': PZ, 'plateBorder': BORDE, 'plateCenterMode': 'tiled',
              'plateColumns': list(ESTADOS), 'innerX0': X0, 'innerX1': X1, 'frontHeight': FH,
              'frontRows': ['sweep', 'stopped'], 'fillTile': 8, 'fillColumns': ['have', 'missing'],
              'primaryFill': base.hexa(STR_P[3]), 'pressedFill': base.hexa(STR_P[2]),
              'primaryText': base.hexa(PANEL),
              'note': 'encima: el frente sweep (x 0-1 la columna) va de innerX0 a innerX1 con la orbital en '
                      '160 ms; a su izquierda, dentro del marco, primaryFill y el texto en primaryText; '
                      'pulsado: todo pressedFill, 2 px abajo; falta: el frente stopped en innerX0 + '
                      'round((innerX1 - innerX0) * Energia / coste), have a su izquierda y missing a su '
                      'derecha, en mosaico absoluto; bloqueado: gris con trama diagonal; nodisp: marco en '
                      'trazos y solo el tipo'})
    with open(os.path.join(d, 'ui_btn_convert.json'), 'w', encoding='utf-8') as fh:
        json.dump(j, fh, indent=1, ensure_ascii=False)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'str_e', 'neutral') | {tuple(PANEL), tuple(FONDO)})
