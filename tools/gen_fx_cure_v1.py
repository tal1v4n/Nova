"""NOVA - ART-FX-CURE v1, la cura de Decaimiento: «La purga».

Ref: NOVA_Assets_Diseno.md A.5 y A.3.5 · NOVA_Estados_Animaciones.md 14.5 ·
Spec 7.4

El momento en que un nodo `Infectado` deja de estarlo: recibe Energia de
cualquier faccion (7.4, *Cura*). En ese tick `NodeDecay` quita la bandera, el
overlay «El miasma» (`ART-ST-DECAY` v2) desaparece y en su sitio empieza este
efecto. 5 frames, 0.4 s. Una sola hoja, `art/fx/cure/v<n>/fx_cure_sheet.png`,
en celdas de 96:

    filas      5   talla 24, 28, 32, 36, 40 (la de «La orbita» del nodo)
    columnas  80   variante 0 frames 0-4, variante 1 frames 0-4, ...

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - ARRANCA DEL MIASMA, EN SU FRAME. El overlay es una nube que respira en
    16 frames, la misma para los trece infectables. La variante v arranca de
    la nube del frame v del bucle (`gen_state_decay_v2.campo` y sus cinco
    gotas), y `VFXPool` toma la del frame en que estaba el overlay, como
    `ART-FX-KILL` con el bucle del proton. El frame 0 conserva al menos el
    60 % de los pixeles del overlay fuera de la caja del numero.
  - EN LA RAMPA DEL DECAIMIENTO. `decaimiento`, la del overlay, indices 1-5.
    La cura no tiene bando: la da cualquiera que le envie Energia, y lo que
    se ve es la infeccion yendose, no quien la cura (eso ya lo dicen
    `ART-FX-ABS` o `ART-FX-DMG`, que suenan en el mismo impacto).
  - EL NUMERO, LIBRE. El miasma cae hacia abajo y sus gotas cruzan el
    numero; el efecto no, nunca: se recorta contra la caja del numero de su
    talla, como el deshielo. Por eso hay una fila por talla.
  - SE APAGA. El ultimo frame es el de menos luz y no pasa de la cuarta
    parte del pico.

LA TESIS DE LA v1: EL IMPACTO EXPULSA LA INFECCION. La Energia que llega
empuja el miasma hacia fuera: la nube se aleja del cuerpo, se aclara y se
deshace en el aire, y las gotas caen deprisa y se apagan.

  - EL EMPUJON (frame 0). La nube del frame del bucle, mas densa y dos
    bandas mas clara: la Energia que entra la enciende.
  - SE ALEJA (frames 1-3). Sale 4, 8 y 12 px, cada vez mas rala.
  - SE VA (frame 4). Unas motas al borde.

    py tools/gen_fx_cure_v1.py                  hoja, comprobaciones y vistas fijas
    py tools/gen_fx_cure_v1.py --gif            y las animadas
    py tools/gen_fx_cure_v1.py --comparativa    las tres propuestas lado a lado
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
import gen_state_decay_v2 as miasma
from gen_fx_send_v1 import ANG, BAYER, C, CELDA, DX, DY, R, a_rgba, borde, luz, tramar
from gen_fx_thaw_v1 import Talla

ASSET      = 'cure'
VERSION    = 1
NOMBRE     = 'La purga'

FRAMES     = 5                      # ceil(0.4 * 12)
FPS        = 12
VARIANTES  = miasma.FRAMES          # 16, una por frame del bucle
TALLAS     = sel.TALLAS
RAMPA      = 'decaimiento'
RESTO      = 0.25
CONTINUIDAD = 0.6


# --- Lo que comparten las propuestas -------------------------------------------

_HOJA = {}


def overlay(v):
    """El frame v del miasma real, leido de su hoja y pasado a indices."""
    if not _HOJA:
        im = np.asarray(Image.open(os.path.join(RAIZ, 'art', 'states', 'decay', 'v2',
                                                'state_decay_sheet.png')).convert('RGBA'))
        ramp = rampa(RAMPA)
        for k in range(VARIANTES):
            c = im[:, k * CELDA:(k + 1) * CELDA]
            idx = np.zeros((CELDA, CELDA), np.int8)
            for i in range(1, 6):
                idx[(c[..., 3] > 0) & np.all(c[..., :3] == ramp[i], axis=-1)] = i
            assert ((c[..., 3] > 0) == (idx > 0)).all(), 'el miasma tiene colores fuera de la rampa'
            _HOJA[k] = idx
    return _HOJA[v]


def campo(t, dr=0.0, k=1.0):
    """La nube de «El miasma» en el instante t, con el radio movido dr px y
    la densidad por k. La misma formula que `gen_state_decay_v2.campo`."""
    m = miasma
    rm = m.R_MEDIO + m.R_PULSO * np.sin(2 * np.pi * t) + dr
    avance = np.clip((R - (rm - m.GROSOR)) / (2 * m.GROSOR), 0.0, 1.6)
    a = ANG - m.CIZALLA * avance
    onda = sum(w * np.cos(n * a + f) for n, w, f in m.ONDA)
    onda = 0.55 + 0.45 * np.clip(onda, -1.0, 1.0)
    sesgo = m.SESGO_ABAJO + (1.0 - m.SESGO_ABAJO) * np.clip(np.sin(ANG), 0.0, 1.0)
    radial = np.clip(1.0 - np.abs(R - rm) / m.GROSOR, 0.0, 1.0) ** m.GAMMA
    pulso = 1.0 + m.DENS_PULSO * np.sin(2 * np.pi * t + 1.0)
    return radial * onda * sesgo * m.DENS * pulso * k


def gotas(t):
    """Las cinco gotas del miasma en el instante t: (x, y, u)."""
    n = len(miasma.GOTAS)
    out = []
    for i, (gx, deriva, _) in enumerate(miasma.GOTAS):
        u = (t + i / n) % 1.0
        out.append((gx + deriva * u, miasma.GOTA_Y0 + (miasma.GOTA_Y1 - miasma.GOTA_Y0) * u, u))
    return out


def comprobar(celdas, t, version):
    fallos = []
    tt = Talla(t)
    for v in range(VARIANTES):
        frames = celdas[v]
        for f, a in enumerate(frames):
            on = a > 0
            if not set(np.unique(a)) <= {0, 1, 2, 3, 4, 5}:
                fallos.append(f'v{v} f{f}: indice fuera de la rampa [1..5]')
            if (on & tt.franja).any():
                fallos.append(f'v{v} f{f}: pisa la caja del numero')
            if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
                fallos.append(f'v{v} f{f}: toca el borde de la celda')
            if not on.any():
                fallos.append(f'v{v} f{f}: vacio')
        ref = (overlay(v) > 0) & tt.libre
        sigue = (frames[0] > 0)[ref].mean()
        if sigue < CONTINUIDAD:
            fallos.append(f'v{v}: el frame 0 no arranca del miasma ({sigue:.0%})')
        l = [luz(a) for a in frames]
        if l[-1] != min(l) or l[-1] > RESTO * max(l):
            fallos.append(f'v{v}: no se apaga: luz {l}')
        if len({a.tobytes() for a in frames}) != FRAMES:
            fallos.append(f'v{v}: frames repetidos')
    if len({celdas[v][0].tobytes() for v in range(VARIANTES)}) != VARIANTES:
        fallos.append('variantes repetidas')
    if fallos:
        raise SystemExit(f'ART-FX-CURE v{version} (talla {t}) no pasa:\n  '
                         + '\n  '.join(fallos[:12]))


def generar(dibujar, version):
    todas = {}
    for t in TALLAS:
        tt = Talla(t)
        celdas = {v: [dibujar(tt, v, f).astype(np.int8) for f in range(FRAMES)]
                  for v in range(VARIANTES)}
        comprobar(celdas, t, version)
        todas[t] = celdas
    return todas


def hoja(todas):
    ramp = rampa(RAMPA)
    return np.vstack([np.hstack([a_rgba(a, ramp) for v in range(VARIANTES) for a in todas[t][v]])
                      for t in TALLAS])


def exporta(destino, img):
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_cure_sheet.png'))
    with open(os.path.join(destino, 'fx_cure_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'fps': FPS, 'variants': VARIANTES,
                   'sizes': list(TALLAS), 'rows': [f'cure_{t}' for t in TALLAS],
                   'columns': 'variante 0 frames 0-4, variante 1 frames 0-4, ...',
                   'note': 'sobre el nodo curado, en el tick en que desaparece el overlay '
                           'state_decay_sheet; la variante, el frame en que estaba su bucle de 16; '
                           'la talla, la de world_select_tallas.json por la huella del nodo; una '
                           'sola rampa (decaimiento), sin bando; pivote en el centro'},
                  f, indent=1)


# --- Vistas --------------------------------------------------------------------

BG, ampliar, sobre, abrir = shot.BG, shot.ampliar, shot.sobre, shot.abrir
nodo, numero, huellas = abs_.nodo, abs_.numero, abs_.huellas
MUESTRA = (('player', 'str', 1, 0), ('enemy', 'pul', 1, 5), ('neutral', 'mag', 2, 9),
           ('enemy', 'str', 3, 3), ('player', 'pul', 3, 12), ('player', 'mag', 5, 7))


def miasma_rgba(v):
    return a_rgba(overlay(v), rampa(RAMPA))


def efecto(todas, pre, n, v, f):
    t = sel.talla_de(huellas()[pre, n][2])
    return a_rgba(todas[t][v][f], rampa(RAMPA))


def vista_hoja(todas, muestra=MUESTRA):
    """Por nodo: infectado, con el miasma en el frame v, y los cinco frames
    de la variante v."""
    filas = []
    for fac, pre, n, v in muestra:
        fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
        fila[:] = BG
        numero(fila, C, C, pre, n)
        sobre(fila, nodo(pre, n, fac), C, C)
        sobre(fila, miasma_rgba(v), C, C)
        for f in range(FRAMES):
            x = (f + 1) * CELDA + C
            numero(fila, x, C, pre, n)
            sobre(fila, nodo(pre, n, fac), x, C)
            sobre(fila, efecto(todas, pre, n, v, f), x, C)
        filas.append(fila)
    return np.vstack(filas)


# Tres nodos infectados, con el miasma respirando, se curan uno tras otro: la
# variante es el frame en que estaba su bucle.
ESCENA = (('player', 'str', 2, (90, 95), 5), ('enemy', 'mag', 3, (220, 115), 14),
          ('player', 'pul', 2, (350, 95), 23))


def vista_tablero(todas, k):
    tab = np.empty((220, 440, 3), np.uint8)
    tab[:] = BG
    for fac, pre, n, (x, y), k0 in ESCENA:
        numero(tab, x, y, pre, n)
        sobre(tab, nodo(pre, n, fac), x, y)
        if k < k0:
            sobre(tab, miasma_rgba(k % VARIANTES), x, y)
        elif k - k0 < FRAMES:
            sobre(tab, efecto(todas, pre, n, k0 % VARIANTES, k - k0), x, y)
    return tab


def vistas(todas, version, gif):
    d = os.path.join(dir_preview('fx_cure'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(vista_hoja(todas), 3)).save(os.path.join(d, 'fx_cure_hoja_x3.png'))
    if gif:
        esc = [vista_tablero(todas, k) for k in range(34)]
        for nombre, ms in (('fx_cure_tablero_x2.gif', 1000 / FPS),
                           ('fx_cure_tablero_x2_lento.gif', 4000 / FPS)):
            im = [Image.fromarray(ampliar(e, 2)) for e in esc]
            im[0].save(os.path.join(d, nombre), save_all=True, append_images=im[1:],
                       duration=round(ms), loop=0)
    print('vistas en', d)


def comparativa():
    import importlib
    muestra = (('player', 'str', 1, 0), ('neutral', 'mag', 2, 9), ('enemy', 'str', 4, 4))
    bloques = []
    for v in (1, 2, 3):
        mod = importlib.import_module(f'gen_fx_cure_v{v}')
        bloques.append(vista_hoja(generar(mod.dibujar, v), muestra))
        sep = np.empty((6, CELDA * (FRAMES + 1), 3), np.uint8)
        sep[:] = (60, 66, 90)
        bloques.append(sep)
    ruta = os.path.join(dir_preview('fx_cure'), 'comparativa_x3.png')
    Image.fromarray(ampliar(np.vstack(bloques[:-1]), 3)).save(ruta)
    print('comparativa en', ruta)


def ejecutar(dibujar, version, nombre, gif):
    destino = os.path.join(RAIZ, 'art', 'fx', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    todas = generar(dibujar, version)
    img = hoja(todas)
    exporta(destino, img)
    print(f'ART-FX-CURE v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in TALLAS:
        c = todas[t][0]
        sigue = min((todas[t][v][0] > 0)[(overlay(v) > 0) & Talla(t).libre].mean()
                    for v in range(VARIANTES))
        print(f'  talla {t}  v0 px {[int((a > 0).sum()) for a in c]}  '
              f'luz {[luz(a) for a in c]}  miasma en f0 >= {sigue:.0%}')
    vistas(todas, version, gif)


# --- La v1: «La purga» ---------------------------------------------------------

SALE   = (0, 4, 8, 12, 15)
DENS   = (1.5, 1.0, 0.6, 0.32, 0.13)
BANDAS = ((2, 3), (2, 4), (3, 4), (4, 5), (5, 5))
DENTRO = R < 44                              # que la nube que sale no toque el borde


def dibujar(tt, v, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    t = v / VARIANTES
    for x, y, u in gotas(t):
        if f >= 3:
            break
        yy = y + (0, 4, 9)[f]
        tt.punto(idx, x, yy, (2, 3, 4)[f] + (u > 0.6))
        tt.punto(idx, x, yy - 1, min((3, 4, 5)[f] + (u > 0.6), 5), solo_vacio=True)
    dens = campo(t, SALE[f], DENS[f]) * tt.libre * DENTRO
    b_in, b_out = BANDAS[f]
    tramar(idx, dens, b_in, b_out, corte=0.24 * max(DENS[f], 0.5))
    return idx


if __name__ == '__main__':
    if '--comparativa' in sys.argv:
        comparativa()
    else:
        ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
