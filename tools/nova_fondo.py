"""NOVA - Primitivas de fondo. Familia exenta de la celda de 96.

Por que vive fuera de `nova_core.py`. Ese modulo implementa el modelo de luz
de un cuerpo dentro de una celda de 96 px: normal fingida de esfera, bandas,
contorno, huella. Aqui no hay cuerpos ni contornos ni huella: hay campos de
densidad de 960x540 px que se resuelven a color por trama. Son dos
familias con reglas distintas, y el contrato ya las separa en su seccion de
Resolucion. Meterlas en el mismo archivo solo compartiria el import.

Lo que SI se comparte, porque es de todo el proyecto: la paleta, la matriz
de Bayer 8x8 y la regla de que un degradado se hace con trama y nunca con
alfa.

EL PRESUPUESTO DE CONTRASTE. Un fondo de este juego tiene un trabajo y solo
uno: no estorbar. La cota no es de gusto, es medible, y es esta:

    la media de luminancia de CUALQUIER ventana de 96x96 px -la celda de un
    nodo- tiene que quedar por debajo del TONO MAS OSCURO DE UN CUERPO DE
    NODO, que es el indice 6 de las rampas de cuerpo

Si el fondo local lo supera, el lado en sombra de un nodo pasa a ser mas
oscuro que el cielo que lo rodea: el nodo deja de leerse como un objeto
iluminado y se lee como un agujero recortado en la imagen. Por debajo, todo
nodo es siempre mas claro que su entorno, mire donde mire la luz.

No es el contorno. La primera version de este modulo puso la cota en la
luminancia de `CONTORNO` (18.1) razonando que el perfilado dejaria de
leerse. Es falso: el contorno esta a 18.1 y el fondo plano a 14.2, asi que
NUNCA fue un perfilado de alto contraste contra el cielo. Su trabajo es
separar piezas que se solapan y dar un canto consistente a la silueta, no
recortarla contra el fondo. Lo que recorta un nodo contra el cielo es su
propio cuerpo, que esta entre tres y diez veces mas claro. Con la cota
puesta en el contorno los tres fondos salian correctos y vacios.

`TOPE` se calcula de la paleta, no se escribe a mano: si manana cambia una
rampa de cuerpo, el presupuesto se mueve solo. `informe()` lo comprueba
sobre la imagen terminada y avisa. Las estrellas se saltan la cota a
proposito: un pixel suelto no mueve la media de 9216 y es la unica luz que
hay aqui.
"""

import numpy as np

from nova_paleta import FONDO, RAMPAS, hex_a_rgb
from nova_core import BAYER8, rampa

ANCHO, ALTO = 960, 540          # 15 x 8.4375 u a PPU 64 (T.8)
CELDA = 96                      # ventana de medida = celda de un nodo

# Rampas con las que se dibuja el CUERPO de un nodo. No entran las de
# estado, poder ni power-up -no forman silueta-, ni `ojo`, que es un detalle
# interior y no toca el cielo, ni las de fondo, que son el cielo.
CUERPOS = ('str_p', 'str_e', 'cor_p', 'cor_e', 'pul_p', 'pul_e',
           'mag_p', 'mag_e', 'mag_n', 'roca_n', 'roca_p', 'roca_e')


def _lum_hex(h):
    r, g, b = hex_a_rgb(h)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


FONDO_LUM = _lum_hex(FONDO)
TOPE = min(_lum_hex(RAMPAS[k][6]) for k in CUERPOS)


def lienzo(w=None, h=None):
    w, h = w or ANCHO, h or ALTO
    return np.tile(np.array(hex_a_rgb(FONDO), np.uint8), (h, w, 1))


