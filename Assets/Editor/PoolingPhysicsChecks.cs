using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using AI;
using CyberBiker.Combat;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PoolingPhysicsChecks {
    private const string Pending="CyberBiker.PoolingPhysicsChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] private sealed class Metric {public string name;public int cycles;public double millisecondsPerCycle;}
    [Serializable] private sealed class Report {public string label;public List<Metric> timings=new List<Metric>();public List<string> checks=new List<string>();public List<string> tileScripts=new List<string>();}
    private static Report report;
    private static bool finishing;
    private static double started;
    private static Func<GameObject,Vector3,Quaternion,Transform,GameObject> rent;
    private static Action<GameObject> release;
    private static Type poolType;
    private static PlayerController player;
    private static GameplayManager manager;
    static PoolingPhysicsChecks(){if(SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Attach(){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);report.checks.Add("PASS "+message);}
    private static void Measure(string name,int count,Action action){for(int i=0;i<4;i++)action();var watch=Stopwatch.StartNew();for(int i=0;i<count;i++)action();watch.Stop();report.timings.Add(new Metric{name=name,cycles=count,millisecondsPerCycle=watch.Elapsed.TotalMilliseconds/count});}
    private static void Tick(){if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<.7f)return;EditorApplication.update-=Tick;try {
        report=new Report{label="baseline"};var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]=="-poolingLabel")report.label=args[i+1];
        player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();
        foreach(var s in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))s.enabled=false;
        poolType=Type.GetType("CyberBiker.Pooling.PrefabPool, Assembly-CSharp");
        if(poolType==null){rent=(p,v,q,t)=>UnityEngine.Object.Instantiate(p,v,q,t);release=o=>UnityEngine.Object.DestroyImmediate(o);}
        else {rent=(Func<GameObject,Vector3,Quaternion,Transform,GameObject>)Delegate.CreateDelegate(typeof(Func<GameObject,Vector3,Quaternion,Transform,GameObject>),poolType.GetMethod("Rent"));release=(Action<GameObject>)Delegate.CreateDelegate(typeof(Action<GameObject>),poolType.GetMethod("Release"));}
        var bulletPrefab=(GameObject)typeof(PlayerController).GetField("bullet",Private).GetValue(player);
        var world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();
        var carPrefab=(GameObject)new SerializedObject(world).FindProperty("cars").GetArrayElementAtIndex(0).objectReferenceValue;
        var tilePrefab=(GameObject)typeof(WorldGenerator).GetField("startTile",Private).GetValue(world);
        var muzzle=UnityEngine.Object.FindAnyObjectByType<MuzzleFlash>();var flashPrefab=(GameObject)typeof(MuzzleFlash).GetField("muzzleFlash",Private).GetValue(muzzle);
        foreach(var tile in (GameObject[])typeof(WorldGenerator).GetField("_activeTiles",Private).GetValue(world))foreach(var script in tile.GetComponentsInChildren<MonoBehaviour>(true))if(script!=null && !report.tileScripts.Contains(script.GetType().FullName))report.tileScripts.Add(script.GetType().FullName);
        var remote=new Vector3(0,1000,0);
        Measure("Projectile create/initialize/retire",100,()=>{var go=rent(bulletPrefab,remote,Quaternion.identity,null);go.GetComponent<Bullet>().Initialize(player.gameObject,15);release(go);});
        var carStart=typeof(Car).GetMethod("Start",Private);
        Measure("Traffic car create/initialize/retire",50,()=>{var go=rent(carPrefab,remote,Quaternion.identity,null);carStart.Invoke(go.GetComponent<Car>(),null);release(go);});
        Measure("Road tile create/retire",8,()=>{var go=rent(tilePrefab,remote,Quaternion.identity,null);release(go);});
        Measure("Muzzle flash create/retire",100,()=>{var go=rent(flashPrefab,remote,Quaternion.identity,null);release(go);});
        CheckPaths();
        Finish(true,JsonUtility.ToJson(report,true));
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void CheckPaths(){
        var ai=UnityEngine.Object.FindAnyObjectByType<AIController>();var saved=ai.dodgePoints;var made=new List<GameObject>();
        float angle=ai.dodgeAngleLimit,radius=(float)typeof(AIController).GetField("collisionRadius",Private).GetValue(ai);
        try {
            ai.dodgePoints=new List<DodgePoint>();var rootObject=new GameObject("Path query root");rootObject.tag="Enemy";rootObject.transform.position=new Vector3(0,1000,0);made.Add(rootObject);
            for(int i=0;i<12;i++){var anchor=new GameObject("Path candidate");anchor.transform.position=new Vector3((i%3-1)*8,1000,10+i*5);made.Add(anchor);ai.dodgePoints.Add(new DodgePoint(anchor.transform.position,anchor));}
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.layer=LayerMask.NameToLayer("WorldObject");blocker.transform.position=new Vector3(0,1000,24);blocker.transform.localScale=new Vector3(3,3,3);made.Add(blocker);Physics.SyncTransforms();
            var reset=typeof(DodgePoint).GetMethod("Reset");var root=new DodgePoint(rootObject.transform.position,rootObject);
            for(int i=0;i<60;i++){
                var origin=rootObject.transform.position+Vector3.right*(i%5-2);var expected=new List<DodgePoint>();ai.dodgeAngleLimit=i%3==0 ? 0 : i%3==1 ? 45 : 90;
                foreach(var candidate in ai.dodgePoints){var p=candidate.GetPosition();if(p.z>origin.z && Mathf.Abs(Vector3.Angle(p-origin,Vector3.forward))<ai.dodgeAngleLimit && !Physics.CheckCapsule(origin,p,radius,LayerMask.GetMask("WorldObject")))expected.Add(candidate);}
                UnityEngine.Random.InitState(753+i);var selected=expected.Count==0 ? null : expected[UnityEngine.Random.Range(0,expected.Count)];UnityEngine.Random.InitState(753+i);
                if(reset!=null)reset.Invoke(root,new object[]{origin,rootObject});else root=new DodgePoint(origin,rootObject);
                if(ai.GetPath(root)!=selected)throw new Exception("Path selection changed with the same geometry and random seed at case "+i);
            }
            report.checks.Add("PASS 60 path decisions match the original angle/capsule/selection rules, including blocked routes and 0/45/90-degree limits");
            ai.dodgeAngleLimit=45;
            var resetRoot=reset!=null ? (Action<Vector3,GameObject>)Delegate.CreateDelegate(typeof(Action<Vector3,GameObject>),root,reset) : null;
            Action query=()=>{if(resetRoot!=null)resetRoot(rootObject.transform.position,rootObject);else root=new DodgePoint(rootObject.transform.position,rootObject);ai.GetPath(root);};
            Measure("AI path query with reusable/fresh root",500,query);
            Require(ai.dodgePoints.Count==12,"Path queries preserve the shared navigation list");
        } finally {ai.dodgePoints=saved;ai.dodgeAngleLimit=angle;foreach(var go in made)UnityEngine.Object.DestroyImmediate(go);}
    }
    private static void Finish(bool ok,string content){if(finishing)return;finishing=true;SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Directory.CreateDirectory("Logs/Pooling");File.WriteAllText("Logs/Pooling/"+(report!=null ? report.label : "error")+(ok ? ".json" : "-error.txt"),content);if(ok && Array.IndexOf(Environment.GetCommandLineArgs(),"-includePoolLifecycle")>=0)PoolLifecycleChecks.BeginInCurrentScene();else EditorApplication.Exit(ok ? 0 : 1);}
}
