"""NOVA - Paleta maestra.

Fuente de verdad del color del proyecto. Cada material es una rampa de 7
tonos ordenada de mas claro a mas oscuro; el indice 0 se reserva al
especular (ver NOVA_Arte_Tecnica.md, "Paleta").

Direccion artistica: retro-futurista cosmico. Tres reglas duras:

  1. ANCLA FUNCIONAL. Toda rampa ligada a un color de 14.4 lo contiene
     literalmente en su indice 3 (a veces el 2). Ese es el tono que el
     jugador asocia al concepto, y no se negocia.
  2. LUZ CALIDA, SOMBRA VIOLETA. El extremo claro tiende al blanco; el
     extremo oscuro converge siempre a la familia indigo/ciruela
     (#1D1F44 - #3D1440). Es lo que da el aire de ilustracion aerografo
     de los 70-80 y lo que hace que 20 rampas distintas parezcan una sola.
  3. NADA TOCA EL FONDO. Ningun indice 6 baja de la luminancia de
     #0B0E1A, para que la silueta nunca se funda con el espacio.
"""

# --- Constantes globales ---------------------------------------------------

FONDO    = '#0B0E1A'   # 14.4 - fondo profundo
CONTORNO = '#141021'   # NOVA_Arte_Tecnica.md - color de contorno

# --- Anclas funcionales (14.4) ---------------------------------------------

ANCLAS = {
    'jugador':     '#3FD7F5',
    'enemigo':     '#F2456B',
    'neutral':     '#8A93A6',
    'congelado':   '#B8F0FF',
    'infectado':   '#7CF23F',
    'rayo':        '#FFD23F',
    'hidrogeno':   '#FF8FD0',
}

# --- Rampas ----------------------------------------------------------------
#            0=especular  1        2        3=ancla  4        5        6=sombra

