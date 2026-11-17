using System;
using System.IO;
using System.Reflection;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEngine;
using P=CyberBiker.EditorTools.GapDashLabPreview;

[InitializeOnLoad]
public static class GapLaunchContinuityChecks {
    private const string Key="CyberBiker.GapLaunchContinuityChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static bool done,ready,endedLaunch;
    private static int test;
    private static double began;
    private static float health;
    private static BikeSwordCombat sword;
    static GapLaunchContinuityChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        Directory.CreateDirectory("Logs/GapLaunchContinuity");File.WriteAllText("Logs/GapLaunchContinuity/Result.txt","");
        SessionState.SetBool(Key,true);P.Loop=false;P.Sound=false;P.Aiming=false;P.LaunchMode=true;P.Speed=65;P.Offset=.75f;P.CarDistance=35;P.Spacing=6.06f;P.Steering=0;P.OrbitView=false;
        P.Start(GapDashLabWindow.ReadSaved());
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Report(string message)=>File.AppendAllText("Logs/GapLaunchContinuity/Result.txt","PASS "+message+"\n");
    private static Car[] Cars=>(Car[])typeof(P).GetField("cars",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    private static Bounds BikeBounds(){Bounds result=default;bool first=true;foreach(var shape in P.Player.GetComponentsInChildren<Collider>())if(shape.enabled && !shape.isTrigger){if(first){first=false;result=shape.bounds;}else result.Encapsulate(shape.bounds);}return result;}
    private static void CheckSolid(){
        foreach(var bike in P.Player.GetComponentsInChildren<Collider>())if(bike.enabled && !bike.isTrigger)
            for(int i=0;i<2;i++){var car=Cars[i].GetComponent<BoxCollider>();Require(car.enabled && !Physics.GetIgnoreCollision(bike,car),"Cars must remain solid");Require(!Physics.ComputePenetration(bike,bike.transform.position,bike.transform.rotation,car,car.transform.position,car.transform.rotation,out _,out _),"Bike penetrated a car");}
    }
    private static void Begin(){
        P.Steering=0;P.Offset=test==1 ? -.75f : .75f;P.Replay();endedLaunch=false;health=P.Player.CurrentHealth;
    }
    private static void Tick(){
        if(done)return;
        try{
            Require(EditorApplication.timeSinceStartup-began<100,"Gap continuity checks timed out: "+P.Status);
            if(!P.Ready)return;
            if(!ready){ready=true;sword=P.Player.GetComponent<BikeSwordCombat>();Begin();return;}
            if(test<2){
                Require(P.Clock<3,"Gap did not complete: "+P.Status);
                Require(P.Gameplay.gameState==GameState.Playing && P.Player.CurrentHealth==health,"Player crashed after Launch ended: "+P.Status);
                CheckSolid();
                if(P.Assist.IsActive && !endedLaunch){
                    bool enter=test==0 || BikeBounds().max.z>Mathf.Min(Cars[0].GetComponent<BoxCollider>().bounds.min.z,Cars[1].GetComponent<BoxCollider>().bounds.min.z)+.4f;
                    if(enter){
                        if(test==0)typeof(BikeSwordCombat).GetField("launchCharge",Private).SetValue(sword,.0001f);else sword.Launch();
                        endedLaunch=true;P.Steering=test==0 ? 1 : -1;
                    }
                }
                Require(P.Assist.CancelCount==0,"Committed gap abandoned when "+(test==0 ? "Launch energy expired" : "Launch was cancelled")+": "+P.Status);
                if(endedLaunch && !sword.IsPursuing && P.Assist.CompletedCount==0){Require(P.Assist.IsActive,"Ending Launch must not release an unfinished gap");Require(Mathf.Abs(P.Player.xThrow)<.001f,"Held steering must remain locked until clear");}
                if(P.Assist.CompletedCount==0)return;
                Require(endedLaunch && !sword.IsPursuing,"Fixture must actually end Launch during the pass");
                if(test==0)Require(sword.LaunchCharge<.01f,"Gap pass or its two close-shave rewards refilled Launch");
                Require(P.Player.GetComponent<Rigidbody>().linearVelocity.z<=P.Speed+.2f,"Completing the gap added forward speed");
                Report((test==0 ? "energy exhaustion during alignment" : "manual cancellation inside the car corridor")+": pass completed with solid cars, no damage, held steering locked and no speed boost.");
                test++;if(test<2){Begin();return;}
                typeof(BikeSwordCombat).GetField("launchCharge",Private).SetValue(sword,0f);P.Steering=0;P.Replay();
                Require(sword.IsPursuing && sword.LaunchCharge>.99f,"Lab replay must start a fresh charge instead of keeping an exhausted previous take");
                Report("Gap Lab replay restores a fresh Launch charge.");P.LaunchMode=false;P.Replay();return;
            }
            if(P.Clock>.18f){Require(!sword.IsPursuing && P.Assist.DashCount==0,"Ordinary riding must not acquire gaps");Report("new assists still require Launch; ordinary riding stays manual.");Finish(true,"PASS Launch gap continuity checks.");}
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Finish(bool success,string message){if(done)return;done=true;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);File.AppendAllText("Logs/GapLaunchContinuity/Result.txt",message+"\n");Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
