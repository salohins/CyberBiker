Shader "CyberBiker/Rider View Fade" {
    Properties {
        _Color("Color",Color)=(1,1,1,1)
        _MainTex("Albedo",2D)="white" {}
        _Glossiness("Smoothness",Range(0,1))=.5
        _Metallic("Metallic",Range(0,1))=0
        _RiderOpacity("View Opacity",Range(0,1))=1
        _UseUnlit("Unlit Material",Float)=0
        [HideInInspector] _RiderFrontDepth("Rider Front Depth",2D)="white" {}
        [HideInInspector] _UseFrontSurfaceDepth("Use Front Depth",Float)=0
        _FadeTex("Teleport Fade",2D)="white" {}
        _FadeRate("Teleport Amount",Float)=0
        _ObjectHeight("Object Height",Float)=2
        _ObjectFadeHeight("Fade Height",Float)=.2
        _ObjectBasePos("Base Position",Vector)=(0,0,0,0)
        _RisePower("Rise",Float)=0
        _SpreadPower("Spread",Float)=0
        _TwistPower("Twist",Float)=0
        _ParticleColor("Teleport Color",Color)=(0,0,0,0)
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex,_FadeTex,_RiderFrontDepth;
    float4 _RiderFrontDepth_TexelSize;
    fixed4 _Color,_ParticleColor;
    half _Glossiness,_Metallic,_RiderOpacity,_UseUnlit;
    float _UseFrontSurfaceDepth;
    float _FadeRate,_ObjectHeight,_ObjectFadeHeight,_RisePower,_SpreadPower,_TwistPower;
    float4 _ObjectBasePos;
    void RiderVertex(inout float4 vertex,out float4 packed) {
        packed=0;
        if(_UseUnlit<.5) {
            float4 worldPos=mul(unity_ObjectToWorld,vertex);
            float height=lerp(_ObjectHeight,-_ObjectFadeHeight,_FadeRate);
            float warp=saturate((worldPos.y-(height+_ObjectBasePos.y))/_ObjectFadeHeight);warp*=warp;
            worldPos.y+=warp*_RisePower;
            float2 dir=worldPos.xz-_ObjectBasePos.xz;float rotation=warp*_TwistPower;
            worldPos.x=cos(rotation)*dir.x-sin(rotation)*dir.y;
            worldPos.z=sin(rotation)*dir.x+cos(rotation)*dir.y;
            worldPos.xz*=_SpreadPower*warp+1;worldPos.xz+=_ObjectBasePos.xz;
            vertex=mul(unity_WorldToObject,worldPos);packed.z=warp;
        }
        packed.xy=UnityObjectToClipPos(vertex).xy;
        packed.w=-UnityObjectToViewPos(vertex).z;
    }
    void TeleportClip(float4 packed) {
        if(_UseUnlit<.5) {
            float clipW=lerp(packed.w,1,unity_OrthoParams.w);
            float2 uv=float2(_ScreenParams.x*packed.x/clipW/128,saturate(packed.z-.01));
            clip(tex2D(_FadeTex,uv).r-.1);
        }
    }
    ENDCG
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        ZWrite Off
        // Invoked for every rider submesh by the camera before any transparent
        // colour rendering, so jacket/body layers share one front surface.
        Pass {
            Name "RIDER_DEPTH"
            Tags {"LightMode"="RiderDepthOnly"}
            ZWrite On
            ZTest LEqual
            ColorMask R
            Cull Back
            CGPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma target 3.0
            struct DepthOutput {float4 position:SV_POSITION;float4 packed:TEXCOORD0;};
            DepthOutput depthVert(appdata_full v) {
                DepthOutput o;RiderVertex(v.vertex,o.packed);o.position=UnityObjectToClipPos(v.vertex);return o;
            }
            float depthFrag(DepthOutput i):SV_Target {TeleportClip(i.packed);return i.packed.w;}
            ENDCG
        }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:fade vertex:vert
        #pragma target 3.0
        struct Input {float2 uv_MainTex;float4 packedData;};
        void vert(inout appdata_full v,out Input data) {
            UNITY_INITIALIZE_OUTPUT(Input,data);
            RiderVertex(v.vertex,data.packedData);
        }
        void surf(Input IN,inout SurfaceOutputStandard o) {
            TeleportClip(IN.packedData);
            float clipW=lerp(IN.packedData.w,1,unity_OrthoParams.w);
            float4 screen=ComputeScreenPos(float4(IN.packedData.xy,0,clipW));
            // Allow one depth-texture texel of surface slope. Without this,
            // sampling/precision differences can punch speckles in the skin.
            float2 depthUv=screen.xy/screen.w;
            float slope=abs(ddx(IN.packedData.w))+abs(ddy(IN.packedData.w));
            float pixelScale=max(1,max(_ScreenParams.x*_RiderFrontDepth_TexelSize.x,_ScreenParams.y*_RiderFrontDepth_TexelSize.y));
            if(_UseFrontSurfaceDepth>.5)clip(tex2D(_RiderFrontDepth,depthUv).r-IN.packedData.w+max(.02,slope*(pixelScale+1)));
            fixed4 texel=tex2D(_MainTex,IN.uv_MainTex);fixed4 color=texel*_Color;
            o.Albedo=color.rgb*(1-_UseUnlit);o.Metallic=_Metallic;o.Smoothness=_Glossiness;
            o.Emission=lerp(texel.rgb/2+_ParticleColor.rgb*_ParticleColor.a*saturate(IN.packedData.z*2),color.rgb,_UseUnlit);
            o.Alpha=color.a*_RiderOpacity;
        }
        ENDCG
    }
    FallBack Off
}
