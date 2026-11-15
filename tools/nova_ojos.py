"""NOVA - Los ojos de los nodos: ARCHIVO DE LO DESCARTADO.

Los nodos no tienen ojos desde `T6-12` (A.8 #5, cerrada el 2026-11-01), y con
ellos salio del inventario `ART-ST-SCARE`. Esto era la parte de
`nova_estados.py` que media la cara: la tabla de anclaje de los ojos sobre los
catorce nodos, las dos tallas del overlay y la vara que lo verificaba.

Se conserva SOLO para que `gen_state_scare_v1.py` y `gen_state_scare_v2.py`,
descartados, sigan corriendo (A.1: una propuesta descartada se queda con su
generador). Ningun asset vivo lo usa. Sobre los sprites de hoy, sin ojos,
`mascara_ojos` no encuentra nada y `verifica_ojos` no mide lo que medía: sus
cifras solo valen contra los sprites con cara, que viven en la historia de
git anterior a `T6-12`.
"""

import os

import numpy as np
from PIL import Image

from nova_core import RAIZ, a_rgba
from nova_estados import SPRITE


# --- Anclaje de ojos --------------------------------------------------------
#
# `Asustado` (14.2) es el unico overlay de A.3 que va SOBRE el cuerpo y sobre
# los CUATRO tipos de nodo. Eso lo convierte en un problema distinto: el `zzz`
# y la brasa solo tenian que caber en un hueco, y este tiene que ATERRIZAR
# encima de unos ojos que cada nodo coloca donde le conviene.
#
# Medido sobre los 34 sprites (los tonos exclusivos de la rampa `ojo`):
#
#     centro en x     47.0 - 47.5  en TODOS.  Practicamente registrado.
#     centro en y     41.1 - 45.5  la Estrella sube con el nivel.
#     separacion       9.5 - 16.0  factor 1.7 entre el Pulsar N1 y la
#                                  Estrella N5.
#     ojo mas grande  10 x 10 px   (Estrella N5)
#
# Un overlay de geometria fija NO puede registrar en los 34, y escalarlo esta
# prohibido: con `Filter Mode: Point` una escala no entera rompe el
# pixel-perfect (contrato, Resolucion; es la misma razon por la que el proton
# tiene tres sprites discretos y no escala continua, A.4.2).
#
# La salida es la misma que la del proton: TAMANOS DISCRETOS mas
# DESPLAZAMIENTO ENTERO. El overlay es un OJO, no un par: se instancia dos
# veces por nodo, cada una en su ancla, y asi la separacion y la deriva
# vertical se resuelven con enteros sin tocar el sprite.
#
# Y la forma no es redonda. Un ojo asustado es MAS ALTO, no mas ancho, y eso
# no es una licencia: el ancho lo raciona la separacion -dos ojos que se tocan
# se leen como uno- y la altura no la raciona nada. Con ojos redondos hacian
# falta dos tallas y en el Pulsar N1 la mayor no agrandaba nada; con elipses
# altas, dos formas cubren los catorce abriendo el ojo al menos 2 px en todos.
#
# (tipo, nivel) -> (talla, ancla izquierda, ancla derecha) desde el centro de
# celda, en pixeles enteros.
OJOS = {
    ('ast', 0): ('chico',  (-8, -2), (+6, -2)),
    ('str', 1): ('chico',  (-6, -4), (+5, -4)),
    ('str', 2): ('grande', (-7, -5), (+6, -5)),
    ('str', 3): ('grande', (-8, -6), (+6, -6)),
    ('str', 4): ('grande', (-8, -6), (+7, -6)),
    ('str', 5): ('grande', (-8, -6), (+8, -6)),
    ('pul', 1): ('chico',  (-6, -2), (+4, -2)),
    ('pul', 2): ('chico',  (-6, -2), (+4, -2)),
    ('pul', 3): ('chico',  (-6, -3), (+5, -3)),
    ('mag', 1): ('chico',  (-6, -2), (+6, -2)),
    ('mag', 2): ('grande', (-7, -3), (+6, -3)),
    ('mag', 3): ('grande', (-7, -3), (+6, -3)),
    ('mag', 4): ('grande', (-8, -2), (+6, -2)),
    ('mag', 5): ('grande', (-8, -2), (+6, -2)),
}

TALLAS = {'chico': (6, 8), 'grande': (8, 12)}   # ancho x alto, en pixeles

def _tonos_ojo():
    """Tonos que SOLO aparecen en la rampa `ojo`, para localizarla sin dudas."""
    from nova_paleta import CONTORNO, RAMPAS
    otros = set()
    for k, v in RAMPAS.items():
        if k != 'ojo':
            otros.update(x.upper() for x in v)
    otros.add(CONTORNO.upper())
    return np.array([[int(h[i:i + 2], 16) for i in (1, 3, 5)]
                     for h in RAMPAS['ojo'] if h.upper() not in otros], np.int16)


def mascara_ojos(ruta):
    """Los pixeles de ojo de un sprite de nodo."""
    a = np.array(Image.open(ruta).convert('RGBA'))
    rgb = a[..., :3].astype(np.int16)
    m = np.zeros(a.shape[:2], bool)
    for c in _tonos_ojo():
        m |= (np.abs(rgb - c).sum(2) == 0) & (a[..., 3] > 0)
    return m


def _islas(m):
    from collections import deque
    visto = np.zeros(m.shape, bool)
    n = 0
    for y, x in zip(*np.where(m)):
        if visto[y, x]:
            continue
        n += 1
        q = deque([(y, x)])
        visto[y, x] = True
        while q:
            cy, cx = q.popleft()
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = cy + dy, cx + dx
                if (0 <= ny < m.shape[0] and 0 <= nx < m.shape[1]
                        and m[ny, nx] and not visto[ny, nx]):
                    visto[ny, nx] = True
                    q.append((ny, nx))
    return n


def verifica_ojos(fn_par, frames):
    """Las dos condiciones que definen este asset, sobre los 14 nodos reales.

    `fn_par(talla, ancla_izq, ancla_der, t)` devuelve el lienzo del par de
    ojos ya colocado.

    1. TAPA. Ni un pixel del ojo viejo puede asomar por debajo del nuevo: dos
       ojos concentricos de distinto tamano se leen como cuatro.
    2. SEPARA. El par tiene que dejar DOS islas conexas. Dos ojos que se tocan
       son un visor, y un visor no tiene expresion.
    """
    fallos = 0
    for (tipo, nivel), (talla, ai, ad) in sorted(OJOS.items()):
        ruta = os.path.join(RAIZ, 'art', SPRITE[tipo].format(n=nivel))
        viejo = mascara_ojos(ruta)
        peor_asoman = peor_islas = 0
        for i in range(frames):
            m = a_rgba(fn_par(talla, ai, ad, i / frames))[..., 3] > 0
            peor_asoman = max(peor_asoman, int((viejo & ~m).sum()))
            peor_islas = max(peor_islas, abs(_islas(m) - 2))
        mal = peor_asoman or peor_islas
        fallos += bool(mal)
        marca = '  <-- FALLA' if mal else ''
        print(f'  {tipo}{nivel} {talla:>6}: asoman {peor_asoman:2} px, '
              f'islas {"2" if not peor_islas else "!=2"}{marca}')
    print(f'  nodos que fallan: {fallos} de {len(OJOS)}')
    return fallos == 0
