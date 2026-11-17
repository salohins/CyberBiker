using System;
using System.IO;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using P=CyberBiker.EditorTools.GapDashLabPreview;

[InitializeOnLoad]
public static class GapDashChecks {
    private const string Key="CyberBiker.GapDashChecks";
    private static int stage;private static double began;private static bool finished;
    private static GapDashLabWindow window;
    private static float health;
    static GapDashChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        Directory.CreateDirectory("Logs/GapDash");File.WriteAllText("Logs/GapDash/Result.txt","RUNNING\n");
        try{
            var profile=AssetDatabase.LoadAssetAtPath<GapDashProfile>(GapDashLabWindow.ProfilePath);Require(profile!=null && profile.windMaterial!=null && profile.whoosh!=null,"Gap dash assets are not assigned");
            var source=File.ReadAllBytes(GapDashLabWindow.ProfilePath);SessionState.SetString(Key+"Original",Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(source)));
            var settings=GapDashLabWindow.ReadSaved();var roundtrip=JsonUtility.FromJson<GapDashSettings>(JsonUtility.ToJson(settings));roundtrip.Normalize();Require(roundtrip.snapRadius==settings.snapRadius,"Preset roundtrip changed settings");
            string folder="Assets/__GapDashChecks",path=folder+"/Tuning.asset";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","__GapDashChecks");AssetDatabase.CopyAsset(GapDashLabWindow.ProfilePath,path);roundtrip.snapRadius=2;GapDashLabWindow.SaveForGame(roundtrip,path);var copy=AssetDatabase.LoadAssetAtPath<GapDashProfile>(path);Require(copy.settings.snapRadius==2 && copy.windMaterial==profile.windMaterial && copy.whoosh==profile.whoosh,"Save did not persist tuning or changed assigned effects");AssetDatabase.DeleteAsset(folder);Report("PASS preset roundtrip and isolated Save for game preserve material/audio references.");
            EditorSceneManager.OpenScene("Assets/Level/Scenes/Menu.unity");EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Level/Scenes/Menu.unity");AudioListener.volume=.37f;
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Stage",0);GapDashLabWindow.Open();P.Current=P.Scenario.OpenGap;P.Offset=.75f;P.Speed=65;P.Spacing=6.06f;P.Steering=0;P.Aiming=false;P.LaunchMode=true;P.Loop=false;P.Sound=false;P.OrbitView=false;P.Start(settings);
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;stage=SessionState.GetInt(Key+"Stage",0);EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Require(bool valid,string text){if(!valid)throw new Exception(text);}
    private static void Report(string text)=>File.AppendAllText("Logs/GapDash/Result.txt",text+"\n");
    private static void Next(int value){stage=value;SessionState.SetInt(Key+"Stage",value);}
    private static void Tick(){if(finished)return;try{
        Require(EditorApplication.timeSinceStartup-began<170,"Gap dash timed out at "+stage+": "+P.Status);
        if(stage==40 && !EditorApplication.isPlaying && !P.Active){
            Require(P.View==null && Mathf.Abs(AudioListener.volume-.37f)<.001f && AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)=="Assets/Level/Scenes/Menu.unity","Lab did not restore scene/audio/texture");
            var hash=Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(GapDashLabWindow.ProfilePath)));Require(hash==SessionState.GetString(Key+"Original",""),"Preview changed the saved tuning asset");Finish(true,"PASS all Gap Dash checks.");return;
        }
        if(!P.Ready)return;
        if(stage==0){window=EditorWindow.GetWindow<GapDashLabWindow>();window.position=new Rect(30,30,1250,850);var defaults=GapDashLabWindow.ReadSaved();JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults),window.Draft);window.Changed();health=P.Player.CurrentHealth;
            P.Player.gameObject.AddComponent<GapDashContactProbe>();
            foreach(var c in P.Player.GetComponentsInChildren<Collider>(true))if(c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy)Report("Body shape "+c.name+" "+c.GetType().Name+": "+c.bounds);
            P.Replay();Next(1);}
        else if(stage==1){
            Require(P.Clock<1.3f,"Open gap did not trigger: grounded="+P.Player.grounded+", velocity="+P.Player.GetComponent<Rigidbody>().linearVelocity+", "+P.Status);
            if(!P.Assist.IsActive || P.Assist.Age<.06f || P.Assist.Wind==null || P.Assist.Wind.particleCount==0)return;
            Require(P.Assist.DashCount==1 && P.Player.transform.position.x<.75f && P.Player.transform.position.x>=P.Assist.TargetX-.05f,"Snap did not smoothly approach the gap centre");Require(Mathf.Abs(P.Player.GetComponent<Rigidbody>().linearVelocity.z-P.Speed)<1,"Gap assist changed the riding speed");
            EditorApplication.isPaused=true;Next(11);
        }else if(stage==11 && EditorApplication.isPaused){
            P.Refresh();float fov=Camera.main.fieldOfView;float before=window.Draft.zoomInFov;window.Draft.zoomInFov=before<10 ? before+4 : before-4;window.Changed();float change=window.Draft.zoomInFov-before;Require(Mathf.Abs(Camera.main.fieldOfView-fov+change*P.Assist.CameraAmount)<.2f,"Paused camera FOV tuning did not update: "+fov+" -> "+Camera.main.fieldOfView+" at "+P.Assist.CameraAmount);
            P.Refresh();float stable=Camera.main.fieldOfView;P.Refresh();Require(Mathf.Abs(Camera.main.fieldOfView-stable)<.05f,"Gap camera accumulates between paused refreshes");
            float density=window.Draft.emission;window.Draft.emission=0;window.Changed();Require(P.Assist.Wind.particleCount==0,"Paused particle density edit did not rebuild the preview");window.Draft.emission=density;window.Changed();Require(P.Assist.Wind.particleCount>0,"Paused particle density restore did not rebuild the preview");
            Require(P.Assist.Wind!=null && P.Assist.Wind.particleCount>0,"Gap wind particles are not visible; emitted="+P.Assist.EmittedParticles+", age="+P.Assist.Age);Capture("GapDash");P.OrbitView=true;P.Refresh();Capture("Orbit");P.OrbitView=false;
            Report("PASS real gap detection, smooth lateral alignment at unchanged riding speed, live wind emission and paused camera tuning without accumulation.");EditorApplication.isPaused=false;Next(2);
        }else if(stage==2 && P.Clock>1.4f){
            Require(P.Assist.CompletedCount==1 && P.Assist.CancelCount==0,"Gap dash did not finish safely: "+P.Status);Require(P.Gameplay.StyleScore.Combo.BasePoints>=P.Gameplay.StyleScoring.gapPass,"Completed gap must award its style score");Require(P.Player.CurrentHealth==health && P.Gameplay.gameState==Gameplay.GameState.Playing,"Gap passage damaged or crashed the player");Require(Mathf.Abs(P.Player.GetComponent<Rigidbody>().linearVelocity.z-P.Speed)<1,"Normal forward speed was not retained");Report("PASS actual solid-car passage completes without damage and retains normal speed.");P.Current=P.Scenario.NarrowGap;P.Replay();Next(3);
        }else if(stage==3 && P.Clock>.27f){Require(P.Assist.DashCount==0,"Too-narrow gap triggered assist");Report("PASS narrow gap rejected.");P.Current=P.Scenario.BlockedExit;P.Replay();Next(4);}
        else if(stage==4 && P.Clock>.27f){Require(P.Assist.DashCount==0,"Third car blocking the exit was ignored");Report("PASS blocked exit rejected.");P.Current=P.Scenario.SingleCar;P.Replay();Next(5);}
        else if(stage==5 && P.Clock>.27f){Require(P.Assist.DashCount==0,"Single car triggered gap dash");P.Current=P.Scenario.OpenGap;P.Offset=6.5f;P.Replay();Next(6);}
        else if(stage==6 && P.Clock>.27f){Require(P.Assist.DashCount==0,"Distant gap stole steering");Report("PASS single car and gap outside snap radius rejected.");P.Offset=.75f;P.Replay();Next(7);}
        else if(stage==7 && P.Assist.IsActive){P.Steering=1;Next(8);}
        else if(stage==8 && !P.Assist.IsActive && P.Assist.CompletedCount==1){Require(P.Assist.CancelCount==0,"Opposing input cancelled the committed gap pass");Report("PASS opposing steering stays locked until the bike fully clears the gap.");P.Steering=0;P.Offset=-.75f;P.Replay();Next(9);}
        else if(stage==9 && P.Clock>1.2f){Require(P.Assist.CompletedCount==1,"Opposite-side approach did not pass safely: "+P.Status);Report("PASS gap alignment works from both sides.");P.SeekEffect(.18f);Require(!P.Assist.IsActive,"Effect scrub should not start physical movement");window.Draft.cameraMotion=false;P.Apply(window.Draft);P.SeekEffect(.18f);Require(P.Assist.Wind.particleCount>0,"Disabling camera motion incorrectly hides VFX preview");P.Refresh();Capture("EffectPreview");Report("PASS paused camera/VFX timeline preview, with independent camera and particle controls.");Next(40);P.Stop();}
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void Capture(string label){P.Refresh();var texture=P.View;var old=RenderTexture.active;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);try{RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes("Logs/GapDash/"+label+".png",image.EncodeToPNG());}finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);}}
    private static void Finish(bool success,string text){if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Report(text);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}

public sealed class GapDashContactProbe:MonoBehaviour {
    private void OnCollisionEnter(Collision collision){
        if(collision.collider.GetComponentInParent<Car>()==null)return;
        File.AppendAllText("Logs/GapDash/Result.txt","CONTACT "+collision.collider.name+" at player "+transform.position+" (gap clock "+P.Clock+")\n");
        for(int i=0;i<collision.contactCount;i++){var p=collision.GetContact(i);File.AppendAllText("Logs/GapDash/Result.txt","  own "+p.thisCollider.name+" "+p.thisCollider.GetType().Name+" point "+p.point+" normal "+p.normal+"\n");}
    }
}
