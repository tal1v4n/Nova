"""NOVA - ART-UI-WHEEL v1, la rueda de poderes del Pulsar: «Las teclas».

Ref: NOVA_Assets_Diseno.md A.6 (y `ART-UI-PANEL` «La sonda», `ART-UI-CHARGE`
«La mecha») · Spec 7.1, 13.3 (punto 6), 13.5, 13.12, 14.4, 16.3, 18 #34,
#35

Este archivo tiene ademas LO COMUN A LAS TRES PROPUESTAS -los estados, los
pictogramas de los poderes, el texto, las comprobaciones y las vistas sobre
el panel elegido-; `gen_wheel_v2.py` y `gen_wheel_v3.py` solo traen su
dibujo.

QUE ES. Las dos ultimas filas del bloque de poderes del panel de un Pulsar
propio (13.3, 13.5): TRES RANURAS en orden fijo -1 Cero Absoluto, 2
Decaimiento, 3 Agujero de Gusano- de 60 x 20, separadas 6 px, y debajo
«Cancelar · +N», de 192 x 20, mientras hay una carga en curso o un poder
listo (18 #35). Es el sitio de hoy (`InfoPanelState.WheelSlotOffsets` y
`CancelOffset`). La celda mide 22 para que el pulsado baje 2 px. El rotulo
de la fila de arriba -«Agujero de Gusano · 45%»- es texto del panel, no de
este asset. El texto va en TextMeshPro con Jersey 10: `1 · 20` (numero y
coste) o `Nv 2` con el candado; las vistas lo pintan con la propia fuente.

LOS ESTADOS DE UNA RANURA (`PulsarPowerWheel.SlotLook`, 13.5, 18 #34):

    normal          se puede empezar a cargar
    encima          el raton encima (160 ms, orbital)
    pulsado         baja 2 px
    bloqueado       Congelado, Reconvirtiendo u otro poder en carga
                    (`Disabled`): gris
    candado         el nivel no llega (`Locked`): candado y `Nv N`
    nodisp          el mapa no tiene ese poder (`Unavailable`)
    cargando        el poder de la carga en curso, con su progreso; puede ir
                    pausada (sin Energia o Congelado)
    listo           cargado: la ranura BRILLA en el color del poder; un clic
                    en ella apunta (18 #34)
    listo_encima    y el raton encima
    listo_pulsado   y pulsada

Ninguno es «falta Energia»: la carga se paga por drenaje (7.1), asi que en
la rueda no hay magenta.

«CANCELAR»: normal, encima, pulsado y bloqueado.

    py tools/gen_wheel_v1.py         piezas, comprobaciones y vistas
    py tools/gen_wheel_v1.py --gif   y la animada

----------------------------------------------------------------------------

LA TESIS DE LA v1: TRES TECLAS DE LA LAMINA. Cada ranura es el boton
secundario de 13.12 con el PICTOGRAMA del poder a la izquierda -un copo,
un nucleo que se deshace y una espiral, en su color de 14.4- y `1 · 20` a
la derecha. Encima, la trama cian de «Carga». Cargando, la tecla se llena
de izquierda a derecha con una trama del color del poder hasta el
progreso, con el marco en ese color. Lista, la tecla entera es del color
del poder, con el texto oscuro, y una luz la cruza cada 2.67 s: brilla.
Candado, gris con un candado de 7 x 9 en vez del pictograma.
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_panel_v1 as pv1
from gen_panel_v1 import (FILA, FONDO, NEU, PAD, PANEL, STR_E, STR_P, ampliar, ancho, colores_de, pega,
                          pinta, placa_lamina, rgba)
from nova_core import BAYER8, RAIZ, dir_preview, rampa

ASSET = 'power_wheel'
SW, SH = 60, 20
CELDA_H = 22
HUECO = 6
CW = 3 * SW + 2 * HUECO          # 192, «Cancelar»
BAJA = 2
TY = 5
PADX = 4
FRAMES = 32
PODERES = ('zero', 'decay', 'wormhole')
RAMPA = {'zero': rampa('hielo'), 'decay': rampa('decaimiento'), 'wormhole': rampa('gusano')}
COSTE = {'zero': 20, 'decay': 35, 'wormhole': 60}        # 16.3
NIVEL = {'zero': 1, 'decay': 2, 'wormhole': 3}           # PowerDefinition.requiredLevel
NOMBRE = {'zero': 'Cero Absoluto', 'decay': 'Decaimiento', 'wormhole': 'Agujero de Gusano'}
ESTADOS = ('normal', 'encima', 'pulsado', 'bloqueado', 'candado', 'nodisp', 'cargando', 'listo',
           'listo_encima', 'listo_pulsado')
ESTADOS_CANCEL = ('normal', 'encima', 'pulsado', 'bloqueado')
C_TEXTO, C_GRIS, C_APAGADO, C_COSTE = NEU[1], NEU[3], NEU[4], STR_P[3]
OSCURO = PANEL


def numero(p):
    return PODERES.index(p) + 1


def textos(estado, p):
    """[(texto, color)] de la etiqueta de la ranura, o [] sin texto."""
    if estado == 'nodisp':
        return [(str(numero(p)), C_APAGADO)]
    if estado == 'candado':
        return [(f'Nv {NIVEL[p]}', C_GRIS)]
    if estado == 'bloqueado':
        return [(f'{numero(p)} · {COSTE[p]}', C_GRIS)]
    if estado.startswith('listo'):
        return [(f'{numero(p)} · ', OSCURO), (str(COSTE[p]), OSCURO)]
    if estado in ('encima', 'pulsado'):
        return [(f'{numero(p)} · ', STR_P[0]), (str(COSTE[p]), STR_P[1])]
    return [(f'{numero(p)} · ', C_TEXTO), (str(COSTE[p]), C_COSTE)]


def ancho_textos(ts):
    return sum(ancho(t) for t, _ in ts)


def pinta_textos(img, ts, x, y, alinea='izq', oscuro=None):
    """Pinta los tramos seguidos; alinea 'der' termina en x, 'centro' centra en x."""
    w = ancho_textos(ts)
    if alinea == 'der':
        x -= w
    elif alinea == 'centro':
        x -= w // 2
    for t, c in ts:
        capa = rgba(*img.shape[:2])
        pinta(capa, t, x, y, c)
        m = capa[..., 3] > 0
        if oscuro is not None:
            capa[m & oscuro, :3] = OSCURO
        img[m] = capa[m]
        x += ancho(t)


def orbital(t):
    import gen_energy_v2 as e2
    return e2.orbital(max(0.0, min(1.0, t)))


# --- Pictogramas de los poderes y el candado ---------------------------------------

PIC = 12


def picto(p, luz, cuerpo, contorno=False):
    """12 x 12: Cero Absoluto, un copo de seis brazos; Decaimiento, un
    nucleo que suelta tres motas hacia abajo; Agujero de Gusano, una espiral."""
    n = PIC
    yy, xx = np.mgrid[0:n, 0:n] + 0.5
    dx, dy = xx - n / 2, yy - n / 2
    r = np.hypot(dx, dy)
    a_m = np.zeros((n, n), bool)            # luz
    b_m = np.zeros((n, n), bool)            # cuerpo
    if p == 'zero':
        for k in range(3):
            a = k * math.pi / 3
            perp = np.abs(-dx * math.sin(a) + dy * math.cos(a))
            b_m |= (perp < 0.62) & (r < 5.6)
        a_m |= r < 1.3
        for k in range(6):
            a = k * math.pi / 3
            px, py = 4.2 * math.cos(a), 4.2 * math.sin(a)
            a_m |= np.hypot(dx - px, dy - py) < 0.8
    elif p == 'decay':
        b_m |= np.hypot(dx + 0.5, dy + 2.0) < 3.4
        a_m |= np.hypot(dx + 1.5, dy + 3.0) < 1.2
        for (mx, my) in ((2.6, 2.4), (-1.0, 3.8), (1.2, 5.2)):
            b_m |= (np.abs(dx - mx) < 0.75) & (np.abs(dy - my) < 0.75)
    else:
        th = np.arctan2(dy, dx)
        for vuelta in range(3):
            rr = (th + math.pi) / (2 * math.pi) * 1.8 + vuelta * 1.8 + 0.6
            b_m |= (np.abs(r - rr) < 0.55) & (rr < 5.8)
        a_m |= r < 1.0
    img = rgba(n, n)
    if contorno:
        sil = a_m | b_m
        img[sil, :3] = luz
        img[sil, 3] = 255
        return img
    img[b_m, :3] = cuerpo
    img[a_m, :3] = luz
    img[a_m | b_m, 3] = 255
    return img


def candado(c1, c2):
    """7 x 9: el arco y el cuerpo con el ojo de la llave."""
    img = rgba(9, 7)
    for y, x in ((0, 2), (0, 3), (0, 4), (1, 1), (1, 5), (2, 1), (2, 5), (3, 1), (3, 5)):
        img[y, x, :3] = c1
        img[y, x, 3] = 255
    img[4:9, 0:7, :3] = c1
    img[4:9, 0:7, 3] = 255
    img[6, 3, :3] = c2
    img[7, 3, :3] = c2
    return img


def colores_picto(estado, p):
    R = RAMPA[p]
    if estado in ('bloqueado',):
        return NEU[2], NEU[4], False
    if estado == 'nodisp':
        return NEU[4], NEU[4], True
    if estado.startswith('listo'):
        return R[0], OSCURO, False
    return R[1], R[3], False


# --- La fila y el panel ----------------------------------------------------------

def fila(gen, estados, progreso=0.45, pausada=False, f=0, t=1.0):
    """Las tres ranuras en una fila de 192 x 22."""
    img = rgba(CELDA_H, CW)
    if hasattr(gen, 'fondo_fila'):
        gen.fondo_fila(img, estados, f)
    for i, (p, e) in enumerate(zip(PODERES, estados)):
        pega(img, gen.ranura(e, p, progreso, pausada, f, t if e in ('encima',) else 1.0), i * (SW + HUECO), 0)
    return img


# Escenas: (titulo, nivel, estados, progreso, pausada, rotulo, cancel)
ESCENAS = (
    ('en reposo', 3, ('normal', 'encima', 'normal'), 0, False, 'Poderes', None),
    ('nivel 1', 1, ('normal', 'candado', 'candado'), 0, False, 'Poderes', None),
    ('cargando', 3, ('bloqueado', 'bloqueado', 'cargando'), 0.45, False, 'Agujero de Gusano · 45%', ('normal', 27)),
    ('pausada', 3, ('bloqueado', 'bloqueado', 'cargando'), 0.45, True, 'Agujero de Gusano · pausado', ('encima', 27)),
    ('listo', 3, ('bloqueado', 'listo_encima', 'bloqueado'), 1.0, False, 'Decaimiento listo', ('normal', 35)),
    ('mapa sin Gusano', 3, ('listo', 'bloqueado', 'nodisp'), 1.0, False, 'Cero Absoluto listo', ('normal', 20)),
)


def caso_panel(escena):
    titulo, nivel, estados, prog, pau, rot, can = escena
    energia = 62
    bloques = [('info', [('titulo', f'Púlsar · Nivel {nivel}'), ('dato', f'Energía {energia} / 100'),
                         ('dato', 'Carga: 3.75 E/s' if can else 'Carga:')]),
               ('mejora', [('dato', f'Mejora: {(25, 40)[nivel - 1]}') if nivel < 3 else ('aviso', 'NIVEL MÁXIMO')]),
               ('reconv', [('rotulo', 'Reconvertir'), ('vacia',)]),
               ('poderes', [('rotulo', rot), ('vacia',)] + ([('vacia',)] if can else []))]
    return dict(fac='player', tipo='pulsar', nivel=nivel, energia=energia, tope=100, bloques=bloques,
                escena=escena)


class EnPanel:
    """El panel de «La sonda» con los botones de reconversion elegidos y la rueda."""

    def __init__(self, gen):
        import gen_panel_v2 as p2
        self.gen, self.p2 = gen, p2
        self.SALIENTE = getattr(p2, 'SALIENTE', 0)

    def panel(self, caso, lado='izq', f=0):
        import gen_btnconv_v1 as bc
        import gen_btnconv_v2 as bc2
        p = self.p2.panel(caso, lado, f)
        titulo, nivel, estados, prog, pau, rot, can = caso['escena']
        conv = 'bloqueado' if can or any(e.startswith('listo') for e in estados) else 'normal'
        pega(p, bc.par(bc2, 'pulsar', caso['energia'], (conv, conv), f), PAD, PAD + 5 * FILA + 2)
        pega(p, fila(self.gen, estados, prog, pau, f), PAD, PAD + 7 * FILA + 2)
        if can:
            pega(p, self.gen.cancelar(can[0], can[1], f), PAD, PAD + 8 * FILA + 2)
        return p

    def antes_de_pegar(self, *a):
        self.p2.antes_de_pegar(*a)


def fondo_trama(h, w):
    import gen_panel_v2 as p2
    c = np.empty((h, w, 3), np.uint8)
    c[:] = FONDO
    tr = np.tile(p2.trama(), (h // 8 + 1, w // 8 + 1, 1))[:h, :w]
    m = tr[..., 3] > 0
    c[m] = tr[..., :3][m]
    return c


def estados_vista(gen, f=0):
    """Una fila por escena: la rueda y, si toca, «Cancelar»."""
    filas = []
    for esc in ESCENAS:
        _, _, estados, prog, pau, _, can = esc
        c = fondo_trama(2 * CELDA_H + 8, CW + 12)
        pega(c, fila(gen, estados, prog, pau, f), 6, 3)
        if can:
            pega(c, gen.cancelar(can[0], can[1], f), 6, 3 + CELDA_H + 2)
        filas.append(c)
    return np.vstack(filas)


def galeria(gen, f=0):
    sh = EnPanel(gen)
    bg = pv1.fondo(1)
    celdas = []
    for i, k in enumerate((0, 2, 4)):
        c = bg[0:330, 0:330].copy() if i % 2 == 0 else bg[210:540, 330:660].copy()
        pv1.abrir(c, sh, caso_panel(ESCENAS[k]), 56, 165, f=f)
        celdas += [c, np.zeros((330, 3, 3), np.uint8)]
    return np.hstack(celdas[:-1])


def vistas(gen, version, gif=False):
    d = os.path.join(dir_preview('ui_' + ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(estados_vista(gen), 4)).save(os.path.join(d, 'estados_x4.png'))
    Image.fromarray(ampliar(galeria(gen), 2)).save(os.path.join(d, 'en_panel_x2.png'))
    if gif:
        # una carga entera de Cero Absoluto hasta lista, y el cursor encima; a 12 fps
        ims = []
        for k in range(48):
            prog = min(1.0, k / 36)
            e = 'cargando' if prog < 1 else ('listo' if k < 42 else 'listo_encima')
            c = fondo_trama(2 * CELDA_H + 8, CW + 12)
            pega(c, fila(gen, (e, 'bloqueado', 'bloqueado'), prog, False, k), 6, 3)
            pega(c, gen.cancelar('normal', int(20 * prog), k), 6, 3 + CELDA_H + 2)
            ims.append(Image.fromarray(ampliar(c, 4)))
        ims[0].save(os.path.join(d, 'carga_x4.gif'), save_all=True, append_images=ims[1:], duration=83, loop=0)
    return d


def comparativa():
    filas = []
    for v in (1, 2, 3):
        if not os.path.isdir(os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{v}')):
            continue
        mod = sys.modules[__name__] if v == 1 else __import__(f'gen_wheel_v{v}')
        filas.append(estados_vista(mod))
        filas.append(np.zeros((filas[-1].shape[0], 4, 3), np.uint8))
    if filas:
        Image.fromarray(ampliar(np.hstack(filas[:-1]), 3)).save(
            os.path.join(dir_preview('ui_' + ASSET), 'comparativa_x3.png'))


# --- Comprobaciones --------------------------------------------------------------

def comprobar(gen, permitidos, nombre):
    fallos = []
    usados = set()

    def mira(clave, im):
        nonlocal usados
        if not set(np.unique(im[..., 3])) <= {0, 255}:
            fallos.append(f'{clave}: alfa no binario')
        usados |= {tuple(int(v) for v in c) for c in im[..., :3][im[..., 3] > 0]}
    for clave, im in gen.piezas().items():
        mira(clave, im)
    for p in PODERES:
        vistos = {}
        for e in ESTADOS:
            im = gen.ranura(e, p, 0.45, False, 0, 1.0)
            mira(f'{p} {e}', im)
            if im.shape[:2] != (CELDA_H, SW):
                fallos.append(f'{p} {e}: mide {im.shape[1]} x {im.shape[0]}')
            vistos[e] = im.tobytes()
        if len(set(vistos.values())) < len(ESTADOS):
            fallos.append(f'{p}: dos estados iguales')
        for e in ('pulsado', 'listo_pulsado'):
            if (gen.ranura(e, p, 1.0, False, 0, 1.0)[:BAJA, :, 3] > 0).any():
                fallos.append(f'{p} {e}: no baja')
        # lista se distingue de «casi» y de la carga pausada
        casi = gen.ranura('cargando', p, 0.98, False, 0, 1.0)
        if (casi == gen.ranura('listo', p, 1.0, False, 0, 1.0)).all():
            fallos.append(f'{p}: lista igual que casi')
        if (gen.ranura('cargando', p, 0.45, True, 0, 1.0) == gen.ranura('cargando', p, 0.45, False, 0, 1.0)).all():
            fallos.append(f'{p}: pausada igual que cargando')
        # el progreso se ve: mas carga, otra ranura
        if (gen.ranura('cargando', p, 0.2, False, 0, 1.0) == gen.ranura('cargando', p, 0.7, False, 0, 1.0)).all():
            fallos.append(f'{p}: el progreso no se ve')
        # lista se mueve: brilla
        if (gen.ranura('listo', p, 1.0, False, 0, 1.0) == gen.ranura('listo', p, 1.0, False, FRAMES // 2, 1.0)).all():
            fallos.append(f'{p}: lista no brilla')
        if not (gen.ranura('listo', p, 1.0, False, 0, 1.0) == gen.ranura('listo', p, 1.0, False, FRAMES, 1.0)).all():
            fallos.append(f'{p}: lista no cierra el bucle')
    for e in ESTADOS_CANCEL:
        im = gen.cancelar(e, 13)
        mira(f'cancelar {e}', im)
        if im.shape[:2] != (CELDA_H, CW):
            fallos.append(f'cancelar {e}: mide {im.shape[1]} x {im.shape[0]}')
    if len({gen.cancelar(e, 13).tobytes() for e in ESTADOS_CANCEL}) < len(ESTADOS_CANCEL):
        fallos.append('cancelar: dos estados iguales')
    fuera = usados - permitidos
    if fuera:
        fallos.append('colores fuera de paleta: ' + ', '.join('#%02X%02X%02X' % c for c in sorted(fuera)))
    # en la rueda no falta nada: sin magenta
    solo_e = {tuple(int(v) for v in c) for c in STR_E} - {tuple(int(v) for v in c) for c in list(STR_P) + list(NEU)}
    if usados & solo_e:
        fallos.append('la rueda lleva magenta')
    # el texto mas largo cabe
    if ancho('Cancelar · +60') + 12 > CW:
        fallos.append('«Cancelar · +60» no cabe')
    fallos += gen.comprobar_propio() if hasattr(gen, 'comprobar_propio') else []
    if fallos:
        raise SystemExit(f'ART-UI-WHEEL «{nombre}» no pasa:\n  ' + '\n  '.join(sorted(set(fallos))))
    return len(usados)


def ejecutar(gen, version, nombre, permitidos):
    n = comprobar(gen, permitidos, nombre)
    d = os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)
    gen.exportar(d)
    vistas(gen, version, '--gif' in sys.argv)
    comparativa()
    print(f'v{version} «{nombre}»: {n} colores  -> {os.path.relpath(d, RAIZ)}')


def hexa(c):
    return '#%02X%02X%02X' % tuple(int(v) for v in c)


def json_comun():
    return {'slot': [SW, SH], 'gap': HUECO, 'cancel': [CW, SH], 'cellHeight': CELDA_H, 'pressedDrop': BAJA,
            'powers': list(PODERES), 'slotStates': list(ESTADOS), 'cancelStates': list(ESTADOS_CANCEL),
            'frames': FRAMES, 'fps': 12, 'hoverMs': 160, 'curve': [0.2, 0.8, 0.2, 1.0],
            'text': {'normal': hexa(C_TEXTO), 'cost': hexa(C_COSTE), 'hover': hexa(STR_P[0]),
                     'hoverCost': hexa(STR_P[1]), 'muted': hexa(C_GRIS), 'unavailable': hexa(C_APAGADO),
                     'onPower': hexa(OSCURO)}}


def hoja_pictos(columnas):
    """Fila = poder; columnas: nombre -> funcion (p) -> 12 x 12."""
    return np.vstack([np.hstack([fn(p) for fn in columnas.values()]) for p in PODERES])


def tecla(marco, PZ=20, relleno=PANEL):
    return placa_lamina(PZ, PZ, marco, relleno)


def trazos_placa(p):
    import gen_btnconv_v1 as bc
    sil = pv1.escalera(*p.shape[:2][::-1])
    p[(sil & ~pv1.erosion(sil)) & ~bc.trazos(sil), :3] = PANEL
    return p


def rayas(p, color):
    dentro = pv1.erosion(pv1.escalera(*p.shape[:2][::-1]))
    for y in range(p.shape[0]):
        for x in range(p.shape[1]):
            if dentro[y, x] and (x + y) % 4 == 0:
                p[y, x, :3] = color
    return p


def cancelar_lamina(estado, devuelve, f=0, t=1.0, PZ=20, BORDE=6):
    """«Cancelar · +N», el secundario de 13.12; comun a las tres."""
    img = rgba(CELDA_H, CW)
    dy = BAJA if estado == 'pulsado' else 0
    marco = {'normal': STR_P[6], 'encima': STR_P[4], 'pulsado': STR_P[4], 'bloqueado': NEU[5]}[estado]
    cuerpo = pv1.nueve(tecla(marco), CW, SH, BORDE)
    if estado == 'bloqueado':
        rayas(cuerpo, NEU[5])
    claro = estado in ('encima', 'pulsado')
    ts = [('Cancelar · ', STR_P[0] if claro else C_GRIS if estado == 'bloqueado' else C_TEXTO),
          (f'+{devuelve}', STR_P[1] if claro else C_GRIS if estado == 'bloqueado' else C_COSTE)]
    pinta_textos(cuerpo, ts, CW // 2, TY, 'centro')
    pega(img, cuerpo, 0, dy)
    return img


# =============================================================================
# v1 «Las teclas»
# =============================================================================

BORDE = 6
PASOS = 5


def carga(paso, c1=STR_P[5], c2=STR_P[4]):
    t = rgba(8, 8)
    umbral = paso / (PASOS + 1) * 0.6
    for y in range(8):
        for x in range(8):
            if BAYER8[y, x] < umbral:
                t[y, x, :3] = c1 if BAYER8[y, x] > umbral / 2 else c2
                t[y, x, 3] = 255
    return t


def marco_de(estado, p):
    R = RAMPA[p]
    return {'normal': STR_P[6], 'encima': STR_P[4], 'pulsado': STR_P[4], 'bloqueado': NEU[5],
            'candado': NEU[5], 'nodisp': NEU[5], 'cargando': R[4], 'listo': R[2],
            'listo_encima': R[0], 'listo_pulsado': R[0]}[estado]


def placa(estado, p):
    R = RAMPA[p]
    relleno = R[3] if estado.startswith('listo') else PANEL
    pl = tecla(marco_de(estado, p), relleno=relleno)
    if estado == 'bloqueado':
        rayas(pl, NEU[5])
    if estado == 'nodisp':
        trazos_placa(pl)
    return pl


def ranura(estado, p, progreso=0.45, pausada=False, f=0, t=1.0):
    R = RAMPA[p]
    img = rgba(CELDA_H, SW)
    dy = BAJA if estado in ('pulsado', 'listo_pulsado') else 0
    cuerpo = pv1.nueve(placa(estado, p), SW, SH, BORDE)
    dentro = pv1.erosion(pv1.escalera(SW, SH))
    if estado in ('encima', 'pulsado'):
        paso = PASOS if estado == 'pulsado' else max(1, int(round(t * PASOS)))
        lleno = int(round((SW - 4) * (1 if estado == 'pulsado' else orbital(t))))
        tr = np.tile(carga(paso), (SH // 8 + 1, SW // 8 + 1, 1))[:SH, :SW]
        m = dentro & (tr[..., 3] > 0)
        m[:, 2 + lleno:] = False
        cuerpo[m] = tr[m]
    if estado == 'cargando':
        lleno = int((SW - 4) * min(progreso, 0.999))
        base_m = dentro.copy()
        base_m[:, 2 + lleno:] = False
        cuerpo[base_m, :3] = R[6] if pausada else R[5]
        tr = np.tile(carga(2 if pausada else 4, R[4], R[4]), (SH // 8 + 1, SW // 8 + 1, 1))[:SH, :SW]
        m = dentro & (tr[..., 3] > 0)
        m[:, 2 + lleno:] = False
        cuerpo[m] = tr[m]
        if not pausada and lleno > 0:      # el frente de la carga
            col = dentro.copy()
            col[:, :1 + lleno] = False
            col[:, 2 + lleno:] = False
            cuerpo[col, :3] = R[2]
    if estado.startswith('listo'):
        # una luz de 3 columnas cruza la tecla cada 32 frames
        cab = int(round((f % FRAMES) / FRAMES * (SW + 16))) - 8
        for x in range(cab, cab + 3):
            if 0 <= x < SW:
                col = dentro[:, x]
                cuerpo[col, x, :3] = R[2] if x == cab + 1 else R[2]
                if x == cab + 1:
                    cuerpo[col, x, :3] = R[1]
    if estado == 'candado':
        pega(cuerpo, candado(NEU[3], PANEL), PADX + 2, 6)
    else:
        luz, cu, cont = colores_picto(estado, p)
        pega(cuerpo, picto(p, luz, cu, cont), PADX, 4)
    pinta_textos(cuerpo, textos(estado, p), SW - PADX, TY, 'der')
    pega(img, cuerpo, 0, dy)
    return img


def cancelar(estado, devuelve, f=0, t=1.0):
    return cancelar_lamina(estado, devuelve, f, t)


def piezas():
    d = {}
    for p in PODERES:
        for e in ('normal', 'encima', 'pulsado', 'bloqueado', 'candado', 'nodisp', 'cargando', 'listo',
                  'listo_encima'):
            d[f'placa_{p}_{e}'] = placa(e, p)
        d[f'carga_{p}'] = np.hstack([carga(2, RAMPA[p][5], RAMPA[p][4]), carga(4, RAMPA[p][5], RAMPA[p][4])])
    for i in range(1, PASOS + 1):
        d[f'carga_{i}'] = carga(i)
    d['pictos'] = hoja_pictos(PICTOS_V1)
    d['candado'] = candado(NEU[3], PANEL)
    return d


PICTOS_V1 = {
    'color': lambda p: picto(p, RAMPA[p][1], RAMPA[p][3]),
    'listo': lambda p: picto(p, RAMPA[p][0], OSCURO),
    'gris': lambda p: picto(p, NEU[2], NEU[4]),
    'contorno': lambda p: picto(p, NEU[4], NEU[4], True),
}


def comprobar_propio():
    fallos = []
    for p in PODERES:
        if PADX + PIC + 1 + ancho_textos(textos('normal', p)) + PADX > SW:
            fallos.append(f'{p}: el pictograma y el texto no caben')
    return fallos


def exportar(d):
    p = piezas()
    cols = ('normal', 'encima', 'pulsado', 'bloqueado', 'candado', 'nodisp', 'cargando', 'listo', 'listo_encima')
    Image.fromarray(np.vstack([np.hstack([p[f'placa_{q}_{e}'] for e in cols]) for q in PODERES]), 'RGBA').save(
        os.path.join(d, 'ui_power_wheel_plate.png'))
    Image.fromarray(np.vstack([p[f'carga_{q}'] for q in PODERES] +
                              [np.hstack([p[f'carga_{i}'] for i in range(1, PASOS + 1)])[:, :16]]), 'RGBA').save(
        os.path.join(d, 'ui_power_wheel_fill.png'))
    Image.fromarray(p['pictos'], 'RGBA').save(os.path.join(d, 'ui_power_wheel_icons.png'))
    Image.fromarray(p['candado'], 'RGBA').save(os.path.join(d, 'ui_power_wheel_lock.png'))
    j = json_comun()
    j.update({'plateCell': 20, 'plateBorder': BORDE, 'plateCenterMode': 'tiled', 'plateRows': list(PODERES),
              'plateColumns': list(cols), 'icon': PIC, 'iconAt': [PADX, 4], 'iconColumns': list(PICTOS_V1),
              'lockAt': [PADX + 2, 6], 'textRight': SW - PADX, 'capTop': TY,
              'note': 'ranura: placa de nueve cortes de su poder y estado; encima, la trama cian de carga (paso 1 a '
                      '5) entra de izquierda a derecha en 160 ms; cargando, la trama del poder (fila del poder: '
                      'pausada, cargando) hasta 2 + floor(56 * progreso) y una columna en el indice 2 del poder en '
                      'el frente; listo, relleno del poder con una luz de 3 columnas (indices 2-1-2) que cruza '
                      'de x -8 a 68 cada 32 frames; pulsado y listo_pulsado, 2 px abajo; el pictograma a la '
                      'izquierda y el texto alineado a la derecha'})
    with open(os.path.join(d, 'ui_power_wheel.json'), 'w', encoding='utf-8') as fh:
        json.dump(j, fh, indent=1, ensure_ascii=False)


if __name__ == '__main__':
    ejecutar(sys.modules[__name__], 1, 'Las teclas',
             colores_de('str_p', 'neutral', 'hielo', 'decaimiento', 'gusano') | {tuple(PANEL), tuple(FONDO)})
