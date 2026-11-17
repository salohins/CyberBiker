using System.Collections.Generic;
using CyberBiker.Audio;
using Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberBiker.Player {
    [DisallowMultipleComponent]
    public sealed class GapDashAssist:MonoBehaviour {
        private struct Traffic {public Car car;public Bounds bounds;}
        private readonly List<Traffic> nearby=new List<Traffic>(32);
        private readonly RaycastHit[] hits=new RaycastHit[64];
        private Rigidbody body;
        private BoxCollider shape;
        private Collider[] solidShapes;
        private Bounds footprint;
        private float neutralWidth,neutralOffsetX,alignmentDuration,guideDeadline;
        private PlayerController player;
        private GameplayManager manager;
        private BikeSwordCombat sword;
        private BikeImpactResponse impact;
        private Car left,right;
        private float age,clock,nextScan,nextAllowed,startX;
        private float cameraAge=100,returnAge=100,returnAmount,particleBudget;
        private bool active,cameraApplied,previewing;
        private float previewAge;
        private Camera lastCamera;
        private Vector3 basePosition,finalPosition;
        private Quaternion baseRotation,finalRotation;
        private float baseFov,finalFov;
        private ParticleSystem wind;
        private readonly System.Random random=new System.Random(81573);
        public GapDashSettings Settings=>manager.GapDash;
        public bool IsActive=>active;
        public int DashCount {get;private set;}
        public int CompletedCount {get;private set;}
        public int CancelCount {get;private set;}
        public int EmittedParticles {get;private set;}
        public float Age=>age;
        public float TargetX {get;private set;}
        public float GapWidth {get;private set;}
        public float EntrySideOffset {get;private set;}
        public float EntryForwardDistance {get;private set;}
        public string Status {get;private set;}="Approach a gap between two cars.";
        public ParticleSystem Wind=>wind;
        public float RequiredBikeWidth=>neutralWidth;
        public float VisualSteering=>0;
        public float CameraAmount=>!Settings.cameraMotion ? 0 : previewing ? CloseDodgeEffects.Envelope(previewAge,Settings.cameraBlendIn,Mathf.Max(0,Settings.maximumDashTime-Settings.cameraBlendIn),Settings.cameraReturn) : active ? Mathf.SmoothStep(0,1,cameraAge/Settings.cameraBlendIn) : returnAmount*(1-Mathf.SmoothStep(0,1,returnAge/Settings.cameraReturn));
        private bool Finishing=>sword!=null && (sword.TrafficProtected || sword.Phase!=BikeSwordCombat.CombatPhase.Idle && sword.Phase!=BikeSwordCombat.CombatPhase.Pursuing);
        private void Awake(){player=GetComponent<PlayerController>();body=GetComponent<Rigidbody>();shape=GetComponent<BoxCollider>();solidShapes=GetComponentsInChildren<Collider>(true);sword=GetComponent<BikeSwordCombat>();impact=GetComponent<BikeImpactResponse>();manager=FindAnyObjectByType<GameplayManager>();manager?.BindGapDash(this);var upright=BodyBounds();neutralWidth=upright.size.x;neutralOffsetX=upright.center.x-body.position.x;}
        // Launch is required to acquire a gap. Once committed, finishing the
        // solid-car passage must survive energy exhaustion or manual cancellation.
        // Combat, collisions, disabled settings and blocked paths still cancel.
        private bool Available=>isActiveAndEnabled && manager!=null && player!=null && player.IsAlive && shape!=null && body!=null && !body.isKinematic && Settings.enabled && sword!=null && (active || sword.IsPursuing) && manager.gameState==GameState.Playing && !Finishing && (impact==null || !impact.IsRecovering) && (manager.DodgeEffects==null || !manager.DodgeEffects.IsActive) && manager.CombatSlowMotionRemaining<=0;
        private void Update(){
            if(manager==null)return;if(manager.gameState==GameState.Pause){if(wind!=null && wind.isPlaying)wind.Pause();return;}
            if(!Available){Cancel("Assist yielded to combat or collision.");returnAmount=0;cameraAge=returnAge=100;if(wind!=null)wind.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return;}
            float dt=Time.unscaledDeltaTime;cameraAge+=dt;returnAge+=dt;
            if(wind!=null && wind.isPaused)wind.Play();
            if(active && Settings.particles)EmitWind(dt);
        }
        // Called before normal motor forces; only sideways guidance is changed.
        public bool Step(float steering){
            previewing=false;float dt=Time.fixedDeltaTime;clock+=dt;
            if(!Available){Cancel(sword==null || !sword.IsPursuing ? "Gap assist requires Launch mode." : "Assist unavailable.");return false;}
            footprint=BodyBounds();
            if(!active){
                if(clock<nextAllowed || clock<nextScan)return false;nextScan=clock+.02f;
                if(!player.grounded || body.linearVelocity.z<Settings.minimumSpeed)return false;
                if(!FindGap(steering,out var a,out var b))return false;
                Begin(a,b);
            }
            if(!Pair(left,right,out var lb,out var rb,out float center)){Cancel("Gap closed or traffic disappeared.");return false;}
            TargetX=center;GapWidth=rb.min.x-lb.max.x;
            // Once committed, the assist owns lateral movement until the whole
            // bike clears the cars. Held or reversed input cannot cancel it.
            age+=dt;
            if(age>=alignmentDuration && footprint.min.z>Mathf.Max(lb.max.z,rb.max.z)+Settings.exitClearance){CompletedCount++;CyberBiker.Scoring.StyleScoreController.Award(CyberBiker.Scoring.StyleAction.GapPass,transform.position+Vector3.up*2);Finish("Gap cleared.");return false;}
            if(age>guideDeadline){Cancel("Dash time limit reached.");return false;}
            float t=Mathf.Clamp01(age/alignmentDuration);float smooth=t*t*t*(t*(6*t-15)+10);
            float nextX=Mathf.Lerp(startX,center,smooth);
            float forward=Mathf.Max(body.linearVelocity.z*dt,1);
            if(!ClearPath(footprint.center,footprint.center+new Vector3(nextX-body.position.x,0,forward),footprint.extents)){Cancel("Traffic blocked the passage.");return false;}
            var v=body.linearVelocity;v.x=(nextX-body.position.x)/Mathf.Max(.001f,dt);body.linearVelocity=v;
            return true;
        }
        private bool FindGap(float steering,out Car a,out Car b){
            a=b=null;nearby.Clear();float best=float.PositiveInfinity;var origin=footprint;
            float scanDistance=Settings.maximumTriggerDistance;
            var traffic=Car.Instances;
            for(int i=0;i<traffic.Count;i++){var car=traffic[i];if(car==null || !car.TryGetTrafficShape(0,out var bounds,out _))continue;if(bounds.max.z<origin.min.z || bounds.min.z>origin.max.z+scanDistance+12 || Mathf.Abs(bounds.center.x-origin.center.x)>12)continue;nearby.Add(new Traffic{car=car,bounds=bounds});}
            for(int i=0;i<nearby.Count;i++)for(int j=i+1;j<nearby.Count;j++){
                var l=nearby[i];var r=nearby[j];if(l.bounds.center.x>r.bounds.center.x){var swap=l;l=r;r=swap;}
                if(!Pair(l.car,r.car,out var lb,out var rb,out float center))continue;
                float distance=Mathf.Min(lb.min.z,rb.min.z)-origin.max.z;
                float closing=body.linearVelocity.z-Mathf.Max(l.car.enabled ? l.car.speed : 0,r.car.enabled ? r.car.speed : 0);
                float delta=center-body.position.x;
                float predictionTime=Mathf.Min(Settings.approachTime,Mathf.Max(0,distance/Mathf.Max(3,closing)));
                float predictedDelta=delta-body.linearVelocity.x*predictionTime;
                bool movingIn=Settings.predictiveSideEntry && delta*body.linearVelocity.x>.05f && Mathf.Abs(body.linearVelocity.x)>.5f;
                bool sideEntry=movingIn && (Mathf.Abs(predictedDelta)<=Settings.snapRadius || delta*predictedDelta<=0);
                // Countersteering may be braking an inward slide. Judge its
                // actual trajectory before treating input as leaving the gap.
                bool outsideRadius=Mathf.Abs(delta)>Settings.snapRadius;
                bool steeringAway=Mathf.Abs(steering)>.55f && steering*delta<-.25f;
                if((outsideRadius || steeringAway) && !sideEntry)continue;
                float duration=AlignmentDuration(delta);
                float alignLead=Mathf.Abs(delta)>.1f ? closing*duration+1 : .5f;
                float triggerDistance=Mathf.Min(Settings.maximumTriggerDistance,Mathf.Max(closing*Settings.approachTime,alignLead+closing*.15f));
                // Prediction can qualify a sideways approach, but cannot move
                // the user's forward limit or seize steering farther away.
                // A sideways correction must finish before the bumpers. Only
                // already-clear bikes may engage while entering the corridor.
                bool alreadyInside=origin.min.x>lb.max.x+Settings.sideClearance && origin.max.x<rb.min.x-Settings.sideClearance;
                float latest=alreadyInside ? .1f : alignLead;
                if(closing<=3 || distance>triggerDistance)continue;
                if(distance<latest){Status="Too late to centre before the bumpers.";continue;}
                float alignDistance=Mathf.Max(1,closing*duration);
                var align=origin.center+new Vector3(delta,0,alignDistance);
                var corridor=align;corridor.x=center+neutralOffsetX;var exit=corridor;exit.z=Mathf.Max(lb.max.z,rb.max.z)+origin.extents.z+Settings.exitClearance;
                var uprightHalf=origin.extents;uprightHalf.x=neutralWidth*.5f;
                if(!ClearPath(origin.center,align,origin.extents) || !ClearPath(corridor,exit,uprightHalf))continue;
                float score=Mathf.Abs(delta)*5+distance*.1f;if(score>=best)continue;best=score;a=l.car;b=r.car;
            }
            return a!=null;
        }
        private bool Pair(Car a,Car b,out Bounds lb,out Bounds rb,out float center){
            center=0;lb=rb=default;if(a==null || b==null || !a.TryGetTrafficShape(0,out lb,out _) || !b.TryGetTrafficShape(0,out rb,out _))return false;
            float gap=rb.min.x-lb.max.x;
            if(gap<neutralWidth+Settings.sideClearance*2){if(!active && gap>0){GapWidth=gap;Status="Gap too narrow for the full bike ("+gap.ToString("0.00")+" m).";}return false;}
            if(gap>Settings.maximumGapWidth || Mathf.Min(lb.max.z,rb.max.z)-Mathf.Max(lb.min.z,rb.min.z)<1)return false;
            center=(lb.max.x+rb.min.x)*.5f-neutralOffsetX;return true;
        }
        private float AlignmentDuration(float delta){
            float straighten=footprint.size.x>neutralWidth+.03f ? .2f : 0;
            return Mathf.Max(straighten,Mathf.Max(Settings.alignmentTime,1.875f*Mathf.Abs(delta)/Settings.maximumAlignmentSpeed));
        }
        private bool ClearPath(Vector3 from,Vector3 to,Vector3 extents){
            var delta=to-from;float distance=delta.magnitude;if(distance<.001f)return true;
            var half=extents+new Vector3(Settings.sideClearance,0,.04f);
            int count=Physics.BoxCastNonAlloc(from,half,delta/distance,hits,Quaternion.identity,distance,LayerMask.GetMask("WorldObject","Default"),QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++){var c=hits[i].collider;if(c==null || c.transform.IsChildOf(transform))continue;if(c.GetComponentInParent<Car>()!=null || c.GetComponentInParent<Enemy>()!=null || c.gameObject.layer==LayerMask.NameToLayer("WorldObject"))return false;}
            return true;
        }
        private Bounds BodyBounds(){
            var result=new Bounds(body.position,Vector3.zero);bool first=true;
            foreach(var c in solidShapes){
                if(c==null || !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy)continue;
                var bounds=c.bounds;
                if(c is BoxCollider box){var t=box.transform;var half=box.size*.5f;var x=t.TransformVector(Vector3.right*half.x);var y=t.TransformVector(Vector3.up*half.y);var z=t.TransformVector(Vector3.forward*half.z);bounds=new Bounds(t.TransformPoint(box.center),new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z))*2);}
                if(first){result=bounds;first=false;}else result.Encapsulate(bounds);
            }
            return result;
        }
        private void Begin(Car a,Car b){left=a;right=b;active=true;age=0;cameraAge=0;returnAge=100;returnAmount=0;startX=body.position.x;
            Pair(a,b,out var lb,out var rb,out float target);EntrySideOffset=target-startX;EntryForwardDistance=Mathf.Min(lb.min.z,rb.min.z)-footprint.max.z;alignmentDuration=AlignmentDuration(EntrySideOffset);
            float closing=Mathf.Max(3,body.linearVelocity.z-Mathf.Max(a.enabled ? a.speed : 0,b.enabled ? b.speed : 0));
            guideDeadline=Mathf.Max(Settings.maximumDashTime,Mathf.Min(8,(Mathf.Max(lb.max.z,rb.max.z)+Settings.exitClearance-footprint.min.z)/closing+.4f));
            DashCount++;Status="Guiding through the gap at riding speed.";EnsureWind();
            var profile=manager.GapDashProfile;if(profile!=null && profile.whoosh!=null)GameAudio.Active?.PlaySwordSound(profile.whoosh,transform.position,transform,Settings.whooshVolume,1.05f);
        }
        private void Finish(string status){if(!active)return;returnAmount=CameraAmount;active=false;returnAge=0;nextAllowed=clock+Settings.cooldown;Status=status;left=right=null;}
        public void Cancel(string reason="Cancelled."){if(active){CancelCount++;Finish(reason);}RemoveCamera(lastCamera);}
        private void EnsureWind(){
            if(wind!=null || manager.GapDashProfile==null || manager.GapDashProfile.windMaterial==null)return;
            var go=new GameObject("Gap dash airflow");go.transform.SetParent(transform,false);wind=go.AddComponent<ParticleSystem>();wind.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=wind.main;main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Shape;main.maxParticles=512;main.startSize3D=true;main.startSpeed=0;main.gravityModifier=0;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=wind.emission;emission.enabled=false;var sh=wind.shape;sh.enabled=false;
            var fade=wind.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.8f,.65f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=wind.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=1;renderer.velocityScale=.02f;renderer.sharedMaterial=manager.GapDashProfile.windMaterial;renderer.maxParticleSize=.18f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;wind.Play();
        }
        private void EmitWind(float dt){
            EnsureWind();if(wind==null)return;particleBudget+=dt*Settings.emission;int n=Mathf.Min(80,Mathf.FloorToInt(particleBudget));particleBudget-=n;
            for(int i=0;i<n;i++){
                float angle=(float)random.NextDouble()*Mathf.PI*2;float radius=Settings.particleRadius*(.8f+(float)random.NextDouble()*.25f);
                var p=new ParticleSystem.EmitParams{position=new Vector3(Mathf.Cos(angle)*radius,.95f+Mathf.Sin(angle)*radius*.65f,-.4f-(float)random.NextDouble()*1.2f),velocity=new Vector3(Mathf.Cos(angle)*.2f,0,-Settings.particleSpeed),startLifetime=Settings.particleLifetime,startSize3D=new Vector3(Settings.particleWidth,Settings.particleLength,Settings.particleWidth),startColor=Settings.particleColor,randomSeed=(uint)(EmittedParticles+1)};wind.Emit(p,1);EmittedParticles++;
            }
        }
        public void RemoveCamera(Camera view){if(!cameraApplied || view==null)return;if((view.transform.position-finalPosition).sqrMagnitude<.000001f)view.transform.position=basePosition;if(Quaternion.Angle(view.transform.rotation,finalRotation)<.001f)view.transform.rotation=baseRotation;if(Mathf.Abs(view.fieldOfView-finalFov)<.0001f)view.fieldOfView=baseFov;cameraApplied=false;}
        public void ApplyCamera(Camera view){
            if(view==null || manager==null || manager.gameState!=GameState.Playing)return;float amount=CameraAmount;if(amount<=.0001f)return;
            lastCamera=view;basePosition=view.transform.position;baseRotation=view.transform.rotation;baseFov=view.fieldOfView;
            float kick=Mathf.Sin(Mathf.Clamp01(cameraAge/.16f)*Mathf.PI)*Settings.cameraKick;
            view.transform.position+=view.transform.TransformDirection(Settings.cameraOffset*amount+Vector3.back*kick);
            view.transform.rotation*=Quaternion.Euler(Settings.cameraEuler*amount);
            float shake=Settings.shake*Mathf.Pow(1-Mathf.Clamp01(cameraAge/Settings.shakeDuration),2);
            view.transform.rotation*=Quaternion.Euler(Mathf.Sin(cameraAge*52)*shake,Mathf.Sin(cameraAge*43)*shake*.5f,Mathf.Sin(cameraAge*63)*shake*.5f);
            view.fieldOfView=Mathf.Clamp(view.fieldOfView-Settings.zoomInFov*amount,25,120);
            finalPosition=view.transform.position;finalRotation=view.transform.rotation;finalFov=view.fieldOfView;cameraApplied=true;
        }
#if UNITY_EDITOR
        public void ResetPreview(){Cancel();previewing=false;clock=nextAllowed=nextScan=0;cameraAge=returnAge=100;returnAmount=0;DashCount=CompletedCount=CancelCount=EmittedParticles=0;if(wind!=null)wind.Clear();}
        public void RefreshPreviewParticles(){
            if(manager==null)return;EnsureWind();if(wind==null)return;wind.Clear();
            bool visible=previewing ? previewAge>0 && previewAge<Settings.maximumDashTime+Settings.particleLifetime : active || returnAge<Settings.particleLifetime;
            if(Settings.particles && visible){EmitWind(.1f);wind.Simulate(Settings.particleLifetime*.35f,true,false,true);wind.Pause();}
        }
        public void PreviewEffect(float seconds){if(manager==null)return;previewing=true;previewAge=cameraAge=Mathf.Max(0,seconds);RefreshPreviewParticles();Status="Scrubbing camera and airflow; Replay tests the physical passage.";}
#endif
        private void OnDisable(){Cancel();returnAmount=0;cameraAge=returnAge=100;if(wind!=null)wind.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        private void OnDestroy(){if(wind!=null)Destroy(wind.gameObject);}
    }
}
