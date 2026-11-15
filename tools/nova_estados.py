"""NOVA - Utilidades comunes a los overlays de estado de A.3.

No hay primitivas aqui. Un overlay se dibuja con las mismas de `nova_core`
que un nodo; lo que no comparte con un nodo es COMO SE COMPRUEBA.

Un sprite de nodo se valida contra si mismo: que no toque el borde de la
celda, que no ensucie la capa `glow`, que el bucle cierre. Un overlay se
valida ademas contra OTRO asset -el sprite sobre el que se aplica- y, si es un
glifo contable, contra sus propias piezas. Eso es lo que vive aqui, para que
las propuestas de un mismo overlay se midan con la misma vara: medir cada una
a su manera es no compararlas.
"""

import os

import numpy as np
from PIL import Image

from nova_core import RAIZ, a_rgba

# El sprite de nodo por defecto: el Asteroide Neutral, que es sobre el que va
# `Dormido`. Los overlays de otro estado pasan los suyos.
ASTEROIDE = 'nodes/asteroide/v1/ast_lvl0_{}.png'


def sprites(*facciones):
    """Rutas absolutas de los sprites de Asteroide de esas facciones."""
    return [os.path.join(RAIZ, 'art', ASTEROIDE.format(f)) for f in facciones]


def _dilata(m):
    o = m.copy()
    o[1:, :] |= m[:-1, :]
    o[:-1, :] |= m[1:, :]
    o[:, 1:] |= m[:, :-1]
    o[:, :-1] |= m[:, 1:]
    return o


def verifica(fn_lienzo, frames, fn_piezas=None, bases=None, args=()):
    """Las comprobaciones de un overlay, sobre los sprites de verdad.

    `fn_lienzo(t, *args)` devuelve el lienzo del overlay en la fase `t`.
    `fn_piezas(t, *args)`, si se pasa, devuelve las mascaras de cada pieza por
    separado: sirve para exigir que un glifo contable no se funda consigo
    mismo. `bases` son las rutas de los sprites sobre los que se aplica.

    Devuelve True si todo pasa, para que el generador pueda fallar de verdad
    en vez de imprimir un aviso que nadie lee.
    """
    if bases is None:
        bases = sprites('neutral')

    cuerpos = {}
    for b in bases:
        if os.path.exists(b):
            cuerpos[os.path.basename(b)] = np.array(
                Image.open(b).convert('RGBA'))[..., 3] > 0
        else:
            print(f'  AVISO: no encuentro {b}; no compruebo solape')

    pisa = {k: 0 for k in cuerpos}
    borde = sucio = pegados = 0
    for i in range(frames):
        lz = fn_lienzo(i / frames, *args)
        a = a_rgba(lz)[..., 3] > 0
        if lz['glow'].any():
            sucio += 1
        if a[0, :].any() or a[-1, :].any() or a[:, 0].any() or a[:, -1].any():
            borde += 1
        for k, cuerpo in cuerpos.items():
            pisa[k] = max(pisa[k], int((a & cuerpo).sum()))
        if fn_piezas is not None:
            ms = fn_piezas(i / frames, *args)
            for x in range(len(ms)):
                for y in range(x + 1, len(ms)):
                    if (_dilata(ms[x]) & ms[y]).any():
                        pegados += 1

    cierra = np.array_equal(a_rgba(fn_lienzo(0.0, *args)),
                            a_rgba(fn_lienzo(1.0, *args)))
    print(f'  bucle cierra (f0 == f{frames}):      {cierra}')
    print(f'  frames con la capa glow sucia:    {sucio} de {frames}')
    print(f'  frames que tocan el borde:        {borde} de {frames}')
    for k in sorted(pisa):
        print(f'  px sobre {k:24} {pisa[k]} (peor frame)')
    if fn_piezas is not None:
        print(f'  pares de piezas que se tocan:     {pegados} en todo el ciclo')

    return (cierra and sucio == 0 and borde == 0
            and all(v == 0 for v in pisa.values()) and pegados == 0)


