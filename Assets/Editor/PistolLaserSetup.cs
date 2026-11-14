using System;
using System.IO;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;

public static class PistolLaserSetup {
    public static void Configure(){
        var shader=Shader.Find("CyberBiker/Pistol Aim Laser");if(shader==null || ShaderUtil.ShaderHasError(shader))throw new Exception("Pistol laser shader must compile");
        const string materialPath="Assets/Resources/PlayerPistolLaser.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}material.SetFloat("_Intensity",1.8f);EditorUtility.SetDirty(material);
        const string path="Assets/Art/Models/Player_Last.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try{
            string before=EditorJsonUtility.ToJson(root.GetComponent<PlayerController>()),sword=EditorJsonUtility.ToJson(root.GetComponent<BikeSwordCombat>());
            var laser=root.GetComponent<PistolAimLaser>() ?? root.AddComponent<PistolAimLaser>();var settings=new SerializedObject(laser);settings.FindProperty("laserMaterial").objectReferenceValue=material;settings.ApplyModifiedPropertiesWithoutUndo();
            if(before!=EditorJsonUtility.ToJson(root.GetComponent<PlayerController>()) || sword!=EditorJsonUtility.ToJson(root.GetComponent<BikeSwordCombat>()))throw new Exception("Laser setup changed gameplay tuning");
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Directory.CreateDirectory("Logs/TapPistol");File.WriteAllText("Logs/TapPistol/LaserSetup.txt","PASS configured red muzzle laser and shared material; existing player/sword tuning preserved.\n");
    }
}
