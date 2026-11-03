"""NOVA - SND-MUS-MAIN, el ciclo: v3 «Senal» -> v1 «Orbitas» -> v2 «Carta estelar».

Ref: NOVA_Sonido.md S.5.2. Decidido por el usuario el 2026-11-02: en los
menus suenan las tres versiones del tema, una tras otra y en ese orden, y el
ciclo vuelve a empezar.

Se pueden encadenar porque comparten tempo, tonalidad y forma: cada bucle
acaba en Mi/Re y empieza en Re maj9, igual que cuando se repite a si mismo.
Lo que no comparten es la cola del final, asi que el empalme se hace aqui y
no en Unity (que no funde dos archivos con precision de muestra):

  el principio de la version que entra se funde, durante 0.5 s, desde el
  principio de la que sale. Como cada bucle cierra sobre si mismo sin
  costura (nova_musica.corta_bucle), su principio es justo lo que seguiria a
  su final: el empalme continua la senal y pasa de una a otra.

Lee los WAV de los tres generadores (gen_mus_main_v<n>.py) y escribe:

  audio/music/main/ciclo/mus_main_intro.wav   la intro de la v3 (18 s)
  audio/music/main/ciclo/mus_main_ciclo.wav   v3 + v1 + v2, 360.000 s
  audio/music/main/ciclo/mus_main_escucha.wav la intro y el ciclo, para oirlo

    .venv\\Scripts\\python tools\\gen_mus_main_ciclo.py
"""

import os
import shutil
import sys

import numpy as np
import soundfile as sf

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nova_musica as M  # noqa: E402

ORDEN = ["v3", "v1", "v2"]
FUNDE_S = 0.5
BASE = M.ruta("audio", "music", "main")
CARPETA = os.path.join(BASE, "ciclo")


def lee(v, parte):
    x, sr = sf.read(os.path.join(BASE, v, f"mus_main_{parte}.wav"), always_2d=True)
    assert sr == M.SR
    return x.T.astype(np.float64)


def encadena(bucles, funde_s=FUNDE_S):
    """Une los bucles en un ciclo cerrado: cada uno empieza fundido desde el
    principio del anterior (el ultimo enlaza con el primero)."""
    k = int(round(funde_s * M.SR))
    w = np.sin(np.linspace(0, np.pi / 2, k)) ** 2
    out = []
    for i, b in enumerate(bucles):
        previo = bucles[i - 1]
        b = b.copy()
        b[:, :k] = b[:, :k] * w + previo[:, :k] * (1 - w)
        out.append(b)
    return np.concatenate(out, axis=1)


def main():
    bucles = [lee(v, "loop") for v in ORDEN]
    for v, b in zip(ORDEN, bucles):
        assert b.shape[1] == 120 * M.SR, (v, b.shape)
    ciclo = encadena(bucles)
    os.makedirs(CARPETA, exist_ok=True)
    r = M.exporta(ciclo, os.path.join(CARPETA, "mus_main_ciclo.wav"))
    shutil.copyfile(os.path.join(BASE, ORDEN[0], "mus_main_intro.wav"),
                    os.path.join(CARPETA, "mus_main_intro.wav"))
    # empalmes: el salto en cada union, frente al salto tipico (1 = nada)
    tip = np.percentile(np.abs(np.diff(ciclo, axis=1)), 99)
    n = 120 * M.SR
    for i in range(3):
        a = ciclo[:, (i + 1) * n - 1] if i < 2 else ciclo[:, -1]
        b = ciclo[:, ((i + 1) * n) % (3 * n)]
        print(f"  {ORDEN[i]} -> {ORDEN[(i + 1) % 3]}: salto {np.abs(b - a).max() / tip:.2f}")
    print(f"ciclo {r['dur']:.3f} s  {r['lufs']:.1f} LUFS  {r['dbtp']:.1f} dBTP")

    # escucha: como sonara en juego, la intro y el ciclo entero una vez
    intro = lee(ORDEN[0], "intro")
    k0 = 12 * M.SR
    esc = np.zeros((2, k0 + ciclo.shape[1]))
    esc[:, :intro.shape[1]] += intro
    esc[:, k0:] += ciclo
    esc *= M.db(min(0.0, -1.0 - M.pico_real(esc)))
    M.exporta(esc, os.path.join(CARPETA, "mus_main_escucha.wav"), bits=16)


if __name__ == "__main__":
    main()