# --- Los catorce nodos -----------------------------------------------------
#
# (tipo, nivel) de cada sprite de nodo distinto: el Asteroide, cinco
# Estrellas, tres Pulsares y cinco Magnetares, en orden alfabetico. Las
# listas de abajo -quien se congela, quien se reconvierte, quien se captura-
# se sacan de aqui.
#
# Hasta `T6-12` esta lista era la tabla de anclaje de los ojos, `OJOS`, que
# servia a `ART-ST-SCARE`. Los nodos ya no tienen cara (A.8 #5): la tabla y
# su verificacion se conservan en `nova_ojos.py` solo para que los dos
# generadores descartados del susto sigan corriendo (A.1).
NODOS = sorted([('ast', 0)] + [('str', n) for n in range(1, 6)]
               + [('pul', n) for n in range(1, 4)] + [('mag', n) for n in range(1, 6)])

# El sprite de cada nodo para las medidas por defecto: el Neutral cuando
# existe (Asteroide y Magnetar) y el del Jugador cuando no.
SPRITE = {
    'ast': 'nodes/asteroide/v1/ast_lvl0_neutral.png',
    'str': 'nodes/estrella/v1/str_lvl{n}_player.png',
    'pul': 'nodes/pulsar/v2/pul_lvl{n}_player.png',
    'mag': 'nodes/magnetar/v1/mag_lvl{n}_neutral.png',
}


# --- Tintes que cubren el cuerpo -------------------------------------------
#
# A.3 parte los overlays en tres casos, y cada uno se comprueba distinto.
# `verifica` (arriba) sirve a los dos GLIFOS del Asteroide: exige cero pixeles
# sobre el cuerpo y la capa `glow` limpia. Un TINTE hace justo lo contrario:
# va ENCIMA del cuerpo por definicion y es trama Bayer por obligacion (A.3,
# «los overlays se traman, no se translucen»). Aplicarle aquella vara seria
# suspenderlo por hacer su trabajo.
#
# Lo que hay que medir en un tinte es lo que NO puede tapar. Son dos cosas, y
# las dos estan escritas en otro sitio del documento, no aqui:
#
#   1. LA CARA. Ya no: los nodos no tienen ojos (A.8 #5, `T6-12`), y la
#      comprobacion de «cara libre» se quito con ellos. El susto es solo una
#      sacudida del transform (14.2), que ningun tinte puede tapar.
#   2. EL CONTADOR DE NIVEL. A.8 #2 cerro que el nivel se lee SOLO en el
#      sprite. En los tres nodos congelables el contador vive en la periferia
#      y en piezas sueltas -las esquirlas del Magnetar, los frentes del
#      Pulsar-, que son islas de alfa separadas del cuerpo. Si el tinte se las
#      come, el jugador deja de saber que esta a punto de capturar, y un nodo
#      congelado es EXACTAMENTE el que esta a punto de capturar (7.3).
#
# `Congelado` se aplica a «cualquier nodo encendido» (14.1). El Asteroide
# nunca lo esta: encenderlo lo convierte en otro tipo (4.1), asi que sale de
# la lista. Son 13 sprites, no 14.
CONGELABLES = [k for k in NODOS if k[0] != 'ast']

# `Reconvirtiendo` es mas estrecho todavia: 14.1 lo limita a «Estrella,
# Pulsar o Magnetar PROPIO», y 8.1 confirma por que -se paga con la Energia
# del propio nodo-. Son los mismos trece tipos y niveles, pero medidos sobre
# el sprite del JUGADOR y no sobre el neutral: el Magnetar neutral existe y
# dispara (3.2), pero no se reconvierte.
RECONVERTIBLES = list(CONGELABLES)

# `Capturado` es el mas ancho de todos: 14.1 lo aplica a «cualquier nodo» y
# `ChangeFaction()` puede caerle a un Asteroide dormido igual que a una
# Estrella N5. Son los CATORCE.
CAPTURABLES = NODOS

