Shader "Custom/ObjectEffects"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _SnapIntensity ("Snap Intensity", Range(0.001,0.05)) = 0.0066
        _AffineOn ("Affine Mapping On", Range(0,1)) = 1
        
        _DiffuseColor("Diffuse Color", Color) = (1,1,1,1)
        _SpecularExponent("Specular Exponent", Float) = 80
        _k ("Ambient, Diffuse, Specular", Vector) = (0.5,0.5,0.8)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

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
                float2 uv : TEXCOORD0;
                half4 color: COLOR0;
                half3 n : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _SnapIntensity;
                float _AffineOn;
            
                half4 _DiffuseColor;
                float _SpecularExponent;
                float4 _k;
            CBUFFER_END

            float2 snapToGrid(float2 value, float snapValue) {
                return floor(value / snapValue + 0.5) * snapValue;
            }
            
            float3 LightingFunc(float3 normalWS, Light light, half3 n)
            {
                half3 l = normalize(light.direction);          // Light direction in world space
                half3 r = 2.0 * dot(n, l) * n - l;                      // Reflection vector
                half3 v = normalize(_WorldSpaceCameraPos - normalWS);   // View direction

                float Ia = _k.x;                                        // Ambient intensity
                float Id = _k.y * saturate(dot(n, l));                  // Diffuse intensity using Lambert's law
                float Is = _k.z * pow(saturate(dot(r, v)), _SpecularExponent); // Specular intensity

                float3 ambient = Ia * _DiffuseColor.rgb;               // Ambient lighting
                float3 diffuse = Id * _DiffuseColor.rgb * light.color; // Diffuse lighting
                float3 specular = Is * light.color;                // Specular lighting

                float3 finalColor = ambient + diffuse + specular;       // Combine all lighting components
                return finalColor;
            }
            
            float3 LightingFunc(float3 normalWS, Light light)
            {
                half3 lightColor = light.color * light.distanceAttenuation;
                return LightingLambert(lightColor, light.direction, normalWS);
            }
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float4 worldPosition = mul(UNITY_MATRIX_MV, IN.positionOS);
                //float4 worldPosition = float4(TransformObjectToWorldNormal(IN.normal), 1.0);
                
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
               
                // vert snapping
                float2 screenPos = OUT.positionHCS.xy / OUT.positionHCS.w;
                screenPos = snapToGrid(screenPos, _SnapIntensity);
                OUT.positionHCS.xy = screenPos * OUT.positionHCS.w;

                // affine tex mapping
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                if (_AffineOn)
                    OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap) * OUT.positionHCS.w;
                
                // gourraud (vertex) shading
                Light light = GetMainLight();
                half3 n = TransformObjectToWorldNormal(IN.normal);         // Convert normal to world space
                half3 l = normalize(light.direction);          // Light direction in world space
                half3 r = 2.0 * dot(n, l) * n - l;                      // Reflection vector
                half3 v = normalize(_WorldSpaceCameraPos - worldPosition);   // View direction

                float Ia = _k.x;                                        // Ambient intensity
                float Id = _k.y * saturate(dot(n, l));                  // Diffuse intensity using Lambert's law
                float Is = _k.z * pow(saturate(dot(r, v)), _SpecularExponent); // Specular intensity

                float3 ambient = Ia * _DiffuseColor.rgb;               // Ambient lighting
                float3 diffuse = Id * _DiffuseColor.rgb * light.color; // Diffuse lighting
                float3 specular = Is * light.color;                // Specular lighting

                float3 finalColor = ambient + diffuse + specular;       // Combine all lighting components

                OUT.color = half4(finalColor, 1.0);                      // Set the final output colour
                OUT.color = half4(LightingFunc(worldPosition, light, n), 1.0);
                OUT.n = n;
                
                InputData inputData = (InputData)0;
                inputData.positionWS = worldPosition;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(worldPosition);
                //inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normal, IN.tangentOS);
                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
                OUT.color = half4(vertexLight, 1.0);
                
                uint lightsCount = GetAdditionalLightsCount();
    uint meshRenderingLayers = GetMeshRenderingLayer();

    LIGHT_LOOP_BEGIN(lightsCount)
        Light light = GetAdditionalLight(lightIndex, vertexInput.positionWS);

#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
    {
        half3 lightColor = light.color * light.distanceAttenuation;
        OUT.color += half4(LightingLambert(lightColor, light.direction, normalInput.normalWS), 1.0);
    }

    LIGHT_LOOP_END
                uint pixelLightCount = GetAdditionalLightsCount();
                //LIGHT_LOOP_BEGIN(pixelLightCount)
                //    Light additionalLight = GetAdditionalLight(lightIndex, worldPosition);
                //    half3 lightColor = light.color * light.distanceAttenuation;
                //    OUT.color += half4(LightingLambert(light.color, light.direction, n), 1.0);
                //LIGHT_LOOP_END
                
                // additional lights
                // for (int i=0; i < GetAdditionalLightsCount(); ++i)
                // {
                //     Light a_light = GetAdditionalLight(i, GetVertexPositionInputs(IN.positionOS).positionWS);
                //     half3 a_l = normalize(light.direction);          // Light direction in world space
                //     half3 a_r = 2.0 * dot(n, a_l) * n - a_l;                      // Reflection vector
                //     
                //     float a_Id = _k.y * saturate(dot(n, a_l));                  // Diffuse intensity using Lambert's law
                //     float a_Is = _k.z * pow(saturate(dot(a_r, v)), _SpecularExponent); // Specular intensity

                //     float3 a_diffuse = a_Id * _DiffuseColor.rgb * a_light.color; // Diffuse lighting
                //     float3 a_specular = a_Is * a_light.color;                // Specular lighting

                //     float3 addColor = ambient + a_diffuse + a_specular;
                //     OUT.color += half4(addColor, 1.0);
                // }
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * IN.color;
                if (_AffineOn)
                    color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv / IN.positionHCS.w) * IN.color;
                    
                
                return color;
            }
            ENDHLSL
        }
    }
}
