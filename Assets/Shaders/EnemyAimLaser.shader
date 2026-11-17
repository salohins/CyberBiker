Shader "CyberBiker/Enemy Aim Laser" {
    Properties {
        _Intensity("Beam brightness",Range(.1,1.25))=.8
        _PlayerEndFade("Player end fade fraction",Range(0,.75))=.25
        [HideInInspector] _VisibleFraction("Visible fraction of full beam",Float)=1
    }
    SubShader {
        Tags { "Queue"="Transparent+35" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Normal alpha blending preserves red at crossings instead of adding
        // white light until both beams become the same overexposed color.
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _Intensity,_PlayerEndFade,_VisibleFraction;
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_FOG_COORDS(1) };
            v2f vert(appdata_full v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;}
            float4 frag(v2f i):SV_Target{
                float edge=abs(i.uv.y*2-1);
                float coverage=1-smoothstep(.75,1,edge);
                // Keep the beam below HDR emission levels before scene exposure.
                float3 rgb=saturate(i.color.rgb)*(.5*clamp(_Intensity,.1,1.25));
                // UV runs muzzle -> visible endpoint. Keep the fade anchored
                // to the player even if a nearer car clips the visible beam.
                float fromPlayer=1-i.uv.x*_VisibleFraction;
                float fade=_PlayerEndFade>.0001 ? smoothstep(0,_PlayerEndFade,fromPlayer) : 1;
                float4 color=float4(rgb,i.color.a*coverage*fade);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,color,float4(0,0,0,0));return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