# `FX-BUFF` dice «toda la faccion» (9.1, 14.5), pero el efecto solo le HACE
# algo a unos tipos: el Rayo Cosmico sube la cadencia del Magnetar y la carga
# del Pulsar, y la Nube de Hidrogeno el intervalo de la Estrella (9.2). El
# Asteroide no gana nada con ninguno de los dos: no dispara, no carga y no
# genera. Marcarlo seria prometerle al jugador una ventaja que ese nodo no
# tiene, que es justo lo que A.2.1 prohibe hacerle al sprite.
#
# Consecuencia util: NINGUN nodo puede llevar los dos buffs a la vez, aunque
# 9.1 permita que los dos power-ups esten activos. Un tipo o es de Rayo o es
# de Hidrogeno. Asi que el overlay nunca tiene que apilarse consigo mismo.
BUFEABLES = {
    'rayo':      [k for k in NODOS if k[0] in ('mag', 'pul')],
    'hidrogeno': [k for k in NODOS if k[0] == 'str'],
}

# Los buffs se miden sobre el sprite del Jugador -van sobre nodos de una
# faccion-, o sea con el mismo mapa que `Reconvirtiendo`: `SPRITE_PROPIO`,
# que se define justo debajo.

SPRITE_PROPIO = {
    'str': 'nodes/estrella/v1/str_lvl{n}_player.png',
    'pul': 'nodes/pulsar/v2/pul_lvl{n}_player.png',
    'mag': 'nodes/magnetar/v1/mag_lvl{n}_player.png',
}


def _etiqueta(m):
    """Componentes conexas 4-vecinas: devuelve (n, matriz de etiquetas)."""
    from collections import deque
    lab = np.zeros(m.shape, np.int32)
    n = 0
    for y, x in zip(*np.where(m)):
        if lab[y, x]:
            continue
        n += 1
        q = deque([(y, x)])
        lab[y, x] = n
        while q:
            cy, cx = q.popleft()
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = cy + dy, cx + dx
                if (0 <= ny < m.shape[0] and 0 <= nx < m.shape[1]
                        and m[ny, nx] and not lab[ny, nx]):
                    lab[ny, nx] = n
                    q.append((ny, nx))
    return n, lab


def satelites(alfa, minimo=40):
    """Las piezas del sprite que NO tocan su cuerpo principal.

    En dos de los tres nodos congelables el contador de nivel vive fuera del
    cuerpo: las esquirlas del Magnetar y los frentes de onda del Pulsar son
    islas de alfa propias. Contarlas antes y despues del tinte es la unica
    forma honesta de saber si el nivel se sigue leyendo; medir «cuanto tapa»
    de media no lo dice, porque tapar 20 px de corona no cuesta nada y tapar
    20 px de esquirla cuesta un nivel.

    `minimo` no es un numero redondo elegido a ojo: separa dos poblaciones
    que en estos sprites no se solapan. Medido sobre los trece, una pieza de
    contador ocupa de 64 a 289 px -esquirlas 64-98, frentes 131-289- y la
    isla de trama mas grande que produce el halo de la Estrella N5 ocupa 20.
    Entre 20 y 64 no hay nada, asi que el corte cae en el hueco y no en el
    borde de ninguna de las dos.
    """
    n, lab = _etiqueta(alfa)
    if n == 0:
        return []
    tam = [(lab == i).sum() for i in range(1, n + 1)]
    principal = 1 + int(np.argmax(tam))
    return [(lab == i) for i in range(1, n + 1)
            if i != principal and tam[i - 1] >= minimo]


