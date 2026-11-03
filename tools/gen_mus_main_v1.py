"""NOVA - SND-MUS-MAIN v1, el tema principal: «Orbitas».

Ref: NOVA_Sonido.md S.5.2 · partitura comun en tools/nova_tema.py · motor en
tools/nova_musica.py · instrumentos en audio/_libs/ (instala_audio_libs.py).

ASOMBRO SERENO: LA LUZ DEL SISTEMA INTERIOR. Electronica cosmica, calida y
abierta; todo es Surge XT con parches de fabrica, sin un solo sample.

  - LAS ORBITAS son el corazon: tres arpegios en resonancia de Laplace
    (1:2:4, como Io, Europa y Ganimedes), cada uno con su pluck -cristal
    arriba, madera en medio, un pop suave abajo-. Entran por capas: en A
    gira Ganimedes, en la segunda frase se le suma Europa, en el Cinturon
    giran las tres y se cruzan; en el Espacio Profundo solo queda la lenta.
  - EL MOTIVO en una campana de cristal (como la celesta del Cero
    Absoluto), que en el Espacio Profundo pasa a un seno puro y lejano, y
    en la vuelta se dobla una octava arriba con una caja de musica.
  - LOS PADS: uno calido y ancho de base; uno con chispas en las partes de
    luz; uno distante en el Espacio Profundo.
  - UN CORO de pad canta el contracanto en la vuelta: el punto mas alto.
  - EL BAJO: blando y largo en la luz, en pulso de nodo en el Cinturon y en
    la vuelta, sub profundo en el Espacio Profundo.

    .venv\\Scripts\\python tools\\gen_mus_main_v1.py
"""

import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nova_musica as M  # noqa: E402
import nova_tema as T  # noqa: E402

VERSION = 1
SEMILLA = 8100
CARPETA = M.ruta("audio", "music", "main", f"v{VERSION}")


def en(notas, *secciones):
    """Las notas cuyo inicio cae en esas secciones del bucle."""
    rangos = [(T.inicio_seccion(s), T.inicio_seccion(s) + 32) for s in secciones]
    return [n for n in notas if any(a <= n.t < b for a, b in rangos)]


def compases(notas, desde, hasta):
    """Las notas entre dos compases del bucle (1 = el primero, inclusive)."""
    return [n for n in notas if 4 * (desde - 1) <= n.t < 4 * hasta]


def partitura_bucle(rng):
    acordes = T.acordes_notas(T.BUCLE, vel=72)
    io, europa, ganimedes = T.orbitas(T.BUCLE, vel=(62, 58, 60), registros=(2, 1, 0))
    mel = T.melodia_bucle()
    bajo_luz = T.bajo_notas(T.BUCLE, "redonda", vel=82)
    bajo_pulso = T.bajo_notas(T.BUCLE, "pulso", vel=86)
    return {
        "pad":       en(acordes, "A", "B", "A'", "D"),
        "pad_hondo": T.acordes_notas(T.SECCIONES["C"], vel=70, desde=T.inicio_seccion("C")),
        "chispas":   en(T.acordes_notas(T.BUCLE, vel=64, octava=1), "B", "A'"),
        "io":        en(io, "B", "A'") + compases(io, 33, 36),
        "europa":    compases(europa, 5, 8) + en(europa, "B", "A'", "D"),
        "ganimedes": en(ganimedes, "A", "B", "A'", "D")
                     + [n for n in en(ganimedes, "C") if (n.t % 2) == 0],
        "campana":   T.humaniza(en(mel, "A", "B", "A'", "D"), rng, ms=4, vel=4),
        "seno":      en(mel, "C"),
        "cajita":    M.desplaza(en(mel, "A'"), 0, 12, vel=70),
        "coro":      M.desplaza(T.CONTRACANTO, T.inicio_seccion("A'")),
        "bajo":      en(bajo_luz, "A", "D") + en(bajo_pulso, "B", "A'"),
        "sub":       en(bajo_luz, "C"),
    }


