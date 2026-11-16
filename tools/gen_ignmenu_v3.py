"""NOVA - ART-UI-IGN-MENU v3, el menu radial de encendido: «Las mechas».

Ref: NOVA_Assets_Diseno.md A.6 (y `ART-WLD-LINK`, `ART-ST-IGN`) · Spec 13.7,
13.12 (patrones «Flujo» e «Ignicion»), 16.1, 18 #29, #36

Lo comun -la geometria, los estados, los pictogramas, la entrada, las
comprobaciones y las vistas- vive en `gen_ignmenu_v1.py`.

LA TESIS DE LA v3: LA ENERGIA DEL ASTEROIDE VA HACIA LO QUE PUEDE SER. Del
borde del Asteroide salen tres FILAMENTOS de plasma del Jugador
(`plasma_p`, el de la linea de envio) hasta cada opcion, una tecla de 40 x
40 con el pictograma y el coste. Al abrir, crecen desde el Asteroide con
su opcion. Si el Asteroide tiene los 10, por cada filamento corre un pulso
blanco hacia fuera cada 2.67 s (patron «Flujo»): la Energia esta lista
para ir. Si no los tiene, el filamento esta encendido solo hasta
`Energia / 10` de su largo, quieto, y LO QUE FALTA son puntos magenta hasta
la tecla: la misma lectura que «El barrido» (`ART-UI-BTN-CONV`).

    encima      el filamento sube un tono y lleva dos pulsos; la tecla,
                marco `str_p[4]` y pictograma claro
    pulsado     el filamento destella en blanco entero y la tecla baja 2 px
    falta       la tecla gris y el coste en magenta; el filamento, como arriba
    nodisp      sin filamento; la tecla con el marco en trazos y el
                pictograma en contorno, sin coste

PIEZAS
    ui_ignition_menu_plate.png      20 x 20, nueve cortes, una columna por
                                    estado
    ui_ignition_menu_icons.png      los pictogramas, fila = tipo
    ui_ignition_menu_filament.png   teselas de 4 x 2 del filamento, rectas
                                    (se giran al montar): fila 0, el que
                                    corre (32 frames, el pulso a lo largo);
                                    filas 1-4 quietas: encendido, encima,
                                    destello, falta (puntos)
    ui_ignition_menu.json
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_ignmenu_v1 as base
from gen_ignmenu_v1 import (BAJA, CELDA, ESTADOS, ENTRADA_MS, ESCALON_MS, PICTO, TIPOS, angulo,
                            hoja_pictos, json_comun, picto, rotula_coste)
import gen_panel_v1 as pv1
from gen_panel_v1 import FONDO, NEU, PANEL, STR_E, STR_P, colores_de, pega, rgba
from nova_core import rampa

VERSION = 3
NOMBRE = 'Las mechas'
FALTA_CRECE = True
LADO = 40
PZ, BORDE = 20, 6
PICTO_Y = 4
COSTE_Y = 26
FRAMES = 32
R0 = 23                         # del centro al arranque: el borde del Asteroide (43 px) mas 1
R1 = base.DIST - LADO // 2 - 4  # hasta antes de la tecla
PL = rampa('plasma_p')
PICTOS = {'claro': (STR_P[1], STR_P[3], False), 'encima': (STR_P[0], STR_P[2], False),
          'gris': (NEU[3], NEU[4], False), 'apagado': (NEU[4], NEU[4], True)}


def tecla(estado):
    return base.tecla(estado)


def opcion(estado, tipo, energia, f=0, t=1.0):
    img = rgba(CELDA, CELDA)
    dy = BAJA if estado == 'pulsado' else 0
    cuerpo = pv1.nueve(tecla(estado), LADO, LADO, BORDE)
    clave = {'normal': 'claro', 'encima': 'encima', 'pulsado': 'encima', 'falta': 'gris',
             'nodisp': 'apagado'}[estado]
    pega(cuerpo, picto(tipo, *PICTOS[clave]), (LADO - PICTO) // 2, PICTO_Y)
    rotula_coste(cuerpo, estado, LADO // 2, COSTE_Y)
    o = (CELDA - LADO) // 2
    pega(img, cuerpo, o, o - BAJA // 2 + dy)
    return img


def color_en(estado, s, largo, energia, f):
    """Color del filamento a s px de su arranque (0..largo), o None."""
    if estado == 'pulsado':
        return PL[0] if s % 1 == 0 else PL[1]
    if estado == 'falta':
        hasta = largo * min(1.0, energia / base.COSTE)
        if s < hasta:
            return PL[3]
        return STR_E[4] if int(s) % 3 == 0 else None
    fase = (f % FRAMES) / FRAMES
    pulsos = (fase,) if estado == 'normal' else (fase, (fase + 0.5) % 1)
    cuerpo = PL[3] if estado == 'normal' else PL[2]
    for p in pulsos:
        cabeza = p * (largo + 6) - 3
        if 0 <= cabeza - s < 1:
            return PL[0]
        if 0 <= cabeza - s < 3:
            return PL[1]
    return cuerpo


def centro(img, c, estados, energia, f, t_entrada):
    for i, e in enumerate(estados):
        if e == 'nodisp':
            continue
        k = 1.0 if t_entrada is None else base.orbital((t_entrada - i * ESCALON_MS) / ENTRADA_MS)
        largo = R1 - R0
        hasta = largo * k
        a = angulo(i)
        ux, uy = math.cos(a), -math.sin(a)
        px, py = -uy, ux
        s = 0.0
        while s <= hasta:
            col = color_en(e, s, largo, energia, f)
            if col is not None:
                r = R0 + s
                for off in (0, 1):
                    x = int(round(c + r * ux + off * px))
                    y = int(round(c + r * uy + off * py))
                    img[y, x, :3] = col
                    img[y, x, 3] = 255
            s += 0.5


def piezas():
    d = {f'tecla_{e}': tecla(e) for e in ESTADOS}
    d['pictos'] = hoja_pictos(PICTOS)
    d['filamento'] = hoja_filamento()
    return d


def hoja_filamento():
    """Teselas de 4 x 2 en fila: la del que corre por frame, y las quietas."""
    def tes(col):
        t = rgba(2, 4)
        t[..., :3] = col
        t[..., 3] = 255
        return t
    fila0 = []
    for f in range(FRAMES):
        t = rgba(2, 4)
        for x in range(4):
            col = color_en('normal', x + (f % 4), 4 + (f % 4), 10, f)
            t[:, x, :3] = col
            t[:, x, 3] = 255
        fila0.append(t)
    vacia = rgba(2, 4)
    puntos = rgba(2, 4)
    puntos[:, 0, :3] = STR_E[4]
    puntos[:, 0, 3] = 255
    quietas = [tes(PL[3]), tes(PL[2]), tes(PL[0]), puntos]
    return np.vstack([np.hstack(fila0)] + [np.hstack([q] + [vacia] * (FRAMES - 1)) for q in quietas])


def comprobar_propio():
    fallos = []
    if R1 - R0 < 10:
        fallos.append('el filamento es demasiado corto')
    if PICTO_Y + PICTO > COSTE_Y - 3:
        fallos.append('el pictograma pisa el coste')
    if COSTE_Y + 10 > LADO - 2:
        fallos.append('el coste pisa el marco')
    # el pulso recorre el filamento y el bucle cierra
    a = base.menu(sys.modules[__name__], ('normal',) * 3, 10, f=0)
    b = base.menu(sys.modules[__name__], ('normal',) * 3, 10, f=FRAMES)
    if not (a == b).all():
        fallos.append('el pulso no cierra el bucle')
    if (a == base.menu(sys.modules[__name__], ('normal',) * 3, 10, f=FRAMES // 2)).all():
        fallos.append('el pulso no se mueve')
    return fallos


def exportar(d):
    p = piezas()
    Image.fromarray(np.hstack([p[f'tecla_{e}'] for e in ESTADOS]), 'RGBA').save(
        os.path.join(d, 'ui_ignition_menu_plate.png'))
    Image.fromarray(p['pictos'], 'RGBA').save(os.path.join(d, 'ui_ignition_menu_icons.png'))
    Image.fromarray(p['filamento'], 'RGBA').save(os.path.join(d, 'ui_ignition_menu_filament.png'))
    j = json_comun()
    j.update({'key': LADO, 'plateCell': PZ, 'plateBorder': BORDE, 'plateCenterMode': 'tiled',
              'plateColumns': list(ESTADOS), 'icon': PICTO, 'iconTop': PICTO_Y, 'iconRows': list(TIPOS),
              'iconColumns': list(PICTOS), 'costCapTop': COSTE_Y,
              'filament': {'from': R0, 'to': R1, 'width': 2, 'frames': FRAMES, 'fps': 12,
                           'rows': ['flow', 'lit', 'hover', 'flash', 'missingDots'],
                           'missingDotEvery': 3},
              'note': 'tecla de key x key centrada en la opcion, como la v1 sin la carga; el filamento va del '
                      'centro del menu en la direccion de cada opcion, de from a to px, y crece con su opcion en '
                      'la entrada; se puede: flow, un pulso hacia fuera cada 32 frames; encima: hover con dos '
                      'pulsos; pulsado: flash; falta: lit hasta round((to - from) * Energia / 10) y '
                      'missingDots en el resto; nodisp: sin filamento'})
    with open(os.path.join(d, 'ui_ignition_menu.json'), 'w', encoding='utf-8') as fh:
        json.dump(j, fh, indent=1, ensure_ascii=False)


if __name__ == '__main__':
    base.ejecutar(sys.modules[__name__], VERSION, NOMBRE,
                  colores_de('str_p', 'str_e', 'neutral', 'plasma_p') | {tuple(PANEL), tuple(FONDO)})
