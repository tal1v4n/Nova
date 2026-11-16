"""NOVA - ART-UI-CHARGE v1, la barra de carga del Pulsar: «La ampolla».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 7.1 (regla de carga), 7.2 (costes),
7.5 (robo de carga), 13.5 (rueda), 13.12 (lenguaje de la UI) · 14.4 (los
colores de los poderes) · 14.7 (capa 6) · 18 #48 (bajo el numero)

Este archivo tiene ademas LO COMUN A LAS TRES PROPUESTAS -el sitio, los
estados, las comprobaciones y las vistas-; `gen_charge_v2.py` y
`gen_charge_v3.py` solo traen su dibujo.

QUE CUENTA LA BARRA (7.1, 7.5). Que poder carga -por su color: hielo,
`decaimiento` o `gusano` (14.4)-, cuanto lleva -`energiaInvertida /
costeTotal`-, si esta PAUSADA -el Pulsar se quedo sin Energia o esta
Congelado- y si esta LISTA, al 100 %, que se mantiene sin limite. Se ve en
los Pulsares de los dos bandos: la barra llena del Enemigo es el contrajuego
entero (7.5), asi que LISTA tiene que leerse desde la otra punta del tablero.

EL SITIO. Bajo el numero de Energia (18 #48): arriba de la barra a media
huella + 2 + 20 (la caja del numero) + 2. El Pulsar mas bajo de los nueve
niveles -nivel 3, huella 68, en y = -3 u del nivel 9- deja la barra a 58 px
de su centro y a 78 - 12 = 66 de la barra de poder: **6 px de alto** como
mucho, con 2 de aire. El generador lo comprueba contra los nueve assets.
De ancho, 48 como mucho.

LAS TRES TESIS:

    v1 «La ampolla»     el poder es una MATERIA que llena un tubo de vidrio
    v2 «Los cartuchos»  la barra MIDE LO QUE CUESTA: un cartucho cada 5
    v3 «La mecha»       la barra es el HAZ EN MINIATURA, que arde hacia la punta

    py tools/gen_charge_v1.py         piezas, comprobaciones y vistas
    py tools/gen_charge_v1.py --gif   y la animada

----------------------------------------------------------------------------

LA TESIS DE LA v1: UNA AMPOLLA. Un tubo de vidrio de 40 x 6 con las puntas
redondeadas y el cristal en la sombra del bando, que se llena de izquierda a
derecha con la MATERIA del poder, cada una con su vida: escarcha que
centellea (Cero Absoluto), un liquido verde con burbujas que suben
(Decaimiento) y un remolino violeta de bandas que corren (Agujero de
Gusano). La materia lleva un menisco claro en el frente.

    PAUSADA  la materia se para y se apaga una banda: el bucle se congela
    LISTA    el cristal toma el color del poder y una luz le da la vuelta
             entera cada 2.67 s -el patron «Sonar» de 13.12, «lo que toca
             hacer ahora»-; en el Pulsar enemigo, lo que amenaza
"""

import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import CONTORNO, RAIZ, dir_preview, rampa
from nova_paleta import FONDO as FONDO_HEX, RAMPAS, hex_a_rgb

ASSET = 'charge_bar'

ALTO_MAX = 6
ANCHO_MAX = 48
AIRE_POWERBAR = 1               # px minimos entre la barra y la de poder
FRAMES = 32                     # 2.67 s a 12 fps: bucle de ambiente (13.12)
FPS = 12
FONDO = np.array(hex_a_rgb(FONDO_HEX), np.uint8)
CONT = np.array(CONTORNO, np.uint8)

PODERES = {'zero': ('hielo', 20), 'decay': ('decaimiento', 35), 'wormhole': ('gusano', 60)}
FACCIONES = ('player', 'enemy')
RAMPA_FAC = {'player': 'str_p', 'enemy': 'str_e'}


def coste(p):
    return PODERES[p][1]


def rampa_poder(p):
    return rampa(PODERES[p][0])


def arriba(huella):
    """Fila de arriba de la barra bajo el centro del nodo (NodeLayout)."""
    return (huella + 1) // 2 + 2 + 20 + 2


# --- Utilidades --------------------------------------------------------------

def colores_de(*nombres):
    s = set()
    for n in nombres:
        s |= {hex_a_rgb(h) for h in RAMPAS[n]}
    return s


def ampliar(im, k):
    return np.repeat(np.repeat(im, k, axis=0), k, axis=1)


