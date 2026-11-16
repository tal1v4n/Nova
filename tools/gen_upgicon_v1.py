"""NOVA - ART-UI-BTN-UPG v1, el icono de mejora sobre el nodo: «La flecha».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.1 (raton), 13.3 (panel), 13.12
(lenguaje, patron «Sonar»), 16.2 (costes de nivel), 18 #18 · 14.7 (capa 6)

Este archivo tiene ademas LO COMUN A LAS TRES PROPUESTAS -los estados, el
sitio, las comprobaciones y las vistas-; `gen_upgicon_v2.py` y
`gen_upgicon_v3.py` solo traen su dibujo.

LA REGLA (decidida con el usuario el 2026-11-16; sustituye al boton de
mejora del panel, que queda como informacion: «Mejora: 40» o NIVEL MAXIMO).
La mejora se hace desde un ICONO encima del nodo, centrado sobre su huella,
solo en un nodo PROPIO que pueda mejorarse -ni Congelado ni Reconvirtiendo
(18 #18), ni al nivel maximo-:

    Energia < coste - 5          no hay icono
    coste - 5 <= Energia < coste AVISO: difuminado, no se puede pulsar
    Energia >= coste             LISTO: difuminado mientras el cursor no
                                 esta sobre el nodo; con el cursor encima,
                                 ACTIVO al 100 %, y solo entonces el clic
                                 izquierdo SOBRE EL ICONO mejora. El resto
                                 del nodo conserva su clic de 13.1

El difuminado no es opacidad -el alfa del proyecto es binario-: es el
icono tramado al 50 % con la Bayer, como los velos de 13.12. Sobre el
icono activo hay dos estados mas: ENCIMA (el cursor sobre el propio icono)
y PULSADO.

EL SITIO. Encima de la huella, centrado, a 2 px. El nodo mejorable mas
grande es una Estrella de nivel 4 (huella 68) y el nodo mas alto de los
nueve niveles esta en y = 3 u, a 78 px del borde: entre su huella y la
barra de arriba (24 px + 2 de aire) quedan **16 px**. Ese es el alto maximo
del icono, y el generador lo comprueba contra los nueve niveles.

    py tools/gen_upgicon_v1.py         piezas, comprobaciones y vistas
    py tools/gen_upgicon_v1.py --gif   y la animada

----------------------------------------------------------------------------

LA TESIS DE LA v1: LA FLECHA. Lo mas directo que hay: una flecha gorda hacia
arriba de 11 x 12 en el cian de lo que se puede hacer (`str_p`, ancla en el
cuerpo, luz arriba a la izquierda y sombra abajo a la derecha, con
contorno). Activa, respira subiendo y bajando 1 px cada 1.33 s; encima, se
enciende (`str_p[1]` con el filo blanco); pulsada, baja 1 px y se aplasta
una fila.
"""

import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import BAYER8, CONTORNO, RAIZ, dir_preview, rampa
from nova_paleta import RAMPAS, hex_a_rgb

ASSET = 'upgrade_icon'
ALTO_MAX = 16
ANCHO_MAX = 24
GAP = 2
TOPE_HUD = 24 + 2
FRAMES = 16                     # 1.33 s a 12 fps
FPS = 12
CONT = np.array(CONTORNO, np.uint8)
FONDO = np.array(hex_a_rgb('#0B0E1A'), np.uint8)
PANEL = np.array(hex_a_rgb('#11142A'), np.uint8)
STR_P = rampa('str_p')
ESTADOS = ('aviso', 'listo', 'activo', 'encima', 'pulsado')
# lo que se puede mejorar: (tipo, carpeta, version, prefijo, niveles con mejora, nivel maximo)
MEJORABLES = (('estrella', 'estrella', 1, 'str', (1, 2, 3, 4), 5),
              ('pulsar', 'pulsar', 2, 'pul', (1, 2), 3),
              ('magnetar', 'magnetar', 1, 'mag', (1, 2, 3, 4), 5))


def rgba(h, w):
    return np.zeros((h, w, 4), np.uint8)


def contornea(img):
    """Anade 1 px de contorno alrededor de lo pintado (crece 1 por lado)."""
    h, w = img.shape[:2]
    out = rgba(h + 2, w + 2)
    out[1:-1, 1:-1] = img
    m = out[..., 3] > 0
    b = np.zeros_like(m)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            b |= np.roll(np.roll(m, dy, 0), dx, 1)
    b &= ~m
    out[b, :3] = CONT
    out[b, 3] = 255
    return out


