"""NOVA - Overlay de estado `Capturado` (ART-ST-CAP), v3: EL SELLO.

Ref: NOVA_Estados_Animaciones.md 14.1, 14.5 y 14.7 - NOVA_Assets_Diseno.md A.3

LA TESIS: UNA CAPTURA NO IRRADIA, AGARRA. 14.1 pide «onda expansiva» y esta
propuesta hace lo contrario a proposito: un solo anillo que se CIERRA desde
el borde de la celda sobre el nodo y se apaga al llegar. Nada sale; algo
entra y se queda.

El motivo es de vocabulario, no de gusto. En NOVA ya hay tres cosas que
salen de un nodo hacia fuera: la corona de la Estrella, los conos y los
frentes del Pulsar y la nube de `Infectado`. Una onda expansiva mas es una
cuarta cosa saliendo, y a 0.4 s el jugador tiene que distinguirla de las
otras tres de un vistazo. **Nada en el proyecto se cierra sobre un nodo.**
Ese gesto esta libre y significa exactamente lo que ha pasado.

    Es ademas lo que diferencia una captura de un impacto. `FX-DMG` -dano
    sin captura- y `FX-ABS` -absorcion aliada- todavia no estan disenados
    (A.5), y los dos son sucesos de impacto sobre un nodo. Si la captura es
    un estallido, los tres van a acabar siendo el mismo estallido de
    distinto tamano. Si la captura es lo unico que se cierra, no hay manera
    de confundirla.

EL ANILLO ENGORDA AL CERRARSE. De 1.6 px de grosor a 5.2: es el mismo
principio que `linea_dipolar` -el grosor sigue a la magnitud- y aqui lo que
crece es la inminencia. Un anillo de grosor constante que se encoge parece
un aro que se aleja; uno que engorda parece algo que aprieta.

SIN NUCLEO Y SIN RELLENO, y se lo puede permitir por una medida:

    La silueta es IDENTICA byte a byte entre facciones en los catorce nodos
    (14.0 #3, comprobada contra los 34 PNG). `ChangeFaction()` es un
    recoloreado en el sitio, no un corte de geometria, al reves que en
    `ART-ST-CONV`. Aqui no hay empalme que tapar, solo un suceso que
    subrayar.

Es la propuesta que menos tapa de las tres, y la unica que no deja la cara
ciega ni un frame: el anillo llega al centro justo cuando se apaga.

EL BLANCO ES SUYO POR ASIGNACION. Al cerrar `ART-ST-CONV` (A.3.6) se decidio
que la reconversion destellara en color de faccion para dejarle el blanco a
este asset, porque 8.1 permite que capturen un nodo mientras se reconvierte.

LO QUE ESTA PROPUESTA PAGA, Y ES LO MAS SERIO DE LAS TRES. **Contradice
14.1**, que pide «flash blanco, onda expansiva» con esas palabras. Aqui hay
blanco pero no hay expansion. Si se elige, 14.1 hay que corregirlo, no
interpretarlo: es un cambio de texto, no una lectura. Y se pierde la
contundencia: cinco frames de un aro fino son mucho menos suceso que un
fogonazo, y `Capturado` es el momento en el que cambia de dueno una pieza
del tablero.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import a_rgba, dir_estado, lienzo, rampa, tramar
from nova_estados import CAPTURABLES, verifica_tinte

ESTADO = 'cap'
VERSION = 3
FRAMES = 5
CELDA = 96
C = CELDA / 2.0

# radio, grosor y banda por frame. El ultimo esta vacio: sale a nada.
# El aro arranca en 42 y no en 45: ondula por armonicos -k llega a 1.067- y
# el grosor suma otro pixel y medio, asi que 45 se salia de la celda.
ARO_R = (42.0, 32.0, 22.0, 12.0, 0.0)
ARO_G = (1.6, 2.6, 3.8, 5.2, 0.0)
ARO_B = (2, 1, 0, 0, 6)
ARM = ((5, 0.045, 0.9), (9, 0.022, 3.6))    # el aro no es un circulo exacto


def cap(t=0.0):
    lz = lienzo(CELDA)
    ramp = rampa('neutral')     # el indice 0 es #FFFFFF en todas las rampas
    y, x = np.mgrid[0:CELDA, 0:CELDA]
    dx, dy = x + 0.5 - C, y + 0.5 - C
    rad = np.hypot(dx, dy)
    ang = np.arctan2(dy, dx)
    k = 1.0 + sum(a * np.cos(n * ang + p) for n, a, p in ARM)
    i = min(int(round(t * FRAMES)), FRAMES - 1)

    r, g = ARO_R[i], ARO_G[i]
    if r <= 0.0:
        return lz
    d = np.clip(1.0 - np.abs(rad - r * k) / g, 0.0, 1.0) ** 0.6
    tramar(lz, d, ramp, banda_int=ARO_B[i], banda_ext=ARO_B[i] + 2, corte=0.40)
    return lz


def main():
    raiz = dir_estado(ESTADO, VERSION)
    frames = [a_rgba(cap(i / FRAMES)) for i in range(FRAMES)]
    Image.fromarray(np.hstack(frames), 'RGBA').save(
        os.path.join(raiz, 'state_captured_sheet.png'))
    Image.fromarray(frames[2], 'RGBA').save(
        os.path.join(raiz, 'state_captured.png'))

    print('ART-ST-CAP v3 «El sello»')
    print(f'  1 hoja de {FRAMES} frames {CELDA * FRAMES}x{CELDA} '
          f'(0.4 s x 12 fps), {len(CAPTURABLES)} nodos capturables')
    if not verifica_tinte(cap, FRAMES, nodos=CAPTURABLES, bucle=False,
                          entra_de_nada=False, exige_contador=False):
        raise SystemExit('alguna comprobacion ha fallado')


if __name__ == '__main__':
    main()
