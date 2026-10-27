"""NOVA - Primitivas de generacion de sprites.

Implementacion de NOVA_Arte_Tecnica.md mas las extensiones que el catalogo
de NOVA_Assets_Diseno.md exige y el contrato original no cubria:

  - `esfera(..., calor, apagado)`  desplazamiento radial de banda. Es lo que
    permite que una Estrella N5 tenga nucleo blanco y una N1 se vea apagada
    usando la MISMA rampa y la MISMA geometria.
  - `corona_radial()`              corona / llamaradas en bandas solidas.
  - `halo_trama()`                 glow por dithering Bayer 8x8. Respeta la
    regla de alfa binario: no hay transparencia parcial, hay trama.
  - capa `glow` en el lienzo       pixeles que se ven pero NO generan
    contorno exterior; si no, cada punto del dither saldria perfilado.

NumPy hace todo el calculo. Pillow solo escribe el PNG. ImageDraw no se usa.
"""

import math
import os

import numpy as np
from PIL import Image

from nova_paleta import RAMPAS, CONTORNO as CONTORNO_HEX

# --- Modelo de luz: global e inmutable -------------------------------------

L        = np.array([-0.45, -0.60,  0.66])
HV       = np.array([-0.247, -0.329, 0.911])
UMBRALES = [0.08, 0.28, 0.48, 0.68, 0.86]
CONTORNO = np.array([int(CONTORNO_HEX[i:i + 2], 16) for i in (1, 3, 5)], np.uint8)

# Matriz de Bayer 8x8 normalizada a [0,1). Umbral de dithering ordenado.
BAYER8 = np.array([
    [ 0, 32,  8, 40,  2, 34, 10, 42],
    [48, 16, 56, 24, 50, 18, 58, 26],
    [12, 44,  4, 36, 14, 46,  6, 38],
    [60, 28, 52, 20, 62, 30, 54, 22],
    [ 3, 35, 11, 43,  1, 33,  9, 41],
    [51, 19, 59, 27, 49, 17, 57, 25],
    [15, 47,  7, 39, 13, 45,  5, 37],
    [63, 31, 55, 23, 61, 29, 53, 21],
], np.float32) / 64.0


RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def dir_nodo(nodo, version):
    """Carpeta de salida de un diseno: art/nodes/<nodo>/v<n>/.

    Un nodo puede tener varias propuestas vivas a la vez. Cada una escribe en
    su propia carpeta con los MISMOS nombres de archivo, para que cambiar de
    version sea cambiar de carpeta y no renombrar 34 sprites.
    """
    d = os.path.join(RAIZ, "art", "nodes", nodo, f"v{version}")
    os.makedirs(d, exist_ok=True)
    return d


def dir_estado(estado, version):
    """Salida de una propuesta de overlay: art/states/<estado>/v<n>/.

    Misma forma que `dir_nodo`, y por el mismo motivo: un overlay por carpeta,
    una propuesta por subcarpeta, con los MISMOS nombres de archivo dentro de
    cada una. Sin el nivel `<estado>` la v1 de `sleep` y la v1 de `ign`
    caerian en la misma carpeta y «v1» dejaria de querer decir nada.
    """
    d = os.path.join(RAIZ, "art", "states", estado, f"v{version}")
    os.makedirs(d, exist_ok=True)
    return d


def dir_preview(nodo):
    d = os.path.join(RAIZ, "art", "_preview", nodo)
    os.makedirs(d, exist_ok=True)
    return d


def rampa(nombre):
    """Devuelve la rampa del diccionario maestro como array (7,3) uint8."""
    hexes = RAMPAS[nombre]
    return np.array([[int(h[i:i + 2], 16) for i in (1, 3, 5)] for h in hexes],
                    dtype=np.uint8)


def lienzo(S):
    return {
        "col":   np.zeros((S, S, 3), np.uint8),
        "lleno": np.zeros((S, S), bool),   # cuerpo solido: genera contorno
        "glow":  np.zeros((S, S), bool),   # trama luminosa: NO genera contorno
        "id":    np.full((S, S), -1, np.int16),
        "S":     S,
    }


def _malla(lz, cx, cy):
    S = lz["S"]
    y, x = np.mgrid[0:S, 0:S]
    return x + 0.5 - cx, y + 0.5 - cy


def _bandas(d, extra_rebote):
    b = 6 - np.digitize(d, UMBRALES)
    return np.where(extra_rebote, np.minimum(b, 4), b)


# --- Primitivas ------------------------------------------------------------

def esferoide(lz, cx, cy, rx, ry, phi, ramp, oid, calor=0.0, apagado=0.0):
    """Cuerpo elipsoidal inclinado. `rx` va en la direccion `phi`.

    Generaliza `esfera` sin duplicar el modelo de luz: la normal se calcula
    en el espacio local de la esfera unidad y se rota de vuelta a pantalla,
    asi que sigue iluminandose con la L global de la regla 1 del contrato.
    Es un cambio de coordenadas, no un modelo nuevo.

    Para que sirve: un cuerpo que gira rapido se achata por su ecuador. Con
    `esfera` todos los nodos comparten silueta circular y se parecen entre
    ellos; el achatamiento y la inclinacion son dos ejes de diseno gratis
    para separarlos.

    calor   : aclara las bandas hacia el centro con caida radial suave.
    apagado : oscurece uniformemente.
    """
    dx, dy = _malla(lz, cx, cy)
    co, si = math.cos(phi), math.sin(phi)
    u = ( dx * co + dy * si) / rx
    v = (-dx * si + dy * co) / ry
    e = u * u + v * v
    m = e <= 1.0
    if not m.any():
        return

    nz = np.sqrt(np.clip(1 - e, 0, 1))
    nx = u * co - v * si          # normal local devuelta a pantalla
    ny = u * si + v * co
    d  = nx * L[0] + ny * L[1] + nz * L[2]
    b  = _bandas(d, (e > 0.80) & (d < 0.30)).astype(np.float32)

    b += apagado
    if calor:
        # 1.0 en el nucleo, 0.0 a partir del 74% del radio
        b -= calor * np.clip(1.30 - 1.75 * np.sqrt(np.minimum(e, 1.0)), 0.0, 1.0)
    b = np.clip(np.rint(b), 0, 6).astype(np.int16)

    # el especular se aplica DESPUES del calor: una N1 apagada conserva brillo
    b = np.where(nx * HV[0] + ny * HV[1] + nz * HV[2] > 0.972, 0, b)

    borde = m & (e > 0.86) & lz["lleno"] & (lz["id"] != oid)

    lz["col"][m]     = ramp[b[m]]
    lz["col"][borde] = CONTORNO
    lz["lleno"][m]   = True
    lz["id"][m]      = oid


