using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TireVisualChecks {
    public static void InspectBody() {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Player_Last.prefab");
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
        var report=new StringBuilder();
        report.AppendLine($"ROOT SCALE {instance.transform.lossyScale}");
        var collider=instance.GetComponent<BoxCollider>();
        report.AppendLine($"PHYSICS BOX center={collider.center:F5} size={collider.size:F5}");
        foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true)) {
            var renderer=filter.GetComponent<MeshRenderer>();
            if(renderer==null || !renderer.enabled || filter.sharedMesh==null) continue;
            DescribeBody(report,instance.transform,filter.transform,filter.sharedMesh,renderer.sharedMaterials);
        }
        foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            if(!renderer.enabled) continue;
            var mesh=new Mesh();renderer.BakeMesh(mesh);
            DescribeBody(report,instance.transform,renderer.transform,mesh,renderer.sharedMaterials);
            Object.DestroyImmediate(mesh);
        }
        Directory.CreateDirectory("Logs/Tire");
        File.WriteAllText("Logs/Tire/body-geometry.txt",report.ToString());
        Object.DestroyImmediate(instance);
    }
    private static void DescribeBody(StringBuilder report,Transform root,Transform owner,Mesh mesh,Material[] materials) {
        var vertices=mesh.vertices;
        for(int s=0;s<mesh.subMeshCount;s++) {
            var bounds=new Bounds();bool first=true;Vector3 left=Vector3.zero,right=Vector3.zero;
            foreach(var index in mesh.GetTriangles(s)) {
                var point=root.InverseTransformPoint(owner.TransformPoint(vertices[index]));
                if(first) {bounds=new Bounds(point,Vector3.zero);left=right=point;first=false;}
                else {bounds.Encapsulate(point);if(point.x<left.x) left=point;if(point.x>right.x) right=point;}
            }
            report.AppendLine($"{owner.name} / {(s<materials.Length && materials[s]!=null ? materials[s].name : s.ToString())} min={bounds.min:F5} max={bounds.max:F5} LEFT={left:F5} RIGHT={right:F5}");
        }
    }
    public static void Render() {
        var shader=Shader.Find("CyberBiker/Stationary Lighting Tire");
        if(shader!=null) foreach(var message in ShaderUtil.GetShaderMessages(shader)) Debug.Log(message.message);
        if(shader==null || ShaderUtil.ShaderHasError(shader)) throw new System.Exception("Tire shader failed to compile");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Player_Last.prefab");
        var prefab=(GameObject)PrefabUtility.InstantiatePrefab(source);
        prefab.transform.position=Vector3.zero;
        MeshFilter filter=null;
        foreach(var f in prefab.GetComponentsInChildren<MeshFilter>()) if(f.name=="Motorcycle") filter=f;
        if(filter==null) throw new System.Exception("Motorcycle mesh missing");
        var renderer=filter.GetComponent<Renderer>();
        var materials=renderer.sharedMaterials;
        var tire=new Material(materials[3]);
        materials[3]=tire;renderer.sharedMaterials=materials;
        var camera=new GameObject("Tire check camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(0,1.0f,-4.2f);
        camera.transform.LookAt(new Vector3(0,.8f,-.9f));
        camera.orthographic=true;camera.orthographicSize=1.25f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.035f,.035f,1);
        var rt=new RenderTexture(768,768,24);camera.targetTexture=rt;
        Directory.CreateDirectory("Logs/Tire");
        float[] phases={0,.137f,.15575f};
        for(int i=0;i<phases.Length;i++) {
            tire.SetFloat("_TreadBlend",i==0 ? 0 : 1);tire.SetFloat("_TreadPhase",phases[i]);tire.SetFloat("_TreadBlur",i==0 ? 0 : .03f);
            camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(768,768,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();
            File.WriteAllBytes("Logs/Tire/frame"+i+".png",image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        var original=new Material(tire);original.shader=Shader.Find("Amazing Assets/Curved World/Unlit");
        original.EnableKeyword("_RIM");original.EnableKeyword("_REFLECTION");
        materials[3]=original;renderer.sharedMaterials=materials;
        camera.Render();RenderTexture.active=rt;
        var baseline=new Texture2D(768,768,TextureFormat.RGB24,false);
        baseline.ReadPixels(new Rect(0,0,768,768),0,0);baseline.Apply();
        File.WriteAllBytes("Logs/Tire/original.png",baseline.EncodeToPNG());
        if(ShaderUtil.ShaderHasError(shader)) throw new System.Exception("Tire shader compilation error after rendering");
        File.WriteAllText("Logs/Tire/render-check.txt","PASS: tire shader compiled and rendered at three fixed phases.");
        RenderTexture.active=null;camera.targetTexture=null;
        Object.DestroyImmediate(baseline);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(prefab);Object.DestroyImmediate(tire);Object.DestroyImmediate(original);
    }
    public static void Inspect() {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Player_Last.prefab");
        Directory.CreateDirectory("Logs/Tire");
        using (var w = new StreamWriter("Logs/Tire/mesh.txt")) {
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true)) {
                var renderer = mf.GetComponent<Renderer>();
                if (renderer == null) continue;
                var mesh = mf.sharedMesh;
                for (int s = 0; s < renderer.sharedMaterials.Length; s++) {
                    var mat = renderer.sharedMaterials[s];
                    w.WriteLine($"OBJECT {mf.name} MATERIAL {s} {mat.name} BOUNDS {mesh.bounds}");
                    if (mat.name != "MotoTiers") continue;
                    var v = mesh.vertices; var uv = mesh.uv;
                    foreach (var i in mesh.GetTriangles(s)) w.WriteLine($"{i} {v[i].x:R} {v[i].y:R} {v[i].z:R} {uv[i].x:R} {uv[i].y:R}");
                }
            }
        }
    }
}
