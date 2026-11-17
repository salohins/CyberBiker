using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CyberBiker.Combat;
using CyberBiker.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CyberBiker.EditorTools {
    [Serializable]
    public sealed class CameraLabTuning {
        public const string RigPath="Assets/Resources/CameraLabRig.asset",PlayerPath="Assets/Art/Models/Player_Last.prefab";
        private const string BaselineKey="CyberBiker.CameraLab.SceneRig";
        public enum Scope { Rig,Launch,Finisher,Dodge }
        public enum Kind { Number,Toggle,Vector,Color }
        [Serializable] public sealed class Value {public string field;public Scope scope;public float number;public Vector3 vector;public Color color;}
        public sealed class Control {
            public Scope scope;public string field,label,group;public Kind kind;public float min,max;
            public Control(Scope scope,string field,string label,string group,float min=0,float max=1,Kind kind=Kind.Number){this.scope=scope;this.field=field;this.label=label;this.group=group;this.min=min;this.max=max;this.kind=kind;}
        }
        public List<Value> values=new List<Value>();
        public static readonly Control[] Controls=BuildControls();
        private static string Label(string field){var text=System.Text.RegularExpressions.Regex.Replace(field,"([a-z])([A-Z])","$1 $2");return char.ToUpperInvariant(text[0])+text.Substring(1);}
        private static Control[] BuildControls(){
            var list=new List<Control>();
            void Add(Scope s,string f,string label,string group,float min,float max)=>list.Add(new Control(s,f,label,group,min,max));
            void Toggle(Scope s,string f,string label,string group)=>list.Add(new Control(s,f,label,group,0,1,Kind.Toggle));
            void Vector(string f,string label,string group)=>list.Add(new Control(Scope.Rig,f,label,group,-50,50,Kind.Vector));
            Vector("targetOffset","Follow offset","Riding");Add(Scope.Rig,"ridingFov","FOV","Riding",15,120);Add(Scope.Rig,"ridingPitch","Pitch","Riding",-40,60);Add(Scope.Rig,"ridingYawOffset","Yaw","Riding",-90,90);Add(Scope.Rig,"ridingRoll","Roll","Riding",-30,30);
            const string combat="Combat riding";
            Vector("combatRiding.offset","Follow offset",combat);Add(Scope.Rig,"combatRiding.fov","FOV",combat,15,120);
            Add(Scope.Rig,"combatRiding.pitch","Pitch",combat,-40,60);Add(Scope.Rig,"combatRiding.yaw","Yaw",combat,-90,90);Add(Scope.Rig,"combatRiding.roll","Roll",combat,-30,30);
            Add(Scope.Rig,"combatRiding.shiftX","Steering X shift",combat,-10,10);Add(Scope.Rig,"combatRiding.shiftY","Steering height shift",combat,-10,10);Add(Scope.Rig,"combatRiding.forwardShift","Steering forward shift",combat,-10,10);Add(Scope.Rig,"combatRiding.steeringRoll","Steering camera lean",combat,-45,45);Add(Scope.Rig,"combatRiding.followResponse","Follow response",combat,.1f,20);
            Toggle(Scope.Rig,"combatRiding.enabled","Enable combat views","Combat switching");
            Add(Scope.Rig,"combatRiding.enemyRange","Enemy range (m)","Combat switching",1,150);Add(Scope.Rig,"combatRiding.exitPadding","Exit range padding (m)","Combat switching",0,40);Add(Scope.Rig,"combatRiding.returnDelay","Return delay (s)","Combat switching",0,5);Add(Scope.Rig,"combatRiding.blendTime","Blend time (s)","Combat switching",.05f,3);
            Vector("aimOffset","Follow / shoulder offset","Cruising aim");Add(Scope.Rig,"aimFov","FOV","Cruising aim",15,120);Add(Scope.Rig,"aimPitch","Pitch","Cruising aim",-40,60);Add(Scope.Rig,"aimYawOffset","Yaw","Cruising aim",-90,90);Add(Scope.Rig,"aimRoll","Roll","Cruising aim",-30,30);
            Add(Scope.Rig,"aimFollowResponse","Follow response","Cruising aim",.1f,20);
            foreach(var f in new[]{"aimXShiftOffset","aimYShiftOffset","aimForwardShiftOffset"})Add(Scope.Rig,f,Label(f),"Cruising aim",-10,10);
            Add(Scope.Rig,"aimAngleShiftOffset","Steering camera lean","Cruising aim",-45,45);
            const string combatAim="Combat aim";
            Vector("combatAim.offset","Follow / shoulder offset",combatAim);Add(Scope.Rig,"combatAim.fov","FOV",combatAim,15,120);
            Add(Scope.Rig,"combatAim.pitch","Pitch",combatAim,-40,60);Add(Scope.Rig,"combatAim.yaw","Yaw",combatAim,-90,90);Add(Scope.Rig,"combatAim.roll","Roll",combatAim,-30,30);
            Add(Scope.Rig,"combatAim.shiftX","Steering X shift",combatAim,-10,10);Add(Scope.Rig,"combatAim.shiftY","Steering height shift",combatAim,-10,10);Add(Scope.Rig,"combatAim.forwardShift","Steering forward shift",combatAim,-10,10);Add(Scope.Rig,"combatAim.steeringRoll","Steering camera lean",combatAim,-45,45);Add(Scope.Rig,"combatAim.followResponse","Follow response",combatAim,.1f,20);
            Toggle(Scope.Rig,"useAirborneOffset","Independent airborne offset","Airborne");Vector("airborneOffset","Follow offset","Airborne");Add(Scope.Rig,"airborneFov","FOV","Airborne",15,120);Add(Scope.Rig,"airbornePitch","Pitch","Airborne",-40,60);Add(Scope.Rig,"airborneYawOffset","Yaw","Airborne",-90,90);Add(Scope.Rig,"airborneRoll","Roll","Airborne",-30,30);
            Add(Scope.Rig,"smoothness","Follow response","Steering & follow",.1f,20);Add(Scope.Rig,"cameraYangle","Shared yaw","Steering & follow",-90,90);
            foreach(var f in new[]{"xShiftOffset","yShiftOffset"})Add(Scope.Rig,f,Label(f),"Steering & follow",-10,10);
            foreach(var f in new[]{"angleShiftOffset"})Add(Scope.Rig,f,Label(f),"Steering & follow",-45,45);
            Toggle(Scope.Rig,"dynamicAimShoulder","Dynamic shoulders","Shared aim shoulders & target");Toggle(Scope.Rig,"targetAwareAimShoulder","Target-aware shoulders","Shared aim shoulders & target");Add(Scope.Rig,"aimShoulderSwitchDistance","Road switch distance","Shared aim shoulders & target",.1f,8);Add(Scope.Rig,"aimShoulderSwitchSpeed","Shoulder glide response","Shared aim shoulders & target",.5f,15);Add(Scope.Rig,"aimTargetShoulderDeadZone","Target dead zone","Shared aim shoulders & target",.1f,8);
            Toggle(Scope.Rig,"aimFraming.enabled","Target framing","Shared aim shoulders & target");Add(Scope.Rig,"aimFraming.targetLookWeight","Look weight","Shared aim shoulders & target",0,1);Add(Scope.Rig,"aimFraming.maximumYaw","Maximum target yaw","Shared aim shoulders & target",0,25);Add(Scope.Rig,"aimFraming.maximumPitch","Maximum target pitch","Shared aim shoulders & target",0,20);Add(Scope.Rig,"aimFraming.smoothTime","Framing blend time","Shared aim shoulders & target",.05f,1);
            Toggle(Scope.Rig,"aimFraming.keepPistolVisible","Keep pistol visible","Shared pistol & visibility");Add(Scope.Rig,"aimFraming.maximumSideCorrection","Max pistol side correction","Shared pistol & visibility",0,4);Add(Scope.Rig,"aimFraming.maximumHeightCorrection","Max pistol height correction","Shared pistol & visibility",0,2);Add(Scope.Rig,"aimFraming.screenMargin","Screen margin","Shared pistol & visibility",0,.25f);Add(Scope.Rig,"aimFraming.bodyClearance","Body clearance","Shared pistol & visibility",0,.2f);
            Toggle(Scope.Rig,"riderVisibility.enabled","Fade obstructing rider","Shared pistol & visibility");Toggle(Scope.Rig,"riderVisibility.fadeNearCameraCenter","Fade at camera centre","Shared pistol & visibility");Add(Scope.Rig,"riderVisibility.obstructedOpacity","Obstructed opacity","Shared pistol & visibility",0,.8f);Add(Scope.Rig,"riderVisibility.fadeOutTime","Fade out time","Shared pistol & visibility",.03f,.75f);Add(Scope.Rig,"riderVisibility.fadeInTime","Fade in time","Shared pistol & visibility",.05f,1);
            Add(Scope.Rig,"speedShakeStartSpeed","Vibration start speed","Speed vibration",0,100);Add(Scope.Rig,"speedShakeFullSpeed","Vibration full speed","Speed vibration",1,200);Add(Scope.Rig,"speedShakePosition","Position","Speed vibration",0,.2f);Add(Scope.Rig,"speedShakeRotation","Rotation","Speed vibration",0,2);Add(Scope.Rig,"speedShakeFrequency","Frequency","Speed vibration",.1f,30);
            foreach(var f in new[]{"passShakePosition","passShakeRotation","passShakeDuration","passShakeFrequency","carImpactPosition","carImpactRotation","carImpactDuration","carImpactFrequency"}){
                float max=f.EndsWith("Position") ? .6f : f.EndsWith("Duration") ? 2 : f.EndsWith("Frequency") ? 25 : 8;Add(Scope.Rig,f,Label(f),"Passing & collision",f.EndsWith("Duration") || f.EndsWith("Frequency") ? .05f : 0,max);
            }
            Add(Scope.Launch,"fovIncrease","Launch FOV increase","Launch / nitro",-20,25);
            list.Add(new Control(Scope.Launch,"cameraViewOffset","View position offset","Launch / nitro",-50,50,Kind.Vector));list.Add(new Control(Scope.Launch,"cameraViewEuler","View rotation","Launch / nitro",-90,90,Kind.Vector));
            foreach(var f in new[]{"ignitionCameraKick","ignitionCameraPitch","ignitionKickDuration","ignitionShakePosition","ignitionShakeRotation","ignitionShakeDuration","ignitionShakeFrequency"}){
                float max=f.EndsWith("Frequency") ? 25 : f.EndsWith("Duration") ? .7f : f.EndsWith("Pitch") ? 5 : f.EndsWith("Rotation") ? 3 : .5f;Add(Scope.Launch,f,Label(f),"Launch / nitro",f.EndsWith("Duration") ? .1f : f.EndsWith("Frequency") ? 6 : 0,max);
            }
            Add(Scope.Launch,"borderGlow","Border glow","Launch / nitro",0,1);Add(Scope.Launch,"borderWidth","Border width","Launch / nitro",.01f,.15f);list.Add(new Control(Scope.Launch,"borderColor","Border color","Launch / nitro",0,1,Kind.Color));
            list.Add(new Control(Scope.Finisher,"cameraViewOffset","View position offset","Sword finisher",-50,50,Kind.Vector));list.Add(new Control(Scope.Finisher,"cameraViewEuler","View rotation","Sword finisher",-90,90,Kind.Vector));
            foreach(var f in new[]{"swingZoom","hitZoom","cameraDolly","cameraSideShift","hitCameraSideShift","hitCameraSideMoveTime","hitCameraSideReturnTime","aftermathPullback","aftermathCameraLift","aftermathCameraDuration","hitCameraLookTurn","targetFramingTurn","cameraRoll","hitShake","hitCameraDuration"}){
                float max=f.Contains("Zoom") ? 25 : f.Contains("Turn") ? 20 : f=="cameraRoll" ? 6 : f=="hitShake" ? .3f : f.Contains("Time") || f.Contains("Duration") ? 3 : 4;Add(Scope.Finisher,f,Label(f),"Sword finisher",f=="hitZoom" ? -20 : f.Contains("Time") || f.Contains("Duration") ? .05f : 0,max);
            }
            Toggle(Scope.Dodge,"cameraMotion","Camera movement","Close dodge");
            list.Add(new Control(Scope.Dodge,"viewOffset","View position offset","Close dodge",-50,50,Kind.Vector));list.Add(new Control(Scope.Dodge,"viewEuler","View rotation","Close dodge",-90,90,Kind.Vector));
            foreach(var f in new[]{"cameraBlendIn","cameraHold","cameraReturn","zoomInFov","sideways","dolly","lift","lookTurn","roll","shake","shakeDuration"})Add(Scope.Dodge,f,Label(f),"Close dodge",f=="lift" ? -.5f : f=="cameraReturn" || f=="shakeDuration" ? .01f : 0,f=="zoomInFov" ? 25 : f=="lookTurn" ? 15 : f=="roll" ? 6 : f=="shake" || f=="dolly" || f=="lift" ? 1 : 2);
            return list.ToArray();
        }
        public Value Get(Scope scope,string field)=>values.Find(v=>v.scope==scope && v.field==field);
        public CameraLabTuning Copy()=>JsonUtility.FromJson<CameraLabTuning>(JsonUtility.ToJson(this));
        private static UnityEngine.Object Owner(Scope scope,CameraController rig,GameObject player,CloseDodgeProfile dodge)=>scope==Scope.Rig ? rig : scope==Scope.Launch ? player.GetComponent<BikeLaunchEffects>() : scope==Scope.Finisher ? player.GetComponent<SwordFinisherEffects>() : dodge;
        public static CameraLabTuning Capture(CameraController rig,GameObject player,CloseDodgeProfile dodge){
            rig.InitializeCombatCameraDefaults();rig.InitializeCombatAimDefaults();
            var result=new CameraLabTuning();foreach(var c in Controls){using(var data=new SerializedObject(Owner(c.scope,rig,player,dodge))){var p=data.FindProperty((c.scope==Scope.Dodge ? "settings." : "")+c.field);if(p==null)throw new InvalidOperationException("Camera control missing: "+c.field);result.values.Add(new Value{scope=c.scope,field=c.field,number=c.kind==Kind.Toggle ? (p.boolValue ? 1 : 0) : c.kind==Kind.Number ? p.floatValue : 0,vector=c.kind==Kind.Vector ? p.vector3Value : default,color=c.kind==Kind.Color ? p.colorValue : default});}}
            return result;
        }
        public static CameraLabTuning ReadSavedDefaults(bool sceneDefaults=false){
            CameraLabTuning result;var player=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);var dodge=AssetDatabase.LoadAssetAtPath<CloseDodgeProfile>(DodgeLabWindow.ProfilePath);
            if(EditorApplication.isPlaying){result=Capture(UnityEngine.Object.FindAnyObjectByType<CameraController>(),player,dodge);string cached=SessionState.GetString(BaselineKey,"");if(!string.IsNullOrEmpty(cached)){var baseline=JsonUtility.FromJson<CameraLabTuning>(cached);baseline.Normalize();foreach(var c in Controls)if(c.scope==Scope.Rig)CopyValue(baseline.Get(c.scope,c.field),result.Get(c.scope,c.field));}}
            else {var scene=EditorSceneManager.OpenPreviewScene("Assets/Level/Scenes/Level.unity");try{CameraController rig=null;foreach(var root in scene.GetRootGameObjects())if(rig==null)rig=root.GetComponentInChildren<CameraController>(true);if(rig==null)throw new InvalidOperationException("Saved Level has no camera rig");result=Capture(rig,player,dodge);SessionState.SetString(BaselineKey,JsonUtility.ToJson(result));}finally{EditorSceneManager.ClosePreviewScene(scene);}}
            var saved=AssetDatabase.LoadAssetAtPath<CameraLabRigProfile>(RigPath);
            if(!sceneDefaults && saved!=null){var root=new GameObject("Camera Lab saved rig reader");root.SetActive(false);root.hideFlags=HideFlags.HideAndDontSave;try{var rig=root.AddComponent<CameraController>();result.ApplyScope(Scope.Rig,rig);saved.Apply(rig);var patch=Capture(rig,player,dodge);foreach(var c in Controls)if(c.scope==Scope.Rig)CopyValue(patch.Get(c.scope,c.field),result.Get(c.scope,c.field));}finally{UnityEngine.Object.DestroyImmediate(root);}}
            if(result.Get(Scope.Rig,"useAirborneOffset").number<.5f)result.Get(Scope.Rig,"airborneOffset").vector=result.Get(Scope.Rig,"targetOffset").vector;
            result.Normalize();return result;
        }
        private static void CopyValue(Value a,Value b){b.number=a.number;b.vector=a.vector;b.color=a.color;}
        private void UpgradeCombatControls(){
            if(values==null)throw new ArgumentException("Camera preset has no values");
            foreach(var c in Controls){
                if(c.scope!=Scope.Rig || !c.field.StartsWith("combatRiding.") || Get(c.scope,c.field)!=null)continue;
                var v=new Value{scope=c.scope,field=c.field};string field=c.field.Substring("combatRiding.".Length);
                string source=field=="offset" ? "targetOffset" : field=="fov" ? "ridingFov" : field=="pitch" ? "ridingPitch" : field=="yaw" ? "ridingYawOffset" : field=="roll" ? "ridingRoll" : field=="shiftX" ? "xShiftOffset" : field=="shiftY" ? "yShiftOffset" : field=="steeringRoll" ? "angleShiftOffset" : field=="followResponse" ? "smoothness" : null;
                if(source!=null){var old=Get(Scope.Rig,source);if(old==null)throw new ArgumentException("Camera preset is missing "+source);CopyValue(old,v);}
                else v.number=field=="enemyRange" ? 60 : field=="exitPadding" ? 10 : field=="blendTime" ? .8f : field=="forwardShift" ? 2 : 1;
                values.Add(v);
            }
        }
        private void UpgradeAimControls(){
            if(Get(Scope.Rig,"aimFollowResponse")==null){var follow=Get(Scope.Rig,"smoothness");if(follow==null)throw new ArgumentException("Camera preset is missing smoothness");values.Add(new Value{scope=Scope.Rig,field="aimFollowResponse",number=follow.number});}
            foreach(var c in Controls){
                if(c.scope!=Scope.Rig || !c.field.StartsWith("combatAim.") || Get(c.scope,c.field)!=null)continue;
                string field=c.field.Substring("combatAim.".Length);
                string source=field=="offset" ? "aimOffset" : field=="fov" ? "aimFov" : field=="pitch" ? "aimPitch" : field=="yaw" ? "aimYawOffset" : field=="roll" ? "aimRoll" : field=="shiftX" ? "aimXShiftOffset" : field=="shiftY" ? "aimYShiftOffset" : field=="forwardShift" ? "aimForwardShiftOffset" : field=="steeringRoll" ? "aimAngleShiftOffset" : "aimFollowResponse";
                var old=Get(Scope.Rig,source);if(old==null)throw new ArgumentException("Camera preset is missing "+source);
                var value=new Value{scope=c.scope,field=c.field};CopyValue(old,value);values.Add(value);
            }
        }
        public void Normalize(){UpgradeCombatControls();UpgradeAimControls();foreach(var c in Controls){var v=Get(c.scope,c.field);if(v==null)throw new ArgumentException("Camera preset is missing "+c.field);if(c.kind==Kind.Number || c.kind==Kind.Toggle){if(float.IsNaN(v.number) || float.IsInfinity(v.number))throw new ArgumentException("Invalid camera value: "+c.field);if(c.kind==Kind.Toggle)v.number=v.number>=.5f ? 1 : 0;}else if(c.kind==Kind.Vector){for(int i=0;i<3;i++){if(float.IsNaN(v.vector[i]) || float.IsInfinity(v.vector[i]))throw new ArgumentException("Invalid camera offset");}}else {for(int i=0;i<4;i++)if(float.IsNaN(v.color[i]) || float.IsInfinity(v.color[i]))throw new ArgumentException("Invalid camera color");}}}
        public void ApplyScope(Scope scope,UnityEngine.Object owner){using(var data=new SerializedObject(owner)){foreach(var c in Controls)if(c.scope==scope){var p=data.FindProperty((scope==Scope.Dodge ? "settings." : "")+c.field);var v=Get(scope,c.field);if(c.kind==Kind.Vector)p.vector3Value=v.vector;else if(c.kind==Kind.Color)p.colorValue=v.color;else if(c.kind==Kind.Toggle)p.boolValue=v.number>=.5f;else p.floatValue=v.number;}data.ApplyModifiedPropertiesWithoutUndo();}}
        public string RigJson(){
            var tree=new Dictionary<string,object>();foreach(var c in Controls)if(c.scope==Scope.Rig){var v=Get(c.scope,c.field);var paths=c.field.Split('.');var node=tree;for(int i=0;i<paths.Length-1;i++){if(!node.TryGetValue(paths[i],out var nested)){nested=new Dictionary<string,object>();node.Add(paths[i],nested);}node=(Dictionary<string,object>)nested;}node[paths[paths.Length-1]]=c.kind==Kind.Toggle ? (v.number>=.5f ? "true" : "false") : c.kind==Kind.Vector ? "{\"x\":"+Number(v.vector.x)+",\"y\":"+Number(v.vector.y)+",\"z\":"+Number(v.vector.z)+"}" : Number(v.number);}
            ((Dictionary<string,object>)tree["combatRiding"])["configured"]="true";
            ((Dictionary<string,object>)tree["combatAim"])["configured"]="true";
            return WriteJson(tree);
        }
        private static string Number(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
        private static string WriteJson(Dictionary<string,object> node){var b=new StringBuilder("{");foreach(var item in node){if(b.Length>1)b.Append(',');b.Append('"').Append(item.Key).Append("\":").Append(item.Value is Dictionary<string,object> nested ? WriteJson(nested) : (string)item.Value);}return b.Append('}').ToString();}
        public void SaveForGame(string rigPath=RigPath,string playerPath=PlayerPath,string dodgePath=DodgeLabWindow.ProfilePath){
            Normalize();var rig=AssetDatabase.LoadAssetAtPath<CameraLabRigProfile>(rigPath);if(rig==null){rig=ScriptableObject.CreateInstance<CameraLabRigProfile>();AssetDatabase.CreateAsset(rig,rigPath);}Undo.RecordObject(rig,"Save camera rig");rig.rigJson=RigJson();EditorUtility.SetDirty(rig);AssetDatabase.SaveAssetIfDirty(rig);
            var root=PrefabUtility.LoadPrefabContents(playerPath);try{ApplyScope(Scope.Launch,root.GetComponent<BikeLaunchEffects>());ApplyScope(Scope.Finisher,root.GetComponent<SwordFinisherEffects>());PrefabUtility.SaveAsPrefabAsset(root,playerPath);}finally{PrefabUtility.UnloadPrefabContents(root);}
            var dodge=AssetDatabase.LoadAssetAtPath<CloseDodgeProfile>(dodgePath);Undo.RecordObject(dodge,"Save dodge camera");ApplyScope(Scope.Dodge,dodge);EditorUtility.SetDirty(dodge);AssetDatabase.SaveAssetIfDirty(dodge);
        }
    }
}
