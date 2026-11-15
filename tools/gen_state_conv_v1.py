"""NOVA - Overlay de estado `Reconvirtiendo` (ART-ST-CONV), v1: EL TORNO.

Ref: NOVA_Estados_Animaciones.md 14.1 y 14.6 - NOVA_GameDesign_Spec.md 8 -
NOVA_Assets_Diseno.md A.3

EL PRIMER ASSET DE A.3 QUE NO ES UN ESTADO, ES UN TRANSITO. Los cinco
anteriores duran lo que dure la condicion -`Dormido` hasta que lo capturan,
`Congelado` hasta 30 s, `Infectado` indefinido-. Este dura **3.0 s exactos**
(8.1) y se acaba. Eso cambia tres cosas de golpe:

  - son **36 frames** y no 16, porque `frames = ceil(3.0 * 12)` y aqui la
    duracion manda sobre el bucle;
  - **no cierra**. Entra desde nada y sale a nada, que es lo que tiene que
    cumplir un efecto que se compone contra el estado `Normal` por los dos
    lados. Un primer frame con pixeles es un salto;
  - **cuenta una historia**, y es la unica de A.3 que la cuenta. Colapso,
    punto neutro, reencendido.

LO QUE UN OVERLAY ADITIVO NO PUEDE HACER, Y HAY QUE DECIRLO. 14.1 pide
«colapso a silueta neutra y reencendido». Un overlay solo SUMA pixeles: con
alfa binario no puede borrar el sprite de debajo ni recolorearlo. La silueta
neutra no la dibuja este asset: **la dibuja el animador cambiando de sprite**,
porque al terminar el nodo es de otro tipo y de nivel 1 (8.1). Lo que hace
este asset es TAPAR EL CORTE. Es exactamente el papel que ya tiene `FX-CAP`
sobre `ChangeFaction()` -«flash blanco + onda»-, solo que doce veces mas
largo.

    Medido: un disco de radio 45 centrado en la celda cubre la union de los
    trece sprites reconvertibles -`x 6-92, y 10-88`, 3956 px- sin dejar ni
    un pixel fuera y sin tocar el borde. Ese numero no se elige: es el
    tamano minimo que hace invisible el cambio de sprite.

Y «NEUTRA» QUIERE DECIR SIN TIPO, NO SIN BANDO. 8.1 deja claro que el nodo
sigue siendo tuyo durante los 3 s -tanto que puede ser capturado, y si lo
capturan la reconversion se cancela-. Pintarlo de gris `neutral` durante
tres segundos diria que ha dejado de ser tuyo, que es falso y es
exactamente el tipo de promesa que A.2.1 prohibe hacerle al sprite. Asi que
esta propuesta va **en color de faccion**, `str_p` y `str_e`, y es el segundo
overlay de A.3 con dos variantes despues de `ART-ST-IGN`. Lo que pierde el
nodo es el TIPO, y el tipo se ve en la geometria, no en la rampa.

DE DONDE SALE LA FORMA. Un torno: el nodo se desmonta en anillos que caen
hacia dentro, se queda un instante en nada, y se vuelve a montar en anillos
que salen. Tres actos sobre 36 frames:

     0-15  COLAPSO       tres anillos caen de r=40 a r=19, desfasados
    16-21  EL CORTE      dos frames de disco lleno -ahi cambia el sprite-
                         y cuatro en los que el disco se retira
    22-34  REENCENDIDO   tres anillos salen y se deshacen al llegar
       35  vacio         el efecto sale a nada

EL CORTE NO VA EN BLANCO, Y ES UNA RESERVA DELIBERADA. 14.5 le da a `FX-CAP`
-la captura- «flash blanco + onda», 0.4 s. Si la reconversion tambien
destella en blanco, los dos eventos mas importantes que le pueden pasar a un
nodo empiezan igual, y encima 8.1 los hace coincidir: un nodo puede ser
capturado MIENTRAS se reconvierte. El corte va en las bandas 1 y 2 de la
rampa de faccion, que son claras pero inequivocamente de color. El blanco
puro se queda para `FX-CAP`, que todavia no esta disenado.

EXENTO DEL CONTADOR DE NIVEL, Y ES LA UNICA EXENCION LEGITIMA. Los otros dos
tintes tienen prohibido tapar las esquirlas y los frentes porque A.8 #2 dejo
el nivel solo en el sprite. Aqui el nivel **esta cambiando**: el nodo acaba
en N1 del tipo nuevo (8.1), asi que durante estos 3 s no hay contador fiable
que proteger. La exencion la tiene este asset porque su duracion coincide
exactamente con la del cambio, y no se hereda.

LO QUE ESTA PROPUESTA PAGA. Anillos concentricos centrados en el nodo son
**simetria radial n-fold centrada**, que es la firma de la Estrella (A.8,
*Ejes de diseno ya ocupados*), y una de cada tres reconversiones acaba
justamente en una Estrella. Es transitorio, que es lo unico que lo hace
defendible.
"""

# El primero de los dos frames del corte lleva la BANDA 3, que es donde
# vive el ancla funcional de 14.4 (`#3FD7F5` / `#F2456B`): el frame mas
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
VERSION = 1
FRAMES = 36         # 3.0 s x 12 fps (8.1). No es un bucle: tiene final
CELDA = 96
C = CELDA / 2.0

