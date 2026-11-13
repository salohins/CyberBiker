using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnemyRoadWaypointChecks {
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run(){
        Directory.CreateDirectory("Logs/EnemyPursuit");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var ai=new GameObject("Original planner").AddComponent<AIController>();typeof(AIController).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ai,null);
        ai.dodgeAngleLimit=45;typeof(AIController).GetField("collisionRadius",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ai,1f);
        var owner=new GameObject("Rider");owner.tag="Enemy";owner.transform.position=Vector3.up*1000;var root=new DodgePoint(owner.transform.position,owner);
        var anchors=new List<GameObject>();string report="";
        try {
            for(int i=0;i<12;i++){var anchor=new GameObject("Existing car point");anchor.transform.position=new Vector3((i%3-1)*8,1000,10+i*5);anchors.Add(anchor);ai.dodgePoints.Add(new DodgePoint(anchor.transform.position,anchor));}
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);anchors.Add(blocker);blocker.layer=LayerMask.NameToLayer("WorldObject");blocker.transform.position=new Vector3(0,1000,24);blocker.transform.localScale=Vector3.one*3;Physics.SyncTransforms();
            for(int i=0;i<60;i++){
                var origin=owner.transform.position+Vector3.right*(i%5-2);ai.dodgeAngleLimit=i%3==0 ? 0 : i%3==1 ? 45 : 90;
                var expected=new List<DodgePoint>();foreach(var candidate in ai.dodgePoints){var p=candidate.GetPosition();if(p.z>origin.z && Mathf.Abs(Vector3.Angle(p-origin,Vector3.forward))<ai.dodgeAngleLimit && !Physics.CheckCapsule(origin,p,1,LayerMask.GetMask("WorldObject")))expected.Add(candidate);}
                UnityEngine.Random.InitState(753+i);var selection=expected.Count==0 ? null : expected[UnityEngine.Random.Range(0,expected.Count)];UnityEngine.Random.InitState(753+i);root.Reset(origin,owner);
                Require(ai.GetPath(root)==selection,"Original random path choice changed at "+i);
            }
            report+="PASS 60 original path decisions match unchanged forward/angle/capsule/random rules.\n";
            ai.dodgeAngleLimit=45;ai.dodgePoints.Clear();root.Reset(owner.transform.position,owner);blocker.SetActive(false);
            var far=new GameObject("Distant car point");anchors.Add(far);far.transform.position=new Vector3(0,1000,200);ai.dodgePoints.Add(new DodgePoint(far.transform.position,far));
            for(int lane=0;lane<5;lane++){
                var road=new DodgePoint(new Vector3((lane-2)*6,1000,40),owner);
                Require(ai.GetPath(root,road)==road,"An empty road must preserve rider preference for lane "+lane);
            }
            Require(ai.dodgePoints.Count==1,"Rider-local road waypoints must not pollute shared car points");report+="PASS five separate empty-road lane points selected ahead of the same distant car point; no shared-list growth.\n";
            far.transform.position=new Vector3(6,1000,30);var near=ai.dodgePoints[0];var preferred=new DodgePoint(new Vector3(-12,1000,40),owner);
            Require(ai.GetPath(root,preferred)==near,"Near car-generated detour must take priority over clear preferred-lane point");
            report+="PASS nearby original car detour has priority over lane preference.\n";
            ai.dodgeAngleLimit=15;far.transform.position=new Vector3(-6,1000,80);
            var deadEnd=new DodgePoint(new Vector3(6,1000,50),owner);
            Require(ai.GetPath(root,deadEnd)==near,"A road point must preserve an onward connection within the original 15-degree turn limit");
            far.transform.position=new Vector3(20,1000,10);
            Require(!ai.CanEnterTraffic(root.GetPosition()),"Spawn point with no legal traffic connection must be rejected");
            Require(ai.GetPath(root,new DodgePoint(new Vector3(0,1000,5),owner))==null,"An unreachable traffic row must not be hidden by a short road point");
            Require(ai.CanEnterTraffic(new Vector3(20,1000,0)),"A lane aligned to a legal generated point must remain spawnable");
            report+="PASS turn-angle dead-end rejected; spawn entry uses the original reachable traffic network.\n";
            ai.dodgeAngleLimit=45;
            ai.dodgePoints.Clear();blocker.SetActive(true);blocker.transform.position=new Vector3(0,1000,20);Physics.SyncTransforms();var blocked=new DodgePoint(new Vector3(0,1000,40),owner);
            Require(ai.GetPath(root,blocked)==null,"A new road point must never bypass the original obstacle test");
            blocker.transform.position=Vector3.right*100+Vector3.up*1000;Physics.SyncTransforms();Require(ai.GetPath(root,blocked)==blocked,"A cleared road point must become reachable");
            report+="PASS blocked road point rejected; selected once the obstruction clears.\n";
            File.WriteAllText("Logs/EnemyPursuit/WaypointRules.txt",report);Debug.Log(report);
        }finally{foreach(var anchor in anchors)UnityEngine.Object.DestroyImmediate(anchor);UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(ai.gameObject);}
    }
}