def esfera(lz, cx, cy, r, ramp, oid, calor=0.0, apagado=0.0):
    """Primitiva base. Nucleones, ojos, cabezas, cuerpos estelares.

    Caso particular de `esferoide` con los dos semiejes iguales. Se conserva
    como funcion propia porque es con diferencia la llamada mas frecuente y
    `esfera(lz, x, y, r, ...)` se lee mejor que repetir el radio y un angulo
    que no significa nada cuando la figura es un circulo.
    """
    esferoide(lz, cx, cy, r, r, 0.0, ramp, oid, calor=calor, apagado=apagado)


def cono_trama(lz, cx, cy, phi, largo, semiancho, ramp, r0=0.0,
               banda_int=2, banda_ext=4, gamma_eje=0.85, gamma_lat=1.8,
               doble=True):
    """Haz conico de luz por dithering Bayer, sobre la capa `glow`.

    Es el contrapunto material de `corona_radial`. Alli el apendice es banda
    solida: hace silueta, lleva contorno y cuenta para la huella de clic.
    Aqui es luz: no marca `lleno`, no genera contorno y no engorda el hitbox.
    Un faro no proyecta materia, y dos nodos que resuelven sus apendices con
    materiales distintos dejan de parecerse aunque compartan paleta.

    phi       : direccion del eje, en radianes.
    largo     : alcance desde el centro hasta donde el haz se apaga.
    gamma_eje : caida a lo largo del eje. Por debajo de 1 el haz se mantiene
                denso casi hasta la punta, que es como se lee un foco.
    gamma_lat : caida a lo ancho. Por encima de 1 el borde del cono queda
                nitido. Los dos van por separado porque con un solo gamma la
                densidad es el producto de dos factores menores que 1 y el
                haz desaparece: 0.5*0.5 elevado a 1.6 es 0.12, por debajo de
                casi toda la matriz de Bayer.
    semiancho : media anchura del cono por unidad de longitud (tan del
                semiangulo). 0.40 son unos 22 grados.
    r0        : distancia a la que arranca el haz. Debe cubrir el cuerpo,
                o la trama se ve pasar por encima del nucleo.
    doble     : dos conos opuestos. Es la geometria real de un pulsar, y la
                simetria bilateral es lo que rompe la lectura radial.
    """
    dx, dy = _malla(lz, cx, cy)
    co, si = math.cos(phi), math.sin(phi)
    a = dx * co + dy * si
    b = -dx * si + dy * co
    aa = np.abs(a) if doble else a

    ancho = np.maximum(aa * semiancho, 1e-6)
    d_eje = np.clip(1.0 - (aa - r0) / max(largo - r0, 1e-6), 0.0, 1.0)
    d_lat = np.clip(1.0 - np.abs(b) / ancho, 0.0, 1.0)
    dens = (d_eje ** gamma_eje) * (d_lat ** gamma_lat)

    S = lz["S"]
    umbral = np.tile(BAYER8, (S // 8 + 1, S // 8 + 1))[:S, :S]
    on = (aa >= r0) & (dens > umbral) & ~lz["lleno"]
    if not on.any():
        return

    banda = np.where(dens > 0.45, banda_int, banda_ext)
    lz["col"][on]  = ramp[banda[on]]
    lz["glow"][on] = True


def _onda(ang, picos, fase, armonicos):
    """Perfil angular de la corona.

    Un solo coseno produce un engranaje: todas las puntas iguales y
    equiespaciadas. Sumando armonicos con peso y desfase propios aparecen
    lenguas largas y cortas alternadas, que es lo que lee como fuego.
    """
    total = np.zeros_like(ang)
    peso = 0.0
    for mult, w, desf in armonicos:
        total += w * (0.5 + 0.5 * np.cos(picos * mult * ang + fase * mult + desf))
        peso += w
    return total / peso


ARMONICOS = ((1.0, 0.62, 0.0), (2.0, 0.24, 1.10), (0.5, 0.14, 0.60))


def corona_radial(lz, cx, cy, r_int, r_ext, picos, fase, ramp, oid,
                  amp=1.0, dureza=1.0, armonicos=ARMONICOS, cizalla=0.0,
                  sesgo_banda=0):
    """Corona / cromosfera. Se dibuja ANTES del cuerpo, nunca despues.

    Dos decisiones sostienen esta primitiva:

    CIZALLA. La onda no se evalua en el angulo del pixel sino en el angulo
    desfasado segun el radio, asi que cada lengua se curva hacia atras al
    alejarse. Sin esto la corona es invariante bajo rotacion de 360/picos
    grados, que es literalmente la definicion de un engranaje, y el ojo lo
    lee como pieza mecanica por mucho que se retoque el color.

    SOMBREADO DESDE EL LIMBO. El gradiente radial se mide desde el borde del
    disco, no desde el centro, para que la franja visible recorra la rampa
    entera: clara en la base, oscura en la punta. Medido desde el centro,
    todo lo visible caeria en el tramo oscuro y la corona se veria plana.
    """
    dx, dy = _malla(lz, cx, cy)
    rad = np.hypot(dx, dy)
    ang = np.arctan2(dy, dx)
    base = r_int * 0.80

    if cizalla:
        avance = np.clip((rad - base) / max(r_ext - base, 1e-6), 0.0, 1.6)
        ang = ang - cizalla * avance

    onda  = _onda(ang, picos, fase, armonicos) ** dureza
    r_lim = r_int + (r_ext - r_int) * ((1.0 - amp) + amp * onda)

    m = rad <= r_lim
    if not m.any():
        return

    rs = np.maximum(rad, 1e-6)
    t  = np.clip((rad - base) / np.maximum(r_lim - base, 1e-6), 0, 1)
    lum = 1.20 - 1.05 * t + 0.28 * ((dx / rs) * L[0] + (dy / rs) * L[1])
    b = np.clip(6 - np.digitize(lum, UMBRALES) + sesgo_banda, 1, 6)

    lz["col"][m]   = ramp[b[m]]
    lz["lleno"][m] = True
    lz["id"][m]    = oid


def halo_trama(lz, cx, cy, ramp, r0=None, grosor=10, banda_int=3, banda_ext=5,
               gamma=1.8, corona=None, fase=0.0, armonicos=ARMONICOS):
    """Glow exterior por dithering Bayer. Cumple la regla de alfa binario.

    Con `corona` (los mismos parametros que se pasaron a corona_radial) el
    glow arranca justo donde termina cada lengua, asi que abraza la silueta
    en vez de encerrarla en un aro. Sin el, es un anillo centrado en r0.

    No marca `lleno`: estos pixeles no generan contorno exterior. Si lo
    generaran, cada punto del dither saldria perfilado y pareceria suciedad.
    """
    dx, dy = _malla(lz, cx, cy)
    rad = np.hypot(dx, dy)

    if corona is not None:
        onda = _onda(np.arctan2(dy, dx), corona["picos"], fase,
                     armonicos) ** corona.get("dureza", 1.0)
        amp = corona.get("amp", 1.0)
        ri = corona["r_int"] + (corona["r_ext"] - corona["r_int"]) * \
            ((1.0 - amp) + amp * onda) + 1.0
    else:
        ri = np.full_like(rad, float(r0))
    re = ri + grosor

    dens = np.clip(1.0 - (rad - ri) / np.maximum(re - ri, 1e-6), 0, 1) ** gamma
    S = lz["S"]
    umbral = np.tile(BAYER8, (S // 8 + 1, S // 8 + 1))[:S, :S]

    on = (rad >= ri) & (rad <= re) & (dens > umbral) & ~lz["lleno"]
    if not on.any():
        return

    b = np.where(dens > 0.55, banda_int, banda_ext)
    lz["col"][on]  = ramp[b[on]]
    lz["glow"][on] = True


def tramar(lz, dens, ramp, banda_int=2, banda_ext=4, corte=0.55,
           capa="glow", oid=None):
    """Convierte un campo de densidad continuo en pixeles, con Bayer 8x8.

    `halo_trama` y `cono_trama` hacian esto mismo cada una por su cuenta, cada
    una con su geometria cableada dentro: un anillo alrededor de un cuerpo y un
    cono alrededor de un eje. Un tinte de estado no es ninguna de las dos
    formas, y no tiene por que serlo: lo unico que comparten las tres es el
    PASO FINAL, comparar una densidad contra la matriz de umbrales. Eso es lo
    que vive aqui, para que una forma nueva no tenga que traerse su propio
    dithering y las propuestas de un mismo overlay se midan con la misma vara.

    `dens` es un array (S,S) en [0,1]. Por defecto escribe en `glow`, que es
    la capa que NO genera contorno: un tinte contorneado saldria con cada
    punto del dither perfilado en negro, que es justo lo que la regla 7 del
    contrato evita.
    """
    S = lz["S"]
    umbral = np.tile(BAYER8, (S // 8 + 1, S // 8 + 1))[:S, :S]
    on = dens > umbral
    if not on.any():
        return on
    b = np.where(dens > corte, banda_int, banda_ext)
    lz["col"][on] = ramp[b[on]]
    lz[capa][on]  = True
    if capa == "lleno" and oid is not None:
        lz["id"][on] = oid
    return on


def anillo(lz, cx, cy, r_int, r_ext, ramp, banda, oid):
    """Annulus solido. Anillos orbitales, radio del Magnetar, ondas."""
    dx, dy = _malla(lz, cx, cy)
    rad = np.hypot(dx, dy)
    m = (rad >= r_int) & (rad <= r_ext)
    if not m.any():
        return
    lz["col"][m]   = ramp[banda]
    lz["lleno"][m] = True
    lz["id"][m]    = oid


# --- Bezier / apendices (sin cambios respecto al contrato) -----------------

def _bez(p, t):
    m = 1 - t
    return (m**3*p[0][0] + 3*m*m*t*p[1][0] + 3*m*t*t*p[2][0] + t**3*p[3][0],
            m**3*p[0][1] + 3*m*m*t*p[1][1] + 3*m*t*t*p[2][1] + t**3*p[3][1])


def _dbez(p, t):
    m = 1 - t
    return (3*m*m*(p[1][0]-p[0][0]) + 6*m*t*(p[2][0]-p[1][0]) + 3*t*t*(p[3][0]-p[2][0]),
            3*m*m*(p[1][1]-p[0][1]) + 6*m*t*(p[2][1]-p[1][1]) + 3*t*t*(p[3][1]-p[2][1]))


def tubo_gota(lz, p, r0, r1, ramp, oid, muestras=56, sesgo_banda=0):
    """Apendice con forma de gota sobre una curva de Bezier."""
    S = lz["S"]
    y, x = np.mgrid[0:S, 0:S]
    px, py = x + 0.5, y + 0.5

    for i in range(muestras + 1):
        t = i / muestras
        cx, cy = _bez(p, t)
        dx, dy = _dbez(p, t)
        ln = np.hypot(dx, dy) or 1.0
        nxv, nyv = -dy / ln, dx / ln

        w = r0 + (r1 - r0) * t**1.7
        if t > 0.80:
            k = (t - 0.80) / 0.20
            w *= np.sqrt(max(1.0 - k * k, 0.0))
        w = max(w, 0.5)

        vx, vy = px - cx, py - cy
        lat = vx * nxv + vy * nyv
        alo = np.abs(vx * (-nyv) + vy * nxv)
        m = (np.abs(lat) <= w) & (alo <= 1.2)
        if not m.any():
            continue

        f  = np.clip(lat / w, -1, 1)
        fz = np.sqrt(1 - f * f)
        d  = f * nxv * L[0] + f * nyv * L[1] + fz * L[2]
        b  = _bandas(d, (f * f > 0.72) & (d < 0.30)) + sesgo_banda

        lz["col"][m]   = ramp[np.clip(b, 0, 6)[m]]
        lz["lleno"][m] = True
        lz["id"][m]    = oid


# --- Salida ----------------------------------------------------------------

def tubo_curva(lz, p, grosor, ramp, oid, muestras=72, sesgo_banda=0,
               contorno_interno=True, umbral_borde=0.70):
    """Tubo de grosor constante sobre una Bezier cubica.

    Se diferencia de `tubo_gota` en que no afila ni redondea la punta: un
    anillo hecho de tramos con punta se leeria como una cadena de cuentas.
    Lleva contorno interno propio, que es lo que permite que la mitad
    delantera de un anillo se despegue del cuerpo que cruza por encima.
    """
    S = lz["S"]
    y, x = np.mgrid[0:S, 0:S]
    px, py = x + 0.5, y + 0.5

    for i in range(muestras + 1):
        t = i / muestras
        cx, cy = _bez(p, t)
        dx, dy = _dbez(p, t)
        ln = np.hypot(dx, dy) or 1.0
        nxv, nyv = -dy / ln, dx / ln

        vx, vy = px - cx, py - cy
        lat = vx * nxv + vy * nyv
        alo = np.abs(vx * (-nyv) + vy * nxv)
        m = (np.abs(lat) <= grosor) & (alo <= 1.2)
        if not m.any():
            continue

        f  = np.clip(lat / grosor, -1, 1)
        fz = np.sqrt(1 - f * f)
        d  = f * nxv * L[0] + f * nyv * L[1] + fz * L[2]
        b  = np.clip(_bandas(d, (f * f > 0.72) & (d < 0.30)) + sesgo_banda, 0, 6)

        borde = None
        if contorno_interno:
            borde = m & (f * f > umbral_borde) & lz["lleno"] & (lz["id"] != oid)

        lz["col"][m] = ramp[b[m]]
        if borde is not None:
            lz["col"][borde] = CONTORNO
        lz["lleno"][m] = True
        lz["id"][m]    = oid


_K_BEZ = 0.5522847498307936   # aproxima un cuarto de elipse con una cubica


def anillo_orbital(lz, cx, cy, a, b, phi, grosor, ramp, oid, mitad,
                   sesgo_banda=0, muestras=64):
    """Anillo orbital inclinado, en dos mitades por profundidad.

    Se llama dos veces por anillo: `mitad='atras'` antes de dibujar el
    cuerpo y `mitad='delante'` despues, con grosor mayor y banda mas clara.
    Son las dos senales que exige la seccion "Profundidad" del contrato:
    orden de dibujo y escala.

    a, b : semiejes en pantalla. b pequeno = orbita casi de canto.
    phi  : inclinacion del eje mayor, en radianes.
    """
    K = _K_BEZ
    co, si = math.cos(phi), math.sin(phi)

    def R(u, v):
        return (cx + u * co - v * si, cy + u * si + v * co)

    if mitad == 'delante':
        cuartos = [[R(a, 0), R(a, K * b), R(K * a, b), R(0, b)],
                   [R(0, b), R(-K * a, b), R(-a, K * b), R(-a, 0)]]
    else:
        cuartos = [[R(-a, 0), R(-a, -K * b), R(-K * a, -b), R(0, -b)],
                   [R(0, -b), R(K * a, -b), R(a, -K * b), R(a, 0)]]

    for q in cuartos:
        tubo_curva(lz, q, grosor, ramp, oid, muestras=muestras,
                   sesgo_banda=sesgo_banda)


def finalizar(lz):
    """Contorno externo sobre el cuerpo solido. Devuelve la mascara de alfa."""
    m = lz["lleno"]
    vec = np.zeros_like(m)
    vec[1:, :]  |= m[:-1, :]
    vec[:-1, :] |= m[1:, :]
    vec[:, 1:]  |= m[:, :-1]
    vec[:, :-1] |= m[:, 1:]
    borde = vec & ~m
    lz["col"][borde] = CONTORNO
    return m | borde | lz["glow"]


def a_rgba(lz):
    alfa = finalizar(lz)
    S = lz["S"]
    img = np.zeros((S, S, 4), np.uint8)
    img[..., :3] = lz["col"]
    img[..., 3]  = np.where(alfa, 255, 0)
    return img


def guardar(lz, ruta):
    Image.fromarray(a_rgba(lz), "RGBA").save(ruta)


# --- Huella de clic ----------------------------------------------------------
#
# La huella solida de un nodo (contrato, "Huella del nodo") es el lado mayor de
# la caja que encierra su materia: lo que hace silueta y cuenta para el clic.
# La luz tramada no cuenta, y desde el PNG ya no se distingue de la materia:
# solo el generador, que tiene las capas separadas, sabe medirla. Por eso la
# exporta, y Unity saca de ahi el radio del collider (18 #23, T6-01).

def huella_px(mascara):
    """Lado mayor, en pixeles, de la caja que encierra la mascara."""
    ys, xs = np.where(mascara)
    return int(max(ys.max() - ys.min(), xs.max() - xs.min()) + 1)


def exporta_huellas(raiz, prefijo, huellas):
    """Escribe <prefijo>_huella.json junto a los PNG, con la huella en px de cada nivel.

    La silueta es la misma en las tres facciones (14.0 #3), asi que la huella
    va por nivel. ArtBridge copia el JSON con la carpeta de la version. Dos
    listas paralelas y no un diccionario, porque JsonUtility no lee diccionarios.
    """
    import json
    niveles = sorted(huellas)
    datos = {"ppu": 64, "levels": niveles, "footprintPixels": [huellas[n] for n in niveles]}
    with open(os.path.join(raiz, f"{prefijo}_huella.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(datos, f, indent=2)
        f.write("\n")


# --- Materia facetada ------------------------------------------------------
#
# Todo lo anterior describe superficies curvas y continuas: la normal fingida
# de esfera se evalua PIXEL A PIXEL y el resultado es un degradado suave.
# Es el material de la Estrella y del Pulsar.
#
# Un cuerpo colapsado no es eso. Aqui la regla 1 del contrato se muestrea una
# vez POR CARA: misma L global, mismos umbrales, mismo rebote, misma rampa.
# No es un modelo de luz nuevo, es el mismo evaluado con otra frecuencia. Lo
# que cambia es la lectura: planos duros en vez de degradado, materia en vez
# de atmosfera.

def _dist_seg(px, py, a, b):
    """Distancia de cada pixel al segmento a-b."""
    vx, vy = b[0] - a[0], b[1] - a[1]
    l2 = vx * vx + vy * vy
    t = np.clip(((px - a[0]) * vx + (py - a[1]) * vy) / max(l2, 1e-9), 0.0, 1.0)
    return np.hypot(px - (a[0] + t * vx), py - (a[1] + t * vy))


def _poligono(px, py, pts, holgura=0.0):
    """Mascara de un poligono convexo, por semiplanos. Sin ImageDraw.

    El signo del area orientada decide el sentido de giro, asi que el
    llamante no tiene que preocuparse de darlo en un orden concreto. Dos
    caras que comparten arista reclaman los mismos pixeles de la frontera y
    no queda holgura entre ellas: tesela exacto.
    """
    n = len(pts)
    area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1]
               for i in range(n))
    s = 1.0 if area > 0 else -1.0
    m = np.ones(px.shape, bool)
    for i in range(n):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % n]
        m &= s * ((x1 - x0) * (py - y0) - (y1 - y0) * (px - x0)) >= -holgura
    return m


def poliedro(lz, cx, cy, verts, ramp, oid, mesa=0.44, sesgo_banda=0,
             aristas=True, especular=True, radio_normal=0.92, banda_arista=5,
             sesgo_mesa=0):
    """Cuerpo facetado: una mesa central rodeada de caras de cintura.

    `verts` son los desplazamientos (dx, dy) de la silueta respecto al
    centro, en orden. La mesa es esa misma silueta escalada por `mesa`, y
    entre las dos queda un anillo de caras cuadrilateras: el talle de una
    piedra tallada.

    LA NORMAL VA EN EL BORDE, NO EN EL CENTROIDE. Si la normal de cada cara
    se toma en su centroide, todas caen a ~0.65 del radio, la condicion de
    luz de rebote (e > 0.80) no se cumple nunca y el lado en sombra se cierra
    entero en la banda 6: un cuerpo muerto pegado al fondo, que es justo lo
    que prohibe la regla 4. Tomada en el punto medio de la arista exterior,
    las caras del limbo entran en el rebote y el lado oscuro respira.

    `sesgo_mesa` sesga SOLO la cara frontal. Su normal es (0,0,1) exacta, o
    sea d = L.z = 0.66, que cae a un pelo del umbral 0.68 y la deja en la
    banda 3 justo por debajo del corte. Como es la cara mas grande, es ella
    la que fija el valor percibido del cuerpo entero: un cuerpo que deberia
    leerse claro sale medio apagado por un margen de 0.02. Subirla una banda
    es corregir ese filo, no falsear la luz; el resto del talle no se toca y
    el contraste entre caras se conserva entero.

    `especular` enciende la banda 0 en la ESQUINA A LA LUZ de la cara que
    mira al vector de brillo, no en la cara entera. Encendiendo la cara
    entera, en un talle de caras desiguales la mas ancha se lleva el brillo y
    sale una cuna blanca del tamano de media figura: deja de leerse como un
    reflejo y se lee como una pieza pegada encima. Una esquirla de banda 0 en
    una esquina es lo que chispea una piedra.
    """
    S = lz["S"]
    y, x = np.mgrid[0:S, 0:S]
    px, py = x + 0.5, y + 0.5

    V = [(cx + dx, cy + dy) for dx, dy in verts]
    T = [(cx + dx * mesa, cy + dy * mesa) for dx, dy in verts]
    R = max(math.hypot(dx, dy) for dx, dy in verts) or 1.0
    n = len(verts)

    # contorno interno: lo que este poliedro tape de otra pieza
    total = _poligono(px, py, V)
    borde = None
    if total.any():
        dist = np.full((S, S), 1e9, np.float32)
        for i in range(n):
            dist = np.minimum(dist, _dist_seg(px, py, V[i], V[(i + 1) % n]))
        borde = total & (dist < 1.25) & lz["lleno"] & (lz["id"] != oid)

    caras = [(T, (0.0, 0.0))]                     # la mesa mira al frente
    for i in range(n):
        j = (i + 1) % n
        med = ((verts[i][0] + verts[j][0]) / 2.0, (verts[i][1] + verts[j][1]) / 2.0)
        caras.append(([T[i], T[j], V[j], V[i]], med))

    datos = []
    for k_cara, (pts, med) in enumerate(caras):
        u = med[0] / R * radio_normal, med[1] / R * radio_normal
        e = u[0] * u[0] + u[1] * u[1]
        nz = math.sqrt(max(1.0 - e, 0.0))
        d = u[0] * L[0] + u[1] * L[1] + nz * L[2]
        b = _bandas(d, (e > 0.80) and (d < 0.30)) + sesgo_banda
        if k_cara == 0:
            b += sesgo_mesa
        b = int(np.clip(b, 0, 6))
        s = u[0] * HV[0] + u[1] * HV[1] + nz * HV[2]
        datos.append([pts, b, s])

    for pts, b, _s in datos:
        m = _poligono(px, py, pts)
        if not m.any():
            continue
        lz["col"][m]   = ramp[b]
        lz["lleno"][m] = True
        lz["id"][m]    = oid

    if aristas:
        # Sin esto, dos caras contiguas que el rebote ha igualado a la misma
        # banda se funden en una mancha y el talle desaparece justo en el
        # lado oscuro, que es donde mas falta hace.
        pliegue = np.zeros((S, S), bool)
        for i in range(n):
            pliegue |= _dist_seg(px, py, T[i], V[i]) < 0.70
            pliegue |= _dist_seg(px, py, T[i], T[(i + 1) % n]) < 0.70
        pliegue &= total
        lz["col"][pliegue] = ramp[banda_arista]

    if especular and len(datos) > 1:
        # va DESPUES de las aristas: un reflejo no lo corta un pliegue
        pts = datos[max(range(1, len(datos)), key=lambda i: datos[i][2])][0]
        k = max(range(len(pts)),
                key=lambda i: (pts[i][0] - cx) * L[0] + (pts[i][1] - cy) * L[1])
        P, A, B = pts[k], pts[(k - 1) % len(pts)], pts[(k + 1) % len(pts)]
        chip = [P,
                (P[0] + (B[0] - P[0]) * 0.42, P[1] + (B[1] - P[1]) * 0.42),
                (P[0] + (A[0] - P[0]) * 0.42, P[1] + (A[1] - P[1]) * 0.42)]
        m = _poligono(px, py, chip) & total
        if m.any():
            lz["col"][m] = ramp[0]

    if borde is not None and borde.any():
        lz["col"][borde] = CONTORNO


def veta(lz, p0, p1, grosor, ramp, banda, oid, solo_dentro=True):
    """Veta recta de banda constante: una grieta que emite luz propia.

    Solida, no tramada. El glow es el material del Pulsar; una fractura en
    una corteza es materia incandescente, y ademas debe contar para la huella
    igual que el resto del cuerpo.
    """
    S = lz["S"]
    y, x = np.mgrid[0:S, 0:S]
    m = _dist_seg(x + 0.5, y + 0.5, p0, p1) <= grosor
    if solo_dentro:
        m &= lz["lleno"]
    if not m.any():
        return
    lz["col"][m]   = ramp[banda]
    lz["lleno"][m] = True
    lz["id"][m]    = oid


def veta_luz(lz, p0, p1, grosor, ramp, banda):
    """Una veta que es LUZ, no materia: sin `lleno` y por tanto sin contorno.

    `veta` es materia incandescente y lo es a proposito: una fractura en una
    corteza cuenta para la huella igual que el resto del cuerpo. Pero un
    overlay que se dibuja ENCIMA de un sprite ya terminado no tiene huella
    que sumar, y ahi el contorno deja de separar y pasa a tachar: al
    finalizar, una linea de 1 px sale con 1 px de contorno a cada lado y
    ocupa 3, en negro, sobre el dibujo de otro. Medido en el bloque de
    `ART-ST-FROZEN` v2, doce aristas asi se llevaban por delante el 71 % de
    un frente de onda del Pulsar -33 px de relleno y 35 de contorno sobre
    una pieza de 142-, y con el, el contador de nivel.

    Es el mismo motivo por el que la capa `glow` existe (regla 7 del
    contrato). Asi que esta va a `glow` con densidad 1: se ve solida y no
    paga contorno.
    """
    S = lz["S"]
    y, x = np.mgrid[0:S, 0:S]
    m = _dist_seg(x + 0.5, y + 0.5, p0, p1) <= grosor
    if not m.any():
        return m
    lz["col"][m]  = ramp[banda]
    lz["glow"][m] = True
    return m


def linea_dipolar(lz, cx, cy, R, eje, ramp, oid, g_polo=2.8, g_ecu=1.1,
                  r_min=0.0, sesgo_banda=0, muestras=200, exp_grosor=1.3,
                  k_eje=1.0, k_ecu=1.0, onda_amp=0.0, onda_n=2, onda_fase=0.0,
                  contorno_interno=True):
    """Linea de campo CERRADA de un dipolo magnetico.

        r(t) = R sin^2(t)      t = angulo al eje del momento

    Una sola ecuacion de una linea. R es el radio ecuatorial: es el unico
    parametro, y ordena la familia entera de lineas de un mismo dipolo.

    POR QUE NO VALE NINGUNA DE LAS OTRAS. `tubo_curva` tiene grosor
    constante y `tubo_gota` afila hasta la punta; ninguna de las dos puede
    engordar y adelgazar segun una magnitud fisica. Y hace falta: en un
    dipolo las lineas se aprietan en los polos -campo intenso- y se abren en
    el ecuador -campo debil-. Ese ensanchamiento ES la informacion. Dibujadas
    de grosor constante, cinco lineas anidadas son cinco rayas concentricas y
    podrian ser cualquier cosa; con el grosor siguiendo a |B|, se leen como
    un campo, se ve donde aprieta y el ojo sabe hacia donde mirar.

        grosor(t) = g_ecu + (g_polo - g_ecu) * |cos t| ^ exp_grosor

    Es |B| acotado. La expresion exacta, |B| ~ sqrt(1+3cos^2 t)/r^3, diverge
    en el polo, donde r tiende a 0: en pixeles eso es una mancha. La cota
    conserva el gradiente, que es lo unico que se mira.

    El sombreado es el de un tubo -regla 1 sobre una seccion circular, con la
    L global-, evaluado en cada muestra con el radio de esa muestra. No es un
    modelo nuevo: es el mismo aplicado a una seccion que cambia.

    `r_min` recorta el tramo interior. Todas las lineas de un dipolo pasan
    por el centro, asi que sin recorte N lineas se amontonan ahi en un
    borron; con el, el amontonamiento cae dentro del cuerpo que va encima.

    `onda_amp` / `onda_n` / `onda_fase` modulan la banda A LO LARGO de la
    linea: un realce que viaja por ella. Es plasma atrapado recorriendo el
    tubo de campo y rebotando entre los dos espejos magneticos de los polos,
    que es lo que de verdad pasa dentro de una botella magnetica. La onda va
    en `t`, el mismo parametro que traza la curva, asi que con `onda_n`
    entero cierra sola sea cual sea el numero de muestras, y con la fase
    recorriendo 0..1 en N frames el bucle cierra por construccion.

    La amplitud se mantiene corta a proposito. Si un lazo llegara a apagarse
    del todo en parte del ciclo, en esos frames se verian menos lazos de los
    que dice el nivel y el contador dejaria de ser fiable, que es lo unico
    que este sprite no se puede permitir.

    `k_eje` y `k_ecu` escalan la curva a lo largo del eje y del ecuador. El
    lazo crudo mide 2R de ancho por 0.77R de alto -es la forma del dipolo, no
    una eleccion-, y esa proporcion de 2.6 a 1 desborda el ecuador de la
    celda antes de que la altura llegue a la mitad. La escala anisotropa lo
    mete dentro sin tocar lo unico que el sprite tiene que decir: el orden de
    la familia, donde aprieta y donde se abre, y que los lazos no se cruzan.
    """
    S = lz["S"]
    y, x = np.mgrid[0:S, 0:S]
    px, py = x + 0.5, y + 0.5
    ax, ay = math.cos(eje), math.sin(eje)      # direccion del momento
    bx, by = -ay, ax                           # perpendicular

    for k in range(muestras):
        t  = 2.0 * math.pi * k / muestras
        st, ct = math.sin(t), math.cos(t)
        r = R * st * st
        if r < r_min:
            continue

        # posicion y tangente analitica de r(t)(cos t * A + sin t * B)
        dr = 2.0 * R * st * ct
        ux, uy = ct * ax + st * bx, ct * ay + st * by
        vx, vy = -st * ax + ct * bx, -st * ay + ct * by
        # escala anisotropa en la base (eje, ecuador), no en pantalla
        ex, ey = r * ct * k_eje, r * st * k_ecu
        dex = (dr * ct - r * st) * k_eje
        dey = (dr * st + r * ct) * k_ecu
        qx, qy = cx + ex * ax + ey * bx, cy + ex * ay + ey * by
        tx, ty = dex * ax + dey * bx, dex * ay + dey * by
        ln = math.hypot(tx, ty) or 1.0
        nxv, nyv = -ty / ln, tx / ln

        w = max(g_ecu + (g_polo - g_ecu) * abs(ct) ** exp_grosor, 0.5)

        dx, dy = px - qx, py - qy
        lat = dx * nxv + dy * nyv
        alo = np.abs(dx * (-nyv) + dy * nxv)
        m = (np.abs(lat) <= w) & (alo <= 1.2)
        if not m.any():
            continue

        f  = np.clip(lat / w, -1, 1)
        fz = np.sqrt(1 - f * f)
        d  = f * nxv * L[0] + f * nyv * L[1] + fz * L[2]
        realce = 0.0
        if onda_amp:
            # SOLO ACLARA, nunca oscurece. Con una onda centrada en cero, la
            # mitad del ciclo en que el realce esta en el tramo negativo deja
            # la linea por debajo de su banda de reposo, y como el tramo
            # brillante cae muchas veces detras de la columna, lo que se ve
            # es el campo entero apagandose y encendiendose: un parpadeo, no
            # un pulso viajando. Desplazada, el reposo es el suelo y lo unico
            # que se mueve es el realce.
            realce = -0.5 * onda_amp * (1.0 + math.cos(onda_n * t
                                                       - 2.0 * math.pi * onda_fase))
        b  = np.rint(_bandas(d, (f * f > 0.72) & (d < 0.30))
                     + sesgo_banda + realce)
        b  = np.clip(b, 0, 6).astype(np.int16)

        borde = None
        if contorno_interno:
            borde = m & (f * f > 0.70) & lz["lleno"] & (lz["id"] != oid)

        lz["col"][m] = ramp[b[m]]
        if borde is not None:
            lz["col"][borde] = CONTORNO
        lz["lleno"][m] = True
        lz["id"][m]    = oid


# --- Materia muerta: contorno organico y superficie concava ----------------
#
# Todo el reparto de NOVA es convexo y emisivo: cuerpos que abultan y que dan
# luz. Falta el material contrario, y es el de la roca: un contorno que no es
# ni circulo ni poligono, y una superficie que en vez de abultar se HUNDE.
#
# La concavidad es el unico eje que no usa ningun otro nodo, y sale gratis de
# la regla 1: un cuenco es una esfera con la normal del plano invertida. Es
# tambien por lo que una foto de la Luna girada 180 grados convierte los
# crateres en cupulas -el ojo solo tiene la direccion de la luz para decidir-,
# asi que aqui es literalmente el mismo modelo con un signo cambiado.

def bulto(lz, cx, cy, r, ramp, oid, perfil=(), calor=0.0, apagado=0.0,
          sesgo_banda=0, especular=True):
    """Cuerpo de contorno organico irregular: ni esfera ni poliedro.

    El limite radial es una suma de armonicos sobre el angulo,

        r_lim(a) = r * (1 + suma de amp_k * cos(k*a + fase_k))

    y el sombreado es la normal fingida de esfera evaluada en el radio
    NORMALIZADO `rad / r_lim`, no en `rad / r`. Esa division es toda la
    primitiva: hace que el terminador siga al contorno, asi que un bulto que
    sobresale se sombrea como parte del cuerpo y no como un pegote encima.
    Con `perfil` vacio esto es exactamente `esfera`.

    Por que no vale ninguna de las otras: `esferoide` solo da elipses -su
    contorno tiene siempre dos ejes de simetria- y `poliedro` da aristas
    duras. Una roca no tiene ni lo uno ni lo otro, y un contorno irregular
    suave es, ademas, la unica silueta que no esta cogida.

    `especular=False` apaga el punto de brillo de la regla 3. El especular es
    lo que hace que una superficie se lea como MATERIAL PULIDO, y hay cosas
    que no lo son: una roca es mate, y un punto blanco encima la convierte en
    canica. Es el unico sitio del proyecto donde saltarse la regla 3 es lo
    correcto, y por eso va como parametro explicito y no por omision.
    """
    dx, dy = _malla(lz, cx, cy)
    rad = np.hypot(dx, dy)
    ang = np.arctan2(dy, dx)

    r_lim = np.full_like(rad, float(r))
    for k, amp, fase in perfil:
        r_lim = r_lim + r * amp * np.cos(k * ang + fase)

    u = rad / np.maximum(r_lim, 1e-6)
    m = u <= 1.0
    if not m.any():
        return

    e  = np.clip(u * u, 0.0, 1.0)
    nz = np.sqrt(np.clip(1 - e, 0, 1))
    rs = np.maximum(rad, 1e-6)
    nx = dx / rs * u
    ny = dy / rs * u
    d  = nx * L[0] + ny * L[1] + nz * L[2]
    b  = _bandas(d, (e > 0.80) & (d < 0.30)).astype(np.float32) + sesgo_banda

    b += apagado
    if calor:
        b -= calor * np.clip(1.30 - 1.75 * np.sqrt(e), 0.0, 1.0)
    b = np.clip(np.rint(b), 0, 6).astype(np.int16)
    if especular:
        b = np.where(nx * HV[0] + ny * HV[1] + nz * HV[2] > 0.972, 0, b)

    borde = m & (e > 0.86) & lz["lleno"] & (lz["id"] != oid)

    lz["col"][m]     = ramp[b[m]]
    lz["col"][borde] = CONTORNO
    lz["lleno"][m]   = True
    lz["id"][m]      = oid


def crater(lz, cx, cy, r, ramp, oid_cuerpo, hondura=1.35, borde=0.22,
           sesgo_cuenco=0, sesgo_borde=0, tope=2):
    """Depresion concava con reborde levantado. La regla 1 con el signo al reves.

    EL CUENCO. La pared de un crater mira hacia DENTRO, asi que su normal
    apunta al lado contrario que la de un bulto en el mismo sitio:

        n_xy = -direccion_radial * hondura * u        n_z = 1

    El resultado es que **se ilumina la pared del fondo y se oscurece la de
    delante**, exactamente al reves que una cupula. No hay ningun modelo
    nuevo: es la misma L global y las mismas bandas.

    EL REBORDE. La corona de material expulsado si abulta, asi que su normal
    apunta hacia fuera y se enciende del lado de la luz. Sin el, el cuenco se
    lee como una mancha de color; con el, cada crater lleva su media luna
    clara y su media luna oscura enfrentadas, que es la firma que el ojo
    reconoce como hoyo. Es la pieza barata que hace legible a la otra.

    UN CRATER MODULA LA SUPERFICIE QUE HAY, NO LA SUSTITUYE. Lo que se aplica
    no es una banda absoluta sino un DESPLAZAMIENTO respecto a la banda de
    una superficie plana, leyendo la banda que ya tiene cada pixel y
    corriendola. Pintando bandas absolutas, un crater colocado en la mitad en
    sombra del cuerpo sale mas claro que la roca que lo rodea y se lee como
    una ampolla en vez de como un hoyo: el error no se ve en el lado
    iluminado, solo en el oscuro, que es donde nadie mira hasta que ya esta
    hecho. Modulando, el hoyo funciona en cualquier punto del cuerpo, sobre
    cualquier rampa de faccion y encima de otro crater.

    `tope` acota el desplazamiento. Sin acotar, la pared en sombra se va a la
    banda 6 y la iluminada a la 1 -un salto de tres bandas contra uno de dos-,
    asi que el mismo crater sale mucho mas marcado por su lado oscuro y la
    hilera entera parece iluminada desde otro sitio. Acotado y simetrico, un
    crater pesa lo mismo caiga donde caiga. Es tambien lo que sustituye aqui
    a la luz de rebote de la regla 4: en un hoyo de 6 px, el rebote de la
    regla 4 se come el contraste entero y el crater desaparece.

    Solo pinta donde ya hay cuerpo y con el `oid` del cuerpo: un crater es un
    accidente de una superficie, no una pieza suelta. No toca `lleno`, asi
    que no cambia la silueta ni la huella; si cae sobre el limbo, muerde el
    contorno, que es justo lo que hace un crater de verdad.
    """
    dx, dy = _malla(lz, cx, cy)
    rad = np.hypot(dx, dy)
    piel = lz["lleno"] & (lz["id"] == oid_cuerpo)

    re = r * (1.0 + borde)
    zona = (rad <= re) & piel
    if not zona.any():
        return

    # banda actual de cada pixel, por busqueda inversa en la rampa. Lo que no
    # sea un tono de la rampa -el contorno- se queda fuera y no se toca.
    idx = np.full(rad.shape, -1, np.int16)
    for i in range(len(ramp)):
        idx = np.where(np.all(lz["col"] == ramp[i], axis=-1), np.int16(i), idx)
    zona &= idx >= 0
    if not zona.any():
        return

    plana = int(_bandas(L[2], False))       # banda de una superficie plana
    rs = np.maximum(rad, 1e-6)
    ux, uy = dx / rs, dy / rs

    def _delta(nx, ny, sesgo):
        n = np.sqrt(nx * nx + ny * ny + 1.0)
        d = (nx * L[0] + ny * L[1] + L[2]) / n
        return np.clip(_bandas(d, False) - plana, -tope, tope) + sesgo

    u = np.clip(rad / max(r, 1e-6), 0, 1)
    cuenco = zona & (rad <= r)
    aro    = zona & (rad > r)

    if cuenco.any():
        dl = _delta(-ux * hondura * u, -uy * hondura * u, sesgo_cuenco)
        b = np.clip(idx + dl, 0, 6)
        lz["col"][cuenco] = ramp[b[cuenco]]
    if aro.any():
        dl = _delta(ux * hondura * 0.85, uy * hondura * 0.85, sesgo_borde)
        b = np.clip(idx + dl, 0, 6)
        lz["col"][aro] = ramp[b[aro]]


def esquirla(lz, cx, cy, verts, ramp, oid, calor=0.0, apagado=0.0,
             sesgo_banda=0, especular=True):
    """Cuerpo de contorno RECTO y sombreado CURVO: una roca partida.

    Completa el trio de cuerpos, que hasta ahora tenia un hueco:

        poliedro   contorno recto  + caras planas     -> cristal tallado
        bulto      contorno suave  + sombreado curvo  -> canto rodado
        esquirla   contorno RECTO  + sombreado CURVO  -> piedra rota

    Esa tercera casilla es la que dibuja un fragmento. Una piedra que se
    parte no gana caras planas por dentro: gana un BORDE recto donde la
    fractura la cortó, y por debajo sigue siendo la misma masa redondeada de
    siempre. Con `poliedro` sale un cristal -la fractura se propaga a todo el
    volumen- y con `bulto` sale un guijarro -no hay fractura en ninguna
    parte-.

    El limite radial de un poligono convexo sale de su funcion soporte: para
    cada arista de normal exterior `n` y distancia `c` al centro, la frontera
    en la direccion `u` esta en `c / (n.u)` cuando `n.u > 0`, y el limite es
    el minimo. A partir de ahi el sombreado es el de `bulto`: la normal
    fingida evaluada en `rad / r_lim`, que es lo que hace que el terminador
    siga al contorno.

    `verts` son desplazamientos (dx, dy) desde el centro, y el poligono debe
    ser CONVEXO: el limite se calcula por soporte y un entrante se rellenaria
    igualmente.
    """
    dx, dy = _malla(lz, cx, cy)
    rad = np.hypot(dx, dy)
    rs = np.maximum(rad, 1e-6)
    ux, uy = dx / rs, dy / rs

    n = len(verts)
    area = sum(verts[i][0] * verts[(i + 1) % n][1] -
               verts[(i + 1) % n][0] * verts[i][1] for i in range(n))
    giro = 1.0 if area > 0 else -1.0

    r_lim = np.full_like(rad, 1e9)
    for i in range(n):
        ax, ay = verts[i]
        bx, by = verts[(i + 1) % n]
        ex, ey = bx - ax, by - ay
        ln = math.hypot(ex, ey) or 1.0
        nx, ny = giro * ey / ln, -giro * ex / ln     # normal exterior
        c = nx * ax + ny * ay
        if c <= 0:
            continue                                  # centro fuera: no convexo
        proy = nx * ux + ny * uy
        with np.errstate(divide='ignore', invalid='ignore'):
            t = np.where(proy > 1e-6, c / np.maximum(proy, 1e-6), 1e9)
        r_lim = np.minimum(r_lim, t)

    u = rad / np.maximum(r_lim, 1e-6)
    m = u <= 1.0
    if not m.any():
        return

    e  = np.clip(u * u, 0.0, 1.0)
    nz = np.sqrt(np.clip(1 - e, 0, 1))
    nx = ux * u
    ny = uy * u
    d  = nx * L[0] + ny * L[1] + nz * L[2]
    b  = _bandas(d, (e > 0.80) & (d < 0.30)).astype(np.float32) + sesgo_banda

    b += apagado
    if calor:
        b -= calor * np.clip(1.30 - 1.75 * np.sqrt(e), 0.0, 1.0)
    b = np.clip(np.rint(b), 0, 6).astype(np.int16)
    if especular:
        b = np.where(nx * HV[0] + ny * HV[1] + nz * HV[2] > 0.972, 0, b)

    borde = m & (e > 0.86) & lz["lleno"] & (lz["id"] != oid)

    lz["col"][m]     = ramp[b[m]]
    lz["col"][borde] = CONTORNO
    lz["lleno"][m]   = True
    lz["id"][m]      = oid


def estratos(lz, cx, cy, ang, grosores, deltas, oid_cuerpo, ramp,
             amp_pliegue=0.0, k_pliegue=1.0, fase=0.0):
    """Capas paralelas que cruzan un cuerpo: estratificacion.

    TODO EL RESTO DEL REPARTO SE ORGANIZA ALREDEDOR DE SU CENTRO -radial,
    bilateral o concentrico-. Los estratos no tienen centro: son una
    DIRECCION. Es el unico principio de composicion que no esta cogido, y
    dice algo que ninguna otra estructura puede decir: que esto es un trozo
    de algo mas grande, porque las capas entran por un borde y salen por el
    otro en vez de envolver la pieza.

    Las capas se cortan LIMPIAS contra la silueta, sin doblarse a seguirla.
    Eso es lo que prueba la fractura: una capa que rodea el contorno dice
    "esto crecio asi"; una capa cortada a hachazo dice "esto se rompio".

    `grosores` y `deltas` se recorren en ciclo, asi que la secuencia de
    capas se escribe una vez y se repite: gruesos desiguales y algun delta
    fuerte aislado leen como roca, y todos iguales leen como persiana.

    Modula la banda que ya hay, no la sustituye, igual que `crater`: sobre la
    mitad en sombra un estrato pintado en absoluto saldria mas claro que la
    roca que lo rodea.
    """
    dx, dy = _malla(lz, cx, cy)
    piel = lz["lleno"] & (lz["id"] == oid_cuerpo)
    if not piel.any():
        return

    co, si = math.cos(ang), math.sin(ang)
    a =  dx * co + dy * si          # a lo largo de la capa
    v = -dx * si + dy * co          # perpendicular: la que numera la capa
    if amp_pliegue:
        v = v + amp_pliegue * np.sin(k_pliegue * a + fase)

    idx = np.full(v.shape, -1, np.int16)
    for i in range(len(ramp)):
        idx = np.where(np.all(lz["col"] == ramp[i], axis=-1), np.int16(i), idx)
    piel &= idx >= 0
    if not piel.any():
        return

    ciclo = float(sum(grosores))
    if ciclo <= 0:
        return
    w = np.mod(v, ciclo)

    borde = 0.0
    for k, g in enumerate(grosores):
        capa = piel & (w >= borde) & (w < borde + g)
        borde += g
        d = deltas[k % len(deltas)]
        if d and capa.any():
            lz["col"][capa] = ramp[np.clip(idx + d, 0, 6)[capa]]
