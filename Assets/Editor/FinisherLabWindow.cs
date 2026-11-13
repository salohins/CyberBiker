using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CyberBiker.EditorTools {
    public sealed class FinisherLabWindow : EditorWindow {
        private FinisherLabTuning tuning;
        private Vector2 scroll;
        private string notice="";
        private readonly System.Collections.Generic.Dictionary<string,bool> expanded=new System.Collections.Generic.Dictionary<string,bool>();
        private static string PreferenceKey=>"CyberBiker.FinisherLab.Draft."+Application.dataPath;
        [MenuItem("CyberBiker/Finisher Lab")]
        public static void Open() {
            var window=GetWindow<FinisherLabWindow>("Finisher Lab");window.minSize=new Vector2(1000,600);window.Show();
        }
        private void OnEnable() {
            autoRepaintOnSceneChange=true;EditorApplication.update+=Refresh;
            try {
                tuning=EditorPrefs.HasKey(PreferenceKey) ? JsonUtility.FromJson<FinisherLabTuning>(EditorPrefs.GetString(PreferenceKey)) : FinisherLabTuning.ReadSavedDefaults();
                tuning.Normalize();
            } catch {tuning=null;}
        }
        private void OnDisable(){EditorApplication.update-=Refresh;}
        private void Refresh(){if(FinisherLabPreview.Active)Repaint();}
        private void Remember() {
            tuning.Normalize();EditorPrefs.SetString(PreferenceKey,JsonUtility.ToJson(tuning));FinisherLabPreview.Apply(tuning);
        }
        public void UseTuning(FinisherLabTuning values) {tuning=values.Copy();Remember();Repaint();}
        private void OnGUI() {
            if(tuning==null) {
                EditorGUILayout.HelpBox("Load the saved player defaults to start tuning.",MessageType.Info);
                if(GUILayout.Button("Load saved defaults"))tuning=FinisherLabTuning.ReadSavedDefaults();return;
            }
            EditorGUILayout.Space(8);EditorGUILayout.LabelField("FINISHER LAB",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Real animations, particles, camera and sound. Changes apply to the preview as you edit.",EditorStyles.miniLabel);
            DrawToolbar();
            DrawPresets();
            using(new EditorGUILayout.HorizontalScope()) {
                var slow=tuning.Get("slowMotionEnabled");EditorGUI.BeginChangeCheck();
                bool enabled=EditorGUILayout.ToggleLeft(new GUIContent("World slow motion","Turning this off immediately cancels finisher world slow motion. Animation speeds and pose hold are separate."),slow.number>=.5f,GUILayout.Width(155));
                if(EditorGUI.EndChangeCheck()){slow.number=enabled ? 1 : 0;Remember();}
                if(GUILayout.Button(new GUIContent("Animation 1×","Set draw, swing and sheathe to authored playback speed. Keeps your transition settings."),GUILayout.Width(115))) {
                    foreach(string field in new[]{"drawPlaybackSpeed","strikePlaybackSpeed","sliceReturnPlaybackSpeed"})tuning.Get(field).number=1;Remember();
                }
                float world=FinisherLabPreview.ViewingTake ? FinisherLabPreview.Take.SelectedWorldSpeed : Time.timeScale;
                EditorGUILayout.LabelField("Draft swing "+tuning.Get("strikePlaybackSpeed").number.ToString("0.##")+"×  ·  "+(FinisherLabPreview.ViewingTake ? "Recorded world " : "Live world ")+world.ToString("0.##")+"×  ·  Save for game keeps edits",EditorStyles.miniLabel);
            }
            if(GUILayout.Button("Inspect source clips — scrub, orbit and compare pose adjustment"))SwordClipInspector.Open();
            // Use the same budget during Layout and Repaint. GetLastRect is only
            // meaningful on Repaint and previously pushed the footer off-screen.
            float bodyHeight=Mathf.Max(220,position.height-200);
            using(new EditorGUILayout.HorizontalScope(GUILayout.Height(bodyHeight))) {
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(390))) {
                    scroll=EditorGUILayout.BeginScrollView(scroll);
                    float labelWidth=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=210;
                    foreach(string group in FinisherLabTuning.Controls.Select(control=>control.group).Distinct()) {
                        if(!expanded.ContainsKey(group))expanded[group]=group=="Animation";
                        expanded[group]=EditorGUILayout.Foldout(expanded[group],group,true,EditorStyles.foldoutHeader);
                        if(!expanded[group])continue;
                        using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                            foreach(var control in FinisherLabTuning.Controls.Where(control=>control.group==group)) {
                                var value=tuning.Get(control.field);EditorGUI.BeginChangeCheck();
                                var label=new GUIContent(control.label,control.hint);
                                if(control.color)value.color=EditorGUILayout.ColorField(label,value.color,true,true,true);
                                else if(control.toggle)value.number=EditorGUILayout.Toggle(label,value.number>=.5f) ? 1 : 0;
                                else value.number=EditorGUILayout.Slider(label,value.number,control.min,control.max);
                                if(EditorGUI.EndChangeCheck())Remember();
                            }
                        }
                        EditorGUILayout.Space(5);
                    }
                    EditorGUILayout.EndScrollView();
                    EditorGUIUtility.labelWidth=labelWidth;
                }
                using(new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true))) {
                    float imageHeight=Mathf.Max(80,Mathf.Min((position.width-410)*9/16,bodyHeight-(FinisherLabPreview.Take!=null ? 345 : 190)));
                    Rect image=GUILayoutUtility.GetRect(100,imageHeight,GUILayout.ExpandWidth(true),GUILayout.Height(imageHeight));
                    EditorGUI.DrawRect(image,new Color(.025f,.04f,.065f));
                    if(FinisherLabPreview.View!=null)EditorGUI.DrawPreviewTexture(image,FinisherLabPreview.View,null,ScaleMode.ScaleToFit);
                    else GUI.Label(image,"Start preview to replay a complete finisher",new GUIStyle(EditorStyles.centeredGreyMiniLabel){fontSize=16});
                    DrawTimeline();
                    DrawTake(image);
                    EditorGUILayout.HelpBox(FinisherLabPreview.Status,MessageType.None);
                    EditorGUILayout.LabelField("Replay tests your edits on a fresh target. Record full 3D take enables backward scrubbing and orbit inspection.",EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.Space(6);
                    if(EditorApplication.isPlaying && !FinisherLabPreview.Active)
                        EditorGUILayout.HelpBox("Stop the current Play session, then Start preview. The Lab uses a separate Play run and restores your open editor scenes when stopped.",MessageType.Info);
                    else EditorGUILayout.LabelField("Save for game keeps tuning in the player prefab for future runs/builds. Export preset saves a separate JSON backup.",EditorStyles.wordWrappedMiniLabel);
                    if(!string.IsNullOrEmpty(notice))EditorGUILayout.HelpBox(notice,MessageType.Info);
                }
            }
        }
        private void DrawToolbar() {
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if(GUILayout.Button("Start preview",EditorStyles.toolbarButton,GUILayout.Width(100))){Remember();FinisherLabPreview.Start(tuning);}
                using(new EditorGUI.DisabledScope(!FinisherLabPreview.Active)) {
                    if(GUILayout.Button("Stop",EditorStyles.toolbarButton,GUILayout.Width(48)))FinisherLabPreview.Stop();
                    if(GUILayout.Button("Replay",EditorStyles.toolbarButton,GUILayout.Width(60))){Remember();FinisherLabPreview.Replay();}
                    bool paused=FinisherLabPreview.ViewingTake ? !FinisherLabPreview.TakePlaying : EditorApplication.isPaused;
                    if(GUILayout.Button(paused ? "Play" : "Pause",EditorStyles.toolbarButton,GUILayout.Width(52))) {
                        if(FinisherLabPreview.ViewingTake)FinisherLabPreview.ToggleTakePlayback();else EditorApplication.isPaused=!EditorApplication.isPaused;
                    }
                    if(GUILayout.Button("Step",EditorStyles.toolbarButton,GUILayout.Width(45))) {
                        if(FinisherLabPreview.ViewingTake)FinisherLabPreview.StepTake(1);else FinisherLabPreview.Step();
                    }
                }
                GUILayout.Space(8);FinisherLabPreview.Left=GUILayout.Toolbar(FinisherLabPreview.Left ? 0 : 1,new[]{"Left","Right"},EditorStyles.toolbarButton,GUILayout.Width(95))==0;
                FinisherLabPreview.Loop=GUILayout.Toggle(FinisherLabPreview.Loop,"Loop",EditorStyles.toolbarButton,GUILayout.Width(45));
                FinisherLabPreview.PauseOnContact=GUILayout.Toggle(FinisherLabPreview.PauseOnContact,"Pause on contact",EditorStyles.toolbarButton,GUILayout.Width(110));
                FinisherLabPreview.Sound=GUILayout.Toggle(FinisherLabPreview.Sound,"Sound (live)",EditorStyles.toolbarButton,GUILayout.Width(80));
                GUILayout.FlexibleSpace();
            }
            using(new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField("Preview ride speed",GUILayout.Width(130));FinisherLabPreview.RideSpeed=EditorGUILayout.Slider(FinisherLabPreview.RideSpeed,20,150,GUILayout.MaxWidth(310));
                EditorGUILayout.LabelField("m/s · applies on Replay",EditorStyles.miniLabel);GUILayout.FlexibleSpace();
            }
            using(new EditorGUILayout.HorizontalScope()) {
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode && !FinisherLabPreview.Active))
                    if(GUILayout.Button("Record full 3D take",GUILayout.Width(150))){Remember();FinisherLabPreview.RecordTake(tuning);}
                EditorGUILayout.LabelField("Minimum length",GUILayout.Width(95));FinisherLabPreview.CaptureLength=EditorGUILayout.Slider(FinisherLabPreview.CaptureLength,3,20,GUILayout.MaxWidth(240));
                EditorGUILayout.LabelField("seconds · extends through recovery, up to 20 s",EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }
        private void DrawTimeline() {
            string current=FinisherLabPreview.ViewingTake ? FinisherLabPreview.Take.SelectedPhase : FinisherLabPreview.Ready ? FinisherLabPreview.Sword.Phase.ToString() : "";
            using(new EditorGUILayout.HorizontalScope())foreach(string phase in new[]{"Drawing","Striking","Sheathing","Recovering"}) {
                var previous=GUI.backgroundColor;if(current==phase)GUI.backgroundColor=new Color(.2f,.85f,1);
                GUILayout.Label(phase,EditorStyles.helpBox,GUILayout.ExpandWidth(true));GUI.backgroundColor=previous;
            }
            var rect=EditorGUILayout.GetControlRect(false,20);
            float progress=FinisherLabPreview.ViewingTake ? FinisherLabPreview.Take.SelectedProgress : FinisherLabPreview.PhaseProgress;
            EditorGUI.ProgressBar(rect,progress,FinisherLabPreview.Ready ? current+" · "+Mathf.RoundToInt(progress*100)+"%" : "Waiting for preview");
            if(FinisherLabPreview.Ready)EditorGUILayout.LabelField("World speed "+Time.timeScale.ToString("0.00")+"×   |   Hits "+FinisherLabPreview.Sword.HitCount+"   |   Impact particles "+FinisherLabPreview.Effects.LastImpactParticleCount,EditorStyles.miniLabel);
        }
        private void DrawTake(Rect image) {
            var take=FinisherLabPreview.Take;
            if(take==null || take.FrameCount<2)return;
            using(new EditorGUI.DisabledScope(FinisherLabPreview.Recording)) {
                EditorGUI.BeginChangeCheck();
                float time=EditorGUILayout.Slider("Whole finisher (s)",take.SelectedTime,0,take.Duration);
                if(EditorGUI.EndChangeCheck())FinisherLabPreview.SeekTake(time);
                using(new EditorGUILayout.HorizontalScope()) {
                    if(GUILayout.Button("◀ frame",GUILayout.Width(65)))FinisherLabPreview.StepTake(-1);
                    if(GUILayout.Button("frame ▶",GUILayout.Width(65)))FinisherLabPreview.StepTake(1);
                    if(GUILayout.Button("View take",GUILayout.Width(75)))FinisherLabPreview.SeekTake(take.SelectedTime);
                    if(GUILayout.Button("Live view",GUILayout.Width(75)))FinisherLabPreview.ShowLive();
                    EditorGUILayout.LabelField("Take playback",EditorStyles.miniLabel,GUILayout.Width(85));
                    FinisherLabPreview.TakeSpeed=EditorGUILayout.Slider(FinisherLabPreview.TakeSpeed,.1f,2,GUILayout.MaxWidth(130));
                }
                using(new EditorGUILayout.HorizontalScope()) {
                    if(GUILayout.Button("Game camera"))take.GameplayCamera();
                    foreach(var angle in new[]{("Front",180f),("Left",90f),("Right",-90f),("Back",0f)})if(GUILayout.Button(angle.Item1))take.CameraAngle(angle.Item2);
                }
                using(new EditorGUILayout.HorizontalScope()) {
                    foreach(var phase in new[]{CyberBiker.Player.BikeSwordCombat.CombatPhase.Drawing,CyberBiker.Player.BikeSwordCombat.CombatPhase.Striking,CyberBiker.Player.BikeSwordCombat.CombatPhase.Sheathing,CyberBiker.Player.BikeSwordCombat.CombatPhase.Recovering}) {
                        float marker=take.FirstPhaseTime(phase);using(new EditorGUI.DisabledScope(marker<0))if(GUILayout.Button(phase.ToString(),EditorStyles.miniButton))FinisherLabPreview.SeekTake(marker);
                    }
                    using(new EditorGUI.DisabledScope(take.ImpactTime<0))if(GUILayout.Button("Hit",EditorStyles.miniButton))FinisherLabPreview.SeekTake(take.ImpactTime);
                    using(new EditorGUI.DisabledScope(take.ExplosionTime<0))if(GUILayout.Button("Explosion",EditorStyles.miniButton))FinisherLabPreview.SeekTake(take.ExplosionTime);
                }
            }
            EditorGUILayout.LabelField("Take: "+take.FrameCount+" frames · ~"+(take.EstimatedBytes/(1024f*1024)).ToString("0.0")+" MB · "+take.SelectedParticleCount+" particles at this frame",EditorStyles.miniLabel);
            if(FinisherLabPreview.TakeDirty)EditorGUILayout.HelpBox("Tuning changed. Record a new take to include your edits; the current take preserves its original result.",MessageType.Warning);
            EditorGUILayout.LabelField("In the take: drag to orbit, middle-drag to pan, scroll to zoom. Scrubbing/playback are silent; live Replay includes sound.",EditorStyles.wordWrappedMiniLabel);
            if(!FinisherLabPreview.ViewingTake || FinisherLabPreview.Recording)return;
            var evt=Event.current;int id=GUIUtility.GetControlID(FocusType.Passive,image);
            if(evt.type==EventType.MouseDown && image.Contains(evt.mousePosition) && (evt.button==0 || evt.button==2)){GUIUtility.hotControl=id;evt.Use();}
            else if(evt.type==EventType.MouseDrag && GUIUtility.hotControl==id) {
                if(evt.button==2)take.Pan(-evt.delta.x*.012f,evt.delta.y*.012f);else take.Orbit(evt.delta.x*.6f,-evt.delta.y*.5f);evt.Use();Repaint();
            } else if(evt.type==EventType.MouseUp && GUIUtility.hotControl==id){GUIUtility.hotControl=0;evt.Use();}
            else if(evt.type==EventType.ScrollWheel && image.Contains(evt.mousePosition)){take.Zoom(1+evt.delta.y*.05f);evt.Use();Repaint();}
        }
        private void DrawPresets() {
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                if(GUILayout.Button("Save for game",EditorStyles.toolbarButton,GUILayout.Width(115))) {
                    try {tuning.SaveForGame();Remember();notice="Saved finisher tuning to Player_Last.prefab. Future runs and builds use these values.";}
                    catch(Exception error){notice=error.Message;Debug.LogException(error);}
                }
                if(GUILayout.Button("Reload saved defaults",EditorStyles.toolbarButton,GUILayout.Width(150))){tuning=FinisherLabTuning.ReadSavedDefaults();Remember();notice="Reloaded the player's saved tuning.";}
                if(GUILayout.Button("Export preset…",EditorStyles.toolbarButton,GUILayout.Width(115))) {
                    string path=EditorUtility.SaveFilePanel("Export finisher preset",Application.dataPath,"FinisherPreset","json");
                    if(!string.IsNullOrEmpty(path))File.WriteAllText(path,JsonUtility.ToJson(tuning,true));
                }
                if(GUILayout.Button("Import preset…",EditorStyles.toolbarButton,GUILayout.Width(115))) {
                    string path=EditorUtility.OpenFilePanel("Import finisher preset",Application.dataPath,"json");
                    if(!string.IsNullOrEmpty(path))try {
                        var imported=JsonUtility.FromJson<FinisherLabTuning>(File.ReadAllText(path));
                        if(imported==null || imported.version!=1)throw new ArgumentException("Unsupported finisher preset.");
                        imported.Normalize();tuning=imported;Remember();notice="Preset loaded into the preview.";
                    } catch(Exception error){notice="Preset was not loaded: "+error.Message;}
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label("Save keeps changes in the game · Export creates a preset file",EditorStyles.miniLabel);
            }
        }
    }
}
