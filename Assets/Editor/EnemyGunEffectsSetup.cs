using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EnemyGunEffectsSetup {
    public static void ConfigureLaser(){
        var shader=Shader.Find("CyberBiker/Enemy Aim Laser");if(shader==null || ShaderUtil.ShaderHasError(shader))throw new Exception("Enemy laser shader must compile");
        const string path="Assets/Resources/EnemyAimLaser.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
        material.SetFloat("_Intensity",.8f);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
    }
    public static void Configure(){
        ConfigureLaser();
        var shader=Shader.Find("CyberBiker/Gun Air and Smoke");if(shader==null || ShaderUtil.ShaderHasError(shader))throw new Exception("Gun air/smoke shader must compile");
        Material SmokeMaterial(string path,bool air){
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.SetFloat("_Air",air ? 1 : 0);
            if(!air)material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StoreAssets/JMO Assets/Cartoon FX Remaster/CFXR Assets/Graphics/cfxr stretch smoke fume.png");
            EditorUtility.SetDirty(material);return material;
        }
        SmokeMaterial("Assets/Resources/EnemyMuzzleSmoke.mat",false);SmokeMaterial("Assets/Resources/BulletAirWake.mat",true);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StoreAssets/Toon Muzzleflash Pack/Example Prefabs/Toon Muzzleflash 8.prefab");if(source==null)throw new Exception("Existing muzzle asset missing");
        const string flashMaterialPath="Assets/Resources/GunMuzzleFlash.mat";var flashMaterial=AssetDatabase.LoadAssetAtPath<Material>(flashMaterialPath);
        if(flashMaterial==null){flashMaterial=new Material(source.GetComponentInChildren<SpriteRenderer>().sharedMaterial);AssetDatabase.CreateAsset(flashMaterial,flashMaterialPath);}
        flashMaterial.SetColor("_TintColor",new Color(1,.86f,.55f,.5f));EditorUtility.SetDirty(flashMaterial);
        var instance=UnityEngine.Object.Instantiate(source);
        try{instance.name="EnemyMuzzleFlash";foreach(var sprite in instance.GetComponentsInChildren<SpriteRenderer>())sprite.sharedMaterial=flashMaterial;PrefabUtility.SaveAsPrefabAsset(instance,"Assets/Resources/EnemyMuzzleFlash.prefab");}finally{UnityEngine.Object.DestroyImmediate(instance);}
        AssetDatabase.SaveAssets();Directory.CreateDirectory("Logs/EnemyGunEffects");File.WriteAllText("Logs/EnemyGunEffects/Setup.txt","PASS reused Toon Muzzleflash 8 and Cartoon FX smoke-fume texture; shared materials configured.\n");
    }
}
