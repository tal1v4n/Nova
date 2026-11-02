"""NOVA - SND-PWR-ZER v1, el congelamiento del nodo: «El vidrio que se calla».

Ref: NOVA_Sonido.md S.4 y S.5.1 · arte que acompana: FX-PWR-ZER (0.7 s,
14.5) -> estado Congelado, ART-ST-FROZEN v3 «El vidrio» (A.3.4) ·
NOVA_GameDesign_Spec 7.3.

Suena en PowerBeamSystem.OnLanded con Type == AbsoluteZero, en el nodo
objetivo. 0.70 s, ataque justo en el primer sample y final seco.

LAS GRIETAS SON LAS DE A.3.4. El overlay del estado tiene trece segmentos:
un tronco con cuatro nudos, cinco ramas que salen y no vuelven, y dos
astillas sueltas. El sonido los rompe en ese orden, en los primeros 0.3 s:

  - TRONCO, cuatro grietas grandes (0, 52, 118, 205 ms), cada una mas
    pequena que la anterior. Llevan el "piu" del hielo en tension: el
    chirrido descendente de un lago helado cuando se raja, porque el agudo
    viaja por la placa mas deprisa que el grave.
  - RAMAS, cinco grietas medianas entre los nudos, sin piu: salen y se
    acaban.
  - ASTILLAS, dos esquirlas que caen ya con el nodo quieto (360 y 470 ms):
    dos notas de cristal sueltas, la ultima en Re, la tonica.

EL ACORDE QUE SE CONGELA. Debajo de las grietas suena un acorde de copas
de vidrio -Re, La, Mi, Sol#: el Re lidio que el haz traia en cascada- que
nace temblando. Cada nota es una pareja de modos desafinados unos hercios:
el batido es el movimiento de las moleculas. Ese desafino cae a cero en
unos 0.4 s y el acorde se queda **quieto**, sin ningun batido: la
temperatura llega al cero absoluto y el sonido deja de moverse. Entonces,
igual que la animacion se queda en un frame (A.3.4, idle detenido), se
corta. Sin cola (S.4 #5): el silencio final es parte del hielo.

Y abajo, apenas, un golpe grave que cae de 110 a 42 Hz: la presion del
frio al cerrarse. Da el peso de cine sin salir de la familia del poder
(agudo, cristalino, seco, S.4 #4).

    .venv\\Scripts\\python tools\\gen_snd_pwr_zero_v1.py
"""

import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nova_audio as A  # noqa: E402

SEMILLA = 7301
DUR = 0.70          # 14.5, FX-PWR-ZER
CORTE = 0.60        # aqui empieza el final seco
LUFS = -15.0

ACORDE = ["D5", "A5", "E6", "G#6", "D7"]

#            t (s)   talla  piu   ganancia
TRONCO = [(0.000, 1.45, 0.85, 1.00),
          (0.052, 1.20, 0.65, 0.78),
          (0.118, 1.00, 0.50, 0.62),
          (0.205, 0.85, 0.40, 0.48)]
RAMAS = [(0.021, 0.62, 0.0, 0.42),
         (0.081, 0.70, 0.0, 0.40),
         (0.144, 0.55, 0.1, 0.33),
         (0.171, 0.66, 0.0, 0.30),
         (0.268, 0.50, 0.1, 0.24)]
ASTILLAS = [(0.360, "A7", 0.20), (0.470, "D7", 0.17)]


