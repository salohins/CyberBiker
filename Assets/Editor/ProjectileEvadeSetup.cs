using System;
using System.IO;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;

public static class ProjectileEvadeSetup {
    public static void Configure(){
        var shader=Shader.Find("CyberBiker/Readable Hostile Projectile");if(shader==null || ShaderUtil.ShaderHasError(shader))throw new Exception("Hostile projectile shader must compile");
        const string materialPath="Assets/Resources/HostileProjectile.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
        const string path="Assets/Art/Models/Player_Last.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try{
            string before=EditorJsonUtility.ToJson(root.GetComponent<PlayerController>()),sword=EditorJsonUtility.ToJson(root.GetComponent<BikeSwordCombat>());
            if(root.GetComponent<PlayerProjectileEvade>()==null)root.AddComponent<PlayerProjectileEvade>();
            if(before!=EditorJsonUtility.ToJson(root.GetComponent<PlayerController>()) || sword!=EditorJsonUtility.ToJson(root.GetComponent<BikeSwordCombat>()))throw new Exception("Evade setup changed gameplay tuning");
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Directory.CreateDirectory("Logs/ProjectileEvade");File.WriteAllText("Logs/ProjectileEvade/Setup.txt","PASS readable bullet material and player evade component; movement/sword settings preserved.\n");
    }
}
