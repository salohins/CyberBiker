Shader "CyberBiker/Sword Impact" {
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            half4 frag(v2f i):SV_Target {
                float2 p=i.uv*2-1;float edge=saturate(1-length(p));
                float crossLine=max(saturate(1-abs(p.x+p.y)*10),saturate(1-abs(p.x-p.y)*10));
                float glow=saturate(pow(edge,7)+pow(crossLine,3)*edge*.85);
                return half4(i.color.rgb,i.color.a*glow);
            }
            ENDCG
        }
    }
}
