using System;
using System.IO;
using System.Linq;
using CyberBiker.Player;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SwordCombatSetup {
    private const string Library="Assets/Art/Animations/Sword/";
    private const string Player="Assets/Art/Models/Player_Last.prefab";
    [Serializable] private class Reference {public string[] boneNames;}
    [Serializable] private class Trajectory {public float contactTime;public Vector3 contactPoint;}
    [Serializable] private class Trajectories {public Trajectory Left,Right;}
    public static void UpgradeBatch(){try{UpgradeFinisher();Debug.Log("SWORD FINISHER SETUP PASS");EditorApplication.Exit(0);}catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}}
    [MenuItem("CyberBiker/Combat/Upgrade sword finisher effects")]
    public static void UpgradeFinisher() {
        const string materialPath=Library+"SwordRibbon.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){var shader=Shader.Find("CyberBiker/Sword Ribbon");if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Sword ribbon shader must compile");material=new Material(shader){name="SwordRibbon"};AssetDatabase.CreateAsset(material,materialPath);}
        var root=PrefabUtility.LoadPrefabContents(Player);
        try {
            var combat=root.GetComponent<BikeSwordCombat>();if(combat==null)throw new InvalidOperationException("Install the sword library first");
            var paths=JsonUtility.FromJson<Trajectories>(File.ReadAllText("SourceAssets/Animations/SwordBladeTrajectories.json"));
            var settings=new SerializedObject(combat);
            settings.FindProperty("leftStrikePoint").vector3Value=paths.Left.contactPoint;settings.FindProperty("rightStrikePoint").vector3Value=paths.Right.contactPoint;
            settings.FindProperty("strikeContactTime").floatValue=paths.Left.contactTime;settings.ApplyModifiedPropertiesWithoutUndo();
            settings.FindProperty("pickupRange").floatValue=14;settings.FindProperty("pickupSideRange").floatValue=9;
            settings.FindProperty("dashDuration").floatValue=.3f;settings.FindProperty("strikePlaybackSpeed").floatValue=.95f;settings.FindProperty("drawPlaybackSpeed").floatValue=4.5f;settings.FindProperty("contactPoseHold").floatValue=.08f;
            settings.FindProperty("sliceReturnPlaybackSpeed").floatValue=3.5f;settings.FindProperty("sliceReturnBlendTime").floatValue=.08f;settings.ApplyModifiedPropertiesWithoutUndo();
            var effects=root.GetComponent<SwordFinisherEffects>() ?? root.AddComponent<SwordFinisherEffects>();
            var fx=new SerializedObject(effects);fx.FindProperty("ribbonMaterial").objectReferenceValue=material;
            fx.FindProperty("hitSlowDuration").floatValue=.55f;fx.FindProperty("hitTimeScale").floatValue=.2f;
            fx.FindProperty("slowMotionRecovery").floatValue=.65f;
            fx.FindProperty("explosionTimeScale").floatValue=.28f;fx.FindProperty("explosionSlowDuration").floatValue=.9f;
            fx.FindProperty("dashTimeScale").floatValue=.35f;fx.FindProperty("dashSlowDuration").floatValue=.65f;
            fx.FindProperty("hitShake").floatValue=.12f;fx.FindProperty("hitZoom").floatValue=-8;
            fx.FindProperty("swingZoom").floatValue=2;fx.FindProperty("cameraDolly").floatValue=.2f;
            fx.FindProperty("aftermathPullback").floatValue=1.1f;fx.FindProperty("aftermathCameraLift").floatValue=.35f;fx.FindProperty("aftermathCameraDuration").floatValue=1.5f;
            fx.FindProperty("hitCameraSideShift").floatValue=1.25f;fx.FindProperty("hitCameraSideMoveTime").floatValue=.18f;fx.FindProperty("hitCameraSideReturnTime").floatValue=.55f;fx.FindProperty("hitCameraLookTurn").floatValue=12;
            var screenShader=Shader.Find("Hidden/CyberBiker/Sword Impact Screen");
            if(screenShader==null || ShaderUtil.ShaderHasError(screenShader))throw new InvalidOperationException("Sword impact screen shader must compile");
            fx.FindProperty("impactScreenShader").objectReferenceValue=screenShader;
            var swishes=fx.FindProperty("swordSwishes");swishes.arraySize=2;
            for(int i=0;i<2;i++)swishes.GetArrayElementAtIndex(i).objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CyberBiker/SwordSwish_0"+(i+1)+".wav");
            fx.FindProperty("swordMetalHit").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CyberBiker/impactMetal_heavy_001.ogg");
            fx.FindProperty("swordLowImpact").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CyberBiker/SwordImpactLow.ogg");
            fx.FindProperty("explosionBlast").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CyberBiker/SwordExplosion_Blast.ogg");
            fx.FindProperty("explosionBass").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CyberBiker/lowFrequency_explosion_000.ogg");
            fx.FindProperty("explosionDebris").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CyberBiker/impactPlate_heavy_000.ogg");
            var tires=root.GetComponent<TireContactParticles>();
            if(tires!=null){var tireSettings=new SerializedObject(tires);fx.FindProperty("smokeMaterial").objectReferenceValue=tireSettings.FindProperty("smokeMaterial").objectReferenceValue;fx.FindProperty("smokeTemplate").objectReferenceValue=tireSettings.FindProperty("smokeTemplate").objectReferenceValue;}
            fx.FindProperty("hitPrefab").objectReferenceValue=BuildImpact();
            fx.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,Player);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
    private static ParticleSystem BuildImpact() {
        const string path=Library+"SwordImpact.prefab";
        var material=AssetDatabase.LoadAssetAtPath<Material>(Library+"SwordImpact.mat");
        if(material==null){var shader=Shader.Find("CyberBiker/Sword Impact");if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Sword impact shader must compile");material=new Material(shader){name="SwordImpact"};AssetDatabase.CreateAsset(material,Library+"SwordImpact.mat");}
        var root=new GameObject("Sword impact");
        try {
            var flash=root.AddComponent<ParticleSystem>();flash.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=flash.main;main.duration=.35f;main.loop=false;main.startLifetime=.08f;main.startSpeed=0;main.startSize=2.2f;main.startColor=new Color(3,3,3,1);main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var shape=flash.shape;shape.enabled=false;var emission=flash.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,1)});
            var size=flash.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.12f,1),new Keyframe(1,0)));
            var renderer=flash.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            var sparksObject=new GameObject("Cut sparks");sparksObject.transform.SetParent(root.transform,false);var sparks=sparksObject.AddComponent<ParticleSystem>();
            main=sparks.main;main.duration=.4f;main.loop=false;main.startLifetime=new ParticleSystem.MinMaxCurve(.15f,.32f);main.startSpeed=new ParticleSystem.MinMaxCurve(7,14);main.startSize=new ParticleSystem.MinMaxCurve(.035f,.075f);main.startColor=new Color(3,1.3f,.3f,1);main.simulationSpace=ParticleSystemSimulationSpace.Local;
            emission=sparks.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,38)});
            shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=65;shape.radius=.08f;
            var color=sparks.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.3f,.1f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
            renderer=sparks.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=3;renderer.velocityScale=.04f;
            var fragmentsObject=new GameObject("Slice fragments");fragmentsObject.transform.SetParent(root.transform,false);var fragments=fragmentsObject.AddComponent<ParticleSystem>();fragments.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            main=fragments.main;main.duration=.3f;main.loop=false;main.startLifetime=new ParticleSystem.MinMaxCurve(.1f,.2f);main.startSpeed=new ParticleSystem.MinMaxCurve(3,8);main.startSize=new ParticleSystem.MinMaxCurve(.1f,.2f);main.startColor=new Color(.3f,1.6f,2.5f,1);main.simulationSpace=ParticleSystemSimulationSpace.Local;
            emission=fragments.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,7)});shape=fragments.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=80;shape.radius=.05f;
            renderer=fragments.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=5;renderer.velocityScale=.04f;
            return PrefabUtility.SaveAsPrefabAsset(root,path).GetComponent<ParticleSystem>();
        }finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    public static void RunBatch() {
        try{Install();Debug.Log("SWORD SETUP PASS");EditorApplication.Exit(0);}
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    [MenuItem("CyberBiker/Combat/Install sword launch experiment")]
    public static void Install() {
        var root=PrefabUtility.LoadPrefabContents(Player);
        try {
            var combat=root.GetComponent<BikeSwordCombat>() ?? root.AddComponent<BikeSwordCombat>();
            var settings=new SerializedObject(combat);
            foreach(var pair in new[]{("draw","Grab"),("sheathe","Back"),("idle","Idle"),("idleLeft","Idle_Left"),("idleRight","Idle_Right"),("attackLeft","Attack_Left"),("attackRight","Attack_Right")}) {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Library+"Player_Sword_"+pair.Item2+".anim");
                if(clip==null)throw new InvalidOperationException("Missing sword clip "+pair.Item2);
                settings.FindProperty(pair.Item1).objectReferenceValue=clip;
            }
            var names=JsonUtility.FromJson<Reference>(File.ReadAllText("SourceAssets/Animations/SwordPoseReference.json")).boneNames;
            var transforms=root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.First());
            var bones=settings.FindProperty("poseBones");bones.arraySize=names.Length;
            for(int i=0;i<names.Length;i++)bones.GetArrayElementAtIndex(i).objectReferenceValue=transforms[names[i].StartsWith("jacket_") ? names[i].TrimEnd('.') : names[i]];
            settings.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,Player);
        } finally {PrefabUtility.UnloadPrefabContents(root);}
        var scene=EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");
        var player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        var sword=player.GetComponent<BikeSwordCombat>();
        var aim=GameObject.Find("AimButton").GetComponent<RectTransform>();
        var old=GameObject.Find("LaunchButton");if(old!=null)UnityEngine.Object.DestroyImmediate(old);
        var buttonObject=new GameObject("LaunchButton",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));
        var rect=buttonObject.GetComponent<RectTransform>();rect.SetParent(aim.parent,false);
        rect.anchorMin=aim.anchorMin;rect.anchorMax=aim.anchorMax;rect.pivot=aim.pivot;
        rect.anchoredPosition=aim.anchoredPosition+new Vector2(0,-255);rect.sizeDelta=new Vector2(260,100);rect.localScale=Vector3.one;
        var image=buttonObject.GetComponent<Image>();image.color=new Color(.33f,.035f,.65f,.94f);
        var button=buttonObject.GetComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.highlightedColor=new Color(.8f,.8f,1);colors.pressedColor=new Color(.55f,.55f,.8f);button.colors=colors;
        var navigation=button.navigation;navigation.mode=Navigation.Mode.None;button.navigation=navigation;
        UnityEventTools.AddPersistentListener(button.onClick,sword.Launch);
        var textObject=new GameObject("Label",typeof(RectTransform));textObject.transform.SetParent(rect,false);
        var text=textObject.AddComponent<TextMeshProUGUI>();text.text="LAUNCH";text.fontSize=36;text.fontStyle=FontStyles.Bold;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;
        var font=UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).FirstOrDefault(t=>t!=text && t.font!=null);if(font!=null)text.font=font.font;
        var textRect=text.rectTransform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=Vector2.zero;textRect.offsetMax=Vector2.zero;
        var serialized=new SerializedObject(sword);serialized.FindProperty("launchButton").objectReferenceValue=button;serialized.FindProperty("launchLabel").objectReferenceValue=text;serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(sword);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
}
