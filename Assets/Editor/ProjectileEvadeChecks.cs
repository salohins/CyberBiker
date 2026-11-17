using System;
using System.IO;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.Pooling;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ProjectileEvadeChecks {
    private const string Key="CyberBiker.ProjectileEvadeChecks";
    private static PlayerController player;private static PlayerProjectileEvade evade;private static GameplayManager manager;
    private static Enemy source;private static Bullet shot;private static Collider shape;private static GameObject prefab;
    private static int stage,lastFrame=-1;private static float at,clock,health,warningAt,firstShotAt;private static Vector3 position;
    private static double began;private static bool finishing;private static string report="";
    static ProjectileEvadeChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){Directory.CreateDirectory("Logs/ProjectileEvade");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Next(){stage++;at=clock;File.AppendAllText("Logs/ProjectileEvade/Progress.txt",stage+"\n");}
    private static Bullet Spawn(float seconds,GameObject owner=null){
        var origin=shape.bounds.center+Vector3.forward*(shape.bounds.extents.z+30*seconds);
        var go=PrefabPool.Rent(prefab,origin,Quaternion.LookRotation(-Vector3.forward));var result=go.GetComponent<Bullet>();result.Initialize(owner!=null ? owner : source.gameObject,10);result.bulletSpeed=30;Physics.SyncTransforms();return result;
    }
    private static void Release(){if(shot!=null && shot.gameObject.activeSelf)PrefabPool.Release(shot.gameObject);shot=null;}
    private static void Tick(){if(finishing || !EditorApplication.isPlaying)return;try{
        Require(EditorApplication.timeSinceStartup-began<110,"Evade check timed out at stage "+stage);
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;clock+=FinisherClock.UnscaledDeltaTime;
        if(stage==0){
            player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();if(player==null || player.Health==null || Time.timeSinceLevelLoad<.5f)return;
            manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();manager.SwitchGameState(GameState.Playing);manager.ProjectileCombat.enemyTrajectoryGuides=false;manager.SetCloseDodgePreview(new CloseDodgeSettings{enabled=false});
            foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>(FindObjectsSortMode.None))director.enabled=false;
            foreach(var traffic in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))traffic.enabled=false;
            foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
            foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))car.gameObject.SetActive(false);
            player.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeAll;player.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;
            foreach(var candidate in player.GetComponentsInChildren<Collider>())if(candidate.enabled && !candidate.isTrigger && (shape==null || candidate.bounds.size.y>shape.bounds.size.y))shape=candidate;
            Require(shape!=null,"Need live player collision shape");position=player.transform.position;health=player.Health.Current;
            evade=player.GetComponent<PlayerProjectileEvade>();Require(evade!=null,"Player prefab must have evade component");
            Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;
            prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bullet.prefab");
            source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),position+Vector3.forward*60,Quaternion.identity).GetComponent<Enemy>();Next();
        }else if(stage==1 && source.HasInitialized){
            source.enabled=false;source.StopAllCoroutines();shot=Spawn(.7f);evade.RefreshThreat();
            File.WriteAllText("Logs/ProjectileEvade/Fixture.txt","Player collider "+shape.name+" "+shape.bounds+" shot "+shot.transform.position+" impact "+evade.ImpactSeconds);
            Require(evade.Threat==shot && !evade.Ready,"Closing projectile must warn before readiness");Require(!evade.TryEvade(),"Early presses must not queue a dodge");
            RequireOriginalRendering(shot);Require(shot.GetComponent<BulletAirWake>().Visible,"Hostile bullets need the separate air wake");
            var camera=Camera.main;var savedPosition=camera.transform.position;var savedRotation=camera.transform.rotation;
            camera.transform.position=shot.transform.position+new Vector3(3,1,-4);camera.transform.LookAt(shot.transform.position);
            Capture("OriginalProjectile");
            camera.transform.position=shot.transform.position+shot.transform.forward*8;camera.transform.LookAt(shot.transform.position);Capture("ProjectileHeadOn");
            camera.transform.SetPositionAndRotation(savedPosition,savedRotation);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=Vector3.Lerp(shot.transform.position,shape.bounds.center,.5f);wall.transform.localScale=Vector3.one*2;Physics.SyncTransforms();evade.RefreshThreat();Require(evade.Threat==null,"A nearer wall must suppress impossible threat warnings");UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();
            report+="PASS real incoming bullet prediction, early rejection, original prefab rendering and obstacle filtering.\n";Next();
        }else if(stage==2){
            evade.RefreshThreat();if(evade.Threat!=shot || evade.ImpactSeconds>.26f)return;
            Require(evade.ImpactSeconds>manager.ProjectileCombat.PerfectWindow,"Normal evade fixture needs non-perfect timing");Capture("Incoming");
            var camera=Camera.main;var savedPosition=camera.transform.position;var savedRotation=camera.transform.rotation;camera.transform.position=shot.transform.position+new Vector3(3,1,-4);camera.transform.LookAt(shot.transform.position);Capture("BulletAirWake");camera.transform.SetPositionAndRotation(savedPosition,savedRotation);
            Require(evade.TryEvade(),"Timed evade must still work without the HUD");
            Require(evade.EvadeCount==1 && evade.PerfectCount==0 && shot.IgnoresPlayer(player),"Timed input must perform one normal evade");Next();
        }else if(stage==3 && clock-at>.08f){
            Require(evade.PoseAmount>.3f,"Evade must produce a visible smooth rider pose");Require(Vector3.Distance(position,player.transform.position)<.001f,"Evade must not steer/teleport the motorcycle");Capture("BodyDodge");Next();
        }else if(stage==4 && clock-at>.7f){
            Require(player.Health.Current==health,"Evaded bullet must pass through without damaging player");Require(evade.PoseAmount<.001f,"Upper body must recover cleanly");Release();
            report+="PASS timed evade without HUD, visible rider movement, unchanged bike position, no bullet damage and smooth pose recovery.\n";
            shot=Spawn(.085f);evade.RefreshThreat();Require(evade.Ready && evade.ImpactSeconds<=manager.ProjectileCombat.PerfectWindow,"Perfect fixture must be imminent");
            Require(evade.TryEvade() && evade.PerfectCount==1,"Last-moment input must earn perfect evade");Require(manager.CombatSlowMotionRemaining>0 && Time.timeScale<1,"Perfect evade must trigger shared slow-motion clock");
            Require(!evade.TryEvade(),"Repeated press must not retrigger the same bullet");Capture("Perfect");Next();
        }else if(stage==5 && clock-at>.9f){
            Require(Mathf.Abs(Time.timeScale-1)<.001f && manager.CombatSlowMotionRemaining<=0,"Perfect slow motion must recover to normal");Require(player.Health.Current==health,"Perfect evade must remain safe");Release();
            report+="PASS perfect timing, one reward per burst and smooth return to full speed.\n";shot=Spawn(.09f);Next();
        }else if(stage==6 && clock-at>.4f){
            Require(player.Health.Current==health-10,"Ignoring the timed button must retain actual projectile damage");Release();
            report+="PASS no input still takes original bullet damage.\n";
            shot=Spawn(.7f,player.gameObject);evade.RefreshThreat();Require(evade.Threat==null,"Friendly shots must never warn");RequireOriginalRendering(shot);Require(!shot.GetComponent<BulletAirWake>().Visible,"Pooled player bullets must not retain enemy air wake");Require(!shot.IgnoresPlayer(player),"Pool lease must clear previous dodge exemption");Release();
            shot=Spawn(.7f);manager.SwitchGameState(GameState.Pause);evade.RefreshThreat();Require(!evade.Ready && !evade.TryEvade(),"Paused input must not evade");Next();
        }else if(stage==7 && clock-at>.1f){
            Require(GameObject.Find("Projectile evade HUD")==null,"Evade HUD must not be created");manager.SwitchGameState(GameState.Playing);Release();
            shot=Spawn(.1f);manager.BeginCombatSlowMotion(.5f,.5f,.1f);int count=manager.CombatSlowMotionCount;
            Require(evade.TryEvade(),"Evade remains available during existing combat slowdown");Require(manager.CombatSlowMotionCount==count,"Evade must not replace existing finisher slowdown");Release();manager.CancelCombatSlowMotion();
            report+="PASS pooled friendly/hostile reset, pause input and no evade HUD and existing slow-motion priority.\n";Next();
        }else if(stage==8 && clock-at>.6f){
            shot=Spawn(.1f);evade.RefreshThreat();Require(evade.Threat==shot,"Natural dodge must begin on a genuine collision course");
            player.transform.position=position+Vector3.right*2;player.GetComponent<Rigidbody>().position=player.transform.position;Physics.SyncTransforms();evade.RefreshThreat();
            Require(evade.Threat==null,"Steering clear must immediately remove the threat");Next();
        }else if(stage==9 && clock-at>.45f){
            Require(evade.PerfectCount==2 && player.Health.Current==health-10,"A last-moment steering dodge must earn perfect feedback without damage");Release();
            report+="PASS last-moment steering dodge earns a perfect reward after a real threatened trajectory becomes a close pass.\n";
            player.transform.position=position;player.GetComponent<Rigidbody>().position=position;Physics.SyncTransforms();
            shot=Spawn(.1f);shot.transform.position+=Vector3.right*3.5f;Physics.SyncTransforms();evade.RefreshThreat();Require(evade.Threat==null,"A safe-lane projectile must not threaten the player");Next();
        }else if(stage==10 && clock-at>.5f){
            Require(evade.PerfectCount==2,"Safe-lane passes must never earn perfect dodge rewards");Release();
            UnityEngine.Object.DestroyImmediate(source.GetComponent<EnemyGunEffects>());typeof(Enemy).GetField("gunEffects",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(source,null);
            source.transform.position=position+new Vector3(5,0,20);Physics.SyncTransforms();
            var arms=source.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();foreach(var layer in arms.layers)layer.rig.weight=1;
            var torso=(Transform)typeof(Enemy).GetField("bodyRotation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(source);torso.rotation=Quaternion.LookRotation(player.transform.position-torso.position);
            source.GetComponent<Animator>().SetBool("Attack",true);source.GetComponent<Animator>().SetBool("GunReady",true);
            typeof(Enemy).GetField("armed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(source,true);Next();
        }else if(stage==11 && clock-at>.15f){
            var warning=source.GetComponent<EnemyShotWarning>();Require(warning.Visible && !warning.IsWarning && warning.CurrentColor.r>.9f && warning.CurrentColor.g>.9f,"Armed enemy lasers must remain white before the windup");
            Capture("EnemyWhiteGameplay");CaptureLaser("EnemyWhiteLasers");
            manager.ProjectileCombat.enemyAimLaserPlayerFadeFraction=0;
            typeof(EnemyShotWarning).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(warning,null);CaptureLaser("EnemyWhiteNoEndFade");
            manager.ProjectileCombat.enemyAimLaserPlayerFadeFraction=.25f;
            typeof(EnemyShotWarning).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(warning,null);
            Require(RenderDifference("EnemyWhiteLasers","EnemyWhiteNoEndFade")>20,"Player-end gradient must visibly fade the beam in the rendered scene");warningAt=clock;
            typeof(Enemy).GetMethod("Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(source,null);Require(source.FiredShotCount==0,"Shot anticipation must precede the actual projectile");Next();
        }else if(stage==12 && clock-at>.15f){
            var warning=source.GetComponent<EnemyShotWarning>();Require(source.FiredShotCount==0 && warning.Visible && warning.IsWarning && warning.CurrentColor.g<.1f,"Upcoming shot must turn its permanent white beam red early");
            Require(warning.WarningRemaining>.3f,"Red warning must last longer than the old capped windup");
            var redLine=source.transform.Find("Enemy shot aim laser").GetComponent<LineRenderer>();var whiteLine=source.transform.Find("Enemy shot aim laser 1").GetComponent<LineRenderer>();
            Require(whiteLine.startWidth<redLine.startWidth*.8f,"White idle beam must be distinctly thinner than red warning");Require(redLine.sharedMaterial.shader.name=="CyberBiker/Enemy Aim Laser","Enemy beams must use their own non-additive shader");Capture("EnemyRedGameplay");CaptureLaser("EnemyRedWarning");Next();
        }else if(stage==13 && source.FiredShotCount>0){
            Require(clock-warningAt>=manager.ProjectileCombat.firingWarning-.025f,"First shot must wait for the full configured warning");firstShotAt=clock;
            var warning=source.GetComponent<EnemyShotWarning>();Require(warning.Visible && !warning.IsWarning && warning.CurrentColor.g>.9f,"Laser must return to white and stay visible after firing");
            var effects=source.GetComponent<EnemyGunEffects>();Require(effects!=null && effects.ShotCount==1 && effects.VisibleFlashCount==1 && effects.SmokeCount>0,"Actual enemy shot must flash and emit smoke");
            Require(source.transform.Find("Enemy shot aim laser").GetComponent<LineRenderer>().startColor.a<.3f,"Firing gun laser must briefly dim after its actual shot");
            CaptureEnemyEffect("EnemyMuzzleShot",effects.LastMuzzlePosition);Next();
        }else if(stage==14 && clock-firstShotAt>manager.ProjectileCombat.enemyShotRecovery+.1f){
            var warning=source.GetComponent<EnemyShotWarning>();Require(source.FiredShotCount==1 && warning.IsWarning && warning.Visible,"Right gun must get its own full red warning");
            Require(Vector3.Distance(warning.Origin,source.GunMuzzle(1).position)<.02f,"Red warning must come from the gun about to fire");Next();
        }else if(stage==15 && source.FiredShotCount>=2){
            Require(clock-firstShotAt>=manager.ProjectileCombat.firingWarning+manager.ProjectileCombat.enemyShotRecovery-.03f,"Old fast shot interval must not truncate the second warning");
            source.StopAllCoroutines();source.GetComponent<EnemyShotWarning>().Cancel();
            var effects=source.GetComponent<EnemyGunEffects>();Require(effects.ShotCount==2 && source.transform.Find("Enemy muzzle flash 1").gameObject.activeSelf,"Second gun must still trigger its muzzle blast");Next();
        }else if(stage==16 && clock-at>.25f){
            var warning=source.GetComponent<EnemyShotWarning>();Require(warning.Visible && !warning.IsWarning && warning.CurrentColor.g>.9f,"White laser must persist between shots");
            Require(source.transform.Find("Enemy shot aim laser 1").GetComponent<LineRenderer>().startColor.a>.8f,"Laser must smoothly recover after the brief shot dimming");
            var effects=source.GetComponent<EnemyGunEffects>();Require(effects.VisibleFlashCount==0 && effects.SmokeCount>0,"Flash must finish while smoke lingers");CaptureEnemyEffect("EnemyGunSmoke",effects.LastMuzzlePosition);
            source.SetEncounterRole(true,20);Next();
        }else if(stage==17 && clock-at>.08f){
            Require(!source.GetComponent<EnemyShotWarning>().Visible,"Queued enemies must not project active fighting lasers");source.SetEncounterRole(false,20);
            report+="PASS permanent bright white gun lasers, full red warning before both shots, white recovery, player-end gradient, brief post-shot dimming/recovery, cadence expansion, queued suppression, lazy muzzle reconnection and gun smoke.\n";
            var sword=player.GetComponent<BikeSwordCombat>();sword.Launch();
            var phase=typeof(BikeSwordCombat).GetProperty("Phase");phase.SetValue(sword,BikeSwordCombat.CombatPhase.Striking);
            shot=Spawn(.08f);evade.RefreshThreat();Require(!evade.TryEvade(),"Sword strike must take priority over evade");Release();sword.CancelForAim();
            report+="PASS sword strike prevents competing evade animation.\n";Finish(true,report);
        }
    }catch(Exception error){Finish(false,error+"\n"+report);}}
    private static int RenderDifference(string first,string second){
        var a=new Texture2D(2,2);var b=new Texture2D(2,2);
        try{a.LoadImage(File.ReadAllBytes("Logs/ProjectileEvade/"+first+".png"));b.LoadImage(File.ReadAllBytes("Logs/ProjectileEvade/"+second+".png"));var one=a.GetPixels32();var two=b.GetPixels32();int changed=0;for(int i=0;i<one.Length;i++)if(Mathf.Abs(one[i].r-two[i].r)+Mathf.Abs(one[i].g-two[i].g)+Mathf.Abs(one[i].b-two[i].b)>20)changed++;return changed;}
        finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
    }
    private static void CaptureEnemyEffect(string name,Vector3 point){
        var camera=Camera.main;var saved=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        try{camera.transform.position=point+new Vector3(4,2,-5);camera.transform.LookAt(point);camera.fieldOfView=45;Capture(name);}
        finally{camera.transform.SetPositionAndRotation(saved,rotation);camera.fieldOfView=fov;}
    }
    private static void CaptureLaser(string name){
        var warning=source.GetComponent<EnemyShotWarning>();var camera=Camera.main;var saved=camera.transform.position;var rotation=camera.transform.rotation;
        try{camera.transform.position=warning.Origin+new Vector3(5,3,-5);camera.transform.LookAt(Vector3.Lerp(warning.Origin,warning.EndPoint,.2f));Capture(name);}
        finally{camera.transform.SetPositionAndRotation(saved,rotation);}
    }
    private static void RequireOriginalRendering(Bullet value){
        var actual=new System.Collections.Generic.List<Renderer>(value.GetComponentsInChildren<Renderer>(true));actual.RemoveAll(renderer=>renderer.name.StartsWith("Bullet airflow "));var original=prefab.GetComponentsInChildren<Renderer>(true);
        Require(actual.Count==original.Length && value.transform.Find("Outlined hostile bullet")==null,"Projectile must use only its original prefab renderers");
        for(int i=0;i<actual.Count;i++){
            Require(actual[i].enabled==original[i].enabled && actual[i].sharedMaterial==original[i].sharedMaterial,"Original projectile renderer/material must be unchanged");
            if(actual[i] is LineRenderer line && original[i] is LineRenderer sourceLine)Require(line.widthMultiplier==sourceLine.widthMultiplier,"Original tracer width must be unchanged");
        }
    }
    private static void Capture(string name){
        var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.aspect=1280f/720;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/ProjectileEvade/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=old;camera.ResetAspect();RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);}
    }
    private static void Finish(bool ok,string message){if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;File.WriteAllText("Logs/ProjectileEvade/Result.txt",message);EditorApplication.delayCall+=()=>EditorApplication.Exit(ok ? 0 : 1);}
}
