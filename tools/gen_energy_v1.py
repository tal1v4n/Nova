"""NOVA - ART-UI-HUD-ENERGY v1, el numero de Energia: «El deposito».

Ref: NOVA_Assets_Diseno.md A.6 · NOVA_Estados_Animaciones.md 14.7 (capa 6) ·
Spec 13.12 (lenguaje de la UI), 18 #48 (el numero bajo el sprite) ·
NOVA_Nodos.md (topes: Asteroide 10, Estrella 50, Pulsar y Magnetar 100)

Este archivo tiene ademas LO COMUN A LAS TRES PROPUESTAS -el esqueleto de
las cifras, el montaje, las comprobaciones y las vistas-; `gen_energy_v2.py`
y `gen_energy_v3.py` solo traen su dibujo.

QUE ES. El numero que el jugador lee mas veces por partida (14.7): la
Energia de cada nodo, bajo su sprite y centrado bajo su huella (18 #48).
Nunca se oculta, en ningun estado. Va en la capa 6, por encima de tintes,
auras y glifos, y por encima de la capa 0 -linea de envio, arco, haz y
anillos de radio-, que pasan por debajo.

UNA SOLA FAMILIA DE CIFRAS. Las cifras de la Carga del proton (A.4.2,
`world_charge_digits`) ya son las de NOVA: 5 x 7 de trazo 1, el 1 de 3. Aqui
van a x2 -trazo de 2 px, 10 x 14, el 1 de 6 x 14- con contorno de 1 px: la
celda mide 12 x 16 (8 x 16 el 1) y el avance es el ancho de la celda, asi
que los contornos de dos cifras se tocan y no se pisan. «100», el numero mas
ancho posible (tope 100), mide 32 px: cabe en el hueco de 38 que deja abierto
«La orbita» por abajo (A.4.3), y los 16 de alto caben en los 20 que reserva
`NodeLayout` (mayuscula de Jersey 10 a x2). El contorno hace que se lea sobre
cualquier cosa de debajo: cielo, anillo de radio o plasma.

LAS TRES TESIS, una por propuesta, con el mismo esqueleto:

    v1 «El deposito»   CUANTO CABE: la cifra se llena como un tanque
    v2 «El contador»   HACIA DONDE VA: las cifras ruedan al cambiar
    v3 «El filamento»  DE QUIEN ES: las cifras son plasma del bando

    py tools/gen_energy_v1.py         piezas, comprobaciones y vistas
    py tools/gen_energy_v1.py --gif   y las animadas

----------------------------------------------------------------------------

LA TESIS DE LA v1: EL NUMERO ES UN DEPOSITO. El numero dice cuanto hay; su
relleno dice cuanto cabe. Las cifras se llenan de abajo arriba con la
Energia sobre el tope del nodo, en 8 escalones -uno por fila de diseno, de
0 a 7-, con una superficie clara arriba del liquido, la misma linea en todas
las cifras del numero. Lo vacio queda en la sombra del bando, todavia
legible. Lleno -en el tope- el numero entero arde en blanco: una Estrella
que ya no acumula, un Asteroide propio listo para encender, un Pulsar con
la recamara llena. Un 25 de una Estrella esta a medias; el mismo 25 de un
Pulsar, a un cuarto, y se ve sin hacer la cuenta.

    escalon = 7 si Energia >= tope; si no, min(6, Energia * 7 // tope)

El color es el del bando (`str_p`, `str_e`, `neutral`): cian lo propio,
magenta lo del Enemigo (13.12). Lo neutral se llena pero no arde: no
tiene recamara, y un Asteroide neutral esta siempre en su tope; si ardiera,
los ocho del tablero serian lo que mas grita.
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

ASSET = 'energy_label'
VERSION = 1

ESCALA = 2
ALTO_DIS = 7                      # filas de diseno de una cifra
ALTO = ALTO_DIS * ESCALA + 2      # 16: la cifra a x2 y su contorno
CAJA = (20, 38)                   # alto y ancho maximos: NodeLayout y «La orbita»
GAP = 2                           # NodeLayout.Gap: entre la huella y el numero
FPS = 12
FONDO = np.array(hex_a_rgb(FONDO_HEX), np.uint8)
CONT = np.array(CONTORNO, np.uint8)

FACCIONES = ('player', 'enemy', 'neutral')
FAC_LEVEL = {0: 'neutral', 1: 'player', 2: 'enemy'}
TOPES = {0: 10, 1: 50, 2: 100, 3: 100}

# Las cifras del proton (gen_proton_v2.py), las mismas: una sola familia.
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


# --- El esqueleto ------------------------------------------------------------

def glifo(c):
    """La cifra en su rejilla de diseno, 7 filas."""
    return np.array([[ch == '#' for ch in fila] for fila in GLIFOS[c]], bool)


def ancho_celda(c):
    return glifo(c).shape[1] * ESCALA + 2


AVANCE = [ancho_celda(c) for c in '0123456789']
CELDA_W = max(AVANCE)


def mascara(c):
    """La cifra a x2 dentro de su celda, con el hueco del contorno."""
    g = np.repeat(np.repeat(glifo(c), ESCALA, 0), ESCALA, 1)
    m = np.zeros((ALTO, g.shape[1] + 2), bool)
    m[1:-1, 1:-1] = g
    return m


def contorno(m):
    h, w = m.shape
    p = np.pad(m, 1)
    b = np.zeros_like(m)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            b |= p[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
    return b & ~m


def fila_dis(y):
    """Fila de diseno (0 arriba, 6 abajo) de una fila de pixel de la celda."""
    return (y - 1) // ESCALA


def celda(c, color):
    """RGBA de una cifra: `color(y, x, m)` da el RGB de cada pixel de trazo."""
    m = mascara(c)
    b = contorno(m)
    img = np.zeros(m.shape + (4,), np.uint8)
    for y, x in zip(*np.nonzero(m)):
        img[y, x, :3] = color(y, x, m)
    img[b, :3] = CONT
    img[m | b, 3] = 255
    return img


def componer(celdas):
    """Las celdas de izquierda a derecha, cada una a su avance."""
    w = sum(c.shape[1] for c in celdas)
    out = np.zeros((celdas[0].shape[0], w, 4), np.uint8)
    x = 0
    for c in celdas:
        out[:, x:x + c.shape[1]] = c
        x += c.shape[1]
    return out


def ancho_numero(texto):
    return sum(AVANCE[int(ch)] for ch in texto)


def hoja(filas):
    """Filas de celdas -cada celda rellenada a CELDA_W por la derecha-."""
    out = []
    for fila in filas:
        cs = []
        for c in fila:
            p = np.zeros((c.shape[0], CELDA_W, 4), np.uint8)
            p[:, :c.shape[1]] = c
            cs.append(p)
        out.append(np.hstack(cs))
    return np.vstack(out)


def escalon(energia, tope):
    if energia >= tope:
        return 7
    return min(6, energia * 7 // tope)


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


def sobre_c(lienzo, im, cx, cy):
    h, w = im.shape[:2]
    sobre(lienzo, im, int(cx) - w // 2, int(cy) - h // 2)


def huella(carpeta, pre, n):
    v = {'asteroide': 1, 'estrella': 1, 'pulsar': 2, 'magnetar': 1}[carpeta]
    with open(os.path.join(RAIZ, 'art', 'nodes', carpeta, f'v{v}', f'{pre}_huella.json')) as f:
        j = json.load(f)
    return j['footprintPixels'][j['levels'].index(n)]


def colocar(lienzo, etq, cx, cy, hu):
    """Como NodeLayout: centrado bajo la huella, a GAP de ella."""
    arriba = cy + (hu + 1) // 2 + GAP + (CAJA[0] - etq.shape[0]) // 2
    sobre(lienzo, etq, int(cx) - etq.shape[1] // 2, int(arriba))


# --- Comprobaciones ------------------------------------------------------------

def comprobar(gen, permitidos, nombre):
    """Todos los numeros posibles, de los tres bandos y los tres topes, en
    todos los frames del bucle: caja, alfa binario, paleta y bucle."""
    fallos = []
    usados = set()
    frames = getattr(gen, 'FRAMES', 1)
    for fac in FACCIONES:
        for tope in (10, 50, 100):
            for e in range(tope + 1):
                for f in range(frames):
                    im = gen.etiqueta(str(e), fac, e, tope, f)
                    if im.shape[0] > CAJA[0] or im.shape[1] > CAJA[1]:
                        fallos.append(f'{e} ({fac}) mide {im.shape[1]} x {im.shape[0]}')
                    if not set(np.unique(im[..., 3])) <= {0, 255}:
                        fallos.append('alfa no binario')
                    usados |= {tuple(int(v) for v in c) for c in im[..., :3][im[..., 3] > 0]}
    fuera = usados - permitidos
    if fuera:
        fallos.append('colores fuera de paleta: ' + ', '.join('#%02X%02X%02X' % c for c in sorted(fuera)))
    # cada pixel de trazo esta rodeado de trazo o contorno: se lee sobre todo
    for c in '0123456789':
        m = mascara(c)
        if m[0].any() or m[-1].any() or m[:, 0].any() or m[:, -1].any():
            fallos.append(f'la cifra {c} no deja sitio a su contorno')
    if ancho_numero('100') > CAJA[1]:
        fallos.append('«100» no cabe en el hueco')
    fallos += gen.comprobar_propio() if hasattr(gen, 'comprobar_propio') else []
    if fallos:
        raise SystemExit(f'ART-UI-HUD-ENERGY «{nombre}» no pasa:\n  ' + '\n  '.join(sorted(set(fallos))[:30]))
    return len(usados)


# --- Vistas ------------------------------------------------------------------

TIPOS = {0: ('asteroide', 'ast'), 1: ('estrella', 'str'), 2: ('pulsar', 'pul'), 3: ('magnetar', 'mag')}
FAC_SPR = {'neutral': 'neutral', 'player': 'player', 'enemy': 'enemy'}


def nodos_nivel(n):
    s = open(os.path.join(RAIZ, 'Assets', 'Data', 'Levels', f'Level{n}.asset'), encoding='utf-8').read()
    out = []
    for m in re.finditer(r'position: \{x: (-?[\d.]+), y: (-?[\d.]+)\}\s+type: (\d)\s+level: (\d)\s+'
                         r'faction: (\d)\s+initialEnergy: (\d+)', s):
        out.append((float(m[1]), float(m[2]), int(m[3]), int(m[4]), FAC_LEVEL[int(m[5])], int(m[6])))
    return out


def sprite_nodo(t, lv, fac):
    carpeta, pre = TIPOS[t]
    v = {'asteroide': 1, 'estrella': 1, 'pulsar': 2, 'magnetar': 1}[carpeta]
    nombre = f'{pre}_lvl0_{fac}.png' if t == 0 else f'{pre}_lvl{lv}_{fac}.png'
    p = os.path.join(RAIZ, 'art', 'nodes', carpeta, f'v{v}', nombre)
    return np.asarray(Image.open(p).convert('RGBA')) if os.path.exists(p) else None


def tablero(gen, f=0, energias=None):
    """El nivel 9 entero sobre su fondo, con el radio del Magnetar, la barra
    de poder y la de arriba elegidas, y cada nodo con su numero."""
    fondo = np.asarray(Image.open(os.path.join(
        RAIZ, 'art', 'bg', 'fondos', 'v2', 'bg_section3.png')).convert('RGB')).copy()
    nodos = nodos_nivel(9)
    for x, y, t, lv, fac, e in nodos:
        if t == 3:
            r = os.path.join(RAIZ, 'art', 'world', 'radius', 'v2', f'world_radius_{fac}_lvl{lv}.png')
            sobre_c(fondo, np.asarray(Image.open(r).convert('RGBA')), 480 + x * 64, 270 - y * 64)
    for i, (x, y, t, lv, fac, e) in enumerate(nodos):
        im = sprite_nodo(t, lv, fac)
        if im is None:
            continue
        cx, cy = 480 + x * 64, 270 - y * 64
        sobre_c(fondo, im, cx, cy)
        if energias is not None:
            e = energias[i % len(energias)]
        tope = TOPES[t]
        e = min(e, tope)
        carpeta, pre = TIPOS[t]
        colocar(fondo, gen.etiqueta(str(e), fac, e, tope, (f + 5 * i) % getattr(gen, 'FRAMES', 1)),
                cx, cy, huella(carpeta, pre, 0 if t == 0 else lv))
    try:
        import gen_powerbar_v1 as pb
        barra = pb.montar(pb.piezas_v1(), 0.46, f % pb.FRAMES)
        mm = barra[..., 3] > 0
        fondo[540 - pb.ALTO:][mm] = barra[..., :3][mm]
        import gen_topbar_v3 as tb
        import gen_topbar_v1 as tb1
        tb1.montar(tb.piezas(), fondo, tb1.etiqueta_de(9), '2:14')
    except Exception:
        pass
    return fondo


def estres(gen, texto='37', fac='player', tope=50, f=0):
    """El mismo numero sobre lo peor que puede tener debajo (14.7): el cielo
    mas claro del fondo, el anillo de radio del Magnetar y una linea de
    envio que lo cruza. Celdas de 56 x 36."""
    bg = np.asarray(Image.open(os.path.join(
        RAIZ, 'art', 'bg', 'fondos', 'v2', 'bg_section1.png')).convert('RGB'))
    lum = bg.astype(int).sum(2)
    k = np.ones(36)
    sx = np.apply_along_axis(lambda r: np.convolve(r, k, 'valid'), 1, lum)
    sxy = np.apply_along_axis(lambda c: np.convolve(c, np.ones(36), 'valid'), 0, sx)
    yy, xx = np.unravel_index(np.argmax(sxy), sxy.shape)
    claro = bg[yy:yy + 36, xx:xx + 56].copy()
    cielo = np.empty((36, 56, 3), np.uint8)
    cielo[:] = FONDO
    anillo = cielo.copy()
    r = np.asarray(Image.open(os.path.join(RAIZ, 'art', 'world', 'radius', 'v2',
                                           f'world_radius_{fac}_lvl3.png')).convert('RGBA'))
    h, w = r.shape[:2]
    filas = np.nonzero((r[..., 3] > 0).any(1))[0]
    sobre(anillo, r[filas[-1] - 20:filas[-1] + 16, w // 2 - 28:w // 2 + 28], 0, 0)
    linea = cielo.copy()
    lk = np.asarray(Image.open(os.path.join(RAIZ, 'art', 'world', 'link', 'v2',
                                            'world_link_sheet.png')).convert('RGBA'))
    sobre(linea, lk[16:32, 0:56], 0, 12)
    celdas = []
    for base in (cielo, claro, anillo, linea):
        c = base.copy()
        etq = gen.etiqueta(texto, fac, int(texto), tope, f)
        sobre(c, etq, 28 - etq.shape[1] // 2, 18 - etq.shape[0] // 2)
        celdas.append(c)
        celdas.append(np.zeros((36, 2, 3), np.uint8))
    return np.hstack(celdas[:-1])


def vistas(gen, version, gif=False):
    d = os.path.join(dir_preview('ui_' + ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    t = tablero(gen)
    Image.fromarray(ampliar(t, 2)).save(os.path.join(d, 'tablero_1080p.png'))
    Image.fromarray(ampliar(t[60:480, 20:420], 3)).save(os.path.join(d, 'tablero_izq_x3.png'))
    filas = [estres(gen, '37', fac, 50) for fac in FACCIONES]
    Image.fromarray(ampliar(np.vstack(filas), 6)).save(os.path.join(d, 'estres_x6.png'))
    if hasattr(gen, 'vistas_propias'):
        gen.vistas_propias(d, gif)
    if gif and getattr(gen, 'FRAMES', 1) > 1:
        cuadros = [Image.fromarray(ampliar(tablero(gen, f)[60:480, 20:420], 2))
                   for f in range(gen.FRAMES)]
        cuadros[0].save(os.path.join(d, 'tablero_x2.gif'), save_all=True,
                        append_images=cuadros[1:], duration=1000 // FPS, loop=0)
    return d


def comparativa():
    filas = []
    for v in (1, 2, 3):
        if not os.path.isdir(os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{v}')):
            continue
        mod = sys.modules[__name__] if v == 1 else __import__(f'gen_energy_v{v}')
        t = tablero(mod)
        filas.append(t[80:300, 40:420])
        filas.append(np.zeros((3, 380, 3), np.uint8))
    if filas:
        Image.fromarray(ampliar(np.vstack(filas[:-1]), 3)).save(
            os.path.join(dir_preview('ui_' + ASSET), 'comparativa_x3.png'))


def ejecutar(gen, version, nombre, permitidos):
    colores = comprobar(gen, permitidos, nombre)
    d = os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)
    gen.exportar(d)
    vistas(gen, version, '--gif' in sys.argv)
    comparativa()
    print(f'v{version} «{nombre}»: {colores} colores  -> {os.path.relpath(d, RAIZ)}')


# =============================================================================
# v1 «El deposito»
# =============================================================================

RAMPA_FAC = {'player': 'str_p', 'enemy': 'str_e', 'neutral': 'neutral'}
LLENO, SUPERFICIE, VACIO = 2, 1, 5        # indices de la rampa
ARDE, ARDE_TOPE = 1, 0                    # el numero en el tope


def cifra_v1(c, fac, esc):
    R = rampa(RAMPA_FAC[fac])
    nivel = ALTO_DIS - esc               # primera fila de diseno llena

    def color(y, x, m):
        fd = fila_dis(y)
        if esc == 7 and fac == 'neutral':
            # lo neutral no tiene recamara: un Asteroide neutral esta SIEMPRE
            # a 10 de 10 y, si ardiera, seria lo mas llamativo del tablero
            return R[LLENO]
        if esc == 7:
            # en el tope arde entero: la fila de arriba de cada trazo, blanca
            return R[ARDE_TOPE] if not m[y - 1, x] else R[ARDE]
        if fd < nivel:
            return R[VACIO]
        # la superficie: la primera fila de pixel del liquido
        if y == 1 + nivel * ESCALA:
            return R[SUPERFICIE]
        return R[LLENO]
    return celda(c, color)


def etiqueta(texto, fac, energia, tope, f=0):
    esc = escalon(energia, tope)
    return componer([cifra_v1(ch, fac, esc) for ch in texto])


def exportar(d):
    filas = [[cifra_v1(c, fac, e) for c in '0123456789']
             for fac in FACCIONES for e in range(8)]
    Image.fromarray(hoja(filas), 'RGBA').save(os.path.join(d, 'ui_energy_label_sheet.png'))
    with open(os.path.join(d, 'ui_energy_label.json'), 'w') as fh:
        json.dump({'cellWidth': CELDA_W, 'cellHeight': ALTO, 'advance': AVANCE,
                   'rows': [f'{fac}_{e}' for fac in FACCIONES for e in range(8)],
                   'factions': list(FACCIONES), 'steps': 8, 'boxHeight': CAJA[0],
                   'note': 'columna = cifra 0-9; fila = bando * 8 + escalon; '
                           'escalon = 7 si Energia >= tope, si no min(6, Energia * 7 // tope); '
                           'pivote abajo a la izquierda; avance = ancho de la celda'}, fh, indent=1)


def vistas_propias(d, gif):
    """Los ocho escalones de los tres bandos, a x6."""
    filas = []
    for fac in FACCIONES:
        fila = []
        for e in range(8):
            c = np.empty((ALTO + 4, 32, 3), np.uint8)
            c[:] = FONDO
            sobre(c, componer([cifra_v1(ch, fac, e) for ch in '48']), 4, 2)
            fila.append(c)
        filas.append(np.hstack(fila))
    Image.fromarray(ampliar(np.vstack(filas), 6)).save(os.path.join(d, 'escalones_x6.png'))


if __name__ == '__main__':
    ejecutar(sys.modules[__name__], 1, 'El deposito', colores_de('str_p', 'str_e', 'neutral') | {tuple(CONT)})
