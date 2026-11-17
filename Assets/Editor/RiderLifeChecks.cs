using System;
using System.IO;
using System.Reflection;
using CyberBiker.Audio;
using CyberBiker.Combat;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RiderLifeChecks {
    private const string Key="CyberBiker.RiderLifeChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static double began,at;
    private static int stage,passes;
    private static bool finished,moveCar,moveEnemy;
    private static Car car;
    private static Enemy enemy,shooter;
    private static Vector3 shooterOffset=new Vector3(4,0,24);
    private static RiderLife life;
    private static PlayerController player;
    private static float gap=10,basePitch;
    private static Quaternion authoredHead,authoredNeck;
    static RiderLifeChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){Directory.CreateDirectory("Logs/RiderLife");File.WriteAllText("Logs/RiderLife/Result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Stage",0);CameraLabPreview.Sound=false;CameraLabPreview.Start(CameraLabTuning.ReadSavedDefaults(),CameraLabPreview.ViewMode.Riding);}
    private static void Attach(){began=at=EditorApplication.timeSinceStartup;stage=SessionState.GetInt(Key+"Stage",0);EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Report(string text)=>File.AppendAllText("Logs/RiderLife/Result.txt",text+"\n");
    private static void Require(bool valid,string text){if(!valid)throw new Exception(text);}
    private static void Next(int value){stage=value;SessionState.SetInt(Key+"Stage",value);at=EditorApplication.timeSinceStartup;}
    private static Transform Head(){foreach(var bone in player.GetComponentsInChildren<Transform>())if(bone.name=="DEF-spine.006")return bone;throw new Exception("Missing helmet bone");}
    private static Transform Neck(){foreach(var bone in player.GetComponentsInChildren<Transform>())if(bone.name=="DEF-spine.005")return bone;throw new Exception("Missing neck bone");}
    private static float Pitch=>-Mathf.Asin(Mathf.Clamp(Head().forward.y,-1,1))*Mathf.Rad2Deg;
    private static void Tick(){if(finished)return;try{
        Require(EditorApplication.timeSinceStartup-began<130,"Rider Life timed out at "+stage+": "+CameraLabPreview.Status);
        if(stage==40 && !EditorApplication.isPlaying && !CameraLabPreview.Active){Finish(true,"PASS all Rider Life checks.");return;}
        if(!CameraLabPreview.Ready)return;
        player=CameraLabPreview.Player;life=player.GetComponent<RiderLife>();Require(life!=null,"Rider Life was not installed on the real player prefab");
        if(moveCar && car!=null)car.transform.position=player.transform.position+new Vector3(4,0,gap);
        if(moveEnemy && enemy!=null)enemy.transform.position=player.transform.position+new Vector3(-6,0,24);
        if(shooter!=null)shooter.transform.position=player.transform.position+shooterOffset;
        if(moveCar || moveEnemy || shooter!=null)Physics.SyncTransforms();
        double age=EditorApplication.timeSinceStartup-at;
        if(stage==0){life.enabled=false;Next(1);}
        else if(stage==1 && age>.8){
            authoredHead=Head().rotation;authoredNeck=Neck().localRotation;basePitch=Pitch;
            var aim=player.GetComponent<BikeAutoAim>();Require(!(bool)typeof(BikeAutoAim).GetField("hasHeadRenderPose",Private).GetValue(aim),"Aiming fix still overrides the normal riding head");
            using(var data=new SerializedObject(life)){data.FindProperty("idleSway").floatValue=0;data.FindProperty("breathingAmount").floatValue=0;data.ApplyModifiedPropertiesWithoutUndo();}life.enabled=true;Next(2);
        }else if(stage==2 && age>1){
            Require(Quaternion.Angle(authoredHead,Head().rotation)<.7f,"Rider Life raised the authored neutral riding head");Capture("AuthoredRiding");Report("PASS original riding helmet pitch retained; no level-head override outside aiming.");
            var root=new GameObject("Rider life traffic probe");root.transform.position=player.transform.position+new Vector3(4,0,50);var collider=root.AddComponent<BoxCollider>();collider.isTrigger=true;car=root.AddComponent<Car>();car.enabled=false;typeof(Car).GetField("vitality",Private).SetValue(car,new Health(100));moveCar=true;gap=50;Next(20);
        }else if(stage==20 && age>.25 && life.HasAttention){
            Require(gap>40 && life.GlanceCount>0,"Car was noticed only at the old late distance");Report("PASS predictive car attention starts at 50 m, before the old 20 m threshold.");gap=10;Next(3);
        }else if(stage==3 && age>.35 && life.HasAttention && life.HeadYaw>8){
            Capture("CarGlance");Require(Mathf.Abs(Pitch-basePitch)<26,"Car glance exceeded its bounded pitch change");Require(Vector3.Angle(Head().forward,car.transform.position+Vector3.up*1.5f-Head().position)<4,"Helmet is not actually looking at the car");Require(life.GlanceCount>0 && life.BodyPoseApplied,"Car glance did not include the subtle body response");Require(life.NeckPoseApplied && life.NeckTurn>2,"Car glance did not use the neck joint");Report("PASS gaze points at the car in 3D and shares the motion between neck and helmet.");gap=1000;Next(21);
        }else if(stage==21 && age>.1){
            Require(life.HeadYaw>1,"Car gaze snapped back instead of blending out");Next(22);
        }else if(stage==22 && age>1.2){
            Report("Car return: yaw="+life.HeadYaw+", neck correction="+life.NeckTurn+", animated-neck change="+Quaternion.Angle(Neck().localRotation,authoredNeck)+", attention="+life.HasAttention);Require(Mathf.Abs(life.HeadYaw)<2 && life.NeckTurn<.5f,"Neck and head did not return to the authored riding pose");Capture("CarReturn");Report("PASS continuous car-glance return and restoration of the original neck pose.");gap=10;life.ReactToPass(player.transform.position+new Vector3(4,0,10),1,car.transform);
            var root=new GameObject("Priority shooting enemy probe");root.transform.position=player.transform.position+new Vector3(-6,0,24);root.AddComponent<BoxCollider>().isTrigger=true;enemy=root.AddComponent<Enemy>();enemy.enabled=false;typeof(Enemy).GetField("vitality",Private).SetValue(enemy,new Health(10000));typeof(Enemy).GetField("armed",Private).SetValue(enemy,true);moveEnemy=true;Next(30);
        }else if(stage==30 && age>.35 && life.PriorityEnemy==enemy && life.HeadYaw< -7){
            Require(!life.NeckPoseApplied,"Car-only neck layer changed shooting-enemy looking behavior");passes=life.PassReactionCount;life.ReactToPass(player.transform.position+Vector3.right*4,1);Require(life.PassReactionCount==passes,"Passing traffic interrupted attention to a shooting enemy");Capture("ShootingEnemyPriority");Report("PASS armed/shooting enemy interrupts an existing closer-car glance and blocks new traffic distractions.");
            typeof(Enemy).GetField("armed",Private).SetValue(enemy,false);typeof(Enemy).GetField("<LastFiredTime>k__BackingField",Private).SetValue(enemy,Time.time);Next(31);
        }else if(stage==31 && age>.2){
            Require(life.PriorityEnemy==enemy,"Enemy attention was dropped immediately between shots");enemy.gameObject.SetActive(false);moveEnemy=false;gap=1000;Next(4);
        }else if(stage==4 && age>1.2){
            Require(Mathf.Abs(life.HeadYaw)<2,"Rider did not return to the original forward pose after the glance");Report("PASS brief nearby-car glance, bounded pitch, shoulder follow and smooth return.");gap=8;passes=life.PassReactionCount;Next(5);
        }else if(stage==5 && age>.3){gap=-8;Next(6);
        }else if(stage==6 && age>.2 && life.PassReactionCount>passes){
            Require(life.BodyRoll>.2f && life.BodyRoll<5,"Right-hand traffic pass must give a small lean away, not a large pose change");Capture("PassReaction");Report("PASS actual muted-audio traffic crossing triggers a visible body lean and glance; no physics movement added.");gap=1000;Next(7);
        }else if(stage==7 && age>1.5){
            Require(Mathf.Abs(life.BodyRoll)<.2f,"Traffic lean did not recover");
            var root=new GameObject("Rider life enemy probe");root.transform.position=player.transform.position+new Vector3(-6,0,24);var collider=root.AddComponent<BoxCollider>();collider.center=Vector3.up*2;collider.size=new Vector3(2,3,2);enemy=root.AddComponent<Enemy>();enemy.enabled=false;typeof(Enemy).GetField("vitality",Private).SetValue(enemy,new Health(10000));moveEnemy=true;Next(8);
        }else if(stage==8 && life.HasAttention && life.HeadYaw< -7){
            Capture("EnemyGlance");Report("PASS rider notices a nearby enemy while riding.");
            var manager=CameraLabPreview.Gameplay;manager.PistolInteractionMode=Gameplay.PistolInteraction.TapToFire;passes=life.PassReactionCount;
            Require(player.RequestPistolTap(),"Pistol tap was not accepted");life.ReactToPass(player.transform.position+Vector3.right*4,1);Require(life.PassReactionCount==passes,"First pistol tap did not immediately suppress traffic reactions before animator update");player.CancelPistolTaps();manager.PistolInteractionMode=Gameplay.PistolInteraction.AimAndAutoFire;
            Report("PASS first shooting input suppresses traffic before animation state catches up.");CameraLabPreview.TargetSide=100;CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.CombatAim);Next(9);
        }else if(stage==9 && age>1.5 && player.IsPistolVisuallyReady){
            var aim=player.GetComponent<BikeAutoAim>();Require(aim.Target==enemy,"Ambient reactions interrupted weapon targeting");Require(Vector3.Angle(aim.Muzzle.right,aim.AimPoint-aim.Muzzle.position)<6,"Ambient body motion disturbed the pistol alignment");Require(!life.BodyPoseApplied && Mathf.Abs(life.BodyRoll)<.001f,"Body reactions must yield to aiming");
            passes=life.PassReactionCount;life.ReactToPass(player.transform.position+Vector3.right*4,1);Require(life.PassReactionCount==passes,"Aiming must ignore environmental body kicks");Capture("AimPriority");Report("PASS pistol alignment preserved and environmental body kicks suppressed while aiming.");
            var root=new GameObject("Visible enemy firing at player");root.transform.position=player.transform.position+shooterOffset;root.AddComponent<BoxCollider>().isTrigger=true;shooter=root.AddComponent<Enemy>();shooter.enabled=false;typeof(Enemy).GetField("vitality",Private).SetValue(shooter,new Health(10000));typeof(Enemy).GetField("armed",Private).SetValue(shooter,true);Next(32);
        }else if(stage==32 && age>.4 && life.PriorityEnemy==shooter && life.HeadYaw>2){
            var aim=player.GetComponent<BikeAutoAim>();Require(aim.Target==enemy,"Head attention must not redirect the pistol's selected target");Require(Vector3.Angle(aim.Muzzle.right,aim.AimPoint-aim.Muzzle.position)<6,"Looking at the shooter disturbed gun alignment");Capture("ShooterOverAimTarget");Report("PASS visible attacker wins head attention even while the pistol targets a different enemy; gun alignment remains correct.");shooterOffset=new Vector3(40,0,12);Next(33);
        }else if(stage==33 && age>.4){
            Require(life.PriorityEnemy!=shooter,"Offscreen shooter retained attention priority");Report("PASS shooter loses priority when outside the camera view.");shooterOffset=new Vector3(4,0,24);Next(34);
        }else if(stage==34 && age>.4 && life.PriorityEnemy==shooter){
            Report("PASS shooter regains priority when visible again.");CameraLabPreview.SwitchView(CameraLabPreview.ViewMode.Finisher);Next(10);
        }else if(stage==10 && CameraLabPreview.Mode==CameraLabPreview.ViewMode.Finisher && FinisherLabPreview.Sword.Phase==BikeSwordCombat.CombatPhase.Striking){
            Require(!life.PoseApplied,"Ambient pose layer must yield to authored sword strike");Capture("SwordPriority");Report("PASS sword strike retains exclusive ownership of the authored pose.");Next(40);CameraLabPreview.Stop();
        }
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void Capture(string label){
        var root=new GameObject("Rider life inspection camera");var camera=root.AddComponent<Camera>();camera.CopyFrom(CameraLabPreview.Camera);camera.enabled=false;
        camera.transform.position=player.transform.position+new Vector3(7,3.4f,-5);camera.transform.LookAt(player.transform.position+Vector3.up*2.7f);camera.fieldOfView=45;
        var texture=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/RiderLife/"+label+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(root);}
    }
    private static void Finish(bool success,string message){if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Report(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
