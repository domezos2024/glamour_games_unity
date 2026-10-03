// Unsichtbarer Tisch, der nur Schatten zeigt: echte Shadow-Map-Schatten des Hauptlichts plus weiche Kontaktschatten
// (Ambient Occlusion) unter bis zu 8 Objekten. Ausgabe: vormultipliziertes Schwarz mit Deckkraft.
Shader "Glamour/ShadowCatcher"
{
    Properties
    {
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.8
        _AOStrength ("AO Strength", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _ShadowStrength;
                float _AOStrength;
            CBUFFER_END
            float4 _Blobs[8]; // xyz = Weltposition (Wuerfelmitte), w = Radius
            float _BlobCount;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                half sh = MainLightRealtimeShadow(sc);
                half shadowA = (1.0 - sh) * _ShadowStrength;
                half ao = 0;
                for (int k = 0; k < 8; k++)
                {
                    if (k >= (int)_BlobCount) break;
                    float4 b = _Blobs[k];
                    float h = max(b.y - 0.5, 0.0);
                    float d = length(i.positionWS.xz - b.xz);
                    float r = b.w * (1.0 + h * 0.8);
                    float f = saturate(1.0 - d / r);
                    ao = max(ao, f * f * (3.0 - 2.0 * f) * _AOStrength / (1.0 + h * 2.5));
                }
                half a = 1.0 - (1.0 - shadowA) * (1.0 - ao);
                return half4(0, 0, 0, a);
            }
            ENDHLSL
        }
    }
}
