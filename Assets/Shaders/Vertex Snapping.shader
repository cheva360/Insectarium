Shader "Custom/VertexSnapping"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _SnapIntensity ("Snap Intensity", Range(0.001,0.05)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _SnapIntensity;
            CBUFFER_END

            float2 snapToGrid(float2 value, float snapValue) {
                return floor(value / snapValue + 0.5) * snapValue;
            }
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float4 worldPosition = mul(UNITY_MATRIX_MV, float4(IN.positionOS.xyz, 1.0));
                
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
               
                float2 screenPos = OUT.positionHCS.xy / OUT.positionHCS.w;
                screenPos = snapToGrid(screenPos, _SnapIntensity);
                OUT.positionHCS.xy = screenPos * OUT.positionHCS.w;

                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                return color;
            }
            ENDHLSL
        }
    }
}
