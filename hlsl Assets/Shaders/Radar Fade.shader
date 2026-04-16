Shader "Unlit/Radar Fade"
{
    Properties
    {
        _Color ("Dot Color", Color) = (0, 1, 0, 1)
        _FadeSpeed ("Fade Speed", Float) = 1.0
        _ElapsedTime ("Elapsed Time", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue" = "Transparent" }
        LOD 100
        
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _FadeSpeed;
            float _ElapsedTime;
            }

            float plot(float2 st, float pct){
              return  smoothstep( pct-0.02, pct, st.y) -
                      smoothstep( pct, pct+0.02, st.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = _Color;
                
                // linear
                // _ElapsedTime * _FadeSpeed gives 0-1
                float fadeAlpha = 1.0 - (_ElapsedTime * _FadeSpeed);
                fadeAlpha = saturate(fadeAlpha); // Clamp to 0-1 range
                
                col = fixed4(_Color.rgb, fadeAlpha * _Color.a);
                return col;
            }
            ENDCG
        }
    }
}