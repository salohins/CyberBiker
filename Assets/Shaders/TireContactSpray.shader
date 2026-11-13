Shader "CyberBiker/Tire Contact Spray" {
    Properties {
        _Intensity ("Glow intensity",Range(0,12))=2.5
        _CoreWhiteness ("White core",Range(0,1))=.75
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
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
            float _CoreWhiteness;
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_FOG_COORDS(1) };
            v2f vert(appdata_full v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o; }
            float4 frag(v2f i):SV_Target {
                float shape=saturate(1-length((i.uv-.5)*2));
                float core=smoothstep(.25,.8,shape);
                float3 glow=lerp(i.color.rgb,float3(1,.94,.78),core*_CoreWhiteness);
                float4 color=float4(glow*_Intensity,i.color.a*shape*lerp(shape,1,.6));
                UNITY_APPLY_FOG_COLOR(i.fogCoord,color,float4(0,0,0,0));return color;
            }
            ENDCG
        }
    }
}
