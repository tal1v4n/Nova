"""NOVA - Generador del anillo de seleccion (ART-WLD-SEL), v1: LA ORBITA.

Ref: NOVA_Assets_Diseno.md A.4.3 · NOVA_GameDesign_Spec.md 13.1 · 14.7

    .venv\\Scripts\\python tools\\gen_select_v1.py          las cinco tallas
    .venv\\Scripts\\python tools\\gen_select_v1.py --vista  ademas, las vistas en
                                                         art/_preview/select/v1/

QUE TIENE QUE DECIR. «Este nodo es tuyo y esta listo: de aqui salen tus
envios.» Se ve sobre los nodos propios seleccionados, que con un lazo pueden
ser seis a la vez, y dura lo que dure la seleccion: segundos o minutos. Va en
la capa 0 de 14.7, DETRAS del nodo, y asoma por fuera del sprite.

CON QUE NO SE PUEDE CONFUNDIR, medido en el tablero:

    anillo del Magnetar   linea de 1 px a medio tono, quieta, en `mag_p`, de
                          96 a 160 px de radio. Este es pequeno (24-40 px),
                          entero y del cian vivo del Jugador (`str_p`)
    `ART-ST-CAP`          dos anillos blancos que se cruzan, 0.4 s y se
                          acaban. Este no llega al blanco y no se acaba
    `ART-WLD-AIM`         la reticula del apuntado, que va SOBRE un objetivo
                          ajeno. Este va DETRAS de un nodo propio

LA PROPUESTA. Una orbita tenue alrededor del nodo y tres cometas que la
recorren en el sentido de las agujas del reloj, un tercio de vuelta por ciclo.
Tres y no dos ni cuatro: con dos se lee un eje, con cuatro una cruz, y las dos
son reticulas. Con tres y en movimiento no hay eje. Y es el mismo gesto del
proton -los tres quarks girando dentro de su bolsa, A.4.2-: un nodo
seleccionado es un nodo cargado, a punto de soltar uno.

ABIERTO POR ABAJO. El numero de Energia va justo debajo del sprite (18 #48):
de la media huella + 2 px a 20 px mas abajo, y hasta 34 px de ancho con tres
cifras. Un anillo cerrado le pasaria por detras y su luz asomaria entre los
trazos de las cifras, en la informacion que mas se lee de la partida (14.7:
el numero nunca se tapa). Asi que la orbita no se dibuja en esa franja, y los
cometas pasan por detras del numero: desaparecen por un lado y salen por el
otro.

TALLAS. El sprite no se escala (pixel-perfect), asi que son cinco anillos,
como las tallas del proton. El radio libre de cada uno es la media huella del
nodo mas 4 px de aire (`AIRE`), redondeado hacia arriba a la talla siguiente:

    talla   radio libre   nodos
    24      24 px         Estrella N1
    28      28 px         Magnetar N1, Pulsar N1
    32      32 px         Estrella N2
    36      36 px         Estrella N3, Pulsar N2, Magnetar N2-N3
    40      40 px         Estrella N4-N5, Pulsar N3, Magnetar N4-N5

Unity elige la talla con la huella que ya exporta cada nodo, igual que el
proton con su Carga: `world_select_tallas.json` lleva los radios y el aire.
La mayor mide 86 px de lado: dos nodos a la separacion minima del tablero
(1.5 u, 96 px) no se tocan.
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, rampa

ASSET = 'select'
VERSION = 1

CELDA = 96
C = CELDA / 2.0          # centro en esquina de pixel, como los nodos
PPU = 64
FRAMES = 16

TALLAS = (24, 28, 32, 36, 40)   # radio libre, px
AIRE = 4                        # px entre la media huella y el anillo

# La franja del numero (NodeLayout): tres cifras de Jersey 10 a 2x miden 34 px,
# mas 2 de margen por lado. Todo lo que caiga por debajo del centro con
# |x| <= 19 no se dibuja.
NUMERO_MEDIO_ANCHO = 19

RAMPA = 'str_p'
ORBITA = 4               # banda de la orbita: media, se ve a 1x sin gritar
COMETAS = 3
VUELTA = 1.0 / COMETAS   # vueltas por ciclo: un tercio, y el bucle cierra
FASE0 = math.radians(-60.0)

# Cometa: cabeza de 2 px de grueso y cola a lo largo de la orbita, en px de
# arco detras de la cabeza -> banda
CABEZA = 3.0             # medio largo de la cabeza, px de arco
# (largo, banda, gruesa): el primer tramo de la cola va a 2 px, como la cabeza
COLA = ((8.0, 2, True), (16.0, 3, False))


def _malla():
    y, x = np.mgrid[0:CELDA, 0:CELDA]
    dx, dy = x + 0.5 - C, y + 0.5 - C
    return dx, dy, np.hypot(dx, dy), np.arctan2(dy, dx)


def franja_numero(dx, dy):
    """Lo que tapa el numero de Energia: debajo del centro y en su ancho."""
    return (dy > 0) & (np.abs(dx) <= NUMERO_MEDIO_ANCHO)


def anillo(R, t):
    """RGBA (CELDA, CELDA) de la talla R en la fase t de [0, 1)."""
    ramp = rampa(RAMPA)
    dx, dy, d, ang = _malla()
    out = np.zeros((CELDA, CELDA, 4), np.uint8)
    libre = ~franja_numero(dx, dy)

    def poner(m, banda):
        m = m & libre
        out[m, :3] = ramp[banda]
        out[m, 3] = 255

    orbita = (d > R) & (d <= R + 1)
    grueso = (d > R - 0.5) & (d <= R + 1.5)
    poner(orbita, ORBITA)

    # Arco detras de cada cabeza, en px medidos sobre la orbita. Positivo es
    # hacia atras (la cola); el giro es horario en pantalla, angulo creciente.
    radio_medio = R + 0.5
    capas = []
    for k in range(COMETAS):
        cabeza = FASE0 + 2 * math.pi * (k / COMETAS + VUELTA * t)
        atras = ((cabeza - ang) % (2 * math.pi)) * radio_medio
        delante = ((ang - cabeza) % (2 * math.pi)) * radio_medio
        capas.append((atras, delante))
    # la cola, de la mas tenue a la mas clara, y la cabeza encima
    for largo, banda, gruesa in reversed(COLA):
        m = np.zeros_like(orbita)
        for atras, _ in capas:
            m |= (grueso if gruesa else orbita) & (atras <= largo)
        poner(m, banda)
    m = np.zeros_like(orbita)
    for atras, delante in capas:
        m |= grueso & ((atras <= CABEZA) | (delante <= CABEZA * 0.5))
    poner(m, 1)
    return out


def hoja(R):
    return np.concatenate([anillo(R, f / FRAMES) for f in range(FRAMES)], 1)


def verifica(R, frames):
    """Lo que no se negocia: alfa binario, dentro de la celda, fuera del
    radio libre, fuera de la franja del numero, bucle cerrado y 16 distintos."""
    dx, dy, d, _ = _malla()
    for i, f in enumerate(frames):
        a = f[..., 3]
        if not np.isin(a, (0, 255)).all():
            raise SystemExit(f'T{R} f{i}: alfa no binario')
        on = a > 0
        if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
            raise SystemExit(f'T{R} f{i}: toca el borde de la celda')
        if (on & (d <= R - 0.5)).any():
            raise SystemExit(f'T{R} f{i}: entra en el radio libre')
        if (on & franja_numero(dx, dy)).any():
            raise SystemExit(f'T{R} f{i}: pisa la franja del numero')
    if not np.array_equal(anillo(R, 0.0), anillo(R, 1.0)):
        raise SystemExit(f'T{R}: el bucle no cierra')
    distintos = len({f.tobytes() for f in frames})
    if distintos != len(frames):
        raise SystemExit(f'T{R}: {distintos} frames distintos de {len(frames)}')
    return distintos


def ruta(raiz, R, sufijo='_sheet'):
    return os.path.join(raiz, f'world_select_{R}{sufijo}.png')


def exporta_tallas(raiz):
    with open(os.path.join(raiz, 'world_select_tallas.json'), 'w', encoding='utf-8') as f:
        json.dump({'ppu': PPU, 'aire': AIRE, 'radios': list(TALLAS)}, f, indent=2)


def talla_de(huella):
    """La talla que le toca a un nodo de esa huella: la primera que deja aire."""
    libre = (huella + 1) // 2 + AIRE
    for R in TALLAS:
        if R >= libre:
            return R
    return TALLAS[-1]


# --- Vistas ---------------------------------------------------------------

NODOS = (  # (carpeta, version, prefijo, niveles)
    ('estrella', 1, 'str', range(1, 6)),
    ('pulsar', 2, 'pul', range(1, 4)),
    ('magnetar', 1, 'mag', range(1, 6)),
)


def _sprite(carpeta, version, pre, n, fac='player', frame=None):
    base = os.path.join(RAIZ, 'art', 'nodes', carpeta, f'v{version}', f'{pre}_lvl{n}_{fac}')
    if frame is not None and os.path.exists(base + '_sheet.png'):
        h = np.asarray(Image.open(base + '_sheet.png').convert('RGBA'))
        return h[:, frame * 96:(frame + 1) * 96]
    return np.asarray(Image.open(base + '.png').convert('RGBA'))


def _huella(carpeta, version, pre, n):
    with open(os.path.join(RAIZ, 'art', 'nodes', carpeta, f'v{version}', f'{pre}_huella.json')) as f:
        j = json.load(f)
    return j['footprintPixels'][j['levels'].index(n)]


def _sobre(lienzo, rgba, cx, cy):
    h, w = rgba.shape[:2]
    x0, y0 = cx - w // 2, cy - h // 2
    x1, y1 = max(x0, 0), max(y0, 0)
    x2, y2 = min(x0 + w, lienzo.shape[1]), min(y0 + h, lienzo.shape[0])
    sub = rgba[y1 - y0:y2 - y0, x1 - x0:x2 - x0]
    m = sub[..., 3] > 0
    lienzo[y1:y2, x1:x2][m] = sub[..., :3][m]


def _numero(lienzo, cx, cy, huella, texto):
    """El numero de Energia como lo pone NodeLayout, con las cifras del
    proton a 2x en el claro de la UI: solo para juzgar el hueco."""
    ruta_c = os.path.join(RAIZ, 'art', 'world', 'proton', 'v2', 'world_charge_digits.png')
    if not os.path.exists(ruta_c):
        return
    dig = np.asarray(Image.open(ruta_c).convert('RGBA'))
    with open(ruta_c.replace('.png', '.json'), encoding='utf-8') as f:
        meta = json.load(f)
    ancho, avance = meta['cellWidth'], meta['advance']
    cifras = [int(ch) for ch in texto]
    total = 2 * (sum(avance[i] for i in cifras[:-1]) + ancho)
    y = cy + (huella + 1) // 2 + 2
    x = cx - total // 2
    for i in cifras:
        g = np.repeat(np.repeat(dig[:, i * ancho:(i + 1) * ancho], 2, 0), 2, 1)
        m = g[..., 3] > 0
        sub = lienzo[y:y + g.shape[0], x:x + g.shape[1]]
        if sub.shape[:2] == m.shape:
            sub[m] = g[..., :3][m]
        x += 2 * avance[i]


def vistas(gen=None, version=VERSION, frames=FRAMES):
    """Los trece nodos seleccionables con su talla, y un tablero de partida.

    `gen(R, t)` es el anillo: la otra version la reutiliza con el suyo.
    """
    gen = gen or anillo
    from nova_paleta import FONDO, hex_a_rgb
    bg = np.array(hex_a_rgb(FONDO), np.uint8)
    d = os.path.join(RAIZ, 'art', '_preview', ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)

    # 1. Los trece, con su talla y su numero, a 3x: frame 0 y frame 5
    filas = []
    for f in sorted({0, min(5, frames - 1)}):
        celdas = []
        for carpeta, v, pre, niveles in NODOS:
            for n in niveles:
                hu = _huella(carpeta, v, pre, n)
                c = np.empty((112, 96, 3), np.uint8)
                c[:] = bg
                _sobre(c, gen(talla_de(hu), f / frames), 48, 48)
                _sobre(c, _sprite(carpeta, v, pre, n, frame=0), 48, 48)
                _numero(c, 48, 48, hu, '25')
                celdas.append(c)
        filas.append(np.concatenate(celdas, 1))
    img = np.concatenate(filas, 0)
    Image.fromarray(np.repeat(np.repeat(img, 3, 0), 3, 1), 'RGB').save(
        os.path.join(d, 'select_tallas_x3.png'))

    # 2. Un tablero de partida sobre el fondo real: cuatro nodos propios
    #    seleccionados (uno, un Magnetar con su radio), y ajenos sin anillo.
    fondo = os.path.join(RAIZ, 'art', 'bg', 'fondos', 'v2', 'ref', 'bg_section1_ref0.png')
    escena = [  # (carpeta, v, pre, n, fac, x, y, seleccionado, numero)
        ('estrella', 1, 'str', 3, 'player', 300, 300, True, '31'),
        ('estrella', 1, 'str', 5, 'player', 420, 200, True, '48'),
        ('pulsar', 2, 'pul', 2, 'player', 200, 180, True, '12'),
        ('magnetar', 1, 'mag', 4, 'player', 470, 380, True, '60'),
        ('estrella', 1, 'str', 2, 'player', 160, 400, False, '17'),
        ('estrella', 1, 'str', 4, 'enemy', 720, 220, False, '40'),
        ('magnetar', 1, 'mag', 3, 'neutral', 640, 400, False, '75'),
        ('pulsar', 2, 'pul', 1, 'enemy', 820, 380, False, '9'),
    ]
    radio_mag = os.path.join(RAIZ, 'art', 'world', 'radius', 'v2', 'world_radius_{}_lvl{}.png')

    def tablero(f):
        if os.path.exists(fondo):
            tab = np.asarray(Image.open(fondo).convert('RGB')).copy()
        else:
            tab = np.empty((540, 960, 3), np.uint8)
            tab[:] = bg
        for carpeta, v, pre, n, fac, x, y, sel, num in escena:
            if pre == 'mag':
                _sobre(tab, np.asarray(Image.open(radio_mag.format(fac, n)).convert('RGBA')), x, y)
        for carpeta, v, pre, n, fac, x, y, sel, num in escena:
            if sel:
                _sobre(tab, gen(talla_de(_huella(carpeta, v, pre, n)), f / frames), x, y)
        for carpeta, v, pre, n, fac, x, y, sel, num in escena:
            _sobre(tab, _sprite(carpeta, v, pre, n, fac, frame=f % 16), x, y)
            _numero(tab, x, y, _huella(carpeta, v, pre, n), num)
        return tab

    t0 = tablero(0)
    Image.fromarray(t0, 'RGB').save(os.path.join(d, 'select_tablero.png'))
    Image.fromarray(np.repeat(np.repeat(t0, 2, 0), 2, 1), 'RGB').save(
        os.path.join(d, 'select_tablero_x2.png'))
    if frames > 1:
        cuadros = [Image.fromarray(np.repeat(np.repeat(tablero(f)[100:460, 100:560], 2, 0), 2, 1), 'RGB')
                   for f in range(frames)]
        cuadros[0].save(os.path.join(d, 'select_tablero_x2.gif'), save_all=True,
                        append_images=cuadros[1:], duration=83, loop=0)
    print('vistas en', d)


def main():
    raiz = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    os.makedirs(raiz, exist_ok=True)
    print(f"{'talla':>6} {'frames':>7} {'pixeles (f0)':>13}")
    for R in TALLAS:
        frames = [anillo(R, f / FRAMES) for f in range(FRAMES)]
        n = verifica(R, frames)
        Image.fromarray(np.concatenate(frames, 1), 'RGBA').save(ruta(raiz, R))
        print(f'{R:6d} {n:7d} {int((frames[0][..., 3] > 0).sum()):13d}')
    exporta_tallas(raiz)
    if '--vista' in sys.argv:
        vistas()


if __name__ == '__main__':
    main()
