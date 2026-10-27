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

from nova_core import BAYER8, RAIZ, dir_preview, rampa

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


# --- La onda -----------------------------------------------------------------

def _t(f):
    return 2.0 * math.pi * (f % FRAMES) / FRAMES


def _onda_cruda(u, f):
    t = _t(f)
    w = np.zeros_like(u, dtype=np.float64)
    for n, a, m, fi in ARMONICOS:
        w += a * np.sin(2.0 * math.pi * n * u / TESELA - m * t + fi)
    return w * (1.0 + RESPIRA * math.sin(t))


def _escala():
    u = np.arange(0, TESELA, 0.25)
    pico = max(np.abs(_onda_cruda(u, f)).max() for f in range(FRAMES))
    return (BANDA - 0.35) / pico


ESCALA = _escala()


def onda(u, f):
    """Desplazamiento del hilo respecto al eje, en px, en la coordenada u del
    tramo (periodica en TESELA) y el frame f (periodica en FRAMES)."""
    return ESCALA * _onda_cruda(np.asarray(u, np.float64), f)


# --- Rasterizado ---------------------------------------------------------------

def _bayer(ancho):
    """La matriz, reflejada en el eje. Tal cual, la fila 7 lee la ultima de
    Bayer —umbral medio 42/64— y la 8, la primera —21/64—: con la misma
    densidad se enciende el doble de luz debajo del eje que encima, y el
    trazo pesa hacia un lado al girarlo. Reflejada, el lado es indiferente."""
    filas = list(range(8)) + list(range(7, -1, -1))
    return np.tile(BAYER8[filas], (1, ancho // 8 + 1))[:, :ancho]


def _filas(y):
    return np.clip(np.floor(y).astype(int), 0, ALTO - 1)


def _nucleo(u0, ancho, f, env):
    """Fila del hilo por columna y la mascara de 1 px, 8-conexa: si dos
    columnas vecinas saltan mas de una fila, el hueco se reparte entre ellas."""
    x = np.arange(ancho)
    filas = _filas(EJE + onda(u0 + x + 0.5, f) * env)
    m = np.zeros((ALTO, ancho), bool)
    m[filas, x] = True
    for c in range(ancho - 1):
        a, b = filas[c], filas[c + 1]
        if abs(a - b) > 1:
            paso = 1 if b > a else -1
            mitad = (a + b) // 2
            for r in range(a + paso, b, paso):
                m[r, c if (r - mitad) * paso <= 0 else c + 1] = True
    return filas, m


def _distancia(u0, ancho, f, env_fn):
    """Distancia de cada pixel a la curva del hilo, con la curva muestreada
    a 1/4 px y 8 px de margen a cada lado para que el borde de una tesela vea
    la curva de la siguiente."""
    xs = np.arange(-8.0, ancho + 8.0, 0.25)
    ys = EJE + onda(u0 + xs, f) * env_fn(xs)
    py, px = np.mgrid[0:ALTO, 0:ancho]
    px = px + 0.5
    py = py + 0.5
    d = np.full((ALTO, ancho), np.inf)
    for i in range(0, len(xs), 64):
        cx = xs[i:i + 64][None, None, :]
        cy = ys[i:i + 64][None, None, :]
        d = np.minimum(d, np.sqrt((px[..., None] - cx) ** 2 +
                                  (py[..., None] - cy) ** 2).min(axis=-1))
    return d


def _paquetes(u0, ancho, f):
    """Centros u de los paquetes que caen en [u0 - 16, u0 + ancho + 16)."""
    base = (FASE_PAQ + VELOCIDAD * (f % FRAMES)) % SEPARACION
    k0 = math.floor((u0 - 16 - base) / SEPARACION)
    out = []
    k = k0
    while True:
        u = base + k * SEPARACION
        if u >= u0 + ancho + 16:
            break
        if u >= u0 - 16:
            out.append(u)
        k += 1
    return out


# --- Ramitas -----------------------------------------------------------------

def _sitios():
    """Sitios de las ramitas de cada frame, en u del tramo, y su lado. Se
    sortean con semilla fija y se rechazan hasta que ningun sitio cae a menos
    de DIST_RAMITAS de uno del frame anterior, incluido el 15 contra el 0."""
    rng = np.random.default_rng(SEMILLA)

    def lejos(a, bs, dmin):
        return all(min(abs(a - b), TESELA - abs(a - b)) >= dmin for b in bs)

    for _ in range(2000):
        sitios = []
        ok = True
        for f in range(FRAMES):
            prev = [s for s, _ in sitios[f - 1]] if f else []
            elegidos = []
            for _ in range(400):
                s = float(rng.integers(0, TESELA))
                if lejos(s, prev, DIST_RAMITAS) and lejos(s, elegidos, 56):
                    elegidos.append(s)
                    if len(elegidos) == RAMITAS:
                        break
            if len(elegidos) < RAMITAS:
                ok = False
                break
            # alternan de lado dentro del frame y de un frame al siguiente
            lado0 = 1 if f % 2 == 0 else -1
            sitios.append([(s, lado0 * (1 if i % 2 == 0 else -1))
                           for i, s in enumerate(sorted(elegidos))])
        if ok and all(lejos(s, [p for p, _ in sitios[-1]], DIST_RAMITAS)
                      for s, _ in sitios[0]):
            return sitios
    raise RuntimeError('no hay reparto de ramitas que no repita sitio')


SITIOS = _sitios()


def _linea(m, x0, y0, x1, y1):
    """Segmento de 1 px, 8-conexo, sin ImageDraw."""
    n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
    for i in range(n):
        s = i / max(n - 1, 1)
        x = int(round(x0 + (x1 - x0) * s))
        y = int(round(y0 + (y1 - y0) * s))
        if 0 <= y < ALTO and 0 <= x < m.shape[1]:
            m[y, x] = True


def _ramitas(u0, ancho, f, filas, env, viva):
    """Mascara de las ramitas que nacen en f (`viva`) o de las que nacieron
    en f - 1 y se apagan (`not viva`), arrastradas por el flujo."""
    m = np.zeros((ALTO, ancho), bool)
    origen = f if viva else (f - 1) % FRAMES
    deriva = 0 if viva else VELOCIDAD // 2
    for s, lado in SITIOS[origen]:
        for rep in (-TESELA, 0, TESELA):
            x = int(s + rep - u0) + deriva
            if not (0 <= x < ancho) or env[x] < 0.6:
                continue
            y = int(filas[x])
            # un rayo quebrado: sale empinado del hilo, se dobla hacia la punta
            # y se abre en horquilla. Al apagarse queda solo el primer codo.
            variante = (int(s) * 7 + origen) % 3
            codo = (x + 1, y + lado * (2 + variante % 2))
            fin = (codo[0] + 3, codo[1] + lado * (1 + (variante == 2)))
            tramos = [((x, y), codo)]
            if viva:
                tramos.append((codo, fin))
                if variante != 0:
                    tramos.append((codo, (codo[0] + 1, codo[1] + lado * 2)))
            for (xa, ya), (xb, yb) in tramos:
                _linea(m, xa, ya, xb, yb)
    m[filas, np.arange(ancho)] = False     # el hilo no es rama
    return m


# --- Piezas ------------------------------------------------------------------

def _pintar(dens, medio, nucleo, extra_claro, extra_blanco, ancho):
    """De densidad a pixeles: trama Bayer anclada a la pieza, tres bandas
    de resplandor, y encima los trazos solidos de claro a mas claro.

    La cola de la densidad se recorta antes de tramar: por debajo de ~0.1
    solo se encienden las celdas mas bajas de la matriz, siempre las mismas,
    y a la misma distancia del hilo salen como una rejilla de puntos."""
    col = np.zeros((ALTO, ancho, 3), np.uint8)
    dens = np.clip((dens - 0.1) / 0.9, 0, 1)
    on = dens > _bayer(ancho)
    idx = np.full(dens.shape, TRAMA[-1][1])
    for corte, banda in reversed(TRAMA[:-1]):
        idx = np.where(dens > corte, banda, idx)
    col[on] = RAMP[idx[on]]
    for mascara, i in ((medio, 3), (extra_claro, 2), (nucleo, 1), (extra_blanco, 0)):
        col[mascara] = RAMP[i]
        on |= mascara
    return col, on


def pieza(nombre, f):
    """Una pieza en el frame f: (rgb, alfa, filas del hilo)."""
    if nombre == 'tramo':
        u0, ancho = 0.0, TESELA
        env_fn = lambda x: np.ones_like(np.asarray(x, np.float64))
    elif nombre == 'punta':
        u0, ancho = 0.0, PUNTA
        # el hilo se cierra sobre el eje en el punto de contacto
        env_fn = lambda x: np.clip(1.0 - np.asarray(x, np.float64) / CONTACTO, 0, 1) ** 0.8
    else:
        u0, ancho = -float(NAC), NAC
        # nace fino: sale del eje y abre hasta la amplitud del tramo
        env_fn = lambda x: np.clip(np.asarray(x, np.float64) / NAC, 0, 1) ** 1.5

    x = np.arange(ancho) + 0.5
    env = env_fn(x)
    filas, nucleo = _nucleo(u0, ancho, f, env)
    alcance = np.ones(ancho, bool)
    if nombre == 'punta':
        alcance = np.arange(ancho) < CONTACTO
        nucleo &= alcance[None, :]

    d = _distancia(u0, ancho, f, env_fn)
    d_prev = _distancia(u0, ancho, (f - 1) % FRAMES, env_fn)
    dens = 0.95 * np.exp(-d / CAIDA) + 0.32 * np.exp(-d_prev / 1.2)

    py, px = np.mgrid[0:ALTO, 0:ancho] + 0.5
    claro = np.zeros((ALTO, ancho), bool)
    blanco = np.zeros((ALTO, ancho), bool)

    medio = np.zeros((ALTO, ancho), bool)

    # paquetes: una cuenta blanca de 4 px que engorda el hilo a 3 en su
    # centro, envuelta en luz densa, con la cola larga hacia el origen
    for ub in _paquetes(u0, ancho, f):
        xb = ub - u0
        if nombre == 'punta' and xb >= CONTACTO:
            continue          # ya ha llegado: lo absorbe el punto de contacto
        if nombre == 'nacimiento' and xb < 6:
            continue
        yb = EJE + float(onda(ub, f)) * float(env_fn(np.array([xb]))[0])
        detras = np.clip(xb - px, 0, None)
        delante = np.clip(px - xb, 0, None)
        dens += 1.1 * np.exp(-(delante / 2.5) ** 2 - (detras / 9.0) ** 2 -
                             ((py - yb) / 2.4) ** 2)
        for c in range(int(xb) - 2, int(xb) + 2):
            if 0 <= c < ancho:
                blanco[filas[c], c] = True
        for c in range(int(xb) - 1, int(xb) + 1):
            if 0 <= c < ancho:
                for r in (filas[c] - 1, filas[c] + 1):
                    if 0 <= r < ALTO:
                        claro[r, c] = True
        for c in range(int(xb) - 6, int(xb) - 2):
            if 0 <= c < ancho:
                for r in (filas[c] - 1, filas[c] + 1):
                    if 0 <= r < ALTO:
                        medio[r, c] = True

    # ramitas: brillantes al nacer, en la banda 3 al apagarse
    viva = _ramitas(u0, ancho, f, filas, env, True)
    muere = _ramitas(u0, ancho, f, filas, env, False)
    if nombre == 'punta':
        viva &= alcance[None, :]
        muere &= alcance[None, :]
    nucleo = nucleo | viva          # la chispa es del color del hilo: brota de el
    medio |= muere & ~nucleo & ~viva

    if nombre == 'nacimiento':
        # el electrodo: luz densa en el centro del nodo, que el nodo tapa
        r = np.hypot(px - 1.0, py - EJE)
        dens += 0.9 * np.exp(-(r / 4.0) ** 2)
        nucleo &= (np.arange(ancho) >= 5)[None, :]
        # nace fino: los primeros pixeles del hilo en la mitad clara mas baja
        fino = nucleo & (np.arange(ancho) < 10)[None, :]
        nucleo &= ~fino
        claro |= fino

    if nombre == 'punta':
        dens_c, rayos, centro, anillo = _contacto(f, px, py)
        dens = np.maximum(dens, dens_c)
        claro |= rayos | anillo
        blanco |= centro
        nucleo &= ~(anillo | centro)

    # lo que el hilo tapa no se trama
    dens = np.where(nucleo | blanco | claro | medio, 0, dens)
    rgb, alfa = _pintar(np.clip(dens, 0, 1), medio, nucleo, claro, blanco, ancho)
    return rgb, alfa, filas


def _llegada():
    """Frame en que un paquete alcanza el punto de contacto."""
    for f in range(FRAMES):
        for ub in _paquetes(0.0, PUNTA, f):
            if CONTACTO <= ub < CONTACTO + VELOCIDAD:
                return f
    raise RuntimeError('ningun paquete llega a la punta')


LLEGADA = _llegada()


def _contacto(f, px, py):
    """El punto de contacto: lo mas brillante del trazo. Respira con el bucle
    y destella tres frames cuando le llega un paquete."""
    t = _t(f)
    desde = (f - LLEGADA) % FRAMES
    destello = (1.0, 0.55, 0.2)[desde] if desde < 3 else 0.0
    cx, cy = float(CONTACTO), EJE
    r = np.hypot(px - cx, py - cy)
    radio = 3.4 + 0.7 * math.sin(t) + 2.6 * destello
    dens = (0.95 + 0.3 * destello) * np.exp(-(r / radio) ** 1.6)

    centro = (r < 1.0)                       # 2 x 2 blanco
    anillo = (r < 2.0) & ~centro             # cruz clara alrededor
    rayos = np.zeros_like(centro)
    largo = (6, 4, 0)[desde] if desde < 3 else 0
    if largo:
        # cruz ortogonal que se encoge en tres frames
        dx, dy = np.abs(px - cx), np.abs(py - cy)
        rayos |= (dy < 1.0) & (dx < 2.0 + largo)
        rayos |= (dx < 1.0) & (dy < 2.0 + min(largo, 5))
    elif f % 4 == 1:
        # chispa diagonal de reposo, un frame de cada cuatro
        dx, dy = px - cx, py - cy
        rayos |= (np.abs(np.abs(dx) - np.abs(dy)) < 0.6) & (r < 4.3)
    rayos &= ~(anillo | centro)
    return dens, rayos, centro, anillo


# --- Hoja y comprobaciones ---------------------------------------------------

PIEZAS = (('nacimiento', NAC), ('tramo', TESELA), ('punta', PUNTA))


def hoja():
    img = np.zeros((ALTO * 3, CELDA * FRAMES, 4), np.uint8)
    frames = {}
    for fila, (nombre, ancho) in enumerate(PIEZAS):
        for f in range(FRAMES):
            rgb, alfa, filas = pieza(nombre, f)
            frames[nombre, f] = (rgb, alfa, filas)
            y0, x0 = fila * ALTO, f * CELDA
            img[y0:y0 + ALTO, x0:x0 + ancho, :3] = np.where(alfa[..., None], rgb, 0)
            img[y0:y0 + ALTO, x0:x0 + ancho, 3] = np.where(alfa, 255, 0)
    return img, frames


def comprobar(img, frames):
    """Lo que tiene que cumplir el trazo. Falla en vez de avisar."""
    fallos = []
    rgb = img[..., :3][img[..., 3] > 0]
    if not set(np.unique(img[..., 3])) <= {0, 255}:
        fallos.append('alfa no binario')
    usados = {tuple(c) for c in np.unique(rgb, axis=0)}
    if not usados <= {tuple(c) for c in RAMP[:6]}:
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
        nuevos = [s for s, _ in SITIOS[f]]
        antes = [s for s, _ in SITIOS[f - 1]]
        if any(min(abs(a - b), TESELA - abs(a - b)) < DIST_RAMITAS
               for a in nuevos for b in antes):
            fallos.append(f'frame {f}: ramita en el sitio de una del frame anterior')

    if fallos:
        raise SystemExit('ART-WLD-LINK v1 no pasa:\n  ' + '\n  '.join(fallos))
    return distintos, desequilibrio


# --- Composicion: la linea entera, como la montara Unity ---------------------

def linea(frames, largo, f):
    """La linea de `largo` px del centro del nodo al cursor, en el frame f:
    nacimiento en 0, punta con el contacto en `largo`, y el tramo anclado en
    la punta y recortado del lado del nodo. RGBA (ALTO, largo + 8)."""
    ancho = int(largo) + PUNTA - CONTACTO
    out = np.zeros((ALTO, ancho, 4), np.uint8)

    def poner(rgb, alfa, x0):
        for c in range(rgb.shape[1]):
            x = x0 + c
            if 0 <= x < ancho:
                m = alfa[:, c]
                out[m, x, :3] = rgb[m, c]
                out[m, x, 3] = 255

    x_punta = int(largo) - CONTACTO
    rgb_t, a_t, _ = frames['tramo', f]
    x = x_punta - TESELA
    while x + TESELA > NAC:
        lo = max(0, NAC - x)
        poner(rgb_t[:, lo:], a_t[:, lo:], x + lo)
        x -= TESELA
    rgb_n, a_n, _ = frames['nacimiento', f]
    poner(rgb_n, a_n, 0)
    rgb_p, a_p, _ = frames['punta', f]
    poner(rgb_p, a_p, x_punta)
    return out


def rotar_sobre(lienzo, tira, ox, oy, ang):
    """Pega la tira con su origen (x 0, eje) en (ox, oy) y girada `ang`
    radianes, muestreando al vecino mas cercano: lo que hace Unity con
    Filter Mode Point al rotar el transform."""
    H, W = lienzo.shape[:2]
    h, w = tira.shape[:2]
    c, s = math.cos(ang), math.sin(ang)
    r = w + h
    x0, x1 = max(0, int(ox - r)), min(W, int(ox + r))
    y0, y1 = max(0, int(oy - r)), min(H, int(oy + r))
    yy, xx = np.mgrid[y0:y1, x0:x1] + 0.5
    dx, dy = xx - ox, yy - oy
    u = c * dx + s * dy
    v = -s * dx + c * dy + EJE
    ui, vi = np.floor(u).astype(int), np.floor(v).astype(int)
    ok = (ui >= 0) & (ui < w) & (vi >= 0) & (vi < h)
    a = np.zeros_like(ok)
    a[ok] = tira[vi[ok], ui[ok], 3] > 0
    zona = lienzo[y0:y1, x0:x1]
    zona[a] = tira[vi[a], ui[a], :3]


def sprite(ruta):
    im = np.asarray(Image.open(ruta).convert('RGBA'))
    return im


def pegar(lienzo, im, cx, cy):
    h, w = im.shape[:2]
    x0, y0 = int(cx - w // 2), int(cy - h // 2)
    m = im[..., 3] > 0
    lienzo[y0:y0 + h, x0:x0 + w][m] = im[..., :3][m]


def escena(frames, f):
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
        tira = linea(frames, largo, (f + desfase) % FRAMES)
        rotar_sobre(fondo, tira, ox, oy, math.atan2(cy - oy, cx - ox))
    for im, (x, y) in ((s3, (170, 330)), (s2, (120, 140)), (p2, (250, 470)),
                       (s2, (330, 300)), (s3, (760, 420)), (e3, (820, 120)),
                       (a1, (560, 190))):
        pegar(fondo, im, x, y)
    return fondo


def ampliar(im, k):
    return np.repeat(np.repeat(im, k, axis=0), k, axis=1)


def vistas(frames, gif, d):
    # la linea sola, x4, los 16 frames uno debajo de otro, sobre el fondo real
    filas = []
    for f in range(FRAMES):
        t = linea(frames, 360, f)
        bg = np.empty((ALTO + 4, t.shape[1] + 8, 3), np.uint8)
        bg[:] = FONDO
        m = t[..., 3] > 0
        bg[2:2 + ALTO, 4:4 + t.shape[1]][m] = t[..., :3][m]
        filas.append(bg)
    Image.fromarray(ampliar(np.vstack(filas), 3)).save(os.path.join(d, 'link_frames_x3.png'))

    Image.fromarray(escena(frames, 0)).save(os.path.join(d, 'link_tablero.png'))
    Image.fromarray(ampliar(escena(frames, 0)[100:500, 60:620], 2)).save(
        os.path.join(d, 'link_tablero_x2.png'))
    if not gif:
        return
    duracion = round(1000 / 12)
    esc = [Image.fromarray(escena(frames, f)) for f in range(FRAMES)]
    esc[0].save(os.path.join(d, 'link_tablero.gif'), save_all=True,
                append_images=esc[1:], duration=duracion, loop=0)
    zoom = [Image.fromarray(ampliar(np.asarray(e)[100:500, 60:620], 2)) for e in esc]
    zoom[0].save(os.path.join(d, 'link_tablero_x2.gif'), save_all=True,
                 append_images=zoom[1:], duration=duracion, loop=0)
    sola = []
    for f in range(FRAMES):
        t = linea(frames, 360, f)
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
    img, frames = hoja()
    distintos, deseq = comprobar(img, frames)
    destino = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{version}')
    os.makedirs(destino, exist_ok=True)
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'world_link_sheet.png'))
    d = os.path.join(dir_preview(ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    vistas(frames, '--gif' in sys.argv, d)
    print(f'v{version} world_link_sheet.png {img.shape[1]}x{img.shape[0]}  '
          f'frames distintos: nacimiento {distintos["nacimiento"]}, '
          f'tramo {distintos["tramo"]}, punta {distintos["punta"]}  '
          f'simetria {deseq:.1%}  llegada en el frame {LLEGADA}  escala {ESCALA:.3f}')


if __name__ == '__main__':
    generar(VERSION)
