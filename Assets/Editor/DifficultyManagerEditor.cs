using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DifficultyManager))]
public sealed class DifficultyManagerEditor : Editor {
    public override bool RequiresConstantRepaint() => Application.isPlaying;
    public override void OnInspectorGUI() {
        DrawDefaultInspector();
        var manager=(DifficultyManager)target;
        if(Application.isPlaying && manager.IsConstantCombat){
            EditorGUILayout.HelpBox("Constant Combat: holds the assigned combat difficulty and continuously replaces defeated riders. No progression stages or wave cooldown.",MessageType.Info);
            EditorGUILayout.LabelField("Current difficulty",manager.currentDifficulty!=null ? manager.currentDifficulty.name : "None");
            return;
        }
        if(manager.difficultySections==null || manager.difficultySections.Length==0)return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Run timeline",EditorStyles.boldLabel);
        float start=0;
        for(int i=0;i<manager.difficultySections.Length;i++) {
            var stage=manager.difficultySections[i];if(stage==null)continue;
            float duration=stage.durationSeconds>0 ? stage.durationSeconds : manager.gameDuration*60/manager.difficultySections.Length;
            string encounter=stage.enemiesPerWave==0 ? "Ride" : stage.enemiesPerWave+" enemies / wave";
            EditorGUILayout.LabelField(FormatTime(start)+" – "+FormatTime(start+duration),stage.name+" · "+encounter);
            start+=duration;
        }
        if(Application.isPlaying) {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Playing",manager.currentDifficulty!=null ? manager.currentDifficulty.name : "None");
            EditorGUILayout.LabelField("Section remaining",manager.SectionRemaining.ToString("0.0")+" s");
            EditorGUILayout.LabelField("Run time",FormatTime(manager.RunElapsed));
        }
        EditorGUILayout.HelpBox("Open a referenced difficulty prefab to tune its duration, traffic and enemy wave. New traffic uses the new settings as rows are generated. Existing enemies finish naturally during riding breaks.",MessageType.Info);
    }
    private static string FormatTime(float seconds)=>Mathf.FloorToInt(seconds/60)+":"+Mathf.FloorToInt(seconds%60).ToString("00");
}
