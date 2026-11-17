using UnityEngine;

namespace CyberBiker.Player {
    public enum MobileSteeringMode { Swipe, Gyroscope }
    // Gravity gives a stable held tilt, rather than a rotation-rate impulse that
    // falls back to zero as soon as the phone stops turning.
    public sealed class TiltSteering {
        public bool Calibrated {get;private set;}
        public float NeutralDegrees {get;private set;}
        public float Output {get;private set;}
        public void ResetCalibration(){Calibrated=false;Output=0;}
        public void ResetOutput(){Output=0;}
        public void Calibrate(float degrees){NeutralDegrees=degrees;Calibrated=true;Output=0;}
        public float Sample(float degrees,float sensitivity,float deadZone,float smoothing,bool invert,float dt){
            if(!Calibrated){Calibrate(degrees);return 0;}
            float angle=Mathf.DeltaAngle(NeutralDegrees,degrees);
            float fullAngle=20/Mathf.Clamp(sensitivity,.25f,3);
            float target=Mathf.Sign(angle)*Mathf.Clamp01((Mathf.Abs(angle)-deadZone)/Mathf.Max(1,fullAngle-deadZone));
            if(invert)target=-target;
            Output=smoothing<=0 ? target : Mathf.Lerp(Output,target,1-Mathf.Exp(-Mathf.Max(0,dt)/smoothing));return Output;
        }
        public static bool TryReadRoll(Vector3 gravity,ScreenOrientation orientation,bool compensated,out float degrees){
            degrees=0;
            if(float.IsNaN(gravity.x) || float.IsNaN(gravity.y) || float.IsInfinity(gravity.x) || float.IsInfinity(gravity.y))return false;
            Vector2 screen=new Vector2(gravity.x,gravity.y);
            // Unity normally compensates sensors already. Rotate only when the
            // project has disabled that global option, avoiding a double turn.
            if(!compensated)switch(orientation){
                case ScreenOrientation.LandscapeLeft:screen=new Vector2(-gravity.y,gravity.x);break;
                case ScreenOrientation.LandscapeRight:screen=new Vector2(gravity.y,-gravity.x);break;
                case ScreenOrientation.PortraitUpsideDown:screen=-screen;break;
            }
            if(screen.sqrMagnitude<.01f)return false;
            degrees=Mathf.Atan2(screen.x,-screen.y)*Mathf.Rad2Deg;return true;
        }
    }
}
