Shader "Hidden/CyberBiker/Launch Screen" {
    Properties {_MainTex("Camera",2D)="white"{}}
    SubShader {
        Cull Off ZWrite Off ZTest Always
        Pass {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Launch, _GlowColor;
            half4 frag(v2f_img i):SV_Target {
                half4 source=tex2D(_MainTex,i.uv);
                float2 edge=min(i.uv,1-i.uv);
                float border=exp(-min(edge.x,edge.y)/max(.005,_Launch.z));
                float corner=pow(saturate(length((i.uv-.5)*1.4)),3);
                // Keep the roadway/target centre clear; no opaque border or HUD tint.
                float glow=saturate(border*.7+corner*.22)*_Launch.x*(1+_Launch.y*.45);
                half3 color=source.rgb+_GlowColor.rgb*glow;
                return half4(color,source.a);
            }
            ENDCG
        }
    }
}
