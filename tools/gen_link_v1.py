"""NOVA - ART-WLD-LINK v1, la linea de envio: «El Hilo Vivo».

El filamento de plasma de 14.9 y A.4.6 (NOVA_Estados_Animaciones.md,
NOVA_Assets_Diseno.md) que une el nodo origen con el cursor durante el
arrastre derecho. Una hoja, `art/world/link/v1/world_link_sheet.png`, con las tres
piezas en filas y los 16 frames del bucle en columnas:

    fila 0  nacimiento   16 x 16   en el centro del nodo, debajo de el (14.7)
    fila 1  tramo       256 x 16   se repite en mosaico, no se estira
    fila 2  punta        32 x 16   el punto de contacto, en x = 24: el cursor

Todas las celdas miden 256 x 16; el nacimiento y la punta ocupan su lado
izquierdo y el resto es transparente. Asi el frame f de las tres piezas cae
en la misma columna de la hoja.

LA TESIS: la linea dice hacia donde va la Energia sin una flecha. Tres capas
de movimiento, las tres periodicas en la longitud del tramo y en los 16
frames, asi que las costuras y el bucle cierran por construccion:

  1. EL HILO. Nucleo de 1 px que serpentea dentro de +-4 px del eje: una suma
     de cuatro armonicos que viajan hacia la punta, cada uno a su velocidad.
     Con una sola velocidad seria una onda rigida que se desliza; con cuatro,
     el hilo se retuerce mientras avanza, que es lo que hace un filamento.
  2. LOS PAQUETES. Dos cuentas de luz por tramo que corren por el hilo hacia
     la punta, a la velocidad del armonico principal. Cuando una llega al
     punto de contacto, este destella: la Energia «llega» al cursor una vez
     por bucle. Es el gesto que convierte una linea en un envio.
  3. LA ESTELA. El hilo del frame anterior queda como un eco tenue en trama,
     la persistencia de una descarga real. Da cuerpo al trazo sin engordar el
     nucleo y suaviza el salto de 12 fps.

Y las ramitas: cortas, inclinadas hacia la punta, viven dos frames —el
segundo, mas tenues y arrastradas por el flujo— y nunca repiten sitio en
frames seguidos. Alternan de lado para que el trazo sea simetrico en promedio
y ningun angulo de rotacion revele un lado mas cargado.

Rampa del Jugador (`str_p`): nucleo en el indice 1, cuentas y punto de
contacto en el 0, resplandor por trama Bayer en el 3, 4 y 5. Todo en la capa
que no genera contorno (regla 7 y «Lo que se dibuja encima de otro dibujo no
lleva contorno», NOVA_Arte_Tecnica.md). Alfa binario.

UNION EN UNITY. El tramo se ancla en la punta: la tesela entera toca la
punta y la que se recorta es la del lado del nodo, junto al nacimiento, que
cae debajo del sprite del nodo (capa 0 de 14.7). Asi la unica costura con
salto queda tapada, y el dibujo del hilo viaja pegado al cursor.

    py tools/gen_link_v1.py            hoja + vistas fijas
    py tools/gen_link_v1.py --gif      ademas, las vistas en movimiento
"""

import math
import os
import sys

import numpy as np
from PIL import Image

from nova_core import RAIZ, Filamento, dir_preview, rampa, rotar_tira

ASSET   = 'link'
VERSION = 1

ALTO     = 16
EJE      = 8.0          # el eje cae en el borde entre las filas 7 y 8
BANDA    = 4            # el hilo vive en +-4 px del eje (A.4.6)
TESELA   = 256          # 4 u a PPU 64; multiplo de 8: la trama no salta
NAC      = 16
PUNTA    = 32
CONTACTO = 24           # x del punto de contacto dentro de la punta
FRAMES   = 16
CELDA    = TESELA

RAMP = rampa('str_p')

# El resplandor: cuanto cae con la distancia al hilo (px) y en que banda de
# la rampa cae cada tramo de densidad, de denso a tenue. Las versiones que
# solo cambian el color los sustituyen.
CAIDA = 1.5
TRAMA = ((0.62, 3), (0.32, 4), (0.0, 5))
FONDO = np.array([0x0B, 0x0E, 0x1A], np.uint8)

# (armonico n en la tesela, amplitud, ciclos m en el bucle, fase). Viaja hacia
# +x cuando n y m tienen el mismo signo; velocidad = m * TESELA / (n * FRAMES)
# px por frame: 4, 8, 6.4 y 5.3. Que no coincidan es lo que lo retuerce.
ARMONICOS = (
    (1,  0.55, 1, 0.3),
    (4,  1.90, 2, 1.1),
    (10, 1.15, 4, 2.4),
    (18, 0.55, 6, 0.7),
)
RESPIRA = 0.12          # la amplitud respira un 12 % a lo largo del bucle

# Los paquetes: cada SEPARACION px, a VELOCIDAD px por frame. Recorren su
# separacion exacta en el bucle, asi que el frame 16 es el 0.
SEPARACION = 128
VELOCIDAD  = SEPARACION // FRAMES
FASE_PAQ   = 0

