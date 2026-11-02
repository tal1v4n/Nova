"""NOVA - SND-WLD-BEAM-ZER v1, el haz de Cero Absoluto: «El aliento polar».

Ref: NOVA_Sonido.md S.4 y S.5.1 · arte que acompana: ART-WLD-BEAM-ZER v1
(«El rosario de escarcha», tools/gen_beam_zero_v1.py) · NOVA_GameDesign_Spec
7.2.2 (0.25 s de haz) y 7.3.

Suena en PowerBeamSystem.OnLaunched con Type == AbsoluteZero, desde el
Pulsar que lanza. 0.25 s de haz y unos 60 ms de salida para fundirse con
SND-PWR-ZER, que arranca en OnLanded.

EL SONIDO SIGUE A LOS TRES FRAMES DEL HAZ, no es un "piu" generico:

  0. NUCLEACION (0-83 ms). Un soplo de aire helado se abre de golpe y el
     primer cristal da su nota. Aun no hay escarcha: solo el aire.
  1. ESCARCHA (83-167 ms). Los copos del rosario crecen: una cascada de
     notas de celesta de cristal baja por el haz, y el chisporroteo de la
     cristalizacion llega a su maximo. Es el frame que se recuerda.
  2. QUIEBRA (167-250 ms). La espina se parte donde estaban los copos: tres
     chasquidos pequenos, como el vidrio que el haz va a dejar en el nodo.

LA TEMPERATURA BAJA. El silbido es ruido de viento por un filtro estrecho
que cae de 3 kHz a 1.2 kHz (la receta de S.5.1) y la cascada de notas lo
recorre por dentro: F#7 E7 C#7 A6 G#6 E6 D6, el acorde de Re lidio de los
dos sonidos de Cero Absoluto. La cuarta aumentada (G#) es el color que el
cine usa para la magia del hielo: suspendido, sin reposo.

RIGIDO. Ningun tono de este sonido tiene vibrato: el haz es el unico trazo
recto del tablero (A.4.2) y el frio no tiembla. El temblor, y el momento en
que se detiene, son de SND-PWR-ZER.

    .venv\\Scripts\\python tools\\gen_snd_beam_zero_v1.py
"""

import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nova_audio as A  # noqa: E402

SEMILLA = 7300
HAZ = 0.25          # 7.2.2
DUR = 0.32          # el haz y su salida
LUFS = -17.0        # un poco por debajo del impacto (-15)

CASCADA = ["F#7", "E7", "C#7", "A6", "G#6", "E6", "D6"]


def genera(semilla=SEMILLA):
    rng = np.random.default_rng(semilla)
    t = A.tiempo(DUR)
    mix = A.lienzo(DUR)

    # La temperatura: 3 kHz -> 1.2 kHz, exponencial, durante todo el haz.
    k = np.clip(t / HAZ, 0, 1)
    fc = 3000 * (1200 / 3000) ** (k ** 0.8)

    # Envolvente del cuerpo: se abre de golpe, se sostiene, y a los 0.25 s
    # cae rapido para dejar sitio al ataque del impacto.
    cuerpo = A.env_tramos(DUR, [(0, 0), (0.006, 1.0), (0.09, 0.85),
                                (0.20, 0.75), (HAZ, 0.55), (DUR, 0.0)])

    # 1. EL SILBIDO: viento entre hielo. Dos filtros sobre el mismo ruido:
    #    uno estrecho (la nota del viento) y uno ancho (el aire).
    r = A.ruido(rng, DUR)
    silbido = A.svf(r, fc, 18.0)
    silbido /= np.max(np.abs(silbido))
    aire = A.svf(r, fc * 1.9, 1.2)
    aire = A.hp(aire, 1800)
    aire /= np.max(np.abs(aire))
    A.place(mix, silbido * cuerpo, 0, 0.42)
    A.place(mix, aire * cuerpo, 0, 0.11)

    # Un hilo de tono puro dentro del silbido, para que la caida se oiga
    # como afinada y no solo como ruido. Muy por debajo: no es un seno, es
    # el alma del viento.
    fase = 2 * np.pi * np.cumsum(fc) / A.SR
    A.place(mix, np.sin(fase) * cuerpo, 0, 0.07)

    # 2. EL SOPLO de la nucleacion: el aire que se aparta al nacer el haz.
    #    Ruido rosa por un paso banda que baja deprisa; da peso sin bajos.
    soplo = A.ruido_rosa(rng, 0.14)
    soplo = A.svf(soplo, np.geomspace(5000, 900, len(soplo)), 0.9)
    soplo *= A.env_ad(0.14, 0.003, 0.035)
    A.place(mix, soplo / np.max(np.abs(soplo)), 0, 0.22)

    # Y un chasquido de escarcha en el instante cero: el ataque.
    A.place(mix, A.grieta(rng, talla=0.35, dur=0.05), 0.0, 0.30)

    # 3. LA CASCADA: celesta de cristal, una nota por copo del rosario.
    #    Empieza en el frame 0 y se espesa en el 1; sube un poco de nivel
    #    hacia el centro, como el brillo del frame de escarcha.
    paso = 0.030
    for i, nm in enumerate(CASCADA):
        t0 = 0.010 + i * paso + rng.uniform(-0.003, 0.003)
        nivel = [0.55, 0.70, 0.85, 0.95, 0.90, 0.80, 0.75][i]
        c = A.campanita(A.nota(nm), 0.20, rng, brillo=0.9, largo=0.085)
        A.place(mix, c, t0, 0.36 * nivel)

    # 4. LA CRISTALIZACION: granos que crecen con el frame de escarcha.
    dens = 1400 * np.exp(-((t - 0.12) / 0.055) ** 2) + 120
    dens[t > HAZ] *= np.exp(-(t[t > HAZ] - HAZ) / 0.02)
    A.place(mix, A.escarcha(rng, DUR, dens), 0, 0.15)

    # 5. LA QUIEBRA: tres grietas pequenas donde estaban los copos.
    for t0, talla, g in [(0.172, 0.30, 0.20), (0.199, 0.42, 0.26),
                         (0.231, 0.36, 0.22)]:
        A.place(mix, A.grieta(rng, talla=talla, piu=0.25, dur=0.06), t0, g)

    # Espacio: una sala pequena y brillante, sin cola (S.4 #5).
    mix = A.sala(mix, rng, cola=0.05, mezcla=0.22)

    mix = A.cortar(mix, DUR - 0.03, 0.03)
    mix = A.fade(mix, dentro=0.0005)
    return A.normaliza(mix, LUFS)


def main():
    x = genera()
    base = os.path.join("audio", "sfx", "beam_zero", "v1")
    wav = A.ruta(base, "snd_wld_beam_zer.wav")
    m = A.export(x, wav)
    A.espectrograma(x, A.ruta(base, "snd_wld_beam_zer_espectro.png"))
    print(f"{wav}\n  {m['dur']:.3f} s  {m['lufs']:.1f} LUFS  {m['dbtp']:.1f} dBTP")
    return x


if __name__ == "__main__":
    main()
