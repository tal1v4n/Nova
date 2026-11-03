"""NOVA - SND-PWR-WRM v1, el robo del nodo: «La fusion».

Ref: NOVA_Sonido.md S.4 y S.5.1 · arte que acompana: FX-PWR-WRM (1.0 s,
14.5) y el estado Capturado, ART-ST-CAP v2 «El relevo» (A.3.7) ·
NOVA_GameDesign_Spec 7.5.

Suena en PowerBeamSystem.OnLanded con Type == Wormhole, en el nodo
objetivo. 1.0 s, el unico de los seis que llega (S.5.1): es el poder mas
caro y tiene que sonar el mas grande.

EL ROBO YA HA PASADO EN EL PRIMER SAMPLE. ChangeFaction() se aplica en
OnLanded y el nodo cambia de paleta en el frame 0 (14.1). Lo que el sonido
cuenta es lo que dibuja «El relevo»: un anillo que SE CIERRA sobre el nodo
-lo que llega-, otro que SALE de el -lo que se va-, y el cruce de los dos
en el centro de sus 0.4 s, a los 0.2. Ese cruce es el golpe.

LA FISICA ES LA DE LIGO. Cuando dos agujeros negros se funden, la onda
gravitacional que emiten suena como un "chirp": la frecuencia y la
amplitud suben cada vez mas deprisa mientras giran uno alrededor del otro
(espiral), estallan al fundirse (fusion) y el agujero resultante vibra y se
calla (ringdown). Es el sonido real de la gravedad y es exactamente esta
forma:

  - CONTACTO (0 ms). El haz desemboca: un ultimo desgarro y un golpe de
    presion. El ataque cae en OnLanded.
  - ESPIRAL (0-200 ms). El anillo que se cierra: el chirp de 35 a 280 Hz,
    con f ~ (t_c - t)^(-3/8), la ley de la espiral; y el aire aspirado
    hacia el nodo, un barrido de ruido que se abre y crece.
  - FUSION (200 ms). El cruce de los anillos: el golpe mas grande del
    juego. Un sub que cae de 75 a 26 Hz, un cuerpo de ruido grave, el
    chasquido del impacto y un destello agudo -el blanco de FX-CAP-. Y el
    ringdown: el nodo vibra un instante en su nota nueva.
  - IMPLOSION (200-850 ms, la receta de S.5.1). El anillo que se va: un
    tono que se hunde de 320 a 40 Hz por un peine que baja con el. Y dentro
    de el vuelve el OTRO CIELO del haz, pero ahora cayendo: CORRIMIENTO AL
    ROJO GRAVITACIONAL, la luz que sale de un pozo de gravedad pierde
    frecuencia. Las estrellas bajan una cuarta mientras se apagan.
  - CIERRE (800 ms). La boca del agujero se cierra: una ultima aspiracion
    y un golpe sordo con una sala corta, como una puerta pesada. Despues,
    nada.

    .venv\\Scripts\\python tools\\gen_snd_pwr_wormhole_v1.py
"""

import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nova_audio as A  # noqa: E402

SEMILLA = 7501
DUR = 1.00          # 14.5, FX-PWR-WRM
FUSION = 0.20       # el cruce de los anillos de ART-ST-CAP (0.4 s / 2)
CIERRE = 0.80
LUFS = -14.0        # el mas grande de los seis

OTRO_CIELO = [("B5", 0.9), ("F#6", 0.75), ("C#7", 0.55), ("D#7", 0.45)]
CORRIMIENTO = -5    # semitonos: una cuarta hacia el rojo


def golpe_grave(f_ini, f_fin, tau_f, dur, caida, rng, empuje=1.8):
    tg = A.tiempo(dur)
    fg = f_fin + (f_ini - f_fin) * np.exp(-tg / tau_f)
    y = np.sin(2 * np.pi * np.cumsum(fg) / A.SR) * A.env_ad(dur, 0.0015, caida)
    return A.satura(y, empuje)


