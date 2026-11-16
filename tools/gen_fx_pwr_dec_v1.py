"""NOVA - ART-FX-PWR-DEC v1, el impacto de Decaimiento: «La camara de niebla».

Ref: NOVA_Assets_Diseno.md A.5, A.3.5 y A.4.2 · NOVA_Estados_Animaciones.md
14.5 · Spec 7.4

El momento en que el haz de Decaimiento (`ART-WLD-BEAM-DEC`, «La cadena de
desintegracion») termina sobre su objetivo y el nodo queda `Infectado`. 9
frames, 0.7 s; al acabar, el overlay «El miasma» (`ART-ST-DECAY` v2) toma el
relevo en su frame 0 y sigue en bucle. Una sola hoja,
`art/fx/pwr_dec/v<n>/fx_pwr_dec_sheet.png`, en celdas de 96:

    filas      5   talla 24, 28, 32, 36, 40 (la de «La orbita» del nodo)
    columnas   9   los frames

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - ACABA EN EL MIASMA, RESPIRANDO. Es la cura al reves (`ART-FX-CURE`
    arranca del overlay; este acaba en el). El ultimo frame ES el frame 15
    del bucle de 16, recortado contra la caja del numero: en el frame
    siguiente el overlay entra por su frame 0 y la nube sigue respirando sin
    salto. El generador lo exige pixel a pixel. Los frames de antes llegan
    por el 13 y el 14 del bucle, asi que la nube ya respira al formarse.
  - EN LA RAMPA DEL DECAIMIENTO, SIN BANDO. `decaimiento`, la del haz y la
    del overlay, indices 1-5; el 0 es el blanco del FX-CAP. El objetivo
    puede ser de cualquiera, tambien el Magnetar neutral.
  - EL NUMERO, LIBRE, con la caja del numero de su talla (la `franja` de
    `ART-FX-THAW`): una fila por talla. El numero es lo que el jugador va a
    mirar caer a 2 por segundo.
  - SIN DIRECCION. El haz llega desde cualquier angulo y la hoja no gira.
  - PRIMERO LO FUERTE. Algun frame llega al indice 1 antes de asentarse.

LA TESIS DE LA v1: LA RADIACION SE VE EN UNA CAMARA DE NIEBLA. Una camara de
Wilson es aire sobresaturado: cada particula que la cruza deja una estela de
gotitas condensadas, y asi se vio la radiacion por primera vez. El impacto
suelta las particulas de la desintegracion -alfas cortas, gruesas y rectas;
betas largas, finas y en espiral, como en un campo magnetico- y sus estelas
de gotas son lo que despues pesa, cae y se queda flotando: el miasma.

  - EL NUCLEO (frame 0). Un destello en el centro con ocho rayos cortos.
  - LAS TRAZAS (frames 1-4). Las particulas salen dejando gotas; la cabeza
    en el indice 1 y la estela mas apagada cuanto mas vieja.
  - SE CONDENSAN (frames 5-7). Las gotas engordan, caen y se apagan, y entre
    ellas aparece la nube del miasma.
  - EL MIASMA (frame 8). El frame 15 del bucle.

    py tools/gen_fx_pwr_dec_v1.py                  hoja, comprobaciones y vistas fijas
    py tools/gen_fx_pwr_dec_v1.py --gif            y las animadas
    py tools/gen_fx_pwr_dec_v1.py --comparativa    las tres propuestas lado a lado
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
import gen_fx_cure_v1 as cure
from gen_fx_send_v1 import BAYER, R
from gen_fx_thaw_v1 import (BG, C, CELDA, Talla, a_rgba, ampliar, huellas, nodo, numero,
                            sobre, luz)

ASSET     = 'pwr_dec'
VERSION   = 1
NOMBRE    = 'La camara de niebla'

FRAMES    = 9                       # ceil(0.7 * 12)
FPS       = 12
TALLAS    = sel.TALLAS
RAMPA     = 'decaimiento'
BUCLE     = cure.VARIANTES          # 16 frames del miasma
ULTIMO    = BUCLE - 1               # el frame del bucle que precede al 0
DENTRO    = R < 43.5                # nada llega al borde de la celda

RUIDO = np.random.default_rng(71).random((CELDA, CELDA))


# --- Lo que comparten las propuestas -------------------------------------------

def miasma(v, tt):
    """El frame v del bucle del miasma, sin la caja del numero."""
    return np.where(tt.libre, cure.overlay(v % BUCLE), 0).astype(np.int8)


def final(tt):
    return miasma(ULTIMO, tt)


def parcial(v, tt, s, banda=None):
    """El frame v del miasma con solo una fraccion s de sus pixeles, al azar
    pero fija (los que aparecen ya no se van), o todos en `banda`."""
    o = miasma(v, tt)
    m = (o > 0) & (RUIDO < s)
    out = np.zeros_like(o)
    out[m] = o[m] if banda is None else banda
    return out


def pinta(idx, m, banda):
    """Enciende m en `banda` salvo donde ya hay algo mas claro."""
    m = m & DENTRO
    idx[m & ((idx == 0) | (idx > banda))] = banda


def punto(idx, tt, x, y, banda):
    ix, iy = int(math.floor(x)), int(math.floor(y))
    if 0 <= ix < CELDA and 0 <= iy < CELDA and tt.libre[iy, ix] and DENTRO[iy, ix]:
        if idx[iy, ix] == 0 or idx[iy, ix] > banda:
            idx[iy, ix] = banda


def debajo(idx, capa):
    """Pone `capa` donde idx esta vacio: lo que ya hay manda."""
    m = (idx == 0) & (capa > 0)
    idx[m] = capa[m]


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
        fallos.append('el ultimo frame no es el miasma')
    if not any((a == 1).any() for a in frames[:-1]):
        fallos.append('ningun frame llega al indice 1')
    if len({a.tobytes() for a in frames}) != FRAMES:
        fallos.append('frames repetidos')
    if fallos:
        raise SystemExit(f'ART-FX-PWR-DEC v{version} (talla {t}) no pasa:\n  '
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
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_pwr_dec_sheet.png'))
    with open(os.path.join(destino, 'fx_pwr_dec_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'fps': FPS, 'sizes': list(TALLAS),
                   'rows': [f'pwr_dec_{t}' for t in TALLAS],
                   'note': 'sobre el objetivo del haz de Decaimiento, al terminar el haz; el '
                           'ultimo frame es el frame 15 de state_decay_sheet sin la caja del '
                           'numero, y en el siguiente entra el overlay por su frame 0; la talla, '
                           'la de world_select_tallas.json por la huella del nodo; una sola rampa '
                           '(decaimiento), sin bando'},
                  f, indent=1)


# --- Vistas --------------------------------------------------------------------

MUESTRA = (('player', 'str', 1), ('enemy', 'pul', 1), ('neutral', 'mag', 2),
           ('enemy', 'str', 3), ('player', 'pul', 3), ('neutral', 'mag', 5))


def efecto(celdas, pre, n, f):
    t = sel.talla_de(huellas()[pre, n][2])
    return a_rgba(celdas[t][f], rampa(RAMPA))


def vista_hoja(celdas, muestra=MUESTRA):
    """Por nodo: los nueve frames y, al final, el nodo ya infectado con el
    overlay en su frame 0, para ver que el relevo no salta."""
    filas = []
    for fac, pre, n in muestra:
        fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
        fila[:] = BG
        for i in range(FRAMES + 1):
            x = i * CELDA + C
            numero(fila, x, C, pre, n)
            sobre(fila, nodo(pre, n, fac), x, C)
            sobre(fila, efecto(celdas, pre, n, i) if i < FRAMES else cure.miasma_rgba(0), x, C)
        filas.append(fila)
    return np.vstack(filas)


# Tres nodos reciben el Decaimiento uno tras otro y se quedan infectados.
ESCENA = (('enemy', 'str', 2, (90, 95), 3), ('neutral', 'mag', 3, (220, 115), 10),
          ('player', 'pul', 2, (350, 95), 17))


def vista_tablero(celdas, k):
    tab = np.empty((220, 440, 3), np.uint8)
    tab[:] = BG
    for fac, pre, n, (x, y), k0 in ESCENA:
        numero(tab, x, y, pre, n)
        sobre(tab, nodo(pre, n, fac), x, y)
        if k0 <= k < k0 + FRAMES:
            sobre(tab, efecto(celdas, pre, n, k - k0), x, y)
        elif k >= k0 + FRAMES:
            sobre(tab, cure.miasma_rgba((k - k0 - FRAMES) % BUCLE), x, y)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_pwr_dec'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(vista_hoja(celdas), 3)).save(os.path.join(d, 'fx_pwr_dec_hoja_x3.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(44)]
        for nombre, ms in (('fx_pwr_dec_tablero_x2.gif', 1000 / FPS),
                           ('fx_pwr_dec_tablero_x2_lento.gif', 4000 / FPS)):
            im = [Image.fromarray(ampliar(e, 2)) for e in esc]
            im[0].save(os.path.join(d, nombre), save_all=True, append_images=im[1:],
                       duration=round(ms), loop=0)
    print('vistas en', d)


def comparativa():
    import importlib
    muestra = (('player', 'str', 1), ('neutral', 'mag', 2), ('enemy', 'str', 4))
    bloques = []
    for v in (1, 2, 3):
        mod = importlib.import_module(f'gen_fx_pwr_dec_v{v}')
        bloques.append(vista_hoja(generar(mod.dibujar, v), muestra))
        sep = np.empty((6, CELDA * (FRAMES + 1), 3), np.uint8)
        sep[:] = (60, 66, 90)
        bloques.append(sep)
    ruta = os.path.join(dir_preview('fx_pwr_dec'), 'comparativa_x3.png')
    Image.fromarray(ampliar(np.vstack(bloques[:-1]), 3)).save(ruta)
    print('comparativa en', ruta)


def ejecutar(dibujar, version, nombre, gif):
    destino = os.path.join(RAIZ, 'art', 'fx', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    celdas = generar(dibujar, version)
    img = hoja(celdas)
    exporta(destino, img)
    print(f'ART-FX-PWR-DEC v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in TALLAS:
        print(f'  talla {t}  px {[int((a > 0).sum()) for a in celdas[t]]}  '
              f'luz {[luz(a) for a in celdas[t]]}')
    vistas(celdas, version, gif)


# --- La v1: «La camara de niebla» ----------------------------------------------

PASO  = 0.4                         # px por paso al integrar una traza


def _trazas():
    """Las particulas: (puntos de su recorrido, beta, frame en que llega al
    final). Alfas cortas y rectas; betas largas que salen rectas y se
    enroscan al final, al perder energia, como en un campo magnetico."""
    rng = np.random.default_rng(17)
    out = []
    n = 9
    base = rng.random() * 2 * math.pi
    for i in range(n):
        a = base + 2 * math.pi * i / n + rng.uniform(-0.25, 0.25)
        beta = i % 3 != 0
        largo = rng.uniform(44, 52) if beta else rng.uniform(21, 25)
        kmax = (1 if i % 2 else -1) * rng.uniform(0.22, 0.3)
        pts = []
        x, y, th, s = C + 3 * math.cos(a), C + 3 * math.sin(a), a, 0.0
        while s < largo:
            pts.append((x, y, s))
            if beta:
                th += kmax * (s / largo) ** 3 * PASO
            x += PASO * math.cos(th)
            y += PASO * math.sin(th)
            s += PASO
            if math.hypot(x - C, y - C) > 42.5:
                break
        out.append((pts, beta, 4 if beta else 2))
    return out


TRAZAS = _trazas()


def _gotas():
    """Las gotas que deja cada traza: (x, y, s relativo, beta). Una cada 2 px
    en las betas y cada 1.3 en las alfas, que son mas densas."""
    rng = np.random.default_rng(5)
    out = []
    for k, (pts, beta, fin) in enumerate(TRAZAS):
        sep = 2.0 if beta else 1.3
        prox = 0.0
        L = pts[-1][2]
        for x, y, s in pts:
            if s >= prox:
                j = rng.uniform(-0.45, 0.45, 2)
                out.append((x + j[0], y + j[1], s / L, beta, fin, k, rng.random()))
                prox += sep * rng.uniform(0.8, 1.2)
    return out


GOTAS = _gotas()


def _nucleo(idx, tt):
    for dy in (-2, -1, 0, 1):
        for dx in (-2, -1, 0, 1):
            if abs(dx + 0.5) + abs(dy + 0.5) < 3:
                punto(idx, tt, C + dx, C + dy, 1)
    for k in range(8):
        a = math.pi / 8 + k * math.pi / 4
        for r in ((3, 4, 5, 6, 7, 8) if k % 2 else (3, 4, 5)):
            punto(idx, tt, C + r * math.cos(a), C + r * math.sin(a), 1 if r < 5 else 2)
            punto(idx, tt, C + r * math.cos(a) + 1, C + r * math.sin(a) + 1, 5)


def dibujar(tt, f):
    if f == FRAMES - 1:
        return final(tt)
    idx = np.zeros((CELDA, CELDA), np.int8)
    if f == 0:
        _nucleo(idx, tt)
        return idx

    if f <= 4:
        # la cabeza de cada traza avanza; la estela se apaga con la edad
        for x, y, u, beta, fin, _, _ in GOTAS:
            cabeza = min(f / fin, 1.0)
            if u > cabeza:
                continue
            nace = u * fin                  # el frame (fraccionario) en que se dejo
            edad = f - nace
            if cabeza < 1.0 and cabeza - u < 0.09:
                banda = 1
            else:
                banda = 2 if edad < 1 else (3 if edad < 2.2 else 4)
            punto(idx, tt, x, y, banda)
            if not beta:                    # las alfas son gruesas
                punto(idx, tt, x + 0.8, y + 0.6, min(banda + 1, 5))
        for x, y, u, beta, fin, _, _ in GOTAS:
            if u <= min(f / fin, 1.0):      # sombra: se lee sobre cuerpos claros
                ix, iy = int(x + 1), int(y + 1)
                if 0 <= ix < CELDA and 0 <= iy < CELDA and idx[iy, ix] == 0:
                    punto(idx, tt, x + 1, y + 1, 5)
        if f == 1:
            punto(idx, tt, C, C, 2)
        return idx

    # se condensan: las gotas engordan, caen y se apagan; entre ellas, la nube
    k = f - 5                               # 0, 1, 2
    for x, y, u, beta, fin, tr, azar in GOTAS:
        if azar > (0.95, 0.75, 0.5)[k]:
            continue
        caida = (1.0, 3.0, 6.0)[k] * (0.6 + 0.8 * azar)
        yy = y + caida
        b = (3, 3, 4)[k] + (u > 0.75)
        punto(idx, tt, x, yy, min(b, 5))
        if k < 2:
            punto(idx, tt, x, yy - 1, min(b + 1, 5))
            if azar < 0.4:
                punto(idx, tt, x + 1, yy, min(b + 1, 5))
    debajo(idx, parcial(ULTIMO - 3 + k, tt, (0.3, 0.55, 0.8)[k]))
    return idx


if __name__ == '__main__':
    if '--comparativa' in sys.argv:
        comparativa()
    else:
        ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
