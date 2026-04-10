Shader "Unlit/Radar Fade"
{
    Properties
    {
        _Color ("Dot Color", Color) = (0, 1, 0, 1)
        _FadeSpeed ("Fade Speed", Float) = 1.0
        _Repeating ("Repeating", Range(0.0, 1.0)) = 0
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
            float _Repeating;

            float impulse( float k, float x ){
                float h = k*x;
                return h *exp(1.0-h);
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
                //col = fixed4(0.0, _SinTime.w + 0.5, 0.0, 1.0);
                if (_Repeating)
                    _Time %= _FadeSpeed*2.5;
                    
                col = fixed4(_Color.rgb, _FadeSpeed * _Time.w*exp(1.0-_FadeSpeed * _Time.w));
                return col;
            }
            ENDCG
        }
    }
}
