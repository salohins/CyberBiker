using System;
using System.IO;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FinisherLabTakeChecks {
    private const string Key="CyberBiker.TakeChecks.";
    private static int stage;
    private static double began,heartbeat;
    private static bool finishing,sawSlow;
    static FinisherLabTakeChecks(){if(SessionState.GetBool(Key+"Pending",false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void CheckPersistence() {
        Directory.CreateDirectory("Logs/FinisherTake");
        byte[] original=File.ReadAllBytes(FinisherLabTuning.PlayerPrefab);
        var tuning=FinisherLabTuning.ReadSavedDefaults();
        tuning.Get("slowMotionEnabled").number=0;tuning.Get("strikePlaybackSpeed").number=1.15f;
        string fixture="Assets/Editor/FinisherLabSaveFixture.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(fixture)==null)Require(AssetDatabase.CopyAsset(FinisherLabTuning.PlayerPrefab,fixture),"Could not copy save fixture");
        tuning.SaveForGame(fixture);
        var saved=AssetDatabase.LoadAssetAtPath<GameObject>(fixture);
        Require(!saved.GetComponent<SwordFinisherEffects>().SlowMotionEnabled,"Save for game must persist the master switch");
        Require(Mathf.Abs(new SerializedObject(saved.GetComponent<BikeSwordCombat>()).FindProperty("strikePlaybackSpeed").floatValue-1.15f)<.001f,"Save must persist animation speed separately");
        Require(System.Linq.Enumerable.SequenceEqual(original,File.ReadAllBytes(FinisherLabTuning.PlayerPrefab)),"Fixture validation must leave the original prefab unchanged");
        var old=tuning.Copy();old.values.RemoveAll(value=>value.field=="slowMotionEnabled");old.Normalize();
        Require(old.Get("slowMotionEnabled").number==1 && old.Get("strikePlaybackSpeed").number==1.15f,"Old drafts must migrate without losing tuning");
        File.WriteAllText("Logs/FinisherTake/SaveChecks.txt","PASS: master switch and independent animation speed persist in a prefab copy; original prefab unchanged; older drafts migrate without resetting their tuning.\n");
    }
    public static void Run() {
        Directory.CreateDirectory("Logs/FinisherTake");
        var tuning=FinisherLabTuning.ReadSavedDefaults();
        tuning.Get("drawPlaybackSpeed").number=1.4f;tuning.Get("strikePlaybackSpeed").number=1;tuning.Get("sliceReturnPlaybackSpeed").number=1.5f;
        tuning.Get("slowMotionEnabled").number=1;tuning.Normalize();
        SessionState.SetBool(Key+"Pending",true);SessionState.SetInt(Key+"Stage",1);
        FinisherLabPreview.CaptureLength=3;FinisherLabPreview.Sound=false;FinisherLabPreview.Left=true;FinisherLabPreview.RecordTake(tuning);
    }
    private static void Attach(){stage=SessionState.GetInt(Key+"Stage",1);began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,text+"\n"+stack);}
    private static void Image(string name) {
        var target=FinisherLabPreview.Take.Texture;var active=RenderTexture.active;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        try{RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes("Logs/FinisherTake/"+name+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);}
    }
    private static void Tick() {
        if(finishing || !EditorApplication.isPlaying)return;
        try {
            if(EditorApplication.timeSinceStartup-began>180)throw new Exception("Take capture timed out at stage "+stage+": "+FinisherLabPreview.Status);
            if(EditorApplication.timeSinceStartup-heartbeat>3){heartbeat=EditorApplication.timeSinceStartup;File.AppendAllText("Logs/FinisherTake/progress.txt","stage="+stage+" recording="+FinisherLabPreview.Recording+" frames="+(FinisherLabPreview.Take?.FrameCount ?? 0)+" "+FinisherLabPreview.Status+"\n");}
            if(stage==1 && FinisherLabPreview.Recording && Time.timeScale<.95f)sawSlow=true;
            if(stage==1 && FinisherLabPreview.Take!=null && FinisherLabPreview.ViewingTake && !FinisherLabPreview.Recording) {
                var take=FinisherLabPreview.Take;Require(sawSlow,"Enabled slow motion must actually slow the world");
                Require(take.FrameCount>60,"Expected a multi-frame 3D take");
                foreach(var phase in new[]{BikeSwordCombat.CombatPhase.Drawing,BikeSwordCombat.CombatPhase.Striking,BikeSwordCombat.CombatPhase.Sheathing,BikeSwordCombat.CombatPhase.Recovering})
                    Require(take.PhaseFrameCount(phase)>=2,"Take must include the complete "+phase+" transition");
                Require(take.ExplosionTime>take.ImpactTime && take.ImpactTime>0,"Hit and explosion must be separate recorded events");
                Require(EditorApplication.isPaused && FinisherClock.EditorCaptureStep==0 && Time.captureDeltaTime==0,"Recording must pause and restore the ordinary clock");
                int hits=FinisherLabPreview.Sword.HitCount;float score=FinisherLabPreview.Gameplay.score;var position=FinisherLabPreview.Player.transform.position;
                float early=take.FirstPhaseTime(BikeSwordCombat.CombatPhase.Striking)+.1f;
                FinisherLabPreview.SeekTake(take.ImpactTime+.05f);Require(take.SelectedWorldSpeed<1,"Recorded world-speed display must reflect the selected slow-motion frame");
                FinisherLabPreview.SeekTake(early);Require(take.RenderedParticleCount==take.SelectedParticleCount,"Particle buffers must restore exactly");
                Require(take.ObjectVisible("Finisher Lab target"),"Seeking before death must restore the enemy visuals");
                string original=take.StateFingerprint();Image("EarlySwing");
                FinisherLabPreview.SeekTake(take.ExplosionTime+.25f);Require(take.RenderedParticleCount==take.SelectedParticleCount,"Explosion particles must restore exactly");Image("ExplosionGameCamera");
                take.CameraAngle(90);take.Zoom(.8f);Image("ExplosionSideCamera");
                string orbit=take.StateFingerprint();take.Orbit(80,10);Require(orbit==take.StateFingerprint(),"Orbit must not advance particles or animation");Image("ExplosionOrbit");
                FinisherLabPreview.SeekTake(early);Require(original==take.StateFingerprint(),"Backward seeking must reproduce bone and particle state");
                take.Step(-1);take.Step(1);Require(original==take.StateFingerprint(),"Backward/forward stepping must restore the same state");
                FinisherLabPreview.SeekTake(take.Duration);Require(!take.ObjectVisible("Finisher Lab target"),"Enemy visuals must disappear after the recorded explosion");
                Require(hits==FinisherLabPreview.Sword.HitCount && score==FinisherLabPreview.Gameplay.score && position==FinisherLabPreview.Player.transform.position,"Seeking must not replay damage, score or physics");
                var tuning=FinisherLabTuning.Capture(FinisherLabPreview.Sword,FinisherLabPreview.Effects);
                FinisherLabPreview.Gameplay.BeginCombatSlowMotion(.2f,2);Require(Time.timeScale<1,"Slow-motion fixture must be active");
                tuning.Get("slowMotionEnabled").number=0;FinisherLabPreview.Apply(tuning);
                Require(Time.timeScale==1 && FinisherLabPreview.Gameplay.CombatSlowMotionRemaining==0,"Master off must cancel slow motion immediately");
                Require(FinisherLabPreview.TakeDirty,"Tuning changes must mark the old take stale");
                stage=2;SessionState.SetInt(Key+"Stage",stage);FinisherLabPreview.Left=false;FinisherLabPreview.RecordTake(tuning);
            } else if(stage==2) {
                if(FinisherLabPreview.Recording)Require(Time.timeScale==1,"Disabled slow motion must stay off during dash, impact and explosion");
                if(FinisherLabPreview.Take!=null && FinisherLabPreview.ViewingTake && !FinisherLabPreview.Recording) {
                    Require(FinisherLabPreview.Sword.LastAttackSide==BikeSwordCombat.AttackSide.Right && FinisherLabPreview.Sword.HitCount==1,"Right take must complete its actual finisher");
                    FinisherLabPreview.SeekTake(FinisherLabPreview.Take.ExplosionTime+.2f);Image("RightNoSlowMotion");
                    Finish(true,"PASS: full left/right 3D finisher takes at fixed capture steps; draw/strike/sheath/recovery frames retained; hit and explosion markers; exact particle-buffer restoration; backward and forward seek/step restores the same bones and particles; expired enemy visuals restored before death; camera orbit/zoom without advancing time; seeking does not apply damage, score or movement; recording clocks restored; stale take warning after edits; master slow-motion off cancels immediately and stays off through the second take. Native captures saved.");
                }
            }
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void Finish(bool success,string message) {
        if(finishing)return;finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key+"Pending",false);
        Directory.CreateDirectory("Logs/FinisherTake");File.WriteAllText("Logs/FinisherTake/result.txt",message);Debug.Log(message);
        EditorApplication.delayCall+=()=>{FinisherLabPreview.ClearTake();EditorApplication.Exit(success ? 0 : 1);};
    }
}
