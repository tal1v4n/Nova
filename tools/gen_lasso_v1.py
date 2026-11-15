"""NOVA - Generador del lazo de seleccion (ART-WLD-LASSO), v1: EL MARCO.

Ref: NOVA_Assets_Diseno.md A.4.3 · NOVA_GameDesign_Spec.md 13.1, 13.12

    .venv\\Scripts\\python tools\\gen_lasso_v1.py          las piezas
    .venv\\Scripts\\python tools\\gen_lasso_v1.py --vista  ademas, las vistas en
                                                        art/_preview/lasso/v1/

QUE ES. El rectangulo del arrastre izquierdo (13.1): va del punto donde se
pulso al cursor y vive lo que dura el gesto, menos de un segundo casi
siempre. Al soltar, los nodos propios que pueden enviar y tienen el centro
dentro quedan seleccionados y lo que se ve pasa a ser «La orbita»
(`ART-WLD-SEL`).

NO TIENE TAMANO, ASI QUE ES PIEZAS. Como la linea de envio (A.4.6), el lazo
se monta en Unity con dos piezas:

    esquina   8 x 8, la de arriba a la izquierda. Las otras tres son la misma
              reflejada, y reflejar es exacto al pixel
    tramo     8 x 1, que se repite en mosaico por cada lado. Los lados
              verticales son el mismo tramo girado 90 grados, tambien exacto

`monta(ancho, alto, frame)` es la referencia de ese montaje: lo que dibuja es
lo que Unity tiene que reproducir, y es lo que usan las vistas.

LA PROPUESTA. Lenguaje de la interfaz (13.12), porque el lazo es interfaz:
cian `STR_P[3]`, «lo que el jugador puede hacer», y la esquina en escalera de
los marcos, que a la escala del arte (la UI va a x2) son 2 + 2 px. Los lados
son trazos de 5 px con huecos de 3 que MARCHAN en sentido horario, un pixel
por frame a 12 fps, con la punta de cada trazo una banda mas clara: dice
«en curso, todavia no has soltado». Las esquinas no se mueven y van una banda
mas claras que los lados: son las que fijan el rectangulo cuando el cursor se
mueve rapido.

Cada lado arranca su mosaico en la esquina de la que sale la marcha -arriba
desde la izquierda, la derecha desde arriba, abajo desde la derecha, la
izquierda desde abajo-, asi que los trazos salen siempre enteros de una
esquina y el recorte cae en la otra.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, rampa

ASSET = 'lasso'
VERSION = 1

RAMPA = 'str_p'
ESQUINA = 8           # lado de la pieza de esquina
BRAZO = 6             # la esquina dibuja sus brazos hasta aqui (exclusivo)
PERIODO = 8           # px del tramo: trazo + hueco
TRAZO = 5
FRAMES = PERIODO      # un pixel por frame: el bucle es el periodo
B_ESQUINA, B_TRAZO, B_PUNTA = 2, 3, 2


def esquina():
    """RGBA (8, 8): la de arriba a la izquierda, con la escalera de 2 + 2."""
    ramp = rampa(RAMPA)
    out = np.zeros((ESQUINA, ESQUINA, 4), np.uint8)
    pts = [(0, x) for x in range(2, BRAZO)] + [(1, 1)] + [(y, 0) for y in range(2, BRAZO)]
    for y, x in pts:
        out[y, x, :3] = ramp[B_ESQUINA]
        out[y, x, 3] = 255
    return out


def tramo(f):
    """RGBA (1, PERIODO) del tramo horizontal en el frame f, marchando a +x."""
    ramp = rampa(RAMPA)
    out = np.zeros((1, PERIODO, 4), np.uint8)
    for k in range(PERIODO):
        i = (k - f) % PERIODO
        if i < TRAZO:
            out[0, k, :3] = ramp[B_PUNTA if i == TRAZO - 1 else B_TRAZO]
            out[0, k, 3] = 255
    return out


def _poner(lienzo, y, x, rgba):
    if rgba[3]:
        lienzo[y, x] = rgba


def monta(ancho, alto, f, tramo_fn=tramo, esquina_fn=esquina, relleno_fn=None):
    """El rectangulo entero, RGBA (alto, ancho), como lo monta Unity."""
    out = np.zeros((alto, ancho, 4), np.uint8)
    if relleno_fn is not None:
        relleno_fn(out)
    t = tramo_fn(f)[0]
    # lados: arriba de izquierda a derecha, derecha de arriba abajo, abajo de
    # derecha a izquierda, izquierda de abajo arriba. Cada uno empieza su
    # mosaico en la esquina de la que sale.
    for k in range(max(0, ancho - 2 * BRAZO)):
        _poner(out, 0, BRAZO + k, t[k % PERIODO])
        _poner(out, alto - 1, ancho - 1 - BRAZO - k, t[k % PERIODO])
    for k in range(max(0, alto - 2 * BRAZO)):
        _poner(out, BRAZO + k, ancho - 1, t[k % PERIODO])
        _poner(out, alto - 1 - BRAZO - k, 0, t[k % PERIODO])
    # esquinas reflejadas, recortadas a medio rectangulo si es pequeno
    e = esquina_fn()
    mw, mh = min(ESQUINA, (ancho + 1) // 2), min(ESQUINA, (alto + 1) // 2)
    for fy in (False, True):
        for fx in (False, True):
            p = e[:mh, :mw]
            if fy:
                p = p[::-1]
            if fx:
                p = p[:, ::-1]
            y0 = alto - mh if fy else 0
            x0 = ancho - mw if fx else 0
            sub = out[y0:y0 + mh, x0:x0 + mw]
            m = p[..., 3] > 0
            sub[m] = p[m]
    return out


def verifica():
    for f in range(FRAMES):
        t = tramo(f)
        if not np.isin(t[..., 3], (0, 255)).all():
            raise SystemExit('alfa no binario')
    if not np.array_equal(tramo(0), tramo(FRAMES)):
        raise SystemExit('el bucle no cierra')
    distintos = len({tramo(f).tobytes() for f in range(FRAMES)})
    if distintos != FRAMES:
        raise SystemExit(f'{distintos} frames distintos de {FRAMES}')
    # desde 12 px de lado, los cuatro lados se ven en todos los frames; por
    # debajo es un lazo degenerado, mas pequeno que el umbral de arrastre
    for w, h in ((12, 12), (13, 20), (20, 13), (64, 40), (301, 97)):
        for f in range(FRAMES):
            r = monta(w, h, f)[..., 3] > 0
            if not (r[0].any() and r[-1].any() and r[:, 0].any() and r[:, -1].any()):
                raise SystemExit(f'{w}x{h} f{f}: falta un lado')
    return distintos


def guarda(raiz, tramo_fn, frames, esquina_fn):
    os.makedirs(raiz, exist_ok=True)
    Image.fromarray(esquina_fn(), 'RGBA').save(os.path.join(raiz, 'world_lasso_corner.png'))
    hoja = np.concatenate([tramo_fn(f) for f in range(frames)], 1)
    nombre = 'world_lasso_edge_sheet.png' if frames > 1 else 'world_lasso_edge.png'
    Image.fromarray(hoja, 'RGBA').save(os.path.join(raiz, nombre))


# --- Vistas ---------------------------------------------------------------

def vistas(monta_fn=None, version=VERSION, frames=FRAMES):
    """El lazo sobre un tablero de partida, a 1x y a 2x, y en GIF si se mueve."""
    import gen_select_v1 as SEL
    monta_fn = monta_fn or monta
    from nova_paleta import FONDO, hex_a_rgb
    bg = np.array(hex_a_rgb(FONDO), np.uint8)
    d = os.path.join(RAIZ, 'art', '_preview', ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)
    fondo = os.path.join(RAIZ, 'art', 'bg', 'fondos', 'v2', 'ref', 'bg_section1_ref0.png')
    escena = [  # (carpeta, v, pre, n, fac, x, y, numero)
        ('estrella', 1, 'str', 3, 'player', 300, 300, '31'),
        ('estrella', 1, 'str', 5, 'player', 420, 200, '48'),
        ('pulsar', 2, 'pul', 2, 'player', 200, 180, '12'),
        ('magnetar', 1, 'mag', 4, 'player', 470, 380, '60'),
        ('estrella', 1, 'str', 2, 'player', 160, 400, '17'),
        ('estrella', 1, 'str', 4, 'enemy', 720, 220, '40'),
        ('asteroide', 1, 'ast', 0, 'player', 600, 330, '6'),
    ]
    # el gesto: de (250, 140) a un cursor que viaja hasta (520, 440)
    x0, y0 = 250, 140

    def tablero(f, x1, y1):
        if os.path.exists(fondo):
            tab = np.asarray(Image.open(fondo).convert('RGB')).copy()
        else:
            tab = np.empty((540, 960, 3), np.uint8)
            tab[:] = bg
        r = monta_fn(x1 - x0 + 1, y1 - y0 + 1, f % frames)
        m = r[..., 3] > 0
        detras = getattr(monta_fn, 'detras', None)
        if detras is not None:   # el velo va detras de los nodos
            rv = detras(x1 - x0 + 1, y1 - y0 + 1)
            mv = rv[..., 3] > 0
            tab[y0:y1 + 1, x0:x1 + 1][mv] = rv[..., :3][mv]
        for carpeta, v, pre, n, fac, x, y, num in escena:
            SEL._sobre(tab, SEL._sprite(carpeta, v, pre, n, fac, frame=f % 16), x, y)
            SEL._numero(tab, x, y, SEL._huella(carpeta, v, pre, n) if pre != 'ast' else 43, num)
        tab[y0:y1 + 1, x0:x1 + 1][m] = r[..., :3][m]
        return tab

    t = tablero(0, 520, 440)
    Image.fromarray(t, 'RGB').save(os.path.join(d, 'lasso_tablero.png'))
    Image.fromarray(np.repeat(np.repeat(t, 2, 0), 2, 1), 'RGB').save(os.path.join(d, 'lasso_tablero_x2.png'))
    # piezas y montajes pequenos a 8x
    piezas = np.zeros((40, 120, 3), np.uint8)
    piezas[:] = bg
    for i, (w, h) in enumerate(((6, 6), (14, 10), (30, 24))):
        r = monta_fn(w, h, 0)
        m = r[..., 3] > 0
        x = 4 + sum((6, 14, 30)[:i]) + 8 * i
        piezas[8:8 + h, x:x + w][m] = r[..., :3][m]
    Image.fromarray(np.repeat(np.repeat(piezas, 8, 0), 8, 1), 'RGB').save(os.path.join(d, 'lasso_piezas_x8.png'))
    # el gesto en GIF: el cursor arrastra durante 24 frames
    cuadros = []
    for f in range(24):
        a = (f + 1) / 24
        x1, y1 = int(round(x0 + 12 + a * 258)), int(round(y0 + 8 + a * 292))
        cuadros.append(Image.fromarray(np.repeat(np.repeat(tablero(f, x1, y1)[100:480, 120:580], 2, 0), 2, 1), 'RGB'))
    cuadros[0].save(os.path.join(d, 'lasso_gesto_x2.gif'), save_all=True,
                    append_images=cuadros[1:], duration=83, loop=0)
    print('vistas en', d)


def main():
    n = verifica()
    raiz = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    guarda(raiz, tramo, FRAMES, esquina)
    print(f'esquina {ESQUINA}x{ESQUINA}, tramo {PERIODO}x1 en {n} frames distintos')
    if '--vista' in sys.argv:
        vistas()


if __name__ == '__main__':
    main()
