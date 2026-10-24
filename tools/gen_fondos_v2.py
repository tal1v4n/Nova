"""NOVA - Fondos de seccion con movimiento. v2, la vigente.

Ref: NOVA_Assets_Diseno.md A.4.5 · NOVA_Tracking.md T.8, T5-05b
`ART-BG-01` Sistema Interior · `ART-BG-02` El Cinturon · `ART-BG-03` Espacio Profundo
`ART-BG-UFO` el ovni de los tres

960x540 px = 15 x 8.4375 u a PPU 64. La composicion es la de la v1 llevada
al lienzo nuevo: toda constante en pixeles va multiplicada por `K`.

LO QUE CAMBIA RESPECTO A LA V1: EL FONDO SE MUEVE. No se anima el dibujo,
se anima la TRAMA. La matriz de Bayer queda clavada a la rejilla y lo que
se desplaza es el campo de densidad que se compara contra ella, asi que las
nubes fluyen encendiendo y apagando pixeles sueltos: nada se mueve en
subpixel y la rejilla no se rompe. `fotograma(t)` es la referencia que el
material de Unity tiene que reproducir.

CADA MAPA SE MUEVE A SU MANERA. El Sistema Interior es luz que barre, el
Cinturon es materia que fluye y el Espacio Profundo es profundidad: el vacio
deriva en capas a velocidades distintas. Ninguno toma prestado el gesto de
otro.

SIN BUCLE. `t` es el tiempo de partida y no vuelve a cero. Cada capa es
periodica por su cuenta -el ruido cierra en horizontal, la respiracion es un
seno- y sus periodos no son multiplos entre si, asi que el conjunto no se
repite dentro de un nivel. En Unity cada capa reduce su propia fase antes
de pasarla al material, para no perder precision en partidas largas.

LAS REGLAS DE LA V1 SIGUEN, Y AHORA EN CADA FOTOGRAMA. Presupuesto de
contraste, solo rampas sin significado, nada en la franja de huella de un
nodo y vineta al reves. `informe_ciclo` mide el presupuesto sobre dos
minutos de fotogramas.

Y LAS DEL MOVIMIENTO son las siete de NOVA_Arte_Tecnica.md, "Fondos con
movimiento", con el ovni como unica excepcion a la de velocidad. No se copian
aqui para que no haya dos listas: lento (7 px/s como mucho), sin destellos,
sin gestos del juego, tiempo de juego, sin bucle, un gesto por mapa, y los
sprites del fondo a pixel entero y a 12 fps.

LO QUE SE EXPORTA (T5-05b). `--exporta` escribe, junto a los PNG del
inventario, todo lo que lee el material de Unity: los campos de densidad a 8
bits, la capa fija, las capas de puntos, la hoja de la grava, un JSON de
parametros por mapa y fotogramas de referencia en `ref/`. `fotograma(t)` se
calcula DESDE ESOS DATOS ya cuantizados y no desde los campos en coma
flotante, asi que la referencia y el material leen exactamente lo mismo: lo
unico que puede separarlos es la aritmetica de la GPU.
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, a_rgba, dir_preview, esferoide, rampa
from nova_core import lienzo as lienzo_celda
from nova_paleta import FONDO, hex_a_rgb
from nova_fondo import (ALTO, ANCHO, TOPE, banda, cobertura, disco, lienzo,
                        ruido, silueta, tramar, ventanas, vineta)

VERSION = 2
SEMILLAS = {1: 20260918, 2: 20260919, 3: 20260920}
K = ANCHO / 2048.0          # escala de las constantes en pixeles de la v1
FPS = 12                    # cadencia de los sprites (contrato, Animacion)
FPS_VISTA = 25              # la trama se mueve en continuo: la vista, fluida
_TESTIGO = np.array((1, 2, 3), np.uint8)   # color que no esta en la paleta

# Instantes de los fotogramas de referencia. El ultimo esta a casi una hora
# de partida: es el que comprueba que reducir la fase no pierde precision.
REFERENCIAS = (0.0, 61.7, 3599.3)


def _elipse(cy, cx, ra, rb, giro, gamma=2.0, h=ALTO, w=ANCHO):
    """Caida eliptica inclinada, en [0,1]. Para discos y galaxias."""
    y, x = np.mgrid[0:h, 0:w]
    co, si = math.cos(giro), math.sin(giro)
    u = ((x - cx) * co + (y - cy) * si) / max(ra, 1e-6)
    v = (-(x - cx) * si + (y - cy) * co) / max(rb, 1e-6)
    return np.clip(1.0 - np.sqrt(u * u + v * v), 0.0, 1.0) ** gamma


# --- Piezas del movimiento --------------------------------------------------

def desliza(campo, dx):
    """El campo desplazado `dx` px a la derecha, con fraccion.

    Es lo que hace el muestreo bilineal de una textura en Unity: la
    densidad se mueve en continuo y la trama decide que pixel cambia.
    """
    s = math.floor(dx)
    f = dx - s
    return (1 - f) * np.roll(campo, s, axis=1) + f * np.roll(campo, s + 1, axis=1)


def muestrea(campo, fy, fx):
    """Muestreo bilineal en coordenadas arbitrarias: `x` cierra, `y` se pega
    al borde. Es el `tex2D` del material, y lo que permite deformar un campo
    en vez de solo deslizarlo."""
    h, w = campo.shape
    fy = np.clip(fy, 0, h - 1.001)
    y0 = np.floor(fy).astype(int)
    x0 = np.floor(fx).astype(int)
    ty, tx = fy - y0, fx - x0
    x0 %= w
    x1 = (x0 + 1) % w
    a = campo[y0, x0] * (1 - tx) + campo[y0, x1] * tx
    b = campo[y0 + 1, x0] * (1 - tx) + campo[y0 + 1, x1] * tx
    return a * (1 - ty) + b * ty


def capa_fija(dibuja, h=ALTO, w=ANCHO):
    """Lo que `dibuja` pinta, como mascara y colores para ponerlo encima."""
    img = np.tile(_TESTIGO, (h, w, 1))
    dibuja(img)
    mascara = np.any(img != _TESTIGO, axis=2)
    return mascara, img


def poner(img, capa):
    mascara, colores = capa
    img[mascara] = colores[mascara]


def pegar(img, capa, y0, x0):
    """Un sprite con su esquina en (y0, x0), recortado contra el lienzo."""
    mascara, colores = capa
    h, w = mascara.shape
    sy0, sx0 = max(0, -y0), max(0, -x0)
    sy1, sx1 = min(h, ALTO - y0), min(w, ANCHO - x0)
    if sy1 <= sy0 or sx1 <= sx0:
        return
    m = mascara[sy0:sy1, sx0:sx1]
    destino = img[y0 + sy0:y0 + sy1, x0 + sx0:x0 + sx1]
    destino[m] = colores[sy0:sy1, sx0:sx1][m]


class Estrellas:
    """El campo de estrellas de la v1, guardado como datos y no pintado.

    Cada estrella lleva su escalon y un umbral propio `u`. Con eso cada
    seccion decide en cada fotograma cuales se ven y donde: el sol las lava,
    el cinturon las tapa al pasar, el vacio las arrastra. Solo las DEBILES
    pueden aparecer y desaparecer en su sitio (regla 2).
    """

    def __init__(self, rng, cuantas, ramp, brillo=1.0, h=ALTO, w=ANCHO):
        self.ys = rng.integers(2, h - 2, cuantas)
        self.xs = rng.integers(2, w - 2, cuantas)
        self.u = rng.random(cuantas)
        tier = rng.random(cuantas)
        self.debil = tier < 0.70
        self.media = (tier >= 0.70) & (tier < 0.93)
        self.fuerte = tier >= 0.93
        self.fase = rng.random(cuantas)
        self.periodo = 7.0 + 8.0 * rng.random(cuantas)
        self.ramp = ramp
        self.b_debil = int(np.clip(round(4 + (1 - brillo) * 1.4), 0, 6))
        self.b_media = int(np.clip(round(3 - brillo * 0.8), 0, 6))

    def pinta(self, img, ve, b_media=None, ys=None, xs=None):
        """`ve`: booleano por estrella. `b_media`: indice por estrella.
        `ys`, `xs`: posicion de este fotograma, si la estrella se mueve."""
        ys = self.ys if ys is None else ys
        xs = self.xs if xs is None else xs
        r = self.ramp
        d = self.debil & ve
        img[ys[d], xs[d]] = r[self.b_debil]
        m = self.media & ve
        bm = np.full(len(ys), self.b_media) if b_media is None else b_media
        img[ys[m], xs[m]] = r[bm[m]]
        h, w = img.shape[:2]
        for y, x in zip(ys[self.fuerte & ve], xs[self.fuerte & ve]):
            img[y, x] = r[0]
            for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                img[(y + dy) % h, (x + dx) % w] = r[3]


def deriva(y0, x0, vy, vx, t, h=ALTO, w=ANCHO):
    """Puntos que viajan y dan la vuelta al lienzo, en pixel entero."""
    return (np.floor(y0 + vy * t).astype(int) % h,
            np.floor(x0 + vx * t).astype(int) % w)


# --- Lo que lee el material -------------------------------------------------
#
# Un PNG lineal guarda bytes, y el material lee `b / 255`. Los campos de
# densidad se guardan asi, redondeados; los puntos guardan su FRACCION de
# partida, truncada, con el 0 reservado para "aqui no hay nada"; y lo fijo va
# a color con su CLASE en el alfa: BAJA se pinta por debajo de los puntos que
# se mueven, ALTA por encima. Es lo que deja meter en una sola textura capas
# que en `fotograma` van a ambos lados de las estrellas.

BAJA, ALTA = 128, 255


def q8(campo):
    """Un campo en [0,1] a 8 bits."""
    return np.clip(np.rint(np.asarray(campo, np.float64) * 255.0), 0, 255).astype(np.uint8)


def de8(b):
    """Lo que lee el material de un texel de 8 bits."""
    return b.astype(np.float64) / 255.0


def cod_frac(f):
    """Fraccion de partida de un punto, 0 <= f < 1, a un byte de 1 a 255."""
    return np.clip(1 + np.floor(np.asarray(f, np.float64) * 255.0), 1, 255).astype(np.uint8)


def frac_de(c):
    return (c.astype(np.float64) - 1.0) / 255.0


def cod_u(u):
    """Umbral propio de una estrella a un byte de 1 a 255."""
    return np.clip(1 + np.floor(np.asarray(u, np.float64) * 255.0), 1, 255).astype(np.uint8)


def u_de(c):
    return (c.astype(np.float64) - 0.5) / 255.0


def rgba(*canales):
    """Hasta cuatro campos de 8 bits en una textura; el alfa que sobra, a 255."""
    h, w = canales[0].shape
    out = np.zeros((h, w, 4), np.uint8)
    out[..., 3] = 255
    for i, c in enumerate(canales):
        out[..., i] = c
    return out


def estrellas_u(cielo, sel):
    """Las estrellas `sel` como textura de umbrales: R = el umbral de la
    estrella del pixel, 0 si no hay. Dos debiles en el mismo pixel pintan el
    mismo color, asi que basta con la que antes se enciende."""
    tmp = np.full((ALTO, ANCHO), 256, np.int32)
    np.minimum.at(tmp, (cielo.ys[sel], cielo.xs[sel]), cod_u(cielo.u[sel]).astype(np.int32))
    return np.where(tmp == 256, 0, tmp).astype(np.uint8)


def fija_de(*capas):
    """Lo fijo de un mapa en una RGBA, por orden de pintado: `(capa, clase)`."""
    out = np.zeros((ALTO, ANCHO, 4), np.uint8)
    for (mascara, colores), clase in capas:
        out[mascara, :3] = colores[mascara]
        out[mascara, 3] = clase
    return out


def poner_fija(img, fija, clase):
    m = fija[..., 3] == clase
    img[m] = fija[..., :3][m]


def hex_de(c):
    return '#%02X%02X%02X' % tuple(int(v) for v in c)


class Mapa:
    """Un fondo con movimiento tal como lo lee el material.

    `fondo(t)` es lo que dibuja el material; `fotograma(t)` le pone encima
    los sprites que mueve `BackgroundFlyby` (la grava; el ovni va aparte, en
    `ruta`). Lo demas es lo que `exporta` escribe en el JSON.
    """

    def __init__(self, n, nombre, ramp):
        self.n, self.nombre, self.ramp = n, nombre, ramp
        self.texturas = []
        self.floats, self.vectores, self.colores = [], [], []
        self.fases, self.listas = [], []
        self.eje = []
        self.rocas = None
        self.ovni = None
        self.fondo = self.fotograma = None
        self.color('_CFondo', hex_a_rgb(FONDO))
        for b in (3, 4, 5, 6):
            self.color(f'_C{b}', ramp[b])

    def textura(self, propiedad, sufijo, datos):
        self.texturas.append((propiedad, sufijo, datos))
        return datos

    def float_(self, propiedad, valor):
        self.floats.append(dict(property=propiedad, value=float(valor)))

    def vector(self, propiedad, valores):
        v = [float(x) for x in valores] + [0.0] * (4 - len(valores))
        self.vectores.append(dict(property=propiedad, value=v))

    def color(self, propiedad, c):
        self.colores.append(dict(property=propiedad, value=hex_de(c)))

    def fase(self, propiedad, ritmo, periodo, desfase=0.0):
        """Una capa que avanza `ritmo` por segundo y cierra cada `periodo`.
        Unity le pasa al material `(desfase + ritmo * t) mod periodo`."""
        self.fases.append(dict(property=propiedad, rate=float(ritmo),
                               period=float(periodo), offset=float(desfase)))

    def lista(self, propiedad, ritmos, periodo, desfases):
        """Lo mismo, un valor por fila: para lo que avanza distinto en cada
        fila de una banda. Periodo 0: una tabla fija."""
        self.listas.append(dict(property=propiedad, rates=[float(r) for r in ritmos],
                                period=float(periodo), offsets=[float(d) for d in desfases]))

    def trama(self, nombre, capas):
        """Las capas de una llamada a `tramar`, con la cobertura ya despejada
        del presupuesto. El material lleva las bandas fijas por posicion -6,
        5, 4 y 3, de la mas oscura a la mas clara- y lo comprueba aqui."""
        bandas = [b for b, _, _ in capas]
        assert bandas == [6, 5, 4, 3][:len(bandas)], bandas
        self.vector(f'_{nombre}Desde', [d for _, d, _ in capas])
        self.vector(f'_{nombre}Cob', [cobertura(self.ramp, b, p) for b, _, p in capas])
        return capas


# --- El ovni ----------------------------------------------------------------

OVNI_CELDA = 32
OVNI_FRAMES = 16
OVNI = dict(sheet='bg_ufo_sheet.png', cell=OVNI_CELDA, frames=OVNI_FRAMES, fps=FPS,
            speed=30.0, sway=3.0, swayPeriod=2.4, intervalMin=45.0, intervalMax=90.0)


def ovni(t):
    """Un platillo de 24 x 13 px, con el modelo de luz de los nodos.

    Se dibuja con `esferoide`, la misma primitiva y la misma luz global que
    la Estrella y el Pulsar, para que pertenezca al tablero y no parezca
    pegado encima. Casco en `neutral`, que no dice nada de ningun bando;
    cupula en `bg_s3`, la rampa del cielo. Cinco luces en el ecuador que se
    persiguen: la encendida va al indice 1 y nunca al blanco (regla 2), y
    el casco cabecea 0.1 rad, que es lo que lo hace flotar y no deslizarse.
    """
    lz = lienzo_celda(OVNI_CELDA)
    casco, cupula = rampa('neutral'), rampa('bg_s3')
    cx, cy = OVNI_CELDA / 2, OVNI_CELDA / 2 + 1
    giro = 0.10 * math.sin(2 * math.pi * t)
    co, si = math.cos(giro), math.sin(giro)
    esferoide(lz, cx + 3 * si, cy - 3 * co, 5.0, 4.5, giro, cupula, 1, calor=0.5)
    esferoide(lz, cx, cy, 11.0, 3.4, giro, casco, 2, apagado=1)
    esferoide(lz, cx - 2.2 * si, cy + 2.2 * co, 5.5, 2.2, giro, casco, 3, apagado=2)
    encendida = int(t * 5) % 5
    for i, u in enumerate(np.linspace(-0.72, 0.72, 5)):
        x = int(round(cx + u * 11 * co - 0.5))
        y = int(round(cy + u * 11 * si + 0.4))
        lz['col'][y, x] = casco[1] if i == encendida else casco[5]
    rgba_ = a_rgba(lz)
    return rgba_[..., 3] > 0, rgba_[..., :3].copy()


HOJA_OVNI = [ovni(i / OVNI_FRAMES) for i in range(OVNI_FRAMES)]


# En cada salida el juego sortea por que lado entra el ovni y, si va en linea
# recta, a que altura (pedido por el usuario el 2026-10-23). `direction` y
# `startY` son la salida de las vistas previas y de los tests; `startYMin` y
# `startYMax`, el margen del sorteo: el ovni entero y su vaiven caben en el
# tablero de punta a punta, tambien con la pendiente.

def ovni_recto(y0, sentido, pendiente=0.0):
    """Cruza el tablero de lado a lado con un vaiven vertical."""
    margen = OVNI_CELDA / 2 + OVNI['sway']
    caida = pendiente * (ANCHO + 2 * OVNI_CELDA)
    return dict(OVNI, direction=sentido, startY=float(y0), slope=float(pendiente),
                startYMin=float(margen + max(0.0, -caida)),
                startYMax=float(ALTO - margen - max(0.0, caida)),
                surf=False, surfOffset=0.0)


def ovni_surf(sentido, desplazamiento):
    """Cruza siguiendo el eje de una banda, a `desplazamiento` px de el: la
    altura la pone la banda y solo se sortea el lado."""
    return dict(OVNI, direction=sentido, startY=0.0, slope=0.0, startYMin=0.0,
                startYMax=0.0, surf=True, surfOffset=float(desplazamiento))


def ruta(desc, eje=None, sentido=None, y0=None):
    """La posicion del ovni `s` segundos despues de salir, o None si ya salio
    del tablero. Es la que reproduce `BackgroundDefinition` en Unity.
    `sentido` e `y0`, si se dan, son los de una salida sorteada."""
    sentido = desc['direction'] if sentido is None else sentido
    y0 = desc['startY'] if y0 is None else y0

    def pos(s):
        recorrido = desc['speed'] * s - OVNI_CELDA
        if recorrido > ANCHO + OVNI_CELDA:
            return None
        x = recorrido if sentido > 0 else ANCHO - recorrido
        if desc['surf']:
            y = eje[int(min(max(x, 0), ANCHO - 1))] + desc['surfOffset']
        else:
            y = y0 + desc['slope'] * recorrido
        return x, y + desc['sway'] * math.sin(2 * math.pi * s / desc['swayPeriod'])
    return pos


def ovni_en(ruta_, s):
    """Centro del ovni en pixel entero y su frame, o None."""
    if s < 0:
        return None
    p = ruta_(s)
    if p is None:
        return None
    x, y = p
    return int(round(x)), int(round(y)), int(s * FPS) % OVNI_FRAMES


def pinta_ovni(img, ruta_, s):
    en = ovni_en(ruta_, s)
    if en is None:
        return
    x, y, frame = en
    pegar(img, HOJA_OVNI[frame], y - OVNI_CELDA // 2, x - OVNI_CELDA // 2)


# --- Seccion 1: Sistema Interior -------------------------------------------

def seccion1(rng):
    """Lleno, polvoriento y barrido por la luz de un sol que no se ve.

    MOVIMIENTO: LUZ QUE BARRE. Del sol fuera de cuadro salen rayos que giran
    despacio, dos juegos en sentidos opuestos, y donde se cruzan la luz
    rebrilla. El conjunto respira en un ciclo de 10 s y, al respirar y al
    pasar un rayo, lava y devuelve las estrellas debiles. El polvo zodiacal
    se aleja del sol a 6 px/s. Los jirones y el planeta quedan quietos: son
    lo que dice que la camara no se mueve.
    """
    ramp = rampa('bg_s1')
    vin = vineta(k=1.25, suelo=0.30)
    T_SOL, A_SOL, V_POLVO = 10.0, 0.12, 6.0

    SOL_Y, SOL_X = ALTO * 0.44, -320 * K
    sol = disco(SOL_Y, SOL_X, 1620 * K, gamma=2.3)
    y, x = np.mgrid[0:ALTO, 0:ANCHO]
    theta = np.arctan2(y - SOL_Y, x - SOL_X)
    # (frecuencia angular, peso, giro en rad/s). A 400 px del sol, 0.01 rad/s
    # son 4 px/s; la frecuencia da rayos de 30 a 60 px de ancho a esa distancia
    RAYOS = [(23, 0.45, 0.011), (37, 0.30, -0.007), (61, 0.25, 0.015)]
    fases = [rng.random() * 2 * math.pi for _ in RAYOS]

    # los rayos llegan mas lejos que el resplandor: el resplandor es la luz
    # difusa, el rayo es la que pasa entre el polvo
    rayo_env = disco(SOL_Y, SOL_X, 1350, gamma=1.3) * (0.4 + 0.6 * vin)

    polvo_env = banda(math.radians(-6.5), ALTO * 0.47, 210 * K, comba=-90 * K)
    polvo_env *= np.clip(1.35 - np.linspace(0, 1, ANCHO)[None, :] * 1.05, 0, 1)
    polvo_n = ruido(rng, celdas=7, octavas=4, persistencia=0.6, periodico=True)

    jir = ruido(rng, celdas=5, octavas=4, cresta=True) ** 2.8
    jir *= np.clip(np.linspace(-0.35, 1.0, ANCHO)[None, :], 0, 1) * vin
    jirones = capa_fija(lambda im: tramar(im, jir, ramp,
                                          [(6, 0.22, 0.24), (5, 0.72, 0.14)]))

    cielo = Estrellas(rng, int(3200 * K * K), rampa('neutral'), brillo=0.85)
    # medias y fuertes, decididas con el sol en reposo: no parpadean nunca
    fija = cielo.u < np.clip(1.0 - sol[cielo.ys, cielo.xs] * 2.4, 0.05, 1.0)

    # mas grande y fuera de cuadro que en la v1: escalado se quedaba en 110 px
    # de radio, que es lo que mide el anillo de un Magnetar (96-160 px). A
    # 300 px solo entra el limbo, y un limbo no se confunde con un anillo
    cy, cx, r = ALTO * 1.15, ANCHO * 1.05, 300
    pl = _elipse(cy, cx, r, r, 0.0, gamma=0.42)
    hacia = (x - cx) * -1.0 + (y - cy) * -0.34 > 22 * K

    def _planeta(im):
        # el filo se mide en densidad, y a 300 px de radio 0.34 daba un canto
        # de 23 px: 0.19 lo deja en 6, lo que media en la v1
        silueta(im, pl, ramp, 5, hacia=hacia, umbral=0.02, filo=0.19)
        im[(pl > 0.02) & (pl < 0.10) & hacia] = ramp[3]
    planeta = capa_fija(_planeta)

    # --- lo que lee el material
    m = Mapa(1, 'Sistema Interior', ramp)
    SOL, VIN, RAYO, POLVO_E, POLVO_N = (q8(c) for c in (sol, vin, rayo_env, polvo_env, polvo_n))
    m.textura('_Fields0', 'fields0', rgba(SOL, VIN, RAYO, POLVO_E))
    m.textura('_Fields1', 'fields1', rgba(POLVO_N))
    U = estrellas_u(cielo, cielo.debil)
    m.textura('_Points', 'points', rgba(U))
    fijo = m.textura('_Static', 'static', fija_de(
        (jirones, BAJA),
        (capa_fija(lambda im: cielo.pinta(im, ~cielo.debil & fija)), ALTA),
        (planeta, ALTA)))

    L1 = m.trama('L1', [(6, 0.00, 0.46), (5, 0.52, 0.24), (4, 0.86, 0.10)])
    L2 = m.trama('L2', [(6, 0.00, 0.42), (5, 0.30, 0.32), (4, 0.65, 0.12)])
    L3 = m.trama('L3', [(6, 0.10, 0.34), (5, 0.55, 0.24), (4, 0.84, 0.16)])
    m.color('_CDebil', cielo.ramp[cielo.b_debil])
    m.float_('_ABreath', A_SOL)
    m.vector('_Sol', (SOL_X, SOL_Y))
    m.vector('_RayK', [k for k, _, _ in RAYOS])
    m.vector('_RayP', [p for _, p, _ in RAYOS])
    m.fase('_Breath', 1.0 / T_SOL, 1.0)
    for i, ((k, p, w), f) in enumerate(zip(RAYOS, fases)):
        m.fase(f'_Ray{i}', -k * w, 2 * math.pi, f)
    m.fase('_Polvo', V_POLVO, ANCHO)

    sol_, vin_, rayo_, pe_, pn_ = (de8(c) for c in (SOL, VIN, RAYO, POLVO_E, POLVO_N))
    hay_debil, u_debil = U > 0, u_de(U)

    def rayos(t):
        r = sum(p * np.sin(k * theta + (f - k * w * t))
                for (k, p, w), f in zip(RAYOS, fases))
        return 0.5 + 0.5 * r

    def fondo(t):
        img = lienzo()
        s = sol_ * (1.0 + A_SOL * math.sin(2 * math.pi * t / T_SOL))
        haz = rayos(t)
        luz = s * (0.25 + 1.10 * haz)
        tramar(img, luz * (0.45 + 0.55 * vin_), ramp, L1)
        # el polvo solo se ve donde le da un rayo: es lo que hace visible el barrido
        # rayos con hueco entre ellos, no una modulacion suave: es el hueco
        # lo que deja ver que giran
        tramar(img, rayo_ * np.clip((haz - 0.48) / 0.30, 0.0, 1.0), ramp, L2)
        polvo = pe_ * (0.15 + 0.85 * desliza(pn_, V_POLVO * t) ** 1.3)
        polvo *= 0.35 + 0.95 * haz
        tramar(img, polvo, ramp, L3)
        poner_fija(img, fijo, BAJA)
        lava = np.clip(1.0 - luz * 2.4, 0.05, 1.0)
        img[hay_debil & (u_debil < lava)] = cielo.ramp[cielo.b_debil]
        poner_fija(img, fijo, ALTA)
        return img

    m.fondo = m.fotograma = fondo
    # cruza con el sol a la espalda, a la altura del polvo
    m.ovni = ovni_recto(ALTO * 0.36, +1, pendiente=0.04)
    return m


# --- Seccion 2: El Cinturon -------------------------------------------------

def seccion2(rng):
    """Una corriente de escombro cruza el cielo y tapa lo que hay detras.

    MOVIMIENTO: MATERIA QUE FLUYE. La corriente fluye A LO LARGO DE SU ARCO:
    el grano vive en coordenadas de la banda, no de la pantalla, y en los
    lados, donde el arco cae, el polvo cae con el. Y no fluye en bloque: el
    borde interior va a 6.5 px/s y el exterior a 4, como un anillo de verdad,
    y las dos corrientes se cruzan y se deshacen la una en la otra. Las
    estrellas debiles se tapan y destapan al paso de cada grumo. La grava
    recorre el arco a su paso y en pixel entero. Y el ovni la surfea.
    """
    ramp = rampa('bg_s2')
    vin = vineta(k=1.45, suelo=0.22)
    ANG, PASO, COMBA, SIGMA = math.radians(-11.0), ALTO * 0.17, 430 * K, 175 * K
    V_EXT, V_INT = 4.0, 6.5

    arco = banda(ANG, PASO, SIGMA, comba=COMBA)
    xs = np.arange(ANCHO)
    eje = PASO + math.tan(ANG) * (xs - ANCHO / 2) + COMBA * ((xs - ANCHO / 2) / (ANCHO / 2)) ** 2

    # coordenadas de la banda: la fila `y` de la pantalla en la columna `x`
    # es la fila `y - eje(x)` del grano, corrida para que no haya negativas
    filas = np.arange(ALTO)[:, None] - np.rint(eje).astype(int)[None, :]
    corrida = int(filas.min())
    filas -= corrida
    hu = int(filas.max()) + 1

    def interior(n):
        """0 en el borde exterior de la corriente, 1 en el interior."""
        return np.clip(0.5 + 0.5 * n / SIGMA, 0.0, 1.0)

    # dos granos, uno por corriente. Mezclarlos por fila da la cizalla sin
    # que el grano se estire con el tiempo, que es lo que haria desplazar
    # cada fila a su velocidad: a los diez minutos seria todo rayas
    grano_ext = ruido(rng, h=hu, celdas=7, octavas=5, persistencia=0.6, periodico=True)
    grano_int = ruido(rng, h=hu, celdas=9, octavas=5, persistencia=0.6, periodico=True)
    mezcla = interior(np.arange(hu) + corrida)[:, None]

    # chispas: puntos sueltos, cada uno a la velocidad de su fila y con su
    # propia fraccion de partida, asi que nunca saltan todas a la vez
    n_ch = int(0.030 * hu * ANCHO)
    ch_f = rng.integers(0, hu, n_ch)
    ch_x = rng.random(n_ch) * ANCHO

    def en_arco(campo_u):
        return campo_u[filas, xs[None, :]]

    cielo = Estrellas(rng, int(2600 * K * K), rampa('neutral'), brillo=0.75)
    fija = cielo.u < np.clip(1.0 - arco[cielo.ys, cielo.xs] * 1.5, 0.0, 1.0)

    brasa = capa_fija(lambda im: im.__setitem__(
        (arco > 0.45) & (rng.random((ALTO, ANCHO)) < 0.004), ramp[3]))

    # la grava: cada roca es un sprite que se traslada por el arco
    rocas = []
    for k in range(10):
        r = 2.5 + rng.random() * 2.5      # 5-10 px: muy por debajo de 0.6 u
        ra = r * (0.75 + 0.6 * rng.random())
        lado = int(math.ceil(max(ra, r))) + 2
        n = 2 * lado + 1
        yy, xx = np.mgrid[0:n, 0:n]
        roca = _elipse(lado, lado, ra, r, rng.random() * 3.1, gamma=0.35, h=n, w=n)
        hacia = (xx - lado) * -0.6 + (yy - lado) * -0.8 > 0
        sprite = capa_fija(lambda im: silueta(im, roca, ramp, 4, hacia=hacia,
                                              umbral=0.02, filo=0.34), h=n, w=n)
        dy = (rng.random() - 0.5) * 210 * K
        rocas.append(dict(sprite=sprite, lado=lado, dy=dy,
                          x0=ANCHO * (k + rng.random()) / 10.0,
                          v=(V_EXT + (V_INT - V_EXT) * float(interior(dy))) * 0.6))

    def _centro(rk, t):
        """El pixel central de una roca en el instante `t`."""
        vuelta = ANCHO + 2 * rk['lado']
        x = int((rk['x0'] + rk['v'] * t) % vuelta) - rk['lado']
        return x, int(round(eje[min(max(x, 0), ANCHO - 1)] + rk['dy']))

    def _roca(img, rk, t):
        x, y = _centro(rk, t)
        pegar(img, rk['sprite'], y - rk['lado'], x - rk['lado'])

    # --- lo que lee el material
    m = Mapa(2, 'El Cinturon', ramp)
    ARCO, VIN = q8(arco), q8(vin)
    m.textura('_Fields0', 'fields0', rgba(ARCO, VIN))
    EXT, INT = q8(grano_ext), q8(grano_int)
    m.textura('_Band', 'fields1', rgba(EXT, INT))
    U = estrellas_u(cielo, cielo.debil)
    m.textura('_Points0', 'points0', rgba(U))

    # las chispas, en coordenadas de la banda: cada una en la celda de su
    # columna de partida, con su fraccion. Caben cuatro por celda, una por
    # canal; las que sobran -una o ninguna en 24 000- se pierden, y la
    # referencia, que lee de aqui, tampoco las ve
    CH = np.zeros((hu, ANCHO, 4), np.uint8)
    llenas = np.zeros((hu, ANCHO), np.int32)
    for f, c, fr in zip(ch_f, np.floor(ch_x).astype(int), ch_x - np.floor(ch_x)):
        if llenas[f, c] < 4:
            CH[f, c, llenas[f, c]] = cod_frac(fr)
            llenas[f, c] += 1
    m.textura('_Points1', 'points1', CH)

    fijo = m.textura('_Static', 'static', fija_de(
        (capa_fija(lambda im: cielo.pinta(im, ~cielo.debil & fija)), BAJA),
        (brasa, ALTA)))

    T = m.trama('T', [(6, 0.00, 0.58), (5, 0.42, 0.32), (4, 0.78, 0.18)])
    m.color('_CDebil', cielo.ramp[cielo.b_debil])
    m.float_('_Corrida', corrida)
    m.float_('_Sigma', SIGMA)
    m.float_('_BandRows', hu)
    m.fase('_GranoExt', V_EXT, ANCHO)
    m.fase('_GranoInt', V_INT, ANCHO)
    filas_u = np.arange(hu)
    v_fila = V_EXT + (V_INT - V_EXT) * interior(filas_u + corrida)
    m.lista('_EjeRow', np.zeros(ANCHO), 0.0, np.rint(eje) + corrida)
    m.lista('_SparkShift', v_fila, ANCHO, np.zeros(hu))
    m.eje = [float(e) for e in eje]

    celda = max(2 * rk['lado'] + 1 for rk in rocas)
    hoja = np.zeros((celda, celda * len(rocas), 4), np.uint8)
    for i, rk in enumerate(rocas):
        mascara, colores = rk['sprite']
        n = mascara.shape[0]
        o = (celda - n) // 2
        trozo = hoja[o:o + n, i * celda + o:i * celda + o + n]
        trozo[mascara, :3] = colores[mascara]
        trozo[mascara, 3] = 255
    m.rocas = dict(sheet='bg_section2_rocks_sheet.png', cell=celda, hoja=hoja,
                   list=[dict(half=rk['lado'], dy=rk['dy'], x0=rk['x0'], speed=rk['v'])
                         for rk in rocas])

    ext_, int_, arco_, vin_ = (de8(c) for c in (EXT, INT, ARCO, VIN))
    hay_debil, u_debil = U > 0, u_de(U)
    cf, cc, ck = np.nonzero(CH)
    c_x = cc + frac_de(CH[cf, cc, ck])
    c_v = v_fila[cf]

    def fondo(t):
        img = lienzo()
        g = (1 - mezcla) * desliza(ext_, V_EXT * t) + mezcla * desliza(int_, V_INT * t)
        grano = en_arco(g)
        tapa = np.clip(arco_ * 1.5 * (0.4 + 0.6 * grano), 0.0, 1.0)
        img[hay_debil & (u_debil < 1.0 - tapa)] = cielo.ramp[cielo.b_debil]
        poner_fija(img, fijo, BAJA)
        tramar(img, arco_ * (0.25 + 0.75 * grano) * (0.55 + 0.45 * vin_), ramp, T)
        chispa_u = np.zeros((hu, ANCHO), bool)
        chispa_u[cf, np.floor(c_x + c_v * t).astype(int) % ANCHO] = True
        img[en_arco(chispa_u) & (arco_ > 0.18)] = ramp[4]
        poner_fija(img, fijo, ALTA)
        return img

    def fotograma(t):
        img = fondo(t)
        for rk in rocas:
            _roca(img, rk, t)
        return img

    m.fondo, m.fotograma = fondo, fotograma
    m.posiciones_rocas = lambda t: [_centro(rk, t) for rk in rocas]
    # el ovni surfea la corriente por su borde interior
    m.ovni = ovni_surf(-1, 0.6 * SIGMA)
    return m


# --- Seccion 3: Espacio Profundo --------------------------------------------

def seccion3(rng):
    """Vacio. Es el unico de los tres cuya idea es la ausencia.

    MOVIMIENTO: EL VACIO DERIVA. Lo que hay se coloca por profundidades. La galaxia y las estrellas fuertes no
    se mueven nunca: estan infinitamente lejos y dan la escala. Las estrellas
    medias derivan a 0.9 px/s. Y delante pasa polvo en tres capas, motas
    a 2, 5.7 y 6.9 px/s, mas claras cuanto mas cerca: la diferencia de
    velocidad es lo que convierte un plano negro en profundidad. Los velos
    no se deslizan, se retuercen: el campo se deforma con otro campo que
    tambien se mueve, y las hebras ondulan como humo. Algunas estrellas
    medias titilan.
    """
    ramp = rampa('bg_s3')
    vin = vineta(k=1.7, suelo=0.12)
    V_VELO1, V_VELO2, V_DEFORMA, A_DEFORMA = -2.5, 2.0, 6.0, 30.0
    V_MEDIAS = (0.22, -0.9)          # (vy, vx) en px/s
    V_MOTAS = ((0.5, -2.0), (1.4, -5.5), (1.6, -6.7))   # de lejos a cerca

    velo1_n = ruido(rng, celdas=3, octavas=5, persistencia=0.52, cresta=True,
                    periodico=True)
    velo1_env = disco(ALTO * 0.16, ANCHO * 0.80, 1150 * K, gamma=1.7) * vin
    velo2_n = ruido(rng, celdas=4, octavas=4, persistencia=0.5, cresta=True,
                    periodico=True)
    velo2_env = disco(ALTO * 0.92, ANCHO * 0.14, 900 * K, gamma=2.0) * vin
    deforma_y = ruido(rng, celdas=2, octavas=2, periodico=True) - 0.5
    deforma_x = ruido(rng, celdas=2, octavas=2, periodico=True) - 0.5
    y, x = np.mgrid[0:ALTO, 0:ANCHO].astype(np.float32)

    cielo = Estrellas(rng, int(1900 * K * K), rampa('neutral'), brillo=1.0)
    fija = cielo.u < vin[cielo.ys, cielo.xs] * 0.9 + 0.1
    titila = cielo.media & (rng.random(len(cielo.ys)) < 0.35)
    DUTY = 0.22         # fraccion del periodo en que la estrella esta bajando

    motas = []
    # las cercanas son mas claras, pero solo viajan: nunca se encienden en su
    # sitio, asi que no son destellos (regla 2)
    for cuantas, banda_m in ((700, 5), (320, 4), (45, 3)):
        motas.append((rng.random(cuantas) * ALTO, rng.random(cuantas) * ANCHO, banda_m))

    gal = _elipse(ALTO * 0.30, ANCHO * 0.17, 128 * K, 26 * K, math.radians(-19),
                  gamma=1.4)
    galaxia = capa_fija(lambda im: tramar(
        im, gal, ramp, [(6, 0.00, 0.85), (5, 0.32, 0.80), (4, 0.58, 0.80),
                        (3, 0.80, 0.85), (2, 0.93, 0.80)]))

    # --- lo que lee el material
    m = Mapa(3, 'Espacio Profundo', ramp)
    V1, V2 = q8(velo1_n), q8(velo2_n)
    DY, DX = q8(deforma_y + 0.5), q8(deforma_x + 0.5)
    E1, E2 = q8(velo1_env), q8(velo2_env)
    m.textura('_Fields0', 'fields0', rgba(V1, V2, DY, DX))
    m.textura('_Fields1', 'fields1', rgba(E1, E2))

    # Las estrellas medias derivan todas a la vez y desde un pixel entero, asi
    # que en cada instante estan corridas el mismo numero entero de pixeles:
    # el material las busca con una sola lectura. Las que titilan guardan su
    # frecuencia como m / P_TITILEO, con m entero: es lo que deja a Unity
    # reducir su fase con un solo modulo de P_TITILEO para todas.
    P_TITILEO, BASE = 3360.0, 224          # 3360 / 224 = 15 s, 3360 / 479 = 7 s
    P0 = np.zeros((ALTO, ANCHO, 4), np.uint8)
    P1 = np.zeros((ALTO, ANCHO, 4), np.uint8)
    for i in np.nonzero(cielo.media & fija)[0]:
        yy, xx = cielo.ys[i], cielo.xs[i]
        if titila[i]:
            P1[yy, xx, 2] = int(np.clip(round(P_TITILEO / cielo.periodo[i]), BASE, BASE + 255)) - BASE
            P1[yy, xx, 3] = 2 + int(math.floor(cielo.fase[i] * 254))
        else:
            P1[yy, xx, 2] = 0
            P1[yy, xx, 3] = 1
    # las motas, cada una en su celda con su fraccion en las dos direcciones:
    # derivan en diagonal, y el material mira las cuatro celdas de las que
    # puede venir. Una por celda; si dos caen juntas, la segunda se pierde
    for (my, mx, _), (tex, ch) in zip(motas, ((P0, 0), (P0, 2), (P1, 0))):
        cy, cx = np.floor(my).astype(int), np.floor(mx).astype(int)
        for a, b, fy, fx in zip(cy, cx, my - cy, mx - cx):
            if tex[a, b, ch] == 0:
                tex[a, b, ch] = cod_frac(fy)
                tex[a, b, ch + 1] = cod_frac(fx)
    m.textura('_Points0', 'points0', P0)
    m.textura('_Points1', 'points1', P1)

    fijo = m.textura('_Static', 'static', fija_de(
        (capa_fija(lambda im: cielo.pinta(im, cielo.debil & fija)), BAJA),
        (capa_fija(lambda im: cielo.pinta(im, cielo.fuerte & fija)), ALTA),
        (galaxia, ALTA)))

    T1 = m.trama('V1', [(6, 0.00, 0.62), (5, 0.42, 0.38), (4, 0.72, 0.24), (3, 0.90, 0.14)])
    T2 = m.trama('V2', [(6, 0.00, 0.44), (5, 0.56, 0.24), (4, 0.86, 0.12)])
    for i in range(3):
        m.color(f'_CMedia{i}', cielo.ramp[cielo.b_media + i])
    m.float_('_ADeforma', A_DEFORMA * 2)
    m.float_('_Duty', DUTY)
    m.float_('_TwinkleBase', BASE)
    m.fase('_DeformaY', V_DEFORMA, ANCHO)
    m.fase('_DeformaX', -V_DEFORMA * 0.7, ANCHO)
    m.fase('_Velo1', V_VELO1, ANCHO)
    m.fase('_Velo2', V_VELO2, ANCHO)
    m.fase('_MediasY', V_MEDIAS[0], ALTO)
    m.fase('_MediasX', V_MEDIAS[1], ANCHO)
    m.fase('_Twinkle', 1.0 / P_TITILEO, 1.0)
    for i, (vy, vx) in enumerate(V_MOTAS):
        m.fase(f'_Mota{i}Y', vy, ALTO)
        m.fase(f'_Mota{i}X', vx, ANCHO)

    v1_, v2_, e1_, e2_ = (de8(c) for c in (V1, V2, E1, E2))
    dy_, dx_ = de8(DY) - 0.5, de8(DX) - 0.5

    def velo(campo, v, t):
        dy = desliza(dy_, V_DEFORMA * t)
        dx = desliza(dx_, -V_DEFORMA * 0.7 * t)
        return muestrea(campo, y + A_DEFORMA * 2 * dy, x + A_DEFORMA * 2 * dx - v * t)

    med_y, med_x = np.nonzero(P1[..., 3])
    med_info = P1[med_y, med_x, 3].astype(int)
    med_tit = med_info >= 2
    med_m = BASE + P1[med_y, med_x, 2].astype(np.float64)
    med_f = (med_info - 2) / 254.0
    motas_q = []
    for (_, _, banda_m), (tex, ch) in zip(motas, ((P0, 0), (P0, 2), (P1, 0))):
        a, b = np.nonzero(tex[..., ch])
        motas_q.append((a + frac_de(tex[a, b, ch]), b + frac_de(tex[a, b, ch + 1]), banda_m))

    def fondo(t):
        img = lienzo()
        v1 = velo(v1_, V_VELO1, t) ** 2.9 * e1_
        tramar(img, v1, ramp, T1)
        v2 = velo(v2_, V_VELO2, t) ** 3.4 * e2_
        tramar(img, v2, ramp, T2)
        # solo derivan las medias: las fuertes y las debiles son el fondo fijo
        poner_fija(img, fijo, BAJA)
        fase = (t * med_m / P_TITILEO + med_f) % 1.0
        bajada = np.where(fase < DUTY, np.sin(math.pi * fase / DUTY) ** 2, 0.0)
        b_media = cielo.b_media + np.where(med_tit, np.rint(2 * bajada), 0).astype(int)
        yy, xx = deriva(med_y, med_x, *V_MEDIAS, t)
        img[yy, xx] = cielo.ramp[b_media]
        poner_fija(img, fijo, ALTA)
        for (my, mx, banda_m), v in zip(motas_q, V_MOTAS):
            yy, xx = deriva(my, mx, *v, t)
            img[yy, xx] = ramp[banda_m]
        return img

    m.fondo = m.fotograma = fondo
    # en diagonal y contra la deriva: es lo unico del vacio que va a su aire
    m.ovni = ovni_recto(ALTO * 0.80, +1, pendiente=-0.30)
    return m


SECCIONES = {1: seccion1, 2: seccion2, 3: seccion3}


# --- Verificacion -----------------------------------------------------------

def informe_ciclo(nombre, fotograma, segundos=120.0, muestras=40):
    """El presupuesto de contraste, en el PEOR fotograma de dos minutos.

    Y dos medidas del movimiento entre un fotograma y el siguiente: cuanto
    cambia como mucho la media de una ventana de 96x96, que es lo que el ojo
    lee como parpadeo, y cuantos pixeles cambian por segundo, que es cuanto
    movimiento hay.
    """
    peor, salto, pps = 0.0, 0.0, 0
    dt = 1.0 / FPS_VISTA
    for t in np.linspace(0.0, segundos, muestras, endpoint=False):
        a, b = fotograma(t), fotograma(t + dt)
        va, vb = ventanas(a), ventanas(b)
        peor = max(peor, float(va.max()))
        salto = max(salto, float(np.abs(vb - va).max()))
        pps += int(np.any(a != b, axis=2).sum())
    pps = pps * FPS_VISTA // muestras
    estado = 'OK ' if peor <= TOPE else 'AVISO'
    print(f'{estado} {nombre:16} peor ventana 96x96 {peor:5.2f} / {TOPE:.1f}  '
          f'margen {TOPE - peor:+5.2f}  salto {salto:4.2f}  px/s {pps}')
    return peor <= TOPE


# --- Exportacion para Unity -------------------------------------------------

def _png(ruta_, datos):
    """Escribe un PNG y comprueba que se lee igual: el material tiene que ver
    exactamente los bytes con los que se calculo la referencia."""
    modo = 'RGBA' if datos.ndim == 3 and datos.shape[2] == 4 else 'RGB'
    Image.fromarray(datos, modo).save(ruta_)
    assert np.array_equal(np.asarray(Image.open(ruta_).convert(modo)), datos), ruta_


def exporta(m, d_bg):
    """Las texturas de datos, la hoja de la grava, el JSON del mapa y los
    fotogramas de referencia. `BackgroundAssets` lee el JSON en Unity y
    los tests de `T5-05b` comparan el material con `ref/`."""
    base = f'bg_section{m.n}'
    d_ref = os.path.join(d_bg, 'ref')
    os.makedirs(d_ref, exist_ok=True)

    texturas = []
    for propiedad, sufijo, datos in m.texturas:
        archivo = f'{base}_{sufijo}.png'
        _png(os.path.join(d_bg, archivo), datos)
        texturas.append(dict(property=propiedad, file=archivo))

    rocas = dict(sheet='', cell=0, list=[])
    if m.rocas is not None:
        _png(os.path.join(d_bg, m.rocas['sheet']), m.rocas['hoja'])
        rocas = {k: v for k, v in m.rocas.items() if k != 'hoja'}

    referencias = []
    for i, t in enumerate(REFERENCIAS):
        archivo = f'ref/{base}_ref{i}.png'
        _png(os.path.join(d_bg, archivo), m.fondo(t))
        planas = []
        if m.rocas is not None:
            for x, y in m.posiciones_rocas(t):
                planas += [x, y]
        referencias.append(dict(t=t, file=archivo, rocks=planas))

    pos = ruta(m.ovni, m.eje)
    muestras = []
    for s in np.arange(0.0, 36.0, 0.37):
        en = ovni_en(pos, float(s))
        muestras.append(dict(s=float(s), visible=en is not None,
                             x=en[0] if en else 0, y=en[1] if en else 0,
                             frame=en[2] if en else 0))

    datos = dict(map=m.n, name=m.nombre, version=VERSION, width=ANCHO, height=ALTO,
                 frame=f'{base}.png', budget=float(TOPE),
                 textures=texturas, floats=m.floats, vectors=m.vectores,
                 colors=m.colores, phases=m.fases, lists=m.listas,
                 axis=m.eje, ufo=m.ovni, rocks=rocas,
                 references=referencias, ufoSamples=muestras)
    with open(os.path.join(d_bg, f'{base}.json'), 'w', encoding='utf-8') as f:
        json.dump(datos, f, indent=1)
    print('exportado', base, f'({len(texturas)} texturas, {len(referencias)} referencias)')


# --- Vistas previas ---------------------------------------------------------

# Un nivel real por mapa, del Apendice del maestro: el mas cargado de cada uno.
TABLEROS = {
    1: [('estrella', 1, 'player', -5.5, -0.5), ('asteroide', 0, 'player', -5, 1.5),
        ('estrella', 2, 'enemy', 5.5, 0.5), ('magnetar', 1, 'enemy', 3.75, -1.5),
        ('asteroide', 0, 'neutral', -3.5, -1.25), ('asteroide', 0, 'neutral', 0, 2.5),
        ('asteroide', 0, 'neutral', 0, -2.5), ('asteroide', 0, 'neutral', 2.75, 1.75)],
    2: [('estrella', 2, 'player', -6, 0), ('estrella', 3, 'enemy', 5.75, 0),
        ('pulsar', 2, 'enemy', 5, 2.5), ('magnetar', 2, 'enemy', 4, -1.75),
        ('magnetar', 3, 'neutral', 0.5, -0.5), ('asteroide', 0, 'neutral', -4, 0.75),
        ('asteroide', 0, 'neutral', -3.5, -2.25), ('asteroide', 0, 'neutral', -2.75, 2.5),
        ('asteroide', 0, 'neutral', -0.75, 2.75), ('asteroide', 0, 'neutral', 2.5, 2),
        ('asteroide', 0, 'neutral', 2.5, -3)],
    3: [('estrella', 2, 'player', -6.25, 1), ('magnetar', 1, 'player', -5, -1.75),
        ('estrella', 4, 'enemy', 6.25, 1.75), ('estrella', 4, 'enemy', 6.25, -1.75),
        ('pulsar', 3, 'enemy', 4.75, 3), ('pulsar', 3, 'enemy', 4.75, -3),
        ('magnetar', 4, 'enemy', 4.5, 0), ('magnetar', 4, 'neutral', 0, 2),
        ('magnetar', 4, 'neutral', 0, -2), ('asteroide', 0, 'neutral', -4.5, 2),
        ('asteroide', 0, 'neutral', -3.25, -2.75), ('asteroide', 0, 'neutral', -3.5, 0.25),
        ('asteroide', 0, 'neutral', -2.5, 3), ('asteroide', 0, 'neutral', 2, 0.25),
        ('asteroide', 0, 'neutral', 2.25, 3), ('asteroide', 0, 'neutral', 2.5, -3)],
}

# Las versiones elegidas, segun la columna "Archivos" de A.2
SPRITE = {'asteroide': 'asteroide/v1/ast_lvl{n}_{f}.png',
          'estrella': 'estrella/v1/str_lvl{n}_{f}.png',
          'pulsar': 'pulsar/v2/pul_lvl{n}_{f}.png',
          'magnetar': 'magnetar/v1/mag_lvl{n}_{f}.png'}


def capa_tablero(piezas):
    """Los nodos de un nivel, como capa para poner encima del fondo."""
    mascara = np.zeros((ALTO, ANCHO), bool)
    colores = np.zeros((ALTO, ANCHO, 3), np.uint8)
    for nodo, niv, fac, ux, uy in piezas:
        sp = np.asarray(Image.open(os.path.join(
            RAIZ, 'art', 'nodes', SPRITE[nodo].format(n=niv, f=fac))).convert('RGBA'))
        h, w = sp.shape[:2]
        x0 = int(round(ANCHO / 2 + ux * 64 - w / 2))
        y0 = int(round(ALTO / 2 - uy * 64 - h / 2))
        m = sp[..., 3] > 0
        mascara[y0:y0 + h, x0:x0 + w] |= m
        colores[y0:y0 + h, x0:x0 + w][m] = sp[..., :3][m]
    return mascara, colores


def gif(destino, fotogramas, fps, escala=1):
    imgs = []
    for f in fotogramas:
        im = Image.fromarray(f, 'RGB')
        if escala > 1:
            im = im.resize((im.width * escala, im.height * escala), Image.NEAREST)
        imgs.append(im.quantize(256, method=Image.Quantize.MEDIANCUT,
                                dither=Image.Dither.NONE))
    imgs[0].save(destino, save_all=True, append_images=imgs[1:],
                 duration=round(1000 / fps), loop=0, optimize=False)
    print('escrito', destino)


def hoja_ovni(d_bg, d_pre):
    """La hoja de 16 frames, y su vista a x6 sobre el fondo plano."""
    hoja = np.zeros((OVNI_CELDA, OVNI_CELDA * OVNI_FRAMES, 4), np.uint8)
    vista = []
    for i, (m, c) in enumerate(HOJA_OVNI):
        celda = hoja[:, i * OVNI_CELDA:(i + 1) * OVNI_CELDA]
        celda[..., :3] = c
        celda[..., 3] = np.where(m, 255, 0)
        fondo = lienzo(OVNI_CELDA, OVNI_CELDA)
        fondo[m] = c[m]
        vista.append(fondo)
    Image.fromarray(hoja, 'RGBA').save(os.path.join(d_bg, 'bg_ufo_sheet.png'))
    if '--sin-gif' not in sys.argv:
        gif(os.path.join(d_pre, 'ovni_x6.gif'), vista, FPS, escala=6)


def main():
    d_bg = os.path.join(RAIZ, 'art', 'bg', 'fondos', f'v{VERSION}')
    d_pre = os.path.join(dir_preview('fondos'), f'v{VERSION}')
    os.makedirs(d_bg, exist_ok=True)
    os.makedirs(d_pre, exist_ok=True)
    hoja_ovni(d_bg, d_pre)
    solo = [int(a) for a in sys.argv[1:] if a.isdigit()] or list(SECCIONES)
    ok = True
    for n in solo:
        m = SECCIONES[n](np.random.default_rng(SEMILLAS[n]))
        fotograma, pos = m.fotograma, ruta(m.ovni, m.eje)
        Image.fromarray(fotograma(0.0), 'RGB').save(
            os.path.join(d_bg, f'bg_section{n}.png'))
        if '--exporta' in sys.argv:
            exporta(m, d_bg)
        ok &= informe_ciclo(f'S{n} {m.nombre}', fotograma)
        tablero = capa_tablero(TABLEROS[n])

        def con_nodos(t, s_ovni):
            img = fotograma(t)
            pinta_ovni(img, pos, s_ovni)
            poner(img, tablero)
            return img

        # la vista fija que cita el inventario; los GIF no se versionan
        Image.fromarray(con_nodos(2.0, 6.0), 'RGB').save(
            os.path.join(d_pre, f's{n}_tablero.png'))
        if '--sin-gif' in sys.argv:
            continue
        f = FPS_VISTA
        gif(os.path.join(d_pre, f's{n}_real.gif'),
            [fotograma(i / f) for i in range(10 * f)], f)
        gif(os.path.join(d_pre, f's{n}_x8.gif'),
            [fotograma(8 * i / f) for i in range(8 * f)], f)
        # el ovni entra a los 2 s de la vista, para que salga en ella
        gif(os.path.join(d_pre, f's{n}_tablero.gif'),
            [con_nodos(i / f, i / f + 4.0) for i in range(8 * f)], f, escala=2)
        # un paso entero del ovni, de borde a borde, a 12 fps
        paso = int((ANCHO + 2 * OVNI_CELDA) / 30.0) + 2
        gif(os.path.join(d_pre, f's{n}_ovni.gif'),
            [con_nodos(i / FPS, i / FPS) for i in range(paso * FPS)], FPS)
    if not ok:
        print('\n  Alguna seccion se pasa del presupuesto en algun fotograma.')


if __name__ == '__main__':
    main()
