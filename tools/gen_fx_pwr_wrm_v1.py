"""NOVA - ART-FX-PWR-WRM v1, el impacto de Agujero de Gusano: «La rasgadura».

Ref: NOVA_Assets_Diseno.md A.5, A.3.7 y A.4.2 · NOVA_Estados_Animaciones.md
14.5 · Spec 7.5

El momento en que el haz de Agujero de Gusano (`ART-WLD-BEAM-WRM`, «El
desgarro») termina sobre su objetivo y el nodo cambia de bando entero. 12
frames, 1.0 s. Una sola hoja, `art/fx/pwr_wrm/v<n>/fx_pwr_wrm_sheet.png`, en
celdas de 96:

    filas      5   talla 24, 28, 32, 36, 40 (la de «La orbita» del nodo)
    columnas  12   los frames

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - ACOMPANA A LA CAPTURA, NO LA ESCONDE. El robo es instantaneo: en el
    tick en que el haz llega, `PowerBeamSystem` llama a `Steal`, el nodo
    cambia de bando (`ChangeFaction`) y en ese mismo frame se recolorea y
    arranca «El relevo» (`ART-ST-CAP`, 0.4 s, los dos anillos blancos). Lo
    que se ve durante todo el efecto es el nodo con su paleta nueva, que es
    la informacion (A.3.7). Este efecto suena a la vez: los frames 0-3 van
    debajo de los anillos y no compiten con ellos -nada de blanco, que es
    de FX-CAP-, y los frames 4-11 son el eco del atajo cuando los anillos
    ya se han ido. Las vistas lo pintan asi, con «El relevo» encima.
  - EN LA RAMPA DEL GUSANO, SIN BANDO. `gusano`, la del haz, indices 1-5;
    el 0 es el blanco de FX-CAP. El morado puro es del Agujero de Gusano y
    de nada mas (contrato, *Paleta*). El bando nuevo ya lo dice el sprite.
  - EL NUMERO, LIBRE, con la caja del numero de su talla (la `franja` de
    `ART-FX-THAW`): una fila por talla. La Energia se conserva entera
    (7.5), y el jugador mira el numero para verlo.
  - SIN DIRECCION. El haz llega desde cualquier angulo y la hoja no gira.
  - SALE A NADA. Algun frame llega al indice 1, y el ultimo es el de menos
    luz y no pasa de la cuarta parte del pico: despues no viene ningun
    overlay, viene `Normal`.

LA TESIS DE LA v1: EL DESGARRO SE QUEDA ABIERTO UN MOMENTO. «El desgarro» es
el espacio rasgado entre el Pulsar y el objetivo, con el otro cielo dentro.
Al llegar, la rasgadura sigue sobre el nodo: un tajo que se abre en una
garganta con labios claros y el centro oscuro, con las estrellas del otro
lado, y que despues se cose y se cierra. Por ahi ha pasado el nodo.

  - EL TAJO (frame 0). Una ranura de 1 px en el indice 1, con chispas.
  - LA GARGANTA (frames 1-4). Se abre a 5 px por lado: labios claros,
    centro en `gusano[5]` y el otro cielo dentro.
  - LA SUTURA (frames 5-8). Se cierra con puntadas que cruzan el tajo.
  - LA CICATRIZ (frames 9-11). Una costura que se apaga y unas estrellas
    que se escaparon por el tajo y se alejan.

El tajo va siempre a -55 grados, el mismo en las cinco tallas: la hoja no
gira y una diagonal no se confunde con el eje del Pulsar (-32) ni con nada
horizontal.

    py tools/gen_fx_pwr_wrm_v1.py                  hoja, comprobaciones y vistas fijas
    py tools/gen_fx_pwr_wrm_v1.py --gif            y las animadas
    py tools/gen_fx_pwr_wrm_v1.py --comparativa    las tres propuestas lado a lado
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
from gen_fx_send_v1 import ANG, BAYER, DX, DY, R, borde
from gen_fx_thaw_v1 import (BG, C, CELDA, Talla, a_rgba, ampliar, huellas, nodo, numero,
                            sobre, luz, abrir)

ASSET     = 'pwr_wrm'
VERSION   = 1
NOMBRE    = 'La rasgadura'

FRAMES    = 12                      # ceil(1.0 * 12)
FPS       = 12
TALLAS    = sel.TALLAS
RAMPA     = 'gusano'
RESTO     = 0.25
DENTRO    = R < 43.5                # nada llega al borde de la celda

RUIDO = np.random.default_rng(83).random((CELDA, CELDA))


# --- Lo que comparten las propuestas -------------------------------------------

def pinta(idx, m, banda, tt):
    """Enciende m en `banda` salvo donde ya hay algo mas claro."""
    m = m & DENTRO & tt.libre
    idx[m & ((idx == 0) | (idx > banda))] = banda


def punto(idx, tt, x, y, banda):
    ix, iy = int(math.floor(x)), int(math.floor(y))
    if 0 <= ix < CELDA and 0 <= iy < CELDA and tt.libre[iy, ix] and DENTRO[iy, ix]:
        if idx[iy, ix] == 0 or idx[iy, ix] > banda:
            idx[iy, ix] = banda


def comprobar(frames, t, version):
    fallos = []
    tt = Talla(t)
    for f, a in enumerate(frames):
        on = a > 0
        if not set(np.unique(a)) <= {0, 1, 2, 3, 4, 5}:
            fallos.append(f'f{f}: indice fuera de la rampa [1..5]')
        if (on & tt.franja).any():
            fallos.append(f'f{f}: pisa la caja del numero')
        if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
            fallos.append(f'f{f}: toca el borde de la celda')
        if not on.any():
            fallos.append(f'f{f}: vacio')
    if not any((a == 1).any() for a in frames):
        fallos.append('ningun frame llega al indice 1')
    l = [luz(a) for a in frames]
    if l[-1] != min(l) or l[-1] > RESTO * max(l):
        fallos.append(f'no se apaga: luz {l}')
    if len({a.tobytes() for a in frames}) != FRAMES:
        fallos.append('frames repetidos')
    if fallos:
        raise SystemExit(f'ART-FX-PWR-WRM v{version} (talla {t}) no pasa:\n  '
                         + '\n  '.join(fallos))


def generar(dibujar, version):
    celdas = {}
    for t in TALLAS:
        tt = Talla(t)
        frames = [dibujar(tt, f).astype(np.int8) for f in range(FRAMES)]
        comprobar(frames, t, version)
        celdas[t] = frames
    return celdas


def hoja(celdas):
    ramp = rampa(RAMPA)
    return np.vstack([np.hstack([a_rgba(a, ramp) for a in celdas[t]]) for t in TALLAS])


def exporta(destino, img):
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_pwr_wrm_sheet.png'))
    with open(os.path.join(destino, 'fx_pwr_wrm_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'fps': FPS, 'sizes': list(TALLAS),
                   'rows': [f'pwr_wrm_{t}' for t in TALLAS],
                   'note': 'sobre el objetivo del haz de Agujero de Gusano, al terminar el haz, '
                           'en el mismo tick de ChangeFaction: el nodo ya lleva su paleta nueva y '
                           'state_captured_sheet suena a la vez, por encima de este efecto; la '
                           'talla, la de world_select_tallas.json por la huella del nodo; una sola '
                           'rampa (gusano), sin bando'},
                  f, indent=1)


# --- Vistas --------------------------------------------------------------------

_CAP = []


def captura(f):
    """El frame f de «El relevo» (`ART-ST-CAP` v2), o None si ya acabo."""
    if not _CAP:
        hoja_cap = abrir('states', 'cap', 'v2', 'state_captured_sheet.png')
        _CAP.extend(hoja_cap[:, k * CELDA:(k + 1) * CELDA] for k in range(hoja_cap.shape[1] // CELDA))
    return _CAP[f] if f < len(_CAP) else None


# (bando de antes, bando nuevo, tipo, nivel)
MUESTRA = (('enemy', 'player', 'str', 1), ('player', 'enemy', 'pul', 1),
           ('neutral', 'player', 'mag', 2), ('player', 'enemy', 'str', 3),
           ('enemy', 'player', 'pul', 3), ('neutral', 'enemy', 'mag', 5))


def efecto(celdas, pre, n, f):
    t = sel.talla_de(huellas()[pre, n][2])
    return a_rgba(celdas[t][f], rampa(RAMPA))


def capa(lienzo, celdas, pre, n, nuevo, f, x, y):
    sobre(lienzo, nodo(pre, n, nuevo), x, y)
    sobre(lienzo, efecto(celdas, pre, n, f), x, y)
    c = captura(f)
    if c is not None:
        sobre(lienzo, c, x, y)


def vista_hoja(celdas, muestra=MUESTRA):
    """Por nodo: antes del robo y los doce frames, con el nodo ya en su bando
    nuevo y «El relevo» encima, como en partida."""
    filas = []
    for antes, nuevo, pre, n in muestra:
        fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
        fila[:] = BG
        numero(fila, C, C, pre, n)
        sobre(fila, nodo(pre, n, antes), C, C)
        for f in range(FRAMES):
            x = (f + 1) * CELDA + C
            numero(fila, x, C, pre, n)
            capa(fila, celdas, pre, n, nuevo, f, x, C)
        filas.append(fila)
    return np.vstack(filas)


# El Jugador roba tres nodos uno tras otro.
ESCENA = (('enemy', 'player', 'str', 2, (90, 95), 3),
          ('neutral', 'player', 'mag', 3, (220, 115), 12),
          ('enemy', 'player', 'pul', 2, (350, 95), 21))


def vista_tablero(celdas, k):
    tab = np.empty((220, 440, 3), np.uint8)
    tab[:] = BG
    for antes, nuevo, pre, n, (x, y), k0 in ESCENA:
        numero(tab, x, y, pre, n)
        if k < k0:
            sobre(tab, nodo(pre, n, antes), x, y)
        elif k - k0 < FRAMES:
            capa(tab, celdas, pre, n, nuevo, k - k0, x, y)
        else:
            sobre(tab, nodo(pre, n, nuevo), x, y)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_pwr_wrm'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(vista_hoja(celdas), 3)).save(os.path.join(d, 'fx_pwr_wrm_hoja_x3.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(40)]
        for nombre, ms in (('fx_pwr_wrm_tablero_x2.gif', 1000 / FPS),
                           ('fx_pwr_wrm_tablero_x2_lento.gif', 4000 / FPS)):
            im = [Image.fromarray(ampliar(e, 2)) for e in esc]
            im[0].save(os.path.join(d, nombre), save_all=True, append_images=im[1:],
                       duration=round(ms), loop=0)
    print('vistas en', d)


def comparativa():
    import importlib
    muestra = (('enemy', 'player', 'str', 1), ('neutral', 'player', 'mag', 2),
               ('player', 'enemy', 'str', 4))
    bloques = []
    for v in (1, 2, 3):
        mod = importlib.import_module(f'gen_fx_pwr_wrm_v{v}')
        bloques.append(vista_hoja(generar(mod.dibujar, v), muestra))
        sep = np.empty((6, CELDA * (FRAMES + 1), 3), np.uint8)
        sep[:] = (60, 66, 90)
        bloques.append(sep)
    ruta = os.path.join(dir_preview('fx_pwr_wrm'), 'comparativa_x3.png')
    Image.fromarray(ampliar(np.vstack(bloques[:-1]), 3)).save(ruta)
    print('comparativa en', ruta)


def ejecutar(dibujar, version, nombre, gif):
    destino = os.path.join(RAIZ, 'art', 'fx', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    celdas = generar(dibujar, version)
    img = hoja(celdas)
    exporta(destino, img)
    print(f'ART-FX-PWR-WRM v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in TALLAS:
        print(f'  talla {t}  px {[int((a > 0).sum()) for a in celdas[t]]}  '
              f'luz {[luz(a) for a in celdas[t]]}')
    vistas(celdas, version, gif)


# --- La v1: «La rasgadura» -----------------------------------------------------

EJE = math.radians(-55)
_ca, _sa = math.cos(EJE), math.sin(EJE)
U = DX * _ca + DY * _sa                 # a lo largo del tajo
V = -DX * _sa + DY * _ca                # a traves

# medio ancho de la garganta, labios y estado de las puntadas por frame
ABRE   = (0.0, 1.6, 3.2, 4.4, 5.0, 4.0, 2.8, 1.6, 0.6, 0.0, 0.0, 0.0)
LABIO  = (1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 5)


def _mellas():
    """El borde rasgado: un salto de 0 a 1 px por tramo de 2 a 5 px a lo
    largo del tajo, distinto a cada lado."""
    rng = np.random.default_rng(41)
    out = []
    for _ in range(2):
        m = np.zeros(120)
        i = 0
        while i < 120:
            n = rng.integers(2, 6)
            m[i:i + n] = rng.choice((0.0, 0.0, 1.0))
            i += n
        out.append(m)
    return out


MELLAS = _mellas()
_iu = np.clip(np.round(U + 60).astype(int), 0, 119)
MELLA = np.where(V >= 0, MELLAS[0][_iu], MELLAS[1][_iu])


def _escapan():
    rng = np.random.default_rng(13)
    return [(rng.uniform(-0.8, 0.8), rng.choice((-1, 1)), rng.uniform(0.7, 1.3)) for _ in range(9)]


ESCAPAN = _escapan()


def largo(tt):
    return min(borde(tt.t) + 7, 41.0)


def dibujar(tt, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    H = largo(tt)
    perfil = np.clip(1 - (U / H) ** 2, 0, 1) ** 0.7
    dentro_tajo = np.abs(U) < H

    w = ABRE[f] * perfil
    if f >= 1 and ABRE[f] > 0:
        w = w + MELLA * (w > 1.2)
    abierta = (np.abs(V) < w) & dentro_tajo
    labio = (np.abs(V) < w + 1.0) & ~abierta & dentro_tajo & (perfil > 0.05)

    if ABRE[f] == 0:
        # el tajo cerrado: una linea, entera en el frame 0 y a trozos al final
        linea = (np.abs(V) < 0.55) & dentro_tajo & (perfil > 0.02)
        if f >= 9:
            corte = (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.35, 0.6, 0.85)[f]
            linea &= RUIDO > corte
        pinta(idx, linea, LABIO[f], tt)
    else:
        pinta(idx, labio, LABIO[f], tt)
        pinta(idx, abierta, 5, tt)
        # el otro cielo: estrellas fijas que el tajo destapa al abrirse, y
        # una nebulosa rala en el indice 4
        hueco = abierta & (np.abs(V) < w - 0.6)
        idx[hueco & tt.libre & DENTRO & (BAYER < 0.18) & (RUIDO > 0.5)] = 4
        idx[hueco & tt.libre & DENTRO & (RUIDO < 0.09)] = 2
        idx[hueco & tt.libre & DENTRO & (RUIDO < 0.035)] = 1

    # chispas del tajo, en el frame 0
    if f == 0:
        for su, lado, _ in ESCAPAN:
            for d in (2, 3):
                x = C + su * H * _ca - lado * d * _sa
                y = C + su * H * _sa + lado * d * _ca
                punto(idx, tt, x, y, 2 if d == 2 else 3)

    # la sutura: puntadas que cruzan el tajo mientras se cierra
    if 5 <= f <= 9:
        n = 7
        for k in range(n):
            su = -0.75 + 1.5 * k / (n - 1)
            if f == 5 and k % 2:
                continue
            ancho = max(w.max(), 1.0) + 1.5
            b = (2, 2, 3, 3, 4)[f - 5]
            for s in np.linspace(-ancho, ancho, 9):
                x = C + su * H * _ca - s * _sa + s * 0.35 * _ca
                y = C + su * H * _sa + s * _ca + s * 0.35 * _sa
                punto(idx, tt, x, y, b)

    # estrellas que se escaparon por el tajo y se alejan
    if f >= 6:
        for su, lado, vel in ESCAPAN:
            d = 3 + (f - 5) * 2.2 * vel
            x = C + su * H * _ca - lado * d * _sa
            y = C + su * H * _sa + lado * d * _ca + 0.2 * (f - 5)
            punto(idx, tt, x, y, min(2 + (f - 6) // 2, 5))
    return idx


if __name__ == '__main__':
    if '--comparativa' in sys.argv:
        comparativa()
    else:
        ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
