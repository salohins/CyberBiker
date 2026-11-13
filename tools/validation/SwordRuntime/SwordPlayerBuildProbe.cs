using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using CyberBiker.Player;

public static class SwordPlayerBuildProbe {
    public static void Run() {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Player_Last.prefab");
        var sword=new SerializedObject(prefab.GetComponent<BikeSwordCombat>());
        var names=new[]{"draw","sheathe","idle","idleLeft","idleRight","attackLeft","attackRight"};
        var original=names.Select(n=>(AnimationClip)sword.FindProperty(n).objectReferenceValue).ToArray();
        SwordRuntimeClipBuild.Prepare();
        var runtimeLibrary=AssetDatabase.LoadAssetAtPath<SwordRuntimeClips>("Assets/Resources/SwordRuntimeClips.asset");
        var clips=original.Select(runtimeLibrary.Resolve).ToArray();
        var boneProperty=sword.FindProperty("poseBones");var ancestors=new HashSet<Transform>();
        for(int i=0;i<boneProperty.arraySize;i++) {
            var bone=(Transform)boneProperty.GetArrayElementAtIndex(i).objectReferenceValue;
            while(bone!=prefab.transform){ancestors.Add(bone);bone=bone.parent;}
        }
        var bones=ancestors.OrderBy(t=>AnimationUtility.CalculateTransformPath(t,prefab.transform).Count(c=>c=='/'))
            .ThenBy(t=>AnimationUtility.CalculateTransformPath(t,prefab.transform),StringComparer.Ordinal)
            .Select(t=>new RuntimeSwordProbe.Bone{path=AnimationUtility.CalculateTransformPath(t,prefab.transform),position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var rig=RuntimeSwordProbe.MakeRig(bones,out var root);
        var frames=new List<RuntimeSwordProbe.Frame>();
        for(int c=0;c<clips.Length;c++)foreach(float progress in new[]{0,.13f,.37f,.59f,.83f,1,.42f,0}) {
            float time=original[c%original.Length].length*progress;original[c%original.Length].SampleAnimation(root,time);
            frames.Add(new RuntimeSwordProbe.Frame{clip=c,time=time,positions=rig.Select(t=>t.localPosition).ToArray(),rotations=rig.Select(t=>t.localRotation).ToArray(),scales=rig.Select(t=>t.localScale).ToArray()});
        }
        UnityEngine.Object.DestroyImmediate(root);
        var probe=new GameObject("Compiled sword pose probe").AddComponent<RuntimeSwordProbe>();
        probe.clips=clips;probe.bones=bones;probe.frames=frames.ToArray();
        const string path="Assets/RuntimeSwordProbe.unity";EditorSceneManager.SaveScene(scene,path);
        Directory.CreateDirectory("Builds/SwordProbe");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{path},locationPathName="Builds/SwordProbe/SwordProbe.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Sword probe build failed: "+report.summary.result);
    }
}
