using System;
using System.Linq;
using CyberBiker.Player;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class SwordRuntimeClipBuild : IPreprocessBuildWithReport {
    private const string LibraryPath = "Assets/Resources/SwordRuntimeClips.asset";
    private const string ClipFolder = "Assets/Art/Animations/Sword/";
    private static readonly string[] Names = {
        "Player_Sword_Grab", "Player_Sword_Back", "Player_Sword_Idle", "Player_Sword_Idle_Left",
        "Player_Sword_Idle_Right", "Player_Sword_Attack_Left", "Player_Sword_Attack_Right"
    };
    public int callbackOrder => -100;
    public void OnPreprocessBuild(BuildReport report) { Prepare(); }

    [MenuItem("CyberBiker/Animations/Rebuild runtime sword curves")]
    public static void Prepare() {
        var sources = Names.Select(name => AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipFolder + name + ".anim")).ToArray();
        if (sources.Any(clip => clip == null || clip.humanMotion || clip.empty))
            throw new BuildFailedException("The transform-baked sword library is incomplete.");
        string digest = "raw-transform-v1:" + string.Join("|", Names.Select(name => AssetDatabase.GetAssetDependencyHash(ClipFolder + name + ".anim").ToString()));
        var library = AssetDatabase.LoadAssetAtPath<SwordRuntimeClips>(LibraryPath);
        if (library != null && library.SourceDigest == digest) {
            foreach (var source in sources) library.Resolve(source);
            return;
        }
        if (library == null) {
            library = ScriptableObject.CreateInstance<SwordRuntimeClips>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        // Only replace subassets owned by this generated library; preview clips stay Generic.
        foreach (var previous in AssetDatabase.LoadAllAssetsAtPath(LibraryPath).OfType<AnimationClip>())
            UnityEngine.Object.DestroyImmediate(previous, true);
        var serialized = new SerializedObject(library);
        var clips = serialized.FindProperty("clips"); clips.arraySize = sources.Length;
        for (int i = 0; i < sources.Length; i++) {
            var source = sources[i];
            var runtime = new AnimationClip { name = source.name, legacy = true, frameRate = source.frameRate, wrapMode = source.wrapMode };
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
                AnimationUtility.SetEditorCurve(runtime, binding, AnimationUtility.GetEditorCurve(source, binding));
            AnimationUtility.SetAnimationClipSettings(runtime, AnimationUtility.GetAnimationClipSettings(source));
            runtime.EnsureQuaternionContinuity();
            if (!runtime.legacy || runtime.empty || Mathf.Abs(runtime.length - source.length) > .0001f)
                throw new BuildFailedException("Invalid runtime sword clip: " + source.name);
            AssetDatabase.AddObjectToAsset(runtime, library);
            clips.GetArrayElementAtIndex(i).objectReferenceValue = runtime;
        }
        serialized.FindProperty("sourceDigest").stringValue = digest;
        serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log("Prepared seven runtime sword clips with raw transform curves; original preview clips are unchanged.");
    }
}
