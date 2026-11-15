"""NOVA - Overlay de estado `Reconvirtiendo` (ART-ST-CONV), v2: LA COLADA.

Ref: NOVA_Estados_Animaciones.md 14.1 y 14.6 - NOVA_GameDesign_Spec.md 8 -
NOVA_Assets_Diseno.md A.3

LA TESIS: NO IMPLOSIONA, SE VUELVE A COLAR. La v1 desmonta el nodo hacia su
centro con anillos concentricos, y eso es **simetria radial n-fold
centrada**, que es la firma de la Estrella (A.8) y el destino de una de cada
tres reconversiones. Esta propuesta no tiene centro: un FRENTE recorre la
celda de arriba abajo, y por donde pasa el nodo deja de estar. Cuando el
frente llega abajo la celda esta llena, ahi cambia el sprite, y el frente
sigue bajando: ahora lo que deja detras es el nodo NUEVO.

Un solo movimiento, en una sola direccion, de principio a fin. No hay
«colapso» y «reencendido» como dos gestos opuestos: hay una colada que pasa.

    0-15   la banda crece desde arriba; su borde inferior baja de 0 a 96
    16-17  la celda llena. Ahi el animador cambia de sprite
    18-34  la banda se vacia por abajo; su borde superior baja de 0 a 96
       35  vacio

LA FORMA SE RECORTA A UN DISCO, Y NO ES ESTETICA. Una banda que cruza la
celda entera la toca por los cuatro lados, y un asset que toca el borde se
corta en el atlas. Recortada a un disco de radio 45 no toca ninguno **y sigue
cubriendo la union de los trece sprites reconvertibles** -`x 6-92, y 10-88`,
3956 px, cero fuera-. Ese radio es la medida que hace invisible el cambio de
sprite, y es la misma para las tres propuestas.

EL BORDE DEL FRENTE VA SOLIDO Y BRILLANTE. Lo que vende la lectura de
«frente que avanza» no es el relleno, es la linea: una banda tramada sin
borde es una cortina; con borde, algo que pasa. No va en `veta_luz` -esa
primitiva traza un SEGMENTO recto y este filo ondula- sino en `tramar` con
densidad 1 sobre la banda de un pixel alrededor de la curva, que es la misma
capa `glow` y por tanto tampoco lleva contorno.

EN COLOR DE FACCION, como la v1 y por el mismo motivo: 8.1 dice que el nodo
sigue siendo tuyo durante los 3 s -tanto que puede ser capturado, y entonces
la reconversion se cancela-. Lo que pierde es el TIPO, no el bando.

EL CORTE NO VA EN BLANCO. 14.5 le da a `FX-CAP` -la captura- «flash blanco
+ onda», y 8.1 permite que capturen un nodo mientras se reconvierte: si los
dos destellan en blanco, los dos eventos se confunden justo cuando coinciden.
El corte va en las bandas 1 y 2 de la rampa de faccion.

LO QUE ESTA PROPUESTA PAGA. Un barrido recto es lo mas parecido a una
transicion de interfaz que hay en este documento: un wipe. NOVA no tiene un
solo elemento de UI dentro del tablero (A.3.2), y una linea horizontal
perfecta cruzando un nodo es justo eso. Se mitiga con el borde irregular
-el frente no es recto, ondula- pero el riesgo esta.
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
VERSION = 2
FRAMES = 36
CELDA = 96
C = CELDA / 2.0

R_TAPA = 45.0       # medido: cubre la union de los 13 sin tocar el borde
BAJADA = (0, 16)    # el frente inferior baja: la banda crece
CORTE = (16, 18)    # celda llena. Aqui cambia el sprite
SUBIDA = (18, 35)   # el frente superior baja: la banda se vacia
ONDA = ((3, 2.6, 0.7), (7, 1.3, 3.9))   # el frente no es recto
ESTELA = 15.0       # a que distancia del frente se apaga la estela
DENS_ESTELA = 0.34  # lo que queda detras: se ve, pero deja ver

# La estela NO es opaca, y esa es la unica correccion de peso que se le hizo
# a esta propuesta. Con la banda llena -una cortina de verdad- el nodo se
# quedaba tapado desde que el frente le pasaba por encima hasta que volvia a
# pasar: medido, **19 frames de 36 con la cara enterrada**, o sea 1.6 s de
# los 3 sin poder avisar, y 14.6 hace `Asustado` compatible con este estado.
# No es un parametro que apretar: una cortina opaca tapa la mitad del efecto
# por construccion. Con el frente denso y la estela al 34 % la cara solo
# queda ciega cuando el filo le pasa por encima.


def _malla():
    y, x = np.mgrid[0:CELDA, 0:CELDA]
    return x + 0.5, y + 0.5


def _frente(x, y0):
    """La altura del frente en cada columna: ondula, no es una recta."""
    return y0 + sum(a * np.sin(n * (x / CELDA) * 2 * np.pi + f)
                    for n, a, f in ONDA)


def conv(t=0.0, faccion='player'):
    lz = lienzo(CELDA)
    ramp = rampa('str_p' if faccion == 'player' else 'str_e')
    x, y = _malla()
    disco = np.hypot(x - C, y - C) <= R_TAPA
    f = t * FRAMES

    if BAJADA[0] <= f < BAJADA[1]:
        u = (f - BAJADA[0]) / (BAJADA[1] - BAJADA[0])
        y0 = -6.0 + (CELDA + 12.0) * u
        fr = _frente(x, y0)
        dens = np.where(y <= fr,
                        DENS_ESTELA + (1.0 - DENS_ESTELA)
                        * np.clip(1.0 - (fr - y) / ESTELA, 0.0, 1.0), 0.0)
        borde = y0
    elif CORTE[0] <= f < CORTE[1]:
        banda = np.ones_like(disco)
        borde = None
        # los dos frames llenos, con banda distinta para que no sean el
        # mismo frame repetido
        k = int(f) - CORTE[0]
        tramar(lz, np.where(disco, 1.0, 0.0), ramp,
               banda_int=3 - k, banda_ext=3 - k, corte=0.0)
        return lz
    else:
        u = (f - (SUBIDA[0] - 1)) / (SUBIDA[1] - (SUBIDA[0] - 1))
        y0 = -6.0 + (CELDA + 12.0) * u
        fr = _frente(x, y0)
        dens = np.where(y > fr,
                        DENS_ESTELA + (1.0 - DENS_ESTELA)
                        * np.clip(1.0 - (y - fr) / ESTELA, 0.0, 1.0), 0.0)
        borde = y0

    tramar(lz, np.where(disco, dens, 0.0), ramp, banda_int=1, banda_ext=3,
           corte=0.55)
    if borde is not None and -6.0 < borde < CELDA + 6.0:
        # el filo del frente, solido y claro, recortado al disco
        filo = np.abs(y - _frente(x, borde)) <= 0.9
        tramar(lz, np.where(filo & disco, 1.0, 0.0), ramp,
               banda_int=0, banda_ext=0, corte=0.0)
    return lz


def main():
    raiz = dir_estado(ESTADO, VERSION)
    for fac in ('player', 'enemy'):
        frames = [a_rgba(conv(i / FRAMES, fac)) for i in range(FRAMES)]
        Image.fromarray(np.hstack(frames), 'RGBA').save(
            os.path.join(raiz, f'state_convert_{fac}_sheet.png'))
        Image.fromarray(frames[8], 'RGBA').save(
            os.path.join(raiz, f'state_convert_{fac}.png'))

    print('ART-ST-CONV v2 «La colada»')
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
