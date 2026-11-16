"""NOVA - ART-UI-CHARGE v3, la barra de carga del Pulsar: «La mecha».

Ref: NOVA_Assets_Diseno.md A.6, A.4.2 (los haces) · Spec 7.1, 7.2.2, 7.5,
13.12 · 14.4

Lo comun -el sitio, los estados, las comprobaciones y las vistas- vive en
`gen_charge_v1.py`.

LA TESIS DE LA v3: LA BARRA ES EL HAZ QUE VIENE. No se llena un deposito:
arde una mecha, y lo que arde es el poder en miniatura, con el dibujo de su
haz (A.4.2): las cuentas de hielo de «El rosario de escarcha», los eslabones
que se desmigan de «La cadena de desintegracion» y el zigzag de «El
desgarro». Todo corre hacia la punta, donde chisporrotea la llama. Delante
de la llama, lo que falta, en una linea de puntos del bando. Detras, a la
izquierda, el casquillo del bando: de quien es el Pulsar se lee aunque la
mecha haya ardido entera.

    el casquillo  2 x 6, `str_p[4]` o `str_e[4]`
    la mecha      2 px de alto en las filas 2-3, teselas de 8 que corren
                  8 px hacia la punta por bucle (32 frames, 2.67 s)
    la llama      5 x 6 centrada en el frente: un rombo con el nucleo blanco y
                  chispas al azar arriba y abajo
    lo que falta  un punto de cada dos en la fila 3, en `str_p[5]`/`str_e[5]`
    PAUSADA       la mecha se para y se apaga una banda, y la llama queda
                  en brasa: el nucleo en el indice 4, sin chispas
    LISTA         la mecha llega a la punta y la llama se hace estrella: una
                  cruz de 5 x 5 que late, y una luz recorre la mecha entera
                  hacia ella -el patron «Sonar» de 13.12-

PIEZAS
    ui_charge_socket.png       dos filas (bando), 2 x 6
    ui_charge_track.png        dos filas (bando), tesela de 2 x 6
    ui_charge_fuse_sheet.png   filas: poder; columnas: 32 frames y la quieta
                               (pausada); teselas de 8 x 6
    ui_charge_flame_sheet.png  filas: poder x {cargando, lista}; columnas: 32
                               frames y, en la fila de cargando, la brasa;
                               celdas de 5 x 6, el frente en x = 2
    ui_charge.json             medidas y reglas de montaje
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_charge_v1 as base
from gen_charge_v1 import (ALTO_MAX, FACCIONES, FONDO, FRAMES, PODERES, RAMPA_FAC, colores_de,
                           coste, hash2, pon, rampa_poder, rgba, tesela)
from nova_core import rampa

VERSION = 3
NOMBRE = 'La mecha'

ANCHO = 48
CASQUILLO = 2
X0 = CASQUILLO                  # la mecha empieza aqui
LARGO = ANCHO - X0              # 46
T = 8                           # tesela de la mecha
LLAMA_W = 5
FRENTE = 2                      # x del frente dentro de la celda de la llama


def casquillo(fac):
    F = rampa(RAMPA_FAC[fac])
    img = rgba(ALTO_MAX, CASQUILLO)
    img[:, 0, :3] = F[4]
    img[0, 1, :3] = F[4]
    img[ALTO_MAX - 1, 1, :3] = F[4]
    img[2:4, 1, :3] = F[3]
    img[..., 3] = np.where(img[..., :3].any(-1), 255, 0)
    return img


def pista(fac):
    F = rampa(RAMPA_FAC[fac])
    img = rgba(ALTO_MAX, 2)
    pon(img, 3, 0, F[5])
    return img


def mecha(p, f, pausada=False):
    """Tesela de 8 x 6 de la mecha, frame f; corre hacia la derecha."""
    R = rampa_poder(p)
    o = 1 if pausada else 0
    f = 0 if pausada else f
    img = rgba(ALTO_MAX, T)
    s = f // 4                                   # 8 px por bucle
    for x in range(T):
        u = (x - s) % T
        if p == 'zero':
            # el rosario: un hilo y cuentas de 2 x 2 cada 4
            pon(img, 2, x, R[3 + o])
            pon(img, 3, x, R[4 + o])
            if u % 4 < 2:
                pon(img, 2, x, R[1 + o])
                pon(img, 3, x, R[2 + o])
        elif p == 'decay':
            # la cadena: eslabones de 3 con 1 de hueco, y miguitas que se caen
            if u % 4 < 3:
                pon(img, 2, x, R[2 + o])
                pon(img, 3, x, R[3 + o])
            else:
                pon(img, 3, x, R[5])
            if not pausada and u % 4 == 1:
                caida = (f + u * 5) % 8
                if caida < 3:
                    pon(img, 4 + (caida > 1), x, R[3])
        else:
            # el desgarro: un zigzag de 2 de alto con su halo
            zz = (0, 0, 1, 1, 1, 0, 0, -1)[u]
            pon(img, 2 + zz, x, R[1 + o])
            pon(img, 3 + zz, x, R[3 + o])
            if 0 <= 3 + zz - 2 < ALTO_MAX and zz != 0:
                pon(img, 2 + zz - zz, x, R[4 + o])
    return img


def llama(p, f, modo='carga'):
    """5 x 6 con el frente en x = 2. modo: carga, brasa o lista."""
    R = rampa_poder(p)
    img = rgba(ALTO_MAX, LLAMA_W)
    if modo == 'brasa':
        for y in (2, 3):
            for x in (1, 2):
                pon(img, y, x, R[4])
        return img
    if modo == 'lista':
        late = (f // 4) % 2
        for y in range(1, 5):
            pon(img, y, 2, R[0] if 2 <= y <= 3 else R[1])
        for x in range(LLAMA_W):
            pon(img, 2, x, R[0] if 1 <= x <= 3 else R[1])
            pon(img, 3, x, R[0] if 1 <= x <= 3 else R[1])
        if late:
            pon(img, 0, 2, R[2])
            pon(img, 5, 2, R[2])
            for y, x in ((1, 1), (1, 3), (4, 1), (4, 3)):
                pon(img, y, x, R[2])
        return img
    # cargando: un rombo con el nucleo blanco, y chispas que saltan arriba y abajo
    for y, x in ((1, 2), (2, 1), (2, 3), (3, 1), (3, 3), (4, 2)):
        pon(img, y, x, R[1])
    for y in (2, 3):
        pon(img, y, 2, R[0])
    for k in range(2):
        h = hash2(f, k, 11)
        y = (0, 1, 4, 5)[h % 4]
        x = 1 + (h >> 3) % 4
        pon(img, y, x, R[1] if k == 0 else R[2])
    return img


def barra(p, inv, fac, pausada=False, f=0):
    lista = inv >= coste(p)
    img = rgba(ALTO_MAX, ANCHO)
    img[:, :CASQUILLO] = casquillo(fac)
    n = LARGO if lista else LARGO * inv // coste(p)
    # lo que falta, por delante de la llama
    zona = tesela(LARGO, pista(fac))
    ok = zona[..., 3] > 0
    img[:, X0:][ok] = zona[ok]
    if n > 0:
        m = tesela(n, mecha(p, f, pausada and not lista))
        if lista:
            # una luz recorre la mecha hacia la punta
            cab = f / FRAMES * (LARGO + 6) - 3
            R = rampa_poder(p)
            for x in range(n):
                if abs(x - cab) < 1.5 and m[2, x, 3]:
                    m[2, x, :3] = R[0]
        sub = img[:, X0:X0 + n]
        sub[:] = 0
        mm = m[..., 3] > 0
        sub[mm] = m[mm]
    if inv > 0 or lista:
        modo = 'lista' if lista else 'brasa' if pausada else 'carga'
        fl = llama(p, f, modo)
        fx = X0 + n - 1 - FRENTE if not lista else ANCHO - LLAMA_W
        for y in range(ALTO_MAX):
            for x in range(LLAMA_W):
                if fl[y, x, 3] and 0 <= fx + x < ANCHO:
                    img[y, fx + x] = fl[y, x]
    return img


def comprobar_propio():
    fallos = []
    for p in PODERES:
        fr = [mecha(p, f) for f in range(FRAMES)]
        if not (mecha(p, FRAMES) == fr[0]).all():
            fallos.append(f'la mecha de {p} no cierra el bucle')
        if len({q.tobytes() for q in fr}) < 4:
            fallos.append(f'la mecha de {p} apenas se mueve')
    return fallos


def exportar(d):
    Image.fromarray(np.vstack([casquillo(f) for f in FACCIONES]), 'RGBA').save(
        os.path.join(d, 'ui_charge_socket.png'))
    Image.fromarray(np.vstack([pista(f) for f in FACCIONES]), 'RGBA').save(
        os.path.join(d, 'ui_charge_track.png'))
    Image.fromarray(np.vstack([np.hstack([mecha(p, f) for f in range(FRAMES)] + [mecha(p, 0, True)])
                               for p in PODERES]), 'RGBA').save(os.path.join(d, 'ui_charge_fuse_sheet.png'))
    filas = []
    for p in PODERES:
        filas.append(np.hstack([llama(p, f) for f in range(FRAMES)] + [llama(p, 0, 'brasa')]))
        filas.append(np.hstack([llama(p, f, 'lista') for f in range(FRAMES)] + [rgba(ALTO_MAX, LLAMA_W)]))
    Image.fromarray(np.vstack(filas), 'RGBA').save(os.path.join(d, 'ui_charge_flame_sheet.png'))
    with open(os.path.join(d, 'ui_charge.json'), 'w') as fh:
        json.dump({'width': ANCHO, 'height': ALTO_MAX, 'socketWidth': CASQUILLO, 'fuseX0': X0,
                   'fuseLength': LARGO, 'fuseTile': T, 'flameWidth': LLAMA_W, 'flameFront': FRENTE,
                   'frames': FRAMES, 'fps': base.FPS, 'powers': list(PODERES), 'factions': list(FACCIONES),
                   'fuseRows': list(PODERES), 'fusePausedColumn': FRAMES,
                   'flameRows': [f'{p}_{m}' for p in PODERES for m in ('charging', 'ready')],
                   'flameEmberColumn': FRAMES,
                   'note': 'n = fuseLength * invertido // coste; la pista en mosaico por todo el largo, la mecha '
                           'en mosaico desde fuseX0 tapando la pista, y la llama con su x = 2 en la ultima '
                           'columna de la mecha; pausada: la mecha quieta (columna 32) y la brasa; lista: la mecha '
                           'entera, la llama lista pegada al borde derecho y una luz de 3 px que recorre la fila 2 '
                           'de la mecha de x = -3 a 49 en el bucle'}, fh, indent=1)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'str_e', 'hielo', 'decaimiento', 'gusano'))
