using System;
using System.IO;
using System.Reflection;
using CyberBiker.EditorTools;
using CyberBiker.Player;
using UnityEditor;
using UnityEngine;

public static class SwordClipInspectorChecks {
    public static void Run() {
        SwordClipInspector window=null;
        try {
            if(typeof(BikeSwordCombat).GetMethod("AimSlash",BindingFlags.Instance|BindingFlags.NonPublic)!=null)
                throw new Exception("Production still contains the forced torso twist.");
            window=ScriptableObject.CreateInstance<SwordClipInspector>();Directory.CreateDirectory("Logs/SwordPoseReview");
            for(int side=0;side<2;side++)foreach(bool old in new[]{false,true}) {
                var texture=window.Capture(side,.5882353f,old);
                File.WriteAllBytes("Logs/SwordPoseReview/"+(side==0 ? "Left" : "Right")+(old ? "OldTwist" : "Raw")+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                if(old && window.ExtraTorsoTurn<60)throw new Exception("Expected to reproduce the old large torso correction.");
                if(!old && window.ExtraTorsoTurn!=0)throw new Exception("Raw inspection must not apply combat corrections.");
            }
            foreach(float fraction in new[]{0f,.5f,1f}) {
                var texture=window.Capture(3,fraction,false);File.WriteAllBytes("Logs/SwordPoseReview/Sheathe"+fraction+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            }
            UnityEngine.Object.DestroyImmediate(window);window=null;
            File.WriteAllText("Logs/SwordPoseReview/InspectorChecks.txt","PASS: left/right raw clips render without runtime pose modification; old 60+ degree torso twists reproduced only by the diagnostic toggle; sheathe beginning/middle/end render; production twist removed.\n");
            SwordCombatChecks.Run();
        } catch(Exception error){if(window!=null)UnityEngine.Object.DestroyImmediate(window);Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
