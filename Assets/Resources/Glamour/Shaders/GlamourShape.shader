// Glamour Games - SDF-Formen-Shader.
// Rendert alle 2D-Formen (abgerundete Rechtecke, Ellipsen, Linien, Boegen, Kugeln, Bilder, Schrift)
// eines Frames in einem einzigen Draw-Call. Kanten werden per Signed-Distance-Feld analytisch geglaettet,
// Weichzeichnung (Glow) als Gauss-Faltung angenaehert. Ausgabe ist vormultipliziertes Alpha in HDR:
// Werte ueber 1.0 werden vom URP-Bloom zu echtem Neon-Leuchten.
Shader "Glamour/Shape"
{
    Properties
    {
        _FontTex ("Font SDF Atlas", 2D) = "black" {}
        _ImgTex ("Image Atlas", 2D) = "white" {}
        _FontAlpha ("Font atlas uses alpha channel", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler2D _FontTex;
            sampler2D _ImgTex;
            float _FontAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 t0 : TEXCOORD0; // lokal.xy, tex.uv
                float4 t1 : TEXCOORD1; // halbe Breite/Hoehe, Radius, Strichbreite
                float4 t2 : TEXCOORD2; // Typ, Blur, Leuchtkraft, additiv
                float4 t3 : TEXCOORD3; // Zusatzparameter
                float4 t4 : TEXCOORD4; // Clip-Rechteck (Designraum)
                float4 t5 : TEXCOORD5; // Verlaufsfarbe B
                float4 t6 : TEXCOORD6; // Verlaufsgeometrie
                float4 t7 : TEXCOORD7; // Objektkoordinate.xy, Clip-Radius, Verlaufsmodus
                float4 tangent : TANGENT; // Zusatzparameter 2
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float4 t0 : TEXCOORD0;
                float4 t1 : TEXCOORD1;
                float4 t2 : TEXCOORD2;
                float4 t3 : TEXCOORD3;
                float4 t4 : TEXCOORD4;
                float4 t5 : TEXCOORD5;
                float4 t6 : TEXCOORD6;
                float4 t7 : TEXCOORD7;
                float4 t8 : TEXCOORD8;  // tangent
                float2 dpos : TEXCOORD9; // Designraum-Position
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.t0 = v.t0; o.t1 = v.t1; o.t2 = v.t2; o.t3 = v.t3; o.t4 = v.t4;
                o.t5 = v.t5; o.t6 = v.t6; o.t7 = v.t7; o.t8 = v.tangent;
                o.dpos = float2(v.vertex.x + 800.0, 450.0 - v.vertex.y);
                return o;
            }

            // Normalverteilungsfunktion ueber erf-Naeherung (Abramowitz/Stegun-artig)
            float erfApprox(float x)
            {
                float s = sign(x); x = abs(x);
                float t = 1.0 / (1.0 + 0.47047 * x);
                float y = 1.0 - (0.3480242 * t - 0.0958798 * t * t + 0.7478556 * t * t * t) * exp(-x * x);
                return s * y;
            }
            float Phi(float z) { return 0.5 * (1.0 + erfApprox(z * 0.70710678)); }

            float sdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }
            float sdEllipse(float2 p, float2 ab)
            {
                float k0 = length(p / ab);
                float k1 = length(p / (ab * ab));
                return k1 > 1e-5 ? k0 * (k0 - 1.0) / k1 : -min(ab.x, ab.y);
            }

            // Deckung aus Distanz d (aussen positiv), Strich (sw>0) oder Flaeche, sigma = Weichheit
            float cover(float d, float sw, float sigma)
            {
                if (sw > 0.0)
                {
                    float h = sw * 0.5;
                    return saturate(Phi((h - d) / sigma) - Phi((-h - d) / sigma));
                }
                return Phi(-d / sigma);
            }

            float3 toLinear(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return GammaToLinearSpace(c);
            #endif
            }

            fixed4 frag (v2f i) : SV_Target
            {
                int type = (int)round(i.t2.x);
                float blur = i.t2.y;
                float2 p = i.t0.xy;
                float2 hs = i.t1.xy;
                float rad = i.t1.z;
                float sw = i.t1.w;
                // lokale Einheiten pro Bildschirmpixel
                float px = max(0.5 * (length(float2(ddx(p.x), ddy(p.x))) + length(float2(ddx(p.y), ddy(p.y)))), 1e-4);
                float sigma = max(blur, 0.45 * px);
                float dpx = max(length(float2(ddx(i.dpos.x), ddy(i.dpos.x))), 1e-4);

                float4 base = i.color;
                // Farbverlauf
                int gm = (int)round(i.t7.w);
                if (gm > 0)
                {
                    float2 o = i.t7.xy;
                    float t;
                    if (gm == 1) { float2 g = i.t6.zw - i.t6.xy; t = dot(o - i.t6.xy, g) / max(dot(g, g), 1e-5); }
                    else t = length(o - i.t6.xy) / max(i.t6.z, 1e-5);
                    base = lerp(i.color, i.t5, saturate(t));
                }
                float3 rgb = toLinear(base.rgb);
                float a = base.a;
                float cov = 1.0;

                if (type == 1) // abgerundetes Rechteck / Kreis / Linie
                {
                    cov = cover(sdRoundBox(p, hs, rad), sw, sigma);
                }
                else if (type == 2) // Ellipse
                {
                    cov = cover(sdEllipse(p, hs), sw, sigma);
                }
                else if (type == 3) // radialer Lichthof
                {
                    float t = length(p) / max(hs.x, 1e-4);
                    float f = saturate(1.0 - t);
                    cov = f * (0.65 + 0.35 * f);
                }
                else if (type == 4) // beleuchtete Kugel
                {
                    float r = max(hs.x, 1e-4);
                    float2 q = p / r;
                    float d2 = dot(q, q);
                    float z = sqrt(saturate(1.0 - d2));
                    float3 n = float3(q.x, q.y, z);
                    float3 L = normalize(float3(-0.45, -0.55, 0.75));
                    float diff = saturate(dot(n, L));
                    float spec = pow(saturate(dot(reflect(-L, n), float3(0, 0, 1))), 28.0);
                    float rim = pow(1.0 - z, 3.0);
                    // Skia-Kugel: hell (75%) -> Farbe -> dunkel (40%)
                    float g = saturate(length(q - float2(-0.35, -0.4)) / 1.5);
                    float3 lit = g < 0.45 ? lerp(lerp(base.rgb, 1.0, 0.75), base.rgb, g / 0.45) : lerp(base.rgb, base.rgb * 0.4, (g - 0.45) / 0.55);
                    rgb = toLinear(lit) * (0.55 + 0.6 * diff) + spec * 0.9 + rim * toLinear(base.rgb) * 0.5;
                    cov = cover(length(p) - r, 0.0, sigma);
                }
                else if (type == 5) // Bild mit optional abgerundeter Maske
                {
                    float4 tx = tex2D(_ImgTex, i.t0.zw);
                    rgb *= tx.rgb;
                    a *= tx.a;
                    cov = rad > 0.0 ? cover(sdRoundBox(p, hs, rad), 0.0, sigma) : 1.0;
                }
                else if (type == 6) // SDF-Glyphe
                {
                    float4 tx = tex2D(_FontTex, i.t0.zw);
                    float v = lerp(tx.r, tx.a, _FontAlpha);
                    float spreadObj = i.t3.x;
                    float d = (0.5 - v) * 2.0 * spreadObj;  // Objekt-Einheiten, aussen positiv
                    float s = max(min(blur, spreadObj / 2.6), 0.5 * px);
                    cov = cover(d, sw, s);
                }
                else if (type == 7) // Polylinien-Streifen
                {
                    cov = cover(abs(p.y) - hs.y, 0.0, sigma);
                }
                else if (type == 8) // Rechteck mit Lochraster
                {
                    float d = sdRoundBox(p, hs, rad);
                    float2 cell = i.t3.xy;
                    float hr = i.t3.z;
                    float cols = fmod(i.t3.w, 1000.0);
                    float rows = floor(i.t3.w / 1000.0);
                    float2 q = p - i.t8.xy;
                    float2 ci = clamp(floor(q / cell), float2(0, 0), float2(cols - 1.0, rows - 1.0));
                    float2 c = (ci + 0.5) * cell;
                    float hole = length(q - c) - hr;
                    cov = cover(max(d, -hole), sw, sigma);
                }
                else if (type == 9) // Kreisbogen mit runden Enden
                {
                    float r = hs.x;
                    float ang = degrees(atan2(p.y, p.x));
                    float rel = fmod(ang - i.t3.x + 720.0, 360.0);
                    float d;
                    if (rel <= i.t3.y) d = abs(length(p) - r);
                    else
                    {
                        float a0 = radians(i.t3.x), a1 = radians(i.t3.x + i.t3.y);
                        d = min(length(p - r * float2(cos(a0), sin(a0))), length(p - r * float2(cos(a1), sin(a1))));
                    }
                    cov = cover(d - sw * 0.5, 0.0, sigma);
                }

                // Clip-Rechteck im Designraum (mit Radius)
                float4 cr = i.t4;
                if (cr.z < 9e4)
                {
                    float2 cc = (cr.xy + cr.zw) * 0.5;
                    float2 ch = max((cr.zw - cr.xy) * 0.5, 0.0);
                    float rr = min(i.t7.z, min(ch.x, ch.y));
                    cov *= saturate(0.5 - sdRoundBox(i.dpos - cc, ch, rr) / dpx);
                }

                a *= cov;
                if (a <= 0.0005) discard;
                rgb *= i.t2.z;
                float add = i.t2.w;
                return float4(rgb * a, a * (1.0 - add));
            }
            ENDCG
        }
    }
}
