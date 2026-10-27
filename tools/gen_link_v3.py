"""NOVA - ART-WLD-LINK v3: «El Hilo Vivo», globo de plasma.

El mismo dibujo que la v1 (`gen_link_v1.py`) con el color de un globo de
plasma de verdad: nucleo blanco rosado, magenta y una vaina violeta. Pedida
por el usuario el 2026-10-26 («algo tipo plasma») como la variante literal.

ROMPE TRES REGLAS, y por eso va aparte de la v2:
  - la linea deja de ser del color del Jugador (14.9: «rampa del Jugador»);
  - el violeta se cruza con `gusano` (`#9B5CF6`), el unico morado del juego,
    reservado al Agujero de Gusano (NOVA_Arte_Tecnica.md, Paleta);
  - el rosa se cruza con `hidrogeno` (`#FF8FD0`), el power-up de la Nube de
    Hidrogeno.
Elegirla obliga a decidir antes que pasa con esas tres reglas.
"""

import gen_link_v1 as base
from gen_link_v2 import aplicar

VERSION = 3

GLOBO = ('#FFFFFF', '#FFEFFF', '#FFB3F2', '#F06AE8', '#B24DF0', '#6B33C4', '#321A66')


if __name__ == '__main__':
    aplicar(GLOBO)            # la misma vaina ancha que la v2
    base.generar(VERSION)
