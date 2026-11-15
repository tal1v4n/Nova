"""NOVA - Overlay de estado `Capturado` (ART-ST-CAP), v1: EL FOGONAZO.

Ref: NOVA_Estados_Animaciones.md 14.1, 14.5 y 14.7 - NOVA_Assets_Diseno.md A.3

LA LECTURA LITERAL DE 14.1: «flash blanco, onda expansiva, cambio de
paleta». Un nucleo blanco que llena el nodo en el frame del impacto y se
apaga, mas un anillo que sale y se deshace. Cinco frames: 0.4 s x 12 fps.

EL BLANCO ESTABA RESERVADO PARA ESTE ASSET, y lo reservo otro. Al cerrar
`ART-ST-CONV` (A.3.6) se decidio que la reconversion NO destellara en blanco
-va en la banda 3 de su rampa de faccion- precisamente porque 8.1 permite
que capturen un nodo mientras se reconvierte: los dos eventos pueden
coincidir sobre el mismo sprite y no pueden empezar igual. Asi que aqui el
blanco no es una eleccion estetica, es una asignacion ya hecha.

    El indice 0 de toda rampa es `#FFFFFF` y esta reservado al especular
    (contrato, *Paleta*). Este es el unico overlay del proyecto que lo usa
    como color propio, y puede porque no compite con ningun especular: los
    tapa todos a la vez durante dos frames.

NO TIENE QUE TAPAR NADA, Y ESO LO CAMBIA TODO. `ART-ST-CONV` existe para
esconder un cambio de sprite: al reconvertir, el nodo pasa a otro tipo y a
nivel 1, y la silueta cambia entera. Aqui no.

    Medido sobre los 14 nodos y sus 34 sprites: la silueta es IDENTICA byte
    a byte entre facciones en los catorce. Es 14.0 #3 -«una variante de
    faccion es la misma llamada de geometria con otra rampa»- comprobada
    contra los PNG, no supuesta.

O sea que `ChangeFaction()` es un recoloreado en el sitio: no hay corte de
geometria que esconder. Este asset no tiene que cubrir la union de nada;
tiene que PUNTUAR. Es la diferencia entre tapar un empalme y subrayar un
suceso, y es la razon por la que el fogonazo puede permitirse ser mas
pequeno que el nodo mas grande.

ARRANCA EN SU PICO, Y NO ENTRA DESDE NADA. La regla que salio de
`ART-ST-CONV` -«un efecto con duracion entra desde nada y sale a nada»- vale
para un efecto que SE PREPARA. Este no se prepara: se dispara. El suceso que
lo lanza es instantaneo y el sprite cambia de paleta en ese mismo frame, asi
que el frame 0 tiene que ser el mas brillante de los cinco. Lo que si tiene
que cumplir es salir a nada, porque despues viene `Normal` y un efecto que
se corta a media intensidad hace un salto.

Con 5 frames no hay sitio para desperdiciar uno en negro: exigirle entrada
seria gastar el 20 % del asset en no decir nada.

DE QUE ESTA HECHO. El nucleo es un disco tramado que se apaga bajando de
banda -0 a 2- y encogiendo; el anillo sale de r=10 a r=40 adelgazando. Ni
uno ni otro llevan contorno: se dibujan encima de un sprite ajeno (contrato,
*Lo que se dibuja encima de otro dibujo no lleva contorno*).

EL NUCLEO SE ENCOGE DEPRISA, Y ESO SE MIDIO. Con el primer reparto -26, 20,
13, 6- tapaba la cara en tres frames de cinco: 0.25 s de un efecto de 0.4
sin poder ver los ojos, con `Asustado` activo si viene un segundo proton
detras. Con 22, 13, 5 son dos, que es el precio inevitable de un fogonazo:
el frame del impacto y el siguiente.

LO QUE ESTA PROPUESTA PAGA. Sigue siendo la que mas tapa de las tres, y es
la unica que tapa algo: un fogonazo con nucleo es, por definicion, un tapon
de dos frames. `Capturado` no bloquea nada y el nodo ya es funcional para su
nuevo dueno desde el frame 0 (14.1), pero el aviso de `Asustado` se pierde
durante 0.17 s.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import a_rgba, dir_estado, lienzo, rampa, tramar
from nova_estados import CAPTURABLES, verifica_tinte

ESTADO = 'cap'
VERSION = 1
FRAMES = 5          # 0.4 s x 12 fps (14.5). No es un bucle
CELDA = 96
C = CELDA / 2.0

NUCLEO_R = (22.0, 13.0, 5.0, 0.0, 0.0)      # radio por frame
NUCLEO_B = (0, 0, 1, 6, 6)                  # banda por frame
ONDA_R = (10.0, 20.0, 28.0, 35.0, 40.0)
ONDA_G = (3.6, 3.0, 2.3, 1.6, 0.9)          # el anillo adelgaza
ONDA_B = (0, 1, 1, 2, 3)


def cap(t=0.0):
    """El overlay en la fase `t`. Cinco frames, arranca en su pico."""
    lz = lienzo(CELDA)
    ramp = rampa('neutral')     # el indice 0 es #FFFFFF en todas las rampas
    y, x = np.mgrid[0:CELDA, 0:CELDA]
    rad = np.hypot(x + 0.5 - C, y + 0.5 - C)
    i = min(int(round(t * FRAMES)), FRAMES - 1)

    r = NUCLEO_R[i]
    if r > 0.0:
        d = np.clip(1.0 - (rad / r) ** 3.0, 0.0, 1.0)
        tramar(lz, d, ramp, banda_int=NUCLEO_B[i], banda_ext=NUCLEO_B[i] + 1,
               corte=0.35)

    ro, g = ONDA_R[i], ONDA_G[i]
    if i < FRAMES - 1:
        a = np.clip(1.0 - np.abs(rad - ro) / g, 0.0, 1.0) ** 0.7
        tramar(lz, a, ramp, banda_int=ONDA_B[i], banda_ext=ONDA_B[i] + 2,
               corte=0.45)
    return lz


def main():
    raiz = dir_estado(ESTADO, VERSION)
    frames = [a_rgba(cap(i / FRAMES)) for i in range(FRAMES)]
    Image.fromarray(np.hstack(frames), 'RGBA').save(
        os.path.join(raiz, 'state_captured_sheet.png'))
    Image.fromarray(frames[1], 'RGBA').save(
        os.path.join(raiz, 'state_captured.png'))

    print('ART-ST-CAP v1 «El fogonazo»')
    print(f'  1 hoja de {FRAMES} frames {CELDA * FRAMES}x{CELDA} '
          f'(0.4 s x 12 fps), {len(CAPTURABLES)} nodos capturables')
    if not verifica_tinte(cap, FRAMES, nodos=CAPTURABLES, bucle=False,
                          entra_de_nada=False, exige_contador=False):
        raise SystemExit('alguna comprobacion ha fallado')


if __name__ == '__main__':
    main()
