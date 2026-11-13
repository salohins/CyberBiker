using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AI;
using CyberBiker.Audio;
using CyberBiker.Combat;
using CyberBiker.Pooling;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PoolLifecycleChecks {
    private const string Pending="CyberBiker.PoolLifecycleChecks";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static PlayerController player;
    private static GameplayManager manager;
    private static GameObject bulletPrefab,timedBullet;
    private static GameObject ownerA,ownerB,flash;
    private static MuzzleFlash muzzle;
    private static int stage,flashFrame,sceneCount;
    private static float stageTime;
    private static double pauseTime,started;
    private static string report="";
    private static bool finishing;
    private static AsyncOperation unloading;
    private static Scene home;
    private static WorldGenerator streamingWorld;
    private static TrafficSpawner streamingTraffic;
    private static GameObject previousFirstTile;
    private static int streamedChunks,reuseBeforeStreaming;
    private static double streamingDeadline;
    static PoolLifecycleChecks(){if(SessionState.GetBool(Pending,false))EditorApplication.delayCall+=Attach;}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Level/Scenes/Level.unity");SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    private static void Attach(){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
    public static void BeginInCurrentScene(){SessionState.SetBool(Pending,true);Attach();}
    private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message+"\n"+stack);}
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Next(){stage++;stageTime=Time.time;}
    private static void Tick(){if(!EditorApplication.isPlaying || Time.timeSinceLevelLoad<.7f)return;try{
        Require(EditorApplication.timeSinceStartup-started<70,"Pool lifecycle checks timed out at stage "+stage);
        if(stage==0){
            player=UnityEngine.Object.FindAnyObjectByType<PlayerController>();manager=UnityEngine.Object.FindAnyObjectByType<GameplayManager>();home=SceneManager.GetActiveScene();
            foreach(var spawner in UnityEngine.Object.FindObjectsByType<TrafficSpawner>(FindObjectsSortMode.None))spawner.enabled=false;
            foreach(var car in UnityEngine.Object.FindObjectsByType<Car>(FindObjectsSortMode.None))car.gameObject.SetActive(false);
            foreach(var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))enemy.gameObject.SetActive(false);
            player.enabled=false;player.GetComponent<Rigidbody>().isKinematic=true;foreach(var shape in player.GetComponentsInChildren<Collider>())shape.enabled=false;
            var touch=player.GetComponent<TouchInputManager>();touch.SetAim(false);touch.enabled=false;manager.SwitchGameState(GameState.Playing);
            bulletPrefab=(GameObject)typeof(PlayerController).GetField("bullet",Private).GetValue(player);muzzle=UnityEngine.Object.FindAnyObjectByType<MuzzleFlash>();
            ownerA=Owner("Owner A",60);ownerB=Owner("Owner B",12);
            CheckProjectiles();CheckCars();CheckTile();CheckOverlap();CheckCapacity();
            timedBullet=PrefabPool.Rent(bulletPrefab,new Vector3(0,3000,0),Quaternion.identity,null);timedBullet.GetComponent<Bullet>().Initialize(ownerA,7);Next();
        }else if(stage==1 && Time.time-stageTime>2){
            PrefabPool.Release(timedBullet);var previous=timedBullet;timedBullet=PrefabPool.Rent(bulletPrefab,new Vector3(0,3000,0),Quaternion.identity,null);timedBullet.GetComponent<Bullet>().Initialize(ownerB,3);
            Require(timedBullet==previous,"Lifetime check must reuse the same projectile");Next();
        }else if(stage==2 && Time.time-stageTime>3.2f){
            Require(timedBullet!=null && timedBullet.activeInHierarchy,"An old projectile lease must not destroy the reused shot at its old expiry");
            manager.SwitchGameState(GameState.Pause);pauseTime=EditorApplication.timeSinceStartup;Next();
        }else if(stage==3 && EditorApplication.timeSinceStartup-pauseTime>.3){
            Require(timedBullet.activeInHierarchy,"Pause must preserve a pooled projectile's lifetime");manager.SwitchGameState(GameState.Playing);Next();
        }else if(stage==4 && Time.time-stageTime>2.2f){
            Require(timedBullet!=null && !timedBullet.activeInHierarchy && !timedBullet.GetComponent<PooledInstance>().IsRented,"A reused projectile must retire after its own scaled five-second lifetime");
            report+="PASS projectile lifetime: reused shot survives old lease expiry, pauses correctly and returns after its own lifetime.\n";
            muzzle.Spawn();flash=muzzle.GetComponentInChildren<PooledInstance>().gameObject;flashFrame=Time.frameCount;Next();
        }else if(stage==5){
            int age=Time.frameCount-flashFrame;if(age<3)Require(flash.activeInHierarchy,"Muzzle flash must remain visible for its three-frame window");
            if(age>=4){Require(!flash.activeInHierarchy,"Muzzle flash must retire after its three-frame window");muzzle.Spawn();Require(muzzle.GetComponentInChildren<PooledInstance>().gameObject==flash,"Next muzzle flash must reuse the retired instance");report+="PASS muzzle flash: original three-frame lifetime, reused geometry and no active-demand cap.\n";
                var scopes=(IDictionary)typeof(PrefabPool).GetField("scopes",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);sceneCount=scopes.Count;
                var temporary=SceneManager.CreateScene("Pool cleanup test");SceneManager.SetActiveScene(temporary);var shot=PrefabPool.Rent(bulletPrefab,new Vector3(0,3000,0),Quaternion.identity,null);PrefabPool.Release(shot);Require(scopes.Count==sceneCount+1,"Each scene must own its cache");SceneManager.SetActiveScene(home);unloading=SceneManager.UnloadSceneAsync(temporary);Next();}
        }else if(stage==6 && unloading.isDone){
            var scopes=(IDictionary)typeof(PrefabPool).GetField("scopes",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);Require(scopes.Count==sceneCount,"Unloading a scene must dispose its pool");report+="PASS scene-owned cache cleanup; original scene's pool remains available.\n";
            streamingWorld=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();streamingTraffic=UnityEngine.Object.FindAnyObjectByType<TrafficSpawner>();streamingTraffic.enabled=true;reuseBeforeStreaming=PrefabPool.ReusedCount;AdvanceWorld();Next();
        }else if(stage==7){
            var tiles=(GameObject[])typeof(WorldGenerator).GetField("_activeTiles",Private).GetValue(streamingWorld);
            Require(EditorApplication.timeSinceStartup<streamingDeadline,"World/traffic buffers failed to refill at chunk "+streamedChunks+"; firstChanged="+(tiles[0]!=previousFirstTile)+"; playerZ="+player.transform.position.z+"; tiles="+tiles[0].transform.position.z+".."+tiles[tiles.Length-1].transform.position.z+"; buffered="+streamingTraffic.BufferedWaveCount+"/"+streamingTraffic.TargetWaveCount+"; furthest="+streamingTraffic.FurthestCarDistance);
            if(tiles[0]==previousFirstTile || tiles[tiles.Length-1]==tiles[tiles.Length-2])return;
            for(int i=1;i<tiles.Length;i++){Require(tiles[i].GetComponent<Tile>().CanFollow(tiles[i-1].GetComponent<Tile>()),"Recycled tiles must retain compatible route sockets");Require(Vector3.Distance(tiles[i].transform.position,tiles[i-1].GetComponent<Tile>().ExitSocket.position)<.001f,"Recycled tiles must join without gaps or offsets");}
            streamedChunks++;
            if(streamedChunks<24)AdvanceWorld();else{streamingDeadline=EditorApplication.timeSinceStartup+4;Next();}
        }else if(stage==8){
            // As in normal travel, advance past the initial distant spawn zone
            // before requiring the full traffic horizon to fit the loaded world.
            Require(EditorApplication.timeSinceStartup<streamingDeadline,"Traffic failed to refill after24 recycled chunks: "+streamingTraffic.BufferedWaveCount+"/"+streamingTraffic.TargetWaveCount);
            if(streamingTraffic.BufferedWaveCount!=streamingTraffic.TargetWaveCount)return;
            Require(PrefabPool.ReusedCount>reuseBeforeStreaming,"Continuous world/traffic generation must actually reuse instances");Require(streamingTraffic.FurthestCarDistance>0,"Traffic must remain ahead of the rider after recycling");report+="PASS continuous streaming:24 chunks with compatible, gap-free joins; full traffic buffer after advancing; reuse delta="+(PrefabPool.ReusedCount-reuseBeforeStreaming)+".\n";Finish(true,report);
        }
    }catch(Exception error){Finish(false,error+"\n"+report);}}
    private static GameObject Owner(string name,float speed){var go=new GameObject(name,typeof(BoxCollider),typeof(Rigidbody));go.transform.position=new Vector3(name=="Owner A" ? 0 : 100,2000,0);var body=go.GetComponent<Rigidbody>();body.useGravity=false;body.linearVelocity=Vector3.forward*speed;return go;}
    private static void AdvanceWorld(){var tiles=(GameObject[])typeof(WorldGenerator).GetField("_activeTiles",Private).GetValue(streamingWorld);previousFirstTile=tiles[0];var position=player.transform.position;position.z=tiles[1].transform.position.z+11;player.transform.position=position;player.GetComponent<Rigidbody>().position=position;streamingDeadline=EditorApplication.timeSinceStartup+4;}
    private static GameObject CarPrefab(){var world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();return (GameObject)new SerializedObject(world).FindProperty("cars").GetArrayElementAtIndex(0).objectReferenceValue;}
    private static void CheckProjectiles(){
        var shot=PrefabPool.Rent(bulletPrefab,new Vector3(0,2000,0),Quaternion.identity,null);var bullet=shot.GetComponent<Bullet>();bullet.Initialize(ownerA,7);
        float baseSpeed=bulletPrefab.GetComponent<Bullet>().bulletSpeed;Require(Mathf.Approximately(bullet.bulletSpeed,baseSpeed+60),"Shot must inherit its current owner's velocity");
        var shape=shot.GetComponent<Collider>();Require(Physics.GetIgnoreCollision(shape,ownerA.GetComponent<Collider>()),"Shot must ignore its owner");
        var target=PrefabPool.Rent(CarPrefab(),new Vector3(0,2000,5),Quaternion.identity,null);var car=target.GetComponent<Car>();float hp=car.CurrentHealth;
        typeof(Bullet).GetMethod("Hit",Private).Invoke(bullet,new object[]{target.GetComponent<Collider>()});float after=car.CurrentHealth;typeof(Bullet).GetMethod("Hit",Private).Invoke(bullet,new object[]{target.GetComponent<Collider>()});
        Require(Mathf.Approximately(after,Mathf.Max(0,hp-7)) && car.CurrentHealth==after,"A pooled projectile must deal its configured damage exactly once");PrefabPool.Release(target);
        var reused=PrefabPool.Rent(bulletPrefab,new Vector3(0,2000,0),Quaternion.identity,null);Require(reused==shot,"Projectile must reuse the same object");bullet=reused.GetComponent<Bullet>();bullet.Initialize(ownerB,3);
        Require(Mathf.Approximately(bullet.bulletSpeed,baseSpeed+12) && new SerializedObject(bullet).FindProperty("damage").floatValue==3,"Reused projectile must reset inherited speed and damage");
        Require(!Physics.GetIgnoreCollision(shape,ownerA.GetComponent<Collider>()) && Physics.GetIgnoreCollision(shape,ownerB.GetComponent<Collider>()),"Reused projectile must clear old collision ignores and apply the new owner");
        PrefabPool.Release(reused);int returns=PrefabPool.ReturnedCount;PrefabPool.Release(reused);Require(PrefabPool.ReturnedCount==returns,"Double return must be harmless");
        var variant=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bullet Variant.prefab");var tracer=PrefabPool.Rent(variant,new Vector3(0,2000,100),Quaternion.identity,null);var trail=tracer.GetComponentInChildren<TrailRenderer>();trail.AddPosition(new Vector3(0,2000,100));trail.AddPosition(new Vector3(0,2000,102));PrefabPool.Release(tracer);var tracerAgain=PrefabPool.Rent(variant,new Vector3(100,2000,200),Quaternion.identity,null);
        Require(tracerAgain==tracer && trail!=null && trail.positionCount==0 && !trail.autodestruct,"Reused tracer must retain its child renderer and clear old trail points");PrefabPool.Release(tracerAgain);
        report+="PASS projectiles: reuse, correct damage once, new-owner velocity/damage/collision ignores, harmless double return and clean tracer trails.\n";
    }
    private static void CheckCars(){
        var prefab=CarPrefab();var go=PrefabPool.Rent(prefab,new Vector3(0,2000,20),Quaternion.identity,null);var car=go.GetComponent<Car>();var point=new DodgePoint(go.transform.position+Vector3.forward*3,go);Require(point.IsValid,"New car waypoint must be valid");
        var positions=(Dictionary<Car,Vector3>)typeof(GameAudio).GetField("relativePositions",Private).GetValue(GameAudio.Active);positions[car]=Vector3.forward;
        var anchors=(Transform[])typeof(GameAudio).GetField("voiceAnchors",Private).GetValue(GameAudio.Active);anchors[0]=car.transform;
        car.TakeDamage(new DamageInfo(999));car.speed=0;car.whichWall=-1;car.enabled=false;PrefabPool.Release(go);
        Require(!point.IsValid && point.GetPosition()==Vector3.zero,"Retiring a car must invalidate its old waypoint");Require(!positions.ContainsKey(car) && anchors[0]==null,"Retiring a car must clear crossing history and detach audio tails");
        var reused=PrefabPool.Rent(prefab,new Vector3(0,2000,200),Quaternion.identity,null);Require(reused==go && !point.IsValid,"Recycling a car must not revive an old waypoint");
        Require(car.IsAlive && car.CurrentHealth==prefab.GetComponent<Car>().CurrentHealth && car.speed==prefab.GetComponent<Car>().speed && car.whichWall==prefab.GetComponent<Car>().whichWall,"Car reuse must reset health, speed and wall selection");
        Require(car.enabled && car.GetComponent<Collider>().enabled==prefab.GetComponent<Collider>().enabled && car.GetComponent<Renderer>().enabled==prefab.GetComponent<Renderer>().enabled,"Car reuse must restore movement, collider and renderer state");
        foreach(var particle in reused.GetComponentsInChildren<ParticleSystem>())Require(!particle.gameObject.activeInHierarchy,"Old car explosion must not appear on a new lease");
        PrefabPool.Release(reused);report+="PASS cars: health/movement/collider/renderer reset, hidden old explosion, lease-safe waypoints and cleared sound crossing/anchor history.\n";
    }
    private static void CheckTile(){
        var source=new GameObject("Tile reset fixture",typeof(Tile));var pickup=GameObject.CreatePrimitive(PrimitiveType.Cube);pickup.name="Pickup";pickup.transform.SetParent(source.transform,false);pickup.transform.localPosition=new Vector3(2,1,3);pickup.AddComponent<Collectible>();source.SetActive(false);
        var tile=PrefabPool.Rent(source,new Vector3(100,2000,0),Quaternion.identity,null);var child=tile.transform.GetChild(0);typeof(Pickup).GetField("consumed",Private).SetValue(child.GetComponent<Collectible>(),true);child.localPosition=Vector3.one*7;child.localScale=Vector3.one*4;child.localRotation=Quaternion.Euler(60,30,20);child.gameObject.SetActive(false);PrefabPool.Release(tile);var again=PrefabPool.Rent(source,new Vector3(100,2000,0),Quaternion.identity,null);
        Require(again==tile && child.gameObject.activeSelf && child.localPosition==new Vector3(2,1,3) && child.localScale==Vector3.one && Quaternion.Angle(child.localRotation,Quaternion.identity)<.01f,"Tile reuse must restore collected children and their original transforms");Require(!(bool)typeof(Pickup).GetField("consumed",Private).GetValue(child.GetComponent<Collectible>()),"Tile reuse must reset the collectible's consumed flag");PrefabPool.Release(again);UnityEngine.Object.DestroyImmediate(source);
        var world=UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();var prefab=(GameObject)typeof(WorldGenerator).GetField("startTile",Private).GetValue(world);var legacy=UnityEngine.Object.Instantiate(prefab,new Vector3(500,2000,0),Quaternion.identity);
        foreach(var node in legacy.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
        byte[] original=RenderTile(legacy,"tile-legacy");UnityEngine.Object.DestroyImmediate(legacy);
        var road=PrefabPool.Rent(prefab,new Vector3(500,2000,0),Quaternion.identity,null);
        foreach(var node in road.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
        byte[] first=RenderTile(road,"tile-fresh");int initialChanged=0;for(int i=0;i<first.Length;i++)if(Mathf.Abs(first[i]-original[i])>2)initialChanged++;
        Require(initialChanged<first.Length*.001f,"Pooled fresh tile must render identically to the original Instantiate path");PrefabPool.Release(road);var roadAgain=PrefabPool.Rent(prefab,new Vector3(500,2000,0),Quaternion.identity,null);Require(roadAgain.layer==prefab.layer,"Tile reuse must restore original layers");foreach(var node in roadAgain.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;byte[] second=RenderTile(roadAgain,"tile-reused");int changed=0,lit=0;for(int i=0;i<first.Length;i++){if(first[i]>8)lit++;if(Mathf.Abs(first[i]-second[i])>2)changed++;}
        Require(lit>100 && changed<first.Length*.001f,"Fresh and reused road tiles must render the same visible geometry/materials");PrefabPool.Release(roadAgain);report+="PASS tiles: original child transforms/activation, collectible reset and fresh/reused road render comparison (changed bytes="+changed+").\n";
    }
    private static byte[] RenderTile(GameObject road,string name){
        var renderers=road.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        var go=new GameObject("Tile comparison camera");var camera=go.AddComponent<Camera>();camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.orthographicSize=bounds.extents.magnitude*.7f;camera.farClipPlane=2000;camera.transform.position=bounds.center+new Vector3(50,90,-90);camera.transform.LookAt(bounds.center);
        var rt=new RenderTexture(320,200,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(320,200,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,320,200),0,0);texture.Apply();Directory.CreateDirectory("Logs/Pooling");File.WriteAllBytes("Logs/Pooling/"+name+".png",texture.EncodeToPNG());var bytes=texture.GetRawTextureData<byte>().ToArray();RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);return bytes;
    }
    private static void CheckOverlap(){
        var floor=new GameObject("Overlap floor",typeof(BoxCollider),typeof(Tile));floor.layer=LayerMask.NameToLayer("Water");floor.transform.position=new Vector3(0,999,300);floor.GetComponent<BoxCollider>().size=new Vector3(50,1,200);var tile=floor.GetComponent<Tile>();tile.carLines=new Transform[5];for(int i=0;i<5;i++){var line=new GameObject("Line");line.transform.SetParent(floor.transform,false);line.transform.localPosition=Vector3.right*(i-2)*3.5f;tile.carLines[i]=line.transform;}
        var obstacle=new GameObject("Overlap car",typeof(BoxCollider),typeof(Obstacle));obstacle.layer=LayerMask.NameToLayer("WorldObject");obstacle.transform.position=new Vector3(0,1000,300);var box=obstacle.GetComponent<BoxCollider>();box.size=new Vector3(2,2,8);var logic=obstacle.GetComponent<Obstacle>();logic.line=2;var blockers=new List<GameObject>();
        for(int amount=0;amount<4;amount++){
            if(amount>0){var b=new GameObject("Overlap blocker",typeof(BoxCollider));b.layer=obstacle.layer;b.transform.position=new Vector3(-3.5f,1000,296);blockers.Add(b);}Physics.SyncTransforms();var expected=new List<Vector3>();
            for(int i=-1;i<2;i+=2)for(int j=-1;j<2;j++){if(j==0)continue;var p=obstacle.transform.position+box.center;p.x=tile.carLines[2+j].position.x;p+=Vector3.forward*(box.bounds.size.z/2)*i;var hits=Physics.OverlapBox(p,new Vector3(1,10,1),obstacle.transform.rotation,LayerMask.GetMask("WorldObject"));if(hits.Length==0 || (hits.Length==1 && hits[0].gameObject==obstacle))expected.Add(p);}
            var actual=logic.GetDodgePoints(8);Require(actual.Count==expected.Count,"NonAlloc overlap count must match the original, including saturated buffers");for(int i=0;i<actual.Count;i++)Require(actual[i].GetPosition()==expected[i],"NonAlloc overlap points must match the original");
        }
        foreach(var b in blockers)UnityEngine.Object.DestroyImmediate(b);UnityEngine.Object.DestroyImmediate(obstacle);UnityEngine.Object.DestroyImmediate(floor);report+="PASS overlap physics: identical pass points for empty, single-blocker and saturated multi-blocker queries.\n";
    }
    private static void CheckCapacity(){var all=new List<GameObject>();for(int i=0;i<100;i++)all.Add(PrefabPool.Rent(bulletPrefab,new Vector3(i*2,3000,1000),Quaternion.identity,null));foreach(var go in all)Require(go.activeInHierarchy,"Active projectile demand must never be capped");foreach(var go in all)PrefabPool.Release(go);int retained=0;foreach(var member in UnityEngine.Object.FindObjectsByType<PooledInstance>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(member.Prefab==bulletPrefab && member.IsCached)retained++;Require(retained<=64,"Inactive projectile cache must remain bounded");report+="PASS capacity:100 concurrent shots allowed, bounded inactive cache.\n";}
    private static void Finish(bool ok,string text){if(finishing)return;finishing=true;SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Directory.CreateDirectory("Logs/Pooling");File.WriteAllText("Logs/Pooling/lifecycle-result.txt",text);EditorApplication.Exit(ok ? 0 : 1);}
}
