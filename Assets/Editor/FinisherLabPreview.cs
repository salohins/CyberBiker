using System;
using System.Reflection;
using AI;
using CyberBiker.Combat;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CyberBiker.EditorTools {
    [InitializeOnLoad]
    public static class FinisherLabPreview {
        private const string Key="CyberBiker.FinisherLab.";
        private const string Level="Assets/Level/Scenes/Level.unity";
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private sealed class NeutralInput : IPlayerInput {
            public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>false;
        }
        private static int stage;
        private static Enemy target;
        private static Camera viewCamera;
        private static RenderTexture previousTarget,view;
        private static double began,hitAt,readyAt;
        private static AsyncOperation reload;
        private static FinisherLabTake take;
        private static bool recording,viewingTake,takePlaying,takeDirty;
        private static int recordedFrame;
        private static float recordedTime,playhead;
        private static double playbackClock;
        public static PlayerController Player { get; private set; }
        public static BikeSwordCombat Sword { get; private set; }
        public static SwordFinisherEffects Effects { get; private set; }
        public static GameplayManager Gameplay { get; private set; }
        public static bool Active=>SessionState.GetBool(Key+"Active",false);
        public static bool Ready=>Active && EditorApplication.isPlaying && stage==2 && Sword!=null;
        public static RenderTexture View=>viewingTake && take!=null ? take.Texture : view;
        public static FinisherLabTake Take=>take;
        public static bool Recording=>recording;
        public static bool ViewingTake=>viewingTake;
        public static bool TakePlaying=>takePlaying;
        public static bool TakeDirty=>takeDirty;
        public static float CaptureLength {get=>SessionState.GetFloat(Key+"CaptureLength",8);set=>SessionState.SetFloat(Key+"CaptureLength",Mathf.Clamp(value,3,20));}
        public static float TakeSpeed {get=>SessionState.GetFloat(Key+"TakeSpeed",1);set=>SessionState.SetFloat(Key+"TakeSpeed",Mathf.Clamp(value,.1f,2));}
        public static string Status { get; private set; }="Ready to preview";
        public static bool Left {get=>SessionState.GetBool(Key+"Left",true);set=>SessionState.SetBool(Key+"Left",value);}
        public static bool Loop {get=>SessionState.GetBool(Key+"Loop",false);set=>SessionState.SetBool(Key+"Loop",value);}
        public static bool PauseOnContact {get=>SessionState.GetBool(Key+"PauseOnHit",false);set=>SessionState.SetBool(Key+"PauseOnHit",value);}
        public static bool Sound {get=>SessionState.GetBool(Key+"Sound",true);set{SessionState.SetBool(Key+"Sound",value);if(Active)AudioListener.volume=(viewingTake || recording) ? 0 : value ? 1 : 0;}}
        public static float RideSpeed {get=>SessionState.GetFloat(Key+"RideSpeed",110);set=>SessionState.SetFloat(Key+"RideSpeed",Mathf.Clamp(value,20,150));}
        public static float PhaseProgress {
            get {
                if(!Ready)return 0;
                if(Sword.Phase==BikeSwordCombat.CombatPhase.Striking)return Sword.SwingProgress;
                float age=(float)typeof(BikeSwordCombat).GetField("phaseTime",Private).GetValue(Sword);
                if(Sword.Phase==BikeSwordCombat.CombatPhase.Recovering) {
                    float duration=(float)typeof(BikeSwordCombat).GetProperty("RecoveryDuration",Private).GetValue(Sword);
                    return Mathf.Clamp01(age/Mathf.Max(.001f,duration));
                }
                string clip=Sword.Phase==BikeSwordCombat.CombatPhase.Drawing ? "draw" : Sword.Phase==BikeSwordCombat.CombatPhase.Sheathing ? "sheathe" : null;
                return clip!=null ? Mathf.Clamp01(age/((AnimationClip)typeof(BikeSwordCombat).GetField(clip,Private).GetValue(Sword)).length) : 0;
            }
        }
        static FinisherLabPreview() {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=OnModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload+=ReleaseView;
            AssemblyReloadEvents.beforeAssemblyReload+=CleanupForReload;
        }
        public static void Start(FinisherLabTuning tuning) {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the current Play session before starting the isolated finisher preview.");
            SessionState.SetString(Key+"PreviousStartScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Key+"OwnsStartScene",true);
            SessionState.SetFloat(Key+"ListenerVolume",AudioListener.volume);
            SessionState.SetString(Key+"Tuning",JsonUtility.ToJson(tuning));
            SessionState.SetBool(Key+"Active",true);
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Level);
            stage=0;Status="Starting isolated preview…";EditorApplication.isPlaying=true;
        }
        public static void Apply(FinisherLabTuning tuning) {
            tuning.Normalize();SessionState.SetString(Key+"Tuning",JsonUtility.ToJson(tuning));
            if(Ready)tuning.Apply(Sword,Effects);
            if(take!=null)takeDirty=true;
        }
        public static void RecordTake(FinisherLabTuning tuning) {
            if(EditorApplication.isPlayingOrWillChangePlaymode && !Active)throw new InvalidOperationException("Stop the current Play session before recording in the Lab.");
            ClearTake();Apply(tuning);Loop=false;PauseOnContact=false;
            if(Active)Replay();else Start(tuning);
            SessionState.SetBool(Key+"RecordRequested",true);
        }
        private static void BeginTake() {
            take=new FinisherLabTake(viewCamera);take.Capture(0,viewCamera,Player,Sword,Effects);
            recordedTime=0;recordedFrame=Time.frameCount;recording=true;takeDirty=false;
            AudioListener.volume=0;
            SessionState.SetFloat(Key+"PreviousCaptureStep",Time.captureDeltaTime);
            SessionState.SetBool(Key+"OwnsCaptureClock",true);Time.captureDeltaTime=1f/60;FinisherClock.EditorCaptureStep=1f/60;
        }
        private static void RestoreCaptureClock() {
            if(SessionState.GetBool(Key+"OwnsCaptureClock",false))Time.captureDeltaTime=SessionState.GetFloat(Key+"PreviousCaptureStep",0);
            SessionState.SetBool(Key+"OwnsCaptureClock",false);FinisherClock.EditorCaptureStep=0;
        }
        private static void CompleteTake() {
            recording=false;RestoreCaptureClock();EditorApplication.isPaused=true;viewingTake=true;takePlaying=false;playhead=0;take.Seek(0);AudioListener.volume=0;
            Status=take.IsFull ? "Take reached its memory/frame limit. Scrub the captured range or shorten the recording." : "Take recorded — scrub the full sequence, drag to orbit, scroll to zoom.";
        }
        public static void SeekTake(float time) {
            if(take==null || take.FrameCount==0)return;
            if(recording)CompleteTake();EditorApplication.isPaused=true;viewingTake=true;takePlaying=false;
            playhead=Mathf.Clamp(time,0,take.Duration);take.Seek(playhead);AudioListener.volume=0;
        }
        public static void StepTake(int direction) {if(take==null)return;SeekTake(take.SelectedTime);take.Step(direction);playhead=take.SelectedTime;}
        public static void ToggleTakePlayback() {
            if(take==null || recording)return;
            if(!viewingTake)SeekTake(0);if(playhead>=take.Duration)playhead=0;
            takePlaying=!takePlaying;playbackClock=EditorApplication.timeSinceStartup;
        }
        public static void ShowLive(){viewingTake=false;takePlaying=false;if(Active)AudioListener.volume=Sound ? 1 : 0;}
        public static void ClearTake() {
            recording=false;viewingTake=false;takePlaying=false;RestoreCaptureClock();take?.Dispose();take=null;
            SessionState.SetBool(Key+"RecordRequested",false);
        }
        private static void CleanupForReload() {
            bool requested=SessionState.GetBool(Key+"RecordRequested",false);
            ClearTake();SessionState.SetBool(Key+"RecordRequested",requested);
        }
        public static void Replay() {
            if(!Active || !EditorApplication.isPlaying)return;
            if(reload!=null && !reload.isDone)return;
            ClearTake();EditorApplication.isPaused=false;ReleaseView();stage=0;target=null;Player=null;Sword=null;Effects=null;Gameplay=null;
            UnityEngine.Random.InitState(7331);
            Status="Resetting scene and effects…";
            reload=SceneManager.LoadSceneAsync(Level,LoadSceneMode.Single);
        }
        public static void Stop() {
            if(!Active)return;
            // Keep the live target until Play mode has stopped; releasing it
            // mid-frame can leave the last camera render pointing at a freed target.
            EditorApplication.isPaused=false;EditorApplication.isPlaying=false;
        }
        public static void Step() {if(Ready){EditorApplication.isPaused=true;EditorApplication.Step();}}
        private static void OnModeChanged(PlayModeStateChange change) {
            if(change==PlayModeStateChange.EnteredPlayMode && Active){stage=0;began=EditorApplication.timeSinceStartup;}
            if(change!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key+"OwnsStartScene",false))return;
            ReleaseView();
            ClearTake();
            string previous=SessionState.GetString(Key+"PreviousStartScene","");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            AudioListener.volume=SessionState.GetFloat(Key+"ListenerVolume",1);
            SessionState.SetBool(Key+"Active",false);SessionState.SetBool(Key+"OwnsStartScene",false);
            Player=null;Sword=null;Effects=null;Gameplay=null;target=null;stage=0;Status="Preview stopped. Saved scenes are unchanged.";
        }
        private static void Set(UnityEngine.Object owner,string field,float value) {
            var data=new SerializedObject(owner);var property=data.FindProperty(field);
            if(property.propertyType==SerializedPropertyType.Integer)property.intValue=Mathf.RoundToInt(value);else property.floatValue=value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Setup() {
            foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
            foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))car.gameObject.SetActive(false);
            foreach(var traffic in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None)){traffic.StopAllCoroutines();traffic.enabled=false;}
            Gameplay.SwitchGameState(GameState.Playing);
            Sword=Player.GetComponent<BikeSwordCombat>();Effects=Player.GetComponent<SwordFinisherEffects>();
            var tuning=JsonUtility.FromJson<FinisherLabTuning>(SessionState.GetString(Key+"Tuning",""));tuning.Apply(Sword,Effects);
            Player.ConfigureTestingMovement(RideSpeed,Player.ForwardAcceleration,Player.SteeringAcceleration,Player.SteeringSpeedLimit);
            Player.GetComponent<TouchInputManager>().SetAim(false);Player.GetComponent<TouchInputManager>().enabled=false;
            typeof(PlayerController).GetField("input",Private).SetValue(Player,new NeutralInput());
            typeof(BikeSwordCombat).GetField("launchKey",Private).SetValue(Sword,KeyCode.None);
            var body=Player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
            body.position=Player.transform.position;body.linearVelocity=Vector3.forward*RideSpeed;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab");
            var instance=UnityEngine.Object.Instantiate(prefab,Player.transform.position+new Vector3(Left ? -3.5f : 3.5f,0,22),Quaternion.identity);
            instance.name="Finisher Lab target";instance.SetActive(false);target=instance.GetComponent<Enemy>();
            Set(target,"health",100);Set(target,"shootingDamage",0);Set(target,"finisherWindow",12);
            typeof(Enemy).GetField("speed",Private).SetValue(target,RideSpeed);
            typeof(Enemy).GetField("path",Private).SetValue(target,new DodgePoint(instance.transform.position+Vector3.forward*10000,instance));
            typeof(Enemy).GetField("followTarget",Private).SetValue(target,instance.transform.position+Vector3.forward*10);
            instance.SetActive(true);
            viewCamera=Camera.main;if(viewCamera==null)throw new InvalidOperationException("Preview scene has no main camera.");
            previousTarget=viewCamera.targetTexture;view=new RenderTexture(1280,720,24){name="Finisher Lab live view"};view.Create();viewCamera.targetTexture=view;
            AudioListener.volume=Sound ? 1 : 0;stage=1;readyAt=EditorApplication.timeSinceStartup;
        }
        private static void Tick() {
            if(takePlaying && viewingTake && take!=null) {
                double now=EditorApplication.timeSinceStartup;playhead+=(float)(now-playbackClock)*TakeSpeed;playbackClock=now;
                if(playhead>take.Duration){if(Loop)playhead=0;else{playhead=take.Duration;takePlaying=false;}}
                take.Seek(playhead);
            }
            if(!Active || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            EditorApplication.QueuePlayerLoopUpdate();
            try {
                if(reload!=null){if(!reload.isDone)return;reload=null;}
                if(stage==0) {
                    Player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();Gameplay=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();
                    if(Player==null || Player.Health==null || Gameplay==null || Time.timeSinceLevelLoad<.35f)return;
                    Setup();
                } else if(stage==1) {
                    if(target==null || !target.IsAlive)return;
                    target.TakeDamage(new DamageInfo(10000,Player.gameObject));
                    if(!target.IsFinisherReady)throw new InvalidOperationException("The preview target could not enter finisher state.");
                    if(SessionState.GetBool(Key+"RecordRequested",false)){SessionState.SetBool(Key+"RecordRequested",false);BeginTake();}
                    Sword.Launch();stage=2;hitAt=0;began=EditorApplication.timeSinceStartup;Status="Live finisher preview";
                } else if(stage==2 && Sword!=null) {
                    if(recording && !EditorApplication.isPaused && recordedFrame!=Time.frameCount) {
                        recordedFrame=Time.frameCount;recordedTime+=1f/60;take.Capture(recordedTime,viewCamera,Player,Sword,Effects);
                        bool finished=Sword.HitCount>0 && (Sword.Phase==BikeSwordCombat.CombatPhase.Pursuing || !Sword.IsActive) && Gameplay.CombatSlowMotionRemaining<=0 && Effects.CameraAmount<.02f && Effects.SmokeParticleCount==0;
                        Status="Recording 3D take: "+recordedTime.ToString("0.00")+" s · "+take.FrameCount+" frames";
                        if(take.IsFull || recordedTime>=20 || (recordedTime>=CaptureLength && finished))CompleteTake();
                    }
                    if(Sword.HitCount>0 && hitAt==0) {
                        hitAt=EditorApplication.timeSinceStartup;
                        if(PauseOnContact && !recording){EditorApplication.isPaused=true;Status="Paused on impact — Step advances one frame";}
                    }
                    if(!recording && !viewingTake && !EditorApplication.isPaused && Loop && hitAt>0 && EditorApplication.timeSinceStartup-hitAt>3
                        && (Sword.Phase==BikeSwordCombat.CombatPhase.Pursuing || !Sword.IsActive)
                        && Gameplay.CombatSlowMotionRemaining<=0 && Effects.CameraAmount<.02f)Replay();
                }
                if(stage==1 && EditorApplication.timeSinceStartup-readyAt>15)throw new InvalidOperationException("Preview target initialization timed out.");
                if(stage==2 && Sword!=null && Sword.HitCount==0 && !EditorApplication.isPaused && EditorApplication.timeSinceStartup-began>25)
                    Status="No hit yet. Replay or reload the saved defaults; check contact timing and dash settings.";
                EditorApplication.QueuePlayerLoopUpdate();
            } catch(Exception error) {Status=error.Message;Debug.LogException(error);Stop();}
        }
        private static void ReleaseView() {
            if(viewCamera!=null)viewCamera.targetTexture=previousTarget;
            if(view!=null) {
                if(EditorApplication.isPlaying)UnityEngine.Object.Destroy(view);
                else {view.Release();UnityEngine.Object.DestroyImmediate(view);}
            }
            view=null;viewCamera=null;previousTarget=null;
        }
    }
}
