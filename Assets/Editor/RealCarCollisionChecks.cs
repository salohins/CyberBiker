using System;
using System.IO;
using System.Reflection;
using System.Text;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RealCarCollisionChecks {
    private const string Pending="CyberBiker.RealCarCollisionChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static bool finishing;
    private static readonly StringBuilder report=new StringBuilder();
    static RealCarCollisionChecks() {if(SessionState.GetBool(Pending,false)) EditorApplication.delayCall+=Attach;}
    public static void Run() {
        EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");
        SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void Attach() {EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string text,string stack,LogType type) {
        if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert) Finish(false,text+"\n"+stack);
    }
    private static void Require(bool condition,string message) {if(!condition) throw new Exception(message);}
    private static void Tick() {
        if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<.3f) return;
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if(player==null || player.Health==null) return;
        EditorApplication.update-=Tick;
        var oldSimulation=Physics.simulationMode;
        try {
            Physics.simulationMode=SimulationMode.Script;
            foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None)) car.gameObject.SetActive(false);
            foreach(var spawner in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None)) spawner.enabled=false;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name="Powered-motor collision test road";floor.layer=LayerMask.NameToLayer("Ground");
            floor.transform.position=new Vector3(0,99.73f,0);floor.transform.localScale=new Vector3(300,.5f,300);
            RearHit(player,0,"centre rear");
            RearHit(player,1,"left rear corner, front wheel misses");
            RearHit(player,-1,"right rear corner, front wheel misses");
            SideKick(player,1);SideKick(player,-1);
            BorderImpact(player,1);BorderImpact(player,-1);
            Finish(true,"PASS: actual Level player and Car_01 prefab; immediate centre/left/right rear crashes, exactly-once damage, instant ragdoll and car collider release, no driving through a crash; left/right separation with steering held and motor active; actual left/right road-border mesh impacts, camera recoil, scratch particles and physical separation.\n"+report);
        } catch(Exception error) {Finish(false,error+"\n"+report);}
        finally {Physics.simulationMode=oldSimulation;Time.timeScale=1;}
    }
    private static void ResetPlayer(PlayerController player) {
        var ragdoll=typeof(PlayerController).GetField("_ragDoll",Private).GetValue(player) as GameObject;
        if(ragdoll!=null) foreach(var collider in ragdoll.GetComponentsInChildren<Collider>()) collider.enabled=false;
        UnityEngine.Object.FindAnyObjectByType<GameplayManager>().SwitchGameState(GameState.Playing);
        player.ResetRagDoll();
        var body=player.GetComponent<Rigidbody>();
        player.transform.SetPositionAndRotation(new Vector3(0,100,0),Quaternion.identity);
        body.position=new Vector3(0,100,0);body.rotation=Quaternion.identity;
        body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeRotation;
        body.linearVelocity=body.angularVelocity=Vector3.zero;
        foreach(var mesh in player.GetComponentsInChildren<MeshFilter>(true))
            if(mesh.name=="Motorcycle") mesh.transform.localRotation=Quaternion.identity;
        player.GetComponent<TouchInputManager>().setLeftPressed(false);
        player.GetComponent<TouchInputManager>().setRightPressed(false);
        player.xThrow=0;Physics.SyncTransforms();
    }
    private static Car SpawnCar(Vector3 position) {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Cars/Car_01.prefab");
        var instance=UnityEngine.Object.Instantiate(asset,position,Quaternion.identity);
        return instance.GetComponent<Car>();
    }
    private static void PoweredStep(PlayerController player) {
        typeof(TouchInputManager).GetMethod("Update",Private).Invoke(player.GetComponent<TouchInputManager>(),null);
        typeof(BikeImpactResponse).GetMethod("FixedUpdate",Private).Invoke(player.GetComponent<BikeImpactResponse>(),null);
        typeof(PlayerController).GetMethod("FixedUpdate",Private).Invoke(player,null);
        Physics.SyncTransforms();Physics.Simulate(.02f);
        typeof(CollisionScrapeParticles).GetMethod("FixedUpdate",Private).Invoke(player.GetComponent<CollisionScrapeParticles>(),null);
    }
    private static void RearHit(PlayerController player,int corner,string label) {
        ResetPlayer(player);
        var body=player.GetComponent<Rigidbody>();
        var car=SpawnCar(new Vector3(0,100,0));var box=car.GetComponent<BoxCollider>();
        Physics.SyncTransforms();
        float rearZ=player.GetComponent<BoxCollider>().bounds.max.z+.35f;
        float x=corner==0 ? 0 : corner*(box.bounds.extents.x+.58f);
        car.transform.position+=new Vector3(x,0,rearZ-box.bounds.min.z);Physics.SyncTransforms();
        float health=player.Health.Current;
        body.linearVelocity=Vector3.forward*35;
        var manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();
        int steps=0;
        while(manager.gameState==GameState.Playing && steps++<15) PoweredStep(player);
        Require(manager.gameState==GameState.Hit,label+" must immediately trigger the crash state");
        var visual=(GameObject)new SerializedObject(player).FindProperty("playerMesh").objectReferenceValue;
        var ragdoll=typeof(PlayerController).GetField("_ragDoll",Private).GetValue(player) as GameObject;
        Require(!visual.activeSelf && ragdoll!=null,label+" must hide the rider and spawn the ragdoll in the collision callback");
        Require(!box.enabled && (bool)typeof(Car).GetField("hit",Private).GetValue(car),label+" must be recognized by the actual car even through the bike's child colliders");
        Require(player.Health.Current==health-car.GetComponent<Obstacle>().damage,label+" must apply existing collision damage exactly once");
        Require(body.linearVelocity.z<=.01f,label+" must stop forward pushing immediately");
        float after=player.Health.Current;
        report.AppendLine($"{label} immediately: state={manager.gameState}, health={health}->{after}, velocity={body.linearVelocity:F3}, kick={player.GetComponent<BikeImpactResponse>().LastKickVelocity:F3}");
        PoweredStep(player);PoweredStep(player);
        Require(player.Health.Current==after && body.linearVelocity.z<=.01f,$"{label} must not keep driving or repeatedly damage the rider after the crash; state={manager.gameState}, health={player.Health.Current}, velocity={body.linearVelocity:F3}");
        report.AppendLine($"{label}: hit by physics step {steps}, health {health}->{after}, recoil={body.linearVelocity:F3}");
        car.gameObject.SetActive(false);
    }
    private static void SideKick(PlayerController player,int side) {
        ResetPlayer(player);
        var body=player.GetComponent<Rigidbody>();
        var car=SpawnCar(new Vector3(0,100,0));var box=car.GetComponent<BoxCollider>();Physics.SyncTransforms();
        float bodyHalfWidth=0;
        foreach(var shape in player.GetComponentsInChildren<Collider>())
            if(shape.enabled && !shape.isTrigger) bodyHalfWidth=Mathf.Max(bodyHalfWidth,Mathf.Abs(shape.bounds.min.x-body.position.x),Mathf.Abs(shape.bounds.max.x-body.position.x));
        // The Level overrides the prefab scale. Start outside the actual body,
        // not overlapping a hardcoded width before physics has even advanced.
        car.transform.position+=new Vector3(side*(box.bounds.extents.x+bodyHalfWidth+.08f),0,2.2f-box.bounds.center.z);Physics.SyncTransforms();
        report.AppendLine($"side {side} setup: root physics={body.position:F3} visual={player.transform.position:F3}, car bounds={box.bounds}, active={car.gameObject.activeInHierarchy}, collider={box.enabled}");
        foreach(var c in player.GetComponentsInChildren<BoxCollider>()) report.AppendLine($" bike box {c.name}: {c.bounds}, enabled={c.enabled}");
        var input=player.GetComponent<TouchInputManager>();
        input.setRightPressed(side>0);input.setLeftPressed(side<0);
        var response=player.GetComponent<BikeImpactResponse>();int before=response.KickCount;
        var manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();
        body.linearVelocity=new Vector3(side*6,0,40);
        int steps=0;
        while(response.KickCount==before && steps++<10) PoweredStep(player);
        Require(response.KickCount==before+1 && manager.gameState==GameState.Playing,
            $"A mid-side swipe must kick without incorrectly crashing the rider; side={side}, kicks={before}->{response.KickCount}, state={manager.gameState}, point={player.GetComponent<CollisionScrapeParticles>().LastContactPoint:F3}, normal={player.GetComponent<CollisionScrapeParticles>().LastContactNormal:F3}, velocity={body.linearVelocity:F3}, kick={response.LastKickVelocity:F3}");
        Require(!box.enabled,"The car must release contact with the compound bike collider");
        CheckRecoilPose(player,side,"car");
        float hitX=body.position.x,maxSeparation=0;
        report.AppendLine($"side {side} initial kick={response.LastKickVelocity:F3}, velocity={body.linearVelocity:F3}, held multiplier={response.SteeringAccelerationMultiplier(side):F3}");
        for(int i=0;i<7;i++) {
            PoweredStep(player);
            maxSeparation=Mathf.Max(maxSeparation,(hitX-body.position.x)*side);
            report.AppendLine($" side step {i}: x={body.position.x:F3}, vx={body.linearVelocity.x:F3}, throttle={response.MotorAccelerationMultiplier:F3}, steering={response.SteeringAccelerationMultiplier(side):F3}, wall={player.wallDirection}, xThrow={player.xThrow}");
        }
        Require(maxSeparation>.15f,$"Held steering erased the physical kick: side={side}, separation={maxSeparation:F4}, velocity={body.linearVelocity}");
        Require(response.KickCount==before+1,"One side swipe must not apply repeated impulses");
        report.AppendLine($"side {side}: separation with held steering={maxSeparation:F3}, kick={response.LastKickVelocity:F3}");
        car.gameObject.SetActive(false);
        CheckPoweredRecovery(player,side,"car");
    }
    private static void Finish(bool success,string message) {
        if(finishing) return;finishing=true;
        EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
        SessionState.SetBool(Pending,false);Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/real-car-collision-check.txt",message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    private static void BorderImpact(PlayerController player,int side) {
        ResetPlayer(player);
        var road=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/World Generator/Tiles/RoadStraight.prefab");
        MeshCollider source=null;
        foreach(var candidate in road.GetComponentsInChildren<MeshCollider>(true)) {
            if(!candidate.CompareTag("Wall") || candidate.sharedMesh==null) continue;
            float centerX=candidate.transform.TransformPoint(candidate.sharedMesh.bounds.center).x;
            if(centerX*side>0) {source=candidate;break;}
            if(source==null) source=candidate;
        }
        Require(source!=null,"The production road must contain a border mesh collider");
        var border=UnityEngine.Object.Instantiate(source.gameObject);
        var surface=border.GetComponent<MeshCollider>();Physics.SyncTransforms();
        var body=player.GetComponent<Rigidbody>();float halfWidth=0;
        foreach(var shape in player.GetComponentsInChildren<Collider>())
            if(shape.enabled && !shape.isTrigger) halfWidth=Mathf.Max(halfWidth,Mathf.Abs(shape.bounds.min.x-body.position.x),Mathf.Abs(shape.bounds.max.x-body.position.x));
        float face=side>0 ? surface.bounds.min.x : surface.bounds.max.x;
        border.transform.position+=new Vector3(side*(halfWidth+.08f)-face,102-surface.bounds.center.y,2.2f-surface.bounds.center.z);
        Physics.SyncTransforms();
        var input=player.GetComponent<TouchInputManager>();input.setRightPressed(side>0);input.setLeftPressed(side<0);
        var response=player.GetComponent<BikeImpactResponse>();int kickBefore=response.KickCount;
        var effects=player.GetComponent<CollisionScrapeParticles>();int sparksBefore=effects.EmittedCount;
        typeof(CollisionScrapeParticles).GetField("nextCameraImpact",Private).SetValue(effects,0f);
        var camera=Camera.main.GetComponent<CameraController>();int cameraBefore=camera.CarImpactCount;
        body.linearVelocity=new Vector3(side*6,0,40);
        int steps=0;
        while(response.KickCount==kickBefore && steps++<10) PoweredStep(player);
        Require(response.KickCount==kickBefore+1,$"Road border {side} must apply physical impact feedback");
        Require(UnityEngine.Object.FindAnyObjectByType<GameplayManager>().gameState==GameState.Playing,"A side scrape on a road border must preserve riding controls");
        Require(effects.EmittedCount>sparksBefore && camera.CarImpactCount>cameraBefore,"The actual road border must produce scratch particles and a camera jolt");
        CheckRecoilPose(player,side,"border");
        float hitX=body.position.x,separation=0;
        for(int i=0;i<7;i++) {PoweredStep(player);separation=Mathf.Max(separation,(hitX-body.position.x)*side);}
        Require(separation>.15f,$"Road border recoil must survive the powered motor and held input: {separation:F4}");
        report.AppendLine($"border {side}: separation={separation:F3}, camera impacts={camera.CarImpactCount-cameraBefore}, sparks={effects.EmittedCount-sparksBefore}");
        border.SetActive(false);
        CheckPoweredRecovery(player,side,"border");
    }
    private static void CheckPoweredRecovery(PlayerController player,int heldDirection,string surface) {
        var response=player.GetComponent<BikeImpactResponse>();
        var body=player.GetComponent<Rigidbody>();
        var animator=player.GetComponent<Animator>();
        var presentation=(BikePresentation)typeof(PlayerController).GetField("presentation",Private).GetValue(player);
        var mesh=(Transform)typeof(BikePresentation).GetField("motorcycleMesh",Private).GetValue(presentation);
        var target=typeof(BikePresentation).GetField("visualSteering",Private);
        var poseTick=typeof(PlayerController).GetMethod("UpdatePresentation",Private);
        float leanRange=new SerializedObject(player).FindProperty("leanRange").floatValue;
        float maxStep=0,previous=animator.GetFloat("dragX");
        for(int i=0;i<100;i++) {
            PoweredStep(player);poseTick.Invoke(player,new object[]{.02f});
            float rider=animator.GetFloat("dragX"),lean=Mathf.DeltaAngle(0,mesh.localEulerAngles.z);
            maxStep=Mathf.Max(maxStep,Mathf.Abs(rider-previous));previous=rider;
            Require(Mathf.Abs(rider+lean/leanRange)<.001f,"Bike roll and rider must share the same smoothed lean throughout real powered recovery");
            if(body.linearVelocity.x*heldDirection<-.2f)
                Require((float)target.GetValue(presentation)*body.linearVelocity.x>=0,
                    "While recoil still moves opposite steering, the lean target must follow actual movement");
            if(i>=3) Require(response.SteeringAccelerationMultiplier(heldDirection)==1,
                "Extra steering resistance must end within 0.2 seconds of impact");
        }
        Require(!response.IsRecovering && animator.GetFloat("dragX")*heldDirection>.95f,
            $"{surface} {heldDirection}: normal steering lean must return after real physics resolves the kick");
        Require(response.PresentationResponse==3,"After recoil, normal steering must retain its original animation speed");
        var input=player.GetComponent<TouchInputManager>();
        input.setRightPressed(heldDirection<0);input.setLeftPressed(heldDirection>0);
        for(int i=0;i<75;i++) {
            PoweredStep(player);poseTick.Invoke(player,new object[]{.02f});
            float rider=animator.GetFloat("dragX");maxStep=Mathf.Max(maxStep,Mathf.Abs(rider-previous));previous=rider;
        }
        Require(animator.GetFloat("dragX")*heldDirection<-.95f,"Steering reversal after recovery must settle at the original normal animation speed");
        Require(maxStep<.24f,"Recoil handoff and later direction changes must remain smooth rather than snapping");
        report.AppendLine($"{surface} powered recovery {heldDirection}: full steering restored, bike/rider synchronized, normal lean speed=3, reversal settled within 1.5s, max pose step={maxStep:F3}");
    }
    private static void CheckRecoilPose(PlayerController player,int heldDirection,string surface) {
        var body=player.GetComponent<Rigidbody>();var velocity=body.linearVelocity;
        Require(velocity.x*heldDirection<-.2f,"Pose test must exercise actual movement opposite the held steering");
        var presentation=(BikePresentation)typeof(PlayerController).GetField("presentation",Private).GetValue(player);
        var poseTick=typeof(PlayerController).GetMethod("UpdatePresentation",Private);
        Transform motorcycle=null;
        foreach(var filter in player.GetComponentsInChildren<MeshFilter>()) if(filter.name=="Motorcycle") motorcycle=filter.transform;
        var rootRotation=player.transform.rotation;var meshRotation=motorcycle.localRotation;
        var animator=player.GetComponent<Animator>();float drag=animator.GetFloat("dragX");bool aim=animator.GetBool("Aim");
        var fields=typeof(BikePresentation).GetFields(Private);var values=new object[fields.Length];
        for(int i=0;i<fields.Length;i++) values[i]=fields[i].GetValue(presentation);
        float input=player.xThrow;
        try {
            // Begin leaning into the hit, then exercise the production presentation
            // path with the real post-collision velocity and steering still held.
            for(int i=0;i<30;i++) presentation.Tick(heldDirection,false,false,heldDirection,3,.02f);
            player.xThrow=heldDirection;
            float previousLean=Mathf.DeltaAngle(0,motorcycle.localEulerAngles.z);
            float previousDrag=animator.GetFloat("dragX");
            for(int i=0;i<40;i++) {
                poseTick.Invoke(player,new object[]{.02f});
                float nextLean=Mathf.DeltaAngle(0,motorcycle.localEulerAngles.z);
                float nextDrag=animator.GetFloat("dragX");
                Require(Mathf.Abs(nextLean-previousLean)<5 && Mathf.Abs(nextDrag-previousDrag)<.17f,
                    "Kick lean and rider animation must flow through small changes without an aggressive fast-response burst");
                previousLean=nextLean;previousDrag=nextDrag;
            }
            float lean=Mathf.DeltaAngle(0,motorcycle.localEulerAngles.z);
            float rider=animator.GetFloat("dragX");
            Require(lean*heldDirection>1 && rider*heldDirection<-.05f,
                $"{surface} recoil must gently align lean and rider animation with movement; held={heldDirection}, vx={velocity.x:F3}, lean={lean:F3}, dragX={rider:F3}");
            Require(Mathf.Abs(rider)<.36f,"Kick-induced rider lean must stay restrained rather than reaching full steering lean");
            var frozen=motorcycle.localRotation;
            poseTick.Invoke(player,new object[]{0f});
            Require(Quaternion.Angle(frozen,motorcycle.localRotation)<.001f && animator.GetFloat("dragX")==rider,"Paused presentation must freeze recoil lean and animation");
            body.linearVelocity=new Vector3(heldDirection*4,velocity.y,velocity.z);
            previousLean=lean;previousDrag=rider;
            for(int i=0;i<40;i++) {
                poseTick.Invoke(player,new object[]{.02f});
                float nextLean=Mathf.DeltaAngle(0,motorcycle.localEulerAngles.z);
                float nextDrag=animator.GetFloat("dragX");
                Require(Mathf.Abs(nextLean-previousLean)<5 && Mathf.Abs(nextDrag-previousDrag)<.17f,
                    "Changing steering after a kick must retain the same smooth pose response");
                previousLean=nextLean;previousDrag=nextDrag;
            }
            Require(Mathf.DeltaAngle(0,motorcycle.localEulerAngles.z)*heldDirection<0 && animator.GetFloat("dragX")*heldDirection>0,
                "A movement reversal must smoothly reverse the lean and rider pose without holding an old opposite pose");
            float beforeNeutral=Mathf.Abs(Mathf.DeltaAngle(0,motorcycle.localEulerAngles.z));
            body.linearVelocity=new Vector3(0,velocity.y,velocity.z);player.xThrow=0;
            for(int i=0;i<6;i++) poseTick.Invoke(player,new object[]{.02f});
            float returning=Mathf.Abs(Mathf.DeltaAngle(0,motorcycle.localEulerAngles.z));
            Require(returning<beforeNeutral && returning>beforeNeutral*.4f,"Returning to neutral must still ease smoothly instead of snapping");
            for(int i=0;i<54;i++) poseTick.Invoke(player,new object[]{.02f});
            Require(Mathf.Abs(Mathf.DeltaAngle(0,motorcycle.localEulerAngles.z))<1 && Mathf.Abs(animator.GetFloat("dragX"))<.05f,
                "Lean and rider animation must settle to neutral once sideways movement stops");
            report.AppendLine($"{surface} pose {heldDirection}: recoil lean={lean:F2}, rider dragX={rider:F3}, vx={velocity.x:F3}");
        } finally {
            for(int i=0;i<fields.Length;i++) if(!fields[i].IsInitOnly) fields[i].SetValue(presentation,values[i]);
            player.transform.rotation=rootRotation;body.rotation=rootRotation;motorcycle.localRotation=meshRotation;
            body.linearVelocity=velocity;player.xThrow=input;animator.SetFloat("dragX",drag);animator.SetBool("Aim",aim);
            Physics.SyncTransforms();
        }
    }
}
