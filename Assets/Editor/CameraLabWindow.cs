using System;
using System.Collections.Generic;
using System.IO;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;
using S=CyberBiker.EditorTools.CameraLabTuning.Scope;
using K=CyberBiker.EditorTools.CameraLabTuning.Kind;

namespace CyberBiker.EditorTools {
    public sealed class CameraLabWindow:EditorWindow {
        private CameraLabTuning draft;
        private Vector2 scroll;
        private readonly HashSet<string> open=new HashSet<string>{"Riding"};
        private string notice="Changes stay in your draft until you press Save for game.";
        private int dragButton=-1;
        private bool sceneHandles;
        public CameraLabTuning Draft=>draft;
        public Rect PreviewRect {get;private set;}
        private static string DraftKey=>"CyberBiker.CameraLab.Draft."+Application.dataPath;
        [MenuItem("CyberBiker/Camera Lab")]
        public static void Open(){var w=GetWindow<CameraLabWindow>("Camera Lab");w.minSize=new Vector2(800,620);w.Show();}
        private void OnEnable(){try{draft=EditorPrefs.HasKey(DraftKey) ? JsonUtility.FromJson<CameraLabTuning>(EditorPrefs.GetString(DraftKey)) : CameraLabTuning.ReadSavedDefaults();draft.Normalize();}catch{draft=CameraLabTuning.ReadSavedDefaults();}EditorApplication.update+=Tick;SceneView.duringSceneGui+=SceneGUI;}
        private void OnDisable(){EditorApplication.update-=Tick;SceneView.duringSceneGui-=SceneGUI;}
        private void Tick(){if(CameraLabPreview.Active)Repaint();}
        public void Changed(){draft.Normalize();EditorPrefs.SetString(DraftKey,JsonUtility.ToJson(draft));CameraLabPreview.Apply(draft);Repaint();SceneView.RepaintAll();}
        private void Safe(Action action){try{action();}catch(Exception error){notice=error.Message;Debug.LogException(error);}}
        private void OnGUI(){
            if(draft==null)return;
            GUILayout.Label("CAMERA LAB",EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))if(GUILayout.Button("Start preview",EditorStyles.toolbarButton))Safe(()=>CameraLabPreview.Start(draft,CameraLabPreview.Mode));
                using(new EditorGUI.DisabledScope(!CameraLabPreview.Active)){
                    if(GUILayout.Button("Stop",EditorStyles.toolbarButton))CameraLabPreview.Stop();
                    if(GUILayout.Button("Replay",EditorStyles.toolbarButton))CameraLabPreview.Replay();
                    using(new EditorGUI.DisabledScope(!CameraLabPreview.Ready)){
                        if(GUILayout.Button(EditorApplication.isPaused ? "Resume" : "Pause",EditorStyles.toolbarButton))EditorApplication.isPaused=!EditorApplication.isPaused;
                        if(GUILayout.Button("Step",EditorStyles.toolbarButton))CameraLabPreview.Step();
                    }
                }
                var mode=(CameraLabPreview.ViewMode)EditorGUILayout.EnumPopup(CameraLabPreview.Mode,GUILayout.Width(155));if(mode!=CameraLabPreview.Mode){Safe(()=>CameraLabPreview.SwitchView(mode));open.Add(Group(mode));}
                CameraLabPreview.Sound=GUILayout.Toggle(CameraLabPreview.Sound,"Sound",EditorStyles.toolbarButton);
                CameraLabPreview.Hold=GUILayout.Toggle(CameraLabPreview.Hold,"Hold impact",EditorStyles.toolbarButton);
            }
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)){
                if(GUILayout.Button("Save for game",EditorStyles.toolbarButton))Safe(()=>{draft.SaveForGame();notice="Saved. These camera settings now apply in the game and future builds.";});
                if(GUILayout.Button("Load saved",EditorStyles.toolbarButton))Safe(()=>{draft=CameraLabTuning.ReadSavedDefaults();Changed();notice="Loaded saved game settings.";});
                if(GUILayout.Button("Scene defaults",EditorStyles.toolbarButton))Safe(()=>{draft=CameraLabTuning.ReadSavedDefaults(true);Changed();notice="Loaded the saved Level camera. Press Save for game to keep it.";});
                if(GUILayout.Button("Export preset",EditorStyles.toolbarButton))Safe(()=>{string path=EditorUtility.SaveFilePanel("Export camera preset","","CameraPreset","json");if(path.Length>0)File.WriteAllText(path,JsonUtility.ToJson(draft,true));});
                if(GUILayout.Button("Import preset",EditorStyles.toolbarButton))Safe(()=>{string path=EditorUtility.OpenFilePanel("Import camera preset","","json");if(path.Length>0){var imported=JsonUtility.FromJson<CameraLabTuning>(File.ReadAllText(path));imported.Normalize();draft=imported;Changed();notice="Preset loaded into the draft. Save for game to keep it.";}});
            }
            using(new EditorGUILayout.HorizontalScope()){
                EditorGUI.BeginChangeCheck();CameraLabPreview.SteeringAmount=EditorGUILayout.Slider("Steering",CameraLabPreview.SteeringAmount,-1,1);CameraLabPreview.Speed=EditorGUILayout.FloatField("Speed (m/s)",CameraLabPreview.Speed,GUILayout.Width(200));if(EditorGUI.EndChangeCheck())CameraLabPreview.RefreshCamera();
            }
            using(new EditorGUILayout.HorizontalScope()){
                EditorGUI.BeginChangeCheck();CameraLabPreview.Lane=EditorGUILayout.Slider("Road position",CameraLabPreview.Lane,-8,8);CameraLabPreview.TargetSide=EditorGUILayout.Slider("Enemy side",CameraLabPreview.TargetSide,-8,8);if(EditorGUI.EndChangeCheck())CameraLabPreview.RefreshCamera();
            }
            if(CameraLabPreview.AutomaticView){
                using(new EditorGUILayout.HorizontalScope()){
                    CameraLabPreview.EnemyPresent=EditorGUILayout.ToggleLeft("Nearby enemy",CameraLabPreview.EnemyPresent,GUILayout.Width(125));
                    CameraLabPreview.EnemyDistance=EditorGUILayout.Slider("Enemy distance (m)",CameraLabPreview.EnemyDistance,2,200);
                }
                GUILayout.Label(CameraLabPreview.Mode==CameraLabPreview.ViewMode.AutoAim ? "Test automatic switching here. Position each view using Cruising Aim and Combat Aim." : "Test automatic switching here. Position each view using Riding and Combat Riding.",EditorStyles.miniLabel);
            }
            using(new EditorGUILayout.HorizontalScope()){
                using(new EditorGUI.DisabledScope(!CameraLabPreview.Ready)){
                    if(GUILayout.Button("Test passing shake"))CameraLabPreview.Rig.TriggerPassShake(1,CameraLabPreview.SteeringAmount>=0 ? 1 : -1);
                    if(GUILayout.Button("Test collision impact"))CameraLabPreview.Rig.TriggerCollisionImpact(1,Vector3.right);
                    if(GUILayout.Button("Frame rider")){var scene=SceneView.lastActiveSceneView;if(scene!=null)scene.LookAt(CameraLabPreview.Player.transform.position+Vector3.up,Quaternion.Euler(10,0,0),8);}
                    using(new EditorGUI.DisabledScope(CameraLabPreview.AutomaticView))if(GUILayout.Button("Use Scene view"))Safe(UseSceneView);
                }
                sceneHandles=GUILayout.Toggle(sceneHandles,"Scene handles","Button");
            }
            EditorGUILayout.HelpBox(notice,MessageType.None);
            using(new EditorGUILayout.HorizontalScope()){
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Clamp(position.width*.30f,280,370)))){
                    scroll=EditorGUILayout.BeginScrollView(scroll);
                    string group=null;bool expanded=false;EditorGUI.BeginChangeCheck();
                    foreach(var control in CameraLabTuning.Controls){
                        if(control.group!=group){group=control.group;bool before=open.Contains(group);expanded=EditorGUILayout.Foldout(before,group,true,EditorStyles.foldoutHeader);if(expanded)open.Add(group);else open.Remove(group);}
                        if(!expanded)continue;var v=draft.Get(control.scope,control.field);
                        if(control.kind==K.Vector)v.vector=EditorGUILayout.Vector3Field(control.label,v.vector);
                        else if(control.kind==K.Toggle)v.number=EditorGUILayout.Toggle(control.label,v.number>=.5f) ? 1 : 0;
                        else if(control.kind==K.Color)v.color=EditorGUILayout.ColorField(control.label,v.color);
                        else v.number=EditorGUILayout.Slider(new GUIContent(control.label),v.number,Mathf.Min(control.min,v.number),Mathf.Max(control.max,v.number));
                    }
                    if(EditorGUI.EndChangeCheck())Safe(Changed);EditorGUILayout.EndScrollView();
                }
                using(new EditorGUILayout.VerticalScope()){
                    GUILayout.Label("Drag: move · Right-drag: rotate · Wheel: distance · Shift+wheel: FOV",EditorStyles.miniLabel);
                    var area=GUILayoutUtility.GetRect(100,100,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));EditorGUI.DrawRect(area,new Color(.035f,.04f,.05f));
                    var live=CameraLabPreview.View;if(live!=null){var fit=Fit(area,live.width/(float)live.height);PreviewRect=fit;GUI.DrawTexture(fit,live,ScaleMode.StretchToFill,false);if(!CameraLabPreview.AutomaticView)HandleMouse(fit);}else GUI.Label(area,"Start preview to see the real camera.\nChoose a view above, then drag here or edit its parameters.",EditorStyles.centeredGreyMiniLabel);
                    GUILayout.Label(CameraLabPreview.Status,EditorStyles.wordWrappedMiniLabel);
                    if(CameraLabPreview.Mode==CameraLabPreview.ViewMode.Finisher){FinisherLabPreview.Left=GUILayout.Toggle(FinisherLabPreview.Left,"Left-side finisher");if(GUILayout.Button("Open Finisher Lab for animation / timing"))FinisherLabWindow.Open();}
                    if(CameraLabPreview.Mode==CameraLabPreview.ViewMode.Dodge){DodgeLabPreview.Gap=EditorGUILayout.Slider("Bullet pass side",DodgeLabPreview.Gap,-3,3);if(GUILayout.Button("Open Dodge Lab for timing / radius"))DodgeLabWindow.Open();}
                }
            }
        }
        private static Rect Fit(Rect r,float aspect){float w=Mathf.Min(r.width,r.height*aspect),h=w/aspect;return new Rect(r.center.x-w/2,r.center.y-h/2,w,h);}
        private static string Group(CameraLabPreview.ViewMode mode)=>mode==CameraLabPreview.ViewMode.Launch ? "Launch / nitro" : mode==CameraLabPreview.ViewMode.Finisher ? "Sword finisher" : mode==CameraLabPreview.ViewMode.Dodge ? "Close dodge" : mode==CameraLabPreview.ViewMode.Airborne ? "Airborne" : mode==CameraLabPreview.ViewMode.CombatRiding ? "Combat riding" : mode==CameraLabPreview.ViewMode.AutoRiding || mode==CameraLabPreview.ViewMode.AutoAim ? "Combat switching" : mode==CameraLabPreview.ViewMode.Riding ? "Riding" : CameraLabPreview.IsCombatAim(mode) ? "Combat aim" : "Cruising aim";
        private bool Effect=>CameraLabPreview.Mode==CameraLabPreview.ViewMode.Launch || CameraLabPreview.Mode==CameraLabPreview.ViewMode.Finisher || CameraLabPreview.Mode==CameraLabPreview.ViewMode.Dodge;
        private bool Combat=>CameraLabPreview.Mode==CameraLabPreview.ViewMode.CombatRiding;
        private bool CombatAiming=>CameraLabPreview.IsCombatAim(CameraLabPreview.Mode);
        private bool Aiming=>CameraLabPreview.IsAimingMode(CameraLabPreview.Mode);
        private S Scope=>CameraLabPreview.Mode==CameraLabPreview.ViewMode.Launch ? S.Launch : CameraLabPreview.Mode==CameraLabPreview.ViewMode.Finisher ? S.Finisher : CameraLabPreview.Mode==CameraLabPreview.ViewMode.Dodge ? S.Dodge : S.Rig;
        private CameraLabTuning.Value PositionValue=>draft.Get(Scope,Effect ? Scope==S.Dodge ? "viewOffset" : "cameraViewOffset" : Combat ? "combatRiding.offset" : CombatAiming ? "combatAim.offset" : Aiming ? "aimOffset" : CameraLabPreview.Mode==CameraLabPreview.ViewMode.Airborne ? "airborneOffset" : "targetOffset");
        public void Pan(Vector3 localDelta){if(CameraLabPreview.Mode==CameraLabPreview.ViewMode.Airborne)draft.Get(S.Rig,"useAirborneOffset").number=1;if(Aiming){var offset=PositionValue.vector;float sign=offset.x>=0 ? 1 : -1;float shoulder=CameraLabPreview.ForcedShoulder!=0 ? CameraLabPreview.ForcedShoulder : CameraLabPreview.Rig!=null ? Mathf.Sign((float)typeof(CameraController).GetField("aimShoulderX",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(CameraLabPreview.Rig)) : sign;offset.x=sign*Mathf.Max(.05f,Mathf.Abs(offset.x)-localDelta.x*shoulder);offset.y-=localDelta.y;offset.z-=localDelta.z;PositionValue.vector=offset;}else PositionValue.vector+=Effect ? localDelta : -localDelta;Changed();}
        public void Rotate(Vector2 degrees){if(Effect){var v=draft.Get(Scope,Scope==S.Dodge ? "viewEuler" : "cameraViewEuler");v.vector+=new Vector3(degrees.x,degrees.y,0);}else if(CombatAiming){draft.Get(S.Rig,"combatAim.pitch").number+=degrees.x;draft.Get(S.Rig,"combatAim.yaw").number+=degrees.y;}else if(Combat){draft.Get(S.Rig,"combatRiding.pitch").number+=degrees.x;draft.Get(S.Rig,"combatRiding.yaw").number+=degrees.y;}else{string prefix=Aiming ? "aim" : CameraLabPreview.Mode==CameraLabPreview.ViewMode.Airborne ? "airborne" : "riding";draft.Get(S.Rig,prefix+"Pitch").number+=degrees.x;draft.Get(S.Rig,prefix+"YawOffset").number+=degrees.y;}Changed();}
        public void Zoom(float amount,bool fov){if(fov){string field=Scope==S.Launch ? "fovIncrease" : Scope==S.Finisher ? "hitZoom" : Scope==S.Dodge ? "zoomInFov" : Combat ? "combatRiding.fov" : CombatAiming ? "combatAim.fov" : Aiming ? "aimFov" : CameraLabPreview.Mode==CameraLabPreview.ViewMode.Airborne ? "airborneFov" : "ridingFov";draft.Get(Scope,field).number+=amount*(Scope==S.Finisher || Scope==S.Dodge ? -1 : 1);}else{if(CameraLabPreview.Mode==CameraLabPreview.ViewMode.Airborne)draft.Get(S.Rig,"useAirborneOffset").number=1;PositionValue.vector+=Vector3.forward*amount*(Effect ? -1 : 1);}Changed();}
        private void HandleMouse(Rect rect){var e=Event.current;int id=GUIUtility.GetControlID(FocusType.Passive);if(e.type==EventType.MouseDown && rect.Contains(e.mousePosition) && (e.button==0 || e.button==1)){dragButton=e.button;GUIUtility.hotControl=id;e.Use();}else if(e.type==EventType.MouseDrag && dragButton>=0 && GUIUtility.hotControl==id){if(dragButton==0)Pan(new Vector3(e.delta.x,-e.delta.y,0)*.012f);else Rotate(new Vector2(-e.delta.y,e.delta.x)*.16f);e.Use();}else if(e.type==EventType.MouseUp && GUIUtility.hotControl==id){dragButton=-1;GUIUtility.hotControl=0;e.Use();}else if(e.type==EventType.ScrollWheel && rect.Contains(e.mousePosition)){Zoom(e.delta.y*(e.shift ? 1.2f : .3f),e.shift);e.Use();}}
        private void SceneGUI(SceneView scene){if(!sceneHandles || !CameraLabPreview.Ready || CameraLabPreview.AutomaticView)return;var camera=CameraLabPreview.Camera;EditorGUI.BeginChangeCheck();var pos=Handles.PositionHandle(camera.transform.position,camera.transform.rotation);if(EditorGUI.EndChangeCheck()){Pan(camera.transform.InverseTransformDirection(pos-camera.transform.position)/Weight);CameraLabPreview.RefreshCamera();}EditorGUI.BeginChangeCheck();var rot=Handles.RotationHandle(camera.transform.rotation,camera.transform.position);if(EditorGUI.EndChangeCheck()){var delta=Quaternion.Inverse(camera.transform.rotation)*rot;Rotate(new Vector2(Mathf.DeltaAngle(0,delta.eulerAngles.x),Mathf.DeltaAngle(0,delta.eulerAngles.y))/Weight);CameraLabPreview.RefreshCamera();}}
        private float Weight=>Scope==S.Launch ? Mathf.Max(.1f,CameraLabPreview.Player.GetComponent<BikeLaunchEffects>().Amount) : Scope==S.Finisher ? Mathf.Max(.1f,CameraLabPreview.Player.GetComponent<SwordFinisherEffects>().CameraAmount) : Scope==S.Dodge ? Mathf.Max(.1f,DodgeLabPreview.Effects.CameraAmount) : 1;
        private void UseSceneView(){var scene=SceneView.lastActiveSceneView;if(scene==null || !CameraLabPreview.Ready)return;var camera=CameraLabPreview.Camera;Pan(camera.transform.InverseTransformDirection(scene.camera.transform.position-camera.transform.position)/Weight);var difference=Quaternion.Inverse(camera.transform.rotation)*scene.camera.transform.rotation;Rotate(new Vector2(Mathf.DeltaAngle(0,difference.eulerAngles.x),Mathf.DeltaAngle(0,difference.eulerAngles.y))/Weight);Zoom(scene.camera.fieldOfView-camera.fieldOfView,true);CameraLabPreview.RefreshCamera();}
    }
}
