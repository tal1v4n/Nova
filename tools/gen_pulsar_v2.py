"""NOVA - Generador del Pulsar, propuesta v2: EL FARO.

Ref: NOVA_Nodos.md 4.3 · NOVA_Assets_Diseno.md A.2.3
Propuesta alternativa a `gen_pulsar_v1.py`. Las dos conviven hasta elegir.

EL PROBLEMA QUE RESUELVE. La v1 y la Estrella comparten los cinco ejes de
diseno que definen la lectura de un sprite: silueta de disco con puas,
simetria radial n-fold, apendice de banda solida, composicion isotropica y
la misma primitiva llevando la voz cantante. Cambiar la paleta o el numero
de puas no separa dos nodos que estan construidos igual. Esta version voltea
los cinco.

                        Estrella / Pulsar v1      Pulsar v2
    Silueta             disco con puas            reloj de arena
    Simetria            radial, centrada          bilateral sobre un eje
    Material apendice   banda solida              luz tramada
    Composicion         isotropica                direccional, a 32 grados
    Cuerpo              esfera                    esferoide achatado
    Contador de nivel   puas radiales             frentes en el haz

LA IDEA. Un pulsar no es una bola que irradia: es un FARO. Gira tan deprisa
que se achata, y lo unico que el universo ve de el son dos conos de luz que
barren. Asi que aqui el organizador de la composicion no es el centro: es un
EJE INCLINADO. Todo cuelga de esa diagonal, y ninguna otra pieza del juego
tiene una diagonal.

MATERIA CONTRA LUZ. La Estrella resuelve todo lo que la rodea con
`corona_radial`, que es banda solida: hace silueta, lleva contorno y cuenta
para la huella de clic. El Pulsar v2 resuelve su haz con `cono_trama`, que
es trama Bayer sobre la capa `glow`: se ve, pero no es materia. Dos nodos
que fabrican sus apendices con materiales distintos dejan de parecerse
aunque compartan paleta, y eso no se consigue retocando colores.

EL NIVEL SE CUENTA EN FRENTES DE ONDA. Cada poder desbloqueado es un pulso
mas viajando por el haz: 1, 2 o 3 arcos abiertos hacia fuera, el dibujo
universal de "esto se aleja del emisor". Se cuentan, no se estiman, que es
lo que exige 4.3: el nivel del Pulsar no cambia ninguna stat, solo
desbloquea poderes. Y los frentes viajan, asi que el mismo elemento que
cuenta el nivel es el que da vida al idle.

Anatomia, en orden de dibujo:

    1. conos        dos haces de luz tramada sobre el eje, siempre dos
    2. frentes      N arcos perpendiculares al eje, N = nivel
    3. banda (atras)  anillo ecuatorial, mitad trasera
    4. cuerpo       esferoide achatado, inclinado con el eje
    5. banda (delante)

Sin cara: los nodos no tienen ojos (A.8 #5, T6-12).

IDLE: hoja de 16 frames. Los frentes viajan por el haz y el bucle cierra por
construccion (ver `_frentes`). Y el cuerpo respira con la `s` del contrato
-alto x s sobre el eje de giro, ancho / sqrt(s)-, porque el contrato da hoja
a la Estrella y al Pulsar precisamente para que la deformacion la dibuje el
generador y no un transform. La banda ecuatorial y los conos no respiran:
son la maquina, y la criatura es el cuerpo. El frame 0 es el
sprite estatico de siempre.

HUELLA. El cono es luz y no cuenta para el hitbox de clic (contrato, "Huella
del nodo"). La huella solida la ponen el cuerpo, la banda ecuatorial y los
frentes, y se comprueba sobre los 16 frames del ciclo, no solo sobre el
frame 0: los frentes se mueven, y el maximo de la huella no cae en el sprite
estatico. Se mantiene dentro del rango de 0.6 u a 1.2 u.
"""

import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from nova_core import (a_rgba, anillo_orbital, cono_trama, dir_nodo,
                       esferoide, exporta_huellas, guardar, huella_px, lienzo, rampa)

NODO = 'pulsar'
VERSION = 2

CELDA = 96
C = CELDA / 2.0
PPU = 64.0

# Inclinacion del eje de giro. Ni 0 ni 45: a 0 la composicion vuelve a ser
# ortogonal y se emparenta con la Estrella, y a 45 el haz sale por la esquina
# de la celda y se recorta. 32 grados deja el cono entero dentro de 96 px.
PHI = math.radians(-32.0)
ECU = PHI + math.pi / 2      # el ecuador es perpendicular al eje de giro

