using System;
using System.IO;
using System.Reflection;
using AI;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyPursuitChecks {
    private const string Key="CyberBiker.PursuitChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class NeutralInput:IPlayerInput {public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;}
    private static PlayerController player;private static Rigidbody body;private static Enemy enemy;private static Car car;private static AIController ai;private static WorldGenerator world;private static Difficulty style;
    private static EnemyEncounterDirector director;private static TrafficSpawner spawner;private static int stage,scenario,samples,behindSamples,lastFrame=-1;private static float at,minGap,worstGap;private static double began;private static bool finishing,baseline;private static string report="";
    static EnemyPursuitChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){Directory.CreateDirectory("Logs/EnemyPursuit");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){began=EditorApplication.timeSinceStartup;baseline=Array.IndexOf(Environment.GetCommandLineArgs(),"-pursuitBaseline")>=0;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void BeginCase() {
        if(enemy!=null)UnityEngine.Object.DestroyImmediate(enemy.gameObject);if(car!=null)UnityEngine.Object.DestroyImmediate(car.gameObject);ai.dodgePoints.Clear();
        var p=player.transform.position+Vector3.forward*20;world.TryGetLaneFrame(p,out var frame);p.x=frame.origin.x+(frame.min+frame.max)*.5f;
        enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),p,Quaternion.identity).GetComponent<Enemy>();enemy.SetRidingLane(.5f,style);
        typeof(Enemy).GetField("shootingDamage",Private).SetValue(enemy,0f);
        car=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Cars/Car_01.prefab"),p+Vector3.forward*30,Quaternion.identity).GetComponent<Car>();car.speed=0;
        at=Time.time;minGap=20;stage=2;
    }
    private static void Tick(){if(finishing || !EditorApplication.isPlaying)return;try {
        Require(EditorApplication.timeSinceStartup-began<100,"Pursuit timeout stage="+stage);
        if(stage==0){player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();if(player==null || player.Health==null || world==null || world.GetComponent<TrafficSpawner>()==null || Time.timeSinceLevelLoad<.3f)return;
            UnityEngine.Object.FindAnyObjectByType<GameplayManager>().SwitchGameState(GameState.Playing);world.GetComponent<TrafficSpawner>().enabled=false;var director=world.GetComponent<EnemyEncounterDirector>();if(director!=null)director.enabled=false;
            foreach(var actor in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))actor.gameObject.SetActive(false);foreach(var traffic in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))traffic.gameObject.SetActive(false);
            foreach(var collider in player.GetComponentsInChildren<Collider>(true))collider.enabled=false;player.GetComponent<TouchInputManager>().enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(player,new NeutralInput());
            body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;player.ConfigureTestingMovement(110,player.ForwardAcceleration,player.SteeringAcceleration,player.SteeringSpeedLimit);body.linearVelocity=Vector3.forward*110;
            style=new GameObject("Original path pursuit fixture").AddComponent<Difficulty>();style.enemyLaneWeave=0;ai=UnityEngine.Object.FindAnyObjectByType<AIController>();Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;BeginCase();return;
        }
        if(stage==2 && enemy.HasInitialized && car.IsAlive){
            // The actual traffic-spawner algorithm builds these same adjacent
            // rear/front dodge points from Obstacle, not custom test waypoints.
            ai.dodgePoints.AddRange(car.GetComponent<Obstacle>().GetDodgePoints2(scenario%2==0 ? 1 : -1));Physics.SyncTransforms();
            UnityEngine.Random.InitState(953+scenario);typeof(Enemy).GetMethod("ClearPath",Private).Invoke(enemy,null);typeof(Enemy).GetField("state",Private).SetValue(enemy,EnemyState.Attack);stage=3;at=Time.time;
        }else if(stage==3){
            float gap=enemy.transform.position.z-player.transform.position.z;minGap=Mathf.Min(minGap,gap);
            if(Time.time-at<3)return;
            float relative=enemy.transform.position.z-car.transform.position.z;
            report+="Case "+scenario+": minimum player gap="+minGap.ToString("F2")+"m; final gap="+gap.ToString("F2")+"m; distance past car="+relative.ToString("F2")+"m; actual forward speed="+enemy.DrivingVelocity.z.ToString("F1")+".\n";
            if(!baseline){Require(minGap>0,"Player overtook enemy while an original dodge-point escape was available");Require(relative>30,"Enemy failed to pass car on its own path");Require(Mathf.Abs(gap-enemy.playerDistance)<4,"Enemy must recover the global following distance");}
            if(++scenario<2){BeginCase();return;}
            if(baseline){Finish(true,report);return;}
            UnityEngine.Object.DestroyImmediate(enemy.gameObject);UnityEngine.Object.DestroyImmediate(car.gameObject);ai.dodgePoints.Clear();
            spawner=world.GetComponent<TrafficSpawner>();spawner.enabled=true;
            var old=world.GetComponent<EnemyEncounterDirector>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            var manager=UnityEngine.Object.FindAnyObjectByType<DifficultyManager>();style.enemiesPerWave=5;style.maxEnemiesOnRoad=5;style.enemyInitialDelay=0;style.enemySpawnInterval=.15f;style.enemyRearArrivalPercent=0;style.enemySpawnDistance=120;style.enemyArrivalClosingSpeed=80;style.enemyArrivalFadeTime=.2f;style.carOffsetModifier=25;style.oneCarRow=2;style.twoCarRow=4;style.threeCarRow=1;manager.currentDifficulty=style;
            director=world.gameObject.AddComponent<EnemyEncounterDirector>();director.Initialize((GameObject)typeof(WorldGenerator).GetField("enemy",Private).GetValue(world),player.gameObject,world,manager,spawner);
            at=Time.time;stage=4;samples=behindSamples=0;worstGap=0;
        }else if(stage==4 || stage==5){
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            foreach(var actor in director.Riders)if(actor.HasInitialized){
                typeof(Enemy).GetField("shootingDamage",Private).SetValue(actor,0f);
                if(!actor.IsArriving && actor.IsAlive){float gap=actor.transform.position.z-player.transform.position.z;samples++;if(gap<0)behindSamples++;worstGap=Mathf.Min(worstGap,gap);}
            }
            if(Time.time-at<18)return;
            Require(samples>300,"Sustained traffic test needs settled rider samples");
            float behindFraction=behindSamples/(float)samples;
            report+=(stage==4 ? "Normal" : "Launch")+" traffic: samples="+samples+", behind player="+(behindFraction*100).ToString("F1")+"%, worst forward gap="+worstGap.ToString("F2")+"m.\n";
            Require(behindFraction<.08f,"Settled enemies repeatedly fall behind in traffic");
            Require(director.ActiveFighter!=null && director.ActiveFighter.transform.position.z>player.transform.position.z,"Active fighter must keep pace through sustained traffic");
            if(stage==4){var sword=player.GetComponent<BikeSwordCombat>();sword.TestingLaunchDuration=25;sword.Launch();Require(sword.IsPursuing,"Actual Launch mode must start");stage=5;at=Time.time;samples=behindSamples=0;worstGap=0;return;}
            Finish(true,report+"PASS original dodge-point routes retain pursuit in normal and actual Launch mode with live generated traffic.\n");
        }
    }catch(Exception error){Finish(false,error+"\n"+report);}}
    private static void Finish(bool ok,string message){if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;File.WriteAllText("Logs/EnemyPursuit/"+(baseline ? "Baseline" : "Fixed")+".txt",message);Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(ok ? 0 : 1);}
}
