"""NOVA - SND-WLD-BEAM-WRM v1, el haz de Agujero de Gusano: «La garganta».

Ref: NOVA_Sonido.md S.4 y S.5.1 · arte que acompana: ART-WLD-BEAM-WRM v1
(«El desgarro», tools/gen_beam_wormhole_v1.py) · NOVA_GameDesign_Spec
7.2.2 (0.25 s de haz) y 7.5.

Suena en PowerBeamSystem.OnLaunched con Type == Wormhole, desde el Pulsar
que lanza. 0.25 s de haz y unos 80 ms de salida, que es aire aspirado hacia
SND-PWR-WRM.

EL AGUJERO DE GUSANO NO MANDA NADA: SE SALTA EL ESPACIO. El arte no dibuja
luz que viaja sino una rasgadura con el centro oscuro y otro cielo dentro.
El sonido sigue sus tres frames:

  0. DESGARRO (0-83 ms). La tela del espacio se rasga: fibras que se rompen
     de una en una, en rafagas, sobre un golpe de presion; y del corte
     saltan chispas, como en el arte.
  1. GARGANTA (83-167 ms). La rasgadura se abre y por ella se oye OTRO
     CIELO: un acorde lejano de estrellas -Si, Fa#, Do#, Re#- que no
     pertenece al tablero, oido a traves de la garganta: pasa por el mismo
     peine que el grave, como si llegara por dos caminos de largo distinto.
     Es el frame de mas luz y el del acorde.
  2. CIERRE (167-250 ms). La garganta vuelve a una ranura: el aire se aspira
     hacia dentro -ruido que crece y se abre, un barrido al reves- y de las
     estrellas quedan unos pocos destellos sueltos.

DEBAJO DE TODO, EL TIRON (la receta de S.5.1). Un grave que sube de 60 a
180 Hz durante el haz entero, por un flanger cuyo peine sube con el: el
espacio se dobla y el oido lo oye como una succion. Saturado, para que el
grave tenga armonicos y se oiga tambien en altavoces pequenos. Es la
familia del poder: grave, envolvente, con tiron de tono (S.4 #4).

    .venv\\Scripts\\python tools\\gen_snd_beam_wormhole_v1.py
"""

import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nova_audio as A  # noqa: E402

SEMILLA = 7500
HAZ = 0.25           # 7.2.2
FRAME = 1 / 12
DUR = 0.33           # el haz y la aspiracion
LUFS = -17.0         # como los otros dos haces

OTRO_CIELO = [("B5", 0.9), ("F#6", 0.75), ("C#7", 0.55), ("D#7", 0.45)]


def genera(semilla=SEMILLA):
    rng = np.random.default_rng(semilla)
    t = A.tiempo(DUR)
    mix = A.lienzo(DUR)
    k = np.clip(t / HAZ, 0, 1)

    # 1. EL TIRON: grave de 60 a 180 Hz por un peine que sube con el.
    f = 60 * 3 ** (k ** 0.9)
    fase = 2 * np.pi * np.cumsum(f) / A.SR
    grave = (np.sin(fase) + 0.55 * A.sierra(fase + 0.7, 180, techo=2500)
             + 0.25 * np.sin(fase * 0.5))           # la suboctava: el peso
    grave = A.satura(grave * 0.7, 2.2)
    grave = A.flanger(grave, 6.0 * (0.6 / 6.0) ** k, profundidad=0.9)
    grave *= A.env_tramos(DUR, [(0, 0), (0.010, 0.55), (FRAME, 0.85),
                                (0.15, 1.0), (HAZ, 0.85),
                                (HAZ + 0.06, 0.0), (DUR, 0.0)])
    A.place(mix, grave / np.max(np.abs(grave)), 0, 0.40)

    # 2. EL DESGARRO: la tela que se rasga y las chispas del corte.
    dens = np.where(t < 0.095, 14000 * np.clip(t / 0.03, 0.15, 1)
                    * np.exp(-np.maximum(t - 0.03, 0) / 0.03), 0)
    tela = A.rasgado(rng, DUR, dens, banda=(600, 5500))
    A.place(mix, tela, 0, 0.62)

    chispas = A.escarcha(rng, DUR, 2500 * np.exp(-t / 0.035) * (t < 0.11),
                         banda=(4000, 12000))
    A.place(mix, chispas / np.max(np.abs(chispas)), 0, 0.40)

    tp = A.tiempo(0.12)
    fp = 48 + (95 - 48) * np.exp(-tp / 0.02)
    presion = np.sin(2 * np.pi * np.cumsum(fp) / A.SR) * A.env_ad(0.12, 0.001, 0.035)
    A.place(mix, A.satura(presion, 1.5), 0, 0.40)

    # 3. EL OTRO CIELO: un acorde lejano oido a traves de la garganta.
    cielo = A.lienzo(DUR)
    for nm, g in OTRO_CIELO:
        fc = A.nota(nm)
        brillo = np.sin(2 * np.pi * fc * t + rng.uniform(0, 6.3))
        brillo *= 1 + 0.25 * np.sin(2 * np.pi * rng.uniform(9, 15) * t)  # titila
        A.place(cielo, brillo, 0, g)
    cielo *= A.env_tramos(DUR, [(0, 0), (0.06, 0.0), (0.105, 1.0),
                                (0.15, 0.9), (0.20, 0.0), (DUR, 0.0)])
    cielo = A.flanger(cielo, 2.5 + 1.5 * np.sin(2 * np.pi * 7 * t), 0.8, etapas=1)
    A.place(mix, cielo / np.max(np.abs(cielo)), 0, 0.42)

    # ...y lo que queda de el en el cierre: destellos sueltos.
    for t0, nm in [(0.178, "F#7"), (0.205, "C#7"), (0.236, "D#7")]:
        A.place(mix, A.campanita(A.nota(nm), 0.08, rng, brillo=0.4, largo=0.03),
                t0, 0.20)

    # 4. LA ASPIRACION: el aire se va hacia dentro, un barrido al reves que
    #    desemboca en OnLanded.
    asp = A.ruido_rosa(rng, DUR)
    asp = A.svf(asp, 250 * (3500 / 250) ** np.clip((t - 0.15) / 0.12, 0, 1), 1.3,
                salida="lp")
    asp *= A.env_tramos(DUR, [(0, 0), (0.15, 0.0), (0.245, 1.0),
                              (0.265, 0.0), (DUR, 0.0)])
    A.place(mix, asp / np.max(np.abs(asp)), 0, 0.28)

    # Espacio: una sala pequena y oscura, sin cola (S.4 #5).
    mix = A.sala(mix, rng, cola=0.05, mezcla=0.20, brillo=5500)

    mix = A.cortar(mix, DUR - 0.035, 0.035)
    mix = A.fade(mix, dentro=0.0005)
    return A.normaliza(mix, LUFS)


def main():
    x = genera()
    base = os.path.join("audio", "sfx", "beam_wormhole", "v1")
    wav = A.ruta(base, "snd_wld_beam_wrm.wav")
    m = A.export(x, wav)
    A.espectrograma(x, A.ruta(base, "snd_wld_beam_wrm_espectro.png"),
                    rampa="gusano")
    print(f"{wav}\n  {m['dur']:.3f} s  {m['lufs']:.1f} LUFS  {m['dbtp']:.1f} dBTP")
    return x


if __name__ == "__main__":
    main()
