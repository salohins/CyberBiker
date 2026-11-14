Shader "CyberBiker/Combat Damage Screen" {
 Properties { _MainTex ("Scene",2D)="white" {} }
 SubShader { Cull Off ZWrite Off ZTest Always
  Pass {
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_TexelSize;float4 _Feedback;float4 _Direction;
   float hash(float n){return frac(sin(n*127.1)*43758.5453);}
   float4 frag(v2f_img i):SV_Target {
    float2 uv=i.uv;float damage=saturate(_Feedback.x);float miss=saturate(_Feedback.y);
    float2 p=(uv-.5)*2;float edge=smoothstep(.25,1,length(p));
    float tick=floor(_Feedback.z*35);float stripe=step(.84,hash(floor(uv.y*32)+tick*9));
    float shift=(hash(tick+3)-.5)*.015*damage*stripe*edge;
    float2 q=uv+float2(shift,0);float aberration=damage*.0025*edge;
    float4 color=tex2D(_MainTex,q);color.r=tex2D(_MainTex,q+float2(aberration,0)).r;color.b=tex2D(_MainTex,q-float2(aberration,0)).b;
    float directional=.55+.45*saturate(dot(normalize(p+float2(.0001,.0001)),normalize(_Direction.xy+float2(.0001,.0001))));
    float rim=smoothstep(.35,1,max(abs(p.x),abs(p.y)));
    color.rgb*=1-damage*edge*.2;
    color.rgb=lerp(color.rgb,float3(.65,.035,.09),damage*rim*directional*.6);
    color.rgb+=damage*stripe*edge*.025*float3(.6,.14,.26);
    float side=smoothstep(.4,1,p.x*_Feedback.w);color.rgb+=miss*side*rim*float3(.05,.25,.32);
    return color;
   }
   ENDCG
  }
 }
 Fallback Off
}
