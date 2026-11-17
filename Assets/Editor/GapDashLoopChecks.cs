using System;
using System.Collections.Generic;
using System.IO;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using CyberBiker.Pooling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using P=CyberBiker.EditorTools.GapDashLabPreview;

[InitializeOnLoad]
public static class GapDashLoopChecks {
    private const string Key="CyberBiker.GapDashLoopChecks";
    private struct Road {public Tile tile;public Vector3 position;public Quaternion rotation;public PooledInstance lease;public uint generation;}
    private static readonly List<Road> road=new List<Road>();
    private static bool finished,initialized,checkedPass;
    private static double began;
    private static float previousClock;
    private static int completed;
    static GapDashLoopChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        Directory.CreateDirectory("Logs/GapDashLoops");File.WriteAllText("Logs/GapDashLoops/Result.txt","RUNNING\n");
        EditorSceneManager.OpenScene("Assets/Level/Scenes/Menu.unity");EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Level/Scenes/Menu.unity");AudioListener.volume=.37f;
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Stopping",false);GapDashLabWindow.Open();P.Current=P.Scenario.OpenGap;P.Offset=.75f;P.Speed=65;P.Spacing=6.06f;P.Steering=0;P.Aiming=false;P.LaunchMode=true;P.Loop=true;P.Sound=false;P.OrbitView=false;P.Start(new GapDashSettings());
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    private static void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Require(bool value,string text){if(!value)throw new Exception(text);}
    private static void Report(string text)=>File.AppendAllText("Logs/GapDashLoops/Result.txt",text+"\n");
    private static void Tick(){if(finished)return;try{
        Require(EditorApplication.timeSinceStartup-began<100,"Automatic loop test timed out.");
        if(SessionState.GetBool(Key+"Stopping",false) && !P.Active && !EditorApplication.isPlaying){Require(P.View==null && Mathf.Abs(AudioListener.volume-.37f)<.001f && AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)=="Assets/Level/Scenes/Menu.unity","Stop did not restore the prior preview environment.");Finish(true,"PASS five consecutive automatic passes with fixed road tiles, normal riding speed and safe camera follow.");return;}
        if(!P.Ready)return;
        if(!initialized){
            foreach(var tile in UnityEngine.Object.FindObjectsByType<Tile>()){var lease=tile.GetComponent<PooledInstance>();road.Add(new Road{tile=tile,position=tile.transform.position,rotation=tile.transform.rotation,lease=lease,generation=lease!=null ? lease.Generation : 0});}
            Require(road.Count>1,"No generated road was available to test.");P.Replay();previousClock=0;initialized=true;return;
        }
        foreach(var entry in road)Require(entry.tile!=null && entry.tile.gameObject.activeInHierarchy && (entry.tile.transform.position-entry.position).sqrMagnitude<.00001f && Quaternion.Angle(entry.tile.transform.rotation,entry.rotation)<.001f && (entry.lease==null || entry.lease.Generation==entry.generation),"Preview road shifted or recycled before pass "+(completed+1)+" (clock "+P.Clock+").");
        Require(UnityEngine.Object.FindObjectsByType<Tile>().Length==road.Count,"Preview road streamer changed the fixed road layout before pass "+(completed+1)+".");
        if(P.Clock+.5f<previousClock){Require(checkedPass,"A loop replayed without completing its gap passage.");checkedPass=false;}
        previousClock=P.Clock;
        if(checkedPass || P.Assist.CompletedCount==0)return;
        Require(P.Assist.CompletedCount==1 && P.Assist.CancelCount==0 && P.Player.IsAlive && P.Gameplay.gameState==Gameplay.GameState.Playing,"Automatic pass "+(completed+1)+" failed: "+P.Status+", grounded="+P.Player.grounded);
        Require(Mathf.Abs(P.Player.GetComponent<Rigidbody>().linearVelocity.z-P.Speed)<1,"Replay changed normal riding speed.");
        Require(Mathf.Abs(Camera.main.transform.position.z-P.Player.transform.position.z)<45,"Camera did not follow the replayed rider.");
        completed++;checkedPass=true;Report("PASS automatic pass "+completed+"; "+road.Count+" road tiles retained their pose and pool generation.");
        if(completed==3)P.Offset=-.75f;
        if(completed==5){P.Loop=false;SessionState.SetBool(Key+"Stopping",true);P.Stop();}
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void Finish(bool success,string text){if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Report(text);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
