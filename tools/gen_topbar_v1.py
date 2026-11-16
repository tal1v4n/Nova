"""NOVA - ART-UI-TOPBAR v1, la barra de arriba: «Las placas».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.6 (HUD permanente), 13.10 (pausa) y
13.12 (lenguaje de la UI) · NOVA_Arte_Tecnica.md, *Importacion en Unity*
(Jersey 10)

Este archivo tiene ademas LO COMUN A LAS TRES PROPUESTAS -las piezas, el
montaje, las comprobaciones y las vistas-; `gen_topbar_v2.py` y
`gen_topbar_v3.py` solo traen su dibujo.

QUE HAY ARRIBA (13.6). A la izquierda, el nivel: `MAPA 2 · NIVEL 5`. A la
derecha, el cronometro con el tiempo transcurrido y el boton de pausa, que
abre el menu de 13.10 como `Esc`.

EL TEXTO NO ES ARTE. Los textos de juego van en TextMeshPro con Jersey 10
(contrato, *Importacion en Unity*), a su tamano nativo: mayuscula de 10 px
de arte, 18.67 px por em. El arte es lo que va DETRAS y ALREDEDOR del texto:
la placa y el boton. Las vistas pintan el texto con la propia fuente, en su
rejilla, para que se vea como quedara.

EL SITIO. Los nodos mas altos de los nueve niveles estan en y = 3 u, a 78 px
del borde de arriba, y la celda de 96 de un nodo con todos sus overlays no
sube de 30 px. La barra cabe en 24: 4 de margen y placas de 20. El generador
lo comprueba contra los nueve assets de nivel, no a ojo.

LAS PIEZAS, iguales en las tres propuestas, para que `T6-08` no dependa de
cual se elija (las medidas, en `ui_topbar.json`):

    ui_topbar_plate.png        20 x 20, nueve cortes con bordes de 6: la
                               placa del nivel y la del cronometro. El centro
                               se repite en mosaico (Image Type: Tiled), no
                               se estira, para que la trama no se deforme
    ui_topbar_pause_sheet.png  tres estados en columnas -normal, encima y
                               pulsado-, celdas de 20 x 22; el pulsado baja
                               2 px (4 de pantalla, 13.12)

La placa del nivel mide lo que su texto mas `padX` a cada lado, con el
centro redondeado hacia arriba a un multiplo de 8 (`plateWidthStep`), y el
texto va centrado en ella; la del cronometro, lo que `00:00`, para que no
baile al cambiar de cifra.

    py tools/gen_topbar_v1.py      piezas, comprobaciones y vistas

----------------------------------------------------------------------------

LA TESIS DE LA v1: LAS PLACAS DE LA LAMINA. El boton secundario de 13.12
tal cual: relleno de panel `#11142A`, marco de 2 px en `str_p[6]` con la
esquina en escalera y el texto en `str_p[1]`. Encima, el marco se aclara a
`str_p[4]` y el icono a blanco; pulsado, baja. Es la que mas se parece a los
menus y a la carcasa de la barra de poder: el HUD entero es un solo aparato.
"""

import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from nova_core import BAYER8, RAIZ, dir_preview, rampa
from nova_paleta import RAMPAS, hex_a_rgb

ASSET = 'topbar'

ANCHO, ALTO_PANT = 960, 540
MARGEN = 4             # al borde de arriba y a los lados
ALTO = 20              # placa y boton
BORDE = 6              # nueve cortes: la escalera (4) mas el marco (2)
PLACA_W = 20           # 6 + 8 + 6: el centro, multiplo de 8, no rompe la trama
PAD_X = 6
GAP = 4                # entre el cronometro y el boton
BOTON = 20
CELDA_B = (22, 20)     # alto, ancho
BAJA = 2               # el pulsado
CAP = 10               # mayuscula de Jersey 10 a tamano nativo
TEXTO_Y = 5            # fila de la mayuscula dentro de la placa: 5..14
ESPACIADO = 1          # mayusculas espaciadas (13.12)
TOPE_HUD = MARGEN + ALTO

