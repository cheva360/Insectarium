Shader "Custom/Pixelation"
{
    Properties
    {
        _Pixelation("Pixelation", Integer) = 512
        _ColorPrecision("Color Precision", Integer) = 32
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float _Pixelation;
            float _ColorPrecision;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
            CBUFFER_END

            half4 Frag(Varyings IN) : SV_Target
            {
                //pixelation 
                float2 uv = IN.texcoord.xy;
                _Pixelation *= 1000;
                uv.x = floor(uv.x * _Pixelation * _BlitTexture_TexelSize.y) / (_Pixelation * _BlitTexture_TexelSize.y);
                uv.y = floor(uv.y * _Pixelation * _BlitTexture_TexelSize.x) / (_Pixelation * _BlitTexture_TexelSize.x);
                
                float4 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
                //color precision
                col = floor(col * _ColorPrecision)/_ColorPrecision;
                return col;
            }
            ENDHLSL
        }
    }
}
