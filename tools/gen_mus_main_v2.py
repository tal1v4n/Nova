"""NOVA - SND-MUS-MAIN v2, el tema principal: «Carta estelar».

Ref: NOVA_Sonido.md S.5.2 · partitura comun en tools/nova_tema.py · motor en
tools/nova_musica.py · muestras de VSCO 2 CE (CC0) por sfizz y Surge XT.

CINEMATOGRAFICA Y EMOTIVA: EL VIAJE ENTERO. Una pequena orquesta real -
cuerdas por secciones, arpa, piano, flauta, organo, glockenspiel, timbales- y
debajo, apenas, un pad y un sub de sintetizador que le dan el aire del
espacio. El motivo pasa de mano en mano, como en una banda sonora:

  intro  El organo abre un pedal de Re, los violines entran arriba en
         trémolo lejano y la flauta lanza la llamada Re-La. El arpa sube.
  A      EL PIANO dice el motivo, solo y cerca: es la primera impresion.
         Cuerdas en pianissimo, el arpa hace las orbitas lentas.
  B      La materia fluye en PIZZICATO: violines, violas y chelos pellizcan
         las tres orbitas de Laplace; la flauta y las violas cantan los
         envios que suben de quinta en quinta.
  C      Espacio profundo: los CHELOS cantan el motivo aumentado en Sib
         lidio sobre el organo y el contrabajo; los violines quedan arriba,
         en trémolo, como estrellas que tiemblan. Un redoble de timbal crece
         hacia la vuelta.
  A'     Toda la orquesta: los violines llevan el motivo, el glockenspiel lo
         dobla dos octavas arriba, el organo pleno sostiene la armonia y las
         violas cantan el contracanto. El timbal marca la llegada a Re.
  D      Vuelve el piano con la llamada y su eco, y el arpa se apaga.

    .venv\\Scripts\\python tools\\gen_mus_main_v2.py
"""

import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nova_musica as M  # noqa: E402
import nova_tema as T  # noqa: E402
from nova_musica import Nota, m  # noqa: E402

VERSION = 2
SEMILLA = 8200
CARPETA = M.ruta("audio", "music", "main", f"v{VERSION}")

DINAMICA = {"A": 52, "B": 66, "C": 50, "A'": 100, "D": 48}   # velocidad por seccion


