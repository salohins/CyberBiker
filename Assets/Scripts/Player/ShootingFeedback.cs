using Gameplay;
using UnityEngine;
using UnityEngine.UI;
using CyberBiker.Audio;

namespace CyberBiker.Player {
    [DisallowMultipleComponent]
    public sealed class ShootingFeedback : MonoBehaviour {
        public static ShootingFeedback Active {get;private set;}
        [Header("Pistol camera impulse")]
        [SerializeField,Range(0,3)] private float shotKickDegrees=.9f;
        [SerializeField,Range(0,.15f)] private float shotCameraKick=.045f;
        [SerializeField,Range(0,3)] private float shotFovKick=.65f;
        [SerializeField,Range(4,30)] private float shotRecovery=15;
        [Header("Confirmed hit markers")]
        [SerializeField,Range(.08f,.5f)] private float hitMarkerDuration=.24f;
        [SerializeField,Range(6,24)] private float hitMarkerSize=14;
        [SerializeField] private Color hitMarkerColor=new Color(.9f,1,1,1);
        [SerializeField] private Color finisherMarkerColor=new Color(1,.58f,.12f,1);
        [Header("Incoming damage")]
        [SerializeField,Range(.1f,.8f)] private float damageDuration=.38f;
        [SerializeField,Range(0,1)] private float damageScreenStrength=.55f;
        [SerializeField,Range(0,2)] private float damageCameraKick=.7f;
        [SerializeField] private Material screenMaterial;
        [Header("Projectile near misses")]
        [Tooltip("Distance from the rider torso that counts as a dodged hostile projectile. Each projectile gives feedback once; separate bullets never suppress each other.")]
        [SerializeField,Range(.5f,6)] private float nearMissRadius=3.2f;
        [SerializeField,Range(.5f,4)] private float nearMissCenterHeight=1.6f;
        public float NearMissRadius {get=>nearMissRadius;set=>nearMissRadius=Mathf.Clamp(value,.5f,6);}
        public float NearMissCenterHeight {get=>nearMissCenterHeight;set=>nearMissCenterHeight=Mathf.Clamp(value,.5f,4);}
        [SerializeField,Range(0,1)] private float nearMissScreenStrength=.23f;
        [SerializeField] private Material nearMissMaterial;
        private sealed class Marker {public RectTransform root;public CanvasGroup group;public Image[] strokes;public Transform target;public Vector3 localPoint,worldPoint;public float time;public bool stagger;}
        private readonly Marker[] markers=new Marker[8];
        private int nextMarker,shotSequence;
        private PlayerController player;private Rigidbody body;private GameplayManager manager;private Camera view;
        private PlayerProjectileEvade evade;
        private Canvas hud;private RectTransform hudRect;private CombatDamageScreen screen;private ParticleSystem slipstream;
        private float recoil,hurtTime,hurtPeak,missTime,missSide;
        private Vector2 damageDirection;
        private bool cameraApplied;private Vector3 basePosition,finalPosition;private Quaternion baseRotation,finalRotation;private float baseFov,finalFov;
        public int ShotCount {get;private set;} public int ConfirmedHitCount {get;private set;} public int DamageCount {get;private set;} public int NearMissCount {get;private set;}
        public float DamageAmount=>hurtTime>0 ? hurtPeak*Mathf.Pow(hurtTime/Mathf.Max(.01f,damageDuration),1.3f) : 0;
        public float RecoilAmount=>recoil;
        public int LiveMarkerCount {get {int count=0;foreach(var marker in markers)if(marker!=null && marker.time>0)count++;return count;}}
        public Vector3 NearMissCenter=>transform.position+Vector3.up*nearMissCenterHeight;
        private bool Playing=>manager!=null && (manager.gameState==GameState.Playing || manager.gameState==GameState.Hit);
        private void Awake(){player=GetComponent<PlayerController>();body=GetComponent<Rigidbody>();manager=FindAnyObjectByType<GameplayManager>();BuildHud();BuildSlipstream();}
        private void OnEnable(){Active=this;}
        private void OnDisable(){if(Active==this)Active=null;RemoveCamera(view);Clear();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetStatic(){Active=null;}
        private void Update(){
            if(!Playing || Time.deltaTime<=0)return;float dt=Time.deltaTime;
            recoil*=Mathf.Exp(-shotRecovery*dt);if(recoil<.001f)recoil=0;
            hurtTime=Mathf.Max(0,hurtTime-dt);missTime=Mathf.Max(0,missTime-dt);
            foreach(var marker in markers)if(marker.time>0)marker.time=Mathf.Max(0,marker.time-dt);
        }
        public void Shot(){if(!isActiveAndEnabled || !Playing)return;recoil=Mathf.Min(1.8f,recoil+1);shotSequence++;ShotCount++;}
        public void ConfirmHit(Transform target,Vector3 point,Vector3 normal,bool finisherReady){
            if(!isActiveAndEnabled || !Playing)return;var marker=markers[nextMarker];nextMarker=(nextMarker+1)%markers.Length;
            marker.target=target;marker.worldPoint=point;marker.localPoint=target!=null ? target.InverseTransformPoint(point) : point;marker.time=hitMarkerDuration;marker.stagger=finisherReady;
            ConfirmedHitCount++;SoundEvents.Emit(SoundCue.PistolHitConfirm,point);
            if(target!=null && target.GetComponent<Enemy>()!=null)CyberBiker.Scoring.StyleScoreController.Award(CyberBiker.Scoring.StyleAction.PistolHit,point);
        }
        public void Hurt(float damage,GameObject source){
            if(!isActiveAndEnabled || damage<=0 || manager==null || manager.gameState==GameState.Pause)return;
            CyberBiker.Scoring.StyleScoreController.BankOnImpact();
            hurtPeak=Mathf.Max(DamageAmount,Mathf.Clamp(damage/30,.35f,1));hurtTime=damageDuration;DamageCount++;
            var camera=view!=null ? view : Camera.main;var direction=source!=null ? source.transform.position-transform.position : -transform.forward;
            if(camera!=null){var local=camera.transform.InverseTransformDirection(direction.normalized);damageDirection=new Vector2(local.x,local.z>=0 ? .6f : -.6f).normalized;}
            SoundEvents.Emit(SoundCue.PlayerHurt,transform.position);
        }
        // Returns true once the closest point has been passed, even if it was
        // too far away. This projectile must never report a second near miss.
        public bool CheckNearMiss(Vector3 start,Vector3 end,float dt,Bullet shot=null){
            if(!isActiveAndEnabled || !Playing || player==null || !player.IsAlive || dt<=0)return false;
            var velocity=body!=null ? body.linearVelocity : Vector3.zero;
            if(shot!=null && shot.FollowsRider)velocity.z=0;
            var movement=velocity*dt;
            var center=NearMissCenter;
            var a=start-(center-movement);var b=end-center;var segment=b-a;float length=segment.sqrMagnitude;
            if(length<.000001f)return false;float t=-Vector3.Dot(a,segment)/length;if(t<0 || t>1)return false;
            var separation=Vector3.Lerp(a,b,t);if(separation.sqrMagnitude>nearMissRadius*nearMissRadius)return true;
            NearMissCount++;missTime=.22f;
            CyberBiker.Scoring.StyleScoreController.Award(CyberBiker.Scoring.StyleAction.BulletDodge,Vector3.Lerp(start,end,t));
            var point=Vector3.Lerp(start,end,t);var camera=view!=null ? view : Camera.main;
            missSide=camera!=null ? Mathf.Sign(Vector3.Dot(point-center,camera.transform.right)) : Mathf.Sign(separation.x);if(missSide==0)missSide=1;
            SoundEvents.Emit(SoundCue.BulletNearMiss,point);
            if(slipstream!=null){
                if(!slipstream.isPlaying)slipstream.Play();
                for(int i=0;i<4;i++){var emit=new ParticleSystem.EmitParams{position=point+Vector3.up*(i-.5f)*.04f,velocity=(body!=null ? body.linearVelocity*.82f : Vector3.zero)+transform.right*missSide*(3+i),startLifetime=.12f+i*.018f,startSize3D=new Vector3(.025f,.25f,.025f),startColor=new Color(.3f,.8f,1,.65f),rotation=20+i*13,randomSeed=(uint)(417+i+NearMissCount*7)};slipstream.Emit(emit,1);}
            }
            return true;
        }
        // Separate retirement from the sound check: the cinematic can use a
        // different radius and torso height without losing its passing sample.
        public bool CheckCloseDodge(Vector3 start,Vector3 end,float dt,Bullet shot){
            if(!isActiveAndEnabled || !Playing || player==null || !player.IsAlive || dt<=0 || shot==null || !shot.IsHostile)return false;
            var dodge=manager.DodgeEffects;if(dodge==null)return true;
            var velocity=body!=null ? body.linearVelocity : Vector3.zero;if(shot.FollowsRider)velocity.z=0;
            var a=start-(dodge.Center-velocity*dt);var b=end-dodge.Center;var travel=b-a;float span=travel.sqrMagnitude;
            if(span<.000001f)return false;float closest=-Vector3.Dot(a,travel)/span;
            if(closest<0)return true;if(closest>1)return false;
            dodge.TryClosePass(Vector3.Lerp(a,b,closest).magnitude,Vector3.Lerp(start,end,closest));return true;
        }
        public void RemoveCamera(Camera camera){
            if(!cameraApplied || camera==null)return;
            if((camera.transform.position-finalPosition).sqrMagnitude<.000001f)camera.transform.position=basePosition;
            if(Quaternion.Angle(camera.transform.rotation,finalRotation)<.001f)camera.transform.rotation=baseRotation;
            if(Mathf.Abs(camera.fieldOfView-finalFov)<.0001f)camera.fieldOfView=baseFov;
            cameraApplied=false;
        }
        public void ApplyCamera(Camera camera){
            if(camera==null)return;view=camera;
            if(screen==null){screen=camera.GetComponent<CombatDamageScreen>() ?? camera.gameObject.AddComponent<CombatDamageScreen>();}
            float damage=Playing ? DamageAmount : 0;float miss=Playing ? missTime/.22f : 0;float kick=Playing ? recoil : 0;
            if(evade==null)evade=GetComponent<PlayerProjectileEvade>();
            float dodgeRoll=Playing && evade!=null ? evade.PoseAmount*evade.Settings.cameraRoll : 0;
            float dodgeFov=Playing && evade!=null && evade.LastPerfect && !manager.CloseDodge.enabled ? evade.FeedbackAmount*evade.Settings.perfectFovKick : 0;
            basePosition=camera.transform.position;baseRotation=camera.transform.rotation;baseFov=camera.fieldOfView;
            float vibration=Mathf.Sin(Time.time*81)*damage;
            camera.transform.position+=camera.transform.forward*(-shotCameraKick*kick)+camera.transform.right*(vibration*.025f);
            camera.transform.rotation*=Quaternion.Euler(-shotKickDegrees*kick+vibration*damageCameraKick,.13f*kick*(shotSequence%2==0 ? 1 : -1),damage*.45f*damageDirection.x+miss*missSide*.12f+dodgeRoll);
            camera.fieldOfView=Mathf.Clamp(camera.fieldOfView-shotFovKick*kick+dodgeFov,20,130);
            finalPosition=camera.transform.position;finalRotation=camera.transform.rotation;finalFov=camera.fieldOfView;cameraApplied=true;
            screen.Configure(screenMaterial,damage*damageScreenStrength,miss*nearMissScreenStrength,damageDirection,missSide);
            DrawMarkers(camera);
        }
        private void BuildHud(){
            var go=new GameObject("Combat feedback HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));hud=go.GetComponent<Canvas>();hud.renderMode=RenderMode.ScreenSpaceOverlay;hud.sortingOrder=60;hudRect=go.GetComponent<RectTransform>();
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            for(int i=0;i<markers.Length;i++){
                var root=new GameObject("Confirmed hit "+i,typeof(RectTransform),typeof(CanvasGroup));root.transform.SetParent(go.transform,false);var marker=new Marker{root=root.GetComponent<RectTransform>(),group=root.GetComponent<CanvasGroup>(),strokes=new Image[4]};marker.group.blocksRaycasts=false;marker.group.interactable=false;marker.group.alpha=0;
                for(int j=0;j<4;j++){var stroke=new GameObject("Stroke",typeof(RectTransform),typeof(Image),typeof(Outline));stroke.transform.SetParent(root.transform,false);var image=stroke.GetComponent<Image>();image.raycastTarget=false;var outline=stroke.GetComponent<Outline>();outline.effectColor=new Color(.015f,.02f,.03f,.9f);outline.effectDistance=new Vector2(1,-1);var rt=image.rectTransform;float angle=45+j*90;rt.anchoredPosition=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad))*hitMarkerSize;rt.sizeDelta=new Vector2(hitMarkerSize*.65f,3);rt.localRotation=Quaternion.Euler(0,0,angle);marker.strokes[j]=image;}
                markers[i]=marker;
            }
        }
        private void DrawMarkers(Camera camera){
            if(hud==null)return;hud.enabled=Playing && LiveMarkerCount>0;
            foreach(var marker in markers){
                if(marker.time<=0 || !Playing){marker.group.alpha=0;continue;}
                if(marker.target!=null)marker.worldPoint=marker.target.TransformPoint(marker.localPoint);
                var point=camera.WorldToViewportPoint(marker.worldPoint);
                if(point.z<=0 || point.x<0 || point.x>1 || point.y<0 || point.y>1){marker.group.alpha=0;continue;}
                float progress=1-marker.time/Mathf.Max(.01f,hitMarkerDuration);marker.group.alpha=Mathf.Min(1,marker.time/.1f);
                marker.root.anchoredPosition=new Vector2((point.x-.5f)*hudRect.rect.width,(point.y-.5f)*hudRect.rect.height);
                marker.root.localScale=Vector3.one*(1+Mathf.Exp(-progress*14)*.3f)*(marker.stagger ? 1.2f : 1);
                foreach(var image in marker.strokes)image.color=marker.stagger ? finisherMarkerColor : hitMarkerColor;
            }
        }
        private void BuildSlipstream(){
            var go=new GameObject("Bullet close-pass streaks");go.transform.SetParent(transform,false);slipstream=go.AddComponent<ParticleSystem>();slipstream.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);slipstream.useAutoRandomSeed=false;slipstream.randomSeed=76533;
            var main=slipstream.main;main.playOnAwake=false;main.loop=false;main.maxParticles=32;main.startLifetime=.18f;main.startSpeed=0;main.startSize3D=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=false;
            var emission=slipstream.emission;emission.enabled=false;var shape=slipstream.shape;shape.enabled=false;
            var color=slipstream.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=nearMissMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.maxParticleSize=.04f;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;slipstream.Play();
        }
        private void Clear(){recoil=hurtTime=missTime=0;if(hud!=null)hud.enabled=false;foreach(var marker in markers)if(marker!=null)marker.time=0;if(screen!=null)screen.Clear();if(slipstream!=null)slipstream.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        private void OnDestroy(){RemoveCamera(view);if(hud!=null)Destroy(hud.gameObject);if(screen!=null)Destroy(screen);}
    }
}
