using System;
using System.IO;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using P=CyberBiker.EditorTools.RiderReactionLabPreview;

[InitializeOnLoad]
public static class RiderReactionLabChecks {
    private const string Key="CyberBiker.RiderReactionLabChecks";
    private static int stage;private static double began,at;private static bool finished;
    private static RiderReactionLabWindow window;
    static RiderReactionLabChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        Directory.CreateDirectory("Logs/RiderReactionsLab");File.WriteAllText("Logs/RiderReactionsLab/Result.txt","RUNNING\n");
        try{
            var source=File.ReadAllBytes(RiderReactionLabTuning.PlayerPath);SessionState.SetString(Key+"Original",Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(source)));
            var tuning=RiderReactionLabTuning.ReadSaved();var json=JsonUtility.ToJson(tuning);var roundtrip=JsonUtility.FromJson<RiderReactionLabTuning>(json);roundtrip.Validate();Require(JsonUtility.ToJson(roundtrip)==json,"JSON preset roundtrip changed tuning");
            string folder="Assets/__RiderReactionLabChecks",path=folder+"/Player.prefab";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","__RiderReactionLabChecks");Require(AssetDatabase.CopyAsset(RiderReactionLabTuning.PlayerPath,path),"Could not create isolated save fixture");
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var originalGun=EditorJsonUtility.ToJson(root.GetComponent<PlayerController>());int components=root.GetComponentsInChildren<Component>(true).Length;
            roundtrip.Get("carNeckShare").number=.23f;roundtrip.SaveForGame(path);root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(Mathf.Abs(RiderReactionLabTuning.Capture(root.GetComponent<RiderLife>()).Get("carNeckShare").number-.23f)<.001f,"Save did not persist neck share");
            Require(root.GetComponentsInChildren<Component>(true).Length==components && EditorJsonUtility.ToJson(root.GetComponent<PlayerController>())==originalGun,"Reaction save changed unrelated player data");
            var invalid=tuning.Copy();invalid.Get("headYawLimit").number=float.NaN;bool rejected=false;try{invalid.SaveForGame(path);}catch(ArgumentException){rejected=true;}Require(rejected,"Nonfinite preset was accepted");AssetDatabase.DeleteAsset(folder);
            Report("PASS preset roundtrip, strict reaction-only save, unrelated player data preserved and invalid values rejected.");
            EditorSceneManager.OpenScene("Assets/Level/Scenes/Menu.unity");EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Level/Scenes/Menu.unity");AudioListener.volume=.37f;
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Stage",0);RiderReactionLabWindow.Open();P.Aiming=false;P.Current=P.Scenario.CarApproach;P.Speed=65;P.CarStart=70;P.Side=4;P.Loop=false;P.Sound=false;P.Start(tuning);
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void Attach(){began=at=EditorApplication.timeSinceStartup;stage=SessionState.GetInt(Key+"Stage",0);EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Require(bool valid,string text){if(!valid)throw new Exception(text);}
    private static void Report(string text)=>File.AppendAllText("Logs/RiderReactionsLab/Result.txt",text+"\n");
    private static void Next(int value){stage=value;SessionState.SetInt(Key+"Stage",value);at=EditorApplication.timeSinceStartup;}
    private static void Tick(){if(finished)return;try{
        Require(EditorApplication.timeSinceStartup-began<160,"Rider Reactions Lab timed out at "+stage+": "+P.Status);
        if(stage==40 && !EditorApplication.isPlaying && !P.Active){
            Require(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)=="Assets/Level/Scenes/Menu.unity" && Mathf.Abs(AudioListener.volume-.37f)<.001f,"Preview did not restore scene/audio");Require(P.View==null,"Inspection texture was not released");
            var hash=Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(RiderReactionLabTuning.PlayerPath)));Require(hash==SessionState.GetString(Key+"Original",""),"Preview or fixture save modified the real player prefab");Finish(true,"PASS all Rider Reactions Lab checks.");return;
        }
        if(!P.Ready)return;
        if(stage==0){
            window=EditorWindow.GetWindow<RiderReactionLabWindow>();window.position=new Rect(30,30,1250,820);
            // Use a known draft for native checks; never save it to the real prefab.
            foreach(var c in RiderReactionLabTuning.Controls)window.Draft.Get(c.field).number=RiderReactionLabTuning.ReadSaved().Get(c.field).number;
            window.Draft.Get("idleSway").number=0;window.Draft.Get("breathingAmount").number=0;window.Changed();
            P.Seek(1.2f);Require(P.Life.HeadYaw>10 && P.Life.NeckTurn>2,"Scrub did not reproduce car head and neck glance: "+P.Life.HeadYaw+" / "+P.Life.NeckTurn);
            float heading=P.Life.HeadYaw,neck=P.Life.NeckTurn;P.Seek(.4f);P.Seek(1.2f);Require(Mathf.Abs(P.Life.HeadYaw-heading)<.1f && Mathf.Abs(P.Life.NeckTurn-neck)<.1f,"Reaction scrubbing is not repeatable");
            window.Draft.Get("carNeckShare").number=0;window.Changed();Require(P.Life.NeckTurn<.01f,"Paused neck edit did not update the pose");window.Draft.Get("carNeckShare").number=.4f;window.Changed();Require(P.Life.NeckTurn>2,"Paused neck restore did not update pose");Capture("CarGlance");
            float yaw=P.OrbitYaw,distance=P.Distance;var mouse=window.PreviewRect.center;Require(window.PreviewRect.width>100,"Preview has no interactive image");
            window.SendEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=mouse});window.SendEvent(new Event{type=EventType.MouseDrag,button=0,mousePosition=mouse+new Vector2(30,15),delta=new Vector2(30,15)});window.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=mouse+new Vector2(30,15)});window.SendEvent(new Event{type=EventType.ScrollWheel,mousePosition=mouse,delta=new Vector2(0,2)});Require(Mathf.Abs(P.OrbitYaw-yaw)>5 && P.Distance>distance,"Preview orbit/zoom mouse events failed");
            P.HeadCloseup=true;P.Distance=3;P.Render();Capture("HeadNeckCloseup");P.HeadCloseup=false;P.Distance=10;P.GameView=true;Require(P.View==CameraLabPreview.View,"Game view did not use gameplay camera");P.Render();P.GameView=false;
            Report("PASS paused live head/neck edits, deterministic timeline scrubbing, real preview orbit/zoom events, closeup and Game view.");
            P.Current=P.Scenario.ClosePass;P.Seek(1.65f);Require(P.Life.BodyRoll>.3f,"Pass body lean is missing");window.Draft.Get("passBodyLean").number=0;window.Changed();Require(Mathf.Abs(P.Life.BodyRoll)<.01f,"Paused body edit did not update the pose");window.Draft.Get("passBodyLean").number=2.4f;window.Changed();Capture("ClosePass");
            float old=P.Age;P.Step();Require(P.Age>old && EditorApplication.isPaused,"Step failed");P.Replay();Require(P.Age==0,"Replay did not rewind");
            Report("PASS close-pass body response, paused lean edit, single-frame step and replay.");
            P.Current=P.Scenario.VisibleShooter;P.Seek(.7f);Require(P.Life.PriorityEnemy!=null && !P.Life.NeckPoseApplied,"Visible shooter behavior changed");Capture("VisibleShooter");P.Current=P.Scenario.ShooterLeavesView;P.Seek(3.5f);Require(P.Life.PriorityEnemy==null,"Offscreen shooter kept head priority");
            Report("PASS visible shooter retains priority and returns after leaving the view; enemy attention has no car neck layer.");
            P.Current=P.Scenario.VisibleShooter;P.Aiming=true;EditorApplication.isPaused=false;Next(1);
        }else if(stage==1 && EditorApplication.timeSinceStartup-at>2 && P.Player.IsPistolVisuallyReady){
            P.Seek(.7f);Require(!P.Life.BodyPoseApplied && P.Life.PriorityEnemy!=null,"Aiming fixture lost shooter priority or overrode torso");Capture("AimingPriority");Report("PASS aiming preview preserves weapon/torso ownership and visible-shooter head attention.");
            P.Aiming=false;EditorApplication.isPaused=false;Next(2);
        }else if(stage==2 && EditorApplication.timeSinceStartup-at>2){
            P.Current=P.Scenario.CarApproach;P.Replay();EditorApplication.isPaused=false;Next(3);
        }else if(stage==3 && P.Age>1.1f){
            Require(P.Life.NeckTurn>1,"Live playback did not animate neck");window.Draft.Get("carNeckShare").number=0;window.Changed();Next(4);
        }else if(stage==4 && P.Age>2){
            Require(P.Life.NeckTurn<1,"Live control change did not affect neck");Report("PASS live scenario playback and edits outside paused mode.");Next(40);P.Stop();
        }
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void Capture(string name){var texture=P.View;var active=RenderTexture.active;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);try{P.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes("Logs/RiderReactionsLab/"+name+".png",image.EncodeToPNG());}finally{RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);}}
    private static void Finish(bool success,string message){if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Report(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
