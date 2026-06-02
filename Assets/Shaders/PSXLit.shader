Shader "Custom/PSXLit"
{
    Properties
    {
        [MainColor] _DiffuseColor("Diffuse Color", Color) = (1,1,1,1)
        
        _Tiled ("Tiled Texture", Range(0, 1)) = 0.0
        _TexScale ("Texture Scale", Float) = 1.0
        
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _SnapIntensity ("Snap Intensity", Range(0.0000,0.1)) = 0.0066
        _AffineOn ("Affine Mapping On", Range(0,1)) = 1
        
        _SpecularExponent("Specular Exponent", Float) = 80
        _k ("Ambient, Diffuse, Specular", Vector) = (0.1,0.1,0.5)
        
        [HDR] _EmissionColor("Emission Color", Color) = (1,1,1,1)
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}
        
        _WaveSize ("Wave Size", Float) = 0
        _WaveLength ("Wave Length", Float) = 1
        _Frequency ("Frequency", Float) = 1
        _MinY ("Min Y", Float) = 0
        _MaxY ("Max Y", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            AlphaToMask On
            
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR0;
                float4 shadowCoords : TEXCOORD2;
                float3 normal : NORMAL;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Tiled;
                float _TexScale;
                float4 _BaseMap_ST;
                half4 _EmissionColor;
                float4 _EmissionMap_ST;
                float _SnapIntensity;
                float _AffineOn;
                half4 _DiffuseColor;
                float _SpecularExponent;
                float4 _k;
            
                float _WaveSize;
                float _WaveLength;
                float _Frequency;
                float _MinY;
                float _MaxY;
            CBUFFER_END

            float2 snapToGrid(float2 value, float snapValue)
            {
                return floor(value / snapValue + 0.5) * snapValue;
            }
            
            float3 LightingFunc(float3 normalWS, Light light, half3 n, float shadow)
            {
                half3 l = normalize(light.direction);
                half3 r = 2.0 * dot(n, l) * n - l;
                half3 v = normalize(_WorldSpaceCameraPos - normalWS);

                float Ia = _k.x;
                float Id = _k.y * saturate(dot(n, l) * shadow);
                float Is = _k.z * pow(saturate(dot(r, v)), _SpecularExponent);

                float3 ambient  = Ia * _DiffuseColor.rgb;
                float3 diffuse  = Id * _DiffuseColor.rgb * light.color;
                float3 specular = Is * light.color;

                return ambient + diffuse + specular;
            }
            
            half3 LotusLambert(half3 lightColor, half3 lightDir, half3 normal, float shadow)
            {
                half NdotL = saturate(dot(normal, lightDir) * shadow);
                return lightColor * NdotL;
            }
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float4 worldPosition = mul(UNITY_MATRIX_MV, IN.positionOS);
                
                float waveAmplitude = _WaveSize * clamp((IN.positionOS.y - _MinY) / (_MaxY - _MinY), 0, 1);
                IN.positionOS.x += sin((IN.positionOS.y + _Time * _Frequency) / _WaveLength) * waveAmplitude;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normal = IN.normal;
                
                
                InputData inputData = (InputData)0;
                inputData.positionWS = OUT.positionWS;
                inputData.normalWS = OUT.normal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(OUT.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(OUT.positionHCS);

                // vert snapping
                if (_SnapIntensity != 0)
                {
                    float2 screenPos = OUT.positionHCS.xy / OUT.positionHCS.w;
                    screenPos = snapToGrid(screenPos, _SnapIntensity);
                    OUT.positionHCS.xy = screenPos * OUT.positionHCS.w;
                }

                // affine tex mapping
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                if (_AffineOn)
                    OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap) * OUT.positionHCS.w;

                // gouraud (vertex) shading
                Light light = GetMainLight();
                half3 n = TransformObjectToWorldNormal(IN.normal);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normal, IN.tangentOS);

                float shadow_amt = MainLightRealtimeShadow(GetShadowCoord(vertexInput));
                OUT.color = half4(LightingFunc(worldPosition, light, n, shadow_amt), 1.0);

                // additional lights
                #if defined(_ADDITIONAL_LIGHTS)
                
                #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint lightIndex = 0; lightIndex < MAX_VISIBLE_LIGHTS; lightIndex++)
                {
                    Light additionalLight = GetAdditionalLight(lightIndex, vertexInput.positionWS);
                    shadow_amt = AdditionalLightRealtimeShadow(lightIndex, vertexInput.positionWS, additionalLight.direction);
                    {
                        half3 lightColor = additionalLight.color * additionalLight.distanceAttenuation;
                        OUT.color += half4(LotusLambert(lightColor, additionalLight.direction, normalInput.normalWS, shadow_amt), 1.0);
                    }
                }
                #endif
                
                uint lightsCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(lightsCount)
                    Light additionalLight = GetAdditionalLight(lightIndex, vertexInput.positionWS);
                    shadow_amt = AdditionalLightRealtimeShadow(lightIndex, vertexInput.positionWS, additionalLight.direction);
                    {
                        half3 lightColor = additionalLight.color * additionalLight.distanceAttenuation;
                        OUT.color += half4(LotusLambert(lightColor, additionalLight.direction, normalInput.normalWS, shadow_amt), 1.0);
                    }
                LIGHT_LOOP_END

                #endif
                
                OUT.shadowCoords = GetShadowCoord(vertexInput);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * IN.color;
                if (_AffineOn)
                    color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv / IN.positionHCS.w) * IN.color;
                
                if (_Tiled)
                {
                    float3 uv = IN.positionWS * _TexScale;
                    float3 Node_Blend = pow(abs(IN.normal), 1);
                    Node_Blend /= dot(Node_Blend, 1.0);
                    
                    float4 Node_X = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv.zy);
                    float4 Node_Y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv.xz);
                    float4 Node_Z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv.xy);
                    
                    if (_AffineOn)
                    {
                        Node_X = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv.zy / IN.positionHCS.w);
                        Node_Y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv.xz / IN.positionHCS.w);
                        Node_Z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv.xy / IN.positionHCS.w);
                    }

                    return Node_X * Node_Blend.x + Node_Y * Node_Blend.y + Node_Z * Node_Blend.z;
                }
                    
                half4 emission_col = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv) * _EmissionColor;
                color += emission_col;

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings  { float4 positionHCS : SV_POSITION; };

            DepthVaryings DepthVert(DepthAttributes IN)
            {
                DepthVaryings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(DepthVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        //UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVaryings  { float4 positionHCS : SV_POSITION; };

            ShadowVaryings ShadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _MainLightPosition.xyz));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, posCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, posCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionHCS = posCS;
                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target { return 0; }
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

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Tiled;
                float _TexScale;
                float4 _BaseMap_ST;
                half4 _EmissionColor;
                float4 _EmissionMap_ST;
                float _SnapIntensity;
                float _AffineOn;
                half4 _DiffuseColor;
                float _SpecularExponent;
                float4 _k;
            
                float _WaveSize;
                float _WaveLength;
                float _Frequency;
                float _MinY;
                float _MaxY;
            CBUFFER_END

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

            float2 snapToGrid(float2 value, float snapValue)
            {
                return floor(value / snapValue + 0.5) * snapValue;
            }

            DNVaryings DepthNormalsVert(DNAttributes IN)
            {
                DNVaryings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                float2 screenPos = OUT.positionHCS.xy / OUT.positionHCS.w;
                screenPos = snapToGrid(screenPos, _SnapIntensity);
                OUT.positionHCS.xy = screenPos * OUT.positionHCS.w;

                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
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