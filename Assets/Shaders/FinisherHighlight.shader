Shader "CyberBiker/Finisher Highlight" {
    Properties {
        _HighlightColor ("Finisher color",Color)=(.035,.18,.85,1)
        _Intensity ("Highlight strength",Range(0,1))=.9
        _GlowStrength ("Rim glow radiance",Range(0,1.5))=1.1
        _Pulse ("Pulse",Float)=1
        _Phase ("Scan phase",Float)=0
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    float4 _HighlightColor;
    float _Pulse,_Phase,_Intensity,_GlowStrength;
    half3 highlightColor() {
        half3 color=max(_HighlightColor.rgb,0);
        half peak=max(color.r,max(color.g,color.b));
        // Bound the overlay even if an existing material still supplies HDR color.
        return color*min(1,.8/max(peak,.0001));
    }
    struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;};
    struct v2f {float4 position:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;};
    v2f vertexBase(appdata v) {
        v2f o;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);
        o.position=UnityWorldToClipPos(o.world);return o;
    }
    v2f vertexShell(appdata v) {
        v2f o=vertexBase(v);o.position=UnityWorldToClipPos(o.world+normalize(o.normal)*.035);return o;
    }
    half3 glowRadiance() {
        half3 color=max(_HighlightColor.rgb,0);
        half peak=max(color.r,max(color.g,color.b));
        return color/max(peak,.0001)*clamp(_GlowStrength,0,1.5)*saturate(_Intensity)*_Pulse;
    }
    half4 shellGlow(v2f i):SV_Target{return half4(glowRadiance(),0);}
    half4 rimGlow(v2f i):SV_Target {
        float rim=pow(saturate(1-abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)))),4);
        return half4(glowRadiance()*rim*.35,0);
    }
    half4 surfaceGlow(v2f i):SV_Target {
        float rim=pow(saturate(1-abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)))),2.5);
        float scan=pow(.5+.5*sin(i.world.y*18-_Phase*8),10)*.02;
        return half4(highlightColor(),_Pulse*saturate(_Intensity)*(.2+rim*.5+scan));
    }
    ENDCG
    SubShader {
        Tags {"Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True"}
        ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            Cull Front Blend One One
            CGPROGRAM
            #pragma vertex vertexShell
            #pragma fragment shellGlow
            #pragma target 3.0
            ENDCG
        }
        Pass {
            Cull Back Offset -1,-1
            CGPROGRAM
            #pragma vertex vertexBase
            #pragma fragment surfaceGlow
            #pragma target 3.0
            ENDCG
        }
        Pass {
            Cull Back Offset -1,-1 Blend One One
            CGPROGRAM
            #pragma vertex vertexBase
            #pragma fragment rimGlow
            #pragma target 3.0
            ENDCG
        }
    }
}
