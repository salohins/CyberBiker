using System;
using System.IO;
using System.Reflection;
using CyberBiker.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CollisionParticleChecks {
    private const string Pending = "CyberBiker.CollisionParticleChecks";
    private static bool attached;
    static CollisionParticleChecks() {
        if (SessionState.GetBool(Pending,false)) EditorApplication.delayCall += Attach;
    }
    public static void Run() {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(Pending,true);
        EditorApplication.isPlaying = true;
    }
    private static void Attach() {
        if (attached) return;
        attached = true;
        EditorApplication.update += Check;
    }
    private static void Require(bool condition,string message) { if (!condition) throw new Exception(message); }
    private static CollisionScrapeParticles CreateProbe(out Rigidbody body) {
        var bike = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bike.name = "Collision test bike";
        bike.transform.position = new Vector3(0,1,0);
        bike.transform.localScale = new Vector3(.9f,.6f,2);
        body = bike.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        var particles = bike.AddComponent<CollisionScrapeParticles>();
        var settings = new SerializedObject(particles);
        settings.FindProperty("sparkMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Player/CollisionScratch.mat");
        settings.ApplyModifiedPropertiesWithoutUndo();
        return particles;
    }
    private static GameObject Surface(string tag,int layer=0) {
        var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = tag+" contact probe";
        surface.tag = tag;
        surface.layer = layer;
        surface.transform.position = new Vector3(1,1,10);
        surface.transform.localScale = new Vector3(1,3,80);
        return surface;
    }
    private static void Step(Rigidbody body,Vector3 velocity,int steps) {
        for (int i=0;i<steps;i++) {
            body.linearVelocity = velocity;
            Physics.SyncTransforms();
            Physics.Simulate(.02f);
            // Manual physics simulation dispatches contacts, but not MonoBehaviour.FixedUpdate.
            foreach(var effect in body.GetComponents<CollisionScrapeParticles>())
                typeof(CollisionScrapeParticles).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(effect,null);
            if (Time.timeScale > 0)
                foreach(var system in body.GetComponentsInChildren<ParticleSystem>()) system.Simulate(.02f,false,false,true);
        }
    }
    private static void Check() {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.update -= Check;
        var previousSimulation = Physics.simulationMode;
        try {
            Physics.simulationMode = SimulationMode.Script;
            Time.timeScale = 1;
            foreach (string path in new[] {"Assets/Art/Models/Player_Last.prefab","Assets/Level/Prefabs/Player_Last.prefab"}) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var fx = prefab.GetComponent<CollisionScrapeParticles>();
                Require(fx!=null && new SerializedObject(fx).FindProperty("sparkMaterial").objectReferenceValue!=null,
                    "Both player prefabs must bind collision particles and their spark material");
                Require(prefab.GetComponent<BikeImpactResponse>()!=null,"Both player prefabs must include physical car impact response");
            }

            var wall = Surface("Wall",LayerMask.NameToLayer("WorldObject"));
            var wallCameraObject=new GameObject("Barrier impact camera test");
            wallCameraObject.tag="MainCamera";wallCameraObject.AddComponent<Camera>();
            var wallCamera=wallCameraObject.AddComponent<CameraController>();wallCamera.enabled=false;
            var wallMesh=wall.AddComponent<MeshCollider>();
            wallMesh.sharedMesh=wall.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(wall.GetComponent<BoxCollider>());
            var effects = CreateProbe(out var body);
            var state = UnityEngine.Random.state;
            Step(body,new Vector3(8,0,40),12);
            Require(effects.ImpactCount>0 && effects.EmittedCount>0,"Real wall contact must produce an impact burst");
            Require(effects.ScrapeEmittedCount>0,"Sliding along the wall must continue the scratch spray");
            Require(wallCamera.CarImpactCount>0,"Barrier impact must trigger the same directional camera feedback as cars");
            int wallCameraHits=wallCamera.CarImpactCount;
            for (int i=0;i<12;i++) {
                int before=effects.EmittedCount;
                Step(body,new Vector3(8,0,40),1);
                Require(effects.EmittedCount-before>=5,"Sliding contact must produce a dense stream on every physics step");
            }
            Require(Mathf.Abs(effects.LastContactPoint.x-.5f)<.1f && effects.LastContactNormal.x<-.8f,
                "Wall sparks must start at the touching side and point out of the barrier");
            Require(state.Equals(UnityEngine.Random.state),"Collision particles must preserve gameplay randomness");
            Require(wallCamera.CarImpactCount==wallCameraHits,"Continuous wall scraping must not restart camera recoil every step");
            Capture(body,effects);
            var hitState=new GameObject("Hit-state contact check");
            hitState.SetActive(false);
            var gameManager=hitState.AddComponent<Gameplay.GameplayManager>();
            var gameSettings=new SerializedObject(gameManager);
            gameSettings.FindProperty("initialState").intValue=(int)Gameplay.GameState.Hit;
            gameSettings.ApplyModifiedPropertiesWithoutUndo();
            typeof(CollisionScrapeParticles).GetField("manager",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(effects,gameManager);
            int beforeHit=effects.EmittedCount;
            Step(body,new Vector3(8,0,40),3);
            Require(effects.EmittedCount>beforeHit,"Entering the game's Hit state must not cut off a continuing collision spray");
            UnityEngine.Object.DestroyImmediate(hitState);
            int emitted = effects.EmittedCount;
            Time.timeScale = 0;
            Step(body,new Vector3(8,0,40),5);
            Require(effects.EmittedCount==emitted,"Paused contact must not emit scrape sparks");
            Time.timeScale = 1;
            Step(body,new Vector3(8,0,40),3);
            Require(effects.EmittedCount>emitted,"Sliding sparks must resume after pause");
            Step(body,new Vector3(-12,0,40),8);
            emitted = effects.EmittedCount;
            Step(body,new Vector3(-12,0,40),5);
            Require(effects.EmittedCount==emitted,"Scrape emission must end when contact is lost");
            UnityEngine.Object.DestroyImmediate(body.gameObject);
            UnityEngine.Object.DestroyImmediate(wall);
            UnityEngine.Object.DestroyImmediate(wallCameraObject);

            var car = Surface("Obstacle");
            var impactCameraObject=new GameObject("Car impact camera test");
            impactCameraObject.tag="MainCamera";impactCameraObject.AddComponent<Camera>();
            var impactCamera=impactCameraObject.AddComponent<CameraController>();impactCamera.enabled=false;
            effects = CreateProbe(out body);
            Step(body,new Vector3(8,0,35),4);
            Require(effects.ImpactCount>0,"Car contact must produce collision sparks");
            Require(impactCamera.CarImpactCount>0,"Real car contact must trigger directional camera feedback");
            int cameraHits=impactCamera.CarImpactCount;
            car.GetComponent<Collider>().enabled=false;
            for (int i=0;i<8;i++) {
                int before=effects.EmittedCount;
                Step(body,new Vector3(0,0,35),1);
                Require(effects.EmittedCount-before>=5,
                    "A car disabling its collision collider must not interrupt the scrape while still touching");
            }
            for (int i=0;i<5;i++) {
                int before=effects.EmittedCount;
                Step(body,Vector3.zero,1);
                Require(effects.EmittedCount-before>=5,"Slowing down during an established collision must not cut the stream");
            }
            Step(body,new Vector3(-12,0,35),8);
            int separated=effects.EmittedCount;
            Step(body,new Vector3(-12,0,35),4);
            Require(effects.EmittedCount==separated,"Disabled car geometry must stop emitting as soon as the bike separates");
            Require(impactCamera.CarImpactCount==cameraHits,"Continuous scraping must not restart the impact camera jolt every frame");
            UnityEngine.Object.DestroyImmediate(body.gameObject);
            UnityEngine.Object.DestroyImmediate(car);
            UnityEngine.Object.DestroyImmediate(impactCameraObject);

            wall = Surface("Wall");
            effects = CreateProbe(out body);
            body.position = new Vector3(.08f,1,0);
            Step(body,Vector3.zero,4);
            Require(effects.EmittedCount==0,"Resting against a surface must not create sparks");
            body.position = new Vector3(-1,1,0);
            Step(body,new Vector3(0,0,35),8);
            Require(effects.EmittedCount==0,"A close pass without contact must not create scratches");
            UnityEngine.Object.DestroyImmediate(body.gameObject);
            UnityEngine.Object.DestroyImmediate(wall);

            var floor = Surface("Untagged",LayerMask.NameToLayer("Ground"));
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(50,.25f,100);
            effects = CreateProbe(out body);
            body.position = new Vector3(0,.45f,0);
            Step(body,new Vector3(0,-5,35),5);
            Require(effects.EmittedCount==0,"Ordinary road contact must not create metal scratch particles");
            UnityEngine.Object.DestroyImmediate(body.gameObject);
            UnityEngine.Object.DestroyImmediate(floor);
            CheckActualBody();
            CheckBikeKick();
            Finish(true,"PASS: player bindings, wall and car scrape continuity, body/handlebar contact rendering, impact camera trigger, pause and separation, near miss and ground rejection, independent randomness; physical car kick direction, strength scaling and cap, mass independence, one-shot cooldown, frontal recoil, motor recovery with steering, pause rejection and Inspector tuning.");
        } catch(Exception error) { Finish(false,error.ToString()); }
        finally { Time.timeScale=1;Physics.simulationMode=previousSimulation; }
    }
    private static void CheckActualBody() {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Player_Last.prefab");
        var bike=(GameObject)PrefabUtility.InstantiatePrefab(source);
        // Isolate the actual production geometry and scrape component from scene UI,
        // crash recovery and weapons, while exercising real compound-body contacts.
        foreach(var behaviour in bike.GetComponentsInChildren<MonoBehaviour>(true))
            if(!(behaviour is CollisionScrapeParticles)) UnityEngine.Object.DestroyImmediate(behaviour);
        foreach(var animator in bike.GetComponentsInChildren<Animator>()) animator.enabled=false;
        var body=bike.GetComponent<Rigidbody>();
        body.position=Vector3.zero;body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeRotation;
        var wall=Surface("Wall",LayerMask.NameToLayer("WorldObject"));
        wall.transform.position=new Vector3(2,1.5f,10);
        wall.transform.localScale=new Vector3(.5f,4,80);
        var effects=bike.GetComponent<CollisionScrapeParticles>();
        Step(body,new Vector3(8,0,40),40);
        Require(effects.ImpactCount>0 && effects.ScrapeEmittedCount>0,"The actual motorcycle must scrape the wall using its body colliders");
        Require(effects.LastContactPoint.y>2 && effects.LastContactPoint.y>bike.GetComponent<BoxCollider>().bounds.max.y+1,
            "A handlebar-first collision must emit at the upper body, not at the low wheel hitbox");
        Directory.CreateDirectory("Logs/Tire");
        File.WriteAllText("Logs/Tire/body-contact.txt",$"contact={effects.LastContactPoint:F4}; local={bike.transform.InverseTransformPoint(effects.LastContactPoint):F4}; root collider max Y={bike.GetComponent<BoxCollider>().bounds.max.y:F4}");
        Capture(body,effects);
        UnityEngine.Object.DestroyImmediate(bike);
        UnityEngine.Object.DestroyImmediate(wall);
    }
    private static BikeImpactResponse KickProbe(float speed,bool frontal,string tag,float mass,float kickLimit,out Rigidbody body,out GameObject surface,int layer=0) {
        surface=Surface(tag,layer);
        if(frontal) {surface.transform.position=new Vector3(0,1,3);surface.transform.localScale=new Vector3(3,3,1);}
        CreateProbe(out body);body.mass=mass;
        var response=body.gameObject.AddComponent<BikeImpactResponse>();
        var settings=new SerializedObject(response);
        settings.FindProperty("maximumKickSpeed").floatValue=kickLimit;settings.ApplyModifiedPropertiesWithoutUndo();
        Vector3 velocity=frontal ? new Vector3(0,0,speed) : new Vector3(speed,0,20);
        for(int i=0;i<8 && response.KickCount==0;i++) Step(body,velocity,1);
        Physics.Simulate(.02f); // observe the impulse over another step without resetting velocity
        return response;
    }
    private static void CheckBikeKick() {
        var light=KickProbe(2,false,"Obstacle",1,5,out var body,out var surface);
        Require(light.KickCount==1 && body.linearVelocity.x<-.5f && light.LastKickVelocity.x<0,
            "Side contact with a car must physically push the bike away");
        float lightStrength=light.LastKickVelocity.magnitude;
        Require(Mathf.Abs(body.linearVelocity.y)<.001f && Mathf.Approximately(light.MotorAccelerationMultiplier,1),
            "A side swipe must keep the bike grounded and preserve forward acceleration");
        Require(light.SteeringAccelerationMultiplier(1)<1 && light.SteeringAccelerationMultiplier(-1)==1,
            "Held steering into the car must not erase recoil, while steering away stays fully responsive");
        Require(light.PresentationSteering(1,.02f)<0,"Recoil presentation must follow actual sideways motion opposite the input");
        var poseRecovery=typeof(BikeImpactResponse).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        for(int i=0;i<10;i++) poseRecovery.Invoke(light,null);
        Require(light.SteeringAccelerationMultiplier(1)==1 && light.SteeringAccelerationMultiplier(-1)==1,
            "Full steering must return within 0.2 seconds instead of extending resistance after the kick");
        for(int i=0;i<15;i++) poseRecovery.Invoke(light,null);
        Require(light.PresentationSteering(1,.02f)<0,"Visual recoil must continue while the bike still slides opposite the input after motor recovery");
        var leanSettings=new SerializedObject(light);
        leanSettings.FindProperty("leanAnimationSpeed").floatValue=12;
        leanSettings.ApplyModifiedPropertiesWithoutUndo();
        var originalVelocity=body.linearVelocity;body.linearVelocity=new Vector3(4,0,20);
        float visual=light.PresentationSteering(1,.02f);
        Require(visual>0 && visual<.4f && light.PresentationResponse==12,
            "Kick reversal must use the Inspector kick animation speed and a restrained lean target");
        for(int i=0;i<60;i++) visual=light.PresentationSteering(1,.02f);
        Require(visual==1 && light.PresentationResponse==BikePresentation.DefaultPoseResponse,"Normal steering presentation must resume smoothly after recoil movement resolves");
        Require(light.PresentationResponse==3,"Tuning kick animation speed must leave ordinary steering lean at its original speed");
        body.linearVelocity=originalVelocity;
        for(int i=0;i<6;i++) Step(body,new Vector3(8,0,20),1);
        Require(light.KickCount==1,"Compound contact and ongoing rubbing must not repeatedly kick the bike");
        UnityEngine.Object.DestroyImmediate(body.gameObject);UnityEngine.Object.DestroyImmediate(surface);

        var hard=KickProbe(25,false,"Obstacle",5,5,out body,out surface);
        Require(hard.KickCount==1 && hard.LastKickVelocity.magnitude>lightStrength && hard.LastKickVelocity.magnitude<=5.001f,
            "Harder impacts must produce stronger, capped kicks");
        Require(body.linearVelocity.x<-4.5f,"Physical kick must work independently of rigidbody mass");
        UnityEngine.Object.DestroyImmediate(body.gameObject);UnityEngine.Object.DestroyImmediate(surface);

        var front=KickProbe(35,true,"Obstacle",1,5,out body,out surface);
        Require(front.KickCount==1 && body.linearVelocity.z<-.5f && front.LastKickVelocity.z<0,
            "A frontal car impact must visibly kick the bike backward");
        Require(front.MotorAccelerationMultiplier==0,"A frontal kick must briefly ease the motor so acceleration does not erase it");
        var recovery=typeof(BikeImpactResponse).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        Time.timeScale=0;recovery.Invoke(front,null);
        Require(front.MotorAccelerationMultiplier==0,"Pausing must freeze physical impact recovery");
        Time.timeScale=1;
        var motor=new BikeMotor(body,300,50,20);
        motor.Tick(.5f,true,1,75,front.MotorAccelerationMultiplier);Physics.Simulate(.02f);
        Require(body.linearVelocity.z<-.1f && body.linearVelocity.x>.1f,
            "Motor recovery and the wall speed guard must preserve recoil while steering remains responsive");
        for(int i=0;i<20;i++) recovery.Invoke(front,null);
        Require(front.MotorAccelerationMultiplier==1,"Normal engine acceleration must fully recover after the brief kick");
        UnityEngine.Object.DestroyImmediate(body.gameObject);UnityEngine.Object.DestroyImmediate(surface);

        var tuned=KickProbe(25,false,"Obstacle",1,2,out body,out surface);
        Require(tuned.KickCount==1 && tuned.LastKickVelocity.magnitude<=2.001f,"Inspector kick strength must tune real impact impulses");
        UnityEngine.Object.DestroyImmediate(body.gameObject);UnityEngine.Object.DestroyImmediate(surface);
        var wall=KickProbe(8,false,"Wall",1,5,out body,out surface);
        Require(wall.KickCount==1 && body.linearVelocity.x<-.5f,"Barrier contact must push the bike away just like car contact");
        UnityEngine.Object.DestroyImmediate(body.gameObject);UnityEngine.Object.DestroyImmediate(surface);
        var otherWall=KickProbe(8,false,"Untagged",1,5,out body,out surface,LayerMask.NameToLayer("WorldObject"));
        Require(otherWall.KickCount==1 && body.linearVelocity.x<-.5f,"Other solid WorldObject walls must receive physical impact feedback");
        UnityEngine.Object.DestroyImmediate(body.gameObject);UnityEngine.Object.DestroyImmediate(surface);
        Time.timeScale=0;
        var paused=KickProbe(8,false,"Obstacle",1,5,out body,out surface);
        Require(paused.KickCount==0,"Paused car contacts must not apply physical impulses");
        Time.timeScale=1;
        UnityEngine.Object.DestroyImmediate(body.gameObject);UnityEngine.Object.DestroyImmediate(surface);
    }
    private static void Capture(Rigidbody body,CollisionScrapeParticles effects) {
        if (SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera = new GameObject("Scrape review camera").AddComponent<Camera>();
        camera.transform.position = body.position+new Vector3(-5,2,-7);
        camera.transform.LookAt(effects.LastContactPoint-Vector3.forward*.5f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f,.035f,.055f);
        var light = new GameObject("Review light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(40,-35,0);
        var texture = new RenderTexture(960,640,24);
        camera.targetTexture = texture;camera.Render();
        var previous = RenderTexture.active;RenderTexture.active=texture;
        var image = new Texture2D(960,640,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();
        Directory.CreateDirectory("Logs/Tire");
        File.WriteAllBytes("Logs/Tire/collision-scrape.png",image.EncodeToPNG());
        RenderTexture.active=previous;
        UnityEngine.Object.DestroyImmediate(image);
        camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);
    }
    private static void Finish(bool passed,string message) {
        SessionState.SetBool(Pending,false);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/collision-particle-check.txt",message);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
