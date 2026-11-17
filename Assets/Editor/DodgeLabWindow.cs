using System;
using System.IO;
using CyberBiker.Combat;
using UnityEditor;
using UnityEngine;

namespace CyberBiker.EditorTools {
    public sealed class DodgeLabDraft : ScriptableObject {public CloseDodgeSettings settings=new CloseDodgeSettings();}
    public sealed class DodgeLabWindow : EditorWindow {
        public const string ProfilePath="Assets/Resources/CloseDodgeTuning.asset";
        private DodgeLabDraft draft;
        private Vector2 scroll;
        private string notice="";
        private static string DraftKey=>"CyberBiker.DodgeLab.Draft."+Application.dataPath;
        public CloseDodgeSettings Draft=>draft.settings;
        [MenuItem("CyberBiker/Dodge Lab")]
        public static void Open(){var window=GetWindow<DodgeLabWindow>("Dodge Lab");window.minSize=new Vector2(700,590);window.Show();}
        private void OnEnable(){
            // HideAndDontSave includes NotEditable, which disables Unity's
            // SerializedProperty controls even in a custom editor window.
            draft=CreateInstance<DodgeLabDraft>();draft.hideFlags=HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
            try{draft.settings=EditorPrefs.HasKey(DraftKey) ? JsonUtility.FromJson<CloseDodgeSettings>(EditorPrefs.GetString(DraftKey)) : ReadSaved();}catch{draft.settings=ReadSaved();}
            draft.settings.Normalize();EditorApplication.update+=Refresh;
        }
        private void OnDisable(){EditorApplication.update-=Refresh;if(draft!=null)DestroyImmediate(draft);}
        private void Refresh(){if(DodgeLabPreview.Active)Repaint();}
        public static CloseDodgeSettings ReadSaved(){var profile=AssetDatabase.LoadAssetAtPath<CloseDodgeProfile>(ProfilePath);return profile!=null ? profile.settings.Copy() : new CloseDodgeSettings();}
        public static void SaveForGame(CloseDodgeSettings settings,string path=ProfilePath){
            settings.Normalize();var profile=AssetDatabase.LoadAssetAtPath<CloseDodgeProfile>(path);
            if(profile==null){profile=CreateInstance<CloseDodgeProfile>();AssetDatabase.CreateAsset(profile,path);}
            Undo.RecordObject(profile,"Save close-dodge tuning");profile.settings=settings.Copy();EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
        }
        private void Remember(){draft.settings.Normalize();EditorPrefs.SetString(DraftKey,JsonUtility.ToJson(draft.settings));DodgeLabPreview.Apply(draft.settings);}
        private void OnGUI(){
            if(draft==null)return;
            EditorGUILayout.Space(6);EditorGUILayout.LabelField("DODGE LAB",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("A real passing bullet triggers the cinematic. Timings are measured in real seconds.",EditorStyles.miniLabel);
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))if(GUILayout.Button("Start preview",EditorStyles.toolbarButton,GUILayout.Width(100))){Remember();DodgeLabPreview.Start(draft.settings);}
                using(new EditorGUI.DisabledScope(!DodgeLabPreview.Ready)){
                    if(GUILayout.Button("Replay",EditorStyles.toolbarButton,GUILayout.Width(55))){Remember();DodgeLabPreview.Replay();}
                    if(GUILayout.Button(EditorApplication.isPaused ? "Play" : "Pause",EditorStyles.toolbarButton,GUILayout.Width(50)))EditorApplication.isPaused=!EditorApplication.isPaused;
                    if(GUILayout.Button("Step",EditorStyles.toolbarButton,GUILayout.Width(45)))DodgeLabPreview.Step();
                }
                using(new EditorGUI.DisabledScope(!DodgeLabPreview.Active))if(GUILayout.Button("Stop",EditorStyles.toolbarButton,GUILayout.Width(45)))DodgeLabPreview.Stop();
                DodgeLabPreview.Loop=GUILayout.Toggle(DodgeLabPreview.Loop,"Loop",EditorStyles.toolbarButton,GUILayout.Width(45));
                DodgeLabPreview.Sound=GUILayout.Toggle(DodgeLabPreview.Sound,"Sound",EditorStyles.toolbarButton,GUILayout.Width(55));
                DodgeLabPreview.Aiming=GUILayout.Toggle(DodgeLabPreview.Aiming,"Aim camera",EditorStyles.toolbarButton,GUILayout.Width(85));
                GUILayout.FlexibleSpace();
            }
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
                if(GUILayout.Button("SAVE FOR GAME",EditorStyles.toolbarButton,GUILayout.Width(125))){Remember();SaveForGame(draft.settings);notice="Saved to CloseDodgeTuning. Future Play sessions and builds use these values.";}
                if(GUILayout.Button("Load saved",EditorStyles.toolbarButton,GUILayout.Width(85))){draft.settings=ReadSaved();Remember();}
                if(GUILayout.Button("Export",EditorStyles.toolbarButton,GUILayout.Width(55))){var path=EditorUtility.SaveFilePanel("Export dodge preset","","DodgePreset","json");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,JsonUtility.ToJson(draft.settings,true));}
                if(GUILayout.Button("Import",EditorStyles.toolbarButton,GUILayout.Width(55))){var path=EditorUtility.OpenFilePanel("Import dodge preset","","json");if(!string.IsNullOrEmpty(path)){try{var imported=JsonUtility.FromJson<CloseDodgeSettings>(File.ReadAllText(path));if(imported==null)throw new InvalidDataException("Empty dodge preset");imported.Normalize();draft.settings=imported;Remember();}catch(Exception error){notice=error.Message;}}}
            }
            using(new EditorGUILayout.HorizontalScope()){
                DodgeLabPreview.Gap=EditorGUILayout.Slider("Pass offset (m)",DodgeLabPreview.Gap,-6,6);
                if(GUILayout.Button("Other side",GUILayout.Width(85)))DodgeLabPreview.Gap=-DodgeLabPreview.Gap;
            }
            using(new EditorGUILayout.HorizontalScope()){
                DodgeLabPreview.RideSpeed=EditorGUILayout.FloatField("Ride speed",DodgeLabPreview.RideSpeed);
                DodgeLabPreview.BulletSpeed=EditorGUILayout.FloatField("Bullet speed",DodgeLabPreview.BulletSpeed);
            }
            DodgeLabPreview.Approach=EditorGUILayout.Slider("Approach time (s)",DodgeLabPreview.Approach,.2f,2);
            using(new EditorGUILayout.HorizontalScope()){
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(345))){
                    scroll=EditorGUILayout.BeginScrollView(scroll,GUILayout.Height(Mathf.Max(220,position.height-215)));
                    var data=new SerializedObject(draft);data.Update();var settings=data.FindProperty("settings");var field=settings.Copy();int depth=field.depth;
                    EditorGUIUtility.labelWidth=190;EditorGUI.BeginChangeCheck();
                    if(field.NextVisible(true))do{if(field.depth<=depth)break;EditorGUILayout.PropertyField(field,true);}while(field.NextVisible(false));
                    if(EditorGUI.EndChangeCheck()){data.ApplyModifiedPropertiesWithoutUndo();Remember();}
                    EditorGUIUtility.labelWidth=0;EditorGUILayout.EndScrollView();
                }
                using(new EditorGUILayout.VerticalScope()){
                    float height=Mathf.Min((position.width-370)*9/16,Mathf.Max(180,position.height-370));var rect=GUILayoutUtility.GetRect(100,height,GUILayout.ExpandWidth(true),GUILayout.Height(height));
                    EditorGUI.DrawRect(rect,new Color(.025f,.04f,.06f));if(DodgeLabPreview.View!=null)EditorGUI.DrawPreviewTexture(rect,DodgeLabPreview.View,null,ScaleMode.ScaleToFit);else GUI.Label(rect,"Start preview to test the close pass",EditorStyles.centeredGreyMiniLabel);
                    var effects=DodgeLabPreview.Effects;
                    using(new EditorGUI.DisabledScope(!DodgeLabPreview.Ready || effects==null || effects.TriggerCount==0)){
                        EditorGUI.BeginChangeCheck();float time=EditorGUILayout.Slider("Effect time (real s)",effects!=null ? Mathf.Min(effects.Age,draft.settings.Duration) : 0,0,Mathf.Max(.01f,draft.settings.Duration));if(EditorGUI.EndChangeCheck())DodgeLabPreview.Seek(time);
                    }
                    EditorGUILayout.HelpBox(DodgeLabPreview.Status,MessageType.None);
                    EditorGUILayout.LabelField("The timeline freezes the world and scrubs the post-pass camera/slow motion. Replay tests the full bullet approach. Scenario edits apply on Replay.",EditorStyles.wordWrappedMiniLabel);
                    if(EditorApplication.isPlaying && !DodgeLabPreview.Active)EditorGUILayout.HelpBox("Stop the current Play session before Start preview. The Lab restores your editor scenes when stopped.",MessageType.Info);
                    if(!string.IsNullOrEmpty(notice))EditorGUILayout.HelpBox(notice,MessageType.Info);
                }
            }
        }
    }
}
