"""NOVA - Generador del Asteroide, propuesta v1: EL DURMIENTE.

Ref: NOVA_Nodos.md 4.1 · NOVA_Assets_Diseno.md A.2.1

DISENO DESDE CERO. No toma prestada ni una pieza de los otros tres: ni
`corona_radial` ni `halo_trama` (Estrella), ni `cono_trama` ni
`anillo_orbital` (Pulsar), ni `linea_dipolar` (Magnetar). Estrena contorno,
material y composicion, y para eso estrena dos primitivas.

LA TESIS. El Asteroide es el unico nodo de NOVA que NO ENSENA NI UN PIXEL DE
LUZ PROPIA. Los otros tres emiten: corona, conos, campo incandescente. Este
solo recibe. Su pixel mas claro es un reflejo. No es una decision de estilo:
es la unica que dice de un vistazo lo que 4.1 pide que se lea -"no genera,
no dispara, no envia, capital muerto"- y lo dice sin un solo simbolo.

De ahi sale todo lo demas:

    Estrella   luz continua        Magnetar   materia que irradia
    Pulsar     luz tramada         Asteroide  MATERIA QUE SOLO REFLEJA

EL EJE LIBRE ERA LA CONCAVIDAD. Todo el reparto es convexo: cuerpos que
abultan. Ninguna pieza del juego se hunde. Un crater es una esfera con la
normal invertida -misma L, mismas bandas, un signo-, y se ilumina la pared
del fondo en vez de la de delante. Es por lo que una foto de la Luna girada
180 grados convierte los crateres en cupulas. Nada mas del juego hace eso, y
es lo que separa a este nodo de los otros tres sin tocar la paleta.

                        los otros tres           Asteroide
    Silueta             disco / reloj / cruz      contorno organico irregular
    Contorno            circulo, elipse, arista   armonicos sobre el angulo
    Superficie          lisa o facetada           PICADA: concava
    Simetria            radial / bilateral / doble ninguna
    Composicion         isotropica / dirigida /   excentrica: nada cae
                        anidada                   donde se espera
    Luz propia          si                        NINGUNA
    Idle                hoja de 16 frames         transform

SIN VALOR APARENTE, LITERALMENTE. 4.1 pide "roca muerta, dormida, sin valor
aparente", y "aparente" es la palabra que manda: la partida entera se apoya
en que el jugador pague 10 por capturarlo y otros 10 por encenderlo sin que
el objeto le prometa nada. Asi que aqui no hay veta incandescente, ni nucleo
que se transparente, ni chispa. Hay una piedra apaleada. La promesa la hace
el menu de encendido, no el sprite.

SIN CARA. Los nodos no tienen ojos (A.8 #5, cerrada el 2026-11-01; T6-12):
hasta entonces este llevaba dos rendijas cerradas, y se quitaron sin tocar
nada mas. Es la misma roca tanto si es Neutral -estado `Dormido`- como si es
propia -estado `SinEncender`-: en los dos casos es capital inerte.

UN SOLO NIVEL. Es el unico nodo sin escalera, asi que no necesita contador y
todo el trabajo de lectura se va a la faccion. La geometria es identica en
las tres: cambia la rampa y nada mas (14.0 #3).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from nova_core import (a_rgba, bulto, crater, dir_nodo, exporta_huellas,
                       guardar, huella_px, lienzo, rampa, veta)

NODO = 'asteroide'
VERSION = 1
IDLE = False        # unico nodo que sigue resolviendo su idle por transform
FACCIONES = ('neutral', 'player', 'enemy')

CELDA = 96
C = CELDA / 2.0
PPU = 64.0

# El mas pequeno del reparto, en el suelo del rango de huella del contrato
# (0.6-1.2 u). Es nivel 0 y no vale nada: si midiera lo que una Estrella N3,
# el tablero mentiria sobre donde esta el valor. El suelo lo pone el clic, no
# el dibujo.
RADIO = 19.0

# Contorno: armonicos (k, amplitud, fase) sobre el angulo. Tres, con k primos
# entre si y sin fases alineadas, o el contorno vuelve a tener un eje de
# simetria y la piedra parece un huevo. Amplitudes cortas: por encima de 0.12
# aparecen entrantes y el limbo deja de leerse como una roca redondeada.
PERFIL = ((2, 0.085, 0.72), (3, 0.055, 2.15), (5, 0.030, 4.05))

# Crateres: (x, y, radio, hondura). Posiciones en multiplos de RADIO.
# COLOCADOS A MANO Y A PROPOSITO. Un reparto regular devuelve simetria, y uno
# aleatorio reparte mal en cuatro intentos de cada cinco: deja dos pegados y
# media roca vacia. Las reglas que siguen son tres:
#   - ninguno centrado: un hoyo redondo en el centro convierte la roca en
#     una diana. (La franja que dejaban libre a la altura de los ojos se
#     conserva: el reparto no se movio al quitarlos, T6-12)
#   - dos muerden el limbo, que es lo que convierte el contorno en corteza
#     en vez de en recorte
#   - tamanos escalonados, no dos iguales, para que se lean como historia y
#     no como textura repetida
CRATERES = (
    (-0.44,  0.48, 0.23, 1.55),
    ( 0.56,  0.34, 0.17, 1.40),
    ( 0.38, -0.60, 0.13, 1.30),
    (-0.66, -0.30, 0.12, 1.25),
    ( 0.06,  0.74, 0.10, 1.20),
    ( 0.70,  0.06, 0.09, 1.15),
    (-0.22, -0.66, 0.08, 1.10),
)

# Grietas: (angulo, radio inicial, radio final). OSCURAS, no incandescentes:
# son sombra en una hendidura, no luz que sale. La diferencia importa, porque
# una veta clara en una roca dice "aqui dentro hay algo" y este nodo tiene
# prohibido decirlo.
GRIETAS = (
    (118.0, 0.30, 0.96),
    (142.0, 0.52, 0.93),
    (-28.0, 0.44, 0.94),
)

NIVELES = {0: dict()}   # un solo nivel, por firma uniforme con el resto


def asteroide(nivel=0, faccion='neutral', t=0.0):
    """Devuelve el lienzo de un Asteroide v1. `t` no se usa: idle por transform."""
    ramp = rampa({'player': 'roca_p', 'enemy': 'roca_e'}.get(faccion, 'roca_n'))
    lz = lienzo(CELDA)

    # 1. la roca. Apagada y MATE: sin especular. Un punto de brillo la
    #    convertiria en canica pulida, y esto es un pedrusco.
    bulto(lz, C, C, RADIO, ramp, oid=10, perfil=PERFIL, apagado=1.15,
          especular=False)

    # 2. crateres, de mayor a menor. Van sobre la piel del cuerpo y no tocan
    #    `lleno`, asi que no cambian la silueta ni la huella.
    for i, (x, y, rr, hond) in enumerate(CRATERES):
        crater(lz, C + x * RADIO, C + y * RADIO, rr * RADIO, ramp,
               oid_cuerpo=10, hondura=hond, borde=0.18 + 0.04 * (i % 2))

    # 3. hendiduras: banda oscura, nunca clara
    for k, (ang, r0, r1) in enumerate(GRIETAS):
        a = math.radians(ang)
        veta(lz, (C + math.cos(a) * RADIO * r0, C + math.sin(a) * RADIO * r0),
             (C + math.cos(a) * RADIO * r1, C + math.sin(a) * RADIO * r1),
             0.80, ramp, 5, oid=10)

    return lz


def main():
    raiz = dir_nodo(NODO, VERSION)
    for fac in FACCIONES:
        guardar(asteroide(0, fac), os.path.join(raiz, f'ast_lvl0_{fac}.png'))

    lz = asteroide(0, 'neutral')
    a = a_rgba(lz)[..., 3] > 0
    ext = huella_px(a)
    if lz['glow'].any():
        print('  AVISO: ha ensuciado la capa glow')
    if a[0, :].any() or a[-1, :].any() or a[:, 0].any() or a[:, -1].any():
        print('  AVISO: toca el borde de la celda')
    print(f'huella: {ext / PPU:.2f}u   (sin glow: solida = total)')
    exporta_huellas(raiz, 'ast', {0: ext})


if __name__ == '__main__':
    main()
