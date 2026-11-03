"""NOVA - SND-WLD-BEAM-DEC v1, el haz de Decaimiento: «La vida media».

Ref: NOVA_Sonido.md S.4 y S.5.1 · arte que acompana: ART-WLD-BEAM-DEC v1
(«La cadena de desintegracion», tools/gen_beam_decay_v1.py) ·
NOVA_GameDesign_Spec 7.2.2 (0.25 s de haz) y 7.4.

Suena en PowerBeamSystem.OnLaunched con Type == Decay, desde el Pulsar que
lanza. 0.25 s de haz y unos 70 ms de salida para fundirse con SND-PWR-DEC.

EL SONIDO CUMPLE LA LEY DEL HAZ. El arte desintegra su cadena al pixel: en
cada frame sobrevive la mitad de los atomos (1, 1/2, 1/4). Aqui pasa lo
mismo, con una vida media de un frame (83 ms):

  - 320 ATOMOS, los de una tesela del haz. Cada uno tiene su instante de
    desintegracion, sacado de la ley exponencial, y cada desintegracion es
    un CLIC DE CONTADOR GEIGER. Al lanzar, el contador fríe: casi tres mil
    clics por segundo que se funden en un chisporroteo. A cada frame quedan
    la mitad, y al final del haz son clics sueltos. Es el sonido que todo el
    mundo reconoce como radiacion, y aqui no es decoracion: es el recuento.
  - LA CADENA se oye como un zumbido de dos hebras -dos sierras a distancia
    de triton, multiplicadas entre si (modulacion en anillo), que da los
    parciales inarmonicos de algo electrico y enfermo- y ESE ZUMBIDO TAMBIEN
    SE DESINTEGRA: esta partido en granos de 5 ms y cada grano es un atomo
    con la misma vida media. Entra entero, a la mitad chisporrotea y al
    final son esquirlas. El sonido se cae a trozos como la cadena.
  - LOS ESLABONES. «Cadena de desintegracion» es la serie de nucleos que se
    convierten unos en otros. Cada frame es un nucleo distinto y el zumbido
    BAJA UN SEMITONO por eslabon: Sol# -> Sol -> Fa#. El cromatismo
    descendente es el lamento del cine; el Sol# es el triton del Re del
    Cero Absoluto: el polo opuesto de la misma escala.
  - LA EMISION. Una de cada ocho desintegraciones suelta su particula beta:
    un silbido de pocos ms que cae de 7 a 2 kHz, como el arte, que lanza
    una particula hacia fuera por cada atomo que se apaga.

Y el disparo: una rociada toxica (ruido por un paso banda que cae, banda
media, S.4 #4) y un golpe de cuerpo moderado; el grave es del Agujero de
Gusano y no se le pisa.

    .venv\\Scripts\\python tools\\gen_snd_beam_decay_v1.py
"""

import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nova_audio as A  # noqa: E402

SEMILLA = 7400
HAZ = 0.25           # 7.2.2
FRAME = 1 / 12       # la vida media: un frame del haz
DUR = 0.32           # el haz y su salida
LUFS = -17.0         # como el haz de Cero Absoluto

ATOMOS = 320         # una tesela de ART-WLD-BEAM-DEC
ESLABONES = ["G#3", "G3", "F#3"]   # un nucleo por frame
TRITON = 2 ** (6 / 12)


def tono_cadena(t):
    """Frecuencia de la hebra baja: un escalon por frame, con 10 ms de
    deslizamiento entre eslabones (el nucleo que se convierte)."""
    fs = [A.nota(e) for e in ESLABONES]
    f = np.full_like(t, fs[0])
    for i in (1, 2):
        b = i * FRAME
        k = np.clip((t - b) / 0.010, 0, 1)
        f = f * (1 - k) + fs[i] * k
    return f


