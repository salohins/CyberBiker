using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections.Generic;

// Apply after the Animator/RigBuilder, then remove before the next animation
// evaluation. Even a culled Animator cannot accumulate this visual offset.
[DefaultExecutionOrder(10000), DisallowMultipleComponent]
public sealed class EnemyHitReaction : MonoBehaviour {
    [Header("Pistol body reaction")]
    [SerializeField] private Transform torso;
    [SerializeField, Range(0,30)] private float joltDegrees = 20;
    [SerializeField, Range(0,15)] private float sideFlinchDegrees = 10;
    [Tooltip("Visual upper-body displacement in world metres. The bike and navigation root stay on their original path.")]
    [SerializeField, Range(0,.3f)] private float torsoKickDistance = .2f;
    [SerializeField, Range(.08f,.5f)] private float recoveryTime = .4f;
    [Header("Bullet contact sparks")]
    [SerializeField] private Material sparkMaterial;
    [SerializeField, Range(0,64)] private int sparksPerHit = 40;
    [SerializeField, InspectorName("Spark Width"), Range(.02f,.12f)] private float sparkSize = .065f;
    [SerializeField, Range(.15f,1)] private float sparkLength = .55f;
    [SerializeField, Range(1,15)] private float sparkSpeed = 12;
    private Enemy enemy;
    private ParticleSystem sparks;
    private Camera view;
    private float elapsed = 1;
    private Vector3 startAngles,targetAngles,currentAngles,startKick,targetKick,currentKick;
    private Vector3 basePosition,appliedPosition;
    private sealed class Attachment {public Transform root,target;public Vector3 localPosition,appliedPosition,worldPosition,targetPosition;public Quaternion localRotation,appliedRotation,worldRotation,targetRotation;public bool applied;}
    private readonly List<Attachment> attachments=new List<Attachment>(2);
    private bool applied;
    private Quaternion baseRotation, appliedRotation;
    public int HitCount { get; private set; }
    public Vector3 LastContactPoint { get; private set; }
    public float AppliedJoltDegrees { get; private set; }
    public float AppliedKickDistance { get; private set; }
    public Transform Torso => torso;
    public ParticleSystem Sparks => sparks;

    private void Awake() {
        enemy = GetComponent<Enemy>();
        view=Camera.main;
        if(torso==null) {
            foreach(var bone in GetComponentsInChildren<Transform>(true))
                if(bone.name=="DEF-spine.002") { torso=bone; break; }
        }
        // The two gun meshes are constrained siblings of the rider skeleton.
        // Carry them with the hand targets during the additive recoil, preserving
        // their authored offsets instead of letting the hands leave the guns.
        foreach(var constraint in GetComponentsInChildren<MultiParentConstraint>(true)) {
            var data=constraint.data;var root=data.constrainedObject;
            if(root==null || !root.name.StartsWith("Gun") || data.sourceObjects.Count!=1 || (torso!=null && root.IsChildOf(torso)))continue;
            var target=data.sourceObjects[0].transform;if(target!=null)attachments.Add(new Attachment{root=root,target=target});
        }
        if(sparkMaterial==null)sparkMaterial=Resources.Load<Material>("EnemyBulletSparks");
        BuildSparks();
    }

    private void OnEnable(){Camera.onPreCull+=ApplyRenderPose;}
    private void ApplyRenderPose(Camera camera){
        if(!applied || !isActiveAndEnabled || torso==null || enemy==null || !enemy.IsAlive || camera.cameraType!=CameraType.Game)return;
        // Match the aiming system's render-pose protection: animation/rig
        // evaluation must not erase this frame's visible additive reaction.
        CaptureAttachments();
        torso.SetLocalPositionAndRotation(appliedPosition,appliedRotation);
        CarryAttachments();
    }

