using UnityEngine;

namespace CyberBiker.Combat {
    [System.Serializable]
    public sealed class CloseDodgeSettings {
        public bool enabled=true;
        [Header("Close pass")]
        [Tooltip("Maximum distance from the torso centre at the bullet's closest passing point, in world units. Hits never trigger it.")]
        [Range(.1f,6)] public float passRadius=1.8f;
        [Range(.5f,4)] public float centerHeight=1.6f;
        [Range(0,5)] public float cooldown=1.2f;
        [Range(0,.5f)] public float delay=.02f;
        [Header("World slow motion — real seconds")]
        public bool slowMotion=true;
        [Range(.1f,1)] public float worldSpeed=.25f;
        [Range(0,.5f)] public float slowBlendIn=.07f;
        [Range(0,3)] public float slowHold=.5f;
        [Range(.01f,2)] public float slowRecovery=.5f;
        [Header("Camera — real seconds")]
        public bool cameraMotion=true;
        public Vector3 viewOffset,viewEuler;
        [Range(0,.6f)] public float cameraBlendIn=.12f;
        [Range(0,2)] public float cameraHold=.2f;
        [Range(.01f,2)] public float cameraReturn=.65f;
        [Range(0,25)] public float zoomInFov=8;
        [Range(0,2)] public float sideways=.65f;
        [Range(0,1)] public float dolly=.2f;
        [Range(-.5f,1)] public float lift=.12f;
        [Range(0,15)] public float lookTurn=3.5f;
        [Range(0,6)] public float roll=1.1f;
        [Range(0,1)] public float shake=.12f;
        [Range(.03f,.6f)] public float shakeDuration=.18f;
        public float Duration=>delay+Mathf.Max(slowMotion ? slowBlendIn+slowHold+slowRecovery : 0,cameraMotion ? Mathf.Max(cameraBlendIn+cameraHold+cameraReturn,shakeDuration) : 0);
        public CloseDodgeSettings Copy()=>JsonUtility.FromJson<CloseDodgeSettings>(JsonUtility.ToJson(this));
        public void Normalize(){
            passRadius=Safe(passRadius,.1f,6,1.8f);centerHeight=Safe(centerHeight,.5f,4,1.6f);cooldown=Safe(cooldown,0,5,1.2f);delay=Safe(delay,0,.5f,.02f);
            worldSpeed=Safe(worldSpeed,.1f,1,.25f);slowBlendIn=Safe(slowBlendIn,0,.5f,.07f);slowHold=Safe(slowHold,0,3,.5f);slowRecovery=Safe(slowRecovery,.01f,2,.5f);
            cameraBlendIn=Safe(cameraBlendIn,0,.6f,.12f);cameraHold=Safe(cameraHold,0,2,.2f);cameraReturn=Safe(cameraReturn,.01f,2,.65f);
            zoomInFov=Safe(zoomInFov,0,25,8);sideways=Safe(sideways,0,2,.65f);dolly=Safe(dolly,0,1,.2f);lift=Safe(lift,-.5f,1,.12f);
            lookTurn=Safe(lookTurn,0,15,3.5f);roll=Safe(roll,0,6,1.1f);shake=Safe(shake,0,1,.12f);shakeDuration=Safe(shakeDuration,.03f,.6f,.18f);
        }
        private static float Safe(float value,float min,float max,float fallback)=>float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value,min,max);
    }
    [CreateAssetMenu(menuName="CyberBiker/Close Dodge Profile")]
    public sealed class CloseDodgeProfile : ScriptableObject {
        public CloseDodgeSettings settings=new CloseDodgeSettings();
    }
}
