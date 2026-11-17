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
    public static class GapDashLabPreview {
        public enum Scenario { OpenGap, NarrowGap, BlockedExit, SingleCar, TightGap }
        private const string Key="CyberBiker.GapDashLab.";
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private sealed class PreviewInput:IPlayerInput {public Vector2 Steering=>new Vector2(GapDashLabPreview.Steering,0);public Vector2 AimDelta=>Vector2.zero;public bool IsAiming=>Aiming;}
        private sealed class SilentWeapon:Weapon {public SilentWeapon():base(1){}protected override bool Fire(ShotRequest shot)=>false;}
        private static Car[] cars;
        private static Camera camera;
        private static RenderTexture texture,previousTexture;
        private static Vector3 origin;
        private static int stage,lastFrame=-1;
        private static float clock;
        private static double nextReplay;
        public static PlayerController Player {get;private set;}
        public static GameplayManager Gameplay {get;private set;}
        public static GapDashAssist Assist {get;private set;}
        public static bool Active=>SessionState.GetBool(Key+"Active",false);
        public static bool Ready=>Active && stage==1 && Assist!=null;
        public static RenderTexture View=>texture;
        public static float Clock=>clock;
        public static string Status=>Ready ? Assist.Status+" Â· dashes "+Assist.DashCount+" Â· cleared "+Assist.CompletedCount+" Â· gap "+Assist.GapWidth.ToString("0.00")+" m" : Active ? "Starting the live traffic previewâ€¦" : "Start preview to ride through a real gap between cars.";
        public static Scenario Current {get=>(Scenario)SessionState.GetInt(Key+"Scenario",0);set{SessionState.SetInt(Key+"Scenario",(int)value);}}
        public static float Speed {get=>SessionState.GetFloat(Key+"Speed",65);set=>SessionState.SetFloat(Key+"Speed",Mathf.Clamp(value,25,130));}
        public static float Offset {get=>SessionState.GetFloat(Key+"Offset",.75f);set=>SessionState.SetFloat(Key+"Offset",Mathf.Clamp(value,-8,8));}
        public static float CarDistance {get=>SessionState.GetFloat(Key+"CarDistance",65);set=>SessionState.SetFloat(Key+"CarDistance",Mathf.Clamp(value,10,85));}
        public static float Spacing {get=>SessionState.GetFloat(Key+"Spacing",6.06f);set=>SessionState.SetFloat(Key+"Spacing",Mathf.Clamp(value,4.5f,8));}
        public static float Steering {get=>SessionState.GetFloat(Key+"Steer",0);set=>SessionState.SetFloat(Key+"Steer",Mathf.Clamp(value,-1,1));}
        public static bool LaunchMode {get=>SessionState.GetBool(Key+"LaunchMode",true);set{if(value==LaunchMode)return;SessionState.SetBool(Key+"LaunchMode",value);if(Ready)Replay();}}
        public static bool Loop {get=>SessionState.GetBool(Key+"Loop",true);set=>SessionState.SetBool(Key+"Loop",value);}
        public static bool Sound {get=>SessionState.GetBool(Key+"Sound",false);set{SessionState.SetBool(Key+"Sound",value);if(Active)AudioListener.volume=value ? 1 : 0;}}
        public static bool Aiming {get=>SessionState.GetBool(Key+"Aiming",false);set{SessionState.SetBool(Key+"Aiming",value);if(Ready)Player.GetComponent<TouchInputManager>().SetAim(value);}}
        public static bool OrbitView {get=>SessionState.GetBool(Key+"Orbit",false);set=>SessionState.SetBool(Key+"Orbit",value);}
        public static float Yaw {get=>SessionState.GetFloat(Key+"Yaw",-30);set=>SessionState.SetFloat(Key+"Yaw",value);}
        public static float Pitch {get=>SessionState.GetFloat(Key+"Pitch",14);set=>SessionState.SetFloat(Key+"Pitch",Mathf.Clamp(value,-5,65));}
        public static float Distance {get=>SessionState.GetFloat(Key+"Distance",12);set=>SessionState.SetFloat(Key+"Distance",Mathf.Clamp(value,3,25));}
        private static Camera orbit;
        static GapDashLabPreview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=ModeChanged;AssemblyReloadEvents.beforeAssemblyReload+=Release;}
        public static void Start(GapDashSettings settings){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the current Play session before starting Gap Dash Lab.");
            SessionState.SetString(Key+"Previous",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));SessionState.SetFloat(Key+"Volume",AudioListener.volume);SessionState.SetBool(Key+"Active",true);stage=0;
            Apply(settings);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Level/Scenes/Level.unity");EditorApplication.isPlaying=true;
        }
        public static void Apply(GapDashSettings settings){settings.Normalize();SessionState.SetString(Key+"Draft",JsonUtility.ToJson(settings));if(Gameplay!=null)Gameplay.SetGapDashPreview(settings);if(Ready && EditorApplication.isPaused){Assist.RefreshPreviewParticles();Refresh();}}
        public static void Stop(){if(!Active)return;EditorApplication.isPaused=false;EditorApplication.isPlaying=false;}
        public static void Step(){if(Ready){EditorApplication.isPaused=true;EditorApplication.Step();}}
        private static void Setup(){
            // Replay rewinds the rider to the same origin. Keep the generated
            // test road resident instead of streaming its starting tiles away.
            foreach(var world in UnityEngine.Object.FindObjectsByType<WorldGenerator>()){world.StopAllCoroutines();world.enabled=false;}
            foreach(var d in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>())d.enabled=false;
            foreach(var s in UnityEngine.Object.FindObjectsByType<TrafficSpawner>()){s.StopAllCoroutines();s.enabled=false;}
            foreach(var e in UnityEngine.Object.FindObjectsByType<Enemy>())e.gameObject.SetActive(false);
            foreach(var c in UnityEngine.Object.FindObjectsByType<Car>())c.gameObject.SetActive(false);
            for(int i=Bullet.ActiveShots.Count-1;i>=0;i--)PrefabPool.Release(Bullet.ActiveShots[i].gameObject);
            Gameplay.SwitchGameState(GameState.Playing);Gameplay.PistolInteractionMode=PistolInteraction.AimAndAutoFire;Gameplay.SetGapDashPreview(JsonUtility.FromJson<GapDashSettings>(SessionState.GetString(Key+"Draft","{}")));
            Player.GetComponent<TouchInputManager>().SetAim(Aiming);Player.GetComponent<TouchInputManager>().enabled=false;Player.GetComponent<PlayerProjectileEvade>().enabled=false;
            typeof(PlayerController).GetField("input",Private).SetValue(Player,new PreviewInput());typeof(PlayerController).GetField("weapon",Private).SetValue(Player,new SilentWeapon());
            Assist=Player.GetComponent<GapDashAssist>();
            origin=Player.transform.position;var body=Player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezeRotation;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Cars/Car_04.prefab");cars=new Car[3];
            for(int i=0;i<cars.Length;i++){cars[i]=UnityEngine.Object.Instantiate(prefab,origin+Vector3.forward*(50+i*20),Quaternion.identity).GetComponent<Car>();cars[i].OnPoolRent(prefab);cars[i].enabled=false;}
            camera=Camera.main;previousTexture=camera.targetTexture;texture=new RenderTexture(1280,720,24){name="Gap Dash live view"};texture.Create();camera.targetTexture=texture;
            orbit=new GameObject("Gap Dash orbit camera").AddComponent<Camera>();orbit.CopyFrom(camera);orbit.enabled=false;orbit.fieldOfView=55;orbit.targetTexture=texture;
            AudioListener.volume=Sound ? 1 : 0;stage=1;Replay();
        }
        public static void Replay(){
            if(!Ready)return;EditorApplication.isPaused=false;Assist.ResetPreview();clock=0;nextReplay=EditorApplication.timeSinceStartup+3;
            var body=Player.GetComponent<Rigidbody>();body.position=origin+Vector3.right*Offset;body.rotation=Quaternion.identity;Player.transform.SetPositionAndRotation(body.position,body.rotation);body.linearVelocity=Vector3.forward*Speed;body.angularVelocity=Vector3.zero;
            var presentation=typeof(PlayerController).GetField("presentation",Private).GetValue(Player);
            foreach(var field in new[]{"yawRotation","animationFrame","visualSteering","steering"})typeof(BikePresentation).GetField(field,Private).SetValue(presentation,0f);
            var moto=typeof(PlayerController).GetField("motorcycleMesh",Private).GetValue(Player) as Transform;if(moto!=null)moto.localRotation=Quaternion.identity;
            Player.xThrow=0;var animator=Player.GetComponent<Animator>();animator.SetFloat("dragX",0);animator.SetFloat("JacketSteering",0);
            // The preview uses real Launch eligibility, while keeping the selected
            // ride speed so this Lab can isolate gap alignment from boost tuning.
            var sword=Player.GetComponent<BikeSwordCombat>();
            typeof(BikeSwordCombat).GetMethod("ResetCombat",Private).Invoke(sword,new object[]{true,false});
            typeof(BikeSwordCombat).GetField("nextLaunch",Private).SetValue(sword,0f);
            sword.ResetLaunchEnergyForPreview();
            sword.TestingSpeedMultiplier=1;
            if(LaunchMode && !Aiming)sword.Launch();
            Player.ConfigureTestingMovement(Speed,Player.ForwardAcceleration,Player.SteeringAcceleration,Player.SteeringSpeedLimit);
            float spacing=Current==Scenario.NarrowGap ? 4.9f : Current==Scenario.TightGap ? 5.4f : Spacing;
            for(int i=0;i<cars.Length;i++){
                var p=origin+new Vector3(i==0 ? -spacing*.5f : i==1 ? spacing*.5f : 0,0,i<2 ? CarDistance : CarDistance+4);
                if(i==2 && Current!=Scenario.BlockedExit || i==1 && Current==Scenario.SingleCar)p=origin+Vector3.forward*1000+Vector3.right*(30+i*10);
                cars[i].transform.position=p;CyberBiker.Audio.GameAudio.Active?.ForgetCar(cars[i]);
            }
            Physics.SyncTransforms();
            // Move the follow camera with the rewind before drawing the first
            // replay frame, rather than showing the end of the previous pass.
            typeof(CameraController).GetMethod("LateUpdate",Private).Invoke(camera.GetComponent<CameraController>(),null);
            Refresh();
        }
        public static void SeekEffect(float seconds){if(!Ready)return;EditorApplication.isPaused=true;Assist.PreviewEffect(seconds);Refresh();}
        public static void Refresh(){
            if(!Ready)return;
            if(EditorApplication.isPaused){
                // Rebuild only the additive effects over this frozen camera.
                // Calling the full follow update advances its smoothing while paused.
                ShootingFeedback.Active?.RemoveCamera(camera);Gameplay.DodgeEffects?.RemoveCamera(camera);Assist.RemoveCamera(camera);
                Assist.ApplyCamera(camera);Gameplay.DodgeEffects?.ApplyCamera(camera);ShootingFeedback.Active?.ApplyCamera(camera);
            }
            if(OrbitView){var q=Quaternion.Euler(Pitch,Yaw,0);orbit.transform.SetPositionAndRotation(Player.transform.position+Vector3.up*2.4f-q*Vector3.forward*Distance,q);orbit.Render();}else camera.Render();
        }
        private static void Tick(){
            if(!Active || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;EditorApplication.QueuePlayerLoopUpdate();
            try{
                if(stage==0){Player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();Gameplay=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();if(Player==null || Player.Health==null || Gameplay==null || Time.timeSinceLevelLoad<.35f)return;Setup();}
                if(!Ready)return;
                if(!EditorApplication.isPaused && lastFrame!=Time.frameCount){lastFrame=Time.frameCount;clock+=Time.deltaTime;if(Loop && clock>3 && EditorApplication.timeSinceStartup>nextReplay)Replay();}
                if(OrbitView || EditorApplication.isPaused)Refresh();
            }catch(Exception error){Debug.LogException(error);Stop();}
        }
        private static void ModeChanged(PlayModeStateChange state){
            if(state==PlayModeStateChange.EnteredPlayMode && Active){stage=0;lastFrame=-1;}
            if(state!=PlayModeStateChange.EnteredEditMode || !Active)return;
            Release();var previous=SessionState.GetString(Key+"Previous","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);AudioListener.volume=SessionState.GetFloat(Key+"Volume",1);
            Player=null;Gameplay=null;Assist=null;cars=null;stage=0;SessionState.SetBool(Key+"Active",false);
        }
        private static void Release(){if(camera!=null)camera.targetTexture=previousTexture;if(orbit!=null)UnityEngine.Object.DestroyImmediate(orbit.gameObject);if(texture!=null){texture.Release();UnityEngine.Object.DestroyImmediate(texture);}camera=null;orbit=null;texture=null;}
    }
}
