"""NOVA - ART-FX-SEND v1, el lanzamiento sobre el nodo origen: «La onda».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5 y 14.7 ·
Spec 5.2 y 13.1

Lo que hace el nodo que envia en el instante en que `ProtonLauncher`
descuenta su Energia: 3 frames, 0.2 s. Una sola hoja,
`art/fx/send/v<n>/fx_send_sheet.png`, en celdas de 96:

    filas     10   player_24 ... player_40, enemy_24 ... enemy_40
    columnas   3   los frames

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - QUIEN ENVIA. Estrella, Pulsar y Magnetar del Jugador o del Enemigo
    (5.2): el Asteroide nunca envia y los neutrales no juegan. Dos bandos,
    en el color del plasma que sale: `plasma_p` y `plasma_e`, los de la
    linea de envio y el proton. Indices 1-5: el blanco es de `FX-CAP`.
  - LAS TALLAS DE «LA ORBITA». El nodo mide de 39 a 72 px de huella. Las
    cinco tallas del anillo de seleccion (24-40 px de radio libre: la media
    huella mas 4) con su `talla_de`: el nodo que estaba seleccionado suelta
    el efecto a la misma distancia a la que tenia el anillo. El efecto vive
    fuera del cuerpo del nodo: no tapa el sprite que lo esta enviando.
  - EL NUMERO NO SE TAPA. La Energia va justo debajo del nodo (18 #48), y es
    lo que acaba de bajar: lo que el jugador mira. El efecto no entra en la
    franja de debajo del centro de |x| <= 19 px, la misma que deja libre el
    anillo de seleccion. Por eso ninguna propuesta apunta al destino: un
    fogonazo hacia abajo caeria entero sobre el numero.
  - VARIOS A LA VEZ. Con un lazo salen hasta seis envios en el mismo tick:
    tiene que ser corto y ligero, y apagarse frame a frame.

LA TESIS DE LA v1: EL ENVIO ES UNA ONDA. Como la piedra en el agua: un aro
sale del borde del nodo y se aleja, y en tres frames ya no esta.

  - GOLPE (frame 0). Un aro solido de 1 px pegado al cuerpo, en el indice 1,
    con un velo de trama por dentro.
  - ONDA (frames 1-2). Se aleja 3 px por frame, una banda y luego dos mas
    apagado, y se va rompiendo en tramos.

    py tools/gen_fx_send_v1.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_send_v1.py --gif    y las animadas
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import BAYER8, RAIZ, dir_preview, rampa
import gen_select_v1 as sel
import gen_fx_shot_v1 as shot

ASSET     = 'send'
VERSION   = 1
NOMBRE    = 'La onda'

CELDA     = 96
C         = CELDA / 2
FRAMES    = 3                       # ceil(0.2 * 12)
FPS       = 12
TALLAS    = sel.TALLAS              # radio libre del anillo de seleccion
AIRE      = sel.AIRE
FACCIONES = shot.FACCIONES
MEDIO_NUMERO = sel.NUMERO_MEDIO_ANCHO

BAYER = np.tile(BAYER8, (CELDA // 8, CELDA // 8))
_y, _x = np.mgrid[0:CELDA, 0:CELDA]
DX = _x + 0.5 - C
DY = _y + 0.5 - C
R = np.hypot(DX, DY)
ANG = np.arctan2(DY, DX)
FRANJA = (DY > 0) & (np.abs(DX) <= MEDIO_NUMERO)
LIBRE = ~FRANJA


# --- Lo que comparten las propuestas -------------------------------------------

def borde(talla):
    """El borde del cuerpo para esa talla: el radio libre menos el aire."""
    return talla - AIRE


def tramar(idx, dens, b_in, b_out, corte=0.55):
    on = (dens > BAYER) & (idx == 0)
    idx[on] = np.where(dens[on] > corte, b_in, b_out)
    return on


def a_rgba(idx, ramp):
    img = np.zeros((CELDA, CELDA, 4), np.uint8)
    on = idx > 0
    img[on, :3] = ramp[idx[on]]
    img[on, 3] = 255
    return img


def luz(idx):
    on = idx > 0
    return int((6 - idx[on]).sum())


def comprobar(frames, talla, version):
    fallos = []
    Rb = borde(talla)
    for f, a in enumerate(frames):
        on = a > 0
        if not set(np.unique(a)) <= {0, 1, 2, 3, 4, 5}:
            fallos.append(f'f{f}: indice fuera de la rampa [1..5]')
        if (on & FRANJA).any():
            fallos.append(f'f{f}: pisa la franja del numero')
        if (on & (R < Rb - 1)).any():
            fallos.append(f'f{f}: entra en el cuerpo del nodo')
        if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
            fallos.append(f'f{f}: toca el borde de la celda')
        if not on.any():
            fallos.append(f'f{f}: vacio')
    if not (frames[0] == 1).any():
        fallos.append('el frame 0 no llega al indice 1')
    l = [luz(a) for a in frames]
    if not all(x > y for x, y in zip(l, l[1:])):
        fallos.append(f'no se apaga: luz {l}')
    if len({a.tobytes() for a in frames}) != FRAMES:
        fallos.append('frames repetidos')
    if fallos:
        raise SystemExit(f'ART-FX-SEND v{version} (talla {talla}) no pasa:\n  '
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
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_send_sheet.png'))
    with open(os.path.join(destino, 'fx_send_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'fps': FPS,
                   'sizes': list(TALLAS), 'air': AIRE,
                   'rows': [f'{fac}_{t}' for fac in FACCIONES for t in TALLAS],
                   'note': 'la talla es la de world_select_tallas.json (talla_de por la huella '
                           'del nodo); pivote en el centro del nodo origen'}, f, indent=1)


# --- Vistas --------------------------------------------------------------------

BG, ampliar, sobre, abrir = shot.BG, shot.ampliar, shot.sobre, shot.abrir


def huellas():
    out = {}
    for carpeta, version, pre, niveles in sel.NODOS:
        with open(os.path.join(RAIZ, 'art', 'nodes', carpeta, f'v{version}',
                               f'{pre}_huella.json')) as f:
            h = json.load(f)
        for n, px in zip(h['levels'], h['footprintPixels']):
            out[pre, n] = (carpeta, version, px)
    return out


def nodo(pre, n, fac):
    carpeta, version, _ = huellas()[pre, n]
    return abrir('nodes', carpeta, f'v{version}', f'{pre}_lvl{n}_{fac}.png')


def efecto(celdas, fac, pre, n, f):
    t = sel.talla_de(huellas()[pre, n][2])
    return a_rgba(celdas[t][f], rampa(FACCIONES[fac]))


def numero(tab, x, y, pre, n):
    """Un rectangulo gris donde va el numero de Energia, solo para ver que
    el efecto lo deja libre."""
    hf = (huellas()[pre, n][2] + 1) // 2
    y0 = int(y) + hf + 2
    tab[y0:y0 + 20, int(x) - 11:int(x) + 11] = (60, 66, 90)


MUESTRA = (('str', 1), ('pul', 1), ('str', 3), ('mag', 4), ('str', 5))


def vista_hoja(celdas):
    """x3. Un nodo de cada talla, por bando: el nodo solo y los tres frames,
    con el sitio del numero marcado en gris."""
    filas = []
    for fac in FACCIONES:
        for pre, n in MUESTRA:
            fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
            fila[:] = BG
            for i in range(FRAMES + 1):
                numero(fila, i * CELDA + C, C, pre, n)
                sobre(fila, nodo(pre, n, fac), i * CELDA + C, C)
                if i:
                    sobre(fila, efecto(celdas, fac, pre, n, i - 1), i * CELDA + C, C)
            filas.append(fila)
    return ampliar(np.vstack(filas), 3)


# Un lazo de tres nodos del Jugador que envian a la vez a una Estrella
# enemiga, y la respuesta del Enemigo un poco despues.
ORIGENES = (('player', 'str', 4, (70, 70), 0), ('player', 'pul', 2, (60, 170), 0),
            ('player', 'mag', 3, (150, 120), 0), ('enemy', 'str', 2, (400, 70), 6))
DESTINOS = {'player': (400, 70), 'enemy': (150, 120)}
CARGA = {'player': 24, 'enemy': 12}


def vista_tablero(celdas, k):
    tab = np.empty((230, 470, 3), np.uint8)
    tab[:] = BG
    for fac, pre, n, (x, y), k0 in ORIGENES:
        numero(tab, x, y, pre, n)
        sobre(tab, nodo(pre, n, fac), x, y)
    sobre(tab, nodo('str', 3, 'enemy'), 400, 70)
    for fac, pre, n, (x, y), k0 in ORIGENES:
        kk = k - k0
        if 0 <= kk < FRAMES:
            sobre(tab, efecto(celdas, fac, pre, n, kk), x, y)
        if kk >= 0:
            dx, dy = DESTINOS[fac][0] - x, DESTINOS[fac][1] - y
            largo = math.hypot(dx, dy)
            s = kk * shot.PASO
            if 0 < s < largo - 30:
                sobre(tab, shot.proton(fac, CARGA[fac], kk), x + dx * s / largo,
                      y + dy * s / largo)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_send'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(vista_hoja(celdas)).save(os.path.join(d, 'fx_send_hoja_x3.png'))
    Image.fromarray(ampliar(vista_tablero(celdas, 1), 2)).save(
        os.path.join(d, 'fx_send_tablero_x2.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(-4, 24)]
        for nombre, ms in (('fx_send_tablero_x2.gif', 1000 / FPS),
                           ('fx_send_tablero_x2_lento.gif', 4000 / FPS)):
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
    print(f'ART-FX-SEND v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in TALLAS:
        print(f'  talla {t}  px {[int((a > 0).sum()) for a in celdas[t]]}  '
              f'luz {[luz(a) for a in celdas[t]]}')
    vistas(celdas, version, gif)


# --- La v1: «La onda» ---------------------------------------------------------------

PASO = 3.0       # cuanto se aleja el aro por frame


def _rotura(f):
    """Por donde se rompe el aro: una onda angular determinista."""
    return (np.sin(5 * ANG + 1.3) + 0.6 * np.sin(9 * ANG + 0.4)) / 1.6 + 0.35 * (2 - f)


def dibujar(t, f):
    Rb = borde(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    r = Rb + 1.5 + PASO * f
    aro = (R >= r - 0.5) & (R < r + 0.5) & LIBRE
    if f == 0:
        idx[aro] = 1
        d = r - 0.5 - R
        dens = np.where((d > 0) & (R >= Rb - 1), np.clip(1 - d / 2.0, 0, 1) * 0.6, 0)
        tramar(idx, dens * LIBRE, 3, 4, corte=0.4)
    else:
        idx[aro & (_rotura(f) > -0.1 * f)] = 1 + f
        d = r - 0.5 - R
        dens = np.where((d > 0) & (d < PASO), (1 - d / PASO) * 0.35 / f, 0)
        tramar(idx, dens * LIBRE, 4, 5, corte=0.3)
    return idx


if __name__ == '__main__':
    ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
