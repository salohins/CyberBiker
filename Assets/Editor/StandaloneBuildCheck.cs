using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class StandaloneBuildCheck {
    public static void Run() {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Level/Scenes/Menu.unity", "Assets/Level/Scenes/Level.unity" },
            locationPathName = "Builds/Verification/CyberBiker.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Standalone build failed: " + report.summary.result);
    }
}
