"""NOVA - ART-UI-WHEEL v3, la rueda de poderes del Pulsar: «El tambor».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 7.1, 13.5, 13.12, 14.4, 16.3, 18 #34,
#35

Lo comun -los estados, los pictogramas, el texto, las comprobaciones y las
vistas- vive en `gen_wheel_v1.py`.

LA TESIS DE LA v3: UNA PIEZA CON TRES CAMARAS. La rueda no son tres
botones sueltos: es UNA placa de 192 x 20 con el lenguaje de 13.12,
partida en tres camaras por dos tabiques, como el tambor de un revolver
desplegado. Cada camara lleva el pictograma de su poder y `1 · 20`. Al
cargar, la camara SE LLENA DE ABAJO ARRIBA con la materia del poder -una
trama de su color con una superficie clara-, y pausada, la materia se
apaga y se queda quieta. Lista, la camara esta llena y la superficie
chispea: brilla. Encima, la camara se marca por dentro con un filo cian;
pulsada, el contenido baja 2 px.

    bloqueado   la camara rayada en gris
    candado     rayada, con el candado y `Nv N`
    nodisp      la camara vacia, en `#0B0E1A`, con un filo en trazos

PIEZAS
    ui_power_wheel_drum.png     la placa del tambor, 20 x 20 en nueve cortes,
                                y el tabique, 6 x 20
    ui_power_wheel_matter.png   fila = poder; teselas de 8 x 8: cargando y
                                pausada, y la superficie en 8 x 1
    ui_power_wheel_icons.png    los pictogramas
    ui_power_wheel.json
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_wheel_v1 as base
from gen_wheel_v1 import (BAJA, CELDA_H, CW, HUECO, PIC, PODERES, RAMPA, SH, SW, candado, colores_picto,
                          json_comun, picto, pinta_textos, textos)
import gen_charge_v1 as c1
import gen_panel_v1 as pv1
from gen_panel_v1 import FONDO, NEU, PANEL, STR_P, colores_de, pega, rgba
from nova_core import BAYER8

VERSION = 3
NOMBRE = 'El tambor'
FRAMES = base.FRAMES
IX0, IX1 = 2, SW - 2             # el interior de una camara, dentro de la celda de 60
IY0, IY1 = 2, SH - 2
TY = 5


def materia(p, pausada=False):
    R = RAMPA[p]
    t = rgba(8, 8)
    for y in range(8):
        for x in range(8):
            t[y, x, :3] = (R[6] if pausada else R[5]) if BAYER8[y, x] >= 0.35 else (R[5] if pausada else R[4])
    t[..., 3] = 255
    return t


def fondo_fila(img, estados, f):
    """La placa entera con sus dos tabiques."""
    placa = pv1.nueve(base.tecla(STR_P[6]), CW, SH, 6)
    for i in (1, 2):
        x = i * (SW + HUECO) - HUECO
        placa[:, x:x + HUECO] = 0
        placa[0:2, x:x + HUECO, :3] = STR_P[6]
        placa[SH - 2:SH, x:x + HUECO, :3] = STR_P[6]
        placa[0:2, x:x + HUECO, 3] = 255
        placa[SH - 2:SH, x:x + HUECO, 3] = 255
        placa[2:SH - 2, x + 2:x + 4, :3] = STR_P[6]
        placa[2:SH - 2, x + 2:x + 4, 3] = 255
        placa[2:SH - 2, x:x + 2, :3] = PANEL
        placa[2:SH - 2, x + 4:x + 6, :3] = PANEL
        placa[2:SH - 2, x:x + 2, 3] = 255
        placa[2:SH - 2, x + 4:x + 6, 3] = 255
    pega(img, placa, 0, 0)


def filo(cuerpo, color, trazos=False):
    for x in range(IX0, IX1):
        for y in (IY0, IY1 - 1):
            if not trazos or x % 4 < 2:
                cuerpo[y, x, :3] = color
                cuerpo[y, x, 3] = 255
    for y in range(IY0, IY1):
        for x in (IX0, IX1 - 1):
            if not trazos or y % 4 < 2:
                cuerpo[y, x, :3] = color
                cuerpo[y, x, 3] = 255


def ranura(estado, p, progreso=0.45, pausada=False, f=0, t=1.0):
    R = RAMPA[p]
    img = rgba(CELDA_H, SW)
    dy = BAJA if estado in ('pulsado', 'listo_pulsado') else 0
    cuerpo = rgba(SH, SW)
    alto = IY1 - IY0
    if estado in ('bloqueado', 'candado'):
        for y in range(IY0, IY1):
            for x in range(IX0, IX1):
                if (x + y) % 4 == 0:
                    cuerpo[y, x, :3] = NEU[5]
                    cuerpo[y, x, 3] = 255
    if estado == 'nodisp':
        cuerpo[IY0:IY1, IX0:IX1, :3] = FONDO
        cuerpo[IY0:IY1, IX0:IX1, 3] = 255
        filo(cuerpo, NEU[5], trazos=True)
    if estado == 'cargando' or estado.startswith('listo'):
        lleno = alto if estado.startswith('listo') else max(1, int(alto * min(progreso, 0.94)))
        tr = np.tile(materia(p, pausada and estado == 'cargando'), (3, 8, 1))[:alto, :IX1 - IX0]
        y0 = IY1 - lleno
        cuerpo[y0:IY1, IX0:IX1] = tr[:lleno]
        sup = R[4] if (pausada and estado == 'cargando') else R[2]
        if not estado.startswith('listo'):
            cuerpo[y0, IX0:IX1, :3] = sup
            if not pausada:                       # la superficie ondea
                for x in range(IX0, IX1):
                    if (x + (f % FRAMES) // 2) % 6 == 0:
                        cuerpo[y0, x, :3] = R[1]
        else:                                     # lista: chispea
            for k in range(5):
                h = c1.hash2((f % FRAMES) // 2, k, 7)
                x = IX0 + h % (IX1 - IX0)
                y = IY0 + (h >> 5) % alto
                cuerpo[y, x, :3] = R[1] if k % 2 else R[0]
    if estado in ('encima', 'pulsado'):
        filo(cuerpo, STR_P[4] if estado == 'encima' else STR_P[2])
    if estado in ('listo_encima', 'listo_pulsado'):
        filo(cuerpo, R[0])
    if estado == 'candado':
        pega(cuerpo, candado(NEU[3], PANEL), base.PADX + 2, 6)
    else:
        luz, cu, cont = colores_picto(estado, p)
        if estado.startswith('listo'):
            luz, cu = R[0], R[2]
        pega(cuerpo, picto(p, luz, cu, cont), base.PADX, 4)
    ts = textos(estado, p)
    if estado.startswith('listo') or estado == 'cargando':
        ts = [(ts[0][0], R[0]), (ts[1][0], R[1])] if not estado == 'cargando' else \
             [(f'{base.numero(p)} · ', base.C_TEXTO), (str(base.COSTE[p]), R[1])]
    pinta_textos(cuerpo, ts, SW - base.PADX, TY, 'der')
    pega(img, cuerpo, 0, dy)
    return img


def cancelar(estado, devuelve, f=0, t=1.0):
    return base.cancelar_lamina(estado, devuelve, f, t)


PICTOS = {
    'color': lambda p: picto(p, RAMPA[p][1], RAMPA[p][3]),
    'listo': lambda p: picto(p, RAMPA[p][0], RAMPA[p][2]),
    'gris': lambda p: picto(p, NEU[2], NEU[4]),
    'contorno': lambda p: picto(p, NEU[4], NEU[4], True),
}


def piezas():
    tambor = rgba(CELDA_H, CW)
    fondo_fila(tambor, ('normal',) * 3, 0)
    d = {'placa': base.tecla(STR_P[6]), 'tabique': tambor[:SH, SW:SW + HUECO]}
    for p in PODERES:
        sup = rgba(1, 8)
        sup[..., :3] = RAMPA[p][2]
        sup[..., 3] = 255
        d[f'materia_{p}'] = np.hstack([materia(p), materia(p, True)])
        d[f'superficie_{p}'] = sup
    d['pictos'] = base.hoja_pictos(PICTOS)
    d['candado'] = candado(NEU[3], PANEL)
    return d


def comprobar_propio():
    fallos = []
    for p in PODERES:
        a = (base.ranura if False else ranura)('cargando', p, 0.2)
        b = ranura('cargando', p, 0.7)
        if (a[..., 3] > 0).sum() >= (b[..., 3] > 0).sum() and (a == b).all():
            fallos.append(f'{p}: la materia no sube')
    return fallos


def exportar(d):
    p = piezas()
    Image.fromarray(np.hstack([p['placa'], p['tabique']]), 'RGBA').save(os.path.join(d, 'ui_power_wheel_drum.png'))
    Image.fromarray(np.vstack([p[f'materia_{q}'] for q in PODERES]), 'RGBA').save(
        os.path.join(d, 'ui_power_wheel_matter.png'))
    Image.fromarray(p['pictos'], 'RGBA').save(os.path.join(d, 'ui_power_wheel_icons.png'))
    Image.fromarray(p['candado'], 'RGBA').save(os.path.join(d, 'ui_power_wheel_lock.png'))
    j = json_comun()
    j.update({'drumCell': 20, 'drumBorder': 6, 'drumCenterMode': 'tiled', 'divider': [HUECO, SH],
              'chamber': [IX0, IY0, IX1, IY1], 'matterColumns': ['charging', 'paused'], 'matterTile': 8,
              'surfaceRamp': 2, 'icon': PIC, 'iconAt': [base.PADX, 4], 'lockAt': [base.PADX + 2, 6],
              'textRight': SW - base.PADX, 'capTop': TY,
              'note': 'el tambor: la placa de 192 x 20 en nueve cortes y los dos tabiques en los huecos de 6; cada '
                      'camara: cargando, la materia del poder desde abajo hasta floor(16 * progreso) filas con '
                      'la superficie en el indice 2 que ondea (1 px en el indice 1 cada 6, corriendo cada 2 '
                      'frames); pausada, la columna paused y la superficie en el indice 4, quieta; listo, llena y '
                      '5 chispas por frame (indices 0 y 1) cada 2 frames; encima, un filo de 1 px en str_p[4]; '
                      'pulsado, en str_p[2] y el contenido 2 px abajo; listo_encima, el filo en el indice 0 del '
                      'poder; bloqueado y candado, rayada; nodisp, vacia con el filo en trazos'})
    with open(os.path.join(d, 'ui_power_wheel.json'), 'w', encoding='utf-8') as fh:
        json.dump(j, fh, indent=1, ensure_ascii=False)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'neutral', 'hielo', 'decaimiento', 'gusano') | {tuple(PANEL), tuple(FONDO)})
