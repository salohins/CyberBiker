Shader "Custom/HologramShaderWithOutline"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0.2, 0.6, 1, 1)
        _ScrollSpeed ("Scroll Speed", Range(0.1, 10)) = 1
        _Distortion ("Distortion", Range(0, 1)) = 0.1
        _GlowColor ("Glow Color", Color) = (0.2, 0.6, 1, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineThickness ("Outline Thickness", Range(0.001, 1)) = 0.01
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        // Outline Pass
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "Always" }
            Cull Front
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragOutline
            #include "UnityCG.cginc"
            
            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            
            uniform float _OutlineThickness;
            uniform float4 _OutlineColor;

            v2f vert(appdata_t v)
            {
                // Expand the vertices along the normals to create the outline
                v2f o;
                float3 normalDir = normalize(v.normal);
                o.pos = UnityObjectToClipPos(v.vertex + normalDir * _OutlineThickness);
                return o;
            }

            fixed4 fragOutline(v2f i) : SV_Target
            {
                // Return the outline color
                return _OutlineColor;
            }
            ENDCG
        }

        // Main Pass (Hologram Effect)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _ScrollSpeed;
            float _Distortion;
            float4 _GlowColor;
            float _GlowIntensity;
            
            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Scroll the texture
                float2 uv = i.uv;
                uv.y += _Time.y * _ScrollSpeed;
                
                // Sample the texture
                fixed4 col = tex2D(_MainTex, uv) * _Color;
                
                // Apply distortion
                uv.x += sin(uv.y * 10 + _Time.y * 5) * _Distortion;
                col.rgb += tex2D(_MainTex, uv).rgb * _Distortion;
                
                // Edge glow
                float edge = smoothstep(0.1, 0.5, abs(uv.y - 0.5));
                col.rgb += edge * _GlowColor.rgb * _GlowIntensity;
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Diffuse"
}
