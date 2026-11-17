using System;
using System.Collections.Generic;
using System.Reflection;
using CyberBiker.Audio;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;

namespace CyberBiker.EditorTools {
    [InitializeOnLoad]
    public static class RiderReactionLabPreview {
        public enum Scenario { IdleRide, CarApproach, ClosePass, NearbyEnemy, VisibleShooter, ShooterLeavesView }
        private const string Key="CyberBiker.RiderReactions.";
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private struct BonePose {
            public Transform bone;public Vector3 position,scale;public Quaternion rotation;
            public BonePose(Transform t){bone=t;position=t.localPosition;rotation=t.localRotation;scale=t.localScale;}
            public void Restore(){if(bone==null)return;bone.localPosition=position;bone.localRotation=rotation;bone.localScale=scale;}
        }
        private static readonly List<BonePose> baseline=new List<BonePose>();
        private static Car car;
        private static Enemy enemy;
        private static Camera inspection;
        private static RenderTexture texture;
        private static CameraLabDriver driver;
        private static int stage,lastFrame=-1;
        private static float age,lastShot=-100;
        private static double captureAfter;
        private static bool passed,rebake,restorePause;
        public static RiderLife Life {get;private set;}
        public static PlayerController Player=>CameraLabPreview.Player;
        public static bool Active=>SessionState.GetBool(Key+"Active",false);
        public static bool Ready=>Active && stage==2 && Life!=null && CameraLabPreview.Ready;
        public static float Age=>age;
        public const float Duration=6;
        public static RenderTexture View=>GameView ? CameraLabPreview.View : texture;
        public static string Status {get;private set;}="Start preview to tune the real rider reactions.";
        public static Scenario Current {get=>(Scenario)SessionState.GetInt(Key+"Scenario",1);set{if(value==Current)return;SessionState.SetInt(Key+"Scenario",(int)value);if(Ready)Replay();}}
        public static bool Loop {get=>SessionState.GetBool(Key+"Loop",true);set=>SessionState.SetBool(Key+"Loop",value);}
        public static bool Sound {get=>SessionState.GetBool(Key+"Sound",false);set{SessionState.SetBool(Key+"Sound",value);if(Active)CameraLabPreview.Sound=value;}}
        public static bool GameView {get=>SessionState.GetBool(Key+"GameView",false);set=>SessionState.SetBool(Key+"GameView",value);}
        public static bool HeadCloseup {get=>SessionState.GetBool(Key+"Closeup",false);set=>SessionState.SetBool(Key+"Closeup",value);}
        public static bool Aiming {get=>SessionState.GetBool(Key+"Aiming",false);set{
            if(value==Aiming)return;SessionState.SetBool(Key+"Aiming",value);if(!Ready)return;
            restorePause=EditorApplication.isPaused;EditorApplication.isPaused=false;
            CameraLabPreview.SwitchView(value ? CameraLabPreview.ViewMode.CombatAim : CameraLabPreview.ViewMode.Riding);
            rebake=true;captureAfter=EditorApplication.timeSinceStartup+1;
        }}
        public static float Side {get=>SessionState.GetFloat(Key+"Side",4);set=>SessionState.SetFloat(Key+"Side",Mathf.Clamp(value,-8,8));}
        public static float CarStart {get=>SessionState.GetFloat(Key+"Start",70);set=>SessionState.SetFloat(Key+"Start",Mathf.Clamp(value,10,100));}
        public static float EnemyDistance {get=>SessionState.GetFloat(Key+"EnemyDistance",24);set=>SessionState.SetFloat(Key+"EnemyDistance",Mathf.Clamp(value,10,70));}
        public static float PassStrength {get=>SessionState.GetFloat(Key+"Strength",1);set=>SessionState.SetFloat(Key+"Strength",Mathf.Clamp01(value));}
        public static float Speed {get=>SessionState.GetFloat(Key+"Speed",65);set{SessionState.SetFloat(Key+"Speed",Mathf.Clamp(value,20,150));if(Active)CameraLabPreview.Speed=Speed;}}
        public static float Steering {get=>CameraLabPreview.SteeringAmount;set=>CameraLabPreview.SteeringAmount=value;}
        public static float OrbitYaw {get=>SessionState.GetFloat(Key+"Yaw",-35);set=>SessionState.SetFloat(Key+"Yaw",value);}
        public static float OrbitPitch {get=>SessionState.GetFloat(Key+"Pitch",12);set=>SessionState.SetFloat(Key+"Pitch",Mathf.Clamp(value,-15,65));}
        public static float Distance {get=>SessionState.GetFloat(Key+"Distance",10);set=>SessionState.SetFloat(Key+"Distance",Mathf.Clamp(value,1.5f,20));}
        public static Vector3 Pan {get;set;}
        private static RiderReactionLabTuning Draft=>JsonUtility.FromJson<RiderReactionLabTuning>(SessionState.GetString(Key+"Draft","{}"));
        static RiderReactionLabPreview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=ModeChanged;AssemblyReloadEvents.beforeAssemblyReload+=ReleaseView;}
        public static void Start(RiderReactionLabTuning tuning){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the current Play session before starting Rider Reactions Lab.");
            tuning.Validate();SessionState.SetString(Key+"Draft",JsonUtility.ToJson(tuning));SessionState.SetBool(Key+"Active",true);stage=0;age=0;rebake=restorePause=passed=false;lastFrame=-1;lastShot=-100;baseline.Clear();
            CameraLabPreview.Speed=Speed;CameraLabPreview.TargetSide=150;CameraLabPreview.SteeringAmount=0;CameraLabPreview.Sound=Sound;
            try{CameraLabPreview.Start(CameraLabTuning.ReadSavedDefaults(),Aiming ? CameraLabPreview.ViewMode.CombatAim : CameraLabPreview.ViewMode.Riding);}catch{SessionState.SetBool(Key+"Active",false);throw;}
        }
        public static void Apply(RiderReactionLabTuning tuning){
            tuning.Validate();SessionState.SetString(Key+"Draft",JsonUtility.ToJson(tuning));if(!Ready)return;tuning.Apply(Life);
            if(EditorApplication.isPaused && baseline.Count>0)Seek(age);
        }
        public static void Stop(){if(Active)CameraLabPreview.Stop();}
        public static void Step(){if(Ready)Seek(Mathf.Min(Duration,age+1f/60));}
        public static void Replay(){if(!Ready)return;age=0;passed=false;lastShot=-100;GameAudio.Active?.ForgetCar(car);Life.ResetReactionPreview();Place(0);if(EditorApplication.isPaused)Seek(0);}
        private static void Setup(){
            Life=Player.GetComponent<RiderLife>();if(Life==null)throw new InvalidOperationException("The player has no Rider Life component.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Cars/Car_04.prefab");
            car=UnityEngine.Object.Instantiate(prefab,Player.transform.position+Vector3.forward*1000,Quaternion.identity).GetComponent<Car>();car.OnPoolRent(prefab);car.enabled=false;
            foreach(var c in car.GetComponentsInChildren<Collider>())c.isTrigger=true;
            enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),Player.transform.position+Vector3.forward*500,Quaternion.identity).GetComponent<Enemy>();
            inspection=new GameObject("Rider Reactions inspection camera").AddComponent<Camera>();inspection.CopyFrom(CameraLabPreview.Camera);inspection.enabled=false;inspection.fieldOfView=45;
            texture=new RenderTexture(1280,720,24){name="Rider Reactions live view"};texture.Create();inspection.targetTexture=texture;
            stage=1;captureAfter=EditorApplication.timeSinceStartup+.65;
        }
        private static void CaptureBaseline(){
            Life.enabled=false;baseline.Clear();foreach(var bone in Player.GetComponentsInChildren<Transform>(true))if(bone!=Player.transform)baseline.Add(new BonePose(bone));Life.enabled=true;Life.ResetReactionPreview();
        }
        private static void Bind(){
            enemy.enabled=false;enemy.StopAllCoroutines();var body=enemy.GetComponent<Rigidbody>();body.linearVelocity=Vector3.zero;body.isKinematic=true;
            foreach(var c in enemy.GetComponentsInChildren<Collider>())c.isTrigger=true;
            driver=Player.GetComponent<CameraLabDriver>();driver.Tick+=Drive;Draft.Apply(Life);CaptureBaseline();stage=2;Replay();
        }
        private static bool IsCar=>Current==Scenario.CarApproach || Current==Scenario.ClosePass;
        private static bool IsEnemy=>Current==Scenario.NearbyEnemy || Current==Scenario.VisibleShooter || Current==Scenario.ShooterLeavesView;
        private static float Gap(float time)=>CarStart-Mathf.Max(0,time-.3f)*Speed;
        private static void Place(float time){
            var origin=Player.transform.position;
            car.transform.position=origin+new Vector3(Side,0,IsCar ? Gap(time) : 1000);
            bool enemyPresent=IsEnemy && (Current!=Scenario.NearbyEnemy || time<1.5f);
            bool offscreen=Current==Scenario.ShooterLeavesView && time>=2;
            var pos=origin+new Vector3(offscreen ? Mathf.Sign(Side==0 ? 1 : Side)*80 : Side,0,enemyPresent ? EnemyDistance : 500);
            enemy.transform.position=pos;enemy.GetComponent<Rigidbody>().position=pos;
            typeof(Enemy).GetField("armed",Private).SetValue(enemy,enemyPresent && Current!=Scenario.NearbyEnemy);
        }
        private static void Drive(){
            if(!Ready || EditorApplication.isPaused || lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            age+=Time.deltaTime;if(age>Duration){if(Loop)Replay();else{age=Duration;EditorApplication.isPaused=true;}}
            Place(age);
            // This fixture owns the crossing so live playback and scrubbing use
            // the same adjustable strength, without a second audio-driven kick.
            GameAudio.Active?.ForgetCar(car);
            if(IsCar && !passed && Gap(age)<=0){Life.ReactToPass(car.transform.position,PassStrength,car.transform);passed=true;}
            if(IsEnemy && Current!=Scenario.NearbyEnemy && age-lastShot>.7f && age<2){
                var gun=enemy.GunMuzzle(0);enemy.GetComponent<EnemyGunEffects>()?.Fire(0,gun,Player.transform.position+Vector3.up*3-gun.position);lastShot=age;
            }
        }
        public static void Seek(float seconds){
            if(!Ready || baseline.Count==0)return;EditorApplication.isPaused=true;age=Mathf.Clamp(seconds,0,Duration);
            Life.ResetReactionPreview();foreach(var pose in baseline)pose.Restore();Life.ResetReactionPreview();
            bool crossed=false;float time=0;
            while(time<age-.00001f){float dt=Mathf.Min(1f/60,age-time);time+=dt;Place(time);Physics.SyncTransforms();if(IsCar && !crossed && Gap(time)<=0){Life.ReactToPass(car.transform.position,PassStrength,car.transform);crossed=true;}Life.StepReactionPreview(dt);}
            Place(age);passed=crossed;Render();
        }
        public static void Render(){
            if(!Ready || inspection==null)return;
            if(GameView){CameraLabPreview.Camera.Render();return;}
            var focus=Player.transform.position+Vector3.up*2.7f;
            if(HeadCloseup)foreach(var bone in Player.GetComponentsInChildren<Transform>())if(bone.name=="DEF-spine.006"){focus=bone.position;break;}
            var rotation=Quaternion.Euler(OrbitPitch,OrbitYaw,0);inspection.transform.SetPositionAndRotation(focus+Pan-rotation*Vector3.forward*Distance,rotation);inspection.Render();
        }
        private static void Tick(){
            if(!Active || !EditorApplication.isPlaying || !CameraLabPreview.Ready)return;
            try{
                if(stage==0)Setup();
                if(stage==1 && enemy.HasInitialized && EditorApplication.timeSinceStartup>captureAfter && (!Aiming || Player.IsPistolVisuallyReady))Bind();
                if(!Ready)return;
                if(rebake && EditorApplication.timeSinceStartup>captureAfter && (!Aiming || Player.IsPistolVisuallyReady)){CaptureBaseline();rebake=false;Replay();if(restorePause)EditorApplication.isPaused=true;}
                Render();Status="Head "+Life.HeadYaw.ToString("0.0")+"° · pitch "+Life.HeadPitch.ToString("0.0")+"° · neck "+Life.NeckTurn.ToString("0.0")+"° · body "+Life.BodyRoll.ToString("0.0")+"° · "+(Life.PriorityEnemy!=null ? "visible shooter priority" : Life.HasAttention ? "looking at nearby traffic / enemy" : "normal riding pose");
            }catch(Exception error){Status=error.Message;Debug.LogException(error);Stop();}
        }
        private static void ModeChanged(PlayModeStateChange mode){
            if(mode==PlayModeStateChange.EnteredPlayMode && Active){stage=0;lastFrame=-1;}
            if(mode!=PlayModeStateChange.EnteredEditMode || !Active)return;
            ReleaseView();Life=null;car=null;enemy=null;driver=null;baseline.Clear();stage=0;SessionState.SetBool(Key+"Active",false);Status="Preview stopped. Your saved scene is unchanged.";
        }
        private static void ReleaseView(){if(inspection!=null)UnityEngine.Object.DestroyImmediate(inspection.gameObject);if(texture!=null){texture.Release();UnityEngine.Object.DestroyImmediate(texture);}inspection=null;texture=null;}
    }
}
