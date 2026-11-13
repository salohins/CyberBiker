using System;
using System.IO;
using System.Reflection;
using CyberBiker.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnemyTrafficChecks {
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly Type Guard=typeof(Enemy).Assembly.GetType("EnemyTrafficAvoidance");
    private static readonly MethodInfo Clip=Guard.GetMethod("Constrain"),Shape=Guard.GetMethod("Shape",Private);
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run() {
        Directory.CreateDirectory("Logs/EnemyRiding");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/enemy 1.prefab"));
        var car=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Cars/Car_01.prefab")).GetComponent<Car>();
        // Edit-mode fixture: provide health without starting the scene's gameplay.
        typeof(Car).GetField("box",Private).SetValue(car,car.GetComponent<BoxCollider>());
        typeof(Car).GetField("vitality",Private).SetValue(car,new Health(100));
        var registry=(System.Collections.Generic.List<Car>)typeof(Car).GetField("activeCars",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        if(!registry.Contains(car))registry.Add(car); // Awake runs only in Play mode.
        var guard=Activator.CreateInstance(Guard,new object[]{enemy});string report="";
        try {
            foreach(var scenario in new[]{"rear approach","fast side cut","diagonal cut","moving traffic","stalled traffic","rotated car"}) {
                enemy.transform.rotation=Quaternion.identity;car.transform.rotation=scenario=="rotated car" ? Quaternion.Euler(0,25,0) : Quaternion.identity;
                car.transform.position=new Vector3(0,1000,scenario=="fast side cut" ? 0 : 20);car.enabled=scenario=="moving traffic" || scenario=="stalled traffic";car.speed=35;
                enemy.transform.position=new Vector3(scenario=="fast side cut" ? -14 : scenario=="diagonal cut" ? -7 : 0,1000,0);
                var desired=scenario=="fast side cut" ? new Vector3(220,0,0) : scenario=="diagonal cut" ? new Vector3(55,0,200) : new Vector3(0,0,220);
                const float dt=.1f;float clipped=0;
                for(int tick=0;tick<24;tick++) {
                    Physics.SyncTransforms();var start=enemy.transform.position;car.TryGetTrafficShape(dt,out var bounds,out var carStep);
                    if(scenario=="stalled traffic")carStep=Vector3.zero;
                    var bike=(Bounds)Shape.Invoke(guard,null);
                    var end=(Vector3)Clip.Invoke(guard,new object[]{start+desired*dt,dt});clipped+=Vector3.Distance(start+desired*dt,end);
                    // Compare the swept navigation sphere with the car's real
                    // oriented box, independently of SphereCast's result.
                    var box=car.GetComponent<BoxCollider>();var shape=box.transform;
                    for(int sample=0;sample<=20;sample++) {
                        float t=sample/20f;var point=bike.center+(end-start)*t;
                        var local=shape.InverseTransformPoint(point-carStep*t)-box.center;
                        var half=box.size*.5f;
                        var closest=new Vector3(Mathf.Clamp(local.x,-half.x,half.x),Mathf.Clamp(local.y,-half.y,half.y),Mathf.Clamp(local.z,-half.z,half.z));
                        var surface=shape.TransformPoint(closest+box.center)+carStep*t;
                        Require(Vector3.SqrMagnitude(point-surface)>=Mathf.Pow(bike.extents.x-.015f,2),scenario+" penetrated the car during tick "+tick+" at "+t);
                    }
                    enemy.transform.position=end;car.transform.position+=carStep;
                }
                Require(clipped>1,scenario+" must reproduce a blocked path and actually constrain it");
                report+="PASS "+scenario+": planner-radius contact volume stays outside the real car at 10Hz, including intermediate positions.\n";
            }
            car.enabled=false;car.transform.SetPositionAndRotation(new Vector3(0,1000,20),Quaternion.identity);enemy.transform.SetPositionAndRotation(new Vector3(0,1000,0),Quaternion.identity);
            car.GetComponent<BoxCollider>().enabled=false;
            var free=enemy.transform.position+Vector3.forward*40;Physics.SyncTransforms();
            Require((Vector3)Clip.Invoke(guard,new object[]{free,.1f})==free,"Destroyed/disabled traffic must no longer block enemies");
            car.GetComponent<BoxCollider>().enabled=true;car.transform.position=new Vector3(6,1000,20);Physics.SyncTransforms();
            Require((Vector3)Clip.Invoke(guard,new object[]{free,.1f})==free,"A clear adjacent lane must retain its original speed");
            report+="PASS adjacent clear lane unchanged; disabled/dead-car collider ignored. No predictive braking layer; the original route controls travel.\n";
            File.WriteAllText("Logs/EnemyRiding/Traffic.txt",report);Debug.Log(report);
        } finally {UnityEngine.Object.DestroyImmediate(enemy);UnityEngine.Object.DestroyImmediate(car.gameObject);}
    }
}
