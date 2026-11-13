Shader "CyberBiker/Nitro Ignition" {
    Properties {
        _MainTex("Cartoon FX texture",2D)="white"{}
        _Mode("Shape: flash, ring, texture",Float)=0
        _Glow("Glow",Float)=1
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha One
        Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Mode,_Glow;
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            half4 frag(v2f i):SV_Target {
                float2 p=i.uv*2-1;
                float r=length(p),shape;
                if(_Mode<.5)shape=pow(saturate(1-r),3);
                else if(_Mode<1.5)shape=1-smoothstep(.025,.12,abs(r-.72));
                else shape=tex2D(_MainTex,i.uv).r;
                return half4(i.color.rgb*_Glow,i.color.a*shape);
            }
            ENDCG
        }
    }
}
