Shader "Custom/PortalMask"
{
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" }

		// Pass 0 — write stencil=1 into the portal quad footprint.
		// ZClip Off disables near-plane clipping so the stencil mask is always
		// written even when the camera is right on top of the portal surface.
		// This eliminates the need for an exit position offset.
		Pass
		{
			Name "StencilWrite"
			Cull Off
			ZWrite Off
			ZTest LEqual
			ZClip Off
			ColorMask 0
			Stencil { Ref 1  Comp Always  Pass Replace }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			struct Attributes { float4 posOS : POSITION; };
			struct Varyings   { float4 posHCS : SV_POSITION; };
			Varyings vert(Attributes i) { Varyings o; o.posHCS = TransformObjectToHClip(i.posOS.xyz); return o; }
			half4    frag(Varyings  i) : SV_Target { return 0; }
			ENDHLSL
		}

		// Pass 1 — reset stencil back to 0 after portal is fully drawn.
		Pass
		{
			Name "StencilClear"
			Cull Off
			ZWrite Off
			ZTest Always
			ZClip Off
			ColorMask 0
			Stencil { Ref 0  Comp Always  Pass Replace }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			struct Attributes { float4 posOS : POSITION; };
			struct Varyings   { float4 posHCS : SV_POSITION; };
			Varyings vert(Attributes i) { Varyings o; o.posHCS = TransformObjectToHClip(i.posOS.xyz); return o; }
			half4    frag(Varyings  i) : SV_Target { return 0; }
			ENDHLSL
		}
	}
}