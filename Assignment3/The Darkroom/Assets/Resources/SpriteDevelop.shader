// The Darkroom — "develop-in" sprite material.
// A latent image rises out of the grain: a noise-thresholded dissolve driven by
// _Develop (0 = undeveloped/blank, 1 = fully developed), with a bright halation
// front that glows where the image is currently surfacing — a chemical-bath wipe
// in place of a flat alpha fade. Unlit + premultiplied, modeled on Sprites/Default
// so it renders for SpriteRenderers under URP 2D; Fallback keeps it from ever
// rendering pink if the program fails to compile/strip.
Shader "Darkroom/SpriteDevelop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Develop ("Develop", Range(0,1)) = 1
        _GrainScale ("Grain Scale", Float) = 38
        _HaloColor ("Halation", Color) = (1.0, 0.95, 0.82, 1)
        _HaloBand ("Halation Band", Range(0.001,0.5)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha   // premultiplied alpha (matches Sprites/Default)

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float  _Develop;
            float  _GrainScale;
            fixed4 _HaloColor;
            float  _HaloBand;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color;
                return OUT;
            }

            // cheap, stable 2D value hash (0..1)
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, IN.texcoord) * IN.color;

                // chunky grain field; the image develops where the grain is below
                // the moving _Develop threshold, with a soft front
                float n      = hash21(floor(IN.texcoord * _GrainScale));
                float reveal = smoothstep(n - 0.06, n + 0.06, _Develop);

                // halation: a warm glow concentrated on the developing front, and
                // strongest mid-develop (vanishes once fully developed)
                float frontDist = abs(n - _Develop);
                float halo = saturate(1.0 - frontDist / max(_HaloBand, 1e-4));
                halo *= _Develop * (1.0 - _Develop) * 3.0;

                fixed4 c;
                c.a   = src.a * reveal;
                c.rgb = src.rgb * c.a;                       // premultiplied base
                c.rgb += _HaloColor.rgb * halo * src.a;      // additive develop-front glow
                return c;
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
