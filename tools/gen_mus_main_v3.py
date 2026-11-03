"""NOVA - SND-MUS-MAIN v3, el tema principal: «Senal».

Ref: NOVA_Sonido.md S.5.2 · partitura comun en tools/nova_tema.py · motor en
tools/nova_musica.py · Surge XT y sintesis propia (tools/nova_audio.py).

TENSION ESTRATEGICA CONTENIDA: EL INSTRUMENTO DE A BORDO (13.12). La musica
de una cabina que escucha el espacio. Mas oscura y mas ritmica que las
otras dos, con la misma armonia y el mismo motivo:

  - EL PULSO ES UN PULSAR. Cada negra es el latido de una estrella de
    neutrones captado por un radiotelescopio (PSR B0329+54 late cada 0.714
    s; aqui, cada 0.75). Y suena con la fisica real de esa senal: el pulso
    cruza el gas interestelar y se DISPERSA, el agudo llega antes que el
    grave, asi que cada latido es un clic que se deshace en un chirrido
    descendente. Es la misma dispersion de las grietas del Cero Absoluto,
    a escala galactica.
  - LA SENAL: «NOVA» en codigo Morse (-. --- ...- .-), un tono de
    telegrafia sobre ruido de radio. Abre la intro, cruza el Espacio
    Profundo y vuelve en la calma. El nombre del juego, escondido en el
    ritmo.
  - EL BAJO es un ostinato de semicorcheas (raiz, quinta, octava) en el
    Cinturon y en la vuelta: el motor de la nave.
  - EL MOTIVO en un seno puro, como el oscilador de prueba de un aparato; en
    la vuelta lo dobla una onda cuadrada una octava arriba: el guino 8-bit
    al pixel art, sin ser chiptune.
  - EL ESPACIO PROFUNDO es solo radio: estatica, un silbido heterodino que
    se desliza (dos emisoras que se cruzan) y el pulsar, lejos, cada dos
    compases.

    .venv\\Scripts\\python tools\\gen_mus_main_v3.py
"""

import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nova_audio as A  # noqa: E402
import nova_musica as M  # noqa: E402
import nova_tema as T  # noqa: E402
from nova_musica import Nota, m  # noqa: E402

VERSION = 3
SEMILLA = 8300
CARPETA = M.ruta("audio", "music", "main", f"v{VERSION}")


# --------------------------------------------------------------------------
# Sintesis propia: pulsar, Morse, radio
# --------------------------------------------------------------------------

def latido(rng, fuerte):
    """Un pulso de pulsar dispersado: clic -> chirrido que cae. La
    dispersion (nova_audio.disperse) retrasa el grave respecto al agudo."""
    k = A.n(0.18)
    imp = np.zeros(k)
    imp[0] = 1.0
    imp[1:40] = rng.standard_normal(39) * 0.25
    ch = A.disperse(imp, etapas=int(140 + 60 * fuerte), a=-0.75)
    ch = A.bp(ch, 220, 6000)
    t = np.arange(k) / A.SR
    ch *= np.exp(-t / (0.035 + 0.02 * fuerte))
    ch /= np.max(np.abs(ch)) + 1e-12
    # el golpe del receptor: un pulso grave corto, como un bombo de radio
    fg = 48 + 70 * np.exp(-t / 0.012)
    golpe = np.sin(2 * np.pi * np.cumsum(fg) / A.SR) * np.exp(-t / 0.05)
    return 0.8 * ch + (0.55 if fuerte else 0.3) * golpe


