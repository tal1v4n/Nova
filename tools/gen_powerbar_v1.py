"""NOVA - ART-UI-POWERBAR v1, la barra de poder: «El conducto».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.6 (HUD permanente) y 13.12
(lenguaje y movimiento de la UI) · NOVA_Arte_Tecnica.md, regla 7

Este archivo tiene ademas LO COMUN A LAS TRES PROPUESTAS -el formato de las
piezas, el montaje, las comprobaciones y las vistas-; `gen_powerbar_v2.py` y
`gen_powerbar_v3.py` solo traen su dibujo.

QUE ENSENA LA BARRA (13.6). La Energia de todos los nodos de cada bando, sin
la Materia Inerte ni la Carga en vuelo. El Jugador crece desde la izquierda y
el Enemigo desde la derecha, y se encuentran: la barra es el REPARTO, la
parte de cada uno sobre la suma de los dos, la lectura de «el Jugador tiene
mas de la mitad» que usa la curva de poder del fin de nivel (13.11). Por eso
nunca hay hueco entre los dos colores: hay un FRENTE.

EL SITIO. Abajo, de lado a lado de la pantalla de 960 x 540, pegada al borde.
Los nodos mas bajos de los nueve niveles estan en y = -3 u, a 78 px del
borde, y lo que cuelga de un nodo -media huella, numero y barra de carga del
Puslar (18 #48)- llega a 61 px en el peor caso. Quedan 17: la barra mide 12
y nunca pisa un numero.

LAS PIEZAS, iguales en las tres propuestas, para que `T6-08` no dependa de
cual se elija (las medidas, en `ui_power_bar.json`):

    ui_power_bar_frame.png        960 x 12   la carcasa, fija, siempre entera
    ui_power_bar_fill_sheet.png   64 x 12 por celda; fila 0 el Jugador y
                                  fila 1 el Enemigo; 32 frames en columnas
    ui_power_bar_front_sheet.png  16 x 12 por celda, el frente, 32 frames;
                                  el reparto cae en x = 8

El relleno se lee por coordenada ABSOLUTA: el pixel x del interior usa la
columna (x - x0) % 64 de la tesela de su bando, sea del Jugador o del
Enemigo. Asi la trama y el dibujo no saltan cuando el frente se mueve, y el
Enemigo no es un espejo: es el mismo dibujo fluyendo hacia el otro lado. El
frente se pega encima, con su x = 8 en el pixel del reparto,
`x0 + round(reparto * (x1 - x0))`, ajustado a `snap`.

MOVIMIENTO (13.12). Es interfaz: la energia fluye. El bucle de 32 frames a
12 fps dura 2.67 s, por encima de los 2.4 s de un bucle de ambiente, y todo
avanza hacia el frente: la energia de cada bando empuja hacia el otro. El
frente sigue al reparto en continuo con la curva orbital; eso es de `T6-08`.

    py tools/gen_powerbar_v1.py          piezas, comprobaciones y vistas fijas
    py tools/gen_powerbar_v1.py --gif    y las animadas

----------------------------------------------------------------------------

LA TESIS DE LA v1: UNA TUBERIA DE PLASMA. La carcasa es el marco de 13.12
-2 px de `str_p[6]` con la esquina en escalera- y dentro corre el plasma de
cada bando, en la rampa de su linea de envio (`plasma_p`, `plasma_e`): un
cuerpo en el ancla, un hilo blanco que se retuerce y paquetes de luz que
corren hacia el frente. Donde se tocan, la costura arde en blanco y salta
chispas, que a veces rompen la carcasa: el choque se ve desde el otro lado
de la pantalla sin leer ni un numero.
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import BAYER8, RAIZ, dir_preview, rampa
from nova_paleta import RAMPAS, hex_a_rgb

ASSET = 'power_bar'

ANCHO  = 960            # la pantalla de referencia, entera
ALTO   = 12             # cabe en los 17 px libres bajo el nodo mas bajo
TESELA = 64             # multiplo de 8: la trama no salta entre teselas
FRAMES = 32             # 2.67 s a 12 fps: bucle de ambiente (13.12)
FPS    = 12
FRENTE = 16
CONTACTO = 8            # x del reparto dentro del frente

PANEL = np.array(hex_a_rgb('#11142A'), np.uint8)   # relleno de panel, 13.12
FONDO = np.array(hex_a_rgb('#0B0E1A'), np.uint8)

MUESTRAS = (0.5, 0.3, 0.78)    # repartos de las vistas fijas


# --- Lienzo por indices -------------------------------------------------------

class Tira:
    """Un trozo de barra en color, con alfa binario."""

    def __init__(self, ancho, alto=ALTO):
        self.rgb = np.zeros((alto, ancho, 3), np.uint8)
        self.on = np.zeros((alto, ancho), bool)

    def pon(self, x, y, color):
        if 0 <= y < self.rgb.shape[0] and 0 <= x < self.rgb.shape[1]:
            self.rgb[y, x] = color
            self.on[y, x] = True

    def rgba(self):
        a = np.zeros(self.rgb.shape[:2] + (4,), np.uint8)
        a[..., :3] = self.rgb
        a[..., 3] = np.where(self.on, 255, 0)
        a[~self.on, :3] = 0
        return a


def bayer(x, y):
    return BAYER8[y % 8, x % 8]


def fase(u, f, n, m, d):
    """Onda de n periodos por tesela que avanza m periodos por bucle hacia d
    (+1 a la derecha, -1 a la izquierda): 2m/n px por frame. Con n y m
    enteros, la tesela y el bucle cierran por construccion."""
    return 2 * math.pi * (n * u / TESELA - d * m * f / FRAMES)


def escalera(ancho, alto, borde=2):
    """Silueta con la esquina en escalera de 13.12 (4 + 4 px de pantalla,
    2 + 2 de arte) y su anillo de `borde` px. Devuelve (silueta, interior)."""
    y, x = np.mgrid[0:alto, 0:ancho]
    ex = np.minimum(x, ancho - 1 - x)
    ey = np.minimum(y, alto - 1 - y)
    corte = ((ey < 2) & (ex < 4)) | ((ey < 4) & (ex < 2))
    silueta = ~corte
    # interior: lo que queda a mas de `borde` px de cualquier hueco, en
    # distancia de Chebyshev
    dentro = silueta.copy()
    for dy in range(-borde, borde + 1):
        for dx in range(-borde, borde + 1):
            s = np.zeros_like(silueta)
            ys = slice(max(0, dy), alto + min(0, dy))
            yd = slice(max(0, -dy), alto + min(0, -dy))
            xs = slice(max(0, dx), ancho + min(0, dx))
            xd = slice(max(0, -dx), ancho + min(0, -dx))
            s[yd, xd] = silueta[ys, xs]
            fuera = np.ones_like(silueta)
            fuera[yd, xd] = False
            dentro &= s & ~fuera
    return silueta, dentro


# --- Montaje: la barra entera, como la monta Unity ---------------------------

def montar(piezas, reparto, f):
    """La barra a un reparto y un frame: carcasa, los dos rellenos por
    coordenada absoluta y el frente encima."""
    x0, x1, snap = piezas['x0'], piezas['x1'], piezas['snap']
    b = x0 + int(round(reparto * (x1 - x0) / snap)) * snap
    out = piezas['marco'].copy()
    rell = piezas['relleno']
    for x in range(x0, x1):
        t = rell['jugador' if x < b else 'enemigo'][f]
        col = t[:, (x - x0) % TESELA]
        m = col[:, 3] > 0
        out[m, x] = col[m]
    fr = piezas['frente'][f]
    for i in range(FRENTE):
        x = b - CONTACTO + i
        if 0 <= x < ANCHO:
            m = fr[:, i, 3] > 0
            out[m, x] = fr[m, i]
    return out


# --- Comprobaciones -------------------------------------------------------------

def colores_de(*nombres):
    s = set()
    for n in nombres:
        s |= {hex_a_rgb(h) for h in RAMPAS[n]}
    return s


def comprobar(piezas, version, permitidos):
    fallos = []
    todo = [piezas['marco']] + piezas['relleno']['jugador'] + \
        piezas['relleno']['enemigo'] + piezas['frente']
    for im in todo:
        if im.shape[0] != ALTO:
            fallos.append(f'alto {im.shape[0]}, no {ALTO}')
            break
        if not set(np.unique(im[..., 3])) <= {0, 255}:
            fallos.append('alfa no binario')
            break
    usados = set()
    for im in todo:
        usados |= {tuple(int(v) for v in c) for c in im[..., :3][im[..., 3] > 0]}
    fuera = usados - permitidos
    if fuera:
        fallos.append(f'{len(fuera)} colores fuera de paleta: '
                      + ', '.join('#%02X%02X%02X' % c for c in sorted(fuera)[:4]))
    for fac in ('jugador', 'enemigo'):
        fr = piezas['relleno'][fac]
        distintos = len({t.tobytes() for t in fr})
        if distintos < piezas.get('min_relleno', FRAMES):
            fallos.append(f'relleno {fac}: solo {distintos} frames distintos')
        # el bucle cierra: el frame siguiente al ultimo es el 0
        if not np.array_equal(piezas['dibujo_relleno'](fac, FRAMES), fr[0]):
            fallos.append(f'relleno {fac}: el bucle no cierra')
        # la tesela cierra: el dibujo en u + 64 es el mismo
        if not np.array_equal(piezas['dibujo_relleno'](fac, 5, desfase=TESELA), fr[5]):
            fallos.append(f'relleno {fac}: la tesela no cierra')
    distintos = len({t.tobytes() for t in piezas['frente']})
    if distintos < piezas.get('min_frente', FRAMES):
        fallos.append(f'frente: solo {distintos} frames distintos')
    if not np.array_equal(piezas['dibujo_frente'](FRAMES), piezas['frente'][0]):
        fallos.append('frente: el bucle no cierra')
    x0, x1, snap = piezas['x0'], piezas['x1'], piezas['snap']
    if (x1 - x0) % snap or (x1 - x0) % TESELA % snap:
        fallos.append('el interior no es multiplo del paso')
    # el reparto 50/50 cae en el centro de la pantalla
    if abs((x0 + (x1 - x0) // 2) - ANCHO // 2) > 0:
        fallos.append('el 50 % no cae en el centro')
    if fallos:
        raise SystemExit(f'ART-UI-POWERBAR v{version} no pasa:\n  ' + '\n  '.join(fallos))
    return len(usados)


# --- Hojas -------------------------------------------------------------------

def exportar(piezas, version):
    d = os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(piezas['marco'], 'RGBA').save(os.path.join(d, 'ui_power_bar_frame.png'))
    hoja = np.vstack([np.hstack(piezas['relleno'][fac]) for fac in ('jugador', 'enemigo')])
    Image.fromarray(hoja, 'RGBA').save(os.path.join(d, 'ui_power_bar_fill_sheet.png'))
    Image.fromarray(np.hstack(piezas['frente']), 'RGBA').save(
        os.path.join(d, 'ui_power_bar_front_sheet.png'))
    with open(os.path.join(d, 'ui_power_bar.json'), 'w') as fh:
        json.dump({'width': ANCHO, 'height': ALTO, 'x0': piezas['x0'], 'x1': piezas['x1'],
                   'snap': piezas['snap'], 'tileWidth': TESELA, 'frames': FRAMES,
                   'fps': FPS, 'frontWidth': FRENTE, 'frontContact': CONTACTO,
                   'fillRows': ['player', 'enemy']}, fh, indent=1)
    return d


# --- Vistas ------------------------------------------------------------------

def ampliar(im, k):
    return np.repeat(np.repeat(im, k, axis=0), k, axis=1)


def sobre(fondo, barra, y0):
    out = fondo.copy()
    m = barra[..., 3] > 0
    out[y0:y0 + ALTO][m] = barra[..., :3][m]
    return out


def sprite(*ruta):
    return np.asarray(Image.open(os.path.join(RAIZ, 'art', 'nodes', *ruta)).convert('RGBA'))


def pegar(lienzo, im, cx, cy):
    h, w = im.shape[:2]
    x0, y0 = cx - w // 2, cy - h // 2
    m = im[..., 3] > 0
    lienzo[y0:y0 + h, x0:x0 + w][m] = im[..., :3][m]


def tablero(barra):
    """El cielo del mapa 1 con los nodos mas grandes en la fila mas baja de
    los niveles (y = -3 u, fila 462) y la barra abajo. La caja punteada es
    donde cae el numero de Energia (18 #48), que aqui no se dibuja."""
    fondo = np.asarray(Image.open(os.path.join(
        RAIZ, 'art', 'bg', 'fondos', 'v2', 'bg_section1.png')).convert('RGB')).copy()
    fila = 270 + 192
    nodos = ((sprite('estrella', 'v1', 'str_lvl5_player.png'), 150, 72),
             (sprite('pulsar', 'v2', 'pul_lvl3_player.png'), 330, 68),
             (sprite('magnetar', 'v1', 'mag_lvl5_neutral.png'), 480, 67),
             (sprite('estrella', 'v1', 'str_lvl5_enemy.png'), 640, 72),
             (sprite('pulsar', 'v2', 'pul_lvl3_enemy.png'), 810, 68))
    gris = np.array(hex_a_rgb(RAMPAS['neutral'][4]), np.uint8)
    for im, x, huella in nodos:
        pegar(fondo, im, x, fila)
        top = fila + (huella + 1) // 2 + 2
        for xx in range(x - 12, x + 12, 2):        # caja del numero, x2 de alto
            fondo[top, xx] = gris
            fondo[top + 19, xx] = gris
    return sobre(fondo, barra, 540 - ALTO)


def vistas(piezas, version, gif):
    d = os.path.join(dir_preview('ui_' + ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    # tres repartos, la barra entera a x2 (como a 1080p), sobre el espacio
    filas = []
    for r in MUESTRAS:
        bg = np.empty((ALTO + 8, ANCHO, 3), np.uint8)
        bg[:] = FONDO
        filas.append(sobre(bg, montar(piezas, r, 0), 4))
    Image.fromarray(ampliar(np.vstack(filas), 2)).save(os.path.join(d, 'barra_x2.png'))
    # el frente de cerca: 8 frames, x6
    det = []
    for f in range(0, FRAMES, 4):
        bg = np.empty((ALTO + 4, 160, 3), np.uint8)
        bg[:] = FONDO
        det.append(sobre(bg, montar(piezas, 0.5, f)[:, 400:560], 2))
    Image.fromarray(ampliar(np.vstack(det), 6)).save(os.path.join(d, 'frente_x6.png'))
    # el tablero entero, a x2: lo que se ve a 1080p
    Image.fromarray(ampliar(tablero(montar(piezas, 0.42, 0)), 2)).save(
        os.path.join(d, 'tablero_1080p.png'))
    Image.fromarray(ampliar(tablero(montar(piezas, 0.42, 0))[400:540, 60:600], 3)).save(
        os.path.join(d, 'tablero_x3.png'))
    if not gif:
        return d
    dur = round(1000 / FPS)
    n = FRAMES * 3
    cuadros, cerca = [], []
    for i in range(n):
        r = 0.5 + 0.22 * math.sin(2 * math.pi * i / n)
        t = tablero(montar(piezas, r, i % FRAMES))
        cuadros.append(Image.fromarray(ampliar(t[380:540], 2)))
        cerca.append(Image.fromarray(ampliar(montar(piezas, r, i % FRAMES)[..., :3][:, 250:710], 4)))
    cuadros[0].save(os.path.join(d, 'tablero.gif'), save_all=True,
                    append_images=cuadros[1:], duration=dur, loop=0)
    cerca[0].save(os.path.join(d, 'barra_x4.gif'), save_all=True,
                  append_images=cerca[1:], duration=dur, loop=0)
    return d


def comparativa():
    """Las propuestas que existan, una bajo otra, a x3, en el centro."""
    filas = []
    for v in (1, 2, 3):
        p = os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{v}')
        if not os.path.isdir(p):
            continue
        mod = __import__(f'gen_powerbar_v{v}')
        piezas = mod.piezas() if v != 1 else piezas_v1()
        for r in (0.5, 0.3):
            bg = np.empty((ALTO + 6, 320, 3), np.uint8)
            bg[:] = FONDO
            filas.append(sobre(bg, montar(piezas, r, 0)[:, 320:640] if r == 0.5
                               else montar(piezas, r, 0)[:, 160:480], 3))
        filas.append(np.zeros((4, 320, 3), np.uint8))
    if filas:
        Image.fromarray(ampliar(np.vstack(filas), 3)).save(
            os.path.join(dir_preview('ui_' + ASSET), 'comparativa_x3.png'))


def ejecutar(construir, version, nombre, permitidos):
    piezas = construir()
    colores = comprobar(piezas, version, permitidos)
    d = exportar(piezas, version)
    vistas(piezas, version, '--gif' in sys.argv)
    comparativa()
    print(f'v{version} «{nombre}»: interior {piezas["x0"]}-{piezas["x1"]}, paso {piezas["snap"]}, '
          f'{colores} colores  -> {os.path.relpath(d, RAIZ)}')


# =============================================================================
# v1 «El conducto»
# =============================================================================

PLASMA = {'jugador': rampa('plasma_p'), 'enemigo': rampa('plasma_e')}
DIR = {'jugador': 1, 'enemigo': -1}
MARCO = rampa('str_p')[6]
X0, X1 = 4, ANCHO - 4          # el interior, dentro del marco en escalera
Y0, Y1 = 2, 10                 # 8 filas de plasma


def marco_v1():
    silueta, dentro = escalera(ANCHO, ALTO)
    im = np.zeros((ALTO, ANCHO, 4), np.uint8)
    im[silueta & ~dentro, :3] = MARCO
    im[dentro, :3] = PANEL
    im[silueta, 3] = 255
    return im


def relleno_v1(fac, f, desfase=0):
    r, d = PLASMA[fac], DIR[fac]
    t = Tira(TESELA)
    for x in range(TESELA):
        u = x + desfase
        # el hilo: dos armonicos a velocidades distintas, se retuerce
        yh = 5.5 + 1.6 * math.sin(fase(u, f, 1, 1, d) + 0.4) \
            + 0.9 * math.sin(fase(u, f, 3, 2, d) + 1.7)
        yh = int(round(min(max(yh, Y0 + 1), Y1 - 2)))
        paquete = math.sin(fase(u, f, 2, 3, d)) > 0.93
        for y in range(Y0, Y1):
            c = abs(y + 0.5 - (Y0 + Y1) / 2)
            if c >= 3.5:
                b = 5 if bayer(u, y) < 0.7 else 4
            elif c >= 2.5:
                b = 4
            else:
                b = 3
                brillo = 0.5 + 0.5 * math.sin(fase(u, f, 4, 3, d) + 0.9 * y)
                if brillo * 0.55 > bayer(u, y):
                    b = 2
            if y == yh:
                b = 0 if paquete else 1
            elif abs(y - yh) == 1 and b > 2:
                b = 2
            t.pon(x, y, r[b])
    return t.rgba()


def frente_v1(f):
    pj, pe = PLASMA['jugador'], PLASMA['enemigo']
    t = Tira(FRENTE)
    rng = np.random.default_rng(7000 + f % FRAMES)
    ancho = 2 if f % 8 in (0, 1) else 1       # la costura respira cada 0.67 s
    for y in range(Y0, Y1):
        for i in range(-ancho - 1, ancho + 1):
            x = CONTACTO + i
            lado = pj if x < CONTACTO else pe
            if -ancho <= i < ancho:
                t.pon(x, y, lado[0] if (y + f) % 2 else lado[1])
            else:
                t.pon(x, y, lado[1])
    # chispas: tres por frame, del lado de cada uno, y una de cada cuatro
    # rompe la carcasa por arriba o por abajo
    for k in range(3):
        x = int(rng.integers(2, FRENTE - 2))
        y = int(rng.integers(Y0, Y1))
        lado = pj if x < CONTACTO else pe
        t.pon(x, y, lado[0])
    if f % 4 == 2:
        x = CONTACTO - 1 + int(rng.integers(0, 2))
        y = 1 if rng.random() < 0.5 else ALTO - 2
        t.pon(x, y, pj[0] if x < CONTACTO else pe[0])
        t.pon(x, y - 1 if y == 1 else y + 1, (pj if x < CONTACTO else pe)[1])
    return t.rgba()


def piezas_v1():
    return {
        'marco': marco_v1(),
        'relleno': {fac: [relleno_v1(fac, f) for f in range(FRAMES)]
                    for fac in ('jugador', 'enemigo')},
        'frente': [frente_v1(f) for f in range(FRAMES)],
        'dibujo_relleno': relleno_v1,
        'dibujo_frente': frente_v1,
        'x0': X0, 'x1': X1, 'snap': 1,
    }


if __name__ == '__main__':
    ejecutar(piezas_v1, 1, 'El conducto',
             colores_de('plasma_p', 'plasma_e') | {tuple(MARCO), tuple(PANEL)})