# `largo` va deliberadamente MAS ALLA del ultimo frente: el cono se apaga con
# la distancia, y si termina donde termina el ultimo arco, ese arco queda
# flotando fuera de la luz que lo emite. El tope lo pone la celda: la esquina
# del cono cae a largo*(cos PHI + SEMIANCHO*sin PHI) = largo*1.07 del centro,
# asi que por encima de 43 px el haz se recorta contra el borde de los 96.
#
# rx, ry  semiejes del cuerpo. rx es el ecuatorial: rx > ry es el achatamiento
#         por rotacion, y es lo que separa la silueta de la de una esfera
# calor   aclarado radial del nucleo
# ban     semieje mayor de la banda ecuatorial
# largo   alcance del cono de luz

# `alcance` (materia) y `largo` (luz) son dos numeros distintos a proposito.
# Los frentes son solidos y cuentan para el hitbox de clic, asi que su viaje
# termina donde termina la huella util; el cono es glow y puede seguir mas
# alla. Con un solo numero, o el haz se queda corto o la huella se pasa de
# 1.2 u. La cota es geometrica: el extremo de un frente cae a
# alcance*(cos PHI + SEMIANCHO*sin PHI) = alcance*1.073 del centro.

NIVELES = {
    1: dict(rx=14, ry=10.5, calor=2.2, ban=19, alcance=30, largo=34),
    2: dict(rx=15, ry=11.0, calor=2.7, ban=20, alcance=32, largo=38),
    3: dict(rx=16, ry=11.5, calor=3.2, ban=21, alcance=34, largo=42),
}

SEMIANCHO = 0.42     # media anchura del cono por unidad de longitud, ~23 grados
APLANADO  = 0.34     # escorzo de los frentes: b = a * APLANADO
ARRANQUE  = 0.30     # posicion del primer frente dentro de su tramo, en [0,1)

FRAMES  = 16         # los idle son de 16 (contrato, Animacion)
RESPIRA = 0.07       # la `s` del contrato


def _recorrido(cfg):
    """Tramo que recorre un frente: del borde del cuerpo al fin de su alcance."""
    d0 = cfg['ry'] + 2.0
    return d0, cfg['alcance'] - d0


def _frentes(nivel, t):
    """Distancias al centro de los N frentes en el instante `t`.

    Las posiciones NO se escriben a mano: se reparten el recorrido en N tramos
    iguales. Es lo que hace que el bucle cierre por construccion. En un ciclo
    cada frente avanza justo un tramo y ocupa el sitio del siguiente, asi que
    el conjunto en t=1 es identico al de t=0 sea cual sea el numero de frames.
    Con posiciones escritas a mano el avance y el recorrido no son
    conmensurables y el ciclo da un salto al reiniciarse.
    """
    cfg = NIVELES[nivel]
    d0, R = _recorrido(cfg)
    tramo = R / nivel
    return [d0 + ((i + ARRANQUE) * tramo + t * tramo) % R for i in range(nivel)]


def _frente(lz, d, cfg, ramp, oid):
    """Un pulso viajando por el haz: frente de onda, no anillo cerrado.

    Se dibuja solo la mitad que mira hacia fuera. Un anillo completo convierte
    la fila de pulsos en un muelle -objetos identicos equiespaciados en linea-
    y se pierde la idea de faro. Media elipse abierta hacia fuera es la forma
    universal de "esto se aleja del emisor", el mismo dibujo de un sonar.

    Se ensancha con la distancia porque viaja dentro del cono, y se adelgaza
    y se apaga porque un pulso que se aleja se diluye. Ese decaimiento es lo
    que permite contarlos aunque dos caigan cerca, que es exactamente donde
    fallan los anillos concentricos.
    """
    d0, R = _recorrido(cfg)
    t = min(max((d - d0) / R, 0.0), 1.0)
    a = max(d * SEMIANCHO, 2.5)
    # Nace grueso y claro junto al cuerpo y muere fino y oscuro en la punta.
    # Ningun frente se salta: si se dejaran de dibujar los mas lejanos, en
    # parte del ciclo se verian dos donde el nivel dice tres, y el contador
    # de nivel dejaria de ser fiable, que es lo unico que este sprite no se
    # puede permitir. Se extinguen adelgazando, no desapareciendo.
    grosor = 2.7 - 1.8 * t
    # tope 2: con sesgo 3 el frente cae a la banda de sombra de la rampa y se
    # funde con el fondo, que es lo que prohibe la regla 3 de Paleta.
    sesgo = min(int(round(3.2 * t)), 2)
    ux, uy = math.cos(PHI), math.sin(PHI)
    # 'atras' es la mitad que se aleja del centro en +PHI; en -PHI es la otra.
    for signo, mitad in ((1, 'atras'), (-1, 'delante')):
        anillo_orbital(lz, C + ux * d * signo, C + uy * d * signo,
                       a, a * APLANADO, ECU, grosor, ramp, oid=oid,
                       mitad=mitad, sesgo_banda=sesgo)


