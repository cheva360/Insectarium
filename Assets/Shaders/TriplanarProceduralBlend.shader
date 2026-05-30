Shader "Custom/TriplanarProceduralBlend"
{
    Properties
    {
        _TextureA ("Texture A", 2D) = "white" {}
        _TextureB ("Texture B", 2D) = "white" {}
        [Toggle] _UseFoliage ("Use Foliage", Float) = 0
        _FoliageTexture ("Foliage Texture", 2D) = "white" {}
        _FoliageMaskPosX ("Foliage Mask +X", 2D) = "black" {}
        _FoliageMaskNegX ("Foliage Mask -X", 2D) = "black" {}
        _FoliageMaskPosY ("Foliage Mask +Y", 2D) = "black" {}
        _FoliageMaskNegY ("Foliage Mask -Y", 2D) = "black" {}
        _FoliageMaskPosZ ("Foliage Mask +Z", 2D) = "black" {}
        _FoliageMaskNegZ ("Foliage Mask -Z", 2D) = "black" {}

        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _FoliageColor ("Foliage Color", Color) = (1,1,1,1)

        _TriplanarScale ("Triplanar Scale", Float) = 1.0
        _TriplanarSharpness ("Triplanar Sharpness", Range(1, 8)) = 2.0
        _FoliageTriplanarScale ("Foliage Triplanar Scale", Float) = 1.0

        _TopNoiseScale ("Top Noise Scale", Float) = 1.0
        _BottomNoiseScale ("Bottom Noise Scale", Float) = 1.0

        _TopBlendHeight ("Top Weathering Height", Float) = 3.0
        _TopBlendSoftness ("Top Weathering Softness", Float) = 0.5
        _TopEdgeBreakup ("Top Edge Breakup", Float) = 0.35

        _BottomBlendHeight ("Bottom Weathering Height", Float) = 0.0
        _BottomBlendSoftness ("Bottom Weathering Softness", Float) = 0.5
        _BottomEdgeBreakup ("Bottom Edge Breakup", Float) = 0.35

        _StreakScale ("Top Streak Scale", Float) = 1.0
        _StreakLength ("Top Streak Length", Float) = 2.0
        _StreakBias ("Top Streak Bias", Range(0.2, 2.0)) = 0.65

        _FoliageSlopeInfluence ("Foliage Slope Influence", Range(0,1)) = 1.0
        _FoliageSlopeMin ("Foliage Slope Min", Range(0,1)) = 0.2
        _FoliageSlopePower ("Foliage Slope Power", Range(0.1, 8.0)) = 2.0
        _FoliageNoiseScale ("Foliage Noise Scale", Float) = 2.0
        _FoliageNoiseStrength ("Foliage Noise Strength", Range(0,1)) = 0.25
        _FoliageSoftness ("Foliage Softness", Range(0.01, 1.0)) = 0.2
        _FoliageTransparency ("Foliage Transparency", Range(0,1)) = 1.0

        _FoliageMaskBoundsMin ("Foliage Mask Bounds Min", Vector) = (0,0,0,0)
        _FoliageMaskBoundsSize ("Foliage Mask Bounds Size", Vector) = (1,1,1,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_TextureA);
            SAMPLER(sampler_TextureA);

            TEXTURE2D(_TextureB);
            SAMPLER(sampler_TextureB);

            TEXTURE2D(_FoliageTexture);
            SAMPLER(sampler_FoliageTexture);

            TEXTURE2D(_FoliageMaskPosX);
            SAMPLER(sampler_FoliageMaskPosX);

            TEXTURE2D(_FoliageMaskNegX);
            SAMPLER(sampler_FoliageMaskNegX);

            TEXTURE2D(_FoliageMaskPosY);
            SAMPLER(sampler_FoliageMaskPosY);

            TEXTURE2D(_FoliageMaskNegY);
            SAMPLER(sampler_FoliageMaskNegY);

            TEXTURE2D(_FoliageMaskPosZ);
            SAMPLER(sampler_FoliageMaskPosZ);

            TEXTURE2D(_FoliageMaskNegZ);
            SAMPLER(sampler_FoliageMaskNegZ);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _FoliageColor;

                float _UseFoliage;
                float _TriplanarScale;
                float _TriplanarSharpness;
                float _FoliageTriplanarScale;

                float _TopNoiseScale;
                float _BottomNoiseScale;

                float _TopBlendHeight;
                float _TopBlendSoftness;
                float _TopEdgeBreakup;

                float _BottomBlendHeight;
                float _BottomBlendSoftness;
                float _BottomEdgeBreakup;

                float _StreakScale;
                float _StreakLength;
                float _StreakBias;

                float _FoliageSlopeInfluence;
                float _FoliageSlopeMin;
                float _FoliageSlopePower;
                float _FoliageNoiseScale;
                float _FoliageNoiseStrength;
                float _FoliageSoftness;
                float _FoliageTransparency;

                float4 _FoliageMaskBoundsMin;
                float4 _FoliageMaskBoundsSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
                float3 normalOS : TEXCOORD5;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(Hash(i + float2(0, 0)), Hash(i + float2(1, 0)), u.x),
                    lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x),
                    u.y
                );
            }

            float3 GetTriplanarWeights(float3 normalValue, float sharpness)
            {
                float3 n = abs(normalize(normalValue));
                float3 weights = pow(max(n, 0.0001), sharpness);
                return weights / max(weights.x + weights.y + weights.z, 0.0001);
            }

            float4 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 absWorldPos, float3 worldNormal, float scale, float sharpness)
            {
                float3 weights = GetTriplanarWeights(worldNormal, sharpness);

                float2 uvX = absWorldPos.zy * scale;
                float2 uvY = absWorldPos.xz * scale;
                float2 uvZ = absWorldPos.xy * scale;

                float4 xSample = SAMPLE_TEXTURE2D(tex, samp, uvX);
                float4 ySample = SAMPLE_TEXTURE2D(tex, samp, uvY);
                float4 zSample = SAMPLE_TEXTURE2D(tex, samp, uvZ);

                return xSample * weights.x + ySample * weights.y + zSample * weights.z;
            }

            float SampleTriplanarMask(float3 positionOS, float3 normalOS)
            {
                float3 boundsMin = _FoliageMaskBoundsMin.xyz;
                float3 boundsSize = max(_FoliageMaskBoundsSize.xyz, float3(0.0001, 0.0001, 0.0001));
                float3 weights = GetTriplanarWeights(normalOS, _TriplanarSharpness);

                float2 uvX = saturate((positionOS.zy - boundsMin.zy) / boundsSize.zy);
                float2 uvY = saturate((positionOS.xz - boundsMin.xz) / boundsSize.xz);
                float2 uvZ = saturate((positionOS.xy - boundsMin.xy) / boundsSize.xy);

                float maskX = normalOS.x >= 0.0
                    ? SAMPLE_TEXTURE2D(_FoliageMaskPosX, sampler_FoliageMaskPosX, uvX).r
                    : SAMPLE_TEXTURE2D(_FoliageMaskNegX, sampler_FoliageMaskNegX, uvX).r;

                float maskY = normalOS.y >= 0.0
                    ? SAMPLE_TEXTURE2D(_FoliageMaskPosY, sampler_FoliageMaskPosY, uvY).r
                    : SAMPLE_TEXTURE2D(_FoliageMaskNegY, sampler_FoliageMaskNegY, uvY).r;

                float maskZ = normalOS.z >= 0.0
                    ? SAMPLE_TEXTURE2D(_FoliageMaskPosZ, sampler_FoliageMaskPosZ, uvZ).r
                    : SAMPLE_TEXTURE2D(_FoliageMaskNegZ, sampler_FoliageMaskNegZ, uvZ).r;

                return maskX * weights.x + maskY * weights.y + maskZ * weights.z;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs norm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = normalize(norm.normalWS);
                OUT.shadowCoord = GetShadowCoord(pos);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalOS = normalize(IN.normalOS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 worldPos = IN.positionWS;
                float3 absWorldPos = abs(worldPos);
                float3 worldNormal = normalize(IN.normalWS);
                float3 objectNormal = normalize(IN.normalOS);

                float4 baseA = SampleTriplanar(
                    TEXTURE2D_ARGS(_TextureA, sampler_TextureA),
                    absWorldPos,
                    worldNormal,
                    _TriplanarScale,
                    _TriplanarSharpness);

                float4 baseB = SampleTriplanar(
                    TEXTURE2D_ARGS(_TextureB, sampler_TextureB),
                    absWorldPos,
                    worldNormal,
                    _TriplanarScale,
                    _TriplanarSharpness);

                float2 topEdgePos = worldPos.xz * _TopNoiseScale;
                float2 bottomEdgePos = worldPos.xz * _BottomNoiseScale;

                float topNoise = (ValueNoise(topEdgePos + float2(11.3, 7.9)) - 0.5) * _TopEdgeBreakup;
                float bottomNoise = (ValueNoise(bottomEdgePos + float2(27.4, 63.1)) - 0.5) * _BottomEdgeBreakup;

                float streakSeed = ValueNoise(worldPos.xz * _StreakScale + float2(73.1, 19.7));
                streakSeed = pow(saturate(streakSeed), _StreakBias);
                float streakDrop = streakSeed * _StreakLength;

                float topThreshold = _TopBlendHeight - streakDrop + topNoise;
                float topMask = smoothstep(
                    topThreshold - max(_TopBlendSoftness, 0.0001),
                    topThreshold + max(_TopBlendSoftness, 0.0001),
                    worldPos.y);

                float bottomThreshold = _BottomBlendHeight + bottomNoise;
                float bottomMask = 1.0 - smoothstep(
                    bottomThreshold - max(_BottomBlendSoftness, 0.0001),
                    bottomThreshold + max(_BottomBlendSoftness, 0.0001),
                    worldPos.y);

                float weatheringMask = saturate(max(topMask, bottomMask));
                half3 baseAlbedo = lerp(baseA.rgb, baseB.rgb, weatheringMask) * _BaseColor.rgb;

                half3 albedo = baseAlbedo;

                if (_UseFoliage > 0.5)
                {
                    float4 foliageSample = SampleTriplanar(
                        TEXTURE2D_ARGS(_FoliageTexture, sampler_FoliageTexture),
                        absWorldPos,
                        worldNormal,
                        _FoliageTriplanarScale,
                        _TriplanarSharpness);

                    float foliagePaint = SampleTriplanarMask(IN.positionOS, objectNormal);

                    float upMask = smoothstep(_FoliageSlopeMin, 1.0, saturate(worldNormal.y));
                    upMask = pow(upMask, _FoliageSlopePower);

                    float foliageSlopeMask = lerp(1.0, upMask, _FoliageSlopeInfluence);

                    float foliageNoise = (ValueNoise(worldPos.xz * _FoliageNoiseScale + float2(91.7, 24.3)) - 0.5) * 2.0;
                    float foliageBreakup = saturate(1.0 + foliageNoise * _FoliageNoiseStrength);

                    float foliageAlpha = saturate(foliageSample.a * _FoliageColor.a);
                    float foliageSoftStep = smoothstep(0.0, max(_FoliageSoftness, 0.0001), foliagePaint);
                    float foliageSoftPaint = lerp(foliagePaint, foliageSoftStep, saturate(_FoliageSoftness * 0.5));
                    float foliageRawMask = saturate(foliageSoftPaint * foliageSlopeMask * foliageBreakup);
                    float foliageMask = saturate(foliageRawMask * foliageAlpha * _FoliageTransparency);

                    half3 foliageAlbedo = foliageSample.rgb * _FoliageColor.rgb;
                    albedo = lerp(baseAlbedo, foliageAlbedo, foliageMask);
                }

                half3 ambient = SampleSH(worldNormal);

                Light mainLight = GetMainLight(IN.shadowCoord);
                half mainNdotL = saturate(dot(worldNormal, mainLight.direction));
                half3 lighting = ambient + (mainLight.color * mainNdotL * mainLight.distanceAttenuation * mainLight.shadowAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light light = GetAdditionalLight(i, worldPos);
                    half ndotl = saturate(dot(worldNormal, light.direction));
                    lighting += light.color * ndotl * light.distanceAttenuation * light.shadowAttenuation;
                }
                #endif

                half3 finalColor = albedo * lighting;
                finalColor = MixFog(finalColor, IN.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct DNVaryings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
            };

            DNVaryings DepthNormalsVert(DNAttributes IN)
            {
                DNVaryings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float4 DepthNormalsFrag(DNVaryings IN) : SV_Target
            {
                return float4(normalize(IN.normalWS) * 0.5 + 0.5, 0.0);
            }
            ENDHLSL
        }
    }
}