# Ramitas: cuantas nacen por frame en un tramo y la distancia minima a las
# del frame anterior, que es lo que impide que repitan sitio.
RAMITAS      = 3
DIST_RAMITAS = 28
SEMILLA      = 51


# El dibujo vive en `nova_core.Filamento` desde el 2026-10-27 (T.7 #50): estos
# son sus valores por defecto, escritos aqui para que el generador diga que
# dibuja. Se construye al generar, con la rampa, la caida y la trama que haya
# en ese momento, porque las versiones de color las sustituyen.

def filamento():
    return Filamento(RAMP, alto=ALTO, eje=EJE, banda=BANDA, tesela=TESELA,
                     nac=NAC, punta=PUNTA, contacto=CONTACTO, frames=FRAMES,
                     armonicos=ARMONICOS, respira=RESPIRA,
                     separacion=SEPARACION, fase_paq=FASE_PAQ,
                     ramitas=RAMITAS, dist_ramitas=DIST_RAMITAS,
                     semilla=SEMILLA, caida=CAIDA, trama=TRAMA)


# --- Hoja y comprobaciones ---------------------------------------------------

PIEZAS = (('nacimiento', NAC), ('tramo', TESELA), ('punta', PUNTA))


def comprobar(fil, img, frames):
    """Lo que tiene que cumplir el trazo. Falla en vez de avisar."""
    fallos = []
    rgb = img[..., :3][img[..., 3] > 0]
    if not set(np.unique(img[..., 3])) <= {0, 255}:
        fallos.append('alfa no binario')
    usados = {tuple(c) for c in np.unique(rgb, axis=0)}
    if not usados <= {tuple(c) for c in fil.ramp[:6]}:
        fallos.append('colores fuera de str_p[0..5]')
    for n in ('nacimiento', 'tramo', 'punta'):
        if dict(PIEZAS)[n] % 8:
            fallos.append(f'{n}: el ancho no es multiplo de 8 y la trama saltaria')

    for f in range(FRAMES):
        filas_t = frames['tramo', f][2]
        if filas_t.min() < EJE - BANDA or filas_t.max() >= EJE + BANDA:
            fallos.append(f'frame {f}: el hilo sale de +-{BANDA} px')
        if abs(int(filas_t[-1]) - int(filas_t[0])) > 1:
            fallos.append(f'frame {f}: costura tramo-tramo de {abs(filas_t[-1] - filas_t[0])} px')
        filas_p = frames['punta', f][2]
        if abs(int(filas_t[-1]) - int(filas_p[0])) > 1:
            fallos.append(f'frame {f}: costura tramo-punta')
        filas_n = frames['nacimiento', f][2]
        if abs(int(filas_n[-1]) - int(filas_t[0])) > 1:
            fallos.append(f'frame {f}: costura nacimiento-tramo sin recorte')
        a = frames['tramo', f][1]
        a2 = frames['tramo', (f + 1) % FRAMES][1]
        if np.array_equal(a, a2):
            fallos.append(f'frames {f} y {f + 1} iguales')

    distintos = {n: len({frames[n, f][0].tobytes() + frames[n, f][1].tobytes()
                         for f in range(FRAMES)}) for n, _ in PIEZAS}
    if distintos['tramo'] < FRAMES:
        fallos.append(f'el tramo solo tiene {distintos["tramo"]} frames distintos')

    # simetria en promedio: lo encendido arriba y abajo del eje
    arriba = sum(frames['tramo', f][1][:8].sum() for f in range(FRAMES))
    abajo = sum(frames['tramo', f][1][8:].sum() for f in range(FRAMES))
    desequilibrio = abs(arriba - abajo) / (arriba + abajo)
    if desequilibrio > 0.06:
        fallos.append(f'asimetrico: {desequilibrio:.1%} mas a un lado')

    for f in range(FRAMES):
        nuevos = [s for s, _ in fil.sitios[f]]
        antes = [s for s, _ in fil.sitios[f - 1]]
        if any(min(abs(a - b), TESELA - abs(a - b)) < DIST_RAMITAS
               for a in nuevos for b in antes):
            fallos.append(f'frame {f}: ramita en el sitio de una del frame anterior')

    if fallos:
        raise SystemExit('ART-WLD-LINK v1 no pasa:\n  ' + '\n  '.join(fallos))
    return distintos, desequilibrio


# --- Composicion: la linea entera, como la montara Unity ---------------------

def sprite(ruta):
    im = np.asarray(Image.open(ruta).convert('RGBA'))
    return im


