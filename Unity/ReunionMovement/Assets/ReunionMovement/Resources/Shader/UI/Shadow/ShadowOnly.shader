Shader "Hidden/UI/ShadowOnly"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.5)
        _ShadowBlurIntensity ("Shadow Blur Intensity", Range(0,8)) = 1
        _ShadowColorFilter ("Shadow Color Filter", Float) = 1
        _ShadowColorGlow ("Shadow Color Glow", Range(0,1)) = 0
        _SamplingWidth ("Sampling Width", Float) = 1
        _SamplingScale ("Sampling Scale", Float) = 1
        _AllowOutOfBoundsShadow ("Allow Out Of Bounds Shadow", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Lighting Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "ShadowOnly"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "../../Base/2D_SDF.cginc"
            #include "../../Base/Common.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;

            float4 _ShadowColor;
            float _ShadowBlurIntensity;
            int _ShadowColorFilter;
            float _ShadowColorGlow;
            float _SamplingWidth;
            float _SamplingScale;
            float _AllowOutOfBoundsShadow;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                // Pass the vertex color through multiplied by material tint
                o.color = v.color * _Color;
                return o;
            }

            v2f _fragInput;
            fixed4 _TextureSampleAdd;

            half4 apply_color_filter(int mode, half4 inColor, half4 factor, float intensity, float glow)
            {
                half4 color = inColor;
                if (mode == 1) // Color.Multiply
                {
                    color.rgb = color.rgb * factor.rgb;
                    color *= factor.a;
                }
                else if (mode == 2) // Color.Additive
                {
                    color.rgb = color.rgb + factor.rgb * color.a * factor.a;
                }
                else if (mode == 3) // Color.Subtractive
                {
                    color.rgb = color.rgb - factor.rgb * color.a * factor.a;
                }
                else if (mode == 4) // Color.Replace
                {
                    color.rgb = factor.rgb * color.a;
                    color *= factor.a;
                }
                else if (mode == 5) // Color.MultiplyLuminance
                {
                    color.rgb = (1 + Luminance(color.rgb)) * factor.rgb * factor.a / 2 * color.a;
                }
                else if (mode == 6) // Color.MultiplyAdditive
                {
                    color.rgb = color.rgb * (1 + factor.rgb * factor.a);
                }
                else if (mode == 7) // Color.HsvModifier
                {
                    const float3 hsv = rgb_to_hsv(color.rgb);
                    color.rgb = hsv_to_rgb(hsv + factor.rgb) * color.a * factor.a;
                    color.a = inColor.a * factor.a;
                }
                else if (mode == 8) // Color.Contrast
                {
                    color.rgb = ((color.rgb - 0.5) * (factor.r + 1) + 0.5 + factor.g * 1.5) * color.a * factor.a;
                    color.a = color.a * factor.a;
                }

                if (0 < mode)
                {
                    color = lerp(inColor, color, intensity);
                    color.a *= 1 - glow * intensity;
                }

                return color;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy * _SamplingScale * _SamplingWidth;

                // Determine if this vertex was marked as shadow by the mesh generator.
                // We use vertex color RGB == 0 and alpha > 0 to indicate shadow quad.
                bool isShadowVertex = IN.color.a > 0.001 && IN.color.r < 0.001 && IN.color.g < 0.001 && IN.color.b < 0.001;

                if (isShadowVertex)
                {
                    // build blurred sample for shadow mask (apply offset here)
                    half4 blurSample = 0;

                    bool allowOOB = _AllowOutOfBoundsShadow > 0.5;

                    if (_ShadowBlurIntensity <= 0.001)
                    {
                        float2 sampleUv = IN.uv;
                        if (!allowOOB) sampleUv = saturate(sampleUv);
                        blurSample = tex2D(_MainTex, sampleUv);
                    }
                    else
                    {
                        const float w[9] = {1.0/16.0, 2.0/16.0, 1.0/16.0,
                                             2.0/16.0, 4.0/16.0, 2.0/16.0,
                                             1.0/16.0, 2.0/16.0, 1.0/16.0};
                        float s = saturate(_ShadowBlurIntensity);
                        float2 off;
                        int idx = 0;
                        for (int y = -1; y <= 1; ++y)
                        {
                            for (int x = -1; x <= 1; ++x)
                            {
                                off = float2(x, y) * texel * (0.5 + s);
                                float2 uv = IN.uv + off;
                                if (!allowOOB) uv = saturate(uv);
                                idx = (y + 1) * 3 + (x + 1);
                                blurSample += tex2D(_MainTex, uv) * w[idx];
                            }
                        }
                    }

                    float shadowMask = blurSample.a;
                    float shadowAlpha = shadowMask * _ShadowColor.a * IN.color.a;
                    half3 shadowPremult = _ShadowColor.rgb * shadowAlpha;
                    return half4(shadowPremult, shadowAlpha);
                }

                // Non-shadow (original) geometry: render normally (tinted texture)
                half4 origSample = tex2D(_MainTex, IN.uv);
                half4 orig = origSample * IN.color; // orig.a = tex.a * vertexColor.a
                // premultiply RGB by alpha for correct blending
                orig.rgb *= orig.a;
                return orig;
            }
            ENDCG
        }
    }
}
