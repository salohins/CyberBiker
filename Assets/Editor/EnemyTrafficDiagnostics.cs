using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using AI;
using CyberBiker.Combat;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyTrafficDiagnostics {
    private const string Key="CyberBiker.TrafficDiagnostics";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Neutral:IPlayerInput{public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;}
    private static readonly Dictionary<Enemy,float> blocked=new Dictionary<Enemy,float>();
    private static PlayerController player;private static AIController ai;private static double started;private static float began;private static int lastFrame=-1;private static bool initialized,finishing,launched;private static int captureRate=60;private static readonly HashSet<Enemy> arrivalsSeen=new HashSet<Enemy>();
    static EnemyTrafficDiagnostics(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){Directory.CreateDirectory("Logs/EnemyTrafficDiagnosis");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Tick(){if(finishing || !EditorApplication.isPlaying || lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;try{
        if(EditorApplication.timeSinceStartup-started>180)throw new Exception("Diagnostic timed out");
        if(!initialized){player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();if(player==null || player.Health==null || Time.timeSinceLevelLoad<.4f)return;
            UnityEngine.Object.FindAnyObjectByType<GameplayManager>().SwitchGameState(GameState.Playing);ai=UnityEngine.Object.FindAnyObjectByType<AIController>();
            player.GetComponent<TouchInputManager>().enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(player,new Neutral());foreach(var c in player.GetComponentsInChildren<Collider>(true))c.enabled=false;
            var body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;body.linearVelocity=Vector3.forward*player.NormalSpeedLimit;
            var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]=="-trafficCaptureRate")captureRate=int.Parse(args[i+1]);
            Time.captureDeltaTime=1f/captureRate;FinisherClock.EditorCaptureStep=1f/captureRate;began=Time.time;initialized=true;return;}
        foreach(var enemy in Enemy.Instances){if(enemy==null || !enemy.isActiveAndEnabled || !enemy.IsAlive)continue;typeof(Enemy).GetField("shootingDamage",Private).SetValue(enemy,0f);
            if(enemy.IsArriving)arrivalsSeen.Add(enemy);
            float playerSpeed=player.GetComponent<Rigidbody>().linearVelocity.z;
            bool stopped=enemy.ArrivalVisibility>.99f && enemy.ForwardSpeed>playerSpeed*.6f && enemy.DrivingVelocity.z<playerSpeed*.25f;
            blocked.TryGetValue(enemy,out float duration);duration=stopped ? duration+Time.deltaTime : 0;blocked[enemy]=duration;
            if(duration>.35f){Dump(enemy);Finish(true,"REPRODUCED a sustained traffic stall with the saved scene, all arrivals included. See Stalled.txt.");return;}
        }
        if(!launched && Time.time-began>20){var sword=player.GetComponent<BikeSwordCombat>();sword.TestingLaunchDuration=25;sword.Launch();launched=true;}
        if(Time.time-began>65)Finish(true,"No sustained stall in this 65-second saved-scene run; capture rate="+captureRate+"; riders observed="+blocked.Count+"; arriving riders observed="+arrivalsSeen.Count+"; Launch tested="+launched+".");
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void Dump(Enemy enemy){
        var text=new StringBuilder();text.AppendLine("time="+Time.time+" player="+player.transform.position+" speed="+player.GetComponent<Rigidbody>().linearVelocity);
        text.AppendLine("enemy="+enemy.name+" pos="+enemy.transform.position+" velocity="+enemy.DrivingVelocity+" requested="+enemy.ForwardSpeed+" yaw="+enemy.transform.eulerAngles+" arriving="+enemy.IsArriving+" behindArrival="+enemy.ArrivingFromBehind+" queued="+enemy.IsEncounterQueued);
        var path=(DodgePoint)typeof(Enemy).GetField("path",Private).GetValue(enemy);text.AppendLine("path="+(path!=null ? path.GetPosition()+" owner="+path.offsetObject.name : "null")+" follow="+typeof(Enemy).GetField("followTarget",Private).GetValue(enemy));
        text.AppendLine("angleLimit="+ai.dodgeAngleLimit+" radius="+typeof(AIController).GetField("collisionRadius",Private).GetValue(ai));
        foreach(var car in Car.Instances)if(car!=null && car.TryGetTrafficShape(Time.fixedDeltaTime,out var bounds,out var delta) && Mathf.Abs(car.transform.position.z-enemy.transform.position.z)<100)text.AppendLine("CAR "+car.name+" pos="+car.transform.position+" bounds="+bounds+" step="+delta);
        foreach(var point in ai.dodgePoints)if(point.IsValid && Mathf.Abs(point.GetPosition().z-enemy.transform.position.z)<150)text.AppendLine("POINT "+point.GetPosition()+" owner="+point.offsetObject.name+" angle="+Vector3.Angle(point.GetPosition()-enemy.transform.position,Vector3.forward));
        File.WriteAllText("Logs/EnemyTrafficDiagnosis/Stalled.txt",text.ToString());Debug.Log(text.ToString());
    }
    private static void Finish(bool ok,string message){if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;File.WriteAllText("Logs/EnemyTrafficDiagnosis/Run.txt",message);Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(ok ? 0 : 1);}
    public static void VerifyFix(){
        RunClearance();
        if(!File.ReadAllText("Logs/EnemyTrafficDiagnosis/Clearance.txt").Contains("collision guard rejected=0."))throw new Exception("Contact safeguard still rejects original-planner-clear routes");
        if(!File.ReadAllText("Logs/EnemyTrafficDiagnosis/Topology.txt").Contains("selected private road point=False"))throw new Exception("Private waypoint still breaks the original onward angle constraint");
    }
    public static void RunClearance(){
        Directory.CreateDirectory("Logs/EnemyTrafficDiagnosis");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"));
        var car=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Cars/Car_01.prefab")).GetComponent<Car>();
        typeof(Car).GetField("box",Private).SetValue(car,car.GetComponent<BoxCollider>());typeof(Car).GetField("vitality",Private).SetValue(car,new Health(100));car.enabled=false;
        var registry=(List<Car>)typeof(Car).GetField("activeCars",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);if(!registry.Contains(car))registry.Add(car);
        var type=typeof(Enemy).Assembly.GetType("EnemyTrafficAvoidance");var guard=Activator.CreateInstance(type,new object[]{enemy});var constrain=type.GetMethod("Constrain");int valid=0,rejected=0;string first="";
        try{for(int length=30;length<=90;length+=5)for(int side=0;side<=20;side++){
            var start=new Vector3(0,1000,0);var end=start+new Vector3(6,0,length);var direction=(end-start).normalized;if(Vector3.Angle(direction,Vector3.forward)>=15)continue;
            car.transform.SetPositionAndRotation(new Vector3(-1+side*.15f,1000,20),Quaternion.identity);Physics.SyncTransforms();
            if(Physics.CheckCapsule(start,end,1,LayerMask.GetMask("WorldObject")))continue;valid++;bool cut=false;
            enemy.transform.rotation=Quaternion.LookRotation(direction);
            for(float distance=0;distance<Vector3.Distance(start,end);distance+=.2f){enemy.transform.position=start+direction*distance;var next=enemy.transform.position+direction*1.5f;var safe=(Vector3)constrain.Invoke(guard,new object[]{next,.02f});if(Vector3.Distance(safe,next)>.02f){cut=true;if(first=="")first="Original radius-1 capsule accepted "+start+" -> "+end+" at "+Vector3.Angle(direction,Vector3.forward)+"deg; car="+car.transform.position+"; contact safeguard blocks at "+enemy.transform.position+" by "+Vector3.Distance(safe,next)+"m.";break;}}
            if(cut)rejected++;
        }
        File.WriteAllText("Logs/EnemyTrafficDiagnosis/Clearance.txt","Original planner accepted="+valid+"; collision guard rejected="+rejected+".\n"+first+"\n");
        car.transform.position=new Vector3(100,1000,20);enemy.transform.position=new Vector3(0,1000,0);Physics.SyncTransforms();
        var planner=new GameObject("Original 15 degree planner").AddComponent<AIController>();typeof(AIController).GetMethod("Awake",Private).Invoke(planner,null);planner.dodgeAngleLimit=15;
        typeof(AIController).GetField("collisionRadius",Private).SetValue(planner,1f);
        var anchor=new GameObject("Next car-generated point");anchor.transform.position=new Vector3(-6,1000,80);
        var nextPoint=new DodgePoint(anchor.transform.position,anchor);planner.dodgePoints.Add(nextPoint);
        var root=new DodgePoint(enemy.transform.position,enemy);var road=new DodgePoint(new Vector3(6,1000,50),enemy);
        bool originalReachable=planner.GetPath(root)==nextPoint;var selected=planner.GetPath(root,road);
        root.Reset(road.GetPosition(),enemy);bool continuation=planner.GetPath(root)!=null;
        File.WriteAllText("Logs/EnemyTrafficDiagnosis/Topology.txt","Original next car point reachable="+originalReachable+"; selected private road point="+(selected==road)+"; car-point continuation reachable from road point="+continuation+"; required onward angle="+Vector3.Angle(nextPoint.GetPosition()-road.GetPosition(),Vector3.forward)+" degrees vs allowed 15.\n");
        UnityEngine.Object.DestroyImmediate(anchor);UnityEngine.Object.DestroyImmediate(planner.gameObject);
        }
        finally{UnityEngine.Object.DestroyImmediate(enemy);UnityEngine.Object.DestroyImmediate(car.gameObject);}
    }
}
