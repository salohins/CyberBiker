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
    public static class CameraLabPreview {
        public enum ViewMode { Riding, CruisingAim, CruisingAimLeft, CruisingAimRight, Airborne, Launch, Finisher, Dodge, CombatRiding, AutoRiding, CombatAim, CombatAimLeft, CombatAimRight, AutoAim }
        private const string Key="CyberBiker.CameraLab.";
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private sealed class PreviewInput:IPlayerInput {
            public Vector2 Steering=>new Vector2(SteeringAmount,0);
            public Vector2 AimDelta=>Vector2.zero;
            public bool IsAiming=>IsAim;
        }
        private sealed class SilentWeapon:Weapon {
            public SilentWeapon():base(1){}
            protected override bool Fire(ShotRequest request)=>false;
        }
        private static int stage;
        private static Enemy source;
        private static Renderer[] sourceRenderers;
        private static bool[] sourceRendererStates;
        private static bool sourceVisible=true;
        private static PlayerController bound;
        private static CameraLabDriver driver;
        private static RenderTexture texture,previousTexture;
        private static Camera ownCamera;
        private static float baseY;
        private static bool applied;
        public static bool Active=>SessionState.GetBool(Key+"Active",false);
        private static int Provider=>SessionState.GetInt(Key+"Provider",0);
        public static ViewMode Mode {get=>(ViewMode)SessionState.GetInt(Key+"Mode",0);private set=>SessionState.SetInt(Key+"Mode",(int)value);}
        public static bool Ready=>Active && EditorApplication.isPlaying && Player!=null && (Provider==1 ? FinisherLabPreview.Ready : Provider==2 ? DodgeLabPreview.Ready : stage==2);
        public static PlayerController Player=>Provider==1 ? FinisherLabPreview.Player : Provider==2 ? DodgeLabPreview.Player : bound;
        public static GameplayManager Gameplay=>Provider==1 ? FinisherLabPreview.Gameplay : Provider==2 ? DodgeLabPreview.Gameplay : UnityEngine.Object.FindAnyObjectByType<GameplayManager>();
        public static Camera Camera=>Ready ? UnityEngine.Camera.main : null;
        public static CameraController Rig=>Camera!=null ? Camera.GetComponent<CameraController>() : null;
        public static RenderTexture View=>Provider==1 ? FinisherLabPreview.View : Provider==2 ? DodgeLabPreview.View : texture;
        public static string Status=>!Active ? "Start preview to move and tune the real gameplay camera." : Provider==1 ? FinisherLabPreview.Status : Provider==2 ? DodgeLabPreview.Status : Ready ? "Live "+ObjectNames.NicifyVariableName(Mode.ToString())+" view · combat blend "+Rig.CombatCameraAmount.ToString("P0") : "Starting live preview…";
        public static bool EnemyPresent {get=>SessionState.GetBool(Key+"EnemyPresent",true);set=>SessionState.SetBool(Key+"EnemyPresent",value);}
        public static float EnemyDistance {get=>SessionState.GetFloat(Key+"EnemyDistance",25);set=>SessionState.SetFloat(Key+"EnemyDistance",Mathf.Clamp(value,2,200));}
        public static float SteeringAmount {get=>SessionState.GetFloat(Key+"Steering",0);set=>SessionState.SetFloat(Key+"Steering",Mathf.Clamp(value,-1,1));}
        public static float Lane {get=>SessionState.GetFloat(Key+"Lane",0);set=>SessionState.SetFloat(Key+"Lane",value);}
        public static float TargetSide {get=>SessionState.GetFloat(Key+"Target",2);set=>SessionState.SetFloat(Key+"Target",value);}
        public static float Speed {get=>SessionState.GetFloat(Key+"Speed",65);set=>SessionState.SetFloat(Key+"Speed",Mathf.Clamp(value,20,150));}
        public static bool Hold {get=>SessionState.GetBool(Key+"Hold",true);set{SessionState.SetBool(Key+"Hold",value);FinisherLabPreview.PauseOnContact=value;}}
        public static bool Sound {get=>SessionState.GetBool(Key+"Sound",false);set{SessionState.SetBool(Key+"Sound",value);if(Provider==1)FinisherLabPreview.Sound=value;else if(Provider==2)DodgeLabPreview.Sound=value;else if(Active)AudioListener.volume=value ? 1 : 0;}}
        public static bool IsCruisingAim(ViewMode mode)=>mode==ViewMode.CruisingAim || mode==ViewMode.CruisingAimLeft || mode==ViewMode.CruisingAimRight;
        public static bool IsCombatAim(ViewMode mode)=>mode==ViewMode.CombatAim || mode==ViewMode.CombatAimLeft || mode==ViewMode.CombatAimRight;
        public static bool IsAimingMode(ViewMode mode)=>IsCruisingAim(mode) || IsCombatAim(mode) || mode==ViewMode.AutoAim;
        public static bool AutomaticView=>Mode==ViewMode.AutoRiding || Mode==ViewMode.AutoAim;
        public static int ForcedShoulder=>Mode==ViewMode.CruisingAimLeft || Mode==ViewMode.CombatAimLeft ? 1 : Mode==ViewMode.CruisingAimRight || Mode==ViewMode.CombatAimRight ? -1 : 0;
        private static bool IsAim=>IsAimingMode(Mode);
        private static CameraLabTuning Draft=>JsonUtility.FromJson<CameraLabTuning>(SessionState.GetString(Key+"Draft","{}"));
        static CameraLabPreview(){EditorApplication.update+=Update;EditorApplication.playModeStateChanged+=ModeChanged;AssemblyReloadEvents.beforeAssemblyReload+=Release;}
        public static void Start(CameraLabTuning tuning,ViewMode mode=ViewMode.Riding){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the current Play session before starting Camera Lab.");
            Mode=mode;SessionState.SetString(Key+"Draft",JsonUtility.ToJson(tuning));SessionState.SetBool(Key+"Active",true);stage=0;bound=null;applied=false;
            int provider=mode==ViewMode.Finisher ? 1 : mode==ViewMode.Dodge ? 2 : 0;SessionState.SetInt(Key+"Provider",provider);
            if(provider==1){FinisherLabPreview.Loop=true;FinisherLabPreview.PauseOnContact=Hold;FinisherLabPreview.Sound=Sound;FinisherLabPreview.RideSpeed=Speed;FinisherLabPreview.Start(FinisherLabTuning.ReadSavedDefaults());}
            else if(provider==2){DodgeLabPreview.Loop=true;DodgeLabPreview.Sound=Sound;DodgeLabPreview.RideSpeed=Speed;DodgeLabPreview.Start(DodgeLabWindow.ReadSaved());}
            else {
                SessionState.SetString(Key+"PreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));SessionState.SetFloat(Key+"Volume",AudioListener.volume);
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Level/Scenes/Level.unity");EditorApplication.isPlaying=true;
            }
        }
        public static void SwitchView(ViewMode mode){
            if(mode==Mode)return;
            if(!Active){Mode=mode;return;}
            int next=mode==ViewMode.Finisher ? 1 : mode==ViewMode.Dodge ? 2 : 0;
            if(next==0 && Provider==0){Mode=mode;Apply(Draft);if(Ready){ConfigureCombatPreview(true);Player.GetComponent<TouchInputManager>().SetAim(IsAim);if(mode==ViewMode.Launch)Player.GetComponent<BikeSwordCombat>().Launch();else Player.GetComponent<BikeSwordCombat>().CancelForAim();RefreshCamera();}return;}
            SessionState.SetInt(Key+"Pending",(int)mode);Stop();
        }
        public static void Stop(){if(!Active)return;if(Provider==1)FinisherLabPreview.Stop();else if(Provider==2)DodgeLabPreview.Stop();else{EditorApplication.isPaused=false;EditorApplication.isPlaying=false;}}
        public static void Replay(){if(!Ready)return;EditorApplication.isPaused=false;if(Provider==1)FinisherLabPreview.Replay();else if(Provider==2)DodgeLabPreview.Replay();else if(Mode==ViewMode.Launch){Player.GetComponent<BikeSwordCombat>().CancelForAim();}else RefreshCamera();applied=false;}
        public static void Step(){if(Ready){EditorApplication.isPaused=true;EditorApplication.Step();}}
        private static void ModeChanged(PlayModeStateChange state){
            if(!Active)return;if(state==PlayModeStateChange.EnteredPlayMode){stage=0;bound=null;applied=false;}
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            if(Provider==0){Release();string previous=SessionState.GetString(Key+"PreviousStart","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);AudioListener.volume=SessionState.GetFloat(Key+"Volume",1);}
            bound=null;driver=null;source=null;stage=0;SessionState.SetBool(Key+"Active",false);
            int pending=SessionState.GetInt(Key+"Pending",-1);SessionState.SetInt(Key+"Pending",-1);
            if(pending>=0)EditorApplication.delayCall+=()=>Start(Draft,(ViewMode)pending);
        }
        private static void Setup(){
            foreach(var d in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>())d.enabled=false;
            foreach(var s in UnityEngine.Object.FindObjectsByType<TrafficSpawner>()){s.StopAllCoroutines();s.enabled=false;}
            foreach(var e in UnityEngine.Object.FindObjectsByType<Enemy>())e.gameObject.SetActive(false);
            foreach(var c in UnityEngine.Object.FindObjectsByType<Car>())c.gameObject.SetActive(false);
            for(int i=Bullet.ActiveShots.Count-1;i>=0;i--)PrefabPool.Release(Bullet.ActiveShots[i].gameObject);
            Gameplay.SwitchGameState(GameState.Playing);Gameplay.PistolInteractionMode=PistolInteraction.AimAndAutoFire;
            var input=bound.GetComponent<TouchInputManager>();input.SetAim(IsAim);input.enabled=false;
            bound.GetComponent<PlayerProjectileEvade>().enabled=false;
            typeof(PlayerController).GetField("input",Private).SetValue(bound,new PreviewInput());typeof(PlayerController).GetField("weapon",Private).SetValue(bound,new SilentWeapon());
            baseY=bound.transform.position.y;var body=bound.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
            bound.ConfigureTestingMovement(Speed,bound.ForwardAcceleration,bound.SteeringAcceleration,bound.SteeringSpeedLimit);
            source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"),bound.transform.position+Vector3.forward*50,Quaternion.identity).GetComponent<Enemy>();
            ownCamera=UnityEngine.Camera.main;previousTexture=ownCamera.targetTexture;texture=new RenderTexture(1280,720,24){name="Camera Lab live view"};texture.Create();ownCamera.targetTexture=texture;
            AudioListener.volume=Sound ? 1 : 0;stage=1;
        }
        private static void Bind(){
            if(driver!=null)driver.Tick=null;bound=Player;driver=bound.GetComponent<CameraLabDriver>();if(driver==null)driver=bound.gameObject.AddComponent<CameraLabDriver>();driver.Tick=Drive;applied=false;
        }
        private static void Drive(){
            if(!Ready)return;Player.xThrow=SteeringAmount;if(Provider!=0)return;
            ConfigureCombatPreview(false);
            Player.grounded=Mode!=ViewMode.Airborne;
            var body=Player.GetComponent<Rigidbody>();var pos=body.position;pos.x=Lane;pos.y=baseY+(Mode==ViewMode.Airborne ? 3 : 0);body.position=pos;Player.transform.position=pos;
            var sword=Player.GetComponent<BikeSwordCombat>();if(Mode==ViewMode.Launch && !sword.IsActive)sword.Launch();
            body.linearVelocity=new Vector3(0,0,Mode==ViewMode.Launch ? sword.SpeedLimit(Speed) : Speed);
            if(source!=null){
                bool show=Mode!=ViewMode.Riding && !IsCruisingAim(Mode) && (!AutomaticView || EnemyPresent);
                // Keep the animated fixture alive. Repeatedly deactivating its
                // rig can race Unity's deferred playable-graph destruction.
                if(sourceVisible!=show && sourceRenderers!=null){for(int i=0;i<sourceRenderers.Length;i++)if(sourceRenderers[i]!=null)sourceRenderers[i].enabled=show && sourceRendererStates[i];sourceVisible=show;}
                var p=pos+new Vector3(TargetSide,0,!show ? 10000 : AutomaticView ? EnemyDistance : 25);source.transform.position=p;source.GetComponent<Rigidbody>().position=p;
            }
        }
        private static void ConfigureCombatPreview(bool instant){
            if(Rig==null)return;
            Rig.SetCombatCameraPreview(Provider==0 && (Mode==ViewMode.Riding || IsCruisingAim(Mode)) ? false : Provider==0 && (Mode==ViewMode.CombatRiding || IsCombatAim(Mode)) ? true : (bool?)null,instant);
        }
        public static void Apply(CameraLabTuning tuning){
            tuning.Normalize();SessionState.SetString(Key+"Draft",JsonUtility.ToJson(tuning));if(!Ready)return;
            tuning.ApplyScope(CameraLabTuning.Scope.Rig,Rig);tuning.ApplyScope(CameraLabTuning.Scope.Launch,Player.GetComponent<BikeLaunchEffects>());tuning.ApplyScope(CameraLabTuning.Scope.Finisher,Player.GetComponent<SwordFinisherEffects>());
            var profile=ScriptableObject.CreateInstance<CloseDodgeProfile>();try{profile.settings=Gameplay.CloseDodge.Copy();tuning.ApplyScope(CameraLabTuning.Scope.Dodge,profile);Gameplay.SetCloseDodgePreview(profile.settings);}finally{UnityEngine.Object.DestroyImmediate(profile);}
            if(ForcedShoulder!=0){using(var data=new SerializedObject(Rig)){data.FindProperty("dynamicAimShoulder").boolValue=false;foreach(var field in new[]{"aimOffset","combatAim.offset"}){var p=data.FindProperty(field);var v=p.vector3Value;v.x=Mathf.Abs(v.x)*ForcedShoulder;p.vector3Value=v;}data.ApplyModifiedPropertiesWithoutUndo();}}
            ConfigureCombatPreview(true);
            applied=true;if(EditorApplication.isPaused)RefreshCamera();
        }
        private static void Set(string field,object value)=>typeof(CameraController).GetField(field,Private).SetValue(Rig,value);
        public static void RefreshCamera(){
            if(!Ready)return;Drive();var draft=Draft;float N(string f)=>draft.Get(CameraLabTuning.Scope.Rig,f).number;
            float amount=Player.grounded && !IsAim ? Rig.CombatCameraAmount : 0;
            float Blend(string normal,string combat)=>Mathf.Lerp(N(normal),N("combatRiding."+combat),amount);
            float aimAmount=IsAim ? Rig.CombatCameraAmount : 0;
            float AimBlend(string normal,string combat)=>Mathf.Lerp(N(normal),N("combatAim."+combat),aimAmount);
            var offset=draft.Get(CameraLabTuning.Scope.Rig,IsAim ? "aimOffset" : Mode==ViewMode.Airborne && N("useAirborneOffset")>.5f ? "airborneOffset" : "targetOffset").vector;
            if(IsAim)offset=Vector3.Lerp(offset,draft.Get(CameraLabTuning.Scope.Rig,"combatAim.offset").vector,aimAmount);
            else if(Mode!=ViewMode.Airborne)offset=Vector3.Lerp(offset,draft.Get(CameraLabTuning.Scope.Rig,"combatRiding.offset").vector,amount);
            Set("fov",IsAim ? AimBlend("aimFov","fov") : Mode==ViewMode.Airborne ? N("airborneFov") : Blend("ridingFov","fov"));Set("cameraXangle",IsAim ? AimBlend("aimPitch","pitch") : Mode==ViewMode.Airborne ? N("airbornePitch") : Blend("ridingPitch","pitch"));Set("sideOffset",SteeringAmount);Set("rotationOffset",SteeringAmount*(IsAim ? AimBlend("aimAngleShiftOffset","steeringRoll") : Blend("angleShiftOffset","steeringRoll")));
            typeof(CameraController).GetMethod("Update",Private).Invoke(Rig,null);
            if(IsAim)offset.x=0;
            offset-=Camera.transform.right*SteeringAmount*(IsAim ? AimBlend("aimXShiftOffset","shiftX") : Blend("xShiftOffset","shiftX"));offset+=Camera.transform.up*Mathf.Abs(SteeringAmount)*(IsAim ? AimBlend("aimYShiftOffset","shiftY") : Blend("yShiftOffset","shiftY"));offset+=Camera.transform.forward*Mathf.Abs(SteeringAmount)*(IsAim ? AimBlend("aimForwardShiftOffset","forwardShift") : Mathf.Lerp(2,N("combatRiding.forwardShift"),amount));
            Set("offset",offset);
            if(ForcedShoulder!=0)Set("aimShoulderX",Mathf.Lerp(Mathf.Abs(draft.Get(CameraLabTuning.Scope.Rig,"aimOffset").vector.x),Mathf.Abs(draft.Get(CameraLabTuning.Scope.Rig,"combatAim.offset").vector.x),aimAmount)*ForcedShoulder);
            typeof(CameraController).GetMethod("LateUpdate",Private).Invoke(Rig,null);Camera.Render();
        }
        private static void Update(){
            if(!Active || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;EditorApplication.QueuePlayerLoopUpdate();
            try{
                if(Provider==0){if(stage==0){bound=UnityEngine.Object.FindAnyObjectByType<PlayerController>();if(bound==null || bound.Health==null || Time.timeSinceLevelLoad<.35f)return;Setup();}else if(stage==1 && source.HasInitialized){source.enabled=false;source.StopAllCoroutines();source.GetComponent<Rigidbody>().isKinematic=true;sourceRenderers=source.GetComponentsInChildren<Renderer>(true);sourceRendererStates=new bool[sourceRenderers.Length];for(int i=0;i<sourceRenderers.Length;i++)sourceRendererStates[i]=sourceRenderers[i].enabled;sourceVisible=true;stage=2;Bind();}}
                if(!Ready)return;if(driver==null || driver.gameObject!=Player.gameObject)Bind();if(!applied)Apply(Draft);
                if(Provider==2 && Hold && DodgeLabPreview.Effects.CameraAmount>.95f && !EditorApplication.isPaused)EditorApplication.isPaused=true;
            }catch(Exception e){Debug.LogException(e);Stop();}
        }
        private static void Release(){if(ownCamera!=null)ownCamera.targetTexture=previousTexture;if(texture!=null){texture.Release();UnityEngine.Object.DestroyImmediate(texture);}texture=null;ownCamera=null;}
    }
}
