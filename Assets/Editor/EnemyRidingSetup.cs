using System;
using System.IO;
using CyberBiker.Player;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

public static class EnemyRidingSetup {
    public static void ConfigurePrefabs() {
        Directory.CreateDirectory("Logs/EnemyRiding");string report="";
        var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Player_Last.prefab").GetComponent<TireContactParticles>();
        if(player==null)throw new Exception("Player tire effects are required as the style source");
        var sparkPath="Assets/Art/Materials/EnemyTireSparks.mat";
        var enemySparks=AssetDatabase.LoadAssetAtPath<Material>(sparkPath);
        if(enemySparks==null){
            var source=(Material)new SerializedObject(player).FindProperty("particleMaterial").objectReferenceValue;
            enemySparks=new Material(source){name="EnemyTireSparks"};AssetDatabase.CreateAsset(enemySparks,sparkPath);
        }
        enemySparks.SetFloat("_Intensity",1.2f);enemySparks.SetFloat("_CoreWhiteness",.1f);EditorUtility.SetDirty(enemySparks);
        foreach(var path in new[]{"Assets/enemy.prefab","Assets/enemy 1.prefab"}) {
            var root=PrefabUtility.LoadPrefabContents(path);
            try {
                var enemy=root.GetComponent<Enemy>();string before=EditorJsonUtility.ToJson(enemy);
                var effects=root.GetComponent<TireContactParticles>() ?? root.AddComponent<TireContactParticles>();
                EditorUtility.CopySerialized(player,effects);var settings=new SerializedObject(effects);
                settings.FindProperty("particleMaterial").objectReferenceValue=enemySparks;
                Set(settings,"steeringEmission",130);Set(settings,"frontWheelAmount",.18f);Set(settings,"particleSize",1.15f);
                Set(settings,"smokeLifetime",.65f);Set(settings,"smokeRise",.65f);Set(settings,"smokeOpacity",.9f);
                Set(settings,"smokeWindSlowdown",.55f);Set(settings,"smokeWindSpread",.32f);Set(settings,"smokeReleaseTime",.16f);
                Set(settings,"startLeanAngle",20);Set(settings,"fullLeanAngle",30);Set(settings,"leanReleaseMargin",3);
                Set(settings,"skidSparkEmission",70);Set(settings,"skidSparkSize",.105f);Set(settings,"skidSparkStreakLength",4);
                Set(settings,"skidSparkLifetime",.22f);Set(settings,"hardLeanSparkBoost",2);Set(settings,"enemyEffectRange",90);
                MeshFilter motorcycle=null;foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))if(filter.name=="Motorcycle"){motorcycle=filter;break;}
                if(motorcycle==null)throw new Exception("Enemy motorcycle mesh not found: "+path);
                Contacts(root.transform,motorcycle,out var rear,out var front);
                settings.FindProperty("rearWheelContact").vector3Value=rear;settings.FindProperty("frontWheelContact").vector3Value=front;settings.ApplyModifiedPropertiesWithoutUndo();
                if(before!=EditorJsonUtility.ToJson(enemy))throw new Exception("Adding tire particles must preserve enemy gameplay fields");
                PrefabUtility.SaveAsPrefabAsset(root,path);
                report+=path+": rear="+rear.ToString("F4")+", front="+front.ToString("F4")+"; player Cartoon FX materials/template reused; enemy gameplay fields preserved.\n";
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        File.WriteAllText("Logs/EnemyRiding/AssetSetup.txt",report);AssetDatabase.SaveAssets();
    }
    private static void Set(SerializedObject settings,string field,float value)=>settings.FindProperty(field).floatValue=value;
    private static void Contacts(Transform root,MeshFilter filter,out Vector3 rear,out Vector3 front) {
        using(var data=MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh))
        using(var vertices=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp)) {
            data[0].GetVertices(vertices);float floor=float.PositiveInfinity;
            for(int i=0;i<vertices.Length;i++) {
                var point=root.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                if(Mathf.Abs(point.x)<.5f)floor=Mathf.Min(floor,point.y);
            }
            float min=float.PositiveInfinity,max=float.NegativeInfinity;
            for(int i=0;i<vertices.Length;i++) {
                var point=root.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                if(Mathf.Abs(point.x)<.5f && point.y<=floor+.08f){min=Mathf.Min(min,point.z);max=Mathf.Max(max,point.z);}
            }
            float rearZ=0,frontZ=0;int nr=0,nf=0;
            for(int i=0;i<vertices.Length;i++) {
                var point=root.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                if(Mathf.Abs(point.x)>=.5f || point.y>floor+.08f)continue;
                if(point.z<(min+max)*.5f){rearZ+=point.z;nr++;}else{frontZ+=point.z;nf++;}
            }
            if(nr==0 || nf==0 || max-min<.5f)throw new Exception("Cannot identify both grounded tire arcs");
            rear=filter.transform.InverseTransformPoint(root.TransformPoint(new Vector3(0,floor+.02f,rearZ/nr)));
            front=filter.transform.InverseTransformPoint(root.TransformPoint(new Vector3(0,floor+.02f,frontZ/nf)));
        }
    }
}
