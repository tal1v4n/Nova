"""NOVA - Overlay de estado `Asustado` (ART-ST-SCARE), v1: LOS OJOS COMO PLATOS.

Ref: NOVA_Estados_Animaciones.md 14.1, 14.2 y 14.7 - NOVA_Assets_Diseno.md A.3

LA LECTURA MAS IMPORTANTE DEL JUEGO. 14.2 lo dice sin rodeos: `Asustado`
«indica de un vistazo que esta a punto de caer», va sobre CUALQUIER nodo -
propio, enemigo y neutral - y 14.6 lo hace compatible con todo, porque siempre
debe poder avisar. Es tambien el unico overlay de A.3 que se dibuja SOBRE el
cuerpo y no en el hueco de al lado.

QUE ES ESTE ASSET Y QUE NO ES. 14.1 pide «ojos muy abiertos, temblor, salto
corto», pero el temblor y el salto ya estan repartidos: son la CAPA 5 de 14.7,
«sacudida de transform», y la resuelve Unity moviendo el nodo entero. Hornear
la sacudida en la hoja seria pagarla dos veces y ademas dejaria el hitbox
temblando. Aqui se dibuja SOLO el ojo.

EL PROBLEMA ES EL ANCLAJE, NO EL DIBUJO. Un ojo asustado es facil; que caiga
encima de los ojos de cuatro nodos distintos, no. Medido sobre los 34 sprites
(ver `nova_estados.OJOS`): el centro en x esta practicamente registrado
-47.0 a 47.5-, pero la separacion va de 9.5 a 16.0 px y el ojo mas grande mide
10x10. Ningun overlay de geometria fija aterriza en los 34, y escalarlo esta
prohibido por el contrato: con `Filter Mode: Point` una escala no entera rompe
el pixel-perfect.

LA SALIDA ES LA DEL PROTON (A.4.2): tallas discretas mas desplazamiento
entero. Y con una vuelta de tuerca: EL ASSET ES UN OJO, NO UN PAR. Se
instancia dos veces por nodo, cada una en su ancla, asi que la separacion y la
deriva vertical se resuelven con enteros y sin tocar el sprite. Dos archivos
cubren los catorce nodos.

LA FORMA SALE DE UNA DESIGUALDAD, NO DE UN GUSTO. El ojo tiene que TAPAR el
viejo -si asoma un borde se leen cuatro ojos- y no puede FUNDIRSE con su
pareja -dos ojos pegados son un visor, y un visor no tiene expresion-. Con
ojos redondos las dos condiciones se pelean: tapar el de 10 px de la Estrella
N5 pide 10 de ancho, y no fundirse en el Pulsar N1 pide menos de 9.5. Vacio.

    Un ojo asustado es MAS ALTO, NO MAS ANCHO.

El ancho lo raciona la separacion; la altura no la raciona nada. Con elipses
altas -6x8 y 8x12- dos formas cubren los catorce, tapan siempre y abren el ojo
al menos 2 px en todos. La restriccion eligio la forma, y resulta que la forma
que la restriccion elige es la correcta: nadie abre los ojos a lo ancho.

DE QUE ESTA HECHO. `esferoide` para la esclerotica, con `calor` alto para que
la rampa `ojo` -que es oscura- se vaya al extremo claro; `esferoide` otra vez,
pequeno y con `apagado`, para la pupila. Mismo material que los ojos normales
de los cuatro nodos, porque es el MISMO OJO abierto de golpe, no un adorno
pegado encima.

LA PUPILA VA PEQUENA Y BAJA. Pequena porque una pupila contraida dentro de
mucha esclerotica es la señal universal de sobresalto; baja porque el ojo se
abre hacia arriba -eso es lo que hace el parpado- y dejar la pupila centrada
en una elipse alta la convierte en un ojo de muñeco, sorprendido pero no
asustado.

SIN ANIMACION, Y A PROPOSITO. Este es un asset de UN FRAME por talla. El
estado ya se mueve: lo sacude el transform de la capa 5. Un bucle horneado
encima competiria con esa sacudida en vez de sumarse, y 16 frames por talla se
pagan en atlas. Es el mismo criterio que puso el idle del Asteroide y el del
Magnetar en transform: el bucle se paga cuando el movimiento dice algo que la
quietud no dice. Ver la v2, que sostiene lo contrario.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

from nova_core import a_rgba, dir_estado, esferoide, lienzo, rampa
from nova_ojos import OJOS, TALLAS, verifica_ojos  # descartado: T6-12

ESTADO = 'scare'
VERSION = 1
FRAMES = 1          # estatico: la sacudida es transform (14.7, capa 5)
CELDA = 96

CALOR = 4.2         # la rampa `ojo` es oscura; el susto la lleva al extremo
                    # claro sin cambiar de rampa. Es el mismo ojo, abierto.
PUPILA_W = 0.20     # del ancho del ojo
PUPILA_H = 0.20     # del alto
PUPILA_BAJA = 0.10  # del alto, hacia abajo: el ojo se abre hacia arriba


def _ojo(lz, cx, cy, talla, dx=0.0, dy=0.0):
    """Un ojo asustado: esclerotica clara y pupila contraida."""
    w, h = TALLAS[talla]
    ramp = rampa('ojo')
    esferoide(lz, cx, cy, w / 2.0, h / 2.0, 0.0, ramp, 90, calor=CALOR)
    esferoide(lz, cx + dx, cy + h * PUPILA_BAJA + dy,
              max(1.4, w * PUPILA_W), max(1.6, h * PUPILA_H), 0.0,
              ramp, 91, apagado=2.2)


def ojo(talla, t=0.0):
    """El asset: UN ojo, centrado en la celda. Se instancia dos veces."""
    lz = lienzo(CELDA)
    _ojo(lz, CELDA / 2.0, CELDA / 2.0, talla)
    return lz


def par(talla, ancla_izq, ancla_der, t=0.0):
    """El par ya colocado sobre un nodo. Solo para verificar y previsualizar.

    No se exporta: lo que se exporta es el ojo suelto, y el par lo monta
    Unity con los dos desplazamientos enteros de `nova_estados.OJOS`.
    """
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

    print('ART-ST-SCARE v1 «Los ojos como platos»')
    print(f'  {len(TALLAS)} archivos de 1 frame, {len(OJOS)} nodos cubiertos')
    if not verifica_ojos(par, FRAMES):
        raise SystemExit('alguna comprobacion ha fallado')


if __name__ == '__main__':
    main()
