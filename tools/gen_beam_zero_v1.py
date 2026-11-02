"""NOVA - ART-WLD-BEAM-ZER v1, el haz de Cero Absoluto: «El rosario de escarcha».

Ref: NOVA_Assets_Diseno.md A.4.2 · NOVA_GameDesign_Spec.md 7.2.2 y 7.3 ·
NOVA_Estados_Animaciones.md 14.5 (FX-BEAM)

El haz del poder del Pulsar al nodo objetivo, 0.25 s = 3 frames a 12 fps,
sin bucle. Una hoja, `art/world/beam_zero/v1/world_beam_zero_sheet.png`, con
la forma de la de la linea de envio: las tres piezas en filas y los 3 frames
en columnas, celdas de 256 x 16.

    fila 0  nacimiento   16 x 16   en el centro del Pulsar, debajo de el
    fila 1  tramo       256 x 16   se repite en mosaico, anclado en la punta
    fila 2  punta        32 x 16   el contacto en x = 24: centro del objetivo

LA TESIS: el cero absoluto es la ausencia de movimiento. Todo el plasma del
juego -la linea de envio, el arco del Magnetar- serpentea, porque es calor.
Este haz es el unico trazo del tablero **perfectamente recto y rigido**: la
temperatura hecha geometria. Y eso resuelve su riesgo mayor: `hielo` es un
cian palido, primo del cian del Jugador y de `plasma_p`, y un Cero Absoluto
lanzado por el Jugador podria leerse como su propia linea de envio. Por el
color, quiza; por la forma, nunca: una es una onda viva, este es una aguja
ensartada de copos.

  - LA ESPINA. Un nucleo de 2 px exactos sobre el eje, en las filas 7 y 8:
    simetrico al pixel respecto al eje largo, como pide A.4.2 para rotar.
  - LOS COPOS. Cristales de seis brazos ensartados en la espina como las
    cuentas de un rosario, cada 16-28 px y de radio 3 o 4. El hexagono es
    la forma del hielo, y es ademas la respuesta a la rotacion libre del
    haz (A.4.2): un copo de seis brazos se lee igual a cualquier angulo.
    Una pluma o un dibujo con direccion propia se rompe al girar; esto no.
  - EL POLVO DE DIAMANTE. Alrededor, en vez del resplandor denso del
    plasma, un vaho ralo y destellos sueltos fuera del trazo: los cristales
    de hielo en el aire que brillan con el sol polar.

TRES FRAMES QUE CUENTAN UNA CONGELACION, no un trazo que entra y sale:

  0. NUCLEACION. La espina aparece en tono medio y cada copo es solo su
     semilla: los dos primeros pixeles de sus brazos.
  1. ESCARCHA. Los copos crecen enteros, con el centro en el tono mas claro
     del haz, y el vaho y los destellos se encienden. Es el frame que se
     recuerda.
  2. QUIEBRA. El hielo es vidrio (A.3.4, «El vidrio», el estado que este
     haz provoca): la espina se parte justo donde estaban los copos, que
     eran sus puntos de tension, y de cada copo quedan las puntas de sus
     brazos, sueltas un pixel hacia fuera como esquirlas. Todo baja una
     banda.

NADA LLEGA AL BLANCO. El indice 0 de `hielo` es `#FFFFFF` y el blanco puro es
de FX-CAP (14.5). El punto mas claro del haz es `hielo[1]`, el centro de cada
copo; `#B8F0FF` -el ancla funcional, en `hielo[2]`- es la espina y los brazos.

    py tools/gen_beam_zero_v1.py            la hoja + vistas fijas
    py tools/gen_beam_zero_v1.py --gif      ademas, el tablero en movimiento
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import BAYER8, RAIZ, Filamento, dir_preview, rampa, rotar_tira
from nova_paleta import FONDO, hex_a_rgb

ASSET   = 'beam_zero'
VERSION = 1
RAMPA   = 'hielo'

FRAMES   = 3
ALTO     = 16
EJE      = 8.0          # entre las filas 7 y 8: la espina las ocupa las dos
TESELA   = 256
NAC      = 16
PUNTA    = 32
CONTACTO = 24
MITAD    = ALTO // 2    # filas 0..7, la mitad de arriba; la de abajo, espejo
TRAZO    = 4            # el trazo solido, en +-4 px del eje: 8 px (A.4.2)

SEMILLA  = 17
SEP      = (16, 28)     # entre copo y copo
RADIOS   = (3.2, 4.2, 4.2)  # radio de cada copo: 4.2 llega justo al borde del trazo
GROSOR   = 0.52         # medio grosor de un brazo, en px
BARBA    = None         # sin dendrita: a 8 px se funde con el brazo en un nudo
DESPEGUE = 1.2          # los brazos nacen a 1 px de la espina: rayos, no un nudo
HUECO    = 2            # la grieta que deja cada copo en la quiebra
DESTELLOS = 10          # polvo de diamante por tesela, fuera del trazo

# Bandas de hielo por frame: (espina, brazo, centro del copo)
TONOS = (
    (3, 3, 2),          # nucleacion
    (2, 2, 1),          # escarcha
    (3, 4, 3),          # quiebra
)
# Vaho: (peso, caida en px) por frame
VAHO = ((0.45, 1.0), (0.8, 1.5), (0.3, 0.9))


def _sembrar(rng, total, lo, hi):
    """Posiciones en [0, total) con separaciones en [lo, hi], periodicas:
    la ultima separacion cierra contra la primera, asi que la tesela casa."""
    pos, x = [], rng.uniform(0, lo)
    while x < total - lo:
        pos.append(x)
        x += rng.uniform(lo, hi)
    return pos


def _brazos(dx, dy, radio, desde=0.0, empuje=0.0):
    """Mascara de los dos brazos de un copo que suben a 60 y 120 grados
    (los de 0 y 180 van por la espina y los de abajo son el espejo).
    `dx`, `dy` son la posicion relativa al centro, `dy` hacia fuera del eje.
    Solo el tramo del brazo entre `desde` y `radio`, desplazado `empuje` px
    a lo largo del propio brazo: asi se sueltan las esquirlas."""
    m = np.zeros(dx.shape, bool)
    for ang in (60.0, 120.0):
        a = math.radians(ang)
        ca, sa = math.cos(a), math.sin(a)
        t = dx * ca + dy * sa - empuje
        perp = np.abs(-dx * sa + dy * ca)
        m |= (perp < GROSOR) & (t > desde) & (t <= radio)
        if BARBA and radio >= 3.5 and desde < BARBA[0]:
            # la dendrita: dos ramitas a +-60 grados del brazo, a media altura
            bx = BARBA[0] * ca + empuje * ca
            by = BARBA[0] * sa + empuje * sa
            for giro in (-60.0, 60.0):
                b = a + math.radians(giro)
                cb, sb = math.cos(b), math.sin(b)
                tb = (dx - bx) * cb + (dy - by) * sb
                pb = np.abs(-(dx - bx) * sb + (dy - by) * cb)
                m |= (pb < GROSOR) & (tb > 0.2) & (tb <= BARBA[1])
    return m


class Rosario:
    """El haz de Cero Absoluto. Misma interfaz que `nova_core.Filamento`
    -`piezas`, `pieza`, `hoja`, `linea`, `eje`, `frames`- para que las vistas
    y Unity lo monten igual que la linea de envio."""

    def __init__(self, ramp):
        self.ramp = ramp
        self.alto, self.eje, self.tesela = ALTO, EJE, TESELA
        self.nac, self.punta, self.c_x = NAC, PUNTA, CONTACTO
        self.frames = FRAMES
        self.piezas = (('nacimiento', NAC), ('tramo', TESELA), ('punta', PUNTA))
        rng = np.random.default_rng(SEMILLA)
        # el centro de cada copo en el centro de un pixel: si no, sus dos
        # brazos se rasterizan distintos y el copo sale torcido
        self.copos = [(math.floor(u) + 0.5, float(rng.choice(RADIOS)))
                      for u in _sembrar(rng, TESELA, *SEP)]
        self.destellos = [(float(rng.uniform(0, TESELA)), int(rng.integers(0, 3)),
                           int(rng.integers(0, FRAMES))) for _ in range(DESTELLOS)]

    def _rel(self, u, centro):
        """Distancia con signo de u al centro, periodica en la tesela."""
        return (u - centro + TESELA / 2) % TESELA - TESELA / 2

    def _mitad(self, nombre, f):
        """La mitad de arriba de una pieza en el frame f, filas 0..7.
        Devuelve (banda, densidad, polvo): banda es el indice de rampa de cada
        pixel solido (-1 vacio) y polvo marca los destellos, que son los
        unicos solidos que pueden salir del trazo."""
        if nombre == 'tramo':
            u0, ancho = 0, TESELA
        elif nombre == 'punta':
            u0, ancho = 0, PUNTA
        else:
            u0, ancho = -NAC, NAC
        x = np.arange(ancho)
        u = (x + u0 + 0.5)[None, :]
        py = (np.arange(MITAD) + 0.5)[:, None]
        dy = EJE - py                           # hacia fuera del eje, > 0
        banda = np.full((MITAD, ancho), -1, np.int8)
        t_espina, t_brazo, t_centro = TONOS[f]

        # alcance de la pieza: el nacimiento sale de debajo del Pulsar y la
        # punta acaba en el contacto
        if nombre == 'punta':
            vivo = x <= CONTACTO
        elif nombre == 'nacimiento':
            vivo = x >= 3
        else:
            vivo = np.ones(ancho, bool)
        copos = [] if nombre == 'nacimiento' else [
            (c, r) for c, r in self.copos
            if nombre == 'tramo' or c < CONTACTO - 4 or c > TESELA - 6]

        # 1. la espina, partida en la quiebra donde estaban los copos
        espina = vivo.copy()
        if f == 2:
            for c, _ in copos:
                espina &= np.abs(self._rel(u[0], c)) > HUECO / 2 + 0.01
        banda[MITAD - 1, espina] = t_espina

        # 2. los copos
        for c, r in copos:
            dx = self._rel(u, c) + 0.0 * dy
            if f == 0:
                m = _brazos(dx, dy, 2.2)                     # la semilla
            elif f == 1:
                m = _brazos(dx, dy, r, desde=DESPEGUE)
            else:
                m = _brazos(dx, dy, r, desde=r / 2, empuje=1.0)
            m &= vivo[None, :] & (py >= MITAD - TRAZO)
            banda[m] = t_brazo
            if f == 1:
                cen = (np.abs(dx) < 1.0) & (py >= MITAD - 1)
                banda[cen & vivo[None, :]] = t_centro

        # 3. la estrella del contacto: el objetivo queda sellado. Casi
        #    siempre la tapa el nodo; es lo que se ve si el objetivo es chico
        if nombre == 'punta':
            dx = (u - (CONTACTO + 0.5)) + 0.0 * dy
            radio = (2.0, 4.0, 4.0)[f]
            m = _brazos(dx, dy, radio, desde=(0, 0, 2.0)[f], empuje=(0, 0, 1.0)[f])
            m |= _brazos(-dx, dy, radio, desde=(0, 0, 2.0)[f], empuje=(0, 0, 1.0)[f])
            m &= py >= MITAD - TRAZO
            banda[m] = t_brazo
            if f == 1:
                banda[(np.abs(dx) < 1.0) & (py >= MITAD - 1)] = t_centro

        if nombre == 'tramo' and ((banda >= 0) & (py < MITAD - TRAZO)).any():
            raise SystemExit('un copo sale del trazo de 8 px')

        # 4. el polvo de diamante: destellos sueltos fuera del trazo, que
        #    viven dos frames cada uno y derivan un pixel en la quiebra
        polvo = np.zeros((MITAD, ancho), bool)
        if nombre == 'tramo' and f > 0:
            for ud, alto_d, nace in self.destellos:
                if (f - max(nace, 1)) % FRAMES >= 2 or f < max(nace, 1):
                    continue
                fila = MITAD - TRAZO - 2 - alto_d
                col = int(ud + (1 if f == 2 else 0)) % TESELA
                if banda[fila, col] < 0:
                    banda[fila, col] = (3, 2, 4)[f]
                    polvo[fila, col] = True

        # 5. el vaho: solo de la espina, ralo. Los copos quedan nitidos sobre
        #    oscuro: si tambien echaran vaho, se rellenaria entre sus brazos
        solido = (banda >= 0) & ~polvo & (py >= MITAD - 1)
        dens = np.zeros((MITAD, ancho))
        ys, xs = np.nonzero(solido)
        if len(ys):
            pyy, pxx = np.mgrid[0:MITAD, 0:ancho] + 0.5
            d = np.full((MITAD, ancho), 99.0)
            for yy, xx in zip(ys, xs):
                d = np.minimum(d, np.hypot(pxx - (xx + 0.5), pyy - (yy + 0.5)))
            peso, caida = VAHO[f]
            dens = peso * np.exp(-(d - 1.0) / caida)
        if nombre == 'nacimiento':
            # el frio que sale del Pulsar: vaho en el origen, que el tapa
            pyy, pxx = np.mgrid[0:MITAD, 0:ancho] + 0.5
            r = np.hypot(pxx - 1.0, pyy - EJE)
            dens = np.maximum(dens, (0.7, 0.9, 0.4)[f] * np.exp(-(r / 4.5) ** 2))
        dens[banda >= 0] = 0
        return banda, dens, polvo

    def pieza(self, nombre, f):
        """(rgb, alfa, filas) de una pieza en el frame f. Se dibuja la mitad
        de arriba y la de abajo es su espejo: simetrica al pixel."""
        banda, dens, _ = self._mitad(nombre, f)
        ancho = banda.shape[1]
        umbral = np.tile(BAYER8, (1, ancho // 8 + 1))[:, :ancho]
        dens = np.clip((dens - 0.1) / 0.9, 0, 1)
        trama = (dens > umbral) & (banda < 0)
        idx = np.where(dens > 0.5, 4, 5).astype(np.int8)
        mitad_b = np.where(banda >= 0, banda, np.where(trama, idx, -1))
        completa = np.vstack([mitad_b, mitad_b[::-1]])
        alfa = completa >= 0
        rgb = np.zeros((ALTO, ancho, 3), np.uint8)
        rgb[alfa] = self.ramp[completa[alfa]]
        return rgb, alfa, np.full(ancho, MITAD - 1)

    def hoja(self):
        img = np.zeros((ALTO * 3, TESELA * FRAMES, 4), np.uint8)
        frames = {}
        for fila, (nombre, ancho) in enumerate(self.piezas):
            for f in range(FRAMES):
                rgb, alfa, filas = self.pieza(nombre, f)
                frames[nombre, f] = (rgb, alfa, filas)
                y0, x0 = fila * ALTO, f * TESELA
                img[y0:y0 + ALTO, x0:x0 + ancho, :3] = np.where(alfa[..., None], rgb, 0)
                img[y0:y0 + ALTO, x0:x0 + ancho, 3] = np.where(alfa, 255, 0)
        return img, frames

    def linea(self, frames, largo, f):
        # el mismo montaje que la linea de envio: nacimiento en 0, punta con
        # el contacto en `largo`, tramo anclado en la punta
        return Filamento.linea(self, frames, largo, f)


# --- Comprobaciones ----------------------------------------------------------

def comprobar(haz, img, frames):
    """Lo que tiene que cumplir el haz. Falla en vez de avisar."""
    fallos = []
    if not set(np.unique(img[..., 3])) <= {0, 255}:
        fallos.append('alfa no binario')
    rgb = img[..., :3][img[..., 3] > 0]
    usados = {tuple(c) for c in np.unique(rgb, axis=0)}
    if not usados <= {tuple(c) for c in haz.ramp[1:7]}:
        fallos.append('colores fuera de hielo[1..6]')
    if tuple(haz.ramp[0]) in usados:
        fallos.append('usa el blanco puro, que es de FX-CAP')
    for (nombre, f), (rgb_p, alfa, _) in frames.items():
        if not np.array_equal(alfa, alfa[::-1]) or not np.array_equal(rgb_p, rgb_p[::-1]):
            fallos.append(f'{nombre} {f}: no es simetrico respecto al eje')
    for f in range(FRAMES):
        # costura: la tesela pegada a si misma no corta ningun copo. Se
        # compara la tesela con la misma dibujada desplazada media vuelta
        a = frames['tramo', f][1]
        b = np.roll(a, TESELA // 2, axis=1)
        if not np.array_equal(np.roll(b, -TESELA // 2, axis=1), a):
            fallos.append(f'frame {f}: costura tramo-tramo')
        # la costura tramo-punta: la punta sigue la tesela desde u = 0
        t, p = frames['tramo', f][1], frames['punta', f][1]
        if f < 2 and not (t[7, -1] and p[7, 0]):
            fallos.append(f'frame {f}: la espina no pasa del tramo a la punta')
        if f < 2 and not frames['tramo', f][1][7].all():
            fallos.append(f'frame {f}: la espina tiene huecos')
    if frames['tramo', 2][1][7].all():
        fallos.append('la quiebra no parte la espina')
    luz = [sum(int(frames[n, f][1].sum()) for n, _ in haz.piezas) for f in range(FRAMES)]
    if not (luz[1] > luz[0] and luz[1] > luz[2]):
        fallos.append(f'la helada no culmina en el frame 1: {luz}')
    for f in range(FRAMES - 1):
        if np.array_equal(frames['tramo', f][1], frames['tramo', f + 1][1]):
            fallos.append(f'frames {f} y {f + 1} iguales')
    if fallos:
        raise SystemExit('ART-WLD-BEAM-ZER v1 no pasa:\n  ' + '\n  '.join(fallos))
    return luz


def costura_real(haz, frames):
    """La prueba de verdad de la costura: copos dibujados en una tira de dos
    teselas sin envolver contra la tesela repetida."""
    for f in range(FRAMES):
        a = frames['tramo', f][1]
        for c, r in haz.copos:
            if c + r > TESELA - 1 or c - r < 0:
                # cruza el borde: tiene que aparecer partido a los dos lados
                izq = a[:, :int(r) + 2].any()
                der = a[:, -int(r) - 2:].any()
                if not (izq and der):
                    raise SystemExit(f'frame {f}: el copo en {c:.1f} no envuelve la costura')


# --- Vistas ------------------------------------------------------------------

BG = np.array(hex_a_rgb(FONDO), np.uint8)
FPS = 12


def ampliar(im, k):
    return np.repeat(np.repeat(im, k, axis=0), k, axis=1)


def abrir(*ruta):
    return np.asarray(Image.open(os.path.join(RAIZ, 'art', *ruta)).convert('RGBA'))


def sobre(lienzo, rgba, cx, cy):
    h, w = rgba.shape[:2]
    x0, y0 = int(cx) - w // 2, int(cy) - h // 2
    x1, y1 = max(x0, 0), max(y0, 0)
    x2, y2 = min(x0 + w, lienzo.shape[1]), min(y0 + h, lienzo.shape[0])
    sub = rgba[y1 - y0:y2 - y0, x1 - x0:x2 - x0]
    m = sub[..., 3] > 0
    lienzo[y1:y2, x1:x2][m] = sub[..., :3][m]


def tira(trazo, ancho_total):
    bg = np.empty((ALTO + 4, ancho_total, 3), np.uint8)
    bg[:] = BG
    m = trazo[..., 3] > 0
    bg[2:2 + ALTO, 4:4 + trazo.shape[1]][m] = trazo[..., :3][m]
    return bg


def tira_frames(haz, frames, link):
    """Los tres frames a 200 px y, debajo, la linea de envio v2 para
    comparar: mismo cian de familia, otra forma. x4."""
    largo = 200
    ancho = largo + PUNTA - CONTACTO + 8
    filas = [tira(haz.linea(frames, largo, f), ancho) for f in range(FRAMES)]
    filas.append(np.tile(BG, (6, ancho, 1)))
    fil_l, frames_l = link
    filas.append(tira(fil_l.linea(frames_l, largo, 0), ancho))
    return ampliar(np.vstack(filas), 4)


# El tablero: un Pulsar del Jugador lanza Cero Absoluto a tres objetivos a
# distancia corta, media y larga. Al lado, una linea de envio del Jugador,
# que es con lo que no se puede confundir. Los nodos van encima del haz, como
# encima de la linea de envio (capa 0 de 14.7).
PULSAR = ((150, 420), ('pulsar', 'v2', 'pul_lvl2_player.png'))
OBJETIVOS = (
    ((330, 300), ('magnetar', 'v1', 'mag_lvl3_neutral.png')),
    ((560, 140), ('estrella', 'v1', 'str_lvl3_enemy.png')),
    ((860, 110), ('pulsar', 'v2', 'pul_lvl3_enemy.png')),
)
LINK_SEND = ((640, 440), (450, 330))


def fondo():
    try:
        return abrir('bg', 'fondos', 'v2', 'ref', 'bg_section1_ref0.png')[..., :3].copy()
    except FileNotFoundError:
        t = np.empty((540, 960, 3), np.uint8)
        t[:] = BG
        return t


def escena(haz, frames, k, link=None, todos=False):
    """El tablero en el frame de juego k. Sin `todos`, cada objetivo recibe
    su haz por turno, uno cada 8 frames; con `todos`, los tres en el frame k."""
    tab = fondo()
    (px, py), _ = PULSAR
    if link is not None:
        fil_l, frames_l = link
        (ax, ay), (bx, by) = LINK_SEND
        t = fil_l.linea(frames_l, math.hypot(bx - ax, by - ay), k % fil_l.frames)
        rotar_tira(tab, t, ax, ay, math.atan2(by - ay, bx - ax), fil_l.eje)
    for i, ((qx, qy), _) in enumerate(OBJETIVOS):
        f = k if todos else (k - 8 * i) % 24
        if f >= FRAMES:
            continue
        t = haz.linea(frames, math.hypot(qx - px, qy - py), f)
        rotar_tira(tab, t, px, py, math.atan2(qy - py, qx - px), haz.eje)
    sobre(tab, abrir('nodes', *PULSAR[1]), *PULSAR[0])
    for (qx, qy), ruta in OBJETIVOS:
        sobre(tab, abrir('nodes', *ruta), qx, qy)
    if link is not None:
        sobre(tab, abrir('nodes', 'estrella', 'v1', 'str_lvl2_player.png'), *LINK_SEND[0])
    return tab


def vistas(haz, frames, link, gif):
    d = os.path.join(dir_preview(ASSET), f'v{VERSION}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(tira_frames(haz, frames, link)).save(os.path.join(d, 'beam_zero_frames_x4.png'))
    for f in range(FRAMES):
        tab = escena(haz, frames, f, link, todos=True)
        Image.fromarray(tab).save(os.path.join(d, f'beam_zero_tablero_f{f}.png'))
        Image.fromarray(ampliar(tab[60:480, 100:620], 2)).save(
            os.path.join(d, f'beam_zero_tablero_f{f}_x2.png'))
    if gif:
        esc = [Image.fromarray(escena(haz, frames, k, link)) for k in range(24)]
        esc[0].save(os.path.join(d, 'beam_zero_tablero.gif'), save_all=True,
                    append_images=esc[1:], duration=round(1000 / FPS), loop=0)
        lento = [Image.fromarray(ampliar(escena(haz, frames, f, link, todos=True)[60:480, 100:620], 2))
                 for f in range(FRAMES)]
        lento[0].save(os.path.join(d, 'beam_zero_lento_x2.gif'), save_all=True,
                      append_images=lento[1:], duration=500, loop=0)
    print('vistas en', d)


def main():
    destino = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    os.makedirs(destino, exist_ok=True)
    haz = Rosario(rampa(RAMPA))
    img, frames = haz.hoja()
    luz = comprobar(haz, img, frames)
    costura_real(haz, frames)
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'world_beam_zero_sheet.png'))
    print(f'hoja {img.shape[1]}x{img.shape[0]}  luz por frame {luz}  copos {len(haz.copos)}')

    import gen_link_v1 as link
    from gen_link_v2 import PLASMA_P, aplicar
    aplicar(PLASMA_P)
    fil_l = link.filamento()
    frames_l = {(n, f): fil_l.pieza(n, f) for n, _ in fil_l.piezas for f in range(fil_l.frames)}
    vistas(haz, frames, (fil_l, frames_l), '--gif' in sys.argv)


if __name__ == '__main__':
    main()
