using System;
using System.IO;
using CyberBiker.Audio;
using UnityEditor;
using UnityEngine;

public static class EngineAudioChecks {
    private static void Require(bool condition, string message) {
        if (!condition) throw new Exception(message);
    }

    public static void Run() {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/CyberBikerAudio.prefab");
        Require(prefab != null, "Audio prefab must exist");
        var preset = new SerializedObject(prefab.GetComponent<GameAudio>());
        var launch = preset.FindProperty("engineLaunch").objectReferenceValue as AudioClip;
        var cruise = preset.FindProperty("engineCruise").objectReferenceValue as AudioClip;
        Require(launch != null && cruise != null && launch.length > 1 && cruise.length > 1, "Launch and cruise recordings must be assigned");
        Require(launch.channels == 1 && cruise.channels == 1, "Both engine recordings must be mono for 3D positioning");

        // A longer equal-power transition finishes before the launch clip's baked fade.
        var handoff = new ArcadeEngineSoundState();
        bool HandoffTick(float dt) => handoff.Update(75, 75, dt, true, true, 8.2f, 2.5f, 0.25f, 4, 0.6f, 0.85f, 1.08f);
        HandoffTick(5.45f);
        Require(handoff.LaunchGain > 0.999f && handoff.CruiseGain < 0.001f, "Handoff must start at the configured time");
        HandoffTick(1.25f);
        Require(Mathf.Abs(handoff.LaunchGain - Mathf.Sqrt(0.5f)) < 0.001f && Mathf.Abs(handoff.CruiseGain - Mathf.Sqrt(0.5f)) < 0.001f,
            "At the handoff midpoint both unrelated clips must retain equal power");
        float launchGain = handoff.LaunchGain, cruiseGain = handoff.CruiseGain, handoffElapsed = handoff.LaunchElapsed;
        HandoffTick(0);
        Require(handoff.LaunchGain == launchGain && handoff.CruiseGain == cruiseGain && handoff.LaunchElapsed == handoffElapsed,
            "Pausing during a handoff must freeze its mix");
        HandoffTick(1.25f);
        Require(handoff.LaunchGain < 0.001f && handoff.CruiseGain > 0.999f && handoff.LaunchElapsed < 8.2f,
            "Handoff must complete before the launch clip ends");

        var model = new ArcadeEngineSoundState();
        bool Tick(float speed, float dt, bool playing = true, bool running = true) => model.Update(speed, 100, dt, playing, running, 4, 1, 0.25f, 4, 0.6f, 0.85f, 1.08f);
        Require(!Tick(0, 0.1f, false) && !model.HasStarted, "Engine launch must wait for gameplay");
        Require(Tick(100, 0.1f), "Engine launch must start when gameplay starts");
        float elapsed = model.LaunchElapsed, pitch = model.Pitch;
        Require(!Tick(0, 0) && model.LaunchElapsed == elapsed && model.Pitch == pitch, "Pause must freeze the engine transition and speed");
        Require(!Tick(100, 0.1f), "Resume must not restart the launch");
        for (int i = 0; i < 100; i++) Require(!Tick(100, 0.1f), "Launch must play only once per run");
        Require(model.CruiseGain > 0.99f && model.LaunchGain < 0.01f, "Engine must transition to sustained cruise");
        pitch = model.Pitch;
        for (int i = 0; i < 200; i++) {
            Tick(100, 0.1f);
            Require(Mathf.Abs(model.Pitch - pitch) < 0.0001f && model.CruiseGain > 0.99f, "Constant speed must not create cyclic shifts");
        }
        Tick(0, 0.3f);
        Require(model.Pitch < pitch, "Slowing down must reduce engine pitch");
        Tick(0, 0.1f, false, false);
        Require(model.CruiseGain == 0 && model.LaunchGain == 0, "Game over must fade the engine out");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/engine-audio-checks.txt", "PASS: mono engine recordings, one launch per run, pause/resume, equal-power crossfade ending before the clip tail, steady-speed pitch, deceleration and game-over transition.");
    }
}
