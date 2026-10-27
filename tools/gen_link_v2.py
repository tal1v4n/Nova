"""NOVA - ART-WLD-LINK v2: «El Hilo Vivo», plasma electrico.

El mismo dibujo que la v1 (`gen_link_v1.py`) con otra rampa y una vaina mas
ancha, pedida por el usuario el 2026-10-26: «algo tipo plasma». Un filamento
de plasma no es de un solo tono: el canal esta blanco de calor y la vaina que
lo envuelve se enfria hacia el azul y el violeta. Aqui el nucleo y los
paquetes van en blanco, el resplandor denso en el cian del Jugador y la vaina
—mas ancha que en la v1, para que se vea— en azul electrico y anil.

Sigue siendo del Jugador: `#3FD7F5` esta literal en el indice 3 (regla 1 de
la paleta), y el azul y el anil de los indices 4 y 5 se quedan entre 236 y
247 grados, en el lado azul, lejos del morado puro de `gusano` (`#9B5CF6`,
~263), que el contrato reserva al Agujero de Gusano.
"""

import numpy as np

import gen_link_v1 as base
from nova_paleta import RAMPAS

VERSION = 2

# Elegida el 2026-10-26: la rampa vive en la paleta, `plasma_p`.
PLASMA_P = RAMPAS['plasma_p']

# Vaina mas ancha, y la banda del cian solo pegada al hilo.
CAIDA = 2.3
TRAMA = ((0.72, 3), (0.42, 4), (0.0, 5))


def rampa(hexes):
    return np.array([[int(h[i:i + 2], 16) for i in (1, 3, 5)] for h in hexes], np.uint8)


def aplicar(hexes):
    base.RAMP = rampa(hexes)
    base.CAIDA = CAIDA
    base.TRAMA = TRAMA


if __name__ == '__main__':
    aplicar(PLASMA_P)
    base.generar(VERSION)