def genera(semilla=SEMILLA):
    rng = np.random.default_rng(semilla)
    t = A.tiempo(DUR)
    mix = A.lienzo(DUR)

    # 1. LA CADENA: dos hebras a triton, en contrafase, y su producto.
    f = tono_cadena(t) * A.deriva(rng, DUR, 6, velocidad=9)
    fase_a = 2 * np.pi * np.cumsum(f) / A.SR
    fase_b = 2 * np.pi * np.cumsum(f * TRITON * 1.004) / A.SR + np.pi
    a = A.sierra(fase_a, f.max(), techo=4500)
    b = A.sierra(fase_b, f.max() * TRITON, techo=4500)
    # el anillo, entre las fundamentales: dos parciales limpios e
    # inarmonicos (la suma y la diferencia), no una nube de ruido
    anillo = np.sin(fase_a) * np.sin(fase_b)
    cadena = 0.55 * a + 0.40 * b + 0.55 * anillo
    # el zumbido electrico del tubo: 47 Hz de modulacion, como un balastro
    cadena *= 1 + 0.30 * np.sin(2 * np.pi * 47 * t)
    # banda media y una formante que baja: el material que se ablanda
    cadena = A.bp(cadena, 160, 4200)
    forma = A.svf(cadena, np.geomspace(1500, 750, len(t)), 3.5)
    cadena = 0.6 * cadena + 0.9 * forma
    cadena /= np.max(np.abs(cadena))
    cuerpo = A.env_tramos(DUR, [(0, 0), (0.004, 1.0), (HAZ, 0.9),
                                (HAZ + 0.05, 0.0), (DUR, 0.0)])
    cadena = A.desintegra(cadena * cuerpo, rng, FRAME, grano=0.005)
    A.place(mix, cadena, 0, 0.80)

    # 2. EL RECUENTO: 320 atomos, un clic Geiger por desintegracion.
    inst = A.vidas(rng, ATOMOS, FRAME)
    inst = inst[inst < HAZ + 0.04]
    A.place(mix, A.geiger(rng, DUR, inst), 0, 0.26)

    # 3. LA EMISION: particulas beta, una de cada ocho.
    for ti in inst[rng.random(len(inst)) < 1 / 8]:
        tz = A.tiempo(0.007)
        fz = 7000 * (2000 / 7000) ** (tz / 0.007)
        z = np.sin(2 * np.pi * np.cumsum(fz) / A.SR) * A.env_ad(0.007, 0.0005, 0.002)
        A.place(mix, z, ti, rng.uniform(0.04, 0.10))

    # 4. EL DISPARO: rociada toxica y golpe de cuerpo.
    roc = A.ruido_rosa(rng, 0.14)
    roc = A.svf(roc, np.geomspace(2600, 650, len(roc)), 1.6)
    roc *= A.env_ad(0.14, 0.003, 0.045)
    A.place(mix, roc / np.max(np.abs(roc)), 0, 0.30)

    tg = A.tiempo(0.12)
    fg = 90 + (175 - 90) * np.exp(-tg / 0.025)
    golpe = np.sin(2 * np.pi * np.cumsum(fg) / A.SR) * A.env_ad(0.12, 0.001, 0.035)
    A.place(mix, golpe, 0, 0.22)

    siseo = A.hp(A.ruido(rng, 0.05), 4000) * A.env_ad(0.05, 0.0005, 0.012)
    A.place(mix, siseo / np.max(np.abs(siseo)), 0, 0.18)

    # Espacio: una sala pequena y apagada, sin cola (S.4 #5).
    mix = A.sala(mix, rng, cola=0.045, mezcla=0.20, brillo=7000)

    mix = A.cortar(mix, DUR - 0.03, 0.03)
    mix = A.fade(mix, dentro=0.0005)
    return A.normaliza(mix, LUFS)


def main():
    x = genera()
    base = os.path.join("audio", "sfx", "beam_decay", "v1")
    wav = A.ruta(base, "snd_wld_beam_dec.wav")
    m = A.export(x, wav)
    A.espectrograma(x, A.ruta(base, "snd_wld_beam_dec_espectro.png"),
                    rampa="decaimiento")
    print(f"{wav}\n  {m['dur']:.3f} s  {m['lufs']:.1f} LUFS  {m['dbtp']:.1f} dBTP")
    return x


if __name__ == "__main__":
    main()
