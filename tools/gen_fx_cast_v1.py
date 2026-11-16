"""NOVA - ART-FX-CAST v1, el lanzamiento de poder: «El fogonazo del faro».

Ref: NOVA_Assets_Diseno.md A.5, A.2.3 y A.4.2 · NOVA_Estados_Animaciones.md
14.5 · Spec 7.2

El momento en que `PulsarCaster` dispara: el Pulsar gasta su Carga y en el
mismo frame sale el haz del poder (`FX-BEAM`, A.4.2) hacia el objetivo. 5
frames, 0.4 s, sobre el Pulsar; el haz dura 0.25 s, asi que el efecto sigue
cuando el poder ya ha llegado (14.5). Una sola hoja,
`art/fx/cast/v<n>/fx_cast_sheet.png`, en celdas de 96:

    filas      9   zero_1, zero_2, zero_3, decay_1 ... wormhole_3
    columnas   5   los frames

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - EN EL COLOR DEL PODER, NO DEL BANDO. Como los haces (14.4): `hielo`,
    `decaimiento` y `gusano`, indices 1-5. El jugador lee QUE se ha lanzado
    en el Pulsar, antes de que el haz llegue; quien lo lanza ya lo dice el
    Pulsar.
  - UNA FILA POR NIVEL DEL PULSAR. Mide 44, 60 o 68 px de huella, y el
    efecto se construye sobre su geometria («El Faro», `gen_pulsar_v2`): el
    eje inclinado 32 grados, el cuerpo, la banda ecuatorial y los conos.
  - SIN DIRECCION AL OBJETIVO. El poder tiene alcance global y sale en
    cualquier angulo; la hoja no gira. La direccion la da el haz, que nace
    en el Pulsar en el mismo frame. El efecto es del Pulsar, no del viaje.
  - EL NUMERO, LIBRE. La caja del numero empieza 1 px por encima del borde
    de la huella.
  - SE APAGA. El ultimo frame es el de menos luz y no pasa de la cuarta
    parte del pico.

LA TESIS DE LA v1: EL FARO DESTELLA. Un pulsar es un faro (A.2.3), y lanzar
un poder es su destello: los dos conos se encienden en el color del poder y
un frente de onda, mas grande que los del idle, sale por el eje en los dos
sentidos. Es la gramatica del propio Pulsar, dicha una vez y fuerte.

  - DESTELLO (frame 0). Los dos conos llenos, del indice 1 en el eje al 3, y
    un frente pegado al cuerpo en cada sentido.
  - EL PULSO (frames 1-3). Los frentes viajan por el eje y se abren; los
    conos se aclaran.
  - SE APAGA (frame 4). Las puntas de los frentes.

    py tools/gen_fx_cast_v1.py                  hoja, comprobaciones y vistas fijas
    py tools/gen_fx_cast_v1.py --gif            y las animadas
    py tools/gen_fx_cast_v1.py --comparativa    las tres propuestas lado a lado
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, dir_preview, rampa
import gen_fx_shot_v1 as shot
import gen_fx_abs_v1 as abs_
import gen_pulsar_v2 as faro
from gen_fx_send_v1 import BAYER, C, CELDA, DX, DY, R, a_rgba, luz, tramar

ASSET     = 'cast'
VERSION   = 1
NOMBRE    = 'El fogonazo del faro'

FRAMES    = 5                       # ceil(0.4 * 12)
FPS       = 12
PODERES   = {'zero': 'hielo', 'decay': 'decaimiento', 'wormhole': 'gusano'}
NIVELES   = (1, 2, 3)
RESTO     = 0.25
MEDIO_NUMERO = 19

# el eje del faro, y las coordenadas de cada pixel en el: u a lo largo del
# eje (hacia arriba a la derecha), v a lo largo del ecuador
UX, UY = math.cos(faro.PHI), math.sin(faro.PHI)
U = DX * UX + DY * UY
V = -DX * UY + DY * UX


# --- Lo que comparten las propuestas -------------------------------------------

class Faro:
    """Lo que una fila sabe del Pulsar que lanza."""

    def __init__(self, n):
        self.n = n
        self.cfg = faro.NIVELES[n]
        self.medio = (abs_.huellas()['pul', n][2] + 1) / 2
        self.franja = (DY > self.medio - 1) & (np.abs(DX) <= MEDIO_NUMERO)
        self.libre = ~self.franja & (R < 45)

    def punto(self, idx, x, y, banda, solo_vacio=False):
        ix, iy = int(math.floor(x)), int(math.floor(y))
        if 0 <= ix < CELDA and 0 <= iy < CELDA and self.libre[iy, ix]:
            if not solo_vacio or idx[iy, ix] == 0:
                idx[iy, ix] = banda


def cono(fa, largo, k):
    """La densidad de los dos conos del faro, de r0 a `largo`, por k."""
    a = np.abs(U)
    dentro = (a > fa.cfg['ry']) & (a < largo) & (np.abs(V) < a * faro.SEMIANCHO)
    eje = 1 - np.clip(np.abs(V) / np.maximum(a * faro.SEMIANCHO, 1e-6), 0, 1)
    lejos = 1 - np.clip((a - fa.cfg['ry']) / max(largo - fa.cfg['ry'], 1), 0, 1)
    return np.where(dentro, (0.35 + 0.65 * eje ** 0.8) * (0.4 + 0.6 * lejos) * k, 0)


def frente(idx, fa, d, grosor, banda):
    """Medio arco abierto hacia fuera a distancia d del centro, en los dos
    sentidos del eje: el frente de onda del idle de «El Faro»."""
    a = max(d * faro.SEMIANCHO, 2.5)
    b = a * faro.APLANADO
    for s in (1, -1):
        p = (U - s * d) * s                      # hacia fuera > 0
        rho = np.hypot(V / a, p / b)
        dist = (rho - 1) * rho / np.maximum(np.hypot(V / a ** 2, p / b ** 2), 1e-6)
        m = (np.abs(dist) < grosor / 2) & (p >= -0.3) & fa.libre & (idx == 0)
        idx[m] = banda


def comprobar(frames, fa, poder, version):
    fallos = []
    for f, a in enumerate(frames):
        on = a > 0
        if not set(np.unique(a)) <= {0, 1, 2, 3, 4, 5}:
            fallos.append(f'f{f}: indice fuera de la rampa [1..5]')
        if (on & fa.franja).any():
            fallos.append(f'f{f}: pisa la caja del numero')
        if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
            fallos.append(f'f{f}: toca el borde de la celda')
        if not on.any():
            fallos.append(f'f{f}: vacio')
    l = [luz(a) for a in frames]
    if l[-1] != min(l) or l[-1] > RESTO * max(l):
        fallos.append(f'no se apaga: luz {l}')
    if not any(((a == 1).any()) for a in frames):
        fallos.append('ningun frame llega al indice 1')
    if len({a.tobytes() for a in frames}) != FRAMES:
        fallos.append('frames repetidos')
    if fallos:
        raise SystemExit(f'ART-FX-CAST v{version} ({poder} {fa.n}) no pasa:\n  '
                         + '\n  '.join(fallos))


def generar(dibujar, version):
    celdas = {}
    for poder in PODERES:
        for n in NIVELES:
            fa = Faro(n)
            frames = [dibujar(fa, poder, f).astype(np.int8) for f in range(FRAMES)]
            comprobar(frames, fa, poder, version)
            celdas[poder, n] = frames
    return celdas


def hoja(celdas):
    filas = []
    for poder, ramp_n in PODERES.items():
        ramp = rampa(ramp_n)
        for n in NIVELES:
            filas.append(np.hstack([a_rgba(a, ramp) for a in celdas[poder, n]]))
    return np.vstack(filas)


def exporta(destino, img):
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_cast_sheet.png'))
    with open(os.path.join(destino, 'fx_cast_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'fps': FPS,
                   'rows': [f'{p}_{n}' for p in PODERES for n in NIVELES],
                   'note': 'sobre el Pulsar que lanza, en el mismo frame que nace el haz; la fila, '
                           'por poder (rampa del poder, no del bando) y nivel del Pulsar; '
                           'sin girar; pivote en el centro del nodo'}, f, indent=1)


# --- Vistas --------------------------------------------------------------------

BG, ampliar, sobre = shot.BG, shot.ampliar, shot.sobre
nodo, numero = abs_.nodo, abs_.numero
MUESTRA = (('player', 'zero', 1), ('enemy', 'decay', 2), ('player', 'wormhole', 3),
           ('enemy', 'zero', 3), ('player', 'decay', 1), ('enemy', 'wormhole', 2))


def efecto(celdas, poder, n, f):
    return a_rgba(celdas[poder, n][f], rampa(PODERES[poder]))


def vista_hoja(celdas, muestra=MUESTRA):
    """Por fila: el Pulsar quieto y los cinco frames encima."""
    filas = []
    for fac, poder, n in muestra:
        fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
        fila[:] = BG
        for i in range(FRAMES + 1):
            x = i * CELDA + C
            numero(fila, x, C, 'pul', n)
            sobre(fila, nodo('pul', n, fac), x, C)
            if i:
                sobre(fila, efecto(celdas, poder, n, i - 1), x, C)
        filas.append(fila)
    return np.vstack(filas)


# Tres Pulsares lanzan uno tras otro, cada uno un poder.
ESCENA = (('player', 'zero', 1, (90, 100), 4), ('enemy', 'decay', 2, (220, 120), 12),
          ('player', 'wormhole', 3, (350, 100), 20))


def vista_tablero(celdas, k):
    tab = np.empty((220, 440, 3), np.uint8)
    tab[:] = BG
    for fac, poder, n, (x, y), k0 in ESCENA:
        numero(tab, x, y, 'pul', n)
        sobre(tab, nodo('pul', n, fac), x, y)
        if k0 <= k < k0 + FRAMES:
            sobre(tab, efecto(celdas, poder, n, k - k0), x, y)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_cast'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(vista_hoja(celdas), 3)).save(os.path.join(d, 'fx_cast_hoja_x3.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(30)]
        for nombre, ms in (('fx_cast_tablero_x2.gif', 1000 / FPS),
                           ('fx_cast_tablero_x2_lento.gif', 4000 / FPS)):
            im = [Image.fromarray(ampliar(e, 2)) for e in esc]
            im[0].save(os.path.join(d, nombre), save_all=True, append_images=im[1:],
                       duration=round(ms), loop=0)
    print('vistas en', d)


def comparativa():
    import importlib
    muestra = (('player', 'zero', 1), ('enemy', 'decay', 2), ('player', 'wormhole', 3))
    bloques = []
    for v in (1, 2, 3):
        mod = importlib.import_module(f'gen_fx_cast_v{v}')
        bloques.append(vista_hoja(generar(mod.dibujar, v), muestra))
        sep = np.empty((6, CELDA * (FRAMES + 1), 3), np.uint8)
        sep[:] = (60, 66, 90)
        bloques.append(sep)
    ruta = os.path.join(dir_preview('fx_cast'), 'comparativa_x3.png')
    Image.fromarray(ampliar(np.vstack(bloques[:-1]), 3)).save(ruta)
    print('comparativa en', ruta)


def ejecutar(dibujar, version, nombre, gif):
    destino = os.path.join(RAIZ, 'art', 'fx', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    celdas = generar(dibujar, version)
    img = hoja(celdas)
    exporta(destino, img)
    print(f'ART-FX-CAST v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for n in NIVELES:
        c = celdas['zero', n]
        print(f'  nivel {n}  px {[int((a > 0).sum()) for a in c]}  luz {[luz(a) for a in c]}')
    vistas(celdas, version, gif)


# --- La v1: «El fogonazo del faro» ----------------------------------------------

def dibujar(fa, poder, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    ry, largo = fa.cfg['ry'], fa.cfg['largo']
    d = ry + 3 + (0, 0.25, 0.5, 0.75, 0.92)[f] * (largo - ry - 6)
    if f < 4:
        frente(idx, fa, d, (2.6, 2.3, 1.8, 1.3)[f], (1, 1, 2, 3)[f])
    else:
        m = np.zeros_like(idx)
        frente(m, fa, d, 1.0, 4)
        idx[(m > 0) & (BAYER > 0.5)] = 5
    k = (1.0, 0.75, 0.45, 0.2, 0.0)[f]
    if k:
        tramar(idx, cono(fa, largo, k) * fa.libre, (1, 2, 3, 4)[f], (3, 4, 5, 5)[f], corte=0.55)
    return idx


if __name__ == '__main__':
    if '--comparativa' in sys.argv:
        comparativa()
    else:
        ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
