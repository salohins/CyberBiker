using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using CyberBiker.Audio;
using CyberBiker.Combat;
using CyberBiker.Player;
using Gameplay;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Controlled desktop Editor comparison, not a prediction of mobile frame rate.
[InitializeOnLoad]
public static class RuntimePerformanceChecks {
    private const string Pending="CyberBiker.RuntimePerformanceChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] private sealed class Result {
        public string label,environment="Unity 6000.6.0f1 Windows Editor, D3D11, explicit 960x540 offscreen render, fixed camera/actor anchors, uncapped; controlled fixture";
        public int riderRayHits,riderTriangles;
        public bool registryLifecycleVerified;
        public List<Measurement> code=new List<Measurement>();
        public List<Frames> modes=new List<Frames>();
    }
    [Serializable] private sealed class Measurement {public string name;public int calls;public double msPerCall,bytesPerCall;}
    [Serializable] private sealed class Frames {public string mode;public int frames;public double fps,frameMedianMs,frameP95Ms,mainThreadMedianMs,gcBytesPerFrame;}
    private sealed class Input : IPlayerInput {private readonly TouchInputManager touch;public Input(TouchInputManager t){touch=t;}public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>touch.IsAiming;}
    private static PlayerController player;
    private static GameplayManager manager;
    private static TouchInputManager touch;
    private static Result result;
    private static int mode=-1,frame=-1;
    private static double began,sampleStarted,warmUntil;
    private static readonly List<double> frameTimes=new List<double>(),cpuTimes=new List<double>();
    private static long gcBytes;
    private static ProfilerRecorder mainThread,gc;
    private static bool finishing;
    private static RenderTexture renderTarget;
    private static readonly List<Transform> actors=new List<Transform>();
    private static readonly List<Vector3> anchors=new List<Vector3>();
    private static Vector3 playerAnchor;
    static RuntimePerformanceChecks(){if(SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");UnityEngine.Random.InitState(418206);SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static Action Method(object instance,string name)=>(Action)Delegate.CreateDelegate(typeof(Action),instance,instance.GetType().GetMethod(name,Private));
    private static Measurement Measure(string name,Action call,int count){for(int i=0;i<8;i++)call();long bytes=GC.GetAllocatedBytesForCurrentThread();var clock=Stopwatch.StartNew();for(int i=0;i<count;i++)call();clock.Stop();return new Measurement{name=name,calls=count,msPerCall=clock.Elapsed.TotalMilliseconds/count,bytesPerCall=GC.GetAllocatedBytesForCurrentThread()>bytes ? (GC.GetAllocatedBytesForCurrentThread()-bytes)/(double)count : -1};}
    private static double Percentile(List<double> values,float fraction){if(values.Count==0)return 0;var copy=values.ToArray();Array.Sort(copy);return copy[Mathf.Clamp(Mathf.CeilToInt(copy.Length*fraction)-1,0,copy.Length-1)];}
    private static void Setup(){
        result=new Result{label="baseline"};var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]=="-performanceLabel")result.label=args[i+1];
        player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();playerAnchor=player.transform.position;manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();touch=player.GetComponent<TouchInputManager>();
        foreach(var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
        foreach(var c in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
        foreach(var s in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))s.enabled=false;
        var world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();var carPrefabs=new SerializedObject(world).FindProperty("cars");
        for(int i=0;i<8;i++){
            var prefab=(GameObject)carPrefabs.GetArrayElementAtIndex(i%carPrefabs.arraySize).objectReferenceValue;
            var car=UnityEngine.Object.Instantiate(prefab,player.transform.position+new Vector3(i%2==0 ? -3.5f : 3.5f,0,30+i*22),Quaternion.identity);car.GetComponent<Car>().whichWall=1;actors.Add(car.transform);anchors.Add(car.transform.position);
        }
        var enemyPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab");
        for(int i=0;i<2;i++){
            var enemy=UnityEngine.Object.Instantiate(enemyPrefab,player.transform.position+new Vector3(i==0 ? -3.5f : 3.5f,0,45+i*20),Quaternion.identity).GetComponent<Enemy>();typeof(Enemy).GetField("shootingDamage",Private).SetValue(enemy,0f);actors.Add(enemy.transform);anchors.Add(enemy.transform.position);
        }
        manager.SwitchGameState(GameState.Playing);manager.PlayerPistolDamage=0;touch.enabled=false;touch.SetAim(false);typeof(PlayerController).GetField("input",Private).SetValue(player,new Input(touch));typeof(BikeSwordCombat).GetField("launchKey",Private).SetValue(player.GetComponent<BikeSwordCombat>(),KeyCode.None);
        foreach(var shape in player.GetComponentsInChildren<Collider>())shape.enabled=false;
        var body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
        QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
        renderTarget=new RenderTexture(960,540,24,RenderTextureFormat.DefaultHDR);Camera.main.targetTexture=renderTarget;
        mainThread=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
        NextMode();
    }
    private static void NextMode(){mode++;frameTimes.Clear();cpuTimes.Clear();gcBytes=0;warmUntil=EditorApplication.timeSinceStartup+1.5;sampleStarted=0;if(mode==1)touch.SetAim(true);if(mode==2){touch.SetAim(false);player.GetComponent<BikeSwordCombat>().Launch();}}
    private static void Benchmark(){
        typeof(BikeSwordCombat).GetMethod("ResetCombat",Private).Invoke(player.GetComponent<BikeSwordCombat>(),new object[]{true,false});
        result.code.Add(Measure("Traffic audio scan",Method(GameAudio.Active,"ScanTraffic"),200));
        result.code.Add(Measure("Pistol target acquisition",Method(player.GetComponent<BikeAutoAim>(),"AcquireTarget"),200));
        var weapon=(Weapon)typeof(PlayerController).GetField("weapon",Private).GetValue(player);typeof(Weapon).GetField("lastShotTime",Private).SetValue(weapon,Time.time);
        result.code.Add(Measure("Pistol attempt during cooldown",Method(player,"FireWeapon"),1000));
        using(var rays=new RiderMeshRaycaster(player)){
            var skin=player.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();skin.BakeMesh(mesh,true);var vertices=mesh.vertices;var triangles=mesh.GetTriangles(0);
            result.riderTriangles=mesh.triangles.Length/3;
            var target=skin.transform.TransformPoint((vertices[triangles[0]]+vertices[triangles[1]]+vertices[triangles[2]])/3);var origin=target-Vector3.forward*6;
            UnityEngine.Object.DestroyImmediate(mesh);
            Action batch=()=>{for(int i=0;i<6;i++){var end=target+Vector3.right*i*.01f;var delta=end-origin;if(rays.Raycast(new Ray(origin,delta.normalized),delta.magnitude+2))result.riderRayHits++;}};
            result.code.Add(Measure("Six exact rider visibility rays (warm mesh)",batch,100));
            if(result.riderRayHits==0)throw new Exception("Visibility benchmark must include real mesh hits");
        }
    }
    private static void CheckRegistryLifecycle(){
        // Reflection keeps the same fixture usable on the original baseline.
        var carsProperty=typeof(Car).GetProperty("Instances",BindingFlags.Static|BindingFlags.Public);
        var enemiesProperty=typeof(Enemy).GetProperty("Instances",BindingFlags.Static|BindingFlags.Public);
        if(carsProperty!=null && enemiesProperty!=null){
            var cars=(IReadOnlyList<Car>)carsProperty.GetValue(null);var enemies=(IReadOnlyList<Enemy>)enemiesProperty.GetValue(null);
            int carCount=cars.Count,enemyCount=enemies.Count;
            var carObject=new GameObject("Registry lifecycle car",typeof(BoxCollider),typeof(Car));var car=carObject.GetComponent<Car>();
            var enemyObject=new GameObject("Registry lifecycle enemy",typeof(Enemy));
            if(cars.Count!=carCount+1 || enemies.Count!=enemyCount+1)throw new Exception("Registries must include newly created actors");
            car.enabled=false;carObject.SetActive(false);carObject.SetActive(true);car.enabled=true;
            enemyObject.SetActive(false);enemyObject.SetActive(true);
            if(cars.Count!=carCount+1 || enemies.Count!=enemyCount+1)throw new Exception("Reactivation must not duplicate registry entries");
            UnityEngine.Object.DestroyImmediate(carObject);UnityEngine.Object.DestroyImmediate(enemyObject);
            if(cars.Count!=carCount || enemies.Count!=enemyCount)throw new Exception("Destroyed actors must be removed from registries");
            result.registryLifecycleVerified=true;
        }
    }
    private static void HoldScene(){
        player.transform.position=playerAnchor;player.GetComponent<Rigidbody>().position=playerAnchor;
        for(int i=0;i<actors.Count;i++)if(actors[i]!=null)actors[i].position=anchors[i];
        Camera.main.transform.SetPositionAndRotation(playerAnchor+new Vector3(0,2,-6),Quaternion.Euler(2,0,0));
        Camera.main.fieldOfView=mode==1 ? 40 : mode==2 ? 57 : 50;
    }
    private static void Tick(){if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<.7f)return;try{
        if(mode<0){Setup();return;}
        if(EditorApplication.timeSinceStartup-began>90)throw new Exception("Performance fixture timed out");
        if(Time.frameCount==frame)return;frame=Time.frameCount;
        HoldScene();
        if(EditorApplication.timeSinceStartup<warmUntil){Camera.main.Render();return;}
        if(!player.IsAlive || manager.gameState!=GameState.Playing)throw new Exception("Performance fixture must remain alive and playing");
        if(sampleStarted==0){sampleStarted=EditorApplication.timeSinceStartup;return;}
        Camera.main.Render();
        frameTimes.Add(Time.unscaledDeltaTime*1000);if(mainThread.Valid)cpuTimes.Add(mainThread.LastValue/1000000d);if(gc.Valid)gcBytes+=gc.LastValue;
        if(frameTimes.Count<90)return;
        result.modes.Add(new Frames{mode=mode==0 ? "Riding" : mode==1 ? "Aiming" : "Launch",frames=frameTimes.Count,fps=frameTimes.Count/(EditorApplication.timeSinceStartup-sampleStarted),frameMedianMs=Percentile(frameTimes,.5f),frameP95Ms=Percentile(frameTimes,.95f),mainThreadMedianMs=Percentile(cpuTimes,.5f),gcBytesPerFrame=gcBytes/(double)frameTimes.Count});
        if(mode<2)NextMode();else {Benchmark();CheckRegistryLifecycle();Finish(true,JsonUtility.ToJson(result,true));}
    }catch(Exception e){Finish(false,e.ToString());}}
    private static void Finish(bool ok,string report){if(finishing)return;finishing=true;SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;mainThread.Dispose();gc.Dispose();Directory.CreateDirectory("Logs/Performance");File.WriteAllText("Logs/Performance/"+(result!=null ? result.label : "error")+(ok ? ".json" : "-error.txt"),report);EditorApplication.Exit(ok ? 0 : 1);}
}
