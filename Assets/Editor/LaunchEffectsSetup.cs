using System;
using System.IO;
using System.Linq;
using CyberBiker.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LaunchEffectsSetup {
    public const string Player="Assets/Art/Models/Player_Last.prefab";
    public const string Flow="Assets/Art/Animations/Sword/LaunchFlow.mat";
    public const string Flame="Assets/Art/Animations/Sword/LaunchFlame.mat";
    public const string WindParticles="Assets/Art/Animations/Sword/LaunchWindParticles.prefab";
    public const string WindGlow="Assets/Art/Animations/Sword/LaunchWindGlow.mat";
    private static ParticleSystem BuildWindParticles() {
        var existing=AssetDatabase.LoadAssetAtPath<ParticleSystem>(WindParticles);if(existing!=null)return existing;
        var scene=SceneManager.GetSceneByPath("Assets/Level/Scenes/Level.unity");bool opened=!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity",OpenSceneMode.Additive);
        GameObject copy=null;
        try {
            var original=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ParticleSystem>(true)).First(p=>p.name=="WindParticle");
            copy=UnityEngine.Object.Instantiate(original.gameObject);copy.name="Launch wind particles";
            foreach(var script in copy.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(script);
            copy.transform.SetParent(null,false);copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);copy.transform.localScale=Vector3.one;
            var particles=copy.GetComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;var trails=particles.trails;trails.enabled=false;
            return PrefabUtility.SaveAsPrefabAsset(copy,WindParticles).GetComponent<ParticleSystem>();
        }finally{if(copy!=null)UnityEngine.Object.DestroyImmediate(copy);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
    [MenuItem("CyberBiker/Combat/Install Launch presentation")]
    public static void Install() {
        var wind=BuildWindParticles();
        var ignition=NitroIgnitionSetup.Build();
        var windShader=Shader.Find("CyberBiker/Launch Wind Particle");
        if(windShader==null || ShaderUtil.ShaderHasError(windShader))throw new Exception("Launch wind glow shader must compile");
        var windGlow=AssetDatabase.LoadAssetAtPath<Material>(WindGlow);
        if(windGlow==null){windGlow=new Material(windShader){name="LaunchWindGlow"};AssetDatabase.CreateAsset(windGlow,WindGlow);}
        var shader=Shader.Find("CyberBiker/Launch Flow");var border=Shader.Find("Hidden/CyberBiker/Launch Screen");var fireShader=Shader.Find("CyberBiker/Launch Flame");
        if(shader==null || border==null || fireShader==null || ShaderUtil.ShaderHasError(shader) || ShaderUtil.ShaderHasError(border) || ShaderUtil.ShaderHasError(fireShader))throw new Exception("Launch shaders must compile");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Flow);
        if(material==null){material=new Material(shader){name="LaunchFlow"};AssetDatabase.CreateAsset(material,Flow);}
        var flame=AssetDatabase.LoadAssetAtPath<Material>(Flame);
        if(flame==null){flame=new Material(fireShader){name="LaunchFlame"};AssetDatabase.CreateAsset(flame,Flame);}
        flame.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StoreAssets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr fire edge.png"));
        flame.SetTexture("_NoiseTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StoreAssets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr uv distortion fire perlin.png"));EditorUtility.SetDirty(flame);
        var root=PrefabUtility.LoadPrefabContents(Player);
        try {
            var component=root.GetComponent<BikeLaunchEffects>() ?? root.AddComponent<BikeLaunchEffects>();var settings=new SerializedObject(component);
            settings.FindProperty("motorcycle").objectReferenceValue=root.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="Motorcycle").transform;
            settings.FindProperty("flowMaterial").objectReferenceValue=material;settings.FindProperty("borderShader").objectReferenceValue=border;
            settings.FindProperty("flameMaterial").objectReferenceValue=flame;
            settings.FindProperty("windParticleTemplate").objectReferenceValue=wind;
            settings.FindProperty("windParticleMaterial").objectReferenceValue=windGlow;
            settings.FindProperty("ignitionPrefab").objectReferenceValue=ignition;
            var jacketAnchors=settings.FindProperty("jacketWindAnchors");
            if(jacketAnchors.arraySize==0) {
                var hems=new[]{"jacket_01_03_end","jacket_01_03.L_end","jacket_01_03.R_end"};jacketAnchors.arraySize=hems.Length;
                for(int i=0;i<hems.Length;i++)jacketAnchors.GetArrayElementAtIndex(i).objectReferenceValue=root.GetComponentsInChildren<Transform>(true).First(t=>t.name==hems[i]);
            }
            settings.FindProperty("powerEngineLoop").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CyberBiker/LaunchEnginePower.wav");
            settings.FindProperty("exhaustCrack").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CyberBiker/LaunchExhaustCrack.wav");
            if(settings.FindProperty("powerEngineLoop").objectReferenceValue==null || settings.FindProperty("exhaustCrack").objectReferenceValue==null)throw new Exception("Launch sound clips missing");
            settings.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,Player);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
    public static void Probe() {
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
        try {
            Directory.CreateDirectory("Logs/LaunchEffects");
            var bike=root.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="Motorcycle");
            File.WriteAllText("Logs/LaunchEffects/bike.txt","Mesh bounds "+bike.sharedMesh.bounds+"\nWorld bounds "+bike.GetComponent<Renderer>().bounds+"\n");
            var vertices=bike.sharedMesh.vertices;var normals=bike.sharedMesh.normals;
            using(var writer=new StreamWriter("Logs/LaunchEffects/vertices.csv")){writer.WriteLine("x,y,z,nx,ny,nz");for(int i=0;i<vertices.Length;i++){var p=vertices[i];var n=normals[i];writer.WriteLine(FormattableString.Invariant($"{p.x},{p.y},{p.z},{n.x},{n.y},{n.z}"));}}
            for(int side=0;side<2;side++) {
                var go=new GameObject("Exhaust review camera");var camera=go.AddComponent<Camera>();
                camera.transform.position=root.transform.TransformPoint(side==0 ? new Vector3(4,2,-4) : new Vector3(5,1.2f,-.3f));camera.transform.LookAt(root.transform.TransformPoint(new Vector3(0,.9f,-.1f)));
                camera.orthographic=true;camera.orthographicSize=1.7f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.07f,.09f);
                var rt=new RenderTexture(1100,900,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
                var image=new Texture2D(1100,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1100,900),0,0);image.Apply();File.WriteAllBytes("Logs/LaunchEffects/exhaust-"+side+".png",image.EncodeToPNG());
                RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
            }
            EditorApplication.Exit(0);
        }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }
}
