using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class RuntimeSwordProbe : MonoBehaviour {
    [Serializable] public class Bone { public string path; public Vector3 position,scale; public Quaternion rotation; }
    [Serializable] public class Frame { public int clip; public float time; public Vector3[] positions,scales; public Quaternion[] rotations; }
    public AnimationClip[] clips;
    public Bone[] bones;
    public Frame[] frames;
    public static Transform[] MakeRig(Bone[] source, out GameObject root) {
        root = new GameObject("Sword pose sampler"); root.SetActive(false);
        var paths = new Dictionary<string,Transform> { { "", root.transform } };
        var result = new Transform[source.Length];
        for (int i=0;i<source.Length;i++) {
            var b=source[i]; int slash=b.path.LastIndexOf('/');
            string parent=slash<0 ? "" : b.path.Substring(0,slash);
            var node=new GameObject(slash<0 ? b.path : b.path.Substring(slash+1)).transform;
            node.SetParent(paths[parent],false); node.localPosition=b.position; node.localRotation=b.rotation; node.localScale=b.scale;
            paths.Add(b.path,node);result[i]=node;
        }
        return result;
    }
    private void Start() {
        string output=Path.Combine(Application.persistentDataPath,"sword-player-probe.txt");
        var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-probeOutput");if(flag>=0 && flag+1<args.Length)output=args[flag+1];
        try {
            var rig=MakeRig(bones,out var root);float worstPosition=0,worstRotation=0,worstScale=0;int failures=0;
            var detail=new System.Text.StringBuilder();
            var clipFailures=new int[clips.Length];
            foreach(var frame in frames) {
                clips[frame.clip].SampleAnimation(root,frame.time);
                for(int i=0;i<rig.Length;i++) {
                    float position=Vector3.Distance(rig[i].localPosition,frame.positions[i]);
                    float rotation=Quaternion.Angle(rig[i].localRotation,frame.rotations[i]);
                    float scale=Vector3.Distance(rig[i].localScale,frame.scales[i]);
                    worstPosition=Mathf.Max(worstPosition,position);worstRotation=Mathf.Max(worstRotation,rotation);worstScale=Mathf.Max(worstScale,scale);
                    if(position>.001f || rotation>.1f || scale>.001f) {
                        failures++;clipFailures[frame.clip]++;if(failures<=20)detail.AppendLine(clips[frame.clip].name+" time="+frame.time+" bone="+bones[i].path+" position="+position+" rotation="+rotation+" scale="+scale);
                    }
                }
            }
            for(int c=0;c<clips.Length;c++)detail.AppendLine(clips[c].name+": mismatches="+clipFailures[c]);
            File.WriteAllText(output,(failures==0 ? "PASS" : "FAIL")+" runtime AnimationClip.SampleAnimation versus Editor: "+frames.Length+" frames, "+rig.Length+" transforms, mismatches="+failures+", max position="+worstPosition+", angle="+worstRotation+", scale="+worstScale+"\n"+detail);
            Application.Quit(failures==0 ? 0 : 1);
        } catch(Exception error) { File.WriteAllText(output,error.ToString());Application.Quit(2); }
    }
}
