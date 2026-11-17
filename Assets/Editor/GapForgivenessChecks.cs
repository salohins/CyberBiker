using System;
using System.IO;
using System.Reflection;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;
using P=CyberBiker.EditorTools.GapDashLabPreview;

[InitializeOnLoad]
public static class GapForgivenessChecks {
    private const string Key="CyberBiker.SolidGapChecks";
    private enum InputPattern { Constant, OpposeAfterStart, AlternateAfterStart }
    private struct Case {
        public string name;public float offset,steer,distance;public P.Scenario scenario;public bool pass;public InputPattern pattern;
        public Case(string n,float x,float s=0,float d=35,P.Scenario scene=P.Scenario.OpenGap,bool passes=true,InputPattern input=InputPattern.Constant){name=n;offset=x;steer=s;distance=d;scenario=scene;pass=passes;pattern=input;}
    }
    private static readonly Case[] Cases={
        new Case("extra wide left",4.8f,0,65),new Case("extra wide right",-4.8f,0,65),
        new Case("wide left",3.2f),new Case("wide right",-3.2f),
        new Case("held steer left",2.8f,-1,50),new Case("held steer right",-2.8f,1,50),
        new Case("small correction left",.75f),new Case("small correction right",-.75f),
        new Case("opposing right after commitment",.75f,0,65,input:InputPattern.OpposeAfterStart),
        new Case("opposing left after commitment",-.75f,0,65,input:InputPattern.OpposeAfterStart),
        new Case("alternating steering during pass",.75f,0,65,input:InputPattern.AlternateAfterStart),
        new Case("physically narrow gap",.75f,0,35,P.Scenario.TightGap,false),
        new Case("too late for wide correction",2.5f,0,12,P.Scenario.OpenGap,false),
        new Case("blocked exit",.75f,0,35,P.Scenario.BlockedExit,false)
    };
    private static bool finished,initialized,sawCorridor,sawLockedInput;private static double began;private static int index;private static float health,completedAt=-1,releasedSteer;
    static GapForgivenessChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){Directory.CreateDirectory("Logs/SolidGapAlignment");File.WriteAllText("Logs/SolidGapAlignment/Result.txt","RUNNING\n");SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Stopping",false);GapDashLabWindow.Open();P.Loop=false;P.Sound=false;P.Aiming=false;P.LaunchMode=true;P.Steering=0;P.Speed=65;P.Spacing=6.06f;P.OrbitView=false;P.Start(GapDashLabWindow.ReadSaved());}
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    private static void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Require(bool value,string text){if(!value)throw new Exception(text);}
    private static void Report(string text)=>File.AppendAllText("Logs/SolidGapAlignment/Result.txt",text+"\n");
    private static Car[] Cars=>(Car[])typeof(P).GetField("cars",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    private static void Begin(){var test=Cases[index];P.Current=test.scenario;P.Offset=test.offset;P.Steering=test.steer;P.CarDistance=test.distance;P.Replay();sawCorridor=sawLockedInput=false;completedAt=-1;health=P.Player.CurrentHealth;}
    private static Bounds BikeBounds(){bool first=true;Bounds b=default;foreach(var c in P.Player.GetComponentsInChildren<Collider>())if(c.enabled && !c.isTrigger){if(first){b=c.bounds;first=false;}else b.Encapsulate(c.bounds);}return b;}
    private static void CheckSolid(){
        foreach(var bike in P.Player.GetComponentsInChildren<Collider>())if(bike.enabled && !bike.isTrigger)
        for(int i=0;i<2;i++){var car=Cars[i].GetComponent<BoxCollider>();Require(car.enabled,"Car collider disabled");Require(!Physics.GetIgnoreCollision(bike,car),"Gap car collision ignored");Require(!Physics.ComputePenetration(bike,bike.transform.position,bike.transform.rotation,car,car.transform.position,car.transform.rotation,out _,out _),"Bike passed through "+Cars[i].name+" in "+Cases[index].name);}
    }
    private static void Tick(){if(finished)return;try{
        Require(EditorApplication.timeSinceStartup-began<150,"Solid alignment checks timed out at "+index+": "+P.Status);
        if(SessionState.GetBool(Key+"Stopping",false) && !P.Active && !EditorApplication.isPlaying){Finish(true,"PASS eleven solid-collision passes, including held, opposing and alternating inputs locked during the pass, with normal steering restored after exit; speed retained and impossible routes rejected.");return;}
        if(!P.Ready)return;
        if(!initialized){initialized=true;var window=EditorWindow.GetWindow<GapDashLabWindow>();JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(GapDashLabWindow.ReadSaved()),window.Draft);window.Changed();index=0;Begin();return;}
        if(index>=Cases.Length)return;var test=Cases[index];
        Require(P.Clock<2.5f,"No successful pass for "+test.name+": "+P.Status);
        if(test.pass){
            CheckSolid();Require(P.Player.CurrentHealth==health && P.Player.IsAlive && P.Gameplay.gameState==Gameplay.GameState.Playing,"Player crashed during "+test.name+": "+P.Status);
            if(P.Assist.IsActive){
                if(test.pattern==InputPattern.OpposeAfterStart)P.Steering=Mathf.Sign(test.offset);
                else if(test.pattern==InputPattern.AlternateAfterStart)P.Steering=Mathf.FloorToInt(P.Assist.Age*12)%2==0 ? 1 : -1;
                Require(Mathf.Abs(P.Player.xThrow)<.001f,"Steering input leaked into the committed pass: "+test.name);
                if(Mathf.Abs(P.Steering)>.5f)sawLockedInput=true;
                var velocity=P.Player.GetComponent<Rigidbody>().linearVelocity;
                Require(velocity.z<=P.Speed+.2f && velocity.z>P.Speed*.85f,"Assist changed forward speed in "+test.name+": "+velocity);
                var bounds=BikeBounds();var l=Cars[0].GetComponent<BoxCollider>().bounds;var r=Cars[1].GetComponent<BoxCollider>().bounds;
                if(bounds.max.z>=Mathf.Min(l.min.z,r.min.z)-.4f){
                    Require(Mathf.Abs(P.Player.GetComponent<Rigidbody>().position.x-P.Assist.TargetX)<.12f,"Bike was not centred before rear bumpers in "+test.name);
                    Require(bounds.min.x>l.max.x && bounds.max.x<r.min.x,"Full bike did not fit inside corridor");
                    if(!sawCorridor){sawCorridor=true;Capture(test.name.Replace(' ','_'));}
                }
            }
            if(P.Assist.CompletedCount==0)return;
            Require(P.Assist.CancelCount==0 && sawCorridor,"Assist abandoned alignment: "+test.name+" / "+P.Status);
            if(test.pattern!=InputPattern.Constant)Require(sawLockedInput,"Opposing/alternating input was not exercised");
            if(completedAt<0){completedAt=P.Clock;releasedSteer=Mathf.Abs(P.Steering)>.5f ? P.Steering : 1;P.Steering=releasedSteer;return;}
            if(P.Clock-completedAt<.08f)return;
            Require(P.Player.xThrow*releasedSteer>.5f && P.Player.GetComponent<Rigidbody>().linearVelocity.x*releasedSteer>.05f,"Manual steering did not return after clearing both cars: "+test.name);
            P.Steering=0;Report("PASS "+test.name+"; input locked through the gap, no overlap or speed boost, normal steering restored after full exit.");
        }else{
            P.Player.grounded=true;P.Assist.Step(test.steer);Require(P.Assist.DashCount==0,"Invalid route accepted: "+test.name);CheckSolid();Report("PASS rejected "+test.name+".");
        }
        index++;if(index<Cases.Length)Begin();else{P.Steering=0;P.CarDistance=35;SessionState.SetBool(Key+"Stopping",true);P.Stop();}
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void Capture(string label){if(index!=0 && index!=2)return;P.OrbitView=true;P.Refresh();var rt=P.View;var old=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes("Logs/SolidGapAlignment/"+label+".png",image.EncodeToPNG());}finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);P.OrbitView=false;}}
    private static void Finish(bool success,string text){if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Report(text);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
