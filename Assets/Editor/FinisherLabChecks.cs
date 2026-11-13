using System;
using System.IO;
using System.Linq;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class FinisherLabChecks {
    private const string Key="CyberBiker.FinisherLabChecks.";
    private static int stage;
    private static double began,stepAt,heartbeat;
    private static int frame;
    private static PlayerController previousPlayer;
    private static bool finishing;
    static FinisherLabChecks(){if(SessionState.GetBool(Key+"Pending",false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static float Value(UnityEngine.Object owner,string field)=>new SerializedObject(owner).FindProperty(field).floatValue;
    public static void Run() {
        try {
            Directory.CreateDirectory("Logs/FinisherLab");
            var saved=FinisherLabTuning.ReadSavedDefaults();
            Require(Mathf.Abs(saved.Get("strikeBlendTime").number-.06f)<.001f,"New strike blend must preserve old timing");
            Require(Mathf.Abs(saved.Get("sheatheBlendTime").number-.12f)<.001f,"New sheathe blend must preserve old timing");
            var draft=saved.Copy();draft.Get("drawPlaybackSpeed").number=1.6f;draft.Get("strikePlaybackSpeed").number=.7f;
            draft.Get("sliceReturnPlaybackSpeed").number=1.8f;draft.Get("strikeBlendTime").number=.14f;
            draft.Get("sheatheBlendTime").number=.22f;draft.Normalize();
            File.WriteAllText("Logs/FinisherLab/preset.json",JsonUtility.ToJson(draft,true));
            var imported=JsonUtility.FromJson<FinisherLabTuning>(File.ReadAllText("Logs/FinisherLab/preset.json"));imported.Normalize();
            Require(imported.values.Count==FinisherLabTuning.Controls.Length,"Every exposed control must survive preset roundtrip");
            string fixture="Assets/Editor/FinisherLabSaveFixture.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(fixture)==null)Require(AssetDatabase.CopyAsset(FinisherLabTuning.PlayerPrefab,fixture),"Could not create isolated save fixture");
            var before=AssetDatabase.LoadAssetAtPath<GameObject>(fixture);
            string untouched=JsonUtility.ToJson(new Snapshot{speed=before.GetComponent<PlayerController>().ConfiguredMaxSpeed,clips=ClipIds(before)});
            imported.SaveForGame(fixture);
            var after=AssetDatabase.LoadAssetAtPath<GameObject>(fixture);
            Require(Mathf.Abs(Value(after.GetComponent<BikeSwordCombat>(),"strikeBlendTime")-.14f)<.001f,"Save must persist the selected animation blend");
            Require(untouched==JsonUtility.ToJson(new Snapshot{speed=after.GetComponent<PlayerController>().ConfiguredMaxSpeed,clips=ClipIds(after)}),"Save must preserve movement and animation references");
            Require(Mathf.Abs(FinisherLabTuning.ReadSavedDefaults().Get("strikeBlendTime").number-.06f)<.001f,"Fixture save must not change the real player prefab");
            var invalid=draft.Copy();invalid.Get("strikePlaybackSpeed").number=float.NaN;bool rejected=false;
            try{invalid.Normalize();}catch(ArgumentException){rejected=true;}Require(rejected,"Invalid presets must be rejected");
            var contact=draft.Copy();contact.Get("strikeContactTime").number=.8f;contact.Get("strikeWindowEnd").number=.5f;contact.Normalize();
            Require(contact.Get("strikeWindowEnd").number>=contact.Get("strikeContactTime").number,"Contact window must remain valid");
            EditorSceneManager.OpenScene("Assets/Level/Scenes/Menu.unity");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Level/Scenes/Menu.unity");
            SessionState.SetString(Key+"SceneHash",Hash("Assets/Level/Scenes/Level.unity"));
            SessionState.SetBool(Key+"Pending",true);SessionState.SetInt(Key+"Stage",1);
            FinisherLabPreview.Left=true;FinisherLabPreview.Loop=false;FinisherLabPreview.PauseOnContact=true;FinisherLabPreview.Sound=false;
            FinisherLabWindow.Open();EditorWindow.GetWindow<FinisherLabWindow>().UseTuning(draft);
            FinisherLabPreview.Start(draft);
        } catch(Exception error){Debug.LogError(error);EditorApplication.Exit(1);}
    }
    [Serializable] private sealed class Snapshot {public float speed;public string clips;}
    private static string ClipIds(GameObject root) {
        var data=new SerializedObject(root.GetComponent<BikeSwordCombat>());
        return string.Join("|",new[]{"draw","sheathe","idle","idleLeft","idleRight","attackLeft","attackRight"}.Select(field=>AssetDatabase.GetAssetPath(data.FindProperty(field).objectReferenceValue)));
    }
    private static string Hash(string path) {using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));}
    private static void Attach(){stage=SessionState.GetInt(Key+"Stage",1);began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Next(){stage++;SessionState.SetInt(Key+"Stage",stage);File.AppendAllText("Logs/FinisherLab/progress.txt","Stage "+stage+"\n");}
    private static void Capture(string name) {
        var target=FinisherLabPreview.View;Require(target!=null && target.width==1280 && target.height==720,"Expected a live HD preview target");
        Camera.main.Render();var active=RenderTexture.active;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        try {
            if(EditorApplication.timeSinceStartup-heartbeat>2) {
                heartbeat=EditorApplication.timeSinceStartup;
                File.AppendAllText("Logs/FinisherLab/progress.txt","Tick stage="+stage+" play="+EditorApplication.isPlaying+" paused="+EditorApplication.isPaused+" active="+FinisherLabPreview.Active+" ready="+FinisherLabPreview.Ready+"\n");
            }
            RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            var pixels=texture.GetPixels32();Require(pixels.Any(p=>p.b>100) && pixels.Any(p=>p.r>100),"Preview must show rendered scene and particles");
            File.WriteAllBytes("Logs/FinisherLab/"+name+".png",texture.EncodeToPNG());
        } finally {RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(texture);}
    }
    private static void Tick() {
        if(finishing || EditorApplication.isCompiling)return;
        try {
            if(EditorApplication.timeSinceStartup-began>90)throw new Exception("Finisher Lab timed out at stage "+stage+": "+FinisherLabPreview.Status);
            if(stage==1 && FinisherLabPreview.Ready && FinisherLabPreview.Sword.HitCount>0 && EditorApplication.isPaused) {
                Require(FinisherLabPreview.Sword.LastAttackSide==BikeSwordCombat.AttackSide.Left,"Expected left attack");
                Require(FinisherLabPreview.Effects.ImpactCount==1 && FinisherLabPreview.Effects.LastImpactParticleCount>0,"Expected actual impact particles");
                Require(Mathf.Abs(Value(FinisherLabPreview.Sword,"drawPlaybackSpeed")-1.6f)<.001f,"Preview must use edited animation speeds");
                Capture("LeftImpact");
                var draft=FinisherLabTuning.Capture(FinisherLabPreview.Sword,FinisherLabPreview.Effects);draft.Get("hitZoom").number=-12;FinisherLabPreview.Apply(draft);
                Require(Mathf.Abs(Value(FinisherLabPreview.Effects,"hitZoom")+12)<.001f,"Live camera edit must apply immediately");
                frame=Time.frameCount;stepAt=EditorApplication.timeSinceStartup;FinisherLabPreview.Step();Next();
            } else if(stage==2 && EditorApplication.timeSinceStartup-stepAt>.3) {
                Require(Time.frameCount>frame && EditorApplication.isPaused,"Step must advance while retaining pause");
                FinisherLabPreview.Left=false;FinisherLabPreview.Replay();Next();
            } else if(stage==3 && FinisherLabPreview.Ready && FinisherLabPreview.Sword.HitCount>0 && EditorApplication.isPaused) {
                Require(FinisherLabPreview.Sword.LastAttackSide==BikeSwordCombat.AttackSide.Right,"Expected right attack");
                Require(FinisherLabPreview.Effects.ImpactCount==1,"Replay must clear the previous impact");
                Require(Mathf.Abs(Value(FinisherLabPreview.Effects,"hitZoom")+12)<.001f,"Replay must retain the edited tuning");
                Capture("RightImpact");previousPlayer=FinisherLabPreview.Player;
                FinisherLabPreview.Loop=true;FinisherLabPreview.PauseOnContact=false;EditorApplication.isPaused=false;Next();
            } else if(stage==4 && FinisherLabPreview.Ready && FinisherLabPreview.Player!=previousPlayer) {
                FinisherLabPreview.Loop=false;Next();FinisherLabPreview.Stop();
            } else if(stage==5 && !EditorApplication.isPlaying && !FinisherLabPreview.Active) {
                Require(SceneManager.GetActiveScene().path=="Assets/Level/Scenes/Menu.unity","Stopping must restore the original open scene");
                Require(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)=="Assets/Level/Scenes/Menu.unity","Stopping must restore the previous Play start scene");
                Require(Hash("Assets/Level/Scenes/Level.unity")==SessionState.GetString(Key+"SceneHash",""),"Preview must not rewrite the gameplay scene");
                Finish(true,"PASS: isolated saved-scene preview; real left/right draw, dash, slice, particles and explosion; pause on impact; one-frame stepping; live camera/animation tuning; replay clears effects and retains edits; automatic looping; preset roundtrip and input validation; safe tuning-only prefab save on a copy; original scene and Play-start scene restored; gameplay scene unchanged. Native preview captures saved.");
            }
        } catch(Exception error){Finish(false,error.ToString());}
    }
    private static void Finish(bool success,string message) {
        if(finishing)return;finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key+"Pending",false);
        Directory.CreateDirectory("Logs/FinisherLab");File.WriteAllText("Logs/FinisherLab/result.txt",message);Debug.Log(message);
        EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);
    }
}
