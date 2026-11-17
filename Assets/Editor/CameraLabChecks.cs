using System;
using System.IO;
using System.Reflection;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using CyberBiker.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using S=CyberBiker.EditorTools.CameraLabTuning.Scope;

[InitializeOnLoad]
public static class CameraLabChecks {
    private const string Key="CyberBiker.CameraLabChecks";
    private static double began,at;
    private static int stage;
    private static bool finished;
    private static CameraLabTuning tuning;
    private static CameraLabWindow window;
    private static Vector3 left;
    static CameraLabChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        Directory.CreateDirectory("Logs/CameraLab");File.WriteAllText("Logs/CameraLab/Result.txt","RUNNING\n");
        try{
            tuning=CameraLabTuning.ReadSavedDefaults();CheckMigration(tuning);var copy=tuning.Copy();Require(copy.values.Count==CameraLabTuning.Controls.Length,"Preset roundtrip dropped controls");
            var changed=copy.Get(S.Rig,"ridingFov");changed.number=float.NaN;bool rejected=false;try{copy.Normalize();}catch(ArgumentException){rejected=true;}Require(rejected,"Invalid preset must be rejected");
            string folder="Assets/CameraLabCheckFixture";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","CameraLabCheckFixture");
            AssetDatabase.CopyAsset(CameraLabTuning.PlayerPath,folder+"/Player.prefab");AssetDatabase.CopyAsset(DodgeLabWindow.ProfilePath,folder+"/Dodge.asset");
            copy=tuning.Copy();copy.Get(S.Rig,"ridingFov").number=61;copy.Get(S.Rig,"combatRiding.fov").number=73;copy.Get(S.Rig,"combatAim.fov").number=47;copy.Get(S.Rig,"aimFov").number=39;copy.Get(S.Rig,"aimFollowResponse").number=6;copy.Get(S.Rig,"aimFraming.maximumYaw").number=11;copy.Get(S.Launch,"fovIncrease").number=13;copy.Get(S.Finisher,"hitCameraSideShift").number=1.7f;copy.Get(S.Dodge,"sideways").number=1.1f;
            copy.SaveForGame(folder+"/Rig.asset",folder+"/Player.prefab",folder+"/Dodge.asset");
            var root=new GameObject("Profile test");root.SetActive(false);try{var rig=root.AddComponent<CameraController>();AssetDatabase.LoadAssetAtPath<CameraLabRigProfile>(folder+"/Rig.asset").Apply(rig);using(var data=new SerializedObject(rig)){Require(Mathf.Abs(data.FindProperty("ridingFov").floatValue-61)<.001f,"Rig save/load failed");Require(data.FindProperty("combatRiding.configured").boolValue && Mathf.Abs(data.FindProperty("combatRiding.fov").floatValue-73)<.001f,"Independent combat camera did not save");Require(data.FindProperty("combatAim.configured").boolValue && data.FindProperty("combatAim.fov").floatValue==47 && data.FindProperty("aimFov").floatValue==39 && data.FindProperty("aimFollowResponse").floatValue==6,"Separate aiming settings did not save");Require(Mathf.Abs(data.FindProperty("aimFraming.maximumYaw").floatValue-11)<.001f,"Nested aim config did not save");}}
            finally{UnityEngine.Object.DestroyImmediate(root);}
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(CameraLabTuning.PlayerPath);var saved=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/Player.prefab");
            using(var a=new SerializedObject(original.GetComponent<BikeSwordCombat>()))using(var b=new SerializedObject(saved.GetComponent<BikeSwordCombat>())){foreach(var field in new[]{"strikePlaybackSpeed","dashDuration","launchDuration","poseBlendTime","strikeContactTime"})Require(a.FindProperty(field).floatValue==b.FindProperty(field).floatValue,"Save changed gameplay/animation: "+field);}
            var savedDodge=AssetDatabase.LoadAssetAtPath<CloseDodgeProfile>(folder+"/Dodge.asset").settings;var originalDodge=DodgeLabWindow.ReadSaved();Require(savedDodge.passRadius==originalDodge.passRadius && savedDodge.worldSpeed==originalDodge.worldSpeed && Mathf.Abs(savedDodge.sideways-1.1f)<.001f,"Save touched dodge gameplay/timing or failed camera save");
            AssetDatabase.DeleteAsset(folder);Report("PASS strict camera-only preset save/load, nested fields, invalid-value rejection; sword gameplay and dodge timing preserved.");
            EditorSceneManager.OpenScene("Assets/Level/Scenes/Menu.unity");EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Level/Scenes/Menu.unity");AudioListener.volume=.37f;
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Stage",0);CameraLabWindow.Open();CameraLabPreview.Sound=false;CameraLabPreview.Start(tuning);
        }catch(Exception e){Finish(false,e.ToString());}
    }
    private static void CheckMigration(CameraLabTuning tuning){
        var legacy=tuning.Copy();legacy.values.RemoveAll(v=>v.field.StartsWith("combatRiding.") || v.field.StartsWith("combatAim.") || v.field=="aimFollowResponse");legacy.Normalize();
        Require(legacy.Get(S.Rig,"combatRiding.offset").vector==legacy.Get(S.Rig,"targetOffset").vector && legacy.Get(S.Rig,"combatRiding.fov").number==legacy.Get(S.Rig,"ridingFov").number,"Old Lab drafts did not inherit the authored riding camera");
        Require(legacy.Get(S.Rig,"combatAim.offset").vector==legacy.Get(S.Rig,"aimOffset").vector && legacy.Get(S.Rig,"combatAim.fov").number==legacy.Get(S.Rig,"aimFov").number && legacy.Get(S.Rig,"aimFollowResponse").number==legacy.Get(S.Rig,"smoothness").number,"Old aiming draft was not preserved");
        var root=new GameObject("Legacy camera profile check");root.SetActive(false);var profile=ScriptableObject.CreateInstance<CameraLabRigProfile>();
        try{var rig=root.AddComponent<CameraController>();profile.rigJson="{\"ridingFov\":67,\"targetOffset\":{\"x\":1,\"y\":-3,\"z\":11}}";profile.Apply(rig);using(var data=new SerializedObject(rig)){Require(data.FindProperty("combatRiding.fov").floatValue==67 && data.FindProperty("combatRiding.offset").vector3Value==new Vector3(1,-3,11),"Old saved camera profile was not migrated from its own riding settings");}}
        finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(profile);}
        root=new GameObject("Combat riding profile migration");root.SetActive(false);profile=ScriptableObject.CreateInstance<CameraLabRigProfile>();
        try{var rig=root.AddComponent<CameraController>();profile.rigJson="{\"combatRiding\":{\"configured\":true,\"fov\":72},\"aimFov\":46,\"aimOffset\":{\"x\":-2,\"y\":-3,\"z\":10},\"smoothness\":6}";profile.Apply(rig);using(var data=new SerializedObject(rig)){Require(data.FindProperty("combatRiding.fov").floatValue==72 && data.FindProperty("combatAim.fov").floatValue==46 && data.FindProperty("combatAim.offset").vector3Value==new Vector3(-2,-3,10) && data.FindProperty("combatAim.followResponse").floatValue==6 && data.FindProperty("aimFollowResponse").floatValue==6,"Adding combat aim overwrote prior camera tuning");}}
        finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(profile);}
        Report("PASS old Lab drafts/presets and saved rig profiles retain their existing camera values.");
    }
    private static void CheckProximity(){
        Enemy enemy=null;foreach(var e in Enemy.Instances)if(e!=null && e.IsAlive && e.gameObject.activeInHierarchy){enemy=e;break;}Require(enemy!=null,"Combat fixture has no enemy");
        var rider=CameraLabPreview.Player.transform.position;var original=enemy.transform.position;
        CombatRidingCamera Fresh()=>new CombatRidingCamera{enemyRange=30,exitPadding=10,returnDelay=.3f,blendTime=.5f};
        var proximity=Fresh();enemy.transform.position=rider+Vector3.forward*35;proximity.Tick(rider,.1f);Require(!proximity.EnemyNearby && proximity.Amount==0,"Enemy outside entry radius triggered combat");
        enemy.transform.position=rider+Vector3.forward*25;proximity.Tick(rider,.1f);Require(proximity.EnemyNearby && proximity.Amount>0 && proximity.Amount<1,"Combat blend jumped instantly");
        enemy.transform.position=rider+Vector3.forward*37;proximity.Tick(rider,.4f);Require(proximity.EnemyNearby && proximity.Amount>.99f,"Exit padding failed to retain combat");
        enemy.transform.position=rider+Vector3.forward*42;proximity.Tick(rider,.1f);Require(proximity.EnemyNearby,"Return delay was skipped");float paused=proximity.Amount;proximity.Tick(rider,0);Require(proximity.Amount==paused && proximity.EnemyNearby,"Paused detection advanced");proximity.Tick(rider,.21f);Require(!proximity.EnemyNearby && proximity.Amount>0 && proximity.Amount<1,"Combat return was not smoothly blended");proximity.Tick(rider,.5f);Require(proximity.Amount==0,"Blend never returned to cruising");
        enemy.transform.position=rider-Vector3.forward*25+Vector3.up*5;proximity.Tick(rider,.5f);Require(proximity.EnemyNearby,"Approaching enemy behind rider was ignored");
        enemy.SetEncounterRole(true,100);var queued=Fresh();queued.Tick(rider,1);Require(!queued.EnemyNearby,"Queued enemy triggered combat view");enemy.SetEncounterRole(false,20);
        enemy.transform.position=rider+Vector3.forward*500;
        var probeRoot=new GameObject("Inactive camera detection probe");probeRoot.SetActive(false);var probe=probeRoot.AddComponent<Enemy>();var registry=(System.Collections.Generic.List<Enemy>)Enemy.Instances;if(!registry.Contains(probe))registry.Add(probe);
        typeof(Enemy).GetField("vitality",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(probe,new Health(100));probe.transform.position=rider+Vector3.forward*2;
        try{var inactive=Fresh();inactive.Tick(rider,1);Require(!inactive.EnemyNearby,"Inactive enemy triggered combat view");}finally{registry.Remove(probe);UnityEngine.Object.DestroyImmediate(probeRoot);}
        enemy.transform.position=rider+Vector3.forward*25;
        var field=typeof(Enemy).GetField("vitality",BindingFlags.Instance|BindingFlags.NonPublic);var alive=field.GetValue(enemy);var dead=new Health(1);dead.TakeDamage(2);field.SetValue(enemy,dead);try{var killed=Fresh();killed.Tick(rider,1);Require(!killed.EnemyNearby,"Dead enemy triggered combat view");}finally{field.SetValue(enemy,alive);}
        enemy.transform.position=original;enemy.GetComponent<Rigidbody>().position=original;
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;stage=SessionState.GetInt(Key+"Stage",0);EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Report(string text)=>File.AppendAllText("Logs/CameraLab/Result.txt",text+"\n");
    private static void Next(int value){stage=value;SessionState.SetInt(Key+"Stage",stage);at=EditorApplication.timeSinceStartup;}
    private static void Tick(){if(finished)return;try{
        Require(EditorApplication.timeSinceStartup-began<150,"Camera Lab timed out at "+stage+": "+CameraLabPreview.Status);
        if(stage==40 && !EditorApplication.isPlaying && !CameraLabPreview.Active){Require(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)=="Assets/Level/Scenes/Menu.unity" && Mathf.Abs(AudioListener.volume-.37f)<.001f,"Scene/audio not restored");Require(CameraLabPreview.View==null,"Render texture not released");Finish(true,"PASS all Camera Lab checks");return;}
        if(!CameraLabPreview.Ready)return;
        if(stage==0){
            window=EditorWindow.GetWindow<CameraLabWindow>();window.position=new Rect(30,30,1200,800);tuning=window.Draft;
            EditorApplication.isPaused=true;CameraLabPreview.Apply(tuning);CameraLabPreview.RefreshCamera();var camera=CameraLabPreview.Camera;float original=tuning.Get(S.Rig,"ridingFov").number;
            window.Zoom(8,true);Require(Mathf.Abs(camera.fieldOfView-(original+8))<.1f,"Paused FOV edit did not update live camera");
            var position=camera.transform.position;window.Pan(Vector3.up*.5f);CameraLabPreview.RefreshCamera();Require(camera.transform.position.y-position.y>.35f,"Paused pan did not move camera");
            window.Rotate(new Vector2(4,3));CameraLabPreview.RefreshCamera();
            // Dispatch actual preview mouse events as well as testing the tuning operations.
            var mouse=window.PreviewRect.center;Require(window.PreviewRect.width>50,"Live image has no interactive area");
            float wheelBefore=tuning.Get(S.Rig,"targetOffset").vector.z;window.SendEvent(new Event{type=EventType.ScrollWheel,mousePosition=mouse,delta=new Vector2(0,2)});Require(tuning.Get(S.Rig,"targetOffset").vector.z>wheelBefore+.4f,"Wheel did not change camera distance");
            var panBefore=tuning.Get(S.Rig,"targetOffset").vector;window.SendEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=mouse});window.SendEvent(new Event{type=EventType.MouseDrag,button=0,mousePosition=mouse+new Vector2(12,-12),delta=new Vector2(12,-12)});window.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=mouse+new Vector2(12,-12)});Require(Vector3.Distance(panBefore,tuning.Get(S.Rig,"targetOffset").vector)>.1f,"Mouse drag did not pan camera");
            Capture("Riding");Report("PASS paused live pan, rotation and FOV controls; real preview mouse wheel and drag events.");
            CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.CombatRiding);Next(50);
        }else if(stage==50){
            var regular=tuning.Get(S.Rig,"targetOffset").vector;float normalFov=tuning.Get(S.Rig,"ridingFov").number;
            tuning.Get(S.Rig,"combatRiding.fov").number=55;tuning.Get(S.Rig,"combatRiding.offset").vector=new Vector3(1,-2.5f,8);tuning.Get(S.Rig,"combatRiding.returnDelay").number=.25f;tuning.Get(S.Rig,"combatRiding.blendTime").number=.35f;window.Changed();
            window.Pan(Vector3.up*.3f);window.Rotate(new Vector2(3,2));window.Zoom(4,true);CameraLabPreview.RefreshCamera();
            Require(Mathf.Abs(CameraLabPreview.Camera.fieldOfView-59)<.1f,"Combat view edits did not reach the camera");Require(tuning.Get(S.Rig,"targetOffset").vector==regular && tuning.Get(S.Rig,"ridingFov").number==normalFov,"Combat view editing changed cruising settings");
            CheckProximity();Capture("CombatRiding");Report("PASS independent combat view pan/rotation/FOV; planar enemy radius, exit padding, delayed return, smooth blend, pause and dead/inactive/queued filtering.");
            CameraLabPreview.EnemyPresent=false;CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.AutoRiding);EditorApplication.isPaused=false;Next(51);
        }else if(stage==51 && EditorApplication.timeSinceStartup-at>1.5f){
            Require(!CameraLabPreview.Rig.CombatEnemyNearby && CameraLabPreview.Rig.CombatCameraAmount<.01f,"Automatic camera did not return to cruising when enemies disappeared");
            CameraLabPreview.EnemyPresent=true;CameraLabPreview.EnemyDistance=25;Next(52);
        }else if(stage==52 && EditorApplication.timeSinceStartup-at>1){
            Require(CameraLabPreview.Rig.CombatEnemyNearby && CameraLabPreview.Rig.CombatCameraAmount>.99f,"Automatic combat view did not activate with a real nearby enemy");Capture("AutoCombat");CameraLabPreview.EnemyDistance=190;Next(53);
        }else if(stage==53 && EditorApplication.timeSinceStartup-at>1.5f){
            Require(!CameraLabPreview.Rig.CombatEnemyNearby && CameraLabPreview.Rig.CombatCameraAmount<.01f,"Far-away enemy incorrectly kept combat camera active");Capture("AutoCruising");Report("PASS automatic real-camera transitions for enemy arrival, disappearance and range exit.");
            EditorApplication.isPaused=true;CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.CruisingAimLeft);Next(60);
        }else if(stage==60){
            tuning.Get(S.Rig,"aimFov").number=61;tuning.Get(S.Rig,"aimOffset").vector=new Vector3(-1.5f,-2.8f,8.5f);tuning.Get(S.Rig,"combatAim.fov").number=44;tuning.Get(S.Rig,"combatAim.offset").vector=new Vector3(-2.5f,-2.3f,7);tuning.Get(S.Rig,"aimFraming.enabled").number=0;window.Changed();
            var fighting=tuning.Get(S.Rig,"combatAim.offset").vector;var riding=tuning.Get(S.Rig,"targetOffset").vector;
            window.Pan(Vector3.up*.2f);window.Rotate(new Vector2(2,1));window.Zoom(2,true);CameraLabPreview.RefreshCamera();
            Require(Mathf.Abs(CameraLabPreview.Camera.fieldOfView-63)<.1f && CameraLabPreview.Rig.CombatCameraAmount==0,"Cruising aiming preview did not use cruising FOV");Require(tuning.Get(S.Rig,"combatAim.offset").vector==fighting && tuning.Get(S.Rig,"targetOffset").vector==riding,"Cruising aim drag changed another camera");Capture("CruisingAimLeft");
            left=CameraLabPreview.Camera.transform.position-CameraLabPreview.Player.transform.position;CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.CruisingAimRight);CameraLabPreview.RefreshCamera();Require((CameraLabPreview.Camera.transform.position-CameraLabPreview.Player.transform.position).x-left.x>1,"Cruising shoulder previews are not separate");Capture("CruisingAimRight");
            var cruising=tuning.Get(S.Rig,"aimOffset").vector;CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.CombatAimLeft);window.Pan(Vector3.up*.2f);window.Rotate(new Vector2(2,1));window.Zoom(3,true);CameraLabPreview.RefreshCamera();
            Require(Mathf.Abs(CameraLabPreview.Camera.fieldOfView-47)<.1f && CameraLabPreview.Rig.CombatCameraAmount==1,"Combat aiming preview did not use combat FOV");Require(tuning.Get(S.Rig,"aimOffset").vector==cruising && tuning.Get(S.Rig,"aimFov").number==63,"Combat aim edits changed cruising aim");Capture("CombatAimLeft");
            left=CameraLabPreview.Camera.transform.position-CameraLabPreview.Player.transform.position;CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.CombatAimRight);CameraLabPreview.RefreshCamera();Require((CameraLabPreview.Camera.transform.position-CameraLabPreview.Player.transform.position).x-left.x>1,"Combat shoulder previews are not separate");Capture("CombatAimRight");Report("PASS independent cruising/combat aim pan, rotation and FOV; both left/right shoulder previews.");
            CameraLabPreview.EnemyPresent=false;CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.AutoAim);EditorApplication.isPaused=false;Next(61);
        }else if(stage==61 && EditorApplication.timeSinceStartup-at>1.6f){
            Require(CameraLabPreview.Rig.CombatCameraAmount<.01f && CameraLabPreview.Camera.fieldOfView>56,"Automatic aiming did not return to cruising view");CameraLabPreview.EnemyPresent=true;CameraLabPreview.EnemyDistance=25;Next(62);
        }else if(stage==62 && EditorApplication.timeSinceStartup-at>1.2f){
            Require(CameraLabPreview.Rig.CombatCameraAmount>.99f && CameraLabPreview.Camera.fieldOfView<56,"Automatic aiming did not blend into combat view");Capture("AutoCombatAim");CameraLabPreview.EnemyPresent=false;Next(63);
        }else if(stage==63 && EditorApplication.timeSinceStartup-at>1.6f){
            Require(CameraLabPreview.Rig.CombatCameraAmount<.01f && CameraLabPreview.Camera.fieldOfView>56,"Automatic aiming did not restore cruising after enemy departure");Capture("AutoCruisingAim");Report("PASS automatic aiming transitions, separate FOVs and cruising restoration after enemy departure.");CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.CombatAimLeft);EditorApplication.isPaused=false;Next(1);
        }else if(stage==1 && EditorApplication.timeSinceStartup-at>1){EditorApplication.isPaused=true;CameraLabPreview.RefreshCamera();left=CameraLabPreview.Camera.transform.position-CameraLabPreview.Player.transform.position;Capture("AimLeft");CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.CombatAimRight);EditorApplication.isPaused=false;Next(2);
        }else if(stage==2 && EditorApplication.timeSinceStartup-at>1){EditorApplication.isPaused=true;CameraLabPreview.RefreshCamera();var right=CameraLabPreview.Camera.transform.position-CameraLabPreview.Player.transform.position;Require(right.x-left.x>1,"Shoulder views are not distinct");Capture("AimRight");Report("PASS left/right aim shoulders.");CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.Airborne);window.Pan(Vector3.up*.2f);CameraLabPreview.RefreshCamera();Require(!CameraLabPreview.Player.grounded,"Airborne mode failed");Capture("Airborne");CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.Launch);EditorApplication.isPaused=false;Next(3);
        }else if(stage==3 && CameraLabPreview.Player.GetComponent<BikeLaunchEffects>().Amount>.8f){
            EditorApplication.isPaused=true;var baseline=tuning.Get(S.Rig,"targetOffset").vector;window.Pan(Vector3.right*.7f);Require(tuning.Get(S.Rig,"targetOffset").vector==baseline,"Launch pan changed riding view");Capture("Launch");Report("PASS airborne and independent launch view.");CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.Finisher);Next(10);
        }else if(stage==10 && CameraLabPreview.Mode==CameraLabPreview.ViewMode.Finisher && FinisherLabPreview.Effects.CameraAmount>.3f){EditorApplication.isPaused=true;window=EditorWindow.GetWindow<CameraLabWindow>();window.Pan(Vector3.up*.2f);Capture("Finisher");Report("PASS real finisher preview and live camera edits.");CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.Dodge);Next(20);
        }else if(stage==20 && CameraLabPreview.Mode==CameraLabPreview.ViewMode.Dodge && DodgeLabPreview.Effects.CameraAmount>.8f){EditorApplication.isPaused=true;window=EditorWindow.GetWindow<CameraLabWindow>();window.Rotate(new Vector2(2,2));Capture("Dodge");Report("PASS real passing-bullet dodge preview and live camera edits.");Next(40);CameraLabPreview.Stop();}
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void Capture(string name){CameraLabPreview.RefreshCamera();var texture=CameraLabPreview.View;if(texture==null)return;var old=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes("Logs/CameraLab/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
    private static void Finish(bool success,string message){if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Report(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
