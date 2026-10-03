// Glamour Games - animierter Hintergrund: Nebel (Domain-Warping-Rauschen), driftende funkelnde Sterne,
// Polarlicht-Baender und Vignette. Laeuft komplett auf der GPU; Farben folgen den Akzentfarben der Szene.
Shader "Glamour/Backdrop"
{
    Properties
    {
        _T ("Time", Float) = 0
        _A1 ("Accent 1", Color) = (0,1,0.95,1)
        _A2 ("Accent 2", Color) = (1,0.12,0.47,1)
        _View ("Design view rect", Vector) = (0,0,1600,900)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-100" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            float _T;
            float4 _A1, _A2, _View;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 d : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.d = v.uv; // Designraum-Koordinaten
                return o;
            }

            float3 lin(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return GammaToLinearSpace(c);
            #endif
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + float2(17.1, 9.2); a *= 0.5; }
                return v;
            }
            float blob(float2 d, float2 c, float r) { float t = saturate(1.0 - length(d - c) / r); return t; }

            // Sternenschicht: ein Stern pro Zelle, driftend und funkelnd
            float3 stars(float2 d, float cell, float z, float t)
            {
                float2 p = d + float2(t * 6.0 * z, -t * 4.0 * z) * 1.0;
                float2 id = floor(p / cell);
                float2 f = frac(p / cell);
                float h = hash21(id);
                float2 sp = float2(hash21(id + 7.3), hash21(id + 3.1)) * 0.8 + 0.1;
                float dist = length((f - sp) * cell);
                float tw = 0.5 + 0.5 * sin(t * 1.5 * z * (0.6 + h) + h * 40.0);
                float r = 0.8 + 1.6 * z;
                float core = saturate(1.0 - dist / r);
                float halo = exp(-dist * dist / (r * r * 9.0));
                float on = step(0.35, h);
                float br = on * (0.15 + 0.6 * tw * z);
                float3 col = lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.85, 0.95), frac(h * 13.0));
                return col * (core * core * 2.2 + halo * 0.35) * br;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 d = i.d;
                float t = _T;
                float x0 = _View.x, y0 = _View.y, w = _View.z - _View.x, h = _View.w - _View.y;
                float3 a1 = lin(_A1.rgb), a2 = lin(_A2.rgb), purple = lin(float3(0.627, 0.125, 1.0));
                float3 col = lin(float3(6.0, 1.0, 15.0) / 255.0);

                // grosse weiche Farbflecken (wie im Original)
                col += a1 * 0.22 * pow(blob(d, float2(x0 + w * (0.2 + 0.08 * sin(t * 0.23)), y0 + h * (0.15 + 0.08 * cos(t * 0.31))), w * 0.55), 1.4);
                col += a2 * 0.18 * pow(blob(d, float2(x0 + w * (0.85 + 0.07 * cos(t * 0.19)), y0 + h * (0.9 + 0.06 * sin(t * 0.27))), w * 0.5), 1.4);
                col += purple * 0.09 * pow(blob(d, float2(x0 + w * (0.5 + 0.2 * sin(t * 0.11)), y0 + h * (0.5 + 0.15 * cos(t * 0.13))), w * 0.35), 1.4);

                // Nebel mit Domain-Warping
                float2 q = d / 520.0;
                float2 warp = float2(fbm(q + float2(0.0, t * 0.03)), fbm(q + float2(5.2, -t * 0.025)));
                float n = fbm(q * 1.4 + warp * 1.6 + float2(t * 0.01, 0));
                float n2 = fbm(q * 2.2 - warp + float2(-t * 0.015, t * 0.008));
                float neb = smoothstep(0.42, 0.95, n) * 0.55 + smoothstep(0.5, 1.0, n2) * 0.25;
                float3 nebCol = lerp(a1, a2, saturate(n2 * 1.3 - 0.2));
                nebCol = lerp(nebCol, purple, 0.35);
                col += nebCol * neb * 0.11;

                // Sterne in drei Tiefenschichten
                col += stars(d, 46.0, 0.25, t) * 0.6;
                col += stars(d + 311.0, 64.0, 0.6, t) * 0.8;
                col += stars(d + 733.0, 92.0, 1.0, t);

                // Polarlicht-Baender oben
                for (int k = 0; k < 3; k++)
                {
                    float baseY = y0 + 120.0 + k * 28.0;
                    float top = baseY + sin((d.x - x0) * 0.004 + t * (0.25 + k * 0.07) + k * 2.0) * 34.0 + sin((d.x - x0) * 0.011 - t * 0.4) * 12.0;
                    float bot = baseY + 90.0 + sin((d.x - x0) * 0.005 + t * 0.3 + k) * 30.0;
                    float v = saturate((d.y - top + 30.0) / max(bot - top + 30.0, 1.0));
                    float band = step(top - 30.0, d.y) * step(d.y, bot) * (v < 0.35 ? v / 0.35 : (1.0 - v) / 0.65);
                    float rays = 0.75 + 0.25 * sin(d.x * 0.05 + t * 0.7 + k * 3.0) * sin(d.x * 0.013 - t * 0.3);
                    float3 ac = k == 1 ? a2 : (k == 2 ? purple : a1);
                    col += ac * band * rays * 0.13;
                }

                // Vignette
                float2 c = float2(x0 + w * 0.5, y0 + h * 0.5);
                float vr = length(d - c) / (max(w, h) * 0.75);
                col *= lerp(1.0, 0.45, smoothstep(0.55, 1.0, vr));

                return float4(col, 1);
            }
            ENDCG
        }
    }
}
