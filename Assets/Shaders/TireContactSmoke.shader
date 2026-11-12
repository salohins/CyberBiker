Shader "CyberBiker/Tire Contact Smoke" {
    Properties {
        _MainTex ("Cartoon FX cloud atlas",2D)="white" {}
        _BumpMap ("Cloud lobe normals",2D)="bump" {}
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex, _BumpMap;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_FOG_COORDS(1)};
            v2f vert(appdata_full v) {v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;}
            float4 frag(v2f i):SV_Target {
                float4 cloud=tex2D(_MainTex,i.uv);
                float3 normal=UnpackNormal(tex2D(_BumpMap,i.uv));
                // Broad toon shading on the existing rounded lobes gives each
                // overlapping billboard volume without stretching it into a ribbon.
                float light=dot(normal,normalize(float3(-.55,.65,.5)));
                float shade=lerp(.42,.76,smoothstep(.03,.13,light));
                shade=lerp(shade,1.12,smoothstep(.55,.67,light));
                float4 color=float4(i.color.rgb*cloud.rgb*shade,i.color.a*cloud.a);
                UNITY_APPLY_FOG(i.fogCoord,color);return color;
            }
            ENDCG
        }
    }
}
