Shader "Hidden/GaussianBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Float) = 2.0
    }

    SubShader
    {
        ZWrite Off
        Cull Off
        ZTest Always

        // Pass 0: 水平方向高斯模糊
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 9 权重高斯核（sigma ≈ 4）
            static const float weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * weights[0];
                float px = _MainTex_TexelSize.x * _BlurSize;

                col += tex2D(_MainTex, i.uv + float2(px, 0.0))  * weights[1];
                col += tex2D(_MainTex, i.uv - float2(px, 0.0))  * weights[1];
                col += tex2D(_MainTex, i.uv + float2(px * 2, 0.0)) * weights[2];
                col += tex2D(_MainTex, i.uv - float2(px * 2, 0.0)) * weights[2];
                col += tex2D(_MainTex, i.uv + float2(px * 3, 0.0)) * weights[3];
                col += tex2D(_MainTex, i.uv - float2(px * 3, 0.0)) * weights[3];
                col += tex2D(_MainTex, i.uv + float2(px * 4, 0.0)) * weights[4];
                col += tex2D(_MainTex, i.uv - float2(px * 4, 0.0)) * weights[4];

                return col;
            }
            ENDCG
        }

        // Pass 1: 垂直方向高斯模糊
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            static const float weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * weights[0];
                float py = _MainTex_TexelSize.y * _BlurSize;

                col += tex2D(_MainTex, i.uv + float2(0.0, py))  * weights[1];
                col += tex2D(_MainTex, i.uv - float2(0.0, py))  * weights[1];
                col += tex2D(_MainTex, i.uv + float2(0.0, py * 2)) * weights[2];
                col += tex2D(_MainTex, i.uv - float2(0.0, py * 2)) * weights[2];
                col += tex2D(_MainTex, i.uv + float2(0.0, py * 3)) * weights[3];
                col += tex2D(_MainTex, i.uv - float2(0.0, py * 3)) * weights[3];
                col += tex2D(_MainTex, i.uv + float2(0.0, py * 4)) * weights[4];
                col += tex2D(_MainTex, i.uv - float2(0.0, py * 4)) * weights[4];

                return col;
            }
            ENDCG
        }
    }
}
