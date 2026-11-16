"""NOVA - ART-FX-CASCADE v2, la cascada de conquista: «La red».

Ref: NOVA_Assets_Diseno.md A.5 y A.4.3 · NOVA_Estados_Animaciones.md 14.5 y
14.9 · Spec 10.1 y 13.12

Lo comun a las tres propuestas -un efecto por nodo, la hoja de cinco
tallas, la rampa del Jugador, la caja del numero, convivir con «El relevo»
y las comprobaciones- vive en `gen_fx_cascade_v1.py`; aqui solo esta el
dibujo.

LA TESIS: EL NODO SE ENCHUFA. Todo lo que el Jugador tiene esta unido por su
plasma: la linea de envio (`ART-WLD-LINK`) es su sistema nervioso. Un nodo
que se suma no recibe nada, se conecta: le brotan cinco zarcillos de plasma
que buscan a los demas, la energia corre por ellos hacia dentro -el patron
«Flujo» de la maqueta (13.12)- y luego se recogen. Con varios nodos en
cadena, el mapa entero se va cableando del cercano al lejano.

  - BROTAN (frames 0-4). Cinco zarcillos salen del borde del cuerpo,
    serpenteando, con la punta en el indice 1; algunos echan una ramita.
  - EL FLUJO (frames 5-7). Ya enteros, tres paquetes de luz bajan por cada
    uno hacia el nodo.
  - SE RECOGEN (frames 8-11). Se apagan de la punta al cuerpo.

Los zarcillos tienen el hilo de 1 px y el temblor del filamento de 14.9, en
la rampa del plasma del Jugador.

    py tools/gen_fx_cascade_v2.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_cascade_v2.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_cascade_v1 as base
from gen_fx_cascade_v1 import C, CELDA, DENTRO, punto
from gen_fx_send_v1 import BAYER
from gen_fx_send_v1 import borde

VERSION = 2
NOMBRE  = 'La red'

LARGO = 43.0                                # hasta donde llega la punta, desde el centro
CRECE = (0.34, 0.58, 0.78, 0.92, 1.0)        # fraccion del zarcillo por frame


def _zarcillos():
    rng = np.random.default_rng(57)
    out = []
    base_a = rng.random() * 2 * math.pi
    for i in range(5):
        a = base_a + 2 * math.pi * i / 5 + rng.uniform(-0.35, 0.35)
        ondas = [(rng.uniform(0.25, 0.5), rng.uniform(0, 6.28), rng.uniform(2.0, 3.2))
                 for _ in range(2)]
        rama = rng.uniform(0.45, 0.7) if i % 2 == 0 else None
        lado = rng.choice((-1, 1))
        out.append((a, ondas, rama, lado))
    return out


ZARCILLOS = _zarcillos()


def camino(tt, a, ondas, desde=None):
    """Los puntos del zarcillo, del borde del cuerpo a la punta: (x, y, s)
    con s de 0 a 1."""
    r0 = max(borde(tt.t) - 4.0, 10.0) if desde is None else desde
    L = max(LARGO - r0, 6.0)
    pts = []
    n = int(L * 2.5)
    for k in range(n + 1):
        s = k / n
        r = r0 + L * s
        desv = sum(amp * math.sin(fr * r + fa) for fr, fa, amp in ondas) * min(s * 3, 1)
        th = a + desv / max(r, 1)
        pts.append((C + r * math.cos(th), C + r * math.sin(th), s))
    return pts


def dibujar(tt, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    rb = borde(tt.t)
    for a, ondas, rama, lado in ZARCILLOS:
        pts = camino(tt, a, ondas)
        if f <= 4:
            hasta, desde = CRECE[f], 0.0
        elif f <= 7:
            hasta, desde = 1.0, 0.0
        else:
            hasta, desde = (0.7, 0.45, 0.22, 0.08)[f - 8], 0.0
        cuerpo = 2 if f <= 1 else (3 if f <= 7 else (4 if f <= 9 else 5))
        for x, y, s in pts:
            if desde <= s <= hasta:
                punto(idx, tt, x, y, cuerpo)
        # la punta
        if f <= 4:
            x, y, _ = pts[int(hasta * (len(pts) - 1))]
            for dx, dy, b in ((0, 0, 1), (1, 0, 1), (0, 1, 2), (-1, 0, 2), (0, -1, 2)):
                punto(idx, tt, x + dx, y + dy, b)
        # la raiz en el borde del cuerpo
        if f <= 1:
            x, y, _ = pts[0]
            for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
                punto(idx, tt, x + dx, y + dy, 1 if f == 0 else 2)
        # la ramita
        if rama is not None and 2 <= f <= 9:
            i0 = int(rama * (len(pts) - 1))
            x0, y0, _ = pts[i0]
            crece = min((f - 1) / 3, 1.0) if f <= 7 else (1.0 - (f - 7) / 3)
            ang = a + lado * 0.8
            for k in range(int(9 * crece)):
                punto(idx, tt, x0 + k * math.cos(ang) * 0.9, y0 + k * math.sin(ang) * 0.9,
                      cuerpo if k < 6 else min(cuerpo + 1, 5))
        # el flujo: paquetes que bajan hacia el nodo
        if 5 <= f <= 7:
            for p in range(3):
                s = 1.0 - ((f - 5) / 3 + p / 3)
                if 0.02 < s < 0.98:
                    x, y, _ = pts[int(s * (len(pts) - 1))]
                    for dx, dy, b in ((0, 0, 1), (1, 0, 2), (-1, 0, 2), (0, 1, 2), (0, -1, 2)):
                        punto(idx, tt, x + dx, y + dy, b)
        # el paquete que llega al cuerpo enciende la raiz
        if f in (6, 7):
            x, y, _ = pts[0]
            punto(idx, tt, x, y, 1)
    # el resplandor: una trama en el indice 5 alrededor del hilo
    if f <= 9:
        hilo = idx > 0
        cerca = np.zeros_like(hilo)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                cerca |= np.roll(np.roll(hilo, dy, 0), dx, 1)
        glow = cerca & ~hilo & (BAYER < (0.5 if f <= 7 else 0.25)) & tt.libre & DENTRO
        idx[glow] = 5
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
