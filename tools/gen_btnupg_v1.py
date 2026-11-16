"""NOVA - ART-UI-BTN-UPG v1, el boton de mejora: «La tecla».

Ref: NOVA_Assets_Diseno.md A.6 (y `ART-UI-PANEL`, «La sonda») · Spec 13.3
(punto 4), 13.12 (botones, patron «Carga»), 16.2 (costes de nivel), 18 #18

Este archivo tiene ademas LO COMUN A LAS TRES PROPUESTAS -los estados, el
texto, las comprobaciones y las vistas sobre el panel elegido-;
`gen_btnupg_v2.py` y `gen_btnupg_v3.py` solo traen su dibujo.

QUE ES. El boton de la cuarta fila del panel de clic derecho, solo en un
nodo propio: `Mejorar` a la izquierda y el coste a la derecha (13.12, «con
coste»). 192 x 20 dentro de los 8 px de margen de «La sonda»; la celda mide
22 para que el pulsado baje 2 px (4 de pantalla). El texto va en
TextMeshPro con Jersey 10; las vistas lo pintan con la propia fuente.

LOS SEIS ESTADOS (13.3, 13.12, `NodeInfoPanel.ButtonLook`):

    normal      se puede mejorar
    encima      el raton esta encima (160 ms, orbital)
    pulsado     baja 2 px
    falta       no hay Energia: atenuado, coste en magenta y lo que falta,
                `40 · faltan 6`; NUNCA se oculta
    bloqueado   Congelado o Reconvirtiendo (18 #18): atenuado, sin magenta,
                porque no falta nada que el jugador pueda poner
    maximo      `NIVEL MAXIMO`: el boton desaparece y queda la lectura (13.3)

    py tools/gen_btnupg_v1.py         piezas, comprobaciones y vistas
    py tools/gen_btnupg_v1.py --gif   y la animada

----------------------------------------------------------------------------

LA TESIS DE LA v1: LA TECLA DE LA LAMINA. El boton secundario de 13.12 tal
cual -relleno `#11142A`, marco de 2 px en `str_p[6]` con la esquina en
escalera- y su patron «Carga»: al poner el raton encima, el boton SE LLENA
de izquierda a derecha con una trama cian que se densifica (en la vista,
los 160 ms en cinco pasos) y el marco se enciende en `str_p[4]`. Pulsado,
lleno y 2 px mas abajo. Sin Energia, marco gris y coste en magenta; bloqueado,
gris y con una trama diagonal: cerrado, no vacio. En el nivel maximo no hay
tecla: dos GALONES a cada lado de `NIVEL MAXIMO`, los de `ART-FX-UPG`, en gris.
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_panel_v1 as pv1
from gen_panel_v1 import (FONDO, NEU, PANEL, STR_E, STR_P, ampliar, colores_de, pega, pinta,
                          placa_lamina, rgba, texto)
from nova_core import BAYER8, RAIZ, dir_preview

ASSET = 'btn_upgrade'
BW, BH = 192, 20
CELDA_H = 22
BAJA = 2
TY = 5                          # fila de la mayuscula dentro del boton
ESTADOS = ('normal', 'encima', 'pulsado', 'falta', 'bloqueado', 'maximo')
C_TEXTO, C_GRIS, C_COSTE, C_FALTA = NEU[1], NEU[3], STR_P[3], STR_E[3]


def etiquetas(estado, coste, faltan):
    """(izquierda, derecha, color izq, color der) del boton en ese estado."""
    if estado == 'maximo':
        return 'NIVEL MÁXIMO', None, C_GRIS, None
    if estado == 'falta':
        return 'Mejorar', f'{coste} · faltan {faltan}', C_GRIS, C_FALTA
    if estado == 'bloqueado':
        return 'Mejorar', str(coste), C_GRIS, C_GRIS
    return 'Mejorar', str(coste), C_TEXTO, C_COSTE


def rotula(img, estado, coste, faltan, dy=0, color_izq=None, color_der=None, centrado=False):
    izq, der, ci, cd = etiquetas(estado, coste, faltan)
    ci = color_izq if color_izq is not None else ci
    cd = color_der if color_der is not None else cd
    if centrado:
        pinta(img, izq, 0, TY + dy, ci, 'centro', BW)
    else:
        pinta(img, izq, 6, TY + dy, ci)
    if der:
        pinta(img, der, BW - 6, TY + dy, cd, 'der')


# --- Comprobaciones --------------------------------------------------------------

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
    vistos = {}
    for e in ESTADOS:
        im = gen.boton(e, 40, 34)
        if im.shape[:2] != (CELDA_H, BW):
            fallos.append(f'{e}: mide {im.shape[1]} x {im.shape[0]}')
        vistos[e] = im.tobytes()
    if len(set(vistos.values())) < len(ESTADOS):
        fallos.append('dos estados iguales')
    # el pulsado no sube nada por encima de su fila 2
    if (gen.boton('pulsado', 40, 40)[:BAJA, :, 3] > 0).any() and not getattr(gen, 'PULSADO_LIBRE', False):
        fallos.append('el pulsado no baja')
    # el texto mas largo cabe: «Mejorar» y «50 · faltan 50»
    if 6 + texto('Mejorar').shape[1] + 8 + texto('50 · faltan 50').shape[1] + 6 > BW:
        fallos.append('el texto mas largo no cabe')
    fallos += gen.comprobar_propio() if hasattr(gen, 'comprobar_propio') else []
    if fallos:
        raise SystemExit(f'ART-UI-BTN-UPG «{nombre}» no pasa:\n  ' + '\n  '.join(sorted(set(fallos))))
    return len(usados)


# --- Vistas: sobre el panel elegido -------------------------------------------------

def caso_estrella(nivel=3, energia=34):
    c = dict(pv1.CASOS['estrella'])
    coste = {1: 20, 2: 30, 3: 40, 4: 50}.get(nivel)
    c['nivel'], c['energia'] = nivel, energia
    c['bloques'] = [
        ('info', [('titulo', f'Estrella · Nivel {nivel}'), ('dato', f'Energía {energia} / 50'),
                  ('dato', 'Generación: 0.77 E/s')]),
        ('mejora', [('vacia',)]),       # el boton se pega encima
        ('reconv', [('rotulo', 'Reconvertir'), ('par', [('Púlsar', 20, 0), ('Magnetar', 20, 0)])]),
    ]
    return c, coste


def en_panel(gen, estado, f=0, t=1.0):
    """El panel de «La sonda» con el boton en su fila (la cuarta)."""
    import gen_panel_v2 as p2
    nivel, energia = (5, 50) if estado == 'maximo' else (3, 34 if estado == 'falta' else 45)
    caso, coste = caso_estrella(nivel, energia)
    p = p2.panel(caso, f=f)
    b = gen.boton(estado, coste or 0, energia, f=f, t=t)
    pega(p, b, pv1.PAD, pv1.PAD + 3 * pv1.FILA + 1)
    return p


def estados_vista(gen):
    filas = []
    for e in ESTADOS:
        c = np.empty((CELDA_H + 6, BW + 12, 3), np.uint8)
        import gen_panel_v2 as p2
        tr = np.tile(p2.trama(), (c.shape[0] // 8 + 1, c.shape[1] // 8 + 1, 1))[:c.shape[0], :c.shape[1]]
        c[:] = FONDO
        m = tr[..., 3] > 0
        c[m] = tr[..., :3][m]
        nivel, energia = (5, 50) if e == 'maximo' else (3, 34 if e == 'falta' else 45)
        pega(c, gen.boton(e, 40, energia), 6, 3)
        filas.append(c)
    return np.vstack(filas)


def vistas(gen, version, gif=False):
    d = os.path.join(dir_preview('ui_' + ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(estados_vista(gen), 4)).save(os.path.join(d, 'estados_x4.png'))
    ps = [en_panel(gen, e) for e in ('normal', 'encima', 'falta', 'maximo')]
    h = max(p.shape[0] for p in ps)
    tira = np.empty((h + 8, sum(p.shape[1] + 8 for p in ps) + 8, 3), np.uint8)
    tira[:] = FONDO
    x = 8
    for p in ps:
        pega(tira, p, x, 4)
        x += p.shape[1] + 8
    Image.fromarray(ampliar(tira, 2)).save(os.path.join(d, 'en_panel_x2.png'))
    if gif:
        # encima -> pulsado -> suelta, a 50 fps
        cuadros = []
        for i in range(10):
            cuadros.append(('normal', 0, 1.0))
        for i in range(9):
            cuadros.append(('encima', i, i * 0.02 / 0.16))
        for i in range(20):
            cuadros.append(('encima', 9 + i, 1.0))
        for i in range(8):
            cuadros.append(('pulsado', 29 + i, 1.0))
        for i in range(20):
            cuadros.append(('encima', 37 + i, 1.0))
        ims = []
        for e, f, t in cuadros:
            c = np.empty((CELDA_H + 8, BW + 16, 3), np.uint8)
            c[:] = FONDO
            pega(c, gen.boton(e, 40, 45, f=f // 4, t=min(1.0, t)), 8, 4)
            ims.append(Image.fromarray(ampliar(c, 4)))
        ims[0].save(os.path.join(d, 'pasar_y_pulsar_x4.gif'), save_all=True,
                    append_images=ims[1:], duration=20, loop=0)
    return d


def comparativa():
    filas = []
    for v in (1, 2, 3):
        if not os.path.isdir(os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{v}')):
            continue
        mod = sys.modules[__name__] if v == 1 else __import__(f'gen_btnupg_v{v}')
        filas.append(estados_vista(mod))
        filas.append(np.zeros((filas[-1].shape[0], 4, 3), np.uint8))
    if filas:
        Image.fromarray(ampliar(np.hstack(filas[:-1]), 3)).save(
            os.path.join(dir_preview('ui_' + ASSET), 'comparativa_x3.png'))


def ejecutar(gen, version, nombre, permitidos):
    colores = comprobar(gen, permitidos, nombre)
    d = os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)
    gen.exportar(d)
    vistas(gen, version, '--gif' in sys.argv)
    comparativa()
    print(f'v{version} «{nombre}»: {colores} colores  -> {os.path.relpath(d, RAIZ)}')


# =============================================================================
# v1 «La tecla»
# =============================================================================

PZ, BORDE = 20, 6
PASOS = 5                       # la carga del encima, en la hoja


def tecla(estado):
    marco = {'normal': STR_P[6], 'encima': STR_P[4], 'pulsado': STR_P[4],
             'falta': NEU[5], 'bloqueado': NEU[5]}[estado]
    p = placa_lamina(PZ, PZ, marco)
    if estado == 'bloqueado':
        dentro = pv1.erosion(pv1.escalera(PZ, PZ))
        for y in range(PZ):
            for x in range(PZ):
                if dentro[y, x] and (x + y) % 4 == 0:
                    p[y, x, :3] = NEU[5]
    return p


def carga(paso):
    """Tesela de 8 x 8 de la trama cian del patron «Carga», paso 1..PASOS."""
    t = rgba(8, 8)
    umbral = paso / (PASOS + 1)
    for y in range(8):
        for x in range(8):
            if BAYER8[y, x] < umbral * 0.55:
                t[y, x, :3] = STR_P[5] if BAYER8[y, x] > umbral * 0.25 else STR_P[4]
                t[y, x, 3] = 255
    return t


def galon():
    """Un galon de 7 x 7: dos chevrones apilados que apuntan arriba, en gris."""
    g = rgba(7, 7)
    for y0 in (0, 3):
        for x in range(7):
            y = y0 + abs(x - 3) // 1 - (abs(x - 3) // 2)   # 0,1,1,2 desde el centro: chevron bajo
            g[y, x, :3] = NEU[3]
            g[y, x, 3] = 255
    return g


def boton(estado, coste, energia, f=0, t=1.0):
    img = rgba(CELDA_H, BW)
    if estado == 'maximo':
        rotula(img, 'maximo', coste, 0, centrado=True)
        w = texto('NIVEL MÁXIMO').shape[1]
        g = galon()
        x0 = (BW - w) // 2
        pega(img, g, x0 - 14, TY + 1)
        pega(img, g, x0 + w + 7, TY + 1)
        return img
    dy = BAJA if estado == 'pulsado' else 0
    cuerpo = pv1.nueve(tecla(estado), BW, BH, BORDE)
    if estado in ('encima', 'pulsado'):
        paso = PASOS if estado == 'pulsado' else max(1, int(round(t * PASOS)))
        lleno = int(round((BW - 4) * (1 if estado == 'pulsado' else pv1_orbital(t))))
        tr = np.tile(carga(paso), (BH // 8 + 1, BW // 8 + 1, 1))[:BH, :BW]
        dentro = pv1.erosion(pv1.escalera(BW, BH))
        m = dentro & (tr[..., 3] > 0)
        m[:, 2 + lleno:] = False
        cuerpo[m] = tr[m]
    pega(img, cuerpo, 0, dy)
    falta = max(0, coste - energia)
    lleno = estado in ('encima', 'pulsado')
    rotula(img, estado, coste, falta, dy, color_izq=STR_P[0] if lleno else None,
           color_der=STR_P[1] if lleno else None)   # sobre la trama cian, el cian claro
    return img


def pv1_orbital(t):
    import gen_energy_v2 as e2
    return e2.orbital(t)


def piezas():
    d = {f'tecla_{e}': tecla(e) for e in ('normal', 'encima', 'pulsado', 'falta', 'bloqueado')}
    for p in range(1, PASOS + 1):
        d[f'carga_{p}'] = carga(p)
    d['galon'] = galon()
    return d


def comprobar_propio():
    fallos = []
    if len({carga(p).tobytes() for p in range(1, PASOS + 1)}) < PASOS:
        fallos.append('dos pasos de la carga iguales')
    return fallos


def exportar(d):
    p = piezas()
    Image.fromarray(np.hstack([p[f'tecla_{e}'] for e in ('normal', 'encima', 'pulsado', 'falta', 'bloqueado')]),
                    'RGBA').save(os.path.join(d, 'ui_btn_upgrade_plate.png'))
    Image.fromarray(np.hstack([p[f'carga_{i}'] for i in range(1, PASOS + 1)]), 'RGBA').save(
        os.path.join(d, 'ui_btn_upgrade_fill.png'))
    Image.fromarray(p['galon'], 'RGBA').save(os.path.join(d, 'ui_btn_upgrade_chevron.png'))
    hexa = lambda c: '#%02X%02X%02X' % tuple(int(v) for v in c)
    with open(os.path.join(d, 'ui_btn_upgrade.json'), 'w') as fh:
        json.dump({'width': BW, 'height': BH, 'cellHeight': CELDA_H, 'pressedDrop': BAJA, 'capTop': TY,
                   'padX': 6, 'plateCell': PZ, 'plateBorder': BORDE, 'plateCenterMode': 'tiled',
                   'plateColumns': ['normal', 'hover', 'pressed', 'missing', 'blocked'],
                   'fillTile': 8, 'fillSteps': PASOS, 'hoverMs': 160, 'hoverCurve': [0.2, 0.8, 0.2, 1.0],
                   'chevronGap': 7,
                   'text': {'normal': hexa(C_TEXTO), 'hover': hexa(STR_P[0]), 'hoverCost': hexa(STR_P[1]), 'muted': hexa(C_GRIS),
                            'cost': hexa(C_COSTE), 'missing': hexa(C_FALTA)},
                   'note': 'encima: la trama de carga entra de izquierda a derecha por dentro del marco con la '
                           'orbital en 160 ms y se densifica de paso 1 a 5; pulsado: lleno en paso 5 y 2 px abajo; '
                           'falta: coste en magenta como «40 · faltan 6»; bloqueado: todo gris; maximo: sin placa, '
                           'NIVEL MAXIMO centrado con un galon a 7 px de cada lado'}, fh, indent=1)


if __name__ == '__main__':
    ejecutar(sys.modules[__name__], 1, 'La tecla',
             colores_de('str_p', 'str_e', 'neutral') | {tuple(PANEL), tuple(FONDO)})
