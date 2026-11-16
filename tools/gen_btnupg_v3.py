"""NOVA - ART-UI-BTN-UPG v3, el boton de mejora: «El conducto».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.3, 13.12 (patron «Flujo», «sin
Energia»), 16.2

Lo comun -los estados, el texto, las comprobaciones y las vistas- vive en
`gen_btnupg_v1.py`.

LA TESIS DE LA v3: EL BOTON ENSENA CUANTO TIENES DEL COSTE. La tecla de
13.12 -relleno `#11142A`, marco de 2 px en escalera- lleva en su base un
CONDUCTO de 2 px que se llena de plasma del Jugador (`plasma_p`) con la
Energia del nodo sobre el coste: `min(1, Energia / coste)` del ancho. Cuando
alcanza, el conducto esta lleno y CORRE -un pulso blanco lo recorre hacia
la derecha cada 2.67 s, el patron «Flujo»-: la mejora esta lista. Cuando no
alcanza, lo que hay en cian quieto y LO QUE FALTA en puntos magenta
(13.12: magenta es lo que falta). El numero `faltan 6` y el hueco magenta
dicen lo mismo, uno en texto y otro en largo.

    encima      el marco en `str_p[4]` y el plasma sube un tono: se ofrece
    pulsado     el conducto destella en blanco -la Energia entra en el
                nodo- y la tecla baja 2 px
    bloqueado   el marco gris y el conducto en `neutral[4]`, quieto:
                congelado o reconvirtiendo, no falta nada que poner
    maximo      sin tecla: `NIVEL MAXIMO` sobre un conducto sellado en gris,
                lleno y quieto

PIEZAS
    ui_btn_upgrade_plate.png    20 x 20, nueve cortes, una columna por
                                estado: normal, encima, pulsado, falta,
                                bloqueado
    ui_btn_upgrade_duct.png     teselas de 8 x 2: fila 0, el plasma que corre
                                (32 frames); filas 1-5, quietas: lleno,
                                encima, destello, falta (puntos), sellado
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
import gen_panel_v1 as pv1
from gen_panel_v1 import FONDO, NEU, PANEL, STR_E, STR_P, colores_de, pega, placa_lamina, rgba, texto
from nova_core import rampa

VERSION = 3
NOMBRE = 'El conducto'
FRAMES = 32
PZ, BORDE = 20, 6
DUCTO_Y = 16                     # filas 16-17, justo encima del marco de abajo
X0, X1 = 3, BW - 3
PL = rampa('plasma_p')


def tecla(estado):
    marco = {'normal': STR_P[6], 'encima': STR_P[4], 'pulsado': STR_P[4],
             'falta': NEU[5], 'bloqueado': NEU[5]}[estado]
    return placa_lamina(PZ, PZ, marco)


def ducto(tipo, f=0):
    """Tesela de 8 x 2."""
    t = rgba(2, 8)
    if tipo == 'corre':
        for x in range(8):
            t[0, x, :3], t[1, x, :3] = PL[2], PL[3]
            if (x + f // 4) % 8 == 0:
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
    return np.tile(ducto(tipo, f), (1, -(-n // 8), 1))[:, :n]


def boton(estado, coste, energia, f=0, t=1.0):
    img = rgba(CELDA_H, BW)
    largo = X1 - X0
    if estado == 'maximo':
        rotula(img, 'maximo', coste, 0, centrado=True)
        pega(img, tira('sellado', largo - 40), X0 + 20, DUCTO_Y + 2)
        return img
    dy = BAJA if estado == 'pulsado' else 0
    cuerpo = pv1.nueve(tecla(estado), BW, BH, BORDE)
    frac = 1.0 if coste <= 0 else min(1.0, energia / coste)
    n = int(round(largo * frac))
    if estado == 'falta':
        pega(cuerpo, tira('falta', largo), X0, DUCTO_Y)
        cuerpo[DUCTO_Y:DUCTO_Y + 2, X0:X0 + n] = tira('lleno', n)
    elif estado == 'bloqueado':
        cuerpo[DUCTO_Y:DUCTO_Y + 2, X0:X0 + n] = tira('sellado', n)
        pega(cuerpo, tira('falta', largo - n), X0 + n, DUCTO_Y) if n < largo else None
    else:
        tipo = {'normal': 'corre', 'encima': 'encima', 'pulsado': 'destello'}[estado]
        cuerpo[DUCTO_Y:DUCTO_Y + 2, X0:X1] = tira(tipo, largo, f)
    pega(img, cuerpo, 0, dy)
    falta = max(0, coste - energia)
    rotula(img, estado, coste, falta, dy - 2,
           color_izq=STR_P[0] if estado in ('encima', 'pulsado') else None)
    return img


def piezas():
    d = {f'tecla_{e}': tecla(e) for e in ('normal', 'encima', 'pulsado', 'falta', 'bloqueado')}
    for tp in ('lleno', 'encima', 'destello', 'falta', 'sellado'):
        d[f'ducto_{tp}'] = ducto(tp)
    for f in range(FRAMES):
        d[f'corre_{f}'] = ducto('corre', f)
    return d


def comprobar_propio():
    fallos = []
    if not (ducto('corre', FRAMES) == ducto('corre', 0)).all():
        fallos.append('el plasma no cierra el bucle')
    # el texto sube 2 px para dejar sitio al conducto: no lo pisa
    if TY - 2 + 10 + 3 > DUCTO_Y:
        fallos.append('el texto pisa el conducto')
    # el hueco magenta crece cuando falta mas
    a = (base_boton('falta', 40, 30)[..., :3] == STR_E[4]).all(-1).sum()
    b = (base_boton('falta', 40, 10)[..., :3] == STR_E[4]).all(-1).sum()
    if not b > a:
        fallos.append('el hueco de lo que falta no crece')
    return fallos


def base_boton(e, c, en):
    return boton(e, c, en)


def exportar(d):
    p = piezas()
    Image.fromarray(np.hstack([p[f'tecla_{e}'] for e in ('normal', 'encima', 'pulsado', 'falta', 'bloqueado')]),
                    'RGBA').save(os.path.join(d, 'ui_btn_upgrade_plate.png'))
    fila0 = np.hstack([p[f'corre_{f}'] for f in range(FRAMES)])
    quietas = [np.hstack([p[f'ducto_{tp}']] + [rgba(2, 8)] * (FRAMES - 1))
               for tp in ('lleno', 'encima', 'destello', 'falta', 'sellado')]
    Image.fromarray(np.vstack([fila0] + quietas), 'RGBA').save(os.path.join(d, 'ui_btn_upgrade_duct.png'))
    with open(os.path.join(d, 'ui_btn_upgrade.json'), 'w') as fh:
        json.dump({'width': BW, 'height': BH, 'cellHeight': CELDA_H, 'pressedDrop': BAJA, 'capTop': TY - 2,
                   'padX': 6, 'plateCell': PZ, 'plateBorder': BORDE, 'plateCenterMode': 'tiled',
                   'plateColumns': ['normal', 'hover', 'pressed', 'missing', 'blocked'],
                   'ductY': DUCTO_Y, 'ductX0': X0, 'ductX1': X1, 'ductTile': 8, 'flowFrames': FRAMES,
                   'ductRows': ['flow', 'full', 'hover', 'flash', 'missingDots', 'sealed'],
                   'note': 'conducto de ductX0 a ductX1 en las filas ductY y ductY+1; listo: la fila flow, que corre; '
                           'encima: hover; pulsado: flash y 2 px abajo; falta: missingDots por todo el largo y full '
                           'encima en round(largo * Energia / coste); bloqueado: sealed en esa fraccion y missingDots '
                           'en el resto; maximo: sin placa, el texto centrado y sealed de x 23 a 169 debajo'},
                  fh, indent=1)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'str_e', 'neutral', 'plasma_p') | {tuple(PANEL), tuple(FONDO)})
