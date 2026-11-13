using System;
using System.IO;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyArrivalChecks {
    private const string Key="CyberBiker.ArrivalChecks";
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class NeutralInput:IPlayerInput {
        public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;
    }
    private static PlayerController player;
    private static WorldGenerator world;
    private static GameplayManager gameplay;
    private static DifficultyManager difficulty;
    private static EnemyEncounterDirector director;
    private static Enemy enemy;
    private static Vector3 previous,pausedPosition;
    private static float lastVisibility,pausedVisibility,frontGap;
    private static double began,pauseAt;
    private static int stage,kicks;
    private static bool finishing,sawPass,sawFade,capturedEntry;
    static EnemyArrivalChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run(){Directory.CreateDirectory("Logs/EnemyArrivals");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void StartArrival(bool behind) {
        if(director!=null){director.enabled=false;UnityEngine.Object.DestroyImmediate(director);}
        if(enemy!=null)enemy.gameObject.SetActive(false);
        var settings=new GameObject(behind ? "Rear arrival fixture" : "Front arrival fixture").AddComponent<Difficulty>();
        settings.enemiesPerWave=1;settings.maxEnemiesOnRoad=1;settings.enemyInitialDelay=0;settings.enemyWaveCooldown=5;
        settings.enemyRearArrivalPercent=behind ? 100 : 0;settings.enemyRearSpawnDistance=45;settings.enemySpawnDistance=180;
        settings.enemyArrivalClosingSpeed=behind ? 60 : 100;settings.enemyArrivalFadeTime=1;settings.enemyQueueSpacing=18;
        difficulty.currentDifficulty=settings;director=world.gameObject.AddComponent<EnemyEncounterDirector>();
        director.Initialize((GameObject)typeof(WorldGenerator).GetField("enemy",Private).GetValue(world),player.gameObject,world,difficulty);
        enemy=null;lastVisibility=0;
    }
    private static void Tick() {
        if(finishing || !EditorApplication.isPlaying)return;
        try {
            if(EditorApplication.timeSinceStartup-began>110)throw new Exception("Arrival test timeout at stage "+stage);
            if(stage==0) {
                player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();
                if(player==null || player.Health==null || world==null || world.GetComponent<TrafficSpawner>()==null || Time.timeSinceLevelLoad<.3f)return;
                gameplay=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();gameplay.SwitchGameState(GameState.Playing);difficulty=UnityEngine.Object.FindAnyObjectByType<DifficultyManager>();
                var old=world.GetComponent<EnemyEncounterDirector>();if(old!=null){old.enabled=false;UnityEngine.Object.DestroyImmediate(old);}
                world.GetComponent<TrafficSpawner>().enabled=false;
                foreach(var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
                foreach(var c in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
                player.GetComponent<TouchInputManager>().enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(player,new NeutralInput());
                var body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
                body.linearVelocity=Vector3.forward*110;player.ConfigureTestingMovement(110,player.ForwardAcceleration,player.SteeringAcceleration,player.SteeringSpeedLimit);
                Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;kicks=player.GetComponent<BikeImpactResponse>().KickCount;
                var material=Resources.Load<Material>("EnemyArrivalFade");Require(material!=null && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader),"Arrival shader must be bundled and compile");
                StartArrival(true);stage=1;return;
            }
            Require(gameplay.gameState==GameState.Playing || stage==3,"Arrival must not crash the player");
            if(enemy!=null && enemy.HasInitialized && stage!=3) {
                Require(Vector3.Distance(previous,enemy.transform.position)<Mathf.Max(10,Time.deltaTime*400),"Arrivals and promotion must move continuously");previous=enemy.transform.position;
                if(enemy.IsArriving) {
                    Require(enemy.FiredShotCount==0,"Arrival must not shoot before settling");
                    float health=enemy.CurrentHealth;enemy.TakeDamage(new DamageInfo(10000,player.gameObject));Require(health==enemy.CurrentHealth && !enemy.IsFinisherReady,"Arrival must not enter combat early");
                    var aim=player.GetComponent<BikeAutoAim>();Require(!(bool)typeof(BikeAutoAim).GetMethod("IsEligible",Private).Invoke(aim,new object[]{enemy,Vector3.zero}),"Player auto aim must wait for the arrival");
                }
            }
            if(stage==1 && director.SpawnedCount==1) {
                enemy=director.Riders[0];previous=enemy.transform.position;
                Require(director.RearArrivalsSpawned==1 && enemy.ArrivingFromBehind,"Rear-only setting must introduce a rear rider");
                Require(director.LastSpawnPosition.z<player.transform.position.z-25,"Rear arrival must begin behind the player");
                Require(!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(Camera.main),new Bounds(director.LastSpawnPosition+Vector3.up*1.5f,new Vector3(4,4,6))),"Rear spawn must be outside the gameplay camera");
                stage=2;
            } else if(stage==2 && enemy.HasInitialized) {
                float gap=enemy.transform.position.z-player.transform.position.z;
                if(gap> -28 && gap< -18){pausedPosition=enemy.transform.position;pausedVisibility=enemy.ArrivalVisibility;gameplay.SwitchGameState(GameState.Pause);pauseAt=EditorApplication.timeSinceStartup;stage=3;}
            } else if(stage==3) {
                Require(enemy.transform.position==pausedPosition && enemy.ArrivalVisibility==pausedVisibility,"Pause must freeze approach movement and fade");
                if(EditorApplication.timeSinceStartup-pauseAt>.15){gameplay.SwitchGameState(GameState.Playing);stage=4;}
            } else if(stage==4) {
                float gap=enemy.transform.position.z-player.transform.position.z;
                if(Mathf.Abs(gap)<4) {
                    Require(Mathf.Abs(enemy.transform.position.x-player.transform.position.x)>=2.2f,"Overtake must pass beside the rider");
                    if(!sawPass){Capture("RearOvertake");sawPass=true;}
                }
                var screen=Camera.main.WorldToViewportPoint(enemy.transform.position+Vector3.up*1.5f);
                if(sawPass && !capturedEntry && screen.z>0 && screen.x>.02f && screen.x<.98f && screen.y>0 && screen.y<1) {
                    Capture("RearEnteringView");capturedEntry=true;
                }
                if(director.ActiveFighter==enemy) {
                    Require(sawPass && !enemy.IsArriving && !enemy.IsEncounterQueued,"Rear rider must pass before becoming the fighter");
                    Require(player.GetComponent<BikeImpactResponse>().KickCount==kicks && Mathf.Abs(player.transform.position.x)<.15f,"Overtake must not kick or shove the player");
                    CheckOriginalMaterials();
                    Require(world.TryGetLaneFrame(player.transform.position-Vector3.forward*45,out var frame) && frame.IsCurrent && frame.ContainsForward(player.transform.position-Vector3.forward*45),"Passed road must remain available for rear arrivals across section changes");
                    Capture("RearSettled");StartArrival(false);stage=5;
                }
            } else if(stage==5 && director.SpawnedCount==1) {
                enemy=director.Riders[0];previous=enemy.transform.position;frontGap=enemy.transform.position.z-player.transform.position.z;
                Require(director.FrontArrivalsSpawned==1 && enemy.IsArriving && !enemy.ArrivingFromBehind && frontGap>=170,"Front arrival must start far ahead");stage=6;
            } else if(stage==6) {
                Require(enemy.ArrivalVisibility>=lastVisibility-.001f,"Arrival visibility must ramp continuously");lastVisibility=enemy.ArrivalVisibility;
                if(lastVisibility>.05f && lastVisibility<.9f){sawFade=true;Capture("FrontFadeIn");}
                if(director.ActiveFighter==enemy) {
                    Require(sawFade && !enemy.IsArriving && enemy.transform.position.z-player.transform.position.z<frontGap-80,"Front rider must approach naturally before fighting");
                    CheckOriginalMaterials();Capture("FrontSettled");
                    enemy.TakeDamage(new DamageInfo(10000,player.gameObject));Require(enemy.IsFinisherReady && enemy.DefeatFromSword(player.gameObject,0,.03f),"An arrived fighter must use normal stagger and finisher combat");
                    Finish(true,"PASS: rear rider spawns outside the gameplay camera on retained generated road, catches up and passes beside the player without kicks or warps, then joins combat; front rider starts far ahead, fades in and closes naturally before combat; original materials restored; neither arrival can fire, take combat damage or attract auto aim; pause freezes movement and fade; retained rear road works across section changes; arrived rider supports the normal pistol-stagger/sword finisher. Native captures saved.");
                }
            }
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void CheckOriginalMaterials() {
        foreach(var renderer in enemy.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)
            Require(material==null || material.shader.name!="CyberBiker/Enemy Arrival Fade","Arrival must restore original materials after fade");
    }
    private static void Capture(string name) {
        var camera=Camera.main;var previous=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/EnemyArrivals/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);}
    }
    private static void Finish(bool success,string message) {
        if(finishing)return;finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key,false);
        Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;File.WriteAllText("Logs/EnemyArrivals/result.txt",message);Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);
    }
}
