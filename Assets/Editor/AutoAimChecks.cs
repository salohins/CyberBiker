using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using CyberBiker.Combat;
using CyberBiker.Player;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AutoAimChecks {
    private const string Pending="CyberBiker.AutoAimChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static PlayerController player;
    private static BikeAutoAim aim;
    private static Enemy enemy,other,template,realEnemy;
    private static GameObject wall;
    private static Rigidbody body;
    private static MuzzleFlash muzzle;
    private static Camera camera;
    private static Transform torso;
    private static int stage,shots;
    private static float stageTime;
    private static Quaternion cameraRotation,torsoRotation;
    private static Vector3 cameraAnchor;
    private static readonly StringBuilder report=new StringBuilder();
    private static bool finishing;
    private static Dictionary<SkinnedMeshRenderer,Material[]> originalRiderMaterials;
    private static Renderer bikeRenderer;
    private static Material[] originalBikeMaterials;
    static AutoAimChecks(){if(SessionState.GetBool(Pending,false)) EditorApplication.delayCall+=Attach;}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Attach(){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert) Finish(false,message+"\n"+stack);}
    private static void Require(bool valid,string message){if(!valid)throw new Exception(message);}
    private static Enemy Probe(string name,Vector3 position) {
        var go=new GameObject(name);go.transform.position=position;
        var box=go.AddComponent<BoxCollider>();box.center=Vector3.up*2;box.size=new Vector3(2,3,2);
        var result=go.AddComponent<Enemy>();result.enabled=false;
        typeof(Enemy).GetField("vitality",Private).SetValue(result,new Health(10000));
        return result;
    }
    private static void Next(){stage++;stageTime=Time.time;}
    private static void Tick() {
        if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<.5f) return;
        try {
            if(stage==0) {
                player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                Require(player!=null && player.Health!=null,"The real Level player must initialize");
                foreach(var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)) {if(template==null)template=e;e.gameObject.SetActive(false);}
                foreach(var c in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
                foreach(var s in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))s.enabled=false;
                UnityEngine.Object.FindAnyObjectByType<GameplayManager>().SwitchGameState(GameState.Playing);
                body=player.GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeAll;
                player.transform.SetPositionAndRotation(new Vector3(0,100,0),Quaternion.identity);
                body.position=player.transform.position;body.linearVelocity=Vector3.zero;
                aim=player.GetComponent<BikeAutoAim>();Require(aim!=null && aim.Enabled,"Auto aim must be bound on the actual player prefab");
                var muzzleObject=(GameObject)new SerializedObject(player).FindProperty("muzzleFlash").objectReferenceValue;
                muzzle=muzzleObject.GetComponent<MuzzleFlash>();
                foreach(var bone in player.GetComponentsInChildren<Transform>())if(bone.name=="DEF-spine.001")torso=bone;
                Require(torso!=null && aim.Shoulder!=null,"Existing torso and right arm must be reused");
                camera=Camera.main;
                var fadeShader=Shader.Find("CyberBiker/Rider View Fade");
                Require(fadeShader!=null && !ShaderUtil.ShaderHasError(fadeShader),"The rider visibility shader must compile");
                CheckFadeSurface(fadeShader);
                originalRiderMaterials=new Dictionary<SkinnedMeshRenderer,Material[]>();
                foreach(var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>())originalRiderMaterials.Add(renderer,renderer.sharedMaterials);
                bikeRenderer=player.transform.GetChild(0).GetComponent<Renderer>();originalBikeMaterials=bikeRenderer.sharedMaterials;
                enemy=Probe("Auto aim enemy",new Vector3(7,100,30));
                shots=muzzle.SpawnCount;player.GetComponent<TouchInputManager>().SetAim(true);
                Next();
            } else if(stage==1 && Time.time-stageTime>.65f) {
                Require(muzzle.SpawnCount==shots,"The pistol must not fire before its grab animation is ready");Next();
            } else if(stage==2 && Time.time-stageTime>2.5f) {
                report.AppendLine($"Initial lock={aim.Target?.name}, shots={muzzle.SpawnCount-shots}, canFire={aim.CanFire}, muzzle right={muzzle.transform.right:F3}, forward={muzzle.transform.forward:F3}, wanted={(aim.AimPoint-muzzle.transform.position).normalized:F3}, arm={aim.Shoulder.eulerAngles:F2}, torso={torso.eulerAngles:F2}");
                foreach(var c in player.GetComponentsInChildren<UnityEngine.Animations.Rigging.MultiParentConstraint>())
                    report.AppendLine($"constraint {c.name}: bone={c.data.constrainedObject.name}, source={c.data.sourceObjects[0].transform.name}, source rotation={c.data.sourceObjects[0].transform.eulerAngles:F2}, weight={c.weight}, rig={c.GetComponentInParent<UnityEngine.Animations.Rigging.Rig>().weight}");
                Require(aim.Target==enemy && muzzle.SpawnCount>shots,"Aim must acquire an enemy and fire after the existing animation finishes");
                aim.ApplyArmAim();
                Require(Vector3.Angle(muzzle.transform.right,aim.AimPoint-muzzle.transform.position)<6,"The actual pistol barrel must point at the enemy when firing");
                Require(Mathf.Abs(player.GetComponent<Animator>().GetFloat("JacketSteering"))<.1f,"Aiming right without steering must keep jacket steering neutral");
                report.AppendLine($"Seated neutral hips: {torso.parent.position-player.transform.position:F3}");
                Require(torso.parent.position.y-player.transform.position.y>.5f,"The authored aiming pose must keep the rider seated above the motorcycle");
                CheckFraming("neutral");
                Capture("auto-aim-neutral");
                CaptureMain("aim-framing-neutral");
                cameraRotation=camera.transform.rotation;cameraAnchor=player.cameraTarget.transform.localPosition;
                torsoRotation=torso.rotation;shots=muzzle.SpawnCount;
                enemy.transform.position=new Vector3(-8,104,30);
                player.GetComponent<TouchInputManager>().setRightPressed(true);Next();
            } else if(stage==3 && Time.time-stageTime>1) {
                aim.ApplyArmAim();
                Require(aim.Target==enemy && muzzle.SpawnCount>shots,"The locked enemy must be followed across and above the road");
                CheckFraming("steering");
                Require(Mathf.Abs(Mathf.DeltaAngle(cameraRotation.eulerAngles.z,camera.transform.eulerAngles.z))>5,"Steering must also lean the camera while aiming");
                Require(player.cameraTarget.transform.localPosition==cameraAnchor,"The torso aim source must not move the camera anchor");
                Require(Quaternion.Angle(aim.TorsoTarget.rotation,Quaternion.Euler(aim.TorsoTarget.eulerAngles.x,aim.TorsoTarget.eulerAngles.y,0))<.1f,"The torso aim source must stay upright during steering");
                Transform mesh=player.transform.GetChild(0);
                Require(Mathf.Abs(Mathf.DeltaAngle(0,mesh.localEulerAngles.z))>20,"The motorcycle must still bank while aiming");
                report.AppendLine($"Tracking while banking: bike roll={Mathf.DeltaAngle(0,mesh.localEulerAngles.z):F2}, torso={torso.eulerAngles:F2}, aim source={aim.TorsoTarget.eulerAngles:F2}, camera change={Quaternion.Angle(cameraRotation,camera.transform.rotation):F3}, shots={muzzle.SpawnCount-shots}");
                Require(Mathf.Abs(Mathf.DeltaAngle(0,torso.eulerAngles.z))<5,"The actual torso bone must counter the motorcycle's banking instead of leaning with it");
                var aimingAnimator=player.GetComponent<Animator>();
                Require(aimingAnimator.GetFloat("JacketSteering")>.8f,"The jacket must follow right steering even while the gun tracks an enemy to the left");
                Require(aimingAnimator.GetFloat("dragY")==0 && aimingAnimator.GetFloat("CamAngle")==0,"Jacket targeting must not select or modify the full-body aiming pose");
                foreach(var clip in aimingAnimator.GetCurrentAnimatorClipInfo(0))
                    Require(clip.weight<.001f || clip.clip.name=="Player_IdleGun.001","Steering during aim must retain the seated aiming clip instead of blending incompatible full-body poses");
                Require(torso.parent.position.y-player.transform.position.y>.5f,"Aiming up/left while steering must keep the rider seated on the bike");
                float rightWeight=0;
                foreach(var clip in aimingAnimator.GetCurrentAnimatorClipInfo(1)) {
                    if(clip.clip.name.Contains("GunLeft")) rightWeight+=clip.weight;
                }
                Require(rightWeight>.5f,"Right steering must use the swapped jacket clip during aiming");
                report.AppendLine($"Jacket-only steering layer while aiming: right weight={rightWeight:F3}, enemy=left/up");
                Capture("auto-aim-steering");
                CaptureMain("aim-framing-steering");
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Aim occluder";
                wall.transform.position=new Vector3(-4,104,15);wall.transform.localScale=new Vector3(30,20,2);
                shots=muzzle.SpawnCount;Next();
            } else if(stage==4 && Time.time-stageTime>.5f) {
                Require(aim.Target==null && muzzle.SpawnCount==shots,"Blocked enemies must lose lock and automatic fire must stop");
                wall.SetActive(false);enemy.gameObject.SetActive(false);
                other=Probe("Replacement enemy",new Vector3(6,101,25));Next();
            } else if(stage==5 && Time.time-stageTime>1) {
                Require(aim.Target==other && muzzle.SpawnCount>shots,"Target loss must acquire the next visible enemy");
                var health=(Health)typeof(Enemy).GetField("vitality",Private).GetValue(other);
                Require(health.Current<10000,"Real player projectiles must damage the acquired enemy");
                shots=muzzle.SpawnCount;
                other.transform.position=new Vector3(0,100,-20);Next();
            } else if(stage==6 && Time.time-stageTime>.5f) {
                Require(aim.Target==null && muzzle.SpawnCount==shots,"Targets behind the rider must not make the body twist around or fire backward");
                other.transform.position=new Vector3(6,101,25);Next();
            } else if(stage==7 && Time.time-stageTime>.8f) {
                Require(aim.Target==other,"A returned target must be acquired again");
                player.GetComponent<TouchInputManager>().SetAim(false);shots=muzzle.SpawnCount;Next();
            } else if(stage==8 && Time.time-stageTime>.4f) {
                Require(aim.Target==null && muzzle.SpawnCount==shots,"Aim off must release the target and stop shooting");
                Require(player.GetComponent<Animator>().GetLayerWeight(1)==0,"The directional jacket aiming layer must turn off outside Aim mode");
                other.gameObject.SetActive(false);
                Require(template!=null,"The scene must supply a production enemy for final hit verification");
                realEnemy=UnityEngine.Object.Instantiate(template,new Vector3(5,100,22),Quaternion.identity);
                realEnemy.enabled=false;
                typeof(Enemy).GetField("vitality",Private).SetValue(realEnemy,new Health(10000));
                realEnemy.gameObject.SetActive(true);
                player.GetComponent<TouchInputManager>().setRightPressed(false);
                player.GetComponent<TouchInputManager>().SetAim(true);shots=muzzle.SpawnCount;Next();
            } else if(stage==9 && Time.time-stageTime>3) {
                var health=(Health)typeof(Enemy).GetField("vitality",Private).GetValue(realEnemy);
                report.AppendLine($"Production enemy: lock={aim.Target?.name}, health={health.Current}, shots={muzzle.SpawnCount-shots}");
                Require(aim.Target==realEnemy && health.Current<10000,"Actual enemy rig, colliders, and health must receive auto-aim projectiles");
                health.TakeDamage(new DamageInfo(10000));shots=muzzle.SpawnCount;Next();
            } else if(stage==10 && Time.time-stageTime>.4f) {
                Require(aim.Target==null && muzzle.SpawnCount==shots,"A dead enemy must immediately stop being tracked and fired on");
                player.transform.position=new Vector3(6,100,0);body.position=player.transform.position;
                realEnemy.transform.position=new Vector3(12,100,18);
                typeof(Enemy).GetField("vitality",Private).SetValue(realEnemy,new Health(10000));
                Physics.SyncTransforms();Next();
            } else if(stage==11 && Time.time-stageTime>2) {
                Require(aim.Target==realEnemy,"Road-edge framing must retain the selected enemy");
                Require((float)typeof(CameraController).GetField("aimShoulderSide",Private).GetValue(camera.GetComponent<CameraController>())<0,
                    "A target to the right must take priority over the road's left-shoulder preference");
                CheckFraming("left-shoulder/right-target");CaptureMain("aim-framing-left-shoulder");
                player.transform.position=new Vector3(-6,100,0);body.position=player.transform.position;
                realEnemy.transform.position=new Vector3(-12,100,18);Physics.SyncTransforms();Next();
            } else if(stage==12 && Time.time-stageTime>2) {
                Require((float)typeof(CameraController).GetField("aimShoulderSide",Private).GetValue(camera.GetComponent<CameraController>())>0,
                    "A target to the left must take priority over the road's right-shoulder preference");
                CheckFraming("right-shoulder/left-target");CaptureMain("aim-framing-right-shoulder");
                realEnemy.gameObject.SetActive(false);
                enemy=Probe("Centred obstruction target",new Vector3(0,100,25));
                player.transform.position=new Vector3(0,100,0);body.position=player.transform.position;Physics.SyncTransforms();
                var settings=new SerializedObject(camera.GetComponent<CameraController>());
                settings.FindProperty("dynamicAimShoulder").boolValue=false;
                var offset=settings.FindProperty("aimOffset").vector3Value;offset.x=0;
                settings.FindProperty("aimOffset").vector3Value=offset;
                settings.FindProperty("aimFraming").FindPropertyRelative("enabled").boolValue=false;
                settings.ApplyModifiedPropertiesWithoutUndo();Next();
            } else if(stage==13 && Time.time-stageTime>1.5f) {
                var controller=camera.GetComponent<CameraController>();
                report.AppendLine($"Obstruction check: opacity={controller.RiderViewOpacity:F3}, camera={camera.transform.position-player.transform.position}, target={aim.TargetViewPoint-player.transform.position}, torso={torso.position-player.transform.position}");
                CaptureMain("aim-normal-height-obstruction");
                Require(aim.Target==enemy && controller.RiderViewOpacity<.35f,"The rider must fade when positioned between the camera and aim target");
                foreach(var pair in originalRiderMaterials) {
                    var actual=pair.Key.sharedMaterials;
                    for(int i=0;i<actual.Length;i++) {
                        if(pair.Value[i].name.StartsWith("Gun"))Require(actual[i]==pair.Value[i],"Pistol material must remain solid and unchanged during rider fade");
                        else Require(actual[i].HasProperty("_RiderOpacity") && actual[i].GetFloat("_RiderOpacity")<.35f,"Body, helmet and jacket must use the temporary transparent view material");
                    }
                }
                var bikeMaterials=bikeRenderer.sharedMaterials;
                for(int i=0;i<bikeMaterials.Length;i++)Require(bikeMaterials[i]==originalBikeMaterials[i],"Rider fading must not alter motorcycle materials");
                report.AppendLine($"Obstructed target rider opacity={controller.RiderViewOpacity:F3}; pistol and motorcycle materials preserved");
                CaptureMain("aim-rider-obstruction-fade");
                // Deliberately oversized skin bounds must not cause a fade
                // when a visible target sits beside the rider on screen.
                foreach(var pair in originalRiderMaterials)pair.Key.localBounds=new Bounds(Vector3.zero,Vector3.one*100);
                enemy.transform.position=new Vector3(8,100,25);Physics.SyncTransforms();Next();
            } else if(stage==14 && Time.time-stageTime>.35f) {
                Require(aim.Target==enemy,"The centre fade must retain the selected target");
                Require(camera.GetComponent<CameraController>().RiderViewOpacity<.35f,
                    "A rider covering the camera centre must remain transparent with an off-centre target selected and no shoulder-switch velocity");
                report.AppendLine("Camera-centre fade remained active with an off-centre enemy selected and stationary shoulders");
                var controller=camera.GetComponent<CameraController>();controller.enabled=false;
                foreach(var pair in originalRiderMaterials) {
                    var actual=pair.Key.sharedMaterials;
                    for(int i=0;i<actual.Length;i++)Require(actual[i]==pair.Value[i],"Disabling/reloading the controller must restore original materials before rebuilding the fade helper");
                }
                controller.enabled=true;
                SetFixedTestShoulder(-1.38f);Next();
            } else if(stage==15 && Time.time-stageTime>.35f) {
                report.AppendLine($"Clear target view rider opacity={camera.GetComponent<CameraController>().RiderViewOpacity:F4}");
                Require(aim.Target==enemy,"Clearing the rider obstruction must retain the selected enemy");
                Require(camera.GetComponent<CameraController>().RiderViewOpacity>.999f,"A clear target view must restore full rider opacity while still aiming");
                foreach(var pair in originalRiderMaterials) {
                    var actual=pair.Key.sharedMaterials;
                    for(int i=0;i<actual.Length;i++)Require(actual[i]==pair.Value[i],"A clear target view must restore every original rider material");
                }
                report.AppendLine("Clear target view restored the original rider materials while Aim remained active");
                enemy.transform.position=new Vector3(0,100,25);Physics.SyncTransforms();SetFixedTestShoulder(0);Next();
            } else if(stage==16 && Time.time-stageTime>1.5f) {
                Require(camera.GetComponent<CameraController>().RiderViewOpacity<.35f,"A new target obstruction must fade the rider again");
                SetSideObstruction(2);Next();
            } else if(stage==17 && Time.time-stageTime>1.1f) {
                CheckSideObstruction("right camera / left target");SetSideObstruction(-2);Next();
            } else if(stage==18 && Time.time-stageTime>1.1f) {
                CheckSideObstruction("left camera / right target");
                enemy.transform.position=new Vector3(-8,100,25);Physics.SyncTransforms();Next();
            } else if(stage==19 && Time.time-stageTime>.35f) {
                Require(camera.GetComponent<CameraController>().RiderViewOpacity>.999f,"Clearing a side target obstruction must restore the rider without leaving Aim");
                player.GetComponent<TouchInputManager>().SetAim(false);Next();
            } else if(stage==20 && Time.time-stageTime>1.5f) {
                Require(camera.GetComponent<CameraController>().RiderViewOpacity>.999f,"Leaving Aim must smoothly restore full rider opacity");
                foreach(var pair in originalRiderMaterials) {
                    var actual=pair.Key.sharedMaterials;
                    for(int i=0;i<actual.Length;i++)Require(actual[i]==pair.Value[i],"Rider fade must restore every original material after the obstruction clears");
                }
                Finish(true,"PASS: authored draw/aim and seated pose; real enemy projectile damage; target-priority shoulders and capped framing; animated-mesh raycast fading for left/right targets with centre fade disabled; solid pistol/motorcycle, clear-view material restoration and single-surface rendering; Aim-off, occlusion and target-loss guards.\n"+report);
            }
            if(Time.timeSinceLevelLoad>40) Finish(false,"Timed out at stage "+stage+"\n"+report);
        }catch(Exception error){Finish(false,error+"\n"+report);}
    }
    private static void SetSideObstruction(float cameraSide) {
        SetFixedTestShoulder(-cameraSide);
        var settings=new SerializedObject(camera.GetComponent<CameraController>());
        settings.FindProperty("riderVisibility").FindPropertyRelative("fadeNearCameraCenter").boolValue=false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var offset=settings.FindProperty("aimOffset").vector3Value;offset.x=0;
        var origin=player.cameraTarget.transform.position-camera.transform.TransformDirection(offset)+camera.transform.right*cameraSide;
        Transform chest=null;foreach(var bone in player.GetComponentsInChildren<Transform>())if(bone.name=="DEF-spine.003")chest=bone;
        var through=Vector3.Lerp(torso.position,chest.position,.6f);
        var destination=origin+(through-origin)*((player.transform.position.z+25-origin.z)/(through.z-origin.z));
        var collider=enemy.GetComponent<Collider>();
        var pointOffset=collider.bounds.center-enemy.transform.position+Vector3.up*collider.bounds.size.y*.15f;
        enemy.transform.position=destination-pointOffset;Physics.SyncTransforms();
    }
    private static void CheckSideObstruction(string label) {
        var viewport=camera.WorldToViewportPoint(aim.TargetViewPoint);
        var opacity=camera.GetComponent<CameraController>().RiderViewOpacity;
        report.AppendLine($"Side obstruction {label}: target={viewport:F3}, opacity={opacity:F3}, player scale={player.transform.lossyScale:F2}");
        Require(aim.Target==enemy && viewport.x>0 && viewport.x<1 && Mathf.Abs(viewport.x-.5f)>.1f,"The obstruction regression must use a visible off-centre target");
        Require(opacity<.35f,"Camera-to-enemy mesh raycasts must fade the rider for off-centre targets with the centre check disabled");
        CaptureMain(label.StartsWith("right") ? "aim-side-target-left-fade" : "aim-side-target-right-fade");
    }
    private static void SetFixedTestShoulder(float x) {
        var controller=camera.GetComponent<CameraController>();var settings=new SerializedObject(controller);
        var offset=settings.FindProperty("aimOffset").vector3Value;offset.x=x;
        settings.FindProperty("aimOffset").vector3Value=offset;settings.ApplyModifiedPropertiesWithoutUndo();
        typeof(CameraController).GetField("aimShoulderX",Private).SetValue(controller,x);
        typeof(CameraController).GetField("aimShoulderVelocity",Private).SetValue(controller,0f);
    }
    private static void Finish(bool passed,string message) {
        if(finishing)return;finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
        SessionState.SetBool(Pending,false);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/auto-aim-check.txt",message);EditorApplication.Exit(passed?0:1);
    }
    private static void CheckFraming(string label) {
        var controller=camera.GetComponent<CameraController>();
        var point=Vector3.Lerp(aim.Pistol.position,aim.Muzzle.position,.65f);
        var viewport=camera.WorldToViewportPoint(point);
        report.AppendLine($"{label} framing: visible={controller.AimPistolVisible}, weapon viewport={viewport:F3}, look={controller.AimTargetLookAngles:F2}, exposure yaw={aim.PistolExposureYaw:F2}, visibility correction={controller.AimPistolCorrection:F2}, camera={camera.transform.position-player.transform.position:F2}");
        Require(viewport.z>0 && viewport.x>.06f && viewport.x<.94f && viewport.y>.06f && viewport.y<.94f,"The pistol must stay inside the camera's safe framing area");
        Require(controller.AimPistolVisible,"Camera framing must clear the rider's body/head from the pistol view");
        Require(Mathf.Abs(controller.AimTargetLookAngles.x)<=10.1f && Mathf.Abs(controller.AimTargetLookAngles.y)<=6.1f,"Camera target follow must remain a gentle capped turn");
    }
    private static void CheckFadeSurface(Shader shader) {
        var root=new GameObject("Single surface fade rendering check");
        var view=root.AddComponent<Camera>();view.enabled=false;view.cullingMask=1<<31;
        view.transform.position=new Vector3(0,1000,-5);view.orthographic=true;view.orthographicSize=1;
        view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.green;view.allowHDR=false;
        var target=new RenderTexture(64,64,24);view.targetTexture=target;
        var material=new Material(shader);material.SetFloat("_UseUnlit",1);material.SetFloat("_RiderOpacity",.5f);material.SetColor("_Color",Color.red);
        var frontDepth=new RenderTexture(64,64,24,RenderTextureFormat.RFloat,RenderTextureReadWrite.Linear);frontDepth.filterMode=FilterMode.Point;frontDepth.Create();
        material.SetTexture("_RiderFrontDepth",frontDepth);material.SetFloat("_UseFrontSurfaceDepth",1);
        var backdropMaterial=new Material(Shader.Find("Unlit/Transparent")) {renderQueue=2999};
        var backdropTexture=new Texture2D(1,1);backdropTexture.SetPixel(0,0,Color.blue);backdropTexture.Apply();backdropMaterial.mainTexture=backdropTexture;
        var renderers=new Renderer[3];
        var buffer=new UnityEngine.Rendering.CommandBuffer();
        view.AddCommandBuffer(UnityEngine.Rendering.CameraEvent.BeforeForwardAlpha,buffer);
        var pixels=new Texture2D(64,64,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try {
            var backdrop=GameObject.CreatePrimitive(PrimitiveType.Quad);backdrop.transform.SetParent(root.transform,false);
            backdrop.layer=31;backdrop.transform.position=new Vector3(0,1000,1);backdrop.transform.localScale=Vector3.one*2;
            backdrop.GetComponent<Renderer>().sharedMaterial=backdropMaterial;
            for(int i=0;i<3;i++) {
                var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(root.transform,false);
                quad.layer=31;quad.transform.position=new Vector3(0,1000,i*.1f);quad.transform.localScale=Vector3.one*2;
                renderers[i]=quad.GetComponent<Renderer>();renderers[i].sharedMaterial=material;renderers[i].enabled=i==0;
            }
            int pass=material.FindPass("RIDER_DEPTH");Require(pass>=0,"Rider fade needs a separate front-surface depth pass");
            buffer.SetRenderTarget(frontDepth);buffer.ClearRenderTarget(true,true,Color.white*(view.farClipPlane+1));
            buffer.DrawRenderer(renderers[0],material,0,pass);buffer.SetRenderTarget(UnityEngine.Rendering.BuiltinRenderTextureType.CameraTarget);
            view.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();var single=pixels.GetPixel(32,32);
            Require(single.r>.15f && single.b>.15f && single.g<.05f,"The rider fade must reveal transparent scenery behind it without blocking its depth");
            buffer.Clear();buffer.SetRenderTarget(frontDepth);buffer.ClearRenderTarget(true,true,Color.white*(view.farClipPlane+1));
            foreach(var renderer in renderers){renderer.enabled=true;buffer.DrawRenderer(renderer,material,0,pass);}
            buffer.SetRenderTarget(UnityEngine.Rendering.BuiltinRenderTextureType.CameraTarget);
            view.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();var layered=pixels.GetPixel(32,32);
            Require(Vector3.Distance(new Vector3(single.r,single.g,single.b),new Vector3(layered.r,layered.g,layered.b))<.03f,
                "Hidden rider surfaces must not show through or change the front surface's opacity");
            report.AppendLine($"Single-surface fade render: one surface={single}, three overlapping surfaces={layered}");
            // A partial front layer checks vertical alignment of the separate
            // depth texture, rather than only sampling a uniform centre pixel.
            renderers[0].transform.position+=Vector3.up;renderers[1].enabled=false;
            buffer.Clear();buffer.SetRenderTarget(frontDepth);buffer.ClearRenderTarget(true,true,Color.white*(view.farClipPlane+1));
            buffer.DrawRenderer(renderers[0],material,0,pass);buffer.DrawRenderer(renderers[2],material,0,pass);
            buffer.SetRenderTarget(UnityEngine.Rendering.BuiltinRenderTextureType.CameraTarget);
            view.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();
            foreach(int y in new[]{16,48}) {
                var sample=pixels.GetPixel(32,y);
                Require(sample.r>.15f && sample.b>.15f && sample.g<.05f,"Front-surface depth must align with the upper and lower rider silhouette");
            }
            report.AppendLine("Transparent scenery remained visible through the rider; separate depth aligned at upper and lower screen samples");
        } finally {
            RenderTexture.active=old;view.RemoveCommandBuffer(UnityEngine.Rendering.CameraEvent.BeforeForwardAlpha,buffer);buffer.Release();
            view.targetTexture=null;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(frontDepth);UnityEngine.Object.DestroyImmediate(backdropTexture);UnityEngine.Object.DestroyImmediate(backdropMaterial);
            UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(root);
        }
    }
    private static void CaptureMain(string name) {
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var texture=new RenderTexture(Mathf.RoundToInt(720*camera.aspect),720,24);
        camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
        var capture=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
        capture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);capture.Apply();
        Directory.CreateDirectory("Logs/AutoAim");File.WriteAllBytes("Logs/AutoAim/"+name+".png",capture.EncodeToPNG());
        camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
        UnityEngine.Object.DestroyImmediate(capture);UnityEngine.Object.DestroyImmediate(texture);
    }
    private static void Capture(string name) {
        Vector3 boreBefore=muzzle.transform.right;Quaternion torsoBefore=torso.rotation;
        var go=new GameObject("Auto aim pose review camera");var view=go.AddComponent<Camera>();
        view.transform.position=player.transform.position+new Vector3(5,3,-5);
        view.transform.LookAt(player.transform.position+Vector3.up*2);
        view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.05f,.05f,.06f);view.fieldOfView=45;
        var texture=new RenderTexture(800,800,24);view.targetTexture=texture;view.Render();
        report.AppendLine($"{name} render pose: bore changed={Vector3.Angle(boreBefore,muzzle.transform.right):F2}, torso changed={Quaternion.Angle(torsoBefore,torso.rotation):F2}");
        Require(Vector3.Angle(boreBefore,muzzle.transform.right)<.2f && Quaternion.Angle(torsoBefore,torso.rotation)<.2f,"Rendering must preserve the tracked gun and upright torso pose");
        var old=RenderTexture.active;RenderTexture.active=texture;
        var capture=new Texture2D(800,800,TextureFormat.RGB24,false);capture.ReadPixels(new Rect(0,0,800,800),0,0);capture.Apply();
        Directory.CreateDirectory("Logs/AutoAim");File.WriteAllBytes("Logs/AutoAim/"+name+".png",capture.EncodeToPNG());
        RenderTexture.active=old;view.targetTexture=null;UnityEngine.Object.DestroyImmediate(capture);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);
    }
}
