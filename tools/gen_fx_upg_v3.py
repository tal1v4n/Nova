"""NOVA - ART-FX-UPG v3, la mejora de nivel: «El salto cuantico».

Ref: NOVA_Assets_Diseno.md A.5 · NOVA_Estados_Animaciones.md 14.5

Lo comun a las tres propuestas -la hoja de veinte filas, las rampas, la caja
del numero y las comprobaciones- vive en `gen_fx_upg_v1.py`; aqui solo esta
el dibujo.

LA TESIS: SUBIR DE NIVEL ES SALTAR DE ORBITA. En fisica, un «nivel» es una
orbita del atomo de Bohr, y un electron sube de una a otra absorbiendo un
foton y SIN PASAR POR EL MEDIO: el salto cuantico. NOVA ya habla esa lengua
(el proton son tres quarks y sus gluones, «La bolsa»), y la mejora es el
unico suceso del juego que se llama igual en los dos idiomas.

  - LLEGA EL FOTON (frames 0-1). Una orbita interior, inclinada, a la medida
    del cuerpo viejo, con su electron. Un foton ondulado entra desde fuera y
    lo alcanza: el electron se enciende.
  - EL SALTO (frame 2). La orbita interior se apaga de golpe y el electron
    aparece en la exterior, al borde del cuerpo nuevo. No hay frame
    intermedio, y esa es la gracia: la mejora no es un crecimiento, es un
    cambio de estado.
  - SE ASIENTA (frames 3-5). El electron sigue su orbita nueva y se apaga
    con ella.

La orbita es una elipse aplanada (0.38) e inclinada 18 grados: dice «atomo»
sin la cruz de tres anillos del simbolo, que A.2.2 descarto para la
Estrella. Y aplanada no baja hasta el numero.

    py tools/gen_fx_upg_v3.py          hoja, comprobaciones y vistas fijas
    py tools/gen_fx_upg_v3.py --gif    y las animadas
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np

import gen_fx_upg_v1 as base
from gen_fx_upg_v1 import C, CELDA, DX, DY

VERSION = 3
NOMBRE  = 'El salto cuantico'

PLANO   = 0.38
INCL    = math.radians(-18)
FASE0   = math.radians(200)          # donde esta el electron: arriba a la izquierda
PASO    = math.radians(28)           # lo que avanza por frame


def _local():
    c, s = math.cos(INCL), math.sin(INCL)
    u = DX * c + DY * s
    v = -DX * s + DY * c
    return u, v


U, V = _local()


def orbita(idx, s, a, banda, grosor=1.0, cada=1):
    """Una elipse de semieje mayor `a`, inclinada, de `grosor` px."""
    b = a * PLANO
    rho = np.hypot(U / a, V / b)
    # distancia al contorno, a primer orden: (rho - 1) / |grad rho|
    d = (rho - 1) * rho / np.maximum(np.hypot(U / a ** 2, V / b ** 2), 1e-6)
    on = (np.abs(d) < grosor / 2) & s.libre & (idx == 0)
    if cada > 1:
        yy, xx = np.nonzero(on)
        quita = ((xx + yy) % cada) != 0
        on[yy[quita], xx[quita]] = False
    idx[on] = banda


def en_orbita(a, t):
    c, s_ = math.cos(INCL), math.sin(INCL)
    u, v = a * math.cos(t), a * PLANO * math.sin(t)
    return C + u * c - v * s_, C + u * s_ + v * c


def electron(idx, s, x, y, banda, halo):
    for dx in range(-2, 3):
        for dy in range(-2, 3):
            d = abs(dx) + abs(dy)
            if d <= 1:
                s.punto(idx, x + dx, y + dy, banda)
            elif d == 2:
                # contorno oscuro: el electron se lee sobre la luz del cuerpo
                s.punto(idx, x + dx, y + dy, 5 if not halo else min(banda + 2, 4),
                        solo_vacio=True)


def foton(idx, s, x0, y0, x1, y1, banda):
    """Un tren de onda de x0,y0 a x1,y1: la onda corta del foton."""
    L = math.hypot(x1 - x0, y1 - y0)
    ux, uy = (x1 - x0) / L, (y1 - y0) / L
    for k in range(int(L * 2)):
        t = k / 2
        w = 2.0 * math.sin(t * 1.3)
        s.punto(idx, x0 + ux * t - uy * w, y0 + uy * t + ux * w, banda)


def dibujar(s, f):
    idx = np.zeros((CELDA, CELDA), np.int8)
    a_in = max(s.piel(), 10) + 1
    a_out = s.despues + 4
    t = FASE0 + PASO * f
    if f <= 1:
        x, y = en_orbita(a_in, t)
        electron(idx, s, x, y, 1 if f else 2, halo=bool(f))
        orbita(idx, s, a_in, 2, grosor=1.0)
        orbita(idx, s, a_in + 1, 5, grosor=1.0)          # su sombra, por fuera
        # el foton entra desde arriba a la izquierda, fuera del cuerpo nuevo
        dirx, diry = math.cos(math.radians(-130)), math.sin(math.radians(-130))
        lejos, cerca = (16, 7) if f == 0 else (9, 2)
        foton(idx, s, x + dirx * lejos, y + diry * lejos * 0.7,
              x + dirx * cerca, y + diry * cerca * 0.7, 1)
    else:
        k = f - 2                                    # 0..3
        x, y = en_orbita(a_out, t)
        electron(idx, s, x, y, (1, 1, 2, 4)[k], halo=k < 2)
        if k < 3:
            orbita(idx, s, a_out, (1, 2, 3)[k], grosor=1.0, cada=(1, 1, 2)[k])
        if k == 0:
            # lo que queda de la orbita vieja: un fantasma ralo
            orbita(idx, s, a_in, 5, cada=3)
    return idx


if __name__ == '__main__':
    base.ejecutar(dibujar, VERSION, NOMBRE, '--gif' in sys.argv)
