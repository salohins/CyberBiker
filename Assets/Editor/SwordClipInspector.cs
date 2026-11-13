using System;
using System.Linq;
using System.Reflection;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;

namespace CyberBiker.EditorTools {
    public sealed class SwordClipInspector : EditorWindow {
        private static readonly string[] Titles={"Left swing","Right swing","Draw","Sheathe","Sword idle","Lean left","Lean right"};
        private static readonly string[] Clips={"Attack_Left","Attack_Right","Grab","Back","Idle","Idle_Left","Idle_Right"};
        private PreviewRenderUtility preview;
        private GameObject model;
        private AnimationClip clip;
        private Transform torso,sword;
        private BikeSwordCombat combat;
        private int action;
        private float progress,speed=1,yaw=0,pitch=12,zoom=1,extraTurn;
        private bool playing,adjustment;
        private Vector3 focus;
        private float radius;
        private double lastTime;
        public float ExtraTorsoTurn=>extraTurn;
        public float Progress {get=>progress;set{progress=Mathf.Clamp01(value);playing=false;}}
        [MenuItem("CyberBiker/Sword Clip Inspector")]
        public static void Open(){var window=GetWindow<SwordClipInspector>("Sword Clip Inspector");window.minSize=new Vector2(650,500);window.Show();}
        private void OnEnable(){lastTime=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}
        private void OnDisable(){EditorApplication.update-=Tick;if(preview!=null)preview.Cleanup();preview=null;model=null;}
        private void Tick(){double now=EditorApplication.timeSinceStartup;if(playing && clip!=null){progress=Mathf.Repeat(progress+(float)(now-lastTime)*speed/clip.length,1);Repaint();}lastTime=now;}
        private void EnsureModel() {
            if(model!=null)return;
            preview=new PreviewRenderUtility();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(FinisherLabTuning.PlayerPrefab);
            model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview.camera.scene);
            model.hideFlags=HideFlags.HideAndDontSave;model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);model.transform.localScale=Vector3.one;
            foreach(var rig in model.GetComponentsInChildren<UnityEngine.Animations.Rigging.RigBuilder>(true))rig.Clear();
            foreach(var behaviour in model.GetComponentsInChildren<Behaviour>(true))behaviour.enabled=false;
            foreach(var system in model.GetComponentsInChildren<ParticleSystem>(true))system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach(var renderer in model.GetComponentsInChildren<ParticleSystemRenderer>(true))renderer.enabled=false;
            foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){renderer.updateWhenOffscreen=true;renderer.forceMatrixRecalculationPerRender=true;}
            var bones=model.GetComponentsInChildren<Transform>(true);torso=bones.Single(t=>t.name=="DEF-spine.001");sword=bones.Single(t=>t.name=="Sword_Main");combat=model.GetComponent<BikeSwordCombat>();
            var renderers=model.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            focus=bounds.center;radius=Mathf.Max(1,bounds.extents.magnitude);
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.075f,.085f,.105f);preview.camera.fieldOfView=35;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=200;
            preview.lights[0].intensity=1;preview.lights[0].transform.rotation=Quaternion.Euler(35,-35,0);preview.lights[1].intensity=.6f;
            SelectAction(action);
        }
        public void SelectAction(int index) {
            action=Mathf.Clamp(index,0,Clips.Length-1);progress=0;
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Sword/Player_Sword_"+Clips[action]+".anim");
            var library=AssetDatabase.LoadAssetAtPath<SwordRuntimeClips>("Assets/Resources/SwordRuntimeClips.asset");clip=library.Resolve(source);
        }
        public void ShowAdjusted(bool value){adjustment=value;}
        private void Sample() {
            clip.SampleAnimation(model,progress*clip.length);extraTurn=0;
            if(adjustment && action<2) {
                var rotation=torso.rotation;
                // Comparison of the removed correction, for diagnosing old poses.
                var blade=sword.TransformPoint(new Vector3(0,-.0082f,0))-torso.position;blade.y=0;
                var direction=new Vector3(action==0 ? -3.5f : 3.5f,0,22).normalized;
                float contact=new SerializedObject(combat).FindProperty("strikeContactTime").floatValue;
                float turn=Vector3.SignedAngle(blade,direction,Vector3.up);
                float weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,contact,progress));
                torso.rotation=Quaternion.AngleAxis(turn*weight,Vector3.up)*torso.rotation;
                extraTurn=Quaternion.Angle(rotation,torso.rotation);
            }
        }
        private void SetCamera(float aspect) {
            preview.camera.aspect=aspect;
            preview.camera.transform.position=focus+Quaternion.Euler(pitch,yaw,0)*Vector3.back*(radius*2.6f*zoom);
            preview.camera.transform.LookAt(focus);
        }
        public Texture2D Capture(int index,float fraction,bool adjusted) {
            EnsureModel();SelectAction(index);progress=Mathf.Clamp01(fraction);adjustment=adjusted;Sample();
            preview.BeginStaticPreview(new Rect(0,0,960,720));SetCamera(960f/720);preview.Render();return preview.EndStaticPreview();
        }
        private void OnGUI() {
            EnsureModel();
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                int selected=EditorGUILayout.Popup(action,Titles,EditorStyles.toolbarPopup,GUILayout.Width(130));if(selected!=action)SelectAction(selected);
                if(GUILayout.Button(playing ? "Pause" : "Play",EditorStyles.toolbarButton,GUILayout.Width(55)))playing=!playing;
                if(GUILayout.Button("◀ frame",EditorStyles.toolbarButton,GUILayout.Width(65))){playing=false;progress=Mathf.Max(0,progress-1/(clip.frameRate*clip.length));}
                if(GUILayout.Button("frame ▶",EditorStyles.toolbarButton,GUILayout.Width(65))){playing=false;progress=Mathf.Min(1,progress+1/(clip.frameRate*clip.length));}
                GUILayout.FlexibleSpace();
                foreach(var angle in new[]{("Front",180f),("Left",90f),("Right",-90f),("Back",0f)})if(GUILayout.Button(angle.Item1,EditorStyles.toolbarButton,GUILayout.Width(48)))yaw=angle.Item2;
            }
            EditorGUI.BeginChangeCheck();float time=EditorGUILayout.Slider("Clip position",progress,0,1);if(EditorGUI.EndChangeCheck()){progress=time;playing=false;}
            speed=EditorGUILayout.Slider("Preview playback speed",speed,.1f,2);
            adjustment=EditorGUILayout.ToggleLeft("Compare old broken torso rotation (diagnostic only)",adjustment);
            Sample();
            EditorGUILayout.LabelField("Frame "+(Mathf.RoundToInt(progress*clip.length*clip.frameRate)+1)+" / "+(Mathf.RoundToInt(clip.length*clip.frameRate)+1)+"   ·   "+(progress*clip.length).ToString("0.000")+" s   ·   Extra torso turn: "+extraTurn.ToString("0.0")+"°",EditorStyles.miniLabel);
            Rect image=GUILayoutUtility.GetRect(100,100,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            var evt=Event.current;
            if(image.Contains(evt.mousePosition)) {
                if(evt.type==EventType.MouseDrag && evt.button==0){yaw+=evt.delta.x*.7f;pitch=Mathf.Clamp(pitch-evt.delta.y*.5f,-60,75);evt.Use();Repaint();}
                if(evt.type==EventType.ScrollWheel){zoom=Mathf.Clamp(zoom*(1+evt.delta.y*.04f),.25f,3);evt.Use();Repaint();}
            }
            if(evt.type==EventType.Repaint) {
                preview.BeginPreview(image,GUIStyle.none);SetCamera(image.width/Mathf.Max(1,image.height));preview.Render();var texture=preview.EndPreview();GUI.DrawTexture(image,texture,ScaleMode.StretchToFill,false);
            }
            EditorGUILayout.HelpBox("Drag to orbit; scroll to zoom. Scrub and step in either direction. Uncheck the comparison to see the authored clip without combat blending, camera shake or particles. Works without entering Play mode.",MessageType.Info);
        }
    }
}
