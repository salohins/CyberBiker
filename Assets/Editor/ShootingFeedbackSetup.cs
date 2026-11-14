using System;
using System.IO;
using System.Text;
using CyberBiker.Player;
using CyberBiker.Audio;
using UnityEditor;
using UnityEngine;

public static class ShootingFeedbackSetup {
    private const string AudioRoot="Assets/Audio/CyberBiker/";
    public static void Configure(){
        Directory.CreateDirectory("Logs/ShootingFeedback");CreateCue("BulletAirSwish_Recorded.mp3","BulletWhiz.wav",.008f,.24f,.85f);CreateCue("PistolHit_Recorded.mp3","PistolHit.wav",.004f,.20f,.94f);
        var shader=Shader.Find("CyberBiker/Combat Damage Screen");if(shader==null || ShaderUtil.ShaderHasError(shader))throw new Exception("Combat screen shader must compile");
        var screen=Material("Assets/Resources/GunFeedbackScreen.mat",shader);
        var particles=Material("Assets/Resources/BulletGraze.mat",Shader.Find("CyberBiker/Tire Contact Spray"));particles.SetFloat("_Intensity",1);particles.SetFloat("_CoreWhiteness",0);EditorUtility.SetDirty(particles);
        var playerPath="Assets/Art/Models/Player_Last.prefab";var root=PrefabUtility.LoadPrefabContents(playerPath);
        try{var motorBefore=EditorJsonUtility.ToJson(root.GetComponent<PlayerController>());var swordBefore=EditorJsonUtility.ToJson(root.GetComponent<BikeSwordCombat>());
            var feedback=root.GetComponent<ShootingFeedback>() ?? root.AddComponent<ShootingFeedback>();var settings=new SerializedObject(feedback);settings.FindProperty("nearMissRadius").floatValue=3.2f;settings.FindProperty("hitMarkerSize").floatValue=14;settings.FindProperty("screenMaterial").objectReferenceValue=screen;settings.FindProperty("nearMissMaterial").objectReferenceValue=particles;settings.ApplyModifiedPropertiesWithoutUndo();
            if(motorBefore!=EditorJsonUtility.ToJson(root.GetComponent<PlayerController>()) || swordBefore!=EditorJsonUtility.ToJson(root.GetComponent<BikeSwordCombat>()))throw new Exception("Feedback installation changed movement or sword settings");
            PrefabUtility.SaveAsPrefabAsset(root,playerPath);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        var audioPath="Assets/Resources/CyberBikerAudio.prefab";root=PrefabUtility.LoadPrefabContents(audioPath);
        try{var settings=new SerializedObject(root.GetComponent<GameAudio>());Clip(settings,"pistolHitConfirm","PistolHit.wav");settings.FindProperty("hitConfirmVolume").floatValue=.8f;settings.FindProperty("bulletNearMissVolume").floatValue=.9f;Clip(settings,"playerDamageThud","impactPlate_heavy_000.ogg");Clip(settings,"bulletNearMiss","BulletWhiz.wav");settings.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,audioPath);}finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();File.WriteAllText("Logs/ShootingFeedback/Setup.txt","PASS: configured player recoil/hit/damage/near-miss presentation; movement and sword serialized settings preserved; shader compiled; downloaded CC0 air swish and impact trimmed and audio cues assigned.\n");
    }
    private static void Clip(SerializedObject settings,string field,string file){var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot+file);if(clip==null)throw new Exception("Missing feedback clip: "+file);settings.FindProperty(field).objectReferenceValue=clip;}
    private static Material Material(string path,Shader shader){if(shader==null)throw new Exception("Missing shader");var result=AssetDatabase.LoadAssetAtPath<Material>(path);if(result==null){result=new Material(shader){name=Path.GetFileNameWithoutExtension(path)};AssetDatabase.CreateAsset(result,path);}return result;}
    private static void CreateCue(string recording,string output,float preRoll,float duration,float peakLevel){
        string source=AudioRoot+recording;AssetDatabase.ImportAsset(source,ImportAssetOptions.ForceSynchronousImport);
        var importer=(AudioImporter)AssetImporter.GetAtPath(source);importer.forceToMono=true;importer.loadInBackground=false;var format=importer.defaultSampleSettings;format.loadType=AudioClipLoadType.DecompressOnLoad;format.preloadAudioData=true;importer.defaultSampleSettings=format;importer.SaveAndReimport();
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(source);clip.LoadAudioData();var samples=new float[clip.samples*clip.channels];if(!clip.GetData(samples,0))throw new Exception("Cannot decode downloaded cue");
        int peak=0;for(int i=1;i<samples.Length;i++)if(Mathf.Abs(samples[i])>Mathf.Abs(samples[peak]))peak=i;
        int onset=0;float threshold=Mathf.Abs(samples[peak])*.05f;while(onset<samples.Length-1 && Mathf.Abs(samples[onset])<threshold)onset++;
        int start=Mathf.Max(0,onset-(int)(clip.frequency*preRoll));int end=Mathf.Min(samples.Length,start+(int)(clip.frequency*duration));
        float localPeak=.001f;for(int i=start;i<end;i++)localPeak=Mathf.Max(localPeak,Mathf.Abs(samples[i]));float scale=peakLevel/localPeak;
        using(var file=new BinaryWriter(File.Create(AudioRoot+output))){int count=end-start;file.Write(Encoding.ASCII.GetBytes("RIFF"));file.Write(36+count*2);file.Write(Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)1);file.Write(clip.frequency);file.Write(clip.frequency*2);file.Write((short)2);file.Write((short)16);file.Write(Encoding.ASCII.GetBytes("data"));file.Write(count*2);
            for(int i=0;i<count;i++){float fade=Mathf.Min(1,Mathf.Min(i/(clip.frequency*.001f),(count-1-i)/(clip.frequency*.035f)));file.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[start+i]*scale*fade,-1,1)*32767));}}
        AssetDatabase.ImportAsset(AudioRoot+output,ImportAssetOptions.ForceSynchronousImport);
        var cueImporter=(AudioImporter)AssetImporter.GetAtPath(AudioRoot+output);var cueFormat=cueImporter.defaultSampleSettings;cueFormat.loadType=AudioClipLoadType.DecompressOnLoad;cueFormat.compressionFormat=AudioCompressionFormat.PCM;cueFormat.preloadAudioData=true;cueImporter.defaultSampleSettings=cueFormat;cueImporter.loadInBackground=false;cueImporter.SaveAndReimport();
    }
}
