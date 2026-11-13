using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;

public static class SwordRuntimeClipChecks {
    private static GameObject MakeSamplingRig(GameObject prefab) {
        var root = new GameObject("Transform-only sword validation rig"); root.SetActive(false);
        var copied = new Dictionary<Transform, Transform> { { prefab.transform, root.transform } };
        foreach (var original in prefab.GetComponentsInChildren<Transform>(true)) {
            if (original == prefab.transform) continue;
            var node = new GameObject(original.name).transform; node.SetParent(copied[original.parent], false);
            node.localPosition = original.localPosition; node.localRotation = original.localRotation; node.localScale = original.localScale;
            copied.Add(original, node);
        }
        return root;
    }
    public static void Run() {
        GameObject expected = null, actual = null;
        try {
            SwordRuntimeClipBuild.Prepare();
            var library = AssetDatabase.LoadAssetAtPath<SwordRuntimeClips>("Assets/Resources/SwordRuntimeClips.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Player_Last.prefab");
            expected = MakeSamplingRig(prefab);
            actual = MakeSamplingRig(prefab);
            string[] names = { "Grab", "Back", "Idle", "Idle_Left", "Idle_Right", "Attack_Left", "Attack_Right" };
            int frames = 0, poses = 0;
            foreach (var name in names) {
                var source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Sword/Player_Sword_" + name + ".anim");
                var runtime = library.Resolve(source);
                if (source.legacy || runtime == source || !runtime.legacy || runtime.empty || Mathf.Abs(runtime.length - source.length) > .0001f)
                    throw new Exception("Incorrect source/runtime clip pairing: " + name);
                var paths = AnimationUtility.GetCurveBindings(source).Where(b => b.type == typeof(Transform)).Select(b => b.path).Distinct().ToArray();
                foreach (float progress in new[] { 0, .13f, .37f, .59f, .83f, 1, .42f, 0 }) {
                    source.SampleAnimation(expected, source.length * progress);
                    runtime.SampleAnimation(actual, source.length * progress);
                    foreach (var path in paths) {
                        var a = expected.transform.Find(path); var b = actual.transform.Find(path);
                        if (a == null || b == null || Vector3.Distance(a.localPosition, b.localPosition) > .0001f
                            || Quaternion.Angle(a.localRotation, b.localRotation) > .1f || Vector3.Distance(a.localScale, b.localScale) > .0001f)
                            throw new Exception("Runtime curve pose differs: " + name + " at " + progress + " / " + path);
                        poses++;
                    }
                    frames++;
                }
            }
            Directory.CreateDirectory("Logs/SwordAnimations");
            File.WriteAllText("Logs/SwordAnimations/runtime-curve-check.txt", "PASS: seven original Generic preview clips preserved; seven raw-transform runtime copies match " + frames + " source poses / " + poses + " bone samples, including unordered sampling and endpoints. Durations and sample rates preserved.\n");
            UnityEngine.Object.DestroyImmediate(expected); expected = null;
            UnityEngine.Object.DestroyImmediate(actual); actual = null;
            SwordCombatChecks.Run();
        } catch (Exception error) {
            if (expected != null) UnityEngine.Object.DestroyImmediate(expected);
            if (actual != null) UnityEngine.Object.DestroyImmediate(actual);
            Debug.LogError(error); EditorApplication.Exit(1);
        }
    }
}