def pegar(lienzo, im, cx, cy):
    h, w = im.shape[:2]
    x0, y0 = int(cx - w // 2), int(cy - h // 2)
    m = im[..., 3] > 0
    lienzo[y0:y0 + h, x0:x0 + w][m] = im[..., :3][m]


def escena(fil, frames, f):
    """El tablero del nivel 3 con tres gestos a la vez: un arrastre largo
    desde una Estrella, una seleccion de tres nodos hacia el mismo cursor
    —cada una desfasada por su orden, 14.9— y un arrastre corto."""
    fondo = np.asarray(Image.open(os.path.join(
        RAIZ, 'art', 'bg', 'fondos', 'v2', 'bg_section1.png')).convert('RGB')).copy()
    nodos = os.path.join(RAIZ, 'art', 'nodes')
    s3 = sprite(os.path.join(nodos, 'estrella', 'v1', 'str_lvl3_player.png'))
    s2 = sprite(os.path.join(nodos, 'estrella', 'v1', 'str_lvl2_player.png'))
    p2 = sprite(os.path.join(nodos, 'pulsar', 'v2', 'pul_lvl2_player.png'))
    e3 = sprite(os.path.join(nodos, 'estrella', 'v1', 'str_lvl3_enemy.png'))
    a1 = sprite(os.path.join(nodos, 'asteroide', 'v1', 'ast_lvl0_neutral.png'))

    gestos = [
        ((170, 330), (560, 190), 0),     # arrastre largo hacia el Asteroide
        ((120, 140), (430, 420), 0),     # seleccion de tres...
        ((250, 470), (430, 420), 5),
        ((330, 300), (430, 420), 10),
        ((760, 420), (840, 360), 3),     # arrastre corto
    ]
    for (ox, oy), (cx, cy), desfase in gestos:
        largo = math.hypot(cx - ox, cy - oy)
        tira = fil.linea(frames, largo, (f + desfase) % FRAMES)
        rotar_tira(fondo, tira, ox, oy, math.atan2(cy - oy, cx - ox), EJE)
    for im, (x, y) in ((s3, (170, 330)), (s2, (120, 140)), (p2, (250, 470)),
                       (s2, (330, 300)), (s3, (760, 420)), (e3, (820, 120)),
                       (a1, (560, 190))):
        pegar(fondo, im, x, y)
    return fondo


def ampliar(im, k):
    return np.repeat(np.repeat(im, k, axis=0), k, axis=1)


def vistas(fil, frames, gif, d):
    # la linea sola, x4, los 16 frames uno debajo de otro, sobre el fondo real
    filas = []
    for f in range(FRAMES):
        t = fil.linea(frames, 360, f)
        bg = np.empty((ALTO + 4, t.shape[1] + 8, 3), np.uint8)
        bg[:] = FONDO
        m = t[..., 3] > 0
        bg[2:2 + ALTO, 4:4 + t.shape[1]][m] = t[..., :3][m]
        filas.append(bg)
    Image.fromarray(ampliar(np.vstack(filas), 3)).save(os.path.join(d, 'link_frames_x3.png'))

    Image.fromarray(escena(fil, frames, 0)).save(os.path.join(d, 'link_tablero.png'))
    Image.fromarray(ampliar(escena(fil, frames, 0)[100:500, 60:620], 2)).save(
        os.path.join(d, 'link_tablero_x2.png'))
    if not gif:
        return
    duracion = round(1000 / 12)
    esc = [Image.fromarray(escena(fil, frames, f)) for f in range(FRAMES)]
    esc[0].save(os.path.join(d, 'link_tablero.gif'), save_all=True,
                append_images=esc[1:], duration=duracion, loop=0)
    zoom = [Image.fromarray(ampliar(np.asarray(e)[100:500, 60:620], 2)) for e in esc]
    zoom[0].save(os.path.join(d, 'link_tablero_x2.gif'), save_all=True,
                 append_images=zoom[1:], duration=duracion, loop=0)
    sola = []
    for f in range(FRAMES):
        t = fil.linea(frames, 360, f)
        bg = np.empty((ALTO + 16, t.shape[1] + 16, 3), np.uint8)
        bg[:] = FONDO
        m = t[..., 3] > 0
        bg[8:8 + ALTO, 8:8 + t.shape[1]][m] = t[..., :3][m]
        sola.append(Image.fromarray(ampliar(bg, 4)))
    sola[0].save(os.path.join(d, 'link_x4.gif'), save_all=True,
                 append_images=sola[1:], duration=duracion, loop=0)


def generar(version):
    """Hoja en art/world/link/v<n>/ y vistas en art/_preview/link/v<n>/, con
    la rampa que tenga RAMP en ese momento: las versiones que solo cambian el
    color la sustituyen y llaman aqui."""
    fil = filamento()
    img, frames = fil.hoja()
    distintos, deseq = comprobar(fil, img, frames)
    destino = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'world_link_sheet.png'))
    d = os.path.join(dir_preview(ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    vistas(fil, frames, '--gif' in sys.argv, d)
    print(f'v{version} world_link_sheet.png {img.shape[1]}x{img.shape[0]}  '
          f'frames distintos: nacimiento {distintos["nacimiento"]}, '
          f'tramo {distintos["tramo"]}, punta {distintos["punta"]}  '
          f'simetria {deseq:.1%}  llegada en el frame {fil.llegada}  escala {fil.escala:.3f}')


if __name__ == '__main__':
    generar(VERSION)
