Shader "CyberBiker/Collision Scratch" {
    Properties { _Intensity ("Spark brightness",Range(0,4))=1.8 }
    SubShader {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _Intensity;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_FOG_COORDS(1)};
            v2f vert(appdata_full v) {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;o.color=v.color;
                UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            float4 frag(v2f i):SV_Target {
                float2 p=abs(i.uv-.5)*2;
                float sides=1-smoothstep(.3,1,p.x);
                float tips=1-smoothstep(.45,1,p.y);
                // Thin warm strokes with bounded alpha blending: overlapping
                // sparks keep their shapes instead of adding into a white flash.
                float3 tint=lerp(i.color.rgb,float3(1,.82,.4),sides*.25);
                float4 color=float4(saturate(tint*_Intensity),i.color.a*sides*tips);
                UNITY_APPLY_FOG(i.fogCoord,color);return color;
            }
            ENDCG
        }
    }
}