def difumina(img):
    """El icono tramado al 50 % con la Bayer: el difuminado de alfa binario."""
    out = img.copy()
    h, w = img.shape[:2]
    for y in range(h):
        for x in range(w):
            if BAYER8[y % 8, x % 8] >= 0.5:
                out[y, x, 3] = 0
    out[out[..., 3] == 0] = 0
    return out


def estado_de(energia, coste, cursor_en_nodo, cursor_en_icono=False, pulsado=False):
    """La regla, para las vistas: None si no hay icono."""
    if energia < coste - 5:
        return None
    if energia < coste:
        return 'aviso'
    if not cursor_en_nodo:
        return 'listo'
    if pulsado:
        return 'pulsado'
    return 'encima' if cursor_en_icono else 'activo'


# --- Comprobaciones --------------------------------------------------------------

def huella(carpeta, v, pre, n):
    j = json.load(open(os.path.join(RAIZ, 'art', 'nodes', carpeta, f'v{v}', f'{pre}_huella.json')))
    return j['footprintPixels'][j['levels'].index(n)]


def colores_de(*nombres):
    s = set()
    for n in nombres:
        s |= {hex_a_rgb(h) for h in RAMPAS[n]}
    return s


def comprobar(gen, permitidos, nombre):
    fallos = []
    usados = set()
    alto = 0
    for tipo, carpeta, v, pre, niveles, _ in MEJORABLES:
        for n in niveles:
            vistos = {}
            for e in ESTADOS:
                for f in range(FRAMES):
                    im = gen.icono(e, tipo, n, f)
                    alto = max(alto, im.shape[0])
                    if im.shape[0] > ALTO_MAX or im.shape[1] > ANCHO_MAX:
                        fallos.append(f'{tipo} {n} {e}: mide {im.shape[1]} x {im.shape[0]}')
                    if not set(np.unique(im[..., 3])) <= {0, 255}:
                        fallos.append('alfa no binario')
                    usados |= {tuple(int(c) for c in p) for p in im[..., :3][im[..., 3] > 0]}
                vistos[e] = gen.icono(e, tipo, n, 0).tobytes()
            # activo, encima y pulsado se distinguen entre si y del difuminado
            for a, b in (('activo', 'encima'), ('encima', 'pulsado'), ('listo', 'activo')):
                if vistos[a] == vistos[b]:
                    fallos.append(f'{tipo} {n}: {a} y {b} son iguales')
            # el difuminado deja la mitad o menos
            act = (gen.icono('activo', tipo, n, 0)[..., 3] > 0).sum()
            dif = (gen.icono('listo', tipo, n, 0)[..., 3] > 0).sum()
            if not dif <= act * 0.6:
                fallos.append(f'{tipo} {n}: el difuminado no difumina')
    fuera = usados - permitidos
    if fuera:
        fallos.append('colores fuera de paleta: ' + ', '.join('#%02X%02X%02X' % c for c in sorted(fuera)))
    # el sitio: encima del nodo mejorable mas grande, en el nodo mas alto, no pisa el HUD
    mx = max(huella(c, v, p, n) for _, c, v, p, ns, _ in MEJORABLES for n in ns)
    for nv in range(1, 10):
        s = open(os.path.join(RAIZ, 'Assets', 'Data', 'Levels', f'Level{nv}.asset'), encoding='utf-8').read()
        for a, b in re.findall(r'position: \{x: (-?[\d.]+), y: (-?[\d.]+)\}', s):
            cy = 270 - float(b) * 64
            if cy - (mx + 1) // 2 - GAP - alto < TOPE_HUD:
                fallos.append(f'nivel {nv}: el icono sobre ({a}, {b}) pisa la barra de arriba')
    fallos += gen.comprobar_propio() if hasattr(gen, 'comprobar_propio') else []
    if fallos:
        raise SystemExit(f'ART-UI-BTN-UPG «{nombre}» no pasa:\n  ' + '\n  '.join(sorted(set(fallos))[:30]))
    return len(usados)


# --- Vistas ----------------------------------------------------------------------

def ampliar(im, k):
    return np.repeat(np.repeat(im, k, axis=0), k, axis=1)


def pega(dst, src, x, y):
    h, w = src.shape[:2]
    y1, x1 = max(y, 0), max(x, 0)
    y2, x2 = min(y + h, dst.shape[0]), min(x + w, dst.shape[1])
    if y2 <= y1 or x2 <= x1:
        return
    s = src[y1 - y:y2 - y, x1 - x:x2 - x]
    m = s[..., 3] > 0
    dst[y1:y2, x1:x2][m] = s[..., :3][m]


