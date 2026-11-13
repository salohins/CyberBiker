using System;
using System.IO;
using System.Reflection;
using AI;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyRoadChecks {
    private const string Pending="CyberBiker.EnemyRoadChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static PlayerController player;
    private static Enemy enemy;
    private static AIController ai;
    private static Rigidbody body;
    private static int stage;
    private static float stageTime,maxX;
    private static double began;
    private static bool baseline,finishing;
    private static string report="";
    static EnemyRoadChecks(){if(SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string s,string stack,LogType t){if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)Finish(false,s+"\n"+stack);}
    private static void Require(bool ok,string s){if(!ok)throw new Exception(s);}
    private static void Next(){stage++;stageTime=Time.time;}
    private static void PlacePlayer(Vector3 p){player.transform.position=p;body.position=p;body.linearVelocity=Vector3.forward*75;}
    private static void Tick(){if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<.7f)return;try{
        Require(EditorApplication.timeSinceStartup-began<35,"Road navigation check timed out at stage "+stage);
        if(stage==0){
            baseline=Array.IndexOf(Environment.GetCommandLineArgs(),"-roadBaseline")>=0;
            player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();var manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();ai=UnityEngine.Object.FindAnyObjectByType<AIController>();
            foreach(var c in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
            foreach(var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
            foreach(var s in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))s.enabled=false;
            ai.dodgePoints.Clear();manager.SwitchGameState(GameState.Playing);player.enabled=false;player.GetComponent<TouchInputManager>().enabled=false;
            typeof(CyberBiker.Player.BikeSwordCombat).GetField("launchKey",Private).SetValue(player.GetComponent<CyberBiker.Player.BikeSwordCombat>(),KeyCode.None);
            body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;foreach(var c in player.GetComponentsInChildren<Collider>())c.enabled=false;
            PlacePlayer(new Vector3(0,1000,-20));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab");enemy=UnityEngine.Object.Instantiate(prefab,new Vector3(0,1000,0),Quaternion.Euler(0,15,0)).GetComponent<Enemy>();typeof(Enemy).GetField("shootingDamage",Private).SetValue(enemy,0f);Next();
        }else if(stage==1 && enemy.IsAlive){typeof(Enemy).GetField("state",Private).SetValue(enemy,EnemyState.Attack);typeof(Enemy).GetField("speed",Private).SetValue(enemy,75f);Next();
        }else if(stage==2){
            maxX=Mathf.Max(maxX,Mathf.Abs(enemy.transform.position.x));
            if(Time.time-stageTime<2)return;
            report+="No-path fallback: max lateral displacement="+maxX.ToString("F3")+"m; heading="+enemy.transform.eulerAngles.y.ToString("F3")+" degrees.\n";
            if(baseline){Require(maxX>15,"Baseline must reproduce no-path lateral escape");Finish(true,report+"REPRODUCED: enemy leaves road while following its own forward probe without a route.\n");return;}
            Require(maxX<3,"No-path fallback must straighten without leaving its lane");
            enemy.transform.SetPositionAndRotation(new Vector3(-62.553f,1000,6998.45f),Quaternion.Euler(0,-1.089f,0));PlacePlayer(new Vector3(0,1000,7198.45f));typeof(Enemy).GetField("state",Private).SetValue(enemy,EnemyState.Spawn);typeof(Enemy).GetField("path",Private).SetValue(enemy,null);typeof(Enemy).GetField("followTarget",Private).SetValue(enemy,Vector3.zero);Next();
        }else if(stage==3 && Time.time-stageTime>3){
            Require(Mathf.Abs(enemy.transform.position.x)<15,"An already escaped enemy must recover smoothly toward the road; x="+enemy.transform.position.x);
            Require(enemy.ForwardSpeed>body.linearVelocity.z,"A passed spawn-state enemy must catch up instead of dropping farther behind");
            Require((EnemyState)typeof(Enemy).GetField("state",Private).GetValue(enemy)==EnemyState.Spawn,"Recovery must preserve the existing70m attack activation range");
            report+="PASS captured off-track case: recovered from X=-62.553m toward the road and resumed distance control; final X="+enemy.transform.position.x.ToString("F3")+"m.\n";Next();
        }else if(stage==4 && Time.time-stageTime>4){
            Require((EnemyState)typeof(Enemy).GetField("state",Private).GetValue(enemy)==EnemyState.Attack,"Recovered enemy must enter Attack after reaching the original activation range");
            report+="PASS recovery catch-up preserves70m attack activation; enemy resumes fighting after closing the gap.\n";Finish(true,report);
        }
    }catch(Exception e){Finish(false,e+"\n"+report);}}
    private static void Finish(bool ok,string content){if(finishing)return;finishing=true;SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Directory.CreateDirectory("Logs/EnemyRoad");File.WriteAllText("Logs/EnemyRoad/"+(baseline ? "baseline" : "fixed")+".txt",content);EditorApplication.Exit(ok ? 0 : 1);}
}
