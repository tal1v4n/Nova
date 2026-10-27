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

IDLE: hoja de 16 frames, «la corona arde en su sitio». Dos movimientos, los
dos cerrados por construccion (contrato, Animacion):

    corona      las lenguas NO giran. Un giro rigido de una corona cizallada
                es un molinillo, y un molinillo es el engranaje que esta
                corona existe para no ser. Lo que se mueve es su LONGITUD:
                cada armonico de `_onda` late con una frecuencia temporal
                entera propia (`K_CORONA`, `K_CROMOSFERA`), asi que las
                lenguas se quedan donde estan y la ola de largo recorre el
                disco. Corona y cromosfera la recorren en sentidos opuestos,
                por la misma razon por la que llevan fase y cizalla distintas.
    cuerpo      respira con la `s` del contrato: alto x s, ancho / sqrt(s),
                volumen conservado. Lo dibuja el generador frame a frame, asi
                que no hay remuestreo; los ojos viajan con el cuerpo.

Frecuencias enteras y `s` periodica: el frame 16 es el 0 pixel a pixel, para
cualquier numero de frames. El frame 0 es el sprite estatico de siempre.

HUELLA. La huella solida (cuerpo + corona) respeta el rango de
0.6 u a 1.2 u acordado para el nodo, con celda de 96 px = 1.5 u (PPU 64).
El halo es resplandor y puede salirse de ese rango: no es superficie y no
participa del hitbox de clic.
"""

import math
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from nova_core import (ARMONICOS, a_rgba, dir_nodo, corona_radial, esfera, esferoide,
                       exporta_huellas, guardar, halo_trama, huella_px, lienzo, rampa)

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

# Idle. Frames del bucle (contrato: los idle son de 16) y amplitud de la
# respiracion, la `s` del contrato.
FRAMES  = 16
RESPIRA = 0.07

# Frecuencia temporal de cada armonico de ARMONICOS (x1, x2, x0.5), en vueltas
# por ciclo. Enteras: es lo que cierra el bucle. El x1 es la lengua y va a 0
# para que no se mueva de sitio; el x0.5 alterna largas y cortas y es la ola
# que recorre el disco; el x2 riza la punta. La cromosfera, al reves.
K_CORONA     = (0, -1, 1)
K_CROMOSFERA = (0, 1, -1)


def _armonicos(k, t):
    """ARMONICOS con su desfase avanzado k vueltas en el instante `t`."""
    return tuple((mult, w, desf + 2 * math.pi * ki * t)
                 for (mult, w, desf), ki in zip(ARMONICOS, k))


def estrella(nivel, faccion, t=0.0):
    """Devuelve el lienzo de una Estrella. `t` en [0,1) para animar."""
    cfg = NIVELES[nivel]
    suf = 'p' if faccion == 'player' else 'e'
    cuerpo = rampa(f'str_{suf}')
    fuego  = rampa(f'cor_{suf}')
    ojo    = rampa('ojo')

    lz = lienzo(CELDA)
    r = cfg['r']
    arm_cor = _armonicos(K_CORONA, t)
    arm_cro = _armonicos(K_CROMOSFERA, t)
    s = 1.0 + RESPIRA * math.sin(2 * math.pi * t)

    perfil_cor = dict(r_int=r - 1, r_ext=r + max(cfg['cor'], 1), **CORONA)

    if cfg['halo']:
        halo_trama(lz, C, C, fuego, grosor=cfg['halo'], banda_int=3,
                   banda_ext=5, gamma=1.6, corona=perfil_cor, armonicos=arm_cor)

    # corona: pocas lenguas, largas y barridas. cor=0 la salta: una enana
    # apagada con una corona de 2 px solo produce pelusa sucia en el borde.
    apag = int(cfg['apagado'])
    if cfg['cor']:
        corona_radial(lz, C, C, r - 1, r + cfg['cor'], fase=0.0, ramp=fuego,
                      oid=1, armonicos=arm_cor,
                      **dict(CORONA, sesgo_banda=CORONA['sesgo_banda'] + apag))

    # cromosfera: muchas lenguas, cortas y casi rectas, una banda mas clara.
    # Fase desplazada media longitud de onda para que no se solapen con las
    # de la corona y vuelva a aparecer un patron regular.
    corona_radial(lz, C, C, r - 1, r + cfg['cro'],
                  fase=math.pi / CROMOSFERA['picos'], ramp=fuego, oid=2,
                  armonicos=arm_cro,
                  **dict(CROMOSFERA, sesgo_banda=CROMOSFERA['sesgo_banda'] + apag))

    # cuerpo que respira: con s = 1 es la esfera de siempre
    esferoide(lz, C, C, r / math.sqrt(s), r * s, 0.0, cuerpo, oid=10,
              calor=cfg['calor'], apagado=cfg['apagado'])

    ro = max(3.2, r * 0.215)
    dx = r * 0.335 / math.sqrt(s)
    dy = -r * 0.24 * s
    esfera(lz, C - dx, C + dy, ro, ojo, oid=20)
    esfera(lz, C + dx, C + dy, ro, ojo, oid=21)

    return lz


def hoja(nivel, faccion):
    """Tira horizontal de FRAMES celdas: ancho = celda x n_frames (contrato)."""
    return np.hstack([a_rgba(estrella(nivel, faccion, t=i / FRAMES))
                      for i in range(FRAMES)])


def main():
    raiz = dir_nodo(NODO, VERSION)
    for nivel in NIVELES:
        for fac in ('player', 'enemy'):
            # el estatico es el frame 0, para UI y hojas de contacto
            guardar(estrella(nivel, fac),
                    os.path.join(raiz, f'str_lvl{nivel}_{fac}.png'))
            Image.fromarray(hoja(nivel, fac), 'RGBA').save(
                os.path.join(raiz, f'str_lvl{nivel}_{fac}_sheet.png'))

    print(f"{'':4} {'solida':>9} {'con halo':>10} {'ciclo':>9}")
    huellas = {}
    for nivel in NIVELES:
        lz = estrella(nivel, 'player')
        solido = lz['lleno'].copy()
        a = a_rgba(lz)[..., 3] > 0
        if a[0, :].any() or a[-1, :].any() or a[:, 0].any() or a[:, -1].any():
            print(f'  AVISO: N{nivel} toca el borde de la celda')
        huellas[nivel] = huella_px(solido)
        # el ciclo entero: la corona late y el cuerpo respira, y el maximo no
        # tiene por que caer en el frame 0
        ciclo = 0
        for i in range(FRAMES):
            f = estrella(nivel, 'player', t=i / FRAMES)
            af = a_rgba(f)[..., 3] > 0
            if af[0, :].any() or af[-1, :].any() or af[:, 0].any() or af[:, -1].any():
                print(f'  AVISO: N{nivel} f{i} toca el borde de la celda')
            ciclo = max(ciclo, huella_px(f['lleno']))
        if not np.array_equal(a_rgba(estrella(nivel, 'player', 0.0)),
                              a_rgba(estrella(nivel, 'player', 1.0))):
            print(f'  AVISO: N{nivel} el bucle no cierra')
        print(f'N{nivel}: {huellas[nivel] / PPU:8.2f}u {huella_px(a) / PPU:9.2f}u '
              f'{ciclo / PPU:8.2f}u')
    exporta_huellas(raiz, 'str', huellas)


if __name__ == '__main__':
    main()
