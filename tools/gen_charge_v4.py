"""NOVA - ART-UI-CHARGE v4, la barra de carga del Pulsar: «El latido».

Ref: NOVA_Assets_Diseno.md A.6 (`ART-UI-CHARGE` v3 «La mecha») · Spec 7.1,
7.5, 13.12 (patrones «Sonar» y «Flujo»), 14.4, 18 #48

Segunda ronda sobre «La mecha» (2026-11-16), pedida por el usuario al verla
en Play: la barra tiene una marca al principio -el casquillo- y ninguna al
final, y LISTA casi no se distingue de CASI: solo se nota en que deja de
moverse. Las tres propuestas de esta ronda -v4, v5 y v6- conservan la carga
de la v3 tal cual y cambian dos cosas:

    EL REMATE   el casquillo del bando, reflejado, cierra la barra por la
                derecha: se ve cuanto mide y cuanto falta. La mecha pasa de
                46 a 44 px.
    LISTA       un estado que no se parece a ninguna carga.

Este archivo tiene lo comun a las tres -la barra con remate, la comparativa
de LISTA frente a CASI- y su propio LISTA; `gen_charge_v5.py` y
`gen_charge_v6.py` solo traen el suyo. El sitio, los estados, las
comprobaciones y el resto de vistas siguen en `gen_charge_v1.py`.

LA TESIS DE LA v4: LISTA ES OTRA COSA, NO UNA MECHA MAS LARGA. Al llegar al
final, la mecha desaparece: en su sitio queda un HAZ SOLIDO del color del
poder -cuerpo en el indice 2, nucleo blanco en las filas 2-3- que LATE,
encendido 4 frames y apagado otros 4 (3 veces por segundo), con la llama
hecha estrella pegada al remate. Una carga nunca parpadea, asi que el
parpadeo es LISTA aunque se mire de reojo.
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_charge_v1 as base
from gen_charge_v1 import (ALTO_MAX, FACCIONES, FONDO, FRAMES, PODERES, ampliar, coste, pon, rampa_poder,
                           rgba, sobre, tesela)
import gen_charge_v3 as v3
from nova_core import RAIZ, dir_preview

VERSION = 4
NOMBRE = 'El latido'
ANCHO = 48
CAP = v3.CASQUILLO                 # 2
X0 = v3.X0                         # 2
LARGO = ANCHO - X0 - CAP           # 44: la mecha entre casquillo y remate
LLAMA_W = v3.LLAMA_W
FRENTE = v3.FRENTE


def remate(fac):
    """El casquillo del bando, reflejado: cierra la barra por la derecha."""
    return v3.casquillo(fac)[:, ::-1].copy()


def barra_con_remate(p, inv, fac, pausada, f, lista_fn):
    """La barra de la v3 con remate. Cargando, igual que la v3; lista, lo que
    pinte lista_fn(img, p, fac, f) entre casquillo y remate."""
    lista = inv >= coste(p)
    img = rgba(ALTO_MAX, ANCHO)
    img[:, :CAP] = v3.casquillo(fac)
    img[:, ANCHO - CAP:] = remate(fac)
    zona = tesela(LARGO, v3.pista(fac))
    ok = zona[..., 3] > 0
    img[:, X0:X0 + LARGO][ok] = zona[ok]
    if lista:
        img[:, X0:X0 + LARGO] = 0
        lista_fn(img, p, fac, f)
        return img
    n = LARGO * inv // coste(p)
    if n > 0:
        m = tesela(n, v3.mecha(p, f, pausada))
        sub = img[:, X0:X0 + n]
        sub[:] = 0
        mm = m[..., 3] > 0
        sub[mm] = m[mm]
    if inv > 0:
        fl = v3.llama(p, f, 'brasa' if pausada else 'carga')
        fx = X0 + n - 1 - FRENTE
        for y in range(ALTO_MAX):
            for x in range(LLAMA_W):
                if fl[y, x, 3] and X0 <= fx + x < ANCHO - CAP:
                    img[y, fx + x] = fl[y, x]
    return img


def estrella(img, p, f):
    """La llama lista de la v3, pegada al remate."""
    fl = v3.llama(p, f, 'lista')
    fx = ANCHO - CAP - LLAMA_W
    for y in range(ALTO_MAX):
        for x in range(LLAMA_W):
            if fl[y, x, 3]:
                img[y, fx + x] = fl[y, x]


# --- La comparativa de esta ronda: LISTA frente a CASI -----------------------

def lista_vs_casi(gen, frames=(0, 4, 8, 12)):
    """Por poder y bando: la barra a coste - 1 y la lista en cuatro frames."""
    filas = []
    for p in PODERES:
        for fac in FACCIONES:
            fila = []
            for estado, f in [('casi', 0)] + [('lista', f) for f in frames]:
                c = np.empty((ALTO_MAX + 4, ANCHO + 6, 3), np.uint8)
                c[:] = FONDO
                inv = coste(p) - 1 if estado == 'casi' else coste(p)
                sobre(c, gen.barra(p, inv, fac, False, f), 3, 2)
                fila.append(c)
            filas.append(np.hstack(fila))
    return np.vstack(filas)


def comparativa_ronda():
    """v3 y las tres de esta ronda, una al lado de otra."""
    mods = [v3]
    for v in (4, 5, 6):
        if os.path.isdir(os.path.join(RAIZ, 'art', 'ui', base.ASSET, f'v{v}')):
            mods.append(sys.modules[__name__] if v == 4 else __import__(f'gen_charge_v{v}'))
    cols = []
    for m in mods:
        cols.append(lista_vs_casi(m))
        cols.append(np.zeros((cols[-1].shape[0], 4, 3), np.uint8))
    Image.fromarray(ampliar(np.hstack(cols[:-1]), 6)).save(
        os.path.join(dir_preview('ui_' + base.ASSET), 'lista_ronda2_x6.png'))
    # y en contexto: los dos Pulsares enemigos del nivel 9, el de abajo listo
    t = [m.__dict__.get('tablero', base.tablero)(m) if False else base.tablero(m) for m in mods]
    recortes = [im[0:540, 560:960] for im in t]
    hueco = np.zeros((540, 4, 3), np.uint8)
    out = []
    for r in recortes:
        out += [r, hueco]
    Image.fromarray(ampliar(np.hstack(out[:-1]), 2)).save(
        os.path.join(dir_preview('ui_' + base.ASSET), 'tablero_ronda2_x2.png'))


def gif_lista(gen, version):
    """La barra pasa de casi a lista y se queda lista, a x8."""
    d = os.path.join(dir_preview('ui_' + base.ASSET), f'v{version}')
    ims = []
    for k in range(48):
        c = np.empty((3 * (ALTO_MAX + 4), ANCHO + 6, 3), np.uint8)
        c[:] = FONDO
        for i, p in enumerate(PODERES):
            inv = coste(p) - 1 if k < 12 else coste(p)
            sobre(c, gen.barra(p, inv, 'player', False, k % FRAMES), 3, 2 + i * (ALTO_MAX + 4))
        ims.append(Image.fromarray(ampliar(c, 8)))
    ims[0].save(os.path.join(d, 'casi_a_lista_x8.gif'), save_all=True, append_images=ims[1:],
                duration=1000 // base.FPS, loop=0)


def exportar_comun(d, extra_json, extra_piezas):
    """Las piezas de la v3 más el remate, y lo propio de cada propuesta."""
    Image.fromarray(np.vstack([v3.casquillo(f) for f in FACCIONES]), 'RGBA').save(os.path.join(d, 'ui_charge_socket.png'))
    Image.fromarray(np.vstack([remate(f) for f in FACCIONES]), 'RGBA').save(os.path.join(d, 'ui_charge_cap.png'))
    Image.fromarray(np.vstack([v3.pista(f) for f in FACCIONES]), 'RGBA').save(os.path.join(d, 'ui_charge_track.png'))
    Image.fromarray(np.vstack([np.hstack([v3.mecha(p, f) for f in range(FRAMES)] + [v3.mecha(p, 0, True)])
                               for p in PODERES]), 'RGBA').save(os.path.join(d, 'ui_charge_fuse_sheet.png'))
    filas = []
    for p in PODERES:
        filas.append(np.hstack([v3.llama(p, f) for f in range(FRAMES)] + [v3.llama(p, 0, 'brasa')]))
        filas.append(np.hstack([v3.llama(p, f, 'lista') for f in range(FRAMES)] + [rgba(ALTO_MAX, LLAMA_W)]))
    Image.fromarray(np.vstack(filas), 'RGBA').save(os.path.join(d, 'ui_charge_flame_sheet.png'))
    for nombre, im in extra_piezas.items():
        Image.fromarray(im, 'RGBA').save(os.path.join(d, nombre))
    j = {'width': ANCHO, 'height': ALTO_MAX, 'socketWidth': CAP, 'capWidth': CAP, 'fuseX0': X0, 'fuseLength': LARGO,
         'fuseTile': v3.T, 'flameWidth': LLAMA_W, 'flameFront': FRENTE, 'frames': FRAMES, 'fps': base.FPS,
         'powers': list(PODERES), 'factions': list(FACCIONES), 'fuseRows': list(PODERES), 'fusePausedColumn': FRAMES,
         'flameRows': [f'{p}_{m}' for p in PODERES for m in ('charging', 'ready')], 'flameEmberColumn': FRAMES,
         'readyFlameX': ANCHO - CAP - LLAMA_W}
    j.update(extra_json)
    with open(os.path.join(d, 'ui_charge.json'), 'w') as fh:
        json.dump(j, fh, indent=1)


def ejecutar(gen, version, nombre, permitidos):
    colores = base.comprobar(gen, permitidos, nombre)
    if hasattr(gen, 'comprobar_ronda'):
        gen.comprobar_ronda()
    d = os.path.join(RAIZ, 'art', 'ui', base.ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)
    gen.exportar(d)
    base.vistas(gen, version, '--gif' in sys.argv)
    os.makedirs(os.path.join(dir_preview('ui_' + base.ASSET), f'v{version}'), exist_ok=True)
    Image.fromarray(ampliar(lista_vs_casi(gen), 8)).save(
        os.path.join(dir_preview('ui_' + base.ASSET), f'v{version}', 'lista_vs_casi_x8.png'))
    gif_lista(gen, version)
    comparativa_ronda()
    print(f'v{version} «{nombre}»: {colores} colores  -> {os.path.relpath(d, RAIZ)}')


def comprobar_remate(gen):
    """El remate esta en todas las barras, cargando o listas, y la carga no lo pisa."""
    fallos = []
    for p in PODERES:
        for fac in FACCIONES:
            for inv in (0, coste(p) // 2, coste(p) - 1, coste(p)):
                for f in (0, 7, 19):
                    b = gen.barra(p, inv, fac, False, f)
                    if not (b[:, ANCHO - CAP:] == remate(fac)).all():
                        fallos.append(f'{p} {fac} {inv}: el remate no esta entero')
    return fallos


# =============================================================================
# v4 «El latido»
# =============================================================================

def haz(p, encendido):
    """Tesela de 8 x 6 del haz solido: cuerpo en el indice 2 (4 apagado) y
    nucleo blanco en las filas 2-3 cuando esta encendido."""
    R = rampa_poder(p)
    t = rgba(ALTO_MAX, 8)
    for x in range(8):
        for y in (1, 2, 3, 4):
            pon(t, y, x, R[2] if encendido else R[4])
        if encendido:
            pon(t, 2, x, R[0])
            pon(t, 3, x, R[1])
    return t


def latido(f):
    return (f // 4) % 2 == 0


def pinta_lista(img, p, fac, f):
    t = tesela(LARGO, haz(p, latido(f)))
    sub = img[:, X0:X0 + LARGO]
    m = t[..., 3] > 0
    sub[m] = t[m]
    estrella(img, p, f)


def barra(p, inv, fac, pausada=False, f=0):
    return barra_con_remate(p, inv, fac, pausada, f, pinta_lista)


def comprobar_propio():
    fallos = comprobar_remate(sys.modules[__name__])
    for p in PODERES:
        on = barra(p, coste(p), 'player', False, 0)
        off = barra(p, coste(p), 'player', False, 4)
        if (on == off).all():
            fallos.append(f'{p}: lista no late')
    return fallos


def exportar(d):
    exportar_comun(d, {
        'ready': 'beat', 'readyBeatFrames': 4,
        'readyTileRows': list(PODERES), 'readyTileColumns': ['on', 'off'],
        'note': 'cargando, como la v3 con la mecha de fuseLength: n = fuseLength * invertido // coste, la llama con su '
                'x = flameFront en la ultima columna de la mecha; lista: la mecha se quita y entre casquillo y remate va '
                'en mosaico la tesela on los frames (f // 4) pares y la off los impares, con la llama lista en '
                'readyFlameX; el remate siempre'},
        {'ui_charge_ready.png': np.vstack([np.hstack([haz(p, True), haz(p, False)]) for p in PODERES])})


if __name__ == '__main__':
    ejecutar(sys.modules[__name__], VERSION, NOMBRE, base.colores_de('str_p', 'str_e', 'hielo', 'decaimiento', 'gusano'))