def genera(semilla=SEMILLA):
    rng = np.random.default_rng(semilla)
    t = A.tiempo(DUR)
    mix = A.lienzo(DUR)

    # 1. EL ACORDE QUE SE CONGELA. Copa de vidrio golpeada y frotada: entra
    #    con el golpe, se hincha un poco (el frio que se extiende) y se
    #    sostiene quieto hasta el corte.
    hinchado = A.env_tramos(DUR, [(0, 0.55), (0.10, 1.0), (0.30, 0.85),
                                  (CORTE, 0.62), (DUR, 0.62)])
    niveles = [0.80, 0.70, 0.62, 0.50, 0.30]
    acorde = A.lienzo(DUR)
    for nm, g in zip(ACORDE, niveles):
        v = A.modal(A.nota(nm), A.VIDRIO_COPA,
                    [1.0, 0.22, 0.09, 0.04, 0.02],
                    [1.6, 0.45, 0.20, 0.10, 0.06],
                    DUR, rng, beat=(6.5, 0.13), ataque=0.002)
        A.place(acorde, v, 0, g)
    acorde *= hinchado
    A.place(mix, acorde / np.max(np.abs(acorde)), 0, 0.36)

    # 2. LAS GRIETAS de A.3.4: tronco, ramas.
    for t0, talla, piu, g in TRONCO:
        A.place(mix, A.grieta(rng, talla, piu, dur=0.25), t0, 0.95 * g)
    for t0, talla, piu, g in RAMAS:
        A.place(mix, A.grieta(rng, talla, piu, dur=0.12), t0, 0.95 * g)

    # ...y las astillas: dos notas sueltas, con un chasquido diminuto.
    for t0, nm, g in ASTILLAS:
        A.place(mix, A.campanita(A.nota(nm), 0.15, rng, brillo=0.7,
                                 largo=0.05), t0, g)
        A.place(mix, A.grieta(rng, 0.2, 0.0, dur=0.03), t0, g * 0.6)

    # 3. EL GOLPE: el destello del impacto y la presion grave.
    destello = A.hp(A.ruido(rng, 0.08), 2500) * A.env_ad(0.08, 0.0005, 0.012)
    A.place(mix, destello / np.max(np.abs(destello)), 0, 0.45)

    tg = A.tiempo(0.25)
    fg = 42 + (110 - 42) * np.exp(-tg / 0.035)
    grave = np.sin(2 * np.pi * np.cumsum(fg) / A.SR) * A.env_ad(0.25, 0.002, 0.075)
    grave += 0.4 * A.lp(A.ruido(rng, 0.25), 180) * A.env_ad(0.25, 0.001, 0.03)
    A.place(mix, grave, 0, 0.32)

    # 4. LA CRISTALIZACION: escarcha que se extiende por el nodo, densa al
    #    principio, y que se apaga a la vez que el batido del acorde.
    #    Despues de 0.32 s no queda ni un grano: el nodo ya esta quieto.
    dens = 2200 * np.exp(-t / 0.08) + 400 * np.exp(-((t - 0.16) / 0.07) ** 2)
    dens[t > 0.32] = 0
    A.place(mix, A.escarcha(rng, DUR, dens, banda=(4500, 15500)), 0, 0.42)

    # 5. EL CRUJIDO del bloque en tension: pulsos de friccion irregulares
    #    (stick-slip) en banda media, por debajo de las grietas.
    k = A.n(0.30)
    tr = np.zeros(k)
    pos = 0.0
    while pos < 0.30:
        tr[A.n(pos)] = rng.uniform(0.5, 1.0)
        pos += 1 / rng.uniform(90, 170)
    tr = A.bp(tr, 450, 2200) * A.env_tramos(0.30, [(0, 0), (0.05, 1.0),
                                                   (0.20, 0.7), (0.30, 0)])
    A.place(mix, tr / np.max(np.abs(tr)), 0.04, 0.10)

    # Espacio: una sala pequena y brillante, sin cola (S.4 #5).
    mix = A.sala(mix, rng, cola=0.07, mezcla=0.25)

    # El final seco: el sonido se congela y se calla.
    mix = A.cortar(mix, CORTE, 0.07)
    return A.normaliza(mix, LUFS)


def main():
    x = genera()
    base = os.path.join("audio", "sfx", "pwr_zero", "v1")
    wav = A.ruta(base, "snd_pwr_zer.wav")
    m = A.export(x, wav)
    A.espectrograma(x, A.ruta(base, "snd_pwr_zer_espectro.png"))
    print(f"{wav}\n  {m['dur']:.3f} s  {m['lufs']:.1f} LUFS  {m['dbtp']:.1f} dBTP")

    # Escucha de revision: el lanzamiento entero como suena en juego, el haz
    # en 0 y el impacto en OnLanded (0.25 s). No es un asset.
    import gen_snd_beam_zero_v1 as haz
    seq = A.lienzo(0.25 + DUR + 0.05)
    A.place(seq, haz.genera(), 0)
    A.place(seq, x, 0.25)
    seq *= A.db(-1.0 - A.pico_real(seq)) if A.pico_real(seq) > -1.0 else 1.0
    p = A.ruta("audio", "sfx", "_preview", "zer_lanzamiento.wav")
    A.export(seq, p)
    A.espectrograma(seq, A.ruta("audio", "sfx", "_preview", "zer_lanzamiento_espectro.png"))
    print(p)
    return x


if __name__ == "__main__":
    main()
