"""NOVA - SND-PWR-DEC v1, la infeccion del nodo: «El coro que se pudre».

Ref: NOVA_Sonido.md S.4 y S.5.1 · arte que acompana: FX-PWR-DEC (0.7 s,
14.5) -> estado Infectado, ART-ST-DECAY v2 «El miasma» (A.3.5) ·
NOVA_GameDesign_Spec 7.4.

Suena en PowerBeamSystem.OnLanded con Type == Decay, en el nodo objetivo.
0.70 s, ataque en el primer sample.

ES EL ESPEJO DEL CERO ABSOLUTO. En SND-PWR-ZER un acorde nace temblando y
su batido cae a cero: el sonido deja de moverse. Aqui pasa al reves: un
CORO nace afinado y se PUDRE. Cada voz es una pareja que empieza al unisono
y se desafina cada vez mas (el batido crece de 0.3 a 11 Hz), y todo el
acorde se hunde un semitono y medio con el temblor irregular de una cinta
que se muere. Lo que el frio detiene, la radiacion lo descompone.

El acorde es el del haz llevado a la armonia: Sol# grave, Re (el triton,
la nota del Cero Absoluto vista desde el otro lado), La (una novena menor
sobre el Sol#, la disonancia mas agria) y Mib. Las voces pasan por dos
formantes que van de «o» a «u»: no es un sinte, es algo que gime.

ENCIMA, LO QUE SE VE EN EL NODO:

  - LA SALPICADURA (0 ms). La nube verde se posa: un golpe humedo -grave
    que cae de 140 a 55 Hz, ruido por un paso bajo resonante que se cierra
    de 6 kHz a 350 Hz, y seis chapoteos pegajosos en los primeros 40 ms-.
  - LA CORROSION. Un chisporroteo de acido sobre metal que arranca denso y
    se calma, como aceite que frie, con efervescencia de burbujas diminutas.
  - EL BURBUJEO QUE BAJA (la receta de S.5.1). Burbujas de Minnaert -el
    tono de una burbuja real sale de su radio- cada vez mas grandes y mas
    lentas: el nodo se vuelve viscoso. De 1.5 kHz a 250 Hz.
  - EL RECUENTO. 70 atomos con una vida media de 100 ms: los clics Geiger se
    espacian hasta quedar sueltos, la Energia que se escapa sola (7.4).
  - LAS CINCO GOTAS de «El miasma», que se descuelgan de la nube en cinco
    tramos iguales: cinco "plinks" que bajan por las notas del coro hasta
    acabar en Sol#, cada uno mas apagado, como la gota del arte que baja de
    banda en vez de desvanecerse.
  - EL MIASMA RESPIRA. Debajo de todo, un aliento: ruido rosa por una
    formante que cae, el aura que respira.

No acaba en seco como el hielo: se apaga, porque la infeccion se queda. Pero
a los 0.70 s ya no queda nada (S.4 #5).

    .venv\\Scripts\\python tools\\gen_snd_pwr_decay_v1.py
"""

import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nova_audio as A  # noqa: E402

SEMILLA = 7401
DUR = 0.70          # 14.5, FX-PWR-DEC
LUFS = -15.0        # como el impacto de Cero Absoluto

CORO = [("G#3", 0.85), ("D4", 0.70), ("A4", 0.55), ("D#5", 0.38)]
HUNDIMIENTO = -1.5  # semitonos que cae el coro en todo el sonido
BATIDO = (0.3, 11.0)  # Hz de desafino, del principio al final

# Las cinco gotas de A.3.5: cinco tramos iguales, notas del coro hacia abajo.
GOTAS = [(0.27, "D6", 0.30), (0.335, "A5", 0.27), (0.40, "D#5", 0.24),
         (0.465, "D5", 0.20), (0.53, "G#4", 0.18)]


def voz(f, dur, rng, t, caida, desafino):
    """Una voz del coro: dos sierras que se separan, por formantes o -> u."""
    w = A.deriva(rng, dur, 18, velocidad=5)  # el temblor de la cinta
    f1 = f * caida * w
    f2 = f1 + desafino
    s = (A.sierra(2 * np.pi * np.cumsum(f1) / A.SR, f1.max(), techo=6000)
         + A.sierra(2 * np.pi * np.cumsum(f2) / A.SR + rng.uniform(0, 6.3),
                    f2.max(), techo=6000))
    k = t / dur
    F1 = 480 + (330 - 480) * k      # «o» -> «u»
    F2 = 850 + (700 - 850) * k
    y = A.svf(s, F1, 5.0) + 0.6 * A.svf(s, F2, 6.0) + 0.12 * A.lp(s, 2500)
    return y / (np.max(np.abs(y)) + 1e-12)


