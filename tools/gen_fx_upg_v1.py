"""NOVA - ART-FX-UPG v1, la mejora de nivel: «La muda».

Ref: NOVA_Assets_Diseno.md A.5 y A.2 · NOVA_Estados_Animaciones.md 14.5 ·
Spec 16.2 y 13.3

El momento en que `NodeLevelComponent` sube un nivel: en ese tick el sprite
pasa del nivel n-1 al n, y casi siempre CRECE (el Magnetar de 41 a 60 px de
huella). 6 frames, 0.5 s, sobre el nodo mejorado. Una sola hoja,
`art/fx/upg/v<n>/fx_upg_sheet.png`, en celdas de 96:

    filas     20   player_str2 ... player_mag5, enemy_str2 ... enemy_mag5
    columnas   6   los frames

LO QUE COMPARTEN LAS TRES PROPUESTAS (vive aqui; v2 y v3 lo importan):

  - UNA FILA POR DESTINO. Se mejora a diez sitios: Estrella 2-5, Pulsar 2-3
    y Magnetar 2-5. Cada fila sabe la huella de antes, la de despues y el
    nivel nuevo, y `VFXPool` la elige por tipo y nivel sin calcular nada.
    Con tallas no bastaba: el salto de tamano es lo que el efecto tiene que
    explicar, y el Magnetar 2 -> 3 no crece nada.
  - EN EL COLOR DEL NODO. Mejora el dueno con la Energia del propio nodo
    (18 #18): `str_p` y `str_e`, las de los colores literales de faccion,
    como el encendido. Indices 1-5: el blanco es de `FX-CAP`.
  - PUEDE CRUZAR EL CUERPO, NO EL NUMERO. El numero baja con el nodo nuevo
    (media huella + 2), y es lo que acaba de bajar: lo protege su caja, que
    empieza 1 px por encima del borde del cuerpo nuevo (`franja`).
  - SE ENCIENDE Y SE APAGA. El ultimo frame es el de menos luz y no pasa de
    la cuarta parte del pico.

LA TESIS DE LA v1: EL NODO MUDA LA PIEL. En el tick de la mejora el sprite ya
es el grande, pero el tamano viejo se queda un instante dibujado encima,
como la piel que deja una serpiente, y el nodo nuevo la revienta.

  - LA PIEL (frame 0). Un aro solido de 3 px en el borde viejo, claro por
    fuera y con la cara de dentro en el indice 5: una cascara, no una luz.
    El jugador ve cuanto media el nodo hace un tick.
  - SE RAJA (frames 1-2). El aro se parte en seis placas que se separan y
    empujan hasta el borde nuevo.
  - SE DESPRENDE (frames 3-5). Las placas salen despedidas, cada una con un
    pequeno giro, se rompen en esquirlas y se apagan.

Si el nodo no crece (Magnetar 2 -> 3 y 4 -> 5), la piel arranca 6 px dentro
del borde: tambien muda, aunque la talla sea la misma.

    py tools/gen_fx_upg_v1.py                  hoja, comprobaciones y vistas fijas
    py tools/gen_fx_upg_v1.py --gif            y las animadas
    py tools/gen_fx_upg_v1.py --comparativa    las tres propuestas lado a lado
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
from gen_fx_send_v1 import ANG, C, CELDA, DX, DY, R, a_rgba, luz, tramar

ASSET     = 'upg'
VERSION   = 1
NOMBRE    = 'La muda'

FRAMES    = 6                       # ceil(0.5 * 12)
FPS       = 12
FACCIONES = {'player': 'str_p', 'enemy': 'str_e'}
DESTINOS  = (('str', 2), ('str', 3), ('str', 4), ('str', 5),
             ('pul', 2), ('pul', 3),
             ('mag', 2), ('mag', 3), ('mag', 4), ('mag', 5))
RESTO     = 0.25
MEDIO_NUMERO = 19


# --- Lo que comparten las propuestas -------------------------------------------

class Salto:
    """Lo que una fila sabe de su mejora."""

    def __init__(self, pre, n):
        h = abs_.huellas()
        self.pre, self.n = pre, n
        self.antes = (h[pre, n - 1][2] + 1) / 2     # radio del cuerpo viejo
        self.despues = (h[pre, n][2] + 1) / 2       # y del nuevo
        self.franja = (DY > self.despues - 1) & (np.abs(DX) <= MEDIO_NUMERO)
        self.libre = ~self.franja

    @property
    def nombre(self):
        return f'{self.pre}{self.n}'

    def piel(self):
        """Donde estaba el borde: el viejo, o 6 px dentro si no crece."""
        return min(self.antes, self.despues - 6)

    def punto(self, idx, x, y, banda, solo_vacio=False):
        ix, iy = int(math.floor(x)), int(math.floor(y))
        if 0 <= ix < CELDA and 0 <= iy < CELDA and self.libre[iy, ix]:
            if not solo_vacio or idx[iy, ix] == 0:
                idx[iy, ix] = banda


def comprobar(frames, s, version):
    fallos = []
    for f, a in enumerate(frames):
        on = a > 0
        if not set(np.unique(a)) <= {0, 1, 2, 3, 4, 5}:
            fallos.append(f'f{f}: indice fuera de la rampa [1..5]')
        if (on & s.franja).any():
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
        raise SystemExit(f'ART-FX-UPG v{version} ({s.nombre}) no pasa:\n  '
                         + '\n  '.join(fallos))


def generar(dibujar, version):
    celdas = {}
    for pre, n in DESTINOS:
        s = Salto(pre, n)
        frames = [dibujar(s, f).astype(np.int8) for f in range(FRAMES)]
        comprobar(frames, s, version)
        celdas[pre, n] = frames
    return celdas


def hoja(celdas):
    filas = []
    for ramp_n in FACCIONES.values():
        ramp = rampa(ramp_n)
        for d in DESTINOS:
            filas.append(np.hstack([a_rgba(a, ramp) for a in celdas[d]]))
    return np.vstack(filas)


def exporta(destino, img):
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_upg_sheet.png'))
    with open(os.path.join(destino, 'fx_upg_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'fps': FPS,
                   'rows': [f'{fac}_{p}{n}' for fac in FACCIONES for p, n in DESTINOS],
                   'note': 'sobre el nodo mejorado; la fila, por bando, tipo y nivel nuevo '
                           '(str 2-5, pul 2-3, mag 2-5); pivote en el centro del nodo'},
                  f, indent=1)


# --- Vistas --------------------------------------------------------------------

BG, ampliar, sobre = shot.BG, shot.ampliar, shot.sobre
nodo, numero = abs_.nodo, abs_.numero
MUESTRA = (('player', 'str', 2), ('player', 'str', 5), ('player', 'pul', 2),
           ('player', 'mag', 2), ('player', 'mag', 3),
           ('enemy', 'str', 3), ('enemy', 'pul', 3), ('enemy', 'mag', 4))


def efecto(celdas, fac, pre, n, f):
    return a_rgba(celdas[pre, n][f], rampa(FACCIONES[fac]))


def vista_hoja(celdas):
    """x3. El nodo antes de mejorar y los seis frames sobre el mejorado, con
    el sitio del numero marcado en gris."""
    filas = []
    for fac, pre, n in MUESTRA:
        fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
        fila[:] = BG
        numero(fila, C, C, pre, n - 1)
        sobre(fila, nodo(pre, n - 1, fac), C, C)
        for f in range(FRAMES):
            x = (f + 1) * CELDA + C
            numero(fila, x, C, pre, n)
            sobre(fila, nodo(pre, n, fac), x, C)
            sobre(fila, efecto(celdas, fac, pre, n, f), x, C)
        filas.append(fila)
    return ampliar(np.vstack(filas), 3)


# Tres mejoras seguidas: el Jugador sube su Estrella a 2 y su Magnetar a 3, y
# el Enemigo responde subiendo su Pulsar a 2.
ESCENA = (('player', 'str', 2, (80, 90), 4), ('player', 'mag', 3, (210, 150), 14),
          ('enemy', 'pul', 2, (350, 80), 24))


def vista_tablero(celdas, k):
    tab = np.empty((230, 440, 3), np.uint8)
    tab[:] = BG
    for fac, pre, n, (x, y), k0 in ESCENA:
        m = n - 1 if k < k0 else n
        numero(tab, x, y, pre, m)
        sobre(tab, nodo(pre, m, fac), x, y)
        if k0 <= k < k0 + FRAMES:
            sobre(tab, efecto(celdas, fac, pre, n, k - k0), x, y)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_upg'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(vista_hoja(celdas)).save(os.path.join(d, 'fx_upg_hoja_x3.png'))
    if gif:
        esc = [vista_tablero(celdas, k) for k in range(36)]
        for nombre, ms in (('fx_upg_tablero_x2.gif', 1000 / FPS),
                           ('fx_upg_tablero_x2_lento.gif', 4000 / FPS)):
            im = [Image.fromarray(ampliar(e, 2)) for e in esc]
            im[0].save(os.path.join(d, nombre), save_all=True, append_images=im[1:],
                       duration=round(ms), loop=0)
    print('vistas en', d)


def comparativa():
    """Las tres propuestas, una debajo de otra, con la misma muestra corta."""
    import importlib
    muestra = (('player', 'str', 2), ('player', 'mag', 3), ('enemy', 'pul', 3))
    bloques = []
    for v in (1, 2, 3):
        mod = importlib.import_module(f'gen_fx_upg_v{v}')
        celdas = generar(mod.dibujar, v)
        for fac, pre, n in muestra:
            fila = np.empty((CELDA, CELDA * (FRAMES + 1), 3), np.uint8)
            fila[:] = BG
            numero(fila, C, C, pre, n - 1)
            sobre(fila, nodo(pre, n - 1, fac), C, C)
            for f in range(FRAMES):
                x = (f + 1) * CELDA + C
                numero(fila, x, C, pre, n)
                sobre(fila, nodo(pre, n, fac), x, C)
                sobre(fila, efecto(celdas, fac, pre, n, f), x, C)
            bloques.append(fila)
        sep = np.empty((6, CELDA * (FRAMES + 1), 3), np.uint8)
        sep[:] = (60, 66, 90)
        bloques.append(sep)
    ruta = os.path.join(dir_preview('fx_upg'), 'comparativa_x3.png')
    Image.fromarray(ampliar(np.vstack(bloques[:-1]), 3)).save(ruta)
    print('comparativa en', ruta)


def ejecutar(dibujar, version, nombre, gif):
    destino = os.path.join(RAIZ, 'art', 'fx', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    celdas = generar(dibujar, version)
    img = hoja(celdas)
    exporta(destino, img)
    print(f'ART-FX-UPG v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for d in DESTINOS:
        print(f'  {d[0]}{d[1]}  px {[int((a > 0).sum()) for a in celdas[d]]}  '
              f'luz {[luz(a) for a in celdas[d]]}')
    vistas(celdas, version, gif)


# --- La v1: «La muda» ----------------------------------------------------------------

PLACAS = 6
GIRO0 = math.radians(-90 + 30)      # las juntas no caen en el eje vertical


def dibujar(s, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    r0, r1 = s.piel(), s.despues
    # donde esta la piel en cada frame: del borde viejo al nuevo, y fuera
    radio = (r0, r0 + (r1 - r0) * 0.5, r1 + 1, r1 + 4, r1 + 7, r1 + 9)[f]
    hueco = (0.0, 0.08, 0.15, 0.21, 0.26, 0.30)[f]          # media junta, en rad
    giro = (0.0, 0.0, 0.03, 0.08, 0.14, 0.20)[f]            # cada placa gira al soltarse
    grueso = (3, 3, 3, 2, 1, 1)[f]
    sector = 2 * math.pi / PLACAS
    a = np.angle(np.exp(1j * (ANG - GIRO0)))                 # -pi..pi
    k = np.floor((a + math.pi) / sector)                     # la placa
    mitad = (k + 0.5) * sector - math.pi                     # su angulo medio
    rel = np.angle(np.exp(1j * (a - mitad)))                 # dentro de la placa
    dentro = (np.abs(rel) < sector / 2 - hueco)
    # el giro: la placa se tuerce, un extremo mas lejos que el otro
    rr = radio + giro * rel * radio
    banda = (R >= rr - grueso / 2) & (R < rr + grueso / 2) & dentro & s.libre
    interior = banda & (R < rr - grueso / 2 + 1)            # la cara de dentro de la placa
    if f == 0:
        exterior = banda & (R >= rr + grueso / 2 - 1)
        idx[banda] = 2
        idx[exterior] = 1
        idx[interior] = 5
        # el velo entre la piel y el borde nuevo: lo que acaba de crecer
        dens = np.where((R > r0 + 1) & (R < r1 - 0.5), 0.35, 0) * s.libre
        tramar(idx, dens, 4, 4, corte=0.3)
    elif f <= 2:
        idx[banda] = 1 if f == 1 else 2
        idx[interior] = 5
        # las juntas echan chispa al abrirse
        for j in range(PLACAS):
            ang = GIRO0 + j * sector - math.pi
            s.punto(idx, C + (radio + 1.5) * math.cos(ang), C + (radio + 1.5) * math.sin(ang),
                    2, solo_vacio=True)
    elif f <= 4:
        # se rompen en esquirlas: la trama se come la placa
        dens = np.where(banda, (0.8, 0.5)[f - 3], 0)
        tramar(idx, dens, 3 if f == 3 else 4, 4 if f == 3 else 5, corte=0.6)
    else:
        rng = np.random.default_rng(7 + s.n * 3 + len(s.pre))
        sueltos = np.argwhere(banda)
        for y, x in sueltos[rng.choice(len(sueltos), size=min(6, len(sueltos)), replace=False)]:
            idx[y, x] = 5
    return idx


if __name__ == '__main__':
    if '--comparativa' in sys.argv:
        comparativa()
    else:
        ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
