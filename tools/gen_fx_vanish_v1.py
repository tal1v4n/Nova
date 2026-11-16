"""NOVA - ART-FX-VANISH v1, los protones huerfanos: «El vaho».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5 · Spec 10.1 y 17 #21

Cuando un bando pierde su ultimo nodo, sus protones en vuelo «se desvanecen
sin impactar» (10.1; 17, caso 21) y empieza la cascada de conquista. 5
frames, 0.4 s. Una sola hoja, `art/fx/vanish/v<n>/fx_vanish_sheet.png`, en
celdas de 64:

    filas     20   player_1 ... player_10, enemy_1 ... enemy_10
    columnas  80   variante 0 frames 0-4, variante 1 frames 0-4 ...

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - NO ES UNA MUERTE. `ART-FX-KILL` es un golpe: el Magnetar lo deja a 0 y
    el proton implosiona. Aqui nadie lo toca: se queda sin dueno, y lo que
    se ve es que se apaga. Ninguna de las tres implosiona.
  - CUALQUIER TALLA. Un proton huerfano puede llevar de 1 a 100 de Carga:
    una fila por bando y talla, como `ART-FX-SHOT`. Los dos bandos, porque
    10.2 tambien deja huerfanos al Jugador, aunque la partida acabe ahi.
  - SUSTITUYE AL PROTON, CON 16 VARIANTES. Como `ART-FX-KILL`: la variante
    v arranca del frame v del bucle de «La bolsa», asi que no hay salto.
    El frame 0 se dibuja con el mismo modelo que el proton
    (`gen_proton_v2`), no copiado de su PNG.
  - EL NUMERO SE VA PRIMERO. El efecto ocupa el sitio del proton y la Carga
    no esta en la hoja: `ProtonVisual` deja de dibujarla en el mismo tick.
    Un proton huerfano ya no cuenta.
  - SE APAGA DE VERDAD: cada frame da menos luz que el anterior (suma de
    luminancia de lo encendido), y el ultimo deja como mucho la quinta
    parte de los pixeles del primero.

LA TESIS DE LA v1: SIN NADIE QUE LA SOSTENGA, LA BOLSA SE DESHACE EN VAHO.
La Carga era lo que la mantenia tensa; sin bando, se afloja hacia fuera.

  - La bolsa crece 1 px por frame y su trama se aclara: el proton se
    ensancha mientras se vuelve transparente, como el aliento en un cristal.
  - Los quarks se separan con ella y bajan una banda cada dos frames.
  - Desde el frame 1 nada lleva contorno: lo que se esta disolviendo ya no
    tiene borde.
  - Una erosion de Bayer quita cada frame una quinta parte mas de pixeles.

    py tools/gen_fx_vanish_v1.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_vanish_v1.py --gif    y las animadas
"""

import contextlib
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import BAYER8, CONTORNO, RAIZ, a_rgba, dir_preview, esfera, lienzo, rampa
import gen_proton_v2 as pro
import gen_fx_shot_v1 as shot

ASSET     = 'vanish'
VERSION   = 1
NOMBRE    = 'El vaho'

CELDA     = 64
C         = CELDA / 2
FRAMES    = 5                   # ceil(0.4 * 12)
VARIANTES = pro.FRAMES
TALLAS    = list(range(1, pro.TALLAS_N + 1))
FPS       = 12
RESTO     = 0.2                 # el ultimo frame, como mucho esta fraccion del primero

