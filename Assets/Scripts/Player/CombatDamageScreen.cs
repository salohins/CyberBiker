using UnityEngine;
namespace CyberBiker.Player {
    [RequireComponent(typeof(Camera)),DisallowMultipleComponent]
    public sealed class CombatDamageScreen:MonoBehaviour {
        private Material material;private float damage,nearMiss,side;private Vector2 direction;
        public int RenderCount {get;private set;}
        public float Strength=>Mathf.Max(damage,nearMiss);
        public void Configure(Material source,float hurt,float miss,Vector2 incoming,float passingSide){
            if(source!=null && material==null)material=new Material(source){hideFlags=HideFlags.HideAndDontSave};damage=hurt;nearMiss=miss;direction=incoming;side=passingSide;
            enabled=material!=null && material.shader.isSupported && Strength>.001f;
        }
        public void Clear(){damage=nearMiss=0;enabled=false;}
        private void OnRenderImage(RenderTexture source,RenderTexture destination){
            if(material==null || !material.shader.isSupported || Strength<.001f){Graphics.Blit(source,destination);return;}
            material.SetVector("_Feedback",new Vector4(damage,nearMiss,Time.time,side));material.SetVector("_Direction",new Vector4(direction.x,direction.y,0,0));
            Graphics.Blit(source,destination,material);RenderCount++;
        }
        private void OnDisable(){Clear();}private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
