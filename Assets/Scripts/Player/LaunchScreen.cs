using UnityEngine;

namespace CyberBiker.Player {
    [RequireComponent(typeof(Camera)),DisallowMultipleComponent]
    public sealed class LaunchScreen : MonoBehaviour {
        private Material material;
        private float amount,pulse,width;
        private Color color;
        public int RenderCount { get; private set; }
        public float Strength => amount;
        public bool IsSupported => material!=null && material.shader.isSupported;
        public void Configure(Shader shader,float strength,float entryPulse,float borderWidth,Color tint) {
            if(shader!=null && (material==null || material.shader!=shader)) {
                if(material!=null)Destroy(material);
                material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            }
            amount=Mathf.Clamp01(strength);pulse=entryPulse;width=borderWidth;color=tint;
        }
        public void Clear(){amount=0;}
        private void OnRenderImage(RenderTexture source,RenderTexture destination) {
            if(!IsSupported || amount<.001f){Graphics.Blit(source,destination);return;}
            material.SetVector("_Launch",new Vector4(amount,pulse,width,0));material.SetColor("_GlowColor",color);
            Graphics.Blit(source,destination,material);RenderCount++;
        }
        private void OnDisable(){Clear();}
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
