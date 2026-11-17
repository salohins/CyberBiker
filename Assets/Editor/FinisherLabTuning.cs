using System;
using System.Collections.Generic;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;

namespace CyberBiker.EditorTools {
    [Serializable]
    public sealed class FinisherLabTuning {
        public const string PlayerPrefab = "Assets/Art/Models/Player_Last.prefab";
        [Serializable] public sealed class Value { public string field; public float number; public Color color; }
        public int version = 2;
        public List<Value> values = new List<Value>();
        public sealed class Control {
            public string field, label, group, hint;
            public float min, max;
            public bool effects, color, toggle;
            public Control(string field, string label, string group, float min, float max, bool effects = false, string hint = "") {
                this.field=field;this.label=label;this.group=group;this.min=min;this.max=max;this.effects=effects;this.hint=hint;
            }
        }
        public static readonly Control[] Controls = {
            new Control("drawPlaybackSpeed","Draw speed ×","Animation",.25f,6, hint:"1 is the authored clip speed. The current quick draw uses 4.5×."),
            new Control("strikePlaybackSpeed","Swing speed ×","Animation",.1f,3),
            new Control("sliceReturnPlaybackSpeed","Sheathe speed ×","Animation",.25f,6),
            new Control("poseBlendTime","Blend into draw (s)","Animation",.01f,1),
            new Control("strikeBlendTime","Draw → strike blend (s)","Animation",.01f,.5f),
            new Control("sheatheBlendTime","Strike → sheathe blend (s)","Animation",.01f,1),
            new Control("sliceReturnBlendTime","Sheathe → ride blend (s)","Animation",.01f,1),
            new Control("contactPoseHold","Hold impact pose (s)","Animation",0,.5f),
            new Control("strikeContactTime","Contact point in swing","Animation",.1f,.8f, hint:"Fraction of the animation where damage and impact occur. Replay to check blade alignment."),
            new Control("strikeWindowEnd","Contact window end","Animation",.5f,.95f),
            new Control("chainPickupDelay","Delay between finishers (s)","Animation",0,1),
            new Control("ridingRecoveryTime","End launch → ride blend (s)","Animation",.02f,2),
            new Control("aimReturnPlaybackSpeed","Sword → pistol return speed ×","Animation",.25f,6),
            new Control("aimReturnBlendTime","Sword → pistol blend (s)","Animation",.01f,.5f),
            new Control("dashDuration","Dash duration (s)","Dash & slow motion",.15f,1),
            new Control("trafficRecoveryGrace","Traffic recovery grace (s)","Dash & slow motion",0,3, hint:"Cars are ignored through the animation and world slow motion, then for this many real seconds. Overlapping cars stay ignored until clear."),
            new Control("slowMotionEnabled","Enable world slow motion","Dash & slow motion",0,1,true) {toggle=true},
            new Control("dashTimeScale","World speed during dash","Dash & slow motion",.15f,1,true),
            new Control("dashSlowDuration","Dash slow motion hold (s)","Dash & slow motion",.1f,2,true),
            new Control("hitTimeScale","World speed at impact","Dash & slow motion",.1f,1,true),
            new Control("hitSlowDuration","Impact slow motion hold (s)","Dash & slow motion",0,2,true),
            new Control("explosionTimeScale","World speed in explosion","Dash & slow motion",.15f,1,true),
            new Control("explosionSlowDuration","Explosion slow motion hold (s)","Dash & slow motion",.1f,3,true),
            new Control("slowMotionRecovery","Return to full speed (s)","Dash & slow motion",.1f,2,true),
            new Control("enemyExplosionDelay","Delay before explosion (s)","Dash & slow motion",.03f,.5f),
            new Control("dashCameraEnabled","Enable dash camera","Dash camera",0,1,true){toggle=true},
            new Control("dashCameraFovOffset","FOV offset (+ = wider)","Dash camera",-25,25,true),
            new Control("dashCameraPosition.x","Position X — sideways","Dash camera",-5,5,true),
            new Control("dashCameraPosition.y","Position Y — height","Dash camera",-5,5,true),
            new Control("dashCameraPosition.z","Position Z — forward","Dash camera",-10,10,true),
            new Control("dashCameraRotation.x","Rotation — pitch","Dash camera",-45,45,true),
            new Control("dashCameraRotation.y","Rotation — yaw","Dash camera",-45,45,true),
            new Control("dashCameraRotation.z","Rotation — roll","Dash camera",-30,30,true),
            new Control("dashCameraMirrorSide","Mirror X/yaw/roll for side","Dash camera",0,1,true){toggle=true},
            new Control("dashCameraTargetTurn","Target framing (degrees)","Dash camera",0,20,true,"Maximum turn toward the enemy. Set 0 to use only your chosen rotation."),
            new Control("dashCameraBlendIn","Blend into dash (real s)","Dash camera",.01f,1,true),
            new Control("dashCameraBlendOut","Blend out of dash (real s)","Dash camera",.01f,1.5f,true),
            new Control("swingZoom","Swing camera zoom","Swing & impact camera",0,20,true),
            new Control("hitZoom","Hit camera FOV offset","Swing & impact camera",-20,25,true, "Negative widens the view. Positive zooms in."),
            new Control("cameraDolly","Swing camera travel","Swing & impact camera",0,1,true),
            new Control("cameraSideShift","Swing sideways travel","Swing & impact camera",0,2,true),
            new Control("hitCameraSideShift","Hit sideways travel","Swing & impact camera",0,3,true),
            new Control("hitCameraSideMoveTime","Side move blend (s)","Swing & impact camera",.08f,.6f,true),
            new Control("hitCameraSideReturnTime","Side return blend (s)","Swing & impact camera",.15f,1.2f,true),
            new Control("aftermathPullback","Explosion pullback","Swing & impact camera",0,3,true),
            new Control("aftermathCameraLift","Explosion camera lift","Swing & impact camera",0,1,true),
            new Control("aftermathCameraDuration","Explosion framing hold (s)","Swing & impact camera",.5f,3,true),
            new Control("hitCameraLookTurn","Look toward impact (degrees)","Swing & impact camera",0,20,true),
            new Control("cameraRoll","Swing camera roll (degrees)","Swing & impact camera",0,6,true),
            new Control("hitShake","Impact shake strength","Swing & impact camera",0,.3f,true),
            new Control("hitCameraDuration","Impact shake duration (s)","Swing & impact camera",.1f,1,true),
            new Control("hitScale","Slice particle scale","Particles & flash",.2f,3,true),
            new Control("trailLifetime","Sword trail lifetime (s)","Particles & flash",.05f,.5f,true),
            new Control("trailColor","Sword trail color (HDR)","Particles & flash",0,0,true) {color=true},
            new Control("windOpacity","Dash wind opacity","Particles & flash",.1f,1,true),
            new Control("windFlowSpeed","Dash wind speed","Particles & flash",1,6,true),
            new Control("smokeCloudSize","Smoke cloud size","Particles & flash",.6f,2,true),
            new Control("smokeDensity","Smoke density","Particles & flash",60,220,true),
            new Control("smokeWakeDuration","Smoke wake duration (s)","Particles & flash",.5f,3,true),
            new Control("impactDarkening","Impact darkening","Particles & flash",0,.8f,true),
            new Control("impactDesaturation","Impact desaturation","Particles & flash",0,1,true),
            new Control("slashFlash","Slash flash strength","Particles & flash",0,1,true),
            new Control("flashDuration","Flash duration (s)","Particles & flash",.02f,.1f,true),
            new Control("contrastDuration","Contrast duration (s)","Particles & flash",.1f,.6f,true),
            new Control("swishVolume","Sword swish volume","Sound",0,2,true),
            new Control("metalHitVolume","Metal impact volume","Sound",0,2,true),
            new Control("lowImpactVolume","Low impact volume","Sound",0,2,true),
            new Control("explosionBlastVolume","Explosion blast volume","Sound",0,2,true),
            new Control("explosionBassVolume","Explosion bass volume","Sound",0,2,true),
            new Control("explosionDebrisVolume","Explosion debris volume","Sound",0,1,true),
            new Control("impactDuckDb","Engine / wind duck (dB)","Sound",0,15,true),
            new Control("impactDuckRecovery","Engine / wind return (s)","Sound",.1f,.7f,true)
        };
        public Value Get(string field) => values.Find(value => value.field == field);
        public static FinisherLabTuning ImportJson(string json){
            var imported=JsonUtility.FromJson<FinisherLabTuning>(json);
            if(imported==null || imported.values==null || imported.version<1 || imported.version>2)throw new ArgumentException("Unsupported finisher preset.");
            imported.Normalize();return imported;
        }
        public FinisherLabTuning Copy() => JsonUtility.FromJson<FinisherLabTuning>(JsonUtility.ToJson(this));
        public static FinisherLabTuning ReadSavedDefaults() {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            return Capture(prefab.GetComponent<BikeSwordCombat>(),prefab.GetComponent<SwordFinisherEffects>());
        }
        public static FinisherLabTuning Capture(BikeSwordCombat sword, SwordFinisherEffects effects) {
            var result=new FinisherLabTuning();
            var combat=new SerializedObject(sword);var fx=new SerializedObject(effects);
            foreach(var control in Controls) {
                var property=(control.effects ? fx : combat).FindProperty(control.field);
                if(property==null)throw new InvalidOperationException("Missing finisher control: "+control.field);
                result.values.Add(new Value{field=control.field,number=control.color ? 0 : control.toggle ? (property.boolValue ? 1 : 0) : property.floatValue,color=control.color ? property.colorValue : default});
            }
            return result;
        }
        public void Normalize() {
            // Old drafts used quarter-strength shared swing offsets during draw.
            // Migrate only absent dash controls, retaining every existing edit.
            AddDashDefault("dashCameraEnabled",1);AddDashDefault("dashCameraFovOffset",1);
            AddDashDefault("dashCameraPosition.x",(Get("cameraSideShift")?.number ?? .8f)*.25f);
            AddDashDefault("dashCameraPosition.y",0);AddDashDefault("dashCameraPosition.z",-(Get("cameraDolly")?.number ?? .2f)*.25f);
            AddDashDefault("dashCameraRotation.x",0);AddDashDefault("dashCameraRotation.y",0);AddDashDefault("dashCameraRotation.z",(Get("cameraRoll")?.number ?? 2)*.25f);
            AddDashDefault("dashCameraMirrorSide",1);AddDashDefault("dashCameraTargetTurn",1.25f);AddDashDefault("dashCameraBlendIn",.15f);AddDashDefault("dashCameraBlendOut",.2f);version=Mathf.Max(2,version);
            // Older Lab presets predate the master slow-motion switch.
            if(Get("slowMotionEnabled")==null)values.Add(new Value{field="slowMotionEnabled",number=1});
            if(Get("trafficRecoveryGrace")==null)values.Add(new Value{field="trafficRecoveryGrace",number=.8f});
            foreach(var control in Controls) {
                var value=Get(control.field);
                if(value==null)throw new ArgumentException("Incomplete finisher preset: "+control.field);
                if(!control.color) {
                    if(float.IsNaN(value.number) || float.IsInfinity(value.number))throw new ArgumentException("Invalid finisher value: "+control.field);
                    value.number=Mathf.Clamp(value.number,control.min,control.max);
                } else if(float.IsNaN(value.color.r) || float.IsInfinity(value.color.r) || float.IsNaN(value.color.g) || float.IsInfinity(value.color.g)
                    || float.IsNaN(value.color.b) || float.IsInfinity(value.color.b) || float.IsNaN(value.color.a) || float.IsInfinity(value.color.a)) {
                    throw new ArgumentException("Invalid finisher color: "+control.field);
                }
            }
            Get("strikeWindowEnd").number=Mathf.Max(Get("strikeWindowEnd").number,Get("strikeContactTime").number);
        }
        private void AddDashDefault(string field,float number){if(Get(field)==null)values.Add(new Value{field=field,number=number});}
        public void Apply(BikeSwordCombat sword, SwordFinisherEffects effects) {
            Normalize();var combat=new SerializedObject(sword);var fx=new SerializedObject(effects);
            foreach(var control in Controls) {
                var property=(control.effects ? fx : combat).FindProperty(control.field);var value=Get(control.field);
                if(control.color)property.colorValue=value.color;else if(control.toggle)property.boolValue=value.number>=.5f;else property.floatValue=value.number;
            }
            combat.ApplyModifiedPropertiesWithoutUndo();fx.ApplyModifiedPropertiesWithoutUndo();
            effects.SlowMotionEnabled=Get("slowMotionEnabled").number>=.5f;
        }
        public void SaveForGame(string prefabPath=PlayerPrefab) {
            Normalize();var root=PrefabUtility.LoadPrefabContents(prefabPath);
            try {
                Apply(root.GetComponent<BikeSwordCombat>(),root.GetComponent<SwordFinisherEffects>());
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
