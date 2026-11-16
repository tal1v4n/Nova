"""NOVA - ART-FX-THAW v1, el deshielo: «El deshielo».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.4 · NOVA_Estados_Animaciones.md 14.5 ·
Spec 7.3

El momento en que un nodo `Congelado` deja de estarlo: su Energia llega a 0
por los impactos que lo drenan, o pasan 30 s sin que nadie lo toque (7.3).
En ese tick `NodeFreeze` quita la bandera, el overlay «El vidrio»
(`ART-ST-FROZEN` v3) desaparece y en su sitio empieza este efecto. 6 frames,
0.5 s. Una sola hoja, `art/fx/thaw/v<n>/fx_thaw_sheet.png`, en celdas de 96:

    filas      5   talla 24, 28, 32, 36, 40 (la de «La orbita» del nodo)
    columnas   6   los frames

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - ARRANCA DEL VIDRIO. El overlay es uno solo para los trece congelables
    (Estrella 1-5, Pulsar 1-3, Magnetar 1-5, tambien el neutral), una red de
    trece segmentos sin centro ni eje. El frame 0 conserva sus grietas en su
    sitio (al menos el 80 % de los pixeles de linea fuera de la caja del
    numero): el efecto sustituye al overlay sin salto, como `ART-FX-KILL`
    sustituye al proton.
  - EN LA RAMPA DEL HIELO. `hielo`, la del overlay, indices 1-5. El hielo no
    tiene bando: lo mismo da de quien sea el nodo, y una sola rampa basta.
    El blanco es de `FX-CAP`, que suena en el mismo tick si el deshielo
    acaba en captura.
  - EL NUMERO, LIBRE. Lo que se deshiela es justo el nodo que se esta
    vaciando: el 0 es lo que el jugador mira. El overlay baja hasta y 76 y
    en los nodos chicos cae sobre el numero; el efecto no, nunca: se recorta
    contra la caja del numero de su talla (3 px por encima del borde hacia
    abajo, la `franja` de `ART-FX-IGN`). Por eso hay una fila por talla.
  - SE APAGA. El ultimo frame es el de menos luz y no pasa de la cuarta
    parte del pico.

LA TESIS DE LA v1: EL HIELO SE FUNDE. La palabra lo dice: deshielo. La
escarcha se evapora primero y las grietas se hacen agua, se parten en gotas
y escurren por el cuerpo hacia abajo, hasta secarse antes de llegar al
numero.

  - LA ESCARCHA SE VA (frame 0). Quedan las lineas, sin su trama.
  - SE PARTEN (frame 1). Cada linea se rompe en trazos y cae 1 px.
  - ESCURREN (frames 2-4). Los trazos se recogen en gotas de 2 px que bajan
    a velocidades distintas y se apagan una banda por frame.
  - SE SECAN (frame 5). Quedan unas gotas sueltas.

    py tools/gen_fx_thaw_v1.py                  hoja, comprobaciones y vistas fijas
    py tools/gen_fx_thaw_v1.py --gif            y las animadas
    py tools/gen_fx_thaw_v1.py --comparativa    las tres propuestas lado a lado
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, _dist_seg, dir_preview, rampa
import gen_select_v1 as sel
import gen_fx_shot_v1 as shot
import gen_fx_abs_v1 as abs_
import gen_state_frozen_v3 as vidrio
from gen_fx_send_v1 import C, CELDA, DX, DY, R, a_rgba, borde, luz, tramar

ASSET     = 'thaw'
VERSION   = 1
NOMBRE    = 'El deshielo'

FRAMES    = 6                       # ceil(0.5 * 12)
FPS       = 12
TALLAS    = sel.TALLAS
RAMPA     = 'hielo'
RESTO     = 0.25
ANTES     = 3
MEDIO_NUMERO = sel.NUMERO_MEDIO_ANCHO
CONTINUIDAD = 0.8


# --- Lo que comparten las propuestas -------------------------------------------

def franja(t):
    return (DY > borde(t) - ANTES) & (np.abs(DX) <= MEDIO_NUMERO)


def segmentos():
    """Los trece segmentos de «El vidrio»: ((x0, y0), (x1, y1), banda)."""
    return vidrio._segmentos()


_PX, _PY = np.meshgrid(np.arange(CELDA) + 0.5, np.arange(CELDA) + 0.5)


def linea(a, b, grosor=vidrio.GROSOR):
    return _dist_seg(_PX, _PY, a, b) <= grosor


def overlay_idx():
    """El overlay real, leido del PNG y pasado a indices de la rampa."""
    im = np.asarray(Image.open(os.path.join(RAIZ, 'art', 'states', 'frozen', 'v3',
                                            'state_frozen_overlay.png')).convert('RGBA'))
    ramp = rampa(RAMPA)
    idx = np.zeros((CELDA, CELDA), np.int8)
    for i in range(1, 6):
        m = (im[..., 3] > 0) & np.all(im[..., :3] == ramp[i], axis=-1)
        idx[m] = i
    assert ((im[..., 3] > 0) == (idx > 0)).all(), 'el overlay tiene colores fuera de la rampa'
    return idx


def lineas():
    m = np.zeros((CELDA, CELDA), bool)
    for a, b, _ in segmentos():
        m |= linea(a, b)
    return m


class Talla:
    def __init__(self, t):
        self.t = t
        self.franja = franja(t)
        self.libre = ~self.franja

    def punto(self, idx, x, y, banda, solo_vacio=False):
        ix, iy = int(math.floor(x)), int(math.floor(y))
        if 0 <= ix < CELDA and 0 <= iy < CELDA and self.libre[iy, ix]:
            if not solo_vacio or idx[iy, ix] == 0:
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
    ref = lineas() & tt.libre
    sigue = (frames[0] > 0)[ref].mean()
    if sigue < CONTINUIDAD:
        fallos.append(f'el frame 0 no arranca del vidrio: conserva {sigue:.0%} de sus lineas')
    l = [luz(a) for a in frames]
    if l[-1] != min(l) or l[-1] > RESTO * max(l):
        fallos.append(f'no se apaga: luz {l}')
    if len({a.tobytes() for a in frames}) != FRAMES:
        fallos.append('frames repetidos')
    if fallos:
        raise SystemExit(f'ART-FX-THAW v{version} (talla {t}) no pasa:\n  '
                         + '\n  '.join(fallos))
    return sigue


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
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_thaw_sheet.png'))
    with open(os.path.join(destino, 'fx_thaw_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'fps': FPS, 'sizes': list(TALLAS),
                   'rows': [f'thaw_{t}' for t in TALLAS],
                   'note': 'sobre el nodo que se deshiela, en el tick en que desaparece el overlay '
                           'state_frozen_overlay; la talla, la de world_select_tallas.json por la '
                           'huella del nodo; una sola rampa (hielo), sin bando; pivote en el centro'},
                  f, indent=1)


# --- Vistas --------------------------------------------------------------------

BG, ampliar, sobre, abrir = shot.BG, shot.ampliar, shot.sobre, shot.abrir
nodo, numero, huellas = abs_.nodo, abs_.numero, abs_.huellas
MUESTRA = (('player', 'str', 1), ('enemy', 'pul', 1), ('neutral', 'mag', 2),
           ('enemy', 'str', 3), ('player', 'pul', 3), ('neutral', 'mag', 5))


def overlay_rgba():
    return abrir('states', 'frozen', 'v3', 'state_frozen_overlay.png')


def efecto(celdas, pre, n, f):
    t = sel.talla_de(huellas()[pre, n][2])
    return a_rgba(celdas[t][f], rampa(RAMPA))


def vista_hoja(celdas, muestra=MUESTRA):
    """x3. Por nodo: congelado, con «El vidrio», y los seis frames."""
    filas = []
    for fac, pre, n in muestra:
        fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
        fila[:] = BG
        numero(fila, C, C, pre, n)
        sobre(fila, nodo(pre, n, fac), C, C)
        sobre(fila, overlay_rgba(), C, C)
        for f in range(FRAMES):
            x = (f + 1) * CELDA + C
            numero(fila, x, C, pre, n)
            sobre(fila, nodo(pre, n, fac), x, C)
            sobre(fila, efecto(celdas, pre, n, f), x, C)
        filas.append(fila)
    return np.vstack(filas)


# Tres nodos congelados se deshielan uno tras otro: un Magnetar neutral, una
# Estrella del Enemigo y un Pulsar del Jugador.
ESCENA = (('neutral', 'mag', 3, (90, 100), 4), ('enemy', 'str', 2, (220, 120), 13),
          ('player', 'pul', 2, (350, 100), 22))


def vista_tablero(celdas, k):
    tab = np.empty((220, 440, 3), np.uint8)
    tab[:] = BG
    for fac, pre, n, (x, y), k0 in ESCENA:
        numero(tab, x, y, pre, n)
        sobre(tab, nodo(pre, n, fac), x, y)
        if k < k0:
            sobre(tab, overlay_rgba(), x, y)
        elif k - k0 < FRAMES:
            sobre(tab, efecto(celdas, pre, n, k - k0), x, y)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_thaw'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(vista_hoja(celdas), 3)).save(os.path.join(d, 'fx_thaw_hoja_x3.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(34)]
        for nombre, ms in (('fx_thaw_tablero_x2.gif', 1000 / FPS),
                           ('fx_thaw_tablero_x2_lento.gif', 4000 / FPS)):
            im = [Image.fromarray(ampliar(e, 2)) for e in esc]
            im[0].save(os.path.join(d, nombre), save_all=True, append_images=im[1:],
                       duration=round(ms), loop=0)
    print('vistas en', d)


def comparativa():
    import importlib
    muestra = (('player', 'str', 1), ('neutral', 'mag', 2), ('enemy', 'str', 4))
    bloques = []
    for v in (1, 2, 3):
        mod = importlib.import_module(f'gen_fx_thaw_v{v}')
        bloques.append(vista_hoja(generar(mod.dibujar, v), muestra))
        sep = np.empty((6, CELDA * (FRAMES + 1), 3), np.uint8)
        sep[:] = (60, 66, 90)
        bloques.append(sep)
    ruta = os.path.join(dir_preview('fx_thaw'), 'comparativa_x3.png')
    Image.fromarray(ampliar(np.vstack(bloques[:-1]), 3)).save(ruta)
    print('comparativa en', ruta)


def ejecutar(dibujar, version, nombre, gif):
    destino = os.path.join(RAIZ, 'art', 'fx', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    celdas = generar(dibujar, version)
    img = hoja(celdas)
    exporta(destino, img)
    print(f'ART-FX-THAW v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in TALLAS:
        sigue = (celdas[t][0] > 0)[lineas() & Talla(t).libre].mean()
        print(f'  talla {t}  px {[int((a > 0).sum()) for a in celdas[t]]}  '
              f'luz {[luz(a) for a in celdas[t]]}  vidrio en f0 {sigue:.0%}')
    vistas(celdas, version, gif)


# --- La v1: «El deshielo» ------------------------------------------------------

CAIDA = (0, 1, 3, 6, 10, 14)        # px que ha bajado cada gota
BANDA = (None, 2, 2, 3, 4, 5)


def gotas():
    """Semillas a lo largo de cada grieta, cada 3 px, con su velocidad."""
    out = []
    rng = np.random.default_rng(11)
    for a, b, banda in segmentos():
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        for k in range(int(L // 3) + 1):
            s = min(k * 3 / L, 1.0)
            out.append((a[0] + (b[0] - a[0]) * s, a[1] + (b[1] - a[1]) * s, banda,
                        rng.uniform(0.6, 1.3)))
    return out


def dibujar(tt, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    if f == 0:
        for a, b, banda in segmentos():
            m = linea(a, b) & tt.libre
            idx[m] = banda
        return idx
    if f == 1:
        # cada linea se rompe en trazos de 2 px, con 1 de hueco, y cae 1 px
        for a, b, banda in segmentos():
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            for k in range(int(L)):
                if k % 3 == 2:
                    continue
                s = k / L
                tt.punto(idx, a[0] + (b[0] - a[0]) * s, a[1] + (b[1] - a[1]) * s + 1, 2)
        return idx
    vivas = gotas()
    for i, (x, y, _, v) in enumerate(vivas):
        if f == 5 and i % 4:
            continue                                       # se secan casi todas
        if f == 4 and i % 2:
            continue
        yy = y + CAIDA[f] * v
        tt.punto(idx, x, yy, BANDA[f])
        if f < 5:
            tt.punto(idx, x, yy - 1, min(BANDA[f] + 1, 5), solo_vacio=True)   # la cola
    return idx


if __name__ == '__main__':
    if '--comparativa' in sys.argv:
        comparativa()
    else:
        ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
