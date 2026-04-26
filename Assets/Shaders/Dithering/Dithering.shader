Shader "Custom/Dithering"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _Steps ("Steps", Integer) = 16
        _RenderScale ("Render Scale", Float) = 1.0
    }
    
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float2 _BaseMap_TexelSize;

            int _Steps;
            float _RenderScale;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 screenPosition : TEXCOORD1;
        };

        half3 GammaToLinearSpace (half3 sRGB)
        {
          return sRGB * (sRGB * (sRGB * 0.305306011h + 0.682171111h) + 0.012522878h);
        }

        half3 LinearToGammaSpace (half3 linRGB)
        {
          linRGB = max(linRGB, half3(0.h, 0.h, 0.h));
          return max(1.055h * pow(linRGB, 0.416666667h) - 0.055h, 0.h);
        }

        float4 Posterize(float4 value, float steps, float bayerValue)
        {
          value.rgb = LinearToGammaSpace(value.rgb);
          value = floor(value * steps + bayerValue) / steps;
          value.rgb = GammaToLinearSpace(value.rgb);
          return value;
        }

        float GetBayer2x2(float2 pixelPosition)
        {
          const float bayer_matrix_2x2[2][2] = {
            {  0.00,  1.00 },
            {  0.25,  0.75 },
          };
          return bayer_matrix_2x2[pixelPosition.x % 2][pixelPosition.y % 2];
        }

        float GetBayer4x4(float2 pixelPosition)
        {
          const float bayer_matrix_4x4[4][4] = {
            { 0.0,    0.5,    0.125,  0.625 },
            { 0.75,   0.25,   0.875,  0.375 },
            { 0.1875, 0.6875, 0.0625, 0.5625 },
            { 0.9375, 0.4375, 0.8125, 0.3125 },
          };
          return bayer_matrix_4x4[pixelPosition.x % 4][pixelPosition.y % 4];
        }

        float GetBayer8x8(float2 pixelPosition)
        {
          const float bayer_matrix_8x8[8][8] = {
            { 0.000, 0.500, 0.125, 0.625, 0.03125, 0.53125, 0.15625, 0.65625 },
            { 0.750, 0.250, 0.875, 0.375, 0.78125, 0.28125, 0.90625, 0.40625 },
            { 0.1875, 0.6875, 0.0625, 0.5625, 0.21875, 0.71875, 0.09375, 0.59375 },
            { 0.9375, 0.4375, 0.8125, 0.3125, 0.96875, 0.46875, 0.84375, 0.34375 },
            { 0.015625, 0.515625, 0.140625, 0.640625, 0.046875, 0.546875, 0.171875, 0.671875 },
            { 0.765625, 0.265625, 0.890625, 0.390625, 0.796875, 0.296875, 0.921875, 0.421875 },
            { 0.203125, 0.703125, 0.078125, 0.578125, 0.234375, 0.734375, 0.109375, 0.609375 },
            { 0.953125, 0.453125, 0.828125, 0.328125, 0.984375, 0.484375, 0.859375, 0.359375 },
          };
          return bayer_matrix_8x8[pixelPosition.x % 8][pixelPosition.y % 8];
        }

        float4 Bayer4x4_float(float4 PixelPosition, float4 Color, float Steps, float RenderScale)
        {
          PixelPosition.xy *= _ScreenParams.xy * RenderScale;

          float bayerValue = GetBayer4x4(PixelPosition.xy);
          float4 outputBayer = step(bayerValue, Color);

          Color = Posterize(Color, Steps, bayerValue);
          return Color;
        }
        
        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
            OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
            OUT.screenPosition = GetVertexPositionInputs(OUT.positionHCS).positionNDC;
            return OUT;
        }

        half4 frag(Varyings IN) : SV_Target
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

            float4 bayer_col = Bayer4x4_float(IN.screenPosition, color, _Steps, _RenderScale);
            
            return bayer_col;
        }
    
    ENDHLSL
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
            ENDHLSL
        }
    }
}
