Shader "CyberBiker/Enemy Speed Trail" {
    Properties {
        _Brightness("Brightness",Float)=1.75
        _RiderBottom("Rider capsule bottom",Vector)=(0,0,0,0)
        _RiderTop("Rider capsule top",Vector)=(0,0,0,0)
        _RiderRadius("Rider clearance",Float)=0
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha One
        Cull Off ZWrite Off ZTest LEqual
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Brightness,_RiderRadius;
            float4 _RiderBottom,_RiderTop;
            struct appdata {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            v2f vert(appdata v) {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.color=v.color;o.uv=v.uv;return o;
            }
            half4 frag(v2f i):SV_Target {
                float across=abs(i.uv.y*2-1);
                float feather=1-smoothstep(.35,1,across);
                float core=pow(saturate(1-across),12);
                float3 axis=_RiderTop.xyz-_RiderBottom.xyz;
                float along=saturate(dot(i.world-_RiderBottom.xyz,axis)/max(dot(axis,axis),.001));
                float distanceToRider=length(i.world-(_RiderBottom.xyz+axis*along));
                float clearance=_RiderRadius>0 ? smoothstep(_RiderRadius,_RiderRadius+.65,distanceToRider) : 1;
                half3 color=i.color.rgb*(.9+core*.25)*_Brightness;
                return half4(color,i.color.a*feather*clearance);
            }
            ENDCG
        }
    }
}
