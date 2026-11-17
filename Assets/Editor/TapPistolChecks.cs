using System;
using System.IO;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.Player;
using CyberBiker.Pooling;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class TapPistolChecks {
    private const string Key="CyberBiker.TapPistolChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static PlayerController player;private static TouchInputManager input;private static GameplayManager manager;
    private static PistolAimLaser laser;
    private static ShootingFeedback feedback;private static Enemy enemy;private static Button button,leftButton;
    private static int stage,shots,draws,lastFrame=-1;private static float at,secondShot;private static double began,pausedAt;private static bool finishing;
    private static string report="";
    static TapPistolChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){Directory.CreateDirectory("Logs/TapPistol");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Next(){stage++;at=Time.time;File.AppendAllText("Logs/TapPistol/Progress.txt",stage+"\n");}
    private static void Touch(int id,Vector2 pos,Vector2 movement,TouchPhase phase,float time){typeof(TouchInputManager).GetMethod("ProcessPistolTouch",Private).Invoke(input,new object[]{id,pos,movement,phase,time});}
    private static void Tick(){if(finishing || !EditorApplication.isPlaying)return;try{
        Require(EditorApplication.timeSinceStartup-began<100,"Tap pistol check timed out at stage "+stage);
        if(stage!=8 && lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(stage==0){
            player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();if(player==null || player.Health==null || Time.timeSinceLevelLoad<.5f)return;
            manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();Require(manager.PistolInteractionMode==PistolInteraction.TapToFire,"The experiment must be enabled by default");manager.SwitchGameState(GameState.Playing);
            manager.PlayerPistolDamage=15;manager.PlayerPistolShotInterval=.45f;manager.PistolHolsterDelay=1.2f;
            foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>(FindObjectsSortMode.None))director.enabled=false;
            foreach(var spawner in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))spawner.enabled=false;
            foreach(var old in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))old.gameObject.SetActive(false);
            foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))car.gameObject.SetActive(false);
            foreach(var shape in player.GetComponentsInChildren<Collider>(true))shape.enabled=false;player.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeAll;player.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;
            laser=player.GetComponent<PistolAimLaser>();Require(laser!=null,"Player prefab must include its aiming laser");input=player.GetComponent<TouchInputManager>();feedback=player.GetComponent<ShootingFeedback>();button=input.aimButton.GetComponent<Button>();
            Require(input.LeftFireButton!=null,"Mirrored left fire button is missing");leftButton=input.LeftFireButton.GetComponent<Button>();
            Require(input.aimButton.TryGetComponent<CanvasGroup>(out var rightOpacity) && input.LeftFireButton.TryGetComponent<CanvasGroup>(out var leftOpacity) && Mathf.Approximately(rightOpacity.alpha,.35f) && Mathf.Approximately(leftOpacity.alpha,.35f),"Both fire controls must initialize with 35% opacity");
            var rightPoint=RectTransformUtility.WorldToScreenPoint(null,input.aimButton.position);var leftPoint=RectTransformUtility.WorldToScreenPoint(null,input.LeftFireButton.position);
            Require(Vector2.Distance(leftPoint,new Vector2(Screen.width-rightPoint.x,rightPoint.y))<2,"Left fire button is not symmetrically placed");
            Require(input.LeftFireButton.localScale==input.aimButton.localScale && input.LeftFireButton.sizeDelta==input.aimButton.sizeDelta,"Left fire button must match the original size");
            Require((bool)typeof(TouchInputManager).GetMethod("IsPistolPointerOverUI",Private).Invoke(input,new object[]{leftPoint}),"Left button must intercept screen-tap polling");
            // Pistol draw/holster timers use real time. A fixed capture clock
            // makes the timed assertions disagree with those timers on slow
            // rendering machines, despite no extra shot being fired.
            Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;
            enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),player.transform.position+new Vector3(3,0,20),Quaternion.identity).GetComponent<Enemy>();Next();
        }else if(stage==1 && enemy.HasInitialized){
            enemy.enabled=false;enemy.StopAllCoroutines();typeof(Enemy).GetField("vitality",Private).SetValue(enemy,new Health(1000));shots=feedback.ShotCount;draws=player.PistolDrawCount;
            leftButton.onClick.Invoke();Require(player.HasBufferedPistolTap,"The mirrored mobile button must request a single shot");Next();
        }else if(stage==2 && feedback.ShotCount>shots){
            Require(feedback.ShotCount==shots+1 && player.PistolDrawCount==draws+1,"First tap must draw once and fire exactly once");Require(!player.HasBufferedPistolTap && player.TapPistolReady,"Keep the pistol ready after consuming the tap");typeof(PistolAimLaser).GetMethod("Refresh",Private).Invoke(laser,new object[]{Camera.main});Require(laser.Visible && Vector3.Distance(laser.Origin,player.GetComponent<BikeAutoAim>().Muzzle.position)<.02f,"Laser must start on the actual animated muzzle");Require(laser.HitCollider!=null && laser.HitCollider.GetComponentInParent<Enemy>()==enemy,"Laser must stop at the target collider");var muzzle=player.GetComponentInChildren<MuzzleFlash>(true);Require(muzzle.LastFlash!=null && muzzle.LastFlash.activeInHierarchy,"First tap must show the player's muzzle blast");Require(Mathf.Abs(muzzle.LastFlash.transform.lossyScale.x-manager.ProjectileCombat.playerFlashScale)<.001f,"Player flash size must be independent of the tiny gun hierarchy scale");File.WriteAllText("Logs/TapPistol/MuzzleScale.txt","Anchor world scale: "+muzzle.transform.lossyScale+"; flash world scale: "+muzzle.LastFlash.transform.lossyScale+"; timed duration: "+muzzle.FlashDuration);Capture("FirstTap");Next();
        }else if(stage==3 && Time.time-at>.65f){
            Require(feedback.ShotCount==shots+1 && player.TapPistolReady,"A ready pistol must not keep firing without new taps");Require(enemy.CurrentHealth<1000,"Real automatically targeted projectiles must hit the enemy");var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=(laser.Origin+laser.EndPoint)*.5f;wall.transform.localScale=new Vector3(3,3,.2f);Physics.SyncTransforms();typeof(PistolAimLaser).GetMethod("Refresh",Private).Invoke(laser,new object[]{Camera.main});Require(laser.HitCollider==wall.GetComponent<Collider>(),"Laser must stop on a nearer wall instead of passing through it");Capture("LaserBlocked");UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();report+="PASS red muzzle laser follows the actual shot direction and stops at targets/nearer obstacles.\n";button.onClick.Invoke();Next();
        }else if(stage==4 && feedback.ShotCount==shots+2){
            Require(player.PistolDrawCount==draws+1,"A second tap during the ready window must not redraw");secondShot=Time.time;for(int i=0;i<8;i++)(i%2==0 ? leftButton : button).onClick.Invoke();Next();
        }else if(stage==5 && feedback.ShotCount>=shots+10){
            Require(feedback.ShotCount==shots+10 && Time.time-secondShot<.15f,"Each rapid tap must fire once immediately without the automatic-mode cooldown");Next();
        }else if(stage==6 && Time.time-at>1.35f){
            Require(feedback.ShotCount==shots+10,"No additional shots may appear after all taps were consumed");Require(!player.TapPistolReady && !player.HasBufferedPistolTap && !player.GetComponent<Animator>().GetBool("Aim"),"Idle timeout must holster cleanly");typeof(PistolAimLaser).GetMethod("Refresh",Private).Invoke(laser,new object[]{Camera.main});Require(!laser.Visible,"Laser must hide when the pistol holsters");Capture("IdleHolstered");
            report+="PASS first-tap draw/single shot, subsequent shot without redraw, every rapid tap fired without a cooldown, no automatic repeat and idle holster.\n";input.TapPistol();Next();
        }else if(stage==7 && feedback.ShotCount==shots+11){
            Require(player.PistolDrawCount==draws+2,"A tap after holstering must draw again");manager.SwitchGameState(GameState.Pause);Require(!input.TapPistol(),"Paused input must not queue a shot");pausedAt=EditorApplication.timeSinceStartup;Next();
        }else if(stage==8 && EditorApplication.timeSinceStartup-pausedAt>.2){
            Require(!player.HasBufferedPistolTap,"Pause must cancel pending work");manager.SwitchGameState(GameState.Playing);Next();
        }else if(stage==9 && Time.time-at>.65f){
            Require(feedback.ShotCount==shots+11,"Resume must not replay a tap");
            int taps=input.PistolTapCount;var point=new Vector2(Screen.width*.5f,Screen.height*.55f);float now=Time.unscaledTime;File.WriteAllText("Logs/TapPistol/PointerDebug.txt","Screen="+Screen.width+"x"+Screen.height+" camera="+Camera.main.pixelRect+" point="+point+"\n");
            var testHits=(System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>)typeof(TouchInputManager).GetField("pistolUiHits",Private).GetValue(input);typeof(TouchInputManager).GetMethod("IsPistolPointerOverUI",Private).Invoke(input,new object[]{point});foreach(var hit in testHits){var selectable=hit.gameObject.GetComponentInParent<Selectable>();File.AppendAllText("Logs/TapPistol/PointerDebug.txt",hit.gameObject.name+" selectable="+(selectable!=null ? selectable.name+" enabled="+selectable.enabled+" interactable="+selectable.interactable : "none")+" trigger="+hit.gameObject.GetComponentInParent<UnityEngine.EventSystems.EventTrigger>()+"\n");}
            Touch(7,point,Vector2.zero,TouchPhase.Began,now);Touch(7,point,Vector2.zero,TouchPhase.Ended,now+.1f);Require(input.PistolTapCount==taps+1,"A short gameplay touch must count as one tap; health="+player.CurrentHealth+" state="+manager.gameState+" dt="+Time.deltaTime+" UI="+typeof(TouchInputManager).GetMethod("IsPistolPointerOverUI",Private).Invoke(input,new object[]{point})+"");
            taps=input.PistolTapCount;Touch(8,point,Vector2.zero,TouchPhase.Began,now);Touch(8,point+Vector2.right*80,Vector2.right*80,TouchPhase.Moved,now+.05f);Require(input.Steering.x>0,"Dragging must continue steering");Touch(8,point+Vector2.right*80,Vector2.zero,TouchPhase.Ended,now+.1f);Require(input.PistolTapCount==taps,"Steering drags must not fire");
            var ui=RectTransformUtility.WorldToScreenPoint(null,input.aimButton.position);Require((bool)typeof(TouchInputManager).GetMethod("IsPistolPointerOverUI",Private).Invoke(input,new object[]{ui}),"The actual fire button must be detected as UI");Touch(9,ui,Vector2.zero,TouchPhase.Began,now);Touch(9,ui,Vector2.zero,TouchPhase.Ended,now+.1f);Require(input.PistolTapCount==taps,"UI touch polling must not duplicate button callbacks");
            ui=input.LeftFireButton.position;Require((bool)typeof(TouchInputManager).GetMethod("IsPistolPointerOverUI",Private).Invoke(input,new object[]{ui}),"Left fire button must be detected as UI");
            Touch(10,ui,Vector2.zero,TouchPhase.Began,now);Touch(10,ui,Vector2.zero,TouchPhase.Ended,now+.1f);Require(input.PistolTapCount==taps,"Left UI touch must not duplicate its callback");
            report+="PASS both fire buttons initialize at 35% opacity; mirrored size/placement, one shot per tap on both sides, alternating rapid taps and no duplicate UI polling shots.\n";Next();
        }else if(stage==10 && feedback.ShotCount==shots+12){
            report+="PASS pause/resume, screen-tap recognition, steering drag separation and no duplicate UI polling shot.\n";
            input.TapPistol();player.GetComponent<BikeSwordCombat>().Launch();Require(!player.HasBufferedPistolTap && !player.TapPistolReady,"Launch must cancel the pistol queue");Next();
        }else if(stage==11 && Time.time-at>.5f){
            Require(feedback.ShotCount==shots+12,"Launch must not release a buffered pistol shot");input.TapPistol();Next();
        }else if(stage==12 && feedback.ShotCount==shots+13){
            Require(!player.GetComponent<BikeSwordCombat>().IsActive,"A new tap must return from launch into a single pistol shot");report+="PASS launch cancels buffered shots; a new tap returns from sword/launch and fires once.\n";
            manager.PistolInteractionMode=PistolInteraction.AimAndAutoFire;Next();
        }else if(stage==13 && Time.time-at>.1f){input.SetAim(true);Next();
        }else if(stage==14 && feedback.ShotCount>=shots+15){
            Require(manager.PlayerPistolDamage==15 && manager.PlayerPistolShotInterval==.45f,"The input experiment must preserve pistol balance");input.SetAim(false);report+="PASS Inspector fallback retains continuous aim/automatic shooting; damage and fire interval unchanged.\n";
            enemy.gameObject.SetActive(false);foreach(var shot in UnityEngine.Object.FindObjectsByType<Bullet>())PrefabPool.Release(shot.gameObject);enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),player.transform.position+new Vector3(3,0,20),Quaternion.identity).GetComponent<Enemy>();shots=feedback.ShotCount;Next();
        }else if(stage==15 && enemy.HasInitialized){
            enemy.enabled=false;enemy.TakeDamage(new DamageInfo(10000,player.gameObject));Require(enemy.IsAlive && enemy.CurrentHealth<=1,"Legacy mode must still protect the finisher-health floor");
            manager.PistolInteractionMode=PistolInteraction.TapToFire;manager.TapPistolCanKill=true;Next();
        }else if(stage==16 && Time.time-at>.1f){input.TapPistol();Next();
        }else if(stage==17 && enemy==null){Require(feedback.ShotCount==shots+1,"One tap must finish the low-health enemy; before="+shots+" after="+feedback.ShotCount);Finish(true,report+"PASS tap-mode pistol contact kills and destroys the enemy through its existing death/explosion path; legacy mode retains the finisher floor.\n");
        }
    }catch(Exception error){Finish(false,error+"\n"+report);}}
    private static void Capture(string name){
        var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.aspect=1280f/720;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/TapPistol/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=old;camera.ResetAspect();RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);}
    }
    private static void Finish(bool ok,string message){if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Time.captureDeltaTime=0;FinisherClock.EditorCaptureStep=0;File.WriteAllText("Logs/TapPistol/Result.txt",message);EditorApplication.delayCall+=()=>EditorApplication.Exit(ok ? 0 : 1);}
}
