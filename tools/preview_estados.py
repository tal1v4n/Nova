"""NOVA - Hoja de contacto de los overlays de estado (A.3).

Un overlay no se juzga solo: se juzga ENCIMA del sprite al que se aplica y
sobre el fondo real. Este script compone cada propuesta sobre su nodo y su
faccion —`Dormido` va sobre el Asteroide Neutral, `SinEncender` sobre el del
Jugador y el del Enemigo (14.1)— y saca los 16 frames en fila a 1x, que es
donde se ve si el simbolo se lee, y ampliados, que es donde se ve si el ciclo
hace lo que dice.

No produce arte. Solo mira.
"""

import importlib
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import RAIZ, a_rgba, dir_preview
from nova_estados import (ASTEROIDE, BUFEABLES, CONGELABLES,
                          SPRITE, SPRITE_PROPIO)
from nova_ojos import OJOS  # solo para `scare`, descartado (T6-12)
from nova_paleta import FONDO, hex_a_rgb

BG = np.array(hex_a_rgb(FONDO), np.uint8)

# La propuesta de `Congelado` contra la que se comprueba la convivencia de
# 14.6. Es la version elegida en A.3, no una cualquiera: cambiarla cambia la
# comparacion entera, porque un prisma macizo y una red de grietas no le
# dejan el mismo sitio al aura de `Infectado`.
HIELO = 'v3'

# (estado, version, modulo, funcion, facciones del sprite de base)
PROPUESTAS = [
    ('sleep', 1, 'gen_state_sleep_v1', 'sleep', ('neutral',)),
    ('sleep', 2, 'gen_state_sleep_v2', 'sleep', ('neutral',)),
    ('ign',   1, 'gen_state_ign_v1',   'ign',   ('player', 'enemy')),
    ('ign',   2, 'gen_state_ign_v2',   'ign',   ('player', 'enemy')),
]


def sobre(base_rgba, capa_rgba):
    """El overlay encima del nodo. Alfa binario: se pisa, no se mezcla."""
    out = base_rgba.copy()
    m = capa_rgba[..., 3] > 0
    out[m] = capa_rgba[m]
    return out


def componer(rgba):
    a = rgba[..., 3:4] > 0
    return np.where(a, rgba[..., :3], BG).astype(np.uint8)


def tira(celdas, escala, hueco=6):
    h, w, _ = celdas[0].shape
    W = len(celdas) * w + (len(celdas) + 1) * hueco
    hoja = np.tile(BG, (h + 2 * hueco, W, 1))
    for i, c in enumerate(celdas):
        x = hueco + i * (w + hueco)
        hoja[hueco:hueco + h, x:x + w] = c
    return np.repeat(np.repeat(hoja, escala, 0), escala, 1)


def apilar(bloques, hueco=10):
    W = max(b.shape[1] for b in bloques)
    H = sum(b.shape[0] for b in bloques) + hueco * (len(bloques) + 1)
    out = np.tile(BG, (H, W, 1))
    y = hueco
    for b in bloques:
        out[y:y + b.shape[0], 0:b.shape[1]] = b
        y += b.shape[0] + hueco
    return out


def _frames(mod, fn, faccion):
    f = getattr(mod, fn)
    n = mod.FRAMES
    if faccion == 'neutral' and fn == 'sleep':
        return [a_rgba(f(i / n)) for i in range(n)]
    return [a_rgba(f(i / n, faccion)) for i in range(n)]


def scare():
    """`Asustado` se juzga distinto: sobre los CATORCE nodos, no sobre uno.

    Lo que hay que ver aqui no es si el ojo es bonito, es si ATERRIZA: si tapa
    el ojo viejo en los cuatro tipos y en todos los niveles, y si el par sigue
    leyendose como dos ojos y no como un visor.
    """
    import importlib
    bloques_1x, bloques_4x = [], []
    normales = []
    for (tipo, nivel), (talla, ai, ad) in sorted(OJOS.items()):
        normales.append(componer(np.array(Image.open(
            os.path.join(RAIZ, 'art', SPRITE[tipo].format(n=nivel))).convert('RGBA'))))
    bloques_1x.append(tira(normales, 1))
    bloques_4x.append(tira(normales, 4))
    for modulo in ('gen_state_scare_v1', 'gen_state_scare_v2'):
        mod = importlib.import_module(modulo)
        cel = []
        for (tipo, nivel), (talla, ai, ad) in sorted(OJOS.items()):
            nodo = np.array(Image.open(
                os.path.join(RAIZ, 'art', SPRITE[tipo].format(n=nivel))).convert('RGBA'))
            cel.append(componer(sobre(nodo, a_rgba(mod.par(talla, ai, ad, 0.0)))))
        bloques_1x.append(tira(cel, 1))
        bloques_4x.append(tira(cel, 4))
        print(f'{modulo}: {len(cel)} nodos')
    d = dir_preview('estados')
    Image.fromarray(apilar(bloques_1x)).save(os.path.join(d, 'scare_1x.png'))
    Image.fromarray(apilar(bloques_4x)).save(os.path.join(d, 'scare_4x.png'))


