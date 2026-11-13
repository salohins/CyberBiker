Shader "CyberBiker/Launch Wind Particle" {
    Properties {_Brightness("Brightness",Float)=3}
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha One
        Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Brightness;
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            half4 frag(v2f i):SV_Target {
                float across=abs(i.uv.x*2-1);
                float feather=pow(saturate(1-across),1.4);
                float taper=pow(saturate(sin(i.uv.y*3.141593)),.8);
                float core=pow(saturate(1-across),8);
                half3 color=i.color.rgb*_Brightness*(.85+core*.35);
                return half4(color,i.color.a*feather*taper);
            }
            ENDCG
        }
    }
}
