using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CyberBiker.Audio;
using CyberBiker.Player;
using Gameplay;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyArrivalAudioChecks {
    private const string Key="CyberBiker.ArrivalAudioChecks";
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class NeutralInput:IPlayerInput {
        public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;
    }
    private static PlayerController player;
    private static GameplayManager gameplay;
    private static GameAudio audio;
    private static Enemy enemy;
    private static GameObject prefab;
    private static AudioSource engine,pooledEngine;
    private static int stage,pass,lastFrame=-1,pausedSamples,pausedWhooshes;
    private static double began,pauseAt;
    private static float started;
    private static bool finishing,recording,heardBehind,heardAhead,tailFollowed,pauseChecked;
    private static readonly List<float> samples=new List<float>();
    private static double left,right,far,near;
    private static int farCount,nearCount;
    private static string report="";
    static EnemyArrivalAudioChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run(){Directory.CreateDirectory("Logs/EnemyArrivalAudio");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Set(string field,float value)=>typeof(GameAudio).GetField(field,Private).SetValue(audio,value);
    private static void Spawn(float side,bool behind=true) {
        var go=UnityEngine.Object.Instantiate(prefab,player.transform.position+new Vector3(side*6,0,behind ? -45 : 180),Quaternion.identity);
        go.name="Arrival audio test rider";enemy=go.GetComponent<Enemy>();enemy.BeginArrival(player.gameObject,behind,40,.8f);enemy.SetEncounterRole(true,38);
        if(behind)engine=audio.EnemyArrivalSource(enemy);
    }
    private static void BeginPass(float side) {
        samples.Clear();left=right=far=near=0;farCount=nearCount=0;heardBehind=heardAhead=tailFollowed=false;
        Spawn(side);Require(engine!=null,"Rear arrival must rent an engine source");
        Require(engine.spatialBlend==1 && engine.clip.channels==1 && engine.clip.length>20 && engine.dopplerLevel>0 && engine.dopplerLevel<1,"Engine must use the long mono recording and real 3D with restrained Doppler");
        if(pass==1)Require(engine==pooledEngine,"Sequential arrivals should reuse the engine source");
        pooledEngine=engine;started=Time.unscaledTime;stage=2;
    }
    private static void Tick() {
        if(finishing || !EditorApplication.isPlaying)return;
        try {
            if(recording && Time.frameCount!=lastFrame){lastFrame=Time.frameCount;CaptureAudio();}
            if(EditorApplication.timeSinceStartup-began>150)throw new Exception("Audio checks timeout at stage "+stage+" pass "+pass);
            if(stage==0) {
                player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                var world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();
                if(player==null || player.Health==null || world==null || world.GetComponent<TrafficSpawner>()==null || Time.timeSinceLevelLoad<.3f)return;
                gameplay=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();gameplay.SwitchGameState(GameState.Playing);audio=GameAudio.Active;
                world.GetComponent<TrafficSpawner>().enabled=false;
                foreach(var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))e.gameObject.SetActive(false);
                foreach(var c in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
                prefab=(GameObject)typeof(WorldGenerator).GetField("enemy",Private).GetValue(world);
                player.GetComponent<TouchInputManager>().enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(player,new NeutralInput());
                var body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
                body.linearVelocity=Vector3.forward*110;player.ConfigureTestingMovement(110,player.ForwardAcceleration,player.SteeringAcceleration,player.SteeringSpeedLimit);
                Set("masterVolume",1);foreach(string field in new[]{"engineVolume","windVolume","trafficVolume","passWhooshVolume","interfaceVolume","weaponVolume","impactVolume","explosionVolume","jacketVolume"})Set(field,0);
                var config=AudioSettings.GetConfiguration();config.speakerMode=AudioSpeakerMode.Stereo;config.sampleRate=48000;Require(AudioSettings.Reset(config),"Stereo capture configuration failed");
                Require(AudioRenderer.Start(),"Native Unity audio capture must start");recording=true;
                Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;started=Time.unscaledTime;stage=1;
            } else if(stage==1 && Time.unscaledTime-started>.5f)BeginPass(-1);
            else if(stage==2) {
                Require(gameplay.gameState==GameState.Playing,"The rider must remain in normal gameplay during the pass");
                float gap=enemy.transform.position.z-player.transform.position.z;
                Require(Vector3.Distance(engine.transform.position,enemy.transform.position+Vector3.up)<.2f,"Engine source must follow the real motorcycle");
                if(gap< -20 && engine.volume>.1f && engine.isPlaying)heardBehind=true;
                if(gap>10 && engine.isPlaying)heardAhead=true;
                if(pass==0 && !pauseChecked && gap> -28 && gap< -18) {
                    pauseChecked=true;
                    pausedSamples=engine.timeSamples;pausedWhooshes=audio.EnemyOvertakeWhooshCount;
                    gameplay.SwitchGameState(GameState.Pause);pauseAt=EditorApplication.timeSinceStartup;stage=3;
                } else CheckPass(gap);
            } else if(stage==3) {
                Require(!engine.isPlaying && engine.timeSamples==pausedSamples && audio.EnemyOvertakeWhooshCount==pausedWhooshes,"Pause must freeze the engine recording without firing a false pass cue");
                if(EditorApplication.timeSinceStartup-pauseAt>.15){gameplay.SwitchGameState(GameState.Playing);stage=2;}
            } else if(stage==4) {
                if(audio.ActiveEnemyArrivalVoiceCount!=0)return;
                Require(!engine.isPlaying && engine.volume==0,"Settled arrival must smoothly release its engine voice");
                Require(heardBehind && heardAhead && tailFollowed,"Engine must be heard from behind through departure, and whoosh must follow the rider");
                double expected=pass==0 ? left : right,opposite=pass==0 ? right : left;
                Require(expected>opposite*1.2 && expected>.0001,"Rendered stereo must favour the correct passing side: L="+left+" R="+right);
                Require(nearCount>0 && farCount>0 && near/nearCount>far/farCount*1.1,"Actual rendered audio must grow louder at close approach");
                SaveWav(pass==0 ? "RearPassLeft.wav" : "RearPassRight.wav");
                report+="PASS "+(pass==0 ? "left" : "right")+" rear pass: stereo energy L="+left.ToString("F3")+" R="+right.ToString("F3")+", near/far RMS ratio="+Math.Sqrt((near/nearCount)/(far/farCount)).ToString("F2")+".\n";
                enemy.gameObject.SetActive(false);
                if(pass==0){pass=1;BeginPass(1);}else {
                    Require(audio.EnemyArrivalEngineCount==2 && audio.EnemyOvertakeWhooshCount==2,"Each arrival must have one engine start and one pass whoosh, including pause/resume");
                    Spawn(1,false);Require(audio.EnemyArrivalSource(enemy)==null && audio.EnemyOvertakeWhooshCount==2,"Front arrivals must not create false rear-pass audio");
                    enemy.gameObject.SetActive(false);Spawn(1);Set("masterVolume",0);started=Time.unscaledTime;stage=5;
                }
            } else if(stage==5 && Time.unscaledTime-started>.15f) {
                Require(engine.volume==0,"Master volume must control the new arrival engine");
                Set("masterVolume",1);enemy.gameObject.SetActive(false);stage=6;
            } else if(stage==6 && audio.ActiveEnemyArrivalVoiceCount==0) {
                Require(!engine.isPlaying,"Disabled enemy must not leave an orphan engine loop");
                Finish(true,report+"PASS native stereo output captured from real left/right overtakes; recorded R6 engine and whoosh follow enemy position in 3D; one whoosh per crossing, no pause replay, mono clips and restrained Doppler, closer source is louder, paused sample position retained, master volume respected, pooled voice reused, front arrivals silent and disabled actors release audio.\n");
            }
        }catch(Exception error){Finish(false,error.ToString());}
    }
    private static void CheckPass(float gap) {
        if(audio.EnemyOvertakeWhooshCount>pass) {
            var whoosh=audio.LastEnemyOvertakeAudio;
            Require(whoosh!=null && whoosh.spatialBlend==1 && whoosh.dopplerLevel==0,"Whoosh must be a moving 3D point source without a second Doppler shift");
            if(whoosh.isPlaying && Vector3.Distance(whoosh.transform.position,enemy.transform.position)<.2f)tailFollowed=true;
        }
        if(!enemy.IsArriving && audio.EnemyOvertakeWhooshCount==pass+1)stage=4;
    }
    private static void CaptureAudio() {
        int frames=AudioRenderer.GetSampleCountForCaptureFrame();using(var buffer=new NativeArray<float>(frames*2,Allocator.Temp)) {
            Require(AudioRenderer.Render(buffer),"Native audio rendering failed");
            float gap=enemy!=null ? enemy.transform.position.z-player.transform.position.z : -100;
            for(int i=0;i<buffer.Length;i+=2) {
                float l=buffer[i],r=buffer[i+1];samples.Add(l);samples.Add(r);
                if(stage==2 && Mathf.Abs(gap)<18){left+=l*l;right+=r*r;}
                if(stage==2 && gap< -25 && gap> -40){far+=l*l+r*r;farCount++;}
                if(stage==2 && Mathf.Abs(gap)<10){near+=l*l+r*r;nearCount++;}
            }
        }
    }
    private static void SaveWav(string name) {
        using(var writer=new BinaryWriter(File.Create("Logs/EnemyArrivalAudio/"+name))) {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)2);int rate=AudioSettings.outputSampleRate;writer.Write(rate);writer.Write(rate*4);writer.Write((short)4);writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples.Count*2);
            foreach(float sample in samples)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-1,1)*32767));
        }
    }
    private static void Finish(bool success,string message) {
        if(finishing)return;finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key,false);
        if(recording){AudioRenderer.Stop();recording=false;}Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;
        File.WriteAllText("Logs/EnemyArrivalAudio/result.txt",message);Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(success ? 0 : 1);
    }
}
