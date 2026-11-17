using UnityEngine;

namespace CyberBiker.Player {
    [System.Serializable]
    public sealed class GapDashSettings {
        public bool enabled=true;
        [Header("Automatic gap assist")]
        [Range(.2f,8)] public float snapRadius=5;
        [Tooltip("Recognize sideways momentum toward a gap, including countersteering to brake a slide. The maximum forward catch distance remains a firm limit.")]
        public bool predictiveSideEntry=true;
        [Range(.08f,1.5f)] public float approachTime=.85f;
        [Tooltip("Firm maximum clearance from the front of the bike to the nearest rear bumper before alignment may start. Prediction cannot override this distance.")]
        [Range(3,100)] public float maximumTriggerDistance=65;
        [Range(.02f,.3f)] public float sideClearance=.04f;
        [Range(1,6)] public float maximumGapWidth=3;
        [Range(10,80)] public float minimumSpeed=25;
        [Range(.1f,4)] public float cooldown=1.2f;
        [Header("Automatic alignment")]
        [Range(6,30)] public float maximumAlignmentSpeed=18;
        [Header("Gap alignment â€” gameplay seconds, normal riding speed")]
        [Tooltip("Minimum blend time. Wide corrections and getting a leaning bike upright may require a longer blend.")]
        [Range(.06f,.35f)] public float alignmentTime=.15f;
        [Tooltip("Minimum safety timeout. Automatically extended when more time is needed to clear both cars at the current riding speed.")]
        [Range(.2f,3)] public float maximumDashTime=1.2f;
        [Range(.2f,5)] public float exitClearance=1.4f;
        [Header("Camera â€” real seconds")]
        public bool cameraMotion=true;
        [Range(.02f,.4f)] public float cameraBlendIn=.1f;
        [Range(.05f,1)] public float cameraReturn=.4f;
        public Vector3 cameraOffset=new Vector3(0,-.45f,.65f);
        public Vector3 cameraEuler=new Vector3(-2,0,0);
        [Range(-15,20)] public float zoomInFov=5;
        [Range(0,3)] public float cameraKick=.18f;
        [Range(0,2)] public float shake=.35f;
        [Range(.05f,.6f)] public float shakeDuration=.22f;
        [Header("Wind burst")]
        public bool particles=true;
        [Range(0,1200)] public float emission=420;
        [Range(.05f,.6f)] public float particleLifetime=.25f;
        [Range(.005f,.1f)] public float particleWidth=.022f;
        [Range(.2f,3)] public float particleLength=1.1f;
        [Range(.2f,2)] public float particleRadius=.65f;
        [Range(1,50)] public float particleSpeed=18;
        public Color particleColor=new Color(.12f,.65f,1,.8f);
        [Range(0,2)] public float whooshVolume=.8f;
        public GapDashSettings Copy()=>JsonUtility.FromJson<GapDashSettings>(JsonUtility.ToJson(this));
        public void Normalize(){
            // Keep imported presets finite and within the same ranges as Inspector controls.
            var fields=typeof(GapDashSettings).GetFields();var defaults=new GapDashSettings();
            foreach(var field in fields){var range=(RangeAttribute)System.Attribute.GetCustomAttribute(field,typeof(RangeAttribute));if(range==null)continue;float v=(float)field.GetValue(this);field.SetValue(this,float.IsNaN(v)||float.IsInfinity(v) ? field.GetValue(defaults) : Mathf.Clamp(v,range.min,range.max));}
            if(!Finite(cameraOffset))cameraOffset=defaults.cameraOffset;if(!Finite(cameraEuler))cameraEuler=defaults.cameraEuler;
            if(!Finite(new Vector3(particleColor.r,particleColor.g,particleColor.b)) || float.IsNaN(particleColor.a) || float.IsInfinity(particleColor.a))particleColor=defaults.particleColor;
        }
        private static bool Finite(Vector3 v)=>!float.IsNaN(v.x+v.y+v.z) && !float.IsInfinity(v.x+v.y+v.z);
    }
    [CreateAssetMenu(menuName="CyberBiker/Gap Dash Profile")]
    public sealed class GapDashProfile:ScriptableObject {
        public GapDashSettings settings=new GapDashSettings();
        public Material windMaterial;
        public AudioClip whoosh;
    }
}
