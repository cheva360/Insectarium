Shader "Custom/PortalView"
{
	Properties { _MainTex ("Exit RT", 2D) = "white" {} }
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" }

		// Pass 0 — stencil-masked view. Used when approaching the portal normally.
		// Only draws where stencil == 1 (inside the portal quad footprint).
		Pass
		{
			Name "PortalView"
			Cull Off
			ZWrite Off
			ZTest Always
			Stencil { Ref 1  Comp Equal  Pass Keep }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

			Varyings vert(uint vertexID : SV_VertexID)
			{
				Varyings OUT;
				float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
				OUT.positionHCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
				OUT.uv = uv;
				#if UNITY_UV_STARTS_AT_TOP
				OUT.uv.y = 1.0 - OUT.uv.y;
				#endif
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target
			{
				return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
			}
			ENDHLSL
		}

		// Pass 1 — fullscreen view with no stencil test.
		// Used when the player is straddling the portal plane. At that point the
		// quad is edge-on, rasterizes zero pixels, and the stencil pass draws
		// nothing — so this pass covers the full screen instead.
		Pass
		{
			Name "PortalViewFullscreen"
			Cull Off
			ZWrite Off
			ZTest Always

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

			Varyings vert(uint vertexID : SV_VertexID)
			{
				Varyings OUT;
				float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
				OUT.positionHCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
				OUT.uv = uv;
				#if UNITY_UV_STARTS_AT_TOP
				OUT.uv.y = 1.0 - OUT.uv.y;
				#endif
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target
			{
				return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
			}
			ENDHLSL
		}
	}
}