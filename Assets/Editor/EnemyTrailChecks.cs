using System;
using System.IO;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyTrailChecks {
    private const string Key="CyberBiker.EnemyTrailChecks";
    private static int stage,firstHit;
    private static double started;
    private static Enemy target;
    private static TrailRenderer trail;
    private static bool finishing,sawHistory;
    static EnemyTrailChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run() {
        Directory.CreateDirectory("Logs/EnemyTrail");CheckShader();
        var tuning=FinisherLabTuning.ReadSavedDefaults();
        tuning.Get("drawPlaybackSpeed").number=1;tuning.Get("strikePlaybackSpeed").number=.95f;
        tuning.Get("sliceReturnPlaybackSpeed").number=3.5f;
        SessionState.SetBool(Key,true);FinisherLabPreview.Sound=false;FinisherLabPreview.Left=true;
        FinisherLabPreview.Start(tuning);
    }
    private static void Attach(){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Tick() {
        if(finishing || !EditorApplication.isPlaying)return;
        try {
            if(EditorApplication.timeSinceStartup-started>50)throw new Exception("Trail check timeout at "+stage);
            if(!FinisherLabPreview.Ready)return;
            var sword=FinisherLabPreview.Sword;
            Require(FinisherLabPreview.Gameplay.gameState==GameState.Playing,"Trail changes must not disrupt the live finisher");
            if(stage==0 && sword.Target!=null) {
                target=sword.Target;trail=target.GetComponentInChildren<TrailRenderer>();
                Require(trail!=null && trail.sharedMaterial.shader.name=="CyberBiker/Enemy Speed Trail","Existing enemy anchor must use the new shared shader");
                firstHit=sword.HitCount;stage=1;
            }
            if(stage==1) {
                if(sword.Phase==BikeSwordCombat.CombatPhase.Drawing && trail.positionCount>2) {
                    sawHistory=true;Require(target.SpeedTrailVisible,"Normal enemy trail must remain visible before the dash");
                    Require(trail.time*target.ForwardSpeed<=18.1f && trail.widthMultiplier>=.35f,"Trail must retain a visible width with a bounded length");
                    CheckGameplayVisibility();
                    Capture("BeforeDash");stage=2;
                }
            } else if(stage==2 && sword.Phase==BikeSwordCombat.CombatPhase.Striking) {
                Require(sawHistory && !trail.enabled && !trail.emitting && trail.positionCount==0,"Starting the dash must immediately clear all old trail points and hide emission");
                Capture("Dash");
                // Interrupt a live dash to verify its surviving enemy starts a
                // fresh trail, rather than reconnecting the old ribbon.
                sword.CancelForAim();stage=3;
            } else if(stage==3 && !sword.HoldsEnemyForStrike(target)) {
                Require(target.IsAlive && sword.HitCount==firstHit,"Cancellation fixture must survive");
                if(!target.SpeedTrailVisible)return;
                Require(trail.emitting && trail.enabled,"Cancelled dash must restore a surviving enemy's trail");
                target.HideSpeedTrailForFinisher();Require(trail.positionCount==0 && !trail.enabled,"Explicit suppression must clear old geometry");
                // Permit ordinary updates to resume it, then record a fresh,
                // uninterrupted encounter (the cancelled target is now behind us).
                stage=4;
            } else if(stage==4 && sword.Phase==BikeSwordCombat.CombatPhase.Idle) {
                FinisherLabPreview.Replay();target=null;trail=null;firstHit=0;stage=5;
            } else if(stage==5) {
                if(target==null && sword.Target!=null){target=sword.Target;trail=target.GetComponentInChildren<TrailRenderer>();}
                if(sword.HitCount<=firstHit)return;
                Require(!trail.enabled && !trail.emitting && trail.positionCount==0,"Defeated enemy trail must stay completely hidden through impact and death");
                Finish(true,"PASS: feathered trail shader rendered; rider capsule removes the crossing section while preserving the far trail; existing enemy anchors use one shared material; normal trail is narrow and length-limited; committed dash clears history and emission immediately; cancellation restores a fresh trail; subsequent actual finisher hides it through death. Native captures saved.");
            }
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void Capture(string name) {
        var rt=FinisherLabPreview.View;var previous=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes("Logs/EnemyTrail/"+name+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
    }
    private static void CheckGameplayVisibility() {
        var camera=Camera.main;var rt=FinisherLabPreview.View;
        var visible=Render(camera,rt,"GameplayTrail");Texture2D hidden=null;
        try {
            trail.enabled=false;hidden=Render(camera,rt,"GameplayWithoutTrail");
            var a=visible.GetPixels32();var b=hidden.GetPixels32();int clearPixels=0;
            for(int i=0;i<a.Length;i++)if(a[i].r-b[i].r>25)clearPixels++;
            Require(clearPixels>80,"Trail must make a clearly visible contribution in the actual gameplay camera; bright red pixels="+clearPixels);
            File.WriteAllText("Logs/EnemyTrail/Visibility.txt","Visible red trail contribution in gameplay camera: "+clearPixels+" pixels above a 25/255 red difference.\n");
        } finally {trail.enabled=true;UnityEngine.Object.DestroyImmediate(visible);UnityEngine.Object.DestroyImmediate(hidden);}
    }
    private static void CheckShader() {
        var material=Resources.Load<Material>("EnemySpeedTrail");Require(material!=null && material.shader.isSupported,"Trail material must be included and supported");
        var go=new GameObject("Trail shader check"){layer=30};var view=new GameObject("Trail shader camera");
        var camera=view.AddComponent<Camera>();var line=go.AddComponent<LineRenderer>();
        var rt=new RenderTexture(960,540,24);Texture2D plain=null,masked=null;
        try {
            line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=3;
            line.SetPositions(new[]{new Vector3(0,1.3f,0),new Vector3(0,1.3f,8),new Vector3(0,1.3f,16)});
            line.widthMultiplier=.35f;line.startColor=new Color(1,.12f,.04f,1);line.endColor=line.startColor;
            camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.orthographic=true;camera.orthographicSize=5.5f;camera.targetTexture=rt;
            camera.transform.position=new Vector3(6,7,8);camera.transform.LookAt(new Vector3(0,1.3f,8));
            plain=Render(camera,rt,"TrailDetail");
            var block=new MaterialPropertyBlock();block.SetVector("_RiderBottom",new Vector3(0,.35f,8));block.SetVector("_RiderTop",new Vector3(0,2.8f,8));block.SetFloat("_RiderRadius",1.25f);line.SetPropertyBlock(block);
            masked=Render(camera,rt,"RiderClearance");
            Require(plain.GetPixel(480,270).r>.1f && masked.GetPixel(480,270).r<.01f,"Shader must remove the trail at the rider, rather than drawing through the body");
            int farPixels=0;foreach(var color in masked.GetPixels())if(color.r>.1f)farPixels++;
            Require(farPixels>200,"Clearance must preserve the trail outside the rider");
            Require(!ShaderUtil.ShaderHasError(material.shader),"Trail shader must compile without errors");
        } finally {
            camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(plain);UnityEngine.Object.DestroyImmediate(masked);
            UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(view);UnityEngine.Object.DestroyImmediate(rt);
        }
    }
    private static Texture2D Render(Camera camera,RenderTexture rt,string name) {
        var previous=RenderTexture.active;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
        File.WriteAllBytes("Logs/EnemyTrail/"+name+".png",image.EncodeToPNG());RenderTexture.active=previous;return image;
    }
    private static void Finish(bool success,string message) {
        if(finishing)return;finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key,false);
        File.WriteAllText("Logs/EnemyTrail/result.txt",message);Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);
    }
}