R_TAPA = 45.0       # medido: cubre la union de los 13 sin tocar el borde
R_ANILLO = 40.0     # tope de los anillos: r + grosor no puede pasar de 46.5
R_MIN = 19.0        # los anillos no bajan de aqui; del centro se ocupa el
                    # corte. Cada vuelta que un anillo da por encima de la
                    # cara es un frame en el que el nodo no puede avisar.

COLAPSO = (0, 16)       # frames [0, 16)
NEUTRO = (16, 22)       # f16 y f17 tapan la celda entera: ahi va el corte
REENCENDIDO = (22, 35)  # y f35 queda vacio: el efecto sale a nada

N_ANILLOS = 3
GROSOR = 4.0
DENS = 0.92
DESFASE = 0.17      # entre anillo y anillo, en fraccion de acto


def _radial():
    y, x = np.mgrid[0:CELDA, 0:CELDA]
    return np.hypot(x + 0.5 - C, y + 0.5 - C)


def _anillo(lz, rad, r, grosor, ramp, dens, banda_int, banda_ext):
    if r <= 0.0:
        return
    d = np.clip(1.0 - np.abs(rad - r) / grosor, 0.0, 1.0) ** 0.8 * dens
    tramar(lz, d, ramp, banda_int=banda_int, banda_ext=banda_ext, corte=0.55)


def conv(t=0.0, faccion='player'):
    """El overlay en la fase `t` de los 3 s. `t` va de 0 a 1."""
    lz = lienzo(CELDA)
    ramp = rampa('str_p' if faccion == 'player' else 'str_e')
    rad = _radial()
    f = t * FRAMES

    if COLAPSO[0] <= f < COLAPSO[1]:
        u = (f - COLAPSO[0]) / (COLAPSO[1] - COLAPSO[0])
        for i in range(N_ANILLOS):
            v = u - i * DESFASE
            if v <= 0.0:
                continue
            v = min(v / (1.0 - (N_ANILLOS - 1) * DESFASE), 1.0)
            r = R_ANILLO - (R_ANILLO - R_MIN) * v
            _anillo(lz, rad, r, GROSOR * (0.7 + 0.3 * (1 - v)), ramp,
                    DENS * (0.55 + 0.45 * v), 1 if v > 0.6 else 2, 3)

    elif NEUTRO[0] <= f < NEUTRO[1]:
        # El corte. f16 y f17 tapan la celda entera -ahi el animador cambia
        # de sprite y no se ve-, y de f18 a f21 el disco se retira. Los dos
        # frames llenos llevan banda distinta para que no sean el mismo
        # frame repetido: una hoja con dos imagenes iguales miente sobre su
        # tamano (contrato, *Reglas de trabajo*).
        k = int(f) - NEUTRO[0]
        if k <= 1:
            d = np.where(rad <= R_TAPA, 1.0, 0.0)
            tramar(lz, d, ramp, banda_int=3 - k, banda_ext=3 - k, corte=0.0)
        else:
            r = R_TAPA * (1.0 - (k - 1) / 5.0)
            d = np.where(rad <= r, 1.0, 0.0)
            tramar(lz, d, ramp, banda_int=2, banda_ext=3, corte=0.5)

    else:
        # arranca en f22 con los anillos YA en marcha: un frame vacio en
        # mitad del efecto es un parpadeo, no una pausa.
        u = (f - (REENCENDIDO[0] - 1)) / (REENCENDIDO[1] - (REENCENDIDO[0] - 1))
        for i in range(N_ANILLOS):
            v = u - i * DESFASE
            if v <= 0.0:
                continue
            v = min(v / (1.0 - (N_ANILLOS - 1) * DESFASE), 1.0)
            if v >= 1.0:
                continue
            r = R_MIN + (R_ANILLO - R_MIN) * v
            _anillo(lz, rad, r, GROSOR * (0.7 + 0.3 * v), ramp,
                    DENS * (1.0 - v) ** 0.8, 1 if v < 0.4 else 2, 4)
    return lz


def main():
    raiz = dir_estado(ESTADO, VERSION)
    for fac in ('player', 'enemy'):
        frames = [a_rgba(conv(i / FRAMES, fac)) for i in range(FRAMES)]
        Image.fromarray(np.hstack(frames), 'RGBA').save(
            os.path.join(raiz, f'state_convert_{fac}_sheet.png'))
        Image.fromarray(frames[FRAMES // 3], 'RGBA').save(
            os.path.join(raiz, f'state_convert_{fac}.png'))

    print('ART-ST-CONV v1 «El torno»')
    print(f'  2 hojas de {FRAMES} frames {CELDA * FRAMES}x{CELDA} '
          f'(3.0 s x 12 fps), {len(RECONVERTIBLES)} nodos reconvertibles')
    ok = True
    for fac in ('player', 'enemy'):
        print(f'  --- {fac} ---')
        ok &= verifica_tinte(conv, FRAMES, nodos=RECONVERTIBLES,
                             sprite=SPRITE_PROPIO, bucle=False,
                             exige_contador=False,
                             args=(fac,))
    if not ok:
        raise SystemExit('alguna comprobacion ha fallado')


if __name__ == '__main__':
    main()
