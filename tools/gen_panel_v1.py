"""NOVA - ART-UI-PANEL v1, el panel de clic derecho: «La placa».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.3 (contenido y comportamiento),
13.4 (velocidad de ataque), 13.5 (rueda), 13.12 (lenguaje de la UI), 8.2
(costes de reconversion), 16.2 (costes de nivel), 18 #30, #33-#35

Este archivo tiene ademas LO COMUN A LAS TRES PROPUESTAS -el contenido de
los casos, el texto, el boceto de los botones, las comprobaciones y las
vistas-; `gen_panel_v2.py` y `gen_panel_v3.py` solo traen su dibujo.

QUE ES. El panel que abre el clic derecho sobre cualquier nodo, a su lado
y sin taparlo (18 #30), con sus seis bloques en el orden de 13.3: tipo y
nivel, Energia / tope, velocidad de ataque, mejora, reconversion y, en un
Pulsar propio, la rueda de poderes. De un nodo ajeno solo se ve la
informacion. El texto va en TextMeshPro con Jersey 10 a tamano nativo, en
filas de 24 (como hoy `InfoPanelState`); las vistas lo pintan con la propia
fuente en su rejilla.

LO QUE ESTE ASSET NO ES. Los botones de mejora y de reconversion y la rueda
son `ART-UI-BTN-UPG`, `ART-UI-BTN-CONV` y `ART-UI-WHEEL`, assets aparte. En
las vistas van como BOCETO con el boton secundario de la lamina de 13.12
-marco de 2 px en `str_p[6]`, coste en cian, sin Energia en gris con el
coste en magenta y lo que falta-, para juzgar el panel lleno. No se
exportan.

EL ALTO SIGUE AL CONTENIDO. Hoy el panel mide siempre nueve filas y en un
nodo ajeno quedan seis vacias. Las tres propuestas lo ajustan a sus filas:
el de un Magnetar enemigo mide tres, el de un Pulsar propio, nueve. Las
piezas son de nueve cortes, asi que el alto es libre.

    py tools/gen_panel_v1.py      piezas, comprobaciones y vistas

----------------------------------------------------------------------------

LA TESIS DE LA v1: LA PLACA DE LA LAMINA. Una sola placa con el lenguaje de
13.12 -relleno `#11142A`, marco de 2 px con la esquina en escalera-, con el
marco del color de QUIEN ES el nodo: `str_p[6]` lo propio, `str_e[5]` lo del
Enemigo y `neutral[5]` lo neutral. La primera fila es una cabecera mas
oscura con el filo inferior en el bando, y el titulo en su color. Una linea
de puntos separa la informacion, las acciones y los poderes. Y una
PESTANA en escalera sale del costado que mira al nodo, a la altura de su
centro: el panel senala de quien habla.
"""

import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import BAYER8, RAIZ, dir_preview, rampa
from nova_paleta import RAMPAS, hex_a_rgb
import gen_topbar_v1 as tb

ASSET = 'info_panel'

W = 208
PAD = 8                       # margen interior, como InfoPanelState
FILA = 24
CAP_Y = 7                     # fila de la mayuscula dentro de su fila: 7..16
BOTON_H = 20
SEPARA = 48                   # NodeClearance: del centro del nodo al panel
PANEL = np.array(hex_a_rgb('#11142A'), np.uint8)
FONDO = np.array(hex_a_rgb('#0B0E1A'), np.uint8)
STR_P, STR_E, NEU = rampa('str_p'), rampa('str_e'), rampa('neutral')
FACCIONES = ('player', 'enemy', 'neutral')
RFAC = {'player': rampa('str_p'), 'enemy': rampa('str_e'), 'neutral': rampa('neutral')}

C_TITULO = STR_P[1]
C_TEXTO = NEU[1]
C_ROTULO = NEU[2]
C_GRIS = NEU[3]
C_COSTE = STR_P[3]
C_FALTA = STR_E[3]


