"""NOVA - ART-FX-PWR-ZER v1, el impacto de Cero Absoluto: «La helada».

Ref: NOVA_Assets_Diseno.md A.5, A.3.4 y A.4.2 · NOVA_Estados_Animaciones.md
14.5 · Spec 7.3

El momento en que el haz de Cero Absoluto (`ART-WLD-BEAM-ZER`, «El rosario
de escarcha») termina sobre su objetivo y el nodo queda `Congelado`. 9
frames, 0.7 s; al acabar, el overlay «El vidrio» (`ART-ST-FROZEN` v3) toma
el relevo y se queda. Una sola hoja, `art/fx/pwr_zer/v<n>/fx_pwr_zer_sheet.png`,
en celdas de 96:

    filas      5   talla 24, 28, 32, 36, 40 (la de «La orbita» del nodo)
    columnas   9   los frames

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - ACABA EN EL VIDRIO. Es el deshielo al reves (`ART-FX-THAW` arranca del
    overlay; este acaba en el). El ultimo frame ES el overlay, recortado
    contra la caja del numero: en el frame siguiente el overlay aparece y
    no se nota el relevo. El generador lo exige pixel a pixel.
  - EN LA RAMPA DEL HIELO, SIN BANDO. `hielo`, la del haz y la del overlay,
    indices 1-5. El objetivo puede ser de cualquiera, tambien el Magnetar
    neutral.
  - EL NUMERO, LIBRE, con la caja del numero de su talla (la `franja` de
    `ART-FX-THAW`): una fila por talla.
  - SIN DIRECCION. El haz llega desde cualquier angulo y la hoja no gira:
    el impacto se organiza sobre la red del vidrio, que no tiene centro ni
    eje (A.3.4).
  - PRIMERO LO FUERTE. Algun frame llega al indice 1 antes de asentarse.

LA TESIS DE LA v1: EL HIELO CRECE. Las grietas no aparecen: se propagan. El
frio entra por el nudo de enmedio del tronco y la red crece desde ahi,
segmento a segmento, con el frente encendido en el indice 1, como la
escarcha que trepa por un cristal. Cuando la red esta entera florece la
escarcha y se asienta en sus bandas.

  - EL GOLPE (frame 0). Un copo de seis puntas en el nudo, en el indice 1:
    el contacto del haz, con su motivo.
  - CRECE (frames 1-6). Cada segmento se dibuja desde su arranque a una
    velocidad comun; el tronco primero, las ramas desde sus nudos, las
    astillas al final. El frente va en el indice 1 y lo ya crecido, en el 2.
  - SE ASIENTA (frames 7-8). La red pasa a sus bandas y florece la
    escarcha: el frame 8 es el overlay.

    py tools/gen_fx_pwr_zer_v1.py                  hoja, comprobaciones y vistas fijas
    py tools/gen_fx_pwr_zer_v1.py --gif            y las animadas
    py tools/gen_fx_pwr_zer_v1.py --comparativa    las tres propuestas lado a lado
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
import gen_fx_thaw_v1 as thaw
import gen_state_frozen_v3 as vidrio
from gen_fx_send_v1 import BAYER
from gen_fx_thaw_v1 import (BG, C, CELDA, Talla, a_rgba, ampliar, huellas, linea, luz,
                            nodo, numero, overlay_idx, overlay_rgba, sobre, tramar)

ASSET     = 'pwr_zer'
VERSION   = 1
NOMBRE    = 'La helada'

FRAMES    = 9                       # ceil(0.7 * 12)
FPS       = 12
TALLAS    = sel.TALLAS
RAMPA     = 'hielo'
NUDO      = vidrio.TRONCO[2]


# --- Lo que comparten las propuestas -------------------------------------------

def final(tt):
    """El ultimo frame: el overlay, sin la caja del numero."""
    return np.where(tt.libre, overlay_idx(), 0).astype(np.int8)


def copo(idx, tt, cx, cy, r, banda, giro=0.0, punta=None):
    """Un copo de seis brazos de radio r, como los del haz."""
    for k in range(6):
        a = giro + k * math.pi / 3
        x1, y1 = cx + r * math.cos(a), cy + r * math.sin(a)
        m = linea((cx, cy), (x1, y1), 0.6) & tt.libre
        idx[m & ((idx == 0) | (idx > banda))] = banda
        if r >= 4:
            # dos barbas en el tercio de fuera
            bx, by = cx + 0.66 * r * math.cos(a), cy + 0.66 * r * math.sin(a)
            for s in (1, -1):
                b = a + s * math.pi / 3
                m = linea((bx, by), (bx + 0.3 * r * math.cos(b), by + 0.3 * r * math.sin(b)),
                          0.55) & tt.libre
                idx[m & (idx == 0)] = punta or banda


def lineas_del_vidrio():
    m = np.zeros((CELDA, CELDA), bool)
    for a, b, _ in thaw.segmentos():
        m |= linea(a, b)
    return m


def asentar(tt, escarcha, banda_lineas=None):
    """La red con su escarcha a medio florecer: `escarcha` en [0, 1] es la
    fraccion de la trama del overlay que ya esta. Las lineas van en sus
    bandas, o todas en `banda_lineas`."""
    o = final(tt)
    lin = lineas_del_vidrio() & (o > 0)
    idx = np.zeros_like(o)
    idx[lin] = o[lin] if banda_lineas is None else banda_lineas
    trama = (o > 0) & ~lin & (BAYER < 0.42 * escarcha)
    idx[trama] = o[trama]
    return idx


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
    if not np.array_equal(frames[-1], final(tt)):
        fallos.append('el ultimo frame no es el vidrio')
    if not any((a == 1).any() for a in frames[:-1]):
        fallos.append('ningun frame llega al indice 1')
    if len({a.tobytes() for a in frames}) != FRAMES:
        fallos.append('frames repetidos')
    if fallos:
        raise SystemExit(f'ART-FX-PWR-ZER v{version} (talla {t}) no pasa:\n  '
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
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_pwr_zer_sheet.png'))
    with open(os.path.join(destino, 'fx_pwr_zer_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'fps': FPS, 'sizes': list(TALLAS),
                   'rows': [f'pwr_zer_{t}' for t in TALLAS],
                   'note': 'sobre el objetivo del haz de Cero Absoluto, al terminar el haz; '
                           'el ultimo frame es state_frozen_overlay sin la caja del numero, y en '
                           'el siguiente entra el overlay; la talla, la de world_select_tallas.json '
                           'por la huella del nodo; una sola rampa (hielo), sin bando'},
                  f, indent=1)


# --- Vistas --------------------------------------------------------------------

MUESTRA = (('player', 'str', 1), ('enemy', 'pul', 1), ('neutral', 'mag', 2),
           ('enemy', 'str', 3), ('player', 'pul', 3), ('neutral', 'mag', 5))


def efecto(celdas, pre, n, f):
    t = sel.talla_de(huellas()[pre, n][2])
    return a_rgba(celdas[t][f], rampa(RAMPA))


def vista_hoja(celdas, muestra=MUESTRA):
    """Por nodo: los nueve frames y, al final, el nodo ya congelado con su
    overlay, para ver que el relevo no salta."""
    filas = []
    for fac, pre, n in muestra:
        fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
        fila[:] = BG
        for i in range(FRAMES + 1):
            x = i * CELDA + C
            numero(fila, x, C, pre, n)
            sobre(fila, nodo(pre, n, fac), x, C)
            sobre(fila, efecto(celdas, pre, n, i) if i < FRAMES else overlay_rgba(), x, C)
        filas.append(fila)
    return np.vstack(filas)


# Tres nodos reciben el Cero Absoluto uno tras otro y se quedan congelados.
ESCENA = (('enemy', 'str', 2, (90, 100), 3), ('neutral', 'mag', 3, (220, 120), 10),
          ('player', 'pul', 2, (350, 100), 17))


def vista_tablero(celdas, k):
    tab = np.empty((220, 440, 3), np.uint8)
    tab[:] = BG
    for fac, pre, n, (x, y), k0 in ESCENA:
        numero(tab, x, y, pre, n)
        sobre(tab, nodo(pre, n, fac), x, y)
        if k0 <= k < k0 + FRAMES:
            sobre(tab, efecto(celdas, pre, n, k - k0), x, y)
        elif k >= k0 + FRAMES:
            sobre(tab, overlay_rgba(), x, y)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_pwr_zer'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(vista_hoja(celdas), 3)).save(os.path.join(d, 'fx_pwr_zer_hoja_x3.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(34)]
        for nombre, ms in (('fx_pwr_zer_tablero_x2.gif', 1000 / FPS),
                           ('fx_pwr_zer_tablero_x2_lento.gif', 4000 / FPS)):
            im = [Image.fromarray(ampliar(e, 2)) for e in esc]
            im[0].save(os.path.join(d, nombre), save_all=True, append_images=im[1:],
                       duration=round(ms), loop=0)
    print('vistas en', d)


def comparativa():
    import importlib
    muestra = (('player', 'str', 1), ('neutral', 'mag', 2), ('enemy', 'str', 4))
    bloques = []
    for v in (1, 2, 3):
        mod = importlib.import_module(f'gen_fx_pwr_zer_v{v}')
        bloques.append(vista_hoja(generar(mod.dibujar, v), muestra))
        sep = np.empty((6, CELDA * (FRAMES + 1), 3), np.uint8)
        sep[:] = (60, 66, 90)
        bloques.append(sep)
    ruta = os.path.join(dir_preview('fx_pwr_zer'), 'comparativa_x3.png')
    Image.fromarray(ampliar(np.vstack(bloques[:-1]), 3)).save(ruta)
    print('comparativa en', ruta)


def ejecutar(dibujar, version, nombre, gif):
    destino = os.path.join(RAIZ, 'art', 'fx', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    celdas = generar(dibujar, version)
    img = hoja(celdas)
    exporta(destino, img)
    print(f'ART-FX-PWR-ZER v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in TALLAS:
        print(f'  talla {t}  px {[int((a > 0).sum()) for a in celdas[t]]}  '
              f'luz {[luz(a) for a in celdas[t]]}')
    vistas(celdas, version, gif)


# --- La v1: «La helada» --------------------------------------------------------

VEL = 7.6                           # px de grieta por frame: la red entera en el 6


def _arranques():
    """Cada segmento con el instante (en px recorridos) en que empieza a
    crecer: el tronco desde el nudo de enmedio hacia los dos lados, las ramas
    desde su nudo cuando el frente pasa por el, las astillas al final."""
    out = []
    T = vidrio.TRONCO
    dist = {T[2]: 0.0}
    for lado in (range(2, 0, -1), range(2, len(T) - 1)):
        for i in lado:
            a, b = (T[i], T[i - 1]) if lado.step == -1 else (T[i], T[i + 1])
            L = math.dist(a, b)
            out.append((a, b, dist[a], 1))
            dist[b] = dist[a] + L
    for rama in vidrio.RAMAS:
        d0 = dist.get(rama[0], 0.0)
        for a, b in zip(rama[:-1], rama[1:]):
            L = math.dist(a, b)
            out.append((a, b, d0, 2))
            dist[b] = d0 + L
            d0 += L
    fin = max(d0 for _, _, d0, _ in out) + 6
    for a, b in vidrio.ASTILLAS:
        out.append((a, b, fin, 2))
    return out


ARRANQUES = _arranques()


def dibujar(tt, f):
    if f == FRAMES - 1:
        return final(tt)
    idx = np.zeros((CELDA, CELDA), np.int8)
    if f == 7:
        return asentar(tt, 0.4)
    recorrido = VEL * f
    if f == 0:
        copo(idx, tt, NUDO[0], NUDO[1], 7, 1, giro=0.26, punta=2)
        return idx
    for a, b, d0, banda in ARRANQUES:
        L = math.dist(a, b)
        hecho = recorrido - d0
        if hecho <= 0:
            continue
        s = min(hecho / L, 1.0)
        p = (a[0] + (b[0] - a[0]) * s, a[1] + (b[1] - a[1]) * s)
        m = linea(a, p) & tt.libre
        idx[m & (idx == 0)] = 2
        if s < 1:
            tt.punto(idx, p[0], p[1], 1)
            tt.punto(idx, p[0] + 0.7, p[1] + 0.7, 1)
    if f <= 2:
        copo(idx, tt, NUDO[0], NUDO[1], 7 - 2 * f, 2 + f, giro=0.26)
    return idx


if __name__ == '__main__':
    if '--comparativa' in sys.argv:
        comparativa()
    else:
        ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
