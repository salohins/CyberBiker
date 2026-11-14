Shader "CyberBiker/Impact Spark Streak" {
    Properties { _Intensity ("Brightness",Range(0,2))=1.2 }
    SubShader {
        Tags { "Queue"="Transparent+30" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _Intensity;
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_FOG_COORDS(1) };
            v2f vert(appdata_full v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o; }
            float4 frag(v2f i):SV_Target {
                float2 p=i.uv*2-1;
                float width=lerp(.08,.95,pow(saturate(1-abs(p.y)),.45));
                float edge=abs(p.x)-width;
                float coverage=saturate(-edge/max(fwidth(edge),.005))*(1-smoothstep(.92,1,abs(p.y)));
                // Saturated, alpha-blended strokes remain distinct when they
                // overlap. No additive halo or HDR white blob at the origin.
                float4 color=float4(saturate(i.color.rgb*_Intensity),i.color.a*coverage);
                UNITY_APPLY_FOG(i.fogCoord,color);
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
