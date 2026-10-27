"""NOVA - Generador del anillo de radio del Magnetar (ART-WLD-RAD-*), v1: EL CERCO.

Ref: NOVA_Assets_Diseno.md A.4.1 · NOVA_Nodos.md 4.4 · A.8 #1

    py tools/gen_radio_v1.py          los 15 anillos
    py tools/gen_radio_v1.py --vista  ademas, las vistas en art/_preview/radio/v1/

QUE TIENE QUE DECIR. Donde empiezan los disparos, al pixel: un proton que pasa
a 0.1 u del borde no recibe ninguno (A.4.1). Es informacion para trazar una
ruta, no decoracion, y en un tablero con tres o cuatro Magnetares no puede
ocupar la pantalla.

Tres piezas, de fuera adentro:

    borde       una linea de 1 px cuyo lado de FUERA es el radio exacto. Los
                cinco radios caen en pixel entero a PPU 64 (96..160 px), asi
                que el borde dibujado y la distancia que compara
                MagnetarTurret coinciden sin redondeo. Sin contorno: en un
                trazo de 1 px el contorno no separa, tacha (como veta_luz)
    cerco       limaduras de hierro por DENTRO del borde: trazos cortos
                orientados por el campo del dipolo, la misma regla que gira
                las esquirlas del sprite (A.2.4). Erizados arriba y abajo,
                peinados a los lados. Dicen de que lado esta el peligro y que
                ese peligro es un campo magnetico, sin pintar el disco
    marcas      el nivel. Una esquirla en miniatura por cada esquirla del
                sprite, en el MISMO angulo en que flota y con el mismo giro. Cada esquirla `i`
                del Magnetar ya es el radio del nivel `i+1` comprimido a la
                celda (A.2.4), asi que la marca es la esquirla proyectada a su
                alcance de verdad: el sprite y el anillo cuentan lo mismo en el
                mismo sitio. Hacia fuera, para no tocar la frontera

UNO POR FACCION (T.7 #34, cerrado el 2026-10-27). Neutral y Enemigo son tenues
porque se ven siempre; el del Jugador es brillante porque solo aparece cuando
lo pide. Mismo dibujo, otra rampa y otra densidad: la del propio Magnetar
(`mag_*`), no la de la Estrella, para que el anillo se lea como suyo.

Alfa binario, sin glow ni bloom, lienzo de 384 con el centro en la esquina de
pixel 192, igual que el pivote del sprite de 96 (A.4.1, Resolucion).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, rampa

import gen_magnetar_v1 as MAG

ASSET = 'radius'
VERSION = 1

LADO = 384
C = LADO / 2.0      # 192.0: esquina de pixel, como el centro de la celda de 96
PPU = 64

RADIO_U = {1: 1.5, 2: 1.75, 3: 2.0, 4: 2.25, 5: 2.5}   # 4.4, cerrados el 2026-10-02

# Por faccion, solo la rampa y sus bandas: borde, marca y las dos filas de
# limaduras. La GEOMETRIA es la misma en las tres; el propio se distingue por
# brillo, no por grosor (decidido con el usuario el 2026-10-27).
FACCIONES = {
    'neutral': dict(ramp='mag_n', borde=3, marca=2, filas=(4, 5)),
    'enemy':   dict(ramp='mag_e', borde=3, marca=2, filas=(4, 5)),
    'player':  dict(ramp='mag_p', borde=1, marca=1, filas=(2, 3)),
}

# Limaduras del cerco: (distancia por dentro del borde, paso a lo largo del
# arco, largo del trazo), en px. Dos filas: la de fuera, densa y clara; la de
# dentro, rala y oscura. Asi el campo se apaga hacia el centro sin trama.
LIMADURAS = ((3.0, 5.0, 3.6), (6.5, 8.0, 3.0))

# Marca de nivel: una esquirla en miniatura, semiejes en px, con su centro a
# MARCA_D px por fuera del borde.
MARCA_LARGO = 4.6
MARCA_ANCHO = 2.3
MARCA_D = 5.5

# Desorden de las limaduras, en fraccion del paso y en px: sin el, un cerco de
# trazos iguales y equiespaciados se lee como la escala de una regla, que es
# interfaz. Determinista: secuencia aurea, como el reparto de las esquirlas.
AUREO = 0.6180339887
JITTER_PASO = 0.45
JITTER_D = 1.2
JITTER_LARGO = 1.1


def _trazo(m, cx, cy, giro, largo, R):
    """Un trazo recto de 1 px centrado en (cx, cy), por dentro del borde."""
    ux, uy = math.cos(giro), math.sin(giro)
    for k in np.linspace(-largo / 2, largo / 2, int(largo * 4) + 1):
        px, py = int(math.floor(cx + ux * k)), int(math.floor(cy + uy * k))
        if math.hypot(px + 0.5 - C, py + 0.5 - C) <= R - 1.5:
            m[py, px] = True


def _limaduras(R, distancia, paso, largo):
    """Mascara de una fila de limaduras orientadas por el campo del dipolo.

    Es la misma regla que orienta las esquirlas del sprite (A.2.4,
    `_giro_campo`): en los polos el trazo sale radial, en el ecuador se tumba
    a lo largo del borde. Con el eje vertical del Magnetar, arriba y abajo el
    cerco erizado y a los lados peinado: el dibujo de las limaduras de hierro
    sobre un iman.
    """
    m = np.zeros((LADO, LADO), bool)
    n = max(int(round(2 * math.pi * (R - distancia) / paso)), 8)
    for i in range(n):
        # tres numeros en [-1, 1) por trazo, de la secuencia aurea
        j1, j2, j3 = (2 * ((i * AUREO * k + 0.5 * k) % 1.0) - 1 for k in (1, 2, 3))
        a = 2 * math.pi * (i + JITTER_PASO * j1) / n
        r = R - distancia + JITTER_D * j2
        giro = math.radians(MAG._giro_campo(math.degrees(a)))
        _trazo(m, C + math.cos(a) * r, C + math.sin(a) * r, giro, largo + JITTER_LARGO * j3, R)
    return m


def angulos_esquirla(nivel):
    """Angulos de pantalla (y hacia abajo) de las esquirlas 0..nivel-1 del sprite."""
    return [math.radians(MAG.ANG_0 + MAG.ANG_PASO * i) for i in range(nivel)]


def anillo(nivel, faccion):
    """RGBA (LADO, LADO) del anillo de un nivel y una faccion."""
    cfg = FACCIONES[faccion]
    ramp = rampa(cfg['ramp'])
    R = RADIO_U[nivel] * PPU

    y, x = np.mgrid[0:LADO, 0:LADO]
    dx, dy = x + 0.5 - C, y + 0.5 - C
    d = np.hypot(dx, dy)
    ang = np.arctan2(dy, dx)

    out = np.zeros((LADO, LADO, 4), np.uint8)

    def poner(m, banda):
        out[m, :3] = ramp[banda]
        out[m, 3] = 255

    # cerco: dos filas de limaduras, la de dentro mas oscura
    for (distancia, paso, largo), banda in reversed(list(zip(LIMADURAS, cfg['filas']))):
        poner(_limaduras(R, distancia, paso, largo), banda)

    # borde: los pixeles cuyo centro cae en (R-1, R]; su lado de fuera es R
    poner((d > R - 1) & (d <= R), cfg['borde'])

    # marcas: una por esquirla, hacia fuera
    # marcas: la esquirla del sprite proyectada a su alcance real, en su
    # angulo y con el giro del campo. Rombo de dos caras: la que mira a la luz
    # global (arriba a la izquierda) una banda mas clara que la otra.
    for a in angulos_esquirla(nivel):
        giro = math.radians(MAG._giro_campo(math.degrees(a)))
        mx, my = C + math.cos(a) * (R + MARCA_D), C + math.sin(a) * (R + MARCA_D)
        u = (dx - (mx - C)) * math.cos(giro) + (dy - (my - C)) * math.sin(giro)
        v = -(dx - (mx - C)) * math.sin(giro) + (dy - (my - C)) * math.cos(giro)
        rombo = (np.abs(u) / MARCA_LARGO + np.abs(v) / MARCA_ANCHO <= 1.0) & (d > R)
        # normal de cada cara en pantalla, contra la luz L = (-0.45, -0.60)
        nx, ny = -math.sin(giro), math.cos(giro)
        clara = (v * (nx * -0.45 + ny * -0.60) >= 0)
        poner(rombo & clara, cfg['marca'])
        poner(rombo & ~clara, cfg['marca'] + 1)

    return out


def ruta(faccion, nivel, raiz):
    return os.path.join(raiz, f'world_radius_{faccion}_lvl{nivel}.png')


def verifica(img, nivel):
    """El borde es el radio exacto, cerrado y sin tocar el lienzo."""
    R = RADIO_U[nivel] * PPU
    a = img[..., 3] > 0
    if a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any():
        raise SystemExit(f'N{nivel}: toca el borde del lienzo')
    y, x = np.mgrid[0:LADO, 0:LADO]
    d = np.hypot(x + 0.5 - C, y + 0.5 - C)
    borde = (d > R - 1) & (d <= R)
    if not a[borde].all():
        raise SystemExit(f'N{nivel}: el borde tiene huecos')
    # cerrado en 8-vecindad: cada pixel del borde tiene dos vecinos del borde
    pad = np.pad(borde, 1)
    vec = sum(np.roll(np.roll(pad, i, 0), j, 1) for i in (-1, 0, 1) for j in (-1, 0, 1)
              if (i, j) != (0, 0))[1:-1, 1:-1]
    if (vec[borde] < 2).any():
        raise SystemExit(f'N{nivel}: el borde no cierra')
    # nada de lo que se dibuja por dentro pasa del radio, salvo las marcas
    return int(borde.sum()), int(a.sum())


def vistas(gen=None, version=VERSION):
    """Las tres facciones a 1x sobre el fondo, con su Magnetar, y un trozo a 2x.

    `gen(nivel, faccion)` es el anillo: otra version la reutiliza con el suyo.
    """
    gen = gen or anillo
    from nova_paleta import FONDO, hex_a_rgb
    bg = np.array(hex_a_rgb(FONDO), np.uint8)
    d = os.path.join(RAIZ, 'art', '_preview', 'radio', f'v{version}')
    os.makedirs(d, exist_ok=True)

    def sobre(lienzo, rgba, cx, cy):
        h, w = rgba.shape[:2]
        x0, y0 = cx - w // 2, cy - h // 2
        x1, y1 = max(x0, 0), max(y0, 0)
        x2, y2 = min(x0 + w, lienzo.shape[1]), min(y0 + h, lienzo.shape[0])
        sub = rgba[y1 - y0:y2 - y0, x1 - x0:x2 - x0]
        m = sub[..., 3] > 0
        lienzo[y1:y2, x1:x2][m] = sub[..., :3][m]

    def sprite(fac, nivel):
        return np.asarray(Image.open(os.path.join(
            RAIZ, 'art', 'nodes', 'magnetar', 'v1', f'mag_lvl{nivel}_{fac}.png')).convert('RGBA'))

    # 1. Tablero de 960 x 540: tres Magnetares de las tres facciones, como en
    #    partida (el propio, seleccionado), y dos nodos alrededor.
    tab = np.empty((540, 960, 3), np.uint8)
    tab[:] = bg
    escena = [('neutral', 3, 480, 270), ('enemy', 5, 760, 160), ('player', 2, 190, 360)]
    for fac, n, x, y in escena:
        sobre(tab, gen(n, fac), x, y)
    for fac, n, x, y in escena:
        sobre(tab, sprite(fac, n), x, y)
    for f, (nodo, v, pre, n, fac, x, y) in enumerate([
            ('estrella', 1, 'str', 3, 'enemy', 640, 400), ('pulsar', 2, 'pul', 2, 'player', 330, 150),
            ('asteroide', 1, 'ast', 0, 'neutral', 600, 250)]):
        im = np.asarray(Image.open(os.path.join(
            RAIZ, 'art', 'nodes', nodo, f'v{v}', f'{pre}_lvl{n}_{fac}.png')).convert('RGBA'))
        sobre(tab, im, x, y)
    Image.fromarray(tab, 'RGB').save(os.path.join(d, 'radio_tablero.png'))
    Image.fromarray(np.repeat(np.repeat(tab, 2, 0), 2, 1), 'RGB').save(os.path.join(d, 'radio_tablero_x2.png'))

    # 2. Los 15 a 1x, una fila por faccion, cada uno con su Magnetar.
    filas = []
    for fac in FACCIONES:
        celdas = []
        for n in RADIO_U:
            c = np.empty((LADO, LADO, 3), np.uint8)
            c[:] = bg
            sobre(c, gen(n, fac), LADO // 2, LADO // 2)
            sobre(c, sprite(fac, n), LADO // 2, LADO // 2)
            celdas.append(c)
        filas.append(np.concatenate(celdas, 1))
    Image.fromarray(np.concatenate(filas, 0), 'RGB').save(os.path.join(d, 'radio_todos.png'))

    # 3. Detalle a 4x del cuadrante de las marcas del N5 enemigo y propio
    det = []
    for fac in ('enemy', 'player'):
        c = np.empty((LADO, LADO, 3), np.uint8)
        c[:] = bg
        sobre(c, gen(5, fac), LADO // 2, LADO // 2)
        sobre(c, sprite(fac, 5), LADO // 2, LADO // 2)
        det.append(np.repeat(np.repeat(c[16:208, 16:208], 3, 0), 3, 1))
    Image.fromarray(np.concatenate(det, 1), 'RGB').save(os.path.join(d, 'radio_detalle_x3.png'))
    print('vistas en', d)


def main():
    raiz = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    os.makedirs(raiz, exist_ok=True)
    print(f"{'':10} {'radio':>6} {'borde':>6} {'pixeles':>8}")
    for fac in FACCIONES:
        for n in RADIO_U:
            img = anillo(n, fac)
            borde, total = verifica(img, n)
            Image.fromarray(img, 'RGBA').save(ruta(fac, n, raiz))
            print(f'{fac:>7} N{n} {RADIO_U[n] * PPU:6.0f} {borde:6d} {total:8d}')
    if '--vista' in sys.argv:
        vistas()


if __name__ == '__main__':
    main()
