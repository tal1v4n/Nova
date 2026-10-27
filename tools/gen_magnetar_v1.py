"""NOVA - Generador del Magnetar, propuesta v1: EL YUNQUE.

Ref: NOVA_Nodos.md 4.4 · NOVA_Assets_Diseno.md A.2.4

DISENO DESDE CERO. A.7 daba por hecho que este nodo heredaria `anillo` y
`anillo_orbital`. No hereda nada: esas dos primitivas son la voz del Pulsar,
y un nodo que se construye con las piezas de otro se parece a otro. El
Magnetar estrena material, silueta, composicion y escalera de nivel.

EL REPARTO DE MATERIALES. Con este nodo el juego cierra una particion limpia
que no existia antes:

    Estrella   luz continua      corona solida + halo, degradado por pixel
    Pulsar     luz tramada       conos y frentes sobre la capa `glow`
    Magnetar   MATERIA           ni un solo pixel de glow en todo el sprite

Es el nodo denso, el de resistencia x2, el unico que no irradia. Su silueta
y su hitbox son exactamente la misma figura: no hay un halo que prometa area
que no se puede clicar. Para el nodo defensivo del juego, eso es la lectura
correcta.

LA IDEA. Un magnetar es una estrella de neutrones con el campo magnetico mas
intenso del universo: tan intenso que le agrieta la corteza. Este es un
cuerpo COLAPSADO Y TALLADO, de caras planas y aristas duras, partido por
vetas incandescentes -sus temblores de estrella-, y a su alrededor flotan
las ESQUIRLAS que su campo ha arrancado a lo que paso cerca. No irradia:
atrapa. El sprite ensena los restos de lo que ya cazo.

                        Estrella        Pulsar v2        Magnetar v1
    Silueta             disco con puas  reloj de arena   cumulo: cuerpo + satelites
    Simetria            radial n-fold   bilateral, eje   ninguna: angulo aureo
    Material apendice   banda solida    luz tramada      materia facetada
    Composicion         isotropica      direccional      dispersa, acumulativa
    Cuerpo              esfera          esferoide        poliedro de caras planas
    Contador de nivel   puas radiales   frentes de onda  esquirlas capturadas

LA ESCALERA DE NIVEL ES ADITIVA Y POSICIONAL, que es la unica que no tiene
escalon debil. La esquirla `i` esta SIEMPRE en el mismo angulo y a la misma
distancia: el nivel N ensena las esquirlas 0..N-1, asi que un N4 es un N3
con una pieza mas en un sitio que antes estaba vacio. Dos niveles contiguos
se diferencian por PRESENCIA, no por grado. La Estrella resuelve su nivel
por grado y por eso su escalon N3->N4 es el punto debil reconocido en A.2.2;
aqui ese fallo no puede darse.

Y LA DISTANCIA NO SE INVENTA: SE HEREDA DEL BALANCE. La esquirla `i` orbita
a la distancia que le corresponde al radio de alcance del nivel `i+1` segun
la tabla de 4.4 (2.0 / 2.4 / 2.8 / 3.2 / 3.6 u), comprimida para caber en la
celda. La esquirla mas externa marca SIEMPRE el alcance del nivel actual, y
la correlacion "nivel <-> radio" que pide A.2.4 deja de ser una promesa del
documento para ser una consecuencia aritmetica del generador.

EL ANGULO AUREO decide DONDE esta cada esquirla. No es adorno: cualquier
reparto regular (360/N) devuelve simetria radial n-fold, que es la firma de
la Estrella, y ademas mueve todas las piezas al subir de nivel y rompe la
aditividad. El angulo aureo no cierra nunca, reparte bien para cualquier N y
deja fija cada pieza. Es filotaxis, el reparto de las semillas de un girasol.

EL CAMPO DIPOLAR decide COMO ESTA GIRADA cada esquirla, y es el corazon del
diseno. Las esquirlas son alargadas y cada una se orienta segun la direccion
real del campo magnetico de un dipolo en el punto donde flota:

    B = (2 cos f, sin f)  en la base (radial, tangencial), f = angulo al eje

o sea, giro = angulo_de_posicion + atan2(sin f, 2 cos f). No es una formula
decorativa: es el campo dipolar, y el dibujo que sale es el de las limaduras
de hierro sobre un iman, que es la imagen universal del magnetismo. Sin esto
el nodo es una piedra con cascotes alrededor y se pisa con el Asteroide; con
esto, LA POSICION DE LA MATERIA DIBUJA EL CAMPO QUE LA SUJETA. El sprite no
ensena un aura: ensena el efecto del aura sobre cosas reales.

EL EJE ES VERTICAL, a proposito. La diagonal ya es del Pulsar y la isotropia
de la Estrella. Vertical es el tercer valor libre, y ademas un eje vertical
con materia repartida sin simetria es exactamente lo que no tiene ninguno de
los otros dos.

IDLE POR TRANSFORM. Decision cerrada del contrato: Asteroide y Magnetar no
generan hoja. `t` existe en la firma solo para que el previsualizador pueda
tratar a los cuatro nodos igual.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from nova_core import (L, a_rgba, dir_nodo, esfera, exporta_huellas, guardar,
                       huella_px, lienzo, poliedro, rampa, veta)

NODO = 'magnetar'
VERSION = 1
IDLE = False        # idle por transform: no hay hoja de frames que juzgar
FACCIONES = ('neutral', 'player', 'enemy')   # el neutral existe y dispara (3.2)

CELDA = 96
C = CELDA / 2.0
PPU = 64.0

# Radio de alcance por nivel, en unidades de mundo. Copiado de 4.4: es dato
# de balance, no de arte, y por eso se cita en vez de reescribirse.
RADIO_U = {1: 2.0, 2: 2.4, 3: 2.8, 4: 3.2, 5: 3.6}

# Compresion del radio a pixeles de celda. 3.6 u a PPU 64 son 230 px de
# radio real: el alcance de verdad lo dibuja ART-WLD-RAD-* (A.4.1), no el
# sprite. Aqui solo se conserva el ORDEN y la proporcion del escalon.
ORBITA_0 = 25.5
ORBITA_K = 5.4

EJE = -90.0         # eje del dipolo: vertical. La diagonal es del Pulsar.

# Silueta del cuerpo: (angulo en grados, factor de radio). SIETE vertices,
# ninguno equiespaciado y ninguno con el mismo radio.
#
# Siete y no nueve. Con nueve vertices sobre un cuerpo de 37 px el poligono
# se convierte en una circunferencia: las caras salen tan estrechas que dos
# contiguas caen en la misma banda, los pliegues desaparecen y el nodo se lee
# como un canto rodado -justo lo que tiene que ser el Asteroide, y lo que
# este no puede ser-. Con siete, cada cara mide 16 px de lado y el talle se
# ve. La regla es la misma que gobierna las bandas de luz: la redondez se
# elige, y aqui se elige NO tenerla.
SILUETA = ((-100, 0.93), (-45, 0.86), (5, 1.00), (55, 0.84), (118, 0.97),
           (175, 0.88), (235, 0.95))
ANCHO = 0.94        # rx = radio * ANCHO, ry = radio
MESA = 0.58         # fraccion del cuerpo que ocupa la cara frontal plana

# Esquirla: cinco vertices, tallada igual que el cuerpo pero en pequeno.
# ALARGADA. Una esquirla redonda es un cascote y su giro no se ve; una
# alargada tiene direccion, y la direccion es toda la informacion aqui.
ESQUIRLA = ((-90, 1.00), (-18, 0.74), (44, 0.95), (132, 0.80), (208, 0.90))
ESQ_LARGO = 1.34    # semiejes de la esquirla, en multiplos de ESQ_R.
ESQ_ANCHO = 0.72    # Mas fina que esto deja de ser materia y parece un guion.

ANG_0 = -58.0       # de donde arranca la espiral
ANG_PASO = 137.507  # angulo aureo

# Tamano y giro propios de cada esquirla. Fijos: dos partidas distintas
# deben ensenar exactamente el mismo Magnetar N3.
# El giro NO esta en esta tabla: lo dicta el campo. Aqui solo el tamano.
ESQ_R = (5.7, 4.9, 6.2, 5.2, 6.0)

# radio  semieje del cuerpo (ry)    vetas  cuantas fracturas
# banda  tono de la veta: 2 templado en N1, 1 incandescente en N5
NIVELES = {
    1: dict(radio=15.0, vetas=1, banda=2),
    2: dict(radio=15.9, vetas=2, banda=2),
    3: dict(radio=16.8, vetas=2, banda=1),
    4: dict(radio=17.7, vetas=3, banda=1),
    5: dict(radio=18.6, vetas=3, banda=1),
}


def _perfil(silueta, rx, ry, giro=0.0):
    """Convierte (angulo, factor) en desplazamientos (dx, dy) de vertice.

    El giro se aplica al PUNTO ya escalado, no al angulo del parametro.
    Sumandolo al angulo, los semiejes rx/ry siguen pegados a los ejes de
    pantalla: la figura se deforma pero no gira, y una esquirla alargada sale
    horizontal diga lo que diga el campo.
    """
    co, si = math.cos(math.radians(giro)), math.sin(math.radians(giro))
    out = []
    for ang, f in silueta:
        a = math.radians(ang)
        x, y = math.cos(a) * rx * f, math.sin(a) * ry * f
        out.append((x * co - y * si, x * si + y * co))
    return out


def _orbita(i):
    """Distancia de la esquirla `i`, derivada del radio de alcance de 4.4."""
    return ORBITA_0 + ORBITA_K * (RADIO_U[i + 1] - RADIO_U[1])


def _giro_campo(ang_pos):
    """Direccion del campo dipolar en un punto, en grados de pantalla.

    En la base (radial, tangencial) de un dipolo, B = (2 cos f, sin f) con
    `f` el angulo al eje. En el polo sale axial; en el ecuador, antiparalela
    al momento; en medio, tumbada. Una esquirla alineada con esa direccion
    es una limadura de hierro, y N limaduras dibujan el campo entero.
    """
    f = math.radians(ang_pos - EJE)
    return ang_pos + math.degrees(math.atan2(math.sin(f), 2.0 * math.cos(f)))


def _esquirla(lz, i, ramp, oid):
    """Una pieza de materia capturada, en su sitio y en su giro de siempre."""
    ang = ANG_0 + ANG_PASO * i
    a = math.radians(ang)
    d = _orbita(i)
    cx, cy = C + math.cos(a) * d, C + math.sin(a) * d
    r = ESQ_R[i]
    poliedro(lz, cx, cy,
             _perfil(ESQUIRLA, r * ESQ_LARGO, r * ESQ_ANCHO, _giro_campo(ang)),
             ramp, oid=oid, mesa=0.52, especular=False)


def _vetas(lz, verts, ramp, cfg, mesa, oid):
    """Fracturas de corteza: temblores de estrella.

    Dos decisiones, y ninguna es cosmetica.

    VAN SOBRE EL PLIEGUE, no cruzando una cara. Una grieta que recorre la
    arista entre dos caras se lee como corteza partida por donde debia
    partirse; una que cruza una cara plana por el medio se lee como una raya
    pintada encima, y eso fue lo que paso en la primera pasada.

    VAN EN EL LADO EN SOMBRA. Una linea clara sobre una cara clara no se ve;
    sobre una cara oscura se lee como luz que sale de dentro, que es lo que
    es. El lado se elige con la L global, no a mano, asi que si algun dia
    cambiara el vector de luz las grietas se mudarian solas.
    """
    orden = sorted(range(len(verts)),
                   key=lambda i: -(verts[i][0] * L[0] + verts[i][1] * L[1]))
    for k in range(cfg['vetas']):
        v = verts[orden[k]]
        p0 = (C + v[0] * mesa, C + v[1] * mesa)
        p1 = (C + v[0] * 0.98, C + v[1] * 0.98)
        veta(lz, p0, p1, 0.90, ramp, cfg['banda'], oid=oid + k)


def magnetar(nivel, faccion, t=0.0):
    """Devuelve el lienzo de un Magnetar v1. `t` no se usa: idle por transform."""
    cfg = NIVELES[nivel]
    ramp = rampa({'player': 'mag_p', 'enemy': 'mag_e'}.get(faccion, 'mag_n'))
    ojo = rampa('ojo')

    lz = lienzo(CELDA)
    ry = cfg['radio']
    rx = ry * ANCHO
    verts = _perfil(SILUETA, rx, ry)

    # 1. esquirlas: el contador de nivel. Fuera del cuerpo, nunca lo tapan.
    for i in range(nivel):
        _esquirla(lz, i, ramp, oid=40 + i)

    # 2. cuerpo tallado. La mesa es grande a proposito: con una mesa pequena
    #    las caras del talle son enormes y el especular se come media cara.
    #    Grande, el talle es un anillo de facetas estrechas -una piedra
    #    tallada- y el brillo cae en una sola esquirla de cara.
    poliedro(lz, C, C, verts, ramp, oid=10, mesa=MESA, sesgo_mesa=-1)

    # 3. vetas incandescentes sobre la corteza ya tallada
    _vetas(lz, verts, ramp, cfg, MESA, oid=20)

    # 4. ojos, los ultimos por encima de todo (14.7). Van sobre la mesa: la
    #    cara plana del frente es literalmente la cara del bicho.
    ro = max(3.0, ry * 0.235)
    sep = rx * 0.42
    for signo in (-1, 1):
        esfera(lz, C + sep * signo, C - ry * 0.13, ro, ojo, oid=30 + signo)

    return lz


def main():
    raiz = dir_nodo(NODO, VERSION)
    for nivel in NIVELES:
        for fac in ('neutral', 'player', 'enemy'):
            guardar(magnetar(nivel, fac),
                    os.path.join(raiz, f'mag_lvl{nivel}_{fac}.png'))

    print(f"{'':4} {'huella':>9}  (el Magnetar no tiene glow: solida = total)")
    huellas = {}
    for nivel in NIVELES:
        lz = magnetar(nivel, 'player')
        a = a_rgba(lz)[..., 3] > 0
        if lz['glow'].any():
            print(f'  AVISO: N{nivel} ha ensuciado la capa glow')
        if a[0, :].any() or a[-1, :].any() or a[:, 0].any() or a[:, -1].any():
            print(f'  AVISO: N{nivel} toca el borde de la celda')
        huellas[nivel] = huella_px(a)
        print(f'N{nivel}: {huellas[nivel] / PPU:8.2f}u')
    exporta_huellas(raiz, 'mag', huellas)


if __name__ == '__main__':
    main()
