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
        _DiceTex ("3D Dice", 2D) = "black" {}
        _TrayTex ("3D Tray", 2D) = "black" {}
        _HeroTex ("3D Hero", 2D) = "black" {}
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
            sampler2D _DiceTex;
            sampler2D _TrayTex;
            sampler2D _HeroTex;
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

            // ---------------- Physikalisch basierte Beleuchtung (Designraum: x rechts, y unten, z zum Betrachter)
            float hash21(float2 q) { q = frac(q * float2(123.34, 456.21)); q += dot(q, q + 45.32); return frac(q.x * q.y); }
            float vnoise(float2 q)
            {
                float2 i0 = floor(q), f = frac(q); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i0), hash21(i0 + float2(1, 0)), f.x), lerp(hash21(i0 + float2(0, 1)), hash21(i0 + float2(1, 1)), f.x), f.y);
            }
            // Studio-Umgebung (gleiche Lichtsetzung wie die 3D-Wuerfel)
            float3 envStudio(float3 R)
            {
                float h = -R.y;
                float3 c = lerp(float3(0.05, 0.03, 0.09), float3(0.32, 0.26, 0.42), saturate(h * 0.5 + 0.5));
                float k1 = dot(R, normalize(float3(-0.5, -0.7, 0.5)));
                c += float3(1.6, 1.55, 1.5) * smoothstep(0.0, 1.0, saturate((k1 - 0.82) / 0.18 * 3.0));
                float k2 = dot(R, normalize(float3(0.7, -0.3, 0.6)));
                c += float3(0.7, 0.45, 1.1) * smoothstep(0.0, 1.0, saturate((k2 - 0.9) / 0.1 * 3.0));
                float k3 = dot(R, normalize(float3(0.1, -0.25, 1.0)));
                c += float3(0.35, 0.33, 0.38) * smoothstep(0.0, 1.0, saturate((k3 - 0.7) / 0.3 * 2.0));
                return c;
            }
            float3 lightGGX(float3 n, float3 L, float3 Lc, float3 albedo, float3 F0, float metal, float rough)
            {
                float3 v = float3(0, 0, 1);
                float NdL = saturate(dot(n, L)); if (NdL <= 0.0) return 0;
                float NdV = max(n.z, 1e-3);
                float3 h = normalize(L + v);
                float NdH = saturate(dot(n, h));
                float a = rough * rough, a2 = a * a;
                float dd = NdH * NdH * (a2 - 1.0) + 1.0;
                float D = a2 / (3.14159 * dd * dd);
                float k = (rough + 1.0) * (rough + 1.0) / 8.0;
                float G = (NdL / (NdL * (1.0 - k) + k)) * (NdV / (NdV * (1.0 - k) + k));
                float3 F = F0 + (1.0 - F0) * pow(1.0 - saturate(dot(h, v)), 5.0);
                float3 spec = D * G * F / (4.0 * NdL * NdV + 1e-4);
                float3 diff = (1.0 - F) * (1.0 - metal) * albedo / 3.14159;
                return (diff + spec) * Lc * NdL * 3.14159;
            }
            float3 shadePBR(float3 n, float3 albedo, float metal, float rough)
            {
                float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metal);
                float3 c = 0;
                c += lightGGX(n, normalize(float3(-0.45, -0.62, 0.65)), float3(1.0, 0.97, 0.92) * 1.55, albedo, F0, metal, rough);
                c += lightGGX(n, normalize(float3(0.6, -0.15, 0.75)), float3(0.75, 0.82, 1.0) * 0.38, albedo, F0, metal, rough);
                c += lightGGX(n, normalize(float3(0.25, -0.35, -0.9)), float3(1.0, 0.85, 1.0) * 0.55, albedo, F0, metal, rough);
                float NdV = max(n.z, 1e-3);
                float3 Fe = F0 + (max(1.0 - rough, F0) - F0) * pow(1.0 - NdV, 5.0);
                float3 R = float3(0, 0, -1) + 2.0 * n.z * n;
                c += envStudio(R) * Fe * (1.0 - rough * 0.7);
                c += albedo * (1.0 - metal) * 0.2;
                return c;
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
                else if (type == 4) // Kugel: PBR (Kunststoff/Glas, Metall ueber t3.x, Rauheit t3.y)
                {
                    float r = max(hs.x, 1e-4);
                    float2 q = p / r;
                    float z = sqrt(saturate(1.0 - dot(q, q)));
                    float3 n = normalize(float3(q.x, q.y, max(z, 0.02)));
                    float metal = i.t3.x, rough = i.t3.y > 0.0 ? i.t3.y : 0.28;
                    float3 alb = toLinear(base.rgb);
                    rgb = shadePBR(n, alb, metal, rough);
                    // leichte Volumen-/Unterflaechenstreuung fuer satte Neonfarben
                    rgb += alb * pow(z, 2.0) * 0.18 * (1.0 - metal);
                    cov = cover(length(p) - r, 0.0, sigma);
                }
                else if (type == 11) // Stab/Barren: liegender Zylinder mit runden Enden, PBR (Metall t3.x, Rauheit t3.y)
                {
                    float hr = max(hs.y, 1e-4);
                    float ny = clamp(p.y / hr, -1.0, 1.0);
                    float ex = abs(p.x) - (hs.x - hr);
                    float nx = ex > 0.0 ? clamp(ex / hr, -1.0, 1.0) * sign(p.x) : 0.0;
                    float3 n = normalize(float3(nx, ny, sqrt(saturate(1.0 - nx * nx - ny * ny)) + 0.02));
                    float metal = i.t3.x, rough = i.t3.y > 0.0 ? i.t3.y : 0.3;
                    float3 alb = toLinear(base.rgb);
                    // feine Laengs-Schleifspuren im Metall
                    float brush = vnoise(float2(p.x * 0.08, p.y * 3.0)) - 0.5;
                    rgb = shadePBR(normalize(n + float3(0, brush * 0.05, 0)), alb, metal, rough);
                    cov = cover(sdRoundBox(p, hs, rad), sw, sigma);
                }
                else if (type == 14) // Spielstein-Scheibe: erhabener Rand, Rillen, Mulde; PBR-Kunststoff (Rauheit t3.y)
                {
                    float r = max(hs.x, 1e-4);
                    float u = length(p) / r;
                    float2 dir = u > 1e-4 ? p / (u * r) : float2(0, 0);
                    // Hoehenprofil-Ableitung dh/du: Aussenfase, Randwulst, Rillen, flache Mulde
                    float dh = 0.0;
                    dh += u > 0.9 ? -(u - 0.9) / 0.1 * 2.2 : 0.0;
                    dh += (u > 0.68 && u < 0.9) ? cos((u - 0.68) / 0.22 * 3.14159) * 0.9 : 0.0;
                    dh += (u < 0.68) ? sin(u * 40.0) * 0.08 + u * 0.25 : 0.0;
                    float3 n = normalize(float3(-dir * dh * 0.55, 1.0));
                    float rough = i.t3.y > 0.0 ? i.t3.y : 0.32;
                    float3 alb = toLinear(base.rgb);
                    rgb = shadePBR(n, alb, 0.0, rough);
                    cov = cover(length(p) - r, 0.0, sigma);
                }
                else if (type == 13) // Wasser: Wellen-Normalen, Kaustik, Sonnenglanz und Himmelsspiegelung (t3.x = Zeit)
                {
                    float t = i.t3.x;
                    float2 q = p * 0.018;
                    float2 g = 0;
                    float2 d1 = normalize(float2(1.0, 0.35)), d2 = normalize(float2(-0.6, 1.0)), d3 = normalize(float2(0.2, -1.0));
                    g += d1 * 1.6 * 0.10 * cos(dot(q, d1) * 1.6 * 6.2832 + t * 1.3);
                    g += d2 * 2.9 * 0.05 * cos(dot(q, d2) * 2.9 * 6.2832 - t * 1.7);
                    g += d3 * 5.3 * 0.022 * cos(dot(q, d3) * 5.3 * 6.2832 + t * 2.3);
                    float e = 0.05, n0 = vnoise(q * 6.0 + t * 0.35);
                    g += float2(vnoise(q * 6.0 + float2(e, 0) + t * 0.35) - n0, vnoise(q * 6.0 + float2(0, e) + t * 0.35) - n0) / e * 0.025;
                    float3 n = normalize(float3(-g.x, -g.y, 1.0));
                    float depth = saturate(0.5 + p.y / max(hs.y * 2.0, 1.0));
                    float3 deep = lerp(float3(0.0, 0.17, 0.30), float3(0.0, 0.05, 0.14), depth);
                    float c1 = vnoise(q * 1.7 + n.xy * 1.5 + t * 0.25), c2 = vnoise(q * 1.9 - n.xy * 1.5 - t * 0.21);
                    float caus = pow(saturate(1.0 - abs(c1 - c2) * 2.2), 5.0);
                    rgb = deep + float3(0.08, 0.45, 0.5) * caus * 0.35;
                    rgb += lightGGX(n, normalize(float3(-0.45, -0.62, 0.65)), float3(1.0, 0.97, 0.92) * 1.4, 0, float3(0.02, 0.02, 0.02), 0, 0.12);
                    float3 R = float3(0, 0, -1) + 2.0 * n.z * n;
                    float fr = 0.02 + 0.98 * pow(1.0 - n.z, 5.0);
                    rgb += envStudio(R) * fr * 0.5;
                    cov = cover(sdRoundBox(p, hs, rad), 0.0, sigma);
                }
                else if (type == 12) // Struktur-Overlay: Filz (t3.x=1) oder Papier (t3.x=2), Staerke t3.y, Randabdunklung t3.z
                {
                    float d = sdRoundBox(p, hs, rad);
                    float s = i.t3.y, edge = i.t3.z;
                    float g;
                    if (i.t3.x < 1.5)
                    {
                        float f1 = vnoise(p * float2(0.9, 0.22)), f2 = vnoise(p * float2(0.22, 0.9) + 17.0), f3 = vnoise(p * 1.7 + 5.0);
                        g = (f1 * 0.4 + f2 * 0.4 + f3 * 0.2 - 0.5) * 2.0;
                    }
                    else g = (vnoise(p * 1.3) * 0.6 + vnoise(p * 3.1 + 9.0) * 0.4 - 0.5) * 2.0;
                    float rim = edge > 0.0 ? pow(saturate(1.0 + d / edge), 2.0) : 0.0;
                    float dark = saturate(-g) * s + rim * 0.55;
                    float lite = saturate(g) * s * 0.25;
                    rgb = float3(1, 1, 1) * lite / max(dark + lite, 1e-4);
                    a = saturate(dark + lite) * base.a;
                    cov = cover(d, 0.0, sigma);
                }
                else if (type == 5) // Bild mit optional abgerundeter Maske
                {
                    float4 tx = tex2D(_ImgTex, i.t0.zw);
                    rgb *= tx.rgb;
                    a *= tx.a;
                    cov = rad > 0.0 ? cover(sdRoundBox(p, hs, rad), 0.0, sigma) : 1.0;
                }
                else if (type == 10 || type == 15 || type == 16) // 3D-Wuerfel / Tischszene / Pokal aus Render-Textur (linear, vormultipliziert, Pokal in HDR)
                {
                    float4 tx = type == 10 ? tex2D(_DiceTex, i.t0.zw) : type == 15 ? tex2D(_TrayTex, i.t0.zw) : tex2D(_HeroTex, i.t0.zw);
                    rgb = tx.rgb / max(tx.a, 1e-4);
                    a *= tx.a;
                }
                else if (type == 6) // SDF-Glyphe
                {
                    float4 tx = tex2D(_FontTex, i.t0.zw);
                    float v = lerp(tx.r, tx.a, _FontAlpha);
                    float spreadObj = i.t3.x;
                    float d = (0.5 - v) * 2.0 * spreadObj;  // Objekt-Einheiten, aussen positiv
                    float s = max(min(blur, spreadObj / 3.0), 0.5 * px);
                    cov = cover(d, sw, s) * saturate((spreadObj - d) / (0.3 * spreadObj));
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

                // Weichgezeichnete Formen am Quad-Rand sauber auf 0 auslaufen lassen (keine sichtbaren Kanten)
                if (blur > 0.0 && (type == 1 || type == 2 || type == 7 || type == 8))
                {
                    float2 ext = (type == 7 ? float2(1e5, hs.y) : hs) + 3.0 * blur + sw * 0.5;
                    float2 wnd = saturate((ext - abs(p)) / blur);
                    cov *= wnd.x * wnd.y;
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
