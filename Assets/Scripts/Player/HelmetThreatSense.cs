using CyberBiker.Combat;
using Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberBiker.Player {
    // One cached set of animated world-space strokes, attached to the actual
    // head pose. No particle/object spawning or scene searches per shot/frame.
    [DefaultExecutionOrder(14600),DisallowMultipleComponent]
    public sealed class HelmetThreatSense : MonoBehaviour {
        private const int StreakCount=12,PointCount=7;
        private static readonly int Intensity=Shader.PropertyToID("_Intensity");
        private readonly LineRenderer[] streaks=new LineRenderer[StreakCount];
        private readonly Vector3[] points=new Vector3[PointCount];
        private PlayerController player;
        private GameplayManager manager;
        private Collider[] shapes;
        private Transform head,torso;
        private Camera view;
        private MaterialPropertyBlock properties;
        private float amount,clock,pulsePhase;
        public bool Threatened {get;private set;}
        public float NearestThreatDistance {get;private set;}=float.PositiveInfinity;
        public float PulseFrequency {get;private set;}
        public float Urgency {get;private set;}
        public float Amount=>amount;
        public Transform Head=>head;
        public Vector3 Center {get;private set;}
        public bool Visible=>streaks[0]!=null && streaks[0].enabled;
        private void Awake(){
            player=GetComponent<PlayerController>();manager=FindAnyObjectByType<GameplayManager>();
            shapes=GetComponentsInChildren<Collider>(true);view=Camera.main;
            foreach(var bone in GetComponentsInChildren<Transform>(true)){
                if(head==null && (bone.name=="DEF-spine.006" || bone.name=="DEF-head"))head=bone;
                if(bone.name=="DEF-spine.001")torso=bone;
            }
        }
        private void CreateStreaks(){
            properties=new MaterialPropertyBlock();
            var material=Resources.Load<Material>("HelmetDangerSense");
            var taper=new AnimationCurve(new Keyframe(0,0),new Keyframe(.2f,1),new Keyframe(.65f,.65f),new Keyframe(1,0));
            for(int i=0;i<streaks.Length;i++){
                var root=new GameObject("Helmet sense streak "+i);root.transform.SetParent(transform,false);
                var line=root.AddComponent<LineRenderer>();streaks[i]=line;line.sharedMaterial=material;
                line.positionCount=PointCount;line.useWorldSpace=true;line.alignment=LineAlignment.View;line.textureMode=LineTextureMode.Stretch;line.widthCurve=taper;
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;line.enabled=false;
            }
        }
        private bool UnderIncomingPath(){
            NearestThreatDistance=float.PositiveInfinity;
            // Traffic colliders deliberately sit low around the bike. Include
            // the visible rider in warning detection so normal head-height
            // shots still warn; this never adds a physical/damage collider.
            var riderBounds=new Bounds(head!=null ? head.position : transform.position+Vector3.up*3,Vector3.one*.65f);
            if(torso!=null)riderBounds.Encapsulate(new Bounds(torso.position,Vector3.one*.65f));
            riderBounds.Expand(manager.ProjectileCombat.helmetSensePadding*2);
            var paths=EnemyShotTrajectory.InFlight;
            for(int i=0;i<paths.Count;i++){
                var guide=paths[i];var bullet=guide.Projectile;
                if(!guide.Visible || !guide.IsInFlight || bullet==null || !bullet.IsHostile || bullet.IgnoresPlayer(player))continue;
                var start=bullet.transform.position;var segment=guide.EndPoint-start;float distance=segment.magnitude;
                if(distance<.001f)continue;
                var ray=new Ray(start,segment/distance);
                if(riderBounds.Contains(start))NearestThreatDistance=0;
                else if(riderBounds.IntersectRay(ray,out float riderContact) && riderContact<=distance)NearestThreatDistance=Mathf.Min(NearestThreatDistance,riderContact);
                foreach(var shape in shapes){
                    if(shape==null || !shape.enabled || shape.isTrigger || !shape.gameObject.activeInHierarchy)continue;
                    var bounds=shape.bounds;bounds.Expand(manager.ProjectileCombat.helmetSensePadding*2);
                    if(bounds.Contains(start))NearestThreatDistance=0;
                    else if(bounds.IntersectRay(ray,out float contact) && contact<=distance)NearestThreatDistance=Mathf.Min(NearestThreatDistance,contact);
                }
            }
            return !float.IsPositiveInfinity(NearestThreatDistance);
        }
        private void LateUpdate(){
            if(manager==null || player==null || !player.IsAlive || !manager.ProjectileCombat.helmetThreatSense){Threatened=false;amount=0;Hide();return;}
            if(manager.gameState!=GameState.Playing){if(manager.gameState!=GameState.Pause){amount=0;Threatened=false;}Hide();return;}
            var settings=manager.ProjectileCombat;Threatened=UnderIncomingPath();
            float dt=FinisherClock.UnscaledDeltaTime;clock+=dt;
            Urgency=Threatened ? Mathf.SmoothStep(0,1,1-NearestDistanceFraction(settings.helmetSenseUrgencyDistance)) : 0;
            PulseFrequency=Mathf.Lerp(settings.helmetSenseFarPulse,settings.helmetSenseNearPulse,Urgency);
            // Accumulate phase rather than multiplying the clock by a changing
            // speed; approaching bullets accelerate the pulse without jumps.
            pulsePhase=Mathf.Repeat(pulsePhase+dt*PulseFrequency*Mathf.PI*2,Mathf.PI*2);
            amount=Mathf.MoveTowards(amount,Threatened ? 1 : 0,dt/Mathf.Max(.01f,Threatened ? settings.helmetSenseFadeIn : settings.helmetSenseFadeOut));
            if(amount<=0){Hide();return;}
            if(view==null)view=Camera.main;
            if(view==null){Hide();return;}
            if(streaks[0]==null)CreateStreaks();
            Center=(head!=null ? head.position : transform.position+Vector3.up*3)+Vector3.up*settings.helmetSenseHeight;
            var right=view.transform.right;var up=view.transform.up;Center-=view.transform.forward*.08f;
            float fade=amount*amount*(3-2*amount);
            float pulse=.5f+.5f*Mathf.Sin(pulsePhase);
            properties.SetFloat(Intensity,settings.helmetSenseBrightness*(.75f+.25f*pulse)*(1+.2f*Urgency));
            for(int i=0;i<streaks.Length;i++){
                float phase=clock*15+i*2.399963f;
                float angle=Mathf.Deg2Rad*(-20+220f*i/(streaks.Length-1)+Mathf.Sin(phase*.6f)*2);
                var outward=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);var tangent=-right*Mathf.Sin(angle)+up*Mathf.Cos(angle);
                float length=settings.helmetSenseLength*(.9f+.1f*Mathf.Sin(phase))*(1+.15f*Urgency*pulse);
                for(int j=0;j<points.Length;j++){
                    float t=j/(float)(points.Length-1);
                    float wave=Mathf.Sin(t*5-phase)*Mathf.Sin(t*Mathf.PI)*.05f;
                    points[j]=Center+outward*(settings.helmetSenseRadius+length*t)+tangent*wave;
                }
                var line=streaks[i];line.enabled=true;line.SetPositions(points);line.widthMultiplier=settings.helmetSenseWidth*(.85f+.3f*pulse);
                var color=settings.helmetSenseColor;color.a=fade*(.8f+.2f*pulse);line.startColor=line.endColor=color;line.SetPropertyBlock(properties);
            }
        }
        private float NearestDistanceFraction(float range)=>Mathf.Clamp01(NearestThreatDistance/Mathf.Max(.01f,range));
        private void Hide(){foreach(var line in streaks)if(line!=null)line.enabled=false;}
        private void OnDisable(){Threatened=false;amount=0;Hide();}
    }
}
