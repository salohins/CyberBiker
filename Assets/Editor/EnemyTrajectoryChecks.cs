using System;
using System.IO;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.Pooling;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyTrajectoryChecks {
    private const string Key="CyberBiker.EnemyTrajectoryChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static PlayerController player;private static Enemy enemy;private static GameplayManager manager;
    private static EnemyShotTrajectory guide;private static Bullet bullet;private static GameObject obstacle;
    private static Vector3 origin,end;private static uint generation;private static int stage,lastFrame=-1;
    private static float at;private static double began;private static bool finishing;private static string report="";
    private static EnemyTrajectoryMotionProbe motion;
    private static bool capturedMotion;
    private static Vector3 colorPlayerPosition;
    private static CyberBiker.Player.HelmetThreatSense sense;
    private static bool helmetPauseChecked;
    private static float helmetPausedAmount;
    private static bool farPulseChecked,nearPulseChecked;
    private static float farPulse,nearPulse;
    static EnemyTrajectoryChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){Directory.CreateDirectory("Logs/EnemyTrajectory");File.WriteAllText("Logs/EnemyTrajectory/Result.txt","RUNNING\n");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Next(){stage++;at=Time.time;}
    private static void Invoke(string method,params object[] args){typeof(Enemy).GetMethod(method,Private).Invoke(enemy,args);}
    private static void Arm(){typeof(Enemy).GetField("armed",Private).SetValue(enemy,true);}
    private static void MovePlayer(Vector3 point){player.transform.position=point;player.GetComponent<Rigidbody>().position=point;Physics.SyncTransforms();}
    private static void Tick(){if(finishing || !EditorApplication.isPlaying)return;try{
        Require(EditorApplication.timeSinceStartup-began<100,"Trajectory check timed out at stage "+stage);
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(stage==0){
            player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();if(player==null || player.Health==null || Time.timeSinceLevelLoad<.5f)return;
            manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();Require(manager.ProjectileCombat.enemyTrajectoryGuides,"Shot trajectory experiment must be enabled by default");manager.SwitchGameState(GameState.Playing);
            foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>(FindObjectsSortMode.None))director.enabled=false;
            foreach(var spawner in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))spawner.enabled=false;
            foreach(var actor in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))actor.gameObject.SetActive(false);
            foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))car.gameObject.SetActive(false);
            player.GetComponent<TouchInputManager>().enabled=false;player.GetComponent<CyberBiker.Player.PlayerProjectileEvade>().enabled=false;
            sense=player.GetComponent<CyberBiker.Player.HelmetThreatSense>();Require(sense!=null && sense.Head!=null,"Helmet sense must auto-bind to the actual player head bone");
            player.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeAll;player.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;
            Time.captureDeltaTime=1f/60;CyberBiker.Player.FinisherClock.EditorCaptureStep=1f/60;
            enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),player.transform.position+new Vector3(4,0,20),Quaternion.identity).GetComponent<Enemy>();Next();
        }else if(stage==1 && enemy.HasInitialized){
            enemy.enabled=false;enemy.StopAllCoroutines();Arm();Invoke("Shoot");guide=enemy.PendingTrajectory;
            Require(guide!=null && guide.IsPreview && !guide.Visible && enemy.FiredShotCount==0,"Each shot must prepare its trajectory before firing and keep it hidden until launch");Next();
        }else if(stage==2 && Time.time-at>.15f){
            Require(!sense.Threatened && !sense.Visible && sense.Amount==0,"A pre-fire hidden guide must not trigger helmet sense");
            Require(!enemy.GetComponent<EnemyShotWarning>().Visible,"Permanent gun lasers must stay hidden in trajectory mode");
            Require(Vector3.Distance(guide.EndPoint,player.transform.position+Vector3.up*3)<.02f,"Windup line must show the enemy's current aim");Capture("BeforeShot");Next();
        }else if(stage==3 && enemy.FiredShotCount==1){
            enemy.StopAllCoroutines();Require(guide.IsInFlight,"Guide must remain active after the bullet launches");bullet=guide.Projectile;Require(bullet!=null && bullet.IsHostile,"Guide must bind the actual spawned bullet");
            origin=guide.Origin;end=guide.EndPoint;generation=guide.Generation;
            string geometry="Natural enemy shot: root="+player.transform.position+", head="+sense.Head.position+", endpoint="+end;foreach(var shape in player.GetComponentsInChildren<Collider>())if(shape.enabled && !shape.isTrigger)geometry+="; solid "+shape.name+" "+shape.bounds;File.AppendAllText("Logs/EnemyTrajectory/Geometry.txt",geometry+"\n");
            Require((bool)typeof(CyberBiker.Player.HelmetThreatSense).GetMethod("UnderIncomingPath",Private).Invoke(sense,null),"The enemy's unmodified shot at the rider must activate helmet threat detection");
            for(int tick=0;tick<4;tick++)typeof(CyberBiker.Player.HelmetThreatSense).GetMethod("LateUpdate",Private).Invoke(sense,null);
            Require(sense.Visible && sense.Amount>.8f,"Natural head-height shots must render helmet warning strokes");Capture("NaturalShotHelmet");
            report+="PASS natural enemy shot at the rider's head activates visible helmet strokes even above the low traffic collision boxes.\n";
            var direction=(end-origin).normalized;Require(Vector3.Cross(bullet.transform.position-origin,direction).magnitude<.005f && Vector3.Dot(bullet.transform.forward,direction)>.999f,"Bullet must fly exactly along the committed line");
            enemy.transform.position+=Vector3.right*4;MovePlayer(player.transform.position+Vector3.left*5);Next();
        }else if(stage==4 && Time.time-at>.05f){
            Require(guide.IsInFlight && Vector3.Distance(guide.Origin,origin)<.001f && Vector3.Distance(guide.EndPoint,end)<.001f,"Fired trajectory must not follow later enemy/player movement");Capture("InFlight");Next();
        }else if(stage==5 && !guide.IsInFlight){
            Require(!guide.Visible && bullet!=null && bullet.IsHostile,"A missed shot line must retire at its endpoint without shortening the bullet lifetime");
            report+="PASS hidden pre-fire trajectory, hidden permanent lasers, exact bullet path, frozen flight line and endpoint cleanup without altering bullet lifetime.\n";
            Invoke("BeginShotWarning",1,.6f);var reused=enemy.PendingTrajectory;Require(reused==guide && reused.Generation>generation,"Completed guides must be pooled and reused");guide=reused;PrefabPool.Release(bullet.gameObject);Next();
        }else if(stage==6 && Time.time-at>.05f){
            Require(guide.IsPreview && !guide.Visible,"Returning an old bullet must not retire a reused guide");enemy.SetEncounterRole(true,20);Next();
        }else if(stage==7 && Time.time-at>.05f){
            Require(!guide.Visible && !guide.IsPreview,"Queued/cancelled enemy must release its pending preview");enemy.SetEncounterRole(false,20);Arm();Invoke("GunShoot",0);guide=FindFlight();Require(guide!=null,"Immediate actual shot must also bind a guide");bullet=guide.Projectile;
            obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.position=guide.Origin+(guide.EndPoint-guide.Origin).normalized*3;obstacle.transform.localScale=Vector3.one;Physics.SyncTransforms();Next();
        }else if(stage==8 && bullet!=null && !bullet.IsHostile){
            if(guide.Visible)return;Require(!guide.IsInFlight,"Impact must retire the line with its bullet");UnityEngine.Object.DestroyImmediate(obstacle);obstacle=null;
            report+="PASS pooled reuse/stale-bullet safety, cancelled windup cleanup and obstacle-impact cleanup.\n";
            Invoke("BeginShotWarning",0,.6f);guide=enemy.PendingTrajectory;manager.SwitchGameState(GameState.Pause);Next();
        }else if(stage==9){
            Require(!guide.Visible,"Pause must hide the trajectory");manager.SwitchGameState(GameState.Playing);Next();
        }else if(stage==10 && Time.time-at>.05f){
            Require(guide.IsPreview && !guide.Visible,"Resume must preserve the pending preview while keeping its line hidden");manager.ProjectileCombat.enemyTrajectoryGuides=false;Next();
        }else if(stage==11 && Time.time-at>.05f){
            Require(!guide.Visible && enemy.GetComponent<EnemyShotWarning>().Visible,"Experiment switch must release guides and restore the old gun lasers");manager.ProjectileCombat.enemyTrajectoryGuides=true;Invoke("GunShoot",1);guide=FindFlight();Require(guide!=null,"New shot needs its own guide after switching back");bullet=guide.Projectile;
            UnityEngine.Object.Destroy(enemy.gameObject);MovePlayer(player.transform.position+Vector3.right*8);Next();
        }else if(stage==12 && Time.time-at>.05f){
            Require(guide.IsInFlight && guide.Visible && bullet!=null && bullet.IsHostile,"Destroying the shooter must not erase an already flying bullet's path");Next();
        }else if(stage==13 && !guide.IsInFlight){
            report+="PASS pause/resume, reversible experiment toggle and in-flight guide survival after shooter destruction.\n";
            MovePlayer(new Vector3(0,player.transform.position.y,player.transform.position.z));
            enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),player.transform.position+new Vector3(4,0,80),Quaternion.identity).GetComponent<Enemy>();Next();
        }else if(stage==14 && enemy.HasInitialized){
            enemy.enabled=false;enemy.StopAllCoroutines();Arm();Invoke("GunShoot",0);guide=FindFlight();
            Require(guide!=null && guide.Projectile.transform.parent.GetComponent<BulletWorld>()!=null,"Moving test must use the real BulletLocal parent");
            bullet=guide.Projectile;MovePlayer(player.transform.position+Vector3.left*5);
            motion=player.gameObject.AddComponent<EnemyTrajectoryMotionProbe>();motion.Begin(player.GetComponent<Rigidbody>(),enemy,guide);Next();
        }else if(stage==15){
            Require(motion.Failure==null,motion.Failure);
            if(motion.Samples>=20 && !capturedMotion){Capture("MovingShot");capturedMotion=true;}
            if(guide.IsInFlight)return;
            Require(motion.Samples>=30 && motion.Travel>40,"Moving sample insufficient: frames="+motion.Samples+", travel="+motion.Travel+", bullet active="+(bullet!=null && bullet.IsHostile));
            report+="PASS moving BulletLocal at 70–110 m/s with steering and independent shooter motion: "+motion.Samples+" rendered frames, "+motion.Travel+"m parent travel, maximum bullet/line error "+motion.MaximumError+"m.\n";
            if(bullet!=null && bullet.IsHostile)PrefabPool.Release(bullet.gameObject);
            MovePlayer(new Vector3(0,player.transform.position.y,player.transform.position.z));enemy.transform.position=player.transform.position+new Vector3(4,0,80);Arm();Invoke("GunShoot",1);guide=FindFlight();
            Require(guide!=null,"Slow-motion test needs its actual shot");MovePlayer(player.transform.position+Vector3.left*5);
            motion.Begin(player.GetComponent<Rigidbody>(),enemy,guide);manager.BeginCombatSlowMotion(.35f,8,0);Next();
        }else if(stage==16){
            Require(motion.Failure==null,motion.Failure);if(guide.IsInFlight)return;
            Require(motion.Samples>=40 && motion.Travel>40,"Slow-motion test must retain enough moving rendered frames");
            report+="PASS slow-motion riding: "+motion.Samples+" rendered frames, "+motion.Travel+"m parent travel, maximum bullet/line error "+motion.MaximumError+"m.\n";
            motion.enabled=false;manager.CancelCombatSlowMotion();MovePlayer(new Vector3(0,player.transform.position.y,player.transform.position.z));colorPlayerPosition=player.transform.position;
            enemy.transform.position=player.transform.position+new Vector3(4,0,20);
            Collider body=null;foreach(var shape in player.GetComponentsInChildren<Collider>())if(shape.enabled && !shape.isTrigger && (body==null || shape.bounds.size.y>body.bounds.size.y))body=shape;
            var gun=enemy.GunMuzzle(0);var aim=body.bounds.center;var request=new ShotRequest(gun.position,aim-gun.position,enemy.gameObject);
            guide=EnemyShotTrajectory.Create(enemy,gun,player.transform,.6f);
            var weapon=new ProjectileWeapon(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bullet.prefab"),.01f,1,enemy.bulletParent.transform);
            Require(weapon.TryFireImmediate(request,Time.time),"Color check must use a real projectile");bullet=weapon.LastProjectile;bullet.bulletSpeed=0;guide.Commit(request,aim,bullet);
            Require(guide.Visible && guide.CurrentColor.g>guide.CurrentColor.r,"In-flight path through the player must remain green");Capture("OnPathGreen");Next();
        }else if(stage==17 && Time.time-at>.08f){
            if(!helmetPauseChecked){
                Require(sense.Threatened && sense.Visible && sense.Amount>.8f,"An in-flight trajectory through the player must show the green helmet strokes");
                Require(Vector3.Distance(sense.Center,sense.Head.position+Vector3.up*manager.ProjectileCombat.helmetSenseHeight-Camera.main.transform.forward*.08f)<.01f,"Halo must follow the animated head position");Capture("HelmetDanger");
                helmetPausedAmount=sense.Amount;manager.SwitchGameState(GameState.Pause);helmetPauseChecked=true;return;
            }
            Require(!sense.Visible && Mathf.Abs(sense.Amount-helmetPausedAmount)<.001f,"Pause must hide and freeze the helmet halo");manager.SwitchGameState(GameState.Playing);
            MovePlayer(colorPlayerPosition+Vector3.left*5);Next();
        }else if(stage==18 && Time.time-at>.03f){
            Require(guide.IsInFlight && guide.Visible && guide.CurrentColor.g>guide.CurrentColor.r,"Steering clear must turn the same committed line green");Capture("SafeGreen");MovePlayer(colorPlayerPosition);Next();
            Require(!sense.Threatened && sense.Amount>0 && sense.Amount<1,"Steering clear must smoothly fade the helmet strokes");
        }else if(stage==19 && Time.time-at>.03f){
            Require(sense.Threatened && sense.Visible,"Moving back into the same path must restore the helmet warning");
            Require(guide.Visible && guide.CurrentColor.g>guide.CurrentColor.r,"Moving back into the same shot path must keep the green line visible");bullet.Evade(player);Next();
        }else if(stage==20 && Time.time-at>.03f){
            Require(!sense.Threatened,"An evaded/ignored projectile must stop threatening the helmet");
            Require(guide.Visible && guide.CurrentColor.g>guide.CurrentColor.r,"A shot that already ignores the player after evasion must be safe green");PrefabPool.Release(bullet.gameObject);
            report+="PASS flight-only green line stays visible on the player path, while steering out and back in, and after evasion.\n";Next();
        }else if(stage==21 && Time.time-at>.2f){
            Require(!sense.Visible && !sense.Threatened && sense.Amount==0,"Helmet trails must finish fading after the last threat is gone");
            Require(EnemyShotTrajectory.InFlight.Count==0,"Returned projectiles must leave no stale threat registrations");Capture("HelmetClear");
            report+="PASS helmet sense: real head attachment, no pre-fire warning, animated green strokes on path, smooth fade on steering clear, retrigger on re-entry, pause hide/freeze, evasion cleanup and empty flight registry.\n";
            enemy.transform.position=player.transform.position+new Vector3(4,0,60);
            Collider body=null;foreach(var shape in player.GetComponentsInChildren<Collider>())if(shape.enabled && !shape.isTrigger && (body==null || shape.bounds.size.y>body.bounds.size.y))body=shape;
            var gun=enemy.GunMuzzle(0);var aim=body.bounds.center;var request=new ShotRequest(gun.position,aim-gun.position,enemy.gameObject);
            guide=EnemyShotTrajectory.Create(enemy,gun,player.transform,.6f);
            var weapon=new ProjectileWeapon(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bullet.prefab"),.01f,1,enemy.bulletParent.transform);
            Require(weapon.TryFireImmediate(request,Time.time),"Pulse check needs an actual approaching projectile");bullet=weapon.LastProjectile;guide.Commit(request,aim,bullet);Next();
        }else if(stage==22){
            if(sense.Threatened && sense.NearestThreatDistance>35){
                if(!farPulseChecked)Capture("HelmetFarPulse");farPulseChecked=true;farPulse=sense.PulseFrequency;
                Require(Mathf.Abs(farPulse-manager.ProjectileCombat.helmetSenseFarPulse)<.01f,"Distant threats must use the slow pulse setting");
            }
            if(sense.Threatened && sense.NearestThreatDistance<5){
                if(!nearPulseChecked)Capture("HelmetUrgentPulse");nearPulseChecked=true;nearPulse=Mathf.Max(nearPulse,sense.PulseFrequency);
                Require(sense.Urgency>.9f && nearPulse>farPulse*2,"Pulse must become visibly faster as the actual bullet approaches the player");
            }
            if(bullet!=null && bullet.IsHostile)return;
            Require(farPulseChecked && nearPulseChecked,"Pulse regression must observe both distant and imminent phases of the same moving shot");
            report+="PASS approaching real bullet: far pulse "+farPulse+"Hz, near pulse "+nearPulse+"Hz; independent brighter helmet material; green trajectory unchanged.\n";Finish(true,report);
        }
    }catch(Exception error){Finish(false,error+"\n"+report);}}
    private static EnemyShotTrajectory FindFlight(){foreach(var item in UnityEngine.Object.FindObjectsByType<EnemyShotTrajectory>(FindObjectsSortMode.None))if(item.IsInFlight)return item;return null;}
    private static void Capture(string name){
        var camera=Camera.main;var previous=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.aspect=1280f/720;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Logs/EnemyTrajectory/"+name+".png",texture.EncodeToPNG());}
        finally{camera.targetTexture=previous;camera.ResetAspect();RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}
    }
    private static void Finish(bool ok,string message){if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Time.captureDeltaTime=0;CyberBiker.Player.FinisherClock.EditorCaptureStep=0;File.WriteAllText("Logs/EnemyTrajectory/Result.txt",message);EditorApplication.delayCall+=()=>EditorApplication.Exit(ok ? 0 : 1);}
}
