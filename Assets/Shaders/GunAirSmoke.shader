Shader "CyberBiker/Gun Air and Smoke" {
    Properties { _MainTex("Texture",2D)="white" {} _Air("Air ribbon",Float)=0 }
    SubShader {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float _Air;
            struct v2f{float4 position:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            v2f vert(appdata_full v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target{
                fixed4 sample=tex2D(_MainTex,i.uv);
                float alpha=_Air>.5 ? 1-smoothstep(.1,1,abs(i.uv.y*2-1)) : sample.a*sample.r;
                return fixed4(i.color.rgb*.65,i.color.a*alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