def verifica_tinte(fn_lienzo, frames=1, nodos=None,
                   min_satelite=0.50, min_distintos=None, convive_con=None,
                   bucle=True, exige_contador=True,
                   entra_de_nada=True, sprite=None, args=()):
    """Lo que hay que medirle a un tinte de estado, sobre los nodos reales.

    `fn_lienzo(t, *args)` devuelve el lienzo del overlay en la fase `t`.

    Mide dos cosas por nodo -cuanto CONTADOR DE NIVEL sobrevive y cuanto
    CONTRASTE tiene el tinte contra el nodo de debajo- y,
    si el tinte se mueve, tres mas sobre el ciclo:

      - que CIERRE, f0 identico a fN;
      - cuantos frames DISTINTOS produce de verdad. Una hoja de 16 frames que
        contiene 7 imagenes no es una animacion, es un atlas mintiendo sobre
        su tamano (contrato, *Reglas de trabajo*; se midio antes en
        `ART-ST-SCARE`, A.3.3, y alli el resultado fue no animar);
      - si convive con otro overlay. 14.6 no es decorativa: `Congelado` +
        `Infectado` es una casilla marcada «Si», y los dos son trama. Dos
        campos de Bayer apilados pueden acabar en un borron donde no se lee
        ninguno de los dos. `convive_con` es la ruta del otro overlay, y lo
        que se imprime es cuanto le tapa este a aquel.

    La union de los frames es la que se usa para las medidas de tapado: un
    pixel que se cubre en UN frame es un pixel que parpadea encima del
    contador, y eso es peor que taparlo fijo, no mejor.

    CON `bucle=False` el asset no es un estado que dura: es un EFECTO CON
    DURACION, como `Reconvirtiendo`, que son 3.0 s exactos y se acaba. Eso
    cambia dos cosas:

      - no se le exige cerrar, se le exige ENTRAR DESDE NADA Y SALIR A NADA.
        Un efecto cuyo primer o ultimo frame ya tiene pixeles aparece o
        desaparece de golpe y hace un salto contra el estado que lo rodea;
      - `exige_contador=False` lo exime del contador de nivel. No es un
        favor: durante una reconversion el nivel del nodo esta cambiando
        -acaba en N1 del tipo nuevo (8.1)-, asi que no hay nada fiable que
        tapar. Es la unica exencion legitima a la regla, y solo la tiene un
        asset cuya duracion coincide con la del cambio.

    `sprite` sustituye el mapa de sprites por defecto: `Reconvirtiendo` se
    mide sobre el nodo PROPIO, no sobre el neutral.
    """
    if nodos is None:
        nodos = CONGELABLES
    mapa = sprite if sprite is not None else SPRITE

    capas = [a_rgba(fn_lienzo(i / frames, *args)) for i in range(frames)]
    tapa = np.zeros(capas[0].shape[:2], bool)
    for c in capas:
        tapa |= c[..., 3] > 0
    lum_capa = capas[0][..., :3].astype(np.float32) @ (0.2126, 0.7152, 0.0722)
    borde = any(c[..., 3][[0, -1], :].any() or c[..., 3][:, [0, -1]].any()
                for c in capas)

    # DONDE cae el overlay, que es lo que decide contra QUE compite. Un tinte
    # que vive pegado al cuerpo compite con el nodo; uno que vive en el aire
    # compite con el fondo, y contra `#0B0E1A` gana siempre -toda la rampa
    # `decaimiento` esta por encima de 87 de luminancia y el fondo esta en
    # 14-. Sin este reparto, la columna `contraste` de mas abajo se calcula
    # sobre una muestra minuscula y dice poco: un aura que solo toca el 1 %
    # del sprite puede sacar un contraste pesimo y verse perfectamente.
    # Va POR NODO, no contra la union: la union de los trece llena media
    # celda -la Estrella N5 sola ocupa `x 6-92`- y contra ella cualquier
    # overlay parece pegado al cuerpo. Sobre un Magnetar N1, que mide 18 px
    # de radio, el mismo overlay puede estar casi entero en el aire.
    al_aire = []
    for (tipo, nivel) in nodos:
        cuerpo = np.array(Image.open(os.path.join(
            RAIZ, 'art', mapa[tipo].format(n=nivel))).convert('RGBA'))[..., 3] > 0
        al_aire.append((tapa & ~cuerpo).sum() / max(int(tapa.sum()), 1))

    fallos = globales = 0
    print(f'  huella del overlay: {int(tapa.sum())} px'
          f'   toca el borde de la celda: {borde}')
    print(f'  del overlay cae al aire, contra el fondo: '
          f'{min(al_aire):.0%} (sobre el nodo mas grande) a '
          f'{max(al_aire):.0%} (sobre el mas pequeno)')

    if frames > 1:
        if bucle:
            distintos = len({c.tobytes() for c in capas})
            tope = min_distintos if min_distintos is not None else frames
        else:
            # Un efecto con duracion acaba en nada, y si ademas empieza en
            # nada esos dos frames son iguales por definicion y no cuentan.
            # Lo que tiene que ser distinto es todo lo demas. En un efecto de
            # impacto el frame 0 SI cuenta: es su pico.
            dentro = [c.tobytes() for c in (capas[1:-1] if entra_de_nada
                                            else capas[:-1])]
            distintos = len(set(dentro))
            tope = min_distintos if min_distintos is not None else len(dentro)
        if bucle:
            ok = np.array_equal(a_rgba(fn_lienzo(0.0, *args)),
                                a_rgba(fn_lienzo(1.0, *args)))
            etiqueta = f'bucle cierra (f0 == f{frames}): {ok}'
        else:
            vacio0 = not (capas[0][..., 3] > 0).any()
            vacioN = not (capas[-1][..., 3] > 0).any()
            ok = vacioN and (vacio0 or not entra_de_nada)
            etiqueta = (f'sale a nada: {vacioN}'
                        + (f', entra desde nada: {vacio0}' if entra_de_nada
                           else ' (arranca en su pico: efecto de impacto)'))
            # `distintos` cuenta solo el interior, ver arriba
        mal = (not ok) or distintos < tope
        globales += bool(mal)
        print(f'  {etiqueta} · frames distintos: {distintos} de {tope}'
              f'{"   <-- FALLA" if mal else ""}')

    if convive_con is not None:
        ruta = os.path.join(RAIZ, 'art', convive_con)
        otro = np.array(Image.open(ruta).convert('RGBA'))[..., 3] > 0
        pisa = (otro & tapa).sum() / max(int(otro.sum()), 1)
        print(f'  convivencia con {os.path.basename(convive_con)}: '
              f'le tapa el {pisa:.1%} de sus {int(otro.sum())} px')
    for (tipo, nivel) in nodos:
        ruta = os.path.join(RAIZ, 'art', mapa[tipo].format(n=nivel))
        a = np.array(Image.open(ruta).convert('RGBA'))[..., 3] > 0
        sats = satelites(a)
        peor_sat = 1.0
        for s in sats:
            peor_sat = min(peor_sat, 1.0 - (s & tapa).sum() / int(s.sum()))
        cubre = (a & tapa).sum() / max(int(a.sum()), 1)

        # CONTRASTE. Un tinte que cae sobre un nodo del mismo tono no se ve, y
        # 14.0 #5 no admite estados que no se ven. `Congelado` dura hasta 30 s
        # (7.3): es el estado mas largo del juego, y la rampa `hielo` es cian
        # claro, o sea la familia de la Estrella del Jugador. Este numero es
        # la distancia media de luminancia entre lo que pinta el overlay y lo
        # que habia debajo, y es la unica de las tres medidas que no habla de
        # lo que el tinte tapa sino de si se ve.
        sobre_nodo = a & tapa
        if sobre_nodo.any():
            rgb = np.array(Image.open(ruta).convert('RGBA'))[..., :3]
            lum_nodo = rgb.astype(np.float32) @ (0.2126, 0.7152, 0.0722)
            dlum = float(np.abs(lum_capa - lum_nodo)[sobre_nodo].mean())
        else:
            dlum = 0.0

        if bucle:
            mal = exige_contador and sats and peor_sat < min_satelite
            fallos += bool(mal)
            print(f'  {tipo}{nivel}: cubre {cubre:5.1%} · '
                  f'{len(sats)} sat, el peor conserva {peor_sat:5.1%} · '
                  f'contraste {dlum:5.1f}'
                  f'{"   <-- FALLA" if mal else ""}')
        else:
            # En un efecto con duracion importa el PICO: cuanto del sprite
            # llega a tapar en su frame mas lleno.
            pico = max((a & (c[..., 3] > 0)).sum() for c in capas) / max(int(a.sum()), 1)
            mal = exige_contador and sats and peor_sat < min_satelite
            fallos += bool(mal)
            print(f'  {tipo}{nivel}: pico de tapado {pico:5.1%} del sprite · '
                  f'contraste {dlum:5.1f}'
                  f'{"   <-- FALLA" if mal else ""}')
    print(f'  nodos que fallan: {fallos} de {len(nodos)}'
          f'{"  ·  " + str(globales) + " comprobacion(es) global(es) fallan" if globales else ""}')
    return fallos == 0 and globales == 0 and not borde
