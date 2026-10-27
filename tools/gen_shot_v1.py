"""NOVA - ART-WLD-RAD-SHOT v1, el arco de disparo del Magnetar: «El chispazo».

Ref: NOVA_Assets_Diseno.md A.4.1 y A.4.6 · NOVA_Estados_Animaciones.md 14.9

El filamento de plasma de 14.9 del centro del Magnetar al proton al que
dispara, en la rampa del Magnetar que dispara. Tres hojas, una por bando,
`art/world/shot/v1/world_radius_shot_<faccion>_sheet.png`, con la misma
forma que la de la linea de envio: las tres piezas en filas y los 2 frames
en columnas, celdas de 256 x 16.

    fila 0  nacimiento   16 x 16   en el centro del Magnetar, debajo de el
    fila 1  tramo       256 x 16   anclado en la punta; mas largo que
                                   cualquier arco, asi que nunca se repite
    fila 2  punta        32 x 16   el contacto en x = 24: el centro del proton

LA TESIS: un chispazo seco, no un hilo vivo. La linea de envio dura lo que
dure el gesto y se puede mirar; el arco dura 0.17 s y a cadencia N5 sale
4.27 veces por segundo, asi que un Magnetar que dispara sin pausa enseña un
arco el 70 % del tiempo. Con tres o cuatro Magnetares en pantalla, un arco
tan cargado como la linea de envio llenaria el tablero de luz. Asi que se
quita todo lo que en la linea es duracion:

  - SIN PAQUETES. Las cuentas que corren hacia la punta dicen «la Energia
    va llegando»; un disparo no llega, ya ha llegado.
  - MAS FINO. El hilo vive en +-3 px y no en +-4, y el resplandor cae el
    doble de rapido: en la mitad de la vaina de la linea de envio.
  - DOS FRAMES QUE NO VUELVEN. El 0 es el golpe: el hilo en la banda clara,
    dos ramitas y el contacto destellando. El 1 es el apagado: el hilo baja
    una banda, las ramitas quedan como un codo arrastrado, el eco del
    frame 0 se queda en trama y el contacto se encoge. Sin bucle no hay
    nada que cerrar, y la onda solo avanza un octavo de su ciclo: lo
    bastante para que se retuerza hacia la punta, no para que viaje.

LA LINEA DISCRETA DEL ANILLO. Como «El susurro» (v2 de A.4.1, elegida): nada
del arco llega al blanco del indice 0, los ajenos van en la mitad media de
`mag_n`/`mag_e` y el propio una banda mas claro, en `mag_p`. El punto mas
brillante es el contacto del frame 0, y ese queda debajo del proton y de su
destello (`ART-FX-SHOT`, A.5), que es quien de verdad dice «le han dado».

    py tools/gen_shot_v1.py            las tres hojas + vistas fijas
    py tools/gen_shot_v1.py --gif      ademas, el tablero en movimiento
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, Filamento, dir_preview, rampa, rotar_tira
from nova_paleta import FONDO, hex_a_rgb

ASSET   = 'shot'
VERSION = 1

FRAMES  = 2
PERIODO = 8             # el frame 1 es la onda un octavo de ciclo despues
BANDA   = 3             # +-3 px; la linea de envio, +-4

# Menos armonicos y de onda mas corta que la linea: el arco mide como mucho
# 2.5 u (160 px) mas lo que se mueve el proton en 0.17 s, y el fundamental de
# la tesela no llegaria a verse. Velocidades m * 256 / (n * 8): 6.4, 5.8 y
# 6.7 px por frame, distintas para que se retuerza en vez de deslizarse.
ARMONICOS = (
    (5,  1.60, 1, 1.3),
    (11, 1.00, 2, 2.2),
    (19, 0.45, 4, 0.9),
)

CAIDA  = 0.9            # la vaina, a la mitad de la de la linea de envio
BRILLO = (0.8, 0.4)     # el resplandor propio, golpe y apagado
ESTELA = 0.3            # el eco del golpe en el frame 1
TRAMA  = ((0.75, 4), (0.0, 5))

# Ramitas: dos en el golpe, ninguna nueva al apagarse (las del 0 se arrastran)
RAMITAS = (2, 0)
SEMILLA = 7

# Banda de cada capa solida (medio, claro, nucleo, blanco) por frame, para
# los ajenos. El propio va una banda mas claro, como su anillo.
BANDAS = (
    (3, 2, 2, 1),       # golpe
    (4, 3, 3, 2),       # apagado
)
TRAMAS = (
    ((0.75, 4), (0.0, 5)),
    ((0.0, 5),),
)

FACCIONES = {
    'neutral': dict(ramp='mag_n', sesgo=0),
    'enemy':   dict(ramp='mag_e', sesgo=0),
    'player':  dict(ramp='mag_p', sesgo=-1),
}

# Cadencias del Magnetar (4.4), en disparos por segundo
CADENCIA = {1: 1.33, 2: 1.87, 3: 2.53, 4: 3.33, 5: 4.27}


class Arco(Filamento):
    """El filamento de 14.9 con dos frames que se apagan en vez de un bucle."""

    def __init__(self, ramp, sesgo):
        self.sesgo = sesgo
        super().__init__(ramp, banda=BANDA, frames=FRAMES, periodo=PERIODO,
                         bucle=False, armonicos=ARMONICOS, respira=0.0,
                         separacion=None, ramitas=RAMITAS, semilla=SEMILLA,
                         caida=CAIDA, trama=TRAMA, brillo=BRILLO,
                         estela=ESTELA, deriva=3)

    def tonos(self, f):
        trama = tuple((c, b + self.sesgo) for c, b in TRAMAS[f])
        bandas = tuple(max(1, b + self.sesgo) for b in BANDAS[f])
        return trama, bandas

    def contacto(self, f, px, py):
        """El golpe: un punto de 2 x 2 con una cruz corta que se encoge al
        frame siguiente. Mas pequeño que el de la linea de envio, porque
        encima va el proton y su destello."""
        cx, cy = float(self.c_x), self.eje
        r = np.hypot(px - cx, py - cy)
        radio = (2.8, 1.8)[f]
        dens = (0.9, 0.55)[f] * np.exp(-(r / radio) ** 1.6)
        centro = (r < 1.0)
        anillo = (r < 2.0) & ~centro if f == 0 else np.zeros_like(centro)
        rayos = np.zeros_like(centro)
        if f == 0:
            dx, dy = np.abs(px - cx), np.abs(py - cy)
            rayos |= (dy < 1.0) & (dx < 5.0)
            rayos |= (dx < 1.0) & (dy < 4.0)
        rayos &= ~(anillo | centro)
        return dens, rayos, centro, anillo


# --- Comprobaciones ----------------------------------------------------------

def comprobar(fil, img, frames, fac):
    """Lo que tiene que cumplir el arco. Falla en vez de avisar."""
    fallos = []
    if not set(np.unique(img[..., 3])) <= {0, 255}:
        fallos.append('alfa no binario')
    rgb = img[..., :3][img[..., 3] > 0]
    usados = {tuple(c) for c in np.unique(rgb, axis=0)}
    if not usados <= {tuple(c) for c in fil.ramp[1:6]}:
        fallos.append('colores fuera de la rampa [1..5]: el arco no llega al blanco')

    for f in range(FRAMES):
        filas_t = frames['tramo', f][2]
        if filas_t.min() < fil.eje - BANDA or filas_t.max() >= fil.eje + BANDA:
            fallos.append(f'frame {f}: el hilo sale de +-{BANDA} px')
        if abs(int(filas_t[-1]) - int(filas_t[0])) > 1:
            fallos.append(f'frame {f}: costura tramo-tramo')
        if abs(int(filas_t[-1]) - int(frames['punta', f][2][0])) > 1:
            fallos.append(f'frame {f}: costura tramo-punta')
        if abs(int(frames['nacimiento', f][2][-1]) - int(filas_t[0])) > 1:
            fallos.append(f'frame {f}: costura nacimiento-tramo')

    # el frame 1 se apaga: menos luz y ninguna mas clara que en el golpe
    luz = [sum(frames[n, f][1].sum() for n, _ in fil.piezas) for f in range(FRAMES)]
    if luz[1] >= luz[0]:
        fallos.append(f'el frame 1 no se apaga: {luz[1]} px contra {luz[0]}')
    if np.array_equal(frames['tramo', 0][1], frames['tramo', 1][1]):
        fallos.append('los dos frames son iguales')

    if fallos:
        raise SystemExit(f'ART-WLD-RAD-SHOT v1 ({fac}) no pasa:\n  ' + '\n  '.join(fallos))
    return luz


def grosor(alfa):
    """Pixeles encendidos por columna del tramo, de media."""
    return alfa.sum() / alfa.shape[1]


# --- Vistas ------------------------------------------------------------------

BG = np.array(hex_a_rgb(FONDO), np.uint8)


def ampliar(im, k):
    return np.repeat(np.repeat(im, k, axis=0), k, axis=1)


def sobre(lienzo, rgba, cx, cy):
    h, w = rgba.shape[:2]
    x0, y0 = int(cx) - w // 2, int(cy) - h // 2
    x1, y1 = max(x0, 0), max(y0, 0)
    x2, y2 = min(x0 + w, lienzo.shape[1]), min(y0 + h, lienzo.shape[0])
    sub = rgba[y1 - y0:y2 - y0, x1 - x0:x2 - x0]
    m = sub[..., 3] > 0
    lienzo[y1:y2, x1:x2][m] = sub[..., :3][m]


def abrir(*ruta):
    return np.asarray(Image.open(os.path.join(RAIZ, 'art', *ruta)).convert('RGBA'))


def proton_provisional(fac):
    """El proton aun no tiene arte (ART-WLD-PRO-*, A.4.2): un disco de 10 px
    en la rampa de su bando, solo para ver donde cae la punta."""
    ramp = rampa('str_p' if fac == 'player' else 'str_e')
    im = np.zeros((12, 12, 4), np.uint8)
    y, x = np.mgrid[0:12, 0:12] + 0.5
    r = np.hypot(x - 6, y - 6)
    im[r < 5, :3] = ramp[3]
    im[r < 3, :3] = ramp[1]
    im[r < 5, 3] = 255
    return im


def tira_frames(arcos, link):
    """Las tres facciones en sus dos frames, a 140 px, y la linea de envio v2
    debajo para comparar el grosor. x4."""
    filas = []
    for fac, (fil, frames) in arcos.items():
        for f in range(FRAMES):
            t = fil.linea(frames, 140, f)
            bg = np.empty((fil.alto + 4, t.shape[1] + 8, 3), np.uint8)
            bg[:] = BG
            m = t[..., 3] > 0
            bg[2:2 + fil.alto, 4:4 + t.shape[1]][m] = t[..., :3][m]
            filas.append(bg)
        filas.append(np.tile(BG, (4, filas[-1].shape[1], 1)))
    if link is not None:
        fil_l, frames_l = link
        t = fil_l.linea(frames_l, 140, 0)
        bg = np.empty((fil_l.alto + 4, t.shape[1] + 8, 3), np.uint8)
        bg[:] = BG
        m = t[..., 3] > 0
        bg[2:2 + fil_l.alto, 4:4 + t.shape[1]][m] = t[..., :3][m]
        filas.append(bg)
    return ampliar(np.vstack(filas), 4)


# Un tablero de 960 x 540 como el de las vistas del anillo: los tres Magnetares
# disparando a protones que cruzan su radio por una cuerda, cada uno con su
# cadencia. Cuando un proton sale, entra el siguiente por el mismo sitio.
ESCENA = (
    # faccion, nivel, centro, angulos de entrada y salida de la cuerda, bando
    ('neutral', 3, (480, 270), (200, 330), 'player'),
    ('enemy',   5, (760, 160), (150, 260), 'player'),
    ('player',  2, (190, 360), (-60, 70), 'enemy'),
)
FPS = 12
PASO = 2.5 * 64 / FPS    # 2.5 u/s a PPU 64: 13.3 px por frame


def proton(n, centro, angulos, k):
    """Posicion del proton en el frame k: recorre la cuerda entre los dos
    angulos, a 0.9 del radio, y vuelve a entrar al salir."""
    from gen_radio_v2 import PPU, RADIO_U
    R = RADIO_U[n] * PPU * 0.9
    (x, y), (a0, a1) = centro, (math.radians(a) for a in angulos)
    ax, ay = x + R * math.cos(a0), y + R * math.sin(a0)
    bx, by = x + R * math.cos(a1), y + R * math.sin(a1)
    largo = math.hypot(bx - ax, by - ay)
    s = (k * PASO) % largo / largo
    return ax + (bx - ax) * s, ay + (by - ay) * s


def escena(arcos, k):
    """El tablero en el frame de juego k (a 12 fps): cada Magnetar dispara a
    su cadencia, y su arco dura 2 frames con la punta siguiendo al proton."""
    from gen_radio_v2 import anillo
    tab = np.empty((540, 960, 3), np.uint8)
    tab[:] = BG
    for fac, n, (x, y), *_ in ESCENA:
        sobre(tab, anillo(n, fac), x, y)
    protones = [(*proton(n, c, ang, k), bando) for _, n, c, ang, bando in ESCENA]
    for (fac, n, (x, y), *_), (qx, qy, _) in zip(ESCENA, protones):
        fil, frames = arcos[fac]
        f = int(k % (FPS / CADENCIA[n]))
        if f >= FRAMES:
            continue
        largo = math.hypot(qx - x, qy - y)
        tira = fil.linea(frames, largo, f)
        rotar_tira(tab, tira, x, y, math.atan2(qy - y, qx - x), fil.eje)
    for qx, qy, bando in protones:
        sobre(tab, proton_provisional(bando), qx, qy)
    for fac, n, (x, y), *_ in ESCENA:
        sobre(tab, abrir('nodes', 'magnetar', 'v1', f'mag_lvl{n}_{fac}.png'), x, y)
    return tab


def vistas(arcos, link, gif):
    d = os.path.join(dir_preview(ASSET), f'v{VERSION}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(tira_frames(arcos, link)).save(os.path.join(d, 'shot_frames_x4.png'))
    tab = escena(arcos, 0)
    Image.fromarray(tab).save(os.path.join(d, 'shot_tablero.png'))
    Image.fromarray(ampliar(tab, 2)).save(os.path.join(d, 'shot_tablero_x2.png'))
    if gif:
        esc = [Image.fromarray(escena(arcos, k)) for k in range(48)]
        esc[0].save(os.path.join(d, 'shot_tablero.gif'), save_all=True,
                    append_images=esc[1:], duration=round(1000 / FPS), loop=0)
        zoom = [Image.fromarray(ampliar(np.asarray(e)[60:460, 440:900], 2)) for e in esc]
        zoom[0].save(os.path.join(d, 'shot_tablero_x2.gif'), save_all=True,
                     append_images=zoom[1:], duration=round(1000 / FPS), loop=0)
    print('vistas en', d)


def main():
    destino = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    os.makedirs(destino, exist_ok=True)
    arcos = {}
    for fac, cfg in FACCIONES.items():
        fil = Arco(rampa(cfg['ramp']), cfg['sesgo'])
        img, frames = fil.hoja()
        luz = comprobar(fil, img, frames, fac)
        Image.fromarray(img, 'RGBA').save(
            os.path.join(destino, f'world_radius_shot_{fac}_sheet.png'))
        arcos[fac] = (fil, frames)
        print(f'{fac:>7} {img.shape[1]}x{img.shape[0]}  luz {luz[0]} -> {luz[1]} px  '
              f'grosor del tramo {grosor(frames["tramo", 0][1]):.2f} -> '
              f'{grosor(frames["tramo", 1][1]):.2f} px/col  escala {fil.escala:.3f}')

    # la linea de envio elegida (v2), para comparar el grosor
    import gen_link_v1 as link
    from gen_link_v2 import PLASMA_P, aplicar
    aplicar(PLASMA_P)
    fil_l = link.filamento()
    frames_l = {(n, f): fil_l.pieza(n, f) for n, _ in fil_l.piezas for f in (0,)}
    print(f'   link grosor del tramo {grosor(frames_l["tramo", 0][1]):.2f} px/col (v2, frame 0)')
    vistas(arcos, (fil_l, frames_l), '--gif' in sys.argv)


if __name__ == '__main__':
    main()
