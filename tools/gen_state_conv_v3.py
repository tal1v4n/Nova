"""NOVA - Overlay de estado `Reconvirtiendo` (ART-ST-CONV), v3: EL APAGON.

Ref: NOVA_Estados_Animaciones.md 14.1 y 14.6 - NOVA_GameDesign_Spec.md 8 -
NOVA_Assets_Diseno.md A.3

LA LECTURA LITERAL DE 14.1: «colapso a silueta neutra y reencendido». Las
otras dos propuestas reinterpretan «neutra» como «sin tipo» y pintan el
efecto en color de faccion. Esta se la toma al pie de la letra: el nodo se
APAGA A GRIS, se queda un instante siendo una silueta sin dueno aparente, y
se reenciende siendo otra cosa. Rampa `neutral`, el `#8A93A6` que 14.4 le da
al bando Neutral.

Es ademas la mas simple de las tres y la unica que necesita **un solo
archivo**: si el efecto es gris, no hay variante de faccion que hacer. Las
otras dos son dos hojas de 36 frames cada una; esta es una.

TRES ACTOS SIN NINGUNA FIGURA. No hay anillos ni frentes: solo una frontera
radial que se come el nodo de fuera adentro y luego se retira encogiendo.

     0-15  APAGON     el gris entra desde el limbo, `r_int` baja de 45 a 0
    16-17  NEUTRO     la celda llena. Ahi el animador cambia de sprite
    18-34  REENCENDIDO el gris colapsa a un punto, `r_ext` baja de 45 a 0,
                      y el nodo nuevo aparece de fuera adentro
       35  vacio

«De fuera adentro» y «colapsando a un punto» no son el mismo gesto invertido,
y por eso el efecto no parece una animacion puesta del reves: entra como una
marea y sale como un sumidero.

EL RIESGO QUE ASUME, Y ES EL GRANDE. Durante casi tres segundos el nodo
**parece Neutral**, y no lo es. 8.1 dice que sigue siendo tuyo -tanto que
puede ser capturado, y si lo capturan la reconversion se cancela y el nodo
queda con el nuevo dueno-. Gris es el color que 14.4 reserva al bando sin
dueno, asi que este overlay le dice al jugador algo que es falso justo
mientras el nodo esta en su momento mas vulnerable. Es exactamente el tipo de
promesa que A.2.1 prohibe hacerle al sprite -«sin valor aparente,
literalmente»-, solo que al reves.

Se propone igual porque es lo que el documento pide con esas palabras, y
porque el coste de las otras dos no es cero: en color de faccion, el efecto
se parece mas a un power-up o a una captura que a un desmontaje. La eleccion
es entre decir una cosa falsa muy claramente y decir la verdadera de forma
mas ambigua.
"""

# El primero de los dos frames del corte lleva la BANDA 3, que es donde
# vive el ancla funcional de 14.4 (`#8A93A6`): el frame mas
# visible del efecto es literalmente el color que el jugador asocia al
# concepto, y no un blanco generico.

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

from nova_core import a_rgba, dir_estado, lienzo, rampa, tramar
from nova_estados import RECONVERTIBLES, SPRITE_PROPIO, verifica_tinte

ESTADO = 'conv'
VERSION = 3
FRAMES = 36
CELDA = 96
C = CELDA / 2.0

R_TAPA = 45.0       # medido: cubre la union de los 13 sin tocar el borde
APAGON = (0, 16)
NEUTRO = (16, 18)
REENCENDIDO = (18, 35)
BORDE_SUAVE = 5.0   # anchura del frente tramado
DENS_VELO = 0.36    # lo que tapa el velo por dentro
ARM = ((3, 0.09, 1.4), (6, 0.05, 4.1))  # la frontera no es un circulo

# EL VELO NO ES OPACO, y esa es la correccion de peso que se le hizo. Con el
# gris lleno -un apagon de verdad- el nodo quedaba tapado desde que la marea
# le cubria la cara hasta que se retiraba: medido, **17 frames de 36 con la
# cara ciega**, 1.4 s de los 3 sin poder avisar, y 14.6 hace `Asustado`
# compatible con este estado. Al 36 % el nodo se ve apagado y se sigue
# viendo, que ademas dice mejor «pierde el tipo» que taparlo: un nodo que no
# se ve no se esta apagando, se ha ido.
#
# Ningun radio pasa de R_TAPA. La frontera ondula por armonicos, y si la
# ondulacion se aplicara tambien al limite exterior el velo se saldria de la
# celda: `k` llega a 1.14 y 45 x 1.14 son 51.


def conv(t=0.0):
    lz = lienzo(CELDA)
    ramp = rampa('neutral')
    y, x = np.mgrid[0:CELDA, 0:CELDA]
    dx, dy = x + 0.5 - C, y + 0.5 - C
    rad = np.hypot(dx, dy)
    ang = np.arctan2(dy, dx)
    k = 1.0 + sum(a * np.cos(n * ang + p) for n, a, p in ARM)
    f = t * FRAMES

    if APAGON[0] <= f < APAGON[1]:
        u = (f - APAGON[0]) / (APAGON[1] - APAGON[0])
        # arranca en 1.25 x R_TAPA y no en R_TAPA: la frontera ondula, y con
        # la ondulacion metida hacia dentro el frame 0 ya tendria pixeles.
        r_int = R_TAPA * 1.25 * (1.0 - u)
        dens = np.clip((rad - r_int * k) / BORDE_SUAVE, 0.0, 1.0) * DENS_VELO
        dens = np.where(rad <= R_TAPA, dens, 0.0)
    elif NEUTRO[0] <= f < NEUTRO[1]:
        j = int(f) - NEUTRO[0]
        tramar(lz, np.where(rad <= R_TAPA, 1.0, 0.0), ramp,
               banda_int=3 - j, banda_ext=3 - j, corte=0.0)
        return lz
    else:
        u = (f - (REENCENDIDO[0] - 1)) / (REENCENDIDO[1] - (REENCENDIDO[0] - 1))
        r_ext = R_TAPA * (1.0 - u)
        dens = np.clip((r_ext * k - rad) / BORDE_SUAVE, 0.0, 1.0) * DENS_VELO
        dens = np.where(rad <= R_TAPA, dens, 0.0)

    tramar(lz, dens, ramp, banda_int=2, banda_ext=4, corte=0.22)
    return lz


def main():
    raiz = dir_estado(ESTADO, VERSION)
    frames = [a_rgba(conv(i / FRAMES)) for i in range(FRAMES)]
    Image.fromarray(np.hstack(frames), 'RGBA').save(
        os.path.join(raiz, 'state_convert_sheet.png'))
    Image.fromarray(frames[10], 'RGBA').save(
        os.path.join(raiz, 'state_convert.png'))

    print('ART-ST-CONV v3 «El apagon»')
    print(f'  1 hoja de {FRAMES} frames {CELDA * FRAMES}x{CELDA} '
          f'(3.0 s x 12 fps), sin variante de faccion, '
          f'{len(RECONVERTIBLES)} nodos reconvertibles')
    if not verifica_tinte(conv, FRAMES, nodos=RECONVERTIBLES,
                          sprite=SPRITE_PROPIO, bucle=False,
                          exige_contador=False):
        raise SystemExit('alguna comprobacion ha fallado')


if __name__ == '__main__':
    main()
