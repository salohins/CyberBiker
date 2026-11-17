using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AI;
using CyberBiker.Combat;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RunProgressionChecks {
    private const string Key="CyberBiker.RunProgressionChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class NeutralInput : IPlayerInput {
        public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;
    }
    private static DifficultyManager manager;
    private static GameplayManager gameplay;
    private static EnemyEncounterDirector director;
    private static PlayerController player;
    private static int stage,lastFrame=-1,killed;
    private static float stageTime;
    private static double began;
    private static bool finished;
    static RunProgressionChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool value,string text){if(!value)throw new Exception(text);}
    private static void Advance(DifficultyManager value,float dt)=>typeof(DifficultyManager).GetMethod("AdvanceTime",Private).Invoke(value,new object[]{dt});
    private static void Report(string text)=>File.AppendAllText("Logs/RunProgression/Result.txt","PASS "+text+"\n");
    public static void Run(){
        Directory.CreateDirectory("Logs/RunProgression");File.WriteAllText("Logs/RunProgression/Result.txt","RUNNING\n");
        try{
            EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");
            var m=UnityEngine.Object.FindAnyObjectByType<DifficultyManager>();
            Require(m.difficultySections.Length==8 && m.loopFromSection==6,"Configured progression must have eight stages and repeat the last riding/fighting pair");
            Require(Mathf.Approximately(m.difficultySections.Sum(x=>x.durationSeconds),300),"Opening progression must total five minutes");
            Require(UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length==0,"Pre-placed test enemies must not spoil the peaceful opening");
            Require(m.difficultySections.Select(x=>x.enemiesPerWave).SequenceEqual(new[]{0,1,0,2,0,2,0,3}),"Progression must alternate riding and progressively larger encounters");
            typeof(DifficultyManager).GetMethod("Awake",Private).Invoke(m,null);
            for(int i=0;i<8;i++){
                Require(m.CurrentSectionIndex==i && m.currentDifficulty==m.difficultySections[i],"Wrong section at "+i);
                Advance(m,m.SectionRemaining-.01f);Require(m.CurrentSectionIndex==i,"Transition happened early at "+i);
                Advance(m,.02f);
            }
            Require(m.CurrentSectionIndex==6,"End must return to the final riding break");
            Advance(m,85);Require(m.CurrentSectionIndex==6 && Mathf.Abs(m.SectionElapsed-.01f)<.01f,"Loop must retain overshoot without skipping the break");
            Report("saved Level resolves all eight stage assets; five-minute sequence; correct boundaries and repeating 25-second ride / 60-second fight tail; no active authored enemies.");
            var legacyRoot=new GameObject("Legacy timing fixture");var legacy=legacyRoot.AddComponent<DifficultyManager>();
            legacy.gameDuration=.1f;legacy.difficultySections=new[]{legacyRoot.AddComponent<Difficulty>(),new GameObject("Second legacy").AddComponent<Difficulty>()};
            typeof(DifficultyManager).GetMethod("Awake",Private).Invoke(legacy,null);Advance(legacy,3.5f);
            Require(legacy.CurrentSectionIndex==1 && Mathf.Abs(legacy.SectionElapsed-.5f)<.001f,"Legacy equal-duration progression must retain overshoot");
            Advance(legacy,15);Require(legacy.CurrentSectionIndex==1 && Mathf.Abs(legacy.SectionElapsed-3)<.001f,"Legacy non-looping last difficulty must hold");
            Report("legacy timing remains compatible; long frames preserve elapsed time; last section holds when looping is disabled.");
            EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");
            SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }catch(Exception e){Finish(false,e.ToString());}
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    private static void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Next(){stage++;stageTime=Time.time;Report("runtime stage "+stage);}
    private static void Tick(){
        if(finished || !EditorApplication.isPlaying || lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try{
            Require(EditorApplication.timeSinceStartup-began<100,"Runtime progression check timed out at "+stage+"; spawned="+(director!=null ? director.SpawnedCount : -1)+"; riders="+(director!=null ? director.Riders.Count : -1));
            if(stage==0){
                player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();var world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();
                if(player==null || player.Health==null || world==null || world.GetComponent<EnemyEncounterDirector>()==null || Time.timeSinceLevelLoad<.3f)return;
                gameplay=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();gameplay.SwitchGameState(GameState.Playing);gameplay.TapPistolCanKill=false;
                manager=UnityEngine.Object.FindAnyObjectByType<DifficultyManager>();manager.enabled=false;
                director=world.GetComponent<EnemyEncounterDirector>();
                foreach(var shape in player.GetComponentsInChildren<Collider>(true))shape.enabled=false;
                player.GetComponent<TouchInputManager>().enabled=false;
                var body=player.GetComponent<Rigidbody>();body.linearVelocity=Vector3.forward*110;body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
                player.ConfigureTestingMovement(110,player.ForwardAcceleration,player.SteeringAcceleration,player.SteeringSpeedLimit);
                typeof(PlayerController).GetField("input",Private).SetValue(player,new NeutralInput());
                // Make fixture arrivals deterministic. The saved front/rear mix stays unchanged.
                manager.difficultySections=manager.difficultySections.Select(source=>{
                    var copy=UnityEngine.Object.Instantiate(source.gameObject).GetComponent<Difficulty>();copy.enemyRearArrivalPercent=0;copy.enemyInitialDelay=.1f;copy.enemySpawnInterval=.1f;copy.enemyArrivalClosingSpeed=120;copy.enemyArrivalFadeTime=.2f;return copy;
                }).ToArray();
                manager.currentDifficulty=manager.difficultySections[0];
                typeof(DifficultyManager).GetField("elapsedTime",Private).SetValue(manager,0f);
                Time.captureDeltaTime=.05f;FinisherClock.EditorCaptureStep=.05f;
                Advance(manager,39.9f);Next();return;
            }
            Require(gameplay.gameState==GameState.Playing,"Fixture must stay alive and playing");
            foreach(var enemy in director.Riders)typeof(Enemy).GetField("shootingDamage",Private).SetValue(enemy,0f);
            if(stage==1 && Time.time-stageTime>.5f){
                Require(director.SpawnedCount==0 && director.Riders.Count==0,"No enemies may spawn in the opening ride");
                float before=manager.SectionElapsed;gameplay.SwitchGameState(GameState.Pause);
                typeof(DifficultyManager).GetMethod("Update",Private).Invoke(manager,null);
                Require(manager.SectionElapsed==before,"Pause advanced progression");gameplay.SwitchGameState(GameState.Playing);
                var assist=player.GetComponent<GapDashAssist>();var sword=player.GetComponent<BikeSwordCombat>();
                Require(!assist.Step(0) && assist.DashCount==0,"Normal riding must never engage gap assist");
                sword.Launch();Require(sword.IsPursuing,"Launch did not start");
                Require((bool)typeof(GapDashAssist).GetProperty("Available",Private).GetValue(assist),"Assist must become eligible in Launch");
                sword.Launch();Require(!sword.IsPursuing && !assist.Step(0),"Assist must stop being eligible when Launch ends");
                Report("real Play mode starts without enemies; pause freezes the stage clock; gap assist rejects riding, permits Launch and rejects Launch cancellation.");
                Advance(manager,.2f);Next();
            }else if(stage==2){
                if(director.ActiveFighter!=null && director.ActiveFighter.HasInitialized && !director.ActiveFighter.IsArriving){Kill(director.ActiveFighter);killed++;Next();}
            }else if(stage==3 && director.Riders.Count==0){
                // Expire the cooldown repeatedly: a capped stage still cannot start a second wave.
                typeof(EnemyEncounterDirector).GetField("waveTimer",Private).SetValue(director,0f);
                if(Time.time-stageTime<1)return;
                Require(killed==1 && director.SpawnedCount==1 && director.WavesStarted==1,"Solo stage started more than one wave");
                Report("solo stage spawns and finishes one real enemy; exhausting its cooldown cannot restart the wave.");
                Advance(manager,30);Require(manager.CurrentSectionIndex==2,"First recovery section missing");Next();
            }else if(stage==4 && Time.time-stageTime>.5f){
                Require(director.SpawnedCount==1 && director.RemainingToSpawn==0,"Riding break spawned another enemy");
                Advance(manager,30);Require(manager.CurrentSectionIndex==3,"Second encounter missing");Next();
            }else if(stage==5){
                if(director.ActiveFighter!=null && director.ActiveFighter.HasInitialized && !director.ActiveFighter.IsArriving){Kill(director.ActiveFighter);killed++;}
                if(killed==3 && director.Riders.Count==0){
                    Require(director.SpawnedCount==3,"Pair stage must add exactly two enemies");
                    Advance(manager,40);Require(manager.currentDifficulty.enemiesPerWave==0,"Next riding break missing");Next();
                }
            }else if(stage==6 && Time.time-stageTime>.5f){
                Require(director.SpawnedCount==3 && director.RemainingToSpawn==0,"Enemies leaked into the next break");
                Report("riding break clears pending spawns; second encounter consumes two enemies; next break remains peaceful.");Finish(true,"PASS native progression and encounter integration checks.");
            }
        }catch(Exception e){Finish(false,e.ToString());}
    }
    private static void Kill(Enemy enemy){enemy.TakeDamage(new DamageInfo(10000,player.gameObject));Require(enemy.IsFinisherReady,"Expected existing finisher health floor");Require(enemy.DefeatFromSword(player.gameObject,0,.01f),"Could not finish active enemy");}
    private static void Finish(bool success,string text){if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;FinisherClock.EditorCaptureStep=0;Time.captureDeltaTime=0;File.AppendAllText("Logs/RunProgression/Result.txt",text+"\n");Debug.Log(text);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
