Shader "CyberBiker/Helmet Danger Sense" {
    Properties { _Intensity("Streak brightness",Range(.1,3))=1.6 }
    SubShader {
        Tags { "Queue"="Transparent+36" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _Intensity;
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_FOG_COORDS(1) };
            v2f vert(appdata_full v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;}
            float4 frag(v2f i):SV_Target{
                float coverage=1-smoothstep(.65,1,abs(i.uv.y*2-1));
                // Modest HDR green for helmet readability, independent of the
                // deliberately low-brightness projectile trajectory shader.
                float4 color=float4(saturate(i.color.rgb)*clamp(_Intensity,.1,3),i.color.a*coverage);
                UNITY_APPLY_FOG_COLOR(i.fogCoord,color,float4(0,0,0,0));return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
