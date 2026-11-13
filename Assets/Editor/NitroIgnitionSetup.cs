using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class NitroIgnitionSetup {
    public const string Folder="Assets/Art/Animations/Sword/";
    public const string Prefab=Folder+"NitroIgnition.prefab";
    private static Material Material(string name,int mode,float glow,Texture texture=null) {
        string path=Folder+name+".mat";var result=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(result!=null)return result;
        var shader=Shader.Find("CyberBiker/Nitro Ignition");
        if(shader==null || ShaderUtil.ShaderHasError(shader))throw new Exception("Nitro ignition shader must compile");
        result=new Material(shader){name=name};result.SetFloat("_Mode",mode);result.SetFloat("_Glow",glow);if(texture!=null)result.SetTexture("_MainTex",texture);
        AssetDatabase.CreateAsset(result,path);return result;
    }
    private static ParticleSystem Layer(string name,Transform parent,Material material,int count,float lifetime,float size,float speed,Color tint) {
        var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);
        var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);system.useAutoRandomSeed=false;system.randomSeed=(uint)(9200+name.Length);
        var main=system.main;main.duration=.7f;main.loop=false;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;main.startLifetime=lifetime;main.startSize=size;main.startSpeed=speed;main.startColor=tint;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.maxParticles=96;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        var emission=system.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
        var shape=system.shape;shape.enabled=speed>0;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=35;shape.radius=.035f;
        var color=system.colorOverLifetime;color.enabled=true;var fade=new Gradient();fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.65f,.3f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(.65f,.4f),new GradientAlphaKey(0,1)});color.color=fade;
        var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=.22f;
        return system;
    }
    private static void Size(ParticleSystem system,float start,float peak,float end) {
        var size=system.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,start),new Keyframe(.25f,peak),new Keyframe(1,end)));
    }
    public static ParticleSystem Build() {
        var existing=AssetDatabase.LoadAssetAtPath<ParticleSystem>(Prefab);if(existing!=null)return existing;
        var flashMat=Material("NitroIgnitionFlash",0,1.6f);
        var ringMat=Material("NitroIgnitionRing",1,1.8f);
        var fireTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StoreAssets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr fire small anim.png");
        if(fireTexture==null)throw new Exception("Cartoon FX fire texture missing");
        var fireMat=Material("NitroIgnitionFire",2,1.15f,fireTexture);
        var smokeMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Player/TireContactSmoke.mat");
        var flash=Layer("Nitro ignition flash",null,flashMat,1,.055f,.6f,0,new Color(1,.75f,.3f,.85f));
        try {
            Size(flash,.2f,1,.15f);
            var ring=Layer("Shock ring",flash.transform,ringMat,1,.23f,.7f,0,new Color(.06f,.45f,1,.6f));Size(ring,.2f,.8f,2);
            var renderer=ring.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;
            var fire=Layer("Fire puffs",flash.transform,fireMat,7,.24f,.42f,2.5f,new Color(1,.42f,.08f,.85f));Size(fire,.3f,1.1f,.65f);
            var main=fire.main;main.startLifetime=new ParticleSystem.MinMaxCurve(.16f,.28f);main.startSize=new ParticleSystem.MinMaxCurve(.3f,.52f);
            var sheet=fire.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=3;sheet.numTilesY=3;sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
            var sparks=Layer("Ignition sparks",flash.transform,flashMat,26,.25f,.025f,6,new Color(1,.55f,.12f,.95f));main=sparks.main;main.startLifetime=new ParticleSystem.MinMaxCurve(.15f,.32f);main.startSpeed=new ParticleSystem.MinMaxCurve(4,8);
            renderer=sparks.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=3;renderer.velocityScale=.015f;Size(sparks,1,.7f,0);
            var smoke=Layer("Pressure smoke",flash.transform,smokeMat,3,.42f,.45f,1.6f,new Color(.42f,.35f,.3f,.5f));Size(smoke,.3f,.9f,1.4f);
            sheet=smoke.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=2;sheet.numTilesY=2;sheet.frameOverTime=0;sheet.startFrame=new ParticleSystem.MinMaxCurve(0,1);
            renderer=smoke.GetComponent<ParticleSystemRenderer>();renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});
            return PrefabUtility.SaveAsPrefabAsset(flash.gameObject,Prefab).GetComponent<ParticleSystem>();
        }finally{UnityEngine.Object.DestroyImmediate(flash.gameObject);}
    }
}