FACCIONES = shot.FACCIONES
OID_Q     = 20
BAYER     = np.tile(BAYER8, (CELDA // 8, CELDA // 8))

_y, _x = np.mgrid[0:CELDA, 0:CELDA]
R = np.hypot(_x + 0.5 - C, _y + 0.5 - C)


# --- Lo que comparten las propuestas -------------------------------------------

@contextlib.contextmanager
def centrado():
    """Las primitivas del proton dibujan alrededor de su C de 24 (celda de
    48); aqui la celda es de 64. Se mueve su centro mientras se dibuja."""
    viejo = pro.C
    pro.C = C
    try:
        yield
    finally:
        pro.C = viejo


def fase(v):
    return 2 * np.pi * v / pro.FRAMES


def cuerpo(ramp, p, t, quarks=True, gluones=True, bolsa=True, apagados=()):
    """El proton de «La bolsa» con los parametros `p` (los de su talla, o
    cambiados) en una celda de 64. `apagados`: indices de quarks que se
    dibujan como un disco plano en la banda 5, sin luz ni contorno."""
    with centrado():
        lz = lienzo(CELDA)
        qs = pro.quarks(p, t)
        if bolsa:
            pro.bolsa(lz, p, ramp, t)
        if quarks:
            for j, (x, y, _, sabor) in enumerate(qs):
                if j in apagados:
                    continue
                if sabor == 'u':
                    esfera(lz, x, y, p['rq'], ramp, OID_Q, calor=0.8)
                else:
                    esfera(lz, x, y, p['rq'], ramp, OID_Q + 1, apagado=1.0)
            lz['glow'] &= ~lz['lleno']
        if gluones:
            pro.gluones(lz, p, ramp, qs, t)
        for j in apagados:
            x, y = qs[j][0], qs[j][1]
            disco = np.hypot(_x + 0.5 - x, _y + 0.5 - y) < p['rq']
            lz['col'][disco] = ramp[5]
            lz['glow'][disco] = True
    return lz


def sin_contorno(lz):
    """Todo pasa a luz: lo solido deja de generar contorno."""
    lz['glow'] |= lz['lleno']
    lz['lleno'][:] = False
    return lz


def oscurecer(img, ramp, bandas):
    """Baja `bandas` bandas cada pixel de la rampa (el contorno no cambia)."""
    if bandas <= 0:
        return img
    out = img.copy()
    on = out[..., 3] > 0
    for i in range(6, -1, -1):
        m = on & np.all(out[..., :3] == ramp[i], axis=-1)
        out[m, :3] = ramp[min(i + bandas, 6)]
    return out


def erosion(img, f, total=FRAMES, desfase=0.0):
    """Quita los pixeles cuyo umbral de Bayer cae por debajo de f/total."""
    out = img.copy()
    quita = BAYER < (f / total + desfase)
    out[quita, 3] = 0
    out[quita, :3] = 0
    return out


def luz(img):
    on = img[..., 3] > 0
    c = img[on, :3].astype(float)
    return float((0.2126 * c[:, 0] + 0.7152 * c[:, 1] + 0.0722 * c[:, 2]).sum())


def comprobar(celdas, fac, t, version, extra=()):
    fallos = []
    validos = {tuple(c) for c in rampa(FACCIONES[fac])} | {tuple(CONTORNO)}
    for nombre in extra:
        validos |= {tuple(c) for c in rampa(nombre)}
    for v in range(VARIANTES):
        imgs = celdas[v]
        for f, img in enumerate(imgs):
            a = img[..., 3]
            if not set(np.unique(a)) <= {0, 255}:
                fallos.append(f'v{v} f{f}: alfa no binario')
            if not {tuple(c) for c in img[a > 0][:, :3]} <= validos:
                fallos.append(f'v{v} f{f}: color fuera de la rampa')
            on = a > 0
            if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
                fallos.append(f'v{v} f{f}: toca el borde')
        l = [luz(i) for i in imgs]
        if not all(x > y for x, y in zip(l, l[1:])):
            fallos.append(f'v{v}: no se apaga, luz {[round(x) for x in l]}')
        px = [int((i[..., 3] > 0).sum()) for i in imgs]
        if px[-1] > RESTO * px[0]:
            fallos.append(f'v{v}: el ultimo frame deja {px[-1]} px de {px[0]}')
        if px[-1] == 0:
            fallos.append(f'v{v}: el ultimo frame esta vacio: sobra un frame')
    distintas = len({celdas[v][0].tobytes() for v in range(VARIANTES)})
    if distintas != VARIANTES:
        fallos.append(f'solo {distintas} variantes distintas en el frame 0')
    if fallos:
        raise SystemExit(f'ART-FX-VANISH v{version} ({fac} {t}) no pasa:\n  '
                         + '\n  '.join(fallos[:12]))


def generar(dibujar, version, extra=()):
    celdas = {}
    for fac, ramp_n in FACCIONES.items():
        ramp = rampa(ramp_n)
        for t in TALLAS:
            c = [[dibujar(ramp, t, f, v) for f in range(FRAMES)] for v in range(VARIANTES)]
            comprobar(c, fac, t, version, extra)
            celdas[fac, t] = c
    return celdas


def hoja(celdas):
    return np.vstack([np.hstack([celdas[fac, t][v][f] for v in range(VARIANTES)
                                 for f in range(FRAMES)])
                      for fac in FACCIONES for t in TALLAS])


def exporta(destino, img):
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_vanish_sheet.png'))
    with open(os.path.join(destino, 'fx_vanish_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'variants': VARIANTES, 'fps': FPS,
                   'rows': [f'{fac}_{t}' for fac in FACCIONES for t in TALLAS],
                   'note': 'fila por faccion y talla del proton; la variante es el frame del '
                           'bucle del proton en que se queda huerfano; columnas: variante 0 '
                           'frames 0-4, variante 1 ...; sustituye al proton y a su Carga; '
                           'pivote en el centro'}, f, indent=1)


# --- Vistas --------------------------------------------------------------------

BG, ampliar, sobre = shot.BG, shot.ampliar, shot.sobre


def vista_hoja(celdas):
    """x3. Por bando y tres tallas, variante 0: el proton y los cinco frames."""
    filas = []
    for fac in FACCIONES:
        for carga in (5, 25, 50):
            t = pro.talla_de(carga)
            fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
            fila[:] = BG
            sobre(fila, shot.proton(fac, carga, 0), C, C)
            for f in range(FRAMES):
                sobre(fila, celdas[fac, t][0][f], (f + 1) * CELDA + C, C)
            filas.append(fila)
    return ampliar(np.vstack(filas), 3)


# El momento de la victoria: tres protones del Enemigo en vuelo, de tallas
# distintas, se quedan huerfanos a la vez en el frame de juego K0.
ENVIOS = (((20, 40), (330, 130), 44), ((330, 30), (40, 150), 23), ((150, 170), (230, 10), 7))
K0 = 8


def vista_tablero(celdas, k):
    tab = np.empty((180, 340, 3), np.uint8)
    tab[:] = BG
    for o, d, carga in ENVIOS:
        largo = math.hypot(d[0] - o[0], d[1] - o[1])
        kk = min(k, K0)
        s = kk * shot.PASO / largo
        x, y = o[0] + (d[0] - o[0]) * s, o[1] + (d[1] - o[1]) * s
        if k < K0:
            sobre(tab, shot.proton('enemy', carga, k), x, y)
        elif k - K0 < FRAMES:
            t = pro.talla_de(carga)
            sobre(tab, celdas['enemy', t][K0 % VARIANTES][k - K0], x, y)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_vanish'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(vista_hoja(celdas)).save(os.path.join(d, 'fx_vanish_hoja_x3.png'))
    Image.fromarray(ampliar(vista_tablero(celdas, K0 + 1), 2)).save(
        os.path.join(d, 'fx_vanish_tablero_x2.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(K0 + FRAMES + 8)]
        for nombre, ms in (('fx_vanish_tablero_x2.gif', 1000 / FPS),
                           ('fx_vanish_tablero_x2_lento.gif', 4000 / FPS)):
            im = [Image.fromarray(ampliar(e, 2)) for e in esc]
            im[0].save(os.path.join(d, nombre), save_all=True, append_images=im[1:],
                       duration=round(ms), loop=0)
    print('vistas en', d)


def ejecutar(dibujar, version, nombre, gif, extra=()):
    destino = os.path.join(RAIZ, 'art', 'fx', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    celdas = generar(dibujar, version, extra)
    img = hoja(celdas)
    exporta(destino, img)
    print(f'ART-FX-VANISH v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in (1, 5, 10):
        c = celdas['player', t][0]
        print(f'  talla {t:>2}  px {[int((i[..., 3] > 0).sum()) for i in c]}')
    vistas(celdas, version, gif)


# --- La v1: «El vaho» ---------------------------------------------------------------

def dibujar(ramp, t, f, v):
    p = dict(pro.TALLAS[t])
    p['bolsa'] += f                       # la bolsa se afloja hacia fuera
    p['R'] *= 1 + 0.08 * f                # y los quarks se separan con ella
    lz = cuerpo(ramp, p, fase(v), gluones=f == 0)
    if f > 0:
        sin_contorno(lz)
    img = a_rgba(lz)
    img = oscurecer(img, ramp, (f + 1) // 2)
    return erosion(img, f) if f else img


if __name__ == '__main__':
    ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
