using System;
using System.IO;
using System.Reflection;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyWheelEffectsPreview {
    private const string Key="CyberBiker.EnemyWheelPreview";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Neutral:IPlayerInput{public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;}
    private static PlayerController player;private static Enemy enemy;private static TireContactParticles fx;private static Transform bike;private static float at;private static double started;private static int stage,lastFrame=-1;private static bool finishing;private static string report="";
    private static ParticleSystem cloud;private static readonly ParticleSystem.Particle[] samples=new ParticleSystem.Particle[256];
    private static uint trackedSeed;private static Vector3 trackedPosition;private static float trackedGap,trackedAlpha;private static int releaseStep;
    static EnemyWheelEffectsPreview(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){Directory.CreateDirectory("Logs/EnemyWheelEffects");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Tick(){if(finishing || !EditorApplication.isPlaying || lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;try{
        Require(EditorApplication.timeSinceStartup-started<90,"Preview timed out at "+stage);
        if(stage==0){
            player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();if(player==null || player.Health==null || Time.timeSinceLevelLoad<.4f)return;
            UnityEngine.Object.FindAnyObjectByType<GameplayManager>().SwitchGameState(GameState.Playing);
            foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>(FindObjectsSortMode.None))director.enabled=false;
            foreach(var traffic in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))traffic.enabled=false;
            foreach(var actor in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))actor.gameObject.SetActive(false);
            foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))car.gameObject.SetActive(false);
            foreach(var collider in player.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            player.GetComponent<TouchInputManager>().enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(player,new Neutral());
            var body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;body.linearVelocity=Vector3.forward*110;player.ConfigureTestingMovement(110,player.ForwardAcceleration,player.SteeringAcceleration,player.SteeringSpeedLimit);
            enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),player.transform.position+Vector3.forward*35+Vector3.right*6,Quaternion.identity).GetComponent<Enemy>();fx=enemy.GetComponent<TireContactParticles>();
            bike=((GameObject)typeof(Enemy).GetField("motoMesh",Private).GetValue(enemy)).transform;
            Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;stage=1;at=Time.time;return;
        }
        if(!enemy.HasInitialized)return;
        enemy.enabled=false;enemy.transform.position=player.transform.position+Vector3.forward*35+Vector3.right*6;enemy.transform.rotation=Quaternion.identity;
        typeof(Enemy).GetField("<DrivingVelocity>k__BackingField",Private).SetValue(enemy,new Vector3(stage>=3 ? 6 : 1.5f,0,110));
        float lean=stage==1 ? 0 : stage==2 ? 5 : stage==3 ? 15 : stage==4 ? 30 : 0;bike.localRotation=Quaternion.Euler(0,0,-lean);Physics.SyncTransforms();
        if(stage==5){ObserveRelease();return;}
        if(Time.time-at<.65f)return;
        if(stage==1){Require(fx.EmittedCount==0,"Straight riding must not constantly emit skidding smoke");report+="PASS straight: no skid emission.\n";}
        else if(stage==2 || stage==3){
            Require(fx.CurrentEmissionRate==0 && fx.CurrentSparkEmissionRate==0,"Gentle weaving and moderate turns must stay clean");
            Capture(stage==2 ? "Gentle" : "Medium",false);Capture(stage==2 ? "GentleClose" : "MediumClose",true);
            report+="PASS "+lean+" degree lean: no continuous skid smoke or sparks.\n";
        }
        else if(stage==4){
            Require(fx.GroundedWheelCount>0 && fx.CurrentEmissionRate>0 && fx.EmittedSparkCount>0,"Smoke and sparks must appear at "+lean+" degree lean");
            string label=stage==2 ? "Gentle" : stage==3 ? "Medium" : "Hard";
            Capture(label,false);Capture(label+"Close",true);
            if(stage==4){var visible=Render();SetSparks(false);var hidden=Render();SetSparks(true);int changed=0;
                var a=visible.GetPixels32();var b=hidden.GetPixels32();for(int i=0;i<a.Length;i++)if(a[i].r>b[i].r+8 || a[i].g>b[i].g+8)changed++;
                UnityEngine.Object.DestroyImmediate(visible);UnityEngine.Object.DestroyImmediate(hidden);
                Require(changed>12,"Enemy sparks must change visible gameplay-camera pixels; count="+changed);report+="PASS visible sparks from riding camera: "+changed+" affected pixels.\n";
            }
            report+=label+": lean="+lean+"; smoke rate="+fx.CurrentEmissionRate.ToString("F1")+"; spark rate="+fx.CurrentSparkEmissionRate.ToString("F1")+"; live particles="+fx.ParticleCount+".\n";
            cloud=enemy.transform.Find("Rear tire smoke").GetComponent<ParticleSystem>();int count=cloud.GetParticles(samples);int youngest=-1;
            for(int i=0;i<count;i++)if(youngest<0 || samples[i].remainingLifetime>samples[youngest].remainingLifetime)youngest=i;
            Require(youngest>=0,"Need a live cloud to inspect its wind dispersal");var tracked=samples[youngest];trackedSeed=tracked.randomSeed;trackedPosition=tracked.position;trackedGap=enemy.transform.position.z-tracked.position.z;trackedAlpha=tracked.GetCurrentColor(cloud).a;
            var playerFx=new SerializedObject(player.GetComponent<TireContactParticles>());Require(playerFx.FindProperty("smokeWindSlowdown").floatValue==0 && playerFx.FindProperty("smokeReleaseTime").floatValue==0,"Existing player smoke profile must remain unchanged");
        }else{Require(fx.CurrentEmissionRate==0 && fx.CurrentSparkEmissionRate==0,"Emission must settle when bike straightens");enemy.gameObject.SetActive(false);Require(fx.ParticleCount==0,"Disable clears all enemy tire particles");Finish(true,report+"PASS settling and disable cleanup.\n");return;}
        stage++;at=Time.time;
    }catch(Exception error){Finish(false,error+"\n"+report);}}
    private static void ObserveRelease(){
        float elapsed=Time.time-at;
        if(releaseStep==0 && elapsed>.045f){Require(fx.CurrentEmissionRate>0 && fx.CurrentSparkEmissionRate==0,"Smoke must taper briefly instead of cutting off; sparks stop with the turn");Capture("ReleaseEarly",false);Capture("ReleaseEarlyClose",true);releaseStep++;}
        if(releaseStep==1 && elapsed>.29f){
            Require(fx.CurrentEmissionRate==0,"Tail emission must end promptly");int count=cloud.GetParticles(samples);int index=-1;for(int i=0;i<count;i++)if(samples[i].randomSeed==trackedSeed){index=i;break;}
            Require(index>=0,"Existing smoke must survive long enough to disperse");var p=samples[index];float gap=enemy.transform.position.z-p.position.z;float alpha=p.GetCurrentColor(cloud).a;
            Require(gap>trackedGap+2,"Airflow must carry the cloud backward relative to the bike");Require(alpha<trackedAlpha*.85f,"The moving cloud must fade progressively");
            report+="PASS wind release: tracked cloud falls "+(gap-trackedGap).ToString("F2")+"m farther behind the bike; alpha "+trackedAlpha+" -> "+alpha+"; existing particle survives emission stop.\n";
            Capture("WindAway",false);Capture("WindAwayClose",true);releaseStep++;
        }
        if(releaseStep==2 && elapsed>.55f){Capture("AlmostGone",false);Capture("AlmostGoneClose",true);releaseStep++;}
        if(elapsed<1)return;
        Require(fx.CurrentEmissionRate==0 && fx.CurrentSparkEmissionRate==0 && fx.ParticleCount==0,"Smoke must finish fading without leaving a permanent trail");Capture("Settled",false);
        enemy.gameObject.SetActive(false);Require(fx.ParticleCount==0,"Disable clears all enemy tire particles");Finish(true,report+"PASS tapered release, progressive airflow/fade, settled particle cleanup and unchanged player profile.\n");
    }
    private static void SetSparks(bool enabled){foreach(var renderer in enemy.GetComponentsInChildren<ParticleSystemRenderer>(true))if(renderer.name.Contains("skid sparks"))renderer.enabled=enabled;}
    private static Texture2D Render(){var camera=Camera.main;var previous=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();return image;}
        finally{camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);}}
    private static void Capture(string name,bool close){var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
        if(close){camera.transform.position=enemy.transform.position+new Vector3(-6,2.7f,-7);camera.transform.LookAt(enemy.transform.position+Vector3.up);}
        var frame=Render();try{File.WriteAllBytes("Logs/EnemyWheelEffects/"+name+".png",frame.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(frame);camera.transform.SetPositionAndRotation(position,rotation);}}
    private static void Finish(bool ok,string message){if(finishing)return;finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Key,false);Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;File.WriteAllText("Logs/EnemyWheelEffects/result.txt",message);Debug.Log(message);EditorApplication.delayCall+=()=>EditorApplication.Exit(ok ? 0 : 1);}
}
