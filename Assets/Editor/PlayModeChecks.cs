using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.Player;
using CyberBiker.UI;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PlayModeChecks {
    private const string Key="CyberBiker.PlayModeChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static DifficultyManager difficulty;
    private static GameplayManager gameplay;
    private static EnemyEncounterDirector director;
    private static PlayerController player;
    private static int stage,lastFrame=-1,spawned;
    private static float stageAt;
    private static double began;
    private static bool done;
    private struct ArrivalSample {public Vector3 position;public float time;}
    private static readonly Dictionary<Enemy,ArrivalSample> arrivals=new Dictionary<Enemy,ArrivalSample>();
    private static int fadeSamples,motionSamples;
    private sealed class NeutralInput:IPlayerInput {public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;}
    static PlayModeChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool value,string text){if(!value)throw new Exception(text);}
    private static void Report(string text)=>File.AppendAllText("Logs/PlayModes/Result.txt","PASS "+text+"\n");
    private static void Advance(DifficultyManager value,float dt)=>typeof(DifficultyManager).GetMethod("AdvanceTime",Private).Invoke(value,new object[]{dt});
    public static void Run(){
        Directory.CreateDirectory("Logs/PlayModes");File.WriteAllText("Logs/PlayModes/Result.txt","RUNNING\n");
        SessionState.SetBool(Key+"HadPrefs",PlayerPrefs.HasKey(TestingSettings.PreferenceKey));
        SessionState.SetString(Key+"Prefs",PlayerPrefs.GetString(TestingSettings.PreferenceKey,""));
        try{
            EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");
            var value=UnityEngine.Object.FindAnyObjectByType<DifficultyManager>();
            Require(value.startMode==RunPlayMode.Progression && value.constantCombatDifficulty!=null && value.constantCombatDifficulty.name=="Difficulty 4","Saved scene must reference the old Difficulty 4 and default to progression");
            Require(value.constantCombatDifficulty.maxEnemiesOnRoad==5 && value.constantCombatDifficulty.enemiesPerWave==5,"Combat preset must target five riders");
            Require(value.difficultySections.Length==8 && value.loopFromSection==6 && Mathf.Approximately(value.difficultySections.Sum(s=>s.durationSeconds),300),"Progression assets or five-minute timeline changed");
            typeof(DifficultyManager).GetMethod("Awake",Private).Invoke(value,null);
            for(int i=0;i<8;i++){Require(value.CurrentSectionIndex==i && value.currentDifficulty==value.difficultySections[i],"Wrong progression boundary");Advance(value,value.SectionRemaining+.001f);}
            Require(value.CurrentSectionIndex==6,"Progression tail must retain the existing loop");
            // Reproduce an already-open scene missing the newly added field.
            var preset=value.constantCombatDifficulty;value.constantCombatDifficulty=null;
            RunModeSelection.Select(RunPlayMode.ConstantCombat);typeof(DifficultyManager).GetMethod("Awake",Private).Invoke(value,null);
            Require(value.IsConstantCombat && value.currentDifficulty==preset,"A missing scene reference must resolve the shared combat preset, not silently select progression");
            RunModeSelection.Select(RunPlayMode.Progression);
            Report("saved Level references the old five-rider Difficulty 4; all eight progression stages and the five-minute loop remain intact.");
            EditorSceneManager.OpenScene("Assets/Level/Scenes/Menu.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }catch(Exception e){Finish(false,e.ToString());}
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    private static void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Next(){stage++;stageAt=Time.time;Report("runtime stage "+stage);}
    private static bool PrepareLevel(){
        player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();gameplay=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();difficulty=UnityEngine.Object.FindAnyObjectByType<DifficultyManager>();
        var world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();
        if(player==null || player.Health==null || world==null || world.GetComponent<EnemyEncounterDirector>()==null || Time.timeSinceLevelLoad<.35f)return false;
        director=world.GetComponent<EnemyEncounterDirector>();gameplay.SwitchGameState(GameState.Playing);gameplay.TapPistolCanKill=true;
        foreach(var shape in player.GetComponentsInChildren<Collider>(true))shape.enabled=false;
        player.GetComponent<TouchInputManager>().enabled=false;
        var body=player.GetComponent<Rigidbody>();body.linearVelocity=Vector3.forward*110;body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
        player.ConfigureTestingMovement(110,player.ForwardAcceleration,player.SteeringAcceleration,player.SteeringSpeedLimit);
        typeof(PlayerController).GetField("input",Private).SetValue(player,new NeutralInput());
        Time.captureDeltaTime=.05f;FinisherClock.EditorCaptureStep=.05f;
        return true;
    }
    private static void Tick(){
        if(done || !EditorApplication.isPlaying || lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try{
            Require(EditorApplication.timeSinceStartup-began<150,"Play mode check timed out at stage "+stage+"; riders="+(director!=null ? director.Riders.Count : -1));
            if(stage==0){
                var menu=UnityEngine.Object.FindAnyObjectByType<MainMenuManager>();if(menu==null)return;
                menu.StartGame();menu.StartGame();var picker=menu.GetComponent<PlayModeMenu>();
                Require(picker!=null && picker.IsOpen && menu.GetComponents<PlayModeMenu>().Length==1,"Play must open exactly one mode selector");
                Require(picker.GetComponentsInChildren<Button>(true).Length==3,"Selector needs two modes and Back");
                Capture(picker.GetComponentInChildren<Canvas>(true),"ModeSelection");picker.Close();Require(!picker.IsOpen,"Back must close the picker");
                menu.StartGame();picker.GetComponentsInChildren<Button>().Single(b=>b.name=="Constant Combat").onClick.Invoke();Next();
            }else if(stage==1){
                if(!PrepareLevel())return;
                Require(difficulty.IsConstantCombat && difficulty.currentDifficulty==difficulty.constantCombatDifficulty && RunModeSelection.Selected==RunPlayMode.ConstantCombat,"Menu choice did not reach the real Level");
                var settings=difficulty.currentDifficulty;
                Require(settings.carOffsetModifier==25 && settings.oneCarRow==2 && settings.twoCarRow==4 && settings.threeCarRow==1,"Combat mode must use the old traffic spacing and row weights");
                Require(Time.timeSinceLevelLoad>=settings.enemyInitialDelay || director.Riders.Count==0,"Opening must respect the preset's short arrival lead-in");
                Advance(difficulty,600);Require(difficulty.IsConstantCombat && difficulty.currentDifficulty==settings && difficulty.CurrentSectionIndex==0,"Constant mode advanced into progression");Next();
            }else if(stage==2){
                SilenceEnemies();ObserveArrivals();Require(director.Riders.Count<=5,"Constant mode exceeded its rider cap");
                if(director.Riders.Count<5 || director.ActiveFighter==null || !director.ActiveFighter.HasInitialized || director.ActiveFighter.IsArriving)return;
                Require(director.Riders.Count(e=>!e.IsEncounterQueued)==1,"Only one rider should retain the existing combat role");
                Require(director.RearArrivalsSpawned+director.FrontArrivalsSpawned==5 && fadeSamples>0 && motionSamples>0,"All five opening riders must use physical approaching/overtaking and fading arrivals");
                spawned=director.SpawnedCount;
                var defeated=director.ActiveFighter;defeated.TakeDamage(new DamageInfo(10000,player.gameObject));
                Require(defeated.IsFinisherReady && defeated.DefeatFromSword(player.gameObject,0,.01f),"Fixture must perform a real enemy defeat before testing replacement");Next();
            }else if(stage==3){
                SilenceEnemies();ObserveArrivals();Require(director.Riders.Count<=5,"Replacement exceeded five riders");
                if(director.SpawnedCount<=spawned || director.Riders.Count<5)return;
                Require(director.WaveCooldownRemaining==0,"Constant mode waited for the old wave cooldown");
                Report("the unmodified combat preset uses original traffic weights/spacing; all five join through smooth arrivals with measured movement and intermediate fade values; Difficulty 4 stays active and replacements arrive while the other four remain.");
                float run=difficulty.RunElapsed;gameplay.SwitchGameState(GameState.Pause);typeof(DifficultyManager).GetMethod("Update",Private).Invoke(difficulty,null);Require(run==difficulty.RunElapsed,"Pause advanced the constant run");gameplay.SwitchGameState(GameState.Playing);
                gameplay.RestartLevel();Next();
            }else if(stage==4){
                if(!PrepareLevel())return;
                Require(difficulty.IsConstantCombat,"Restart lost the chosen play mode");
                var tune=UnityEngine.Object.FindAnyObjectByType<TestingSettingsMenu>();if(tune==null || tune.Draft==null)return;
                tune.Open();tune.GetComponentsInChildren<Button>(true).Single(b=>b.name=="PROGRESSION").onClick.Invoke();tune.Close();
                Require(difficulty.IsConstantCombat && RunModeSelection.Selected==RunPlayMode.ConstantCombat,"Cancel applied an unconfirmed mode choice");
                gameplay.SwitchGameState(GameState.Playing);tune.Open();
                Require(tune.GetComponentsInChildren<Button>(true).Single(b=>b.name=="CONSTANT COMBAT").GetComponent<Image>().color.g>.8f,"Reopening TUNE retained the cancelled draft");
                Capture(tune.GetComponentInChildren<Canvas>(true),"TuneModes");
                tune.GetComponentsInChildren<Button>(true).Single(b=>b.name=="PROGRESSION").onClick.Invoke();tune.Apply(false);Next();
            }else if(stage==5){
                if(!PrepareLevel())return;
                Require(!difficulty.IsConstantCombat && difficulty.currentDifficulty==difficulty.difficultySections[0] && difficulty.CurrentSectionIndex==0,"TUNE switch must restart into the progression opening");
                Require(director.Riders.Count==0,"Constant-combat enemies leaked into the new progression run");
                Report("restart preserves the chosen mode; cancelling TUNE leaves it unchanged; applying another mode reloads cleanly into the peaceful progression opening.");
                spawned=director.SpawnedCount;Advance(difficulty,39-difficulty.SectionElapsed);Next();
            }else if(stage==6 && Time.time-stageAt>.4f){
                Require(director.SpawnedCount==spawned && director.Riders.Count==0,"Warmup spawned enemies");Advance(difficulty,1.1f);Next();
            }else if(stage==7){
                SilenceEnemies();if(director.SpawnedCount==spawned)return;
                Require(difficulty.CurrentSectionIndex==1 && director.SpawnedCount==spawned+1,"Progression must start its original solo rival stage");
                Report("real progression still starts without riders and then introduces its original single-rival first encounter.");
                var tune=UnityEngine.Object.FindAnyObjectByType<TestingSettingsMenu>();if(tune==null || tune.Draft==null)return;
                tune.Open();tune.GetComponentsInChildren<Button>(true).Single(b=>b.name=="CONSTANT COMBAT").onClick.Invoke();tune.Apply(false);Next();
            }else if(stage==8){
                if(!PrepareLevel())return;
                Require(difficulty.IsConstantCombat && difficulty.currentDifficulty.carOffsetModifier==25 && difficulty.currentDifficulty.oneCarRow==2 && difficulty.currentDifficulty.twoCarRow==4 && difficulty.currentDifficulty.threeCarRow==1,"TUNE must restart into the original combat traffic layout");
                SilenceEnemies();ObserveArrivals();Require(director.Riders.Count<=5,"TUNE combat start exceeded its rider cap");
                if(director.Riders.Count<5 || director.ActiveFighter==null || director.ActiveFighter.IsArriving)return;
                Require(director.RearArrivalsSpawned+director.FrontArrivalsSpawned==5,"TUNE combat start must also use the smooth arrival path for all five riders");
                Report("the real TUNE Progression-to-Combat Apply path also starts through smooth arrivals, retaining the original traffic settings and five-rider target.");Finish(true,"PASS native two-play-mode checks.");
            }
        }catch(Exception e){Finish(false,e.ToString());}
    }
    private static void SilenceEnemies(){foreach(var enemy in director.Riders)if(enemy!=null)typeof(Enemy).GetField("shootingDamage",Private).SetValue(enemy,0f);}
    private static void ObserveArrivals(){
        foreach(var enemy in director.Riders){
            if(enemy==null)continue;
            if(!arrivals.TryGetValue(enemy,out var previous)){
                Require(enemy.IsArriving,"A newly spawned rider appeared without the smooth arrival path");
                float gap=enemy.transform.position.z-player.transform.position.z;
                Require(enemy.ArrivingFromBehind ? gap< -15 : gap>90,"New riders must begin behind/outside the near fighting area or far ahead");
            }else if(enemy.IsArriving){
                float dt=Time.time-previous.time;float movement=Vector3.Distance(enemy.transform.position,previous.position);
                Require(movement<=(110+difficulty.currentDifficulty.enemyArrivalClosingSpeed+50)*(dt+.03f)+2,"Arrival position jumped instead of riding smoothly");
                if(movement>.01f)motionSamples++;
            }
            if(enemy.IsArriving && enemy.ArrivalVisibility>0 && enemy.ArrivalVisibility<.99f)fadeSamples++;
            arrivals[enemy]=new ArrivalSample{position=enemy.transform.position,time=Time.time};
        }
    }
    private static void Capture(Canvas canvas,string name){
        var mode=canvas.renderMode;var view=canvas.worldCamera;float distance=canvas.planeDistance;var active=RenderTexture.active;
        var nodes=canvas.GetComponentsInChildren<Transform>(true);var layers=new int[nodes.Length];
        var camera=new GameObject("Play mode capture camera").AddComponent<Camera>();var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{
            for(int i=0;i<nodes.Length;i++){layers[i]=nodes[i].gameObject.layer;nodes[i].gameObject.layer=31;}
            camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.01f,.02f,.04f);camera.orthographic=true;camera.cullingMask=1<<31;camera.targetTexture=rt;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();
            RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes("Logs/PlayModes/"+name+".png",pixels.EncodeToPNG());
        }finally{
            canvas.renderMode=mode;canvas.worldCamera=view;canvas.planeDistance=distance;for(int i=0;i<nodes.Length;i++)nodes[i].gameObject.layer=layers[i];RenderTexture.active=active;
            UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
    private static void Finish(bool success,string text){
        if(done)return;done=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        if(SessionState.GetBool(Key+"HadPrefs",false))PlayerPrefs.SetString(TestingSettings.PreferenceKey,SessionState.GetString(Key+"Prefs",""));else PlayerPrefs.DeleteKey(TestingSettings.PreferenceKey);PlayerPrefs.Save();
        FinisherClock.EditorCaptureStep=0;Time.captureDeltaTime=0;File.AppendAllText("Logs/PlayModes/Result.txt",text+"\n");Debug.Log(text);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);
    }
}
