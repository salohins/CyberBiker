Shader "CyberBiker/Sword Ribbon" {
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
                half feather=smoothstep(0,.12,i.uv.y)*smoothstep(0,.08,1-i.uv.y);
                half core=pow(saturate(i.uv.y),8);
                return half4(lerp(i.color.rgb,half3(3,3,3),core*.6),i.color.a*feather);
            }
            ENDCG
        }
    }
}
