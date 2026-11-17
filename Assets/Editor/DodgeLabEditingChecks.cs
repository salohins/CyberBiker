using System;
using System.IO;
using System.Reflection;
using CyberBiker.EditorTools;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DodgeLabEditingChecks {
    private const string Key="CyberBiker.DodgeLabEditingChecks";
    private static double began;
    private static bool stopping,finished;
    static DodgeLabEditingChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){
        Directory.CreateDirectory("Logs/DodgeLabEditing");File.WriteAllText("Logs/DodgeLabEditing/Result.txt","RUNNING\n");
        DodgeLabWindow.Open();CheckEditing("before preview");
        SessionState.SetBool(Key,true);DodgeLabPreview.Loop=false;DodgeLabPreview.Sound=false;DodgeLabPreview.Gap=1.3f;DodgeLabPreview.Start(DodgeLabWindow.ReadSaved());
    }
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Require(bool valid,string text){if(!valid)throw new Exception(text);}
    private static void CheckEditing(string phase){
        var window=EditorWindow.GetWindow<DodgeLabWindow>();window.position=new Rect(60,60,765,874);window.Repaint();
        var draft=(DodgeLabDraft)typeof(DodgeLabWindow).GetField("draft",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
        Require((draft.hideFlags&HideFlags.NotEditable)==0,"Draft still has the read-only flag "+phase);
        using(var data=new SerializedObject(draft)){
            foreach(var field in new[]{"enabled","passRadius","slowMotion","worldSpeed","cameraMotion","zoomInFov"})Require(data.FindProperty("settings."+field).editable,"Control is not editable: "+field+" "+phase);
            float original=draft.settings.passRadius;
            data.FindProperty("settings.passRadius").floatValue=2.65f;Require(data.ApplyModifiedPropertiesWithoutUndo(),"Editor radius edit was rejected "+phase);
            typeof(DodgeLabWindow).GetMethod("Remember",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
            Require(Mathf.Abs(window.Draft.passRadius-2.65f)<.001f,"Draft did not accept the edited radius "+phase);
            if(DodgeLabPreview.Ready)Require(Mathf.Abs(DodgeLabPreview.Gameplay.CloseDodge.passRadius-2.65f)<.001f,"Edit did not reach the live preview "+phase);
            data.Update();data.FindProperty("settings.passRadius").floatValue=original;data.ApplyModifiedPropertiesWithoutUndo();typeof(DodgeLabWindow).GetMethod("Remember",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
        }
        File.AppendAllText("Logs/DodgeLabEditing/Result.txt","PASS editable serialized controls, radius change and live apply "+phase+" at 765px width.\n");
    }
    private static void Tick(){if(finished)return;try{
        Require(EditorApplication.timeSinceStartup-began<70,"Editing check timed out");
        if(!stopping && DodgeLabPreview.Ready){CheckEditing("during preview");EditorApplication.isPaused=true;CheckEditing("while paused");stopping=true;SessionState.SetBool(Key+"Stopping",true);DodgeLabPreview.Stop();}
        if(SessionState.GetBool(Key+"Stopping",false) && !EditorApplication.isPlaying && !DodgeLabPreview.Active){SessionState.SetBool(Key+"Stopping",false);Finish(true,"PASS all editing checks");}
    }catch(Exception error){Finish(false,error.ToString());}}
    private static void Finish(bool success,string text){if(finished)return;finished=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;File.AppendAllText("Logs/DodgeLabEditing/Result.txt",text+"\n");EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);}
}
