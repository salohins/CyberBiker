using System;
using System.IO;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;
using P=CyberBiker.EditorTools.GapDashLabPreview;

namespace CyberBiker.EditorTools {
    public sealed class GapDashLabDraft:ScriptableObject {public GapDashSettings settings=new GapDashSettings();}
    public sealed class GapDashLabWindow:EditorWindow {
        public const string ProfilePath="Assets/Resources/GapDashTuning.asset";
        private GapDashLabDraft draft;
        private Vector2 scroll;
        private string notice="Changes are temporary until you click Save for game.";
        private float scrub;
        private bool dragging;
        private static string DraftKey=>"CyberBiker.GapDashLab.Draft."+Application.dataPath;
        public GapDashSettings Draft=>draft.settings;
        public Rect PreviewRect {get;private set;}
        [MenuItem("CyberBiker/Gap Dash Lab")]
        public static void Open(){var w=GetWindow<GapDashLabWindow>("Gap Dash Lab");w.minSize=new Vector2(900,650);w.Show();}
        private void OnEnable(){draft=CreateInstance<GapDashLabDraft>();draft.hideFlags=HideFlags.HideAndDontSave & ~HideFlags.NotEditable;try{draft.settings=EditorPrefs.HasKey(DraftKey) ? JsonUtility.FromJson<GapDashSettings>(EditorPrefs.GetString(DraftKey)) : ReadSaved();}catch{draft.settings=ReadSaved();}
            if(EditorPrefs.GetInt(DraftKey+".AssistRevision",0)<5){draft.settings.snapRadius=Mathf.Max(5,draft.settings.snapRadius);draft.settings.approachTime=Mathf.Max(.85f,draft.settings.approachTime);draft.settings.maximumTriggerDistance=Mathf.Max(65,draft.settings.maximumTriggerDistance);draft.settings.sideClearance=Mathf.Min(.04f,draft.settings.sideClearance);draft.settings.maximumAlignmentSpeed=18;if(P.CarDistance<=35)P.CarDistance=65;EditorPrefs.SetInt(DraftKey+".AssistRevision",5);EditorPrefs.SetString(DraftKey,JsonUtility.ToJson(draft.settings));}
            if(EditorPrefs.GetInt(DraftKey+".AssistRevision",0)<7){draft.settings.enabled=true;EditorPrefs.SetInt(DraftKey+".AssistRevision",7);EditorPrefs.SetString(DraftKey,JsonUtility.ToJson(draft.settings));}
            draft.settings.Normalize();EditorApplication.update+=Tick;}
        private void OnDisable(){EditorApplication.update-=Tick;if(draft!=null)DestroyImmediate(draft);}
        private void Tick(){if(P.Active)Repaint();}
        public static GapDashSettings ReadSaved(){var profile=AssetDatabase.LoadAssetAtPath<GapDashProfile>(ProfilePath);return profile!=null ? profile.settings.Copy() : new GapDashSettings();}
        public static void SaveForGame(GapDashSettings settings,string path=ProfilePath){settings.Normalize();var profile=AssetDatabase.LoadAssetAtPath<GapDashProfile>(path);if(profile==null){profile=CreateInstance<GapDashProfile>();AssetDatabase.CreateAsset(profile,path);}Undo.RecordObject(profile,"Save gap dash tuning");profile.settings=settings.Copy();EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);}
        public void Changed(){draft.settings.Normalize();EditorPrefs.SetString(DraftKey,JsonUtility.ToJson(draft.settings));P.Apply(draft.settings);Repaint();}
        public void EasyPassing(){var s=Draft;s.enabled=true;s.snapRadius=5;s.approachTime=.85f;s.maximumTriggerDistance=65;s.sideClearance=.04f;s.maximumGapWidth=3.5f;s.minimumSpeed=25;s.cooldown=.3f;s.maximumAlignmentSpeed=18;s.alignmentTime=.12f;s.maximumDashTime=1.2f;s.exitClearance=1.4f;P.CarDistance=65;Changed();notice="Earlier, wider alignment loaded; preview cars start farther ahead. Cars stay solid. Camera and wind tuning retained. Save for game to keep it.";}
        private void Safe(Action action){try{action();}catch(Exception error){notice=error.Message;Debug.LogException(error);}}
        private void OnGUI(){
            if(draft==null)return;GUILayout.Label("GAP DASH LAB",EditorStyles.boldLabel);GUILayout.Label("Launch mode only: centre before the rear bumpers. All cars remain solid.",EditorStyles.miniLabel);
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))if(GUILayout.Button("Start preview",EditorStyles.toolbarButton))Safe(()=>{Changed();P.Start(Draft);});
                using(new EditorGUI.DisabledScope(!P.Ready)){
                    if(GUILayout.Button("Replay",EditorStyles.toolbarButton)){Changed();P.Replay();}
                    if(GUILayout.Button(EditorApplication.isPaused ? "Resume" : "Pause",EditorStyles.toolbarButton))EditorApplication.isPaused=!EditorApplication.isPaused;
                    if(GUILayout.Button("Step",EditorStyles.toolbarButton))P.Step();
                }
                using(new EditorGUI.DisabledScope(!P.Active))if(GUILayout.Button("Stop",EditorStyles.toolbarButton))P.Stop();
                P.LaunchMode=GUILayout.Toggle(P.LaunchMode,"Launch",EditorStyles.toolbarButton);P.Loop=GUILayout.Toggle(P.Loop,"Loop",EditorStyles.toolbarButton);P.Sound=GUILayout.Toggle(P.Sound,"Sound",EditorStyles.toolbarButton);P.Aiming=GUILayout.Toggle(P.Aiming,"Aim camera",EditorStyles.toolbarButton);
            }
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
                if(GUILayout.Button("Save for game",EditorStyles.toolbarButton))Safe(()=>{Changed();SaveForGame(Draft);notice="Saved. The game and future builds use these gap dash settings.";});
                if(GUILayout.Button("Load saved",EditorStyles.toolbarButton)){draft.settings=ReadSaved();Changed();}
                if(GUILayout.Button("Easy passing",EditorStyles.toolbarButton))EasyPassing();
                if(GUILayout.Button("Export preset",EditorStyles.toolbarButton))Safe(()=>{var p=EditorUtility.SaveFilePanel("Export gap dash preset","","GapDash","json");if(p.Length>0)File.WriteAllText(p,JsonUtility.ToJson(Draft,true));});
                if(GUILayout.Button("Import preset",EditorStyles.toolbarButton))Safe(()=>{var p=EditorUtility.OpenFilePanel("Import gap dash preset","","json");if(p.Length>0){draft.settings=JsonUtility.FromJson<GapDashSettings>(File.ReadAllText(p));Changed();}});
            }
            EditorGUILayout.HelpBox(notice,MessageType.None);
            using(new EditorGUILayout.HorizontalScope()){P.Current=(P.Scenario)EditorGUILayout.EnumPopup("Test scenario",P.Current);P.Speed=EditorGUILayout.FloatField("Ride speed (m/s)",P.Speed);}
            using(new EditorGUILayout.HorizontalScope()){P.Offset=EditorGUILayout.Slider("Initial offset from gap",P.Offset,-8,8);P.Spacing=EditorGUILayout.Slider("Car centre spacing",P.Spacing,4.5f,8);}
            P.CarDistance=EditorGUILayout.Slider("Cars start ahead (m)",P.CarDistance,10,85);
            P.Steering=EditorGUILayout.Slider("Test steering (locked in pass)",P.Steering,-1,1);
            using(new EditorGUILayout.HorizontalScope()){
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(350))){
                    scroll=EditorGUILayout.BeginScrollView(scroll);using(var data=new SerializedObject(draft)){data.Update();var field=data.FindProperty("settings").Copy();int depth=field.depth;float old=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=180;EditorGUI.BeginChangeCheck();if(field.NextVisible(true))do{if(field.depth<=depth)break;EditorGUILayout.PropertyField(field,true);}while(field.NextVisible(false));if(EditorGUI.EndChangeCheck()){data.ApplyModifiedPropertiesWithoutUndo();Changed();}EditorGUIUtility.labelWidth=old;}EditorGUILayout.EndScrollView();
                }
                using(new EditorGUILayout.VerticalScope()){
                    P.OrbitView=GUILayout.Toggle(P.OrbitView,"Orbit inspection view â€” drag to rotate, wheel to zoom","Button");
                    var area=GUILayoutUtility.GetRect(100,100,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));EditorGUI.DrawRect(area,new Color(.035f,.04f,.05f));
                    if(P.View!=null){float w=Mathf.Min(area.width,area.height*16/9),h=w*9/16;var rect=new Rect(area.center.x-w/2,area.center.y-h/2,w,h);PreviewRect=rect;GUI.DrawTexture(rect,P.View,ScaleMode.StretchToFill,false);if(P.OrbitView)Mouse(rect);}else GUI.Label(area,"Start preview to test the automatic passage.",EditorStyles.centeredGreyMiniLabel);
                    using(new EditorGUI.DisabledScope(!P.Ready)){EditorGUI.BeginChangeCheck();scrub=EditorGUILayout.Slider("Camera / VFX time (s)",scrub,0,Draft.maximumDashTime+Draft.cameraReturn);if(EditorGUI.EndChangeCheck())P.SeekEffect(scrub);}
                    GUILayout.Label(P.Status,EditorStyles.wordWrappedMiniLabel);
                    GUILayout.Label("Scenario changes apply on Replay. Live tuning updates immediately.\nThe effect slider previews camera/VFX; Replay tests the full physical snap and passage.\nSteering is locked until fully clear of both cars. Narrow or blocked gaps must not trigger.",EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
        private void Mouse(Rect rect){var e=Event.current;int id=GUIUtility.GetControlID(FocusType.Passive);if(e.type==EventType.MouseDown && e.button==0 && rect.Contains(e.mousePosition)){dragging=true;GUIUtility.hotControl=id;e.Use();}else if(e.type==EventType.MouseDrag && dragging && GUIUtility.hotControl==id){P.Yaw+=e.delta.x*.25f;P.Pitch-=e.delta.y*.25f;P.Refresh();e.Use();}else if(e.type==EventType.MouseUp && GUIUtility.hotControl==id){dragging=false;GUIUtility.hotControl=0;e.Use();}else if(e.type==EventType.ScrollWheel && rect.Contains(e.mousePosition)){P.Distance+=e.delta.y*.3f;P.Refresh();e.Use();}}
    }
}
