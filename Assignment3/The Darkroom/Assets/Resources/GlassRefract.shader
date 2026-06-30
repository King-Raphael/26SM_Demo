// The Darkroom — real-glass refraction for the HUD exposure bar.
// A UI shader that samples the captured scene behind it (_GrabTex), bends it by the
// surface slope of a horizontal glass ROD and softly blurs it (frosted refraction),
// then shades it as ONE continuous cylindrical tone with a crisp top sheen and a
// bright Fresnel rim — so it reads as a solid glass rod, not two stacked layers.
// Neutral (no colour cast). Capsule shape is computed from the UV, so a plain
// RawImage quad is enough. Degrades to UI/Default if it fails to compile.
Shader "Darkroom/GlassRefract"
{
    Properties
    {
        [PerRendererData] _MainTex ("Tex", 2D) = "white" {}
        _Color   ("Tint", Color) = (1,1,1,1)
        _GrabTex ("Scene Grab", 2D) = "black" {}
        _Aspect  ("Aspect (w/h)", Float) = 22.5
        _Refract ("Refraction", Range(0,0.06)) = 0.02
        _Blur    ("Blur", Range(0,0.012)) = 0.004
        _Tint    ("Glass Tint", Color) = (0.93,0.95,0.99,1)
        _Spec    ("Specular", Range(0,3)) = 1.5
        _Rim     ("Edge Rim", Range(0,2)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent" "IgnoreProjector"="True"
            "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True"
        }
        Cull Off  Lighting Off  ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex:POSITION; float4 color:COLOR; float2 texcoord:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 scr:TEXCOORD1; };

            sampler2D _GrabTex;
            fixed4 _Color, _Tint;
            float _Aspect, _Refract, _Blur, _Spec, _Rim;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                o.scr = ComputeScreenPos(o.pos);
                return o;
            }

            fixed3 sampleBG(float2 uv, float b)
            {
                fixed3 s = tex2D(_GrabTex, uv).rgb * 0.40;
                s += tex2D(_GrabTex, uv + float2( b, 0)).rgb * 0.15;
                s += tex2D(_GrabTex, uv + float2(-b, 0)).rgb * 0.15;
                s += tex2D(_GrabTex, uv + float2(0,  b)).rgb * 0.15;
                s += tex2D(_GrabTex, uv + float2(0, -b)).rgb * 0.15;
                return s;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // rounded-capsule mask straight from the quad UV (no shape sprite)
                float2 p = float2((i.uv.x - 0.5) * _Aspect, i.uv.y - 0.5);
                float halfStraight = _Aspect * 0.5 - 0.5;
                float qx = max(abs(p.x) - halfStraight, 0.0);
                float dist = sqrt(qx * qx + p.y * p.y) - 0.5;
                float aa = max(fwidth(dist), 1e-4);
                float mask = saturate(0.5 - dist / aa);
                if (mask <= 0.001) discard;

                // cylinder cross-section: c = -1 bottom .. +1 top, n = fullness
                float v = i.uv.y;
                float c = v * 2.0 - 1.0;
                float n = sqrt(saturate(1.0 - c * c));

                // REFRACTION: bend the captured scene vertically by the surface tilt,
                // then frost it with a small 5-tap blur
                float2 suv = i.scr.xy / i.scr.w;
                float2 ruv = suv + float2(0.0, c * _Refract);
                fixed3 bg = sampleBG(ruv, _Blur);

                // ONE continuous glass tone (no stacked layers): refracted bg, tinted,
                // a touch darker through the thick centre
                fixed3 glass = bg * _Tint.rgb * lerp(0.75, 1.05, n);
                // crisp top sheen + a bright Fresnel rim around the whole edge (the 3D read)
                glass += pow(saturate((v - 0.5) / 0.5), 3.0) * _Spec;
                glass += pow(1.0 - n, 2.2) * _Rim;

                float a = mask * i.color.a * saturate(0.5 + n * 0.5);
                return fixed4(glass * i.color.rgb, a);
            }
        ENDCG
        }
    }

    Fallback "UI/Default"
}
