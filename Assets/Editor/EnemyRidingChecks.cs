using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyRidingChecks {
    private const string Key="CyberBiker.RidingChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Input : IPlayerInput { public Vector2 Steering=>steering;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;public Vector2 steering; }
    private static readonly Dictionary<Enemy,float> lanes=new Dictionary<Enemy,float>();
    private static readonly Dictionary<Enemy,Vector3> positions=new Dictionary<Enemy,Vector3>();
    private static EnemyEncounterDirector director;
    private static Difficulty settings;
    private static GameplayManager gameplay;
    private static PlayerController player;
    private static WorldGenerator world;
    private static Enemy enemy;
    private static Car car;
    private static Input input;
    private static int stage,changes,smoke,sparks,pausedCount;
    private static float stageAt,heldLane,maxLean;
    private static Vector3 pausedPosition;
    private static double started,pauseAt;
    private static bool finishing,captured;
    private static string report="";
    static EnemyRidingChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(){Directory.CreateDirectory("Logs/EnemyRiding");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Advance(){stage++;stageAt=Time.time;File.AppendAllText("Logs/EnemyRiding/progress.txt",stage+"\n");}
    private static void Tick() {
        if(finishing || !EditorApplication.isPlaying)return;
        try {
            Require(EditorApplication.timeSinceStartup-started<150,"Riding test timeout at stage "+stage);
            if(stage==0) {
                player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();
                if(player==null || player.Health==null || world==null || world.GetComponent<TrafficSpawner>()==null || Time.timeSinceLevelLoad<.3f)return;
                gameplay=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();gameplay.SwitchGameState(GameState.Playing);
                var old=world.GetComponent<EnemyEncounterDirector>();if(old!=null){old.enabled=false;UnityEngine.Object.DestroyImmediate(old);}
                world.GetComponent<TrafficSpawner>().enabled=false;
                foreach(var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
                foreach(var c in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
                foreach(var shape in player.GetComponentsInChildren<Collider>(true))shape.enabled=false;
                player.GetComponent<TouchInputManager>().enabled=false;input=new Input();typeof(PlayerController).GetField("input",Private).SetValue(player,input);
                var body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
                player.ConfigureTestingMovement(110,player.ForwardAcceleration,player.SteeringAcceleration,player.SteeringSpeedLimit);body.linearVelocity=Vector3.forward*110;
                Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;
                var manager=UnityEngine.Object.FindAnyObjectByType<DifficultyManager>();settings=new GameObject("Riding fixture").AddComponent<Difficulty>();
                settings.enemiesPerWave=5;settings.maxEnemiesOnRoad=5;settings.enemyInitialDelay=0;settings.enemySpawnInterval=.1f;
                settings.enemySpawnDistance=100;settings.enemyRearArrivalPercent=0;settings.enemyArrivalClosingSpeed=120;settings.enemyArrivalFadeTime=.2f;settings.enemyLaneChangeInterval=1.2f;
                manager.currentDifficulty=settings;director=world.gameObject.AddComponent<EnemyEncounterDirector>();
                director.Initialize((GameObject)typeof(WorldGenerator).GetField("enemy",Private).GetValue(world),player.gameObject,world,manager);Advance();return;
            }
            if(stage<=3)foreach(var actor in director.Riders) {
                if(!actor.HasInitialized)continue;
                typeof(Enemy).GetField("shootingDamage",Private).SetValue(actor,0f);
                if(lanes.TryGetValue(actor,out float lane) && Mathf.Abs(lane-actor.LanePreference)>.01f)changes++;
                lanes[actor]=actor.LanePreference;
                if(positions.TryGetValue(actor,out var old))Require(Vector3.Distance(old,actor.transform.position)<Mathf.Max(8,Time.deltaTime*400),"Riding must not warp");
                positions[actor]=actor.transform.position;
                if(world.TryGetLaneFrame(actor.transform.position,out var frame)) {
                    float x=Vector3.Dot(actor.transform.position-frame.origin,frame.right);Require(x>=frame.min-.2f && x<=frame.max+.2f,"Rider must stay inside the road");
                }
                var fx=actor.GetComponent<TireContactParticles>();Require(fx!=null && fx.enabled,"Enemy must have working player-style tire effects");
                smoke=Mathf.Max(smoke,fx.EmittedCount-fx.EmittedSparkCount);sparks=Mathf.Max(sparks,fx.EmittedSparkCount);maxLean=Mathf.Max(maxLean,actor.DrivingLeanDegrees);
                if(!captured && actor.DrivingLeanDegrees>22 && fx.ParticleCount>8 && Vector3.Distance(actor.transform.position,player.transform.position)<80){Capture("WheelEffects",actor);captured=true;}
            }
            if(stage==1 && director.Riders.Count==5 && Time.time-stageAt>1) {
                Require(world.TryGetLaneFrame(director.Riders[0].transform.position,out var frame),"Generated lane frame required");
                int count=frame.tile.carLines.Length;var used=new int[count];foreach(var actor in director.Riders)used[Mathf.RoundToInt(actor.LanePreference*(count-1))]++;
                int min=100,max=0;foreach(int value in used){min=Mathf.Min(min,value);max=Mathf.Max(max,value);}Require(max-min<=1,"Initial riders must spread evenly across all lanes");
                report+="PASS balanced preferred lanes: "+string.Join(",",used)+".\n";Advance();
            } else if(stage==2 && Time.time-stageAt>7) {
                Require(changes>=2,"Riders must make dynamic lane changes");Require(smoke>10 && sparks>3 && maxLean>20,"Lane changes must produce smoke and sparks from actual bank; smoke="+smoke+" sparks="+sparks+" lean="+maxLean);
                Capture("Group",null);enemy=director.ActiveFighter;Require(enemy!=null && !enemy.IsArriving,"Fighter must finish its arrival");
                enemy.TakeDamage(new DamageInfo(10000,player.gameObject));Require(enemy.IsFinisherReady,"Finisher fixture must stagger");heldLane=enemy.LanePreference;
                report+="PASS smooth road-bound lane changes="+changes+"; emitted smoke="+smoke+", sparks="+sparks+", peak lean="+maxLean.ToString("F1")+" degrees.\n";Advance();
            } else if(stage==3 && Time.time-stageAt>1.5f) {
                Require(enemy.LanePreference==heldLane,"Staggered target must hold its preferred lane");
                Require(Mathf.Abs(enemy.DrivingVelocity.x)<.3f,"Staggered target must settle its lateral velocity");
                pausedPosition=enemy.transform.position;pausedCount=enemy.GetComponent<TireContactParticles>().EmittedCount;gameplay.SwitchGameState(GameState.Pause);pauseAt=EditorApplication.timeSinceStartup;Advance();
            } else if(stage==4 && EditorApplication.timeSinceStartup-pauseAt>.2) {
                Require(enemy.transform.position==pausedPosition && enemy.GetComponent<TireContactParticles>().EmittedCount==pausedCount,"Pause freezes motion and particle births");
                gameplay.SwitchGameState(GameState.Playing);director.enabled=false;
                foreach(var actor in director.Riders)if(actor!=enemy)actor.gameObject.SetActive(false);
                typeof(Enemy).GetMethod("RecoverFromFinisher",Private).Invoke(enemy,null);
                settings.enemyLaneWeave=0;enemy.SetRidingLane(.5f,settings);
                world.TryGetLaneFrame(enemy.transform.position,out var frame);var p=enemy.transform.position;p.x=frame.origin.x+frame.right.x*(frame.min+frame.max)*.5f;
                enemy.transform.SetPositionAndRotation(p,Quaternion.identity);typeof(Enemy).GetMethod("ClearPath",Private).Invoke(enemy,null);
                var ai=UnityEngine.Object.FindAnyObjectByType<AI.AIController>();ai.dodgePoints.Clear();
                car=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Cars/Car_01.prefab"),p+Vector3.forward*30,Quaternion.identity).GetComponent<Car>();car.speed=0;
                ai.dodgePoints.AddRange(car.GetComponent<Obstacle>().GetDodgePoints2(p.x<0 ? 1 : -1));Physics.SyncTransforms();Advance();
            } else if(stage==5) {
                var guard=typeof(Enemy).GetField("trafficAvoidance",Private).GetValue(enemy);
                var shape=(Bounds)guard.GetType().GetMethod("Shape",Private).Invoke(guard,null);
                var planner=UnityEngine.Object.FindAnyObjectByType<AI.AIController>();
                Require(!Physics.CheckSphere(shape.center,Mathf.Max(0,planner.NavigationRadius-.02f),LayerMask.GetMask("WorldObject")),"Actual enemy must retain the original planner's contact clearance");
                if(Time.time-stageAt<1.8f)return;
                Require(enemy.transform.position.z>car.transform.position.z+8,"Enemy must take its original dodge-point detour without a manual lane-change rescue");Capture("PassingCar",enemy);Advance();
            } else if(stage==6 && Time.time-stageAt>3) {
                Require(enemy.transform.position.z>car.transform.position.z+8,"Enemy must resume passing when a lane opens");
                report+="PASS staggered lane hold; pause freezes motion/emission; actual enemy uses original traffic dodge points to pass a car without falling behind or a manual lane-change rescue.\n";
                enemy.gameObject.SetActive(false);Require(enemy.GetComponent<TireContactParticles>().ParticleCount==0,"Disabling enemy clears wheel particles");
                input.steering=Vector2.right;Advance();
            } else if(stage==7 && Time.time-stageAt>.6f) {
                var fx=player.GetComponent<TireContactParticles>();Require(fx.EmittedCount>3 && fx.EmittedSparkCount>0,"Player tire effects must still emit during steering");
                report+="PASS enemy FX clear on disable; existing player steering smoke/sparks still work.\n";Finish(true,report);
            }
        } catch(Exception error){Finish(false,error+"\n"+report);}
    }
    private static void Capture(string name,Enemy subject) {
        var camera=Camera.main;var previousPosition=camera.transform.position;var previousRotation=camera.transform.rotation;
        if(subject!=null){camera.transform.position=subject.transform.position+new Vector3(-5,2.5f,-5);camera.transform.LookAt(subject.transform.position+Vector3.up);}
        var previous=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/EnemyRiding/"+name+".png",image.EncodeToPNG());}
        finally{camera.transform.SetPositionAndRotation(previousPosition,previousRotation);camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);}
    }
    private static void Finish(bool ok,string message){if(finishing)return;finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key,false);Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;File.WriteAllText("Logs/EnemyRiding/Live.txt",message);Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(ok ? 0 : 1);}
}