    public void React(Vector3 point, Vector3 normal, Vector3 direction) {
        if(!isActiveAndEnabled || enemy==null || !enemy.IsAlive)return;
        HitCount++; LastContactPoint=point;
        startAngles=currentAngles;startKick=currentKick;elapsed=0;
        direction=direction.sqrMagnitude>.001f ? direction.normalized : transform.forward;
        var localDirection=transform.InverseTransformDirection(direction);
        float side=Mathf.Clamp(Vector3.Dot(point-(torso!=null ? torso.position : transform.position),transform.right),-1,1);
        float flinchSide=Mathf.Abs(side)>.18f ? Mathf.Sign(side) : (HitCount%2==0 ? -1 : 1);
        targetAngles=new Vector3(localDirection.z*joltDegrees,-side*joltDegrees*.25f,
            -localDirection.x*joltDegrees*.55f+flinchSide*sideFlinchDegrees);
        targetKick=new Vector3(localDirection.x*.35f-flinchSide*.55f,-.75f,localDirection.z*.6f)*torsoKickDistance;
        normal=normal.sqrMagnitude>.001f ? normal.normalized : -direction;
        if(!sparks.isPlaying)sparks.Play();
        var tangent=Vector3.Cross(normal,Mathf.Abs(normal.y)>.9f ? Vector3.right : Vector3.up).normalized;
        var bitangent=Vector3.Cross(normal,tangent);
        if(view==null)view=Camera.main;
        // Keep thin streaks readable at fighting distance, using a 720p reference.
        // This changes the stroke dimensions, never its light/bloom intensity.
        float pixelSize=view!=null ? Vector3.Distance(view.transform.position,point)*2*Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f)/720f : 0;
        float width=Mathf.Max(sparkSize,pixelSize*3.2f);
        float length=Mathf.Max(sparkLength,pixelSize*16);
        var scale=transform.lossyScale;
        var inverseScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(scale.x)),1/Mathf.Max(.001f,Mathf.Abs(scale.y)),1/Mathf.Max(.001f,Mathf.Abs(scale.z)));
        // Keep the short spray in the moving rider frame. World-space
        // inheritance drifts away as steering changes at high riding speeds.
        for(int i=0;i<sparksPerHit;i++) {
            float angle=(i*2.399963f+HitCount*.73f);
            float spread=.75f+.65f*((i*7%11)/10f);
            var outward=(normal*.65f+tangent*Mathf.Cos(angle)*spread+bitangent*Mathf.Sin(angle)*spread).normalized;
            float rotation=view!=null ? -Mathf.Atan2(Vector3.Dot(outward,view.transform.right),Vector3.Dot(outward,view.transform.up))*Mathf.Rad2Deg : angle*Mathf.Rad2Deg;
            var emit=new ParticleSystem.EmitParams {
                position=transform.InverseTransformPoint(point),
                velocity=transform.InverseTransformVector(outward*sparkSpeed*(.55f+(i%5)*.14f)),
                startLifetime=.26f+(i%4)*.035f,
                startSize3D=Vector3.Scale(new Vector3(width*(.8f+(i%3)*.15f),length*(.65f+(i%5)*.14f),width),inverseScale),
                rotation=rotation,
                startColor=i%3==0 ? new Color(1,.96f,.78f,1) : new Color(1,.72f,.24f,1),
                randomSeed=(uint)(1253+HitCount*31+i)
            };
            sparks.Emit(emit,1);
        }
    }

    private void Update() {
        RemoveOffset();
        if(Time.deltaTime>0)elapsed=Mathf.Min(recoveryTime,elapsed+Time.deltaTime);
    }
    private void LateUpdate() {
        if(torso==null || enemy==null || !enemy.IsAlive || elapsed>=recoveryTime){currentAngles=currentKick=Vector3.zero;return;}
        float u=elapsed/Mathf.Max(.001f,recoveryTime);
        if(u<.14f) {
            float attack=Mathf.SmoothStep(0,1,u/.14f);
            currentAngles=Vector3.Lerp(startAngles,targetAngles,attack);currentKick=Vector3.Lerp(startKick,targetKick,attack);
        } else {
            float pulse=u<.22f ? 1 : 1-Mathf.SmoothStep(0,1,(u-.22f)/.78f);
            currentAngles=targetAngles*pulse;currentKick=targetKick*pulse;
        }
        CaptureAttachments();
        baseRotation=torso.localRotation;basePosition=torso.localPosition;
        var delta=Quaternion.AngleAxis(currentAngles.x,transform.right)*
            Quaternion.AngleAxis(currentAngles.z,transform.forward)*Quaternion.AngleAxis(currentAngles.y,transform.up);
        torso.rotation=delta*torso.rotation;
        var kick=transform.TransformDirection(currentKick);torso.position+=kick;
        appliedRotation=torso.localRotation;appliedPosition=torso.localPosition;applied=true;
        AppliedJoltDegrees=Quaternion.Angle(baseRotation,appliedRotation);AppliedKickDistance=kick.magnitude;
        CarryAttachments();
    }
    private void CaptureAttachments(){
        foreach(var item in attachments){
            item.worldPosition=item.root.position;item.worldRotation=item.root.rotation;
            item.targetPosition=item.target.position;item.targetRotation=item.target.rotation;
        }
    }
    private void CarryAttachments(){
        foreach(var item in attachments){
            var change=item.target.rotation*Quaternion.Inverse(item.targetRotation);
            if((item.target.position-item.targetPosition).sqrMagnitude<.00000001f && Quaternion.Angle(change,Quaternion.identity)<.001f)continue;
            item.localPosition=item.root.localPosition;item.localRotation=item.root.localRotation;
            item.root.SetPositionAndRotation(item.target.position+change*(item.worldPosition-item.targetPosition),change*item.worldRotation);
            item.appliedPosition=item.root.localPosition;item.appliedRotation=item.root.localRotation;item.applied=true;
        }
    }
    private void RemoveOffset() {
        if(applied && torso!=null){
            if(Quaternion.Angle(torso.localRotation,appliedRotation)<.01f)torso.localRotation=baseRotation;
            if((torso.localPosition-appliedPosition).sqrMagnitude<.0000001f)torso.localPosition=basePosition;
        }
        foreach(var item in attachments)if(item.applied && item.root!=null){
            if(Quaternion.Angle(item.root.localRotation,item.appliedRotation)<.01f)item.root.localRotation=item.localRotation;
            if((item.root.localPosition-item.appliedPosition).sqrMagnitude<.0000001f)item.root.localPosition=item.localPosition;
            item.applied=false;
        }
        applied=false;AppliedJoltDegrees=AppliedKickDistance=0;
    }
    private void OnDisable() {
        Camera.onPreCull-=ApplyRenderPose;RemoveOffset();elapsed=recoveryTime;currentAngles=currentKick=Vector3.zero;
        if(sparks!=null)sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    private void BuildSparks() {
        var child=new GameObject("Pistol contact sparks");child.transform.SetParent(transform,false);
        sparks=child.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        sparks.useAutoRandomSeed=false;sparks.randomSeed=6741;
        var main=sparks.main;main.playOnAwake=false;main.loop=false;main.maxParticles=192;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.startSpeed=0;main.startSize3D=true;main.gravityModifier=.3f;
        var emission=sparks.emission;emission.enabled=false;var shape=sparks.shape;shape.enabled=false;
        var color=sparks.colorOverLifetime;color.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.5f,.2f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.7f,.3f),new GradientAlphaKey(0,1)});color.color=gradient;
        var size=sparks.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.15f));
        var renderer=child.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=sparkMaterial;
        // Orient to spray velocity relative to the rider, not inherited road speed.
        renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.velocityScale=0;
        renderer.cameraVelocityScale=0;renderer.maxParticleSize=.1f;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
}
