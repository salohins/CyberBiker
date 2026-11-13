using System;
using System.IO;
using System.Linq;
using CyberBiker.Player;
using CyberBiker.UI;
using Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class TestingSettingsChecks {
    private const string Pending = "CyberBiker.TestingSettingsChecks";
    private static int stage;
    private static double began, changedAt;
    private static GameplayManager gameplay;
    private static PlayerController player;
    private static TestingSettingsMenu menu;
    private static PlayerController previousPlayer;
    private static bool finishing;
    private static float originalSpeed;
    static TestingSettingsChecks() {
        if (SessionState.GetBool(Pending, false)) EditorApplication.delayCall += Attach;
    }
    public static void Run() {
        SessionState.SetBool(Pending + ".HadPrefs", PlayerPrefs.HasKey(TestingSettings.PreferenceKey));
        SessionState.SetString(Pending + ".Prefs", PlayerPrefs.GetString(TestingSettings.PreferenceKey, ""));
        PlayerPrefs.DeleteKey(TestingSettings.PreferenceKey);
        Screen.SetResolution(1280, 720, false);
        EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    private static void Attach() {
        began = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }
    private static void OnLog(string message, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message + "\n" + stack);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Near(float actual, float expected, string name) { Require(Mathf.Abs(actual - expected) < .02f, name + ": " + actual + " expected " + expected); }
    private static void Enter(string row, string value) {
        var field = menu.GetComponentsInChildren<TMP_InputField>(true).Single(f => f.transform.parent.name == row);
        field.text = value; field.onEndEdit.Invoke(value);
    }
    private static void Next() { stage++; changedAt = EditorApplication.timeSinceStartup; }
    private static void Capture(string name) {
        var canvas = menu.GetComponentInChildren<Canvas>(true);
        var camera = Camera.main;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var uiObject = new GameObject("Settings capture camera") { hideFlags = HideFlags.HideAndDontSave };
        var uiCamera = uiObject.AddComponent<Camera>();
        uiCamera.orthographic = true; uiCamera.clearFlags = CameraClearFlags.Depth;
        uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
        uiCamera.nearClipPlane = .01f; uiCamera.farClipPlane = 10; uiCamera.targetTexture = target;
        try {
            camera.targetTexture = target;
            canvas.enabled = false; camera.Render(); canvas.enabled = true;
            // Render UI after the scene's post effects, matching ScreenSpaceOverlay.
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = uiCamera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            menu.GetComponentInChildren<ScrollRect>(true).verticalNormalizedPosition = name == "combat" ? 0 : 1;
            Canvas.ForceUpdateCanvases(); uiCamera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes("Logs/SettingsMenu/" + name + ".png", pixels.EncodeToPNG());
        } finally {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            uiCamera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(uiObject);
        }
    }
    private static void Tick() {
        if (!EditorApplication.isPlaying || finishing) return;
        try {
            double now = EditorApplication.timeSinceStartup;
            if (now - began > 100) throw new Exception("Settings test timed out at stage " + stage);
            if (stage == 0) {
                player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                gameplay = UnityEngine.Object.FindAnyObjectByType<GameplayManager>();
                menu = UnityEngine.Object.FindAnyObjectByType<TestingSettingsMenu>();
                if (player == null || player.Health == null || menu == null || menu.Draft == null || Time.timeSinceLevelLoad < .5f) return;
                Near(player.ConfiguredMaxSpeed, TestingSettings.Defaults.maxSpeed, "Untouched scene speed");
                originalSpeed = player.ConfiguredMaxSpeed;
                gameplay.SwitchGameState(GameState.Playing);
                var body = player.GetComponent<Rigidbody>(); body.useGravity = false;
                body.position = new Vector3(0, 1000, body.position.z); body.linearVelocity = Vector3.zero;
                menu.Open(); Require(menu.IsOpen && Time.timeScale == 0, "Opening settings must pause riding");
                Require(menu.GetComponentsInChildren<TMP_InputField>(true).Length == 10, "Expected ten tuning fields");
                Enter("Max speed", "100"); Enter("Acceleration", "450");
                Enter("Steering acceleration", "70"); Enter("Sideways speed", "25");
                Enter("Launch multiplier", "1.70"); Enter("Launch duration", "10");
                Enter("Pistol damage", "22"); Enter("Shot interval", "0.65"); Enter("Enemy distance", "35");
                Enter("Touch sensitivity", "14.5");
                Near(player.ConfiguredMaxSpeed, TestingSettings.Defaults.maxSpeed, "Draft must not apply before button press");
                Require(!PlayerPrefs.HasKey(TestingSettings.PreferenceKey), "Editing draft must not save preferences");
                Next();
            } else if (stage == 1 && now - changedAt > .4) {
                Canvas.ForceUpdateCanvases();
                var scroll = menu.GetComponentInChildren<ScrollRect>(true);
                Require(scroll.content.rect.height > scroll.viewport.rect.height, "Long settings list must scroll");
                Require(scroll.viewport.rect.height > 150, "Settings viewport is too short");
                Directory.CreateDirectory("Logs/SettingsMenu");
                Capture("riding");
                Next();
            } else if (stage == 2 && now - changedAt > .5) {
                menu.GetComponentInChildren<ScrollRect>(true).verticalNormalizedPosition = 0;
                Next();
            } else if (stage == 3 && now - changedAt > .4) {
                Capture("combat");
                Next();
            } else if (stage == 4 && now - changedAt > .5) {
                menu.Apply(false);
                Near(player.NormalSpeedLimit, 100, "Live speed cap"); Near(player.ForwardAcceleration, 450, "Live acceleration");
                Near(player.SteeringAcceleration, 70, "Live steering acceleration"); Near(player.SteeringSpeedLimit, 25, "Live sideways limit");
                Near(gameplay.PlayerPistolDamage, 22, "Live damage"); Near(gameplay.PlayerPistolShotInterval, .65f, "Live interval");
                Near(gameplay.EnemyDistanceFromPlayer, 35, "Global distance");
                Near(player.GetComponent<TouchInputManager>().TouchSteeringSensitivity, 14.5f, "Live touch sensitivity");
                Near(player.GetComponent<BikeSwordCombat>().TestingSpeedMultiplier, 1.7f, "Launch multiplier");
                Near(player.GetComponent<BikeSwordCombat>().TestingLaunchDuration, 10, "Launch duration");
                Require(!menu.IsOpen && gameplay.IsResuming && Time.timeScale == 0, "Apply should use existing resume countdown");
                Require(PlayerPrefs.HasKey(TestingSettings.PreferenceKey), "Applied values must persist");
                Next();
            } else if (stage == 5 && !gameplay.IsResuming && now - changedAt > 3.3) {
                Require(gameplay.gameState == GameState.Playing, "Riding must resume");
                Near(player.GetComponent<Rigidbody>().maxLinearVelocity, 100, "Motor must use live speed limit");
                menu.Open(); Enter("Max speed", "50"); menu.Close();
                Near(player.NormalSpeedLimit, 100, "Cancel must discard draft");
                Next();
            } else if (stage == 6 && !gameplay.IsResuming) {
                menu.Open(); previousPlayer = player; menu.Apply(true); Next();
            } else if (stage == 7) {
                var fresh = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                if (fresh == null || fresh == previousPlayer || fresh.Health == null) return;
                player = fresh; gameplay = UnityEngine.Object.FindAnyObjectByType<GameplayManager>();
                menu = UnityEngine.Object.FindAnyObjectByType<TestingSettingsMenu>();
                if (menu == null || menu.Draft == null) return;
                Near(player.NormalSpeedLimit, 100, "Saved speed on restart"); Near(gameplay.EnemyDistanceFromPlayer, 35, "Saved global distance");
                Near(gameplay.PlayerPistolDamage, 22, "Saved damage");
                Near(TestingSettings.Defaults.maxSpeed, originalSpeed, "Restart must retain original defaults");
                Near(player.GetComponent<TouchInputManager>().TouchSteeringSensitivity, 14.5f, "Saved touch sensitivity");
                gameplay.SwitchGameState(GameState.Pause);
                menu.Open(); menu.ResetDraft(); menu.Apply(false);
                Near(player.NormalSpeedLimit, originalSpeed, "Reset restores scene speed");
                Near(player.GetComponent<TouchInputManager>().TouchSteeringSensitivity, TestingSettings.Defaults.touchSensitivity, "Reset touch sensitivity");
                Near(gameplay.EnemyDistanceFromPlayer, TestingSettings.Defaults.enemyDistance, "Reset restores global distance");
                Require(!PlayerPrefs.HasKey(TestingSettings.PreferenceKey), "Reset must remove saved overrides");
                Require(gameplay.gameState == GameState.Pause && !gameplay.IsResuming, "Settings opened from pause must remain paused");
                Finish(true, "PASS: original defaults, ten touch-friendly numeric/slider controls, scroll layout, unit conversion, draft/cancel, live motor speed/acceleration/steering, swipe sensitivity, global enemy distance, pistol damage/fire interval, launch tuning, pause/countdown, saved settings across restart, reset to authored defaults and prior-pause preservation. Native overlay-equivalent menu screenshots captured.");
            }
        } catch (Exception exception) { Finish(false, exception.ToString()); }
    }
    private static void Finish(bool success, string message) {
        if (finishing) return; finishing = true;
        EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
        SessionState.SetBool(Pending, false);
        if (SessionState.GetBool(Pending + ".HadPrefs", false)) PlayerPrefs.SetString(TestingSettings.PreferenceKey, SessionState.GetString(Pending + ".Prefs", ""));
        else PlayerPrefs.DeleteKey(TestingSettings.PreferenceKey);
        PlayerPrefs.Save();
        Directory.CreateDirectory("Logs/SettingsMenu"); File.WriteAllText("Logs/SettingsMenu/result.txt", message);
        Debug.Log(message);
        EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
    }
}
