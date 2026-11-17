using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;
using P=CyberBiker.EditorTools.FinisherLabPreview;

[InitializeOnLoad]
public static class FinisherDashCameraChecks {
    private const string Key="CyberBiker.FinisherDashCameraChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int stage;
    private static bool done,sawReturn;
    private static double began,nextTrace;
    private static FinisherLabTuning draft;
    static FinisherDashCameraChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Near(float a,float b,string message){Require(Mathf.Abs(a-b)<.002f,message+": "+a+" expected "+b);}
    private static void VectorNear(Vector3 a,Vector3 b,string message){Require(Vector3.Distance(a,b)<.002f,message+": "+a+" expected "+b);}
    private static void Report(string message)=>File.AppendAllText("Logs/FinisherDashCamera/Result.txt","PASS "+message+"\n");
    private static void Value(string name,float value)=>draft.Get(name).number=value;
    private static FinisherLabTuning TestTuning(){
        var t=FinisherLabTuning.ReadSavedDefaults();
        foreach(var pair in new[]{("slowMotionEnabled",0f),("drawPlaybackSpeed",.25f),("strikePlaybackSpeed",1f),("dashDuration",.24f),("dashCameraFovOffset",8f),("dashCameraPosition.x",1.1f),("dashCameraPosition.y",.3f),("dashCameraPosition.z",-.7f),("dashCameraRotation.x",4f),("dashCameraRotation.y",7f),("dashCameraRotation.z",9f),("dashCameraTargetTurn",0f)})t.Get(pair.Item1).number=pair.Item2;
        return t;
    }
    public static void Run(){
        Directory.CreateDirectory("Logs/FinisherDashCamera");File.WriteAllText("Logs/FinisherDashCamera/Result.txt","");
        try{
            var original=File.ReadAllBytes(FinisherLabTuning.PlayerPrefab);draft=TestTuning();
            var roundtrip=FinisherLabTuning.ImportJson(JsonUtility.ToJson(draft));
            Require(FinisherLabTuning.Controls.Count(c=>c.group=="Dash camera")==12 && FinisherLabTuning.Controls.Any(c=>c.group=="Swing & impact camera"),"Dedicated camera groups missing");
            foreach(var control in FinisherLabTuning.Controls.Where(c=>c.group=="Dash camera"))Near(roundtrip.Get(control.field).number,draft.Get(control.field).number,"Preset "+control.field);
            var legacy=draft.Copy();legacy.values.RemoveAll(v=>v.field.StartsWith("dashCamera",StringComparison.Ordinal));legacy.Get("cameraSideShift").number=1.6f;legacy.Get("cameraDolly").number=.8f;legacy.Get("cameraRoll").number=4;legacy.Normalize();
            Near(legacy.Get("dashCameraPosition.x").number,.4f,"Legacy side migration");Near(legacy.Get("dashCameraPosition.z").number,-.2f,"Legacy travel migration");Near(legacy.Get("dashCameraRotation.z").number,1,"Legacy roll migration");Near(legacy.Get("cameraRoll").number,4,"Legacy swing preserved");
            legacy.version=1;Require(FinisherLabTuning.ImportJson(JsonUtility.ToJson(legacy)).version==2,"Version 1 import was rejected");
            var future=draft.Copy();future.version=3;bool futureRejected=false;try{FinisherLabTuning.ImportJson(JsonUtility.ToJson(future));}catch(ArgumentException){futureRejected=true;}Require(futureRejected,"Unsupported future version accepted");
            var invalid=draft.Copy();invalid.Get("dashCameraPosition.x").number=float.NaN;bool rejected=false;try{invalid.Normalize();}catch(ArgumentException){rejected=true;}Require(rejected,"Invalid dash camera offset accepted");
            string fixture="Assets/Editor/FinisherDashCameraSaveFixture.prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(fixture)==null)Require(AssetDatabase.CopyAsset(FinisherLabTuning.PlayerPrefab,fixture),"Could not copy isolated save fixture");
            roundtrip.SaveForGame(fixture);var saved=new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(fixture).GetComponent<SwordFinisherEffects>());
            Near(saved.FindProperty("dashCameraPosition").vector3Value.x,1.1f,"Save position");Near(saved.FindProperty("dashCameraRotation").vector3Value.z,9,"Save rotation");Near(saved.FindProperty("dashCameraFovOffset").floatValue,8,"Save FOV");
            Require(original.SequenceEqual(File.ReadAllBytes(FinisherLabTuning.PlayerPrefab)),"Test save modified the production prefab");
            File.WriteAllText("Logs/FinisherDashCamera/Preset.json",JsonUtility.ToJson(roundtrip,true));
            Report("12 independent Dash camera controls; vector axes and preset round trip; old draft migration; invalid input rejected; tuning saved on prefab copy with original untouched.");
            SessionState.SetBool(Key,true);P.Left=true;P.Sound=false;P.Loop=false;P.PauseOnContact=false;P.Start(draft);
        }catch(Exception error){Debug.LogError(error);EditorApplication.Exit(1);}
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    private static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Private).SetValue(owner,value);
    private static void CheckCombatBase(){
        var rig=Camera.main.GetComponent<CameraController>();var settings=new SerializedObject(rig).FindProperty("combatRiding");
        Require(settings.FindPropertyRelative("enabled").boolValue,"Combat camera fixture must be enabled");
        Near(rig.CombatCameraAmount,1,"Preview must remain fully in combat camera");Require(rig.CombatEnemyNearby,"Combat view must remain active after target death");
        Near((float)typeof(CameraController).GetField("fov",Private).GetValue(rig),settings.FindPropertyRelative("fov").floatValue,"Saved combat base FOV");
        Near((float)typeof(CameraController).GetField("cameraXangle",Private).GetValue(rig),settings.FindPropertyRelative("pitch").floatValue,"Saved combat base pitch");
    }
    private static void AdvanceFx(int frames){var update=typeof(SwordFinisherEffects).GetMethod("Update",Private);for(int i=0;i<frames;i++)update.Invoke(P.Effects,null);}
    private static void Capture(string name){var rt=P.View;Camera.main.Render();var old=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes("Logs/FinisherDashCamera/"+name+".png",image.EncodeToPNG());}finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);}}
    private static void Tick(){
        if(done || !P.Ready)return;
        try{
            Require(EditorApplication.timeSinceStartup-began<100,"Dash camera checks timed out at "+stage+": "+P.Status);
            CheckCombatBase();
            if(stage==0 && P.Sword.Phase==BikeSwordCombat.CombatPhase.Drawing && P.Effects.DashCameraAmount>.999f){
                EditorApplication.isPaused=true;FinisherClock.EditorCaptureStep=1f/60;
                var fx=P.Effects;P.RefreshLiveCamera();Near(fx.LastDashCameraFovOffset,8,"Left dash FOV");VectorNear(fx.LastDashCameraPosition,new Vector3(-1.1f,.3f,-.7f),"Left position mirror");VectorNear(fx.LastDashCameraRotation,new Vector3(4,-7,-9),"Left rotation mirror");
                var camera=Camera.main;float fov=camera.fieldOfView;var position=camera.transform.position;var rotation=camera.transform.rotation;var playerPosition=P.Player.transform.position;int hits=P.Sword.HitCount;
                draft=FinisherLabTuning.Capture(P.Sword,fx);Value("dashCameraFovOffset",12);Value("dashCameraPosition.y",1.3f);Value("dashCameraRotation.x",14);P.Apply(draft);
                Near(camera.fieldOfView-fov,4,"Paused live FOV edit");Require(Vector3.Distance(position,camera.transform.position)>.9f && Quaternion.Angle(rotation,camera.transform.rotation)>9,"Paused live position/rotation edits did not render");
                position=camera.transform.position;rotation=camera.transform.rotation;fov=camera.fieldOfView;
                foreach(var pair in new[]{("swingZoom",20f),("cameraDolly",0f),("cameraSideShift",0f),("cameraRoll",0f),("hitZoom",25f),("hitCameraSideShift",3f)})Value(pair.Item1,pair.Item2);P.Apply(draft);
                VectorNear(camera.transform.position,position,"Swing controls moved dash camera");Near(Quaternion.Angle(camera.transform.rotation,rotation),0,"Swing controls rotated dash camera");Near(camera.fieldOfView,fov,"Impact/swing controls changed dash FOV");
                for(int i=0;i<6;i++)P.RefreshLiveCamera();VectorNear(camera.transform.position,position,"Paused refresh accumulated position");Near(Quaternion.Angle(camera.transform.rotation,rotation),0,"Paused refresh accumulated rotation");Near(camera.fieldOfView,fov,"Paused refresh accumulated FOV");VectorNear(P.Player.transform.position,playerPosition,"Camera edit moved player");Require(P.Sword.HitCount==hits,"Camera edit advanced combat");
                Value("dashCameraMirrorSide",0);P.Apply(draft);VectorNear(fx.LastDashCameraPosition,new Vector3(1.1f,1.3f,-.7f),"Fixed-side position");VectorNear(fx.LastDashCameraRotation,new Vector3(14,7,9),"Fixed rotation");Value("dashCameraMirrorSide",1);P.Apply(draft);Capture("LeftDashLiveEdit");
                Value("dashCameraBlendIn",.15f);P.Apply(draft);Set(fx,"dashCameraBlend",0f);AdvanceFx(6);float quick=fx.DashCameraAmount;
                Value("dashCameraBlendIn",.3f);P.Apply(draft);Set(fx,"dashCameraBlend",0f);AdvanceFx(6);Require(quick>fx.DashCameraAmount+.3f,"Entry timing does not control the transition");
                Value("dashCameraEnabled",0);Value("dashCameraBlendOut",.3f);P.Apply(draft);Set(fx,"dashCameraBlend",1f);AdvanceFx(6);float slowReturn=fx.DashCameraAmount;
                Value("dashCameraBlendOut",.1f);P.Apply(draft);Set(fx,"dashCameraBlend",1f);AdvanceFx(6);Require(slowReturn>fx.DashCameraAmount+.5f,"Exit timing does not control the transition");
                Report("live left dash FOV/position/rotation; mirrored or fixed side; paused edits render without camera drift or gameplay advancement; swing/impact controls independent; entry and exit timing tested at fixed real-time steps.");
                draft=TestTuning();P.Apply(draft);P.Left=false;EditorApplication.isPaused=false;FinisherClock.EditorCaptureStep=0;P.Replay();stage=1;
            }else if(stage==1 && P.Sword.Phase==BikeSwordCombat.CombatPhase.Drawing && P.Effects.DashCameraAmount>.999f){
                EditorApplication.isPaused=true;P.RefreshLiveCamera();VectorNear(P.Effects.LastDashCameraPosition,new Vector3(1.1f,.3f,-.7f),"Right position mirror");VectorNear(P.Effects.LastDashCameraRotation,new Vector3(4,7,9),"Right rotation mirror");Near(P.Effects.LastDashCameraFovOffset,8,"Replay FOV");Capture("RightDash");
                Report("Replay clears left state, retains dash tuning and correctly mirrors the right dash.");Value("drawPlaybackSpeed",1.4f);P.Apply(draft);
                // Native render/readback can be slower than the physics catch-up
                // cap. Keep the animation and approach on the same test clock.
                Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;EditorApplication.isPaused=false;stage=2;
            }else if(stage==2){
                if(EditorApplication.timeSinceStartup>nextTrace){nextTrace=EditorApplication.timeSinceStartup+1;File.AppendAllText("Logs/FinisherDashCamera/Progress.txt","phase="+P.Sword.Phase+" hits="+P.Sword.HitCount+" dash="+P.Sword.IsDashing+" target="+(P.Sword.Target!=null ? (P.Sword.Target.transform.position-P.Player.transform.position).ToString() : "none")+" camera="+P.Effects.DashCameraAmount+"\n");}
                if(!P.Sword.IsDashing && P.Sword.Phase!=BikeSwordCombat.CombatPhase.Drawing && P.Effects.DashCameraAmount>.02f && P.Effects.DashCameraAmount<.98f)sawReturn=true;
                if(P.Sword.HitCount>0 && P.Effects.CarriedExplosionCount>0 && !P.Sword.IsActive && P.Effects.DashCameraAmount<.001f){
                    Require(sawReturn && P.Effects.ImpactCount==1,"Dash did not blend into the actual swing/impact/explosion");Report("dash blends out through the actual swing and kill; impact and carried explosion still play; dash camera fully releases afterward. Saved combat base FOV/pitch and fully active combat view remain stable throughout both replays and after target death.");Finish(true,"PASS independent finisher dash camera checks.");
                }
            }
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Finish(bool success,string text){if(done)return;done=true;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);FinisherClock.EditorCaptureStep=0;Time.captureDeltaTime=0;File.AppendAllText("Logs/FinisherDashCamera/Result.txt",text+"\n");Debug.Log(text);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
