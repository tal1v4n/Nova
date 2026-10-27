"""NOVA - Generador de la Estrella (ART-NOD-STR-01..05).

Ref: NOVA_Nodos.md 4.2 · NOVA_Assets_Diseno.md A.2.2

DIRECCION. La Estrella es lo unico que genera Energia en todo el juego, asi
que tiene que leerse como algo que arde. Pero una corona de lenguas
equiespaciadas es invariante bajo rotacion y el ojo la lee como engranaje.
La solucion no es quitar la corona: es romperle la simetria con cizalla
espiral y partirla en dos capas de densidad distinta.

Anatomia, en orden de dibujo:

    1. halo          trama Bayer alrededor de la corona, solo N5
    2. corona        pocas lenguas, largas, oscuras, muy cizalladas
    3. cromosfera    muchas lenguas, cortas, claras, poco cizalladas
    4. cuerpo        esfera de faccion; `calor` blanquea el nucleo
    5. ojos          dos esferas oscuras, siempre por encima de todo

Las dos capas llevan fase y cizalla distintas a proposito: si coincidieran
se sumarian en las mismas lenguas y volveria el engranaje, solo que mas
gordo.

HUELLA. La huella solida (cuerpo + corona) respeta el rango de
0.6 u a 1.2 u acordado para el nodo, con celda de 96 px = 1.5 u (PPU 64).
El halo es resplandor y puede salirse de ese rango: no es superficie y no
participa del hitbox de clic.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from nova_core import (a_rgba, dir_nodo, corona_radial, esfera, exporta_huellas, guardar,
                       halo_trama, huella_px, lienzo, rampa)

NODO = 'estrella'
VERSION = 1

CELDA = 96
C = CELDA / 2.0
PPU = 64.0

# r        radio del disco
# calor    aclarado radial del nucleo (0 = frio, 3.0 = nucleo blanco)
# apagado  oscurecimiento uniforme (enana apagada)
# cro      alcance de la cromosfera por encima del limbo, en px
# cor      alcance de la corona por encima del limbo, en px
# halo     grosor de la trama exterior, o None

NIVELES = {
    1: dict(r=17, calor=0.0, apagado=1.0, cro=3, cor=0,  halo=None),
    2: dict(r=19, calor=0.7, apagado=0.0, cro=4, cor=8,  halo=None),
    3: dict(r=21, calor=1.5, apagado=0.0, cro=5, cor=11, halo=None),
    4: dict(r=23, calor=2.3, apagado=0.0, cro=6, cor=13, halo=None),
    5: dict(r=25, calor=3.0, apagado=0.0, cro=7, cor=13, halo=7),
}

# picos, amp, dureza, cizalla, sesgo de banda
CROMOSFERA = dict(picos=15, amp=0.70, dureza=1.10, cizalla=0.30, sesgo_banda=-1)
CORONA     = dict(picos=6,  amp=0.94, dureza=1.15, cizalla=0.75, sesgo_banda=0)


def estrella(nivel, faccion, t=0.0):
    """Devuelve el lienzo de una Estrella. `t` en [0,1) para animar."""
    cfg = NIVELES[nivel]
    suf = 'p' if faccion == 'player' else 'e'
    cuerpo = rampa(f'str_{suf}')
    fuego  = rampa(f'cor_{suf}')
    ojo    = rampa('ojo')

    lz = lienzo(CELDA)
    r = cfg['r']
    fase = 2 * math.pi * t

    perfil_cor = dict(r_int=r - 1, r_ext=r + max(cfg['cor'], 1), **CORONA)

    if cfg['halo']:
        halo_trama(lz, C, C, fuego, grosor=cfg['halo'], banda_int=3,
                   banda_ext=5, gamma=1.6, corona=perfil_cor, fase=fase)

    # corona: pocas lenguas, largas y barridas. cor=0 la salta: una enana
    # apagada con una corona de 2 px solo produce pelusa sucia en el borde.
    apag = int(cfg['apagado'])
    if cfg['cor']:
        corona_radial(lz, C, C, r - 1, r + cfg['cor'], fase=fase, ramp=fuego,
                      oid=1, **dict(CORONA, sesgo_banda=CORONA['sesgo_banda'] + apag))

    # cromosfera: muchas lenguas, cortas y casi rectas, una banda mas clara.
    # Fase desplazada media longitud de onda para que no se solapen con las
    # de la corona y vuelva a aparecer un patron regular.
    corona_radial(lz, C, C, r - 1, r + cfg['cro'],
                  fase=fase + math.pi / CROMOSFERA['picos'], ramp=fuego, oid=2,
                  **dict(CROMOSFERA, sesgo_banda=CROMOSFERA['sesgo_banda'] + apag))

    esfera(lz, C, C, r, cuerpo, oid=10,
           calor=cfg['calor'], apagado=cfg['apagado'])

    ro = max(3.2, r * 0.215)
    dx = r * 0.335
    dy = -r * 0.24
    esfera(lz, C - dx, C + dy, ro, ojo, oid=20)
    esfera(lz, C + dx, C + dy, ro, ojo, oid=21)

    return lz


def main():
    raiz = dir_nodo(NODO, VERSION)
    for nivel in NIVELES:
        for fac in ('player', 'enemy'):
            guardar(estrella(nivel, fac),
                    os.path.join(raiz, f'str_lvl{nivel}_{fac}.png'))

    print(f"{'':4} {'solida':>9} {'con halo':>10}")
    huellas = {}
    for nivel in NIVELES:
        lz = estrella(nivel, 'player')
        solido = lz['lleno'].copy()
        a = a_rgba(lz)[..., 3] > 0
        if a[0, :].any() or a[-1, :].any() or a[:, 0].any() or a[:, -1].any():
            print(f'  AVISO: N{nivel} toca el borde de la celda')
        huellas[nivel] = huella_px(solido)
        print(f'N{nivel}: {huellas[nivel] / PPU:8.2f}u {huella_px(a) / PPU:9.2f}u')
    exporta_huellas(raiz, 'str', huellas)


if __name__ == '__main__':
    main()
