"""NOVA - ART-UI-BTN-CONV v1, el boton de reconversion: «Las teclas».

Ref: NOVA_Assets_Diseno.md A.6 (y `ART-UI-PANEL`, «La sonda») · Spec 8.1,
8.2 (costes), 13.3 (punto 5), 13.12 (botones, patron «Carga»), 18 #29, #33

Este archivo tiene ademas LO COMUN A LAS TRES PROPUESTAS -los estados, el
texto, las comprobaciones y las vistas sobre el panel elegido-;
`gen_btnconv_v2.py` y `gen_btnconv_v3.py` solo traen su dibujo.

QUE ES. Los dos botones de la sexta fila del panel de clic derecho, solo en
un nodo propio: uno por destino, en el orden de 16.2, con el tipo a la
izquierda y el coste a la derecha (13.12, «con coste»). Cada uno mide
94 x 20, separados 4 px, dentro de los 8 px de margen de «La sonda»: el
mismo sitio que hoy `InfoPanelState.ConversionOffsets`. La celda mide 22
para que el pulsado baje 2 px (4 de pantalla). El texto va en TextMeshPro
con Jersey 10; las vistas lo pintan con la propia fuente.

EL ANCHO MANDA. «Magnetar» mide 63 px y «30», 16: con 6 de margen a cada
lado y 3 de aire, 94 justos. «30 · faltan 12» (81) NO CABE. Lo que falta
(13.12) no puede ir en texto: cada propuesta lo dice con el dibujo, y el
coste va en magenta, como hoy.

LOS SEIS ESTADOS (13.3, 18 #29, #33, `NodeInfoPanel.ButtonLook`):

    normal      se puede reconvertir
    encima      el raton esta encima (160 ms, orbital)
    pulsado     baja 2 px
    falta       no hay Energia (`MissingEnergy`): atenuado, coste en magenta
                y lo que falta dibujado; NUNCA se oculta
    bloqueado   Congelado, Infectado, cargando o con un poder cargado (8.1,
                `Disabled`): atenuado y SIN magenta, porque no falta nada
                que el jugador pueda poner
    nodisp      el tipo no existe en la partida (`Unavailable`, 18 #29):
                gris y sin coste

Reconvirtiendo no es un estado del boton: los dos dejan sitio a
«Reconvirtiendo a ...» en la fila del rotulo (18 #33).

    py tools/gen_btnconv_v1.py         piezas, comprobaciones y vistas
    py tools/gen_btnconv_v1.py --gif   y la animada

----------------------------------------------------------------------------

LA TESIS DE LA v1: LA TECLA DE LA LAMINA. El boton secundario de 13.12 tal
cual -relleno `#11142A`, marco de 2 px en `str_p[6]` con la esquina en
escalera- con su patron «Carga»: encima, el boton SE LLENA de izquierda a
derecha con una trama cian (160 ms, orbital) y el marco se enciende en
`str_p[4]`. Pulsado, lleno y mas denso, 2 px mas abajo. Sin Energia, marco
gris, coste en magenta y, bajo el coste, UNA PASTILLA magenta POR CADA 5 de
Energia que falten: se cuentan de un vistazo y no piden texto. Bloqueado,
gris con una trama diagonal: cerrado, no vacio. No disponible, el marco en
trazos: el boton no existe todavia.
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import gen_panel_v1 as pv1
from gen_panel_v1 import (FILA, FONDO, NEU, PAD, PANEL, STR_E, STR_P, ampliar, ancho, colores_de,
                          pega, pinta, placa_lamina, rgba, texto)
from nova_core import BAYER8, RAIZ, dir_preview

ASSET = 'btn_convert'
BW, BH = 94, 20
CELDA_H = 22
BAJA = 2
TY = 5                          # fila de la mayuscula dentro del boton
PADX = 6
HUECO = 4                       # entre los dos botones
ESTADOS = ('normal', 'encima', 'pulsado', 'falta', 'bloqueado', 'nodisp')
NOMBRE = {'estrella': 'Estrella', 'pulsar': 'Púlsar', 'magnetar': 'Magnetar'}
COSTE = {'estrella': 20, 'pulsar': 20, 'magnetar': 30}      # 8.2, desde
DESTINOS = {'estrella': ('pulsar', 'magnetar'), 'pulsar': ('estrella', 'magnetar'),
            'magnetar': ('estrella', 'pulsar')}            # orden de 16.2
C_TEXTO, C_GRIS, C_APAGADO, C_COSTE, C_FALTA = NEU[1], NEU[3], NEU[4], STR_P[3], STR_E[3]
OSCURO = PANEL                  # texto del boton primario (13.12)


def colores(estado):
    """(color del tipo, color del coste) en ese estado; None = sin coste."""
    return {'normal': (C_TEXTO, C_COSTE), 'encima': (STR_P[0], STR_P[1]),
            'pulsado': (STR_P[0], STR_P[1]), 'falta': (C_GRIS, C_FALTA),
            'bloqueado': (C_GRIS, C_GRIS), 'nodisp': (C_APAGADO, None)}[estado]


def rotula(img, estado, destino, coste, dy=0, ci=None, cd=None, ty=TY, oscuro=None):
    """El tipo a la izquierda y el coste a la derecha. `oscuro`, una mascara
    del boton: donde es True el texto va en el oscuro del primario."""
    c1, c2 = colores(estado)
    c1 = ci if ci is not None else c1
    c2 = cd if cd is not None else c2
    capa = rgba(*img.shape[:2])
    pinta(capa, NOMBRE[destino], PADX, ty + dy, c1)
    if c2 is not None:
        pinta(capa, str(coste), BW - PADX, ty + dy, c2, 'der')
    m = capa[..., 3] > 0
    if oscuro is not None:
        o = np.zeros_like(m)
        o[dy:dy + oscuro.shape[0]] = oscuro[:img.shape[0] - dy]
        capa[m & o, :3] = OSCURO
    img[m] = capa[m]


def faltan(coste, energia):
    return max(0, coste - energia)


def dentro_de(w=BW, h=BH):
    return pv1.erosion(pv1.escalera(w, h))


def trazos(sil):
    """El marco en trazos de 2 cada 4, siguiendo el contorno."""
    h, w = sil.shape
    m = np.zeros_like(sil)
    for y in range(h):
        for x in range(w):
            m[y, x] = sil[y, x] and ((x if y in (0, 1, h - 2, h - 1) else y) % 4) < 2
    return m


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
        im = gen.boton(e, 'magnetar', 30, 18 if e == 'falta' else 45)
        if im.shape[:2] != (CELDA_H, BW):
            fallos.append(f'{e}: mide {im.shape[1]} x {im.shape[0]}')
        if not set(np.unique(im[..., 3])) <= {0, 255}:
            fallos.append(f'{e}: alfa no binario')
        fuera = {tuple(int(v) for v in c) for c in im[..., :3][im[..., 3] > 0]} - permitidos
        if fuera:
            fallos.append(f'{e}: colores fuera de paleta')
        vistos[e] = im.tobytes()
    if len(set(vistos.values())) < len(ESTADOS):
        fallos.append('dos estados iguales')
    if (gen.boton('pulsado', 'magnetar', 30, 45)[:BAJA, :, 3] > 0).any():
        fallos.append('el pulsado no baja')
    # el texto mas largo cabe: «Magnetar» y «30»
    if PADX + ancho('Magnetar') + 3 + ancho('30') + PADX > BW:
        fallos.append('el texto mas largo no cabe')
    # dos botones y su hueco caben en la fila de «La sonda»
    if 2 * BW + HUECO != pv1.W - 2 * PAD:
        fallos.append('los dos botones no llenan la fila')
    # lo que falta se ve, y crece cuando falta mas
    def magenta(im):
        return sum(int((im[..., :3] == c).all(-1).sum()) for c in STR_E)
    poco = magenta(gen.boton('falta', 'magnetar', 30, 25))
    mucho = magenta(gen.boton('falta', 'magnetar', 30, 5))
    if not mucho > poco:
        fallos.append('lo que falta no crece')
    # bloqueado y no disponible no dicen «te falta Energia» (18 #33)
    for e in ('bloqueado', 'nodisp'):
        if magenta(gen.boton(e, 'magnetar', 30, 45)):
            fallos.append(f'{e} lleva magenta')
    fallos += gen.comprobar_propio() if hasattr(gen, 'comprobar_propio') else []
    if fallos:
        raise SystemExit(f'ART-UI-BTN-CONV «{nombre}» no pasa:\n  ' + '\n  '.join(sorted(set(fallos))))
    return len(usados)


# --- Vistas: sobre el panel elegido -------------------------------------------------

# Cada caso: el nodo, sus filas de texto y el estado de cada boton.
CASOS = {
    'estrella': dict(fac='player', tipo='estrella', nivel=3, energia=45, tope=50,
                     info=['Estrella · Nivel 3', 'Energía 45 / 50', 'Generación: 0.77 E/s'],
                     mejora='Mejora: 40', estados=('normal', 'encima')),
    'magnetar': dict(fac='player', tipo='magnetar', nivel=2, energia=22, tope=100,
                     info=['Magnetar · Nivel 2', 'Energía 22 / 100', 'Cadencia: 1.9 disparos/s'],
                     mejora='Mejora: 30', estados=('falta', 'falta')),
    'pulsar': dict(fac='player', tipo='pulsar', nivel=3, energia=62, tope=100,
                   info=['Púlsar · Nivel 3', 'Energía 62 / 100', 'Carga: 3.75 E/s'],
                   mejora='NIVEL MÁXIMO', estados=('bloqueado', 'bloqueado')),
    'sin_pulsar': dict(fac='player', tipo='estrella', nivel=2, energia=31, tope=50,
                       info=['Estrella · Nivel 2', 'Energía 31 / 50', 'Generación: 0.63 E/s'],
                       mejora='Mejora: 30', estados=('nodisp', 'pulsado')),
}


def caso_panel(clave):
    c = CASOS[clave]
    mejora = ('aviso', c['mejora']) if c['mejora'].isupper() else ('dato', c['mejora'])
    bloques = [('info', [('titulo', c['info'][0])] + [('dato', s) for s in c['info'][1:]]),
               ('mejora', [mejora]),
               ('reconv', [('rotulo', 'Reconvertir'), ('vacia',)])]
    if c['tipo'] == 'pulsar':
        bloques.append(('poderes', [('rotulo', 'Agujero de Gusano · 45%'),
                                    ('ranuras', [('Cero', 'libre'), ('Decai.', 'libre'), ('Gusano', 'carga')]),
                                    ('cancel', 'Cancelar · +13')]))
    return dict(fac=c['fac'], tipo=c['tipo'], nivel=c['nivel'], energia=c['energia'], tope=c['tope'],
                bloques=bloques, clave=clave)


def par(gen, desde, energia, estados, f=0, t=1.0):
    """Los dos botones con su hueco, 192 x 22."""
    img = rgba(CELDA_H, 2 * BW + HUECO)
    for i, (dest, e) in enumerate(zip(DESTINOS[desde], estados)):
        pega(img, gen.boton(e, dest, COSTE[desde], energia, f=f, t=t), i * (BW + HUECO), 0)
    return img


class EnPanel:
    """Hace pasar el panel de «La sonda» con los botones por un `gen` de
    gen_panel_v1, para usar su `abrir` y su tablero."""

    def __init__(self, gen):
        import gen_panel_v2 as p2
        self.gen, self.p2 = gen, p2
        self.SALIENTE = getattr(p2, 'SALIENTE', 0)

    def panel(self, caso, lado='izq', f=0):
        p = self.p2.panel(caso, lado, f)
        c = CASOS[caso['clave']]
        pega(p, par(self.gen, c['tipo'], c['energia'], c['estados'], f), PAD, PAD + 5 * FILA + 2)
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


def estados_vista(gen):
    filas = []
    for e in ESTADOS:
        c = fondo_trama(CELDA_H + 6, 2 * BW + HUECO + 12)
        pega(c, par(gen, 'estrella', 12 if e == 'falta' else 45, (e, e)), 6, 3)
        filas.append(c)
    # y lo que falta, de menos a mas: un Magnetar con 25, 15 y 5
    for en in (25, 15, 5):
        c = fondo_trama(CELDA_H + 6, 2 * BW + HUECO + 12)
        pega(c, par(gen, 'magnetar', en, ('falta', 'falta')), 6, 3)
        filas.append(c)
    return np.vstack(filas)


def galeria(gen):
    sh = EnPanel(gen)
    bg = pv1.fondo(1)
    celdas = []
    for i, clave in enumerate(('estrella', 'magnetar', 'pulsar', 'sin_pulsar')):
        alto = 330
        c = bg[0:alto, 0:330].copy() if i % 2 == 0 else bg[540 - alto:540, 330:660].copy()
        pv1.abrir(c, sh, caso_panel(clave), 56, alto // 2)
        celdas.append(c)
    h = max(c.shape[0] for c in celdas)
    out = []
    for c in celdas:
        if c.shape[0] < h:
            c = np.vstack([c, np.zeros((h - c.shape[0], c.shape[1], 3), np.uint8)])
        out += [c, np.zeros((h, 3, 3), np.uint8)]
    return np.hstack(out[:-1])


def vistas(gen, version, gif=False):
    d = os.path.join(dir_preview('ui_' + ASSET), f'v{version}')
    os.makedirs(d, exist_ok=True)
    Image.fromarray(ampliar(estados_vista(gen), 4)).save(os.path.join(d, 'estados_x4.png'))
    Image.fromarray(ampliar(galeria(gen), 2)).save(os.path.join(d, 'en_panel_x2.png'))
    if gif:
        # el cursor entra en el primero, pulsa, sale y entra en el segundo, a 50 fps
        guion = ([('normal', 'normal', 1.0)] * 10 + [('encima', 'normal', i / 8) for i in range(9)] +
                 [('encima', 'normal', 1.0)] * 16 + [('pulsado', 'normal', 1.0)] * 8 +
                 [('encima', 'normal', 1.0)] * 10 + [('normal', 'encima', i / 8) for i in range(9)] +
                 [('normal', 'encima', 1.0)] * 20)
        ims = []
        for k, (a, b, t) in enumerate(guion):
            c = fondo_trama(CELDA_H + 8, 2 * BW + HUECO + 16)
            img = rgba(CELDA_H, 2 * BW + HUECO)
            for i, (dest, e) in enumerate(zip(DESTINOS['estrella'], (a, b))):
                tt = min(1.0, t) if e == 'encima' else 1.0
                pega(img, gen.boton(e, dest, 20, 45, f=k // 4, t=tt), i * (BW + HUECO), 0)
            pega(c, img, 8, 4)
            ims.append(Image.fromarray(ampliar(c, 4)))
        ims[0].save(os.path.join(d, 'pasar_y_pulsar_x4.gif'), save_all=True,
                    append_images=ims[1:], duration=20, loop=0)
    return d


def comparativa():
    filas = []
    for v in (1, 2, 3):
        if not os.path.isdir(os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{v}')):
            continue
        mod = sys.modules[__name__] if v == 1 else __import__(f'gen_btnconv_v{v}')
        filas.append(estados_vista(mod))
        filas.append(np.zeros((filas[-1].shape[0], 4, 3), np.uint8))
    if filas:
        Image.fromarray(ampliar(np.hstack(filas[:-1]), 3)).save(
            os.path.join(dir_preview('ui_' + ASSET), 'comparativa_x3.png'))


def ejecutar(gen, version, nombre, permitidos):
    colores_n = comprobar(gen, permitidos, nombre)
    d = os.path.join(RAIZ, 'art', 'ui', ASSET, f'v{version}')
    os.makedirs(d, exist_ok=True)
    gen.exportar(d)
    vistas(gen, version, '--gif' in sys.argv)
    comparativa()
    print(f'v{version} «{nombre}»: {colores_n} colores  -> {os.path.relpath(d, RAIZ)}')


def hexa(c):
    return '#%02X%02X%02X' % tuple(int(v) for v in c)


def json_comun():
    return {'width': BW, 'height': BH, 'cellHeight': CELDA_H, 'pressedDrop': BAJA, 'capTop': TY,
            'padX': PADX, 'gap': HUECO, 'hoverMs': 160, 'hoverCurve': [0.2, 0.8, 0.2, 1.0],
            'text': {'normal': hexa(C_TEXTO), 'cost': hexa(C_COSTE), 'hover': hexa(STR_P[0]),
                     'hoverCost': hexa(STR_P[1]), 'muted': hexa(C_GRIS), 'missing': hexa(C_FALTA),
                     'unavailable': hexa(C_APAGADO)},
            'states': list(ESTADOS)}


# =============================================================================
# v1 «Las teclas»
# =============================================================================

PZ, BORDE = 20, 6
PASOS = 5                       # la carga del encima, en la hoja
PASTILLA = 5                    # una pastilla por cada 5 de Energia que falten
PY = 16                         # filas 16-17, sobre el marco de abajo


def tecla(estado):
    marco = {'normal': STR_P[6], 'encima': STR_P[4], 'pulsado': STR_P[4],
             'falta': NEU[5], 'bloqueado': NEU[5], 'nodisp': NEU[5]}[estado]
    p = placa_lamina(PZ, PZ, marco)
    sil = pv1.escalera(PZ, PZ)
    dentro = dentro_de(PZ, PZ)
    if estado == 'bloqueado':
        for y in range(PZ):
            for x in range(PZ):
                if dentro[y, x] and (x + y) % 4 == 0:
                    p[y, x, :3] = NEU[5]
    if estado == 'nodisp':
        borde = sil & ~dentro
        hueco = borde & ~trazos(sil)
        p[hueco, :3] = PANEL
    return p


def carga(paso):
    """Tesela de 8 x 8 de la trama cian del patron «Carga», paso 1..PASOS."""
    t = rgba(8, 8)
    umbral = paso / (PASOS + 1) * 0.6
    for y in range(8):
        for x in range(8):
            if BAYER8[y, x] < umbral:
                t[y, x, :3] = STR_P[5] if BAYER8[y, x] > umbral / 2 else STR_P[4]
                t[y, x, 3] = 255
    return t


def pastilla():
    p = rgba(2, 2)
    p[..., :3] = STR_E[4]
    p[..., 3] = 255
    return p


def boton(estado, destino, coste, energia, f=0, t=1.0):
    import gen_energy_v2 as e2
    img = rgba(CELDA_H, BW)
    dy = BAJA if estado == 'pulsado' else 0
    cuerpo = pv1.nueve(tecla(estado), BW, BH, BORDE)
    if estado in ('encima', 'pulsado'):
        paso = PASOS if estado == 'pulsado' else max(1, int(round(t * PASOS)))
        lleno = int(round((BW - 4) * (1 if estado == 'pulsado' else e2.orbital(t))))
        tr = np.tile(carga(paso), (BH // 8 + 1, BW // 8 + 1, 1))[:BH, :BW]
        m = dentro_de() & (tr[..., 3] > 0)
        m[:, 2 + lleno:] = False
        cuerpo[m] = tr[m]
    if estado == 'falta':
        n = -(-faltan(coste, energia) // PASTILLA)
        for i in range(n):
            pega(cuerpo, pastilla(), BW - PADX - 2 - 3 * i, PY)
    pega(img, cuerpo, 0, dy)
    rotula(img, estado, destino, coste, dy)
    return img


def piezas():
    d = {f'tecla_{e}': tecla(e) for e in ESTADOS}
    for p in range(1, PASOS + 1):
        d[f'carga_{p}'] = carga(p)
    d['pastilla'] = pastilla()
    return d


def comprobar_propio():
    fallos = []
    if len({carga(p).tobytes() for p in range(1, PASOS + 1)}) < PASOS:
        fallos.append('dos pasos de la carga iguales')
    # la tira mas larga de pastillas (falta todo el coste del Magnetar) no
    # pisa el coste
    n = -(-30 // PASTILLA)
    if BW - PADX - 2 - 3 * (n - 1) < BW - PADX - ancho('30') - 6:
        fallos.append('las pastillas se salen de debajo del coste')
    return fallos


def exportar(d):
    p = piezas()
    Image.fromarray(np.hstack([p[f'tecla_{e}'] for e in ESTADOS]), 'RGBA').save(
        os.path.join(d, 'ui_btn_convert_plate.png'))
    Image.fromarray(np.hstack([p[f'carga_{i}'] for i in range(1, PASOS + 1)]), 'RGBA').save(
        os.path.join(d, 'ui_btn_convert_fill.png'))
    Image.fromarray(p['pastilla'], 'RGBA').save(os.path.join(d, 'ui_btn_convert_pip.png'))
    j = json_comun()
    j.update({'plateCell': PZ, 'plateBorder': BORDE, 'plateCenterMode': 'tiled',
              'plateColumns': list(ESTADOS), 'fillTile': 8, 'fillSteps': PASOS,
              'pip': {'energyPerPip': PASTILLA, 'y': PY, 'rightX': BW - PADX - 2, 'step': 3},
              'note': 'encima: la trama de carga entra de izquierda a derecha por dentro del marco con la '
                      'orbital en 160 ms y se densifica de paso 1 a 5; pulsado: lleno en paso 5 y 2 px abajo; '
                      'falta: el coste en magenta y ceil(faltan / 5) pastillas de 2 x 2 hacia la izquierda '
                      'desde rightX, cada step px; bloqueado: gris con trama diagonal; nodisp: marco en '
                      'trazos y solo el tipo, sin coste'})
    with open(os.path.join(d, 'ui_btn_convert.json'), 'w', encoding='utf-8') as fh:
        json.dump(j, fh, indent=1, ensure_ascii=False)


if __name__ == '__main__':
    ejecutar(sys.modules[__name__], 1, 'Las teclas',
             colores_de('str_p', 'str_e', 'neutral') | {tuple(PANEL), tuple(FONDO)})
