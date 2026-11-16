"""NOVA - ART-UI-WHEEL v2, la rueda de poderes del Pulsar: «Las mechas».

Ref: NOVA_Assets_Diseno.md A.6 (y `ART-UI-CHARGE` «La mecha») · Spec 7.1,
13.5, 13.12, 14.4, 16.3, 18 #34, #35

Lo comun -los estados, el texto, las comprobaciones y las vistas- vive en
`gen_wheel_v1.py`.

LA TESIS DE LA v2: LA RUEDA Y LA BARRA SON LA MISMA COSA. Cada ranura es
una tecla de 13.12 con el texto arriba y, en su base, la MECHA de su poder,
la misma de `ART-UI-CHARGE` (6 x 48: el casquillo, la mecha con el dibujo
del haz y la llama). Sin carga, la mecha esta entera y apagada: es el
poder que esperas. Encima, se enciende y corre. Cargando, la ranura ensena
EXACTAMENTE la barra que hay bajo el Pulsar -lo que arde, la llama en el
frente y lo que falta en puntos-, y pausada, la brasa. Lista, la mecha
entera con la estrella en la punta y la luz que la recorre: la ranura
brilla igual que la barra. No hay pictogramas: el dibujo del haz es el
pictograma.

    bloqueado   la mecha en gris, quieta
    candado     sin mecha: el candado y `Nv N`, y la pista en gris
    nodisp      el marco en trazos y el numero

PIEZAS
    ui_power_wheel_plate.png    20 x 20, nueve cortes; fila = poder,
                                columna = estado
    ui_power_wheel_fuse_off.png la mecha apagada (quieta) por poder, y la
                                gris, teselas de 8 x 6
    ui_power_wheel.json
    La mecha que corre, la llama, la pista y el casquillo son las piezas de
    `ART-UI-CHARGE` v3 (`art/ui/charge_bar/v3/`).
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_wheel_v1 as base
from gen_wheel_v1 import (BAJA, CELDA_H, ESTADOS, PODERES, RAMPA, SH, SW, candado, json_comun,
                          pinta_textos, rayas, textos, trazos_placa)
import gen_charge_v1 as c1
import gen_charge_v3 as c3
import gen_panel_v1 as pv1
from gen_panel_v1 import FONDO, NEU, PANEL, STR_P, colores_de, pega, rgba

VERSION = 2
NOMBRE = 'Las mechas'
BORDE = 6
TY = 4                           # la mayuscula, filas 4-13: la mecha dibuja en las 14-15
BY = 12                          # la mecha, filas 12-17
BX = (SW - c3.ANCHO) // 2        # 6


def agrisa(img):
    """Cada color a la banda gris de su luminancia."""
    out = img.copy()
    m = out[..., 3] > 0
    lum = out[..., :3].astype(float) @ np.array([0.3, 0.59, 0.11])
    idx = np.clip((255 - lum) / 255 * 6, 2, 5).astype(int)
    for k in range(2, 6):
        out[m & (idx == k), :3] = NEU[k]
    return out


def apagada(p):
    """La mecha entera y quieta, con el casquillo y sin llama."""
    img = rgba(6, c3.ANCHO)
    img[:, :c3.CASQUILLO] = c3.casquillo('player')
    m = c1.tesela(c3.LARGO, c3.mecha(p, 0, pausada=True))
    sub = img[:, c3.X0:]
    mm = m[..., 3] > 0
    sub[mm] = m[mm]
    return img


def encendida(p, f):
    img = rgba(6, c3.ANCHO)
    img[:, :c3.CASQUILLO] = c3.casquillo('player')
    m = c1.tesela(c3.LARGO, c3.mecha(p, f))
    sub = img[:, c3.X0:]
    mm = m[..., 3] > 0
    sub[mm] = m[mm]
    return img


def marco_de(estado, p):
    R = RAMPA[p]
    return {'normal': STR_P[6], 'encima': STR_P[4], 'pulsado': STR_P[4], 'bloqueado': NEU[5],
            'candado': NEU[5], 'nodisp': NEU[5], 'cargando': R[4], 'listo': R[2],
            'listo_encima': R[0], 'listo_pulsado': R[0]}[estado]


def placa(estado, p):
    relleno = RAMPA[p][6] if estado in ('listo_encima', 'listo_pulsado') else PANEL
    pl = base.tecla(marco_de(estado, p), relleno=relleno)
    if estado == 'nodisp':
        trazos_placa(pl)
    return pl


def textos_v2(estado, p):
    R = RAMPA[p]
    if estado.startswith('listo'):
        return [(f'{base.numero(p)} · ', R[0]), (str(base.COSTE[p]), R[1])]
    if estado == 'cargando':
        return [(f'{base.numero(p)} · ', R[1]), (str(base.COSTE[p]), R[2])]
    return textos(estado, p)


def ranura(estado, p, progreso=0.45, pausada=False, f=0, t=1.0):
    img = rgba(CELDA_H, SW)
    dy = BAJA if estado in ('pulsado', 'listo_pulsado') else 0
    cuerpo = pv1.nueve(placa(estado, p), SW, SH, BORDE)
    barra = None
    if estado == 'normal':
        barra = apagada(p)
    elif estado in ('encima', 'pulsado'):
        barra = encendida(p, f)
    elif estado == 'bloqueado':
        barra = agrisa(apagada(p))
    elif estado == 'candado':
        barra = agrisa(c3.barra(p, 0, 'player'))
    elif estado == 'cargando':
        inv = min(base.COSTE[p] - 1, int(round(progreso * base.COSTE[p])))
        barra = c3.barra(p, inv, 'player', pausada, f)
    elif estado.startswith('listo'):
        barra = c3.barra(p, base.COSTE[p], 'player', False, f)
    if barra is not None:
        pega(cuerpo, barra, BX, BY)
    if estado == 'candado':
        ts = textos(estado, p)
        w = 7 + 3 + base.ancho_textos(ts)
        x = (SW - w) // 2
        pega(cuerpo, candado(NEU[3], PANEL), x, TY)
        pinta_textos(cuerpo, ts, x + 10, TY)
    else:
        pinta_textos(cuerpo, textos_v2(estado, p), SW // 2, TY, 'centro')
    pega(img, cuerpo, 0, dy)
    return img


def cancelar(estado, devuelve, f=0, t=1.0):
    return base.cancelar_lamina(estado, devuelve, f, t)


COLS = ('normal', 'encima', 'pulsado', 'bloqueado', 'candado', 'nodisp', 'cargando', 'listo', 'listo_encima')


def piezas():
    d = {}
    for p in PODERES:
        for e in COLS:
            d[f'placa_{p}_{e}'] = placa(e, p)
        d[f'apagada_{p}'] = c3.mecha(p, 0, pausada=True)
        d[f'gris_{p}'] = agrisa(c3.mecha(p, 0, pausada=True))
    d['candado'] = candado(NEU[3], PANEL)
    return d


def comprobar_propio():
    fallos = []
    if TY + 10 > BY + 2:
        fallos.append('el texto pisa la mecha')
    if BY + 6 > SH - 2:
        fallos.append('la mecha pisa el marco')
    for p in PODERES:
        if base.ancho_textos(textos('normal', p)) > SW - 8:
            fallos.append(f'{p}: el texto no cabe')
    return fallos


def exportar(d):
    p = piezas()
    Image.fromarray(np.vstack([np.hstack([p[f'placa_{q}_{e}'] for e in COLS]) for q in PODERES]), 'RGBA').save(
        os.path.join(d, 'ui_power_wheel_plate.png'))
    Image.fromarray(np.vstack([np.hstack([p[f'apagada_{q}'], p[f'gris_{q}']]) for q in PODERES]), 'RGBA').save(
        os.path.join(d, 'ui_power_wheel_fuse_off.png'))
    Image.fromarray(p['candado'], 'RGBA').save(os.path.join(d, 'ui_power_wheel_lock.png'))
    j = json_comun()
    j.update({'plateCell': 20, 'plateBorder': BORDE, 'plateCenterMode': 'tiled', 'plateRows': list(PODERES),
              'plateColumns': list(COLS), 'capTop': TY, 'fuseAt': [BX, BY], 'fuseOffColumns': ['off', 'grey'],
              'chargeArt': 'art/ui/charge_bar/v3/ (ui_charge_socket, ui_charge_track, ui_charge_fuse_sheet, '
                           'ui_charge_flame_sheet)',
              'note': 'texto centrado arriba; en la base, en fuseAt, una barra de 48 x 6 montada como la de '
                      'ART-UI-CHARGE: normal, el casquillo del Jugador y la mecha apagada (fuse_off) en mosaico; '
                      'encima y pulsado, la mecha que corre; cargando, la barra de carga tal cual (con la brasa si '
                      'esta pausada); listo, la barra lista; bloqueado, la mecha gris; candado, la pista en gris '
                      'y el candado con Nv N centrados arriba; nodisp, sin barra'})
    with open(os.path.join(d, 'ui_power_wheel.json'), 'w', encoding='utf-8') as fh:
        json.dump(j, fh, indent=1, ensure_ascii=False)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'neutral', 'hielo', 'decaimiento', 'gusano') | {tuple(PANEL), tuple(FONDO)})