def frozen():
    """`Congelado` se juzga sobre los TRECE nodos congelables, no sobre uno.

    14.1 lo aplica a «cualquier nodo encendido», y el Asteroide nunca lo
    esta: encenderlo lo convierte en otro tipo (4.1). Asi que aqui no sale.

    Lo que hay que ver en esta hoja son tres cosas, y ninguna es si el hielo
    es bonito: si el nodo sigue leyendose como su tipo, si la CARA sigue ahi
    -14.6 hace `Congelado` compatible con `Asustado`- y si el CONTADOR DE
    NIVEL se sigue contando: las esquirlas del Magnetar y los frentes del
    Pulsar son lo primero que se pierde, y un nodo congelado es justo el que
    el jugador esta decidiendo si captura (7.3).
    """
    import importlib
    bloques_1x, bloques_3x = [], []
    normales = [componer(np.array(Image.open(
        os.path.join(RAIZ, 'art', SPRITE[t].format(n=n))).convert('RGBA')))
        for (t, n) in CONGELABLES]
    bloques_1x.append(tira(normales, 1))
    bloques_3x.append(tira(normales, 3))
    for modulo in ('gen_state_frozen_v1', 'gen_state_frozen_v2',
                   'gen_state_frozen_v3'):
        mod = importlib.import_module(modulo)
        capa = a_rgba(mod.frozen(0.0))
        cel = []
        for (t, n) in CONGELABLES:
            nodo = np.array(Image.open(
                os.path.join(RAIZ, 'art', SPRITE[t].format(n=n))).convert('RGBA'))
            cel.append(componer(sobre(nodo, capa)))
        bloques_1x.append(tira(cel, 1))
        bloques_3x.append(tira(cel, 3))
        print(f'{modulo}: {len(cel)} nodos congelados')
    d = dir_preview('estados')
    Image.fromarray(apilar(bloques_1x)).save(os.path.join(d, 'frozen_1x.png'))
    Image.fromarray(apilar(bloques_3x)).save(os.path.join(d, 'frozen_3x.png'))


def decay():
    """`Infectado` se juzga sobre los trece, en movimiento y CON EL HIELO PUESTO.

    Tres cosas que esta hoja tiene que contestar y las otras no:

      1. Si se lee sin el hielo, sobre los trece y a 1x.
      2. Si se lee CON el hielo. 14.6 marca `Congelado` + `Infectado` como
         «Sí», y los dos son trama: apilados pueden acabar en un borrón
         donde no se distingue ninguno de los dos. El bloque va debajo
         (capa 2) y el aura encima (capa 3), que es el orden de 14.7.
      3. Si el ciclo hace algo. Los 16 frames en fila, sobre un solo nodo.
    """
    import importlib
    from nova_core import RAIZ as _R
    hielo = np.array(Image.open(os.path.join(
        _R, 'art', 'states', 'frozen', HIELO, 'state_frozen_overlay.png')
    ).convert('RGBA'))

    sueltos, helados, ciclos = [], [], []
    normales = [componer(np.array(Image.open(
        os.path.join(RAIZ, 'art', SPRITE[t].format(n=n))).convert('RGBA')))
        for (t, n) in CONGELABLES]
    sueltos.append(tira(normales, 1))
    helados.append(tira(normales, 1))
    for modulo in ('gen_state_decay_v1', 'gen_state_decay_v2',
                   'gen_state_decay_v3'):
        mod = importlib.import_module(modulo)
        capa = a_rgba(mod.decay(0.0))
        a, b = [], []
        for (t, n) in CONGELABLES:
            nodo = np.array(Image.open(
                os.path.join(RAIZ, 'art', SPRITE[t].format(n=n))).convert('RGBA'))
            a.append(componer(sobre(nodo, capa)))
            b.append(componer(sobre(sobre(nodo, hielo), capa)))
        sueltos.append(tira(a, 1))
        helados.append(tira(b, 1))
        # el ciclo entero sobre un solo nodo: la Estrella N3, que es de
        # tamano medio y tiene corona con la que competir
        nodo = np.array(Image.open(
            os.path.join(RAIZ, 'art', SPRITE['str'].format(n=3))).convert('RGBA'))
        ciclos.append(tira([componer(sobre(nodo, a_rgba(mod.decay(i / mod.FRAMES))))
                            for i in range(mod.FRAMES)], 2))
        print(f'{modulo}: {len(a)} nodos, {mod.FRAMES} frames')
    d = dir_preview('estados')
    Image.fromarray(apilar(sueltos)).save(os.path.join(d, 'decay_1x.png'))
    Image.fromarray(apilar(helados)).save(os.path.join(d, 'decay_con_hielo_1x.png'))
    Image.fromarray(apilar(ciclos)).save(os.path.join(d, 'decay_ciclo_2x.png'))


