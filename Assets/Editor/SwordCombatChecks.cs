using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CyberBiker.Player;
using CyberBiker.Audio;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.UI;

[InitializeOnLoad]
public static class SwordCombatChecks {
    private sealed class FixtureInput : IPlayerInput {
        private readonly TouchInputManager touch;
        public FixtureInput(TouchInputManager owner){touch=owner;}
        public Vector2 Steering=>Vector2.zero;
        public Vector2 AimDelta=>Vector2.zero;
        public bool IsAiming=>touch.aim;
    }
    private const string Pending="CyberBiker.SwordCombatChecks";
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private static PlayerController player;
    private static BikeSwordCombat sword;
    private static Enemy template,target,second;
    private static Rigidbody body;
    private static Button button;
    private static Animator animator;
    private static RuntimeAnimatorController originalController;
    private static Avatar originalAvatar;
    private static RigBuilder rig;
    private static MuzzleFlash muzzle;
    private static int stage,shots;
    private static float started,stageTime;
    private static bool captured,finishing;
    private static string report="";
    private static SwordFinisherEffects fx;
    private static GameplayManager gameplay;
    private static bool trailSeen,pauseChecked,pauseStarted,windSeen;
    private static double pauseClock;
    private static float pausedSlow;
    private static float pausedDuck;
    private static float nextDiagnostic;
    private static bool smokeCaptured;
    private static double blastClock=-1,previousRecoveryClock;
    private static bool extendedBlastSlowSeen,recoverySeen,wideCameraSeen,blastCameraCaptured;
    private static float previousRecoveryScale;
    private static bool naturalFinishChecked;
    private static bool sideCameraSeen;
    private static Transform[] swordProps;
    private static Vector3[] mountedPositions,mountedScales;
    private static Quaternion[] mountedRotations;
    private static int cancellationCase,completedSheathes,cancellationShots;
    private static bool releasedAim;
    private static double queuedAimClock;
    private static bool fastFirstShotSeen;
    private static bool mountedBetweenCuts;
    private static readonly System.Collections.Generic.List<Enemy> pendingStaggers=new System.Collections.Generic.List<Enemy>();
    private static GameObject strikeBlocker;
    private static GameObject sideLaneBlocker;
    private static BikeSwordCombat.CombatPhase lastPhase=(BikeSwordCombat.CombatPhase)(-1);
    static SwordCombatChecks(){if(SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){SwordCombatSetup.UpgradeFinisher();EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Attach(){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Next(){stage++;stageTime=Time.time;captured=false;}
    private static Enemy Spawn(float x,float z) {
        var go=UnityEngine.Object.Instantiate(template.gameObject,player.transform.position+new Vector3(x,0,z),Quaternion.identity);
        go.name="Sword test enemy";
        var result=go.GetComponent<Enemy>();
        typeof(Enemy).GetField("speed",Private).SetValue(result,110f);
        typeof(Enemy).GetField("shootingDamage",Private).SetValue(result,0f);
        typeof(Enemy).GetField("health",Private).SetValue(result,1000);
        // Keep these two side/reach cases deterministic; production steering AI
        // otherwise chooses paths through the traffic that this fixture hides.
        typeof(Enemy).GetField("path",Private).SetValue(result,new AI.DodgePoint(go.transform.position+Vector3.forward*10000,go));
        typeof(Enemy).GetField("followTarget",Private).SetValue(result,go.transform.position+Vector3.forward*10);
        go.SetActive(true);pendingStaggers.Add(result);return result;
    }
    private static void Tick() {
        if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<.7f)return;
        try {
            if(sword!=null && sword.IsDashing && fx!=null && fx.VisibleWindRibbons>=12)windSeen=true;
            if(sideLaneBlocker!=null && target!=null && target.IsAlive && sword!=null && sword.HitCount==0) {
                var offset=player.transform.TransformVector((Vector3)typeof(BikeSwordCombat).GetField("leftStrikePoint",Private).GetValue(sword));
                sideLaneBlocker.transform.position=target.transform.position-new Vector3(offset.x,0,offset.z)+Vector3.up*1.5f;
            }
            for(int i=pendingStaggers.Count-1;i>=0;i--) {
                var enemy=pendingStaggers[i];
                if(enemy==null){pendingStaggers.RemoveAt(i);continue;}
                if(enemy.IsAlive && gameplay!=null && gameplay.gameState==GameState.Playing){enemy.TakeDamage(new CyberBiker.Combat.DamageInfo(900,player.gameObject));Require(enemy.IsFinisherReady,"Sword fixtures must be weakened before pickup");pendingStaggers.RemoveAt(i);}
            }
            if(stage==0) {
                started=Time.time;player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();sword=player.GetComponent<BikeSwordCombat>();fx=player.GetComponent<SwordFinisherEffects>();gameplay=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();
                Require(sword!=null && fx!=null,"Pickup combat and effects must be installed on the real player");
                foreach(var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)){if(template==null)template=e;e.gameObject.SetActive(false);}
                foreach(var c in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))c.gameObject.SetActive(false);
                foreach(var spawner in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))spawner.enabled=false;
                gameplay.SwitchGameState(GameState.Playing);body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
                player.transform.SetPositionAndRotation(new Vector3(0,100,0),Quaternion.identity);body.position=player.transform.position;body.linearVelocity=Vector3.forward*110;
                animator=player.GetComponent<Animator>();originalController=animator.runtimeAnimatorController;originalAvatar=animator.avatar;rig=player.GetComponent<RigBuilder>();muzzle=player.GetComponentInChildren<MuzzleFlash>(true);shots=muzzle.SpawnCount;
                swordProps=player.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Sword_Main" || t.name=="Sword_Holder").ToArray();
                Require(swordProps.Length==2,"Both mounted sword prop bones must be present");
                mountedPositions=swordProps.Select(t=>t.localPosition).ToArray();mountedRotations=swordProps.Select(t=>t.localRotation).ToArray();mountedScales=swordProps.Select(t=>t.localScale).ToArray();
                var touch=player.GetComponent<TouchInputManager>();touch.enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(player,new FixtureInput(touch));typeof(BikeSwordCombat).GetField("launchKey",Private).SetValue(sword,KeyCode.None);
                button=GameObject.Find("LaunchButton").GetComponent<Button>();target=Spawn(-3.5f,35);second=Spawn(3.5f,70);
                // A car-sized obstacle at the rejected side-alignment endpoint
                // must not redirect or block the direct dash to this enemy.
                sideLaneBlocker=GameObject.CreatePrimitive(PrimitiveType.Cube);sideLaneBlocker.name="Side-lane dash regression obstacle";
                sideLaneBlocker.transform.position=target.transform.position-player.transform.TransformVector((Vector3)typeof(BikeSwordCombat).GetField("leftStrikePoint",Private).GetValue(sword))+Vector3.up*1.5f;
                sideLaneBlocker.transform.localScale=new Vector3(1.2f,3,1.2f);Next();
            } else if(stage==1 && Time.time-stageTime>.15f) {
                button.onClick.Invoke();Require(sword.Phase==BikeSwordCombat.CombatPhase.Pursuing && sword.DrawCompletionCount==0,"Launch must begin mounted in the normal riding pose");RequireSwordMounted();Next();
            }
            else if(stage==2) {
                if(sword.Phase==BikeSwordCombat.CombatPhase.Pursuing)RequireSwordMounted();
                if(sword.HitCount==0 && sword.Phase==BikeSwordCombat.CombatPhase.Pursuing && target.transform.position.z-player.transform.position.z>8)Require(Mathf.Abs(player.transform.position.x)<.05f,"The bike must not align or steer toward a distant enemy");
                trailSeen|=fx.TrailVertexCount>=6;CaptureStrike("left");
                if(sword.HitCount==1) {
                    RequireDirectContact(target);if(sideLaneBlocker!=null)sideLaneBlocker.SetActive(false);
                    Require(sword.LastPickupDistance<=15,"A pickup must begin only at close range");Require(sword.LastAttackSide==BikeSwordCombat.AttackSide.Left,"Left pickup needs the left slash");
                    Require(sword.DrawCompletionCount==1,"The sword draw must finish immediately before the first dash");
                    Require(!target.IsAlive && Mathf.Abs(target.transform.position.x+3.5f)<.1f,"The cut must kill without moving the enemy sideways");
                    Require(target.SwordDefeatVelocity.z>body.linearVelocity.z+10,"A cut must kick the defeated enemy forward");
                    Require(fx.ImpactCount==1 && fx.LastImpactParticleCount>0 && gameplay.CombatSlowMotionCount>=2,"One cut needs one immediate impact and one slow-motion beat");
                    Require(GameAudio.Active!=null && GameAudio.Active.SwordSoundCount>=3 && GameAudio.Active.SwordDuckingGain<.7f,"A cut needs the recorded swish, both impact layers and riding-mix ducking");
                    report+="PASS close direct dash: no distant alignment, left one-shot cut, no enemy lateral snap, forward death kick, immediate impact and slow motion.\n";
                    report+="First contact player="+player.transform.position+" body="+body.position+" velocity="+body.linearVelocity+" dash direction="+typeof(BikeSwordCombat).GetField("dashDirection",Private).GetValue(sword)+" saved="+typeof(BikeSwordCombat).GetField("dashVelocity",Private).GetValue(sword)+" start="+typeof(BikeSwordCombat).GetField("dashStart",Private).GetValue(sword)+"\n";Next();
                } else Require(Time.time-stageTime<6,"First pickup failed: "+sword.Phase+" delta="+(target.transform.position-player.transform.position));
            } else if(stage==3) {
                if(!pauseStarted){CaptureStrike("impact",true);CaptureImpactCamera();pausedSlow=gameplay.CombatSlowMotionRemaining;pausedDuck=GameAudio.Active.SwordDuckingGain;gameplay.SwitchGameState(GameState.Pause);pauseClock=EditorApplication.timeSinceStartup;pauseStarted=true;}
                Require(Time.timeScale==0 && gameplay.CombatSlowMotionRemaining==pausedSlow,"Pause must freeze the cut clock");
                Require(Mathf.Abs(GameAudio.Active.SwordDuckingGain-pausedDuck)<.0001f,"Pause must freeze the impact mix recovery");
                if(EditorApplication.timeSinceStartup-pauseClock>.2){gameplay.SwitchGameState(GameState.Playing);Next();}
            } else if(stage==4) {
                if(sword.Phase==BikeSwordCombat.CombatPhase.Pursuing && sword.HitCount==1) {
                    RequireSwordMounted();Require(sword.BoostBlend>.99f,"Boost must stay at full strength through the per-slice return");mountedBetweenCuts=true;
                }
                if(fx.CarriedExplosionCount>0 && blastClock<0)blastClock=EditorApplication.timeSinceStartup;
                double blastAge=blastClock>=0 ? EditorApplication.timeSinceStartup-blastClock : -1;
                if(blastAge>.35 && blastAge<.7){Require(Time.timeScale<.65f,"Slow motion must hold while riding into the explosion");extendedBlastSlowSeen=true;}
                if(blastAge>.1 && blastAge<.7){wideCameraSeen|=fx.LastCameraZoom<-.5f;sideCameraSeen|=fx.LastCameraSideOffset<-1;}
                if(blastAge>.35 && !blastCameraCaptured){CaptureImpactCamera("explosion-camera",false);blastCameraCaptured=true;}
                if(gameplay.IsCombatTimeRecovering) {
                    double now=EditorApplication.timeSinceStartup;
                    if(recoverySeen && now-previousRecoveryClock<.15)Require(Time.timeScale>=previousRecoveryScale-.001f && Time.timeScale-previousRecoveryScale<.3f,"Recovery must ease the clock upward without a speed jump");
                    recoverySeen=true;previousRecoveryClock=now;previousRecoveryScale=Time.timeScale;
                }
                if(!smokeCaptured && fx.SmokeParticleCount>40){CaptureStrike("smoke",true);smokeCaptured=true;captured=false;}
                if(Time.time>=nextDiagnostic){nextDiagnostic=Time.time+.5f;report+="Chain progress phase="+sword.Phase+" player="+player.transform.position+" body="+body.position+" velocity="+body.linearVelocity+" yaw="+player.transform.eulerAngles.y+" target="+(sword.Target!=null ? sword.Target.name : "none")+"\n";}
                trailSeen|=fx.TrailVertexCount>=6;CaptureStrike("right");
                if(sword.HitCount==2) {
                    RequireDirectContact(second);
                    Require(sword.LaunchCount==1,"A single launch must pick up multiple enemies without ending the boost between them");
                    Require(mountedBetweenCuts && sword.SheatheCompletionCount>=1 && sword.DrawCompletionCount==2,"Each pickup must draw just before the dash and sheath before returning to boosted riding; draws="+sword.DrawCompletionCount+" sheathes="+sword.SheatheCompletionCount+" mounted="+mountedBetweenCuts);
                    Require(sword.LastAttackSide==BikeSwordCombat.AttackSide.Right && !second.IsAlive,"The next enemy needs a right one-shot cut");
                    Require(Mathf.Abs(second.transform.position.x-3.5f)<.1f,"The second enemy must keep its lane through contact");
                    Require(trailSeen && fx.ImpactCount==2,"Both pickups need visible blade trails and exactly one impact each");
                    Require(windSeen,"Pickup dashes must show the curved wind ribbons while dashing");
                    report+="PASS per-pickup sword cycle: mounted riding on Launch, draw before each dash, sheath after each slice, mounted riding between enemies with uninterrupted boost and forward momentum.\n";
                    // Expire the launch on the final cut so sheathing cannot cut
                    // the last explosion's slow-motion beat short.
                    typeof(BikeSwordCombat).GetField("elapsed",Private).SetValue(sword,100f);
                    target=Spawn(6,35);Next();
                } else Require(Time.time-stageTime<6,"Second pickup failed: "+sword.Phase+" player="+player.transform.position+" enemy="+second.transform.position+" velocity="+body.linearVelocity+" steering="+player.xThrow);
            } else if(stage==5) {
                if(!naturalFinishChecked && fx.CarriedExplosionCount>=2) {
                    Require(sword.AllowsAftermathSlowMotion && gameplay.CombatSlowMotionRemaining>1 && Time.timeScale<.5f,"Natural launch expiry must preserve the final explosion's slow motion");
                    naturalFinishChecked=true;player.GetComponent<TouchInputManager>().SetAim(true);
                }
                if(Time.time-stageTime<=2.5f)return;
                Require(naturalFinishChecked,"The final explosion must complete before cancellation recovery");
                Require(!sword.IsActive && muzzle.SpawnCount>shots,"Pistol aiming/fire must resume after cancelling the launch");
                RequireSwordMounted();
                Require(Time.timeScale==1 && Mathf.Abs(Time.fixedDeltaTime-.02f)<.00001f,"Normal clock and physics step must be restored");
                Require(fx.CarriedExplosionCount>=2 && fx.LastExplosionVelocity.z>80,"Both death explosions must inherit forward momentum");
                Require(fx.SmokeWakeCount>=1,"Passing through a moving blast must start the thick smoke wake");
                Require(extendedBlastSlowSeen && recoverySeen && wideCameraSeen,"Explosion needs an extended slow hold, smooth time recovery and a wider camera composition");
                Require(sideCameraSeen && Mathf.Abs(fx.LastCameraSideOffset)<.1f,"The camera must move toward the left slash, hold through the blast and return behind the bike");
                report+="PASS hit side view: signed side travel held through explosion, original follow position restored after recovery.\n";
                Require(GameAudio.Active.SwordExplosionSoundCount>=6,"Both sword explosions need blast, bass and debris layers");
                report+="PASS explosion ride-through: extended slow hold, eased time-scale recovery, wider/lifted camera, three spatial explosion sound layers per kill, final blast preserved at natural launch expiry.\n";
                Require(GameAudio.Active.SwordSoundCount>=6 && Mathf.Abs(GameAudio.Active.SwordDuckingGain-1)<.001f,"Both cuts need their sound layers and the riding mix must recover");
                Require(Camera.main.GetComponent<SwordImpactScreen>().Strength<.001f,"Contact screen treatment must fully clear after recovery");
                report+="PASS contact treatment: native camera shader rendered, recorded swish/metal/low impact on both cuts, engine/wind dip and pause-safe recovery, clean normal view restored.\n";
                CheckExplosion();target.gameObject.SetActive(false);player.GetComponent<TouchInputManager>().SetAim(false);body.constraints=RigidbodyConstraints.FreezeAll;body.linearVelocity=Vector3.zero;
                target=Spawn(0,5);typeof(Enemy).GetField("speed",Private).SetValue(target,0f);
                strikeBlocker=GameObject.CreatePrimitive(PrimitiveType.Cube);strikeBlocker.transform.position=player.transform.position+new Vector3(0,1.8f,2);strikeBlocker.transform.localScale=new Vector3(5,4,.3f);Next();
            } else if(stage==6 && Time.time-stageTime>.15f){button.onClick.Invoke();Next();}
            else if(stage==7 && Time.time-stageTime>.9f) {
                Require(sword.HitCount==2 && target.IsAlive && fx.ImpactCount==2,"A blocked lunge must not pass through an obstacle or damage the enemy");
                player.GetComponent<TouchInputManager>().SetAim(true);Next();
            } else if(stage==8 && Time.time-stageTime>1.1f) {
                Require(!sword.IsActive && fx.TrailVertexCount==0 && Time.timeScale==1,"Cancellation must clear the trail and restore the clock");
                report+="PASS obstruction, pause/resume, trail cleanup, pistol recovery, original Animator/Avatar/RigBuilder retained.\n";
                cancellationCase=0;StartAimCancellationCase();Next();
            } else if(stage==9) {
                bool ready=cancellationCase==0 ? sword.Phase==BikeSwordCombat.CombatPhase.Drawing && (float)typeof(BikeSwordCombat).GetField("phaseTime",Private).GetValue(sword)>.03f : cancellationCase==1 ? sword.Phase==BikeSwordCombat.CombatPhase.Pursuing : sword.Phase==BikeSwordCombat.CombatPhase.Striking && sword.SwingProgress>.12f;
                if(ready) {
                    completedSheathes=sword.SheatheCompletionCount;cancellationShots=muzzle.SpawnCount;releasedAim=false;
                    player.GetComponent<TouchInputManager>().SetAim(true);
                    queuedAimClock=EditorApplication.timeSinceStartup;
                    if(cancellationCase==1) {
                        Require(!sword.IsActive && player.GetComponent<TouchInputManager>().aim,"Mounted Launch must switch straight to pistol Aim without an unnecessary sheath");
                        player.GetComponent<TouchInputManager>().SwitchAim(false);releasedAim=true;
                    } else Require(sword.Phase==BikeSwordCombat.CombatPhase.Sheathing && !player.GetComponent<TouchInputManager>().aim,"Aim must wait while the sword-return clip plays");
                    Next();
                } else Require(Time.time-stageTime<2,"Could not reach cancellation phase "+cancellationCase+": "+sword.Phase);
            } else if(stage==10) {
                var touch=player.GetComponent<TouchInputManager>();
                if(sword.IsActive)Require(!touch.aim && muzzle.SpawnCount==cancellationShots,"Aim/fire must stay inactive until the sword is mounted");
                if(cancellationCase<2 && !releasedAim && Time.time-stageTime>.1f) {
                    if(cancellationCase==0)touch.SetAim(false);else touch.SwitchAim(false);
                    releasedAim=true;
                }
                if(!sword.IsActive) {
                    Require(EditorApplication.timeSinceStartup-queuedAimClock<.45,"Sword-to-Aim handoff must finish promptly even during slow motion");
                    Require(sword.SheatheCompletionCount==completedSheathes+(cancellationCase==1 ? 0 : 1),"Cancellation must sheath only when the sword was actually drawn");
                    Require(touch.aim==(cancellationCase==2),"Queued Aim must respect release/toggle changes during sheathing");RequireSwordMounted();
                    if(cancellationCase==2)target.transform.position=player.transform.position+Vector3.forward*25;
                    Next();
                } else Require(Time.time-stageTime<2,"Sword return did not complete for case "+cancellationCase);
            } else if(stage==11) {
                if(cancellationCase==2 && !fastFirstShotSeen && muzzle.SpawnCount>cancellationShots) {
                    double latency=EditorApplication.timeSinceStartup-queuedAimClock;
                    Require(latency<.85,"First pistol shot after a sword cancel must be prompt: "+latency.ToString("F3")+" real seconds");
                    report+="First shot from Aim pressed during launch: "+latency.ToString("F3")+" real seconds.\n";fastFirstShotSeen=true;
                }
                if(Time.time-stageTime<=(cancellationCase==2 ? 2.5f : .8f))return;
                RequireSwordMounted();
                if(cancellationCase==2){Require(fastFirstShotSeen,"Pistol auto fire must resume after the full sword return; aim="+player.GetComponent<TouchInputManager>().aim+" armed="+typeof(PlayerController).GetField("armed",Private).GetValue(player));report+="PASS Aim during draw, pursuit and strike: fast real-time handoff under 0.45s, complete sheathing before Aim/fire, mounted sword retained, queued release/toggle respected.\n";Finish(true,report);}
                else {cancellationCase++;StartAimCancellationCase();stage=9;stageTime=Time.time;captured=false;}
            }
            if(stage>0) {
                Require(animator.runtimeAnimatorController==originalController && animator.avatar==originalAvatar && rig.enabled,"The original animation setup must remain intact");
                Require(player.IsAlive && Time.time-started<35,"Pickup validation timed out at stage "+stage);
            }
        }catch(Exception error){Finish(false,error+"\n"+report);}
    }
    private static void RequireSwordMounted() {
        // The holder follows the Animator's riding/aim poses. The blade's local
        // transform relative to that holder is what must return to its mount.
        for(int i=0;i<swordProps.Length;i++)if(swordProps[i].name=="Sword_Main")Require(Vector3.Distance(swordProps[i].localPosition,mountedPositions[i])<.00002f && Quaternion.Angle(swordProps[i].localRotation,mountedRotations[i])<.2f && Vector3.Distance(swordProps[i].localScale,mountedScales[i])<.001f,"Sword must stay on its back mount: "+swordProps[i].name+" actual="+swordProps[i].localPosition.ToString("F7")+" expected="+mountedPositions[i].ToString("F7")+" angle="+Quaternion.Angle(swordProps[i].localRotation,mountedRotations[i]));
    }
    private static void StartAimCancellationCase() {
        player.GetComponent<TouchInputManager>().SetAim(false);
        if(strikeBlocker!=null)strikeBlocker.SetActive(false);
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
        if(cancellationCase!=1){target=Spawn(0,5);typeof(Enemy).GetField("speed",Private).SetValue(target,0f);}
        typeof(BikeSwordCombat).GetField("nextLaunch",Private).SetValue(sword,0f);sword.Launch();
    }
    private static void CheckExplosion() {
        var effect=(ParticleSystem)typeof(Enemy).GetField("explosion",Private).GetValue(template);
        Require(effect!=null && second==null,"The defeated enemy must finish its forward kick and explode");
    }
    private static void RequireDirectContact(Enemy enemy) {
        var direction=(Vector3)typeof(BikeSwordCombat).GetField("dashDirection",Private).GetValue(sword);
        var separation=Vector3.ProjectOnPlane(enemy.transform.position-player.transform.position,Vector3.up);
        float sideways=Mathf.Abs(Vector3.Dot(separation,new Vector3(direction.z,0,-direction.x)));
        Require(sideways<.35f,"Dash must finish on the direct approach line, not at the enemy's side; sideways error="+sideways);
        Require(Vector3.Dot(separation,direction)>1,"Dash must stop short rather than move into/past the enemy");
    }
    private static void CaptureImpactCamera(string name="contact-camera",bool contact=true) {
        var view=Camera.main;fx.ApplyCamera(view);
        var screen=view.GetComponent<SwordImpactScreen>();Require(screen!=null && screen.IsSupported && (!contact || screen.Strength>.1f),"Contact contrast shader must be supported and active");
        int renders=screen.RenderCount;
        var rt=new RenderTexture(900,600,24);var previous=view.targetTexture;var active=RenderTexture.active;
        view.targetTexture=rt;view.Render();RenderTexture.active=rt;
        var image=new Texture2D(900,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,600),0,0);image.Apply();
        Directory.CreateDirectory("Logs/SwordCombat");File.WriteAllBytes("Logs/SwordCombat/"+name+".png",image.EncodeToPNG());
        view.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        if(contact)Require(screen.RenderCount>renders,"The impact treatment must execute on the real game camera");
    }
    private static void CaptureStrike(string side,bool force=false) {
        if(!force && (captured || sword.Phase!=BikeSwordCombat.CombatPhase.Striking))return;
        var phaseTime=(float)typeof(BikeSwordCombat).GetField("phaseTime",Private).GetValue(sword);if(!force && phaseTime<.25f)return;
        if(!force)Require(fx.CameraAmount>.1f,"The cinematic camera must be active through the swing (its smooth zoom may cross zero)");
        if(!force && sword.IsDashing)Require(fx.VisibleWindRibbons>=12,"The dash must have many curved wind ribbons around the rider");
        var go=new GameObject("Sword review camera");var camera=go.AddComponent<Camera>();
        camera.transform.position=player.transform.position+new Vector3(5,4,-6);camera.transform.LookAt(player.transform.position+Vector3.up*2);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.05f,.06f);camera.fieldOfView=45;
        var rt=new RenderTexture(900,700,24);camera.targetTexture=rt;camera.Render();var active=RenderTexture.active;RenderTexture.active=rt;
        var texture=new Texture2D(900,700,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,900,700),0,0);texture.Apply();Directory.CreateDirectory("Logs/SwordCombat");File.WriteAllBytes("Logs/SwordCombat/"+side+"-attack.png",texture.EncodeToPNG());
        RenderTexture.active=active;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);captured=true;
    }
    private static void Finish(bool passed,string result) {
        if(finishing)return;finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;SessionState.SetBool(Pending,false);
        Directory.CreateDirectory("Logs/SwordCombat");File.WriteAllText("Logs/SwordCombat/result.txt",result);EditorApplication.Exit(passed?0:1);
    }
}
