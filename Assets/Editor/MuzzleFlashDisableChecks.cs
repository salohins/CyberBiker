using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using CyberBiker.Pooling;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MuzzleFlashDisableChecks {
    private const string Key="CyberBiker.MuzzleDisableChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static GameObject owner,prefab,flash,newFlash,stale;
    private static MuzzleFlash muzzle;
    private static int stage,frame,spawnFrame,cycles,lastFrame=-1;
    private static double started;
    private static bool finishing;
    private static AsyncOperation unload;
    private static string report="";
    static MuzzleFlashDisableChecks(){if(SessionState.GetBool(Key,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){Directory.CreateDirectory("Logs/MuzzleDisable");EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    private static void Attach(){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Next(){stage++;frame=Time.frameCount;}
    private static MuzzleFlash Fixture(string name){
        var root=new GameObject(name);root.transform.position=Vector3.up*2000;
        var go=new GameObject("MuzzleFlashPosition");go.transform.SetParent(root.transform,false);
        var result=go.AddComponent<MuzzleFlash>();typeof(MuzzleFlash).GetField("muzzleFlash",Private).SetValue(result,prefab);return result;
    }
    private static GameObject Spawn(){muzzle.Spawn();var live=(List<(GameObject instance,int expires)>)typeof(MuzzleFlash).GetField("flashes",Private).GetValue(muzzle);return live[live.Count-1].instance;}
    private static void Tick(){if(finishing || !EditorApplication.isPlaying || Time.timeSinceLevelLoad<.5f)return;try{
        Require(EditorApplication.timeSinceStartup-started<60,"Muzzle disable test timed out at "+stage);
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(stage==0){
            foreach(var director in UnityEngine.Object.FindObjectsByType<EnemyEncounterDirector>(FindObjectsSortMode.None))director.enabled=false;
            foreach(var spawner in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))spawner.enabled=false;
            foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
            foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))car.gameObject.SetActive(false);
            var player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();player.enabled=false;player.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeAll;
            var source=player.GetComponentInChildren<MuzzleFlash>(true);prefab=(GameObject)typeof(MuzzleFlash).GetField("muzzleFlash",Private).GetValue(source);
            UnityEngine.Object.FindAnyObjectByType<GameplayManager>().SwitchGameState(GameState.Playing);
            SaveMissingScripts();muzzle=Fixture("Muzzle ancestor test");owner=muzzle.transform.parent.gameObject;
            flash=Spawn();owner.SetActive(false);Next();
        }else if(stage==1 && Time.frameCount>frame){
            Require(flash!=null && !flash.activeInHierarchy && flash.GetComponent<PooledInstance>().IsCached,"Ancestor disable must safely return the child flash");
            owner.SetActive(true);Require(Spawn()==flash,"Disabled-owner flash must remain reusable");muzzle.enabled=false;Next();
        }else if(stage==2 && Time.frameCount>frame){
            Require(flash.GetComponent<PooledInstance>().IsCached,"Disabling only the component must return the flash");muzzle.enabled=true;flash=Spawn();
            muzzle.gameObject.SetActive(false);muzzle.gameObject.SetActive(true);newFlash=Spawn();spawnFrame=Time.frameCount;Next();
        }else if(stage==3 && Time.frameCount>frame){
            Require(!flash.GetComponent<PooledInstance>().IsRented && newFlash.GetComponent<PooledInstance>().IsRented,"Rapid aim off/on must retire the old flash without retiring the new one");
            stale=PrefabPool.Rent(prefab,Vector3.up*2000,Quaternion.identity);
            typeof(PrefabPool).GetMethod("ReleaseAfterHierarchyChange").Invoke(null,new object[]{stale});
            PrefabPool.Release(stale);var newer=PrefabPool.Rent(prefab,Vector3.up*2000,Quaternion.identity);Require(newer==stale,"Stale lease fixture must reuse the same flash");Next();
        }else if(stage==4 && Time.frameCount>frame){
            Require(stale.GetComponent<PooledInstance>().IsRented,"An old deferred request must not return a newer lease");PrefabPool.Release(stale);Next();
        }else if(stage==5){
            int age=Time.frameCount-spawnFrame;if(age<3)Require(newFlash.activeInHierarchy,"Keep the normal three-frame flash lifetime");
            if(age<4)return;Require(newFlash.GetComponent<PooledInstance>().IsCached,"Normal flash expiry must still reuse the cache");
            report+="PASS ancestor/component disable, immediate aim off/on, newer lease protection, original three-frame expiry and pooled reuse.\n";Next();
        }else if(stage==6 && Time.frameCount>frame){
            if(cycles++<40){Spawn();owner.SetActive(false);owner.SetActive(true);Spawn();frame=Time.frameCount;return;}
            report+="PASS 40 repeated aim off/on cycles with the actual Toon muzzle-flash prefab and no hierarchy errors.\n";
            var home=SceneManager.GetActiveScene();var temporary=SceneManager.CreateScene("Muzzle unload fixture");SceneManager.SetActiveScene(temporary);
            var other=Fixture("Muzzle pending unload");other.Spawn();other.transform.parent.gameObject.SetActive(false);SceneManager.SetActiveScene(home);unload=SceneManager.UnloadSceneAsync(temporary);Next();
        }else if(stage==7 && unload.isDone){
            report+="PASS scene unload with a pending muzzle-flash return.\n";Finish(true,report);
        }
    }catch(Exception error){Finish(false,error+"\n"+report);}}
    private static void SaveMissingScripts(){
        string missing="";
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())foreach(var node in root.GetComponentsInChildren<Transform>(true)){
            int count=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
            if(count>0)missing+=node.name+" | missing="+count+" | root="+root.name+"\n";
        }
        File.WriteAllText("Logs/MuzzleDisable/MissingScripts.txt",missing.Length>0 ? missing : "No missing-script components found on loaded scene objects.\n");
    }
    private static void Finish(bool ok,string message){if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;File.WriteAllText("Logs/MuzzleDisable/Result.txt",message);EditorApplication.delayCall+=()=>EditorApplication.Exit(ok ? 0 : 1);}
}
