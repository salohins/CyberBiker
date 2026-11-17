using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using CyberBiker.Player;
using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CyberBiker.UI {
    // A scene-owned overlay: no changes to authored canvases or prefab instances.
    public sealed partial class TestingSettingsMenu : MonoBehaviour {
        private static readonly Color Cyan = new Color(.18f, .9f, 1);
        private static readonly Color Ink = new Color(.035f, .055f, .085f, .98f);
        private readonly List<Action> refreshFields = new List<Action>();
        private GameplayManager gameplay;
        private PlayerController player;
        private BikeSwordCombat sword;
        private TestingSettings draft;
        private RunPlayMode draftMode;
        private DifficultyManager difficulties;
        private GameObject modal;
        private RectTransform safeRoot, modalSafe;
        private Button opener;
        private bool resumeOnClose, resetRequested;
        private bool calibrateRequested;
        private TMP_Text gyroStatus;
        private string lastGyroStatus;
        private Rect lastSafe;
        private Vector2Int lastSize;
        public bool IsOpen => modal != null && modal.activeSelf;
        public TestingSettings Draft => draft?.Copy();

        public static void Install(GameplayManager owner) {
            if (owner.GetComponentInChildren<TestingSettingsMenu>(true) != null) return;
            var obj = new GameObject("Test settings");
            obj.transform.SetParent(owner.transform, false);
            obj.AddComponent<TestingSettingsMenu>().gameplay = owner;
        }
        public static bool CloseOnBack(GameplayManager owner) {
            var menu = owner.GetComponentInChildren<TestingSettingsMenu>(true);
            if (menu == null || !menu.IsOpen) return false;
            menu.Close(); return true;
        }
        private IEnumerator Start() {
            // Player.Start captures scene defaults and applies saved settings first.
            yield return null;
            player = FindAnyObjectByType<PlayerController>();
            if (player == null || gameplay == null) { Destroy(gameObject); yield break; }
            sword = player.GetComponent<BikeSwordCombat>();
            difficulties=gameplay.GetComponent<DifficultyManager>();
            draft = TestingSettings.Read(player, gameplay, sword);
            draftMode=difficulties!=null ? difficulties.Mode : RunPlayMode.Progression;
            Build();
        }
        private void Update() {
            if (opener == null) return;
            opener.interactable = !gameplay.IsResuming && gameplay.gameState != GameState.Intro;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (lastSafe != Screen.safeArea || lastSize != size) UpdateSafeArea();
            if(IsOpen && gyroStatus!=null){string status=calibrateRequested ? "Neutral calibration queued. Hold the phone comfortably and press APPLY." : player.GetComponent<TouchInputManager>().GyroscopeStatus;if(status!=lastGyroStatus){gyroStatus.text=status;lastGyroStatus=status;}}
        }
        public void Open() {
            if (modal == null || gameplay.IsResuming || gameplay.gameState == GameState.Intro) return;
            draft = TestingSettings.Read(player, gameplay, sword);
            draftMode=difficulties!=null ? difficulties.Mode : RunPlayMode.Progression;
            resetRequested = false;
            calibrateRequested=false;
            resumeOnClose = gameplay.gameState == GameState.Playing || gameplay.gameState == GameState.Hit;
            if (resumeOnClose) gameplay.OpenPauseMenu();
            foreach (var refresh in refreshFields) refresh();
            modal.SetActive(true);
            opener.gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        public void Close() {
            if (!IsOpen) return;
            if(ClosePresetsIfOpen())return;
            modal.SetActive(false);
            opener.gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (resumeOnClose && gameplay.gameState == GameState.Pause) gameplay.ResumeGame();
        }
        public void ResetDraft() {
            if (TestingSettings.Defaults == null) return;
            draft = TestingSettings.Defaults.Copy(); resetRequested = true;
            calibrateRequested=false;
            foreach (var refresh in refreshFields) refresh();
        }
        public void Apply(bool restart) {
            if (!IsOpen) return;
            draft.Apply(player, gameplay, sword);
            if(calibrateRequested && draft.steeringMode==MobileSteeringMode.Gyroscope)player.GetComponent<TouchInputManager>().RequestGyroscopeCalibration();
            if (resetRequested) TestingSettings.ClearSaved(); else draft.Save();
            if(difficulties!=null && draftMode!=difficulties.Mode){RunModeSelection.Select(draftMode);restart=true;}
            if (restart) { modal.SetActive(false); gameplay.RestartLevel(); }
            else Close();
        }
        private void Build() {
            var canvasObject = Node("Testing canvas", transform);
            var canvas = canvasObject.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20000;
            var scaler = canvasObject.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            canvasObject.gameObject.AddComponent<GraphicRaycaster>();
            safeRoot = Node("Safe area", canvasObject);
            opener = MakeButton("TUNE", safeRoot, Cyan, Open);
            Place((RectTransform)opener.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -62), new Vector2(144, -14));

            var shade = Node("Settings overlay", canvasObject);
            shade.gameObject.AddComponent<Image>().color = new Color(0, 0, .025f, .78f);
            modal = shade.gameObject;
            modalSafe = Node("Safe area", shade);
            var panel = Node("Panel", modalSafe);
            Place(panel, new Vector2(.055f, .055f), new Vector2(.945f, .945f));
            panel.gameObject.AddComponent<Image>().color = Ink;
            var edge = panel.gameObject.AddComponent<Outline>(); edge.effectColor = new Color(.08f, .6f, .8f, .7f); edge.effectDistance = new Vector2(1, -1);
            var title = Label("TEST SETTINGS", panel, 30, Cyan);
            Place(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(28, -57), new Vector2(-28, -10));
            var hint = Label("Apply saves on this device. Reset restores original values. PRESETS exports/imports JSON.", panel, 18, new Color(.65f, .74f, .82f));
            Place(hint.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(28, -91), new Vector2(-28, -54));

            var viewport = Node("Settings viewport", panel);
            Place(viewport, Vector2.zero, Vector2.one, new Vector2(24, 92), new Vector2(-24, -151));
            viewport.gameObject.AddComponent<Image>().color = new Color(.025f, .04f, .065f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.viewport = viewport;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            var content = CreateSettingsPage("Ride settings",viewport);
            scroll.content = content;
            if(difficulties!=null){Section("PLAY MODE",content);PlayModeChoice(content);}
            Section("CONTROLS", content);
            SteeringChoice(content);
            Field("Swipe steering sensitivity", "Touch sensitivity", content, 0, 19, .1f, () => draft.touchSensitivity, v => draft.touchSensitivity = v);
            Field("Tilt sensitivity", "Gyro sensitivity", content, .25f, 3, .05f, () => draft.gyroSensitivity, v => draft.gyroSensitivity=v);
            Field("Tilt dead zone (degrees)", "Gyro dead zone", content, 0, 5, .1f, () => draft.gyroDeadZone, v => draft.gyroDeadZone=v);
            Field("Tilt smoothing (seconds)", "Gyro smoothing", content, 0, .4f, .01f, () => draft.gyroSmoothing, v => draft.gyroSmoothing=v);
            var invertRow=Node("Invert tilt",content);invertRow.gameObject.AddComponent<LayoutElement>().preferredHeight=60;
            var invert=MakeButton("",invertRow,new Color(.15f,.22f,.3f),()=>{draft.gyroInvert=!draft.gyroInvert;resetRequested=false;foreach(var refresh in refreshFields)refresh();});
            refreshFields.Add(()=>invert.GetComponentInChildren<TMP_Text>().text=draft.gyroInvert ? "INVERT TILT: ON" : "INVERT TILT: OFF");
            var calibration=Node("Calibrate tilt",content);calibration.gameObject.AddComponent<LayoutElement>().preferredHeight=60;
            MakeButton("CALIBRATE ON APPLY",calibration,new Color(.15f,.22f,.3f),()=>calibrateRequested=true);
            gyroStatus=Label("",content,18,new Color(.65f,.74f,.82f));gyroStatus.gameObject.AddComponent<LayoutElement>().preferredHeight=62;
            foreach(var refresh in refreshFields)refresh();
            Section("RIDING", content);
            Field("Normal speed limit (m/s)", "Max speed", content, 1, 200, 1, () => draft.maxSpeed, v => draft.maxSpeed = v);
            Field("Forward acceleration (m/s²)", "Acceleration", content, 0, 1000, 1, () => draft.acceleration, v => draft.acceleration = v);
            Field("Steering acceleration (m/s²)", "Steering acceleration", content, 0, 200, 1, () => draft.steeringAcceleration, v => draft.steeringAcceleration = v);
            Field("Sideways speed limit (m/s)", "Sideways speed", content, 0, 60, .1f, () => draft.steeringSpeed, v => draft.steeringSpeed = v);
            Section("LAUNCH & COMBAT", content);
            Field("Launch speed multiplier", "Launch multiplier", content, 1, 2, .01f, () => draft.launchMultiplier, v => draft.launchMultiplier = v);
            Field("Full Launch bar duration (real seconds)", "Launch duration", content, 1, 30, .1f, () => draft.launchDuration, v => draft.launchDuration = v);
            Field("Score to refill a full Launch bar", "Launch refill score", content, 1, 100000, 1, () => draft.launchRefillScore, v => draft.launchRefillScore = v);
            Field("Minimum charge to start Launch (%)", "Launch minimum charge", content, 1, 100, 1, () => draft.launchMinimumCharge, v => draft.launchMinimumCharge = v);
            Field("Launch charge at run start (%)", "Launch starting charge", content, 0, 100, 1, () => draft.launchStartingCharge, v => draft.launchStartingCharge = v);
            Field("Pistol damage per shot", "Pistol damage", content, 0, 200, 1, () => draft.pistolDamage, v => draft.pistolDamage = v);
            Field("Seconds between shots", "Shot interval", content, .05f, 3, .01f, () => draft.shotInterval, v => draft.shotInterval = v);
            Field("Enemy follow distance (metres)", "Enemy distance", content, 1, 150, 1, () => draft.enemyDistance, v => draft.enemyDistance = v);
            BuildBalanceTabs(panel,viewport,scroll,content);
            var buttons = Node("Actions", panel);
            Place(buttons, Vector2.zero, new Vector2(1, 0), new Vector2(24, 22), new Vector2(-24, 76));
            var reset = MakeButton("RESET", buttons, new Color(.15f, .22f, .3f), ResetDraft);
            var cancel = MakeButton("CANCEL", buttons, new Color(.15f, .22f, .3f), Close);
            var apply = MakeButton("APPLY", buttons, Cyan, () => Apply(false));
            var restart = MakeButton("APPLY + RESTART", buttons, new Color(.8f, .3f, 1), () => Apply(true));
            var actionButtons = new[] { reset, cancel, apply, restart };
            for (int i = 0; i < actionButtons.Length; i++)
                Place((RectTransform)actionButtons[i].transform, new Vector2(i * .25f, 0), new Vector2((i + 1) * .25f, 1), new Vector2(4, 0), new Vector2(-4, 0));
            UpdateSafeArea(); modal.SetActive(false);
        }
        private void PlayModeChoice(Transform parent){
            var row=Node("Play mode",parent);row.gameObject.AddComponent<LayoutElement>().preferredHeight=68;
            foreach(var mode in new[]{RunPlayMode.Progression,RunPlayMode.ConstantCombat}){
                var selected=mode;var button=MakeButton(mode==RunPlayMode.Progression ? "PROGRESSION" : "CONSTANT COMBAT",row,Ink,()=>{draftMode=selected;foreach(var refresh in refreshFields)refresh();});
                float left=mode==RunPlayMode.Progression ? 0 : .5f;Place((RectTransform)button.transform,new Vector2(left,.08f),new Vector2(left+.49f,.92f));
                refreshFields.Add(()=>{bool on=draftMode==selected;button.GetComponent<Image>().color=on ? Cyan : new Color(.15f,.22f,.3f);button.GetComponentInChildren<TMP_Text>().color=on ? Ink : Color.white;});
            }
            var hint=Label("Changing play mode restarts the run when you press APPLY.",parent,18,new Color(.65f,.74f,.82f));hint.gameObject.AddComponent<LayoutElement>().preferredHeight=38;
        }
        private void SteeringChoice(Transform parent){
            var row=Node("Steering mode",parent);row.gameObject.AddComponent<LayoutElement>().preferredHeight=72;
            row.gameObject.AddComponent<Image>().color=new Color(.06f,.085f,.125f);
            var label=Label("Steering mode",row,21,Color.white);Place(label.rectTransform,Vector2.zero,new Vector2(.37f,1),new Vector2(12,0),new Vector2(-8,0));
            foreach(var mode in new[]{MobileSteeringMode.Swipe,MobileSteeringMode.Gyroscope}){
                var selected=mode;var button=MakeButton(mode==MobileSteeringMode.Swipe ? "SWIPE" : "GYROSCOPE",row,Ink,()=>{draft.steeringMode=selected;resetRequested=false;foreach(var refresh in refreshFields)refresh();});
                float start=mode==MobileSteeringMode.Swipe ? .38f : .69f;Place((RectTransform)button.transform,new Vector2(start,.12f),new Vector2(start+.29f,.88f));
                refreshFields.Add(()=>{bool on=draft.steeringMode==selected;button.GetComponent<Image>().color=on ? Cyan : new Color(.15f,.22f,.3f);button.GetComponentInChildren<TMP_Text>().color=on ? Ink : Color.white;});
            }
        }
        private void UpdateSafeArea() {
            lastSafe = Screen.safeArea; lastSize = new Vector2Int(Screen.width, Screen.height);
            float width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            foreach (var root in new[] { safeRoot, modalSafe })
                if (root != null) Place(root, new Vector2(lastSafe.xMin / width, lastSafe.yMin / height), new Vector2(lastSafe.xMax / width, lastSafe.yMax / height));
        }
        private void Field(string caption, string name, Transform parent, float min, float max, float step, Func<float> get, Action<float> set) {
            var row = Node(name, parent); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 72;
            row.gameObject.AddComponent<Image>().color = new Color(.06f, .085f, .125f);
            var label = Label(caption, row, 21, Color.white);
            label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=21;
            Place(label.rectTransform, Vector2.zero, new Vector2(.39f, 1), new Vector2(12, 0), new Vector2(-8, 0));
            var sliderRect = Node("Slider", row);
            Place(sliderRect, new Vector2(.4f, .5f), new Vector2(.79f, .5f), new Vector2(0, -24), new Vector2(-16, 24));
            var slider = sliderRect.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var track = Node("Track", sliderRect); Place(track, new Vector2(0, .45f), new Vector2(1, .55f));
            track.gameObject.AddComponent<Image>().color = new Color(.18f, .25f, .34f);
            var fill = Node("Fill", track); fill.gameObject.AddComponent<Image>().color = Cyan; slider.fillRect = fill;
            var handleArea = Node("Handle area", sliderRect); Place(handleArea, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(10, -16), new Vector2(-10, 16));
            var handle = Node("Handle", handleArea); handle.sizeDelta = new Vector2(22, 0);
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Cyan;
            slider.handleRect = handle; slider.targetGraphic = handleImage;
            var inputRect = Node("Value", row);
            Place(inputRect, new Vector2(.8f, .5f), new Vector2(1, .5f), new Vector2(0, -24), new Vector2(-12, 24));
            var inputImage = inputRect.gameObject.AddComponent<Image>(); inputImage.color = new Color(.1f, .15f, .22f);
            var field = inputRect.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = inputImage; field.contentType = TMP_InputField.ContentType.DecimalNumber;
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            var text = Label("", inputRect, 23, Cyan); text.alignment = TextAlignmentOptions.Center;
            Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(5, 0), new Vector2(-5, 0));
            field.textViewport = text.rectTransform; field.textComponent = text;
            string format = step >= 1 ? "0" : step >= .1f ? "0.0" : step>=.01f ? "0.00" : "0.000";
            Action refresh = () => {
                slider.SetValueWithoutNotify(get());
                field.SetTextWithoutNotify(get().ToString(format, CultureInfo.InvariantCulture));
            };
            Action<float> change = value => {
                set(Mathf.Clamp(Mathf.Round(value / step) * step, min, max));
                resetRequested = false; refresh();
            };
            slider.onValueChanged.AddListener(v => change(v));
            field.onEndEdit.AddListener(value => {
                if (float.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    && !float.IsNaN(parsed) && !float.IsInfinity(parsed)) change(parsed);
                else refresh();
            });
            refreshFields.Add(refresh); refresh();
        }
        private static void Section(string name, Transform parent) {
            var text = Label(name, parent, 20, Cyan);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
        }
        private static Button MakeButton(string caption, Transform parent, Color color, Action action) {
            var rect = Node(caption, parent);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            var label = Label(caption, rect, 20, color == Cyan || color.r > .5f ? Ink : Color.white);
            label.alignment = TextAlignmentOptions.Center; label.fontStyle = FontStyles.Bold;
            return button;
        }
        private static TextMeshProUGUI Label(string text, Transform parent, float size, Color color) {
            var rect = Node("Label", parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text; label.fontSize = size; label.color = color;
            label.font = TMP_Settings.defaultFontAsset; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }
        private static RectTransform Node(string name, Transform parent) {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false); Place(rect, Vector2.zero, Vector2.one); return rect;
        }
        private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 lower = default, Vector2 upper = default) {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = lower; rect.offsetMax = upper;
        }
    }
}
