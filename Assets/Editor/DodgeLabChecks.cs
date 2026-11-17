using System;
using System.IO;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using CyberBiker.Pooling;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DodgeLabChecks {
    private const string Key="CyberBiker.DodgeLabChecks.";
    private static PlayerController player;private static GameplayManager manager;private static CloseDodgeEffects effects;private static Enemy source;private static Bullet shot;
    private static int stage,lastFrame=-1,sounds;private static float clock,at,health,pausedAge;private static Vector3 position;private static double began;private static bool finishing;
    static DodgeLabChecks(){if(SessionState.GetBool(Key+"Active",false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        Directory.CreateDirectory("Logs/DodgeLab");File.WriteAllText("Logs/DodgeLab/Result.txt","RUNNING\n");
        var original=DodgeLabWindow.ReadSaved();var edited=original.Copy();edited.passRadius=2.4f;edited.cameraReturn=.8f;edited.worldSpeed=.3f;
        const string fixture="Assets/Editor/DodgeLabSaveFixture.asset";DodgeLabWindow.SaveForGame(edited,fixture);
        var saved=AssetDatabase.LoadAssetAtPath<CloseDodgeProfile>(fixture).settings;
        if(Mathf.Abs(saved.passRadius-2.4f)>.001f || Mathf.Abs(saved.cameraReturn-.8f)>.001f || Mathf.Abs(DodgeLabWindow.ReadSaved().passRadius-original.passRadius)>.001f)throw new Exception("Dodge profile save must persist edits without modifying the real profile");
        var roundtrip=JsonUtility.FromJson<CloseDodgeSettings>(JsonUtility.ToJson(edited));roundtrip.Normalize();Require(Mathf.Abs(roundtrip.worldSpeed-.3f)<.001f,"Preset roundtrip failed");
        Report("PASS shared profile save and JSON roundtrip; real saved tuning preserved.");
        SessionState.SetBool(Key+"Active",true);SessionState.SetInt(Key+"Stage",0);EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");EditorApplication.isPlaying=true;
    }
    private static void Attach(){stage=SessionState.GetInt(Key+"Stage",0);began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
    private static void Report(string text){File.AppendAllText("Logs/DodgeLab/Result.txt",text+"\n");}
    private static void Next(int next=-1){stage=next>=0 ? next : stage+1;SessionState.SetInt(Key+"Stage",stage);at=clock;File.AppendAllText("Logs/DodgeLab/Progress.txt",stage+"\n");}
    private static void Release(){if(shot!=null && shot.gameObject.activeInHierarchy)PrefabPool.Release(shot.gameObject);shot=null;}
    private static Bullet Spawn(float gap,bool friendly=false){
        Release();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bullet.prefab");var root=PrefabPool.Rent(prefab,effects.Center+Vector3.right*gap+Vector3.forward*12,Quaternion.LookRotation(-Vector3.forward));
        var bullet=root.GetComponent<Bullet>();bullet.Initialize(friendly ? player.gameObject : source.gameObject,10);bullet.bulletSpeed=40;Physics.SyncTransforms();return bullet;
    }
    private static void Tick(){if(finishing)return;try{
        Require(EditorApplication.timeSinceStartup-began<150,"Dodge check timed out at stage "+stage+" — "+DodgeLabPreview.Status);
        if(stage==100 && !EditorApplication.isPlaying){
            Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;
            EditorSceneManager.OpenScene("Assets/Level/Scenes/Menu.unity");EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Level/Scenes/Menu.unity");
            SessionState.SetString(Key+"Level",Hash("Assets/Level/Scenes/Level.unity"));AudioListener.volume=.37f;
            DodgeLabWindow.Open();DodgeLabPreview.Loop=false;DodgeLabPreview.Sound=false;DodgeLabPreview.Gap=1.3f;DodgeLabPreview.RideSpeed=65;DodgeLabPreview.Approach=.5f;
            Next(101);DodgeLabPreview.Start(DodgeLabWindow.ReadSaved());return;
        }
        if(stage==106 && !EditorApplication.isPlaying){
            Require(!DodgeLabPreview.Active && DodgeLabPreview.View==null,"Lab must release its view and active flag on Stop");
            Require(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)=="Assets/Level/Scenes/Menu.unity" && Mathf.Abs(AudioListener.volume-.37f)<.001f,"Lab must restore prior start scene and listener volume");
            Require(Hash("Assets/Level/Scenes/Level.unity")==SessionState.GetString(Key+"Level",""),"Preview must not modify the saved gameplay scene");
            Report("PASS Dodge Lab: real moving-world close pass, replay both sides, live edits, slow-motion toggle, effect scrubbing, save/presets, and scene/audio/texture restoration.");Finish(true,"PASS all checks");return;
        }
        if(!EditorApplication.isPlaying)return;
        if(stage>=101){LabTick();return;}
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;clock+=FinisherClock.UnscaledDeltaTime;
        if(stage==0){
            player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();if(player==null || player.Health==null || Time.timeSinceLevelLoad<.5f)return;
            manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();manager.SwitchGameState(GameState.Playing);manager.SetCloseDodgePreview(new CloseDodgeSettings{cooldown=.3f,slowHold=.3f,slowRecovery=.3f,cameraReturn=.4f});
            foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>())director.enabled=false;
            foreach(var spawner in UnityEngine.Object.FindObjectsByType<TrafficSpawner>())spawner.enabled=false;
            foreach(var actor in UnityEngine.Object.FindObjectsByType<Enemy>())actor.gameObject.SetActive(false);
            foreach(var car in UnityEngine.Object.FindObjectsByType<Car>())car.gameObject.SetActive(false);
            player.GetComponent<TouchInputManager>().enabled=false;player.GetComponent<PlayerProjectileEvade>().enabled=false;player.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeAll;player.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;
            effects=player.GetComponent<CloseDodgeEffects>();Require(effects!=null && manager.DodgeEffects==effects,"Player must auto-install and bind the cinematic");
            position=player.transform.position;health=player.Health.Current;Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;
            source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),position+Vector3.forward*80,Quaternion.identity).GetComponent<Enemy>();Next();
        }else if(stage==1 && source.HasInitialized){source.enabled=false;source.StopAllCoroutines();shot=Spawn(1.3f);Next();
        }else if(stage==2 && clock-at>.12f){Require(effects.TriggerCount==0,"Cinematic must wait for the bullet to pass, not trigger on approach");Next();
        }else if(stage==3 && effects.TriggerCount==1){Require(Mathf.Abs(effects.LastPassDistance-1.3f)<.08f,"Closest pass radius must be measured at the torso");Next();
        }else if(stage==4 && clock-at>.18f){
            Require(Time.timeScale<.5f && effects.LastCameraZoom>2 && effects.LastCameraSide>0,"Close pass must slow the real world and apply zoom/side camera movement");
            Require(manager.CombatSlowMotionRemaining==0 && Vector3.Distance(player.transform.position,position)<.001f,"Cinematic must not occupy the finisher channel or move the bike");Capture("CloseDodgeRight");pausedAge=effects.Age;manager.SwitchGameState(GameState.Pause);Next();
        }else if(stage==5 && clock-at>.15f){Require(Time.timeScale==0 && Mathf.Abs(effects.Age-pausedAge)<.001f,"Pause must freeze cinematic timing");manager.SwitchGameState(GameState.Playing);Next();
        }else if(stage==6 && clock-at>1){
            Require(!effects.IsActive && Mathf.Abs(Time.timeScale-1)<.001f && effects.LastCameraZoom==0,"World and camera must recover fully");Release();shot=Spawn(2.3f);Next();
        }else if(stage==7 && clock-at>.5f){
            Require(effects.TriggerCount==1,"Outside-radius bullet must not trigger the cinematic");sounds=ShootingFeedback.Active.NearMissCount;manager.CloseDodge.passRadius=3.8f;shot=Spawn(-3.5f);Next();
        }else if(stage==8 && effects.TriggerCount==2){
            Require(ShootingFeedback.Active.NearMissCount==sounds,"Cinematic radius must work independently beyond the sound radius");Report("PASS real pass-only trigger, radius filtering, independent sound radius, actual zoom/side camera, frozen bike, pause and smooth recovery.");Next();
        }else if(stage==9 && clock-at>1){
            Require(effects.Side<0,"Left pass must choose the opposite camera side");effects.ResetForPreview();manager.CloseDodge.passRadius=1.8f;shot=Spawn(0);Next();
        }else if(stage==10 && player.Health.Current<health){
            Require(effects.TriggerCount==0,"An actual player hit must not award a close dodge");health=player.Health.Current;shot=Spawn(1.3f,true);Next();
        }else if(stage==11 && clock-at>.5f){
            Require(effects.TriggerCount==0,"Friendly bullets must not trigger close dodges");shot=Spawn(0);shot.Evade(player);Next();
        }else if(stage==12 && effects.TriggerCount==1){
            Require(player.Health.Current==health && effects.LastPassDistance<.1f,"An explicitly evaded bullet must trigger only when it really passes, without damage");Next();
        }else if(stage==13 && clock-at>1){
            manager.BeginCombatSlowMotion(.4f,1,.1f);Require(!effects.TryClosePass(1,effects.Center+Vector3.right),"Existing finisher slow motion must keep priority");effects.Cancel();Require(manager.CombatSlowMotionRemaining>0,"Cancelling dodge must never cancel finisher slow motion");manager.CancelCombatSlowMotion();
            effects.ResetForPreview();manager.CloseDodge.cooldown=3;Require(effects.TryClosePass(1,effects.Center+Vector3.right),"Fresh close pass should trigger");Next();
        }else if(stage==14 && clock-at>1){
            Require(!effects.IsActive && !effects.TryClosePass(1,effects.Center+Vector3.right),"Cooldown must reject a second effect after recovery");
            effects.enabled=false;Require(Mathf.Abs(Time.timeScale-1)<.001f,"Disabling the effect must restore its clock channel");effects.enabled=true;Release();
            Report("PASS left/right direction, player-hit rejection, friendly rejection, pooled projectile reuse, explicit-evade pass, finisher priority, cooldown and disable cleanup.");
            Next(100);Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;EditorApplication.isPlaying=false;
        }
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void LabTick(){
        if(!DodgeLabPreview.Ready)return;
        Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;
        var fx=DodgeLabPreview.Effects;
        if(stage==101 && fx.TriggerCount>0 && fx.Age>.2f){
            Require(DodgeLabPreview.View!=null && fx.LastPassDistance<DodgeLabPreview.Gameplay.CloseDodge.passRadius,"Lab must preview a real moving-world pass");
            Capture("LabRight");DodgeLabPreview.Seek(.2f);Require(EditorApplication.isPaused && fx.LastCameraZoom>0,"Timeline seek must pause and apply the real camera");
            var draft=DodgeLabWindow.ReadSaved();draft.zoomInFov=12;DodgeLabPreview.Apply(draft);DodgeLabPreview.Seek(draft.delay+draft.cameraBlendIn+draft.cameraHold*.5f);Require(Mathf.Abs(fx.LastCameraZoom-12)<.01f,"Live camera edits must apply while scrubbing");
            draft.slowMotion=false;DodgeLabPreview.Apply(draft);Require(Mathf.Abs(Time.timeScale-1)<.001f,"Slow-motion toggle must actually turn off the world slowdown");
            draft.slowMotion=true;DodgeLabPreview.Apply(draft);DodgeLabPreview.Seek(draft.delay+draft.slowBlendIn+.05f);Require(Time.timeScale<.5f,"Scrub must preview slow-motion timing");
            DodgeLabPreview.Seek(draft.Duration);Require(Mathf.Abs(Time.timeScale-1)<.001f && fx.LastCameraZoom==0,"Scrub end must restore the camera and world speed");
            DodgeLabPreview.Gap=-1.3f;DodgeLabPreview.Replay();Next(102);
        }else if(stage==102 && fx.TriggerCount>0 && fx.Age>.2f){
            Require(fx.Side<0 && fx.LastCameraSide<0,"Lab Replay must support the other passing side");Capture("LabLeft");DodgeLabPreview.Gap=4;DodgeLabPreview.Replay();Next(103);
        }else if(stage==103 && Time.timeSinceLevelLoad>0 && !fx.IsActive && fx.TriggerCount==0 && DodgeLabPreview.Status.StartsWith("No close dodge")){
            Require(Mathf.Abs(Time.timeScale-1)<.001f,"Outside-radius lab scenario must remain at normal speed");Next(106);Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;DodgeLabPreview.Stop();
        }
    }
    private static string Hash(string path){using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)));}
    private static void Capture(string name){var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/DodgeLab/"+name+".png",image.EncodeToPNG());}finally{camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);}}
    private static void Finish(bool success,string text){if(finishing)return;finishing=true;SessionState.SetBool(Key+"Active",false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;Report((success ? "" : "FAIL ")+text);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
