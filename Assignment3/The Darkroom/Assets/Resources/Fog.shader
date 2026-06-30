Shader "Darkroom/Fog"
{
    // Procedural drifting fog for VaporMotes: domain-warped FBM noise sampled in WORLD
    // space and evolved over time, so dense patches FORM and DISSIPATE (gather/disperse)
    // and flow internally — not a rigid texture sliding around. Tint + overall density
    // come from the SpriteRenderer's vertex colour; soft-masked to fade at the quad edges.
    Properties
    {
        _Scale ("Noise Scale", Float) = 0.1
        _Speed ("Drift Speed", Float) = 0.08
        _Seed  ("Seed", Float) = 0.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off

        Pass
        {
            Name "Fog"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Scale, _Speed, _Seed;

            struct Attr { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct V2F  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float2 wpos : TEXCOORD1; };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }
            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            float Fbm(float2 p)
            {
                float s = 0.0, amp = 0.5;
                [unroll] for (int k = 0; k < 4; k++) { s += amp * VNoise(p); p *= 2.03; amp *= 0.5; }
                return s;
            }

            V2F vert(Attr i)
            {
                V2F o;
                VertexPositionInputs vp = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.wpos = vp.positionWS.xy;
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }

            half4 frag(V2F i) : SV_Target
            {
                float2 p = i.wpos * _Scale + _Seed;
                float t = _Time.y * _Speed;
                // domain warp -> billowing; the +t terms make the field EVOLVE (form/dissipate)
                float2 warp = float2(Fbm(p * 0.6 + t * 0.25), Fbm(p * 0.6 + 5.2 - t * 0.20));
                float d = Fbm(p + warp * 1.4 + float2(0.0, t * 0.45));
                d = saturate(d * 1.8 - 0.55); // contrast: clear gaps vs dense clumps that drift

                float2 e = (i.uv - 0.5) * 2.0;
                float mask = saturate(1.0 - dot(e, e));
                mask *= mask; // fade to nothing at the quad edges
                mask *= 0.55 + 0.45 * (1.0 - i.uv.y); // settles low, thins toward the top (heavier air)

                half a = d * mask * i.color.a;
                // catch the overhead light: the haze brightens + warms toward the top of
                // the frame, where the hanging lamps are (a cheap volumetric-glow approx)
                float lit = 0.8 + 0.6 * i.uv.y;
                half3 rgb = i.color.rgb * lit;
                rgb = lerp(rgb, rgb * half3(1.12, 1.04, 0.88), i.uv.y * 0.5); // warmer up high
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
