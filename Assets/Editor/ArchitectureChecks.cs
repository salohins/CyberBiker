using System;
using System.IO;
using CyberBiker.Combat;
using CyberBiker.Runs;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArchitectureChecks {
    private static int assertions;
    private sealed class TestWeapon : Weapon {
        public int Shots { get; private set; }
        public TestWeapon() : base(1) { }
        protected override bool Fire(ShotRequest request) { Shots++; return true; }
    }
    private static void Check(bool condition, string message) {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    [MenuItem("CyberBiker/Verify architecture")]
    public static void Run() {
        var health = new Health(100);
        int deaths = 0, changes = 0;
        health.Died += () => deaths++;
        health.Changed += (current, maximum) => changes++;
        Check(!health.TakeDamage(-10) && health.Current == 100, "Negative damage must not heal");
        Check(health.TakeDamage(new DamageInfo(25)) && health.Current == 75, "Damage should reduce health");
        Check(health.Heal(1000) && health.Current == 100, "Healing should clamp to maximum");
        Check(health.TakeDamage(1000) && !health.IsAlive && health.Current == 0, "Lethal damage should clamp to zero");
        Check(!health.TakeDamage(1) && !health.Heal(10), "Dead targets must stay dead");
        Check(deaths == 1 && changes == 3, "Observers must receive changes and one death");
        Weapon weapon = new TestWeapon();
        int fired = 0; weapon.Fired += () => fired++;
        var request = new ShotRequest(Vector3.zero, Vector3.forward, null);
        Check(weapon.TryFire(request, 0), "First shot should fire");
        Check(!weapon.TryFire(request, 0.5f), "Cooldown should reject early shots");
        Check(weapon.TryFire(request, 1) && fired == 2, "Cooldown should permit next shot and broadcast it");
        Check(!weapon.TryFire(new ShotRequest(Vector3.zero, Vector3.zero, null), 2), "Zero direction must be rejected");
        weapon.SetInterval(0.1f);
        Check(weapon.TryFire(request, 1.2f), "Live interval tuning must affect the next eligible shot without resetting the weapon");
        var engine = new CyberBiker.Audio.BikeEngineMix();
        engine.Update(0, 100, 6, 0.1f, false);
        Check(engine.Gear == 1 && engine.EngineRpm >= 1600 && engine.EngineRpm <= 1700, "Stopped engine must idle");
        for (int i = 0; i < 1200; i++) engine.Update(Mathf.Min(100, i / 6f), 100, 6, 1f / 60, true);
        Check(engine.Gear == 6 && engine.EngineRpm > 13000 && engine.EngineRpm < 15501, "Drivetrain must shift through gears and settle below redline");
        float cruiseRpm = engine.EngineRpm;
        engine.Update(100, 100, 6, 0);
        Check(engine.EngineRpm == cruiseRpm, "Paused engine state must freeze");
        for (int i = 0; i < 180; i++) engine.Update(100 - i / 3f, 100, 6, 1f / 60, false);
        Check(engine.Load < 0.1f && engine.EngineRpm < cruiseRpm, "Engine braking must unload the recorded voice while RPM follows wheel speed");
        var session = new RunSession(GameState.Playing, 2);
        session.Tick(1, 100); session.ChangeState(GameState.Pause); session.Tick(10, 100);
        Check(session.Score == 1 && session.Elapsed == 1, "Pause must freeze scoring and duration");
        session.ChangeState(GameState.Playing); session.Tick(1, 200); session.Tick(1, 300);
        Check(session.SampleCount == 2 && session.GetSample(0).Score == 3 && session.GetSample(1).Score == 6,
            "Bounded struct history must preserve chronological samples");
        session.ChangeState(GameState.GameOver);
        Check(!session.ChangeState(GameState.Playing), "Death cannot be reversed by a delayed recovery");
        var firstObject = new GameObject("Street socket probe");
        var secondObject = new GameObject("Roof socket probe");
        var previous = firstObject.AddComponent<Tile>();
        var candidate = secondObject.AddComponent<Tile>();
        var serialized = new SerializedObject(candidate);
        serialized.FindProperty("entryType").enumValueIndex = (int)RouteSurface.Roof;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Check(!candidate.CanFollow(previous), "Roof entry must reject a street exit");
        serialized = new SerializedObject(previous);
        serialized.FindProperty("exitType").enumValueIndex = (int)RouteSurface.Roof;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Check(candidate.CanFollow(previous), "Roof entry must accept a roof exit");
        UnityEngine.Object.DestroyImmediate(firstObject);
        UnityEngine.Object.DestroyImmediate(secondObject);
        foreach (var path in new[] { "Assets/Level/Scenes/Menu.unity", "Assets/Level/Scenes/Level.unity" }) {
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var root in scene.GetRootGameObjects()) foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                    "Missing script: " + transform.name);
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/architecture-checks.txt", $"PASS: {assertions} assertions; health, weapon, run history, pause/death and scene script bindings.");
        Debug.Log($"Architecture checks passed: {assertions}");
    }
}
