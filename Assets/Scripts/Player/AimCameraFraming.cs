using System;
using UnityEngine;

namespace CyberBiker.Player {
    // View composition only: this never changes the aim target, weapon or bike.
    public sealed class AimCameraFraming {
        [Serializable]
        public sealed class Settings {
            public bool enabled = true;
            [Tooltip("A gentle camera turn toward the selected enemy, rather than a full lock-on.")]
            [Range(0,1)] public float targetLookWeight = .35f;
            [Range(0,25)] public float maximumYaw = 10;
            [Range(0,20)] public float maximumPitch = 6;
            [Tooltip("Seconds for the view to ease into a framing correction.")]
            [Range(.05f,1)] public float smoothTime = .25f;
            public bool keepPistolVisible = true;
            [Tooltip("Maximum extra sideways movement if the rider hides the pistol.")]
            [Range(0,4)] public float maximumSideCorrection = 3;
            [Range(0,2)] public float maximumHeightCorrection = .8f;
            [Range(0,.25f)] public float screenMargin = .08f;
            [Tooltip("Extra space around the rider's chest/head when finding a clear pistol view.")]
            [Range(0,.2f)] public float bodyClearance = .05f;
        }

        private readonly BikeAutoAim aim;
        private readonly Transform lowerTorso,chest,head,leftShoulder,rightShoulder;
        private Vector2 correction,correctionVelocity,preferred;
        private float yaw,pitch,yawVelocity,pitchVelocity;
        private float bodyOpacity=1;
        public Vector2 LookAngles => new Vector2(yaw,pitch);
        public Vector2 VisibilityOffset => correction;
        public bool WeaponVisible {get;private set;}

        public AimCameraFraming(PlayerController player) {
            aim=player.GetComponent<BikeAutoAim>();
            foreach(var bone in player.GetComponentsInChildren<Transform>(true)) {
                if(bone.name=="DEF-spine.001") lowerTorso=bone;
                if(bone.name=="DEF-spine.003") chest=bone;
                if(bone.name=="DEF-spine.005") head=bone;
                if(bone.name=="DEF-upper_arm.L") leftShoulder=bone;
                if(bone.name=="DEF-upper_arm.R") rightShoulder=bone;
            }
        }

        public void Apply(Camera camera,Settings settings,bool aiming,float dt,float riderOpacity=1) {
            bodyOpacity=riderOpacity;
            Quaternion basis=camera.transform.rotation;Vector3 position=camera.transform.position;
            Vector3 right=basis*Vector3.right;
            if(dt<=0) {
                camera.transform.position=position+right*correction.x+Vector3.up*correction.y;
                camera.transform.rotation=basis*Quaternion.Euler(pitch,yaw,0);
                return;
            }
            bool active=settings.enabled && aiming && aim!=null && aim.IsWeaponReady && aim.Pistol!=null && aim.Muzzle!=null;
            Vector2 goal=Vector2.zero;
            if(active && settings.keepPistolVisible && chest!=null && head!=null) {
                goal=FindClearView(camera,settings,position,basis,right);
            } else preferred=Vector2.zero;
            float time=Mathf.Max(.05f,settings.smoothTime);
            correction=Vector2.SmoothDamp(correction,goal,ref correctionVelocity,time,Mathf.Infinity,Mathf.Max(0,dt));
            camera.transform.position=position+right*correction.x+Vector3.up*correction.y;
            Vector2 angles=active && aim.Target!=null ? TargetAngles(camera.transform.position,basis,settings) : Vector2.zero;
            yaw=Mathf.SmoothDampAngle(yaw,angles.x,ref yawVelocity,time,Mathf.Infinity,Mathf.Max(0,dt));
            pitch=Mathf.SmoothDampAngle(pitch,angles.y,ref pitchVelocity,time,Mathf.Infinity,Mathf.Max(0,dt));
            camera.transform.rotation=basis*Quaternion.Euler(pitch,yaw,0);
            WeaponVisible=active && ViewScore(camera,settings,camera.transform.position,camera.transform.rotation)>=0;
        }

