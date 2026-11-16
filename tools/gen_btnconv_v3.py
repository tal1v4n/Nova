"""NOVA - ART-UI-BTN-CONV v3, el boton de reconversion: «El conducto».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 8.1, 8.2, 13.3 (punto 5), 13.12
(patron «Flujo», «sin Energia»), 18 #29, #33

Lo comun -los estados, el texto, las comprobaciones y las vistas- vive en
`gen_btnconv_v1.py`.

LA TESIS DE LA v3: EL BOTON ENSENA CUANTO TIENES DEL COSTE. La tecla de
13.12 -relleno `#11142A`, marco de 2 px en escalera- lleva en su base un
CONDUCTO de 2 px que se llena de plasma del Jugador (`plasma_p`) con la
Energia del nodo sobre el coste: `min(1, Energia / coste)` del ancho.
Cuando alcanza, el conducto esta lleno y CORRE -un pulso blanco lo
recorre hacia la derecha cada 2.67 s, el patron «Flujo»-: se puede pagar.
Cuando no alcanza, lo que hay en cian quieto y LO QUE FALTA en puntos
magenta (13.12: magenta es lo que falta). Era la v3 del boton de mejora,
descartada solo porque la mejora salio del panel (18 #54); aqui vuelve a
tener un coste que mirar.

    encima      el marco en `str_p[4]` y el plasma sube un tono: se ofrece
    pulsado     el conducto destella en blanco -la Energia sale del nodo-
                y la tecla baja 2 px
    bloqueado   el marco gris y el conducto sellado en gris, quieto y
                entero: no falta nada que poner (18 #33)
    nodisp      el marco en trazos, solo el tipo y sin conducto

PIEZAS
    ui_btn_convert_plate.png    20 x 20, nueve cortes, una columna por
                                estado
    ui_btn_convert_duct.png     teselas de 8 x 2: fila 0, el plasma que corre
                                (32 frames); filas 1-5, quietas: lleno,
                                encima, destello, falta (puntos), sellado
    ui_btn_convert.json
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_btnconv_v1 as base
from gen_btnconv_v1 import BAJA, BH, BW, CELDA_H, ESTADOS, TY, dentro_de, json_comun, rotula, trazos
import gen_panel_v1 as pv1
from gen_panel_v1 import FONDO, NEU, PANEL, STR_E, STR_P, colores_de, pega, placa_lamina, rgba
from nova_core import rampa

VERSION = 3
NOMBRE = 'El conducto'
FRAMES = 32
PZ, BORDE = 20, 6
DUCTO_Y = 16                     # filas 16-17, justo encima del marco de abajo
X0, X1 = 3, BW - 3
SUBE = 2                         # el texto sube para dejar sitio al conducto
PL = rampa('plasma_p')


def tecla(estado):
    marco = {'normal': STR_P[6], 'encima': STR_P[4], 'pulsado': STR_P[4],
             'falta': NEU[5], 'bloqueado': NEU[5], 'nodisp': NEU[5]}[estado]
    p = placa_lamina(PZ, PZ, marco)
    if estado == 'nodisp':
        sil = pv1.escalera(PZ, PZ)
        p[(sil & ~dentro_de(PZ, PZ)) & ~trazos(sil), :3] = PANEL
    return p


def ducto(tipo, f=0):
    """Tesela de 8 x 2."""
    t = rgba(2, 8)
    if tipo == 'corre':
        for x in range(8):
            t[0, x, :3], t[1, x, :3] = PL[2], PL[3]
            if (x - f // 4) % 8 == 0:
                t[0, x, :3] = PL[0]
                t[1, x, :3] = PL[1]
    elif tipo == 'lleno':
        t[0, :, :3], t[1, :, :3] = PL[2], PL[3]
    elif tipo == 'encima':
        t[0, :, :3], t[1, :, :3] = PL[1], PL[2]
    elif tipo == 'destello':
        t[0, :, :3], t[1, :, :3] = PL[0], PL[1]
    elif tipo == 'falta':
        for x in range(0, 8, 2):
            t[1, x, :3] = STR_E[4]
    elif tipo == 'sellado':
        t[0, :, :3], t[1, :, :3] = NEU[3], NEU[4]
    t[..., 3] = np.where(t[..., :3].any(-1), 255, 0)
    return t


def tira(tipo, n, f=0):
    return np.tile(ducto(tipo, f), (1, -(-max(n, 1) // 8), 1))[:, :n]


def boton(estado, destino, coste, energia, f=0, t=1.0):
    img = rgba(CELDA_H, BW)
    largo = X1 - X0
    dy = BAJA if estado == 'pulsado' else 0
    cuerpo = pv1.nueve(tecla(estado), BW, BH, BORDE)
    if estado == 'falta':
        n = int(round(largo * (0 if coste <= 0 else min(1.0, energia / coste))))
        pega(cuerpo, tira('falta', largo), X0, DUCTO_Y)
        if n:
            cuerpo[DUCTO_Y:DUCTO_Y + 2, X0:X0 + n] = tira('lleno', n)
    elif estado == 'bloqueado':
        cuerpo[DUCTO_Y:DUCTO_Y + 2, X0:X1] = tira('sellado', largo)
    elif estado != 'nodisp':
        tipo = {'normal': 'corre', 'encima': 'encima', 'pulsado': 'destello'}[estado]
        cuerpo[DUCTO_Y:DUCTO_Y + 2, X0:X1] = tira(tipo, largo, f)
    pega(img, cuerpo, 0, dy)
    claro = estado in ('encima', 'pulsado')
    rotula(img, estado, destino, coste, dy, ci=STR_P[0] if claro else None,
           ty=TY - SUBE if estado != 'nodisp' else TY)
    return img


def piezas():
    d = {f'tecla_{e}': tecla(e) for e in ESTADOS}
    for tp in ('lleno', 'encima', 'destello', 'falta', 'sellado'):
        d[f'ducto_{tp}'] = ducto(tp)
    for f in range(FRAMES):
        d[f'corre_{f}'] = ducto('corre', f)
    return d


def comprobar_propio():
    fallos = []
    if not (ducto('corre', FRAMES) == ducto('corre', 0)).all():
        fallos.append('el plasma no cierra el bucle')
    if TY - SUBE + 10 + 3 > DUCTO_Y:
        fallos.append('el texto pisa el conducto')
    return fallos


def exportar(d):
    p = piezas()
    Image.fromarray(np.hstack([p[f'tecla_{e}'] for e in ESTADOS]), 'RGBA').save(
        os.path.join(d, 'ui_btn_convert_plate.png'))
    fila0 = np.hstack([p[f'corre_{f}'] for f in range(FRAMES)])
    quietas = [np.hstack([p[f'ducto_{tp}']] + [rgba(2, 8)] * (FRAMES - 1))
               for tp in ('lleno', 'encima', 'destello', 'falta', 'sellado')]
    Image.fromarray(np.vstack([fila0] + quietas), 'RGBA').save(os.path.join(d, 'ui_btn_convert_duct.png'))
    j = json_comun()
    j.update({'capTop': TY - SUBE, 'capTopUnavailable': TY, 'plateCell': PZ, 'plateBorder': BORDE,
              'plateCenterMode': 'tiled', 'plateColumns': list(ESTADOS),
              'ductY': DUCTO_Y, 'ductX0': X0, 'ductX1': X1, 'ductTile': 8, 'flowFrames': FRAMES, 'fps': 12,
              'ductRows': ['flow', 'full', 'hover', 'flash', 'missingDots', 'sealed'],
              'note': 'conducto de ductX0 a ductX1 en las filas ductY y ductY+1; se puede pagar: flow, que '
                      'corre; encima: hover; pulsado: flash y 2 px abajo; falta: missingDots por todo el largo '
                      'y full encima en round(largo * Energia / coste); bloqueado: sealed entero; nodisp: sin '
                      'conducto y el texto en capTopUnavailable'})
    with open(os.path.join(d, 'ui_btn_convert.json'), 'w', encoding='utf-8') as fh:
        json.dump(j, fh, indent=1, ensure_ascii=False)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'str_e', 'neutral', 'plasma_p') | {tuple(PANEL), tuple(FONDO)})
