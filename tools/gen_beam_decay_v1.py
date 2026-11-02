"""NOVA - ART-WLD-BEAM-DEC v1, el haz de Decaimiento: «La cadena de desintegracion».

Ref: NOVA_Assets_Diseno.md A.4.2 · NOVA_GameDesign_Spec.md 7.2.2 y 7.4 ·
NOVA_Estados_Animaciones.md 14.5 (FX-BEAM)

El haz del poder del Pulsar al nodo objetivo, 0.25 s = 3 frames a 12 fps,
sin bucle. Una hoja, `art/world/beam_decay/v1/world_beam_decay_sheet.png`, con
la forma de la del Cero Absoluto (`gen_beam_zero_v1.py`, elegido) y de la
linea de envio: nacimiento (16), tramo (256) y punta (32, contacto en x 24)
en filas, y los 3 frames en columnas. Se monta igual: tramo en mosaico.

LA TESIS: el poder es desintegracion radiactiva, y la desintegracion tiene
una ley exacta: en cada vida media se desintegra la mitad de los nucleos, al
azar y sin memoria. El haz la cumple al pixel.

  - LA CADENA. «Cadena de desintegracion» es el nombre fisico de la serie de
    nucleos que se van convirtiendo unos en otros. Aqui es literal: dos
    hebras en contrafase -espejo una de otra respecto al eje, que es la
    simetria que pide A.4.2 para rotar- que se cruzan cada 16 px y dibujan
    una cadena de eslabones. En cada cruce, un NUCLEO, lo mas claro del haz.
  - LA VIDA MEDIA. Cada pixel de las hebras es un atomo con su propio
    numero al azar, sembrado y periodico en la tesela. En el frame f sigue
    vivo si ese numero es menor que 1/2^f: la cadena entra entera, en el
    frame 1 queda la mitad y en el 2 la cuarta parte. Es la ley de la
    desintegracion, y sale sola de la trama.
  - LA EMISION. Cada atomo que se desintegra suelta una particula hacia
    fuera del eje, que en el frame siguiente se aleja otro poco y se apaga
    una banda. La radiacion se ve salir del haz.

Asi se separan los tres trazos largos del juego sin depender del color: el
plasma es una onda viva, el hielo una aguja rigida y el decaimiento una
cadena que se deshace mientras la miras. Y conecta con «El miasma»
(A.3.5), el estado que provoca: el mismo verde, la misma idea de algo que se
cae a trozos.

NADA LLEGA AL BLANCO. `decaimiento[0]` es `#FFFFFF`, del FX-CAP (14.5). Los
nucleos van en `decaimiento[1]` y las hebras en `decaimiento[3]`, `#7CF23F`,
el ancla funcional de 14.4.

    py tools/gen_beam_decay_v1.py            la hoja + vistas fijas
    py tools/gen_beam_decay_v1.py --gif      ademas, el tablero en movimiento
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import BAYER8, RAIZ, Filamento, dir_preview, rampa
import gen_beam_zero_v1 as zero
from gen_beam_zero_v1 import (ALTO, CONTACTO, EJE, FRAMES, MITAD, NAC, PUNTA,
                              TESELA, TRAZO, ampliar, escena, tira_frames)

ASSET   = 'beam_decay'
VERSION = 1
RAMPA   = 'decaimiento'

PERIODO = 32            # un eslabon cada 16 px: 8 vueltas por tesela, que casa
AMPLITUD = 3.0          # las hebras en +-3 px: dentro del trazo de 8
SEMILLA = 29
SALTO_Y = (1, 3)        # lo que salta una particula hacia fuera al nacer, px
SALTO_X = (0, 2)        # y hacia la punta
EMITE   = 0.6           # la fraccion de atomos que suelta particula visible
LEJOS   = 2             # lo que se aleja otra vez al frame siguiente
NUCLEO  = 1             # medio ancho del nucleo, en px

# Bandas por frame: (hebra, nucleo, particula nueva, particula vieja)
TONOS = (
    (3, 1, None, None),
    (3, 2, 4, None),
    (4, 3, 4, 5),
)
VAHO = ((0.6, 1.3), (0.4, 1.1), (0.2, 0.9))


class Cadena:
    """El haz de Decaimiento. Misma interfaz que `Rosario` y `Filamento`."""

    def __init__(self, ramp):
        self.ramp = ramp
        self.alto, self.eje, self.tesela = ALTO, EJE, TESELA
        self.nac, self.punta, self.c_x = NAC, PUNTA, CONTACTO
        self.frames = FRAMES
        self.piezas = (('nacimiento', NAC), ('tramo', TESELA), ('punta', PUNTA))
        # el numero de cada atomo: uno por pixel de la mitad de arriba de la
        # tesela; la mitad de abajo es su espejo
        rng = np.random.default_rng(SEMILLA)
        self.azar = rng.random((MITAD, TESELA))
        # y su emision: cuanto salta hacia fuera y hacia delante la particula
        # que suelta al desintegrarse, y si la suelta. Un salto comun haria
        # que un tramo plano de hebra se moviera en bloque: una raya, no
        # radiacion
        self.salto_y = rng.integers(SALTO_Y[0], SALTO_Y[1] + 1, (MITAD, TESELA))
        self.salto_x = rng.integers(SALTO_X[0], SALTO_X[1] + 1, (MITAD, TESELA))
        self.emite = rng.random((MITAD, TESELA)) < EMITE

    def _geometria(self, nombre):
        if nombre == 'tramo':
            u0, ancho = 0, TESELA
        elif nombre == 'punta':
            u0, ancho = 0, PUNTA
        else:
            u0, ancho = -NAC, NAC
        x = np.arange(ancho)
        if nombre == 'punta':
            # las hebras se cierran sobre el contacto
            env = np.clip(1.0 - x / CONTACTO, 0, 1) ** 0.8
            vivo = x <= CONTACTO
        elif nombre == 'nacimiento':
            # y salen del centro del Pulsar, que las tapa
            env = np.clip((x - 3) / (NAC - 3), 0, 1) ** 1.2
            vivo = x >= 3
        else:
            env = np.ones(ancho)
            vivo = np.ones(ancho, bool)
        return u0, ancho, x, env, vivo

    def _hebra(self, nombre):
        """Mascara de la hebra de arriba (la mas alejada del eje en cada
        columna): y = eje - A |sin|. Muestreada a cuartos de pixel para que
        el trazo de 1 px no deje huecos en las pendientes."""
        u0, ancho, x, env, vivo = self._geometria(nombre)
        m = np.zeros((MITAD, ancho), bool)
        for k in range(4):
            xs = x + (k + 0.5) / 4
            y = EJE - AMPLITUD * env * np.abs(np.sin(2 * math.pi * (xs + u0) / PERIODO))
            fila = np.clip(np.floor(y).astype(int), 0, MITAD - 1)
            m[fila, x] = True
        m &= vivo[None, :]
        return m

    def _nucleos(self, nombre):
        """Los cruces de las hebras sobre el eje, cada medio periodo."""
        u0, ancho, x, env, vivo = self._geometria(nombre)
        m = np.zeros((MITAD, ancho), bool)
        if nombre == 'nacimiento':
            return m
        u = x + u0 + 0.5
        fase = np.mod(u, PERIODO / 2)
        cerca = np.minimum(fase, PERIODO / 2 - fase) <= NUCLEO
        m[MITAD - 1] = cerca & vivo
        if nombre == 'punta':
            # el contacto: un nucleo mayor, el que se queda en el objetivo
            m[MITAD - 2:, :] |= (np.abs(x + 0.5 - (CONTACTO + 0.5)) <= 1.5)[None, :]
        return m

    def _azar(self, nombre, ancho, u0):
        cols = np.mod(np.arange(ancho) + u0, TESELA)
        return self.azar[:, cols]

    def _vivos(self, nombre, f):
        """Los atomos de la hebra que siguen vivos en el frame f, y los que se
        desintegraron justo en el f."""
        u0, ancho, *_ = self._geometria(nombre)
        hebra = self._hebra(nombre)
        v = self._azar(nombre, ancho, u0)
        vivos = hebra & (v < 0.5 ** f)
        recien = hebra & (v < 0.5 ** (f - 1)) & ~vivos if f > 0 else np.zeros_like(hebra)
        return vivos, recien

    def _mitad(self, nombre, f):
        u0, ancho, x, env, vivo = self._geometria(nombre)
        banda = np.full((MITAD, ancho), -1, np.int8)
        t_hebra, t_nucleo, t_nueva, t_vieja = TONOS[f]
        ciclico = nombre == 'tramo'

        cols = np.mod(np.arange(ancho) + u0, TESELA)
        sy, sx, emite = (self.salto_y[:, cols], self.salto_x[:, cols],
                         self.emite[:, cols])

        def mover(m, extra):
            """Cada atomo de la mascara suelta su particula: su propio salto
            hacia fuera (arriba) y hacia la punta, mas `extra` filas si ya
            lleva un frame volando. En el tramo envuelve, como la tesela."""
            out = np.zeros_like(m)
            ys, xs = np.nonzero(m & emite)
            dy, dx = sy[ys, xs] + extra, sx[ys, xs] + (1 if extra else 0)
            ys, xs = ys - dy, xs + dx
            if ciclico:
                xs %= ancho
            ok = (ys >= 0) & (xs >= 0) & (xs < ancho)
            out[ys[ok], xs[ok]] = True
            return out

        # las particulas, debajo de todo lo demas: las del frame anterior se
        # han alejado otro poco y apagado; las de este frame acaban de salir
        if f >= 2:
            _, antes = self._vivos(nombre, f - 1)
            vieja = mover(antes, LEJOS)
            banda[vieja] = t_vieja
        if f >= 1:
            _, recien = self._vivos(nombre, f)
            nueva = mover(recien, 0)
            banda[nueva] = t_nueva
        particulas = banda >= 0

        vivos, _ = self._vivos(nombre, f)
        banda[vivos] = t_hebra
        nuc = self._nucleos(nombre)
        banda[nuc] = t_nucleo

        # el vaho: de lo que sigue vivo, cada vez mas ralo
        fuente = vivos | nuc
        dens = np.zeros((MITAD, ancho))
        ys, xs = np.nonzero(fuente)
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
            dens = np.maximum(dens, (0.8, 0.6, 0.35)[f] * np.exp(-(r / 4.5) ** 2))
        # dentro de los eslabones no hay vaho: el hueco es lo que hace cadena
        dentro = np.zeros((MITAD, ancho), bool)
        hebra = self._hebra(nombre)
        tiene = hebra.any(axis=0)
        alto_h = np.where(tiene, np.argmax(hebra, axis=0), MITAD)
        dentro[:] = np.arange(MITAD)[:, None] > alto_h[None, :]
        dens[dentro] = 0
        dens[banda >= 0] = 0
        return banda, dens, particulas

    def pieza(self, nombre, f):
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
        fallos.append('colores fuera de decaimiento[1..6]')
    if tuple(haz.ramp[0]) in usados:
        fallos.append('usa el blanco puro, que es de FX-CAP')
    for (nombre, f), (rgb_p, alfa, _) in frames.items():
        if not np.array_equal(alfa, alfa[::-1]) or not np.array_equal(rgb_p, rgb_p[::-1]):
            fallos.append(f'{nombre} {f}: no es simetrico respecto al eje')
    # la cadena cabe en el trazo de 8 px; solo las particulas salen al halo
    hebra = haz._hebra('tramo')
    if hebra[:MITAD - TRAZO].any():
        fallos.append('la cadena sale del trazo de 8 px')
    # la vida media: cada frame quedan la mitad de los atomos (+-15 %)
    n = [int(haz._vivos('tramo', f)[0].sum()) for f in range(FRAMES)]
    for f in (1, 2):
        r = n[f] / n[f - 1]
        if not 0.35 <= r <= 0.65:
            fallos.append(f'frame {f}: quedan {r:.0%} de los atomos, no la mitad')
    # la tesela casa: el periodo cabe entero y el azar es de la tesela
    if TESELA % PERIODO:
        fallos.append('el periodo no divide la tesela')
    luz = [sum(int(frames[nm, f][1].sum()) for nm, _ in haz.piezas) for f in range(FRAMES)]
    if not (luz[0] > luz[1] > luz[2]):
        fallos.append(f'la cadena no se apaga frame a frame: {luz}')
    for f in range(FRAMES - 1):
        if np.array_equal(frames['tramo', f][1], frames['tramo', f + 1][1]):
            fallos.append(f'frames {f} y {f + 1} iguales')
    if fallos:
        raise SystemExit('ART-WLD-BEAM-DEC v1 no pasa:\n  ' + '\n  '.join(fallos))
    return luz, n


# --- Vistas ------------------------------------------------------------------

def detalle(img, ruta):
    """Los 3 frames de los primeros 96 px del tramo, x8."""
    bg = zero.BG
    filas = []
    for f in range(FRAMES):
        t = img[ALTO:2 * ALTO, f * TESELA:f * TESELA + 96]
        o = np.empty((ALTO, 96, 3), np.uint8)
        o[:] = bg
        m = t[..., 3] > 0
        o[m] = t[..., :3][m]
        filas += [o, np.tile(bg, (3, 96, 1))]
    Image.fromarray(ampliar(np.vstack(filas), 8)).save(ruta)


def vistas(haz, frames, img, link, gif, otros):
    d = os.path.join(dir_preview(ASSET), f'v{VERSION}')
    os.makedirs(d, exist_ok=True)
    detalle(img, os.path.join(d, 'beam_decay_detalle_x8.png'))
    # los tres frames y, debajo, el Cero Absoluto elegido y la linea de envio
    tira = tira_frames(haz, frames, link)
    hz, fz = otros
    ancho = 200 + PUNTA - CONTACTO + 8
    z = ampliar(zero.tira(hz.linea(fz, 200, 1), ancho), 4)
    Image.fromarray(np.vstack([tira, z])).save(os.path.join(d, 'beam_decay_frames_x4.png'))
    for f in range(FRAMES):
        tab = escena(haz, frames, f, link, todos=True)
        Image.fromarray(tab).save(os.path.join(d, f'beam_decay_tablero_f{f}.png'))
        Image.fromarray(ampliar(tab[60:480, 100:620], 2)).save(
            os.path.join(d, f'beam_decay_tablero_f{f}_x2.png'))
    if gif:
        esc = [Image.fromarray(escena(haz, frames, k, link)) for k in range(24)]
        esc[0].save(os.path.join(d, 'beam_decay_tablero.gif'), save_all=True,
                    append_images=esc[1:], duration=round(1000 / zero.FPS), loop=0)
        lento = [Image.fromarray(ampliar(escena(haz, frames, f, link, todos=True)[60:480, 100:620], 2))
                 for f in range(FRAMES)]
        lento[0].save(os.path.join(d, 'beam_decay_lento_x2.gif'), save_all=True,
                      append_images=lento[1:], duration=500, loop=0)
    print('vistas en', d)


def main():
    destino = os.path.join(RAIZ, 'art', 'world', ASSET, f'v{VERSION}')
    os.makedirs(destino, exist_ok=True)
    haz = Cadena(rampa(RAMPA))
    img, frames = haz.hoja()
    luz, n = comprobar(haz, img, frames)
    Image.fromarray(img, 'RGBA').save(os.path.join(destino, 'world_beam_decay_sheet.png'))
    print(f'hoja {img.shape[1]}x{img.shape[0]}  luz por frame {luz}  atomos vivos {n}')

    hz = zero.Rosario(rampa(zero.RAMPA))
    _, fz = hz.hoja()
    import gen_link_v1 as link
    from gen_link_v2 import PLASMA_P, aplicar
    aplicar(PLASMA_P)
    fil_l = link.filamento()
    frames_l = {(nm, f): fil_l.pieza(nm, f) for nm, _ in fil_l.piezas for f in range(fil_l.frames)}
    vistas(haz, frames, img, (fil_l, frames_l), '--gif' in sys.argv, (hz, fz))


if __name__ == '__main__':
    main()
