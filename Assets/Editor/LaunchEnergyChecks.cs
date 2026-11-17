using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CyberBiker.Player;
using CyberBiker.Scoring;
using CyberBiker.UI;
using Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LaunchEnergyChecks {
    private const string Key="CyberBiker.LaunchEnergyChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static PlayerController player,oldPlayer;
    private static GameplayManager game;
    private static BikeSwordCombat sword;
    private static Rigidbody body;
    private static int stage;
    private static bool done;
    private static float charge;
    private static double began,at;
    static LaunchEnergyChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        Directory.CreateDirectory("Logs/LaunchEnergy");File.WriteAllText("Logs/LaunchEnergy/Result.txt","");
        SessionState.SetBool(Key+"HadPrefs",PlayerPrefs.HasKey(TestingSettings.PreferenceKey));SessionState.SetString(Key+"Prefs",PlayerPrefs.GetString(TestingSettings.PreferenceKey,""));
        PlayerPrefs.DeleteKey(TestingSettings.PreferenceKey);PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Near(float actual,float expected,string message){Require(Mathf.Abs(actual-expected)<.001f,message+": "+actual+" expected "+expected);}
    private static void Report(string text){File.AppendAllText("Logs/LaunchEnergy/Result.txt","PASS "+text+"\n");}
    private static void Next(int value){stage=value;at=EditorApplication.timeSinceStartup;}
    private static void Set(object item,string field,object value)=>item.GetType().GetField(field,Private).SetValue(item,value);
    private static bool Bind(){
        player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();game=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();
        if(player==null || game==null || player.Health==null || Time.timeSinceLevelLoad<.6f)return false;
        sword=player.GetComponent<BikeSwordCombat>();body=player.GetComponent<Rigidbody>();
        return sword.EnergyHud!=null;
    }
    private static void Isolate(){
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
        foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))car.gameObject.SetActive(false);
        foreach(var traffic in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None)){traffic.StopAllCoroutines();traffic.enabled=false;}
        foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>(FindObjectsSortMode.None)){director.StopAllCoroutines();director.enabled=false;}
        player.GetComponent<TouchInputManager>().enabled=false;Set(sword,"launchKey",KeyCode.None);
        body.linearVelocity=Vector3.zero;body.constraints=RigidbodyConstraints.FreezeAll;body.position=new Vector3(0,100,0);player.transform.position=body.position;
        game.SwitchGameState(GameState.Playing);
    }
    private static void Tick(){
        if(done || !EditorApplication.isPlaying)return;EditorApplication.QueuePlayerLoopUpdate();
        try{
            Require(EditorApplication.timeSinceStartup-began<100,"Launch energy check timed out at "+stage);
            if(stage==0){
                if(!Bind())return;Isolate();Near(sword.LaunchCharge,1,"Initial bar");Require(sword.CanStartLaunch,"Full bar must allow Launch");
                Capture("FullCharge");sword.TestingLaunchDuration=1;sword.TestingLaunchRefillScore=1000;
                sword.Launch();Require(sword.IsPursuing,"Launch must activate with charge");Next(1);
            }else if(stage==1 && EditorApplication.timeSinceStartup-at>.25){
                Require(sword.LaunchCharge<.9f && sword.LaunchCharge>.1f,"Launch must consume energy in real time");charge=sword.LaunchCharge;
                sword.Launch();Require(!sword.IsPursuing,"Cancel must stop Launch");Near(sword.LaunchCharge,charge,"Cancel must preserve remaining charge");
                Set(sword,"nextLaunch",0f);sword.Launch();Near(sword.LaunchCharge,charge,"Reactivation must not refill the bar");
                game.SwitchGameState(GameState.Pause);charge=sword.LaunchCharge;Next(2);
            }else if(stage==2 && EditorApplication.timeSinceStartup-at>.2){
                Near(sword.LaunchCharge,charge,"Pause must freeze energy");game.SwitchGameState(GameState.Playing);Next(3);
            }else if(stage==3 && !sword.IsPursuing){
                Near(sword.LaunchCharge,0,"Empty bar must stop Launch");int launches=sword.LaunchCount;Set(sword,"nextLaunch",0f);sword.Launch();Require(!sword.IsActive && sword.LaunchCount==launches,"Empty bar must reject repeat Launch");
                sword.GainLaunchEnergy(50);Near(sword.LaunchCharge,.05f,"Partial score refill");Require(!sword.CanStartLaunch,"Below-threshold charge must block Launch");sword.Launch();Require(!sword.IsActive,"Low charge must reject activation");
                sword.GainLaunchEnergy(50);Near(sword.LaunchCharge,.1f,"Minimum charge refill");Require(sword.CanStartLaunch,"Threshold must re-enable Launch");
                Report("full initial bar; real-time drain; cancellation/reactivation preserve charge; pause freezes drain; exhaustion stops Launch; empty/low charge cannot activate; threshold re-enables it.");
                var score=game.StyleScore;
                foreach(var action in new[]{StyleAction.PistolHit,StyleAction.PistolKill,StyleAction.GapPass}){int awards=score.Combo.AwardCount;score.Record(action,player.transform.position,100);Require(score.Combo.AwardCount==awards+1,"Excluded energy action must still score");Near(sword.LaunchCharge,.1f,"Pistol/gap actions must not refill Launch");}
                var gap=player.GetComponent<GapDashAssist>();Set(gap,"active",true);score.Record(StyleAction.NearMiss,player.transform.position,100);Set(gap,"active",false);Near(sword.LaunchCharge,.1f,"Close shaves inside an assisted gap must not refill Launch");
                score.Combo.Bank();Near(sword.LaunchCharge,.1f,"Banking excluded rewards must not refill Launch");
                score.Record(StyleAction.BulletDodge,player.transform.position,100);Near(sword.LaunchCharge,.2f,"Bullet dodge action refill");
                score.Record(StyleAction.NearMiss,player.transform.position,100);Near(sword.LaunchCharge,.3f,"Driving action refill");
                score.Record(StyleAction.PerfectDodge,player.transform.position,100);Require(score.Combo.Multiplier==2,"Multiplier fixture");Near(sword.LaunchCharge,.5f,"Multiplier must increase refill");
                charge=sword.LaunchCharge;score.Combo.Bank();Near(sword.LaunchCharge,charge,"Bank must not double-credit combo energy");
                game.Session.AddScore(100);Near(sword.LaunchCharge,.6f,"Direct score refill");game.Session.Tick(.5f,20);Near(sword.LaunchCharge,.6001f,"Distance score refill");
                charge=sword.LaunchCharge;sword.GainLaunchEnergy(-100);sword.GainLaunchEnergy(float.NaN);sword.GainLaunchEnergy(float.PositiveInfinity);Near(sword.LaunchCharge,charge,"Invalid credit");
                // Render after the HUD has observed this frame's score awards.
                Report("pistol hits/kills, gap passes and their incidental close shaves still score but never refill, including at bank time; independent driving/dodge rewards refill with multipliers; direct/distance score and clamping remain intact.");
                Next(4);
            }else if(stage==4){
                if(EditorApplication.timeSinceStartup-at<.15)return;
                Capture("ScoreRefill");sword.GainLaunchEnergy(100000);Near(sword.LaunchCharge,1,"Refill cap");
                var menu=game.GetComponentInChildren<TestingSettingsMenu>();if(menu==null || menu.GetComponentsInChildren<TMP_InputField>(true).Length==0)return;
                menu.Open();Require(menu.IsOpen,"TUNE must open");
                foreach(var pair in new[]{("Launch duration","3"),("Launch refill score","2000"),("Launch minimum charge","20"),("Launch starting charge","0")}){
                    var field=menu.GetComponentsInChildren<TMP_InputField>(true).Single(f=>f.transform.parent.name==pair.Item1);field.SetTextWithoutNotify(pair.Item2);field.onEndEdit.Invoke(pair.Item2);
                }
                Near(sword.TestingLaunchRefillScore,1000,"TUNE changes stay draft");
                string json=menu.ExportPresetJson();Require(BalancePresetIO.TryImport(json,menu.Draft,out var preset,out _) && preset.launchRefillScore==2000 && preset.launchMinimumCharge==20 && preset.launchStartingCharge==0 && preset.launchDuration==3,"Energy preset round trip failed");
                var legacy=TestingSettings.Defaults.Copy();JsonUtility.FromJsonOverwrite("{\"maxSpeed\":80}",legacy);Require(legacy.launchRefillScore==1500 && legacy.launchMinimumCharge==10 && legacy.launchStartingCharge==100,"Old presets must keep energy defaults");
                File.WriteAllText("Logs/LaunchEnergy/ExamplePreset.json",json);menu.Apply(false);Near(sword.TestingLaunchRefillScore,2000,"Apply refill setting");Near(sword.TestingLaunchMinimumCharge,20,"Apply threshold");Near(sword.LaunchCharge,1,"Applying settings must not change current charge");
                oldPlayer=player;game.RestartLevel();Next(5);
            }else if(stage==5){
                if(!Bind() || player==oldPlayer)return;Isolate();Near(sword.LaunchCharge,0,"Saved starting charge must apply on the next run");Near(sword.TestingLaunchRefillScore,2000,"Saved refill");Near(sword.TestingLaunchMinimumCharge,20,"Saved threshold");Near(sword.TestingLaunchDuration,3,"Saved capacity duration");
                sword.GainLaunchEnergy(400);Near(sword.LaunchCharge,.2f,"Saved refill scale");Require(sword.CanStartLaunch,"Saved activation threshold");
                sword.Launch();Require(sword.IsPursuing,"Crash fixture must have active Launch");game.SwitchGameState(GameState.Hit);Near(sword.LaunchCharge,0,"Crash must burn the entire remaining bar");Require(!sword.IsPursuing && sword.BoostBlend==0,"Crash must stop Launch and its boost immediately");
                Report("crash state empties the entire bar and immediately stops active Launch and boost.");
                Report("TUNE fields remain draft until Apply; export/import retains energy controls; older JSON retains defaults; live Apply does not refill; configuration persists and starting charge applies on a fresh run.");
                Finish(true,"PASS Launch energy integration checks.");
            }
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void Capture(string name){
        var canvas=sword.EnergyHud.GetComponentInParent<Canvas>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;var active=RenderTexture.active;
        var nodes=canvas.GetComponentsInChildren<Transform>(true);var layers=new int[nodes.Length];var camera=new GameObject("Energy capture camera").AddComponent<Camera>();var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{for(int i=0;i<nodes.Length;i++){layers[i]=nodes[i].gameObject.layer;nodes[i].gameObject.layer=31;}camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.025f,.04f);camera.cullingMask=1<<31;camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes("Logs/LaunchEnergy/"+name+".png",pixels.EncodeToPNG());}
        finally{canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;for(int i=0;i<nodes.Length;i++)nodes[i].gameObject.layer=layers[i];RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    private static void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Finish(bool success,string message){
        if(done)return;done=true;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
        if(SessionState.GetBool(Key+"HadPrefs",false))PlayerPrefs.SetString(TestingSettings.PreferenceKey,SessionState.GetString(Key+"Prefs",""));else PlayerPrefs.DeleteKey(TestingSettings.PreferenceKey);PlayerPrefs.Save();
        File.AppendAllText("Logs/LaunchEnergy/Result.txt",message+"\n");Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);
    }
}