def por_seccion(lista_acordes, fn):
    """Aplica fn(acorde, compas, velocidad) a cada compas del bucle."""
    out = []
    for i, a in enumerate(lista_acordes):
        sec = T.ORDEN[i // 8]
        out += fn(a, i, DINAMICA[sec])
    return out


def ligadas(notas):
    """Une notas iguales seguidas (mismo tono, una empieza donde acaba la
    otra): una cuerda no reataca un acorde que se repite."""
    notas = sorted(notas, key=lambda n: (n.midi, n.t))
    out = []
    for n in notas:
        if out and out[-1].midi == n.midi and abs(out[-1].t + out[-1].dur - n.t) < 1e-6:
            out[-1] = Nota(out[-1].t, out[-1].dur + n.dur, n.midi, out[-1].vel)
        else:
            out.append(n)
    return sorted(out, key=lambda n: n.t)


def cuerdas(lista, desde=0.0, dinamica=None):
    """Reparte cada acorde entre violines (las dos notas de arriba, una
    octava mas alta), violas (las dos de en medio) y chelos (la de abajo y
    la fundamental)."""
    vl, va, vc, cb = [], [], [], []
    for i, a in enumerate(lista):
        v = dinamica(i) if dinamica else 60
        voz = sorted(T.voicing(a))
        t = desde + 4 * i
        vl += [Nota(t, 4, n + 12, v) for n in voz[-2:]]
        va += [Nota(t, 4, n, v - 4) for n in voz[1:3]]
        vc += [Nota(t, 4, voz[0], v - 2), Nota(t, 4, T.bajo_de(a) + 12, v - 6)]
        cb += [Nota(t, 4, T.bajo_de(a), v)]
    return [ligadas(x) for x in (vl, va, vc, cb)]


def partitura_bucle(rng):
    din = lambda i: DINAMICA[T.ORDEN[i // 8]]
    vl, va, vc, cb = cuerdas(T.BUCLE, dinamica=din)
    io, europa, ganimedes = T.orbitas(T.BUCLE, vel=(70, 74, 78), registros=(1, 1, 0))
    mel = T.melodia_bucle()
    a2 = T.inicio_seccion("A'")
    c0 = T.inicio_seccion("C")
    # redoble de timbal en los dos ultimos compases de C, hacia Re
    redoble = [Nota(a2 - 6, 6, m("A2"), 70)]
    golpe = [Nota(a2, 2, m("D2"), 108), Nota(a2 + 16, 2, m("A2"), 90)]
    return {
        "violines": T.humaniza(T.en(vl, "A", "B", "D"), rng),
        "violas":   T.humaniza(T.en(va, "A", "B", "C", "D"), rng),
        "chelos":   T.humaniza(T.en(vc, "A", "B", "D"), rng),
        "contrabajos": T.humaniza(cb, rng),
        "trem":     [Nota(n.t, n.dur, n.midi, 46) for n in ligadas(T.en(vl, "C"))],
        "piano":    T.humaniza(T.en(mel, "A", "D"), rng, ms=10, vel=6),
        "arpa":     T.humaniza(T.en(ganimedes, "A", "C", "D")
                               + T.en(europa, "A'") + T.compases(europa, 5, 8), rng, ms=5),
        "pizz_vl":  T.humaniza(T.en(io, "B"), rng, ms=6),
        "pizz_va":  T.humaniza(T.en(europa, "B"), rng, ms=6),
        "pizz_vc":  T.humaniza(T.en(ganimedes, "B"), rng, ms=6),
        "flauta":   T.humaniza(M.desplaza(T.en(mel, "B"), 0, 0, vel=88), rng),
        "chelo_solo": T.humaniza(M.desplaza(T.en(mel, "C"), 0, -12, vel=90), rng),
        "organo":   T.acordes_notas(T.SECCIONES["C"], vel=60, desde=c0),
        "organo_pleno": T.acordes_notas(T.SECCIONES["A'"], vel=90, desde=a2),
        "violines_tema": T.humaniza(M.desplaza(T.en(mel, "A'"), 0, 0, vel=104), rng),
        "violas_tema": T.humaniza(M.desplaza(T.CONTRACANTO, a2, 0, vel=92), rng),
        "chelos_pleno": T.humaniza(T.en(vc, "C", "A'"), rng),
        "violines_pleno": T.humaniza(T.en(vl, "A'"), rng),
        # el glockenspiel de VSCO va de Sol4 a Do7: lo que sale se pliega
        "glock":    [Nota(n.t, n.dur, n.midi - 12 if n.midi > 96 else n.midi, n.vel)
                     for n in M.desplaza(T.en(mel, "A'"), 0, 12, vel=74)
                     + [n for n in M.desplaza(T.en(mel, "A"), 0, 12, vel=48) if n.dur >= 2]],
        "redoble":  redoble,
        "timbal":   golpe,
        "pad":      T.acordes_notas(T.BUCLE, vel=60),
        "sub":      T.en(T.bajo_notas(T.BUCLE, "redonda", vel=80), "C", "A'"),
    }


def partitura_intro(rng):
    pedal = [Nota(0, 16, m("D3"), 64), Nota(0, 16, m("A3"), 60)]
    trem = [Nota(0, 16, m("A5"), 40), Nota(4, 12, m("D6"), 40), Nota(8, 8, m("B5"), 42)]
    _, europa, ganimedes = T.orbitas(T.INTRO, vel=(60, 62, 64), registros=(1, 1, 0))
    return {
        "organo":   pedal + [Nota(8, 4, m("F3"), 58), Nota(12, 4, m("B3"), 58)],
        "trem":     trem,
        "flauta":   T.MELODIA_INTRO,
        "arpa":     [n for n in ganimedes if n.t >= 4] + [n for n in europa if n.t >= 12],
        "contrabajos": [Nota(0, 8, m("D2"), 50), Nota(8, 4, m("Bb1"), 54), Nota(12, 4, m("D2"), 56)],
        "pad":      T.acordes_notas(T.INTRO, vel=52),
    }


#   pista           instrumento (sfz: VSCO, o parche Surge)  nivel  pan   sala
INSTRUMENTOS = {
    "violines":      ("sfz:ViolinEnsSusVib",       -27.0, -0.35, 0.30),
    "violines_pleno": ("sfz:ViolinEnsSusVib",      -24.5, -0.35, 0.30),
    "violines_tema": ("sfz:ViolinEnsSusVib",       -21.5, -0.25, 0.30),
    "violas":        ("sfz:ViolaEnsSusVib",        -28.5, 0.15, 0.30),
    "violas_tema":   ("sfz:ViolaEnsSusVib",        -26.0, 0.15, 0.30),
    "chelos":        ("sfz:CelloEnsSusVib",        -27.5, 0.35, 0.28),
    "chelos_pleno":  ("sfz:CelloEnsSusVib",        -25.0, 0.35, 0.28),
    "chelo_solo":    ("sfz:CelloEnsSusVib",        -22.5, 0.25, 0.30),
    "contrabajos":   ("sfz:ContrabassSusVB",       -28.0, 0.30, 0.22),
    "trem":          ("sfz:ViolinEnsTrem",         -31.0, -0.45, 0.40),
    "piano":         ("sfz:UprightPiano",          -22.0, 0.0, 0.32),
    "arpa":          ("sfz:Harp",                  -27.5, -0.15, 0.32),
    "pizz_vl":       ("sfz:ViolinEnsPizz",         -30.0, -0.40, 0.30),
    "pizz_va":       ("sfz:ViolaEnsPizz",          -29.5, 0.10, 0.30),
    "pizz_vc":       ("sfz:CelloEnsPizz",          -28.5, 0.40, 0.28),
    "flauta":        ("sfz:FluteSusVib",           -23.5, 0.05, 0.35),
    "organo":        ("sfz:OrganQuiet",            -27.0, 0.0, 0.35),
    "organo_pleno":  ("sfz:OrganLoud",             -26.0, 0.0, 0.35),
    "glock":         ("sfz:Glockenspiel",          -31.0, 0.20, 0.35),
    "redoble":       ("sfz:TimpaniRolls",          -27.0, 0.0, 0.30),
    "timbal":        ("sfz:Timpani",               -25.0, 0.0, 0.30),
    "pad":           ("Pads/Verb Pad",             -32.0, 0.0, 0.20),
    "sub":           ("Basses/Sub 2",              -30.0, 0.0, 0.02),
}


def toca(partes, dur_s):
    voces = []
    for nombre, notas in partes.items():
        if not notas:
            continue
        inst, nivel, pan, sala = INSTRUMENTOS[nombre]
        t0 = time.time()
        if inst.startswith("sfz:"):
            corto = any(k in inst for k in ("Trem", "Flute"))
            notas = M.parte_largas(notas, 6 if corto else 8)
            x = M.sfz(inst[4:], notas, T.T, dur_s)
        else:
            x = M.surge(inst, notas, T.T, dur_s)
        print(f"  {nombre:15s} {inst:24s} {len(notas):4d} notas  {time.time() - t0:5.1f} s")
        voces.append(M.Voz(nombre, x, nivel, pan, sala))
    return voces


def main():
    rng = np.random.default_rng(SEMILLA)
    ir = M.ir_sala(rng, rt60=2.6, predelay=0.03, oscuridad=0.35, ancho=1.0)
    bucle = partitura_bucle(rng)
    intro = partitura_intro(rng)

    print("bucle:")
    vb = toca({k: T.linea_bucle(v) for k, v in bucle.items()}, T.DUR_RENDER_BUCLE)
    mb = T.aplica_arco(M.equilibra(vb, T.DUR_RENDER_BUCLE, ir), T.ARCO_BUCLE,
                       periodo=T.T.s(T.BUCLE_P))
    print("intro:")
    vi = toca(intro, T.DUR_RENDER_INTRO)
    mi = T.aplica_arco(M.equilibra(vi, T.DUR_RENDER_INTRO, ir), T.ARCO_INTRO)

    pref = "mus_main"
    r = M.produce_tema(CARPETA, pref, mi, mb, T.T, T.T.s(T.INTRO_P), T.T.s(T.BUCLE_P))
    midi = {k: intro.get(k, []) + M.desplaza(bucle.get(k, []), T.INTRO_P)
            for k in set(intro) | set(bucle)}
    M.exporta_midi(midi, T.T, os.path.join(CARPETA, f"{pref}.mid"))
    M.espectrograma_tema(r["escucha"], os.path.join(CARPETA, f"{pref}_espectro.png"))
    print(f"intro {r['intro']['dur']:.2f} s   bucle {r['bucle']['dur']:.3f} s  "
          f"{r['bucle']['lufs']:.1f} LUFS  {r['bucle']['dbtp']:.1f} dBTP  "
          f"costura {r['costura']:.2f}")
    return r


if __name__ == "__main__":
    main()
