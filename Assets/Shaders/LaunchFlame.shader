Shader "CyberBiker/Launch Flame" {
    Properties {
        _MainTex("Cartoon FX flame shape",2D)="white"{}
        _NoiseTex("Fire distortion",2D)="gray"{}
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_NoiseTex;
            float _FlameClock;
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            half4 frag(v2f i):SV_Target {
                float noise=tex2D(_NoiseTex,float2(i.uv.x*2-_FlameClock*5,i.uv.y*2+_FlameClock*.8)).r;
                float2 uv=i.uv;uv.y+=(noise-.5)*.16*uv.x;
                float shape=tex2D(_MainTex,uv).r;
                float mask=smoothstep(.15,.4,shape-noise*.3);
                float core=pow(saturate(1-abs(uv.y*2-1)),3)*(1-uv.x*.7);
                half3 fire=lerp(half3(.9,.08,.005),half3(1.05,.68,.12),core);
                fire=lerp(half3(.06,.3,1),fire,smoothstep(0,.2,uv.x));
                float alpha=mask*i.color.a*smoothstep(0,.05,uv.x)*smoothstep(0,.1,1-uv.x);
                return half4(fire,alpha*.9);
            }
            ENDCG
        }
    }
}
