using System;
using System.Reflection;
using CyberBiker.Combat;
using CyberBiker.Player;
using CyberBiker.Pooling;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CyberBiker.EditorTools {
    [InitializeOnLoad]
    public static class DodgeLabPreview {
        private const string Key="CyberBiker.DodgeLab.";
        private const string Level="Assets/Level/Scenes/Level.unity";
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private sealed class NeutralInput : IPlayerInput {public Vector2 Steering=>Vector2.zero;public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>Aiming;}
        private static int stage,lastFrame=-1;
        private static Enemy source;
        private static Bullet shot;
        private static Camera camera;
        private static RenderTexture view,previousTarget;
        private static float clock,shotAt;
        public static PlayerController Player {get;private set;}
        public static GameplayManager Gameplay {get;private set;}
        public static CloseDodgeEffects Effects {get;private set;}
        public static bool Active=>SessionState.GetBool(Key+"Active",false);
        public static bool Ready=>Active && stage==2 && Player!=null && Effects!=null;
        public static RenderTexture View=>view;
        public static string Status {get;private set;}="Start a preview to test a real passing bullet.";
        public static bool Loop {get=>SessionState.GetBool(Key+"Loop",true);set=>SessionState.SetBool(Key+"Loop",value);}
        public static bool Sound {get=>SessionState.GetBool(Key+"Sound",true);set{SessionState.SetBool(Key+"Sound",value);if(Active)AudioListener.volume=value ? 1 : 0;}}
        public static bool Aiming {get=>SessionState.GetBool(Key+"Aiming",false);set{SessionState.SetBool(Key+"Aiming",value);if(Ready)Player.GetComponent<TouchInputManager>().SetAim(value);}}
        public static float Gap {get=>SessionState.GetFloat(Key+"Gap",1.25f);set=>SessionState.SetFloat(Key+"Gap",Mathf.Clamp(value,-6,6));}
        public static float RideSpeed {get=>SessionState.GetFloat(Key+"RideSpeed",65);set=>SessionState.SetFloat(Key+"RideSpeed",Mathf.Clamp(value,20,150));}
        public static float BulletSpeed {get=>SessionState.GetFloat(Key+"BulletSpeed",70);set=>SessionState.SetFloat(Key+"BulletSpeed",Mathf.Clamp(value,20,150));}
        public static float Approach {get=>SessionState.GetFloat(Key+"Approach",.65f);set=>SessionState.SetFloat(Key+"Approach",Mathf.Clamp(value,.2f,2));}
        static DodgeLabPreview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=ModeChanged;AssemblyReloadEvents.beforeAssemblyReload+=ReleaseView;}
        public static void Start(CloseDodgeSettings settings){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the current Play session before starting the Dodge Lab preview.");
            SessionState.SetString(Key+"PreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetFloat(Key+"Volume",AudioListener.volume);SessionState.SetBool(Key+"OwnsStart",true);SessionState.SetBool(Key+"Active",true);
            Apply(settings);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Level);stage=0;Status="Starting preview…";EditorApplication.isPlaying=true;
        }
        public static void Apply(CloseDodgeSettings settings){settings.Normalize();SessionState.SetString(Key+"Settings",JsonUtility.ToJson(settings));if(Gameplay!=null){Gameplay.SetCloseDodgePreview(settings);if(EditorApplication.isPaused && Effects!=null && Effects.TriggerCount>0)Seek(Effects.Age);}}
        public static void Stop(){if(!Active)return;EditorApplication.isPaused=false;EditorApplication.isPlaying=false;}
        public static void Step(){if(Ready){EditorApplication.isPaused=true;EditorApplication.Step();}}
        private static void ModeChanged(PlayModeStateChange mode){
            if(mode==PlayModeStateChange.EnteredPlayMode && Active){stage=0;clock=0;lastFrame=-1;}
            if(mode!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key+"OwnsStart",false))return;
            ReleaseView();var previous=SessionState.GetString(Key+"PreviousStart","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            AudioListener.volume=SessionState.GetFloat(Key+"Volume",1);SessionState.SetBool(Key+"Active",false);SessionState.SetBool(Key+"OwnsStart",false);
            Player=null;Gameplay=null;Effects=null;source=null;shot=null;stage=0;Status="Preview stopped. Your saved scenes are unchanged.";
        }
        private static void Setup(){
            foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>())director.enabled=false;
            foreach(var traffic in UnityEngine.Object.FindObjectsByType<TrafficSpawner>()){traffic.StopAllCoroutines();traffic.enabled=false;}
            foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>())enemy.gameObject.SetActive(false);
            foreach(var car in UnityEngine.Object.FindObjectsByType<Car>())car.gameObject.SetActive(false);
            for(int i=Bullet.ActiveShots.Count-1;i>=0;i--)PrefabPool.Release(Bullet.ActiveShots[i].gameObject);
            Gameplay.SwitchGameState(GameState.Playing);Gameplay.SetCloseDodgePreview(JsonUtility.FromJson<CloseDodgeSettings>(SessionState.GetString(Key+"Settings","{}")));
            Effects=Player.GetComponent<CloseDodgeEffects>();if(Effects==null)Effects=Player.gameObject.AddComponent<CloseDodgeEffects>();
            Player.GetComponent<TouchInputManager>().SetAim(Aiming);Player.GetComponent<TouchInputManager>().enabled=false;
            Player.GetComponent<PlayerProjectileEvade>().enabled=false;typeof(PlayerController).GetField("input",Private).SetValue(Player,new NeutralInput());
            Player.ConfigureTestingMovement(RideSpeed,Player.ForwardAcceleration,Player.SteeringAcceleration,Player.SteeringSpeedLimit);
            var body=Player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;body.linearVelocity=Vector3.forward*RideSpeed;
            source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),Player.transform.position+Vector3.forward*100,Quaternion.identity).GetComponent<Enemy>();
            camera=Camera.main;if(camera==null)throw new InvalidOperationException("Dodge preview needs the gameplay camera.");
            previousTarget=camera.targetTexture;view=new RenderTexture(1280,720,24){name="Dodge Lab live view"};view.Create();camera.targetTexture=view;
            AudioListener.volume=Sound ? 1 : 0;stage=1;
        }
        public static void Replay(){
            if(!Ready)return;EditorApplication.isPaused=false;Effects.ResetForPreview();if(shot!=null && shot.gameObject.activeInHierarchy)PrefabPool.Release(shot.gameObject);
            var body=Player.GetComponent<Rigidbody>();body.linearVelocity=Vector3.forward*RideSpeed;
            Player.ConfigureTestingMovement(RideSpeed,Player.ForwardAcceleration,Player.SteeringAcceleration,Player.SteeringSpeedLimit);
            source.transform.position=Player.transform.position+Vector3.forward*(BulletSpeed*Approach)+Vector3.right*Gap;
            var enemyBody=source.GetComponent<Rigidbody>();if(enemyBody!=null)enemyBody.position=source.transform.position;Physics.SyncTransforms();
            var gun=source.GunMuzzle(0);var point=Effects.Center+Vector3.right*Gap;var request=new ShotRequest(gun.position,point-gun.position,source.gameObject);
            var parent=GameObject.FindGameObjectWithTag("Finish").transform;var weapon=new ProjectileWeapon(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bullet.prefab"),.01f,0,parent);
            var guide=EnemyShotTrajectory.Create(source,gun,Player.transform,.6f);
            if(!weapon.TryFireImmediate(request,Time.time))throw new InvalidOperationException("Could not spawn the preview bullet.");
            shot=weapon.LastProjectile;shot.bulletSpeed=BulletSpeed;guide.Commit(request,point,shot);
            source.GetComponent<EnemyGunEffects>()?.Fire(0,gun,request.Direction);shotAt=clock;Status="Incoming bullet — waiting for its closest passing point.";
        }
        public static void Seek(float time){
            if(!Ready || Effects.TriggerCount==0)return;EditorApplication.isPaused=true;Effects.SeekPreview(time);
            var controller=camera.GetComponent<CameraController>();typeof(CameraController).GetMethod("LateUpdate",Private).Invoke(controller,null);camera.Render();
            Status="Scrubbing the post-pass effect. Replay resets the bullet and tests the full pass again.";
        }
        private static void Tick(){
            if(!Active || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            EditorApplication.QueuePlayerLoopUpdate();
            try{
                if(stage==0){Player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();Gameplay=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();if(Player==null || Player.Health==null || Gameplay==null || Time.timeSinceLevelLoad<.35f)return;Setup();}
                else if(stage==1 && source.HasInitialized){source.enabled=false;source.StopAllCoroutines();typeof(Enemy).GetField("armed",Private).SetValue(source,true);var body=source.GetComponent<Rigidbody>();body.linearVelocity=Vector3.zero;body.isKinematic=true;stage=2;Replay();}
                if(!Ready || EditorApplication.isPaused || lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;clock+=FinisherClock.UnscaledDeltaTime;
                // Keep the fixture ahead like a riding enemy. Leaving it fixed
                // in world space lets the player overtake it during the preview.
                var sourcePosition=Player.transform.position+Vector3.forward*(BulletSpeed*Approach)+Vector3.right*Gap;
                source.transform.position=sourcePosition;source.GetComponent<Rigidbody>().position=sourcePosition;
                if(Effects.TriggerCount>0)Status=Effects.IsActive ? "Close dodge · "+Effects.LastPassDistance.ToString("0.00")+" m · "+Time.timeScale.ToString("0.00")+"× world speed" : "Recovered. Replay or scrub the effect timeline.";
                else if(clock-shotAt>Approach+.5f)Status="No close dodge: try reducing Pass offset or increasing Pass radius. A direct hit does not count.";
                if(Loop && clock-shotAt>Approach+Gameplay.CloseDodge.Duration+1.3f)Replay();
            }catch(Exception error){Status=error.Message;Debug.LogException(error);Stop();}
        }
        private static void ReleaseView(){if(camera!=null)camera.targetTexture=previousTarget;if(view!=null){view.Release();UnityEngine.Object.DestroyImmediate(view);}view=null;camera=null;}
    }
}
