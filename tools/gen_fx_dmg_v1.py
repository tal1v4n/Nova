"""NOVA - ART-FX-DMG v1, el dano sin captura: «La grieta».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5 · Spec 6.2

Lo que le pasa al nodo ajeno que recibe un proton hostil y no cae
(`ImpactResult.Damaged`, 6.2): 4 frames, 0.3 s. Una sola hoja,
`art/fx/dmg/v<n>/fx_dmg_sheet.png`, en celdas de 96:

    filas     10   player_24 ... player_40, enemy_24 ... enemy_40
    columnas  16   variante 0 frames 0-3, variante 1 frames 0-3 ...

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan). Las
tallas, la franja del numero, el cuerpo libre y las vistas son los de
`ART-FX-SEND` y `ART-FX-ABS` (`gen_fx_send_v1.py`, `gen_fx_abs_v1.py`):

  - EN EL COLOR DEL QUE ATACA. El golpe es su plasma: `plasma_p` si el
    proton es del Jugador, `plasma_e` si es del Enemigo, caiga sobre quien
    caiga. La fila la elige el bando del proton, no el del nodo.
  - SOBRE CUALQUIER NODO AJENO: enemigos de los cuatro tipos y neutrales,
    Asteroide y Magnetar. Las cinco tallas de «La orbita» por la huella.
  - ALGO SE ROMPE. El aro que sale es el envio y el que entra la absorcion;
    el dano no es un aro. Se lee como un golpe: claro en el primer frame y
    apagandose despues. El generador exige el indice 1 en el frame 0 y menos
    luz en cada frame.
  - CUATRO VARIANTES. En un asedio caen protones seguidos sobre el mismo
    nodo; `VFXPool` sortea una en cada impacto.
  - EL NUMERO, LIBRE: lo que acaba de bajar es la Energia. EL CUERPO, NO:
    es el unico efecto que puede pisarlo, porque lo que se rompe es el. Solo
    0.3 s, y nada entra mas de `DENTRO` px desde el borde.

LA TESIS DE LA v1: EL NODO SE AGRIETA. Tres grietas en zigzag cruzan el borde
del cuerpo, como en un cristal golpeado, y lo de fuera se desprende:

  - GOLPE (frame 0). Tres grietas de 5 px dentro a 5 fuera, claras donde
    cruzan el borde.
  - SE DESPRENDEN (frames 1-3). Lo de dentro se apaga sobre el cuerpo; lo de
    fuera se parte en trozos que se alejan 2 px por frame, hasta unas motas.

    py tools/gen_fx_dmg_v1.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_dmg_v1.py --gif    y las animadas
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
from gen_fx_send_v1 import (ANG, C, CELDA, DX, DY, FACCIONES, FRANJA, LIBRE, R,
                            TALLAS, a_rgba, borde, luz, tramar)

ASSET     = 'dmg'
VERSION   = 1
NOMBRE    = 'La grieta'

FRAMES    = 4                       # ceil(0.3 * 12)
VARIANTES = 4
FPS       = 12
DENTRO    = 8                       # px que el efecto puede entrar en el cuerpo


# --- Lo que comparten las propuestas -------------------------------------------

def punto(idx, x, y, banda, solo_vacio=False):
    ix, iy = int(math.floor(x)), int(math.floor(y))
    if 0 <= ix < CELDA and 0 <= iy < CELDA and LIBRE[iy, ix]:
        if not solo_vacio or idx[iy, ix] == 0:
            idx[iy, ix] = banda


def comprobar(celdas, talla, version):
    fallos = []
    Rb = borde(talla)
    for v in range(VARIANTES):
        frames = celdas[v]
        for f, a in enumerate(frames):
            on = a > 0
            if not set(np.unique(a)) <= {0, 1, 2, 3, 4, 5}:
                fallos.append(f'v{v} f{f}: indice fuera de la rampa [1..5]')
            if (on & FRANJA).any():
                fallos.append(f'v{v} f{f}: pisa la franja del numero')
            if (on & (R < Rb - DENTRO)).any():
                fallos.append(f'v{v} f{f}: entra mas de {DENTRO} px en el cuerpo')
            if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
                fallos.append(f'v{v} f{f}: toca el borde de la celda')
            if not on.any():
                fallos.append(f'v{v} f{f}: vacio')
        if not (frames[0] == 1).any():
            fallos.append(f'v{v}: el golpe no llega al indice 1')
        l = [luz(a) for a in frames]
        if not all(x > y for x, y in zip(l, l[1:])):
            fallos.append(f'v{v}: no se apaga, luz {l}')
        if len({a.tobytes() for a in frames}) != FRAMES:
            fallos.append(f'v{v}: frames repetidos')
    for f in range(FRAMES):
        if len({celdas[v][f].tobytes() for v in range(VARIANTES)}) != VARIANTES:
            fallos.append(f'f{f}: variantes repetidas')
    if fallos:
        raise SystemExit(f'ART-FX-DMG v{version} (talla {talla}) no pasa:\n  '
                         + '\n  '.join(fallos[:12]))


def generar(dibujar, version):
    celdas = {}
    for t in TALLAS:
        c = [[dibujar(t, f, v).astype(np.int8) for f in range(FRAMES)] for v in range(VARIANTES)]
        comprobar(c, t, version)
        celdas[t] = c
    return celdas


def hoja(celdas):
    filas = []
    for ramp_n in FACCIONES.values():
        ramp = rampa(ramp_n)
        for t in TALLAS:
            filas.append(np.hstack([a_rgba(celdas[t][v][f], ramp)
                                    for v in range(VARIANTES) for f in range(FRAMES)]))
    return np.vstack(filas)


def exporta(destino, img):
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_dmg_sheet.png'))
    with open(os.path.join(destino, 'fx_dmg_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'variants': VARIANTES, 'fps': FPS,
                   'sizes': list(TALLAS), 'air': sel.AIRE,
                   'rows': [f'{fac}_{t}' for fac in FACCIONES for t in TALLAS],
                   'note': 'la fila es la del bando del PROTON que golpea y la talla del nodo '
                           'golpeado (talla_de por su huella); columnas: variante 0 frames 0-3, '
                           'variante 1 ...; variante al azar en cada impacto; pivote en el '
                           'centro del nodo'}, f, indent=1)


# --- Vistas --------------------------------------------------------------------

BG, ampliar, sobre = shot.BG, shot.ampliar, shot.sobre
nodo, numero, huellas = abs_.nodo, abs_.numero, abs_.huellas


def efecto(celdas, fac, pre, n, v, f):
    t = sel.talla_de(huellas()[pre, n][2])
    return a_rgba(celdas[t][v][f], rampa(FACCIONES[fac]))


# (bando del nodo, tipo, nivel, bando del atacante)
MUESTRA = (('neutral', 'ast', 0, 'player'), ('enemy', 'mag', 1, 'player'),
           ('enemy', 'str', 2, 'player'), ('neutral', 'mag', 3, 'enemy'),
           ('player', 'str', 5, 'enemy'))


def vista_hoja(celdas):
    """x3. Un nodo de cada talla golpeado: el nodo solo, los cuatro frames de
    la variante 0 y el golpe de las otras tres."""
    filas = []
    for nfac, pre, n, afac in MUESTRA:
        celd = [None] + [(0, f) for f in range(FRAMES)] + [(v, 0) for v in (1, 2, 3)]
        fila = np.empty((CELDA, CELDA * len(celd), 3), np.uint8)
        fila[:] = BG
        for i, vf in enumerate(celd):
            numero(fila, i * CELDA + C, C, pre, n)
            sobre(fila, nodo(pre, n, nfac), i * CELDA + C, C)
            if vf:
                sobre(fila, efecto(celdas, afac, pre, n, *vf), i * CELDA + C, C)
        filas.append(fila)
    return ampliar(np.vstack(filas), 3)


# Un asedio: el Jugador manda una rafaga contra una Estrella enemiga y un
# Asteroide neutral; el Enemigo responde contra una Estrella del Jugador.
OBJETIVOS = {'a': ('enemy', 'str', 3, (330, 70)), 'b': ('neutral', 'ast', 0, (330, 180)),
             'c': ('player', 'str', 2, (90, 180))}
ORIGENES = {'p': ('player', 'mag', 4, (90, 60)), 'e': ('enemy', 'pul', 2, (200, 30))}
RAFAGA = (('p', 'a', 0), ('p', 'a', 3), ('p', 'a', 6), ('p', 'b', 2), ('p', 'b', 7),
          ('e', 'c', 4), ('p', 'a', 9))


def vista_tablero(celdas, k):
    tab = np.empty((230, 420, 3), np.uint8)
    tab[:] = BG
    for fac, pre, n, (x, y) in list(OBJETIVOS.values()) + list(ORIGENES.values()):
        numero(tab, x, y, pre, n)
        sobre(tab, nodo(pre, n, fac), x, y)
    for i, (o, d, k0) in enumerate(RAFAGA):
        afac, _, _, (ox, oy) = ORIGENES[o]
        _, pre, n, (dx, dy) = OBJETIVOS[d]
        hf = (huellas()[pre, n][2] + 1) // 2
        total = math.hypot(dx - ox, dy - oy)
        largo = total - hf
        kk = k - k0
        s = kk * shot.PASO
        if 0 <= s < largo:
            sobre(tab, shot.proton(afac, 6, kk), ox + (dx - ox) * s / total,
                  oy + (dy - oy) * s / total)
        else:
            fx = kk - math.ceil(largo / shot.PASO)
            if 0 <= fx < FRAMES:
                sobre(tab, efecto(celdas, afac, pre, n, (i * 3) % VARIANTES, fx), dx, dy)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_dmg'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(vista_hoja(celdas)).save(os.path.join(d, 'fx_dmg_hoja_x3.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(40)]
        for nombre, ms in (('fx_dmg_tablero_x2.gif', 1000 / FPS),
                           ('fx_dmg_tablero_x2_lento.gif', 4000 / FPS)):
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
    print(f'ART-FX-DMG v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in TALLAS:
        print(f'  talla {t}  px {[int((a > 0).sum()) for a in celdas[t][0]]}  '
              f'luz {[luz(a) for a in celdas[t][0]]}')
    vistas(celdas, version, gif)


# --- La v1: «La grieta» -------------------------------------------------------------

GRIETAS = 3
# Donde va cada vertice del zigzag, en px desde el borde de la talla: el borde
# real del cuerpo cae entre 0 y 3 px por dentro de ese borde, asi que la
# grieta empieza dentro y lo cruza.
TRAMOS  = (-6.0, -3.5, -1.0, 1.5, 4.0)
FUERA   = 2          # los dos ultimos tramos son los de fuera, los que saltan


def _grietas(t, v):
    """Las grietas de la variante v: angulo y polilinea de puntos (s, desvio).
    Deterministas."""
    rng = np.random.default_rng(300 + 17 * v + t)
    base = -math.pi / 2 + v * 0.55
    out = []
    for k in range(GRIETAS):
        a = base + 2 * math.pi * k / GRIETAS + rng.uniform(-0.25, 0.25)
        lado = 1 if rng.random() < 0.5 else -1
        pts = []
        for s in TRAMOS:
            lado = -lado
            pts.append((s, lado * rng.uniform(0.6, 1.5)))
        out.append((a, pts))
    return out


def _tramo(idx, Rb, a, p0, p1, desplaza, banda):
    ux, uy = math.cos(a), math.sin(a)
    px, py = -uy, ux
    n = 12
    for i in range(n + 1):
        s = p0[0] + (p1[0] - p0[0]) * i / n
        w = p0[1] + (p1[1] - p0[1]) * i / n
        r = Rb + 0.5 + s + desplaza
        punto(idx, C + r * ux + w * px, C + r * uy + w * py, banda)


def dibujar(t, f, v):
    Rb = borde(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    for a, pts in _grietas(t, v):
        tramos = list(zip(pts, pts[1:]))
        dentro, fuera = tramos[:-FUERA], tramos[-FUERA:]
        if f == 0:
            # clara donde cruza el borde, un poco menos en las puntas
            for j, (p0, p1) in enumerate(tramos):
                _tramo(idx, Rb, a, p0, p1, 0.0, 1 if 1 <= j <= 2 else 2)
        elif f < 3:
            # lo de dentro se apaga sobre el cuerpo
            for p0, p1 in dentro[-1:]:
                _tramo(idx, Rb, a, p0, p1, 0.0, 2 + f)
            # lo de fuera salta: un tramo de cada dos, cada frame mas lejos
            for j, (p0, p1) in enumerate(fuera):
                if (j + f) % 2 == 0:
                    _tramo(idx, Rb, a, p0, p1, 2.0 * f, 1 + f)
        else:
            for s, w in pts[-2:]:
                r = Rb + 0.5 + s + 5.0
                punto(idx, C + r * math.cos(a) - w * math.sin(a),
                      C + r * math.sin(a) + w * math.cos(a), 4)
    return idx


if __name__ == '__main__':
    ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
