using System;
using System.IO;
using System.Reflection;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NeutralAimChecks {
    private const string Key="CyberBiker.NeutralAimChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int stage;
    private static double began,at;
    private static bool finished;
    private static CameraLabTuning tuning;
    private static Quaternion levelHead;
    private static Enemy probe;
    private static bool moveProbe;
    static NeutralAimChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){Directory.CreateDirectory("Logs/NeutralAim");File.WriteAllText("Logs/NeutralAim/Result.txt","RUNNING\n");SessionState.SetBool(Key,true);CameraLabPreview.Start(CameraLabTuning.ReadSavedDefaults(),CameraLabPreview.ViewMode.CruisingAim);}
    private static void Attach(){began=at=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Report(string text)=>File.AppendAllText("Logs/NeutralAim/Result.txt",text+"\n");
    private static void Tick(){if(finished)return;try{
        if(EditorApplication.timeSinceStartup-began>75)throw new Exception("Neutral aim timed out at "+stage);
        if(!CameraLabPreview.Ready)return;
        if(moveProbe && probe!=null){probe.transform.position=CameraLabPreview.Player.transform.position+new Vector3(6,5,25);Physics.SyncTransforms();}
        if(stage<5 && !CameraLabPreview.Player.IsPistolVisuallyReady)return;
        if(stage==0){tuning=CameraLabTuning.ReadSavedDefaults();tuning.Get(CameraLabTuning.Scope.Rig,"aimPitch").number=0;tuning.Get(CameraLabTuning.Scope.Rig,"riderVisibility.enabled").number=0;CameraLabPreview.Apply(tuning);stage=1;at=EditorApplication.timeSinceStartup;}
        else if(stage==1 && EditorApplication.timeSinceStartup-at>1){CheckNeutral("LevelCamera");levelHead=Head().rotation;tuning.Get(CameraLabTuning.Scope.Rig,"aimPitch").number=30;CameraLabPreview.Apply(tuning);stage=2;at=EditorApplication.timeSinceStartup;}
        else if(stage==2 && EditorApplication.timeSinceStartup-at>2){
            CheckNeutral("PitchedCamera");Require(Quaternion.Angle(levelHead,Head().rotation)<1,"Camera pitch changed the neutral head pose");Report("PASS level head, barrel, laser/shot direction with level and 30-degree pitched cameras.");
            var root=new GameObject("Neutral aim reacquisition probe");root.transform.position=CameraLabPreview.Player.transform.position+new Vector3(6,5,25);var shape=root.AddComponent<BoxCollider>();shape.center=Vector3.up*2;shape.size=new Vector3(2,3,2);probe=root.AddComponent<Enemy>();probe.enabled=false;typeof(Enemy).GetField("vitality",Private).SetValue(probe,new CyberBiker.Combat.Health(10000));moveProbe=true;stage=3;at=EditorApplication.timeSinceStartup;
        }else if(stage==3 && EditorApplication.timeSinceStartup-at>1.5){
            var aim=CameraLabPreview.Player.GetComponent<BikeAutoAim>();Require(aim.Target==probe,"Neutral look prevented target acquisition");Require(Vector3.Angle(aim.Muzzle.right,aim.AimPoint-aim.Muzzle.position)<6,"Enemy tracking no longer aligns the pistol");Inspect("EnemyTracking");moveProbe=false;probe.gameObject.SetActive(false);stage=4;at=EditorApplication.timeSinceStartup;
        }else if(stage==4 && EditorApplication.timeSinceStartup-at>1.5){
            CheckNeutral("AfterTargetLoss");Report("PASS enemy acquisition/arm alignment and smooth return to forward gaze after losing the target.");CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.Riding);var life=CameraLabPreview.Player.GetComponent<RiderLife>();if(life!=null)life.enabled=false;stage=5;at=EditorApplication.timeSinceStartup;
        }else if(stage==5 && EditorApplication.timeSinceStartup-at>1.3){
            levelHead=Head().rotation;Inspect("CruisingAuthored");
            var aim=CameraLabPreview.Player.GetComponent<BikeAutoAim>();var data=new SerializedObject(aim);data.FindProperty("levelHeadWithoutTarget").boolValue=false;data.ApplyModifiedPropertiesWithoutUndo();stage=6;at=EditorApplication.timeSinceStartup;
        }else if(stage==6 && EditorApplication.timeSinceStartup-at>.7){
            Require(Quaternion.Angle(levelHead,Head().rotation)<.5f,"Aiming head correction still changes ordinary riding");
            var aim=CameraLabPreview.Player.GetComponent<BikeAutoAim>();var data=new SerializedObject(aim);data.FindProperty("levelHeadWithoutTarget").boolValue=true;data.ApplyModifiedPropertiesWithoutUndo();
            CameraLabPreview.SteeringAmount=1;stage=7;at=EditorApplication.timeSinceStartup;
        }else if(stage==7 && EditorApplication.timeSinceStartup-at>1.5){
            var aim=CameraLabPreview.Player.GetComponent<BikeAutoAim>();Require(!(bool)typeof(BikeAutoAim).GetField("hasHeadRenderPose",Private).GetValue(aim),"Normal riding still receives the aiming head override");
            Require(Mathf.Abs(Mathf.DeltaAngle(0,CameraLabPreview.Player.transform.GetChild(0).localEulerAngles.z))>15,"Motorcycle bank changed");Inspect("SteeringAuthored");Finish(true,"PASS normal riding and steering retain the authored head rotation; correction is aiming only. PASS all neutral aim checks.");
        }
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
    private static Transform Head(){foreach(var bone in CameraLabPreview.Player.GetComponentsInChildren<Transform>())if(bone.name=="DEF-spine.006")return bone;throw new Exception("No helmet bone");}
    private static void CheckNeutral(string label){
        var player=CameraLabPreview.Player;var aim=player.GetComponent<BikeAutoAim>();var point=(Vector3)typeof(PlayerController).GetMethod("PistolTargetPoint",Private).Invoke(player,null);
        Require(aim.Target==null,label+": target must be absent");Require(Mathf.Abs(Head().forward.y)<.025f,label+": head is not looking level");Require(Vector3.Angle(aim.Muzzle.right,Vector3.forward)<2,label+": barrel is not pointing straight");Require(Vector3.Angle(point-aim.Muzzle.position,Vector3.forward)<2,label+": shot/laser still follows camera pitch");
        if(label=="PitchedCamera"){
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Neutral shot cover probe";wall.transform.position=aim.Muzzle.position+Vector3.forward*6;wall.transform.localScale=new Vector3(2,2,.2f);Physics.SyncTransforms();var blocked=(Vector3)typeof(PlayerController).GetMethod("PistolTargetPoint",Private).Invoke(player,null);Require(Vector3.Distance(blocked,aim.Muzzle.position)<7,"Level fallback shoots through nearby cover");UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();
        }
        var laser=player.GetComponent<PistolAimLaser>();typeof(PistolAimLaser).GetMethod("Refresh",Private).Invoke(laser,new object[]{CameraLabPreview.Camera});Require(laser.Visible && Vector3.Angle(laser.EndPoint-laser.Origin,Vector3.forward)<2,label+": visible laser does not follow the level shot direction");
        Inspect(label);
    }
    private static void Inspect(string label){
        var player=CameraLabPreview.Player;var aim=player.GetComponent<BikeAutoAim>();Transform head=null,torso=null;foreach(var bone in player.GetComponentsInChildren<Transform>()){if(bone.name=="DEF-spine.006")head=bone;if(bone.name=="DEF-spine.001")torso=bone;}
        var point=(Vector3)typeof(PlayerController).GetMethod("PistolTargetPoint",Private).Invoke(player,null);
        Report(label+": cameraPitch="+CameraLabPreview.Camera.transform.eulerAngles.x+" bore="+aim.Muzzle.right.ToString("F3")+" shot="+(point-aim.Muzzle.position).normalized.ToString("F3")+" headForward="+head.forward.ToString("F3")+" headUp="+head.up.ToString("F3")+" torso="+torso.eulerAngles.ToString("F2"));
        foreach(var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>()){
            int index=Array.IndexOf(renderer.bones,head);if(index<0 || index>=renderer.sharedMesh.bindposes.Length)continue;
            var bind=(renderer.transform.localToWorldMatrix*renderer.sharedMesh.bindposes[index].inverse).rotation;
            var axis=Quaternion.Inverse(bind)*Vector3.forward;
            Report("Head bind axis="+axis.ToString("F3")+" actual look="+head.TransformDirection(axis).ToString("F3"));break;
        }
        Save(CameraLabPreview.Camera,label+"Main");var root=new GameObject("Neutral aim inspection camera");var camera=root.AddComponent<Camera>();camera.CopyFrom(CameraLabPreview.Camera);camera.enabled=false;camera.targetTexture=null;camera.transform.position=player.transform.position+new Vector3(9,3.5f,2);camera.transform.LookAt(player.transform.position+Vector3.up*2.7f);camera.fieldOfView=50;Save(camera,label+"Side");UnityEngine.Object.DestroyImmediate(root);
    }
    private static void Save(Camera camera,string label){var old=camera.targetTexture;var active=RenderTexture.active;var texture=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);try{camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/NeutralAim/"+label+".png",image.EncodeToPNG());}finally{camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(texture);}}
    private static void Finish(bool success,string message){if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Report(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
