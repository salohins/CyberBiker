using System;
using System.IO;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;
using P=CyberBiker.EditorTools.RiderReactionLabPreview;

namespace CyberBiker.EditorTools {
    public sealed class RiderReactionLabWindow:EditorWindow {
        private RiderReactionLabTuning draft;
        private Vector2 scroll;
        private string notice="Preview edits are temporary. Save for game writes these reactions to the player prefab.";
        private bool dragging;
        public RiderReactionLabTuning Draft=>draft;
        public Rect PreviewRect {get;private set;}
        private static string DraftKey=>"CyberBiker.RiderReactions.Draft."+Application.dataPath;
        [MenuItem("CyberBiker/Rider Reactions Lab")]
        public static void Open(){var w=GetWindow<RiderReactionLabWindow>("Rider Reactions Lab");w.minSize=new Vector2(900,650);w.Show();}
        private void OnEnable(){try{draft=EditorPrefs.HasKey(DraftKey) ? JsonUtility.FromJson<RiderReactionLabTuning>(EditorPrefs.GetString(DraftKey)) : RiderReactionLabTuning.ReadSaved();draft.Validate();}catch{draft=RiderReactionLabTuning.ReadSaved();}EditorApplication.update+=Tick;}
        private void OnDisable(){EditorApplication.update-=Tick;}
        private void Tick(){if(P.Active)Repaint();}
        private void Safe(Action action){try{action();}catch(Exception error){notice=error.Message;Debug.LogException(error);}}
        public void Changed(){draft.Validate();EditorPrefs.SetString(DraftKey,JsonUtility.ToJson(draft));P.Apply(draft);Repaint();}
        public void Orbit(Vector2 delta){P.OrbitYaw+=delta.x;P.OrbitPitch-=delta.y;P.Render();Repaint();}
        public void Zoom(float delta){P.Distance+=delta;P.Render();Repaint();}
        private void ScenarioChanged(){if(P.Ready && EditorApplication.isPaused)P.Seek(P.Age);}
        private void OnGUI(){
            if(draft==null)return;
            GUILayout.Label("RIDER REACTIONS LAB",EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))if(GUILayout.Button("Start preview",EditorStyles.toolbarButton))Safe(()=>P.Start(draft));
                using(new EditorGUI.DisabledScope(!P.Active))if(GUILayout.Button("Stop",EditorStyles.toolbarButton))P.Stop();
                using(new EditorGUI.DisabledScope(!P.Ready)){
                    if(GUILayout.Button("Replay",EditorStyles.toolbarButton)){P.Replay();EditorApplication.isPaused=false;}
                    if(GUILayout.Button(EditorApplication.isPaused ? "Resume" : "Pause",EditorStyles.toolbarButton))EditorApplication.isPaused=!EditorApplication.isPaused;
                    if(GUILayout.Button("Step",EditorStyles.toolbarButton))P.Step();
                }
                P.Current=(P.Scenario)EditorGUILayout.EnumPopup(P.Current,GUILayout.Width(165));
                P.Loop=GUILayout.Toggle(P.Loop,"Loop",EditorStyles.toolbarButton);
                P.Sound=GUILayout.Toggle(P.Sound,"Sound",EditorStyles.toolbarButton);
            }
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
                if(GUILayout.Button("Save for game",EditorStyles.toolbarButton))Safe(()=>{draft.SaveForGame();notice="Saved to Player_Last prefab. Reactions will use this tuning in the game and builds.";});
                if(GUILayout.Button("Load saved",EditorStyles.toolbarButton))Safe(()=>{draft=RiderReactionLabTuning.ReadSaved();Changed();notice="Loaded the saved player reaction settings.";});
                if(GUILayout.Button("Export preset",EditorStyles.toolbarButton))Safe(()=>{var path=EditorUtility.SaveFilePanel("Export rider reactions","","RiderReactions","json");if(path.Length>0)File.WriteAllText(path,JsonUtility.ToJson(draft,true));});
                if(GUILayout.Button("Import preset",EditorStyles.toolbarButton))Safe(()=>{var path=EditorUtility.OpenFilePanel("Import rider reactions","","json");if(path.Length>0){var imported=JsonUtility.FromJson<RiderReactionLabTuning>(File.ReadAllText(path));imported.Validate();draft=imported;Changed();notice="Preset loaded into the draft. Save for game to keep it.";}});
            }
            EditorGUILayout.HelpBox(notice,MessageType.None);
            using(new EditorGUILayout.HorizontalScope()){
                EditorGUI.BeginChangeCheck();P.Side=EditorGUILayout.Slider("Traffic / enemy side (m)",P.Side,-8,8);
                if(GUILayout.Button("Other side",GUILayout.Width(85)))P.Side=-P.Side;
                P.Speed=EditorGUILayout.FloatField("Ride speed (m/s)",P.Speed,GUILayout.Width(235));
                if(EditorGUI.EndChangeCheck())ScenarioChanged();
            }
            using(new EditorGUILayout.HorizontalScope()){
                EditorGUI.BeginChangeCheck();
                if(P.Current==P.Scenario.CarApproach || P.Current==P.Scenario.ClosePass){P.CarStart=EditorGUILayout.Slider("Car starts ahead (m)",P.CarStart,10,100);P.PassStrength=EditorGUILayout.Slider("Pass strength",P.PassStrength,0,1);}
                else P.EnemyDistance=EditorGUILayout.Slider("Enemy distance (m)",P.EnemyDistance,10,70);
                P.Steering=EditorGUILayout.Slider("Steering",P.Steering,-1,1);
                if(EditorGUI.EndChangeCheck()){CameraLabPreview.RefreshCamera();ScenarioChanged();}
                P.Aiming=GUILayout.Toggle(P.Aiming,"Aiming pose",GUILayout.Width(105));
            }
            using(new EditorGUILayout.HorizontalScope()){
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Clamp(position.width*.32f,315,400)))){
                    scroll=EditorGUILayout.BeginScrollView(scroll);string group=null;EditorGUI.BeginChangeCheck();
                    foreach(var control in RiderReactionLabTuning.Controls){
                        if(control.group!=group){group=control.group;GUILayout.Space(8);GUILayout.Label(group,EditorStyles.boldLabel);}
                        var v=draft.Get(control.field);
                        var label=new GUIContent(control.label,control.field+" — "+(control.label.Contains("smoothing") ? "Smaller values turn faster; larger values blend more slowly." : "Applies immediately to the live preview."));
                        if(control.toggle)v.number=EditorGUILayout.Toggle(label,v.number>=.5f) ? 1 : 0;
                        else v.number=EditorGUILayout.Slider(label,v.number,Mathf.Min(control.min,v.number),Mathf.Max(control.max,v.number));
                    }
                    if(EditorGUI.EndChangeCheck())Safe(Changed);EditorGUILayout.EndScrollView();
                }
                using(new EditorGUILayout.VerticalScope()){
                    using(new EditorGUILayout.HorizontalScope()){
                        if(GUILayout.Button("Full bike")){P.GameView=false;P.HeadCloseup=false;P.Distance=10;P.Pan=Vector3.zero;P.Render();}
                        if(GUILayout.Button("Head & neck")){P.GameView=false;P.HeadCloseup=true;P.Distance=3;P.Pan=Vector3.zero;P.Render();}
                        P.GameView=GUILayout.Toggle(P.GameView,"Game view","Button");
                    }
                    GUILayout.Label("Drag: orbit · Shift-drag: pan · Wheel: zoom",EditorStyles.miniLabel);
                    var area=GUILayoutUtility.GetRect(100,100,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));EditorGUI.DrawRect(area,new Color(.035f,.04f,.05f));
                    if(P.View!=null){var fit=Fit(area,P.View.width/(float)P.View.height);PreviewRect=fit;GUI.DrawTexture(fit,P.View,ScaleMode.StretchToFill,false);if(!P.GameView)HandleMouse(fit);}
                    else GUI.Label(area,"Start preview, choose a scenario, then edit the controls.\nThe real rider and animated pose are used in Play mode.",EditorStyles.centeredGreyMiniLabel);
                    using(new EditorGUI.DisabledScope(!P.Ready)){
                        EditorGUI.BeginChangeCheck();float time=EditorGUILayout.Slider("Reaction timeline (s)",P.Age,0,P.Duration);if(EditorGUI.EndChangeCheck())P.Seek(time);
                    }
                    GUILayout.Label(P.Ready ? P.Status : P.Active ? "Starting the live rider preview…" : P.Status,EditorStyles.wordWrappedMiniLabel);
                    GUILayout.Label("Scrubbing pauses the preview. Edits rebuild the reaction at the chosen time.\nVisible shooters keep priority; body reactions yield while aiming.",EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
        private static Rect Fit(Rect r,float aspect){float w=Mathf.Min(r.width,r.height*aspect),h=w/aspect;return new Rect(r.center.x-w/2,r.center.y-h/2,w,h);}
        private void HandleMouse(Rect rect){
            var e=Event.current;int id=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.MouseDown && rect.Contains(e.mousePosition) && e.button==0){dragging=true;GUIUtility.hotControl=id;e.Use();}
            else if(e.type==EventType.MouseDrag && dragging && GUIUtility.hotControl==id){if(e.shift){P.Pan+=Quaternion.Euler(P.OrbitPitch,P.OrbitYaw,0)*new Vector3(-e.delta.x,e.delta.y,0)*P.Distance*.0018f;P.Render();}else Orbit(e.delta*.25f);e.Use();}
            else if(e.type==EventType.MouseUp && GUIUtility.hotControl==id){dragging=false;GUIUtility.hotControl=0;e.Use();}
            else if(e.type==EventType.ScrollWheel && rect.Contains(e.mousePosition)){Zoom(e.delta.y*.25f);e.Use();}
        }
    }
}