RAMPAS = {

    # ---- Estrella: cuerpo de faccion, nucleo que blanquea por nivel ------
    'str_p':      ('#FFFFFF','#E8FEFF','#A6F2FD','#3FD7F5','#2A9AD4','#2B5CA6','#26306B'),
    'str_e':      ('#FFFFFF','#FFE7EC','#FF9EB2','#F2456B','#B92A5C','#7A1F52','#3D1440'),

    # ---- Linea de envio (ART-WLD-LINK v2, A.4.6): plasma del Jugador -----
    # Nucleo blanco de calor, el ancla del Jugador junto al hilo y la vaina
    # que se enfria hacia el azul electrico y el anil. El anil se queda en
    # 236-247 grados, lejos del morado de `gusano` (~263).
    'plasma_p':   ('#FFFFFF','#F4F0FF','#A9EEFF','#3FD7F5','#5B6BF5','#5A48D6','#2A1F66'),

    # ---- Proton del Enemigo (ART-WLD-PRO-E, A.4.2): su plasma -------------
    # Espejo de `plasma_p`: nucleo blanco, el ancla del Enemigo y la vaina
    # que se enfria hacia el carmin y la ciruela de `str_e[6]`. Se queda en
    # 290-345 grados, lejos del morado de `gusano` (~263).
    'plasma_e':   ('#FFFFFF','#FFF0F3','#FFB3C6','#F2456B','#D8336F','#8E2160','#3D1440'),

    # ---- Corona de la Estrella: misma familia, un punto mas luminosa -----
    'cor_p':      ('#FFFFFF','#DBFBFF','#7FE8FC','#35C2EE','#2288C8','#23569C','#1E2C60'),
    'cor_e':      ('#FFFFFF','#FFDFE6','#FF8FA8','#EE3A66','#C02458','#851C4E','#45143E'),

    # ---- Pulsar: estrella de neutrones, azul-violeta electrico -----------
    'pul_p':      ('#FFFFFF','#E4FBFF','#9EE7FA','#4FC6F0','#2E7FC4','#2B4A9B','#211F5E'),
    'pul_e':      ('#FFFFFF','#FFE4EE','#FF9CC0','#F2457F','#BE2668','#7C1C55','#3B1240'),

    # ---- Magnetar: metalico, desaturado, denso ---------------------------
    'mag_p':      ('#FFFFFF','#DCF2FA','#92C6DE','#4E93B8','#35648F','#2B3F6D','#1D2144'),
    'mag_e':      ('#FFFFFF','#FFE0E4','#E094A4','#B85A72','#8E3355','#5E203F','#33122C'),
    'mag_n':      ('#FFFFFF','#E6E9F2','#AFB6C8','#7C849B','#545B75','#383C5A','#1F1F3C'),

    # ---- Asteroide: roca muerta, parda fria ------------------------------
    'roca_n':     ('#FFFFFF','#D8D5DC','#A79FB0','#7B7189','#564D66','#372F4A','#1F192F'),
    'roca_p':     ('#FFFFFF','#DCF4FB','#A2C4D4','#6F93AB','#4C6A86','#33475F','#1D2440'),
    'roca_e':     ('#FFFFFF','#FBDFE4','#CDA0AC','#9A6E7F','#70485E','#4B2E44','#281829'),

    # ---- Neutral generico (UI, zzz, anillos neutros) ---------------------
    'neutral':    ('#FFFFFF','#E2E6EF','#B5BCCD','#8A93A6','#5D6580','#3B4060','#22203F'),

    # ---- Estados (14.1) --------------------------------------------------
    'hielo':      ('#FFFFFF','#EAFDFF','#B8F0FF','#7FD8F5','#4FA8D8','#35669F','#232F63'),
    'decaimiento':('#FFFFFF','#E4FFD0','#B4FA84','#7CF23F','#46B02C','#256B2B','#143A2C'),

    # ---- Power-ups (9.2) -------------------------------------------------
    'rayo':       ('#FFFFFF','#FFF6D2','#FFE68A','#FFD23F','#D9932A','#96571F','#4C2A2E'),
    'hidrogeno':  ('#FFFFFF','#FFE9F5','#FFC0E3','#FF8FD0','#CE5AA6','#8C3577','#481B4C'),

    # ---- Agujero de gusano (7.5): violeta, el unico acento morado puro ---
    'gusano':     ('#FFFFFF','#F0E2FF','#C9A2FF','#9B5CF6','#6C31C4','#452088','#241247'),

    # ---- Ojo: casi negro violaceo. Los nodos ya no tienen ojos (T6-12): solo
    #      la usan las propuestas descartadas (A.1), por eso se conserva ----
    'ojo':        ('#FFFFFF','#C9CBE8','#7C7FB4','#4A4A84','#2E2B5C','#1E1A42','#141021'),

    # ---- Fondos (A.4.5): las UNICAS rampas que no significan nada --------
    #
    # Todas las demas entradas de este diccionario llevan un significado de
    # juego encima: faccion, estado o power-up. Un fondo pintado con una de
    # ellas le dice al jugador algo que no es cierto -que ese trozo de cielo
    # es de alguien, o que algo esta congelado-, y lo dice durante toda la
    # partida. Por eso las tres secciones no se separan por tono sino por
    # estructura y densidad, y por eso estas tres rampas existen: para no
    # tener que robarle el color a nada.
    #
    # `gusano`, `hielo`, `decaimiento`, `rayo` e `hidrogeno` estan
    # EXPRESAMENTE prohibidas en un fondo. Son las cinco que el jugador tiene
    # que reconocer al instante en mitad del tablero.
    #
    # Las tres viven en el extremo oscuro y convergen a la familia
    # indigo/ciruela de la regla 2, que es lo que hace que el fondo y la
    # sombra de los nodos sean el mismo espacio. El indice 6 se queda un
    # punto por encima del fondo plano #0B0E1A, para que el polvo nunca
    # parezca un agujero.
    'bg_s1':      ('#FFFFFF','#EFE3D8','#B8A79C','#7A6A70','#4E4152','#2E2740','#15121F'),
    'bg_s2':      ('#FFFFFF','#E4E8F2','#A9B0C4','#6E7590','#474D68','#2B2F47','#141624'),
    'bg_s3':      ('#FFFFFF','#DCE0FA','#9AA0D2','#626796','#3E4068','#262744','#111120'),
}


def hex_a_rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))