def morse_tono(dur, f=1175.0):
    """Un tono de telegrafia: seno con 4 ms de rampa para que no haga clic."""
    t = A.tiempo(dur)
    y = np.sin(2 * np.pi * f * t) + 0.08 * np.sin(2 * np.pi * 2 * f * t)
    r = min(A.n(0.004), len(t) // 2)
    env = np.ones(len(t))
    env[:r] = np.linspace(0, 1, r)
    env[-r:] = np.linspace(1, 0, r)
    return y * env


def estereo(x, rng, ancho=0.3):
    """Mono a estereo con un retardo corto decorrelado (Haas suave)."""
    d = A.n(rng.uniform(0.006, 0.011))
    l = x
    r = np.concatenate([np.zeros(d), x[:-d]]) if d else x
    return np.vstack([l * (1 + ancho) / 2 + r * (1 - ancho) / 2,
                      r * (1 + ancho) / 2 + l * (1 - ancho) / 2])


def pista_sintetica(notas, dur_s, fabrica):
    """Coloca un sonido sintetizado por cada nota (la nota es el evento)."""
    buf = np.zeros(int(round(dur_s * A.SR)))
    for n in notas:
        A.place(buf, fabrica(n), T.T.s(n.t), n.vel / 127)
    return buf


def radio(rng, dur_s, curva):
    """Estatica de radio: ruido rosa por una banda que deriva, con
    crepitar, y un silbido heterodino que se desliza. `curva(t)` es el
    nivel (0..1) en cada instante."""
    k = int(dur_s * A.SR)
    t = np.arange(k) / A.SR
    ruido = A.ruido_rosa(rng, dur_s)
    banda = A.bp(ruido, 500, 3800)
    crepita = A.escarcha(rng, dur_s, 60 + 140 * curva(t), banda=(1500, 7000))
    deriva = 1500 + 900 * np.sin(2 * np.pi * t / 23.0) + 500 * np.sin(2 * np.pi * t / 9.3)
    silbido = np.sin(2 * np.pi * np.cumsum(deriva) / A.SR) * 0.25
    y = (0.6 * banda / np.std(banda) + 0.4 * crepita / (np.std(crepita) + 1e-9)
         + silbido) * curva(t)
    return y


# --------------------------------------------------------------------------
# Partitura
# --------------------------------------------------------------------------

def pulsar_notas(lista, desde=0.0):
    """El latido por seccion: A en 1 y 3; B y A' en cada negra con la
    corchea fantasma antes del 1; C cada dos compases; D en 1 y 3."""
    out = []
    for i, _ in enumerate(lista):
        sec = T.ORDEN[i // 8]
        t0 = desde + 4 * i
        if sec in ("A", "D"):
            out += [Nota(t0, 0.1, 0, 100), Nota(t0 + 2, 0.1, 1, 70)]
        elif sec in ("B", "A'"):
            out += [Nota(t0 + b, 0.1, 0 if b == 0 else 1, 105 if b == 0 else 72)
                    for b in range(4)]
            out.append(Nota(t0 + 3.5, 0.1, 1, 40))
        elif sec == "C" and i % 2 == 0:
            out.append(Nota(t0, 0.1, 0, 64))
    return out


def partitura_bucle(rng):
    mel = T.melodia_bucle()
    io, europa, ganimedes = T.orbitas(T.BUCLE, vel=(64, 60, 62), registros=(2, 1, 0))
    c0, a2, d0 = (T.inicio_seccion(s) for s in ("C", "A'", "D"))
    return {
        "pad":     T.en(T.acordes_notas(T.BUCLE, vel=64), "A", "B", "A'", "D"),
        "pad_c":   T.acordes_notas(T.SECCIONES["C"], vel=60, desde=c0),
        "bajo":    T.en(T.bajo_notas(T.BUCLE, "pulso", vel=84), "A", "D")
                   + T.en(T.bajo_notas(T.BUCLE, "ostinato", vel=92, octava=1), "B", "A'"),
        "sub":     T.en(T.bajo_notas(T.BUCLE, "redonda", vel=82), "C", "B", "A'"),
        "seno":    T.en(mel, "A", "C", "A'", "D"),
        "envios":  T.en(mel, "B"),
        "chip":    M.desplaza(T.en(mel, "A'"), 0, 12, vel=72),
        "chip_io": T.en(io, "A'") + T.compases(io, 13, 16),
        "europa":  T.en(europa, "B", "A'"),
        "ganimedes": T.en(ganimedes, "A", "D") + T.compases(ganimedes, 9, 16),
        "pulsar":  pulsar_notas(T.BUCLE),
        "morse":   T.morse_nova(0, c0 + 4) + T.morse_nova(0, d0 + 18, vel=60),
        "radio":   [],
    }


def partitura_intro():
    return {
        "pad_c":  T.acordes_notas(T.INTRO, vel=56),
        "morse":  T.morse_nova(0, 0.5),
        "pulsar": [Nota(t, 0.1, 0 if t % 4 == 0 else 1, 50 + 4 * int(t))
                   for t in range(8, 16)],
        "seno":   T.MELODIA_INTRO,
        "sub":    [Nota(8, 8, m("D2"), 70)],
        "radio":  [],
    }


#   pista       instrumento                     nivel  pan    sala
INSTRUMENTOS = {
    "pad":       ("Pads/Still",                  -25.0, 0.0, 0.22),
    "pad_c":     ("Pads/Distant",                -24.5, 0.0, 0.30),
    "bajo":      ("Basses/Square Bass",          -25.0, 0.0, 0.04),
    "sub":       ("Basses/Sub 2",                -27.0, 0.0, 0.02),
    "seno":      ("Leads/Sine Lead",             -22.5, 0.0, 0.30),
    "envios":    ("Leads/µcomputer",             -25.0, 0.10, 0.28),
    "chip":      ("Plucks/Pure Square",          -28.0, -0.20, 0.25),
    "chip_io":   ("Plucks/Pure Square",          -32.0, 0.35, 0.25),
    "europa":    ("Plucks/Soft Space Oboe Pops", -30.0, -0.30, 0.25),
    "ganimedes": ("Plucks/Soft Space Oboe Pops", -29.0, 0.20, 0.25),
    "pulsar":    ("py:pulsar",                   -24.0, 0.0, 0.18),
    "morse":     ("py:morse",                    -26.0, 0.25, 0.35),
    "radio":     ("py:radio",                    -33.0, 0.0, 0.10),
}


def curva_radio_bucle(periodo):
    c0, a2 = T.T.s(T.inicio_seccion("C")), T.T.s(T.inicio_seccion("A'"))

    def f(t):
        tt = t % periodo
        base = 0.15
        en_c = np.clip((tt - c0 + 1.5) / 1.5, 0, 1) * np.clip((a2 - tt) / 2.0, 0, 1)
        return base + 0.85 * en_c
    return f


def toca(partes, dur_s, rng, curva_radio):
    voces = []
    for nombre, notas in partes.items():
        inst, nivel, pan, sala = INSTRUMENTOS[nombre]
        t0 = time.time()
        if inst == "py:pulsar":
            mono = pista_sintetica(notas, dur_s, lambda n: latido(rng, n.midi == 0))
            x = estereo(mono, rng, 0.5)
        elif inst == "py:morse":
            mono = pista_sintetica(notas, dur_s, lambda n: morse_tono(T.T.s(n.dur)))
            x = estereo(mono, rng, 0.2)
        elif inst == "py:radio":
            x = estereo(radio(rng, dur_s, curva_radio), rng, 0.9)
        elif not notas:
            continue
        else:
            x = M.surge(inst, notas, T.T, dur_s)
        print(f"  {nombre:10s} {inst:28s} {len(notas):4d} notas  {time.time() - t0:5.1f} s")
        voces.append(M.Voz(nombre, x, nivel, pan, sala))
    return voces


def main():
    rng = np.random.default_rng(SEMILLA)
    ir = M.ir_sala(rng, rt60=2.2, predelay=0.02, oscuridad=0.6, ancho=1.0)
    bucle = partitura_bucle(rng)
    intro = partitura_intro()
    per = T.T.s(T.BUCLE_P)

    print("bucle:")
    vb = toca({k: T.linea_bucle(v) for k, v in bucle.items()}, T.DUR_RENDER_BUCLE, rng,
              curva_radio_bucle(per))
    mb = T.aplica_arco(M.equilibra(vb, T.DUR_RENDER_BUCLE, ir), T.ARCO_BUCLE, periodo=per)
    print("intro:")
    vi = toca(intro, T.DUR_RENDER_INTRO, rng,
              lambda t: 0.9 * np.clip(1 - (t - 9) / 4, 0.1, 1))
    mi = T.aplica_arco(M.equilibra(vi, T.DUR_RENDER_INTRO, ir), T.ARCO_INTRO)

    pref = "mus_main"
    r = M.produce_tema(CARPETA, pref, mi, mb, T.T, T.T.s(T.INTRO_P), per)
    midi = {k: intro.get(k, []) + M.desplaza(bucle.get(k, []), T.INTRO_P)
            for k in set(intro) | set(bucle) if k != "radio"}
    M.exporta_midi(midi, T.T, os.path.join(CARPETA, f"{pref}.mid"))
    M.espectrograma_tema(r["escucha"], os.path.join(CARPETA, f"{pref}_espectro.png"))
    print(f"intro {r['intro']['dur']:.2f} s   bucle {r['bucle']['dur']:.3f} s  "
          f"{r['bucle']['lufs']:.1f} LUFS  {r['bucle']['dbtp']:.1f} dBTP  "
          f"costura {r['costura']:.2f}")
    return r


if __name__ == "__main__":
    main()
