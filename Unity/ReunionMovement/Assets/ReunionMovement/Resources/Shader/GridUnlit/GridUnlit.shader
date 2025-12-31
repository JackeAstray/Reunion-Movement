Shader "Custom/GridUnlit"
{
    Properties
    {
        _Color("Base Color", Color) = (0.7,0.7,0.7,1)
        _Space("Grid Space", Float) = 10.0
        _GridWidth("Grid Width", Float) = 0.5
        _GridIntensity("Grid Intensity", Range(0,1)) = 0.7
        _Scale("World Scale", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            fixed4 _Color;
            float _Space;
            float _GridWidth;
            float _GridIntensity;
            float _Scale;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.localPos = v.vertex.xyz;
                o.uv = v.uv;
                return o;
            }

            // returns 0 at the center of a grid line, 1 away from line
            float gridValue(float2 coord, float space, float gridWidth)
            {
                float2 gv = frac(coord / space);
                float2 d = abs(gv - 0.5);
                float minDist = min(d.x, d.y);
                float lineWidth = gridWidth / space; // normalized
                // smoothstep for anti-aliased line
                return smoothstep(0.0, lineWidth, minDist);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Use object-local XZ as the coordinate plane so the grid follows the model orientation
                float2 coord = i.localPos.xz * _Scale;

                // Combine two grid scales like the original shader (fine + coarse)
                float g1 = gridValue(coord, _Space, _GridWidth);
                float g2 = gridValue(coord, _Space * 5.0, _GridWidth * 2.0);
                float g = saturate(g1 * g2);

                // Mix between grid intensity and full brightness
                float mul = lerp(_GridIntensity, 1.0, g);

                fixed3 col = _Color.rgb * mul;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}