def partitura_intro():
    acordes = T.acordes_notas(T.INTRO, vel=66)
    io, europa, ganimedes = T.orbitas(T.INTRO, vel=(52, 54, 56))
    return {
        "pad_hondo": acordes,
        "chispas":   [n for n in T.acordes_notas(T.INTRO, vel=58, octava=1) if n.t >= 4],
        "ganimedes": [n for n in ganimedes if n.t >= 4],
        "europa":    [n for n in europa if n.t >= 12],
        "campana":   T.MELODIA_INTRO,
        "sub":       T.bajo_notas(T.INTRO, "redonda", vel=78),
    }


#           parche de fabrica de Surge XT          nivel  pan   sala
INSTRUMENTOS = {
    "pad":       ("Pads/MKS-70 Warm Pad",          -23.5, 0.0, 0.25),
    "pad_hondo": ("Pads/Distant",                  -23.5, 0.0, 0.30),
    "chispas":   ("Pads/Sparkly",                  -31.0, 0.0, 0.30),
    "io":        ("Plucks/Diamonds",               -31.5, 0.35, 0.30),
    "europa":    ("Plucks/Nice Pluck 1",           -30.0, -0.30, 0.30),
    "ganimedes": ("Plucks/Soft Space Oboe Pops",   -28.5, 0.10, 0.25),
    "campana":   ("Plucks/Fantasy Bell",           -22.5, 0.0, 0.35),
    "seno":      ("Leads/Sine Lead",               -24.0, 0.0, 0.40),
    "cajita":    ("Plucks/Magic Music Box",        -30.0, 0.20, 0.35),
    "coro":      ("Pads/Ooh",                      -27.0, -0.10, 0.30),
    "bajo":      ("Basses/Mellow",                 -25.0, 0.0, 0.05),
    "sub":       ("Basses/Sub 2",                  -26.0, 0.0, 0.03),
}


def toca(partes, dur_s):
    voces = []
    for nombre, notas in partes.items():
        patch, nivel, pan, sala = INSTRUMENTOS[nombre]
        t0 = time.time()
        x = M.surge(patch, notas, T.T, dur_s)
        print(f"  {nombre:10s} {patch:30s} {len(notas):4d} notas  {time.time() - t0:5.1f} s")
        voces.append(M.Voz(nombre, x, nivel, pan, sala))
    return voces


def main():
    rng = np.random.default_rng(SEMILLA)
    ir = M.ir_sala(rng, rt60=3.2, predelay=0.025, oscuridad=0.45)
    bucle = partitura_bucle(rng)
    intro = partitura_intro()

    print("bucle:")
    vb = toca({k: T.linea_bucle(v) for k, v in bucle.items()}, T.DUR_RENDER_BUCLE)
    mb = T.aplica_arco(M.equilibra(vb, T.DUR_RENDER_BUCLE, ir), T.ARCO_BUCLE,
                       periodo=T.T.s(T.BUCLE_P))
    print("intro:")
    vi = toca(intro, T.DUR_RENDER_INTRO)
    # la intro se equilibra con los mismos objetivos que el bucle
    mi = T.aplica_arco(M.equilibra(vi, T.DUR_RENDER_INTRO, ir), T.ARCO_INTRO)

    pref = "mus_main"
    r = M.produce_tema(CARPETA, pref, mi, mb, T.T, T.T.s(T.INTRO_P), T.T.s(T.BUCLE_P))
    pistas = {k: v for k, v in intro.items()}
    midi = {}
    for k in set(intro) | set(bucle):
        midi[k] = intro.get(k, []) + M.desplaza(bucle.get(k, []), T.INTRO_P)
    M.exporta_midi(midi, T.T, os.path.join(CARPETA, f"{pref}.mid"))
    M.espectrograma_tema(r["escucha"], os.path.join(CARPETA, f"{pref}_espectro.png"))
    print(f"intro {r['intro']['dur']:.2f} s   bucle {r['bucle']['dur']:.3f} s  "
          f"{r['bucle']['lufs']:.1f} LUFS  {r['bucle']['dbtp']:.1f} dBTP  "
          f"costura {r['costura']:.2f}")
    return r


if __name__ == "__main__":
    main()
