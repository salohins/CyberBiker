Shader "CyberBiker/Pistol Aim Laser" {
 Properties { _Intensity ("Red laser brightness",Range(.5,3))=1.8 }
 SubShader {
  Tags {"Queue"="Transparent+35" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend SrcAlpha One ZWrite Off Cull Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   float _Intensity;
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_FOG_COORDS(1)};
   v2f vert(appdata_full v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;}
   float4 frag(v2f i):SV_Target{float core=saturate(1-abs(i.uv.y-.5)*2);float4 c=float4(i.color.rgb*_Intensity,i.color.a*smoothstep(0,.7,core));UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(0,0,0,0));return c;}
   ENDCG
  }
 }
 Fallback Off
}
