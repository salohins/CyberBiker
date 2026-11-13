using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public static class SwordAnimationImport {
    private static Avatar previewAvatar;
    private const string Folder="Assets/Art/Animations/Sword";
    private const string PlayerPath="Assets/Art/Models/Player_Last.prefab";
    [Serializable] private class Manifest {public float fps;public int boneCount;public ClipSpec[] clips;}
    [Serializable] private class ClipSpec {public string name,file;public int firstFrame,lastFrame;public float length;public bool loop;}
    [Serializable] private class ReferenceFile {public ReferenceClip[] clips;}
    [Serializable] private class ReferenceClip {public string name;public ReferenceFrame[] samples;}
    [Serializable] private class ReferenceFrame {public int frame;public ReferenceBone[] bones;}
    [Serializable] private class ReferenceBone {public string name;public Vector3 position;public Quaternion rotation;}
    [Serializable] private class ClipReport {
        public string name;public bool human,loop;public float length,frameRate,minHipHeight,maxHipHeight,swordTravel,maximumGripError,maximumBonePositionError;
        public int transformBindings;public string[] remappedPaths;
    }
    [Serializable] private class Report {public bool passed;public string error;public List<ClipReport> clips=new List<ClipReport>();}

    [MenuItem("CyberBiker/Animations/Import and verify sword library")]
    public static void ImportAndVerify() {Run(false);}
    public static void RunBatch() {Run(true);}
    private static void Require(bool valid,string message) {if(!valid)throw new InvalidOperationException(message);}
    private static void Run(bool batch) {
        var report=new Report();Directory.CreateDirectory("Logs/SwordAnimations");
        try {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder+"/SwordAnimations.manifest.json"));
            Require(manifest.clips.Length==7 && manifest.fps==60,"Expected seven source sword actions at 60 FPS");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            string avatarPath=Folder+"/SwordPreviewAvatar.asset";
            var temporary=UnityEngine.Object.Instantiate(prefab);temporary.name=prefab.name;
            var generic=AvatarBuilder.BuildGenericAvatar(temporary,"");generic.name="SwordPreviewAvatar";
            UnityEngine.Object.DestroyImmediate(temporary);
            previewAvatar=AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
            if(previewAvatar==null){previewAvatar=generic;AssetDatabase.CreateAsset(previewAvatar,avatarPath);}
            else{EditorUtility.CopySerialized(generic,previewAvatar);UnityEngine.Object.DestroyImmediate(generic);EditorUtility.SetDirty(previewAvatar);}
            var avatar=prefab.GetComponent<Animator>().avatar;
            Require(avatar!=null && avatar.isValid && avatar.isHuman,"The existing player's humanoid Avatar must remain valid");
            var paths=prefab.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).Where(g=>g.Count()==1)
                .ToDictionary(g=>g.Key,g=>AnimationUtility.CalculateTransformPath(g.First(),prefab.transform));
            var clips=new Dictionary<string,AnimationClip>();
            foreach(var spec in manifest.clips) {
                string fbx=Folder+"/"+spec.file;
                var importer=AssetImporter.GetAtPath(fbx) as ModelImporter;
                Require(importer!=null,"Missing FBX: "+fbx);
                var restModel=AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                // Keep every baked bone, including translations and prop/jacket bones.
                // A separate Generic preview Avatar avoids Humanoid pose reconstruction.
                importer.importAnimation=true;importer.animationType=ModelImporterAnimationType.Generic;
                importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.sourceAvatar=null;
                importer.animationCompression=ModelImporterAnimationCompression.Off;
                importer.optimizeGameObjects=false;importer.resampleCurves=true;
                importer.importCameras=false;importer.importLights=false;importer.importConstraints=false;
                importer.materialImportMode=ModelImporterMaterialImportMode.None;
                var defaults=importer.defaultClipAnimations;
                Require(defaults.Length==1,"Expected exactly one baked take in "+fbx);
                var settings=defaults[0];settings.name=spec.name;
                settings.lastFrame=settings.firstFrame+(spec.lastFrame-spec.firstFrame);
                settings.loopTime=spec.loop;settings.loopPose=false;
                var mask=new AvatarMask();mask.AddTransformPath(restModel.transform,true);
                settings.maskType=ClipAnimationMaskType.CreateFromThisModel;settings.ConfigureClipFromMask(mask);UnityEngine.Object.DestroyImmediate(mask);
                settings.lockRootRotation=true;settings.keepOriginalOrientation=true;
                settings.lockRootHeightY=true;settings.keepOriginalPositionY=true;
                settings.lockRootPositionXZ=true;settings.keepOriginalPositionXZ=true;
                importer.clipAnimations=new[]{settings};importer.SaveAndReimport();
                var imported=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
                Require(imported.Length==1,"FBX did not import exactly one clip: "+spec.name);
                var clip=UnityEngine.Object.Instantiate(imported[0]);clip.name=spec.name;clip.hideFlags=HideFlags.None;
                var fbxModel=AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                var sourceRig=fbxModel.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="unityRig");
                var targetRig=prefab.transform.Find(paths["unityRig"]);
                float positionScale=sourceRig.localScale.x/targetRig.localScale.x;
                var remapped=new List<string>();
                foreach(var binding in AnimationUtility.GetCurveBindings(imported[0])) {
                    if(binding.type!=typeof(Transform))continue;
                    var curve=AnimationUtility.GetEditorCurve(imported[0],binding);
                    // The player motor owns its root, not the animation asset.
                    if(string.IsNullOrEmpty(binding.path) || binding.path=="unityRig"){AnimationUtility.SetEditorCurve(clip,binding,null);continue;}
                    string leaf=PlayerBoneName(binding.path.Split('/').Last());
                    Require(paths.TryGetValue(leaf,out string targetPath),"Unbound animation bone: "+binding.path);
                    if(binding.propertyName.StartsWith("m_LocalPosition")) {
                        var keys=curve.keys;
                        for(int i=0;i<keys.Length;i++){keys[i].value*=positionScale;keys[i].inTangent*=positionScale;keys[i].outTangent*=positionScale;}
                        curve.keys=keys;
                    }
                    var mapped=binding;mapped.path=targetPath;
                    AnimationUtility.SetEditorCurve(clip,binding,null);AnimationUtility.SetEditorCurve(clip,mapped,curve);
                    if(binding.path!=targetPath)remapped.Add(binding.path+" -> "+targetPath);
                }
                string asset=Folder+"/"+spec.name+".anim";
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);
                if(existing==null)AssetDatabase.CreateAsset(clip,asset);
                else {EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);clip=existing;EditorUtility.SetDirty(clip);}
                Require(!clip.humanMotion && !clip.legacy && !clip.empty,"Expected a populated transform-baked clip: "+spec.name);
                Require(Mathf.Abs(clip.length-spec.length)<1/manifest.fps+.001f,"Incorrect imported duration: "+spec.name+" / "+clip.length);
                Require(Mathf.Abs(clip.frameRate-manifest.fps)<.01f,"Incorrect sample rate: "+spec.name);
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                    if(binding.type==typeof(Transform))Require(prefab.transform.Find(binding.path)!=null,"Clip binding does not resolve on the player: "+binding.path);
                clips.Add(spec.name,clip);
                report.clips.Add(new ClipReport {name=spec.name,human=clip.humanMotion,length=clip.length,frameRate=clip.frameRate,loop=spec.loop,
                    transformBindings=AnimationUtility.GetCurveBindings(clip).Count(b=>b.type==typeof(Transform)),remappedPaths=remapped.Distinct().ToArray()});
            }
            AssetDatabase.SaveAssets();
            var references=JsonUtility.FromJson<ReferenceFile>(File.ReadAllText("SourceAssets/Animations/SwordPoseReference.json"));
            VerifyPlayback(prefab,manifest,clips,references,report);
            CreatePreview(prefab,manifest,clips);
            AssetDatabase.SaveAssets();SwordRuntimeClipBuild.Prepare();report.passed=true;
        } catch(Exception error) {report.error=error.ToString();Debug.LogError(report.error);}
        File.WriteAllText("Logs/SwordAnimations/import-report.json",JsonUtility.ToJson(report,true));
        Debug.Log(report.passed ? "SWORD IMPORT PASS: seven clips, existing player rig, pose/timing/path checks and player-prefab previews." : "SWORD IMPORT FAILED: "+report.error);
        if(batch)EditorApplication.Exit(report.passed?0:1);
    }

    private static GameObject VisualInstance(GameObject prefab,Scene scene) {
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
        foreach(var script in instance.GetComponentsInChildren<MonoBehaviour>(true))if(script!=null)script.enabled=false;
        foreach(var collider in instance.GetComponentsInChildren<Collider>(true))collider.enabled=false;
        foreach(var body in instance.GetComponentsInChildren<Rigidbody>(true))body.isKinematic=true;
        foreach(var particles in instance.GetComponentsInChildren<ParticleSystem>(true))particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach(var source in instance.GetComponentsInChildren<AudioSource>(true)){source.playOnAwake=false;source.enabled=false;}
        foreach(var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            renderer.updateWhenOffscreen=true;renderer.forceMatrixRecalculationPerRender=true;
        }
        instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);instance.transform.localScale=Vector3.one;
        foreach(var t in instance.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        var animator=instance.GetComponent<Animator>();animator.avatar=previewAvatar;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        return instance;
    }
    private static void VerifyPlayback(GameObject prefab,Manifest manifest,Dictionary<string,AnimationClip> clips,ReferenceFile references,Report report) {
        var scene=EditorSceneManager.NewPreviewScene();GameObject instance=null;var materials=new List<Material>();
        try {
            instance=VisualInstance(prefab,scene);var animator=instance.GetComponent<Animator>();
            var bones=instance.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.First());
            var sword=bones["Sword_Main"];var hand=bones["DEF-hand.R"];var hips=bones["DEF-spine"];
            var cameraObject=new GameObject("Sword animation verification camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.cullingMask=1<<31;camera.enabled=false;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.08f);camera.fieldOfView=40;
            camera.transform.position=new Vector3(4,3,-5);camera.transform.LookAt(new Vector3(0,1,0));
            var lightObject=new GameObject("Sword verification key");SceneManager.MoveGameObjectToScene(lightObject,scene);
            var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.cullingMask=1<<31;light.transform.rotation=Quaternion.Euler(35,-35,0);
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)) {
                var list=renderer.sharedMaterials;
                for(int i=0;i<list.Length;i++)if(list[i]!=null) {
                    list[i]=new Material(list[i]);materials.Add(list[i]);
                    if(list[i].HasProperty("_FadeRate"))list[i].SetFloat("_FadeRate",0);
                    if(list[i].HasProperty("_ObjectHeight"))list[i].SetFloat("_ObjectHeight",100);
                    if(list[i].HasProperty("_ObjectFadeHeight"))list[i].SetFloat("_ObjectFadeHeight",1);
                }
                renderer.sharedMaterials=list;
            }
            foreach(var spec in manifest.clips) {
                var clip=clips[spec.name];var item=report.clips.Single(r=>r.name==spec.name);
                item.minHipHeight=float.PositiveInfinity;
                var graph=PlayableGraph.Create("Verify "+spec.name);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                try {
                    animator.runtimeAnimatorController=null;animator.Rebind();
                    var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);playable.SetSpeed(0);
                    var output=AnimationPlayableOutput.Create(graph,"Sword pose",animator);output.SetSourcePlayable(playable);graph.Play();
                    Vector3 first=Vector3.zero;
                    for(int frame=spec.firstFrame;frame<=spec.lastFrame;frame++) {
                        playable.SetTime((frame-spec.firstFrame)/manifest.fps);playable.SetDone(false);graph.Evaluate(0);
                        var position=instance.transform.InverseTransformPoint(sword.position);
                        if(frame==spec.firstFrame)first=position;item.swordTravel=Mathf.Max(item.swordTravel,Vector3.Distance(position,first));
                        float height=instance.transform.InverseTransformPoint(hips.position).y;
                        item.minHipHeight=Mathf.Min(item.minHipHeight,height);item.maxHipHeight=Mathf.Max(item.maxHipHeight,height);
                        Require(instance.transform.position.sqrMagnitude<.000001f,"Sword clip moved the player root: "+spec.name);
                        Require(IsFinite(position) && IsFinite(hand.position),"Non-finite sword or hand pose: "+spec.name);
                        var reference=references.clips.Single(c=>c.name==spec.name).samples.FirstOrDefault(s=>s.frame==frame);
                        if(reference!=null) {
                            foreach(var referenceBone in reference.bones) {
                                Require(bones.TryGetValue(PlayerBoneName(referenceBone.name),out var targetBone),"Unbound reference bone: "+referenceBone.name);
                                var p=referenceBone.position;
                                var expectedPosition=new Vector3(-p.x,p.z,-p.y);
                                item.maximumBonePositionError=Mathf.Max(item.maximumBonePositionError,Vector3.Distance(instance.transform.InverseTransformPoint(targetBone.position),expectedPosition));
                            }
                            float expected=Vector3.Distance(reference.bones.Single(b=>b.name=="Sword_Main").position,reference.bones.Single(b=>b.name=="DEF-hand.R").position);
                            float error=Mathf.Abs(Vector3.Distance(sword.position,hand.position)-expected);
                            item.maximumGripError=Mathf.Max(item.maximumGripError,error);
                            Capture(camera,"Logs/SwordAnimations/"+spec.name+"_"+frame+".png");
                        }
                    }
                    Require(item.minHipHeight>.6f && item.maxHipHeight<1.65f,"Sword clip loses the seated rider pose: "+spec.name);
                    Require(item.maximumGripError<.05f,"Sword/hand attachment differs from Blender by "+item.maximumGripError+"m: "+spec.name);
                    Require(item.maximumBonePositionError<.01f,"Bone poses differ from Blender by "+item.maximumBonePositionError+"m: "+spec.name);
                    if(spec.name.Contains("Attack"))Require(item.swordTravel>.25f,"Sword attack imported as a static pose: "+spec.name);
                } finally {graph.Destroy();}
            }
        } finally {
            if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);
            foreach(var material in materials)UnityEngine.Object.DestroyImmediate(material);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static bool IsFinite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    // The latest Blender source adds trailing dots to several right-jacket bone names.
    private static string PlayerBoneName(string name) => name.StartsWith("jacket_") ? name.TrimEnd('.') : name;
    private static void Capture(Camera camera,string path) {
        var target=new RenderTexture(640,640,24);var old=camera.targetTexture;var active=RenderTexture.active;
        var texture=new Texture2D(640,640,TextureFormat.RGB24,false);
        try {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,640,640),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
        } finally {
            camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);
        }
    }
    private static void CreatePreview(GameObject prefab,Manifest manifest,Dictionary<string,AnimationClip> clips) {
        string path=Folder+"/SwordPreview.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine=controller.layers[0].stateMachine;
        foreach(var old in machine.states)machine.RemoveState(old.state);
        foreach(var spec in manifest.clips) {
            var state=machine.AddState(spec.name);state.motion=clips[spec.name];state.writeDefaultValues=true;
            if(spec.name=="Player_Sword_Idle")machine.defaultState=state;
        }
        EditorUtility.SetDirty(controller);
        var scene=EditorSceneManager.NewPreviewScene();var instance=VisualInstance(prefab,scene);
        try {
            instance.name="PlayerSwordPreview";instance.GetComponent<Animator>().runtimeAnimatorController=controller;
            PrefabUtility.SaveAsPrefabAsset(instance,Folder+"/PlayerSwordPreview.prefab");
        } finally {UnityEngine.Object.DestroyImmediate(instance);EditorSceneManager.ClosePreviewScene(scene);}
    }
}


