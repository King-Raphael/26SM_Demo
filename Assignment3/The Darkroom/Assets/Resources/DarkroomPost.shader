Shader "Darkroom/Post"
{
    // Fullscreen photographic pass for the Darkroom/Post renderer feature:
    //  - Sabattier SOLARIZATION pulse on every exposure switch (highlights fold back +
    //    a bright Mackie-line edge, red-biased in Over).
    //  - red-biased HALATION: bright areas bleed a soft glow (warm-cream normally,
    //    red-orange in Over / threat — never pure white).
    //  - shadow-weighted emulsion GRAIN.
    // Driven by global floats set per-exposure by DarkroomPostDriver.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "DarkroomPost"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _DR_Solar;     // 0..1 Sabattier pulse (decays after each switch)
            float _DR_Grain;     // grain amount
            float _DR_Halation;  // halation amount
            float _DR_HalRed;    // 0..1: how red the halation/Mackie fringe is
            float _DR_GrainA, _DR_GrainB, _DR_GrainMix; // two grain seeds + crossfade (smooth animation)
            // _BlitTexture + _BlitTexture_TexelSize + sampler_LinearClamp come from Blit.hlsl

            // robust hash (Dave Hoskins) — no axis-aligned streaks at large pixel coords
            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float luma = dot(col, half3(0.299, 0.587, 0.114));

                // Sabattier solarization (only while the pulse is live)
                if (_DR_Solar > 0.001)
                {
                    float fold = smoothstep(0.45, 0.9, luma); // mostly the brights fold
                    half3 inv = 1.0 - col;
                    col = lerp(col, inv, fold * _DR_Solar * 0.7);
                    float edge = saturate(fwidth(luma) * 6.0); // Mackie line on contrast edges
                    half3 mackie = lerp(half3(1.0, 1.0, 1.0), half3(1.0, 0.55, 0.30), _DR_HalRed);
                    col += mackie * edge * _DR_Solar * 0.3;
                }

                // halation: a cheap 8-tap bright bleed, tinted warm-cream -> red in Over
                if (_DR_Halation > 0.001)
                {
                    float2 px = _BlitTexture_TexelSize.xy;
                    half3 bleed = half3(0.0, 0.0, 0.0);
                    [unroll] for (int k = 0; k < 8; k++)
                    {
                        float a = k * 0.785398;
                        float2 o = float2(cos(a), sin(a)) * px * 3.0;
                        half3 s = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + o).rgb;
                        bleed += max(half3(0.0, 0.0, 0.0), s - 0.6); // only true highlights
                    }
                    bleed /= 8.0;
                    half3 halTint = lerp(half3(1.0, 0.92, 0.78), half3(1.0, 0.42, 0.22), _DR_HalRed);
                    col += bleed * halTint * _DR_Halation;
                }

                // emulsion grain — animated, weighted into the shadows
                if (_DR_Grain > 0.001)
                {
                    float2 gc = uv * _BlitTexture_TexelSize.zw * 0.42;
                    float gA = Hash21(gc + _DR_GrainA * 131.0) - 0.5;
                    float gB = Hash21(gc + _DR_GrainB * 131.0) - 0.5;
                    float m = _DR_GrainMix;
                    // crossfade two grain fields (no discrete jump = no stutter), variance-
                    // normalised so the grain intensity stays constant across the blend
                    float g = lerp(gA, gB, m) * rsqrt((1.0 - m) * (1.0 - m) + m * m);
                    float w = saturate(1.0 - abs(luma - 0.42) * 1.6); // peaks in mid/low tones, fades in highlights
                    col += g * _DR_Grain * (0.3 + 0.7 * w);
                }

                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