# --- Los casos (13.3, 13.4) ----------------------------------------------------
#
# Bloques: ('info' | 'mejora' | 'reconv' | 'poderes', [filas]). Filas:
#   ('titulo', texto) ('dato', texto) ('aviso', texto)
#   ('boton', etiqueta, coste, faltan)        faltan = 0 si alcanza
#   ('rotulo', texto)
#   ('par', [(etiqueta, coste, faltan), ...])
#   ('ranuras', [(etiqueta, estado)])          estado: libre, carga, candado
#   ('cancel', texto)

CASOS = {
    'estrella': dict(fac='player', tipo='estrella', nivel=3, energia=34, tope=50, bloques=[
        ('info', [('titulo', 'Estrella · Nivel 3'), ('dato', 'Energía 34 / 50'),
                  ('dato', 'Generación: 0.77 E/s')]),
        ('mejora', [('boton', 'Mejorar', 40, 6)]),
        ('reconv', [('rotulo', 'Reconvertir'), ('par', [('Púlsar', 20, 0), ('Magnetar', 20, 0)])]),
    ]),
    'magnetar': dict(fac='enemy', tipo='magnetar', nivel=2, energia=58, tope=100, bloques=[
        ('info', [('titulo', 'Magnetar · Nivel 2'), ('dato', 'Energía 58 / 100'),
                  ('dato', 'Cadencia: 1.9 disparos/s')]),
    ]),
    'pulsar': dict(fac='player', tipo='pulsar', nivel=3, energia=62, tope=100, bloques=[
        ('info', [('titulo', 'Púlsar · Nivel 3'), ('dato', 'Energía 62 / 100'),
                  ('dato', 'Carga: 3.75 E/s')]),
        ('mejora', [('aviso', 'NIVEL MÁXIMO')]),
        ('reconv', [('rotulo', 'Reconvertir'), ('par', [('Estrella', 20, 0), ('Magnetar', 20, 0)])]),
        ('poderes', [('rotulo', 'Agujero de Gusano · 45%'),
                     ('ranuras', [('Cero', 'libre'), ('Decai.', 'libre'), ('Gusano', 'carga')]),
                     ('cancel', 'Cancelar · +13')]),
    ]),
    'asteroide': dict(fac='player', tipo='asteroide', nivel=0, energia=7, tope=10, bloques=[
        ('info', [('titulo', 'Asteroide'), ('aviso', 'DORMIDO'), ('dato', 'Encender: 10')]),
    ]),
}


def filas_de(caso):
    return sum(len(f) for _, f in caso['bloques'])


# --- Texto y dibujo -------------------------------------------------------------

ARRIBA = 3                    # filas por encima de la mayuscula: las tildes de Í, Á, Ó