def conv():
    """`Reconvirtiendo` se juzga sobre EL CORTE, que es su único trabajo.

    Las otras hojas componen un overlay sobre un nodo fijo. Esta no puede:
    al terminar la reconversión el nodo es de **otro tipo y de nivel 1**
    (8.1), así que el animador cambia de sprite a mitad de la animación y lo
    que hay que ver es si ese cambio se nota. Aquí se compone la primera
    mitad sobre el nodo de origen y la segunda sobre el de destino, que es
    lo que va a pasar en pantalla.

    El caso que se enseña es el peor de los seis posibles: **Magnetar N5 →
    Estrella N1**, que es el que más cambia de silueta (de 1.05 u con cinco
    esquirlas a 0.61 u sin corona) y el que 8.3 usa de ejemplo.
    """
    import importlib
    origen = np.array(Image.open(os.path.join(
        RAIZ, 'art', 'nodes', 'magnetar', 'v1', 'mag_lvl5_player.png')
    ).convert('RGBA'))
    destino = np.array(Image.open(os.path.join(
        RAIZ, 'art', 'nodes', 'estrella', 'v1', 'str_lvl1_player.png')
    ).convert('RGBA'))

    crudo, bloques = [], []
    for i in range(36):
        crudo.append(componer(origen if i < 17 else destino))
    bloques.append(tira(crudo, 2))
    for modulo in ('gen_state_conv_v1', 'gen_state_conv_v2',
                   'gen_state_conv_v3'):
        mod = importlib.import_module(modulo)
        cel = []
        for i in range(mod.FRAMES):
            base = origen if i < 17 else destino
            args = () if modulo.endswith('v3') else ('player',)
            cel.append(componer(sobre(base, a_rgba(mod.conv(i / mod.FRAMES, *args)))))
        bloques.append(tira(cel, 2))
        print(f'{modulo}: {len(cel)} frames sobre el corte mag5 -> str1')
    d = dir_preview('estados')
    Image.fromarray(apilar(bloques)).save(os.path.join(d, 'conv_corte_2x.png'))


def cap():
    """`Capturado` se juzga sobre el CAMBIO DE PALETA, y sobre los catorce.

    Es el overlay más ancho de A.3: 14.1 lo aplica a «cualquier nodo», así
    que aquí sale también el Asteroide, que es el más pequeño del reparto y
    el que más fácil queda enterrado bajo un fogonazo.

    Cada fila enseña los 5 frames sobre cuatro nodos, con el sprite del
    dueño **nuevo** desde el frame 0: 14.1 dice que el nodo ya es funcional
    para su nuevo dueño desde ese frame, así que el cambio de paleta ocurre
    antes que el efecto, no después. Lo que hay que ver es si el efecto
    consigue que ese cambio se lea como un suceso y no como un parpadeo.
    """
    import importlib
    casos = [('Asteroide', 'nodes/asteroide/v1/ast_lvl0_{f}.png', 0),
             ('Estrella N3', 'nodes/estrella/v1/str_lvl3_{f}.png', 0),
             ('Púlsar N2', 'nodes/pulsar/v2/pul_lvl2_{f}.png', 0),
             ('Magnetar N4', 'nodes/magnetar/v1/mag_lvl4_{f}.png', 0)]
    bloques = []
    antes, despues = [], []
    for _, ruta, _ in casos:
        antes.append(componer(np.array(Image.open(os.path.join(
            RAIZ, 'art', ruta.format(f='enemy'))).convert('RGBA'))))
        despues.append(componer(np.array(Image.open(os.path.join(
            RAIZ, 'art', ruta.format(f='player'))).convert('RGBA'))))
    bloques.append(tira(antes + despues, 2))
    for modulo in ('gen_state_cap_v1', 'gen_state_cap_v2', 'gen_state_cap_v3'):
        mod = importlib.import_module(modulo)
        cel = []
        for _, ruta, _ in casos:
            nodo = np.array(Image.open(os.path.join(
                RAIZ, 'art', ruta.format(f='player'))).convert('RGBA'))
            for i in range(mod.FRAMES):
                cel.append(componer(sobre(nodo, a_rgba(mod.cap(i / mod.FRAMES)))))
        bloques.append(tira(cel, 2))
        print(f'{modulo}: {len(cel)} celdas (4 nodos x {mod.FRAMES} frames)')
    d = dir_preview('estados')
    Image.fromarray(apilar(bloques)).save(os.path.join(d, 'cap_2x.png'))


