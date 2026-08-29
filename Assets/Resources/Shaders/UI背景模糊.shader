// UI 背景模糊：抓屏纹理 5×5 带权重 均值 模糊（UI 标准模板：PerRendererData 主纹理 + 透明混合）。
// _BlurSize 控制 模糊 强度（采样 半径，像素）。注意：变量名 必须 英文（HLSL 对 Unicode 标识符 支持 不稳）。
Shader "UI/背景模糊"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0, 8)) = 2
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off
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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy * _BlurSize;
                fixed4 total = 0;
                float weightSum = 0;
                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        float w = 1.0 - (abs(x) + abs(y)) * 0.15;   // 中心 权重 高，边缘 低
                        total += tex2D(_MainTex, i.uv + float2(x, y) * texel) * w;
                        weightSum += w;
                    }
                }
                return total / weightSum;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
