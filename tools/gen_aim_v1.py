"""NOVA - Generador del cursor de apuntado (ART-WLD-AIM), v1: EL CEPO.

Ref: NOVA_Assets_Diseno.md A.4.3 · NOVA_GameDesign_Spec.md 13.5, 7.2.1 · 14.4

    .venv\\Scripts\\python tools\\gen_aim_v1.py          las quince hojas
    .venv\\Scripts\\python tools\\gen_aim_v1.py --vista  ademas, las vistas en
                                                      art/_preview/aim/v1/

QUE ES. En modo apuntado (13.5) el jugador tiene un poder cargado y busca a
quien lanzarlo. No hay alcance que dibujar: los objetivos validos -nodos
encendidos que no son suyos, los del Enemigo y el Magnetar neutral (7.2.1)-
se resaltan y lo demas se atenua, y eso es codigo, no arte. Lo que pone este
asset es la RETICULA: aparece sobre el objetivo valido que hay bajo el
cursor, y sobre nada mas.

LA PROPUESTA. Tres puntas de flecha que apuntan al nodo, arriba, a la
izquierda y a la derecha, por fuera del cuerpo y por ENCIMA del nodo. No hay
cuarta: abajo va el numero de Energia (18 #48), y la abertura es la misma que
deja «La orbita». Tampoco van en diagonal: la esquina de abajo a la derecha es
del galon del buff (A.3.8), y el Enemigo tambien recoge power-ups.

    color      el del PODER que se va a lanzar, no el del Jugador: hielo,
               decaimiento o gusano (14.4), el mismo que su haz. Dice que va a
               caer, no solo donde
    contorno   1 px de `CONTORNO`, como los glifos: tiene que leerse sobre el
               magenta de un nodo enemigo, sobre el gris del Magnetar neutral y
               sobre el cielo
    entrada    al posarse en un objetivo las puntas llegan desde fuera en tres
               pasos -5, 2 y 1 px- y en el cuarto frame se quedan clavadas.
               Sale rapido y se posa, la curva orbital de 13.12 hecha a pasos
               de pixel entero. Despues no se mueve: el apuntado ya se esta
               moviendo con el raton, y una reticula que late encima de un
               nodo que tambien late es ruido

Cuatro frames por hoja: los tres de la entrada y el de reposo. Unity
reproduce la entrada cada vez que la reticula cambia de objetivo y se queda
en el ultimo.

TALLAS. Las cinco de `ART-WLD-SEL` (24 a 40 px de radio libre), elegidas con
la misma huella y el mismo `world_select_tallas.json`: la punta queda a 1 px
del radio libre. Con la entrada desde fuera, la talla 40 se sale de la celda
de 96, asi que esta familia va en celda de 112, centrada. Tres poderes por
cinco tallas: quince hojas.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, rampa
from nova_paleta import CONTORNO, hex_a_rgb

import gen_select_v1 as SEL

ASSET = 'aim'
VERSION = 1

CELDA = 112
C = CELDA / 2.0
TALLAS = SEL.TALLAS
PODERES = {'zero': 'hielo', 'decay': 'decaimiento', 'wormhole': 'gusano'}

ALTO = 7               # de la punta a la base, px
MEDIA_BASE = 5.0       # media anchura de la base, px
HUECO = 1              # px entre el radio libre y la punta
ENTRADA = (5, 2, 1, 0) # px de mas hacia fuera, frame a frame; el ultimo es el reposo
B_CARA, B_LUZ = 2, 1   # banda del cuerpo de la punta y de su cara de dentro

# direcciones: arriba, izquierda, derecha (x, y), hacia fuera del nodo
DIRECCIONES = ((0, -1), (-1, 0), (1, 0))


def _malla():
    y, x = np.mgrid[0:CELDA, 0:CELDA]
    return x + 0.5 - C, y + 0.5 - C


def _punta(u, v, R, extra):
    """Mascara de una punta en la direccion (u, v), con `extra` px de mas."""
    dx, dy = _malla()
    a = dx * u + dy * v                 # a lo largo, hacia fuera
    b = -dx * v + dy * u                # de lado
    r0 = R + HUECO + extra              # la punta
    t = (a - r0) / ALTO                 # 0 en la punta, 1 en la base
    return (t >= 0) & (t <= 1) & (np.abs(b) <= MEDIA_BASE * t + 0.5)


def reticula(poder, R, f):
    ramp = rampa(PODERES[poder])
    out = np.zeros((CELDA, CELDA, 4), np.uint8)
    cuerpo = np.zeros((CELDA, CELDA), bool)
    luz = np.zeros_like(cuerpo)
    for u, v in DIRECCIONES:
        m = _punta(u, v, R, ENTRADA[f])
        cuerpo |= m
        luz |= m & ~_punta(u, v, R - 1.5, ENTRADA[f])   # la franja de la punta, la que mira al nodo
    # contorno: lo que toca el cuerpo por los cuatro lados y no es cuerpo
    borde = np.zeros_like(cuerpo)
    borde[1:] |= cuerpo[:-1]
    borde[:-1] |= cuerpo[1:]
    borde[:, 1:] |= cuerpo[:, :-1]
    borde[:, :-1] |= cuerpo[:, 1:]
    borde &= ~cuerpo
    out[borde, :3] = hex_a_rgb(CONTORNO)
    out[cuerpo, :3] = ramp[B_CARA]
    out[luz & cuerpo, :3] = ramp[B_LUZ]
    out[borde | cuerpo, 3] = 255
    return out


def hoja(poder, R):
    return np.concatenate([reticula(poder, R, f) for f in range(len(ENTRADA))], 1)


def verifica(poder, R):
    dx, dy = _malla()
    d = np.hypot(dx, dy)
    frames = [reticula(poder, R, f) for f in range(len(ENTRADA))]
    for i, fr in enumerate(frames):
        on = fr[..., 3] > 0
        if not np.isin(fr[..., 3], (0, 255)).all():
            raise SystemExit(f'{poder} T{R} f{i}: alfa no binario')
        if on[0].any() or on[-1].any() or on[:, 0].any() or on[:, -1].any():
            raise SystemExit(f'{poder} T{R} f{i}: toca el borde de la celda')
        if (on & (d <= R - 0.5)).any():
            raise SystemExit(f'{poder} T{R} f{i}: entra en el radio libre')
        if (on & (dy > 0) & (np.abs(dx) <= SEL.NUMERO_MEDIO_ANCHO)).any():
            raise SystemExit(f'{poder} T{R} f{i}: pisa la franja del numero')
    if len({fr.tobytes() for fr in frames}) != len(frames):
        raise SystemExit(f'{poder} T{R}: hay frames repetidos')
    return frames


def ruta(raiz, poder, R):
    return os.path.join(raiz, f'world_aim_{poder}_{R}_sheet.png')


# --- Vistas ---------------------------------------------------------------

def vistas():
    from nova_paleta import FONDO
    bg = np.array(hex_a_rgb(FONDO), np.uint8)
    d = os.path.join(RAIZ, 'art', '_preview', ASSET, f'v{VERSION}')
    os.makedirs(d, exist_ok=True)

    # 1. Los objetivos posibles con su talla, a 3x: Estrellas y Pulsares
    #    enemigos, y Magnetares enemigos y neutrales, con los tres poderes.
    objetivos = [('estrella', 1, 'str', n, 'enemy') for n in range(1, 6)] + \
                [('pulsar', 2, 'pul', n, 'enemy') for n in range(1, 4)] + \
                [('magnetar', 1, 'mag', n, 'neutral') for n in range(1, 6)]
    filas = []
    for poder in PODERES:
        celdas = []
        for carpeta, v, pre, n, fac in objetivos:
            c = np.empty((CELDA + 8, CELDA, 3), np.uint8)
            c[:] = bg
            hu = SEL._huella(carpeta, v, pre, n)
            SEL._sobre(c, SEL._sprite(carpeta, v, pre, n, fac, frame=0), CELDA // 2, CELDA // 2)
            SEL._numero(c, CELDA // 2, CELDA // 2, hu, '25')
            SEL._sobre(c, reticula(poder, SEL.talla_de(hu), len(ENTRADA) - 1), CELDA // 2, CELDA // 2)
            celdas.append(c)
        filas.append(np.concatenate(celdas, 1))
    img = np.concatenate(filas, 0)
    Image.fromarray(np.repeat(np.repeat(img, 3, 0), 3, 1), 'RGB').save(os.path.join(d, 'aim_objetivos_x3.png'))

    # 2. La entrada, frame a frame, a 6x
    fr = []
    for f in range(len(ENTRADA)):
        c = np.empty((CELDA, CELDA, 3), np.uint8)
        c[:] = bg
        SEL._sobre(c, SEL._sprite('estrella', 1, 'str', 3, 'enemy', frame=0), CELDA // 2, CELDA // 2)
        SEL._sobre(c, reticula('zero', SEL.talla_de(61), f), CELDA // 2, CELDA // 2)
        fr.append(c)
    Image.fromarray(np.repeat(np.repeat(np.concatenate(fr, 1), 4, 0), 4, 1), 'RGB').save(
        os.path.join(d, 'aim_entrada_x4.png'))

    # 3. Un tablero en modo apuntado: el Pulsar propio con su poder listo, los
    #    no validos atenuados como la vista provisional y la reticula sobre la
    #    Estrella enemiga bajo el cursor.
    fondo = os.path.join(RAIZ, 'art', 'bg', 'fondos', 'v2', 'ref', 'bg_section1_ref0.png')
    tab = np.asarray(Image.open(fondo).convert('RGB')).copy() if os.path.exists(fondo) else np.tile(bg, (540, 960, 1))
    escena = [  # (carpeta, v, pre, n, fac, x, y, numero, valido)
        ('pulsar', 2, 'pul', 3, 'player', 200, 300, '64', False),
        ('estrella', 1, 'str', 3, 'player', 330, 180, '31', False),
        ('estrella', 1, 'str', 4, 'enemy', 640, 240, '40', True),
        ('magnetar', 1, 'mag', 3, 'neutral', 520, 400, '75', True),
        ('pulsar', 2, 'pul', 1, 'enemy', 800, 380, '9', True),
        ('asteroide', 1, 'ast', 0, 'enemy', 420, 300, '6', False),
    ]
    for carpeta, v, pre, n, fac, x, y, num, valido in escena:
        spr = SEL._sprite(carpeta, v, pre, n, fac, frame=0).copy()
        if not valido:   # el velo provisional: el fondo profundo al 60 %
            m = spr[..., 3] > 0
            spr[m, :3] = (spr[m, :3] * 0.4 + bg * 0.6).astype(np.uint8)
        SEL._sobre(tab, spr, x, y)
        SEL._numero(tab, x, y, SEL._huella(carpeta, v, pre, n) if pre != 'ast' else 43, num)
    SEL._sobre(tab, reticula('decay', SEL.talla_de(68), len(ENTRADA) - 1), 640, 240)
    Image.fromarray(tab, 'RGB').save(os.path.join(d, 'aim_tablero.png'))
    Image.fromarray(np.repeat(np.repeat(tab, 2, 0), 2, 1), 'RGB').save(os.path.join(d, 'aim_tablero_x2.png'))
    print('vistas en', d)


def main():
    raiz = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    os.makedirs(raiz, exist_ok=True)
    for poder in PODERES:
        for R in TALLAS:
            frames = verifica(poder, R)
            Image.fromarray(np.concatenate(frames, 1), 'RGBA').save(ruta(raiz, poder, R))
    print(f'{len(PODERES) * len(TALLAS)} hojas de {len(ENTRADA)} frames, celda de {CELDA}')
    if '--vista' in sys.argv:
        vistas()


if __name__ == '__main__':
    main()
