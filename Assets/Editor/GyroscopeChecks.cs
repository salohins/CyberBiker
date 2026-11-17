using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CyberBiker.Player;
using CyberBiker.UI;
using Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GyroscopeChecks {
    private const string Key="CyberBiker.GyroscopeChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int stage,lastFrame=-1;private static float clock,at;private static double began;private static bool finishing;
    private static PlayerController player,oldPlayer;private static GameplayManager manager;private static TouchInputManager input;private static TestingSettingsMenu menu;
    static GyroscopeChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        Directory.CreateDirectory("Logs/Gyroscope");File.WriteAllText("Logs/Gyroscope/Result.txt","RUNNING\n");PureChecks();
        SessionState.SetBool(Key+"HadPrefs",PlayerPrefs.HasKey(TestingSettings.PreferenceKey));SessionState.SetString(Key+"Prefs",PlayerPrefs.GetString(TestingSettings.PreferenceKey,""));PlayerPrefs.DeleteKey(TestingSettings.PreferenceKey);
        SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");EditorApplication.isPlaying=true;
    }
    private static void PureChecks(){
        foreach(var angle in new[]{-25f,0,25f}){
            Require(TiltSteering.TryReadRoll(Gravity(angle),ScreenOrientation.LandscapeLeft,true,out float direct) && Mathf.Abs(direct-angle)<.01f,"Compensated sensor roll");
            var g=Gravity(angle);
            Require(TiltSteering.TryReadRoll(new Vector3(g.y,-g.x,0),ScreenOrientation.LandscapeLeft,false,out float left) && Mathf.Abs(left-angle)<.01f,"Landscape-left screen mapping");
            Require(TiltSteering.TryReadRoll(new Vector3(-g.y,g.x,0),ScreenOrientation.LandscapeRight,false,out float right) && Mathf.Abs(right-angle)<.01f,"Landscape-right screen mapping");
        }
        Require(!TiltSteering.TryReadRoll(Vector3.back,ScreenOrientation.LandscapeLeft,true,out _),"Flat/invalid projection must not steer");
        var filter=new TiltSteering();Require(filter.Sample(5,1,2,0,false,.02f)==0,"First valid sample must calibrate neutral");
        Require(filter.Sample(6,1,2,0,false,.02f)==0,"Dead zone must reject small movement");
        Require(filter.Sample(25,1,2,0,false,.02f)>.99f && filter.Sample(25,1,2,0,false,.02f)>.99f,"Held tilt must keep steering without drift toward zero");
        Require(filter.Sample(-15,1,2,0,false,.02f)<-.99f && filter.Sample(25,1,2,0,true,.02f)<-.99f,"Left tilt and inversion");
        var a=new TiltSteering();var b=new TiltSteering();a.Calibrate(0);b.Calibrate(0);
        for(int i=0;i<60;i++)a.Sample(20,1,2,.12f,false,1f/60);
        for(int i=0;i<30;i++)b.Sample(20,1,2,.12f,false,1f/30);
        Require(Mathf.Abs(a.Output-b.Output)<.001f,"Smoothing must be frame-rate independent");
        filter.Calibrate(170);Require(filter.Sample(-170,1,2,0,false,.02f)>.99f,"Neutral must handle roll wrap-around");
        Report("PASS calibrated held tilt, dead zone, direction/invert, landscape mapping without double compensation, flat-phone rejection and frame-rate-independent smoothing.");
    }
    private static Vector3 Gravity(float angle)=>new Vector3(Mathf.Sin(angle*Mathf.Deg2Rad),-Mathf.Cos(angle*Mathf.Deg2Rad),0);
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Require(bool value,string text){if(!value)throw new Exception(text);}
    private static void Report(string text){File.AppendAllText("Logs/Gyroscope/Result.txt",text+"\n");}
    private static void Next(){stage++;at=clock;}
    private static void Button(string caption){menu.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<TMP_Text>().text==caption).onClick.Invoke();}
    private static void Enter(string row,string value){var field=menu.GetComponentsInChildren<TMP_InputField>(true).Single(f=>f.transform.parent.name==row);field.text=value;field.onEndEdit.Invoke(value);}
    private static void Touch(TouchPhase phase,Vector2 point,Vector2 movement,float time){typeof(TouchInputManager).GetMethod("ProcessPistolTouch",Private).Invoke(input,new object[]{55,point,movement,phase,time});}
    private static void Tick(){if(finishing || !EditorApplication.isPlaying)return;try{
        Require(EditorApplication.timeSinceStartup-began<110,"Gyroscope check timed out at stage "+stage);
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;clock+=FinisherClock.UnscaledDeltaTime;
        if(stage==0){
            player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();menu=UnityEngine.Object.FindAnyObjectByType<TestingSettingsMenu>();if(player==null || player.Health==null || menu==null || menu.Draft==null || Time.timeSinceLevelLoad<.5f)return;
            manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();manager.SwitchGameState(GameState.Playing);input=player.GetComponent<TouchInputManager>();Require(input.SteeringMode==MobileSteeringMode.Swipe,"Swipe must remain the default");
            foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>())director.enabled=false;
            foreach(var traffic in UnityEngine.Object.FindObjectsByType<TrafficSpawner>())traffic.enabled=false;
            foreach(var actor in UnityEngine.Object.FindObjectsByType<Enemy>())actor.gameObject.SetActive(false);
            foreach(var car in UnityEngine.Object.FindObjectsByType<Car>())car.gameObject.SetActive(false);
            player.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeAll;player.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;
            Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;input.SetGyroscopeTestSample(Gravity(0));input.ConfigureGyroscope(MobileSteeringMode.Gyroscope,1,2,0,false);Next();
        }else if(stage==1 && clock-at>.05f){Require(input.GyroscopeCalibrated && Mathf.Abs(input.Steering.x)<.001f,"Gyro must auto-calibrate without steering on entry");input.SetGyroscopeTestSample(Gravity(20));Next();
        }else if(stage==2 && clock-at>.2f){
            Require(input.Steering.x>.99f,"Constant physical tilt must continuously steer the real input interface");
            Require(input.aimButton.GetComponent<Graphic>().raycastTarget,"Gyro steering must preserve the mobile FIRE touch target");
            var point=new Vector2(Screen.width*.47f,Screen.height*.55f);int taps=input.PistolTapCount;
            Touch(TouchPhase.Began,point,Vector2.zero,Time.unscaledTime);Touch(TouchPhase.Moved,point+Vector2.left*100,Vector2.left*100,Time.unscaledTime+.05f);Touch(TouchPhase.Ended,point+Vector2.left*100,Vector2.zero,Time.unscaledTime+.1f);
            Require(input.Steering.x>.99f && input.PistolTapCount==taps,"Swipe gestures must not fight gyro steering or fire accidentally");input.aimButton.GetComponent<Button>().onClick.Invoke();Require(input.PistolTapCount==taps+1,"Mobile FIRE button must still work with gyro steering");
            manager.SwitchGameState(GameState.Pause);input.SetGyroscopeTestSample(Gravity(-20));Next();
        }else if(stage==3 && clock-at>.05f){Require(input.Steering==Vector2.zero,"Pause must suppress sensor steering");input.RequestGyroscopeCalibration();Next();
        }else if(stage==4 && clock-at>.05f){Require(input.GyroscopeCalibrated,"Calibration must work while the Tune menu is paused");manager.SwitchGameState(GameState.Playing);Next();
        }else if(stage==5 && clock-at>.05f){Require(Mathf.Abs(input.Steering.x)<.001f,"Calibrated held posture must be neutral after resume");input.SetGyroscopeTestSample(Gravity(0));Next();
        }else if(stage==6 && clock-at>.05f){Require(input.Steering.x>.99f,"New neutral must affect subsequent tilt");typeof(TouchInputManager).GetMethod("OnApplicationFocus",Private).Invoke(input,new object[]{false});Next();
        }else if(stage==7 && clock-at>.05f){Require(input.Steering==Vector2.zero,"Focus loss must clear tilt steering");typeof(TouchInputManager).GetMethod("OnApplicationFocus",Private).Invoke(input,new object[]{true});Next();
        }else if(stage==8 && clock-at>.05f){
            Require(Mathf.Abs(input.Steering.x)<.001f,"Focus return must recalibrate safely");input.SetGyroscopeTestSample(new Vector3(1,0,0),true,ScreenOrientation.LandscapeRight,false);Next();
        }else if(stage==9 && clock-at>.05f){
            Require(Mathf.Abs(input.Steering.x)<.001f,"Screen rotation must recenter without a sudden steering kick");input.SetGyroscopeTestSample(new Vector3(Mathf.Cos(20*Mathf.Deg2Rad),Mathf.Sin(20*Mathf.Deg2Rad),0),true,ScreenOrientation.LandscapeRight,false);input.ConfigureGyroscope(MobileSteeringMode.Gyroscope,1,2,0,true);Next();
        }else if(stage==10 && clock-at>.05f){
            Require(input.Steering.x<-.99f,"Orientation-aware real input must honor inversion");input.SetGyroscopeTestSample(Gravity(0),false);input.ConfigureGyroscope(MobileSteeringMode.Gyroscope,1,2,0,false);Require(!input.UsesGyroscopeSteering,"No sensor must fall back to swipe");
            var point=new Vector2(Screen.width*.47f,Screen.height*.55f);Touch(TouchPhase.Began,point,Vector2.zero,Time.unscaledTime);Touch(TouchPhase.Moved,point+Vector2.right*80,Vector2.right*80,Time.unscaledTime+.05f);Require(input.Steering.x>0,"Swipe fallback must still steer");Touch(TouchPhase.Ended,point+Vector2.right*80,Vector2.zero,Time.unscaledTime+.1f);
            Report("PASS native input routing, sustained tilt, no swipe/firing interference, calibration in pause, focus loss/return, orientation recenter, invert and unsupported-sensor swipe fallback.");
            input.ConfigureGyroscope(MobileSteeringMode.Swipe,1,2,.12f,false);manager.SwitchGameState(GameState.Pause);menu.Open();Button("GYROSCOPE");Enter("Gyro sensitivity","1.70");Enter("Gyro dead zone","1.0");Enter("Gyro smoothing","0.06");Button("INVERT TILT: OFF");Button("CALIBRATE ON APPLY");
            Require(input.SteeringMode==MobileSteeringMode.Swipe && menu.Draft.steeringMode==MobileSteeringMode.Gyroscope,"Tune controls must edit a draft until APPLY");
            Directory.CreateDirectory("Logs/SettingsMenu");typeof(TestingSettingsChecks).GetField("menu",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,menu);typeof(TestingSettingsChecks).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"gyro-controls"});
            menu.Apply(false);Require(input.SteeringMode==MobileSteeringMode.Gyroscope && Mathf.Abs(input.GyroSensitivity-1.7f)<.001f && input.GyroInverted,"Tune APPLY must switch mode and apply gyro parameters");Require(PlayerPrefs.HasKey(TestingSettings.PreferenceKey),"Gyro mode must save on this device");oldPlayer=player;manager.RestartLevel();Next();
        }else if(stage==11){
            player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();menu=UnityEngine.Object.FindAnyObjectByType<TestingSettingsMenu>();if(player==null || player==oldPlayer || player.Health==null || menu==null || menu.Draft==null)return;
            input=player.GetComponent<TouchInputManager>();manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();Require(input.SteeringMode==MobileSteeringMode.Gyroscope && Mathf.Abs(input.GyroSmoothing-.06f)<.001f && input.GyroInverted,"Gyro choice and settings must survive scene restart");
            manager.SwitchGameState(GameState.Pause);menu.Open();menu.ResetDraft();menu.Apply(false);Require(input.SteeringMode==MobileSteeringMode.Swipe && !PlayerPrefs.HasKey(TestingSettings.PreferenceKey),"RESET must restore authored swipe controls and clear preferences");
            Report("PASS actual Tune selector/sliders/invert/calibration UI, draft/apply, persistence across restart, reset and native overlay capture.");Finish(true,"PASS all gyro checks");
        }
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void Finish(bool success,string text){if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;
        if(SessionState.GetBool(Key+"HadPrefs",false))PlayerPrefs.SetString(TestingSettings.PreferenceKey,SessionState.GetString(Key+"Prefs",""));else PlayerPrefs.DeleteKey(TestingSettings.PreferenceKey);PlayerPrefs.Save();Report((success ? "" : "FAIL ")+text);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
