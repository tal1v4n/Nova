// T5-05b: fondo del mapa 2, El Cinturón (ART-BG-02). Materia que fluye: el grano de la corriente
// vive en coordenadas de la banda —la fila y - eje(x)— y corre a 4 px/s por el borde exterior y a
// 6.5 por el interior; las chispas viajan cada una a la velocidad de su fila y las estrellas
// débiles se tapan al paso de cada grumo. Reproduce seccion2() de tools/gen_fondos_v2.py; la grava
// y el ovni son sprites de BackgroundFlyby.
Shader "Nova/Background/Map2"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite", 2D) = "white" {}
        [NoScaleOffset] _Fields0 ("Arco y viñeta", 2D) = "black" {}
        [NoScaleOffset] _Band ("Grano exterior e interior, en la banda", 2D) = "black" {}
        [NoScaleOffset] _Points0 ("Estrellas débiles", 2D) = "black" {}
        [NoScaleOffset] _Points1 ("Chispas, en la banda", 2D) = "black" {}
        [NoScaleOffset] _Static ("Lo fijo", 2D) = "black" {}
        _Window ("Ventana", Vector) = (0, 0, 960, 540)
        _CFondo ("Fondo", Color) = (0, 0, 0, 1)
        _C3 ("Banda 3", Color) = (0, 0, 0, 1)
        _C4 ("Banda 4", Color) = (0, 0, 0, 1)
        _C5 ("Banda 5", Color) = (0, 0, 0, 1)
        _C6 ("Banda 6", Color) = (0, 0, 0, 1)
        _CDebil ("Estrella débil", Color) = (0, 0, 0, 1)
        _TDesde ("Corriente: desde", Vector) = (0, 0, 0, 0)
        _TCob ("Corriente: cobertura", Vector) = (0, 0, 0, 0)
        _Corrida ("Primera fila de la banda", Float) = 0
        _Sigma ("Media anchura de la corriente", Float) = 1
        _BandRows ("Filas de la banda", Float) = 1
        _GranoExt ("Fase: grano exterior", Float) = 0
        _GranoInt ("Fase: grano interior", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        Blend Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BackgroundVert
            #pragma fragment Frag

            #include "Background.hlsl"

            TEXTURE2D(_Fields0);
            TEXTURE2D(_Band);
            TEXTURE2D(_Points0);
            TEXTURE2D(_Points1);
            float4 _CDebil;
            float4 _TDesde, _TCob;
            float _Corrida, _Sigma, _BandRows;
            float _GranoExt, _GranoInt;

            // Por columna, la fila de la banda que cae en la fila 0 de la pantalla: rint(eje) + corrida.
            // Por fila de la banda, lo que han corrido sus chispas, ya reducido a 960 (la banda mide
            // 846 filas; 1023 es el tope de Unity para una lista). Las pone BackgroundView: un
            // material no guarda listas.
            float _EjeRow[960];
            float _SparkShift[1023];

            // ¿Alguno de los cuatro puntos de esta celda cae en el píxel? `carry`: la celda es la de la
            // izquierda, y su punto solo llega si su fracción más la corrida pasa de 1.
            bool AnySpark(float4 cell, float phase, bool carry)
            {
                bool hit = false;
                [unroll] for (int c = 0; c < 4; c++)
                {
                    if (cell[c] > 0.0)
                    {
                        bool over = Fraction(cell[c]) + phase >= 1.0;
                        hit = hit || over == carry;
                    }
                }
                return hit;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                int2 p = BackgroundPixel(input.uv);
                float threshold = Bayer(p);
                float4 f0 = ROW(_Fields0, p.x, p.y, FONDO_ALTO);    // arco, viñeta
                int rows = (int)_BandRows;
                int r = p.y - (int)_EjeRow[p.x];

                int e0, e1, i0, i1;
                float fe, fi;
                Shift(_GranoExt, p.x, e0, e1, fe);
                Shift(_GranoInt, p.x, i0, i1, fi);
                float outer = lerp(ROW(_Band, e0, r, rows).r, ROW(_Band, e1, r, rows).r, fe);
                float inner = lerp(ROW(_Band, i0, r, rows).g, ROW(_Band, i1, r, rows).g, fi);
                float mix = saturate(0.5 + 0.5 * (r + _Corrida) / _Sigma);
                float grain = (1.0 - mix) * outer + mix * inner;

                float3 color = _CFondo.rgb;
                float cover = saturate(f0.r * 1.5 * (0.4 + 0.6 * grain));
                float u = ROW(_Points0, p.x, p.y, FONDO_ALTO).r;
                if (u > 0.0 && u - 0.5 / 255.0 < 1.0 - cover)
                    color = _CDebil.rgb;
                float4 s = StaticAt(p);
                color = StaticLow(color, s);
                color = Dither(color, f0.r * (0.25 + 0.75 * grain) * (0.55 + 0.45 * f0.g), _TDesde, _TCob, threshold);

                if (f0.r > 0.18)
                {
                    float shift = _SparkShift[r];
                    float k = floor(shift);
                    float phase = shift - k;
                    int c0 = Wrap(p.x - (int)k, FONDO_ANCHO);
                    int c1 = Wrap(p.x - (int)k - 1, FONDO_ANCHO);
                    if (AnySpark(ROW(_Points1, c0, r, rows), phase, false) || AnySpark(ROW(_Points1, c1, r, rows), phase, true))
                        color = _C4.rgb;
                }

                color = StaticHigh(color, s);
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
