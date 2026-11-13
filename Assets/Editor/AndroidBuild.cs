using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuild {
    // Run on a separate project copy when the main Editor is open.
    public static void Run() {
        var args = Environment.GetCommandLineArgs();
        var outputFlag = Array.IndexOf(args, "-androidOutput");
        var output = Path.GetFullPath(outputFlag >= 0 && outputFlag + 1 < args.Length
            ? args[outputFlag + 1] : "Builds/Android/CyberBiker.apk");
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path).ToArray();
        if (scenes.Length == 0) throw new InvalidOperationException("No enabled build scenes.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new InvalidOperationException("Install Android Build Support for this Unity Editor.");

        Directory.CreateDirectory(Path.GetDirectoryName(output));
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.buildApkPerCpuArchitecture = false;
        // Local test APK; no private release-signing credentials are needed.
        PlayerSettings.Android.useCustomKeystore = false;
        var versionFlag = Array.IndexOf(args, "-androidVersionCode");
        if (versionFlag >= 0) {
            if (versionFlag + 1 >= args.Length || !int.TryParse(args[versionFlag + 1], out var versionCode) || versionCode < 1)
                throw new ArgumentException("androidVersionCode must be a positive integer.");
            PlayerSettings.Android.bundleVersionCode = versionCode;
        }
        var identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        if (string.IsNullOrWhiteSpace(identifier))
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.cyberbiker.game");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });
        var summary = report.summary;
        var text = $"Result: {summary.result}\nUnity: {Application.unityVersion}\n" +
            $"Output: {output}\nScenes: {string.Join(", ", scenes)}\n" +
            $"Backend: IL2CPP\nArchitectures: ARMv7, ARM64\nVersion code: {PlayerSettings.Android.bundleVersionCode}\n" +
            $"Size: {summary.totalSize} bytes\nDuration: {summary.totalTime}\n" +
            $"Errors: {summary.totalErrors}\nWarnings: {summary.totalWarnings}\n";
        File.WriteAllText(Path.ChangeExtension(output, ".build.txt"), text);
        Debug.Log(text);
        if (summary.result != BuildResult.Succeeded)
            throw new Exception("Android build failed: " + summary.result);
    }
}
