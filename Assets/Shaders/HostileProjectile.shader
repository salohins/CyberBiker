Shader "CyberBiker/Readable Hostile Projectile" {
    Properties { _Tail ("Trail",Float)=0 }
    SubShader {
        Tags { "Queue"="Transparent+40" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float _Tail;
            struct v2f { float4 position:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            v2f vert(appdata_full v) { v2f o;o.position=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;o.color=v.color;return o; }
            fixed4 frag(v2f i):SV_Target {
                float2 p=i.uv*2-1;
                float edge=_Tail>.5 ? abs(p.y) : length(p);
                float aa=max(fwidth(edge),.012);
                float coverage=1-smoothstep(1-aa,1,edge);
                float inside=1-smoothstep(.66,.76,edge);
                float core=1-smoothstep(.12,.36,edge);
                float3 color=lerp(float3(.025,.009,.012),i.color.rgb,inside);
                color=lerp(color,min(float3(1,1,1),i.color.rgb*1.3+float3(.06,.025,.025)),core*.3);
                if(_Tail>.5)color=lerp(float3(.008,.002,.003),i.color.rgb*.35,inside);
                return fixed4(color,coverage*i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
