// T5-05b: fondo del mapa 3, Espacio Profundo (ART-BG-03). El vacío deriva por profundidades: dos
// velos que ondulan como humo —su campo se deforma con otro campo que también corre—, estrellas
// medias que derivan a 0.9 px/s y titilan, y tres capas de motas cada vez más rápidas y más claras.
// La galaxia y las estrellas fuertes no se mueven. Reproduce seccion3() de tools/gen_fondos_v2.py.
Shader "Nova/Background/Map3"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite", 2D) = "white" {}
        [NoScaleOffset] _Fields0 ("Velos y deformación", 2D) = "black" {}
        [NoScaleOffset] _Fields1 ("Envolventes de los velos", 2D) = "black" {}
        [NoScaleOffset] _Points0 ("Motas lejanas y medias", 2D) = "black" {}
        [NoScaleOffset] _Points1 ("Motas cercanas y estrellas medias", 2D) = "black" {}
        [NoScaleOffset] _Static ("Lo fijo", 2D) = "black" {}
        _Window ("Ventana", Vector) = (0, 0, 960, 540)
        _CFondo ("Fondo", Color) = (0, 0, 0, 1)
        _C3 ("Banda 3", Color) = (0, 0, 0, 1)
        _C4 ("Banda 4", Color) = (0, 0, 0, 1)
        _C5 ("Banda 5", Color) = (0, 0, 0, 1)
        _C6 ("Banda 6", Color) = (0, 0, 0, 1)
        _CMedia0 ("Estrella media", Color) = (0, 0, 0, 1)
        _CMedia1 ("Estrella media, un índice abajo", Color) = (0, 0, 0, 1)
        _CMedia2 ("Estrella media, dos índices abajo", Color) = (0, 0, 0, 1)
        _V1Desde ("Velo 1: desde", Vector) = (0, 0, 0, 0)
        _V1Cob ("Velo 1: cobertura", Vector) = (0, 0, 0, 0)
        _V2Desde ("Velo 2: desde", Vector) = (0, 0, 0, 0)
        _V2Cob ("Velo 2: cobertura", Vector) = (0, 0, 0, 0)
        _ADeforma ("Amplitud de la deformación", Float) = 0
        _Duty ("Fracción del titileo que baja", Float) = 0.22
        _TwinkleBase ("Base de la frecuencia del titileo", Float) = 224
        _DeformaY ("Fase: deformación vertical", Float) = 0
        _DeformaX ("Fase: deformación horizontal", Float) = 0
        _Velo1 ("Fase: velo 1", Float) = 0
        _Velo2 ("Fase: velo 2", Float) = 0
        _MediasY ("Fase: estrellas medias, vertical", Float) = 0
        _MediasX ("Fase: estrellas medias, horizontal", Float) = 0
        _Twinkle ("Fase: titileo", Float) = 0
        _Mota0Y ("Fase: motas lejanas, vertical", Float) = 0
        _Mota0X ("Fase: motas lejanas, horizontal", Float) = 0
        _Mota1Y ("Fase: motas medias, vertical", Float) = 0
        _Mota1X ("Fase: motas medias, horizontal", Float) = 0
        _Mota2Y ("Fase: motas cercanas, vertical", Float) = 0
        _Mota2X ("Fase: motas cercanas, horizontal", Float) = 0
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
            TEXTURE2D(_Fields1);
            TEXTURE2D(_Points0);
            TEXTURE2D(_Points1);
            float4 _CMedia0, _CMedia1, _CMedia2;
            float4 _V1Desde, _V1Cob, _V2Desde, _V2Cob;
            float _ADeforma, _Duty, _TwinkleBase;
            float _DeformaY, _DeformaX, _Velo1, _Velo2, _MediasY, _MediasX, _Twinkle;
            float _Mota0Y, _Mota0X, _Mota1Y, _Mota1X, _Mota2Y, _Mota2X;

            // muestrea() de gen_fondos_v2.py sobre _Fields0: bilineal, x cierra, y se pega al borde.
            float4 Sample(float fy, float fx)
            {
                fy = clamp(fy, 0.0, FONDO_ALTO - 1.001);
                float y0 = floor(fy);
                float xf = floor(fx);
                float ty = fy - y0;
                float tx = fx - xf;
                int x0 = Wrap((int)xf, FONDO_ANCHO);
                int x1 = Wrap(x0 + 1, FONDO_ANCHO);
                int y = (int)y0;
                float4 a = ROW(_Fields0, x0, y, FONDO_ALTO) * (1.0 - tx) + ROW(_Fields0, x1, y, FONDO_ALTO) * tx;
                float4 b = ROW(_Fields0, x0, y + 1, FONDO_ALTO) * (1.0 - tx) + ROW(_Fields0, x1, y + 1, FONDO_ALTO) * tx;
                return a * (1.0 - ty) + b * ty;
            }

            // Una capa de motas: todas derivan a la misma velocidad desde su posición de partida, así
            // que el punto que cae en el píxel solo puede venir de cuatro celdas. `zw`: la capa está
            // en los canales b y a.
            bool Mote(int2 p, float dy, float dx, bool zw, bool second)
            {
                float ky = floor(dy);
                float kx = floor(dx);
                float py = dy - ky;
                float px = dx - kx;
                bool hit = false;
                [unroll] for (int a = 0; a < 2; a++)
                {
                    [unroll] for (int b = 0; b < 2; b++)
                    {
                        int row = Wrap(p.y - (int)ky - a, FONDO_ALTO);
                        int column = Wrap(p.x - (int)kx - b, FONDO_ANCHO);
                        float4 cell = second ? ROW(_Points1, column, row, FONDO_ALTO) : ROW(_Points0, column, row, FONDO_ALTO);
                        float2 c = zw ? cell.zw : cell.xy;
                        if (c.x > 0.0)
                        {
                            bool overY = Fraction(c.x) + py >= 1.0;
                            bool overX = Fraction(c.y) + px >= 1.0;
                            hit = hit || (overY == (a == 1) && overX == (b == 1));
                        }
                    }
                }
                return hit;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                int2 p = BackgroundPixel(input.uv);
                float threshold = Bayer(p);

                int y0, y1, x0, x1;
                float fy, fx;
                Shift(_DeformaY, p.x, y0, y1, fy);
                Shift(_DeformaX, p.x, x0, x1, fx);
                float dy = lerp(ROW(_Fields0, y0, p.y, FONDO_ALTO).b, ROW(_Fields0, y1, p.y, FONDO_ALTO).b, fy) - 0.5;
                float dx = lerp(ROW(_Fields0, x0, p.y, FONDO_ALTO).a, ROW(_Fields0, x1, p.y, FONDO_ALTO).a, fx) - 0.5;
                float sy = p.y + _ADeforma * dy;
                float sx = p.x + _ADeforma * dx;
                float veil1 = Sample(sy, sx - _Velo1).r;
                float veil2 = Sample(sy, sx - _Velo2).g;
                float4 envelope = ROW(_Fields1, p.x, p.y, FONDO_ALTO);

                float3 color = _CFondo.rgb;
                color = Dither(color, pow(veil1, 2.9) * envelope.r, _V1Desde, _V1Cob, threshold);
                color = Dither(color, pow(veil2, 3.4) * envelope.g, _V2Desde, _V2Cob, threshold);

                float4 s = StaticAt(p);
                color = StaticLow(color, s);

                // Las estrellas medias derivan todas el mismo número entero de píxeles: una lectura.
                int ky = (int)floor(_MediasY);
                int kx = (int)floor(_MediasX);
                float4 star = ROW(_Points1, Wrap(p.x - kx, FONDO_ANCHO), Wrap(p.y - ky, FONDO_ALTO), FONDO_ALTO);
                int info = (int)round(star.a * 255.0);
                if (info >= 1)
                {
                    int dim = 0;
                    if (info >= 2)
                    {
                        float m = _TwinkleBase + round(star.b * 255.0);
                        float phase = frac(_Twinkle * m + (info - 2) / 254.0);
                        float wave = sin(PI * phase / _Duty);
                        float down = phase < _Duty ? wave * wave : 0.0;
                        dim = (int)round(2.0 * down);
                    }
                    color = dim == 0 ? _CMedia0.rgb : dim == 1 ? _CMedia1.rgb : _CMedia2.rgb;
                }

                color = StaticHigh(color, s);

                if (Mote(p, _Mota0Y, _Mota0X, false, false))
                    color = _C5.rgb;
                if (Mote(p, _Mota1Y, _Mota1X, true, false))
                    color = _C4.rgb;
                if (Mote(p, _Mota2Y, _Mota2X, false, true))
                    color = _C3.rgb;
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
