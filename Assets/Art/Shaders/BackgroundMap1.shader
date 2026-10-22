// T5-05b: fondo del mapa 1, Sistema Interior (ART-BG-01). Luz que barre: tres juegos de rayos que
// salen de un sol fuera de cuadro, el polvo zodiacal que solo se ve donde le da un rayo, y las
// estrellas débiles que el sol lava al respirar. Reproduce seccion1() de tools/gen_fondos_v2.py;
// las fases las reduce BackgroundView y las texturas y constantes las pone BackgroundAssets desde
// bg_section1.json.
Shader "Nova/Background/Map1"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite", 2D) = "white" {}
        [NoScaleOffset] _Fields0 ("Sol, viñeta, rayos, polvo", 2D) = "black" {}
        [NoScaleOffset] _Fields1 ("Grano del polvo", 2D) = "black" {}
        [NoScaleOffset] _Points ("Estrellas débiles", 2D) = "black" {}
        [NoScaleOffset] _Static ("Lo fijo", 2D) = "black" {}
        _Window ("Ventana", Vector) = (0, 0, 960, 540)
        _CFondo ("Fondo", Color) = (0, 0, 0, 1)
        _C3 ("Banda 3", Color) = (0, 0, 0, 1)
        _C4 ("Banda 4", Color) = (0, 0, 0, 1)
        _C5 ("Banda 5", Color) = (0, 0, 0, 1)
        _C6 ("Banda 6", Color) = (0, 0, 0, 1)
        _CDebil ("Estrella débil", Color) = (0, 0, 0, 1)
        _L1Desde ("Resplandor: desde", Vector) = (0, 0, 0, 0)
        _L1Cob ("Resplandor: cobertura", Vector) = (0, 0, 0, 0)
        _L2Desde ("Rayos: desde", Vector) = (0, 0, 0, 0)
        _L2Cob ("Rayos: cobertura", Vector) = (0, 0, 0, 0)
        _L3Desde ("Polvo: desde", Vector) = (0, 0, 0, 0)
        _L3Cob ("Polvo: cobertura", Vector) = (0, 0, 0, 0)
        _ABreath ("Amplitud de la respiración", Float) = 0
        _Sol ("Sol (x, y)", Vector) = (0, 0, 0, 0)
        _RayK ("Frecuencia de los rayos", Vector) = (0, 0, 0, 0)
        _RayP ("Peso de los rayos", Vector) = (0, 0, 0, 0)
        _Breath ("Fase: respiración", Float) = 0
        _Ray0 ("Fase: rayos 0", Float) = 0
        _Ray1 ("Fase: rayos 1", Float) = 0
        _Ray2 ("Fase: rayos 2", Float) = 0
        _Polvo ("Fase: polvo", Float) = 0
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
            TEXTURE2D(_Points);
            float4 _CDebil;
            float4 _L1Desde, _L1Cob, _L2Desde, _L2Cob, _L3Desde, _L3Cob;
            float _ABreath;
            float4 _Sol, _RayK, _RayP;
            float _Breath, _Ray0, _Ray1, _Ray2, _Polvo;

            float4 Frag(Varyings input) : SV_Target
            {
                int2 p = BackgroundPixel(input.uv);
                float threshold = Bayer(p);
                float4 f0 = ROW(_Fields0, p.x, p.y, FONDO_ALTO);    // sol, viñeta, rayos, polvo

                float theta = atan2(p.y - _Sol.y, p.x - _Sol.x);
                float beam = 0.5 + 0.5 * (_RayP.x * sin(_RayK.x * theta + _Ray0)
                                        + _RayP.y * sin(_RayK.y * theta + _Ray1)
                                        + _RayP.z * sin(_RayK.z * theta + _Ray2));
                float sun = f0.r * (1.0 + _ABreath * sin(TWO_PI * _Breath));
                float light = sun * (0.25 + 1.10 * beam);

                float3 color = _CFondo.rgb;
                color = Dither(color, light * (0.45 + 0.55 * f0.g), _L1Desde, _L1Cob, threshold);
                color = Dither(color, f0.b * saturate((beam - 0.48) / 0.30), _L2Desde, _L2Cob, threshold);

                int x0, x1;
                float f;
                Shift(_Polvo, p.x, x0, x1, f);
                float grain = lerp(ROW(_Fields1, x0, p.y, FONDO_ALTO).r, ROW(_Fields1, x1, p.y, FONDO_ALTO).r, f);
                float dust = f0.a * (0.15 + 0.85 * pow(grain, 1.3)) * (0.35 + 0.95 * beam);
                color = Dither(color, dust, _L3Desde, _L3Cob, threshold);

                float4 s = StaticAt(p);
                color = StaticLow(color, s);
                float u = ROW(_Points, p.x, p.y, FONDO_ALTO).r;
                if (u > 0.0 && u - 0.5 / 255.0 < clamp(1.0 - light * 2.4, 0.05, 1.0))
                    color = _CDebil.rgb;
                color = StaticHigh(color, s);
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
