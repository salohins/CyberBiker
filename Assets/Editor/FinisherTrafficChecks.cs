using System;
using System.IO;
using System.Reflection;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FinisherTrafficChecks {
    private const string Key="CyberBiker.TrafficChecks";
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class SteeringInput : IPlayerInput {
        public float value;
        public Vector2 Steering=>new Vector2(value,0);
        public Vector2 AimDelta=>Vector2.zero;
        public bool IsAiming=>false;
    }
    private static SteeringInput input;
    private static Car car;
    private static BoxCollider carShape,bikeShape;
    private static Rigidbody body;
    private static int stage,kicks;
    private static double began,pauseAt;
    private static float dashX,hitX,postHitX,pausedGrace;
    private static bool finishing,dashSteered,hitSteered,sawSlow,sawGrace;
    static FinisherTrafficChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run() {
        Directory.CreateDirectory("Logs/FinisherTraffic");CheckPairs();CheckSave();
        var tuning=FinisherLabTuning.ReadSavedDefaults();
        tuning.Get("drawPlaybackSpeed").number=4.5f;tuning.Get("strikePlaybackSpeed").number=.7f;
        tuning.Get("sliceReturnPlaybackSpeed").number=3.5f;tuning.Get("sliceReturnBlendTime").number=.15f;
        tuning.Get("contactPoseHold").number=.28f;tuning.Get("trafficRecoveryGrace").number=.8f;
        SessionState.SetBool(Key,true);FinisherLabPreview.Left=true;FinisherLabPreview.Sound=false;FinisherLabPreview.Loop=false;
        FinisherLabPreview.Start(tuning);
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void PlaceCar(Vector3 center) {
        car.transform.position+=center-carShape.bounds.center;Physics.SyncTransforms();
    }
    private static Car SpawnCar(Vector3 center) {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Cars/Car_01.prefab");
        var go=UnityEngine.Object.Instantiate(prefab,center,Quaternion.identity);go.name="Finisher traffic regression car";
        var result=go.GetComponent<Car>();result.OnPoolRent(prefab);result.enabled=false;
        car=result;carShape=go.GetComponent<BoxCollider>();Physics.SyncTransforms();PlaceCar(center);return result;
    }
    private static void Tick() {
        if(finishing || !EditorApplication.isPlaying)return;
        try {
            if(EditorApplication.timeSinceStartup-began>100)throw new Exception("Traffic checks timed out at "+stage+" "+FinisherLabPreview.Status);
            if(!FinisherLabPreview.Ready)return;
            var player=FinisherLabPreview.Player;var sword=FinisherLabPreview.Sword;var gm=FinisherLabPreview.Gameplay;
            if(stage>0 && stage<6)Require(gm.gameState==GameState.Playing || stage==4,"Protected traffic must not trigger a crash at stage "+stage);
            if(stage==0) {
                Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;
                input=new SteeringInput();typeof(PlayerController).GetField("input",Private).SetValue(player,input);
                body=player.GetComponent<Rigidbody>();bikeShape=player.GetComponent<BoxCollider>();
                // This encounter isolates traffic. Holding steering for the
                // entire aftermath would otherwise eventually reach a real wall.
                foreach(var shape in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                    if(shape.GetComponentInParent<PlayerController>()==null && shape.GetComponentInParent<Enemy>()==null)shape.enabled=false;
                CheckAttackSide(sword);
                kicks=player.GetComponent<BikeImpactResponse>().KickCount;stage=1;
            }
            if(stage==1 && sword.IsDashing) {
                Require(sword.TrafficProtected,"Protection must start by the committed dash");
                SpawnCar(player.transform.position+Vector3.up*1.2f);dashX=body.linearVelocity.x;input.value=.8f;stage=2;
            } else if(stage==2) {
                PlaceCar(player.transform.position+Vector3.up*1.2f);
                if(sword.IsDashing && body.linearVelocity.x>dashX+.05f)dashSteered=true;
                Require(carShape.enabled && car.IsAlive,"Pass-through must leave the car intact and solid for everyone else");
                if(sword.HitCount>0) {
                    Require(dashSteered,"Steering must add lateral velocity during the live dash");
                    Require(Physics.GetIgnoreCollision(bikeShape,carShape),"The real bike/car collider pair must be ignored");
                    Require(!sword.StepPickupDash(),"After contact the finisher must relinquish movement to the motor");
                    hitX=body.position.x;postHitX=body.linearVelocity.x;input.value=-1;stage=3;
                }
            } else if(stage==3) {
                PlaceCar(player.transform.position+Vector3.up*1.2f);
                if(body.linearVelocity.x<postHitX-.05f)hitSteered=true;
                if(gm.CombatSlowMotionRemaining>0){sawSlow=true;Require(sword.TrafficProtected,"Protection must outlast the animation when slow motion remains");}
                if(!sword.IsAssisting && (sword.Phase==BikeSwordCombat.CombatPhase.Pursuing || sword.Phase==BikeSwordCombat.CombatPhase.Idle)
                    && gm.CombatSlowMotionRemaining<=0 && sword.TrafficGraceRemaining<.65f && sword.TrafficGraceRemaining>.1f) {
                    Require(hitSteered && Mathf.Abs(body.position.x-hitX)>.02f,"Steering must move the bike during impact/recovery too");
                    Require(sawSlow && sword.TrafficProtected,"There must be protection during aftermath and the grace window");
                    sawGrace=true;input.value=0;pausedGrace=sword.TrafficGraceRemaining;gm.SwitchGameState(GameState.Pause);pauseAt=EditorApplication.timeSinceStartup;stage=4;
                }
            } else if(stage==4) {
                Require(Time.timeScale==0 && sword.TrafficGraceRemaining==pausedGrace,"Pause must freeze the recovery grace");
                if(EditorApplication.timeSinceStartup-pauseAt>.15){gm.SwitchGameState(GameState.Playing);stage=5;}
            } else if(stage==5) {
                PlaceCar(player.transform.position+Vector3.up*1.2f);
                if(!sword.TrafficProtected) {
                    Require(sawGrace && sword.IgnoresTraffic(carShape) && Physics.GetIgnoreCollision(bikeShape,carShape),"Expiry inside a car must retain only that overlapping pair");
                    var response=player.GetComponent<BikeImpactResponse>();
                    Require(response.KickCount==kicks,"Protected cars must not apply physical kicks; last="+response.LastHitCollider+" x="+body.position.x);
                    PlaceCar(player.transform.position+new Vector3(0,1.2f,-35));stage=6;
                }
            } else if(stage==6) {
                Require(gm.gameState==GameState.Playing,"Clearing an overlapping car must be safe");
                if(Physics.GetIgnoreCollision(bikeShape,carShape))return;
                Require(!sword.IgnoresTraffic(carShape),"A separated car must return to normal collisions");
                sword.GainLaunchEnergy(100000);Require(sword.LaunchCharge>.99f,"Crash fixture must begin fully charged");
                body.linearVelocity=new Vector3(0,0,110);SpawnCar(player.transform.position+new Vector3(0,1.2f,8));stage=7;
            } else if(stage==7 && gm.gameState==GameState.Hit) {
                Require(sword.LaunchCharge==0 && !sword.IsPursuing,"Actual car crash must burn all Launch energy");
                Finish(true,"PASS: actual finisher survives an overlapping real car through dash, hit, slow motion and 0.8-second grace; manual steering accelerates during dash and reverses after contact; normal motor owns post-hit movement; pause freezes grace; cars remain intact; no collision kick; expiry inside a car waits for separation; collisions restore after clearing; a subsequent ordinary rear collision still crashes. Front/near-centre/left targets choose the left swing; right targets choose the right swing. Owned-pair cleanup, pooled collider reuse, walls, old presets and saved grace also checked.");
            }
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void CheckAttackSide(BikeSwordCombat sword) {
        var target=sword.Target;Require(target!=null,"Attack-side fixture requires a real finisher-ready target");
        var original=target.transform.position;
        var canStart=typeof(BikeSwordCombat).GetMethod("CanStartStrike",Private);
        try {
            foreach(float x in new[]{0f,.2f,-3f,3f}) {
                target.transform.position=sword.transform.position+new Vector3(x,0,10);Physics.SyncTransforms();
                Require((bool)canStart.Invoke(sword,null),"Attack-side target must be reachable");
                Require(sword.LastAttackSide==(x>1 ? BikeSwordCombat.AttackSide.Right : BikeSwordCombat.AttackSide.Left),"Front must prefer left, with right reserved for a target on the right; x="+x);
            }
        } finally {target.transform.position=original;Physics.SyncTransforms();canStart.Invoke(sword,null);}
    }
    private static void CheckSave() {
        var draft=FinisherLabTuning.ReadSavedDefaults();draft.Get("trafficRecoveryGrace").number=1.2f;
        string fixture="Assets/Editor/FinisherTrafficSaveFixture.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(fixture)==null)Require(AssetDatabase.CopyAsset(FinisherLabTuning.PlayerPrefab,fixture),"Cannot copy tuning fixture");
        var original=File.ReadAllBytes(FinisherLabTuning.PlayerPrefab);draft.SaveForGame(fixture);
        Require(Mathf.Abs(new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(fixture).GetComponent<BikeSwordCombat>()).FindProperty("trafficRecoveryGrace").floatValue-1.2f)<.001f,"Lab must persist recovery grace");
        Require(System.Linq.Enumerable.SequenceEqual(original,File.ReadAllBytes(FinisherLabTuning.PlayerPrefab)),"Save fixture must leave source tuning untouched");
        draft.values.RemoveAll(value=>value.field=="trafficRecoveryGrace");draft.Normalize();
        Require(draft.Get("trafficRecoveryGrace").number==.8f,"Older presets need the safe grace default");
    }
    private static void CheckPairs() {
        var bike=new GameObject("Pair check bike");var bikeShape=bike.AddComponent<BoxCollider>();
        var other=new GameObject("Pair check traffic");other.tag="Obstacle";var otherShape=other.AddComponent<BoxCollider>();
        var wall=new GameObject("Pair check wall");wall.tag="Wall";var wallShape=wall.AddComponent<BoxCollider>();
        var secondary=bike.AddComponent<SphereCollider>();var contacts=new FinisherTrafficContacts(bike);
        try {
            Physics.SyncTransforms();Physics.IgnoreCollision(secondary,otherShape,true);
            contacts.ProtectPath(Vector3.zero,Vector3.forward*3);
            Require(Physics.GetIgnoreCollision(bikeShape,otherShape) && !Physics.GetIgnoreCollision(bikeShape,wallShape),"Only traffic should be ignored");
            contacts.ReleaseClearPairs(false);Require(contacts.Contains(otherShape),"Overlap must retain its owned pairs after expiry");
            otherShape.enabled=false;otherShape.enabled=true;Physics.SyncTransforms();contacts.ProtectPath(Vector3.zero,Vector3.forward*3);
            Require(Physics.GetIgnoreCollision(bikeShape,otherShape),"Pooled colliders need protection reapplied");
            contacts.Dispose();Require(!Physics.GetIgnoreCollision(bikeShape,otherShape) && Physics.GetIgnoreCollision(secondary,otherShape),"Cleanup must restore owned pairs and preserve pre-existing ignores");
        }finally {contacts.Dispose();UnityEngine.Object.DestroyImmediate(bike);UnityEngine.Object.DestroyImmediate(other);UnityEngine.Object.DestroyImmediate(wall);}
    }
    private static void Finish(bool success,string message) {
        if(finishing)return;finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key,false);
        FinisherClock.EditorCaptureStep=0;Time.captureDeltaTime=0;
        File.WriteAllText("Logs/FinisherTraffic/result.txt",message);Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);
    }
}