def texto(s):
    """Jersey 10 a tamano nativo, con las tildes: se dibuja a 56 px (3 por
    pixel de diseno) y se toma el centro de cada bloque. La mayuscula cae en
    las filas ARRIBA..ARRIBA+9. Las mayusculas espaciadas (13.12) llevan 1 px
    de aire entre letras; el texto corrido, el suyo."""
    from PIL import ImageDraw, ImageFont
    if tb._F56 is None:
        tb._F56 = ImageFont.truetype(tb.FUENTE, 56)
    esp = 1 if s.upper() == s else 0
    cols = []
    for i, ch in enumerate(s):
        w = int(round(tb._F56.getlength(ch)))
        im = Image.new('1', (max(w, 3) + 6, 90), 0)
        d = ImageDraw.Draw(im)
        d.fontmode = '1'
        d.text((0, 0), ch, font=tb._F56, fill=1)
        a = np.array(im)[1::3, 1::3][5 - ARRIBA:5 + tb.CAP + 3]
        cols.append(a[:, :w // 3])
        if i < len(s) - 1 and esp:
            cols.append(np.zeros((a.shape[0], esp), bool))
    return np.hstack(cols)


def ancho(s):
    return texto(s).shape[1]


def pinta(img, s, x, y, color, alinea='izq', ancho_caja=0):
    m = texto(s)
    if alinea == 'der':
        x = x - m.shape[1]
    elif alinea == 'centro':
        x = x + (ancho_caja - m.shape[1]) // 2
    y -= ARRIBA
    h, w = m.shape
    sub = img[y:y + h, x:x + w]
    mm = m[:sub.shape[0], :sub.shape[1]]
    sub[mm, :3] = color
    sub[mm, 3] = 255


def escalera(w, h):
    return tb.escalera(w, h)


def erosion(m, r=2):
    return tb.erosion(m, r)


def rgba(h, w):
    return np.zeros((h, w, 4), np.uint8)


def pega(dst, src, x, y):
    h, w = src.shape[:2]
    y1, x1 = max(y, 0), max(x, 0)
    y2, x2 = min(y + h, dst.shape[0]), min(x + w, dst.shape[1])
    if y2 <= y1 or x2 <= x1:
        return
    s = src[y1 - y:y2 - y, x1 - x:x2 - x]
    m = s[..., 3] > 0
    if dst.shape[2] == 4:
        dst[y1:y2, x1:x2][m] = s[m]
    else:
        dst[y1:y2, x1:x2][m] = s[..., :3][m]


def nueve(pieza, w, h, borde):
    """Nueve cortes: esquinas enteras, bordes y centro en mosaico (Tiled)."""
    ph, pw = pieza.shape[:2]
    out = rgba(h, w)
    cw, ch = pw - 2 * borde, ph - 2 * borde

    def mos(src, ww, hh):
        reps = (-(-hh // src.shape[0]), -(-ww // src.shape[1]), 1)
        return np.tile(src, reps)[:hh, :ww]
    b = borde
    out[:b, :b] = pieza[:b, :b]
    out[:b, w - b:] = pieza[:b, pw - b:]
    out[h - b:, :b] = pieza[ph - b:, :b]
    out[h - b:, w - b:] = pieza[ph - b:, pw - b:]
    out[:b, b:w - b] = mos(pieza[:b, b:b + cw], w - 2 * b, b)
    out[h - b:, b:w - b] = mos(pieza[ph - b:, b:b + cw], w - 2 * b, b)
    out[b:h - b, :b] = mos(pieza[b:b + ch, :b], b, h - 2 * b)
    out[b:h - b, w - b:] = mos(pieza[b:b + ch, pw - b:], b, h - 2 * b)
    out[b:h - b, b:w - b] = mos(pieza[b:b + ch, b:b + cw], w - 2 * b, h - 2 * b)
    return out


def placa_lamina(w, h, marco, relleno=PANEL):
    sil = escalera(w, h)
    dentro = erosion(sil)
    img = rgba(h, w)
    img[sil, :3] = marco
    img[dentro, :3] = relleno
    img[sil, 3] = 255
    return img


# --- Boceto de los botones (no es este asset) -------------------------------------

def boceto_boton(w, etiqueta, coste=None, faltan=0, activo=False):
    ok = faltan == 0
    marco = STR_P[4] if activo else STR_P[6] if ok else NEU[5]
    b = placa_lamina(w, BOTON_H, marco)
    ty = (BOTON_H - 10) // 2
    pinta(b, etiqueta, 6, ty, C_TEXTO if ok else C_GRIS)
    if coste is not None:
        s = str(coste) if ok else f'{coste} · faltan {faltan}'
        pinta(b, s, w - 6, ty, C_COSTE if ok else C_FALTA, 'der')
    return b


def boceto_ranura(w, etiqueta, estado):
    if estado == 'candado':
        return boceto_boton(w, etiqueta, None, 1)
    return boceto_boton(w, etiqueta, None, 0, estado == 'carga')


def fila_contenido(img, fila, x, y, w, bando):
    """Pinta una fila de contenido en (x, y), caja de w x FILA."""
    k = fila[0]
    if k == 'titulo':
        pinta(img, fila[1], x, y + CAP_Y, bando['titulo'])
    elif k == 'dato':
        pinta(img, fila[1], x, y + CAP_Y, C_TEXTO)
    elif k == 'aviso':
        pinta(img, fila[1], x, y + CAP_Y, C_GRIS)
    elif k == 'rotulo':
        pinta(img, fila[1], x, y + CAP_Y, C_ROTULO)
    elif k == 'boton':
        pega(img, boceto_boton(w, fila[1], fila[2], fila[3]), x, y + 2)
    elif k == 'cancel':
        pega(img, boceto_boton(w, fila[1]), x, y + 2)
    elif k == 'par':
        bw = (w - 4) // 2
        for i, (e, c, f) in enumerate(fila[1]):
            pega(img, boceto_boton(bw, e, c, f), x + i * (bw + 4), y + 2)
    elif k == 'ranuras':
        bw = (w - 12) // 3
        for i, (e, est) in enumerate(fila[1]):
            pega(img, boceto_ranura(bw, e, est), x + i * (bw + 6), y + 2)


# --- Comprobaciones --------------------------------------------------------------

def colores_de(*nombres):
    s = set()
    for n in nombres:
        s |= {hex_a_rgb(h) for h in RAMPAS[n]}
    return s


def comprobar(gen, permitidos, nombre):
    fallos = []
    usados = set()
    for clave, im in gen.piezas().items():
        if not set(np.unique(im[..., 3])) <= {0, 255}:
            fallos.append(f'{clave}: alfa no binario')
        usados |= {tuple(int(v) for v in c) for c in im[..., :3][im[..., 3] > 0]}
    fuera = usados - permitidos
    if fuera:
        fallos.append('colores fuera de paleta: ' + ', '.join('#%02X%02X%02X' % c for c in sorted(fuera)))
    # el texto mas largo de cada caso cabe con su margen
    util = W - 2 * PAD - getattr(gen, 'EXTRA_IZQ', 0)
    for clave, caso in CASOS.items():
        for _, filas in caso['bloques']:
            for f in filas:
                if f[0] in ('titulo', 'dato', 'aviso', 'rotulo') and ancho(f[1]) > util:
                    fallos.append(f'{clave}: «{f[1]}» no cabe')
        im = gen.panel(caso)
        if im.shape[1] > W + 16 or im.shape[0] > 9 * FILA + 2 * PAD + 40:
            fallos.append(f'{clave}: el panel mide {im.shape[1]} x {im.shape[0]}')
    fallos += gen.comprobar_propio() if hasattr(gen, 'comprobar_propio') else []
    if fallos:
        raise SystemExit(f'ART-UI-PANEL «{nombre}» no pasa:\n  ' + '\n  '.join(sorted(set(fallos))))
    return len(usados)


# --- Vistas ----------------------------------------------------------------------

SPR = {'estrella': ('estrella', 1, 'str'), 'pulsar': ('pulsar', 2, 'pul'),
       'magnetar': ('magnetar', 1, 'mag'), 'asteroide': ('asteroide', 1, 'ast')}


def nodo(lienzo, caso, cx, cy, f=0):
    """El nodo con su numero (y la barra de carga si es el Pulsar), con los
    assets elegidos."""
    import gen_energy_v1 as e1
    import gen_energy_v3 as e3
    import gen_charge_v1 as c1
    import gen_charge_v3 as c3
    carpeta, v, pre = SPR[caso['tipo']]
    nv = caso['nivel']
    nombre = f'{pre}_lvl{nv}_{caso["fac"]}.png'
    im = np.asarray(Image.open(os.path.join(RAIZ, 'art', 'nodes', carpeta, f'v{v}', nombre)).convert('RGBA'))
    e1.sobre_c(lienzo, im, cx, cy)
    hu = e1.huella(carpeta, pre, nv)
    e = caso['energia']
    e1.colocar(lienzo, e3.etiqueta(str(e), caso['fac'], e, caso['tope'], f), cx, cy, hu)
    if caso['tipo'] == 'pulsar':
        b = c3.barra('wormhole', 27, caso['fac'], False, f)
        c1.sobre(lienzo, b, int(cx) - b.shape[1] // 2, int(cy) + c1.arriba(hu))
    return hu


def abrir(lienzo, gen, caso, cx, cy, izquierda=False, f=0):
    """El nodo y su panel a su lado, como InfoPanelState.CenterBeside."""
    nodo(lienzo, caso, cx, cy, f)
    p = gen.panel(caso, lado='der' if izquierda else 'izq', f=f)
    h, w = p.shape[:2]
    x = int(cx) - SEPARA - w if izquierda else int(cx) + SEPARA - getattr(gen, 'SALIENTE', 0)
    y = int(cy) - h // 2
    y = max(28, min(y, 540 - 14 - h))
    if hasattr(gen, 'antes_de_pegar'):
        gen.antes_de_pegar(lienzo, caso, cx, cy, x, y, w, h, izquierda, f)
    pega(lienzo, p, x, y)


def fondo(seccion=1):
    return np.asarray(Image.open(os.path.join(
        RAIZ, 'art', 'bg', 'fondos', 'v2', f'bg_section{seccion}.png')).convert('RGB')).copy()


def galeria(gen):
    """Los cuatro casos, cada uno con su nodo, sobre el cielo."""
    bg = fondo(1)
    celdas = []
    for clave in ('estrella', 'pulsar', 'magnetar', 'asteroide'):
        c = bg[0:300, 0:330].copy() if clave in ('estrella', 'magnetar') else bg[240:540, 330:660].copy()
        abrir(c, gen, CASOS[clave], 56, 150)
        celdas.append(c)
        celdas.append(np.zeros((300, 3, 3), np.uint8))
    return np.hstack(celdas[:-1])


def tablero(gen, f=0):
    """El nivel 9 a 960 x 540 con el panel abierto sobre la Estrella propia."""
    import gen_energy_v1 as e1
    import gen_energy_v3 as e3
    t = e1.tablero(e3, f)
    caso = dict(CASOS['estrella'])
    caso['nivel'], caso['energia'] = 2, 25
    caso['bloques'] = [
        ('info', [('titulo', 'Estrella · Nivel 2'), ('dato', 'Energía 25 / 50'),
                  ('dato', 'Generación: 0.63 E/s')]),
        ('mejora', [('boton', 'Mejorar', 30, 5)]),
        ('reconv', [('rotulo', 'Reconvertir'), ('par', [('Púlsar', 20, 0), ('Magnetar', 20, 0)])]),
    ]
    abrir(t, gen, caso, 480 - 6.25 * 64, 270 - 64, f=f)
    return t


def ampliar(im, k):
    return np.repeat(np.repeat(im, k, axis=0), k, axis=1)


def vistas(gen, version):
    d = os.path.join(dir_preview('ui_' + ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(galeria(gen), 2)).save(os.path.join(d, 'casos_x2.png'))
    t = tablero(gen)
    Image.fromarray(ampliar(t, 2)).save(os.path.join(d, 'tablero_1080p.png'))
    # los cuatro paneles solos, de cerca
    ps = [gen.panel(CASOS[k]) for k in ('estrella', 'pulsar', 'magnetar', 'asteroide')]
    h = max(p.shape[0] for p in ps) + 8
    tira = np.empty((h, sum(p.shape[1] + 8 for p in ps) + 8, 3), np.uint8)
    tira[:] = FONDO
    x = 8
    for p in ps:
        pega(tira, p, x, 4)
        x += p.shape[1] + 8
    Image.fromarray(ampliar(tira, 3)).save(os.path.join(d, 'paneles_x3.png'))
    if '--gif' in sys.argv and getattr(gen, 'FRAMES', 1) > 1:
        ims = [Image.fromarray(ampliar(galeria(gen)[:, :663] if False else tablero(gen, f)[100:440, 0:420], 2))
               for f in range(gen.FRAMES)]
        ims[0].save(os.path.join(d, 'tablero_x2.gif'), save_all=True, append_images=ims[1:],
                    duration=1000 // 12, loop=0)
    return d


def comparativa():
    filas = []
    for v in (1, 2, 3):
        if not os.path.isdir(os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{v}')):
            continue
        mod = sys.modules[__name__] if v == 1 else __import__(f'gen_panel_v{v}')
        filas.append(galeria(mod))
        filas.append(np.zeros((3, filas[-1].shape[1], 3), np.uint8))
    if filas:
        Image.fromarray(ampliar(np.vstack(filas[:-1]), 2)).save(
            os.path.join(dir_preview('ui_' + ASSET), 'comparativa_x2.png'))


def ejecutar(gen, version, nombre, permitidos):
    colores = comprobar(gen, permitidos, nombre)
    d = os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)
    gen.exportar(d)
    vistas(gen, version)
    comparativa()
    print(f'v{version} «{nombre}»: {colores} colores  -> {os.path.relpath(d, RAIZ)}')


# =============================================================================
# v1 «La placa»
# =============================================================================

BORDE = 6
PZ = 20
MARCO = {'player': STR_P[6], 'enemy': STR_E[5], 'neutral': NEU[5]}
PESTANA_W = 8
SALIENTE = 0


def bando_de(fac):
    R = RFAC[fac]
    return {'titulo': R[2] if fac != 'neutral' else NEU[1], 'filo': R[4], 'marco': MARCO[fac]}


def placa(fac):
    return placa_lamina(PZ, PZ, MARCO[fac])


def cabecera(fac):
    """Tesela de 8 x (FILA + 2): la cabecera, mas oscura, con el filo del bando."""
    t = rgba(FILA + 2, 8)
    t[..., :3] = FONDO
    t[-1, :, :3] = bando_de(fac)['filo']
    t[..., 3] = 255
    return t


def separador(fac):
    t = rgba(1, 2)
    t[0, 0, :3] = MARCO[fac]
    t[0, 0, 3] = 255
    return t


def pestana(fac):
    """La pestana que mira al nodo, con la punta a la izquierda: un triangulo
    en escalera de 8 x 16, de 2 en 2 px, con el marco de 2 y el relleno del
    panel, que se funde con el cuerpo."""
    h = 16
    img = rgba(h, PESTANA_W)
    for x in range(PESTANA_W):
        y0 = h // 2 - 2 - (x // 2) * 2        # crece 2 px por cada 2 columnas
        y1 = h // 2 + 2 + (x // 2) * 2
        y0, y1 = max(y0, 0), min(y1, h)
        img[y0:y1, x, :3] = MARCO[fac]
        img[y0:y1, x, 3] = 255
        if x >= 2:
            img[y0 + 2:y1 - 2, x, :3] = PANEL
    return img


def panel(caso, lado='izq', f=0):
    """El panel de un caso, con el texto y el boceto de los botones pintados.
    `lado`: el costado que mira al nodo."""
    fac = caso['fac']
    bando = bando_de(fac)
    n = filas_de(caso)
    h = PAD + FILA * n + PAD
    cuerpo = nueve(placa(fac), W, h, BORDE)
    # la cabecera: la primera fila, de borde a borde por dentro del marco
    cab = np.tile(cabecera(fac), (1, (W - 4) // 8 + 1, 1))[:, :W - 4]
    zona = cuerpo[2:2 + FILA + 2 + PAD - 2, 2:W - 2]
    hh = min(cab.shape[0], zona.shape[0])
    m = cuerpo[2:2 + hh, 2:W - 2, 3] > 0
    cuerpo[2:2 + hh, 2:W - 2][m] = cab[:hh][m]
    y = PAD
    for i, (cat, filas) in enumerate(caso['bloques']):
        if i > 0:
            # la linea de puntos entre bloques
            sep = np.tile(separador(fac), (1, (W - 2 * PAD) // 2, 1))
            pega(cuerpo, sep, PAD, y - 1)
        for fila in filas:
            fila_contenido(cuerpo, fila, PAD, y, W - 2 * PAD, bando)
            y += FILA
    # la pestana, a media altura, en el costado del nodo
    p = pestana(fac)
    out = rgba(h, W + PESTANA_W)
    if lado == 'izq':
        pega(out, cuerpo, PESTANA_W, 0)
        pega(out, p, 0, h // 2 - p.shape[0] // 2)
        # la pestana abre el marco: el relleno pasa por debajo
        out[h // 2 - 6:h // 2 + 6, PESTANA_W:PESTANA_W + 2, :3] = PANEL
    else:
        pega(out, cuerpo, 0, 0)
        pega(out, p[:, ::-1], W, h // 2 - p.shape[0] // 2)
        out[h // 2 - 6:h // 2 + 6, W - 2:W, :3] = PANEL
    return out


def piezas():
    d = {}
    for fac in FACCIONES:
        d[f'placa_{fac}'] = placa(fac)
        d[f'cabecera_{fac}'] = cabecera(fac)
        d[f'separador_{fac}'] = separador(fac)
        d[f'pestana_{fac}'] = pestana(fac)
    return d


def exportar(d):
    p = piezas()
    Image.fromarray(np.vstack([p[f'placa_{f}'] for f in FACCIONES]), 'RGBA').save(
        os.path.join(d, 'ui_info_panel_plate.png'))
    Image.fromarray(np.vstack([p[f'cabecera_{f}'] for f in FACCIONES]), 'RGBA').save(
        os.path.join(d, 'ui_info_panel_header.png'))
    Image.fromarray(np.vstack([p[f'separador_{f}'] for f in FACCIONES]), 'RGBA').save(
        os.path.join(d, 'ui_info_panel_separator.png'))
    Image.fromarray(np.vstack([p[f'pestana_{f}'] for f in FACCIONES]), 'RGBA').save(
        os.path.join(d, 'ui_info_panel_tab.png'))
    hexa = lambda c: '#%02X%02X%02X' % tuple(int(v) for v in c)
    with open(os.path.join(d, 'ui_info_panel.json'), 'w') as fh:
        json.dump({'width': W, 'padding': PAD, 'rowHeight': FILA, 'capTop': CAP_Y,
                   'plateCell': PZ, 'plateBorder': BORDE, 'plateCenterMode': 'tiled',
                   'rows': list(FACCIONES), 'headerHeight': FILA + 2, 'headerTile': 8,
                   'separatorTile': 2, 'tabSize': [PESTANA_W, 16],
                   'titleColor': {f: hexa(bando_de(f)['titulo']) for f in FACCIONES},
                   'textColor': hexa(C_TEXTO), 'labelColor': hexa(C_ROTULO), 'mutedColor': hexa(C_GRIS),
                   'note': 'alto = 2 * padding + rowHeight * filas; la cabecera tapa la primera fila por dentro '
                           'del marco (x 2..ancho-2); el separador en mosaico 1 px encima de la primera fila de '
                           'cada bloque salvo el primero; la pestana sale del costado del nodo, centrada, con la '
                           'punta hacia el, y el marco se abre 12 px donde se toca'}, fh, indent=1)


if __name__ == '__main__':
    ejecutar(sys.modules[__name__], 1, 'La placa',
             colores_de('str_p', 'str_e', 'neutral') | {tuple(PANEL), tuple(FONDO)})
