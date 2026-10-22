#ifndef NOVA_BACKGROUND_INCLUDED
#define NOVA_BACKGROUND_INCLUDED

// T5-05b: lo común de los tres fondos con movimiento (A.4.5, NOVA_Arte_Tecnica.md, «Fondos con
// movimiento»). La referencia es fotograma(t) de tools/gen_fondos_v2.py: cada shader de mapa la
// reproduce paso a paso, con las mismas texturas cuantizadas, y los tests de BackgroundTests lo
// comparan con sus fotogramas de referencia. Nada se muestrea con filtro: todo se lee texel a
// texel con Load y se interpola a mano, como en numpy, porque el bilineal del hardware redondea
// la fracción a pocos bits y movería los empates contra la matriz de Bayer.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#define FONDO_ANCHO 960
#define FONDO_ALTO 540

// Lo que enseña el sprite: x, y, ancho y alto en píxeles del fondo, con el origen arriba a la
// izquierda. El tablero entero es (0, 0, 960, 540); la carta estelar de 13.9 enseña una franja.
float4 _Window;

float4 _CFondo;
float4 _C3;
float4 _C4;
float4 _C5;
float4 _C6;

TEXTURE2D(_Static);

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

Varyings BackgroundVert(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = input.uv;
    return output;
}

// El píxel del fondo que cae bajo este fragmento, contado como en numpy: fila 0 arriba. La trama
// se ancla a esta rejilla y no a la de la pantalla (regla 7 del contrato).
int2 BackgroundPixel(float2 uv)
{
    float2 p = floor(float2(uv.x, 1.0 - uv.y) * _Window.zw);
    p = clamp(p, 0.0, _Window.zw - 1.0);
    return int2(p + _Window.xy);
}

// Un texel por su fila contada desde arriba. Unity guarda la fila 0 de un PNG abajo.
#define ROW(tex, x, y, alto) LOAD_TEXTURE2D(tex, int2((x), (alto) - 1 - (y)))

// BAYER8 de tools/nova_core.py, sin normalizar.
static const float BAYER8[64] =
{
     0, 32,  8, 40,  2, 34, 10, 42,
    48, 16, 56, 24, 50, 18, 58, 26,
    12, 44,  4, 36, 14, 46,  6, 38,
    60, 28, 52, 20, 62, 30, 54, 22,
     3, 35, 11, 43,  1, 33,  9, 41,
    51, 19, 59, 27, 49, 17, 57, 25,
    15, 47,  7, 39, 13, 45,  5, 37,
    63, 31, 55, 23, 61, 29, 53, 21,
};

float Bayer(int2 p)
{
    return BAYER8[(p.y & 7) * 8 + (p.x & 7)] / 64.0;
}

int Wrap(int v, int n)
{
    int r = v % n;
    return r < 0 ? r + n : r;
}

// tramar() de tools/nova_fondo.py: cada banda enciende como mucho su cobertura de los píxeles que
// pasan de su umbral, y la matriz de Bayer decide cuáles. Las bandas van fijas por posición, de la
// más oscura a la más clara (6, 5, 4 y 3); una cobertura 0 no enciende nada.
float3 Dither(float3 color, float density, float4 from, float4 coverage, float threshold)
{
    float d0 = saturate(density);
    float4 d = saturate((d0 - from) / max(1e-6, 1.0 - from)) * coverage;
    if (d.x > threshold) color = _C6.rgb;
    if (d.y > threshold) color = _C5.rgb;
    if (d.z > threshold) color = _C4.rgb;
    if (d.w > threshold) color = _C3.rgb;
    return color;
}

// desliza() de gen_fondos_v2.py: las dos columnas de las que sale el píxel x de un campo corrido
// dx píxeles a la derecha, y el peso de la segunda. dx llega ya reducido a su periodo.
void Shift(float dx, int x, out int x0, out int x1, out float f)
{
    float s = floor(dx);
    f = dx - s;
    x0 = Wrap(x - (int)s, FONDO_ANCHO);
    x1 = Wrap(x - (int)s - 1, FONDO_ANCHO);
}

// La fracción de partida de un punto, guardada en un byte de 1 a 255; el 0 es «vacío».
float Fraction(float stored)
{
    return (round(stored * 255.0) - 1.0) / 255.0;
}

// Lo fijo del mapa, por clases (fija_de en gen_fondos_v2.py): alfa 128 va por debajo de los puntos
// que se mueven, 255 por encima.
float4 StaticAt(int2 p)
{
    return ROW(_Static, p.x, p.y, FONDO_ALTO);
}

float3 StaticLow(float3 color, float4 s)
{
    return s.a > 0.25 && s.a < 0.75 ? s.rgb : color;
}

float3 StaticHigh(float3 color, float4 s)
{
    return s.a >= 0.75 ? s.rgb : color;
}

#endif