        private Vector2 TargetAngles(Vector3 position,Quaternion basis,Settings settings) {
            Vector3 direction=Quaternion.Inverse(basis)*(aim.TargetViewPoint-position);
            if(direction.z<=0) return Vector2.zero;
            float y=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            float p=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
            return new Vector2(Mathf.Clamp(y*settings.targetLookWeight,-settings.maximumYaw,settings.maximumYaw),
                Mathf.Clamp(p*settings.targetLookWeight,-settings.maximumPitch,settings.maximumPitch));
        }
        private Quaternion CandidateRotation(Vector3 position,Quaternion basis,Settings settings) {
            var angles=aim.Target!=null ? TargetAngles(position,basis,settings) : Vector2.zero;
            return basis*Quaternion.Euler(angles.y,angles.x,0);
        }
        private Vector2 FindClearView(Camera camera,Settings settings,Vector3 position,Quaternion basis,Vector3 right) {
            preferred.x=Mathf.Clamp(preferred.x,-settings.maximumSideCorrection,settings.maximumSideCorrection);
            preferred.y=Mathf.Clamp(preferred.y,0,settings.maximumHeightCorrection);
            float baseline=ViewScore(camera,settings,position,CandidateRotation(position,basis,settings));
            if(baseline>.06f || (baseline>=0 && preferred.sqrMagnitude<.001f)) {preferred=Vector2.zero;return preferred;}
            Vector3 previous=position+right*preferred.x+Vector3.up*preferred.y;
            if(preferred.sqrMagnitude>.001f && ViewScore(camera,settings,previous,CandidateRotation(previous,basis,settings))>=0) return preferred;
            Vector2 best=Vector2.zero;float bestScore=baseline,bestCost=float.PositiveInfinity;bool found=false;
            float weaponSide=Mathf.Sign(Vector3.Dot(aim.Muzzle.position-chest.position,right));
            if(weaponSide==0) weaponSide=1;
            for(int height=0;height<=3;height++) for(int side=-6;side<=6;side++) {
                var candidate=new Vector2(side*weaponSide*settings.maximumSideCorrection/6,height*settings.maximumHeightCorrection/3);
                Vector3 point=position+right*candidate.x+Vector3.up*candidate.y;
                float score=ViewScore(camera,settings,point,CandidateRotation(point,basis,settings));
                float cost=candidate.x*candidate.x+candidate.y*candidate.y*1.5f+(candidate-preferred).sqrMagnitude*.1f;
                if(score>=0 && (!found || cost<bestCost)) {best=candidate;bestCost=cost;found=true;}
                else if(!found && score>bestScore) {best=candidate;bestScore=score;}
            }
            preferred=best;return best;
        }

        private float ViewScore(Camera camera,Settings settings,Vector3 position,Quaternion rotation) {
            if(aim.Pistol==null || aim.Muzzle==null) return -1;
            float width=leftShoulder!=null && rightShoulder!=null ? Vector3.Distance(leftShoulder.position,rightShoulder.position) : .7f;
            float radius=Mathf.Max(.18f,width*.42f)+settings.bodyClearance;
            float headRadius=Mathf.Max(.16f,width*.3f)+settings.bodyClearance;
            Vector3 weapon=Vector3.Lerp(aim.Pistol.position,aim.Muzzle.position,.65f);
            float score=Mathf.Min(ScreenClearance(camera,settings,position,rotation,weapon),ScreenClearance(camera,settings,position,rotation,aim.Muzzle.position));
            if(bodyOpacity>.5f && lowerTorso!=null && chest!=null && head!=null) {
                for(int i=0;i<4;i++) {
                    Vector3 body=Vector3.Lerp(lowerTorso.position,chest.position,i/3f);
                    score=Mathf.Min(score,DistanceToSegment(body,position,weapon)-radius);
                    score=Mathf.Min(score,DistanceToSegment(body,position,aim.Muzzle.position)-radius);
                }
                score=Mathf.Min(score,DistanceToSegment(head.position,position,weapon)-headRadius);
                score=Mathf.Min(score,DistanceToSegment(head.position,position,aim.Muzzle.position)-headRadius);
            }
            return score;
        }
        private static float ScreenClearance(Camera camera,Settings settings,Vector3 position,Quaternion rotation,Vector3 point) {
            Vector3 local=Quaternion.Inverse(rotation)*(point-position);
            if(local.z<=camera.nearClipPlane) return -1;
            float halfHeight=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)*local.z;
            float x=.5f+local.x/(2*halfHeight*camera.aspect),y=.5f+local.y/(2*halfHeight);
            return Mathf.Min(Mathf.Min(x-settings.screenMargin,1-settings.screenMargin-x),Mathf.Min(y-settings.screenMargin,1-settings.screenMargin-y))*2;
        }
        private static float DistanceToSegment(Vector3 point,Vector3 start,Vector3 end) {
            Vector3 line=end-start;float length=line.sqrMagnitude;
            float t=length>.0001f ? Mathf.Clamp01(Vector3.Dot(point-start,line)/length) : 0;
            return Vector3.Distance(point,start+line*t);
        }
        public void Reset() {correction=correctionVelocity=preferred=Vector2.zero;yaw=pitch=yawVelocity=pitchVelocity=0;WeaponVisible=false;}
    }
}
