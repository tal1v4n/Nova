"""NOVA - Overlay de estado `Asustado` (ART-ST-SCARE), v2: LA PUPILA DILATADA.

Ref: NOVA_Estados_Animaciones.md 14.1, 14.2 y 14.7 - NOVA_Assets_Diseno.md A.3

EL MISMO OJO, LA OTRA ANATOMIA DEL MIEDO. Las dos propuestas comparten todo lo
que sale de la medida -el anclaje de `nova_estados.OJOS`, las dos tallas 6x8 y
8x12, la esclerotica clara, el ojo estatico- porque eso no es cuestion de
gusto: es lo unico que tapa los 34 ojos del reparto sin fundirse. Lo unico que
cambia es la pupila, y con ella el miedo del que se habla:

                        v1 Los ojos como platos   v2 La pupila dilatada
    Pupila              contraida, 20% del ojo    dilatada, 34% del ojo
    Posicion            baja, bajo el parpado     casi centrada
    De que miedo habla  sobresalto: adrenalina,   pavor: la pupila se abre
                        la pupila se cierra de    para tragar luz, es el
                        golpe                     miedo largo
    Punto fuerte        mucha esclerotica = el    la cara conserva su cara;
                        gesto mas violento        el bicho da lastima
    Punto debil         el bicho parece de        mas pupila es mas mancha
                        dibujos animados          oscura, y el ojo normal
                                                  tambien es una mancha oscura

MEDIDO, POR SI AYUDA A ELEGIR: sobre los 14 nodos, la zona de los ojos se
oscurece -27.6 de luminancia media con la v1 y -42.3 con esta. Las dos cambian
la cara mucho; la v2 la cambia mas. Lo que el numero NO dice es cual se lee
antes como «miedo», y eso solo se ve en `art/_preview/estados/scare_1x.png`.

SIN ANIMACION, Y NO POR AHORRAR. Este asset es de UN FRAME por talla en las
dos propuestas, y no es una decision de coste: es que NO CABE una animacion.
Se probaron los tres canales que este proyecto usa a tamano pequeno, y los
tres se los come la rejilla:

    pupila que se desplaza    7-9 frames distintos de 16
    pupila que se contrae     5-8 frames distintos de 16
    esclerotica que pulsa     2-8 frames distintos de 16

Un ojo de 6x8 px tiene una pupila de 3 px y unos 2 px de juego; subir el
recorrido no ayuda, satura en 9. Una hoja de 16 frames que solo contiene 7
imagenes distintas no es una animacion, es un atlas mintiendo sobre su tamano.
Y el estado YA se mueve: lo sacude el transform de la capa 5 de 14.7. Es el
mismo criterio que dejo el idle del Asteroide y el del Magnetar en transform.

EL RESTO DEL RAZONAMIENTO -por que el asset es un OJO y no un par, por que hay
dos tallas discretas y por que la forma es mas alta que ancha- esta en
`gen_state_scare_v1.py` y en `nova_estados.OJOS`. No se repite aqui: es comun
a las dos y no es lo que se elige.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

from nova_core import a_rgba, dir_estado, esferoide, lienzo, rampa
from nova_ojos import OJOS, TALLAS, verifica_ojos  # descartado: T6-12

ESTADO = 'scare'
VERSION = 2
FRAMES = 1
CELDA = 96

CALOR = 4.2
PUPILA_W = 0.34     # dilatada. En la v1 es 0.20.
PUPILA_H = 0.32
PUPILA_BAJA = 0.04  # casi centrada: una pupila dilatada no se esconde bajo el
                    # parpado, se planta en medio del ojo
APAGADO = 2.6       # un punto mas oscura que la de la v1, para que la mancha
                    # no se deshaga al crecer


def _ojo(lz, cx, cy, talla):
    w, h = TALLAS[talla]
    ramp = rampa('ojo')
    esferoide(lz, cx, cy, w / 2.0, h / 2.0, 0.0, ramp, 90, calor=CALOR)
    esferoide(lz, cx, cy + h * PUPILA_BAJA,
              max(1.0, w * PUPILA_W), max(1.2, h * PUPILA_H), 0.0,
              ramp, 91, apagado=APAGADO)


def ojo(talla, t=0.0):
    """El asset: UN ojo, centrado en la celda. Se instancia dos veces."""
    lz = lienzo(CELDA)
    _ojo(lz, CELDA / 2.0, CELDA / 2.0, talla)
    return lz


def par(talla, ancla_izq, ancla_der, t=0.0):
    """El par ya colocado sobre un nodo. Solo para verificar y previsualizar."""
    lz = lienzo(CELDA)
    c = CELDA / 2.0
    for (ex, ey) in (ancla_izq, ancla_der):
        _ojo(lz, c + ex, c + ey, talla)
    return lz


def main():
    raiz = dir_estado(ESTADO, VERSION)
    for talla in TALLAS:
        Image.fromarray(a_rgba(ojo(talla)), 'RGBA').save(
            os.path.join(raiz, f'state_scared_{talla}.png'))

    print('ART-ST-SCARE v2 «La pupila dilatada»')
    print(f'  {len(TALLAS)} archivos de 1 frame, {len(OJOS)} nodos cubiertos')
    if not verifica_ojos(par, FRAMES):
        raise SystemExit('alguna comprobacion ha fallado')


if __name__ == '__main__':
    main()
