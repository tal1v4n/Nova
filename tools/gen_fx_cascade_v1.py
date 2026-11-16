"""NOVA - ART-FX-CASCADE v1, la cascada de conquista: «Los fuegos artificiales».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.7 · NOVA_Estados_Animaciones.md 14.5 ·
Spec 10.1 y 10.3

Al ganar, todos los nodos neutrales que quedan -Asteroides y Magnetares-
pasan al Jugador, del mas cercano a sus nodos al mas lejano, repartidos en
3 s (10.1). Una sola hoja, `art/fx/cascade/v<n>/fx_cascade_sheet.png`, en
celdas de 96:

    filas      5   talla 24, 28, 32, 36, 40 (la de «La orbita» del nodo)
    columnas  12   los frames

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - UN EFECTO POR NODO, NO UNO DE 3 s. `WinConditionChecker` pasa cada
    nodo al Jugador en su instante -el i-esimo de n a los (i + 1) / n de
    los 3 s- con `Steal`, como un Agujero de Gusano, y lanza
    `OnConquered(node)` para este efecto. Asi que el asset es lo que le pasa
    a UN nodo: 1.0 s, 12 frames, desde ese tick. La ola de cerca a lejos no
    la dibuja la hoja; la dibuja el orden en que se disparan. A.5 decia
    «3.0 s, 36 frames» como si fuese una sola animacion.
  - ACOMPANA A LA CAPTURA. Como en `ART-FX-PWR-WRM`: en el mismo frame el
    nodo se recolorea y arranca «El relevo» (`ART-ST-CAP`), que va encima.
    Nada de blanco, que es de FX-CAP.
  - EN EL COLOR DEL JUGADOR. La cascada solo va al Jugador: `plasma_p`, la
    rampa de su plasma, indices 1-5.
  - EL NUMERO, LIBRE, con la caja del numero de su talla: una fila por
    talla. Cada nodo conserva su Energia (10.1) y la pantalla de victoria
    cuenta cuantos llegaron por la cascada (13.11).
  - EL FRAME 0 SE SOSTIENE SOLO. Con el ultimo nodo de la cascada la
    partida se congela y entra la pantalla de victoria (10.3): ese nodo se
    queda en el frame 0, asi que el frame 0 tiene que leerse como imagen
    fija, y llega al indice 1.
  - SALE A NADA. El ultimo frame es el de menos luz y no pasa de la cuarta
    parte del pico.

LA TESIS DE LA v1: SE CELEBRA. La partida esta ganada y lo que queda es
fiesta: cada nodo que se suma lanza un cohete en el color del Jugador que
estalla sobre el. Con varios nodos en cadena, del cercano al lejano, es una
traca que recorre el mapa.

  - EL DISPARO (frame 0). Un destello en el centro del nodo y el cohete
    saliendo.
  - LA SUBIDA (frames 1-2). El cohete sube con su estela de chispas.
  - EL ESTALLIDO (frame 3). Sobre el nodo, a 16 px del centro.
  - LA PALMERA (frames 4-8). Veinte chispas se abren hasta 23 px con su
    trazo, y un corazon de diez mas lentas; frenan, caen y las puntas
    crepitan.
  - LA LLUVIA (frames 9-11). Caen como un sauce y se apagan.

    py tools/gen_fx_cascade_v1.py                  hoja, comprobaciones y vistas fijas
    py tools/gen_fx_cascade_v1.py --gif            y las animadas
    py tools/gen_fx_cascade_v1.py --comparativa    las tres propuestas lado a lado
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
from gen_fx_thaw_v1 import (BG, C, CELDA, Talla, a_rgba, ampliar, huellas, sobre, luz,
                            abrir)

ASSET     = 'cascade'
VERSION   = 1
NOMBRE    = 'Los fuegos artificiales'

FRAMES    = 12                      # 1.0 s por nodo
FPS       = 12
TALLAS    = sel.TALLAS
RAMPA     = 'plasma_p'
RESTO     = 0.25
DENTRO    = R < 43.5

RUIDO = np.random.default_rng(97).random((CELDA, CELDA))


# --- Lo que comparten las propuestas -------------------------------------------

def pinta(idx, m, banda, tt):
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
    if not (frames[0] == 1).any():
        fallos.append('el frame 0 no llega al indice 1')
    l = [luz(a) for a in frames]
    if l[-1] != min(l) or l[-1] > RESTO * max(l):
        fallos.append(f'no se apaga: luz {l}')
    if len({a.tobytes() for a in frames}) != FRAMES:
        fallos.append('frames repetidos')
    if fallos:
        raise SystemExit(f'ART-FX-CASCADE v{version} (talla {t}) no pasa:\n  '
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
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_cascade_sheet.png'))
    with open(os.path.join(destino, 'fx_cascade_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'fps': FPS, 'sizes': list(TALLAS),
                   'rows': [f'cascade_{t}' for t in TALLAS],
                   'note': 'uno por nodo, sobre cada nodo que WinConditionChecker.OnConquered pasa '
                           'al Jugador, en ese tick: el nodo ya lleva su paleta nueva y '
                           'state_captured_sheet suena a la vez, por encima; la talla, la de '
                           'world_select_tallas.json por la huella del nodo (tambien la del '
                           'Asteroide); una sola rampa (plasma_p), la del Jugador'},
                  f, indent=1)


# --- Vistas --------------------------------------------------------------------

_CAP = []


def captura(f):
    if not _CAP:
        h = abrir('states', 'cap', 'v2', 'state_captured_sheet.png')
        _CAP.extend(h[:, k * CELDA:(k + 1) * CELDA] for k in range(h.shape[1] // CELDA))
    return _CAP[f] if f < len(_CAP) else None


def huella(pre, n):
    if pre == 'ast':
        with open(os.path.join(RAIZ, 'art', 'nodes', 'asteroide', 'v1', 'ast_huella.json')) as fh:
            return json.load(fh)['footprintPixels'][0]
    return huellas()[pre, n][2]


def nodo(pre, n, fac):
    if pre == 'ast':
        return abrir('nodes', 'asteroide', 'v1', f'ast_lvl0_{fac}.png')
    carpeta, version, _ = huellas()[pre, n]
    return abrir('nodes', carpeta, f'v{version}', f'{pre}_lvl{n}_{fac}.png')


def numero(tab, x, y, pre, n):
    hf = (huella(pre, n) + 1) // 2
    y0 = int(y) + hf + 2
    tab[y0:y0 + 20, int(x) - 11:int(x) + 11] = (60, 66, 90)


def efecto(celdas, pre, n, f):
    return a_rgba(celdas[sel.talla_de(huella(pre, n))][f], rampa(RAMPA))


def capa(lienzo, celdas, pre, n, f, x, y):
    sobre(lienzo, nodo(pre, n, 'player'), x, y)
    sobre(lienzo, efecto(celdas, pre, n, f), x, y)
    c = captura(f)
    if c is not None:
        sobre(lienzo, c, x, y)


# Solo hay dos clases de neutral: Asteroides y Magnetares.
MUESTRA = (('ast', 0), ('mag', 1), ('mag', 2), ('mag', 4))


def vista_hoja(celdas, muestra=MUESTRA):
    """Por nodo: neutral antes y los doce frames, ya del Jugador y con «El
    relevo» encima, como en partida."""
    filas = []
    for pre, n in muestra:
        fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
        fila[:] = BG
        numero(fila, C, C, pre, n)
        sobre(fila, nodo(pre, n, 'neutral'), C, C)
        for f in range(FRAMES):
            x = (f + 1) * CELDA + C
            numero(fila, x, C, pre, n)
            capa(fila, celdas, pre, n, f, x, C)
        filas.append(fila)
    return np.vstack(filas)


# Un mapa al ganar: dos nodos del Jugador a la izquierda y seis neutrales
# que caen de cerca a lejos en 3 s (36 frames): el i-esimo de 6 a los
# (i + 1) / 6, y con el ultimo se congela.
PROPIOS = (('str', 3, (60, 150)), ('pul', 2, (70, 290)))
NEUTRALES = (('ast', 0, (190, 110)), ('ast', 0, (200, 260)), ('mag', 2, (320, 190)),
             ('ast', 0, (430, 90)), ('ast', 0, (450, 300)), ('mag', 4, (570, 200)))
INICIO = 4


def vista_tablero(celdas, k):
    tab = np.empty((380, 660, 3), np.uint8)
    tab[:] = BG
    for pre, n, (x, y) in PROPIOS:
        numero(tab, x, y, pre, n)
        sobre(tab, nodo(pre, n, 'player'), x, y)
    total = len(NEUTRALES)
    fin = INICIO + 36
    kk = min(k, fin)                    # al acabar la cascada, todo se congela
    for i, (pre, n, (x, y)) in enumerate(NEUTRALES):
        numero(tab, x, y, pre, n)
        k0 = INICIO + round(36 * (i + 1) / total)
        if kk < k0:
            sobre(tab, nodo(pre, n, 'neutral'), x, y)
        elif kk - k0 < FRAMES:
            capa(tab, celdas, pre, n, kk - k0, x, y)
        else:
            sobre(tab, nodo(pre, n, 'player'), x, y)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_cascade'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(vista_hoja(celdas), 3)).save(os.path.join(d, 'fx_cascade_hoja_x3.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(INICIO + 36 + 14)]
        for nombre, ms in (('fx_cascade_tablero_x2.gif', 1000 / FPS),
                           ('fx_cascade_tablero_x2_lento.gif', 4000 / FPS)):
            im = [Image.fromarray(ampliar(e, 2)) for e in esc]
            im[0].save(os.path.join(d, nombre), save_all=True, append_images=im[1:],
                       duration=round(ms), loop=0)
    print('vistas en', d)


def comparativa():
    import importlib
    bloques = []
    for v in (1, 2, 3):
        mod = importlib.import_module(f'gen_fx_cascade_v{v}')
        bloques.append(vista_hoja(generar(mod.dibujar, v)))
        sep = np.empty((6, CELDA * (FRAMES + 1), 3), np.uint8)
        sep[:] = (60, 66, 90)
        bloques.append(sep)
    ruta = os.path.join(dir_preview('fx_cascade'), 'comparativa_x3.png')
    Image.fromarray(ampliar(np.vstack(bloques[:-1]), 3)).save(ruta)
    print('comparativa en', ruta)


def ejecutar(dibujar, version, nombre, gif):
    destino = os.path.join(RAIZ, 'art', 'fx', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    celdas = generar(dibujar, version)
    img = hoja(celdas)
    exporta(destino, img)
    print(f'ART-FX-CASCADE v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in TALLAS:
        print(f'  talla {t}  px {[int((a > 0).sum()) for a in celdas[t]]}  '
              f'luz {[luz(a) for a in celdas[t]]}')
    vistas(celdas, version, gif)


# --- La v1: «Los fuegos artificiales» ------------------------------------------

def _chispas():
    """Veinte chispas de la palmera y diez del corazon, mas lentas."""
    rng = np.random.default_rng(31)
    out = []
    for n, vel, extra in ((20, 1.0, 0), (10, 0.55, 1)):
        base = rng.random() * 2 * math.pi
        for i in range(n):
            a = base + 2 * math.pi * i / n + rng.uniform(-0.16, 0.16)
            out.append((a, vel * rng.uniform(0.88, 1.12), rng.random(), extra))
    return out


CHISPAS = _chispas()
#           f:  3    4     5     6     7     8     9     10    11
ABRE  = (None,) * 3 + (4.0, 10.0, 15.0, 19.0, 21.5, 23.0, 24.0, 24.6, 25.0)
CAE   = (None,) * 3 + (0.0, 0.2, 0.7, 1.5, 2.7, 4.3, 6.3, 8.7, 11.4)
BANDA = (None,) * 3 + (1, 1, 1, 2, 2, 3, 3, 4, 5)
APAGA = (None,) * 3 + (0, 0, 0, 0, 0, 0.15, 0.35, 0.55, 0.75)

YB = C - 16.0                               # donde estalla, sobre el centro del nodo
XB = C + 2.0


def _estrella(idx, tt, x, y, r, b0):
    for k in range(-r, r + 1):
        b = b0 if abs(k) < 2 else min(b0 + 1, 5)
        punto(idx, tt, x + k, y, b)
        punto(idx, tt, x, y + k, b)
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        punto(idx, tt, x + dx, y + dy, min(b0 + 1, 5))


def _trazo(idx, tt, p, q, b):
    n = max(2, int(math.dist(p, q) * 1.5))
    for k in range(n + 1):
        s = k / n
        punto(idx, tt, p[0] + (q[0] - p[0]) * s, p[1] + (q[1] - p[1]) * s, b)


def pos(a, v, f):
    r = ABRE[f] * v
    return XB + r * math.cos(a), YB + r * math.sin(a) + CAE[f]


def dibujar(tt, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    x0, y0 = C + 0.5, C + 2.0                # sale del centro del nodo

    if f <= 2:
        s = (0.12, 0.5, 0.85)[f]
        hx, hy = x0 + (XB - x0) * s, y0 + (YB - y0) * s
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            punto(idx, tt, hx + dx, hy + dy, 1)
        for k in range(1, 12):
            ty = hy + 1.6 * k + 1
            if ty > y0 + 2:
                break
            jit = 0.9 * math.sin(k * 2.1 + f * 1.3)
            punto(idx, tt, hx + jit + 0.5, ty, 2 if k < 3 else (3 if k < 6 else 4))
        if f == 0:
            _estrella(idx, tt, x0, y0 + 1, 3, 1)
        if f == 1:
            _estrella(idx, tt, x0, y0 + 1, 1, 3)
        return idx

    if f == 3:
        _estrella(idx, tt, XB, YB, 3, 1)
    if f == 4:
        _estrella(idx, tt, XB, YB, 1, 3)

    for a, v, azar, corazon in CHISPAS:
        if azar < APAGA[f]:
            continue
        b = min(BANDA[f] + corazon, 5)
        p = pos(a, v, f)
        if f >= 4:
            _trazo(idx, tt, pos(a, v, f - 1), p, min(b + 1, 5))
        punto(idx, tt, p[0], p[1], b)
        if f in (7, 8) and azar > 0.55 and not corazon:
            d = 1 + (f == 8)
            for dx, dy in ((-d, 0), (d, 0), (0, -d), (0, d)):
                punto(idx, tt, p[0] + dx, p[1] + dy, 2 if f == 7 else 3)
    return idx


if __name__ == '__main__':
    if '--comparativa' in sys.argv:
        comparativa()
    else:
        ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
