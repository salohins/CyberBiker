using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using CyberBiker.UI;
using Gameplay;
using UnityEditor;
using UnityEngine;
using P=CyberBiker.EditorTools.GapDashLabPreview;

[InitializeOnLoad]
public static class GapSideEntryChecks {
    private const string Key="CyberBiker.GapSideEntryChecks";
    private const BindingFlags Private=BindingFlags.Static|BindingFlags.NonPublic;
    private struct Case {
        public string name;public float offset,velocity,steering,speed,distance;public P.Scenario scenario;public bool pass,banked;
        public Case(string n,float x,float vx,float steer,float v=110,float z=45,P.Scenario s=P.Scenario.OpenGap,bool p=true,bool banked=false){name=n;offset=x;velocity=vx;steering=steer;speed=v;distance=z;scenario=s;pass=p;this.banked=banked;}
    }
    private static readonly Case[] Cases={
        new Case("countersteer from right",3.2f,-8,1),new Case("countersteer from left",-3.2f,8,-1),
        new Case("fast inward right",4.8f,-8,-1,110,85),new Case("fast inward left",-4.8f,8,1,110,85),
        new Case("normal speed inward right",4.8f,-8,-1,65,65),new Case("normal speed inward left",-4.8f,8,1,65,65),
        new Case("already in catch radius right",1.6f,-5,-1,110,45),new Case("already in catch radius left",-1.6f,5,1,110,45),
        new Case("neutral correction right",1.6f,0,0,110,45),new Case("neutral correction left",-1.6f,0,0,110,45),
        new Case("banked side approach right",4.8f,-8,-1,110,85,banked:true),new Case("banked side approach left",-4.8f,8,1,110,85,banked:true),
        new Case("blocked side entry",3.2f,-8,-1,110,45,P.Scenario.BlockedExit,false),
        new Case("narrow side entry",3.2f,-8,-1,110,45,P.Scenario.TightGap,false),
        new Case("too late side entry",3.2f,-8,-1,110,12,P.Scenario.OpenGap,false)
    };
    private static bool done,ready,sawCentered,sawOutsideCapture;
    private static int index;
    private static float health;
    private static double began;
    static GapSideEntryChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        Directory.CreateDirectory("Logs/GapSideEntry");File.WriteAllText("Logs/GapSideEntry/Result.txt","");SessionState.SetBool(Key,true);
        P.Loop=false;P.Sound=false;P.Aiming=false;P.LaunchMode=true;P.Steering=0;P.Spacing=6.06f;P.OrbitView=false;
        // Broad side-entry cases use an explicit 65m limit. Separate boundary
        // checks below verify the authored 20m-style short capture behavior.
        var fixture=GapDashLabWindow.ReadSaved();fixture.maximumTriggerDistance=65;P.Start(fixture);
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static Car[] Cars=>(Car[])typeof(P).GetField("cars",Private).GetValue(null);
    private static Bounds BikeBounds(){Bounds result=default;bool first=true;foreach(var c in P.Player.GetComponentsInChildren<Collider>())if(c.enabled && !c.isTrigger){if(first){result=c.bounds;first=false;}else result.Encapsulate(c.bounds);}return result;}
    private static void PlaceBumpers(float clearance){
        Physics.SyncTransforms();float current=Mathf.Min(Cars[0].GetComponent<BoxCollider>().bounds.min.z,Cars[1].GetComponent<BoxCollider>().bounds.min.z)-BikeBounds().max.z;
        for(int i=0;i<2;i++)Cars[i].transform.position+=Vector3.forward*(clearance-current);Physics.SyncTransforms();
    }
    private static void CheckHardLimit(){
        var fixture=P.Assist.Settings.Copy();var shortRange=fixture.Copy();shortRange.maximumTriggerDistance=20;shortRange.predictiveSideEntry=true;P.Apply(shortRange);
        P.Speed=65;P.Offset=3.2f;P.Steering=1;P.CarDistance=65;P.Current=P.Scenario.OpenGap;P.Replay();
        var body=P.Player.GetComponent<Rigidbody>();body.linearVelocity=new Vector3(-8,0,65);P.Player.grounded=true;
        Require(!P.Assist.Step(1) && P.Assist.DashCount==0,"Prediction seized control far beyond the 20m limit");
        Require(Mathf.Abs(body.linearVelocity.x+8)<.001f && P.Assist.CameraAmount==0,"Early prediction changed steering velocity or camera");
        PlaceBumpers(20.3f);Require(!P.Assist.Step(1) && P.Assist.DashCount==0,"Alignment started outside the configured limit");
        PlaceBumpers(19.7f);Require(P.Assist.Step(1) && P.Assist.IsActive,"Safe inward/countersteering entry inside the limit was lost");
        Require(P.Assist.EntryForwardDistance<=20.001f && Mathf.Abs(P.Assist.EntrySideOffset)>P.Assist.Settings.snapRadius,"Inside-limit prediction was not exercised from outside the base side radius");
        Report("20m hard limit: no control/camera change far away or at 20.3m; predictive countersteering catch at 19.7m.");
        P.Speed=110;P.Offset=3.2f;P.Steering=-1;P.CarDistance=35;P.Replay();body.linearVelocity=new Vector3(-20,0,110);P.Player.grounded=true;PlaceBumpers(19.7f);
        Require(!P.Assist.Step(-1) && P.Assist.DashCount==0 && P.Assist.Status.Contains("Too late"),"Impossible close alignment must reject instead of overriding the limit");
        Report("too-short high-speed correction rejected without starting outside the limit.");P.Apply(fixture);
    }
    private static void Begin(){
        var c=Cases[index];P.Speed=c.speed;P.Offset=c.offset;P.Steering=c.steering;P.CarDistance=c.distance;P.Current=c.scenario;P.Replay();
        P.Player.GetComponent<Rigidbody>().linearVelocity=new Vector3(c.velocity,0,c.speed);health=P.Player.CurrentHealth;sawCentered=sawOutsideCapture=false;
        if(c.banked){
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var presentation=typeof(PlayerController).GetField("presentation",flags).GetValue(P.Player);float lean=Mathf.Sign(c.steering)*.8f;
            float yaw=(float)typeof(BikePresentation).GetField("rotationSensitivity",flags).GetValue(presentation)*lean;
            foreach(string field in new[]{"animationFrame","visualSteering","steering"})typeof(BikePresentation).GetField(field,flags).SetValue(presentation,lean);
            typeof(BikePresentation).GetField("yawRotation",flags).SetValue(presentation,yaw);
            var rotation=Quaternion.Euler(0,yaw,0);P.Player.transform.rotation=rotation;P.Player.GetComponent<Rigidbody>().rotation=rotation;
            float range=(float)typeof(BikePresentation).GetField("leanRange",flags).GetValue(presentation);
            ((Transform)typeof(PlayerController).GetField("motorcycleMesh",flags).GetValue(P.Player)).localRotation=Quaternion.Euler(0,0,-range*lean);
            Physics.SyncTransforms();
        }
    }
    private static void Tick(){
        if(done || !P.Ready)return;
        try{
            Require(EditorApplication.timeSinceStartup-began<160,"Side-entry checks timed out at "+index);
            if(!ready){
                ready=true;
                var settings=TestingSettings.Read(P.Player,P.Gameplay,P.Player.GetComponent<BikeSwordCombat>());Require(settings.triggerBalance.gapPredictiveSideEntry,"Predictive side entry default missing");settings.triggerBalance.gapPredictiveSideEntry=false;
                Require(BalancePresetIO.TryImport(BalancePresetIO.ExportJson(settings),settings,out var imported,out _) && !imported.triggerBalance.gapPredictiveSideEntry,"Predictive toggle preset round trip failed");
                Require(JsonUtility.FromJson<GapDashSettings>("{\"snapRadius\":2}").predictiveSideEntry,"Older gap profiles must retain the predictive default");
                var menu=P.Gameplay.GetComponentInChildren<TestingSettingsMenu>();Require(menu!=null && menu.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="gapPredictiveSideEntry"),"Predictive side entry control missing from TUNE");
                Report("predictive TUNE control exists; preset false round trip and legacy profile default retained.");CheckHardLimit();Begin();return;
            }
            var c=Cases[index];
            if(!c.pass){
                P.Player.grounded=true;P.Assist.Step(c.steering);Require(P.Assist.DashCount==0,"Unsafe side entry accepted: "+c.name);
                Report("rejected "+c.name);Advance();return;
            }
            Require(P.Gameplay.gameState==GameState.Playing && P.Player.CurrentHealth==health,"Crashed in "+c.name+": "+P.Status);
            Require(P.Clock<2.5f,"No pass for "+c.name+": "+P.Status);
            foreach(var bike in P.Player.GetComponentsInChildren<Collider>())if(bike.enabled && !bike.isTrigger)
                for(int j=0;j<2;j++){var car=Cars[j].GetComponent<BoxCollider>();Require(car.enabled && !Physics.GetIgnoreCollision(bike,car),"Cars lost solidity");Require(!Physics.ComputePenetration(bike,bike.transform.position,bike.transform.rotation,car,car.transform.position,car.transform.rotation,out _,out _),"Side entry crossed a solid car: "+c.name);}
            if(P.Assist.IsActive){
                Require(P.Assist.EntryForwardDistance<=P.Assist.Settings.maximumTriggerDistance+.001f,"Side entry exceeded the configured catch distance");
                if(Mathf.Abs(P.Assist.EntrySideOffset)>P.Assist.Settings.snapRadius+.05f)sawOutsideCapture=true;
                Require(Mathf.Abs(P.Player.xThrow)<.001f,"Steering escaped the committed alignment");
                var b=BikeBounds();var left=Cars[0].GetComponent<BoxCollider>().bounds;var right=Cars[1].GetComponent<BoxCollider>().bounds;
                if(b.max.z>=Mathf.Min(left.min.z,right.min.z)-.2f){Require(b.min.x>left.max.x && b.max.x<right.min.x,"Side entry did not straighten and centre before bumpers: "+c.name);sawCentered=true;}
                float speed=P.Player.GetComponent<Rigidbody>().linearVelocity.z;Require(speed<=c.speed+.3f && speed>c.speed*.85f,"Side entry changed forward speed");
            }
            Require(P.Assist.CancelCount==0,"Alignment cancelled in "+c.name+": "+P.Status);
            if(P.Assist.CompletedCount==0)return;
            Require(sawCentered,"Never entered a centred corridor");
            if(index<2)Require(sawOutsideCapture,"Countersteering case did not test predictive catch outside the base radius");
            Report(c.name+": moving/steering approach aligned before bumpers, solid cars, no damage or forward boost.");Advance();
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void Advance(){index++;if(index==Cases.Length)Finish(true,"PASS moving side-entry gap checks.");else Begin();}
    private static void Report(string text)=>File.AppendAllText("Logs/GapSideEntry/Result.txt","PASS "+text+"\n");
    private static void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Finish(bool success,string text){if(done)return;done=true;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);File.AppendAllText("Logs/GapSideEntry/Result.txt",text+"\n");Debug.Log(text);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