PANEL = np.array(hex_a_rgb('#11142A'), np.uint8)
FONDO = np.array(hex_a_rgb('#0B0E1A'), np.uint8)
STR_P = rampa('str_p')

FUENTE = os.path.join(RAIZ, 'Assets', 'Art', 'Fonts', 'Jersey10-Regular.ttf')


# --- Texto en la rejilla nativa de Jersey 10 ----------------------------------

_F56 = None


def texto(cadena, espaciado=ESPACIADO):
    """Mascara del texto a tamano nativo: se dibuja a 56 px (3 px por pixel
    de diseno) y se toma el centro de cada bloque de 3. Filas: la mayuscula
    ocupa 0..9."""
    global _F56
    if _F56 is None:
        _F56 = ImageFont.truetype(FUENTE, 56)
    cols = []
    for i, ch in enumerate(cadena):
        w = int(round(_F56.getlength(ch)))
        im = Image.new('1', (max(w, 3) + 6, 90), 0)
        d = ImageDraw.Draw(im)
        d.fontmode = '1'
        d.text((0, 0), ch, font=_F56, fill=1)
        a = np.array(im)[1::3, 1::3][5:5 + CAP + 3]        # mayuscula en 0..9
        cols.append(a[:, :w // 3])
        if i < len(cadena) - 1:
            cols.append(np.zeros((a.shape[0], espaciado), bool))
    return np.hstack(cols)


def ancho_texto(cadena):
    return texto(cadena).shape[1]


# --- Utilidades --------------------------------------------------------------

def bayer(x, y):
    return BAYER8[y % 8, x % 8]


def rgba(rgb, on):
    a = np.zeros(on.shape + (4,), np.uint8)
    a[on, :3] = rgb[on]
    a[on, 3] = 255
    return a


def escalera(ancho, alto, abajo=True, arriba=True):
    y, x = np.mgrid[0:alto, 0:ancho]
    ex = np.minimum(x, ancho - 1 - x)
    corte = np.zeros((alto, ancho), bool)
    if arriba:
        corte |= ((y < 2) & (ex < 4)) | ((y < 4) & (ex < 2))
    if abajo:
        eb = alto - 1 - y
        corte |= ((eb < 2) & (ex < 4)) | ((eb < 4) & (ex < 2))
    return ~corte


def erosion(m, r=2):
    h, w = m.shape
    out = m.copy()
    p = np.pad(m, r, constant_values=False)
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            out &= p[r + dy:r + dy + h, r + dx:r + dx + w]
    return out


# --- Montaje -----------------------------------------------------------------

def placa(piezas, ancho):
    """La placa a un ancho, como Unity con Image Type: Tiled: los bordes
    enteros y el centro repetido desde el borde izquierdo."""
    p = piezas['placa']
    centro = p[:, BORDE:PLACA_W - BORDE]
    n = ancho - 2 * BORDE
    reps = np.tile(centro, (1, n // centro.shape[1] + 1, 1))[:, :n]
    return np.hstack([p[:, :BORDE], reps, p[:, PLACA_W - BORDE:]])


def pegar(lienzo, im, x, y):
    h, w = im.shape[:2]
    m = im[..., 3] > 0
    lienzo[y:y + h, x:x + w][m] = im[..., :3][m]


def pegar_texto(lienzo, mascara, x, y, color):
    h, w = mascara.shape
    lienzo[y:y + h, x:x + w][mascara] = color


def ancho_placa(texto_px):
    """El texto con su aire, y el centro redondeado a 8 hacia arriba: el
    mosaico entra entero y la trama no se corta en el borde derecho."""
    n = texto_px + 2 * PAD_X - 2 * BORDE
    return -(-n // 8) * 8 + 2 * BORDE


def rects(etiqueta, arriba=MARGEN):
    """Las cajas del HUD en la pantalla de 960 x 540: (x, y, w, h)."""
    wl = ancho_placa(ancho_texto(etiqueta))
    wt = ancho_placa(ancho_texto('00:00'))
    xb = ANCHO - MARGEN - BOTON
    return {'nivel': (MARGEN, arriba, wl, ALTO),
            'crono': (xb - GAP - wt, arriba, wt, ALTO),
            'pausa': (xb, arriba, BOTON, ALTO)}


def montar(piezas, lienzo, etiqueta, tiempo, estado=0):
    r = rects(etiqueta, piezas.get('arriba', MARGEN))
    for clave, txt in (('nivel', etiqueta), ('crono', tiempo)):
        x, y, w, h = r[clave]
        pegar(lienzo, placa(piezas, w), x, y)
        m = texto(txt)
        tx = x + (w - m.shape[1]) // 2      # centrado: la placa sube de 8 en 8
        pegar_texto(lienzo, m, tx, y + TEXTO_Y, piezas['color_texto'][clave])
    x, y, _, _ = r['pausa']
    b = piezas['pausa'][estado]
    pegar(lienzo, b, x, y)
    return lienzo


# --- Comprobaciones ------------------------------------------------------------

def niveles():
    """(numero, [(x, y) en pixeles de pantalla]) de los nueve assets."""
    out = []
    d = os.path.join(RAIZ, 'Assets', 'Data', 'Levels')
    for n in range(1, 10):
        s = open(os.path.join(d, f'Level{n}.asset'), encoding='utf-8').read()
        pos = [(480 + float(a) * 64, 270 - float(b) * 64)
               for a, b in re.findall(r'position: \{x: (-?[\d.]+), y: (-?[\d.]+)\}', s)]
        out.append((n, pos))
    return out


def etiqueta_de(n):
    return f'MAPA {(n - 1) // 3 + 1} · NIVEL {n}'


def colores_de(*nombres):
    s = set()
    for n in nombres:
        s |= {hex_a_rgb(h) for h in RAMPAS[n]}
    return s


def comprobar(piezas, version, permitidos):
    fallos = []
    p = piezas['placa']
    if p.shape[:2] != (ALTO, PLACA_W):
        fallos.append(f'placa de {p.shape[1]} x {p.shape[0]}')
    for i, b in enumerate(piezas['pausa']):
        if b.shape[:2] != CELDA_B:
            fallos.append(f'boton {i}: celda de {b.shape[1]} x {b.shape[0]}')
    todo = [p] + piezas['pausa']
    if any(not set(np.unique(im[..., 3])) <= {0, 255} for im in todo):
        fallos.append('alfa no binario')
    usados = set()
    for im in todo:
        usados |= {tuple(int(v) for v in c) for c in im[..., :3][im[..., 3] > 0]}
    usados |= {tuple(int(v) for v in c) for c in piezas['color_texto'].values()}
    fuera = usados - permitidos
    if fuera:
        fallos.append('colores fuera de paleta: ' + ', '.join('#%02X%02X%02X' % c for c in sorted(fuera)))
    if len({b.tobytes() for b in piezas['pausa']}) < 3:
        fallos.append('dos estados del boton iguales')
    # el pulsado es el normal o el encima bajado BAJA px
    # el texto cabe entero en la placa, con su aire
    if TEXTO_Y < 3 or TEXTO_Y + CAP > ALTO - 3:
        fallos.append('el texto no cabe en la placa')
    # nada del HUD pisa la celda de 96 de un nodo, en ninguno de los nueve
    for n, pos in niveles():
        for clave, (x, y, w, h) in rects(etiqueta_de(n), piezas.get('arriba', MARGEN)).items():
            for cx, cy in pos:
                if x < cx + 48 and cx - 48 < x + w and y < cy + 48 and cy - 48 < y + h + BAJA:
                    fallos.append(f'nivel {n}: {clave} pisa el nodo de ({cx:.0f}, {cy:.0f})')
    # el cronometro no se sale con la hora mas ancha
    if max(ancho_texto(f'{m}:{s:02d}') for m in range(10) for s in range(60)) > ancho_texto('00:00'):
        fallos.append('hay tiempos mas anchos que 00:00')
    if fallos:
        raise SystemExit(f'ART-UI-TOPBAR v{version} no pasa:\n  ' + '\n  '.join(fallos))
    return len(usados)


# --- Hojas -------------------------------------------------------------------

def exportar(piezas, version):
    d = os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(piezas['placa'], 'RGBA').save(os.path.join(d, 'ui_topbar_plate.png'))
    Image.fromarray(np.hstack(piezas['pausa']), 'RGBA').save(os.path.join(d, 'ui_topbar_pause_sheet.png'))
    hexa = {k: '#%02X%02X%02X' % tuple(int(v) for v in c) for k, c in piezas['color_texto'].items()}
    with open(os.path.join(d, 'ui_topbar.json'), 'w') as fh:
        json.dump({'margin': MARGEN, 'top': piezas.get('arriba', MARGEN), 'height': ALTO,
                   'plateWidthStep': 8, 'textAlign': 'center', 'plateBorder': BORDE,
                   'plateCenterMode': 'tiled', 'padX': PAD_X, 'gap': GAP,
                   'textCapTop': TEXTO_Y, 'textCapHeight': CAP, 'letterSpacing': ESPACIADO,
                   'levelText': hexa['nivel'], 'timerText': hexa['crono'],
                   'timerTemplate': '00:00', 'pauseCell': [CELDA_B[1], CELDA_B[0]],
                   'pauseStates': ['normal', 'hover', 'pressed'], 'pressedDrop': BAJA}, fh, indent=1)
    return d


# --- Vistas ------------------------------------------------------------------

def ampliar(im, k):
    return np.repeat(np.repeat(im, k, axis=0), k, axis=1)


def sprite(*ruta):
    return np.asarray(Image.open(os.path.join(RAIZ, 'art', 'nodes', *ruta)).convert('RGBA'))


def pegar_c(lienzo, im, cx, cy):
    h, w = im.shape[:2]
    x0, y0 = int(cx) - w // 2, int(cy) - h // 2
    m = im[..., 3] > 0
    lienzo[y0:y0 + h, x0:x0 + w][m] = im[..., :3][m]


def tablero(piezas, estado=0, tiempo='2:14'):
    """El nivel 9 entero -el que tiene mas nodos arriba-, con la barra de
    poder elegida abajo: el HUD completo a 960 x 540."""
    fondo = np.asarray(Image.open(os.path.join(
        RAIZ, 'art', 'bg', 'fondos', 'v2', 'bg_section3.png')).convert('RGB')).copy()
    tipos = {0: ('asteroide', 'v1', 'ast_lvl0'), 1: ('estrella', 'v1', 'str_lvl{}'),
             2: ('pulsar', 'v2', 'pul_lvl{}'), 3: ('magnetar', 'v1', 'mag_lvl{}')}
    fac = {0: 'neutral', 1: 'player', 2: 'enemy'}
    s = open(os.path.join(RAIZ, 'Assets', 'Data', 'Levels', 'Level9.asset'), encoding='utf-8').read()
    for m in re.finditer(r'position: \{x: (-?[\d.]+), y: (-?[\d.]+)\}\s+type: (\d)\s+level: (\d)\s+faction: (\d)', s):
        x, y, t, lv, fa = float(m[1]), float(m[2]), int(m[3]), int(m[4]), int(m[5])
        carpeta, v, nombre = tipos[t]
        nombre = nombre.format(lv) if t else nombre
        try:
            im = sprite(carpeta, v, f'{nombre}_{fac[fa]}.png')
        except FileNotFoundError:
            continue
        pegar_c(fondo, im, 480 + x * 64, 270 - y * 64)
    import gen_powerbar_v1 as pb
    barra = pb.montar(pb.piezas_v1(), 0.46, 0)
    mm = barra[..., 3] > 0
    fondo[ALTO_PANT - pb.ALTO:][mm] = barra[..., :3][mm]
    return montar(piezas, fondo, etiqueta_de(9), tiempo, estado)


def vistas(piezas, version):
    d = os.path.join(dir_preview('ui_' + ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    t = tablero(piezas)
    Image.fromarray(ampliar(t, 2)).save(os.path.join(d, 'tablero_1080p.png'))
    Image.fromarray(ampliar(t[0:130], 3)[:, :]).save(os.path.join(d, 'arriba_x3.png'))
    # las piezas de cerca: la placa a tres anchos y los tres estados, x6
    filas = []
    for etq in ('MAPA 1 · NIVEL 1', 'MAPA 3 · NIVEL 9'):
        bg = np.empty((ALTO + 8, 200, 3), np.uint8)
        bg[:] = FONDO
        m = texto(etq)
        w = ancho_placa(m.shape[1])
        pegar(bg, placa(piezas, w), 4, 4)
        pegar_texto(bg, m, 4 + (w - m.shape[1]) // 2, 4 + TEXTO_Y, piezas['color_texto']['nivel'])
        filas.append(bg)
    bg = np.empty((CELDA_B[0] + 8, 200, 3), np.uint8)
    bg[:] = FONDO
    for i, b in enumerate(piezas['pausa']):
        pegar(bg, b, 4 + i * 30, 4)
    filas.append(bg)
    Image.fromarray(ampliar(np.vstack(filas), 6)).save(os.path.join(d, 'piezas_x6.png'))
    return d


def comparativa():
    filas = []
    for v in (1, 2, 3):
        if not os.path.isdir(os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{v}')):
            continue
        mod = __import__(f'gen_topbar_v{v}')
        p = mod.piezas() if v != 1 else piezas_v1()
        t = tablero(p)
        filas.append(np.hstack([t[0:40, 0:200], t[0:40, 760:960]]))
        filas.append(np.zeros((3, 400, 3), np.uint8))
    if filas:
        Image.fromarray(ampliar(np.vstack(filas), 3)).save(
            os.path.join(dir_preview('ui_' + ASSET), 'comparativa_x3.png'))


def ejecutar(construir, version, nombre, permitidos):
    piezas = construir()
    colores = comprobar(piezas, version, permitidos)
    d = exportar(piezas, version)
    vistas(piezas, version)
    comparativa()
    print(f'v{version} «{nombre}»: {colores} colores  -> {os.path.relpath(d, RAIZ)}')


# =============================================================================
# v1 «Las placas»
# =============================================================================

def placa_v1(marco=STR_P[6]):
    sil = escalera(PLACA_W, ALTO)
    dentro = erosion(sil)
    rgb = np.zeros((ALTO, PLACA_W, 3), np.uint8)
    rgb[sil] = marco
    rgb[dentro] = PANEL
    return rgba(rgb, sil)


def icono_pausa(rgb, on, x0, y0, color):
    """Dos barras de 3 x 10, separadas 2: el signo de pausa a la altura de
    la mayuscula, para que lea como el texto de al lado."""
    for bx in (x0, x0 + 5):
        rgb[y0:y0 + CAP, bx:bx + 3] = color
        on[y0:y0 + CAP, bx:bx + 3] = True


def boton_v1(estado):
    h, w = CELDA_B
    rgb = np.zeros((h, w, 3), np.uint8)
    on = np.zeros((h, w), bool)
    dy = BAJA if estado == 2 else 0
    marco = STR_P[4] if estado == 1 else STR_P[6]
    icono = STR_P[0] if estado == 1 else STR_P[1]
    sil = escalera(BOTON, ALTO)
    dentro = erosion(sil)
    rgb[dy:dy + ALTO][sil] = marco
    rgb[dy:dy + ALTO][dentro] = PANEL
    on[dy:dy + ALTO] = sil
    icono_pausa(rgb, on, 6, dy + TEXTO_Y, icono)
    return rgba(rgb, on)


def piezas_v1():
    return {'placa': placa_v1(),
            'pausa': [boton_v1(e) for e in range(3)],
            'color_texto': {'nivel': STR_P[1], 'crono': STR_P[1]}}


if __name__ == '__main__':
    ejecutar(piezas_v1, 1, 'Las placas', colores_de('str_p') | {tuple(PANEL)})
