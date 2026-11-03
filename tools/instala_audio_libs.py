"""NOVA - descarga los instrumentos de la musica en audio/_libs/.

Ref: NOVA_Sonido.md S.1.4 y S.7 #5. Nada se instala en la maquina: los
plugins son las ediciones portables (un .vst3 y sus datos en una carpeta) y
`pedalboard` los carga desde aqui sin interfaz. audio/_libs/ esta en
.gitignore; este script es lo que se versiona.

    .venv\\Scripts\\python tools\\instala_audio_libs.py

Idempotente: lo que ya esta descargado no se vuelve a bajar.

  plugins/surge-xt/   Surge XT 1.3.4, portable (GPL-3). Sintetizador.
  plugins/sfizz/      sfizz 1.2.3 (BSD-2). Reproductor de muestras SFZ.
  muestras/vsco2ce/   VSCO 2 Community Edition, rama SFZ (CC0), solo los
                      instrumentos de INSTRUMENTOS_VSCO.
"""

import os
import shutil
import subprocess
import sys
import urllib.request
import zipfile

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LIBS = os.path.join(RAIZ, "audio", "_libs")

ZIPS = {
    "surge-xt": "https://github.com/surge-synthesizer/releases-xt/releases/"
                "download/1.3.4/surge-xt-win64-1.3.4-portable-install.zip",
    "sfizz": "https://github.com/sfztools/sfizz/releases/download/1.2.3/"
             "sfizz-1.2.3-win64.zip",
}

VSCO = "https://github.com/sgossner/VSCO-2-CE.git"
# Carpetas de muestras y los .sfz que las usan (rama SFZ).
INSTRUMENTOS_VSCO = [
    "Strings/Violin Section", "Strings/Viola Section", "Strings/Cello Section",
    "Strings/Solo Contrabass", "Strings/Harp",
    "Keys/Organ", "Keys/Upright Piano",
    "Percussion/Glock", "Percussion/Timpani",
    "Woodwinds/Flute",
]


def baja_zip(nombre, url):
    destino = os.path.join(LIBS, "plugins", nombre)
    if os.path.isdir(destino) and os.listdir(destino):
        print(f"  {nombre}: ya esta")
        return
    os.makedirs(destino, exist_ok=True)
    tmp = destino + ".zip"
    print(f"  {nombre}: descargando {url}")
    urllib.request.urlretrieve(url, tmp)
    with zipfile.ZipFile(tmp) as z:
        z.extractall(destino)
    os.remove(tmp)


def baja_vsco():
    """Clon parcial de la rama SFZ con solo INSTRUMENTOS_VSCO. Despues se
    borra .git: guarda una segunda copia comprimida de las muestras (casi
    1 GB) y la libreria no se actualiza. Para cambiar la lista de
    instrumentos, borra audio/_libs/muestras/vsco2ce y vuelve a ejecutar."""
    destino = os.path.join(LIBS, "muestras", "vsco2ce")
    marca = os.path.join(destino, ".nova_instrumentos")
    lista = "\n".join(INSTRUMENTOS_VSCO)
    if os.path.isfile(marca) and open(marca, encoding="utf-8").read() == lista:
        print("  vsco2ce: ya esta")
        return
    if os.path.isdir(destino):
        shutil.rmtree(destino, onerror=lambda f, p, e: (os.chmod(p, 0o666), f(p)))
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    subprocess.run(["git", "clone", "--depth", "1", "--branch", "SFZ",
                    "--filter=blob:none", "--sparse", VSCO, destino], check=True)
    # en modo cono los .sfz de la raiz entran siempre; las carpetas, a mano
    subprocess.run(["git", "-C", destino, "sparse-checkout", "set"]
                   + INSTRUMENTOS_VSCO, check=True)
    shutil.rmtree(os.path.join(destino, ".git"),
                  onerror=lambda f, p, e: (os.chmod(p, 0o666), f(p)))
    open(marca, "w", encoding="utf-8").write(lista)
    print("  vsco2ce: listo")


def main():
    if shutil.which("git") is None:
        sys.exit("hace falta git en el PATH")
    os.makedirs(LIBS, exist_ok=True)
    for nombre, url in ZIPS.items():
        baja_zip(nombre, url)
    baja_vsco()
    print(LIBS)


if __name__ == "__main__":
    main()
