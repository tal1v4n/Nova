"""NOVA - Overlay de estado `Capturado` (ART-ST-CAP), v2: EL RELEVO.

Ref: NOVA_Estados_Animaciones.md 14.1, 14.5 y 14.7 - NOVA_Assets_Diseno.md A.3

LA TESIS: LO QUE PASA NO ES UNA EXPLOSION, ES UN CAMBIO DE MANOS. 14.1 pide
«flash blanco, onda expansiva», y una onda expansiva dice «aqui ha estallado
algo». Lo que ha pasado de verdad es que el nodo ha cambiado de dueno
(`ChangeFaction()`), y eso tiene dos partes: uno lo suelta y otro lo coge.

Asi que hay DOS anillos y van en sentidos contrarios: uno se cierra desde el
borde de la celda hacia el nodo -lo que llega- y otro sale del nodo hacia
fuera -lo que se va-. Se cruzan a mitad del efecto, en el frame 2 de 5, que
es exactamente el centro de los 0.4 s. El cruce es el relevo.

SIN NUCLEO, Y ES LO QUE LA HACE BARATA. La v1 pone un fogonazo que llena el
nodo y le tapa la cara dos frames de cinco. Aqui no hay relleno: dos anillos
finos no tapan casi nada, y lo que se ve durante todo el efecto es el nodo
con su paleta nueva, que es la informacion. Se puede permitir no tapar nada
por una razon medida:

    La silueta es IDENTICA byte a byte entre facciones en los catorce nodos.
    Es 14.0 #3 comprobada contra los 34 PNG. `ChangeFaction()` es un
    recoloreado en el sitio: no hay corte de geometria que esconder, al
    reves que en `ART-ST-CONV`, que existe justamente para esconder uno.

Este asset no tiene que tapar un empalme: tiene que SUBRAYAR un suceso.

EL BLANCO ES SUYO POR ASIGNACION, no por gusto: al cerrar `ART-ST-CONV`
(A.3.6) se decidio que la reconversion destellara en color de faccion para
dejarle el blanco a este, porque 8.1 permite que capturen un nodo mientras
se reconvierte y los dos eventos no pueden empezar igual.

ARRANCA EN SU PICO. Un efecto que se dispara no se prepara: el frame 0 es el
mas brillante y el sprite cambia de paleta en ese mismo frame. Lo que si
cumple es salir a nada, porque despues viene `Normal`.

LO QUE ESTA PROPUESTA PAGA. Dos anillos en cinco frames son cuatro
posiciones por anillo, y a esa velocidad el ojo puede leerlos como uno solo
rebotando en vez de como dos cruzandose. Es el riesgo de contar una historia
de dos partes en 0.4 s.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import a_rgba, dir_estado, lienzo, rampa, tramar
from nova_estados import CAPTURABLES, verifica_tinte

ESTADO = 'cap'
VERSION = 2
FRAMES = 5
CELDA = 96
C = CELDA / 2.0

# el que llega: se cierra desde fuera
DENTRO_R = (44.0, 33.0, 23.0, 14.0, 0.0)
DENTRO_G = (1.8, 2.2, 2.8, 3.4, 0.0)
DENTRO_B = (1, 0, 0, 1, 6)

# el que se va: sale del nodo
FUERA_R = (7.0, 17.0, 26.0, 34.0, 0.0)
FUERA_G = (3.4, 2.8, 2.1, 1.3, 0.0)
FUERA_B = (0, 0, 1, 2, 6)


def _aro(lz, rad, r, g, ramp, banda):
    if r <= 0.0 or g <= 0.0:
        return
    d = np.clip(1.0 - np.abs(rad - r) / g, 0.0, 1.0) ** 0.7
    tramar(lz, d, ramp, banda_int=banda, banda_ext=banda + 2, corte=0.42)


def cap(t=0.0):
    lz = lienzo(CELDA)
    ramp = rampa('neutral')     # el indice 0 es #FFFFFF en todas las rampas
    y, x = np.mgrid[0:CELDA, 0:CELDA]
    rad = np.hypot(x + 0.5 - C, y + 0.5 - C)
    i = min(int(round(t * FRAMES)), FRAMES - 1)
    _aro(lz, rad, DENTRO_R[i], DENTRO_G[i], ramp, DENTRO_B[i])
    _aro(lz, rad, FUERA_R[i], FUERA_G[i], ramp, FUERA_B[i])
    return lz


def main():
    raiz = dir_estado(ESTADO, VERSION)
    frames = [a_rgba(cap(i / FRAMES)) for i in range(FRAMES)]
    Image.fromarray(np.hstack(frames), 'RGBA').save(
        os.path.join(raiz, 'state_captured_sheet.png'))
    Image.fromarray(frames[1], 'RGBA').save(
        os.path.join(raiz, 'state_captured.png'))

    print('ART-ST-CAP v2 «El relevo»')
    print(f'  1 hoja de {FRAMES} frames {CELDA * FRAMES}x{CELDA} '
          f'(0.4 s x 12 fps), {len(CAPTURABLES)} nodos capturables')
    if not verifica_tinte(cap, FRAMES, nodos=CAPTURABLES, bucle=False,
                          entra_de_nada=False, exige_contador=False):
        raise SystemExit('alguna comprobacion ha fallado')


if __name__ == '__main__':
    main()
