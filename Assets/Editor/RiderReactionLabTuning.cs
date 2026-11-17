using System;
using System.Collections.Generic;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;

namespace CyberBiker.EditorTools {
    [Serializable]
    public sealed class RiderReactionLabTuning {
        public const string PlayerPath="Assets/Art/Models/Player_Last.prefab";
        [Serializable] public sealed class Value {public string field;public float number;}
        public sealed class Control {
            public string field,label,group;public float min,max;public bool toggle;
            public Control(string f,string label,string group,float min,float max,bool toggle=false){field=f;this.label=label;this.group=group;this.min=min;this.max=max;this.toggle=toggle;}
        }
        public List<Value> values=new List<Value>();
        public static readonly Control[] Controls={
            new Control("reactionsEnabled","Enable reactions","General",0,1,true),
            new Control("headYawLimit","Head yaw limit","Head",10,75),
            new Control("ridingHeadPitchLimit","Riding enemy pitch change","Head",0,15),
            new Control("headResponse","General look smoothing","Head",.08f,.6f),
            new Control("carNeckShare","Neck share","Car head & neck",0,.65f),
            new Control("carPitchRange","Car pitch change limit","Car head & neck",5,35),
            new Control("carLookResponse","Turn-in smoothing","Car head & neck",.05f,.4f),
            new Control("carReturnResponse","Return smoothing","Car head & neck",.15f,.8f),
            new Control("glanceDuration","Glance hold (s)","Car timing & notice",.2f,1.5f),
            new Control("glanceCooldown","Glance cooldown (s)","Car timing & notice",.5f,5),
            new Control("carAnticipationTime","Anticipation (s)","Car timing & notice",.3f,2),
            new Control("carNoticeRange","Minimum notice range (m)","Car timing & notice",5,35),
            new Control("carSideRange","Side notice range (m)","Car timing & notice",2,12),
            new Control("bodyTurnShare","Shoulder follow","Body",0,.15f),
            new Control("passBodyLean","Pass lean (degrees)","Body",0,5),
            new Control("passRecovery","Pass recovery (s)","Body",.25f,1.2f),
            new Control("breathingAmount","Breathing","Idle movement",0,1.5f),
            new Control("idleSway","Idle sway","Idle movement",0,1),
            new Control("enemyNoticeRange","Idle enemy notice range (m)","Enemy attention",10,80),
            new Control("shootingEnemyRange","Visible shooter range (m)","Enemy attention",30,150),
            new Control("combatHeadResponse","Combat head smoothing","Enemy attention",.03f,.2f),
            new Control("aimingHeadPitchLimit","Combat pitch limit","Enemy attention",5,35),
            new Control("followAimTarget","Fallback to aimed target","Enemy attention",0,1,true)
        };
        public Value Get(string field)=>values.Find(v=>v.field==field);
        public RiderReactionLabTuning Copy()=>JsonUtility.FromJson<RiderReactionLabTuning>(JsonUtility.ToJson(this));
        public static RiderReactionLabTuning Capture(RiderLife life){
            if(life==null)throw new InvalidOperationException("The player prefab has no Rider Life component.");
            var result=new RiderReactionLabTuning();using(var data=new SerializedObject(life))foreach(var c in Controls){var p=data.FindProperty(c.field);if(p==null)throw new InvalidOperationException("Missing reaction control: "+c.field);result.values.Add(new Value{field=c.field,number=c.toggle ? (p.boolValue ? 1 : 0) : p.floatValue});}return result;
        }
        public static RiderReactionLabTuning ReadSaved()=>Capture(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath).GetComponent<RiderLife>());
        public void Validate(){foreach(var c in Controls){var value=Get(c.field);if(value==null || float.IsNaN(value.number) || float.IsInfinity(value.number))throw new ArgumentException("Invalid or missing reaction control: "+c.label);}}
        public void Apply(RiderLife life){Validate();using(var data=new SerializedObject(life)){foreach(var c in Controls){var p=data.FindProperty(c.field);if(c.toggle)p.boolValue=Get(c.field).number>=.5f;else p.floatValue=Get(c.field).number;}data.ApplyModifiedPropertiesWithoutUndo();}}
        public void SaveForGame(string path=PlayerPath){
            Validate();var root=PrefabUtility.LoadPrefabContents(path);try{Apply(root.GetComponent<RiderLife>());PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