def genera(semilla=SEMILLA):
    rng = np.random.default_rng(semilla)
    t = A.tiempo(DUR)
    mix = A.lienzo(DUR)
    k = t / DUR

    # 1. EL CORO QUE SE PUDRE.
    caida = 2 ** (HUNDIMIENTO * k ** 1.4 / 12)
    desafino = BATIDO[0] + (BATIDO[1] - BATIDO[0]) * k ** 1.2
    coro = A.lienzo(DUR)
    for nm, g in CORO:
        A.place(coro, voz(A.nota(nm), DUR, rng, t, caida, desafino), 0, g)
    coro *= A.env_tramos(DUR, [(0, 0), (0.025, 0.55), (0.12, 1.0),
                               (0.35, 0.75), (0.60, 0.30), (DUR, 0.0)])
    A.place(mix, coro / np.max(np.abs(coro)), 0, 0.50)

    # 2. LA SALPICADURA: golpe humedo, paso bajo que se cierra, chapoteos.
    tg = A.tiempo(0.25)
    fg = 55 + (140 - 55) * np.exp(-tg / 0.03)
    golpe = np.sin(2 * np.pi * np.cumsum(fg) / A.SR) * A.env_ad(0.25, 0.001, 0.05)
    A.place(mix, golpe, 0, 0.42)

    humedo = A.ruido_rosa(rng, 0.18)
    humedo = A.svf(humedo, np.geomspace(6000, 350, len(humedo)), 2.2, salida="lp")
    humedo *= A.env_ad(0.18, 0.001, 0.045)
    A.place(mix, humedo / np.max(np.abs(humedo)), 0, 0.45)

    for i in range(6):
        tc = rng.uniform(0.0, 0.04) if i else 0.0
        ch = A.bp(A.ruido(rng, 0.02), 600, 2600) * A.env_ad(0.02, 0.0003, 0.003)
        A.place(mix, ch / np.max(np.abs(ch)), tc, rng.uniform(0.15, 0.30))

    # 3. LA CORROSION: chisporroteo de acido y efervescencia.
    dens = 3800 * np.exp(-t / 0.16) + 150 * np.exp(-t / 0.5)
    fritura = A.escarcha(rng, DUR, dens, banda=(1800, 9000))
    fritura *= 1 + 0.5 * A.lp(A.ruido(rng, DUR), 25) / 0.1  # ondula, no es plano
    A.place(mix, fritura / np.max(np.abs(fritura)), 0, 0.50)

    for ti in np.sort(rng.uniform(0.0, 0.35, 70) ** 1.5 / 0.35 ** 0.5):
        A.place(mix, A.burbuja(rng.uniform(2800, 6500), rng, 0.03), ti,
                rng.uniform(0.05, 0.12))

    # 4. EL BURBUJEO QUE BAJA: burbujas cada vez mas grandes y lentas.
    tb = 0.03
    while tb < 0.58:
        ritmo = 75 * np.exp(-((tb - 0.17) / 0.17) ** 2) + 14
        f0 = 1500 * (250 / 1500) ** (tb / 0.58) * rng.uniform(0.82, 1.22)
        A.place(mix, A.burbuja(f0, rng, 0.14), tb,
                rng.uniform(0.10, 0.22) * (1 - 0.4 * tb / 0.58))
        tb += rng.exponential(1 / ritmo)

    # 5. EL RECUENTO: 70 atomos, clics que se espacian.
    inst = A.vidas(rng, 70, 0.10, t0=0.01)
    A.place(mix, A.geiger(rng, DUR - 0.06, inst), 0, 0.34)

    # 6. LAS CINCO GOTAS del miasma.
    for tgota, nm, g in GOTAS:
        A.place(mix, A.gota(A.nota(nm), rng, 0.12), tgota, g)

    # 7. EL MIASMA RESPIRA: un aliento por una formante que cae.
    aliento = A.ruido_rosa(rng, DUR)
    aliento = A.svf(aliento, np.geomspace(1100, 380, len(t)), 3.0)
    aliento *= A.env_tramos(DUR, [(0, 0), (0.06, 0.4), (0.22, 1.0),
                                  (0.50, 0.5), (DUR, 0.0)])
    A.place(mix, aliento / np.max(np.abs(aliento)), 0, 0.14)

    # Espacio: una sala pequena y apagada, sin cola (S.4 #5).
    mix = A.sala(mix, rng, cola=0.06, mezcla=0.22, brillo=6500)

    mix = A.cortar(mix, DUR - 0.07, 0.07)
    mix = A.fade(mix, dentro=0.0005)
    return A.normaliza(mix, LUFS)


def main():
    x = genera()
    base = os.path.join("audio", "sfx", "pwr_decay", "v1")
    wav = A.ruta(base, "snd_pwr_dec.wav")
    m = A.export(x, wav)
    A.espectrograma(x, A.ruta(base, "snd_pwr_dec_espectro.png"),
                    rampa="decaimiento")
    print(f"{wav}\n  {m['dur']:.3f} s  {m['lufs']:.1f} LUFS  {m['dbtp']:.1f} dBTP")

    # Escucha de revision: el lanzamiento entero como suena en juego, el haz
    # en 0 y el impacto en OnLanded (0.25 s). No es un asset.
    import gen_snd_beam_decay_v1 as haz
    seq = A.lienzo(0.25 + DUR + 0.05)
    A.place(seq, haz.genera(), 0)
    A.place(seq, x, 0.25)
    seq *= A.db(-1.0 - A.pico_real(seq)) if A.pico_real(seq) > -1.0 else 1.0
    p = A.ruta("audio", "sfx", "_preview", "dec_lanzamiento.wav")
    A.export(seq, p)
    A.espectrograma(seq, A.ruta("audio", "sfx", "_preview",
                                "dec_lanzamiento_espectro.png"),
                    rampa="decaimiento")
    print(p)
    return x


if __name__ == "__main__":
    main()
