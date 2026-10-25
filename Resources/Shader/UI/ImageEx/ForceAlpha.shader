// ============================================================
// ReunionMovement 强制 Alpha 辅助着色器
// 用途：CameraToImageEx 组件把相机画面 blit 到 RenderTexture 时，
//       将 alpha 强制置为 1，避免 ImageEx（UI 着色器）因主纹理透明
//       而看不到相机画面。
// 注意：本着色器仅供 CameraToImageEx 内部 Graphics.Blit 使用，勿直接挂载。
// ============================================================
Shader "Hidden/ReunionMovement/ForceAlpha"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }

        ZWrite Off
        ZTest Always
        Cull Off

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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                col.a = 1.0;
                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}
