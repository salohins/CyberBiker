Shader "CyberBiker/Stationary Lighting Tire"
{
    Properties {
        _MainTex ("Baked motorcycle atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TreadStrength ("Moving pattern blend", Range(0,1)) = 1
        _GrooveContrast ("Tread groove contrast", Range(0,1)) = 0.85
        _TreadPhase ("Tread revolutions", Float) = 0
        _TreadBlur ("Shutter blur", Range(0,0.3)) = 0
        _TreadBlend ("Moving pattern blend", Range(0,1)) = 0
        _RimColor ("Rim color", Color) = (0,0.886,1,1)
        _RimBias ("Rim bias", Range(-1,1)) = 0.404
        _ReflectionCubeMap ("Reflection", Cube) = "" {}
        _ReflectionColor ("Reflection color", Color) = (1,1,1,1)
        _ReflectionMaskOffset ("Reflection mask", Range(-1,1)) = -1
        _ReflectionFresnelBias ("Reflection bias", Range(-1,1)) = -0.088
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass {
            Cull [_Cull]
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _Color, _RimColor, _ReflectionColor;
            samplerCUBE _ReflectionCubeMap;
            float _TreadPhase, _TreadStrength, _GrooveContrast, _TreadBlur, _TreadBlend, _RimBias, _ReflectionMaskOffset, _ReflectionFresnelBias;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; float3 view:TEXCOORD2; UNITY_FOG_COORDS(3) };
            v2f vert(appdata v) {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                o.normal=UnityObjectToWorldNormal(v.normal); o.view=WorldSpaceViewDir(v.vertex);
                UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            bool Inside(float2 uv, float4 r) { return all(uv>=r.xy) && all(uv<=r.zw); }
            // Exact tread-only UV islands of the existing Motorcycle mesh. Sidewalls,
            // rim colors, and all other atlas islands retain their original sampling.
            float4 TreadRect(float2 uv, out float direction) {
                direction=-1;
                if(Inside(uv,float4(.225726,.658692,.256570,.736997))) return float4(.225726,.658692,.256570,.736997);
                if(Inside(uv,float4(.705769,.047152,.736664,.125752))) return float4(.705769,.047152,.736664,.125752);
                if(Inside(uv,float4(.575158,.699503,.601653,.778619))) return float4(.575158,.699503,.601653,.778619);
                if(Inside(uv,float4(.748364,.047152,.774910,.126565))) return float4(.748364,.047152,.774910,.126565);
                direction=1;
                if(Inside(uv,float4(.689937,.286248,.720832,.364973))) return float4(.689937,.286248,.720832,.364973);
                if(Inside(uv,float4(.680723,.645922,.711561,.724714))) return float4(.680723,.645922,.711561,.724714);
                if(Inside(uv,float4(.732533,.286248,.759080,.365788))) return float4(.732533,.286248,.759080,.365788);
                if(Inside(uv,float4(.742791,.440381,.769280,.519989))) return float4(.742791,.440381,.769280,.519989);
                return 0;
            }
            float Luma(float3 c) { return dot(c,float3(.2126,.7152,.0722)); }
            float3 Lighting(float2 uv,float4 r) {
                float3 sum=0;
                [unroll] for(int k=-3;k<=3;k++) {
                    float2 p=uv+float2(0,k*17*_MainTex_TexelSize.y);
                    p=clamp(p,r.xy+_MainTex_TexelSize.xy,r.zw-_MainTex_TexelSize.xy);
                    sum+=tex2D(_MainTex,p).rgb;
                }
                return sum/7;
            }
            float4 frag(v2f i):SV_Target {
                float4 atlas=tex2D(_MainTex,i.uv);
                float direction; float4 rect=TreadRect(i.uv,direction);
                if(rect.z>0) {
                    float2 local=(i.uv-rect.xy)/(rect.zw-rect.xy);
                    // A continuous tread layer has no painted light/shadow to rotate.
                    float wave=local.y*4 + abs(local.x-.5)*1.4 + _TreadPhase*16*direction;
                    float groove=abs(frac(wave)-.5);
                    float aa=max(fwidth(wave),.015);
                    float grooveMask=1-smoothstep(.11-aa,.11+aa,groove);
                    float contrast=_GrooveContrast*(1-saturate(_TreadBlur)*.75);
                    float3 fixedLighting=Lighting(i.uv,rect);
                    // Balance bright shoulders against deeper grooves so contrast
                    // increases without moving or substantially lifting baked light.
                    float3 moving=fixedLighting*(1+contrast*(.22-grooveMask));
                    float edge=smoothstep(0,.06,min(local.x,1-local.x))*smoothstep(0,.035,min(local.y,1-local.y));
                    atlas.rgb=lerp(atlas.rgb,moving,saturate(_TreadBlend)*_TreadStrength*edge);
                }
                float3 normal=normalize(i.normal), view=normalize(i.view);
                float4 color=atlas*_Color;
                float fresnel=1-saturate(dot(normal,view)+_ReflectionFresnelBias);
                color.rgb+=texCUBE(_ReflectionCubeMap,reflect(-view,normal)).rgb*_ReflectionColor.rgb*fresnel*fresnel*saturate(atlas.a+_ReflectionMaskOffset);
                color.rgb+=_RimColor.rgb*(1-saturate(dot(normal,view)+_RimBias));
                UNITY_APPLY_FOG(i.fogCoord,color); color.a=1; return color;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}
