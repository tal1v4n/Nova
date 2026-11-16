"""NOVA - ART-UI-CHARGE v2, la barra de carga del Pulsar: «Los cartuchos».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 7.1, 7.2, 7.5, 13.12 · 14.4

Lo comun -el sitio, los estados, las comprobaciones y las vistas- vive en
`gen_charge_v1.py`.

LA TESIS DE LA v2: LA BARRA MIDE LO QUE CUESTA. Un cartucho por cada 5 de
Energia del coste: 4 para Cero Absoluto (20), 7 para Decaimiento (35) y 12
para Agujero de Gusano (60). La barra del Gusano mide 47 px y la de Cero 15:
el LARGO dice que poder viene antes que el color, y el poder mas caro es el
que mas se ve desde la otra punta del tablero. Cada cartucho tiene 5 filas y
CADA FILA ES 1 DE ENERGIA INVERTIDA, de abajo arriba: la barra cuenta en
pixeles exactamente lo que el numero de arriba va perdiendo (7.1).

    el cartucho  3 x 6: la tapa arriba, en el bando (`str_p[4]`, `str_e[4]`),
                 y 5 filas de carga; lo vacio en la sombra del bando
                 (indice 6), lo cargado en el color del poder (indice 2) y
                 la fila que acaba de entrar en blanco (indice 0): el frente
    PAUSADA      lo cargado se apaga dos bandas (indice 4) y la fila que
                 espera parpadea en gris, 8 frames encendida y 8 apagada
    LISTA        las tapas toman el color del poder (indice 1) y un
                 fogonazo recorre los cartuchos de izquierda a derecha cada
                 2.67 s, 2 frames por cartucho: el patron «Sonar» de 13.12

Los cartuchos van cada 4 px (3 de cartucho y 1 de aire) y la barra se
centra bajo el nodo.

PIEZAS
    ui_charge_cells.png   filas: bando x poder (player_zero, player_decay,
                          player_wormhole, enemy_...); columnas: las 20
                          celdas de 3 x 6 que dice `ui_charge.json` (cells)
    ui_charge.json        paso, celdas y reglas de montaje
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_charge_v1 as base
from gen_charge_v1 import (ALTO_MAX, FACCIONES, FONDO, FRAMES, PODERES, RAMPA_FAC, colores_de,
                           coste, rampa_poder, rgba)
from nova_core import rampa

VERSION = 2
NOMBRE = 'Los cartuchos'

CW = 3
PASO = 4
FILAS = 5                        # una por cada 1 de Energia
POR_CARTUCHO = 5
BARRIDO = 2                      # frames del fogonazo por cartucho
PARPADEO = 16                    # frames del ciclo de espera: 8 y 8
GRIS = rampa('neutral')[1]

CELDAS = (['empty'] + [f'front{k}' for k in range(1, 6)] + ['full'] +
          [f'dim{k}' for k in range(0, 5)] + [f'wait{k}' for k in range(0, 5)] +
          ['dimfull', 'readyfull', 'readyflash'])


def celda(fac, p, nombre):
    F = rampa(RAMPA_FAC[fac])
    R = rampa_poder(p)
    img = rgba(ALTO_MAX, CW)
    tapa = R[1] if nombre.startswith('ready') else F[4]
    img[0, :, :3] = tapa
    lit, base_c, front, espera = 0, R[2], False, False
    if nombre.startswith('front'):
        lit, front = int(nombre[5:]), True
    elif nombre in ('full', 'readyfull'):
        lit = FILAS
    elif nombre == 'readyflash':
        lit, base_c = FILAS, R[0]
    elif nombre == 'dimfull':
        lit, base_c = FILAS, R[4]
    elif nombre.startswith('dim'):
        lit, base_c = int(nombre[3:]), R[4]
    elif nombre.startswith('wait'):
        lit, base_c, espera = int(nombre[4:]), R[4], True
    for i in range(FILAS):                     # i = 0 la fila de abajo
        y = ALTO_MAX - 1 - i
        if i < lit:
            img[y, :, :3] = R[0] if (front and i == lit - 1) else base_c
        elif espera and i == lit:
            img[y, :, :3] = GRIS
        else:
            img[y, :, :3] = F[6]
    img[..., 3] = 255
    return img


HOJA = {(fac, p): {n: celda(fac, p, n) for n in CELDAS} for fac in FACCIONES for p in PODERES}


def barra(p, inv, fac, pausada=False, f=0):
    n = coste(p) // POR_CARTUCHO
    cel = HOJA[(fac, p)]
    lista = inv >= coste(p)
    img = rgba(ALTO_MAX, n * PASO - 1)
    llenos, resto = divmod(inv, POR_CARTUCHO)
    for i in range(n):
        if lista:
            on = (f - i * BARRIDO) % FRAMES < BARRIDO
            c = cel['readyflash' if on else 'readyfull']
        elif pausada:
            if i < llenos:
                c = cel['dimfull']
            elif i == llenos:
                c = cel[f'wait{resto}' if f % PARPADEO < PARPADEO // 2 else f'dim{resto}']
            else:
                c = cel['empty']
        else:
            if i < llenos - (1 if resto == 0 and llenos else 0):
                c = cel['full']
            elif resto == 0 and llenos and i == llenos - 1:
                c = cel['front5']
            elif i == llenos and resto:
                c = cel[f'front{resto}']
            else:
                c = cel['empty']
        img[:, i * PASO:i * PASO + CW] = c
    return img


def comprobar_propio():
    fallos = []
    for p in PODERES:
        if coste(p) % POR_CARTUCHO:
            fallos.append(f'el coste de {p} no es multiplo de {POR_CARTUCHO}')
        # cada 1 de Energia invertida cambia la barra
        for inv in range(coste(p)):
            if (barra(p, inv, 'player') == barra(p, inv + 1, 'player')).all():
                fallos.append(f'{p}: {inv} y {inv + 1} son iguales')
    if FRAMES % PARPADEO:
        fallos.append('el parpadeo no cierra el bucle')
    if (coste('wormhole') // POR_CARTUCHO) * BARRIDO > FRAMES:
        fallos.append('el fogonazo no cabe en el bucle')
    return fallos


def exportar(d):
    filas = [np.hstack([HOJA[(fac, p)][n] for n in CELDAS]) for fac in FACCIONES for p in PODERES]
    Image.fromarray(np.vstack(filas), 'RGBA').save(os.path.join(d, 'ui_charge_cells.png'))
    with open(os.path.join(d, 'ui_charge.json'), 'w') as fh:
        json.dump({'cellWidth': CW, 'cellHeight': ALTO_MAX, 'pitch': PASO, 'energyPerCell': POR_CARTUCHO,
                   'rows': [f'{fac}_{p}' for fac in FACCIONES for p in PODERES], 'cells': CELDAS,
                   'frames': FRAMES, 'fps': base.FPS, 'readySweepFrames': BARRIDO,
                   'waitBlinkFrames': PARPADEO,
                   'note': 'cartuchos = coste / 5, cada 4 px, centrados bajo el nodo; llenos, resto = divmod(invertido, 5). '
                           'Cargando: los llenos en full y el ultimo con frente (front<resto>, o front5 si resto es 0); '
                           'pausada: los llenos en dimfull y el siguiente alterna wait<resto> y dim<resto> cada 8 frames; '
                           'lista: readyfull, y el cartucho i en readyflash cuando (f - 2 i) % 32 < 2'}, fh, indent=1)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'str_e', 'hielo', 'decaimiento', 'gusano') | {tuple(GRIS)})
