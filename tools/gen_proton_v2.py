"""NOVA - ART-WLD-PRO-P y ART-WLD-PRO-E v2, el proton: «La bolsa».

Ref: NOVA_Assets_Diseno.md A.4.2 · NOVA_Estados_Animaciones.md 14.3 · Spec 6.1

Un proton de verdad, no un atomo: TRES QUARKS -dos u y un d- atados por
gluones dentro de una bolsa, que es el modelo con el que la fisica dibuja un
nucleon. Veinte hojas, una por bando y talla -diez tallas, una por cada 5 de
Carga-, `art/world/proton/v2/world_proton_<faccion>_<talla>.png`, en celdas
de 48:

    fila 0  cuerpo   16 frames, bucle   los quarks girando, los gluones vibrando
    fila 1  gotas     4 frames          la estela, la misma de la v1

Y una hoja de cifras propia, `world_charge_digits.png` + `.json`.

LO QUE LO SEPARA DE UN ATOMO. Un atomo se dibuja con orbitas de electrones
alrededor de un nucleo; aqui no hay ni una orbita ni un nucleo: hay tres
cuentas iguales de dos en dos y distintas la tercera, y un muelle entre cada
par. El muelle es como se dibuja un gluon en un diagrama de Feynman, y es la
firma: ninguna otra cosa del tablero tiene una linea que vibra en zigzag.

  - LA BOLSA. Una membrana de trama, densa en el borde y vacia por dentro.
    Es luz, no materia (`glow`): no da contorno ni engorda el hitbox, y deja
    el centro limpio para la Carga.
  - LOS QUARKS: uud. Dos encendidos (`calor`) y uno apagado una banda. Son
    las unicas piezas solidas, con su luz global y su contorno, asi que el
    ojo los cuenta: tres.
  - LOS GLUONES siguen la circunferencia de los quarks, nunca la cruzan: el
    triangulo recto pasaria por encima del numero. La onda viaja por el
    muelle con fase entera, y cierra el bucle.
  - GIRA SU CONTENIDO, NO SU SPRITE. Los quarks dan una vuelta por bucle y
    respiran medio pixel; la luz sigue siendo la `L` global en cada frame
    porque cada quark se ilumina donde esta. Es el espin del proton sin
    girar un solo transform.

LAS CIFRAS. Jersey 10 no convencio al usuario en la Carga (2026-11-01). Las
cifras de NOVA se calculan como todo lo demas: 5 x 7 px de trazo 1, el 1
estrecho de 3, en `neutral[1]` arriba y `neutral[2]` en las dos filas de abajo
-un degradado de dos bandas que les da cuerpo- y contorno de 1 px. Sin
trama: compiten con el fondo. `ProtonVisual` las compone por sprites con el
avance del .json, sin TextMeshPro. Si convencen, la misma hoja sirve para la
Energia de los nodos (una sola familia en el tablero).

    py tools/gen_proton_v2.py          hojas, cifras y vistas fijas
    py tools/gen_proton_v2.py --gif    y las animadas
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import (CONTORNO, RAIZ, a_rgba, dir_preview, esfera, lienzo,
                       rampa, tramar, _malla)
from nova_paleta import FONDO, hex_a_rgb
import gen_proton_v1 as v1

ASSET   = 'proton'
VERSION = 2

CELDA   = 48
FRAMES  = 16
GOTAS   = v1.GOTAS
PASO_F  = v1.PASO_F
FPS     = 12
C       = CELDA / 2

FACCIONES = {'player': 'plasma_p', 'enemy': 'plasma_e'}

# --- Las cifras ----------------------------------------------------------------

GLIFOS = {
    '0': ('.###.', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.'),
    '1': ('.#.', '##.', '.#.', '.#.', '.#.', '.#.', '###'),
    '2': ('.###.', '#...#', '....#', '...#.', '..#..', '.#...', '#####'),
    '3': ('####.', '....#', '....#', '.###.', '....#', '....#', '####.'),
    '4': ('...#.', '..##.', '.#.#.', '#..#.', '#####', '...#.', '...#.'),
    '5': ('#####', '#....', '####.', '....#', '....#', '#...#', '.###.'),
    '6': ('..##.', '.#...', '#....', '####.', '#...#', '#...#', '.###.'),
    '7': ('#####', '....#', '...#.', '..#..', '..#..', '.#...', '.#...'),
    '8': ('.###.', '#...#', '#...#', '.###.', '#...#', '#...#', '.###.'),
    '9': ('.###.', '#...#', '#...#', '.####', '....#', '...#.', '.##..'),
}
ALTO_CIFRA = 7
NEUTRAL = rampa('neutral')


def _glifo(c):
    return np.array([[ch == '#' for ch in fila] for fila in GLIFOS[c]], bool)


def texto(s):
    """La Carga como RGBA, con contorno: cifras separadas por 1 px que el
    contorno ocupa, asi que nunca se tocan."""
    gl = [_glifo(c) for c in s]
    w = sum(g.shape[1] for g in gl) + (len(gl) - 1) + 2
    h = ALTO_CIFRA + 2
    m = np.zeros((h, w), bool)
    x = 1
    for g in gl:
        m[1:1 + ALTO_CIFRA, x:x + g.shape[1]] = g
        x += g.shape[1] + 1
    borde = np.zeros_like(m)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            borde |= np.roll(np.roll(m, dy, 0), dx, 1)
    borde &= ~m
    img = np.zeros((h, w, 4), np.uint8)
    filas = np.arange(h)[:, None].repeat(w, 1)
    img[m, :3] = np.where((filas[m] >= 1 + ALTO_CIFRA - 2)[:, None],
                          NEUTRAL[2], NEUTRAL[1])
    img[borde, :3] = CONTORNO
    img[m | borde, 3] = 255
    return img


def hoja_cifras(destino):
    """Las diez cifras en celdas de 7 x 9 (5 + contorno), alineadas a la
    izquierda, y su avance en el .json, de 0 a 9: el 1 avanza 4, las demas 6."""
    celdas, avance = [], []
    for c in '0123456789':
        t = texto(c)
        cel = np.zeros((ALTO_CIFRA + 2, 7, 4), np.uint8)
        cel[:, :t.shape[1]] = t
        celdas.append(cel)
        avance.append(_glifo(c).shape[1] + 1)
    Image.fromarray(np.hstack(celdas), 'RGBA').save(
        os.path.join(destino, 'world_charge_digits.png'))
    # Lista y no diccionario: es lo que JsonUtility de Unity sabe leer (T6-04).
    with open(os.path.join(destino, 'world_charge_digits.json'), 'w') as f:
        json.dump({'cellWidth': 7, 'cellHeight': ALTO_CIFRA + 2, 'advance': avance,
                   'note': 'avance de 0 a 9; contorno de 1 px incluido en la celda, '
                           'y dos cifras seguidas comparten la columna de contorno'}, f, indent=1)


# --- El cuerpo -------------------------------------------------------------------

# DIEZ TALLAS, UNA POR CADA 5 DE CARGA (pedido del usuario, 2026-11-01): 1-5,
# 6-10, ..., 46-50, y de 51 a 100 la mayor. El proton crece y encoge con su
# Carga en escalones que se ven, no en tres saltos.
#
# La talla 10 es la `l` de la primera entrega, ni un pixel mas: el usuario no
# quiere un 50 exagerado. La 1 es la mas pequena en la que los tres quarks y
# sus muelles se siguen contando. Entre las dos, cada parametro se interpola
# en linea recta y las espiras se redondean.
#
# Con diez tallas el numero ya no cabe siempre en el hueco: dos cifras miden
# 13 px y obligarian a todas las tallas de 10-49 a medir lo mismo. Asi que los
# quarks y los muelles PASAN POR DETRAS DEL NUMERO. Las cifras van encima, con
# contorno, y se leen igual; el cuerpo es el que gira alrededor de su Carga.
#
#   R      radio al que van los quarks
#   rq     radio de un quark
#   bolsa  radio de la membrana
#   vueltas  espiras de cada gluon
#   gota   radio de la gota recien soltada
TALLAS_N    = 10
CARGA_PASO  = 5
MENOR = dict(R=6.0,  rq=1.8, bolsa=9.2,  vueltas=2, gota=2.4)
MAYOR = dict(R=15.2, rq=3.2, bolsa=20.0, vueltas=5, gota=3.8)


def _talla(k):
    u = (k - 1) / (TALLAS_N - 1)
    p = {c: MENOR[c] + (MAYOR[c] - MENOR[c]) * u for c in MENOR}
    p['vueltas'] = int(round(p['vueltas']))
    return p


TALLAS = {k: _talla(k) for k in range(1, TALLAS_N + 1)}
OID_Q = 20


def talla_de(carga):
    """1-5 -> 1, 6-10 -> 2, ..., 46-50 -> 10, y de 51 en adelante tambien 10."""
    return min(max((carga + CARGA_PASO - 1) // CARGA_PASO, 1), TALLAS_N)


def radio_bolsa_px(k):
    """Hasta donde tapa la bolsa: una gota no se ve hasta salir de ella."""
    return int(math.ceil(TALLAS[k]['bolsa'] + 2))


def quarks(p, t):
    """Posicion y sabor de los tres quarks en t: una vuelta por bucle y una
    respiracion de medio pixel en el radio, de frecuencia entera."""
    out = []
    for j, sabor in enumerate('uud'):
        a = -math.pi / 2 + t + 2 * math.pi * j / 3
        r = p['R'] + 0.5 * math.sin(2 * t + 2.1 * j)
        out.append((C + r * math.cos(a), C + r * math.sin(a), a, sabor))
    return out


def gluones(lz, p, ramp, qs, t):
    """Un muelle entre cada par de quarks, por la circunferencia. Pixeles de
    1 px en `glow`: la cresta de la onda en la banda 2, el valle en la 4. La
    amplitud baja con la talla, para que el muelle no se coma una bolsa chica."""
    S = lz['S']
    n = len(qs)
    amp = 0.9 + 0.4 * (p['R'] - MENOR['R']) / (MAYOR['R'] - MENOR['R'])
    for j in range(n):
        a0 = qs[j][2]
        a1 = qs[(j + 1) % n][2]
        if a1 <= a0:
            a1 += 2 * math.pi
        hueco = (p['rq'] + 1.5) / p['R']
        a0, a1 = a0 + hueco, a1 - hueco
        for s in np.linspace(0.0, 1.0, 160):
            a = a0 + (a1 - a0) * s
            fase = 2 * math.pi * p['vueltas'] * s - 2 * t
            r = p['R'] + amp * math.sin(fase)
            x, y = int(C + r * math.cos(a)), int(C + r * math.sin(a))
            if 0 <= x < S and 0 <= y < S and not lz['lleno'][y, x]:
                lz['col'][y, x] = ramp[2 if math.cos(fase) > 0 else 4]
                lz['glow'][y, x] = True


def bolsa(lz, p, ramp, t):
    """La membrana: trama densa en el borde que se vacia hacia dentro en
    3 px y se apaga hacia fuera en 2. Late con la misma frecuencia entera."""
    dx, dy = _malla(lz, C, C)
    r = np.hypot(dx, dy)
    Rb = p['bolsa'] + 0.4 * math.sin(2 * t)
    d = r - Rb
    dens = np.where(d < 0, np.clip(1 + d / 3.2, 0, 1) ** 2.2,
                    np.clip(1 - d / 2.2, 0, 1) ** 1.6) * 0.85
    dens[r < p['R'] + p['rq'] + 1] = 0
    tramar(lz, dens, ramp, banda_int=3, banda_ext=5, corte=0.55)


def cuerpo(ramp, talla, t):
    p = TALLAS[talla]
    lz = lienzo(CELDA)
    qs = quarks(p, t)
    bolsa(lz, p, ramp, t)
    for x, y, _, sabor in qs:
        if sabor == 'u':
            esfera(lz, x, y, p['rq'], ramp, OID_Q, calor=0.8)
        else:
            esfera(lz, x, y, p['rq'], ramp, OID_Q + 1, apagado=1.0)
    lz['glow'] &= ~lz['lleno']
    gluones(lz, p, ramp, qs, t)
    return lz


def gota(ramp, talla, k):
    """La gota de la v1 con el radio de esta talla: solo trama, sin contorno."""
    R0 = TALLAS[talla]['gota']
    lz = lienzo(CELDA)
    dx, dy = _malla(lz, C, C)
    r = np.hypot(dx, dy)
    R = R0 * (1.0 - 0.12 * k)
    amp = (1.0, 0.80, 0.62, 0.45)[k]
    dens = np.clip(1.25 - r / R, 0, 1) ** 1.2 * amp
    tramar(lz, dens, ramp, banda_int=1 + k // 2, banda_ext=3 + (k + 1) // 2, corte=0.62)
    return lz


def hoja(ramp, talla):
    filas = [np.hstack([a_rgba(cuerpo(ramp, talla, 2 * np.pi * f / FRAMES))
                        for f in range(FRAMES)])]
    g = [a_rgba(gota(ramp, talla, k)) for k in range(GOTAS)]
    g += [np.zeros((CELDA, CELDA, 4), np.uint8)] * (FRAMES - GOTAS)
    filas.append(np.hstack(g))
    return np.vstack(filas)


def exporta_tallas(destino):
    """world_proton_tallas.json: lo que Unity necesita saber de las tallas y
    no puede medir en el PNG. ProtonArt lo lee al asignar el arte (T6-04)."""
    with open(os.path.join(destino, 'world_proton_tallas.json'), 'w') as f:
        json.dump({'sizes': TALLAS_N, 'chargePerSize': CARGA_PASO,
                   'bagRadiusPixels': [radio_bolsa_px(k) for k in TALLAS]}, f, indent=1)


# --- Comprobaciones ----------------------------------------------------------------

def caja_numero(s, ddx=0, ddy=0):
    t = texto(s)
    h, w = t.shape[:2]
    x0 = int(round(C - w / 2)) + ddx
    y0 = int(round(C - h / 2)) + ddy
    m = np.zeros((CELDA, CELDA), bool)
    m[y0:y0 + h, x0:x0 + w] = True
    return m


def comprobar(img, ramp, talla, fac):
    """El generador falla si algo no se cumple. Ya no se exige el ojo libre:
    las cifras van encima de todo y el cuerpo pasa por detras de ellas."""
    alfa = img[..., 3]
    assert set(np.unique(alfa)) <= {0, 255}, 'alfa no binario'
    validos = {tuple(c) for c in ramp} | {tuple(CONTORNO)}
    usados = {tuple(c) for c in img[alfa > 0][:, :3]}
    assert usados <= validos, f'{fac} {talla}: color fuera de rampa'
    cuerpos = [img[:CELDA, f * CELDA:(f + 1) * CELDA] for f in range(FRAMES)]
    for f, cel in enumerate(cuerpos):
        a = cel[..., 3] > 0
        assert not (a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any()), \
            f'{fac} {talla} frame {f}: toca el borde'
    distintos = len({c.tobytes() for c in cuerpos})
    assert distintos == FRAMES, f'{fac} {talla}: {distintos} frames distintos'
    f16 = a_rgba(cuerpo(ramp, talla, 2 * np.pi))
    assert np.array_equal(f16, cuerpos[0]), f'{fac} {talla}: el bucle no cierra'
    gotas = [img[CELDA:, k * CELDA:(k + 1) * CELDA] for k in range(GOTAS)]
    px = [int((g[..., 3] > 0).sum()) for g in gotas]
    assert all(a > b for a, b in zip(px, px[1:])) and px[-1] > 0, \
        f'{fac} {talla}: la gota no se apaga en orden {px}'
    caja = np.any([c[..., 3] > 0 for c in cuerpos], axis=0)
    ys, xs = np.where(caja)
    return (xs.max() - xs.min() + 1, ys.max() - ys.min() + 1)


# --- Vistas --------------------------------------------------------------------------

BG = np.array(hex_a_rgb(FONDO), np.uint8)
ampliar, sobre, abrir = v1.ampliar, v1.sobre, v1.abrir
MUESTRA = {'s': '7', 'm': '42', 'l': '100'}   # las tres tallas de v1 y v3


def muestra(t):
    return MUESTRA[t] if t in MUESTRA else str(t * CARGA_PASO)


def con_numero(cel, s):
    out = cel.copy()
    t = texto(s)
    h, w = t.shape[:2]
    x0, y0 = int(round(C - w / 2)), int(round(C - h / 2))
    m = t[..., 3] > 0
    out[y0:y0 + h, x0:x0 + w][m] = t[m]
    return out


def pegar(fila, cel, i):
    m = cel[..., 3] > 0
    fila[:, i * CELDA:(i + 1) * CELDA][m] = cel[..., :3][m]


def _tallas(hojas):
    return list(dict.fromkeys(t for _, t in hojas))


def hoja_contacto(hojas):
    filas = []
    for fac in FACCIONES:
        for t in _tallas(hojas):
            h = hojas[fac, t]
            celdas = [con_numero(h[:CELDA, f * CELDA:(f + 1) * CELDA], muestra(t))
                      for f in (0, 4, 8, 12)]
            celdas += [h[CELDA:, k * CELDA:(k + 1) * CELDA] for k in range(GOTAS)]
            fila = np.empty((CELDA, CELDA * len(celdas), 3), np.uint8)
            fila[:] = BG
            for i, c in enumerate(celdas):
                pegar(fila, c, i)
            filas.append(fila)
    return ampliar(np.vstack(filas), 4)


def cifras_vista():
    fila = np.empty((24, 150, 3), np.uint8)
    fila[:] = BG
    t = texto('0123456789')
    sobre(fila, t, 40, 12)
    t2 = texto('100')
    sobre(fila, t2, 110, 12)
    return ampliar(fila, 6)


def escala(hojas):
    """x3: las tallas en fila entre nodos reales, cada una con su Carga."""
    tallas = _tallas(hojas)
    piezas = [abrir('nodes', 'asteroide', 'v1', 'ast_lvl0_neutral.png'),
              abrir('nodes', 'estrella', 'v1', 'str_lvl1_player.png'),
              *[con_numero(hojas['player', t][:CELDA, :CELDA], muestra(t)) for t in tallas],
              abrir('nodes', 'pulsar', 'v2', 'pul_lvl2_player.png'),
              abrir('nodes', 'magnetar', 'v1', 'mag_lvl5_enemy.png')]
    paso = 52 if len(tallas) > 3 else 70
    tab = np.empty((112, 40 + paso * len(piezas), 3), np.uint8)
    tab[:] = BG
    x = 40
    for p in piezas:
        sobre(tab, p, x, 56)
        x += paso
    return ampliar(tab, 3)


def tablero(hojas, k, fondo):
    tab = fondo.copy()
    for fac, carga, o, d, k0 in v1.ENVIOS:
        t = talla_de(carga)
        h = hojas[fac, t]
        largo = math.hypot(d[0] - o[0], d[1] - o[1])
        ux, uy = (d[0] - o[0]) / largo, (d[1] - o[1]) / largo
        s = ((k - k0) * v1.VEL) % (largo + 120)
        if s > largo:
            continue
        # la bolsa es hueca: una gota solo se ve cuando ya ha salido de ella
        r = v1.PASO_GOTA * math.floor((s - radio_bolsa_px(t)) / v1.PASO_GOTA)
        while r > 0:
            edad = int((s - r) / v1.VEL) // PASO_F
            if edad >= GOTAS:
                break
            if edad >= 0:
                sobre(tab, h[CELDA:, edad * CELDA:(edad + 1) * CELDA],
                      round(o[0] + ux * r), round(o[1] + uy * r))
            r -= v1.PASO_GOTA
        f = k % FRAMES
        cel = con_numero(h[:CELDA, f * CELDA:(f + 1) * CELDA], str(carga))
        sobre(tab, cel, round(o[0] + ux * s), round(o[1] + uy * s))
    return tab


def desgaste(hojas):
    """Un proton de 50 que un Magnetar desgasta de 2 en 2 hasta 2: la talla
    baja un escalon cada 5 de Carga. Un frame por disparo, x4."""
    frames = []
    for carga in range(50, 0, -2):
        t = talla_de(carga)
        f = carga % FRAMES
        cel = con_numero(hojas['enemy', t][:CELDA, f * CELDA:(f + 1) * CELDA], str(carga))
        fila = np.empty((CELDA, CELDA, 3), np.uint8)
        fila[:] = BG
        pegar(fila, cel, 0)
        frames.append(Image.fromarray(ampliar(fila, 4)))
    return frames


def vistas(hojas, gif):
    d = os.path.join(dir_preview(ASSET), f'v{VERSION}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(hoja_contacto(hojas)).save(os.path.join(d, 'proton_hoja_x4.png'))
    Image.fromarray(escala(hojas)).save(os.path.join(d, 'proton_escala_x3.png'))
    Image.fromarray(cifras_vista()).save(os.path.join(d, 'cifras_x6.png'))
    fondo = abrir('bg', 'fondos', 'v2', 'bg_section1.png')[..., :3].copy()
    tab = tablero(hojas, 22, fondo)
    Image.fromarray(tab).save(os.path.join(d, 'proton_tablero.png'))
    Image.fromarray(ampliar(tab, 2)).save(os.path.join(d, 'proton_tablero_x2.png'))
    if gif:
        esc = [Image.fromarray(tablero(hojas, k, fondo)) for k in range(96)]
        esc[0].save(os.path.join(d, 'proton_tablero.gif'), save_all=True,
                    append_images=esc[1:], duration=round(1000 / FPS), loop=0)
        zoom = [Image.fromarray(ampliar(np.asarray(e)[120:420, 240:720], 2)) for e in esc]
        zoom[0].save(os.path.join(d, 'proton_tablero_x2.gif'), save_all=True,
                     append_images=zoom[1:], duration=round(1000 / FPS), loop=0)
        des = desgaste(hojas)
        des[0].save(os.path.join(d, 'proton_desgaste_x4.gif'), save_all=True,
                    append_images=des[1:], duration=250, loop=0)
        bucle = []
        tallas = _tallas(hojas)
        for f in range(FRAMES):
            fila = np.empty((CELDA, CELDA * len(tallas), 3), np.uint8)
            fila[:] = BG
            for i, t in enumerate(tallas):
                pegar(fila, con_numero(hojas['player', t][:CELDA, f * CELDA:(f + 1) * CELDA],
                                       muestra(t)), i)
            bucle.append(Image.fromarray(ampliar(fila, 4)))
        bucle[0].save(os.path.join(d, 'proton_bucle_x4.gif'), save_all=True,
                      append_images=bucle[1:], duration=round(1000 / FPS), loop=0)
    print('vistas en', d)


def main():
    destino = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    os.makedirs(destino, exist_ok=True)
    # las tres tallas s/m/l de la primera entrega ya no existen
    for viejo in os.listdir(destino):
        if viejo.startswith('world_proton_') and viejo[:-4].rsplit('_', 1)[-1] in ('s', 'm', 'l'):
            os.remove(os.path.join(destino, viejo))
    hoja_cifras(destino)
    exporta_tallas(destino)
    hojas = {}
    anchos = []
    for fac, ramp_n in FACCIONES.items():
        ramp = rampa(ramp_n)
        for t in TALLAS:
            img = hoja(ramp, t)
            caja = comprobar(img, ramp, t, fac)
            Image.fromarray(img, 'RGBA').save(
                os.path.join(destino, f'world_proton_{fac}_{t}.png'))
            hojas[fac, t] = img
            if fac == 'player':
                anchos.append(caja[0])
            lo, hi = (t - 1) * CARGA_PASO + 1, t * CARGA_PASO
            print(f'{fac:>6} {t:>2}  cuerpo {caja[0]}x{caja[1]} px  '
                  f'carga {lo}-{hi if t < TALLAS_N else 100}  bolsa {radio_bolsa_px(t)} px')
    assert all(a <= b for a, b in zip(anchos, anchos[1:])), f'las tallas no crecen: {anchos}'
    vistas(hojas, '--gif' in sys.argv)


if __name__ == '__main__':
    main()
