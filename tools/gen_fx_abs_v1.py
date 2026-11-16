"""NOVA - ART-FX-ABS v1, la absorcion aliada: «La resaca».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5 · Spec 6.2

Lo que hace el nodo que recibe un proton de su propio bando
(`ImpactResult.Absorbed`, 6.2): 4 frames, 0.3 s. Una sola hoja,
`art/fx/abs/v<n>/fx_abs_sheet.png`, en celdas de 96:

    filas     10   player_24 ... player_40, enemy_24 ... enemy_40
    columnas   4   los frames

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan). Casi
todo es lo de `ART-FX-SEND` («La onda», `gen_fx_send_v1.py`), del que se
toman las tallas, la franja del numero, el color y la hoja:

  - QUIEN ABSORBE. Cualquier nodo del Jugador o del Enemigo, tambien un
    Asteroide propio, que recibe Energia para encenderse. Su huella (43 px)
    cae en la talla 28 de «La orbita».
  - EL GESTO CONTRARIO A «LA ONDA». El envio es algo que sale; la absorcion
    es algo que entra. El generador lo exige: ningun frame ocupa mas que el
    anterior (se mide el radio de lo encendido) y el ultimo acaba pegado al
    cuerpo, a 3 px como mucho.
  - LIGERO. Un refuerzo llega en rafagas: un lazo de seis nodos manda seis
    protones a la misma Estrella, que absorbe seis veces en un segundo.
  - EL NUMERO Y EL CUERPO, LIBRES, como en el envio. Y en el color del
    proton que llega, que es el del nodo: `plasma_p` y `plasma_e`.

LA TESIS DE LA v1: LA ONDA AL REVES. La resaca: el aro que «La onda» echaba
fuera vuelve, de lejos hacia el cuerpo, y se enciende al tocarlo.

  - LLEGA (frame 0). Un aro de trama rala a 10 px del cuerpo.
  - SE CIERRA (frames 1-2). A 7 y a 4 px, ya solido, una banda mas claro
    cada vez, roto por arriba y entero al final.
  - TOCA (frame 3). Pegado al cuerpo en el indice 1, con un velo por dentro.

    py tools/gen_fx_abs_v1.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_abs_v1.py --gif    y las animadas
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
import gen_fx_send_v1 as send
from gen_fx_send_v1 import (ANG, C, CELDA, FACCIONES, FRANJA, LIBRE, R, TALLAS,
                            a_rgba, borde, luz, tramar)

ASSET     = 'abs'
VERSION   = 1
NOMBRE    = 'La resaca'

FRAMES    = 4                       # ceil(0.3 * 12)
FPS       = 12
PEGADO    = 3.0                     # el ultimo frame acaba a esto del cuerpo
TOLERANCIA = 0.5                    # px que un frame puede pasar al anterior


# --- Lo que comparten las propuestas -------------------------------------------

def extension(a):
    on = a > 0
    return float(R[on].max()) if on.any() else 0.0


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
    ext = [extension(a) for a in frames]
    # medio pixel de tolerancia: un aro de 1 px a radio fijo no enciende los
    # mismos pixeles en todos los frames, y eso no es crecer
    if not all(x >= y - TOLERANCIA for x, y in zip(ext, ext[1:])):
        fallos.append(f'no entra: radios {[round(e, 1) for e in ext]}')
    if ext[-1] > Rb + PEGADO:
        fallos.append(f'el ultimo frame no acaba en el cuerpo ({ext[-1]:.1f} > {Rb + PEGADO})')
    if not (frames[-1] == 1).any():
        fallos.append('el ultimo frame no llega al indice 1: la llegada es lo mas claro')
    if len({a.tobytes() for a in frames}) != FRAMES:
        fallos.append('frames repetidos')
    if fallos:
        raise SystemExit(f'ART-FX-ABS v{version} (talla {talla}) no pasa:\n  '
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
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_abs_sheet.png'))
    with open(os.path.join(destino, 'fx_abs_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'fps': FPS,
                   'sizes': list(TALLAS), 'air': send.AIRE,
                   'rows': [f'{fac}_{t}' for fac in FACCIONES for t in TALLAS],
                   'note': 'la talla es la de world_select_tallas.json (talla_de por la huella '
                           'del nodo, tambien el Asteroide); pivote en el centro del nodo destino'},
                  f, indent=1)


# --- Vistas --------------------------------------------------------------------

BG, ampliar, sobre, abrir = shot.BG, shot.ampliar, shot.sobre, shot.abrir


def huellas():
    out = dict(send.huellas())
    with open(os.path.join(RAIZ, 'art', 'nodes', 'asteroide', 'v1', 'ast_huella.json')) as f:
        h = json.load(f)
    out['ast', 0] = ('asteroide', 1, h['footprintPixels'][0])
    return out


def nodo(pre, n, fac):
    carpeta, version, _ = huellas()[pre, n]
    return abrir('nodes', carpeta, f'v{version}', f'{pre}_lvl{n}_{fac}.png')


def efecto(celdas, fac, pre, n, f):
    t = sel.talla_de(huellas()[pre, n][2])
    return a_rgba(celdas[t][f], rampa(FACCIONES[fac]))


def numero(tab, x, y, pre, n):
    hf = (huellas()[pre, n][2] + 1) // 2
    y0 = int(y) + hf + 2
    tab[y0:y0 + 20, int(x) - 11:int(x) + 11] = (60, 66, 90)


MUESTRA = (('str', 1), ('ast', 0), ('str', 3), ('mag', 4), ('str', 5))


def vista_hoja(celdas):
    """x3. Un nodo de cada talla por bando: el nodo solo y los cuatro frames,
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


