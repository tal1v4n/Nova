"""NOVA - ART-FX-IGN v1, el encendido: «La cerilla».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.2 · NOVA_Estados_Animaciones.md 14.5 ·
NOVA_Nodos.md 4.1

El momento en que un Asteroide propio se enciende: `NodeIgnition` consume 10
de Energia y en ese tick el nodo pasa a Estrella, Pulsar o Magnetar de nivel
1. 8 frames, 0.6 s, sobre el nodo recien nacido. Una sola hoja,
`art/fx/ign/v<n>/fx_ign_sheet.png`, en celdas de 96:

    filas      4   player_24, player_28, enemy_24, enemy_28
    columnas   8   los frames

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - ES LA BRASA QUE PRENDE. Hasta este tick el Asteroide llevaba encima
    «El pedernal» (`ART-ST-READY`, A.3.2): una brasa con cinco trazos que
    crepitan hacia arriba, «enciendelo ya». El encendido la continua: sale
    de su sitio, sobre la roca, y en sus rampas, `str_p` y `str_e`, las de
    los colores literales de faccion. Indices 1-5: el blanco es de `FX-CAP`.
  - DOS TALLAS. Lo que nace es un nodo de nivel 1: Estrella (39 px de
    huella, talla 24), Pulsar (44, talla 28) o Magnetar (41, talla 28), con
    la `talla_de` de «La orbita».
  - TAPA EL RELEVO. El sprite pasa de roca a nodo en el frame 0, y el efecto
    puede cruzar el cuerpo para que el cambio no se vea como un corte. Lo
    que no pisa nunca es el numero: su caja, que empieza 3 px por encima
    del borde de la talla (`franja`).
  - SE ENCIENDE Y SE APAGA. Crece hasta su pico y acaba en poco: el ultimo
    frame es el de menos luz y no pasa de la cuarta parte del pico.

LA TESIS DE LA v1: SE ENCIENDE COMO UNA CERILLA. La brasa cae sobre el nodo,
prende en la cabeza y la llama abraza el cuerpo antes de asentarse.

  - CAE (frames 0-2). La brasa, del tamano de la del pedernal, baja desde su sitio con
    una estela hasta tocar el nodo por arriba.
  - PRENDE (frame 3). Un fogonazo en la cabeza: una estrella de 5 px y un
    arco del borde encendido.
  - ABRAZA (frames 4-6). Dos llamas bajan por los dos lados del cuerpo,
    pegadas a el, hasta los lados del numero.
  - SE ASIENTA (frame 7). Quedan unas brasas sueltas en el borde.

    py tools/gen_fx_ign_v1.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_ign_v1.py --gif    y las animadas
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, dir_preview, rampa
import gen_select_v1 as sel
import gen_fx_shot_v1 as shot
import gen_fx_abs_v1 as abs_
from gen_fx_send_v1 import (ANG, C, CELDA, DX, DY, FRANJA, LIBRE, R, a_rgba,
                            borde, luz, tramar)
from gen_fx_dmg_v1 import punto

ASSET     = 'ign'
VERSION   = 1
NOMBRE    = 'La cerilla'

FRAMES    = 8                       # ceil(0.6 * 12)
FPS       = 12
TALLAS    = (24, 28)
FACCIONES = {'player': 'str_p', 'enemy': 'str_e'}
BRASA     = (C + 5.0, 15.0)         # el centro de la brasa de «El pedernal»
RESTO     = 0.25
ANTES     = 3                       # px por encima del borde de la talla donde empieza la franja


def franja(t):
    """La caja del numero para un efecto que cruza el cuerpo. La de
    `ART-FX-SEND` es toda la columna bajo el centro, porque aquel no entra en
    el cuerpo; el numero empieza en la media huella + 2, y la media huella
    de un nodo de nivel 1 no baja del borde de su talla menos 3 (Estrella:
    20 en la talla 24; Pulsar 22 y Magnetar 21 en la 28)."""
    from gen_fx_send_v1 import MEDIO_NUMERO
    return (DY > borde(t) - ANTES) & (np.abs(DX) <= MEDIO_NUMERO)


# --- Lo que comparten las propuestas -------------------------------------------

def comprobar(frames, talla, version):
    fallos = []
    for f, a in enumerate(frames):
        on = a > 0
        if not set(np.unique(a)) <= {0, 1, 2, 3, 4, 5}:
            fallos.append(f'f{f}: indice fuera de la rampa [1..5]')
        if (on & franja(talla)).any():
            fallos.append(f'f{f}: pisa la caja del numero')
        if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
            fallos.append(f'f{f}: toca el borde de la celda')
        if not on.any():
            fallos.append(f'f{f}: vacio')
    l = [luz(a) for a in frames]
    if l[-1] != min(l) or l[-1] > RESTO * max(l):
        fallos.append(f'no se asienta: luz {l}')
    if not any(((a == 1).any()) for a in frames):
        fallos.append('ningun frame llega al indice 1')
    if len({a.tobytes() for a in frames}) != FRAMES:
        fallos.append('frames repetidos')
    if fallos:
        raise SystemExit(f'ART-FX-IGN v{version} (talla {talla}) no pasa:\n  '
                         + '\n  '.join(fallos))


def generar(dibujar, version):
    celdas = {}
    for t in TALLAS:
        frames = [dibujar(t, f).astype(np.int8) for f in range(FRAMES)]
        comprobar(frames, t, version)
        celdas[t] = frames
    return celdas


def hoja(celdas):
    filas = []
    for ramp_n in FACCIONES.values():
        ramp = rampa(ramp_n)
        for t in TALLAS:
            filas.append(np.hstack([a_rgba(a, ramp) for a in celdas[t]]))
    return np.vstack(filas)


def exporta(destino, img):
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_ign_sheet.png'))
    with open(os.path.join(destino, 'fx_ign_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'fps': FPS, 'sizes': list(TALLAS),
                   'rows': [f'{fac}_{t}' for fac in FACCIONES for t in TALLAS],
                   'note': 'sobre el nodo recien encendido; la talla, la de world_select_tallas.json '
                           'por la huella del nodo de nivel 1 (Estrella 24, Pulsar y Magnetar 28); '
                           'pivote en el centro del nodo'}, f, indent=1)


# --- Vistas --------------------------------------------------------------------

BG, ampliar, sobre, abrir = shot.BG, shot.ampliar, shot.sobre, shot.abrir
nodo, numero, huellas = abs_.nodo, abs_.numero, abs_.huellas
NACE = (('str', 1), ('pul', 1), ('mag', 1))


def efecto(celdas, fac, pre, f):
    t = sel.talla_de(huellas()[pre, 1][2])
    return a_rgba(celdas[t][f], rampa(FACCIONES[fac]))


def brasa(fac):
    return abrir('states', 'ign', 'v2', f'state_ready_{fac}.png')


def vista_hoja(celdas):
    """x3. Por bando y tipo: el Asteroide listo, con su brasa, y los ocho
    frames sobre el nodo que nace."""
    filas = []
    for fac in FACCIONES:
        for pre, n in NACE:
            fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
            fila[:] = BG
            numero(fila, C, C, 'ast', 0)
            sobre(fila, nodo('ast', 0, fac), C, C)
            sobre(fila, brasa(fac), C, C)
            for f in range(FRAMES):
                x = (f + 1) * CELDA + C
                numero(fila, x, C, pre, n)
                sobre(fila, nodo(pre, n, fac), x, C)
                sobre(fila, efecto(celdas, fac, pre, f), x, C)
            filas.append(fila)
    return ampliar(np.vstack(filas), 3)


# Tres Asteroides que se encienden uno tras otro: dos del Jugador (Estrella y
# Pulsar) y uno del Enemigo (Magnetar).
ESCENA = (('player', 'str', (90, 80), 4), ('player', 'pul', (220, 140), 14),
          ('enemy', 'mag', (350, 80), 24))


def vista_tablero(celdas, k):
    tab = np.empty((220, 440, 3), np.uint8)
    tab[:] = BG
    for fac, pre, (x, y), k0 in ESCENA:
        if k < k0:
            numero(tab, x, y, 'ast', 0)
            sobre(tab, nodo('ast', 0, fac), x, y)
            sobre(tab, brasa(fac), x, y)
        else:
            numero(tab, x, y, pre, 1)
            sobre(tab, nodo(pre, 1, fac), x, y)
            if k - k0 < FRAMES:
                sobre(tab, efecto(celdas, fac, pre, k - k0), x, y)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_ign'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(vista_hoja(celdas)).save(os.path.join(d, 'fx_ign_hoja_x3.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(40)]
        for nombre, ms in (('fx_ign_tablero_x2.gif', 1000 / FPS),
                           ('fx_ign_tablero_x2_lento.gif', 4000 / FPS)):
            im = [Image.fromarray(ampliar(e, 2)) for e in esc]
            im[0].save(os.path.join(d, nombre), save_all=True, append_images=im[1:],
                       duration=round(ms), loop=0)
    print('vistas en', d)


def ejecutar(dibujar, version, nombre, gif):
    destino = os.path.join(RAIZ, 'art', 'fx', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    celdas = generar(dibujar, version)
    img = hoja(celdas)
    exporta(destino, img)
    print(f'ART-FX-IGN v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in TALLAS:
        print(f'  talla {t}  px {[int((a > 0).sum()) for a in celdas[t]]}  '
              f'luz {[luz(a) for a in celdas[t]]}')
    vistas(celdas, version, gif)


# --- La v1: «La cerilla» ------------------------------------------------------------

def _mota(idx, x, y, banda, grande):
    """La brasa: del tamano de la de «El pedernal» (un nucleo de 3 x 3 y su
    halo en rombo), para que se lea como la misma que cae."""
    for dx in range(-2, 3):
        for dy in range(-2, 3):
            d = abs(dx) + abs(dy)
            if d <= 1:
                punto(idx, x + dx, y + dy, banda)
            elif d == 2 and grande:
                punto(idx, x + dx, y + dy, banda + 1)


def dibujar(t, f):
    Rb = borde(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    cima = (C, C - Rb + 1.0)                    # donde toca: arriba del cuerpo
    if f <= 2:
        s = (f + 1) / 3
        x = BRASA[0] + (cima[0] - BRASA[0]) * s
        y = BRASA[1] + (cima[1] - BRASA[1]) * s
        _mota(idx, x, y, 1, True)
        # la estela, de donde venia
        for k in np.linspace(0.15, 0.6, 4):
            sx = BRASA[0] + (cima[0] - BRASA[0]) * (s - k / 3)
            sy = BRASA[1] + (cima[1] - BRASA[1]) * (s - k / 3)
            punto(idx, sx, sy, 3 if k < 0.4 else 4, solo_vacio=True)
    elif f == 3:
        x, y = cima
        for d in range(-3, 4):
            punto(idx, x + d, y, 1 if abs(d) < 2 else 2)
            punto(idx, x, y + d, 1 if abs(d) < 2 else 2)
        for d in (-2, 2):
            punto(idx, x + d, y + d, 3)
            punto(idx, x - d, y + d, 3)
        dif = np.abs(np.angle(np.exp(1j * (ANG + np.pi / 2))))
        arco = (dif < 0.6) & (R >= Rb - 0.5) & (R < Rb + 1.5) & LIBRE & (idx == 0)
        idx[arco] = 2
    elif f <= 6:
        # dos llamas que bajan por los lados: el frente avanza y la cola se apaga
        frente = 0.6 + (f - 3) * 0.75           # radianes desde arriba
        dif = np.abs(np.angle(np.exp(1j * (ANG + np.pi / 2))))
        llama = (R >= Rb - 0.5) & (R < Rb + 1.5) & LIBRE
        idx[llama & (dif <= frente) & (dif > frente - 0.35)] = 1 if f < 6 else 2
        idx[llama & (dif <= frente - 0.35) & (dif > frente - 0.9)] = 3
        dens = np.where(llama & (dif <= frente - 0.9), 0.35, 0)
        tramar(idx, dens, 4, 5, corte=0.3)
        # lenguas hacia fuera en el frente
        lengua = (R >= Rb + 1.5) & (R < Rb + 3.5) & LIBRE & (dif <= frente) & (dif > frente - 0.2)
        idx[lengua & (idx == 0)] = 2
    else:
        rng = np.random.default_rng(90 + t)
        for a in rng.uniform(-math.pi, math.pi, 7):
            r = Rb + 1.0
            punto(idx, C + r * math.cos(a), C + r * math.sin(a), 4)
    return idx


if __name__ == '__main__':
    ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
