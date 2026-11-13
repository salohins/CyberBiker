Shader "CyberBiker/Enemy Arrival Fade" {
    Properties {
        _MainTex("Baked texture",2D)="white" {}
        _Color("Tint",Color)=(1,1,1,1)
        _EmissionMap("Emission",2D)="white" {}
        _EmissionColor("Emission tint",Color)=(0,0,0,0)
        _ReflectionCubeMap("Reflection",Cube)="" {}
        _ReflectionColor("Reflection tint",Color)=(1,1,1,1)
        _ReflectionMaskOffset("Reflection mask",Float)=0
        _ReflectionFresnelBias("Reflection bias",Float)=0
        _RimColor("Rim",Color)=(0,0,0,0)
        _RimBias("Rim bias",Float)=0
        _ArrivalVisibility("Visibility",Range(0,1))=0
        _ArrivalReflection("Use reflection",Float)=0
        _ArrivalRim("Use rim",Float)=0
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Blend One OneMinusSrcAlpha
        Cull Back ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex,_EmissionMap;samplerCUBE _ReflectionCubeMap;
            float4 _MainTex_ST,_Color,_EmissionColor,_ReflectionColor,_RimColor;
            float _ArrivalVisibility,_ArrivalReflection,_ArrivalRim,_ReflectionMaskOffset,_ReflectionFresnelBias,_RimBias;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float3 normal:NORMAL;};
            struct v2f {float4 position:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;float3 view:TEXCOORD2;UNITY_FOG_COORDS(3)};
            v2f vert(appdata v) {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);
                o.normal=UnityObjectToWorldNormal(v.normal);o.view=WorldSpaceViewDir(v.vertex);UNITY_TRANSFER_FOG(o,o.position);return o;
            }
            half4 frag(v2f i):SV_Target {
                half4 texel=tex2D(_MainTex,i.uv);half3 normal=normalize(i.normal),view=normalize(i.view);
                half edge=1-saturate(dot(normal,view));half3 color=texel.rgb*_Color.rgb;
                color+=tex2D(_EmissionMap,i.uv).rgb*_EmissionColor.rgb;
                color+=_ArrivalReflection*texCUBE(_ReflectionCubeMap,reflect(-view,normal)).rgb*_ReflectionColor.rgb*saturate(texel.a+_ReflectionMaskOffset)*saturate(edge+_ReflectionFresnelBias);
                color+=_ArrivalRim*_RimColor.rgb*saturate(edge+_RimBias);
                half alpha=saturate(_ArrivalVisibility)*_Color.a;
                half4 result=half4(color*alpha,alpha);UNITY_APPLY_FOG_COLOR(i.fogCoord,result,half4(0,0,0,0));return result;
            }
            ENDCG
        }
    }
}
