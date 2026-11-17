using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CyberBiker.Balance;
using CyberBiker.Combat;
using CyberBiker.Player;
using CyberBiker.Scoring;
using CyberBiker.UI;
using Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BalanceTuneChecks {
    private const string Key="CyberBiker.BalanceTuneChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static PlayerController player,previousPlayer;
    private static GameplayManager game;
    private static TestingSettingsMenu menu;
    private static int stage,lastFrame=-1;
    private static double began;
    private static bool done;
    private static string exported,profileBefore,gapBefore;
    private sealed class Neutral:IPlayerInput {public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;}
    static BalanceTuneChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool value,string text){if(!value)throw new Exception(text);}
    private static void Near(float a,float b,string name)=>Require(Mathf.Abs(a-b)<.001f,name+": "+a+" != "+b);
    private static void Report(string text)=>File.AppendAllText("Logs/BalanceTune/Result.txt","PASS "+text+"\n");
    public static void Run(){
        Directory.CreateDirectory("Logs/BalanceTune");File.WriteAllText("Logs/BalanceTune/Result.txt","RUNNING\n");
        SessionState.SetBool(Key+"HadPrefs",PlayerPrefs.HasKey(TestingSettings.PreferenceKey));SessionState.SetString(Key+"Prefs",PlayerPrefs.GetString(TestingSettings.PreferenceKey,""));PlayerPrefs.DeleteKey(TestingSettings.PreferenceKey);
        try{
            var settings=new StyleScoringSettings();var combo=new StyleCombo();combo.Add(StyleAction.PistolHit,40,settings);combo.Add(StyleAction.NearMiss,120,settings);combo.Add(StyleAction.SwordFinisher,800,settings);
            var distanceRun=new CyberBiker.Runs.RunSession(GameState.Playing);distanceRun.Tick(1,100);Near(distanceRun.Score,1,"Original distance score rate");distanceRun.DistanceScoreRate=.02f;distanceRun.Tick(1,100);Near(distanceRun.Score,3,"Configured distance score rate");distanceRun.DistanceScoreRate=0;distanceRun.Tick(1,100);Near(distanceRun.Score,3,"Disabled distance scoring");
            Require(combo.Potential==1920 && combo.Multiplier==2,"Default combo arithmetic changed");int banked=0;combo.Banked+=v=>banked+=v;combo.BankAfterImpact(.25f);combo.BankAfterImpact(.25f);Require(banked==480 && combo.BankCount==1 && combo.Multiplier==1,"Configurable hit payout must pay the selected fraction exactly once");
            settings.stepsPerMultiplier=2;settings.repeatSteps=0;settings.varietySteps=3;settings.repetitionValue=.5f;settings.minimumRepeatValue=.1f;
            combo.Add(StyleAction.PistolHit,100,settings);combo.Add(StyleAction.NearMiss,100,settings);combo.Add(StyleAction.NearMiss,100,settings);Require(combo.Multiplier==2 && combo.BasePoints==250,"Configured variety/repeat steps or repeated points are ignored");
            settings.maximumMultiplier=1;combo.Reconfigure(settings);Require(combo.Multiplier==1,"Live multiplier cap is ignored");
            var zeroRepeat=new StyleCombo();var zeroSettings=new StyleScoringSettings{repetitionValue=.1f,minimumRepeatValue=0,minimumAwardPoints=0};zeroRepeat.Add(StyleAction.PistolHit,1,zeroSettings);int moves=zeroRepeat.Moves;Require(zeroRepeat.Add(StyleAction.PistolHit,1,zeroSettings)==0 && zeroRepeat.Moves==moves,"Zero-point repeats must not keep a combo alive");
            var source=new TestingSettings{maxSpeed=75,acceleration=300,steeringAcceleration=50,steeringSpeed=20,launchMultiplier=1.35f,launchDuration=7,pistolDamage=15,shotInterval=.45f,enemyDistance=20,touchSensitivity=10};
            var copy=source.Copy();copy.scoreBalance.pistolHit=999;copy.triggerBalance.bulletRadius=5;Require(source.scoreBalance.pistolHit==40 && source.triggerBalance.bulletRadius==3.2f,"Draft copies share mutable nested settings");
            JsonUtility.FromJsonOverwrite("{\"maxSpeed\":95}",copy);Require(copy.scoreBalance.pistolHit==999 && copy.triggerBalance.bulletRadius==5,"Legacy saved settings discarded new balance groups");
            var invalid=source.Copy();invalid.scoreBalance.impactPayoutFraction=99;invalid.scoreBalance.comboWindow=float.NaN;invalid.triggerBalance.evadeWindow=.1f;invalid.triggerBalance.perfectWindow=.25f;invalid.triggerBalance.bulletRadius=float.PositiveInfinity;invalid.Normalize();
            Near(invalid.scoreBalance.impactPayoutFraction,1,"Payout clamping");Near(invalid.scoreBalance.comboWindow,5,"Non-finite combo fallback");Near(invalid.triggerBalance.perfectWindow,.1f,"Perfect window must fit evade window");Near(invalid.triggerBalance.bulletRadius,3.2f,"Non-finite radius fallback");
            Require(!BalancePresetIO.TryImport("{}",source,out _,out _) && !BalancePresetIO.TryImport("not json",source,out _,out _) && !BalancePresetIO.TryImport(new string('x',BalancePresetIO.MaximumCharacters+1),source,out _,out _),"Invalid or oversized presets accepted");
            string json=BalancePresetIO.ExportJson(source);Require(BalancePresetIO.TryImport(json,source,out var imported,out _) && imported.scoreBalance.pistolHit==40,"Portable preset round trip failed");
            Require(BalancePresetIO.TryImport("\uFEFF"+json,source,out _,out _),"UTF-8 BOM preset was rejected");
            Require(BalancePresetIO.TryImport("{\"format\":\"CyberBikerBalance\",\"schemaVersion\":1,\"settings\":{\"scoreBalance\":{\"pistolHit\":88}}}",source,out var partial,out _) && partial.maxSpeed==75 && partial.scoreBalance.pistolHit==88 && partial.scoreBalance.pistolKill==350 && partial.triggerBalance.bulletRadius==3.2f,"Partial presets must retain omitted current/default values");
            Require(!BalancePresetIO.TryImport(json.Replace("\"schemaVersion\": 1","\"schemaVersion\": 99"),source,out _,out _),"Future schema must be rejected without applying");
            Report("default arithmetic retained; live combo growth/caps and fractional hit banking; deep draft copies; legacy field overlay; finite/range/cross-field validation; invalid/future/oversized preset rejection.");
            Screen.SetResolution(1280,720,false);EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }catch(Exception e){Finish(false,e.ToString());}
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    private static void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static bool Prepare(){
        player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();game=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();menu=UnityEngine.Object.FindAnyObjectByType<TestingSettingsMenu>();
        if(player==null || player.Health==null || menu==null || menu.Draft==null || Time.timeSinceLevelLoad<.4f)return false;
        game.SwitchGameState(GameState.Playing);player.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeAll;player.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;player.GetComponent<TouchInputManager>().enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(player,new Neutral());return true;
    }
    private static TMP_InputField Input(string name)=>menu.GetComponentsInChildren<TMP_InputField>(true).Single(f=>f.transform.parent.name==name);
    private static void Enter(string name,string value){var input=Input(name);input.text=value;input.onEndEdit.Invoke(value);}
    private static void Tick(){
        if(done || !EditorApplication.isPlaying || lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try{
            Require(EditorApplication.timeSinceStartup-began<130,"Balance TUNE timed out at "+stage);
            if(stage==0){
                if(!Prepare())return;
                Near(game.StyleScoring.impactPayoutFraction,.5f,"Authored hit payout");Near(game.StyleScoring.repetitionValue,.72f,"Authored repetition");
                File.WriteAllText("Logs/BalanceTune/AuthoredDefaults.json",BalancePresetIO.ExportJson(TestingSettings.Defaults));
                profileBefore=JsonUtility.ToJson(Resources.Load<CloseDodgeProfile>("CloseDodgeTuning"));gapBefore=JsonUtility.ToJson(game.GapDashProfile);
                menu.Open();Require(menu.IsOpen && game.gameState==GameState.Pause,"TUNE must pause the game");
                foreach(var type in new[]{typeof(ScoreBalanceTuning),typeof(TriggerBalanceTuning)})foreach(var field in type.GetFields())if(field.GetCustomAttribute<BalanceControlAttribute>()!=null && field.FieldType!=typeof(bool))Require(Input(field.Name)!=null,"Missing TUNE balance control: "+field.Name);
                menu.SelectTab(1);Enter("pistolHit","77");Require(menu.Draft.scoreBalance.pistolHit==77 && game.StyleScoring.pistolHit==40,"Editing draft changed live rewards");
                var detached=menu.Draft;detached.scoreBalance.pistolHit=999;Require(menu.Draft.scoreBalance.pistolHit==77,"Public draft leaks nested state");
                menu.Close();game.SwitchGameState(GameState.Playing);menu.Open();Require(menu.Draft.scoreBalance.pistolHit==40,"Cancel retained draft changes");
                Enter("distancePointsPerUnit","0.02");
                Enter("pistolHit","77");Enter("pistolKill","900");Enter("bulletDodge","222");Enter("nearMiss","300");Enter("impactPayoutFraction","0.25");Enter("comboWindow","7.5");Enter("maximumMultiplier","5");Enter("stepsPerMultiplier","2");Enter("minimumRepeatValue","0.4");Enter("razorClearance","0.2");Enter("razorMultiplier","3");Enter("postHitDrivingCooldown","0.8");Capture("Score");
                menu.SelectTab(2);Enter("bulletRadius","2.1");Enter("bulletCenterHeight","2.3");Enter("cinematicRadius","0.7");Enter("cinematicCenterHeight","2.2");Enter("cinematicCooldown","0.4");Enter("evadeWindow","0.2");Enter("perfectWindow","0.08");Enter("perfectRewardCooldown","0.5");Enter("gapSnapRadius","4");Enter("gapTriggerDistance","45");Enter("swordTargetRange","120");Enter("swordPickupRange","10");Enter("swordSideRange","7");Capture("Triggers");
                menu.OpenPresets();exported=menu.ExportPresetJson();Require(!exported.Contains("instanceID") && !exported.Contains("m_FileID"),"Preset contains nonportable asset references");
                Require(BalancePresetIO.TryImport(exported,menu.Draft,out var roundtrip,out _) && roundtrip.scoreBalance.pistolHit==77 && Mathf.Abs(roundtrip.triggerBalance.bulletRadius-2.1f)<.001f,"Edited preset round trip failed");
                string path=BalancePresetIO.ExportFile(exported);Require(File.Exists(path) && File.ReadAllText(path)==exported,"File export bytes differ from visible draft");File.WriteAllText("Logs/BalanceTune/ExampleBalance.json",exported);Capture("Presets");
                Require(!menu.ImportPresetJson("{}") && menu.Draft.scoreBalance.pistolHit==77,"Invalid import mutated the draft");
                menu.Close();Require(menu.IsOpen && !menu.PresetsOpen,"Back must close presets before leaving TUNE");Enter("pistolHit","999");Require(menu.ImportPresetJson(exported) && Input("pistolHit").text=="77" && game.StyleScoring.pistolHit==40,"Import must refresh fields without applying to gameplay");
                menu.Apply(false);game.SwitchGameState(GameState.Playing);
                Near(game.Session.DistanceScoreRate,.02f,"Applied distance score rate");
                Near(game.StyleScoring.pistolHit,77,"Applied reward");Near(game.StyleScoring.comboWindow,7.5f,"Applied combo");Near(game.StyleScoring.razorMultiplier,3,"Applied close shave multiplier");Near(game.StyleScoring.impactPayoutFraction,.25f,"Applied hit payout");
                var feedback=player.GetComponent<ShootingFeedback>();Near(feedback.NearMissRadius,2.1f,"Applied bullet scoring radius");Near(feedback.NearMissCenter.y-player.transform.position.y,2.3f,"Applied scoring centre height");Near(game.CloseDodge.passRadius,.7f,"Applied cinematic radius");Near(game.ProjectileCombat.PerfectWindow,.08f,"Applied perfect window");Near(game.GapDash.maximumTriggerDistance,45,"Applied gap trigger");Near(TestingSettings.Read(player,game,player.GetComponent<BikeSwordCombat>()).triggerBalance.swordPickupRange,10,"Applied finisher range");
                Require(JsonUtility.ToJson(Resources.Load<CloseDodgeProfile>("CloseDodgeTuning"))==profileBefore && JsonUtility.ToJson(game.GapDashProfile)==gapBefore,"Applying phone settings modified authored profile assets");
                var score=game.StyleScore;int awards=score.Combo.AwardCount;var center=feedback.NearMissCenter;
                feedback.CheckNearMiss(center+new Vector3(2.2f,0,2),center+new Vector3(2.2f,0,-2),.02f);Require(score.Combo.AwardCount==awards,"Bullet outside tuned radius scored");
                feedback.CheckNearMiss(center+new Vector3(1.9f,0,2),center+new Vector3(1.9f,0,-2),.02f);Require(score.Combo.AwardCount==awards+1 && score.Combo.BasePoints==222,"Real bullet pass ignored tuned radius/reward");score.Combo.Bank();
                score.Record(StyleAction.PistolHit,center);score.Record(StyleAction.NearMiss,center);int expected=Mathf.FloorToInt(score.Combo.Potential*.25f);float before=game.Session.Score;player.TakeDamage(new DamageInfo(1));score.Crash();Near(game.Session.Score-before,expected,"Actual damage must use tuned payout without duplicate credit");
                Report("all numeric balance controls exist; tabs render; draft/cancel/import separation; portable JSON and exact file bytes; actual bullet pass obeys tuned radius and points; actual damage obeys tuned payout once; authored assets unchanged.");
                previousPlayer=player;game.RestartLevel();stage=1;
            }else if(stage==1){
                if(!Prepare() || player==previousPlayer)return;
                Near(game.Session.DistanceScoreRate,.02f,"Saved distance score rate after restart");
                Near(game.StyleScoring.pistolHit,77,"Saved reward after restart");Near(player.GetComponent<ShootingFeedback>().NearMissRadius,2.1f,"Saved radius after restart");Near(game.CloseDodge.passRadius,.7f,"Saved cinematic radius after restart");Near(game.GapDash.maximumTriggerDistance,45,"Saved gap range after restart");
                Near(TestingSettings.Defaults.scoreBalance.pistolHit,40,"Original points remain defaults");Near(TestingSettings.Defaults.triggerBalance.bulletRadius,3.2f,"Original radius remains default");
                menu.Open();menu.ResetDraft();Near(menu.Draft.scoreBalance.pistolHit,40,"Reset draft points");Near(game.StyleScoring.pistolHit,77,"Reset draft applied prematurely");menu.Apply(false);game.SwitchGameState(GameState.Playing);
                Near(game.StyleScoring.pistolHit,40,"Reset applied points");Near(game.StyleScoring.impactPayoutFraction,.5f,"Reset hit payout");Near(player.GetComponent<ShootingFeedback>().NearMissRadius,3.2f,"Reset applied radius");Require(!PlayerPrefs.HasKey(TestingSettings.PreferenceKey),"Reset left saved overrides");
                Near(game.Session.DistanceScoreRate,.01f,"Reset applied distance score rate");
                Report("new balance groups save across scene restart; original defaults remain intact; Reset stays a draft until Apply and clears saved overrides.");
                PlayerPrefs.SetString(TestingSettings.PreferenceKey,"{\"maxSpeed\":95}");PlayerPrefs.Save();previousPlayer=player;game.RestartLevel();stage=2;
            }else if(stage==2){
                if(!Prepare() || player==previousPlayer)return;
                Near(player.NormalSpeedLimit,95,"Legacy saved movement setting");Near(game.StyleScoring.pistolHit,40,"Legacy settings retain authored points");Near(player.GetComponent<ShootingFeedback>().NearMissRadius,3.2f,"Legacy settings retain authored trigger radius");
                Report("old saved JSON containing only existing fields loads without zeroing the new scoring and trigger settings.");Finish(true,"PASS native balance tuning and export/import checks.");
            }
        }catch(Exception e){Finish(false,e.ToString());}
    }
    private static void Capture(string name){
        var canvas=menu.GetComponentInChildren<Canvas>(true);var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;var active=RenderTexture.active;
        var nodes=canvas.GetComponentsInChildren<Transform>(true);var layers=new int[nodes.Length];var camera=new GameObject("Balance capture camera").AddComponent<Camera>();var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{for(int i=0;i<nodes.Length;i++){layers[i]=nodes[i].gameObject.layer;nodes[i].gameObject.layer=31;}camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.cullingMask=1<<31;camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes("Logs/BalanceTune/"+name+".png",pixels.EncodeToPNG());}
        finally{canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;for(int i=0;i<nodes.Length;i++)nodes[i].gameObject.layer=layers[i];RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    private static void Finish(bool success,string text){
        if(done)return;done=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        if(SessionState.GetBool(Key+"HadPrefs",false))PlayerPrefs.SetString(TestingSettings.PreferenceKey,SessionState.GetString(Key+"Prefs",""));else PlayerPrefs.DeleteKey(TestingSettings.PreferenceKey);PlayerPrefs.Save();
        File.AppendAllText("Logs/BalanceTune/Result.txt",text+"\n");Debug.Log(text);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);
    }
}
