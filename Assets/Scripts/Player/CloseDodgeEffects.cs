using CyberBiker.Combat;
using Gameplay;
using UnityEngine;

namespace CyberBiker.Player {
    [DefaultExecutionOrder(400),DisallowMultipleComponent]
    public sealed class CloseDodgeEffects : MonoBehaviour {
        private GameplayManager manager;
        private PlayerController player;
        private BikeSwordCombat sword;
        private float age=100,clock,nextAllowed,side=1;
        private bool active,cameraApplied;
        private Camera lastCamera;
        private Vector3 basePosition,finalPosition;
        private Quaternion baseRotation,finalRotation;
        private float baseFov,finalFov;
        public CloseDodgeSettings Settings=>manager.CloseDodge;
        public Vector3 Center=>transform.position+Vector3.up*Settings.centerHeight;
        public bool IsActive=>active;
        public int TriggerCount {get;private set;}
        public float LastPassDistance {get;private set;}
        public float Age=>age;
        public float Side=>side;
        public float CameraAmount=>active && Settings.enabled && Settings.cameraMotion ? Envelope(age-Settings.delay,Settings.cameraBlendIn,Settings.cameraHold,Settings.cameraReturn) : 0;
        public float WorldTimeScale=>active && Settings.enabled && Settings.slowMotion ? Mathf.Lerp(1,Settings.worldSpeed,Envelope(age-Settings.delay,Settings.slowBlendIn,Settings.slowHold,Settings.slowRecovery)) : 1;
        public float LastCameraZoom {get;private set;}
        public float LastCameraSide {get;private set;}
        private bool Finishing=>sword!=null && (sword.TrafficProtected || sword.Phase!=BikeSwordCombat.CombatPhase.Idle && sword.Phase!=BikeSwordCombat.CombatPhase.Pursuing);
        private void Awake(){manager=FindAnyObjectByType<GameplayManager>();player=GetComponent<PlayerController>();sword=GetComponent<BikeSwordCombat>();manager?.BindCloseDodge(this);}
        private void Update(){
            if(manager==null)return;
            if(manager.gameState==GameState.Pause)return;
            if(manager.gameState!=GameState.Playing || player==null || !player.IsAlive || !Settings.enabled || Finishing || manager.CombatSlowMotionRemaining>0){Cancel();return;}
            float dt=FinisherClock.UnscaledDeltaTime;clock+=dt;
            if(!active)return;
            age+=dt;if(age>=Settings.Duration)active=false;
            manager.RefreshTimeState();
        }
        public bool TryClosePass(float distance,Vector3 point){
            if(!isActiveAndEnabled || manager==null || manager.gameState!=GameState.Playing || player==null || !player.IsAlive || !Settings.enabled || active || clock<nextAllowed || Finishing || (manager.GapDashEffects!=null && manager.GapDashEffects.IsActive) || manager.CombatSlowMotionRemaining>0 || distance>Settings.passRadius)return false;
            var view=Camera.main;var right=view!=null ? view.transform.right : transform.right;
            side=Vector3.Dot(point-Center,right)>=0 ? 1 : -1;
            age=0;active=true;LastPassDistance=distance;TriggerCount++;nextAllowed=clock+Settings.cooldown;
            manager.RefreshTimeState();return true;
        }
        public void Cancel(){active=false;age=100;RemoveCamera(lastCamera);if(manager!=null)manager.RefreshTimeState();}
        public void ResetForPreview(){Cancel();clock=nextAllowed=0;TriggerCount=0;}
#if UNITY_EDITOR
        public void SeekPreview(float realSeconds){if(manager==null)return;age=Mathf.Clamp(realSeconds,0,Settings.Duration);active=age<Settings.Duration;manager.RefreshTimeState();}
#endif
        public static float Envelope(float elapsed,float blendIn,float hold,float blendOut){
            if(elapsed<0)return 0;
            if(blendIn>0 && elapsed<blendIn)return Mathf.SmoothStep(0,1,elapsed/blendIn);
            elapsed-=blendIn;if(elapsed<hold)return 1;
            return blendOut>0 ? 1-Mathf.SmoothStep(0,1,(elapsed-hold)/blendOut) : 0;
        }
        public void RemoveCamera(Camera camera){
            if(!cameraApplied || camera==null)return;
            if((camera.transform.position-finalPosition).sqrMagnitude<.000001f)camera.transform.position=basePosition;
            if(Quaternion.Angle(camera.transform.rotation,finalRotation)<.001f)camera.transform.rotation=baseRotation;
            if(Mathf.Abs(camera.fieldOfView-finalFov)<.0001f)camera.fieldOfView=baseFov;
            cameraApplied=false;
        }
        public void ApplyCamera(Camera camera){
            LastCameraZoom=LastCameraSide=0;
            if(camera==null || manager==null || manager.gameState!=GameState.Playing)return;
            float amount=CameraAmount;if(amount<=0)return;
            lastCamera=camera;basePosition=camera.transform.position;baseRotation=camera.transform.rotation;baseFov=camera.fieldOfView;
            var settings=Settings;LastCameraZoom=settings.zoomInFov*amount;LastCameraSide=settings.sideways*side*amount;
            camera.transform.position+=camera.transform.TransformDirection(settings.viewOffset)*amount;
            camera.transform.rotation*=Quaternion.Euler(settings.viewEuler*amount);
            camera.transform.position+=camera.transform.right*LastCameraSide+camera.transform.forward*(settings.dolly*amount)+camera.transform.up*(settings.lift*amount);
            camera.transform.rotation*=Quaternion.Euler(0,-side*settings.lookTurn*amount,side*settings.roll*amount);
            float t=age-settings.delay;
            if(t>=0 && t<settings.shakeDuration){
                float shake=settings.shake*Mathf.Pow(1-t/settings.shakeDuration,2);
                camera.transform.rotation*=Quaternion.Euler(Mathf.Sin(t*65)*shake,Mathf.Sin(t*49)*shake,Mathf.Sin(t*77)*shake*.5f);
            }
            camera.fieldOfView=Mathf.Clamp(camera.fieldOfView-LastCameraZoom,25,120);
            finalPosition=camera.transform.position;finalRotation=camera.transform.rotation;finalFov=camera.fieldOfView;cameraApplied=true;
        }
        private void OnDisable(){Cancel();}
    }
}
