"""NOVA - ART-UI-IGN-MENU v1, el menu radial de encendido: «Las teclas».

Ref: NOVA_Assets_Diseno.md A.6 · Spec 13.7 (el menu), 13.12 (lenguaje de
la UI, patrones «Ignicion» y «Carga»), 16.1 (coste de encendido), 18 #28,
#29, #36

Este archivo tiene ademas LO COMUN A LAS TRES PROPUESTAS -la geometria,
los estados, los pictogramas de los tres tipos, la entrada, las
comprobaciones y las vistas-; `gen_ignmenu_v2.py` y `gen_ignmenu_v3.py`
solo traen su dibujo.

QUE ES. El menu que abre el clic izquierdo sobre un Asteroide propio, sin
seleccion, centrado en el cursor (13.7): tres opciones, siempre las mismas
y en el orden de 16.2 -Estrella, Pulsar, Magnetar-, en el sentido de las
agujas del reloj desde arriba. La geometria es la de
`IgnitionMenuState`: cada opcion a 64 px del centro (1 u), a 90, -30 y
210 grados, con un circulo de clic de 24 px de radio; el nombre del tipo
va encima de la opcion de arriba y debajo de las de abajo, a 4 px del
circulo de clic (hoy va en diagonal hacia fuera y pisa las de abajo). El coste y el
nombre van en TextMeshPro con Jersey 10; las vistas los pintan con la
propia fuente.

LOS ESTADOS DE CADA OPCION (13.7, 13.12, `IgnitionMenu.OptionLook`):

    normal      se puede encender
    encima      el raton esta encima (160 ms, orbital)
    pulsado     baja 2 px
    falta       el Asteroide tiene menos de 10: atenuada y con el coste en
                magenta; NO se oculta y pulsarla deja el menu abierto
    nodisp      el tipo no existe en la partida o el nivel no lo permite
                (18 #29, #36): gris y sin coste

LA ENTRADA (patron «Ignicion» de 13.12). Las tres salen del centro hacia
su sitio con la orbital, 320 ms cada una y 80 ms de escalonado: la UI se
mueve en continuo. El nombre entra con su opcion.

    py tools/gen_ignmenu_v1.py         piezas, comprobaciones y vistas
    py tools/gen_ignmenu_v1.py --gif   y la animada

----------------------------------------------------------------------------

LA TESIS DE LA v1: TRES TECLAS DE LA LAMINA. Cada opcion es el boton
secundario de 13.12 -relleno `#11142A`, marco de 2 px en `str_p[6]` y la
esquina en escalera, ningun radio- en un cuadrado de 44 x 44, con un
PICTOGRAMA del tipo arriba y el coste debajo. Encima, el patron «Carga»: la
tecla se llena de abajo arriba con una trama cian y el marco se enciende.
Sin Energia, gris con el coste en magenta. No disponible, el marco en
trazos y el pictograma solo en contorno.
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_panel_v1 as pv1
from gen_panel_v1 import (FONDO, NEU, PANEL, STR_E, STR_P, ampliar, ancho, colores_de, pega, pinta,
                          placa_lamina, rgba)
from nova_core import BAYER8, RAIZ, dir_preview

ASSET = 'ignition_menu'
DIST = 64                       # IgnitionMenuState.OptionDistance, 1 u
RADIO = 24                      # IgnitionMenuState.OptionRadius, el clic
HUECO_NOMBRE = 4                # del circulo de clic a la mayuscula del nombre
EXTENT = 112                    # IgnitionMenuState.Extent, 1.75 u
CELDA = 56                      # la celda de una opcion, centrada en ella
BAJA = 2
COSTE = 10                      # 16.1, coste de encendido (las vistas)
TIPOS = ('estrella', 'pulsar', 'magnetar')
NOMBRE = {'estrella': 'Estrella', 'pulsar': 'Púlsar', 'magnetar': 'Magnetar'}
ESTADOS = ('normal', 'encima', 'pulsado', 'falta', 'nodisp')
LIENZO = 2 * EXTENT + 32
C_NOMBRE, C_GRIS, C_APAGADO, C_COSTE, C_FALTA = NEU[1], NEU[3], NEU[4], STR_P[3], STR_E[3]
ENTRADA_MS, ESCALON_MS = 320, 80


def angulo(i):
    return math.radians(90 - 120 * i)


def desplazamiento(i, r=DIST):
    """(dx, dy) en pixeles de pantalla, y hacia abajo."""
    a = angulo(i)
    return r * math.cos(a), -r * math.sin(a)


def orbital(t):
    import gen_energy_v2 as e2
    return e2.orbital(max(0.0, min(1.0, t)))


# --- Pictogramas de los tres tipos -------------------------------------------------

PICTO = 18


def picto(tipo, luz, sombra, contorno=False):
    """Pictograma de 18 x 18: la Estrella, un sol con ocho rayos; el Pulsar,
    un nucleo con sus dos conos; el Magnetar, un nucleo entre dos lazos de
    campo. Cuerpo en `luz` y medio tono en `sombra`. `contorno` deja solo el
    borde (no disponible)."""
    n = PICTO
    yy, xx = np.mgrid[0:n, 0:n] + 0.5
    cx = cy = n / 2
    dx, dy = xx - cx, yy - cy
    r = np.hypot(dx, dy)
    th = np.arctan2(dy, dx)
    cuerpo = np.zeros((n, n), bool)
    medio = np.zeros((n, n), bool)
    if tipo == 'estrella':
        cuerpo |= r <= 4.2
        for k in range(8):
            a = k * math.pi / 4
            largo = 8.6 if k % 2 == 0 else 6.6
            along = dx * math.cos(a) + dy * math.sin(a)
            perp = np.abs(-dx * math.sin(a) + dy * math.cos(a))
            medio |= (along > 5.2) & (along < largo) & (perp < 0.75)
    elif tipo == 'pulsar':
        cuerpo |= r <= 3.2
        for a in (-math.pi / 4, 3 * math.pi / 4):          # arriba derecha y abajo izquierda
            along = dx * math.cos(a) + dy * math.sin(a)
            perp = np.abs(-dx * math.sin(a) + dy * math.cos(a))
            medio |= (along > 3.8) & (along < 8.8) & (perp < 0.6 + 0.42 * (along - 3.8))
    else:
        cuerpo |= r <= 3.2
        for s in (-1, 1):
            ex = (dx - s * 4.6) / 4.3
            ey = dy / 6.6
            e = np.hypot(ex, ey)
            medio |= (e > 0.78) & (e < 1.0) & (s * dx > 1.2)
    img = rgba(n, n)
    if contorno:
        sil = cuerpo | medio
        borde = sil & ~pv1.erosion(sil, 1)
        img[borde, :3] = luz
        img[borde, 3] = 255
        return img
    img[medio, :3] = sombra
    img[cuerpo, :3] = luz
    img[(cuerpo | medio), 3] = 255
    return img


# --- El menu entero ----------------------------------------------------------------

def nombre_xy(i):
    """Centro de la etiqueta del tipo, relativo al centro del menu: encima de
    la opcion de arriba y debajo de las de abajo, centrada en su x, con la
    mayuscula a HUECO_NOMBRE del circulo de clic. «Hacia fuera» en diagonal,
    como hoy `IgnitionMenu`, pisa la propia opcion en las dos de abajo."""
    dx, dy = desplazamiento(i)
    s = -1 if i == 0 else 1
    return dx, dy + s * (RADIO + HUECO_NOMBRE + 5)


def menu(gen, estados, energia, f=0, t_entrada=None, t_encima=1.0):
    """El menu en un lienzo RGBA de LIENZO x LIENZO con su centro en medio.
    `estados`, uno por opcion. `t_entrada` en ms desde que se abre."""
    img = rgba(LIENZO, LIENZO)
    c = LIENZO // 2
    if hasattr(gen, 'centro'):
        gen.centro(img, c, estados, energia, f, t_entrada)
    for i, (tipo, e) in enumerate(zip(TIPOS, estados)):
        k = 1.0 if t_entrada is None else orbital((t_entrada - i * ESCALON_MS) / ENTRADA_MS)
        if k <= 0:
            continue
        dx, dy = desplazamiento(i, DIST * k)
        op = gen.opcion(e, tipo, energia, f=f, t=t_encima if e == 'encima' else 1.0)
        cel = op.shape[0]
        pega(img, op, int(round(c + dx)) - cel // 2, int(round(c + dy)) - cel // 2)
        if k >= 0.999 and hasattr(gen, 'rotulo'):
            gen.rotulo(img, i, tipo, e, c)
        elif k >= 0.999:
            nx, ny = nombre_xy(i)
            color = {'normal': C_NOMBRE, 'encima': STR_P[0], 'pulsado': STR_P[0]}.get(e, C_GRIS)
            if e == 'nodisp':
                color = C_APAGADO
            pinta(img, NOMBRE[tipo], int(round(c + nx)) - ancho(NOMBRE[tipo]) // 2, int(round(c + ny)) - 5, color)
    return img


def rotula_coste(img, estado, cx, y, color=None):
    """El coste centrado en cx con la mayuscula en la fila y."""
    if estado == 'nodisp':
        return
    col = color if color is not None else (C_FALTA if estado == 'falta' else
                                           STR_P[1] if estado in ('encima', 'pulsado') else C_COSTE)
    pinta(img, str(COSTE), cx - ancho(str(COSTE)) // 2, y, col)


# --- Comprobaciones --------------------------------------------------------------

def comprobar(gen, permitidos, nombre):
    fallos = []
    usados = set()
    CEL = getattr(gen, 'CELDA', CELDA)
    for clave, im in gen.piezas().items():
        if not set(np.unique(im[..., 3])) <= {0, 255}:
            fallos.append(f'{clave}: alfa no binario')
        usados |= {tuple(int(v) for v in c) for c in im[..., :3][im[..., 3] > 0]}
    for tipo in TIPOS:
        vistos = {}
        for e in ESTADOS:
            im = gen.opcion(e, tipo, 6 if e == 'falta' else 10)
            if im.shape[:2] != (CEL, CEL):
                fallos.append(f'{tipo} {e}: mide {im.shape[1]} x {im.shape[0]}')
            if not set(np.unique(im[..., 3])) <= {0, 255}:
                fallos.append(f'{tipo} {e}: alfa no binario')
            usados |= {tuple(int(v) for v in c) for c in im[..., :3][im[..., 3] > 0]}
            vistos[e] = im.tobytes()
            # la opcion cabe en su celda y no se mete en la de al lado: entre
            # dos centros hay 111 px
            ys, xs = np.nonzero(im[..., 3])
            if len(xs) and (xs.min() < 1 or xs.max() >= CEL - 1 or ys.max() >= CEL - 1):
                fallos.append(f'{tipo} {e}: toca el borde de su celda')
        if len(set(vistos.values())) < len(ESTADOS):
            fallos.append(f'{tipo}: dos estados iguales')
        if (gen.opcion('pulsado', tipo, 10)[:BAJA, :, 3] > 0).any():
            fallos.append(f'{tipo}: el pulsado no baja')
    fuera = usados - permitidos
    if fuera:
        fallos.append('colores fuera de paleta: ' + ', '.join('#%02X%02X%02X' % c for c in sorted(fuera)))

    solo_e = [c for c in STR_E if not any((c == o).all() for o in list(STR_P) + list(NEU))]

    def magenta(im):
        return sum(int((im[..., :3] == c).all(-1).sum()) for c in solo_e)
    m = menu(gen, ('falta',) * 3, 6)
    if not magenta(m):
        fallos.append('sin Energia no hay magenta')
    if magenta(menu(gen, ('nodisp',) * 3, 10)) or magenta(menu(gen, ('normal',) * 3, 10)):
        fallos.append('magenta donde no falta nada')
    if getattr(gen, 'FALTA_CRECE', False):
        if not magenta(menu(gen, ('falta',) * 3, 2)) > magenta(menu(gen, ('falta',) * 3, 8)):
            fallos.append('lo que falta no crece')
    # el menu entero cabe en el Extent de IgnitionMenuState (para que la
    # camara lo pueda meter en pantalla) y los nombres no pisan las opciones
    m = menu(gen, ('normal',) * 3, 10)
    ys, xs = np.nonzero(m[..., 3])
    c = LIENZO // 2
    if max(abs(xs - c).max(), abs(ys - c).max()) > EXTENT:
        fallos.append('el menu se sale del Extent')
    for i, tipo in enumerate(TIPOS):
        nx, ny = nombre_xy(i)
        x0 = c + nx - ancho(NOMBRE[tipo]) / 2
        cajas = [(c + desplazamiento(j)[0] - CELDA / 2, c + desplazamiento(j)[1] - CELDA / 2) for j in range(3)]
        for (ox, oy) in cajas:
            if (x0 < ox + CELDA - 4 and x0 + ancho(NOMBRE[tipo]) > ox + 4 and
                    c + ny - 8 < oy + CELDA - 4 and c + ny + 8 > oy + 4):
                fallos.append(f'el nombre de {tipo} pisa una opcion')
    fallos += gen.comprobar_propio() if hasattr(gen, 'comprobar_propio') else []
    if fallos:
        raise SystemExit(f'ART-UI-IGN-MENU «{nombre}» no pasa:\n  ' + '\n  '.join(sorted(set(fallos))))
    return len(usados)


# --- Vistas --------------------------------------------------------------------

def cielo(h, w, seccion=1, x=0, y=0):
    bg = np.asarray(Image.open(os.path.join(RAIZ, 'art', 'bg', 'fondos', 'v2', f'bg_section{seccion}.png')).convert('RGB'))
    return bg[y:y + h, x:x + w].copy()


def asteroide(lienzo, cx, cy, fac='player'):
    import gen_energy_v1 as e1
    im = np.asarray(Image.open(os.path.join(RAIZ, 'art', 'nodes', 'asteroide', 'v1',
                                            f'ast_lvl0_{fac}.png')).convert('RGBA'))
    e1.sobre_c(lienzo, im, cx, cy)


def con_numero(lienzo, cx, cy, energia):
    import gen_energy_v1 as e1
    import gen_energy_v3 as e3
    hu = e1.huella('asteroide', 'ast', 0)
    e1.colocar(lienzo, e3.etiqueta(str(energia), 'player', energia, 10, 0), cx, cy, hu)


CASOS = (('se puede', ('normal', 'encima', 'normal'), 10),
         ('falta', ('falta', 'falta', 'falta'), 6),
         ('mapa 1', ('pulsado', 'nodisp', 'normal'), 12))


def caso(gen, estados, energia, f=0, t_entrada=None, t_encima=1.0, seccion=1, x=40, y=20):
    c = cielo(LIENZO, LIENZO, seccion, x, y)
    m = LIENZO // 2
    asteroide(c, m, m)
    con_numero(c, m, m, energia)
    pega(c, menu(gen, estados, energia, f, t_entrada, t_encima), 0, 0)
    return c


def estados_vista(gen, f=0):
    celdas = []
    for i, (_, est, en) in enumerate(CASOS):
        celdas += [caso(gen, est, en, f, x=40 + 260 * i), np.zeros((LIENZO, 3, 3), np.uint8)]
    return np.hstack(celdas[:-1])


def tablero(gen, f=0):
    """El nivel 9 con el menu abierto sobre un Asteroide propio en (-3.5, 0.25)."""
    import gen_energy_v1 as e1
    import gen_energy_v3 as e3
    t = e1.tablero(e3, f)
    cx, cy = int(480 - 3.5 * 64), int(270 - 0.25 * 64)
    asteroide(t, cx, cy)
    pega(t, menu(gen, ('normal', 'encima', 'normal'), 10, f), cx - LIENZO // 2, cy - LIENZO // 2)
    return t


def vistas(gen, version, gif=False):
    d = os.path.join(dir_preview('ui_' + ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(estados_vista(gen), 2)).save(os.path.join(d, 'casos_x2.png'))
    Image.fromarray(ampliar(tablero(gen), 2)).save(os.path.join(d, 'tablero_1080p.png'))
    piezas = gen.piezas()
    if gif:
        # se abre (entrada), el cursor pasa por la Estrella, pulsa; a 50 fps
        ims = []
        n_ent = (ENTRADA_MS + 2 * ESCALON_MS) // 20 + 10
        for k in range(n_ent):
            ims.append(caso(gen, ('normal',) * 3, 10, f=k // 4, t_entrada=k * 20))
        for k in range(9):
            ims.append(caso(gen, ('encima', 'normal', 'normal'), 10, f=(n_ent + k) // 4, t_encima=k / 8))
        for k in range(25):
            ims.append(caso(gen, ('encima', 'normal', 'normal'), 10, f=(n_ent + 9 + k) // 4))
        for k in range(8):
            ims.append(caso(gen, ('pulsado', 'normal', 'normal'), 10, f=(n_ent + 34 + k) // 4))
        ims = [Image.fromarray(ampliar(im, 2)) for im in ims]
        ims[0].save(os.path.join(d, 'abrir_y_pulsar_x2.gif'), save_all=True, append_images=ims[1:],
                    duration=20, loop=0)
    return d


def comparativa():
    filas = []
    for v in (1, 2, 3):
        if not os.path.isdir(os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{v}')):
            continue
        mod = sys.modules[__name__] if v == 1 else __import__(f'gen_ignmenu_v{v}')
        filas.append(estados_vista(mod))
        filas.append(np.zeros((4, filas[-1].shape[1], 3), np.uint8))
    if filas:
        Image.fromarray(ampliar(np.vstack(filas[:-1]), 2)).save(
            os.path.join(dir_preview('ui_' + ASSET), 'comparativa_x2.png'))


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
    return {'cell': CELDA, 'optionDistance': DIST, 'clickRadius': RADIO, 'nameGap': HUECO_NOMBRE,
            'extent': EXTENT, 'pressedDrop': BAJA, 'options': list(TIPOS), 'states': list(ESTADOS),
            'enterMs': ENTRADA_MS, 'staggerMs': ESCALON_MS, 'hoverMs': 160,
            'curve': [0.2, 0.8, 0.2, 1.0],
            'text': {'name': hexa(C_NOMBRE), 'nameHover': hexa(STR_P[0]), 'cost': hexa(C_COSTE),
                     'costHover': hexa(STR_P[1]), 'muted': hexa(C_GRIS), 'missing': hexa(C_FALTA),
                     'unavailable': hexa(C_APAGADO)},
            'pivot': 'centro de la celda = centro de la opcion'}


def hoja_pictos(colores):
    """Fila = tipo, columna = variante de color (dict nombre -> (luz, sombra, contorno))."""
    filas = []
    for tipo in TIPOS:
        filas.append(np.hstack([picto(tipo, l, s, c) for (l, s, c) in colores.values()]))
    return np.vstack(filas)


# =============================================================================
# v1 «Las teclas»
# =============================================================================

LADO = 44
PZ, BORDE = 20, 6
PASOS = 5
PICTO_Y = 6                     # fila de arriba del pictograma, dentro de la tecla
COSTE_Y = 29                    # fila de la mayuscula del coste
PICTOS = {'claro': (STR_P[1], STR_P[3], False), 'encima': (STR_P[0], STR_P[2], False),
          'gris': (NEU[3], NEU[4], False), 'apagado': (NEU[4], NEU[4], True)}


def tecla(estado):
    marco = {'normal': STR_P[6], 'encima': STR_P[4], 'pulsado': STR_P[4],
             'falta': NEU[5], 'nodisp': NEU[5]}[estado]
    p = placa_lamina(PZ, PZ, marco)
    if estado == 'nodisp':
        import gen_btnconv_v1 as bc
        sil = pv1.escalera(PZ, PZ)
        p[(sil & ~pv1.erosion(sil)) & ~bc.trazos(sil), :3] = PANEL
    return p


def carga(paso):
    t = rgba(8, 8)
    umbral = paso / (PASOS + 1) * 0.6
    for y in range(8):
        for x in range(8):
            if BAYER8[y, x] < umbral:
                t[y, x, :3] = STR_P[5] if BAYER8[y, x] > umbral / 2 else STR_P[4]
                t[y, x, 3] = 255
    return t


def picto_de(estado, tipo):
    clave = {'normal': 'claro', 'encima': 'encima', 'pulsado': 'encima', 'falta': 'gris',
             'nodisp': 'apagado'}[estado]
    return picto(tipo, *PICTOS[clave])


def opcion(estado, tipo, energia, f=0, t=1.0):
    img = rgba(CELDA, CELDA)
    dy = BAJA if estado == 'pulsado' else 0
    cuerpo = pv1.nueve(tecla(estado), LADO, LADO, BORDE)
    if estado in ('encima', 'pulsado'):
        paso = PASOS if estado == 'pulsado' else max(1, int(round(t * PASOS)))
        alto = int(round((LADO - 4) * (1 if estado == 'pulsado' else orbital(t))))
        tr = np.tile(carga(paso), (LADO // 8 + 1, LADO // 8 + 1, 1))[:LADO, :LADO]
        m = pv1.erosion(pv1.escalera(LADO, LADO)) & (tr[..., 3] > 0)
        m[:LADO - 2 - alto] = False
        cuerpo[m] = tr[m]
    pega(cuerpo, picto_de(estado, tipo), (LADO - PICTO) // 2, PICTO_Y)
    rotula_coste(cuerpo, estado, LADO // 2, COSTE_Y)
    o = (CELDA - LADO) // 2
    pega(img, cuerpo, o, o - BAJA // 2 + dy)
    return img


def piezas():
    d = {f'tecla_{e}': tecla(e) for e in ESTADOS}
    for p in range(1, PASOS + 1):
        d[f'carga_{p}'] = carga(p)
    d['pictos'] = hoja_pictos(PICTOS)
    return d


def comprobar_propio():
    fallos = []
    if len({carga(p).tobytes() for p in range(1, PASOS + 1)}) < PASOS:
        fallos.append('dos pasos de la carga iguales')
    if PICTO_Y + PICTO > COSTE_Y - 3:
        fallos.append('el pictograma pisa el coste')
    return fallos


def exportar(d):
    p = piezas()
    Image.fromarray(np.hstack([p[f'tecla_{e}'] for e in ESTADOS]), 'RGBA').save(
        os.path.join(d, 'ui_ignition_menu_plate.png'))
    Image.fromarray(np.hstack([p[f'carga_{i}'] for i in range(1, PASOS + 1)]), 'RGBA').save(
        os.path.join(d, 'ui_ignition_menu_fill.png'))
    Image.fromarray(p['pictos'], 'RGBA').save(os.path.join(d, 'ui_ignition_menu_icons.png'))
    j = json_comun()
    j.update({'key': LADO, 'plateCell': PZ, 'plateBorder': BORDE, 'plateCenterMode': 'tiled',
              'plateColumns': list(ESTADOS), 'fillTile': 8, 'fillSteps': PASOS,
              'icon': PICTO, 'iconTop': PICTO_Y, 'iconRows': list(TIPOS), 'iconColumns': list(PICTOS),
              'costCapTop': COSTE_Y,
              'note': 'tecla de key x key centrada en la opcion (1 px mas arriba; pulsado, 1 px mas abajo); '
                      'pictograma centrado arriba (claro; encima y pulsado, encima; falta, gris; nodisp, '
                      'apagado) y el coste centrado debajo; encima: la trama de carga sube de abajo arriba '
                      'por dentro del marco con la orbital en 160 ms, de paso 1 a 5'})
    with open(os.path.join(d, 'ui_ignition_menu.json'), 'w', encoding='utf-8') as fh:
        json.dump(j, fh, indent=1, ensure_ascii=False)


if __name__ == '__main__':
    ejecutar(sys.modules[__name__], 1, 'Las teclas',
             colores_de('str_p', 'str_e', 'neutral') | {tuple(PANEL), tuple(FONDO)})
