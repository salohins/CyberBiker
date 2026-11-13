Shader "Hidden/CyberBiker/Sword Impact Screen" {
    Properties { _MainTex ("Camera", 2D) = "white" {} }
    SubShader {
        Cull Off ZWrite Off ZTest Always
        Pass {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Impact, _Contact;
            half4 frag(v2f_img i):SV_Target {
                half4 source=tex2D(_MainTex,i.uv);
                float2 p=(i.uv-_Contact.xy)*float2(_Contact.z,1);
                // Preserve the hit's centre; darken and desaturate the surroundings.
                float edge=smoothstep(.12,.8,length(p));
                float gray=dot(source.rgb,half3(.2126,.7152,.0722));
                half3 color=lerp(source.rgb,gray.xxx,_Impact.y*(.35+.65*edge));
                color*=1-_Impact.x*(.3+.7*edge);
                // A single short, tapered slash of light, rather than a whiteout.
                float along=dot(p,normalize(float2(1,.42*_Impact.w)));
                float across=dot(p,normalize(float2(-.42*_Impact.w,1)));
                float streak=exp(-abs(across)*160)*exp(-along*along*12);
                float glow=exp(-dot(p,p)*22);
                color+=half3(.7,.86,1)*_Impact.z*(streak*.8+glow*.12);
                return half4(color,source.a);
            }
            ENDCG
        }
    }
}
