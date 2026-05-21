Shader "Custom/PortalDepthSeal"
{
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" }

		// Writes the portal quad's depth into the depth buffer.
		// No color output. This "seals" the portal so that scene geometry
		// behind the portal plane is correctly occluded by it during the
		// subsequent opaque rendering pass.
		Pass
		{
			Name "DepthSeal"
			Cull Off
			ZWrite On
			ZTest LEqual
			ColorMask 0

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