def genera(semilla=SEMILLA):
    rng = np.random.default_rng(semilla)
    t = A.tiempo(DUR)
    mix = A.lienzo(DUR)

    # 0. CONTACTO: el ultimo desgarro y la presion, en el primer sample.
    dens = 12000 * np.exp(-t / 0.018) * (t < 0.06)
    A.place(mix, A.rasgado(rng, DUR, dens), 0, 0.60)
    A.place(mix, golpe_grave(110, 55, 0.02, 0.15, 0.04, rng), 0, 0.42)

    # 1. ESPIRAL: el chirp de la onda gravitacional, hasta la fusion.
    f, a = A.chirp_gravitacional(DUR, FUSION, 35.0, 280.0)
    fase = 2 * np.pi * np.cumsum(f) / A.SR
    espiral = (np.sin(fase) + 0.35 * np.sin(2 * fase) + 0.15 * np.sin(3 * fase)) * a
    espiral = A.satura(espiral * 0.9, 2.0)
    A.place(mix, A.cortar(espiral, FUSION, 0.004), 0, 0.55)

    asp = A.ruido_rosa(rng, FUSION + 0.01)
    ka = np.linspace(0, 1, len(asp))
    asp = A.svf(asp, 200 * (3000 / 200) ** ka, 1.4, salida="lp")
    asp *= ka ** 2.2
    A.place(mix, asp / np.max(np.abs(asp)), 0, 0.45)

    # 2. FUSION: el golpe mas grande, en el cruce de los anillos.
    A.place(mix, golpe_grave(75, 26, 0.07, 0.6, 0.20, rng, empuje=2.4), FUSION, 0.62)
    cuerpo = A.bp(A.ruido(rng, 0.3), 60, 500) * A.env_ad(0.3, 0.001, 0.07)
    A.place(mix, cuerpo / np.max(np.abs(cuerpo)), FUSION, 0.60)
    chasquido = A.lp(A.ruido(rng, 0.03), 4000) * A.env_ad(0.03, 0.0002, 0.004)
    A.place(mix, chasquido / np.max(np.abs(chasquido)), FUSION, 0.90)
    destello = A.hp(A.ruido(rng, 0.12), 5000) * A.env_ad(0.12, 0.0005, 0.03)
    A.place(mix, destello / np.max(np.abs(destello)), FUSION, 0.45)

    # ...y el ringdown: el agujero nuevo vibra en su modo y se calla.
    tr = A.tiempo(0.25)
    ring = np.sin(2 * np.pi * np.cumsum(330 * (1 - 0.12 * tr / 0.25)) / A.SR)
    ring *= np.exp(-tr / 0.045)
    A.place(mix, A.satura(ring, 1.5), FUSION, 0.30)

    # 3. IMPLOSION: el anillo que se va, un tono que se hunde por un peine.
    di = CIERRE + 0.05 - FUSION
    ti = A.tiempo(di)
    ki = ti / di
    fi = 320 * (40 / 320) ** (ki ** 0.7)
    fasei = 2 * np.pi * np.cumsum(fi) / A.SR
    imp = np.sin(fasei) + 0.5 * A.sierra(fasei, 320, techo=3000)
    imp = A.satura(imp * 0.8, 2.0)
    imp = A.flanger(imp, 0.6 * (7.0 / 0.6) ** ki, profundidad=0.9)
    imp *= A.env_tramos(di, [(0, 0), (0.02, 1.0), (0.25, 0.55),
                             (0.5, 0.25), (di, 0.0)])
    A.place(mix, imp / np.max(np.abs(imp)), FUSION, 0.32)

    # ...con el otro cielo dentro, cayendo al rojo.
    dc = 0.55
    tc = A.tiempo(dc)
    rojo = 2 ** (CORRIMIENTO * (tc / dc) ** 0.8 / 12)
    cielo = A.lienzo(dc)
    for nm, g in OTRO_CIELO:
        fs = A.nota(nm) * rojo
        s = np.sin(2 * np.pi * np.cumsum(fs) / A.SR + rng.uniform(0, 6.3))
        s *= 1 + 0.25 * np.sin(2 * np.pi * rng.uniform(9, 15) * tc)
        A.place(cielo, s, 0, g)
    cielo = A.flanger(cielo, 2.5 + 1.5 * np.sin(2 * np.pi * 5 * tc), 0.8, etapas=1)
    cielo *= A.env_tramos(dc, [(0, 0), (0.03, 1.0), (0.2, 0.6), (dc, 0.0)])
    A.place(mix, cielo / np.max(np.abs(cielo)), FUSION + 0.02, 0.38)

    # 4. CIERRE: la boca se cierra, una aspiracion y un golpe sordo.
    da = 0.18
    asp2 = A.ruido_rosa(rng, da)
    ka = np.linspace(0, 1, len(asp2))
    asp2 = A.svf(asp2, 150 * (1800 / 150) ** ka, 1.3, salida="lp") * ka ** 2.5
    A.place(mix, asp2 / np.max(np.abs(asp2)), CIERRE - da, 0.20)
    A.place(mix, golpe_grave(85, 38, 0.025, 0.18, 0.05, rng), CIERRE, 0.28)
    sordo = A.bp(A.ruido(rng, 0.08), 150, 900) * A.env_ad(0.08, 0.0005, 0.02)
    A.place(mix, sordo / np.max(np.abs(sordo)), CIERRE, 0.40)

    # Espacio: una sala corta y oscura (la receta: "reverb corta").
    mix = A.sala(mix, rng, cola=0.08, mezcla=0.25, brillo=4500)

    mix = A.cortar(mix, DUR - 0.07, 0.07)
    mix = A.fade(mix, dentro=0.0005)
    return A.normaliza(mix, LUFS)


def main():
    x = genera()
    base = os.path.join("audio", "sfx", "pwr_wormhole", "v1")
    wav = A.ruta(base, "snd_pwr_wrm.wav")
    m = A.export(x, wav)
    A.espectrograma(x, A.ruta(base, "snd_pwr_wrm_espectro.png"), rampa="gusano")
    print(f"{wav}\n  {m['dur']:.3f} s  {m['lufs']:.1f} LUFS  {m['dbtp']:.1f} dBTP")

    # Escucha de revision: el lanzamiento entero como suena en juego, el haz
    # en 0 y el impacto en OnLanded (0.25 s). No es un asset.
    import gen_snd_beam_wormhole_v1 as haz
    seq = A.lienzo(0.25 + DUR + 0.05)
    A.place(seq, haz.genera(), 0)
    A.place(seq, x, 0.25)
    seq *= A.db(-1.0 - A.pico_real(seq)) if A.pico_real(seq) > -1.0 else 1.0
    p = A.ruta("audio", "sfx", "_preview", "wrm_lanzamiento.wav")
    A.export(seq, p)
    A.espectrograma(seq, A.ruta("audio", "sfx", "_preview",
                                "wrm_lanzamiento_espectro.png"), rampa="gusano")
    print(p)
    return x


if __name__ == "__main__":
    main()
