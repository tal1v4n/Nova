"""NOVA - ART-UI-HUD-ENERGY v3, el numero de Energia: «El filamento».

Ref: NOVA_Assets_Diseno.md A.6 · 14.7 (capa 6) · 14.9 (filamento de plasma)
· Spec 13.12 · 18 #48

Lo comun -el esqueleto de las cifras, las comprobaciones y las vistas- vive
en `gen_energy_v1.py`.

LA TESIS DE LA v3: EL NUMERO ES DE QUIEN ES. Las cifras no se pintan: se
trazan con el plasma del bando, el mismo de la linea de envio y del proton
(`plasma_p`, `plasma_e`), en su ancla. Y la energia corre: un pulso blanco
recorre el trazo de cada cifra como la corriente por un filamento -el
patron «Flujo» de 13.12-, con una cola de dos tonos. El pulso sigue el
trazo en el orden en que se escribiria la cifra, y cada cifra lleva el suyo
desfasado 11 frames, asi que la corriente salta de una a otra. El numero
de un nodo propio se ve vivo y cian desde lejos; el del Enemigo, magenta.

LO NEUTRAL NO TIENE PLASMA. Un Asteroide neutral o el Magnetar neutral no
son de nadie: su numero va quieto, en `neutral[2]`, sin pulso. Al capturar
un nodo, su numero se enciende -empieza a correr-; es la unica propuesta en
la que el cambio de dueno se lee tambien en el numero.

EL BUCLE. 32 frames a 12 fps (2.67 s), un bucle de ambiente de 13.12; todos
los numeros comparten el mismo, asi que cuentan como uno de los tres por
pantalla. El pulso avanza por la longitud del trazo de cada cifra, de modo
que en todas cierra.

PIEZAS
    ui_energy_label_player_sheet.png   filas: cifras 0..9; columnas: 32 frames;
    ui_energy_label_enemy_sheet.png    celdas de 12 x 16 (el 1 a la izquierda,
                                       8 de ancho)
    ui_energy_label_neutral.png        una fila: cifras 0..9, quietas
    ui_energy_label.json               avance, frames y desfase
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_energy_v1 as base
from gen_energy_v1 import (ALTO, AVANCE, CELDA_W, CONT, ESCALA, FONDO, ampliar, celda,
                           colores_de, componer, glifo, hoja, sobre)
from nova_core import rampa

VERSION = 3
NOMBRE = 'El filamento'

FRAMES = 32
DESFASE = 11                    # frames entre una cifra y la siguiente
PLASMA = {'player': rampa('plasma_p'), 'enemy': rampa('plasma_e')}
NEUTRAL = rampa('neutral')
CUERPO, CABEZA, COLA = 3, 0, (1, 2)


def recorrido(c):
    """Orden de los pixeles de diseno de la cifra a lo largo del trazo: un
    recorrido en profundidad desde un extremo -o desde arriba a la
    izquierda, si la cifra es un lazo-, primero por los vecinos rectos."""
    g = glifo(c)
    pix = {(y, x) for y, x in zip(*np.nonzero(g))}
    rectos = ((0, 1), (1, 0), (0, -1), (-1, 0))
    diag = ((1, 1), (1, -1), (-1, 1), (-1, -1))

    def vecinos(p):
        return [(p[0] + dy, p[1] + dx) for dy, dx in rectos + diag if (p[0] + dy, p[1] + dx) in pix]
    extremos = sorted(p for p in pix if len(vecinos(p)) == 1)
    inicio = extremos[0] if extremos else min(pix)
    orden, visto, pila = [], set(), [inicio]
    while pila:
        p = pila.pop()
        if p in visto:
            continue
        visto.add(p)
        orden.append(p)
        for q in reversed(vecinos(p)):
            if q not in visto:
                pila.append(q)
    return {p: i for i, p in enumerate(orden)}


ORDEN = {c: recorrido(c) for c in '0123456789'}


def cifra_v3(c, fac, f):
    if fac == 'neutral':
        return celda(c, lambda y, x, m: NEUTRAL[2])
    R = PLASMA[fac]
    orden = ORDEN[c]
    L = len(orden)
    cabeza = f / FRAMES * L

    def color(y, x, m):
        i = orden[((y - 1) // ESCALA, (x - 1) // ESCALA)]
        d = (cabeza - i) % L                     # cuanto lleva el pulso pasado
        if d < 1:
            return R[CABEZA]
        if d < 2:
            return R[COLA[0]]
        if d < 3.5:
            return R[COLA[1]]
        return R[CUERPO]
    return celda(c, color)


def etiqueta(texto, fac, energia, tope, f=0):
    return componer([cifra_v3(ch, fac, (f + k * DESFASE) % FRAMES) for k, ch in enumerate(texto)])


def comprobar_propio():
    fallos = []
    for c in '0123456789':
        if len(ORDEN[c]) != int(glifo(c).sum()):
            fallos.append(f'el recorrido de la cifra {c} no pasa por todo el trazo')
        fr = [cifra_v3(c, 'player', f).tobytes() for f in range(FRAMES)]
        if len(set(fr)) < FRAMES * 0.5:
            fallos.append(f'la cifra {c} repite demasiados frames')
        if cifra_v3(c, 'player', FRAMES).tobytes() != fr[0]:
            fallos.append(f'el bucle de la cifra {c} no cierra')
    return fallos


def exportar(d):
    for fac in ('player', 'enemy'):
        filas = [[cifra_v3(c, fac, f) for f in range(FRAMES)] for c in '0123456789']
        Image.fromarray(hoja(filas), 'RGBA').save(
            os.path.join(d, f'ui_energy_label_{fac}_sheet.png'))
    Image.fromarray(hoja([[cifra_v3(c, 'neutral', 0) for c in '0123456789']]), 'RGBA').save(
        os.path.join(d, 'ui_energy_label_neutral.png'))
    with open(os.path.join(d, 'ui_energy_label.json'), 'w') as fh:
        json.dump({'cellWidth': CELDA_W, 'cellHeight': ALTO, 'advance': AVANCE,
                   'frames': FRAMES, 'fps': base.FPS, 'digitPhaseOffset': DESFASE,
                   'boxHeight': base.CAJA[0],
                   'note': 'hojas del bando: fila = cifra, columna = frame; la cifra k del numero '
                           '(de izquierda a derecha) usa el frame (f + k * digitPhaseOffset) % frames; '
                           'neutral: una fila de cifras quietas; pivote abajo a la izquierda'}, fh, indent=1)


def vistas_propias(d, gif):
    filas = []
    for fac in ('player', 'enemy', 'neutral'):
        fila = []
        for f in range(0, FRAMES, 4):
            c = np.empty((ALTO + 4, 40, 3), np.uint8)
            c[:] = FONDO
            sobre(c, etiqueta('100' if fac != 'neutral' else '10', fac, 0, 100, f), 3, 2)
            fila.append(c)
        filas.append(np.hstack(fila))
    Image.fromarray(ampliar(np.vstack(filas), 6)).save(os.path.join(d, 'pulso_x6.png'))
    if gif:
        ims = []
        for f in range(FRAMES):
            c = np.empty((3 * (ALTO + 4), 44, 3), np.uint8)
            c[:] = FONDO
            for i, (t, fac) in enumerate((('48', 'player'), ('100', 'enemy'), ('10', 'neutral'))):
                sobre(c, etiqueta(t, fac, 0, 100, f), 4, 2 + i * (ALTO + 4))
            ims.append(Image.fromarray(ampliar(c, 6)))
        ims[0].save(os.path.join(d, 'pulso_x6.gif'), save_all=True,
                    append_images=ims[1:], duration=1000 // base.FPS, loop=0)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('plasma_p', 'plasma_e', 'neutral') | {tuple(CONT)})
