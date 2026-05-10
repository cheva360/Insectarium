Shader "Custom/Radar Fade"
{
    Properties
    {
        _Color ("Dot Color", Color) = (0.5443218, 0.8301887, 0.5443218, 1)
        _EmissionColor ("Emission Color", Color) = (0.5443218, 0.8301887, 0.5443218, 1)
        _EmissionIntensity ("Emission Intensity", Float) = 1.0
        _FadeSpeed ("Fade Speed", Float) = 1.0
        _ElapsedTime ("Elapsed Time", Float) = 0.0
        _Repeating ("Repeating", Range(0.0, 1.0)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            fixed4 _Color;
            fixed4 _EmissionColor;
            float _EmissionIntensity;
            float _FadeSpeed;
            float _ElapsedTime;
            float _Repeating;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Repeating
                    ? fmod(_ElapsedTime, 1.0 / _FadeSpeed)
                    : _ElapsedTime;

                float normalizedT = saturate(t * _FadeSpeed);

                // Hold alpha for ~75% of duration, then fade out sharply
                float alpha = 1.0 - pow(normalizedT, 4.0);

                fixed3 finalColor = _Color.rgb + _EmissionColor.rgb * _EmissionIntensity * alpha;
                return fixed4(finalColor, alpha * _Color.a);
            }
            ENDCG
        }
    }
}