def nodo_con_icono(lienzo, gen, tipo, n, cx, cy, energia, tope, estado, f=0):
    import gen_energy_v1 as e1
    import gen_energy_v3 as e3
    _, carpeta, v, pre, _, _ = next(m for m in MEJORABLES if m[0] == tipo)
    spr = np.asarray(Image.open(os.path.join(RAIZ, 'art', 'nodes', carpeta, f'v{v}',
                                             f'{pre}_lvl{n}_player.png')).convert('RGBA'))
    e1.sobre_c(lienzo, spr, cx, cy)
    hu = huella(carpeta, v, pre, n)
    e1.colocar(lienzo, e3.etiqueta(str(energia), 'player', energia, tope, f), cx, cy, hu)
    if estado:
        ic = gen.icono(estado, tipo, n, f)
        pega(lienzo, ic, int(cx) - ic.shape[1] // 2, int(cy) - (hu + 1) // 2 - GAP - ic.shape[0])


TITULOS = (('aviso', 'aviso'), ('listo', 'listo'), ('activo', 'activo'), ('encima', 'encima'),
           ('pulsado', 'pulsado'))


def estados_vista(gen, f=0):
    """Una Estrella de nivel 3 propia en los cinco estados, sobre el cielo."""
    bg = np.asarray(Image.open(os.path.join(RAIZ, 'art', 'bg', 'fondos', 'v2', 'bg_section1.png')).convert('RGB'))
    celdas = []
    for i, (e, _) in enumerate(TITULOS):
        c = bg[100:220, 100 + i * 96:196 + i * 96].copy()
        en = 37 if e == 'aviso' else 44
        nodo_con_icono(c, gen, 'estrella', 3, 48, 64, en, 50, e, f)
        celdas.append(c)
        celdas.append(np.zeros((120, 2, 3), np.uint8))
    return np.hstack(celdas[:-1])


def tipos_vista(gen, f=0):
    """Los iconos activos de todos los tipos y niveles mejorables, a solas."""
    filas = []
    for tipo, _, _, _, niveles, _ in MEJORABLES:
        fila = np.empty((ALTO_MAX + 6, 4 * (ANCHO_MAX + 6) + 4, 3), np.uint8)
        fila[:] = FONDO
        for i, n in enumerate(niveles):
            ic = gen.icono('activo', tipo, n, f)
            pega(fila, ic, 4 + i * (ANCHO_MAX + 6) + (ANCHO_MAX - ic.shape[1]) // 2, 3)
        filas.append(fila)
    return np.vstack(filas)


def tablero(gen, f=0):
    """El nivel 9 con tres nodos propios en los tres tramos: la Estrella de
    nivel 2 activa (cursor encima), el Magnetar listo y una Estrella en aviso."""
    import gen_energy_v1 as e1
    import gen_energy_v3 as e3
    t = e1.tablero(e3, f)
    # la Estrella propia del nivel 9 esta en (-6.25, 1); el Magnetar propio en (-5, -1.75)
    nodo_con_icono(t, gen, 'estrella', 2, 480 - 6.25 * 64, 270 - 64, 30, 50, 'activo', f)
    nodo_con_icono(t, gen, 'magnetar', 1, 480 - 5 * 64, 270 + 1.75 * 64, 50, 100, 'listo', f)
    return t


def vistas(gen, version, gif=False):
    d = os.path.join(dir_preview('ui_' + ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(estados_vista(gen), 4)).save(os.path.join(d, 'estados_x4.png'))
    Image.fromarray(ampliar(tipos_vista(gen), 6)).save(os.path.join(d, 'tipos_x6.png'))
    t = tablero(gen)
    Image.fromarray(ampliar(t, 2)).save(os.path.join(d, 'tablero_1080p.png'))
    Image.fromarray(ampliar(t[120:480, 0:420], 3)).save(os.path.join(d, 'tablero_izq_x3.png'))
    if gif:
        # la Energia sube de 33 a 40; el cursor llega, pasa al icono y pulsa
        seq = []
        for e in range(33, 41):
            seq += [(e, False, False, False)] * 6
        seq += [(40, False, False, False)] * 12
        seq += [(40, True, False, False)] * 24
        seq += [(40, True, True, False)] * 16
        seq += [(40, True, True, True)] * 3
        ims = []
        bg = np.asarray(Image.open(os.path.join(RAIZ, 'art', 'bg', 'fondos', 'v2', 'bg_section1.png')).convert('RGB'))
        for i, (e, nodo_, icono_, puls) in enumerate(seq):
            c = bg[100:220, 100:200].copy()
            est = estado_de(e, 40, nodo_, icono_, puls)
            nodo_con_icono(c, gen, 'estrella', 3, 50, 64, e, 50, est, i % FRAMES)
            ims.append(Image.fromarray(ampliar(c, 4)))
        ims[0].save(os.path.join(d, 'secuencia_x4.gif'), save_all=True, append_images=ims[1:],
                    duration=1000 // FPS, loop=0)
    return d


def comparativa():
    filas = []
    for v in (1, 2, 3):
        if not os.path.isdir(os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{v}')):
            continue
        mod = sys.modules[__name__] if v == 1 else __import__(f'gen_upgicon_v{v}')
        filas.append(estados_vista(mod))
        filas.append(np.zeros((3, filas[-1].shape[1], 3), np.uint8))
    if filas:
        Image.fromarray(ampliar(np.vstack(filas[:-1]), 3)).save(
            os.path.join(dir_preview('ui_' + ASSET), 'comparativa_x3.png'))


def ejecutar(gen, version, nombre, permitidos):
    colores = comprobar(gen, permitidos, nombre)
    d = os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)
    gen.exportar(d)
    vistas(gen, version, '--gif' in sys.argv)
    comparativa()
    print(f'v{version} «{nombre}»: {colores} colores  -> {os.path.relpath(d, RAIZ)}')


def hoja_estados(gen, tipo=None, n=None):
    """Filas: estados; columnas: frames."""
    return np.vstack([np.hstack([gen.icono(e, tipo, n, f) for f in range(FRAMES)]) for e in ESTADOS])


# =============================================================================
# v1 «La flecha»
# =============================================================================

FW, FH = 11, 12


def flecha(cuerpo, luz, sombra, aplasta=False):
    img = rgba(FH, FW)
    c = FW // 2
    for y in range(FH):
        if y < 6:
            a = y                      # la punta: 1, 3, 5 ... 11 de ancho
        else:
            a = 2                      # el asta: 5 de ancho
        for x in range(c - a, c + a + 1):
            col = cuerpo
            if x == c - a or (y < 6 and y == 0):
                col = luz
            elif x == c + a or y == FH - 1:
                col = sombra
            img[y, x, :3] = col
            img[y, x, 3] = 255
    if aplasta:
        img = np.vstack([img[:6], img[7:], rgba(1, FW)])
    return img


def icono(estado, tipo=None, n=None, f=0):
    if estado in ('encima', 'pulsado'):
        a = flecha(STR_P[1], STR_P[0], STR_P[3], aplasta=estado == 'pulsado')
    else:
        a = flecha(STR_P[3], STR_P[1], STR_P[5])
    a = contornea(a)
    out = rgba(FH + 4, FW + 2)          # 16 de alto: sitio para respirar y bajar
    dy = 1
    if estado == 'activo':
        dy = 0 if (f // (FRAMES // 2)) % 2 == 0 else 1
    elif estado == 'pulsado':
        dy = 2
    out[dy:dy + a.shape[0]] = np.where(a[..., 3:] > 0, a, out[dy:dy + a.shape[0]])
    if estado in ('aviso', 'listo'):
        out = difumina(out)
    return out


def piezas():
    return {'hoja': hoja_estados(sys.modules[__name__])}


def exportar(d):
    Image.fromarray(hoja_estados(sys.modules[__name__]), 'RGBA').save(
        os.path.join(d, 'ui_upgrade_icon_sheet.png'))
    with open(os.path.join(d, 'ui_upgrade_icon.json'), 'w') as fh:
        json.dump({'cellWidth': FW + 2, 'cellHeight': FH + 4, 'frames': FRAMES, 'fps': FPS,
                   'rows': list(ESTADOS), 'gap': GAP, 'hintMargin': 5, 'pivot': 'bottom-center',
                   'note': 'fila = estado (aviso, listo, activo, encima, pulsado), columna = frame; el icono va '
                           'centrado sobre la huella con su borde de abajo a gap px de ella; el mismo para los tres '
                           'tipos y todos los niveles'}, fh, indent=1)


if __name__ == '__main__':
    ejecutar(sys.modules[__name__], 1, 'La flecha', colores_de('str_p') | {tuple(CONT)})
