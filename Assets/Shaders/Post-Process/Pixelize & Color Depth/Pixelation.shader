Shader "Custom/Pixelation"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _WidthPixelation("Width Pixelation", Float) = 512
        _HeightPixelation("Height Pixelation", Float) = 512
        _ColorPrecision("Color Precision", Float) = 32.0
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

            float _WidthPixelation;
            float _HeightPixelation;
            float _ColorPrecision;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
            CBUFFER_END

            half4 Frag(Varyings IN) : SV_Target
            {
                //pixelation 
                float2 uv = IN.texcoord.xy;
                uv.x = floor(uv.x * _WidthPixelation) / _WidthPixelation;
                uv.y = floor(uv.y * _HeightPixelation) / _HeightPixelation;
                
                float4 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
                //color precision
                col = floor(col * _ColorPrecision)/_ColorPrecision;
                return col;
            }
            ENDHLSL
        }
    }
}
