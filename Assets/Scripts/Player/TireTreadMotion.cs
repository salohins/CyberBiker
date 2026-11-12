using UnityEngine;
using Gameplay;

namespace CyberBiker.Player {
    // Animate material detail only; rotating the baked atlas would rotate its lighting.
    public sealed class TireTreadMotion : MonoBehaviour {
        [SerializeField, Min(0.01f)] private float wheelRadius = 0.72f;
        [Tooltip("Visual rotation scale, reduced to avoid a strobing tread at runner speeds.")]
        [SerializeField, Range(0,1)] private float spinScale = 0.2f;
        [SerializeField, Range(0,1)] private float motionBlur = 0.4f;
        [Tooltip("Maximum visible tread bands passing per second. Keeps high-speed movement readable instead of strobing.")]
        [SerializeField, Range(4,24)] private float maxTreadBandRate = 18f;
        private Renderer tireRenderer;
        private int slot = -1;
        private Rigidbody body;
        private GameplayManager manager;
        private MaterialPropertyBlock properties;
        public float TreadPhase { get; private set; }
        private static readonly int Phase = Shader.PropertyToID("_TreadPhase");
        private static readonly int Blend = Shader.PropertyToID("_TreadBlend");
        private static readonly int Blur = Shader.PropertyToID("_TreadBlur");
        private void Awake() {
            body=GetComponent<Rigidbody>(); properties=new MaterialPropertyBlock();
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>(true)) {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                    if(materials[i]!=null && materials[i].shader.name=="CyberBiker/Stationary Lighting Tire") {
                        tireRenderer=renderer;slot=i;return;
                    }
            }
        }
        private void Update() {
            if(tireRenderer==null || body==null) return;
            if(manager==null) manager=FindAnyObjectByType<GameplayManager>();
            if(Time.deltaTime<=0 || (manager!=null && manager.gameState!=GameState.Playing)) return;
            float speed=Vector3.Dot(body.linearVelocity,transform.forward);
            float turns=speed*spinScale/(Mathf.PI*2*Mathf.Max(.01f,wheelRadius));
            // There are 16 bands around the tire. Cap their apparent temporal
            // frequency below the frame rate while retaining speed response.
            float readableRate=Mathf.Min(maxTreadBandRate,0.3f/Time.deltaTime);
            turns=Mathf.Sign(turns)*readableRate/16*(1-Mathf.Exp(-Mathf.Abs(turns)*16/readableRate));
            TreadPhase=Mathf.Repeat(TreadPhase+turns*Time.deltaTime,1);
            tireRenderer.GetPropertyBlock(properties,slot);
            properties.SetFloat(Phase,TreadPhase);
            properties.SetFloat(Blend,Mathf.InverseLerp(0,2,Mathf.Abs(speed)));
            properties.SetFloat(Blur,Mathf.Min(.3f,Mathf.Abs(turns)*Time.deltaTime*4*motionBlur));
            tireRenderer.SetPropertyBlock(properties,slot);
        }
        private void OnDisable() {
            if(tireRenderer!=null && slot>=0) tireRenderer.SetPropertyBlock(null,slot);
        }
    }
}
