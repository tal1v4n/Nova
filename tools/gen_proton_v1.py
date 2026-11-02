"""NOVA - ART-WLD-PRO-P y ART-WLD-PRO-E v1, el proton: «El vortice».

Ref: NOVA_Assets_Diseno.md A.4.2 · NOVA_Estados_Animaciones.md 14.3 · Spec 6.1

Un anillo de plasma inclinado -un toro visto de tres cuartos- con la Carga
en el ojo. Seis hojas, una por bando y talla,
`art/world/proton/v1/world_proton_<faccion>_<talla>.png`, en celdas de 48:

    fila 0  cuerpo   16 frames, bucle   el anillo con sus nudos girando
    fila 1  gotas     4 frames          la estela, de recien nacida a nada

LA TESIS: EL HUECO NO SE LE HACE AL NUMERO, EL NUMERO HACE EL HUECO. 14.3
pide un hueco central legible y el numero es TextMeshPro a 1x, Jersey 10:
10 px de alto y 7, 15 o 17 de ancho segun tenga una, dos o tres cifras. Asi
que cada talla es el anillo mas pequeno que rodea su numero mas ancho, y la
talla queda atada a las cifras: `s` para 1-9, `m` para 10-49, `l` para
50-100. El ojo se estira en horizontal porque los numeros son mas anchos que
altos, y un ojo eliptico es exactamente lo que se ve de un anillo inclinado:
la forma que pide el texto es la que da la profundidad.

  - UN TORO, NO UNA ESFERA. Un disco con un numero encima es una ficha de
    interfaz; ningun nodo tiene agujero, asi que el proton no se confunde
    con ninguno ni a 1x. Es la forma de un anillo de vortice: plasma que
    viaja enrollado sobre si mismo.
  - LOS NUDOS SON LA CARGA. Uno, dos o tres puntos de calor recorren el
    anillo con una cola que se enfria detras, y el resplandor exterior se
    enciende tras cada nudo con cizalla (regla 6). Al acabar el bucle cada
    nudo ocupa el sitio del siguiente: cierra por construccion para
    cualquier numero de nudos. La mitad de delante brilla mas que la de
    atras, que es la otra senal de profundidad.
  - EL CUERPO NO GIRA NUNCA. Girar el sprite giraria con el su luz y
    romperia la L global; por eso la estela no va pegada al cuerpo. Son
    GOTAS de plasma que `ProtonVisual` suelta en pixel entero a lo largo del
    camino recorrido y que se apagan en cuatro pasos de trama: la estela es
    literalmente el pasado del proton, apunta siempre hacia atras sin rotar
    nada y no puede contradecir la luz (decidido con el usuario el
    2026-11-01). Las gotas son la cuenta que la linea de envio enseña
    corriendo hacia la punta (A.4.6): la linea promete paquetes y el proton
    los va dejando caer.
  - PLASMA, LA RAMPA DE LA LINEA. `plasma_p`, la de la linea de envio, y su
    espejo `plasma_e`. El proton es la Energia que esa linea anunciaba.

    py tools/gen_proton_v1.py          las seis hojas + vistas fijas
    py tools/gen_proton_v1.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image, ImageFont

from nova_core import (CONTORNO, RAIZ, a_rgba, dir_preview, lienzo, rampa,
                       tramar, tubo_curva, _malla)
from nova_paleta import FONDO, hex_a_rgb

ASSET   = 'proton'
VERSION = 1

CELDA   = 48        # contrato, Resolucion: world_proton_* a 48 x 48
FRAMES  = 16        # bucle sin duracion fija (contrato, Animacion)
GOTAS   = 4         # pasos de la gota
PASO_F  = 2         # frames de juego por paso: la estela vive 0.67 s
FPS     = 12
C       = CELDA / 2  # el centro de la celda, donde TextMeshPro centra la Carga

FACCIONES = {'player': 'plasma_p', 'enemy': 'plasma_e'}

# Cada talla, el anillo mas pequeno que rodea su numero mas ancho.
#   ai, bi   semiejes del ojo: el borde interior del tubo
#   g        semigrosor del tubo
#   nudos    puntos de calor; mas Carga, mas nudos
#   sesgo    bandas: la talla pequena arde una banda mas apagada
#   halo     alcance del resplandor exterior, en px
#   gota     radio de la gota recien soltada
#   cifras   las cadenas mas anchas que tiene que alojar
TALLAS = {
    's': dict(ai=6.6, bi=7.2, g=2.0, nudos=1, sesgo=1,  halo=3.0, gota=2.8,
              cifras=('8', '4', '9')),
    'm': dict(ai=10.6, bi=7.4, g=2.6, nudos=2, sesgo=0,  halo=4.0, gota=3.2,
              cifras=('48', '38', '49', '10')),
    'l': dict(ai=12.4, bi=7.8, g=3.0, nudos=3, sesgo=0,  halo=4.0, gota=3.8,
              cifras=('100', '88', '98', '50')),
}
UMBRAL_CARGA = {'s': (1, 9), 'm': (10, 49), 'l': (50, 100)}

OID = 10


# --- El cuerpo ---------------------------------------------------------------

def _ang_param(dx, dy, A, B):
    """Angulo parametrico de la elipse: el que recorre un nudo."""
    return np.arctan2(dy / B, dx / A)


def _envolver(a):
    return (a + np.pi) % (2 * np.pi) - np.pi


def nudos(t, n):
    """Angulos de los n nudos en el instante t (0..2pi). Cada uno avanza
    2pi/n por bucle: al cerrar, ocupa el sitio del siguiente."""
    return [-2.2 + 2 * np.pi * (j + t / (2 * np.pi)) / n for j in range(n)]


def calor_nudos(psi, t, n):
    """Cuanto aclara cada pixel del anillo: un pico seco delante del nudo y
    una cola que se enfria detras. Va en [0,1]."""
    f = np.zeros_like(psi)
    for th in nudos(t, n):
        d = _envolver(psi - th)
        f = np.maximum(f, np.where(d > 0, np.exp(-(d / 0.22) ** 2),
                                   np.exp(d / 0.85)))
    return f


TRAMOS = 24        # el toro en tramos de 15 grados


def _arco(ai, bi, g, a0, a1):
    """Cubica que aproxima el arco a0..a1 de la elipse central de un tramo:
    el borde interior fijo en (ai, bi) y el centro del tubo g mas afuera."""
    A, B = ai + g, bi + g
    k = 4.0 / 3.0 * math.tan((a1 - a0) / 4.0)

    def e(a):
        return C + A * math.cos(a), C + B * math.sin(a)

    def de(a):
        return -A * math.sin(a), B * math.cos(a)

    p0, p3 = e(a0), e(a1)
    d0, d1 = de(a0), de(a1)
    return [p0, (p0[0] + k * d0[0], p0[1] + k * d0[1]),
            (p3[0] - k * d1[0], p3[1] - k * d1[1]), p3]


def toro(lz, p, ramp):
    """El anillo visto de tres cuartos. El borde interior es el ojo y no se
    mueve; el tubo engorda hacia delante -abajo, sin(a) > 0- y adelgaza hacia
    atras hasta un 60 %. Es la escala de la seccion Profundidad del contrato
    hecha continua: lo de delante, mas grande. La mitad de atras va una banda
    mas oscura y se dibuja antes, que es la otra senal. Mismo `oid` en todos
    los tramos, asi que no hay costura entre ellos."""
    g = p['g']
    tramos = [(2 * math.pi * i / TRAMOS, 2 * math.pi * (i + 1) / TRAMOS)
              for i in range(TRAMOS)]
    atras = [tr for tr in tramos if math.sin(sum(tr) / 2) < 0]
    delante = [tr for tr in tramos if math.sin(sum(tr) / 2) >= 0]
    for grupo, sesgo in ((atras, p['sesgo'] + 1), (delante, p['sesgo'])):
        for a0, a1 in grupo:
            gi = g * (0.8 + 0.2 * math.sin((a0 + a1) / 2))
            tubo_curva(lz, _arco(p['ai'], p['bi'], gi, a0, a1), gi, ramp, OID,
                       muestras=12, sesgo_banda=sesgo, contorno_interno=False)


def cuerpo(ramp, talla, t):
    """Un frame del anillo, en un lienzo de 48."""
    p = TALLAS[talla]
    A, B, g = p['ai'] + p['g'], p['bi'] + p['g'], p['g']
    lz = lienzo(CELDA)

    toro(lz, p, ramp)

    dx, dy = _malla(lz, C, C)
    psi = _ang_param(dx, dy, A, B)
    tubo = lz['lleno'] & (lz['id'] == OID)

    # los nudos: aclaran la banda que ya hay (modulan, no sustituyen)
    banda = np.full(tubo.shape, 6, np.int16)
    for k in range(7):
        banda[tubo & np.all(lz['col'] == ramp[k], axis=-1)] = k
    delante = np.sin(psi) > 0
    tope = np.where(delante, 3, 2)
    sube = np.rint(calor_nudos(psi, t, p['nudos']) * tope).astype(np.int16)
    nueva = np.clip(banda - sube, np.where(delante, 0, 1), 6)
    lz['col'][tubo] = ramp[nueva[tubo]]

    # el resplandor: fuera del tubo, nunca en el ojo, encendido tras cada nudo
    rho_ext = np.hypot(dx / (A + g), dy / (B + g))
    rho_int = np.hypot(dx / p['ai'], dy / p['bi'])
    media = 0.5 * (A + B) + g
    fuera = (rho_ext - 1.0) * media              # px desde el borde exterior
    avance = np.clip(fuera / p['halo'], 0.0, 1.6)
    estela = calor_nudos(psi - 0.55 * avance, t, p['nudos'])   # cizalla
    dens = np.clip(1.0 - fuera / p['halo'], 0, 1) ** 1.5 * (0.30 + 0.70 * estela)
    dens[(rho_ext < 1.0) | (rho_int < 1.0)] = 0
    tramar(lz, dens, ramp, banda_int=2, banda_ext=4, corte=0.5)
    lz['glow'] &= ~lz['lleno']
    return lz


# --- La gota -----------------------------------------------------------------

def gota(ramp, talla, k):
    """La gota de estela en su paso k (0 recien soltada, 3 la ultima). Solo
    trama: es luz que se queda atras, no materia, y no lleva contorno."""
    p = TALLAS[talla]
    lz = lienzo(CELDA)
    dx, dy = _malla(lz, C, C)
    r = np.hypot(dx, dy)
    R = p['gota'] * (1.0 - 0.12 * k)
    amp = (1.0, 0.80, 0.62, 0.45)[k]
    dens = np.clip(1.25 - r / R, 0, 1) ** 1.2 * amp
    tramar(lz, dens, ramp, banda_int=1 + k // 2, banda_ext=3 + (k + 1) // 2,
           corte=0.62)
    return lz


# --- Hoja y comprobaciones ---------------------------------------------------

def hoja(ramp, talla):
    filas = [np.hstack([a_rgba(cuerpo(ramp, talla, 2 * np.pi * f / FRAMES))
                        for f in range(FRAMES)])]
    g = [a_rgba(gota(ramp, talla, k)) for k in range(GOTAS)]
    g += [np.zeros((CELDA, CELDA, 4), np.uint8)] * (FRAMES - GOTAS)
    filas.append(np.hstack(g))
    return np.vstack(filas)


def _glifos(texto):
    """La Carga como la pinta TextMeshPro a 1x: Jersey 10 a 18.67 px por em.
    Se rasteriza a 56 px (3 por pixel de diseno) y se reduce por bloques."""
    ft = ImageFont.truetype(os.path.join(RAIZ, 'Assets', 'Art', 'Fonts',
                                         'Jersey10-Regular.ttf'), 56)
    mk = ft.getmask(texto, mode='L')
    m = np.frombuffer(bytes(mk), np.uint8).reshape(mk.size[1], mk.size[0]) > 127
    ys, xs = np.where(m)
    m = m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h, w = m.shape[0] // 3, m.shape[1] // 3
    return m[:h * 3, :w * 3].reshape(h, 3, w, 3).mean(axis=(1, 3)) > 0.5


def numero_en(alfa, texto, dx=0, dy=0):
    """Mascara del numero centrado en la celda, con un desplazamiento."""
    gl = _glifos(texto)
    h, w = gl.shape
    x0 = int(round(C - w / 2)) + dx
    y0 = int(round(C - h / 2)) + dy
    out = np.zeros(alfa.shape, bool)
    out[y0:y0 + h, x0:x0 + w] = gl
    return out


def comprobar(img, ramp, talla, fac):
    """El generador falla si algo no se cumple."""
    alfa = img[..., 3]
    assert set(np.unique(alfa)) <= {0, 255}, 'alfa no binario'
    validos = {tuple(c) for c in ramp} | {tuple(CONTORNO)}
    usados = {tuple(c) for c in img[alfa > 0][:, :3]}
    assert usados <= validos, f'{fac} {talla}: color fuera de rampa'

    cuerpos = [img[:CELDA, f * CELDA:(f + 1) * CELDA] for f in range(FRAMES)]
    gotas = [img[CELDA:, k * CELDA:(k + 1) * CELDA] for k in range(GOTAS)]

    for f, cel in enumerate(cuerpos):
        a = cel[..., 3] > 0
        assert not (a[0].any() or a[-1].any() or a[:, 0].any() or a[:, -1].any()), \
            f'{fac} {talla} frame {f}: toca el borde de la celda'
        # el ojo: ni un pixel opaco bajo el numero, en las dos paridades
        for txt in TALLAS[talla]['cifras']:
            for ddx in (0, 1):
                for ddy in (0, 1):
                    n = numero_en(a, txt, ddx, ddy)
                    tapa = n & a & ~np.all(cel[..., :3] == CONTORNO, axis=-1)
                    assert not tapa.any(), \
                        f'{fac} {talla}: el anillo tapa «{txt}» ({tapa.sum()} px)'

    distintos = len({c.tobytes() for c in cuerpos})
    assert distintos == FRAMES, f'{fac} {talla}: {distintos} frames distintos de {FRAMES}'
    # el bucle cierra: el frame 16 es el 0
    f16 = a_rgba(cuerpo(ramp, talla, 2 * np.pi))
    assert np.array_equal(f16, cuerpos[0]), f'{fac} {talla}: el bucle no cierra'

    px = [int((g[..., 3] > 0).sum()) for g in gotas]
    assert all(a > b for a, b in zip(px, px[1:])) and px[-1] > 0, \
        f'{fac} {talla}: la gota no se apaga en orden {px}'
    for g in gotas:
        assert not (np.all(g[..., :3] == CONTORNO, axis=-1) & (g[..., 3] > 0)).any(), \
            'la gota lleva contorno'

    caja = np.any([c[..., 3] > 0 for c in cuerpos], axis=0)
    ys, xs = np.where(caja)
    return (xs.max() - xs.min() + 1, ys.max() - ys.min() + 1), px


# --- Vistas ------------------------------------------------------------------

BG = np.array(hex_a_rgb(FONDO), np.uint8)


def ampliar(im, k):
    return np.repeat(np.repeat(im, k, axis=0), k, axis=1)


def sobre(lienzo_, rgba, cx, cy):
    h, w = rgba.shape[:2]
    x0, y0 = int(cx) - w // 2, int(cy) - h // 2
    x1, y1 = max(x0, 0), max(y0, 0)
    x2, y2 = min(x0 + w, lienzo_.shape[1]), min(y0 + h, lienzo_.shape[0])
    if x2 <= x1 or y2 <= y1:
        return
    sub = rgba[y1 - y0:y2 - y0, x1 - x0:x2 - x0]
    m = sub[..., 3] > 0
    lienzo_[y1:y2, x1:x2][m] = sub[..., :3][m]


def con_numero(cel, texto, ramp):
    """La celda con su Carga encima, como en partida: el tono 1 de la rampa."""
    out = cel.copy()
    n = numero_en(out[..., 3] > 0, texto)
    out[n, :3] = ramp[1]
    out[n, 3] = 255
    return out


def abrir(*ruta):
    return np.asarray(Image.open(os.path.join(RAIZ, 'art', *ruta)).convert('RGBA'))


def talla_de(carga):
    return next(t for t, (a, b) in UMBRAL_CARGA.items() if a <= carga <= b)


def hoja_contacto(hojas):
    """x4: por bando, las tres tallas con su numero, cuatro frames del bucle
    y las cuatro gotas; debajo, al lado de nodos reales para la escala."""
    filas = []
    muestra = {'s': '7', 'm': '42', 'l': '100'}
    for fac, ramp_n in FACCIONES.items():
        ramp = rampa(ramp_n)
        for t in TALLAS:
            h = hojas[fac, t]
            celdas = [con_numero(h[:CELDA, f * CELDA:(f + 1) * CELDA], muestra[t], ramp)
                      for f in (0, 4, 8, 12)]
            celdas += [h[CELDA:, k * CELDA:(k + 1) * CELDA] for k in range(GOTAS)]
            fila = np.empty((CELDA, CELDA * len(celdas), 3), np.uint8)
            fila[:] = BG
            for i, c in enumerate(celdas):
                m = c[..., 3] > 0
                fila[:, i * CELDA:(i + 1) * CELDA][m] = c[..., :3][m]
            filas.append(fila)
    return ampliar(np.vstack(filas), 4)


def escala():
    """x3: los protones del Jugador entre nodos reales, para ver la talla."""
    tab = np.empty((112, 520, 3), np.uint8)
    tab[:] = BG
    piezas = [abrir('nodes', 'asteroide', 'v1', 'ast_lvl0_neutral.png'),
              abrir('nodes', 'estrella', 'v1', 'str_lvl1_player.png'),
              None, None, None,
              abrir('nodes', 'pulsar', 'v2', 'pul_lvl2_player.png'),
              abrir('nodes', 'magnetar', 'v1', 'mag_lvl5_enemy.png')]
    ramp = rampa('plasma_p')
    hp = {t: hoja(ramp, t)[:CELDA, :CELDA] for t in TALLAS}
    prot = [con_numero(hp['s'], '7', ramp), con_numero(hp['m'], '42', ramp),
            con_numero(hp['l'], '100', ramp)]
    x = 50
    for i, p in enumerate(piezas):
        if p is None:
            p = prot[i - 2]
        sobre(tab, p, x, 56)
        x += 70
    return ampliar(tab, 3)


# Un tablero: tres envios cruzandose y un Magnetar enemigo. Los protones
# vuelan a 2.5 u/s (13.3 px por frame a 12 fps) y sueltan una gota cada
# PASO_GOTA px; la gota baja un paso cada PASO_F frames y tras el cuarto
# desaparece.
PASO_GOTA = 7.0
VEL = 2.5 * 64 / FPS
ENVIOS = (
    # bando, carga, origen, destino, frame de salida
    ('player', 7,   (150, 400), (820, 120), 0),
    ('player', 42,  (170, 120), (780, 440), 6),
    ('enemy',  100, (840, 300), (120, 260), 3),
    ('enemy',  23,  (520, 470), (470, 60),  10),
)


def tablero(hojas, k, fondo):
    tab = fondo.copy()
    for fac, carga, o, d, k0 in ENVIOS:
        ramp = rampa(FACCIONES[fac])
        t = talla_de(carga)
        h = hojas[fac, t]
        largo = math.hypot(d[0] - o[0], d[1] - o[1])
        ux, uy = (d[0] - o[0]) / largo, (d[1] - o[1]) / largo
        s = ((k - k0) * VEL) % (largo + 120)
        if s > largo:
            continue
        # gotas: la de recorrido r se solto hace (s - r) / VEL frames
        r = PASO_GOTA * math.floor(s / PASO_GOTA)
        while r > 0:
            edad = int((s - r) / VEL) // PASO_F
            if edad >= GOTAS:
                break
            if edad >= 0:
                g = h[CELDA:, edad * CELDA:(edad + 1) * CELDA]
                sobre(tab, g, round(o[0] + ux * r), round(o[1] + uy * r))
            r -= PASO_GOTA
        f = k % FRAMES
        cel = con_numero(h[:CELDA, f * CELDA:(f + 1) * CELDA], str(carga), ramp)
        sobre(tab, cel, round(o[0] + ux * s), round(o[1] + uy * s))
    return tab


def vistas(hojas, gif):
    d = os.path.join(dir_preview(ASSET), f'v{VERSION}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(hoja_contacto(hojas)).save(os.path.join(d, 'proton_hoja_x4.png'))
    Image.fromarray(escala()).save(os.path.join(d, 'proton_escala_x3.png'))
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
        bucle = []
        for f in range(FRAMES):
            fila = np.empty((CELDA, CELDA * 6, 3), np.uint8)
            fila[:] = BG
            for i, (fac, t) in enumerate(hojas):
                ramp = rampa(FACCIONES[fac])
                c = con_numero(hojas[fac, t][:CELDA, f * CELDA:(f + 1) * CELDA],
                               {'s': '7', 'm': '42', 'l': '100'}[t], ramp)
                m = c[..., 3] > 0
                fila[:, i * CELDA:(i + 1) * CELDA][m] = c[..., :3][m]
            bucle.append(Image.fromarray(ampliar(fila, 4)))
        bucle[0].save(os.path.join(d, 'proton_bucle_x4.gif'), save_all=True,
                      append_images=bucle[1:], duration=round(1000 / FPS), loop=0)
    print('vistas en', d)


def main():
    destino = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    os.makedirs(destino, exist_ok=True)
    hojas = {}
    for fac, ramp_n in FACCIONES.items():
        ramp = rampa(ramp_n)
        for t in TALLAS:
            img = hoja(ramp, t)
            caja, px = comprobar(img, ramp, t, fac)
            Image.fromarray(img, 'RGBA').save(
                os.path.join(destino, f'world_proton_{fac}_{t}.png'))
            hojas[fac, t] = img
            print(f'{fac:>6} {t}  {img.shape[1]}x{img.shape[0]}  cuerpo {caja[0]}x{caja[1]} px'
                  f'  carga {UMBRAL_CARGA[t][0]}-{UMBRAL_CARGA[t][1]}  gota {px} px')
    vistas(hojas, '--gif' in sys.argv)


if __name__ == '__main__':
    main()
