"""NOVA - ART-WLD-BEAM-WRM v1, el haz de Agujero de Gusano: «El desgarro».

Ref: NOVA_Assets_Diseno.md A.4.2 · NOVA_GameDesign_Spec.md 7.2.2 y 7.5 ·
NOVA_Estados_Animaciones.md 14.5 (FX-BEAM)

El haz del poder del Pulsar al nodo objetivo, 0.25 s = 3 frames a 12 fps,
sin bucle. Una hoja, `art/world/beam_wormhole/v1/world_beam_wormhole_sheet.png`,
con la forma y el montaje de los otros dos haces (`gen_beam_zero_v1.py` y
`gen_beam_decay_v1.py`, elegidos): nacimiento (16), tramo (256, en mosaico)
y punta (32, contacto en x 24) en filas, y los 3 frames en columnas.

LA TESIS: el Agujero de Gusano no manda nada por el espacio; se salta el
espacio. Asi que su haz no es luz que viaja sino una RASGADURA: el espacio
entre el Pulsar y el objetivo se abre como una tela, y por la grieta se ve
el otro lado.

  - EL CENTRO OSCURO. Los otros dos haces -y todo el plasma- tienen el
    alma clara. Este la tiene oscura, en `gusano[6]`, entre dos LABIOS
    brillantes en `gusano[3]`, el ancla `#9B5CF6`. Es el unico trazo del
    juego con el centro en negativo, y eso lo separa de la linea de envio
    aunque su halo indigo sea primo de este morado.
  - OTRO CIELO DENTRO. En la garganta abierta brillan estrellas que no son
    las del fondo, y una nebulosa rala: el atajo por el que el nodo cambia
    de bando entero (7.5).
  - LABIOS DESGARRADOS. El ancho de la garganta cambia a saltos a lo largo del haz
    -2 o 3 px por lado, en tramos sembrados de 2 a 6 px- y deja hilachas
    sueltas fuera del labio: una tela rasgada, no un tubo.

TRES FRAMES:

  0. DESGARRO. El espacio se raja: una ranura de 1 px entre labios en el
     tono mas claro, con chispas que saltan del corte.
  1. GARGANTA. La grieta se abre a 2-3 px por lado y enseña el otro cielo.
     Es el frame de mas luz.
  2. CIERRE. La grieta se cierra a 1 px con los labios apagados, y de las
     estrellas solo quedan unos puntos sobre el eje: la luz del otro lado,
     tragada.

NADA LLEGA AL BLANCO. `gusano[0]` es `#FFFFFF`, del FX-CAP (14.5); lo mas
claro son los labios del desgarro y las estrellas, en `gusano[1]`.

    py tools/gen_beam_wormhole_v1.py            la hoja + vistas fijas
    py tools/gen_beam_wormhole_v1.py --gif      ademas, el tablero en movimiento
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import BAYER8, RAIZ, Filamento, dir_preview, rampa
import gen_beam_zero_v1 as zero
import gen_beam_decay_v1 as decay
from gen_beam_zero_v1 import (ALTO, CONTACTO, EJE, FRAMES, MITAD, NAC, PUNTA,
                              TESELA, TRAZO, ampliar, escena, tira_frames)

ASSET   = 'beam_wormhole'
VERSION = 1
RAMPA   = 'gusano'

SEMILLA  = 41
ABIERTO  = (1, None, 1)        # el ancho por frame; None = el de la garganta
ESTRELLAS = 34                 # por tesela, en la garganta
TRAMO    = (2, 6)              # largo de cada tramo de borde, px
CAMBIO   = 0.7                 # probabilidad de que el tramo siguiente cambie de ancho
HILACHAS = 16                  # por tesela
NEBULOSA = 0.10                # fraccion de la garganta en nebulosa
CHISPAS  = 9                   # por tesela, en el desgarro
QUEDAN   = 6                   # puntos de luz sobre el eje en el cierre

# Bandas por frame: (labio, interior, estrella, chispa)
TONOS = (
    (1, 6, None, 2),
    (3, 6, 1, None),
    (4, 6, 2, None),
)
VAHO = ((0.6, 1.2), (0.75, 1.4), (0.3, 0.9))


class Desgarro:
    """El haz de Agujero de Gusano. Misma interfaz que los otros dos."""

    def __init__(self, ramp):
        self.ramp = ramp
        self.alto, self.eje, self.tesela = ALTO, EJE, TESELA
        self.nac, self.punta, self.c_x = NAC, PUNTA, CONTACTO
        self.frames = FRAMES
        self.piezas = (('nacimiento', NAC), ('tramo', TESELA), ('punta', PUNTA))
        rng = np.random.default_rng(SEMILLA)
        # las estrellas del otro lado: columna, fila (0 = sobre el eje,
        # 1 = una mas afuera) y tono
        self.estrellas = [(int(rng.integers(0, TESELA)), int(rng.choice((0, 0, 1))),
                           int(rng.choice((1, 1, 2)))) for _ in range(ESTRELLAS)]
        self.chispas = [(int(rng.integers(0, TESELA)), int(rng.integers(1, 3)))
                        for _ in range(CHISPAS)]
        self.quedan = sorted(int(rng.integers(0, TESELA)) for _ in range(QUEDAN))
        # el borde roto: la garganta abierta cambia de ancho a saltos, en
        # tramos de 2 a 6 px, como una tela rasgada y no como un tubo. La
        # tesela empieza y acaba en un tramo distinto, asi que la costura no
        # se nota mas que cualquier otro salto
        g, x = np.empty(TESELA, int), 0
        anterior = 2
        while x < TESELA:
            largo = int(rng.integers(TRAMO[0], TRAMO[1] + 1))
            anterior = 5 - anterior if rng.random() < CAMBIO else anterior
            g[x:x + largo] = anterior
            x += largo
        self.garganta = g
        # hilachas: un pixel suelto fuera del labio
        self.hilachas = sorted(int(c) for c in rng.choice(TESELA, HILACHAS, replace=False))
        # y la nebulosa del otro cielo, rala y al azar
        self.nebulosa = rng.random((MITAD, TESELA)) < NEBULOSA

    def _geometria(self, nombre):
        return decay.Cadena._geometria(self, nombre)

    def _ancho(self, nombre, f):
        """Semiancho de la garganta, entero, por columna."""
        u0, ancho, x, env, vivo = self._geometria(nombre)
        if ABIERTO[f] is not None:
            w = np.full(ancho, float(ABIERTO[f]))
            if nombre != 'tramo':
                w = np.where(env > 0.35, w, 0.0)
        else:
            g = self.garganta[np.mod(x + u0, TESELA)]
            w = np.clip(np.round(g * env), 0, TRAZO - 1)
        return np.where(vivo, w, -1).astype(int)

    def _mitad(self, nombre, f):
        u0, ancho, x, env, vivo = self._geometria(nombre)
        banda = np.full((MITAD, ancho), -1, np.int8)
        t_labio, t_dentro, t_estrella, t_chispa = TONOS[f]
        w = self._ancho(nombre, f)
        filas = np.arange(MITAD)[:, None]
        # la garganta: las w filas junto al eje; el labio, la siguiente
        dentro = (w[None, :] > 0) & (filas >= MITAD - w[None, :])
        labio = (w[None, :] >= 0) & (filas == MITAD - 1 - w[None, :])
        banda[dentro] = t_dentro
        banda[labio] = t_labio

        cols = np.mod(x + u0, TESELA)
        donde = {int(c): i for i, c in enumerate(cols)}

        # el otro cielo: nebulosa rala y estrellas, solo en la garganta abierta
        if t_estrella is not None and f == 1:
            neb = dentro & self.nebulosa[:, cols]
            banda[neb] = 5
            for c, alto_e, tono in self.estrellas:
                if c in donde:
                    i = donde[c]
                    fila = MITAD - 1 - alto_e
                    if dentro[fila, i]:
                        banda[fila, i] = tono
        if f == 2:
            for c in self.quedan:
                if c in donde and dentro[MITAD - 1, donde[c]]:
                    banda[MITAD - 1, donde[c]] = t_estrella
        # hilachas del borde roto, un pixel fuera del labio
        if f == 1 and nombre == 'tramo':
            for c in self.hilachas:
                fila = MITAD - 2 - w[c]
                if fila >= MITAD - TRAZO:
                    banda[fila, c] = 4
        # chispas del corte, fuera del labio
        if t_chispa is not None and nombre == 'tramo':
            for c, alto_c in self.chispas:
                fila = MITAD - 1 - w[c] - 1 - alto_c
                if fila >= 0:
                    banda[fila, c] = t_chispa
                    if alto_c == 2 and c + 1 < ancho:
                        banda[fila + 1, (c + 1) % ancho] = t_chispa

        if nombre == 'punta' and f == 1:
            # la otra boca: el labio se cierra en un aro sobre el contacto
            py, px = np.mgrid[0:MITAD, 0:ancho] + 0.5
            r = np.hypot(px - (CONTACTO + 0.5), py - EJE)
            aro = (np.abs(r - 3.0) < 0.6) & (banda < 0)
            banda[aro] = t_labio

        # el vaho: solo hacia fuera, desde los labios
        dens = np.zeros((MITAD, ancho))
        ys, xs = np.nonzero(labio)
        if len(ys):
            py, px = np.mgrid[0:MITAD, 0:ancho] + 0.5
            d = np.full((MITAD, ancho), 99.0)
            for yy, xx in zip(ys, xs):
                d = np.minimum(d, np.hypot(px - (xx + 0.5), py - (yy + 0.5)))
            peso, caida = VAHO[f]
            dens = peso * np.exp(-(d - 1.0) / caida)
        if nombre == 'nacimiento':
            py, px = np.mgrid[0:MITAD, 0:ancho] + 0.5
            r = np.hypot(px - 1.0, py - EJE)
            dens = np.maximum(dens, (0.8, 0.9, 0.4)[f] * np.exp(-(r / 4.5) ** 2))
        dens[dentro] = 0
        dens[banda >= 0] = 0
        return banda, dens, None

    def pieza(self, nombre, f):
        return decay.Cadena.pieza(self, nombre, f)

    def hoja(self):
        return zero.Rosario.hoja(self)

    def linea(self, frames, largo, f):
        return Filamento.linea(self, frames, largo, f)


# --- Comprobaciones ----------------------------------------------------------

def comprobar(haz, img, frames):
    fallos = []
    if not set(np.unique(img[..., 3])) <= {0, 255}:
        fallos.append('alfa no binario')
    rgb = img[..., :3][img[..., 3] > 0]
    usados = {tuple(c) for c in np.unique(rgb, axis=0)}
    if not usados <= {tuple(c) for c in haz.ramp[1:7]}:
        fallos.append('colores fuera de gusano[1..6]')
    if tuple(haz.ramp[0]) in usados:
        fallos.append('usa el blanco puro, que es de FX-CAP')
    for (nombre, f), (rgb_p, alfa, _) in frames.items():
        if not np.array_equal(alfa, alfa[::-1]) or not np.array_equal(rgb_p, rgb_p[::-1]):
            fallos.append(f'{nombre} {f}: no es simetrico respecto al eje')
    for f in range(FRAMES):
        rgb_t, a_t, _ = frames['tramo', f]
        # el centro es oscuro: el eje del tramo, casi todo en gusano[6]
        eje = rgb_t[7][a_t[7]]
        oscuro = (eje == haz.ramp[6]).all(axis=1).mean() if len(eje) else 0
        if oscuro < 0.8:
            fallos.append(f'frame {f}: el eje no es oscuro ({oscuro:.0%})')
        # los labios caben en el trazo de 8 px
        b, _, _ = haz._mitad('tramo', f)
        w = haz._ancho('tramo', f)
        if (MITAD - 1 - w).min() < MITAD - TRAZO:
            fallos.append(f'frame {f}: el labio sale del trazo de 8 px')
    w1 = haz._ancho('tramo', 1)
    if w1.min() < 2:
        fallos.append('la garganta no se abre a 2 px en todo el tramo')
    luz = [sum(int(frames[nm, f][1].sum()) for nm, _ in haz.piezas) for f in range(FRAMES)]
    if not (luz[1] > luz[0] and luz[1] > luz[2]):
        fallos.append(f'la garganta no culmina en el frame 1: {luz}')
    for f in range(FRAMES - 1):
        if np.array_equal(frames['tramo', f][0], frames['tramo', f + 1][0]):
            fallos.append(f'frames {f} y {f + 1} iguales')
    if fallos:
        raise SystemExit('ART-WLD-BEAM-WRM v1 no pasa:\n  ' + '\n  '.join(fallos))
    return luz


# --- Vistas ------------------------------------------------------------------

def vistas(haz, frames, img, link, gif, otros):
    d = os.path.join(dir_preview(ASSET), f'v{VERSION}')
    os.makedirs(d, exist_ok=True)
    decay.detalle(img, os.path.join(d, 'beam_wormhole_detalle_x8.png'))
    # los tres frames; debajo, la linea de envio y los otros dos haces
    tira = tira_frames(haz, frames, link)
    ancho = 200 + PUNTA - CONTACTO + 8
    extra = [ampliar(zero.tira(h.linea(fr, 200, f), ancho), 4) for h, fr, f in otros]
    Image.fromarray(np.vstack([tira] + extra)).save(os.path.join(d, 'beam_wormhole_frames_x4.png'))
    for f in range(FRAMES):
        tab = escena(haz, frames, f, link, todos=True)
        Image.fromarray(tab).save(os.path.join(d, f'beam_wormhole_tablero_f{f}.png'))
        Image.fromarray(ampliar(tab[60:480, 100:620], 2)).save(
            os.path.join(d, f'beam_wormhole_tablero_f{f}_x2.png'))
    if gif:
        esc = [Image.fromarray(escena(haz, frames, k, link)) for k in range(24)]
        esc[0].save(os.path.join(d, 'beam_wormhole_tablero.gif'), save_all=True,
                    append_images=esc[1:], duration=round(1000 / zero.FPS), loop=0)
        lento = [Image.fromarray(ampliar(escena(haz, frames, f, link, todos=True)[60:480, 100:620], 2))
                 for f in range(FRAMES)]
        lento[0].save(os.path.join(d, 'beam_wormhole_lento_x2.gif'), save_all=True,
                      append_images=lento[1:], duration=500, loop=0)
    print('vistas en', d)


def main():
    destino = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    os.makedirs(destino, exist_ok=True)
    haz = Desgarro(rampa(RAMPA))
    img, frames = haz.hoja()
    luz = comprobar(haz, img, frames)
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'world_beam_wormhole_sheet.png'))
    print(f'hoja {img.shape[1]}x{img.shape[0]}  luz por frame {luz}')

    hz = zero.Rosario(rampa(zero.RAMPA))
    _, fz = hz.hoja()
    hd = decay.Cadena(rampa(decay.RAMPA))
    _, fd = hd.hoja()
    import gen_link_v1 as link
    from gen_link_v2 import PLASMA_P, aplicar
    aplicar(PLASMA_P)
    fil_l = link.filamento()
    frames_l = {(nm, f): fil_l.pieza(nm, f) for nm, _ in fil_l.piezas for f in range(fil_l.frames)}
    vistas(haz, frames, img, (fil_l, frames_l), '--gif' in sys.argv,
           [(hz, fz, 1), (hd, fd, 0)])


if __name__ == '__main__':
    main()
