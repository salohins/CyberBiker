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
public static class EnemyEncounterChecks {
    private const string Key="CyberBiker.EncounterChecks";
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class NeutralInput : IPlayerInput {
        public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;
    }
    private static EnemyEncounterDirector director;
    private static Difficulty settings,next;
    private static DifficultyManager manager;
    private static GameplayManager gameplay;
    private static PlayerController player;
    private static WorldGenerator world;
    private static Enemy expected;
    private static int stage,killed,spawned,previousWaves;
    private static float beganStage,lastKill,pausedTimer;
    private static double began,pauseAt;
    private static bool finishing,sawHandoff;
    private static readonly Dictionary<Enemy,Vector3> positions=new Dictionary<Enemy,Vector3>();
    static EnemyEncounterChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run(){Directory.CreateDirectory("Logs/EnemyEncounters");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Advance(){stage++;beganStage=Time.time;File.AppendAllText("Logs/EnemyEncounters/progress.txt","Stage "+stage+"\n");}
    private static Difficulty Settings(string name,int size,int cap) {
        var result=new GameObject(name).AddComponent<Difficulty>();result.carOffsetModifier=40;result.oneCarRow=2;result.twoCarRow=1;result.threeCarRow=0;
        result.enemiesPerWave=size;result.maxEnemiesOnRoad=cap;result.enemyInitialDelay=.4f;result.enemySpawnInterval=.15f;
        result.enemyWaveCooldown=1.2f;result.enemyHandoffDelay=.1f;result.enemySpawnDistance=100;result.enemyQueueSpacing=16;
        result.enemyRearArrivalPercent=0;result.enemyArrivalClosingSpeed=120;result.enemyArrivalFadeTime=.2f;return result;
    }
    private static void Kill(Enemy enemy) {
        Require(enemy!=null && enemy.HasInitialized && enemy.IsAlive && !enemy.IsEncounterQueued,"Only the initialized active fighter may be finished");
        enemy.TakeDamage(new DamageInfo(10000,player.gameObject));Require(enemy.IsFinisherReady,"Active fighter must use the existing pistol stagger");
        Require(enemy.DefeatFromSword(player.gameObject,0,.03f),"Existing sword finisher must kill the fighter");
    }
    private static void Tick() {
        if(finishing || !EditorApplication.isPlaying)return;
        try {
            if(EditorApplication.timeSinceStartup-began>150)throw new Exception("Encounter timeout at stage "+stage+" riders="+(director!=null ? director.Riders.Count : -1));
            if(stage==0) {
                player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();
                if(player==null || player.Health==null || world==null || world.GetComponent<TrafficSpawner>()==null || Time.timeSinceLevelLoad<.25f)return;
                gameplay=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();gameplay.SwitchGameState(GameState.Playing);
                var old=world.GetComponent<EnemyEncounterDirector>();if(old!=null){old.enabled=false;UnityEngine.Object.DestroyImmediate(old);}
                foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
                foreach(var shape in player.GetComponentsInChildren<Collider>(true))shape.enabled=false;
                player.GetComponent<TouchInputManager>().enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(player,new NeutralInput());
                var body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
                player.ConfigureTestingMovement(110,player.ForwardAcceleration,player.SteeringAcceleration,player.SteeringSpeedLimit);body.linearVelocity=Vector3.forward*110;
                Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;
                manager=UnityEngine.Object.FindAnyObjectByType<DifficultyManager>();settings=Settings("Five riders, three visible",5,3);manager.currentDifficulty=settings;
                director=world.gameObject.AddComponent<EnemyEncounterDirector>();
                director.Initialize((GameObject)typeof(WorldGenerator).GetField("enemy",Private).GetValue(world),player.gameObject,world,manager,world.GetComponent<TrafficSpawner>());
                Advance();return;
            }
            Require(gameplay.gameState==GameState.Playing || stage==5,"Encounter test must keep the player riding");
            CheckRolesAndRoad();
            if(stage==1) {
                if(Time.time-beganStage<.25f)Require(director.SpawnedCount==0,"Initial delay must not depend on generating a car row");
                if(director.Riders.Count==3){Require(director.RemainingToSpawn==2,"A five-enemy wave must respect a three-rider cap");Advance();}
            } else if(stage==2 && Time.time-beganStage>3.5f) {
                Require(director.SpawnedCount==3,"A full queue must not keep spawning enemies");
                Require(director.ActiveFighter.FiredShotCount>0,"The active rider must still fight with its original weapon/animation system");
                foreach(var enemy in director.Riders)if(enemy.IsEncounterQueued) {
                    Require(enemy.FiredShotCount==0,"Waiting riders must not shoot");float health=enemy.CurrentHealth;
                    enemy.TakeDamage(new DamageInfo(10000,player.gameObject));Require(enemy.CurrentHealth==health && !enemy.IsFinisherReady,"Waiting riders must not become accidental finisher targets");
                    typeof(Enemy).GetMethod("TakeGun",Private).Invoke(enemy,null);typeof(Enemy).GetMethod("GunShoot",Private).Invoke(enemy,new object[]{0});
                    Require(enemy.FiredShotCount==0,"Animation events must not arm or fire a queued rider");
                    var aim=player.GetComponent<BikeAutoAim>();var eligible=typeof(BikeAutoAim).GetMethod("IsEligible",Private);
                    Require(!(bool)eligible.Invoke(aim,new object[]{enemy,Vector3.zero}),"Auto aim must skip the waiting queue");
                    float gap=enemy.transform.position.z-player.transform.position.z;
                    Require(gap>gameplay.EnemyDistanceFromPlayer+6,"Waiting riders must hold a distinct position beyond the fighter; gap="+gap);
                }
                Capture("QueuedRiders");lastKill=-10;Advance();
            } else if(stage==3) {
                if(expected!=null && director.ActiveFighter==expected){Require(!expected.IsEncounterQueued,"Next rider must be promoted without recreating it");sawHandoff=true;expected=null;}
                if(killed<5 && expected==null && director.ActiveFighter!=null && director.ActiveFighter.HasInitialized && Time.time-lastKill>.45f) {
                    foreach(var candidate in director.Riders)if(candidate.IsEncounterQueued){expected=candidate;break;}
                    Kill(director.ActiveFighter);killed++;lastKill=Time.time;
                }
                if(killed==5 && director.Riders.Count==0 && director.WaveCooldownRemaining>0) {
                    Require(sawHandoff && director.SpawnedCount==5 && director.RemainingToSpawn==0,"All five riders must be consumed exactly once, refilling the queue as needed");
                    previousWaves=director.WavesStarted;Advance();
                }
            } else if(stage==4 && Time.time-beganStage>.15f) {
                Require(director.WavesStarted==previousWaves && director.SpawnedCount==5,"The inter-wave rest must wait until the whole wave is cleared");
                pausedTimer=director.WaveCooldownRemaining;gameplay.SwitchGameState(GameState.Pause);pauseAt=EditorApplication.timeSinceStartup;Advance();
            } else if(stage==5) {
                Require(director.WaveCooldownRemaining==pausedTimer && director.WavesStarted==previousWaves,"Pause must freeze the encounter timer");
                if(EditorApplication.timeSinceStartup-pauseAt>.15) {
                    next=Settings("Next difficulty: two riders",2,2);next.enemySpawnInterval=0;next.enemyWaveCooldown=0;next.enemyHandoffDelay=0;
                    manager.currentDifficulty=next;gameplay.SwitchGameState(GameState.Playing);Advance();
                }
            } else if(stage==6 && director.SpawnedCount==7 && director.Riders.Count==2) {
                Require(director.WavesStarted==previousWaves+1 && director.RemainingToSpawn==0,"The next wave must read the new difficulty's wave size and cap");
                next.enemiesPerWave=0;Advance();
            } else if(stage==7) {
                if(director.ActiveFighter!=null && director.ActiveFighter.HasInitialized && director.ActiveFighter.IsAlive && !director.ActiveFighter.IsEncounterQueued)Kill(director.ActiveFighter);
                if(director.Riders.Count==0){spawned=director.SpawnedCount;Advance();}
            } else if(stage==8 && Time.time-beganStage>.8f) {
                Require(director.SpawnedCount==spawned,"A peaceful section must stop new waves");
                next.enemiesPerWave=1;next.maxEnemiesOnRoad=1;next.enemyInitialDelay=0;next.enemySpawnDistance=35;Advance();
            } else if(stage==9 && director.SpawnedCount==spawned+1 && director.ActiveFighter!=null && director.ActiveFighter.HasInitialized) {
                Kill(director.ActiveFighter);Advance();
            } else if(stage==10 && director.SpawnedCount==spawned+2) {
                Require(Time.time-beganStage<1,"Zero cooldown must allow a back-to-back solo encounter");
                Finish(true,"PASS: enemy spawning runs independently of traffic rows; initial delay; five-rider wave with a three-rider cap and queue refill; exactly one fighting/targetable rider; waiting riders navigate on the road, hold separate gaps, cannot shoot or stagger; actual pistol-to-sword defeats promote existing queued actors without teleports; exact wave total; cooldown only after whole wave; pause freezes timer; next difficulty controls next wave; peaceful section disables spawning; zero-cooldown solo encounters work. Native queued-rider capture saved.");
            }
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void CheckRolesAndRoad() {
        int fighters=0;foreach(var enemy in director.Riders) {
            if(enemy==null || !enemy.HasInitialized || !enemy.IsAlive)continue;
            typeof(Enemy).GetField("shootingDamage",Private).SetValue(enemy,0f);
            if(!enemy.IsEncounterQueued)fighters++;
            if(world.TryGetLaneFrame(enemy.transform.position,out var frame) && frame.IsCurrent) {
                float x=Vector3.Dot(enemy.transform.position-frame.origin,frame.right);
                Require(x>=frame.min-.2f && x<=frame.max+.2f,"Queued enemy must remain on the road");
            }
            if(positions.TryGetValue(enemy,out var previous))Require(Vector3.Distance(previous,enemy.transform.position)<Mathf.Max(10,Time.deltaTime*400),"Queue handoff must not warp enemy position");
            positions[enemy]=enemy.transform.position;
        }
        Require(fighters<=1,"There may only be one active fighter");
        Require(director.Riders.Count<=(stage<=5 ? 3 : stage<=8 ? 2 : 1),"Visible population must respect the current wave's cap");
    }
    private static void Capture(string name) {
        var camera=Camera.main;var previous=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/EnemyEncounters/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);}
    }
    private static void Finish(bool success,string message) {
        if(finishing)return;finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key,false);
        FinisherClock.EditorCaptureStep=0;Time.captureDeltaTime=0;File.WriteAllText("Logs/EnemyEncounters/result.txt",message);Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);
    }
}