def sobre(lienzo, im, x0, y0):
    h, w = im.shape[:2]
    x1, y1 = max(x0, 0), max(y0, 0)
    x2, y2 = min(x0 + w, lienzo.shape[1]), min(y0 + h, lienzo.shape[0])
    if x2 <= x1 or y2 <= y1:
        return
    sub = im[y1 - y0:y2 - y0, x1 - x0:x2 - x0]
    m = sub[..., 3] > 0
    lienzo[y1:y2, x1:x2][m] = sub[..., :3][m]


def rgba(h, w):
    return np.zeros((h, w, 4), np.uint8)


def pon(img, y, x, rgb):
    if 0 <= y < img.shape[0] and 0 <= x < img.shape[1]:
        img[y, x, :3] = rgb
        img[y, x, 3] = 255


def hash2(a, b, c=0):
    """Ruido entero determinista."""
    v = (a * 73856093) ^ (b * 19349663) ^ (c * 83492791)
    return (v ^ (v >> 13)) & 0xFFFF


def tesela(lienzo_w, tile):
    """Una tesela repetida desde la izquierda, como Draw Mode: Tiled."""
    reps = -(-lienzo_w // tile.shape[1])
    return np.tile(tile, (1, reps, 1))[:, :lienzo_w]


# --- Comprobaciones ------------------------------------------------------------

def pulsares():
    """(nivel, cx, cy en px de pantalla, huella) de cada Pulsar de los nueve."""
    hu = json.load(open(os.path.join(RAIZ, 'art', 'nodes', 'pulsar', 'v2', 'pul_huella.json')))
    out = []
    for n in range(1, 10):
        s = open(os.path.join(RAIZ, 'Assets', 'Data', 'Levels', f'Level{n}.asset'), encoding='utf-8').read()
        for m in re.finditer(r'position: \{x: (-?[\d.]+), y: (-?[\d.]+)\}\s+type: (\d)\s+level: (\d)', s):
            x, y, t, lv = float(m[1]), float(m[2]), int(m[3]), int(m[4])
            if t == 2:
                out.append((n, 480 + x * 64, 270 - y * 64, hu['footprintPixels'][hu['levels'].index(lv)]))
    return out


def estados():
    """Todos los estados posibles: (poder, invertido, bando, pausada)."""
    for p in PODERES:
        for fac in FACCIONES:
            for inv in range(coste(p) + 1):
                yield p, inv, fac, False
                if 0 < inv < coste(p):
                    yield p, inv, fac, True


def comprobar(gen, permitidos, nombre):
    fallos = []
    usados = set()
    alto = 0
    for p, inv, fac, pau in estados():
        for f in range(FRAMES):
            im = gen.barra(p, inv, fac, pau, f)
            alto = max(alto, im.shape[0])
            if im.shape[0] > ALTO_MAX or im.shape[1] > ANCHO_MAX:
                fallos.append(f'{p} {inv} mide {im.shape[1]} x {im.shape[0]}')
            if not set(np.unique(im[..., 3])) <= {0, 255}:
                fallos.append('alfa no binario')
            usados |= {tuple(int(v) for v in c) for c in im[..., :3][im[..., 3] > 0]}
    fuera = usados - permitidos
    if fuera:
        fallos.append('colores fuera de paleta: ' + ', '.join('#%02X%02X%02X' % c for c in sorted(fuera)))
    for p in PODERES:
        for fac in FACCIONES:
            c = coste(p)
            # LISTA se distingue de «casi»: en todos los frames
            for f in range(FRAMES):
                if (gen.barra(p, c - 1, fac, False, f) == gen.barra(p, c, fac, False, f)).all():
                    fallos.append(f'{p}: lista no se distingue de {c - 1}')
                    break
            # PAUSADA se distingue de cargando, en algun frame
            m = c // 2
            if all((gen.barra(p, m, fac, True, f) == gen.barra(p, m, fac, False, f)).all()
                   for f in range(FRAMES)):
                fallos.append(f'{p}: pausada no se distingue')
            # el bucle de LISTA no es una imagen fija
            if len({gen.barra(p, c, fac, False, f).tobytes() for f in range(FRAMES)}) < 2:
                fallos.append(f'{p}: lista no se mueve')
    # el poder se distingue de los otros dos a media carga
    for fac in FACCIONES:
        vistos = {}
        for p in PODERES:
            vistos[p] = gen.barra(p, coste(p) // 2, fac, False, 0).tobytes()
        if len(set(vistos.values())) < 3:
            fallos.append('dos poderes iguales')
    # el sitio: ninguna barra pisa la de poder en ninguno de los nueve niveles
    for n, cx, cy, hu in pulsares():
        abajo = cy + arriba(hu) + alto
        if abajo > 540 - 12 - AIRE_POWERBAR:
            fallos.append(f'nivel {n}: la barra del Pulsar de ({cx:.0f}, {cy:.0f}) baja a {abajo:.0f}')
    fallos += gen.comprobar_propio() if hasattr(gen, 'comprobar_propio') else []
    if fallos:
        raise SystemExit(f'ART-UI-CHARGE «{nombre}» no pasa:\n  ' + '\n  '.join(sorted(set(fallos))[:30]))
    return len(usados)


# --- Vistas ------------------------------------------------------------------

def _energia():
    import gen_energy_v1 as e1
    import gen_energy_v3 as e3
    return e1, e3


def pulsar_sprite(fac, lv=3):
    p = os.path.join(RAIZ, 'art', 'nodes', 'pulsar', 'v2', f'pul_lvl{lv}_{fac}.png')
    return np.asarray(Image.open(p).convert('RGBA'))


def pulsar_con_barra(lienzo, gen, cx, cy, fac, energia, p, inv, pau, f, lv=3):
    e1, e3 = _energia()
    hu = json.load(open(os.path.join(RAIZ, 'art', 'nodes', 'pulsar', 'v2', 'pul_huella.json')))
    huella = hu['footprintPixels'][hu['levels'].index(lv)]
    e1.sobre_c(lienzo, pulsar_sprite(fac, lv), cx, cy)
    e1.colocar(lienzo, e3.etiqueta(str(energia), fac, energia, 100, f), cx, cy, huella)
    if p is not None:
        b = gen.barra(p, inv, fac, pau, f)
        sobre(lienzo, b, int(cx) - b.shape[1] // 2, int(cy) + arriba(huella))


CASOS = (  # (titulo, fraccion invertida, pausada)
    ('25 %', .25, False), ('70 %', .7, False), ('70 % pausada', .7, True), ('lista', 1.0, False))


def contexto(gen, f=0):
    """Un Pulsar de nivel 3 por poder, bando y caso, con su numero encima."""
    filas = []
    for p in PODERES:
        for fac in FACCIONES:
            fila = []
            for _, fr, pau in CASOS:
                c = np.empty((116, 84, 3), np.uint8)
                c[:] = FONDO
                inv = int(round(fr * coste(p)))
                pulsar_con_barra(c, gen, 42, 48, fac, 100 - inv if fr < 1 else 40, p, inv, pau, f)
                fila.append(c)
            filas.append(np.hstack(fila))
    return np.vstack(filas)


def tablero(gen, f=0):
    """El nivel 9 con los numeros elegidos, y sus dos Pulsares enemigos: el
    de arriba cargando un Agujero de Gusano y el de abajo -el mas bajo de los
    nueve niveles- con un Decaimiento listo."""
    e1, e3 = _energia()
    t = e1.tablero(e3, f)
    casos = {(4.75, 3.0): ('wormhole', 27, False), (4.75, -3.0): ('decay', 35, False)}
    hu = json.load(open(os.path.join(RAIZ, 'art', 'nodes', 'pulsar', 'v2', 'pul_huella.json')))
    huella = hu['footprintPixels'][hu['levels'].index(3)]
    for (x, y), (p, inv, pau) in casos.items():
        b = gen.barra(p, inv, 'enemy', pau, f)
        cx, cy = 480 + x * 64, 270 - y * 64
        sobre(t, b, int(cx) - b.shape[1] // 2, int(cy) + arriba(huella))
    return t


def secuencia(gen):
    """Un Pulsar propio carga Cero Absoluto, se queda sin Energia a medias,
    recibe y termina; luego espera listo. (invertido, pausada) por frame."""
    out = []
    for i in range(0, 11):
        out.append((i, False))
    for _ in range(14):
        out.append((10, True))
    for i in range(11, 21):
        out.append((i, False))
    for _ in range(FRAMES):
        out.append((20, False))
    return out


def vistas(gen, version, gif=False):
    d = os.path.join(dir_preview('ui_' + ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(contexto(gen), 3)).save(os.path.join(d, 'contexto_x3.png'))
    t = tablero(gen)
    Image.fromarray(ampliar(t, 2)).save(os.path.join(d, 'tablero_1080p.png'))
    Image.fromarray(ampliar(t[0:540, 560:960], 2)).save(os.path.join(d, 'tablero_der_x2.png'))
    # las barras solas, a x8: los tres poderes en los cuatro casos
    filas = []
    for p in PODERES:
        for fac in FACCIONES:
            fila = []
            for _, fr, pau in CASOS:
                c = np.empty((ALTO_MAX + 4, ANCHO_MAX + 4, 3), np.uint8)
                c[:] = FONDO
                b = gen.barra(p, int(round(fr * coste(p))), fac, pau, 0)
                sobre(c, b, 2 + (ANCHO_MAX - b.shape[1]) // 2, 2)
                fila.append(c)
            filas.append(np.hstack(fila))
    Image.fromarray(ampliar(np.vstack(filas), 8)).save(os.path.join(d, 'barras_x8.png'))
    if gif:
        ims = []
        seq = secuencia(gen)
        for i, (inv, pau) in enumerate(seq):
            c = np.empty((112, 100, 3), np.uint8)
            c[:] = FONDO
            pulsar_con_barra(c, gen, 50, 48, 'player', 60 - inv, 'zero', inv, pau, i % FRAMES)
            ims.append(Image.fromarray(ampliar(c, 4)))
        ims[0].save(os.path.join(d, 'carga_x4.gif'), save_all=True,
                    append_images=ims[1:], duration=1000 // FPS, loop=0)
        ims = [Image.fromarray(ampliar(contexto(gen, f), 2)) for f in range(FRAMES)]
        ims[0].save(os.path.join(d, 'contexto_x2.gif'), save_all=True,
                    append_images=ims[1:], duration=1000 // FPS, loop=0)
    return d


def comparativa():
    filas = []
    for v in (1, 2, 3):
        if not os.path.isdir(os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{v}')):
            continue
        mod = sys.modules[__name__] if v == 1 else __import__(f'gen_charge_v{v}')
        c = contexto(mod)
        filas.append(np.vstack([c[0:116], c[232:348], c[464:580]]))
        filas.append(np.zeros((348, 3, 3), np.uint8))
    if filas:
        Image.fromarray(ampliar(np.hstack(filas[:-1]), 2)).save(
            os.path.join(dir_preview('ui_' + ASSET), 'comparativa_x2.png'))


def ejecutar(gen, version, nombre, permitidos):
    colores = comprobar(gen, permitidos, nombre)
    d = os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)
    gen.exportar(d)
    vistas(gen, version, '--gif' in sys.argv)
    comparativa()
    print(f'v{version} «{nombre}»: {colores} colores  -> {os.path.relpath(d, RAIZ)}')


# =============================================================================
# v1 «La ampolla»
# =============================================================================

ANCHO = 40
TESELA = 8


def silueta():
    """La ampolla: 40 x 6 con las puntas redondeadas en escalera de 1 px."""
    m = np.zeros((ALTO_MAX, ANCHO), bool)
    m[0, 2:ANCHO - 2] = True
    m[1, 1:ANCHO - 1] = True
    m[2:4, :] = True
    m[4, 1:ANCHO - 1] = True
    m[5, 2:ANCHO - 2] = True
    return m


SIL = silueta()
DENTRO = np.zeros_like(SIL)
DENTRO[1:5, 2:ANCHO - 2] = True
DENTRO[2:4, 1:ANCHO - 1] = True
VIDRIO = SIL & ~DENTRO
X0, X1 = 1, ANCHO - 1            # columnas de la materia: [X0, X1)
ANCHO_MATERIA = X1 - X0          # 38


def contorno_vidrio():
    """El orden de los pixeles del vidrio dando la vuelta: para la luz."""
    pix = [(y, x) for y, x in zip(*np.nonzero(VIDRIO))]
    cy, cx = (ALTO_MAX - 1) / 2, (ANCHO - 1) / 2
    import math
    return sorted(pix, key=lambda q: math.atan2((q[0] - cy) * 6, q[1] - cx))


ORDEN_VIDRIO = contorno_vidrio()


def vidrio_v1(fac, listo_poder=None, f=0):
    img = rgba(ALTO_MAX, ANCHO)
    if listo_poder is None:
        R = rampa(RAMPA_FAC[fac])
        for y, x in ORDEN_VIDRIO:
            pon(img, y, x, R[5])
        # el brillo del cristal: la fila de arriba, en su tercio izquierdo
        for x in range(3, 12):
            pon(img, 0, x, R[4])
    else:
        R = rampa_poder(listo_poder)
        n = len(ORDEN_VIDRIO)
        cabeza = f / FRAMES * n
        for i, (y, x) in enumerate(ORDEN_VIDRIO):
            d = (cabeza - i) % n
            pon(img, y, x, R[0] if d < 2 else R[1] if d < 6 else R[3])
    # el interior vacio: el fondo, para que la ampolla no se vea hueca
    img[DENTRO, :3] = FONDO
    img[DENTRO, 3] = 255
    return img


def materia_v1(p, f, pausada=False):
    """Tesela de 8 x 4 de la materia, frame f. Pausada: quieta y una banda
    mas oscura."""
    R = rampa_poder(p)
    o = 1 if pausada else 0
    f = 0 if pausada else f
    t = rgba(4, TESELA)
    for y in range(4):
        for x in range(TESELA):
            if p == 'zero':
                # escarcha: hielo con centellas que se encienden y se apagan
                c = R[3 + o] if (x + y) % 3 else R[2 + o]
                h = hash2(x, y, 7)
                fase = (h % FRAMES)
                if not pausada and (f - fase) % FRAMES < 3 and h % 3 == 0:
                    c = R[0]
            elif p == 'decay':
                # liquido: el fondo mas oscuro y burbujas que suben
                c = R[4 + o] if y == 3 else R[3 + o]
                for b in range(2):
                    bx = (b * 5 + 2) % TESELA
                    by = 3 - ((f // 4 + b * 4) % 6)
                    if not pausada and x == bx and y == by:
                        c = R[1]
            else:
                # remolino: bandas diagonales que corren hacia el frente
                k = (x - y - f // 2) % 8
                c = R[2 + o] if k < 2 else R[3 + o] if k < 5 else R[4 + o]
            t[y, x, :3] = c
            t[y, x, 3] = 255
    return t


def menisco_v1(p, pausada=False):
    R = rampa_poder(p)
    t = rgba(4, 1)
    t[:, 0, :3] = R[2] if pausada else R[1]
    t[..., 3] = 255
    return t


def barra(p, inv, fac, pausada=False, f=0):
    lista = inv >= coste(p)
    img = vidrio_v1(fac, p if lista else None, f)
    n = ANCHO_MATERIA if lista else ANCHO_MATERIA * inv // coste(p)
    if n > 0:
        m = tesela(n, materia_v1(p, f, pausada and not lista))
        if not lista:
            m[:, -1:] = menisco_v1(p, pausada)
        # la materia en las cuatro filas de dentro, recortada por las puntas
        zona = np.zeros((ALTO_MAX, ANCHO, 4), np.uint8)
        zona[1:5, X0:X0 + n] = m
        ok = DENTRO & (zona[..., 3] > 0)
        img[ok] = zona[ok]
    return img


def exportar(d):
    """El vidrio quieto por bando, el vidrio lista por poder (32 frames), la
    materia por poder (32 frames y una quieta, la pausada) y el menisco."""
    vid = np.vstack([vidrio_v1(fac) for fac in FACCIONES])
    Image.fromarray(vid, 'RGBA').save(os.path.join(d, 'ui_charge_glass.png'))
    lista = np.vstack([np.hstack([vidrio_v1('player', p, f) for f in range(FRAMES)]) for p in PODERES])
    Image.fromarray(lista, 'RGBA').save(os.path.join(d, 'ui_charge_glass_ready_sheet.png'))
    mat = np.vstack([np.hstack([materia_v1(p, f) for f in range(FRAMES)] + [materia_v1(p, 0, True)])
                     for p in PODERES])
    Image.fromarray(mat, 'RGBA').save(os.path.join(d, 'ui_charge_fill_sheet.png'))
    men = np.vstack([np.hstack([menisco_v1(p), menisco_v1(p, True)]) for p in PODERES])
    Image.fromarray(men, 'RGBA').save(os.path.join(d, 'ui_charge_meniscus.png'))
    with open(os.path.join(d, 'ui_charge.json'), 'w') as fh:
        json.dump({'width': ANCHO, 'height': ALTO_MAX, 'fillX0': X0, 'fillY0': 1, 'fillWidth': ANCHO_MATERIA,
                   'fillHeight': 4, 'fillTile': TESELA, 'frames': FRAMES, 'fps': FPS,
                   'powers': list(PODERES), 'factions': list(FACCIONES),
                   'glassRows': list(FACCIONES), 'readyRows': list(PODERES),
                   'fillRows': list(PODERES), 'fillPausedColumn': FRAMES,
                   'note': 'relleno = fillWidth * invertido // coste columnas desde fillX0, la tesela en mosaico '
                           'desde la izquierda y el menisco en su ultima columna; pausada: la columna 32, quieta; '
                           'lista: el vidrio del poder con su frame y la materia entera, sin menisco; '
                           'la materia se recorta por las puntas redondeadas (la mascara es el interior del vidrio)'},
                  fh, indent=1)


if __name__ == '__main__':
    ejecutar(sys.modules[__name__], 1, 'La ampolla',
             colores_de('str_p', 'str_e', 'hielo', 'decaimiento', 'gusano') | {tuple(FONDO)})
