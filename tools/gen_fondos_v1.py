"""NOVA - Generador de los tres fondos de seccion.

Ref: NOVA_Assets_Diseno.md A.4.5 · NOVA_GameDesign_Spec.md 11.1
`ART-BG-01` Sistema Interior · `ART-BG-02` El Cinturon · `ART-BG-03` Espacio Profundo

2048x1152 px = 32x18 u a PPU 64: el area jugable entera, sin scroll.

DE ANTES DE T.8. El tablero encogio a 15 x 8.4375 u y `nova_fondo` paso a
960x540; esta version fija su propio lienzo para seguir reproduciendo sus
PNG tal cual. La vigente es la v2 (`gen_fondos_v2.py`).

TRES REGLAS, Y LAS TRES SON COMPROBABLES

1. PRESUPUESTO DE CONTRASTE. Toda silueta de nodo va perfilada con #141021
   (luminancia 18.1) y eso funciona porque es mas claro que el fondo plano
   (14.2). Si el fondo local sube por encima de 18.1, el perfilado pasa a ser
   mas oscuro que lo que lo rodea y deja de leerse como borde: se lee como un
   agujero. La media de cualquier ventana de 96x96 -la celda de un nodo- se
   queda por debajo. Lo verifica `informe()` sobre el PNG terminado.

2. SOLO RAMPAS SIN SIGNIFICADO. Todas las demas rampas del proyecto llevan
   un significado encima -faccion, estado, power-up- y un fondo pintado con
   una de ellas le miente al jugador durante toda la partida. `gusano`,
   `hielo`, `decaimiento`, `rayo` e `hidrogeno` estan expresamente
   prohibidas: son las cinco que hay que reconocer al instante.

   La consecuencia es que LAS TRES SECCIONES NO SE SEPARAN POR TONO. Se
   separan por estructura y densidad, que es mas dificil y mas duradero: el
   Sistema Interior esta lleno y barrido por la luz de su sol, el Cinturon
   esta cruzado por una corriente de escombro que tapa las estrellas, y el
   Espacio Profundo esta vacio. Se reconocen por como estan compuestos, no
   por de que color son.

3. LA VINETA VA AL REVES. En casi cualquier juego la vineta oscurece los
   bordes para llevar la mirada al centro. Aqui el centro es el tablero, asi
   que todo lo que el fondo tenga que ensenar se va a los bordes y la zona
   donde se juega se queda lo mas vacia posible.

NADA DEL FONDO PUEDE MEDIR LO QUE MIDE UN NODO. La huella de un nodo va de
0.6 a 1.2 u (38 a 77 px). Cualquier objeto del fondo que caiga en esa franja
es un nodo a ojos del jugador hasta que intente clicarlo, y eso es peor que
un fondo feo. Asi que todo lo que hay aqui esta o muy por debajo -grava de
20 px- o muy por encima -un planeta de 276 px-. Es la unica regla de
composicion de este archivo que no se puede negociar por estetica.

TODO POR TRAMA. No hay un solo degradado suave: cada nube es un campo de
densidad resuelto con la misma matriz de Bayer 8x8 que usan el halo de la
Estrella y los conos del Pulsar. Es lo que hace que el fondo pertenezca a la
misma imagen que los nodos en vez de parecer una foto detras de ellos.

Determinista: cada seccion lleva su semilla fija, asi que el PNG es salida
derivada como todo lo demas y se puede regenerar identico.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import numpy as np
from PIL import Image

import nova_fondo
from nova_core import RAIZ, rampa
from nova_fondo import (banda, disco, estrellas, informe, lienzo, ruido,
                        silueta, tramar, vineta)

# las primitivas leen el lienzo del modulo al llamarlas, no al importarlas
nova_fondo.ANCHO, nova_fondo.ALTO = 2048, 1152
ANCHO, ALTO = nova_fondo.ANCHO, nova_fondo.ALTO

VERSION = 1
SEMILLAS = {1: 20260918, 2: 20260919, 3: 20260920}


def _elipse(cy, cx, ra, rb, giro, gamma=2.0):
    """Caida eliptica inclinada, en [0,1]. Para discos y galaxias."""
    y, x = np.mgrid[0:ALTO, 0:ANCHO]
    co, si = math.cos(giro), math.sin(giro)
    u = ((x - cx) * co + (y - cy) * si) / max(ra, 1e-6)
    v = (-(x - cx) * si + (y - cy) * co) / max(rb, 1e-6)
    return np.clip(1.0 - np.sqrt(u * u + v * v), 0.0, 1.0) ** gamma


# --- Seccion 1: Sistema Interior -------------------------------------------

def seccion1(rng):
    """Lleno, polvoriento y barrido por la luz de un sol que no se ve.

    El sol esta FUERA del encuadre, a la izquierda. Dibujarlo dentro seria
    meter en el tablero un objeto mas brillante que cualquier Estrella del
    juego, y la Estrella es el nodo que el jugador tiene que encontrar
    primero. Lo unico que entra en la imagen es su luz, que ademas explica
    por que este cielo tiene polvo y por que hay menos estrellas visibles a
    la izquierda: el resplandor las lava.
    """
    img = lienzo()
    ramp = rampa('bg_s1')
    vin = vineta(k=1.25, suelo=0.30)

    # resplandor del sol fuera de cuadro. Se lleva la mitad del presupuesto
    # porque cubre media imagen; lo que cubre poco puede permitirse mas.
    sol = disco(ALTO * 0.44, -320, 1620, gamma=2.3)
    tramar(img, sol * (0.45 + 0.55 * vin), ramp,
           [(6, 0.00, 0.46), (5, 0.52, 0.24), (4, 0.86, 0.10)])

    # luz zodiacal: el polvo del plano del sistema, alineado con el sol
    polvo = banda(math.radians(-6.5), ALTO * 0.47, 210, comba=-90)
    polvo *= 0.30 + 0.70 * ruido(rng, celdas=3, octavas=4)
    polvo *= np.clip(1.35 - np.linspace(0, 1, ANCHO)[None, :] * 1.05, 0, 1)
    tramar(img, polvo, ramp, [(6, 0.10, 0.34), (5, 0.55, 0.24), (4, 0.84, 0.16)])

    # jirones sueltos lejos del sol, para que la derecha no quede muerta
    jir = ruido(rng, celdas=5, octavas=4, cresta=True) ** 2.8
    jir *= np.clip(np.linspace(-0.35, 1.0, ANCHO)[None, :], 0, 1) * vin
    tramar(img, jir, ramp, [(6, 0.22, 0.24), (5, 0.72, 0.14)])

    # el resplandor lava las estrellas cercanas al sol
    estrellas(img, rng, 3200, rampa('neutral'),
              factor=np.clip(1.0 - sol * 2.4, 0.05, 1.0), brillo=0.85)

    # un planeta interior a contraluz: solo se le ve el filo, y del lado del sol
    y, x = np.mgrid[0:ALTO, 0:ANCHO]
    pl = _elipse(ALTO * 0.76, ANCHO * 0.84, 138, 138, 0.0, gamma=0.42)
    hacia = (x - ANCHO * 0.84) * -1.0 + (y - ALTO * 0.76) * -0.34 > 22
    silueta(img, pl, ramp, 5, hacia=hacia, umbral=0.02, filo=0.34)
    # el canto mas fino y mas claro, pegado al limbo: es lo que le da
    # volumen a una esfera de la que solo se ve el borde
    img[(pl > 0.02) & (pl < 0.14) & hacia] = ramp[3]
    return img


# --- Seccion 2: El Cinturon -------------------------------------------------

def seccion2(rng):
    """Una corriente de escombro cruza el cielo y tapa lo que hay detras.

    El arco esta COMBADO a proposito. Una banda recta de esquina a esquina
    pasa por el centro de la imagen, que es justo donde se juega; combada,
    sube por el tercio superior, cae por los lados y deja la mitad de abajo
    limpia. La composicion sale de la regla, no al reves.

    Y el cinturon OCULTA ESTRELLAS. Es lo unico que hace que se lea como
    materia y no como una mancha clara: el polvo que se ve es el que
    devuelve luz, y detras no se ve nada.
    """
    img = lienzo()
    ramp = rampa('bg_s2')
    vin = vineta(k=1.45, suelo=0.22)

    arco = banda(math.radians(-11.0), ALTO * 0.17, 175, comba=430)
    grano = ruido(rng, celdas=7, octavas=5, persistencia=0.6)

    # estrellas primero: el cinturon se dibuja encima y las tapa
    estrellas(img, rng, 2600, rampa('neutral'),
              factor=np.clip(1.0 - arco * 1.5, 0.0, 1.0), brillo=0.75)

    tramar(img, arco * (0.25 + 0.75 * grano) * (0.55 + 0.45 * vin), ramp,
           [(6, 0.00, 0.58), (5, 0.42, 0.32), (4, 0.78, 0.18)])

    # escombro fino. Reparto plano, no por ruido suave: con ruido suave los
    # granos salen agrupados en manchas y se leen como cumulos lejanos, que
    # es exactamente lo que este cielo no tiene.
    chispa = (arco > 0.18) & (rng.random((ALTO, ANCHO)) < 0.030)
    img[chispa] = ramp[4]
    brasa = (arco > 0.45) & (rng.random((ALTO, ANCHO)) < 0.004)
    img[brasa] = ramp[3]

    # unas pocas rocas grandes, en silueta y con el filo a la luz
    y, x = np.mgrid[0:ALTO, 0:ANCHO]
    for k in range(14):
        t = (k + 0.5) / 14.0
        cx = ANCHO * (0.05 + 0.90 * t)
        cy = (ALTO * 0.17 + math.tan(math.radians(-11.0)) * (cx - ANCHO / 2)
              + 430 * ((cx - ANCHO / 2) / (ANCHO / 2)) ** 2
              + (rng.random() - 0.5) * 210)
        r = 4 + rng.random() * 6   # 8-20 px: grava, muy por debajo de 0.6 u
        roca = _elipse(cy, cx, r * (0.75 + 0.6 * rng.random()), r,
                       rng.random() * 3.1, gamma=0.35)
        silueta(img, roca, ramp, 4,
                hacia=(x - cx) * -0.6 + (y - cy) * -0.8 > 0,
                umbral=0.02, filo=0.34)
    return img


# --- Seccion 3: Espacio Profundo --------------------------------------------

def seccion3(rng):
    """Vacio. Es el unico de los tres cuya idea es la ausencia.

    Aqui el trabajo no es llenar sino resistir la tentacion: media imagen es
    fondo plano. Lo poco que hay -dos velos de filamentos en esquinas
    opuestas y una galaxia del tamano de una una- esta ahi para dar escala, y
    la escala es lo que convierte el vacio en distancia en vez de en un PNG
    sin terminar.
    """
    img = lienzo()
    ramp = rampa('bg_s3')
    vin = vineta(k=1.7, suelo=0.12)

    # velo de filamentos: `cresta` es lo que da hebras en vez de manchas
    velo = ruido(rng, celdas=3, octavas=5, persistencia=0.52, cresta=True) ** 2.9
    velo *= disco(ALTO * 0.16, ANCHO * 0.80, 1150, gamma=1.7) * vin
    tramar(img, velo, ramp, [(6, 0.00, 0.62), (5, 0.42, 0.38), (4, 0.72, 0.24), (3, 0.90, 0.14)])

    velo2 = ruido(rng, celdas=4, octavas=4, persistencia=0.5, cresta=True) ** 3.4
    velo2 *= disco(ALTO * 0.92, ANCHO * 0.14, 900, gamma=2.0) * vin
    tramar(img, velo2, ramp, [(6, 0.00, 0.44), (5, 0.56, 0.24), (4, 0.86, 0.12)])

    estrellas(img, rng, 1900, rampa('neutral'), factor=vin * 0.9 + 0.1,
              brillo=1.0)

    # una galaxia lejana, de canto. Da la escala de toda la seccion.
    gal = _elipse(ALTO * 0.30, ANCHO * 0.17, 128, 26, math.radians(-19), gamma=1.4)
    tramar(img, gal, ramp, [(6, 0.00, 0.85), (5, 0.32, 0.80), (4, 0.58, 0.80),
                            (3, 0.80, 0.85), (2, 0.93, 0.80)])
    return img


SECCIONES = {1: ('Sistema Interior', seccion1),
             2: ('El Cinturon', seccion2),
             3: ('Espacio Profundo', seccion3)}


def main():
    d = os.path.join(RAIZ, 'art', 'bg', 'fondos', f'v{VERSION}')
    os.makedirs(d, exist_ok=True)
    ok = True
    for n, (nombre, fn) in SECCIONES.items():
        img = fn(np.random.default_rng(SEMILLAS[n]))
        Image.fromarray(img, 'RGB').save(os.path.join(d, f'bg_section{n}.png'))
        ok &= informe(f'S{n} {nombre}', img)
    if not ok:
        print('\n  Alguna seccion se pasa del presupuesto: el perfilado de los '
              'nodos dejaria de leerse como borde en esa zona.')


if __name__ == '__main__':
    main()
