"""NOVA - ART-FX-SHOT v1, el destello sobre el proton alcanzado: «El campanazo».

Ref: NOVA_Assets_Diseno.md A.5 y A.4.1 · NOVA_Estados_Animaciones.md 14.3, 14.5 y 14.7

Lo que le pasa al proton cuando le llega el arco del Magnetar
(`ART-WLD-RAD-SHOT`, «El chispazo»): 2 frames, 0.17 s, el mismo instante que
el arco. Una sola hoja, `art/fx/shot/v<n>/fx_shot_sheet.png`, en celdas de 64:

    filas     20   player_1 ... player_10, enemy_1 ... enemy_10
    columnas   8   variante 0 frame 0, variante 0 frame 1, variante 1 frame 0 ...

LO QUE COMPARTEN LAS TRES PROPUESTAS (y por eso vive aqui y v2 y v3 lo
importan: una sola vara para medirlas):

  - VA ENCIMA DEL NUMERO Y NO LO PISA. Es la capa 7 de 14.7, por encima de
    la Carga, y la Carga es justo lo que el disparo cambia: baja de 2 en 2.
    El generador falla si un pixel cae en la caja de cualquier Carga de su
    talla, con su contorno y un pixel de holgura por si proton y destello
    caen a un pixel de distancia.
  - UNA FILA POR TALLA. El proton mide de 23 a 43 px segun su Carga; un
    destello que no conoce la bolsa o se pierde dentro de la grande o se
    sale de la pequena. Las medidas salen de `gen_proton_v2.TALLAS`.
  - EN EL COLOR DEL PROTON, NO DEL MAGNETAR. El arco ya trae el color de
    quien dispara; el destello es el plasma del que recibe. `plasma_p` y
    `plasma_e`, indices 1-5: el [1] es casi blanco, tan claro como el
    contacto del arco (`mag_*[1]` del propio, [2] de los ajenos), asi que la
    luz no salta al pasar del arco al proton. El blanco puro es de `FX-CAP`.
  - CUATRO VARIANTES. Un Magnetar N5 dispara 4.27 veces por segundo; el
    mismo destello en el mismo sitio se leeria como un parpadeo del sprite.
    `VFXPool` elige una al azar en cada disparo.
  - SIGUE AL PROTON. En 0.17 s el proton recorre 27 px: el destello va
    colgado de el, como la punta del arco.
  - ALFA BINARIO Y SIN CONTORNO: es luz.

LA TESIS DE LA v1: LA BOLSA SUENA COMO UNA CAMPANA. El proton es una bolsa
(«La bolsa», A.4.2) y lo que se golpea es la membrana. Una membrana golpeada
vibra en su modo cuadrupolar: se aplasta en un eje y se estira en el otro, y
al rebotar al reves. Eso son los dos frames:

  - GOLPE (frame 0). La bolsa entera se enciende y un aro solido de 1 px la
    cine por fuera, APLASTADO en el eje del golpe. El aire de fuera, en trama.
  - REBOTE (frame 1). El aro se ha soltado 3 px hacia fuera, deformado al
    reves -el eje que estaba aplastado ahora es el largo- y roto en tramos,
    una banda mas apagado. Dentro solo queda un rastro ralo.

Las cuatro variantes giran el eje del golpe de 45 en 45 grados.

    py tools/gen_fx_shot_v1.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_shot_v1.py --gif    y las animadas
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import BAYER8, RAIZ, dir_preview, rampa, rotar_tira
from nova_paleta import FONDO, hex_a_rgb
import gen_proton_v2 as pro

ASSET     = 'shot'
VERSION   = 1
NOMBRE    = 'El campanazo'

CELDA     = 64
C         = CELDA / 2
FRAMES    = 2
VARIANTES = 4
FPS       = 12
DANO      = 2            # el numero baja de 2 en 2 (14.3)

FACCIONES = {'player': 'plasma_p', 'enemy': 'plasma_e'}
TALLAS    = list(range(1, pro.TALLAS_N + 1))

BAYER = np.tile(BAYER8, (CELDA // 8, CELDA // 8))
_y, _x = np.mgrid[0:CELDA, 0:CELDA]
DX = _x + 0.5 - C
DY = _y + 0.5 - C
R = np.hypot(DX, DY)
ANG = np.arctan2(DY, DX)


# --- Lo que comparten las propuestas -------------------------------------------

def medidas(t):
    """Radio de la membrana y hasta donde llega su trama, en px."""
    return pro.TALLAS[t]['bolsa'], pro.radio_bolsa_px(t)


def cargas(t):
    lo = (t - 1) * pro.CARGA_PASO + 1
    hi = t * pro.CARGA_PASO if t < pro.TALLAS_N else 100
    return range(lo, hi + 1)


_NUMERO = {}


def numero(t):
    """Lo que el destello no puede pisar: la caja de cada Carga de la talla,
    centrada como la centra `gen_proton_v2.con_numero`, con un pixel de
    holgura alrededor."""
    if t not in _NUMERO:
        m = np.zeros((CELDA, CELDA), bool)
        for s in cargas(t):
            tx = pro.texto(str(s))
            h, w = tx.shape[:2]
            x0, y0 = int(round(C - w / 2)), int(round(C - h / 2))
            m[y0 - 1:y0 + h + 1, x0 - 1:x0 + w + 1] = True
        _NUMERO[t] = m
    return _NUMERO[t]


def tramar(idx, dens, b_in, b_out, corte=0.55, encima=False):
    """Densidad -> pixeles con Bayer 8x8, en una matriz de indices de rampa
    (0 = vacio). Por defecto no pisa lo ya pintado."""
    on = dens > BAYER
    if not encima:
        on &= idx == 0
    idx[on] = np.where(dens[on] > corte, b_in, b_out)
    return on


def a_rgba(idx, ramp):
    img = np.zeros((CELDA, CELDA, 4), np.uint8)
    on = idx > 0
    img[on, :3] = ramp[idx[on]]
    img[on, 3] = 255
    return img


def luz(idx):
    """Cuanta luz da un frame: cada pixel pesa mas cuanto mas claro."""
    on = idx > 0
    return int((6 - idx[on]).sum())


def comprobar(celdas, t, fac, version):
    """Lo que tiene que cumplir cualquier propuesta. Falla en vez de avisar."""
    fallos = []
    prot = numero(t)
    _, Re = medidas(t)
    for v in range(VARIANTES):
        idx = celdas[v]
        for f in range(FRAMES):
            a = idx[f]
            on = a > 0
            if not set(np.unique(a)) <= {0, 1, 2, 3, 4, 5}:
                fallos.append(f'v{v} f{f}: indice fuera de la rampa [1..5]')
            if (on & prot).any():
                fallos.append(f'v{v} f{f}: pisa el numero ({int((on & prot).sum())} px)')
            if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
                fallos.append(f'v{v} f{f}: toca el borde de la celda')
            if not (on & (R > Re)).any():
                fallos.append(f'v{v} f{f}: no sale de la bolsa')
        if not (idx[0] == 1).any():
            fallos.append(f'v{v}: el golpe no llega al indice 1, el del contacto del arco')
        if luz(idx[1]) >= luz(idx[0]):
            fallos.append(f'v{v}: el frame 1 no se apaga ({luz(idx[1])} >= {luz(idx[0])})')
        if np.array_equal(idx[0], idx[1]):
            fallos.append(f'v{v}: los dos frames son iguales')
    for f in range(FRAMES):
        distintas = len({celdas[v][f].tobytes() for v in range(VARIANTES)})
        if distintas != VARIANTES:
            fallos.append(f'f{f}: solo {distintas} variantes distintas')
    if fallos:
        raise SystemExit(f'ART-FX-SHOT v{version} ({fac} {t}) no pasa:\n  '
                         + '\n  '.join(fallos))


def generar(dibujar, version):
    """Todas las celdas: {(fac, t): [[idx f0, idx f1] por variante]}."""
    celdas = {}
    for fac in FACCIONES:
        for t in TALLAS:
            c = [[dibujar(t, f, v).astype(np.int8) for f in range(FRAMES)]
                 for v in range(VARIANTES)]
            comprobar(c, t, fac, version)
            celdas[fac, t] = c
    return celdas


def hoja(celdas):
    filas = []
    for fac, ramp_n in FACCIONES.items():
        ramp = rampa(ramp_n)
        for t in TALLAS:
            filas.append(np.hstack([a_rgba(celdas[fac, t][v][f], ramp)
                                    for v in range(VARIANTES) for f in range(FRAMES)]))
    return np.vstack(filas)


def exporta(destino, img):
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'fx_shot_sheet.png'))
    filas = [f'{fac}_{t}' for fac in FACCIONES for t in TALLAS]
    with open(os.path.join(destino, 'fx_shot_sheet.json'), 'w') as f:
        json.dump({'cellSize': CELDA, 'frames': FRAMES, 'variants': VARIANTES,
                   'fps': FPS, 'rows': filas,
                   'note': 'una fila por faccion y talla del proton (world_proton_tallas.json); '
                           'columnas: variante 0 frame 0, variante 0 frame 1, variante 1 frame 0...; '
                           'pivote en el centro, colgado del proton'}, f, indent=1)


# --- Vistas --------------------------------------------------------------------

BG = np.array(hex_a_rgb(FONDO), np.uint8)


def ampliar(im, k):
    return np.repeat(np.repeat(im, k, axis=0), k, axis=1)


def sobre(lienzo, rgba, cx, cy):
    h, w = rgba.shape[:2]
    x0, y0 = int(round(cx)) - w // 2, int(round(cy)) - h // 2
    x1, y1 = max(x0, 0), max(y0, 0)
    x2, y2 = min(x0 + w, lienzo.shape[1]), min(y0 + h, lienzo.shape[0])
    if x2 <= x1 or y2 <= y1:
        return
    sub = rgba[y1 - y0:y2 - y0, x1 - x0:x2 - x0]
    m = sub[..., 3] > 0
    lienzo[y1:y2, x1:x2][m] = sub[..., :3][m]


def abrir(*ruta):
    return np.asarray(Image.open(os.path.join(RAIZ, 'art', *ruta)).convert('RGBA'))


_PROTON = {}


def proton(fac, carga, k):
    """El proton real («La bolsa» v2) en el frame k de su bucle, con su Carga."""
    t = pro.talla_de(carga)
    if (fac, t) not in _PROTON:
        _PROTON[fac, t] = abrir('world', 'proton', 'v2', f'world_proton_{fac}_{t}.png')
    h = _PROTON[fac, t]
    f = k % pro.FRAMES
    return pro.con_numero(h[:pro.CELDA, f * pro.CELDA:(f + 1) * pro.CELDA], str(carga))


def destello(celdas, fac, carga, v, f):
    t = pro.talla_de(carga)
    return a_rgba(celdas[fac, t][v][f], rampa(FACCIONES[fac]))


_ARCOS = {}


def arcos():
    """Los tres arcos de «El chispazo», como los monta gen_shot_v1."""
    if not _ARCOS:
        import gen_shot_v1 as sh
        for fac, cfg in sh.FACCIONES.items():
            fil = sh.Arco(rampa(cfg['ramp']), cfg['sesgo'])
            _, frames = fil.hoja()
            _ARCOS[fac] = (fil, frames)
    return _ARCOS


def arco(tab, fac, x, y, qx, qy, f):
    fil, frames = arcos()[fac]
    largo = math.hypot(qx - x, qy - y)
    rotar_tira(tab, fil.linea(frames, largo, f), x, y, math.atan2(qy - y, qx - x), fil.eje)


def magnetar(n, fac):
    return abrir('nodes', 'magnetar', 'v1', f'mag_lvl{n}_{fac}.png')


def vista_hoja(celdas):
    """x3. Por bando y cuatro tallas: el proton solo, el golpe y el rebote de
    la variante 0, y el golpe de las otras tres variantes."""
    filas = []
    for fac in FACCIONES:
        for carga in (5, 15, 30, 50):
            celd = [None, (0, 0), (0, 1), (1, 0), (2, 0), (3, 0)]
            fila = np.empty((CELDA, CELDA * len(celd), 3), np.uint8)
            fila[:] = BG
            for i, vf in enumerate(celd):
                sobre(fila, proton(fac, carga, 0), i * CELDA + C, C)
                if vf is not None:
                    sobre(fila, destello(celdas, fac, carga, *vf), i * CELDA + C, C)
            filas.append(fila)
    return ampliar(np.vstack(filas), 3)


def vista_golpe(celdas):
    """x3. El disparo entero en cuatro tiempos -antes, golpe, rebote, despues-
    con el arco debajo: un Magnetar enemigo N5 a un proton del Jugador de 30, y
    uno neutral N3 a un proton del Enemigo de 12."""
    casos = (('enemy', 5, 'player', 30), ('neutral', 3, 'enemy', 12))
    W, H = 190, 84
    filas = []
    for mfac, n, pfac, carga in casos:
        celdas_f = []
        for paso in range(4):
            tab = np.empty((H, W, 3), np.uint8)
            tab[:] = BG
            mx, my, qx, qy = 42, 52, 150 + 4 * paso, 34
            c = carga if paso < 1 else carga - DANO
            if paso in (1, 2):
                arco(tab, mfac, mx, my, qx, qy, paso - 1)
            sobre(tab, magnetar(n, mfac), mx, my)
            sobre(tab, proton(pfac, c, paso), qx, qy)
            if paso in (1, 2):
                sobre(tab, destello(celdas, pfac, c, 0, paso - 1), qx, qy)
            celdas_f.append(tab)
            celdas_f.append(np.tile(BG, (H, 4, 1)))
        filas.append(np.hstack(celdas_f))
    return ampliar(np.vstack(filas), 3)


# Un Magnetar enemigo N5 y protones del Jugador que cruzan su radio por una
# cuerda. Cada disparo les quita 2 de Carga; si llegan a 0, desaparecen y entra
# el siguiente. Es lo que mas veces va a ver el jugador.
PASO = 2.5 * 64 / FPS            # 2.5 u/s a PPU 64
MAG = (180, 110)
CUERDAS = (((-10, 40), (370, 150), 'player', 34, 0),
           ((370, 200), (-10, 70), 'player', 9, 14))


def vista_desgaste(celdas, k, rng_v):
    """El frame k de juego (a 12 fps) del cruce, 360 x 220."""
    import gen_radio_v2 as rad
    import gen_shot_v1 as sh
    tab = np.empty((220, 360, 3), np.uint8)
    tab[:] = BG
    sobre(tab, rad.anillo(5, 'enemy'), *MAG)
    R5 = rad.RADIO_U[5] * rad.PPU
    periodo = FPS / sh.CADENCIA[5]
    vivos = []
    for (o, d, fac, carga0, k0) in CUERDAS:
        largo = math.hypot(d[0] - o[0], d[1] - o[1])
        vuelta = int(largo / PASO) + 6
        kk = (k - k0) % vuelta
        x = o[0] + (d[0] - o[0]) * kk * PASO / largo
        y = o[1] + (d[1] - o[1]) * kk * PASO / largo
        # disparos recibidos hasta ahora: frames de juego dentro del radio en
        # los que la torreta estaba lista
        recibidos, golpe = 0, None
        for j in range(kk + 1):
            xj = o[0] + (d[0] - o[0]) * j * PASO / largo
            yj = o[1] + (d[1] - o[1]) * j * PASO / largo
            if math.hypot(xj - MAG[0], yj - MAG[1]) <= R5 and int((k - kk + j) % periodo) == 0:
                recibidos += 1
                golpe = j
        carga = carga0 - DANO * recibidos
        if carga <= 0 or kk * PASO > largo:
            continue
        f = None if golpe is None else kk - golpe
        vivos.append((x, y, fac, carga, f, recibidos))
    for x, y, fac, carga, f, nr in vivos:
        if f is not None and f < 2:
            arco(tab, 'enemy', *MAG, x, y, f)
    sobre(tab, magnetar(5, 'enemy'), *MAG)
    for x, y, fac, carga, f, nr in vivos:
        sobre(tab, proton(fac, carga, k), x, y)
        if f is not None and f < 2:
            v = rng_v[nr % len(rng_v)]
            sobre(tab, destello(celdas, fac, carga, v, f), x, y)
    return tab


def vistas(celdas, version, gif):
    d = os.path.join(dir_preview('fx_shot'), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(vista_hoja(celdas)).save(os.path.join(d, 'fx_shot_hoja_x3.png'))
    Image.fromarray(vista_golpe(celdas)).save(os.path.join(d, 'fx_shot_golpe_x3.png'))
    rng_v = [2, 0, 3, 1, 0, 2, 1, 3]
    # un frame con golpe en pantalla, a 1x y a x2
    for k in range(40):
        tab = vista_desgaste(celdas, k, rng_v)
        if k >= 6 and (k % round(FPS / 4.27)) == 0:
            break
    Image.fromarray(tab).save(os.path.join(d, 'fx_shot_tablero.png'))
    Image.fromarray(ampliar(tab, 2)).save(os.path.join(d, 'fx_shot_tablero_x2.png'))
    if gif:
        esc = [vista_desgaste(celdas, k, rng_v) for k in range(72)]
        im = [Image.fromarray(ampliar(e, 2)) for e in esc]
        im[0].save(os.path.join(d, 'fx_shot_tablero_x2.gif'), save_all=True,
                   append_images=im[1:], duration=round(1000 / FPS), loop=0)
        lento = [Image.fromarray(ampliar(e, 2)) for e in esc]
        lento[0].save(os.path.join(d, 'fx_shot_tablero_x2_lento.gif'), save_all=True,
                      append_images=lento[1:], duration=round(4000 / FPS), loop=0)
    print('vistas en', d)


def ejecutar(dibujar, version, nombre, gif):
    destino = os.path.join(RAIZ, 'art', 'fx', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    celdas = generar(dibujar, version)
    img = hoja(celdas)
    exporta(destino, img)
    print(f'ART-FX-SHOT v{version} «{nombre}»: {img.shape[1]} x {img.shape[0]}')
    for t in TALLAS:
        c = celdas['player', t]
        px = [int((c[0][f] > 0).sum()) for f in range(FRAMES)]
        print(f'  talla {t:>2}  bolsa {medidas(t)[1]:>2} px  golpe {px[0]:>3} px  '
              f'rebote {px[1]:>3} px  luz {luz(c[0][0])} -> {luz(c[0][1])}')
    vistas(celdas, version, gif)


# --- La v1: «El campanazo» -------------------------------------------------------

AMP    = 1.3     # cuanto se aplasta el aro, en px
SUELTA = 3.0     # cuanto sale el aro entre el golpe y el rebote


def _ruido(v):
    """Por donde se rompe el aro del rebote: una onda angular con fase propia
    de cada variante, determinista."""
    rng = np.random.default_rng(40 + v)
    fases = rng.uniform(0, 2 * np.pi, 3)
    return (np.sin(3 * ANG + fases[0]) + 0.7 * np.sin(5 * ANG + fases[1])
            + 0.5 * np.sin(8 * ANG + fases[2])) / 2.2


def dibujar(t, f, v):
    Rb, Re = medidas(t)
    libre = ~numero(t)
    eje = v * math.pi / 4                # el eje del golpe, de 45 en 45 grados
    modo = np.cos(2 * (ANG - eje))       # el modo cuadrupolar de la membrana
    idx = np.zeros((CELDA, CELDA), np.int8)
    if f == 0:
        rho = Re + 1.0 - AMP * modo      # aplastado en el eje del golpe
        aro = (R >= rho - 0.5) & (R < rho + 0.5) & libre
        idx[aro] = 1
        # la bolsa se enciende entera, densa en la membrana
        dens = np.clip(1 - np.abs(R - Rb) / 2.5, 0, 1) * 0.5
        dens[R >= rho - 0.5] = 0
        tramar(idx, dens * libre, 2, 3, corte=0.5)
        # el aire de fuera, que se apaga en 3 px
        d = R - (rho + 0.5)
        dens = np.where(d >= 0, np.clip(1 - d / 3.0, 0, 1) ** 1.4 * 0.75, 0)
        tramar(idx, dens * libre, 3, 4, corte=0.45)
    else:
        rho = Re + 1.0 + SUELTA + AMP * modo   # rebota al reves
        aro = (R >= rho - 0.5) & (R < rho + 0.5) & (_ruido(v) > -0.25) & libre
        idx[aro] = 2
        # el rastro de dentro, ralo y apagado
        d = rho - 0.5 - R
        dens = np.where((d > 0) & (R > Rb - 1), np.clip(1 - d / 4.0, 0, 1) * 0.4, 0)
        tramar(idx, dens * libre, 4, 5, corte=0.3)
    return idx


if __name__ == '__main__':
    ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