def pulsar(nivel, faccion, t=0.0):
    """Devuelve el lienzo de un Pulsar v2. `t` en [0,1) para animar el pulso."""
    cfg = NIVELES[nivel]
    suf = 'p' if faccion == 'player' else 'e'
    cuerpo = rampa(f'pul_{suf}')

    lz = lienzo(CELDA)
    fase = t
    s = 1.0 + RESPIRA * math.sin(2 * math.pi * t)
    rx, ry = cfg['rx'] / math.sqrt(s), cfg['ry'] * s

    # 1. los dos conos de luz. Arrancan cubriendo el cuerpo para que la trama
    #    no se vea pasar por encima del nucleo.
    cono_trama(lz, C, C, PHI, largo=cfg['largo'], semiancho=SEMIANCHO,
               ramp=cuerpo, r0=cfg['ry'] + 1, banda_int=2, banda_ext=4,
               gamma_eje=0.85, gamma_lat=1.8)

    # 2. frentes de onda: el contador de nivel y, a la vez, el motor del idle.
    for i, d in enumerate(_frentes(nivel, fase)):
        _frente(lz, d, cfg, cuerpo, oid=30 + i)

    # 3-5. banda ecuatorial partida por el cuerpo. Es la senal de que esto
    #      gira, y la que pone la mayor parte de la huella solida.
    anillo_orbital(lz, C, C, cfg['ban'], cfg['ban'] * 0.30, ECU, 1.5,
                   cuerpo, oid=5, mitad='atras', sesgo_banda=2)

    esferoide(lz, C, C, rx, ry, ECU, cuerpo, oid=10, calor=cfg['calor'])

    anillo_orbital(lz, C, C, cfg['ban'], cfg['ban'] * 0.30, ECU, 2.1,
                   cuerpo, oid=6, mitad='delante', sesgo_banda=0)

    return lz


def hoja(nivel, faccion):
    """Tira horizontal de FRAMES celdas: ancho = celda x n_frames (contrato)."""
    return np.hstack([a_rgba(pulsar(nivel, faccion, t=i / FRAMES))
                      for i in range(FRAMES)])


def main():
    raiz = dir_nodo(NODO, VERSION)
    for nivel in NIVELES:
        for fac in ('player', 'enemy'):
            # el estatico es el frame 0, para UI y hojas de contacto
            guardar(pulsar(nivel, fac),
                    os.path.join(raiz, f'pul_lvl{nivel}_{fac}.png'))
            Image.fromarray(hoja(nivel, fac), 'RGBA').save(
                os.path.join(raiz, f'pul_lvl{nivel}_{fac}_sheet.png'))

    print(f"{'':4} {'solida':>9} {'con luz':>10} {'ciclo':>9}")
    huellas = {}
    for nivel in NIVELES:
        lz = pulsar(nivel, 'player')
        solido = lz['lleno'].copy()
        a = a_rgba(lz)[..., 3] > 0
        if a[0, :].any() or a[-1, :].any() or a[:, 0].any() or a[:, -1].any():
            print(f'  AVISO: N{nivel} toca el borde de la celda')
        huellas[nivel] = huella_px(solido)
        # la huella se mide sobre el ciclo entero (A.2.3): los frentes viajan
        ciclo = 0
        for i in range(FRAMES):
            f = pulsar(nivel, 'player', t=i / FRAMES)
            af = a_rgba(f)[..., 3] > 0
            if af[0, :].any() or af[-1, :].any() or af[:, 0].any() or af[:, -1].any():
                print(f'  AVISO: N{nivel} f{i} toca el borde de la celda')
            ciclo = max(ciclo, huella_px(f['lleno']))
        if not np.array_equal(a_rgba(pulsar(nivel, 'player', 0.0)),
                              a_rgba(pulsar(nivel, 'player', 1.0))):
            print(f'  AVISO: N{nivel} el bucle no cierra')
        print(f'N{nivel}: {huellas[nivel] / PPU:8.2f}u {huella_px(a) / PPU:9.2f}u '
              f'{ciclo / PPU:8.2f}u')
    exporta_huellas(raiz, 'pul', huellas)


if __name__ == '__main__':
    main()