def _bayer(h, w):
    return np.tile(BAYER8, (h // 8 + 1, w // 8 + 1))[:h, :w]


def luminancia(img):
    f = img.astype(np.float32)
    return 0.2126 * f[..., 0] + 0.7152 * f[..., 1] + 0.0722 * f[..., 2]


# --- Campos de densidad ----------------------------------------------------

def _valor(rng, h, w, cy, cx, periodico=False):
    """Ruido de valor con interpolacion suave, en [0,1].

    Con `periodico` la columna `w` vuelve a ser la 0: el campo se puede
    deslizar en horizontal sin costura, que es lo que necesita un fondo que
    se mueve.
    """
    g = rng.random((cy + 1, cx if periodico else cx + 1))
    ys = np.linspace(0, cy, h, endpoint=False)
    xs = np.linspace(0, cx, w, endpoint=False)
    y0, x0 = ys.astype(int), xs.astype(int)
    x1 = (x0 + 1) % cx if periodico else x0 + 1
    fy, fx = ys - y0, xs - x0
    fy = fy * fy * (3 - 2 * fy)                  # smoothstep
    fx = fx * fx * (3 - 2 * fx)
    a, b = g[y0][:, x0], g[y0][:, x1]
    c, d = g[y0 + 1][:, x0], g[y0 + 1][:, x1]
    arr = a + (b - a) * fx
    aba = c + (d - c) * fx
    return arr + (aba - arr) * fy[:, None]


def ruido(rng, h=None, w=None, celdas=4, octavas=4, persistencia=0.55,
          cresta=False, periodico=False):
    """fBm en [0,1]. Con `cresta`, filamentos: 1 - |2n-1|, que es lo que
    convierte una mancha en una nube con hebras. Con `periodico`, cierra en
    horizontal: ver `_valor`."""
    h, w = h or ALTO, w or ANCHO
    out = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octavas):
        c = celdas * 2 ** o
        n = _valor(rng, h, w, max(1, round(c * h / w)), c, periodico)
        if cresta:
            n = 1.0 - np.abs(2.0 * n - 1.0)
        out += amp * n
        tot += amp
        amp *= persistencia
    return out / tot


def disco(cy, cx, r, gamma=2.0, h=None, w=None):
    """Caida radial en [0,1]: 1 en el centro, 0 a partir de `r`."""
    h, w = h or ALTO, w or ANCHO
    y, x = np.mgrid[0:h, 0:w]
    d = np.hypot(y - cy, x - cx) / max(r, 1e-6)
    return np.clip(1.0 - d, 0.0, 1.0) ** gamma


def banda(ang, paso_y, sigma, comba=0.0, h=None, w=None):
    """Franja gaussiana inclinada, opcionalmente combada.

    `comba` arquea la franja: es lo que permite que un cinturon cruce la
    imagen sin pasar por el centro, que es donde se juega.
    """
    h, w = h or ALTO, w or ANCHO
    y, x = np.mgrid[0:h, 0:w]
    t = (x - w / 2.0) / (w / 2.0)
    eje = paso_y + np.tan(ang) * (x - w / 2.0) + comba * t * t
    return np.exp(-0.5 * ((y - eje) / max(sigma, 1e-6)) ** 2)


def vineta(k=1.35, suelo=0.25, h=None, w=None):
    """Vineta AL REVES: minima en el centro, maxima en los bordes.

    En casi cualquier juego la vineta oscurece los bordes para llevar la
    mirada al centro. Aqui el centro es el tablero, y lo que hay que llevar
    al centro no es la mirada sino la ATENCION, que es lo contrario: todo lo
    que el fondo tenga que ensenar se va a los bordes, y la zona donde se
    juega se queda lo mas vacia y oscura posible.
    """
    h, w = h or ALTO, w or ANCHO
    y, x = np.mgrid[0:h, 0:w]
    r = np.hypot((y - h / 2.0) / (h / 2.0), (x - w / 2.0) / (w / 2.0)) / 1.4142
    return np.clip(suelo + (1.0 - suelo) * r ** k, 0.0, 1.0)


# --- Salida ----------------------------------------------------------------

def cobertura(ramp, banda, parte=1.0):
    """Fraccion MAXIMA de pixeles que una banda puede ocupar sin pasarse.

    El presupuesto no es una regla de estilo que luego se ajusta a ojo: es
    una ecuacion con una sola incognita. La media de una ventana en la que
    una fraccion `c` de pixeles lleva la banda `b` y el resto el fondo es

        media = c * lum(b) + (1 - c) * lum(fondo)

    e imponer `media <= TOPE` despeja `c` directamente. Para el indice 6 de
    una rampa de fondo salen unos dos tercios de cobertura; para el 5, algo
    mas del diez por ciento; para el 4, la mitad de eso. De ahi sale la
    unica forma que tiene un fondo de NOVA de verse rico sin estorbar: mucha
    trama muy oscura, poca trama media y acentos contados.

    `parte` reparte el presupuesto entre capas que se superponen, para que
    la suma siga cumpliendo la cota y no solo cada una por separado.
    """
    r, g, b = ramp[banda]
    lb = 0.2126 * float(r) + 0.7152 * float(g) + 0.0722 * float(b)
    if lb <= FONDO_LUM:
        return 1.0
    return float(np.clip(parte * (TOPE - FONDO_LUM) / (lb - FONDO_LUM), 0.0, 1.0))


def tramar(img, dens, ramp, capas, mascara=None):
    """Trama por COBERTURA, no por escalera de tonos.

    `capas` es una lista de `(banda, desde, parte)`, de la mas oscura a la
    mas clara. Cada capa enciende como mucho `cobertura(ramp, banda, parte)`
    de los pixeles cuya densidad supera `desde`, y quien decide que pixel se
    enciende es la matriz de Bayer: regla 7 del contrato sobre un lienzo
    grande.

    Por que no una escalera de tonos como en un sprite. En una celda de 96
    px un degradado puede recorrer la rampa entera porque la pieza es
    pequena y va sobre fondo liso. Un fondo ocupa la pantalla: si la
    densidad llega a 1 y la banda cubre el 100 % de una ventana, esa ventana
    YA esta por encima del presupuesto por mucho que la banda sea oscura.
    Aqui lo que se modula no es el tono, es cuantos pixeles se encienden.
    """
    h, w = dens.shape
    umbral = _bayer(h, w)
    d0 = np.clip(dens, 0, 1)
    for banda, desde, parte in capas:
        c = cobertura(ramp, banda, parte)
        d = np.clip((d0 - desde) / max(1e-6, 1.0 - desde), 0, 1) * c
        on = d > umbral
        if mascara is not None:
            on &= mascara
        if on.any():
            img[on] = ramp[banda]


def silueta(img, dens, ramp, banda_filo, hacia=None, umbral=0.02, filo=0.30):
    """Un cuerpo lejano: masa a color de fondo y un filo a la luz.

    Un objeto oscuro sobre un fondo oscuro no se ve por su masa, se ve por
    el filo. Pintar la masa con el propio color de fondo y dejar solo el
    canto iluminado es lo que convierte una mancha en un planeta o en una
    roca: el ojo completa el volumen a partir del borde.
    """
    cuerpo = dens > umbral
    if not cuerpo.any():
        return
    img[cuerpo] = np.array(hex_a_rgb(FONDO), np.uint8)
    canto = cuerpo & (dens < filo)
    if hacia is not None:
        canto &= hacia
    img[canto] = ramp[banda_filo]


def estrellas(img, rng, cuantas, ramp, factor=None, brillo=1.0):
    """Campo de estrellas determinista, en tres escalones de brillo.

    Tres y no un degradado: a 1 px no hay degradado posible, y una estrella
    que dudara entre dos tonos se veria como ruido. Las mas brillantes
    llevan cruz de cuatro pixeles, que es lo unico que las distingue de una
    estrella normal cuando todas miden un pixel.
    """
    h, w = img.shape[:2]
    ys = rng.integers(2, h - 2, cuantas)
    xs = rng.integers(2, w - 2, cuantas)
    if factor is not None:
        vive = rng.random(cuantas) < factor[ys, xs]
        ys, xs = ys[vive], xs[vive]
    tier = rng.random(len(ys))

    debil = tier < 0.70
    media = (tier >= 0.70) & (tier < 0.93)
    fuerte = tier >= 0.93

    b_debil = int(np.clip(round(4 + (1 - brillo) * 1.4), 0, 6))
    b_media = int(np.clip(round(3 - brillo * 0.8), 0, 6))
    img[ys[debil], xs[debil]] = ramp[b_debil]
    img[ys[media], xs[media]] = ramp[b_media]
    for y, x in zip(ys[fuerte], xs[fuerte]):
        img[y, x] = ramp[0]
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            img[y + dy, x + dx] = ramp[3]


def ventanas(img):
    """Media de luminancia de cada ventana de 96x96 px, por suma acumulada."""
    ac = luminancia(img).cumsum(0).cumsum(1)
    ac = np.pad(ac, ((1, 0), (1, 0)))
    k = CELDA
    return (ac[k:, k:] - ac[:-k, k:] - ac[k:, :-k] + ac[:-k, :-k]) / (k * k)


def informe(nombre, img):
    """Comprueba el presupuesto de contraste sobre la imagen terminada."""
    lum = luminancia(img)
    peor = ventanas(img).max()
    colores = len(np.unique(img.reshape(-1, 3), axis=0))
    estado = 'OK ' if peor <= TOPE else 'AVISO'
    print(f'{estado} {nombre:14} media {lum.mean():5.2f}  '
          f'peor ventana 96x96 {peor:5.2f} / {TOPE:.1f}  '
          f'margen {TOPE - peor:+5.2f}  colores {colores}')
    return peor <= TOPE
