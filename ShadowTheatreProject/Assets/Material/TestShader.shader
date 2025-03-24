Shader "Custom/TestShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorLevels ("Color Levels", Int) = 4
        _EdgeThreshold ("Edge Threshold", Range(0,1)) = 0.2
        _EdgeColor ("Edge Color", Color) = (0,0,0,1)
        _Brightness ("Brightness", Range(0.5, 2.0)) = 1.0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            sampler2D _CameraDepthNormalsTexture;
            int _ColorLevels;
            float _EdgeThreshold;
            float4 _EdgeColor;
            float _Brightness;

            // 边缘检测函数
            float edgeDetection(float2 uv)
            {
                // 声明独立的深度和法线变量
                float centerDepth, leftDepth, rightDepth, topDepth, bottomDepth;
                float3 centerNormal, leftNormal, rightNormal, topNormal, bottomNormal;

                // 解码中心点
                DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, uv), 
                    centerDepth, centerNormal);
                
                // 解码相邻像素
                DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, uv + float2(1,0)/_ScreenParams.xy), 
                    leftDepth, leftNormal);
                DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, uv + float2(-1,0)/_ScreenParams.xy), 
                    rightDepth, rightNormal);
                DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, uv + float2(0,1)/_ScreenParams.xy), 
                    topDepth, topNormal);
                DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, uv + float2(0,-1)/_ScreenParams.xy), 
                    bottomDepth, bottomNormal);

                // 计算深度差异
                float depthEdge = abs(centerDepth - leftDepth) + abs(centerDepth - rightDepth) +
                                 abs(centerDepth - topDepth) + abs(centerDepth - bottomDepth);

                // 计算法线差异
                float normalEdge = distance(centerNormal, leftNormal) + 
                                 distance(centerNormal, rightNormal) +
                                 distance(centerNormal, topNormal) + 
                                 distance(centerNormal, bottomNormal);

                return saturate(depthEdge * 10 + normalEdge * 0.5);
            }

            // 颜色量化函数
            float3 quantizeColor(float3 color)
            {
                color = saturate(color * _Brightness); 
                color = floor(color * _ColorLevels) / _ColorLevels;
                return color;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 原始颜色
                float4 col = tex2D(_MainTex, i.uv);
                
                // 边缘检测
                float edge = edgeDetection(i.uv);
                edge = step(_EdgeThreshold, edge);
                
                // 颜色量化
                float3 quantized = quantizeColor(col.rgb);
                
                // 使用一个安全的边缘颜色，确保alpha不为0
                float4 safeEdgeColor = _EdgeColor;
                if (safeEdgeColor.a <= 0)
                {
                    // 即使alpha想要设为0，也保留非常小的值
                    safeEdgeColor.a = 0.001f;
                }
                
                // 混合边缘和颜色（保留原始alpha）
                float4 result = lerp(float4(quantized, col.a), safeEdgeColor, edge);
                return result;
            }
            ENDCG
        }
    }
}