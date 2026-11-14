using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EnemyHitReactionSetup {
    public static void Configure() {
        var shader=Shader.Find("CyberBiker/Impact Spark Streak");
        if(shader==null || ShaderUtil.ShaderHasError(shader))throw new Exception("Spark shader must compile");
        const string path="Assets/Resources/EnemyBulletSparks.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
        material.shader=shader;material.SetFloat("_Intensity",1.2f);EditorUtility.SetDirty(material);
        foreach(var prefab in new[]{"Assets/enemy.prefab","Assets/enemy 1.prefab"}) {
            var root=PrefabUtility.LoadPrefabContents(prefab);
            try {
                var enemy=root.GetComponent<Enemy>();var before=EditorJsonUtility.ToJson(enemy);
                var reaction=root.GetComponent<EnemyHitReaction>() ?? root.AddComponent<EnemyHitReaction>();
                Transform torso=null;
                foreach(var bone in root.GetComponentsInChildren<Transform>(true))if(bone.name=="DEF-spine.002"){torso=bone;break;}
                if(torso==null)throw new Exception("Missing rider spine: "+prefab);
                var settings=new SerializedObject(reaction);settings.FindProperty("torso").objectReferenceValue=torso;settings.FindProperty("sparkMaterial").objectReferenceValue=material;settings.ApplyModifiedPropertiesWithoutUndo();
                if(before!=EditorJsonUtility.ToJson(enemy))throw new Exception("Enemy gameplay settings changed");
                PrefabUtility.SaveAsPrefabAsset(root,prefab);
            } finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();Directory.CreateDirectory("Logs/EnemyHitReaction");
        File.WriteAllText("Logs/EnemyHitReaction/Setup.txt","PASS: both enemy prefabs have a configured spine jolt and contact spark material; Enemy gameplay settings preserved.\n");
    }
}
