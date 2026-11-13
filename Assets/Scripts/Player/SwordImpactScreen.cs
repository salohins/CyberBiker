using UnityEngine;

namespace CyberBiker.Player {
    // Runs on the game camera, before overlay UI. Owns its material instead of
    // editing the scene's shared post-processing profile.
    [RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    public sealed class SwordImpactScreen : MonoBehaviour {
        private Material material;
        private float darkness, desaturation, flash;
        private Vector2 center = new Vector2(.5f,.5f);
        private float side = 1;
        public int RenderCount { get; private set; }
        public float Strength => Mathf.Max(darkness,flash);
        public bool IsSupported => material!=null && material.shader.isSupported;

        public void Configure(Shader shader,float shade,float desaturate,float light,Vector2 contact,float direction) {
            if(shader!=null && (material==null || material.shader!=shader)) {
                if(material!=null)Destroy(material);
                material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            }
            darkness=shade;desaturation=desaturate;flash=light;center=contact;side=direction;
        }
        public void Clear(){darkness=desaturation=flash=0;}
        private void OnRenderImage(RenderTexture source,RenderTexture destination) {
            if(!IsSupported || Strength<.001f){Graphics.Blit(source,destination);return;}
            material.SetVector("_Impact",new Vector4(darkness,desaturation,flash,side));
            material.SetVector("_Contact",new Vector4(center.x,center.y,(float)source.width/source.height,0));
            Graphics.Blit(source,destination,material);RenderCount++;
        }
        private void OnDisable(){Clear();}
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