# Un refuerzo en rafaga: dos nodos del Jugador mandan protones a su Estrella
# N4, uno detras de otro, y el Enemigo refuerza su Asteroide.
DESTINOS = {'player': ('str', 4, (330, 110)), 'enemy': ('ast', 0, (110, 60))}
ENVIOS = (('player', ('str', 2, (90, 180)), 18, 0), ('player', ('mag', 3, (110, 60)), 11, 3),
          ('player', ('str', 2, (90, 180)), 9, 8), ('enemy', ('str', 2, (330, 30)), 7, 2))


def vista_tablero(celdas, k):
    tab = np.empty((220, 420, 3), np.uint8)
    tab[:] = BG
    nodos = {DESTINOS['player'][2]: ('player',) + DESTINOS['player'][:2],
             (90, 180): ('player', 'str', 2), (330, 30): ('enemy', 'str', 2)}
    # el Magnetar del Jugador y el Asteroide del Enemigo no pueden compartir sitio
    nodos[(110, 60)] = ('enemy', 'ast', 0)
    for (x, y), (fac, pre, n) in nodos.items():
        numero(tab, x, y, pre, n)
        sobre(tab, nodo(pre, n, fac), x, y)
    for fac, (opre, on, (ox, oy)), carga, k0 in ENVIOS:
        dpre, dn, (dx, dy) = DESTINOS[fac]
        if (ox, oy) == (dx, dy):
            continue
        hf = (huellas()[dpre, dn][2] + 1) // 2
        largo = math.hypot(dx - ox, dy - oy) - hf
        kk = k - k0
        s = kk * shot.PASO
        if 0 <= s < largo:
            sobre(tab, shot.proton(fac, carga, kk), ox + (dx - ox) * s / (largo + hf),
                  oy + (dy - oy) * s / (largo + hf))
        else:
            llega = math.ceil(largo / shot.PASO)
            fx = kk - llega
            if 0 <= fx < FRAMES:
                sobre(tab, efecto(celdas, fac, dpre, dn, fx), dx, dy)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_abs'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(vista_hoja(celdas)).save(os.path.join(d, 'fx_abs_hoja_x3.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(36)]
        for nombre, ms in (('fx_abs_tablero_x2.gif', 1000 / FPS),
                           ('fx_abs_tablero_x2_lento.gif', 4000 / FPS)):
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
    print(f'ART-FX-ABS v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in TALLAS:
        print(f'  talla {t}  px {[int((a > 0).sum()) for a in celdas[t]]}  '
              f'radio {[round(extension(a), 1) for a in celdas[t]]}')
    vistas(celdas, version, gif)


# --- La v1: «La resaca» -------------------------------------------------------------

DISTANCIAS = (10.0, 7.0, 4.0, 1.5)     # del cuerpo, por frame


def _rotura():
    return (np.sin(5 * ANG + 0.7) + 0.6 * np.sin(9 * ANG + 2.1)) / 1.6


def dibujar(t, f):
    Rb = borde(t)
    idx = np.zeros((CELDA, CELDA), np.int8)
    r = Rb + DISTANCIAS[f]
    aro = (R >= r - 0.5) & (R < r + 0.5) & LIBRE
    if f == 0:
        dens = np.clip(1 - np.abs(R - r) / 1.5, 0, 1) * 0.45
        tramar(idx, dens * LIBRE, 3, 4, corte=0.4)
    elif f < 3:
        idx[aro & (_rotura() > (-0.2 if f == 1 else -0.7))] = 4 - f
    else:
        idx[aro] = 1
        d = r - 0.5 - R
        dens = np.where((d > 0) & (R >= Rb - 1), np.clip(1 - d / 1.5, 0, 1) * 0.6, 0)
        tramar(idx, dens * LIBRE, 2, 3, corte=0.4)
    return idx


if __name__ == '__main__':
    ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
