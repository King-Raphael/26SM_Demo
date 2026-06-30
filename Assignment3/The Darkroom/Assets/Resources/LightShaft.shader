Shader "Darkroom/LightShaft"
{
    // Procedural volumetric god-ray for the hanging lamps: a soft cone (narrow + bright at
    // the bulb, widening + fading toward the floor) with drifting/settling FBM dust inside,
    // so the beam reads as light catching motes in the air — refined + alive, like the fog.
    // Additive; tint + overall density come from the SpriteRenderer's vertex colour.
    Properties
    {
        _Speed ("Dust Speed", Float) = 0.06
        _Scale ("Dust Scale", Float) = 0.5
        _Seed  ("Seed", Float) = 0.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha One   // additive — light adds to the scene
        ZWrite Off Cull Off

        Pass
        {
            Name "LightShaft"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Speed, _Scale, _Seed;

            struct Attr { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct V2F  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float2 wpos : TEXCOORD1; };

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
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
                float yTop = i.uv.y;                 // 1 at the bulb (top), 0 at the floor (bottom)

                // cone: half-width grows from a point at the bulb to wide at the floor
                float halfW = lerp(0.06, 0.5, 1.0 - yTop);
                float cone = saturate(1.0 - abs(i.uv.x - 0.5) / max(halfW, 1e-3));
                cone = cone * cone;                  // soft edges

                // bright near the source, dissolving SOFTLY to nothing at the bottom
                // (smoothstep is flat at 0, so no hard-cut edge where the beam ends)
                float vfall = smoothstep(0.04, 0.72, yTop);

                // dust caught in the beam: FBM in world space, settling downward over time
                float t = _Time.y * _Speed;
                float dust = Fbm(i.wpos * _Scale + float2(_Seed, t * 1.5));
                dust = 0.5 + 0.5 * dust;             // keep the beam lit, with dusty striations

                half a = cone * vfall * dust * i.color.a;
                return half4(i.color.rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
