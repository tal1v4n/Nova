"""NOVA - ART-FX-KILL v1, el proton destruido: «El colapso».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.3 y 14.5 · Spec 6.3

Lo que queda del proton cuando un Magnetar lo deja a 0: «implosion corta, no
deja nada» (14.3). 3 frames, 0.25 s. Una sola hoja,
`art/fx/kill/v<n>/fx_kill_sheet.png`, en celdas de 48 como el proton:

    filas      2   player, enemy
    columnas  48   variante 0 frame 0, 1, 2, variante 1 frame 0 ...

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - SIEMPRE EN LA TALLA 1. Un proton solo muere por un disparo del Magnetar
    (6.3: -2 por disparo), asi que muere con 1 o 2 de Carga: la talla 1 de
    «La bolsa», la de 23 px. Una fila por bando y ninguna por talla.
  - SUSTITUYE AL PROTON, Y POR ESO TIENE 16 VARIANTES. El sprite del proton
    desaparece en el tick del disparo y en su sitio empieza este efecto. La
    variante v arranca con los quarks donde estaban en el frame v del bucle
    de 16: `VFXPool` toma la del frame en que murio y no hay salto.
  - IMPLOSION DE VERDAD. Cada frame ocupa menos que el anterior y el primero
    no pasa de la bolsa: el generador mide el radio de lo encendido y falla
    si crece. El ultimo deja como mucho 12 px: no deja nada.
  - EN EL COLOR DEL PROTON: `plasma_p` y `plasma_e`, con la luz global y el
    contorno de los quarks, que son los de «La bolsa». La luz de la
    implosion no llega al blanco puro, que es de `FX-CAP`.
  - VA CON EL ARCO. Muere en el tick del disparo, asi que el arco de «El
    chispazo» le llega en los dos primeros frames, y no lleva `FX-SHOT`:
    el destello es para el que sobrevive.

LA TESIS DE LA v1: LA BOLSA SE CIERRA SOBRE SI MISMA. Sin Carga no hay nada
que la hinche, y la membrana cae hacia dentro arrastrando a los quarks.

  - CAIDA (frame 0). La membrana es ya un aro solido a dos tercios de su
    radio, encendido por la tension, y los quarks, apretados contra el.
  - NUCLEO (frame 1). Todo cabe en un punto caliente de 5 px con cuatro
    trazos cortos que apuntan hacia dentro: lo que aun esta cayendo.
  - NADA (frame 2). Una mota de 2 x 2 que se apaga.

    py tools/gen_fx_kill_v1.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_kill_v1.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import (CONTORNO, RAIZ, a_rgba, dir_preview, esfera, lienzo,
                       rampa, tramar)
import gen_proton_v2 as pro
import gen_fx_shot_v1 as shot

ASSET     = 'kill'
VERSION   = 1
NOMBRE    = 'El colapso'

CELDA     = pro.CELDA          # 48, la del proton
C         = CELDA / 2
FRAMES    = 3                  # ceil(0.25 * 12)
VARIANTES = pro.FRAMES         # una por frame del bucle del proton
TALLA     = 1
P         = pro.TALLAS[TALLA]
RE        = pro.radio_bolsa_px(TALLA)
MAX_FIN   = 12                 # px que puede dejar el ultimo frame
FPS       = 12

FACCIONES = shot.FACCIONES
OID_Q     = 20

_y, _x = np.mgrid[0:CELDA, 0:CELDA]
DX = _x + 0.5 - C
DY = _y + 0.5 - C
R = np.hypot(DX, DY)
ANG = np.arctan2(DY, DX)


# --- Lo que comparten las propuestas -------------------------------------------

def fase(v):
    """El t del bucle del proton en su frame v."""
    return 2 * np.pi * v / pro.FRAMES


def quarks(v, escala=1.0):
    """Los tres quarks del frame v, con el radio de su orbita escalado."""
    out = []
    for x, y, a, sabor in pro.quarks(P, fase(v)):
        out.append((C + (x - C) * escala, C + (y - C) * escala, a, sabor))
    return out


def poner_quarks(lz, ramp, qs, rq):
    for x, y, _, sabor in qs:
        if sabor == 'u':
            esfera(lz, x, y, rq, ramp, OID_Q, calor=0.8)
        else:
            esfera(lz, x, y, rq, ramp, OID_Q + 1, apagado=1.0)


def solido(lz, mask, ramp, banda):
    """Pixeles de luz solidos, sin contorno: van a `glow`."""
    m = mask & ~lz['lleno']
    lz['col'][m] = ramp[banda]
    lz['glow'][m] = True


def extension(img):
    """Hasta donde llega lo encendido, en px desde el centro."""
    on = img[..., 3] > 0
    return float(R[on].max()) if on.any() else 0.0


def comprobar(celdas, fac, version):
    fallos = []
    ramp = rampa(FACCIONES[fac])
    validos = {tuple(c) for c in ramp} | {tuple(CONTORNO)}
    for v in range(VARIANTES):
        imgs = celdas[v]
        ext = [extension(i) for i in imgs]
        for f, img in enumerate(imgs):
            a = img[..., 3]
            if not set(np.unique(a)) <= {0, 255}:
                fallos.append(f'v{v} f{f}: alfa no binario')
            if not {tuple(c) for c in img[a > 0][:, :3]} <= validos:
                fallos.append(f'v{v} f{f}: color fuera de la rampa')
            on = a > 0
            if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
                fallos.append(f'v{v} f{f}: toca el borde')
            if not on.any():
                fallos.append(f'v{v} f{f}: vacio')
        if ext[0] > RE + 1:
            fallos.append(f'v{v}: el frame 0 se sale de la bolsa ({ext[0]:.1f} > {RE + 1})')
        if not all(a > b for a, b in zip(ext, ext[1:])):
            fallos.append(f'v{v}: no implosiona, radios {[round(e, 1) for e in ext]}')
        fin = int((imgs[-1][..., 3] > 0).sum())
        if fin > MAX_FIN:
            fallos.append(f'v{v}: el ultimo frame deja {fin} px (maximo {MAX_FIN})')
        if len({i.tobytes() for i in imgs}) != FRAMES:
            fallos.append(f'v{v}: frames repetidos')
    distintas = len({celdas[v][0].tobytes() for v in range(VARIANTES)})
    if distintas != VARIANTES:
        fallos.append(f'solo {distintas} variantes distintas en el frame 0')
    if fallos:
        raise SystemExit(f'ART-FX-KILL v{version} ({fac}) no pasa:\n  ' + '\n  '.join(fallos))


def generar(dibujar, version):
    celdas = {}
    for fac, ramp_n in FACCIONES.items():
        ramp = rampa(ramp_n)
        c = [[a_rgba(dibujar(ramp, f, v)) for f in range(FRAMES)] for v in range(VARIANTES)]
        comprobar(c, fac, version)
        celdas[fac] = c
    return celdas


def hoja(celdas):
    return np.vstack([np.hstack([celdas[fac][v][f] for v in range(VARIANTES)
                                 for f in range(FRAMES)]) for fac in FACCIONES])


def exporta(destino, img):
    import json
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_kill_sheet.png'))
    with open(os.path.join(destino, 'fx_kill_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'variants': VARIANTES, 'fps': FPS,
                   'rows': list(FACCIONES),
                   'note': 'talla 1 del proton; la variante es el frame del bucle del proton '
                           'en que muere; columnas: variante 0 frames 0-2, variante 1 ...; '
                           'pivote en el centro, en el sitio del proton'}, f, indent=1)


# --- Vistas --------------------------------------------------------------------

BG, ampliar, sobre = shot.BG, shot.ampliar, shot.sobre
_SHOT = {}


def destello(fac, carga, v, f):
    """ART-FX-SHOT v3, el elegido, recortado de su hoja."""
    if not _SHOT:
        _SHOT['h'] = shot.abrir('fx', 'shot', 'v3', 'fx_shot_sheet.png')
    t = pro.talla_de(carga)
    fila = list(FACCIONES).index(fac) * pro.TALLAS_N + t - 1
    col = v * 2 + f
    c = shot.CELDA
    return _SHOT['h'][fila * c:(fila + 1) * c, col * c:(col + 1) * c]


def vista_hoja(celdas):
    """x4. Por bando y tres variantes: el proton en su frame, con 2 de Carga,
    y los tres frames de la implosion."""
    filas = []
    for fac in FACCIONES:
        for v in (0, 5, 11):
            fila = np.empty((CELDA, CELDA * 5, 3), np.uint8)
            fila[:] = BG
            sobre(fila, shot.proton(fac, 2, v), C, C)
            for f in range(FRAMES):
                sobre(fila, celdas[fac][v][f], (f + 1) * CELDA + C, C)
            filas.append(fila)
    return ampliar(np.vstack(filas), 4)


def vista_golpe(celdas):
    """x3. El ultimo disparo en cinco tiempos -antes, los tres frames y
    despues- con el arco, que dura dos: Magnetar enemigo N3 contra un proton
    del Jugador de 2, y uno neutral N2 contra uno del Enemigo de 1."""
    casos = (('enemy', 3, 'player', 2, 3), ('neutral', 2, 'enemy', 1, 9))
    W, H = 170, 70
    filas = []
    for mfac, n, pfac, carga, v0 in casos:
        tiras = []
        for paso in range(5):
            tab = np.empty((H, W, 3), np.uint8)
            tab[:] = BG
            mx, my, qx, qy = 38, 42, 140, 28
            if paso in (1, 2):
                shot.arco(tab, mfac, mx, my, qx, qy, paso - 1)
            sobre(tab, shot.magnetar(n, mfac), mx, my)
            if paso == 0:
                sobre(tab, shot.proton(pfac, carga, v0), qx, qy)
            elif paso <= FRAMES:
                sobre(tab, celdas[pfac][v0 + 1][paso - 1], qx, qy)
            tiras += [tab, np.tile(BG, (H, 4, 1))]
        filas.append(np.hstack(tiras))
    return ampliar(np.vstack(filas), 3)


def vista_tablero(celdas, k):
    """El frame de juego k: un proton del Jugador de 12 cruza el radio de un
    Magnetar enemigo N5, encaja los disparos con `ART-FX-SHOT` y muere con
    este efecto. 300 x 180."""
    import gen_radio_v2 as rad
    import gen_shot_v1 as sh
    mag = (170, 95)
    o, d = (-10, 30), (330, 150)
    tab = np.empty((180, 300, 3), np.uint8)
    tab[:] = BG
    sobre(tab, rad.anillo(5, 'enemy'), *mag)
    R5 = rad.RADIO_U[5] * rad.PPU
    periodo = FPS / sh.CADENCIA[5]
    largo = math.hypot(d[0] - o[0], d[1] - o[1])
    vuelta = int(largo / shot.PASO) + 10
    kk = k % vuelta

    def pos(j):
        s = j * shot.PASO / largo
        return o[0] + (d[0] - o[0]) * s, o[1] + (d[1] - o[1]) * s

    carga, golpe, muerte, n = 12, None, None, 0
    for j in range(kk + 1):
        x, y = pos(j)
        if muerte is None and math.hypot(x - mag[0], y - mag[1]) <= R5 \
                and int(j % periodo) == 0:
            carga -= shot.DANO
            n += 1
            golpe = j
            if carga <= 0:
                muerte = j
    x, y = pos(muerte if muerte is not None else kk)
    f_arco = kk - golpe if golpe is not None else None
    if f_arco is not None and f_arco < 2:
        shot.arco(tab, 'enemy', *mag, x, y, f_arco)
    sobre(tab, shot.magnetar(5, 'enemy'), *mag)
    if muerte is None:
        sobre(tab, shot.proton('player', carga, kk), x, y)
        if f_arco is not None and f_arco < 2:
            sobre(tab, destello('player', carga, n % 4, f_arco), x, y)
    elif kk - muerte < FRAMES:
        v = muerte % VARIANTES
        sobre(tab, celdas['player'][v][kk - muerte], x, y)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_kill'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(vista_hoja(celdas)).save(os.path.join(d, 'fx_kill_hoja_x4.png'))
    Image.fromarray(vista_golpe(celdas)).save(os.path.join(d, 'fx_kill_golpe_x3.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(40)]
        for nombre, ms in (('fx_kill_tablero_x2.gif', 1000 / FPS),
                           ('fx_kill_tablero_x2_lento.gif', 4000 / FPS)):
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
    print(f'ART-FX-KILL v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for v in (0, 4, 8, 12):
        c = celdas['player'][v]
        print(f'  variante {v:>2}  px {[int((i[..., 3] > 0).sum()) for i in c]}  '
              f'radio {[round(extension(i), 1) for i in c]}')
    vistas(celdas, version, gif)


# --- La v1: «El colapso» ----------------------------------------------------------

def dibujar(ramp, f, v):
    lz = lienzo(CELDA)
    if f == 0:
        rb = P['bolsa'] * 0.66
        poner_quarks(lz, ramp, quarks(v, 0.62), P['rq'] * 0.85)
        aro = (R >= rb - 0.5) & (R < rb + 0.5)
        solido(lz, aro, ramp, 2)
        dens = np.clip(1 - R / rb, 0, 1) * 0.55
        dens[R >= rb - 0.5] = 0
        tramar(lz, dens, ramp, banda_int=3, banda_ext=4, corte=0.45)
    elif f == 1:
        nucleo = R < 2.2
        solido(lz, nucleo & (R < 1.0), ramp, 1)
        solido(lz, nucleo, ramp, 2)
        # cuatro trazos cortos que caen hacia el centro, girados con la fase
        for k in range(4):
            a = fase(v) + k * math.pi / 2 + math.pi / 4
            for r in (4.0, 5.0):
                x, y = int(math.floor(C + r * math.cos(a))), int(math.floor(C + r * math.sin(a)))
                lz['col'][y, x] = ramp[3 if r < 4.5 else 4]
                lz['glow'][y, x] = True
    else:
        mota = (np.abs(DX) < 1) & (np.abs(DY) < 1)
        solido(lz, mota, ramp, 3)
    return lz


if __name__ == '__main__':
    ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