def buff():
    """`FX-BUFF` se juzga EN GRUPO, que es como se va a ver.

    Es el único overlay que se aplica a todos los nodos de una facción a la
    vez y dura 20 s (9.1): en pantalla van a estar ocho o diez marcados
    simultáneamente y en fase. Esa es justo la situación en la que el
    Asteroide v2 se cayó por leer como papel pintado (A.2.1), así que la
    pregunta de esta hoja no es «¿se ve la marca?» sino **«¿ocho marcas a la
    vez siguen siendo información o ya son textura?»**.

    Cada bloque enseña el grupo entero de cada power-up —Rayo sobre Magnetar
    y Púlsar, Hidrógeno sobre Estrella— en cuatro momentos del ciclo, que es
    donde se ve si el conjunto late o parpadea.
    """
    import importlib
    bloques = []
    crudo = []
    for power in ('rayo', 'hidrogeno'):
        for (t, n) in BUFEABLES[power]:
            crudo.append(componer(np.array(Image.open(os.path.join(
                RAIZ, 'art', SPRITE_PROPIO[t].format(n=n))).convert('RGBA'))))
    bloques.append(tira(crudo, 2))
    for modulo in ('gen_state_buff_v1', 'gen_state_buff_v2',
                   'gen_state_buff_v3'):
        mod = importlib.import_module(modulo)
        pasos = [0] if mod.FRAMES == 1 else [0, 4, 8, 12]
        for f in pasos:
            cel = []
            for power in ('rayo', 'hidrogeno'):
                capa = a_rgba(mod.buff(f / mod.FRAMES, power))
                for (t, n) in BUFEABLES[power]:
                    nodo = np.array(Image.open(os.path.join(
                        RAIZ, 'art', SPRITE_PROPIO[t].format(n=n))).convert('RGBA'))
                    cel.append(componer(sobre(nodo, capa)))
            bloques.append(tira(cel, 2))
        print(f'{modulo}: {len(pasos)} momentos del ciclo x 13 nodos')
    d = dir_preview('estados')
    Image.fromarray(apilar(bloques)).save(os.path.join(d, 'buff_2x.png'))


def main():
    por_estado = {}
    for estado, version, modulo, fn, facciones in PROPUESTAS:
        mod = importlib.import_module(modulo)
        for fac in facciones:
            nodo = np.array(Image.open(
                os.path.join(RAIZ, 'art', ASTEROIDE.format(fac))).convert('RGBA'))
            celdas = [componer(sobre(nodo, c)) for c in _frames(mod, fn, fac)]
            por_estado.setdefault(estado, {'1x': [], '4x': []})
            por_estado[estado]['1x'].append(tira(celdas, 1))
            por_estado[estado]['4x'].append(tira(celdas[::4], 4))
            print(f'{estado} v{version} sobre ast_lvl0_{fac}: {len(celdas)} frames')

    d = dir_preview('estados')
    for estado, b in por_estado.items():
        Image.fromarray(apilar(b['1x'])).save(os.path.join(d, f'{estado}_1x.png'))
        Image.fromarray(apilar(b['4x'])).save(os.path.join(d, f'{estado}_4x.png'))
    scare()
    frozen()
    decay()
    conv()
    cap()
    buff()
    print(f'escritas en {d}')


if __name__ == '__main__':
    main()
